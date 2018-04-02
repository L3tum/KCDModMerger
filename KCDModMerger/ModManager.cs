#region usings

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using KCDModMerger.Annotations;
using KCDModMerger.Mods;
using KCDModMerger.Properties;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

#endregion

namespace KCDModMerger
{
    public class ModManager : INotifyPropertyChanged
    {
        private const string VERSION = "1.4 'Ariadnes Threads'";
        private const string TEMP_FILES = "\\TempFiles";
        private const string TEMP_MERGED_DIR = "\\MergedFiles";
        private const string MODMANAGER_DIR = "\\ModMerger";
        private const string MERGED_MOD = "\\zzz_ModMerger";
        private readonly List<string> _mergedFiles = new List<string>();
        internal readonly ObservableCollection<string> Conflicts = new ObservableCollection<string>();
        internal readonly List<ModFile> ModFiles = new List<ModFile>();
        internal readonly ObservableCollection<string> ModNames = new ObservableCollection<string>();
        internal readonly List<Mod> Mods = new List<Mod>();
        private string OLD_ROOT_FOLDER = Settings.Default.KCDPath;

        private Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModManager"/> class.
        /// </summary>
        internal ModManager()
        {
            Settings.Default.PropertyChanged += SettingsChanged;

            var isValid = Update();

            if (!isValid)
            {
                Logger.Log("Saved KCD Root Folder is not the root folder of KCD!");
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Updates this instance.
        /// </summary>
        /// <returns></returns>
        internal bool Update()
        {
            var shouldUpdate = Settings.Default.KCDPath != "" &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Mods") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Data") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Bin") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Localization") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Engine");


            if (shouldUpdate)
            {
                var freeSpace = GetTotalFreeSpace(Settings.Default.KCDPath.Split('\\').First() + "\\");

                if (freeSpace != -1)
                {
                    Logger.Log("Free Space on Drive " + Settings.Default.KCDPath.Split(':').First() + " is " +
                               App.ConvertToHighest(freeSpace),
                        true);

                    if (freeSpace < (100 /*MB*/ * 1024f /*KB*/ * 1024f /*Bytes*/))
                    {
                        MessageBox.Show(
                            "There are only " + App.ConvertToHighest(freeSpace) + " left on this drive!",
                            "KCDModMerger",
                            MessageBoxButton.OK);
                    }
                }


                CreateModdingDirectory();
                UpdateModList();
            }
            else
            {
                Logger.Log("Clearing previously found Stuff because root folder is not valid...");
                ModNames.Clear();
                Mods.Clear();
                ModFiles.Clear();
                Conflicts.Clear();
                Logger.Log("Cleared previously found Stuff!");

                OnPropertyChanged();
            }

            return shouldUpdate;
        }

        /// <summary>
        /// Runs the kdiff3.
        /// </summary>
        /// <param name="args">The arguments.</param>
        internal void RunKDiff3(string args)
        {
            Logger.Log("Starting KDiff with args: " + args, true);
            Process process = new Process();
            process.StartInfo.FileName =
                Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) +
                "\\Tools\\KDiff3\\kdiff3.exe";
            process.StartInfo.Arguments = args;
            process.Start();
            process.WaitForExit();
            Logger.Log("Kdiff Finished!");
        }

