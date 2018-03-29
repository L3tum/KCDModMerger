using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Xml.Linq;
using KCDModMerger.Annotations;
using Newtonsoft.Json;

namespace KCDModMerger
{
    internal class Mod : INotifyPropertyChanged
    {
        private string author;
        private string createdOn;
        private string description;
        private string displayName;
        private ModFile[] files;
        private string folderName;
        private string[] folders;
        private string version;
        private string[] versionsSupported = new string[0];
        private ModFile[] dataFiles;
        private string[] mergedFiles = new string[0];

        private readonly string[] DISALLOWED_FILETYPES = new[]
            {".tbl", ".skin", ".dds", ".cgf", ".cdf", ".chr", ".usm", ".sqcb", ".1", ".2", ".3", ".4", ".5"};

        public ModFile[] DataFiles
        {
            get => dataFiles;
            set
            {
                dataFiles = value;
                OnPropertyChanged();
            }
        }

        #region Properties

        public string Author
        {
            get => author;
            set => author = value;
        }

        public string CreatedOn
        {
            get => createdOn;
            set => createdOn = value;
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public ModFile[] Files
        {
            get => files;
            set => files = value;
        }

        public string FolderName
        {
            get => folderName;
            set => folderName = value;
        }

        public string[] Folders
        {
            get => folders;
            set => folders = value;
        }

        public string Version
        {
            get => version;
            set => version = value;
        }

        public string[] VersionsSupported
        {
            get => versionsSupported;
            set => versionsSupported = value;
        }

        public string[] MergedFiles
        {
            get => mergedFiles;
            set => mergedFiles = value;
        }

        #endregion

        public Mod(string folderName)
        {
            Logger.Log("Initializing Mod...");
            Logger.Log("Folder: " + folderName, true);
            this.FolderName = folderName;
            DisplayName = folderName;

            this.ReadManifest(folderName);
            this.Folders = GetFolders(folderName);
            List<ModFile> folderFiles = new List<ModFile>();

            Logger.Log("Searching for Paks...");

            foreach (string folder in Folders)
            {
                Logger.Log("Searching for Paks in " + folder.Split('\\').Last());
                var paks = GetPaks(folder);

                Logger.Log("Found " + paks.Length + " Paks in " + folder.Split('\\').Last(), true);

                foreach (string pak in paks)
                {
                    Logger.Log("Found " + pak + " in " + folder.Split('\\').Last(), true);
                    folderFiles.Add(new ModFile(pak, displayName, folder, pak));
                }
            }

            Logger.Log("Found total of " + folderFiles.Count, true);

            Files = folderFiles.ToArray();

            Logger.Log("Searching for actual files in Paks...");

            List<ModFile> zippedFiles = new List<ModFile>();
            foreach (ModFile file in files)
            {
                Logger.Log("Searching in " + file.FileName);
                using (FileStream fs = File.Open(file.FileName, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            if (entry.FullName.Contains(".") &&
                                !DISALLOWED_FILETYPES.Any(s => entry.FullName.EndsWith(s)))
                            {
                                if (file.FilePath.EndsWith("Localization"))
                                {
                                    Logger.Log("Found Localization File " + file.FileName.Split('\\').Last() + "\\" +
                                               entry.FullName, true);
                                    zippedFiles.Add(new ModFile(
                                        file.FileName.Split('\\').Last() + "\\" + entry.FullName, displayName,
                                        file.FilePath, file.FileName));
                                }
                                else
                                {
                                    Logger.Log("Found Data File " + entry.FullName, true);
                                    zippedFiles.Add(new ModFile(entry.FullName, displayName, file.FilePath,
                                        file.FileName));
                                }
                            }
                        }
                    }
                }
            }

            Logger.Log("Found total of " + zippedFiles.Count, true);

            DataFiles = zippedFiles.ToArray();
        }


        private string[] GetFolders(string baseFolder)
        {
            return Directory.GetDirectories(baseFolder);
        }

        private string[] GetPaks(string folder)
        {
            return Directory.GetFiles(folder, "*.pak");
        }

        private void ReadManifest(string baseFolder)
        {
            Logger.Log("Reading Manifest...");
            if (File.Exists(baseFolder + "\\mod.manifest"))
            {
                using (StreamReader sr = new StreamReader(baseFolder + "\\mod.manifest"))
                {
                    XDocument doc = XDocument.Parse(sr.ReadToEnd()); //or XDocument.Load(baseFolder + "/mod.manifest")
                    var jsonText = JsonConvert.SerializeXNode(doc);
                    jsonText = Regex.Replace(jsonText, "\"\\?xml\":{[^}]+", "").Replace("{},", "{");
                    dynamic dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);

                    Logger.Log("Manifest:");
                    Logger.Log(jsonText);

                    if (this.HasProperty(dyn, "kcd_mod"))
                    {
                        Logger.Log("Found kcd_mod directive!");
                        var kcd_mod = dyn.kcd_mod;

                        if (this.HasProperty(kcd_mod, "info"))
                        {
                            Logger.Log("Found info directive!");
                            var info = kcd_mod.info;

                            var properties = ((IDictionary<string, object>) info);

                            foreach (var property in properties)
                                switch (property.Key)
                                {
                                    case "name":
                                    {
                                        Logger.Log("Found name: " + info.name, true);
                                        DisplayName = info.name;
                                        break;
                                    }
                                    case "description":
                                    {
                                        Logger.Log("Found description: " + info.description, true);
                                        Description = info.description;
                                        break;
                                    }
                                    case "author":
                                    {
                                        Logger.Log("Found author: " + info.author, true);
                                        Author = info.author;
                                        break;
                                    }
                                    case "version":
                                    {
                                        Logger.Log("Found version: " + info.version, true);
                                        Version = info.version;
                                        break;
                                    }
                                    case "created_on":
                                    {
                                        Logger.Log("Found created_on: " + info.created_on, true);
                                        CreatedOn = info.created_on;
                                        break;
                                    }
                                }
                        }
                        else
                        {
                            Logger.Log("Could not find info directive!");
                        }

                        if (this.HasProperty(kcd_mod, "supports") && this.HasProperty(kcd_mod.supports, "kcd_version"))
                        {
                            Logger.Log("Found List of supported versions!");
                            try
                            {
                                string s = string.Join(",", kcd_mod.supports.kcd_version);
                                Logger.Log("Supported Versions: " + s, true);
                                versionsSupported = s.Split(',');
                            }
                            catch (Exception e)
                            {
                                Logger.Log("List of Supported Versions was faulty: " + e.Message, true);
                            }
                        }
                        else
                        {
                            Logger.Log("Could not find supports directive (it doesn't really work anyways)!");
                        }

                        if (this.HasProperty(kcd_mod, "merged_files") && this.HasProperty(kcd_mod.merged_files, "file"))
                        {
                            Logger.Log("Found List of Merged Files!");
                            try
                            {
                                string s = string.Join(",", kcd_mod.merged_files.file);
                                Logger.Log("Files:" + s, true);
                                mergedFiles = s.Split(',');
                            }
                            catch (Exception e)
                            {
                                Logger.Log("List of Merged Files was faulty: " + e.Message, true);
                            }
                        }
                    }
                    else
                    {
                        Logger.Log("Could not find kcd_mod directive!");
                    }
                }

                Logger.Log("Finished Reading Manifest!");
            }
            else
            {
                Logger.Log("Manifest does not exist!");
            }
        }

        private bool HasProperty(dynamic dyn, string property)
        {
            return ((IDictionary<string, object>) dyn).ContainsKey(property);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}