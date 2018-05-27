#region usings

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using KCDModMerger.Properties;

#endregion

namespace KCDModMerger.Mods
{
    internal class ModMerger
    {
        private readonly string destPath;
        private readonly List<string> mergedFiles;
        private VanillaFileManager vanillaFileManager;

        /*
         if (dirName == "Data")
           {
           Logging.Logger.Log("Adding Startup Script");
           
           var scriptsDir = Directory.CreateDirectory(directory + "\\Scripts");
           scriptsDir = scriptsDir.CreateSubdirectory("Startup");
           
           using (FileStream fs = File.Open(scriptsDir.FullName + "\\KCDModMerger.lua", FileMode.Create))
           {
           using (StreamWriter file = new StreamWriter(fs))
           {
           file.Write("System.LogAlways(string.format(\"$5[INFO] Loaded KCDModMerger Merged Mods!\"))" /*+
           Environment.NewLine + "System.LogAlways(string.format(\"$5[INFO] Files: " +
           string.Join(", ", _mergedFiles) + "\"))"* /);
           }
           }
           
           Logging.Logger.Log("Added Startup Script!");
           }
         */

        internal ModMerger(string destPath, List<ModFile> filesToMerge, List<string> mergedFiles = null)
        {
            this.destPath = destPath;
            this.mergedFiles = mergedFiles ?? new List<string>();
            vanillaFileManager = new VanillaFileManager(ModManager.directoryManager.kcdFolder);

            MergeFiles(filesToMerge);

            var dataPak = ModManager.directoryManager.PakDirectory(destPath + "\\Data");
            var localDir = new DirectoryInfo(destPath + "\\Localization");
            var localPaks = new List<string>();

            foreach (DirectoryInfo directory in localDir.GetDirectories())
            {
                localPaks.Add(ModManager.directoryManager.PakDirectory(directory.FullName));
            }

            CopyPakkedData(dataPak);
            CopyPakkedLocal(localPaks);
            WriteManifest();
        }

        private void WriteManifest()
        {
            if (File.Exists(DirectoryManager.MERGED_MOD + "\\mod.manifest"))
            {
                Directory.Delete(DirectoryManager.MERGED_MOD + "\\mod.manifest");
            }

            using (XmlWriter xml = XmlWriter.Create(DirectoryManager.MERGED_MOD + "\\mod.manifest"))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("kcd_mod");
                xml.WriteStartElement("info");
                xml.WriteElementString("name", "MMM - ModMerger Merged Mods");
                xml.WriteElementString("description", "Merged Mods by KCDModMerger");
                xml.WriteElementString("author", "Mortimer");
                xml.WriteElementString("version", ModManager.VERSION);
                xml.WriteElementString("created_on", DateTime.Now.ToString());
                xml.WriteEndElement();

                if (mergedFiles.Count > 0)
                {
                    xml.WriteStartElement("merged_files");

                    foreach (string mergedFile in mergedFiles)
                    {
                        if (!string.IsNullOrEmpty(mergedFile))
                        {
                            xml.WriteElementString("file", mergedFile);
                        }
                    }

                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                xml.WriteEndDocument();
            }
        }

        private void CopyPakkedLocal(List<string> localPaks)
        {
            if (Directory.Exists(DirectoryManager.MERGED_MOD))
            {
                if (Directory.Exists(DirectoryManager.MERGED_MOD + "\\Localization"))
                {
                    var existingPaks = new DirectoryInfo(DirectoryManager.MERGED_MOD + "\\Localization").GetFiles()
                        .Select(e => e.FullName).ToList();

                    foreach (string localPak in localPaks)
                    {
                        var existingPak =
                            existingPaks.FirstOrDefault(e => e.Split('\\').Last() == localPak.Split('\\').Last());

                        if (!string.IsNullOrEmpty(existingPak))
                        {
                            MergePaks(existingPak, localPak);
                        }
                    }
                }
            }
        }

        private void CopyPakkedData(string dataPak)
        {
            if (Directory.Exists(DirectoryManager.MERGED_MOD))
            {
                if (Directory.Exists(DirectoryManager.MERGED_MOD + "\\Data"))
                {
                    var existingPaks = new DirectoryInfo(DirectoryManager.MERGED_MOD + "\\Data").GetFiles();

                    if (existingPaks.Length > 0)
                    {
                        var existingPak = existingPaks.First().FullName;

                        MergePaks(existingPak, dataPak);

                        return;
                    }
                }
            }

            File.Copy(dataPak, DirectoryManager.MERGED_MOD + "\\Data\\" + dataPak.Split('\\').Last());
        }