        /// <summary>
        /// Creates the modding directory.
        /// </summary>
        /// <returns></returns>
        internal string CreateModdingDirectory()
        {
            if (OLD_ROOT_FOLDER != "")
            {
                Logger.Log("Deleting Old Folder");
                DeleteFolder(OLD_ROOT_FOLDER + MODMANAGER_DIR);
                Logger.Log("Deleted Old Folder!");
            }

            OLD_ROOT_FOLDER = Settings.Default.KCDPath;

            DeleteFolder(Settings.Default.KCDPath + MODMANAGER_DIR);

            Logger.Log("Creating " + MODMANAGER_DIR + " Directory and Subs");

            var dir = Directory.CreateDirectory(Settings.Default.KCDPath + MODMANAGER_DIR);

            Logger.Log("Created " + MODMANAGER_DIR + " Directory!");

            var subDir = dir.CreateSubdirectory(TEMP_FILES.Replace("\\", ""));

            Logger.Log("Created " + MODMANAGER_DIR + TEMP_FILES + " Subdirectory!");

            var mergedDir = dir.CreateSubdirectory(TEMP_MERGED_DIR.Replace("\\", ""));
            mergedDir.CreateSubdirectory("Localization");
            mergedDir.CreateSubdirectory("Data");

            Logger.Log("Created " + MODMANAGER_DIR + TEMP_MERGED_DIR + " Subdirectory!");

            var vanillaDir = subDir.CreateSubdirectory("Vanilla");
            vanillaDir.CreateSubdirectory("Localization");
            vanillaDir.CreateSubdirectory("Data");

            Logger.Log("Created " + MODMANAGER_DIR + TEMP_FILES + "\\Vanilla Subdirectory!");

            return dir.FullName;
        }

        /// <summary>
        /// Copies the merged files to mods.
        /// </summary>
        internal void CopyMergedToMods()
        {
            Logger.Log("Copying Files from Merged to " + MERGED_MOD);
            var rootDir = Settings.Default.KCDPath + "\\Mods" + MERGED_MOD;

            Logger.Log("Creating " + MERGED_MOD + " Mod");

            var dir = Directory.CreateDirectory(rootDir);

            Logger.Log("Created " + MERGED_MOD + " Mod!");

            Logger.Log("Writing Manifest");

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
                if (_mergedFiles.Count > 0)
                {
                    xml.WriteStartElement("merged_files");

                    foreach (string mergedFile in _mergedFiles)
                    {
                        if (mergedFile != "")
                        {
                            xml.WriteElementString("file", mergedFile);
                        }
                    }

                    xml.WriteEndElement();

                    lock (_mergedFiles)
                    {
                        _mergedFiles.Clear();
                    }
                }

                xml.WriteEndElement();
                xml.WriteEndDocument();
            }

            Logger.Log("Wrote Manifest!");

            Logger.Log("Copying Files to " + MERGED_MOD);

            CopyFilesRecursively(
                Directory.CreateDirectory(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged"),
                dir);

            Logger.Log("Copied Files to " + MERGED_MOD, true);
        }

