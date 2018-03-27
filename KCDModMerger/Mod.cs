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
        private string[] versionsSupported;
        private ModFile[] dataFiles;
        private readonly string[] DISALLOWED_FILETYPES = new[] {".tbl", ".skin", ".dds", ".cgf", ".cdf", ".chr", ".usm", ".sqcb", ".1", ".2", ".3", ".4", ".5"};

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

        #endregion

        public Mod(string folderName)
        {
            this.FolderName = folderName;
            DisplayName = folderName;

            this.ReadManifest(folderName);
            this.Folders = GetFolders(folderName);
            List<ModFile> folderFiles = new List<ModFile>();

            foreach (string folder in Folders)
            {
                var paks = GetPaks(folder);

                foreach (string pak in paks)
                {
                    folderFiles.Add(new ModFile(pak, displayName, folder, pak));
                }
            }

            Files = folderFiles.ToArray();

            List<ModFile> zippedFiles = new List<ModFile>();
            foreach (ModFile file in files)
            {
                using (FileStream fs = File.Open(file.FileName, FileMode.Open))
                {
                    using (ZipArchive zip = new ZipArchive(fs))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            if (entry.FullName.Contains(".") && !DISALLOWED_FILETYPES.Any(s => entry.FullName.EndsWith(s)))
                            {
                                if (file.FilePath.EndsWith("Localization"))
                                {
                                    zippedFiles.Add(new ModFile(
                                        file.FileName.Split('\\').Last() + "\\" + entry.FullName, displayName,
                                        file.FilePath, file.FileName));
                                }
                                else
                                {
                                    zippedFiles.Add(new ModFile(entry.FullName, displayName, file.FilePath,
                                        file.FileName));
                                }
                            }
                        }
                    }
                }
            }

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
            if (File.Exists(baseFolder + "\\mod.manifest"))
            {
                using (StreamReader sr = new StreamReader(baseFolder + "\\mod.manifest"))
                {
                    XDocument doc = XDocument.Parse(sr.ReadToEnd()); //or XDocument.Load(baseFolder + "/mod.manifest")
                    var jsonText = JsonConvert.SerializeXNode(doc);
                    jsonText = Regex.Replace(jsonText, "\"\\?xml\":{[^}]+", "").Replace("{},", "{");
                    dynamic dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);

                    if (this.HasProperty(dyn, "kcd_mod"))
                    {
                        var kcd_mod = dyn.kcd_mod;

                        if (this.HasProperty(kcd_mod, "info"))
                        {
                            var info = kcd_mod.info;

                            var properties = ((IDictionary<string, object>) info);

                            foreach (var property in properties)
                                switch (property.Key)
                                {
                                    case "name":
                                    {
                                        DisplayName = info.name;
                                        break;
                                    }
                                    case "description":
                                    {
                                        Description = info.description;
                                        break;
                                    }
                                    case "author":
                                    {
                                        Author = info.author;
                                        break;
                                    }
                                    case "version":
                                    {
                                        Version = info.version;
                                        break;
                                    }
                                    case "created_on":
                                    {
                                        CreatedOn = info.created_on;
                                        break;
                                    }
                                }
                        }

                        if (this.HasProperty(kcd_mod, "supports"))
                        {
                            var versionsSupps = kcd_mod.supports;
                            this.VersionsSupported = versionsSupps.kcd_version;
                        }
                    }
                }
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