        /// <summary>
        /// Merges the paks.
        /// </summary>
        /// <param name="basePak">The base pak.</param>
        /// <param name="overwritePak">The overwrite pak.</param>
        private void MergePaks(string basePak, string overwritePak)
        {
            using (ZipArchive dataZip = ZipFile.Open(overwritePak, ZipArchiveMode.Read))
            {
                using (ZipArchive existingZip = ZipFile.Open(basePak, ZipArchiveMode.Update))
                {
                    foreach (ZipArchiveEntry entry in dataZip.Entries)
                    {
                        existingZip.GetEntry(entry.FullName)?.Delete();

                        var nextEntry = existingZip.CreateEntry(entry.FullName);

                        using (Stream s = nextEntry.Open())
                        {
                            using (Stream st = entry.Open())
                            {
                                st.CopyTo(s);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Merges the files.
        /// </summary>
        /// <param name="filesToMerge">The files to merge.</param>
        /// <returns></returns>
        private void MergeFiles(List<ModFile> filesToMerge)
        {
            Dictionary<string, ModFile> baseFiles = new Dictionary<string, ModFile>();

            foreach (ModFile file in filesToMerge)
            {
                mergedFiles.Add("(" + file.ModName + ") " + (file.IsLocalization ? file.PakFileName + "/" : "") +
                                file.FileName);

                if (!baseFiles.ContainsKey(file.FileName))
                {
                    var s = vanillaFileManager.ExtractVanillaFile(file);
                    baseFiles.Add(file.FileName,
                        new ModFile("Vanilla", file.FileName, file.PakFileName, s, true, file.IsLocalization, true));
                }

                if (file.IsLocalization)
                {
                    if (MergeLocalization(baseFiles[file.FileName], file, out string destFilePath))
                    {
                        baseFiles[file.FileName] = new ModFile("", file.FileName, "", destFilePath, false, true, true);
                    }
                    else
                    {
                        Logging.Logger.Log($"Merge failed for base {baseFiles[file.FileName]} and overwrite {file}!");
                    }
                }
                else
                {
                    if (MergeData(baseFiles[file.FileName], file, out string destFilePath))
                    {
                        baseFiles[file.FileName] = new ModFile("", file.FileName, "", destFilePath, false, false, true);
                    }
                    else
                    {
                        Logging.Logger.Log($"Merge failed for base {baseFiles[file.FileName]} and overwrite {file}!");
                    }
                }
            }
        }

        private bool MergeData(ModFile baseFile, ModFile overwriteFile, out string destFilePath)
        {
            var path = destPath + "\\Data";

            // File is under a subdirectory in the zip which we need to mimick on the file system
            if (baseFile.FileName.Contains("/"))
            {
                var parts = baseFile.FileName.Replace("/", "\\").Split('\\');

                // We need to remove the actual file name from the directory path
                var convertedParts = parts.ToList().Remove(parts[parts.Length - 1]);

                path += "\\" + string.Join("\\", convertedParts);
            }

            Directory.CreateDirectory(path);

            var file = ModManager.directoryManager.ExtractFile(overwriteFile);

            destFilePath = path + "\\" + overwriteFile.FileName;

            // No basefile, just copy overwriteFile
            if (baseFile.Equals(overwriteFile))
            {
                File.Copy(file, destFilePath);
            }
            else
            {
                // File already extracted
                if (baseFile.IsExtracted)
                {
                    Utilities.RunKDiff3("\"" + baseFile.PakFilePath + "\" \"" + file + "\" -o \"" + destFilePath +
                                        "\" --auto");
                }
                else
                {
                    var bFile = ModManager.directoryManager.ExtractFile(baseFile);
                    Utilities.RunKDiff3("\"" + bFile + "\" \"" + file + "\" -o \"" + destFilePath + "\" --auto");
                }
            }

            return true;
        }

        private bool MergeLocalization(ModFile baseFile, ModFile overwriteFile, out string destFilePath)
        {
            var path = destPath + "\\Localization\\" + overwriteFile.PakFileName.Replace(".pak", "");

            Directory.CreateDirectory(path);

            var file = ModManager.directoryManager.ExtractFile(overwriteFile);

            destFilePath = path + "\\" + overwriteFile.FileName;

            if (baseFile.Equals(overwriteFile))
            {
                File.Copy(file, destFilePath);
            }
            else
            {
                // File already extracted
                if (baseFile.IsExtracted)
                {
                    Utilities.RunKDiff3("\"" + baseFile.PakFilePath + "\" \"" + file + "\" -o \"" + destFilePath +
                                        "\" --auto");
                }
                else
                {
                    var bFile = ModManager.directoryManager.ExtractFile(baseFile);
                    Utilities.RunKDiff3("\"" + bFile + "\" \"" + file + "\" -o \"" + destFilePath + "\" --auto");
                }
            }

            return true;
        }
    }
}