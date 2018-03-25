using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Threading;
using System.Windows;
using System.Xml;
using KCDModMerger.Annotations;
using KCDModMerger.Properties;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace KCDModMerger
{
    internal class ModManager : INotifyPropertyChanged
    {
        private const string VERSION = "1.0";
        internal readonly List<string> ModNames = new List<string>();
        internal readonly List<Mod> Mods = new List<Mod>();
        internal readonly List<ModFile> ModFiles = new List<ModFile>();
        internal readonly List<string> Conflicts = new List<string>();
        private const string TEMP_FILES = "\\TempFiles";
        private const string TEMP_MERGED_DIR = "\\MergedFiles";
        private const string MODMANAGER_DIR = "\\ModManager";

        public ModManager()
        {
            CreateModdingDirectory();
            Settings.Default.PropertyChanged += SettingsChanged;

            if (Settings.Default.KCDPath != "") UpdateModList();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RunKDiff3(string args)
        {
            Process process = new Process();
            process.StartInfo.FileName =
                Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(ModManager)).Location) +
                "\\Tools\\KDiff3\\kdiff3.exe";
            process.StartInfo.Arguments = args;
            process.Start();
            process.WaitForExit();
        }

        public string CreateModdingDirectory()
        {
            if (Directory.Exists(Settings.Default.KCDPath + MODMANAGER_DIR))
            {
                try
                {
                    Directory.Delete(Settings.Default.KCDPath + MODMANAGER_DIR, true);
                }
                catch (IOException e)
                {
                    var result =
                        MessageBox.Show("Could not clean ModManager Directory! Do you have it open in Explorer?");

                    if (result == MessageBoxResult.OK)
                    {
                        // Try again after 10 Seconds
                        Thread.Sleep(10000);
                        Directory.Delete(Settings.Default.KCDPath + MODMANAGER_DIR, true);
                    }
                }
            }

            var dir = Directory.CreateDirectory(Settings.Default.KCDPath + MODMANAGER_DIR);
            var subDir = dir.CreateSubdirectory(TEMP_FILES.Replace("\\", ""));

            var mergedDir = dir.CreateSubdirectory(TEMP_MERGED_DIR.Replace("\\", ""));
            mergedDir.CreateSubdirectory("Localization");
            mergedDir.CreateSubdirectory("Data");

            var vanillaDir = subDir.CreateSubdirectory("Vanilla");
            vanillaDir.CreateSubdirectory("Localization");
            vanillaDir.CreateSubdirectory("Data");

            return dir.FullName;
        }

        public void CopyMergedToMods()
        {
            var rootDir = Settings.Default.KCDPath + "\\Mods\\ModMerger";
            if (Directory.Exists(rootDir))
            {
                try
                {
                    Directory.Delete(rootDir, true);
                }
                catch (IOException e)
                {
                    var result =
                        MessageBox.Show("Could not clean ModManager Directory! Do you have it open in Explorer?");

                    if (result == MessageBoxResult.OK)
                    {
                        // Try again after 10 Seconds
                        Thread.Sleep(10000);
                        Directory.Delete(rootDir, true);
                    }
                }
            }

            var dir = Directory.CreateDirectory(rootDir);

            using (XmlWriter xml = XmlWriter.Create(rootDir + "\\mod.manifest"))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("kcd_mod");
                xml.WriteStartElement("info");
                xml.WriteElementString("name", "MMM - ModMerger Merged Mods");
                xml.WriteElementString("description", "Merged Mods by KCDModMerger");
                xml.WriteElementString("author", "Mortimer");
                xml.WriteElementString("version", VERSION);
                xml.WriteElementString("created_on", DateTime.Now.ToString());
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndDocument();
            }

            CopyFilesRecursively(
                Directory.CreateDirectory(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged"),
                dir);
        }

        private void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        public string MergeFiles(string baseFile, string overwriteFile)
        {
            // Replacing the path to the Data/Localization should reveal either one as the first path
            var cleanPath = overwriteFile.Replace(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_FILES, "");
            var temp = cleanPath.Split('\\').ToList();
            temp.RemoveAt(0);
            temp.RemoveAt(0);
            cleanPath = string.Join("\\", temp);

            // This may happen if the Vanilla File does not exist (e.g. Cheat Mod)
            if (baseFile == "")
            {
                // First is EmptyString, second is ModName, third is Data or Localization
                if (cleanPath.StartsWith("Data"))
                {
                    var destFolder = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Data";

                    var dir = Directory.CreateDirectory(destFolder);
                    string[] subDirs = null;
                    var fileName = cleanPath.Replace("Data", "");
                    if (cleanPath.Contains("/"))
                    {
                        subDirs = cleanPath.Replace("Data", "").Split('/');
                        fileName = subDirs.Last();

                        var list = subDirs.ToList();
                        list.RemoveAt(subDirs.Length - 1);
                        subDirs = list.ToArray();

                        foreach (string subDir in subDirs)
                        {
                            dir = dir.CreateSubdirectory(subDir.Replace("\\", ""));
                        }
                    }

                    var destFile = destFolder + "\\" +
                                   (subDirs != null ? string.Join("\\", subDirs) : "") + "\\" + fileName;

                    File.Copy(overwriteFile, destFile);

                    return destFile;
                }
                else
                {
                    // We'll use the overwriteFile here since this will always be there
                    var parts = cleanPath.Split('\\');

                    // IF in Localization the pak file should be the second path
                    var languagePak = parts[1];
                    var destFolder = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR +
                                     "\\Localization\\" + languagePak;
                    Directory.CreateDirectory(destFolder);

                    File.Copy(overwriteFile, destFolder + "\\" + parts.Last());

                    return destFolder + "\\" + parts.Last();
                }
            }

            //Is in the Data directory. Simply merge all files into a giant pak
            if (cleanPath.StartsWith("Data"))
            {
                var destFolder = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Data";

                var dir = Directory.CreateDirectory(destFolder);
                string[] subDirs = null;
                var fileName = cleanPath.Replace("Data", "");
                if (cleanPath.Contains("/"))
                {
                    subDirs = cleanPath.Replace("Data", "").Split('/');
                    fileName = subDirs.Last();

                    var list = subDirs.ToList();
                    list.RemoveAt(subDirs.Length - 1);
                    subDirs = list.ToArray();

                    foreach (string subDir in subDirs)
                    {
                        dir = dir.CreateSubdirectory(subDir.Replace("\\", ""));
                    }
                }

                var destFile = destFolder + "\\" +
                               (subDirs != null ? string.Join("\\", subDirs) : "") + "\\" + fileName;

                RunKDiff3("\"" + baseFile + "\" \"" + overwriteFile + "\" -o \"" + destFile + "\" --auto");

                return destFile;
            }
            // Is in Localization. Need to merge a bit differently honoring the different languages
            else
            {
                // We'll use the overwriteFile here since this will always be there
                var parts = cleanPath.Split('\\');

                // IF in Localization the pak file should be the second path
                var languagePak = parts[1];
                var destFolder = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR +
                                 "\\Localization\\" + languagePak;
                Directory.CreateDirectory(destFolder);
                RunKDiff3("\"" + baseFile + "\" \"" + overwriteFile + "\" -o \"" + destFolder + "\\" +
                          parts.Last() +
                          "\" --auto");

                return destFolder + "\\" + parts.Last();
            }
        }

        public string ExtractFile(ModFile file)
        {
            if (File.Exists(file.PakFile))
            {
                using (FileStream fs = new FileStream(file.PakFile, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs))
                    {
                        var fileName = file.FilePath.EndsWith("Localization")
                            ? file.FileName.Split('\\').Last()
                            : file.FileName;
                        var zippedFile = zip.Entries.Where(entry => entry.FullName == fileName).First();

                        if (zippedFile != null)
                        {
                            var destFolder = CreateDirectories(file, false);

                            using (FileStream destFile = File.Open(destFolder + "\\" + fileName, FileMode.OpenOrCreate))
                            {
                                using (Stream srcFile = zippedFile.Open())
                                {
                                    srcFile.CopyTo(destFile);
                                }
                            }

                            return destFolder + "\\" + fileName;
                        }
                    }
                }
            }

            return "";
        }

        public string ExtractVanillaFile(ModFile file)
        {
            if (file.FilePath.EndsWith("Localization"))
            {
                if (File.Exists(Settings.Default.KCDPath + "\\Localization\\" + file.PakFile.Split('\\').Last()))
                {
                    var fileName = file.FilePath.EndsWith("Localization")
                        ? file.FileName.Split('\\').Last()
                        : file.FileName;
                    using (FileStream fs =
                        new FileStream(Settings.Default.KCDPath + "\\Localization\\" + file.PakFile.Split('\\').Last(),
                            FileMode.Open))
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            var zippedFile = zip.Entries.Where(entry => entry.FullName == fileName).First();

                            if (zippedFile != null)
                            {
                                var destFolder = CreateDirectories(file, true);

                                using (FileStream destFile = File.Open(destFolder + "\\" + fileName,
                                    FileMode.OpenOrCreate))
                                {
                                    using (Stream srcFile = zippedFile.Open())
                                    {
                                        srcFile.CopyTo(destFile);
                                    }
                                }

                                return destFolder + "\\" + fileName;
                            }
                        }
                    }
                }
            }
            else
            {
                var pakFile = FindVanillaDataFile(file.FileName);

                if (pakFile != String.Empty)
                {
                    using (FileStream fs = new FileStream(pakFile, FileMode.Open))
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            var zippedFile = zip.Entries.Where(entry => entry.FullName == file.FileName).First();

                            if (zippedFile != null)
                            {
                                var destFolder = CreateDirectories(file, true);

                                using (FileStream destFile =
                                    File.Open(destFolder + "\\" + file.FileName, FileMode.OpenOrCreate))
                                {
                                    using (Stream srcFile = zippedFile.Open())
                                    {
                                        srcFile.CopyTo(destFile);
                                    }
                                }

                                return destFolder + "\\" + file.FileName;
                            }
                        }
                    }
                }
            }

            return "";
        }

        public void PakData()
        {
            var rootDir = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged";
            var dir = Directory.CreateDirectory(rootDir);
            dir.CreateSubdirectory("Data");

            ZipFile.CreateFromDirectory(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Data",
                rootDir + "\\Data\\data.pak");
        }

        public void PakLocalization()
        {
            var rootDir = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged";
            var dir = Directory.CreateDirectory(rootDir);
            dir.CreateSubdirectory("Localization");

            var languages =
                Directory.GetDirectories(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR +
                                         "\\Localization");

            foreach (string language in languages)
            {
                ZipFile.CreateFromDirectory(language,
                    Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged\\Localization\\" +
                    language.Split('\\').Last() + ".pak");
            }
        }

        private string CreateDirectories(ModFile file, bool vanilla)
        {
            var rootFolder = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_FILES + "\\" +
                             (vanilla ? "Vanilla" : file.ModName);
            var subdirectory = file.FilePath.Split('\\').Last();
            var localizationSubDirectry = (file.FilePath.EndsWith("Localization")
                ? "\\" + file.PakFile.Split('\\').Last().Replace(".pak", "")
                : "");
            var fileSpecificSubDirs = file.FileName.Contains("/")
                ? file.FileName.Split('/')
                : null;
            // Remove file name
            if (fileSpecificSubDirs != null)
            {
                var list = fileSpecificSubDirs.ToList();
                list.RemoveAt(fileSpecificSubDirs.Length - 1);
                fileSpecificSubDirs = list.ToArray();
            }

            var dir = Directory.CreateDirectory(rootFolder);
            dir.CreateSubdirectory("Localization");
            dir.CreateSubdirectory("Data");

            var subDir = dir.CreateSubdirectory(subdirectory);

            if (localizationSubDirectry != "")
            {
                subDir = subDir.CreateSubdirectory(localizationSubDirectry.Replace("\\", ""));
            }

            if (fileSpecificSubDirs != null)
            {
                foreach (string s in fileSpecificSubDirs)
                {
                    subDir = subDir.CreateSubdirectory(s);
                }
            }

            return rootFolder + "\\" + subdirectory + localizationSubDirectry;
        }

        private string FindVanillaDataFile(string file)
        {
            var saved = JsonConvert.DeserializeObject<Dictionary<string, string>>(Settings.Default.FilePaths);

            if (saved == null)
            {
                saved = new Dictionary<string, string>();
            }

            if (saved.ContainsKey(file))
            {
                if (File.Exists(saved[file]))
                {
                    return saved[file];
                }
            }

            var path = Settings.Default.KCDPath + "\\Data";
            var paks = Directory.GetFiles(path, "*.pak", SearchOption.AllDirectories);

            foreach (string pak in paks)
            {
                using (FileStream fs = File.Open(pak, FileMode.Open))
                {
                    try
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            if (zip.Entries.Count > 0)
                            {
                                var srcFile = zip.Entries.FirstOrDefault(entry => entry.FullName.Equals(file));

                                if (srcFile != null)
                                {
                                    saved[file] = pak;
                                    Settings.Default.FilePaths =
                                        JsonConvert.SerializeObject(saved, Formatting.Indented);
                                    Settings.Default.Save();

                                    return pak;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
#if TRACE
                        Trace.WriteLine(e);
#endif
                    }
                }
            }

            return "";
        }

        private void UpdateModList()
        {
            Mods.Clear();
            ModNames.Clear();
            Conflicts.Clear();
            ModFiles.Clear();
            var folders = GetFolders(Settings.Default.KCDPath + "\\Mods");

            foreach (var folder in folders)
            {
                Mods.Add(new Mod(folder));
            }

            List<string> tempFiles = new List<string>();

            foreach (var mod in Mods)
            {
                ModNames.Add(mod.DisplayName);

                foreach (ModFile modFile in mod.DataFiles)
                {
                    tempFiles.Add(modFile.FileName);
                }

                ModFiles.AddRange(mod.DataFiles);
            }

            var duplicates =
                new HashSet<string>(tempFiles.GroupBy(x => x).Where(x => x.Skip(1).Any()).Select(x => x.Key));
            Conflicts.AddRange(duplicates);

            OnPropertyChanged();
        }

        private string[] GetFolders(string path)
        {
            if (Directory.Exists(path)) return Directory.GetDirectories(path);

            return new List<string>().ToArray();
        }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "KCDPath") UpdateModList();
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}