        /// <summary>
        /// Copies the files recursively.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        private void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
            {
                if (File.Exists(Path.Combine(target.FullName, file.Name)))
                {
                    Logger.Log("File already exists!");
                    Logger.Log("Appending Entries from " + file.FullName + " to " +
                               Path.Combine(target.FullName, file.Name));
                    using (FileStream srcFile = File.OpenRead(file.FullName))
                    {
                        using (ZipArchive srcZip = new ZipArchive(srcFile))
                        {
                            using (var destZip = ZipFile.Open(Path.Combine(target.FullName, file.Name),
                                ZipArchiveMode.Update))
                            {
                                foreach (var srcEntry in srcZip.Entries)
                                {
                                    if (destZip.Entries.Any(entry => entry.FullName == srcEntry.FullName))
                                    {
                                        Logger.Log("Replacing " + srcEntry.FullName);
                                        destZip.Entries.First(entry => entry.FullName == srcEntry.FullName).Delete();
                                    }
                                    else
                                    {
                                        Logger.Log("Adding " + srcEntry.FullName);
                                    }

                                    var destEntry = destZip.CreateEntry(srcEntry.FullName);

                                    using (Stream destStream = destEntry.Open())
                                    {
                                        using (Stream srcStream = srcEntry.Open())
                                        {
                                            srcStream.CopyTo(destStream);
                                        }
                                    }

                                    Logger.Log("Added/Replaced " + srcEntry.FullName, true);
                                }
                            }
                        }
                    }

                    Logger.Log("Appended Entries from " + file.FullName + " to " +
                               Path.Combine(target.FullName, file.Name), true);
                }
                else
                {
                    Logger.Log("Copying " + file.FullName + " to " + Path.Combine(target.FullName, file.Name));
                    file.CopyTo(Path.Combine(target.FullName, file.Name));
                    Logger.Log("Copied " + file.FullName + " to " + Path.Combine(target.FullName, file.Name), true);
                }
            }
        }

        /// <summary>
        /// Merges the files.
        /// </summary>
        /// <param name="baseFile">The base file.</param>
        /// <param name="overwriteFile">The overwrite file.</param>
        /// <returns></returns>
        internal string MergeFiles(string baseFile, string overwriteFile)
        {
            Logger.Log("Merging " + baseFile + " and " + overwriteFile);
            // Replacing the path to the Data/Localization should reveal either one as the first path
            var cleanPath = overwriteFile.Replace(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_FILES, "");
            var temp = cleanPath.Split('\\').ToList();
            temp.RemoveAt(0);
            temp.RemoveAt(0);
            cleanPath = string.Join("\\", temp);

            _mergedFiles.Add(baseFile != "" && !baseFile.Contains(TEMP_MERGED_DIR)
                ? baseFile.Replace(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_FILES, "")
                : "");
            _mergedFiles.Add(overwriteFile.Replace(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_FILES, ""));

            // This may happen if the Vanilla File does not exist (e.g. Cheat Mod)
            if (baseFile == "")
            {
                // First is EmptyString, second is ModName, third is Data or Localization
                if (cleanPath.StartsWith("Data"))
                {
                    var destFolder = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Data";

                    Logger.Log("Creating Directory " + destFolder);
                    var dir = Directory.CreateDirectory(destFolder);
                    Logger.Log("Created Directory " + destFolder, true);

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
                            Logger.Log("Creating Subdirectory " + subDir.Replace("\\", ""));
                            dir = dir.CreateSubdirectory(subDir.Replace("\\", ""));
                            Logger.Log("Created Subdirectory " + subDir.Replace("\\", ""), true);
                        }
                    }

                    var destFile = (destFolder + "\\" +
                                    (subDirs != null ? string.Join("\\", subDirs) + "\\" : "") + fileName)
                        .Replace("\\\\", "\\");

                    Logger.Log("Copying " + overwriteFile + " to " + destFile);

                    File.Copy(overwriteFile, destFile);

                    Logger.Log("Copied File to " + destFile, true);

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

                    Logger.Log("Creating Directory " + destFolder);
                    Directory.CreateDirectory(destFolder);
                    Logger.Log("Created Directory " + destFolder, true);
                    Logger.Log("Copying File " + overwriteFile + " to " + destFolder + "\\" + parts.Last());

                    File.Copy(overwriteFile, destFolder + "\\" + parts.Last());

                    Logger.Log("Copied File to " + destFolder + "\\" + parts.Last(), true);

                    return destFolder + "\\" + parts.Last();
                }
            }

            //Is in the Data directory. Simply merge all files into a giant pak
            if (cleanPath.StartsWith("Data"))
            {
                var destFolder = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Data";

                Logger.Log("Creating Directory " + destFolder);
                var dir = Directory.CreateDirectory(destFolder);
                Logger.Log("Created Directory " + destFolder, true);

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
                        Logger.Log("Creating Subdirectory " + subDir.Replace("\\", ""));
                        dir = dir.CreateSubdirectory(subDir.Replace("\\", ""));
                        Logger.Log("Created Subdirectory " + subDir.Replace("\\", ""), true);
                    }
                }

                var destFile = (destFolder + "\\" +
                                (subDirs != null ? string.Join("\\", subDirs) + "\\" : "") + fileName)
                    .Replace("\\\\", "\\");

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

                Logger.Log("Creating Directory " + destFolder);
                Directory.CreateDirectory(destFolder);
                Logger.Log("Created Directory " + destFolder, true);

                RunKDiff3("\"" + baseFile + "\" \"" + overwriteFile + "\" -o \"" + destFolder + "\\" +
                          parts.Last() +
                          "\" --auto");

                return destFolder + "\\" + parts.Last();
            }
        }

        /// <summary>
        /// Extracts the file from zip.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        internal string ExtractFile(ModFile file)
        {
            Logger.Log("Extracting " + file.FileName + " from " + file.ModName + " in " + file.PakFile);
            if (File.Exists(file.PakFile))
            {
                using (FileStream fs = new FileStream(file.PakFile, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs))
                    {
                        var fileName = file.FilePath.EndsWith("Localization")
                            ? file.FileName.Split('\\').Last()
                            : file.FileName;
                        var zippedFile = zip.Entries.FirstOrDefault(entry => entry.FullName == fileName);

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

                            Logger.Log("Extracted " + file.FileName + " from " + file.ModName + " in " + file.PakFile +
                                       " to " + destFolder + "\\" + fileName, true);

                            return destFolder + "\\" + fileName;
                        }
                    }
                }
            }

            Logger.Log("Could not extract " + file.FileName + " from " + file.ModName + " in " + file.PakFile, true);

            return "";
        }

        /// <summary>
        /// Extracts the vanilla file from zip.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        internal string ExtractVanillaFile(ModFile file)
        {
            Logger.Log("Extracting " + file.FileName + " from Vanilla");
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
                            var zippedFile = zip.Entries.FirstOrDefault(entry => entry.FullName == fileName);

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

                                Logger.Log("Extracted " + file.FileName + " from Vanilla in " +
                                           Settings.Default.KCDPath + "\\Localization\\" +
                                           file.PakFile.Split('\\').Last() +
                                           " to " + destFolder + "\\" + fileName, true);

                                return destFolder + "\\" + fileName;
                            }
                        }
                    }
                }
            }
            else
            {
                var pakFile = FindVanillaDataFile(file.FileName);

                if (pakFile != string.Empty)
                {
                    using (FileStream fs = new FileStream(pakFile, FileMode.Open))
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            var zippedFile = zip.Entries.FirstOrDefault(entry => entry.FullName == file.FileName);

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

                                Logger.Log("Extracted " + file.FileName + " from Vanilla in " + pakFile +
                                           " to " + destFolder + "\\" + file.FileName, true);

                                return destFolder + "\\" + file.FileName;
                            }
                        }
                    }
                }
            }

            Logger.Log("Could not extract " + file.FileName + " from Vanilla in " + file.PakFile, true);

            return "";
        }

        /// <summary>
        /// Paks the data.
        /// </summary>
        internal void PakData()
        {
            Logger.Log("Packing Data Archive");
            var rootDir = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged";
            var dir = Directory.CreateDirectory(rootDir);
            dir.CreateSubdirectory("Data");

            Logger.Log("Adding Startup Script");

            var scriptsDir = Directory.CreateDirectory(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR +
                                                       "\\Data\\Scripts");
            scriptsDir.CreateSubdirectory("Startup");

            using (FileStream fs =
                File.Open(
                    Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR +
                    "\\Data\\Scripts\\Startup\\KCDModMerger.lua", FileMode.Create))
            {
                using (StreamWriter file = new StreamWriter(fs))
                {
                    file.Write("System.LogAlways(string.format(\"$5[INFO] Loaded KCDModMerger Merged Mods!\"))" +
                               Environment.NewLine + "System.LogAlways(string.format(\"$5[INFO] Files: " +
                               string.Join(", ", _mergedFiles) + "\"))");
                }
            }

            Logger.Log("Added Startup Script!");

            ZipFile.CreateFromDirectory(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Data",
                rootDir + "\\Data\\data.pak");
            Logger.Log("Packed Data Archive!");
        }

        /// <summary>
        /// Paks the localization.
        /// </summary>
        internal void PakLocalization()
        {
            Logger.Log("Packing Localization Archives");
            var rootDir = Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged";
            var dir = Directory.CreateDirectory(rootDir);
            dir.CreateSubdirectory("Localization");

            var languages =
                Directory.GetDirectories(Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR +
                                         "\\Localization");

            foreach (string language in languages)
            {
                Logger.Log("Packing Localization Archive " + language);
                ZipFile.CreateFromDirectory(language,
                    Settings.Default.KCDPath + MODMANAGER_DIR + TEMP_MERGED_DIR + "\\Merged\\Localization\\" +
                    language.Split('\\').Last() + ".pak");
            }

            Logger.Log("Packed Localization Archives!");
        }

        /// <summary>
        /// Creates the directories.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="vanilla">if set to <c>true</c> creates directories for vanilla file.</param>
        /// <returns></returns>
        private string CreateDirectories(ModFile file, bool vanilla)
        {
            Logger.Log("Creating Directories in " + TEMP_FILES + " for " + file.FileName +
                       (vanilla ? "(Vanilla)" : "(" + file.ModName + ")"));
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

            Logger.Log("Creating Directory " + rootFolder);
            var dir = Directory.CreateDirectory(rootFolder);
            Logger.Log("Created Directory " + rootFolder, true);
            dir.CreateSubdirectory("Localization");
            dir.CreateSubdirectory("Data");
            Logger.Log("Created Subdirectories Data and Localization in " + rootFolder, true);

            var subDir = dir.CreateSubdirectory(subdirectory);

            if (localizationSubDirectry != "")
            {
                Logger.Log("Creating Subdirectory " + localizationSubDirectry.Replace("\\", "") + " in " + subDir.Name);
                subDir = subDir.CreateSubdirectory(localizationSubDirectry.Replace("\\", ""));
                Logger.Log("Created Subdirectory " + localizationSubDirectry.Replace("\\", "") + " in " + subDir.Name,
                    true);
            }

            if (fileSpecificSubDirs != null)
            {
                foreach (string s in fileSpecificSubDirs)
                {
                    var oldDir = subDir.Name;
                    Logger.Log("Creating Subdirectory " + s + " in " + subDir.Name);
                    subDir = subDir.CreateSubdirectory(s);
                    Logger.Log("Created Subdirectory " + s + " in " + oldDir, true);
                }
            }

            Logger.Log("Created Directories in " + TEMP_FILES + " for " + file.FileName +
                       (vanilla ? "(Vanilla)" : "(" + file.ModName + ")"), true);

            return rootFolder + "\\" + subdirectory + localizationSubDirectry;
        }

        /// <summary>
        /// Finds the vanilla data file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        private string FindVanillaDataFile(string file)
        {
            Logger.Log("Finding Vanilla Data File for " + file);

            Dictionary<string, List<string>> saved = null;

            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json"))
            {
                Logger.Log("Loading Saved Vanilla Data File Paths");
                using (StreamReader fs =
                    new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json"))
                {
                    var json = fs.ReadToEnd();
                    saved = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                    Logger.Log("Loaded Saved Vanilla Data File Paths!");
                }
            }

            if (saved == null)
            {
                Logger.Log("Saved Vanilla Data Files are empty, creating");
                saved = new Dictionary<string, List<string>>();
                Logger.Log("Saved Vanilla Data Files created!");
            }

            var savedPak = saved.FirstOrDefault(pair => pair.Value.Contains(file));

            if (!savedPak.IsDefault())
            {
                Logger.Log(file + " was found in saved!");
                if (File.Exists(savedPak.Key))
                {
                    Logger.Log(file + " found saved pak file " + savedPak.Key, true);
                    using (FileStream fs = File.OpenRead(savedPak.Key))
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            var entry = zip.Entries.FirstOrDefault(zipEntry => zipEntry.FullName == file);

                            if (entry != null)
                            {
                                Logger.Log(file + " still exists in pak!");

                                // No need to save, nothing changed
                                return savedPak.Key;
                            }

                            Logger.Log(file + " doesn't exist in pak anymore!");
                            savedPak.Value.Remove(file);
                        }
                    }
                }
                else
                {
                    Logger.Log(savedPak.Key + " doesn't exist anymore!");
                    saved.Remove(savedPak.Key);
                }
            }

            Logger.Log("Searching Paks");

            var path = Settings.Default.KCDPath + "\\Data";
            var paks = Directory.GetFiles(path, "*.pak", SearchOption.AllDirectories);

            Logger.Log("Found " + paks.Length + " paks!");

            foreach (string pak in paks)
            {
                Logger.Log("Searching in " + pak);
                using (FileStream fs = File.Open(pak, FileMode.Open))
                {
                    try
                    {
                        using (ZipArchive zip = new ZipArchive(fs))
                        {
                            if (zip.Entries.Count > 0)
                            {
                                saved[pak] = new List<string>();
                                foreach (ZipArchiveEntry zipArchiveEntry in zip.Entries)
                                {
                                    saved[pak].Add(zipArchiveEntry.FullName);
                                }

                                var srcFile = zip.Entries.FirstOrDefault(entry => entry.FullName.Equals(file));

                                if (srcFile != null)
                                {
                                    Logger.Log(file + " found in " + pak, true);

                                    SaveVanillaFilePaths(saved);

                                    return pak;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log(pak + ": could not be opened! (This is not a critical issue!) " + e.Message);
                    }
                }
            }

            Logger.Log(file + " could not be found!");

            return "";
        }

        /// <summary>
        /// Saves the vanilla file paths.
        /// </summary>
        /// <param name="saved">The saved.</param>
        private void SaveVanillaFilePaths(Dictionary<string, List<string>> saved)
        {
            Logger.Log("Saving Vanilla File Paths");
            var file = AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json";
            if (File.Exists(file))
            {
                File.Delete(file);
            }

            using (StreamWriter sw = File.CreateText(AppDomain.CurrentDomain.BaseDirectory + "\\VanillaFiles.json"))
            {
                sw.Write(JsonConvert.SerializeObject(saved, Formatting.Indented));
            }

            Logger.Log("Saved!");
        }

        /// <summary>
        /// Scans the data dir.
        /// </summary>
        private void ScanDataDir()
        {
            Logger.Log("Scanning Data Directory for Mods");
            if (Directory.Exists(Settings.Default.KCDPath + "\\Data"))
            {
                var files = Directory.GetFiles(Settings.Default.KCDPath + "\\Data", "zzz*.pak");

                Logger.Log("Found " + files.Length + " Mods!");

                foreach (var file in files)
                {
                    var fileName = file.Split('\\').Last();
                    var name = fileName.Replace("zzz_", "").Replace("zzz", "").Replace(".pak", "");
                    var rootDir = Settings.Default.KCDPath + "\\Mods\\" + name;

                    Logger.Log("Converting " + name);

                    if (Directory.Exists(rootDir))
                    {
                        Logger.Log("Mod Directory already exists! Changing name to " + rootDir + "_extracted!");
                        rootDir += "_extracted";
                    }

                    Logger.Log("Creating Directory " + rootDir);
                    var dir = Directory.CreateDirectory(rootDir);
                    Logger.Log("Created Directory " + rootDir, true);

                    Logger.Log("Creating Subdirectories Data and Localization");
                    dir.CreateSubdirectory("Data");
                    dir.CreateSubdirectory("Localization");
                    Logger.Log("Created Subdirectories Data and Localization!");

                    Logger.Log("Writing Manifest File");

                    using (XmlWriter xml = XmlWriter.Create(rootDir + "\\mod.manifest"))
                    {
                        xml.WriteStartDocument();
                        xml.WriteStartElement("kcd_mod");
                        xml.WriteStartElement("info");
                        xml.WriteElementString("name", name);
                        xml.WriteElementString("description", "Extracted by KCDModMerger");
                        xml.WriteElementString("author", "KCDModMerger");
                        xml.WriteElementString("version", VERSION);
                        xml.WriteElementString("created_on", DateTime.Now.ToString());
                        xml.WriteEndElement();
                        xml.WriteEndElement();
                        xml.WriteEndDocument();
                    }

                    Logger.Log("Wrote Manifest File!");

                    Logger.Log("Copying " + file + " to " + rootDir + "\\Data\\" + fileName);

                    File.Copy(file, rootDir + "\\Data\\" + fileName);
                    if (File.Exists(rootDir + "\\Data\\" + fileName))
                    {
                        Logger.Log("Copy Successful!");
                        Logger.Log("Deleting old file");
                        File.Delete(file);
                        Logger.Log("Deleted old file!");
                    }
                    else
                    {
                        Logger.Log("Copy not successful...abort!");
                    }
                }
            }
        }

        /// <summary>
        /// Updates the mod list.
        /// The actual Update is threaded!
        /// </summary>
        private void UpdateModList()
        {
            Mods.Clear();
            ModNames.Clear();
            Conflicts.Clear();
            ModFiles.Clear();
            _mergedFiles.Clear();

            OnPropertyChanged(nameof(Conflicts));

            ScanDataDir();

            var folders = GetFolders(Settings.Default.KCDPath + "\\Mods");

            Logger.Log("Found " + folders.Length + " Mods!");

            List<string> tempFiles = new List<string>();
            var tasks = new Task[folders.Length];
            var index = 0;

            foreach (var folder in folders)
            {
                Logger.Log("Checking if " + folder.Split('\\').Last() + " is a valid mod directory");

                var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

                if (files.Any(file => file.EndsWith(".manifest") || file.EndsWith(".pak")))
                {
                    Logger.Log(folder.Split('\\').Last() + " looks like a Mod!");
                    Logger.Log("Adding Mod " + folder.Split('\\').Last());

                    tasks[index] = Task.Factory.StartNew(() =>
                    {
                        var logger = new StringBuilder();
                        var mod = new Mod(folder, logger);

                        logger.AppendLine(
                            Logger.BuildLogWithDate("Adding Mod " + mod.manifest.DisplayName + " to Global Storage"));

                        MainWindow.dispatcherGlobal.Invoke(() => { ModNames.Add(mod.manifest.DisplayName); },
                            DispatcherPriority.ApplicationIdle);

                        lock (tempFiles)
                        {
                            foreach (ModFile modFile in mod.DataFiles)
                            {
                                tempFiles.Add(modFile.FileName);
                            }
                        }

                        lock (ModFiles)
                        {
                            ModFiles.AddRange(mod.DataFiles);
                        }

                        lock (_mergedFiles)
                        {
                            _mergedFiles.AddRange(mod.MergedFiles);
                        }

                        lock (Mods)
                        {
                            Mods.Add(mod);
                        }

                        logger.AppendLine(
                            Logger.BuildLogWithDate("Added " + mod.manifest.DisplayName + " to Global Storage!"));

                        Logger.Log(logger);

                        return mod;
                    });
                    index++;
                }
                else
                {
                    Logger.Log(folder.Split('\\').Last() + " does not look like a valid mod directory!");
                }
            }

            Task.Factory.ContinueWhenAll(tasks, tasksCompleted =>
            {
                var sb = new StringBuilder();

                lock (tempFiles)
                {
                    tempFiles.Sort(string.Compare);
                }

                lock (ModFiles)
                {
                    ModFiles.Sort((x, y) => string.CompareOrdinal(x.ModName, y.ModName));
                }

                lock (Mods)
                {
                    Mods.Sort((x, y) => string.CompareOrdinal(x.manifest.DisplayName, y.manifest.DisplayName));
                }

                sb.AppendLine(Logger.BuildLogWithDate("Finding Conflicts"));

                var duplicates = new HashSet<string>(tempFiles.GroupBy(x => x).Where(x => x.Skip(1).Any())
                    .Select(x => x.Key));

                MainWindow.dispatcherGlobal.Invoke(() =>
                {
                    foreach (string duplicate in duplicates)
                    {
                        Conflicts.Add(duplicate);
                    }
                });

                sb.AppendLine(Logger.BuildLogWithDate("Found " + duplicates.Count + " Conflicts!"));

                OnPropertyChanged(nameof(Conflicts));

                Logger.Log(sb);
            });
        }

        /// <summary>
        /// Gets all folders.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        private string[] GetFolders(string path)
        {
            Logger.Log("Finding Folders in " + path);
            if (Directory.Exists(path))
            {
                var dirs = Directory.GetDirectories(path);

                Logger.Log("Found " + dirs.Length + " Folders!");

                return dirs;
            }

            Logger.Log("There is no " + path.Split('\\').Last() + " Directory!");
            MessageBox.Show("There is no " + path.Split('\\').Last() + " Directory!", "KCDModMerger",
                MessageBoxButton.OK);

            return new List<string>().ToArray();
        }

        /// <summary>
        /// Settings changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void SettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "KCDPath")
            {
                Logger.Log("KCD Root Path changed to " + Settings.Default.KCDPath, true);
                if (!MainWindow.isMerging)
                {
                    var isValid = Update();

                    if (!isValid)
                    {
                        Logger.Log("Chosen KCD Path is not the KCD root folder!");
                        MessageBox.Show("Chosen Path is not the root folder of KCD! " + Environment.NewLine +
                                        "It should be something like ...\\KingdomComeDeliverance!", "KCDModMerger",
                            MessageBoxButton.OK);
                    }
                }
                else
                {
                    Logger.Log("Update rejected because of ongoing merge operation!");
                    MessageBox.Show("Change will be applied after ongoing merge operation is finished!", "KCDModMerger",
                        MessageBoxButton.OK);
                }
            }
        }

        /// <summary>
        /// Gets the total free space.
        /// </summary>
        /// <param name="driveName">The drive name.</param>
        /// <returns></returns>
        private long GetTotalFreeSpace(string driveName)
        {
            var drives = DriveInfo.GetDrives();

            var drive = drives.FirstOrDefault(info => info.IsReady && info.Name == driveName);

            if (drive != null)
            {
                return drive.AvailableFreeSpace;
            }

            return -1;
        }

        /// <summary>
        /// Deletes the folder.
        /// </summary>
        /// <param name="folder">The folder.</param>
        private void DeleteFolder(string folder)
        {
            Logger.Log("Deleting " + folder + " Directory");
            if (Directory.Exists(folder))
            {
                for (var i = 0; i < 2; i++)
                {
                    try
                    {
                        Directory.Delete(folder, true);
                    }
                    catch (Exception e)
                    {
                        Logger.Log("Tried deleting " + folder + " Directory but " + e.Message);
                        var result =
                            MessageBox.Show("Could not clean " + folder +
                                            " Directory! If you have it open in Window Explorer, please close it!",
                                "KCDModMerger",
                                MessageBoxButton.OK);

                        if (result == MessageBoxResult.OK || result == MessageBoxResult.Yes)
                        {
                            // Try again after 10 Seconds
                            Thread.Sleep(10000);
                        }

                        if (result == MessageBoxResult.Cancel || result == MessageBoxResult.No)
                        {
                            Logger.Log("Tried deleting " + folder + " Directory but got aborted!");
                            MessageBox.Show("Could not clean " + folder +
                                            " Directory! Future Operation of this program is not guaranteed!",
                                "KCDModMerger", MessageBoxButton.OK);
                            return;
                        }
                    }

                    if (!Directory.Exists(folder))
                    {
                        Logger.Log("Deleted " + folder + " Directory!");
                        return;
                    }
                }

                if (Directory.Exists(folder))
                {
                    Logger.Log("Tried deleting " + folder + " Directory but failed!");
                    MessageBox.Show("Could not clean " + folder +
                                    " Directory! Future Operation of this program is not guaranteed!",
                        "KCDModMerger", MessageBoxButton.OK);
                }
            }
            else
            {
                Logger.Log(folder + " does not exist!");
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (timer == null)
            {
                timer = new Timer(state =>
                {
                    var sb = new StringBuilder();

                    sb.AppendLine(Logger.BuildLogWithDate("Notifying ModManager Listeners"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    timer = null;
                    sb.AppendLine(Logger.BuildLogWithDate("Notified ModManager Listeners!"));

                    Logger.Log(sb);
                }, null, 1000, Timeout.Infinite);
            }
            else
            {
                timer.Change(1000, Timeout.Infinite);
            }
        }
    }
}