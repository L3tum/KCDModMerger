#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using KCDModMerger.Annotations;
using KCDModMerger.Logging;
using Newtonsoft.Json;

#endregion

namespace KCDModMerger.Mods
{
    internal class ModManifest : INotifyPropertyChanged
    {
        private readonly string file;
        private string humanReadableInfo = "";

        /// <summary>
        ///     Initializes a new instance of the <see cref="ModManifest" /> class.
        /// </summary>
        /// <param name="file">The file.</param>
        public ModManifest(string file)
        {
            this.file = file;
            var parts = file.Split('\\');
            DisplayName = parts[parts.Length - 2];

            ReadManifest();
        }

        public string HumanReadableInfo
        {
            get => humanReadableInfo;
            private set
            {
                humanReadableInfo = value;
                OnPropertyChanged(nameof(HumanReadableInfo));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     Reads the manifest.
        /// </summary>
        [LogInterceptor]
        private void ReadManifest()
        {
            if (!string.IsNullOrEmpty(file))
            {
                Logging.Logger.Log("Reading Manifest for " +
                                   file.Split('\\').ElementAt(file.Split('\\').Length - 2));
                if (File.Exists(file))
                    using (var sr = new StreamReader(file))
                    {
                        var doc = XDocument.Parse(sr.ReadToEnd()); //or XDocument.Load(baseFolder + "/mod.manifest")
                        var jsonText = JsonConvert.SerializeXNode(doc);
                        jsonText = Regex.Replace(jsonText, "\"\\?xml\":{[^}]+", "").Replace("{},", "{");
                        dynamic dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);

                        Logging.Logger.Log("Manifest: " + jsonText, true);

                        if (this.HasProperty(dyn, "kcd_mod"))
                        {
                            Logging.Logger.Log("Found kcd_mod directive!");
                            var kcd_mod = dyn.kcd_mod;

                            if (this.HasProperty(kcd_mod, "info"))
                            {
                                Logging.Logger.Log("Found info directive!");
                                var info = kcd_mod.info;

                                var properties = (IDictionary<string, object>) info;

                                foreach (var property in properties)
                                    switch (property.Key)
                                    {
                                        case "name":
                                        {
                                            Logging.Logger.Log("Found name: " + info.name, true);
                                            DisplayName = info.name;
                                            break;
                                        }
                                        case "description":
                                        {
                                            Logging.Logger.Log("Found description: " + info.description, true);
                                            Description = info.description;
                                            break;
                                        }
                                        case "author":
                                        {
                                            Logging.Logger.Log("Found author: " + info.author, true);
                                            Author = info.author;
                                            break;
                                        }
                                        case "version":
                                        {
                                            Logging.Logger.Log("Found version: " + info.version, true);
                                            Version = info.version;
                                            break;
                                        }
                                        case "created_on":
                                        {
                                            Logging.Logger.Log("Found created_on: " + info.created_on, true);
                                            CreatedOn = info.created_on;
                                            break;
                                        }
                                    }
                            }

                            if (this.HasProperty(kcd_mod, "supports") &&
                                this.HasProperty(kcd_mod.supports, "kcd_version"))
                            {
                                Logging.Logger.Log("Found List of supported versions!");
                                try
                                {
                                    string s = string.Join(",", kcd_mod.supports.kcd_version);
                                    Logging.Logger.Log("Supported Versions: " + s, true);
                                    VersionsSupported = s.Split(',');
                                }
                                catch (Exception e)
                                {
                                    Logging.Logger.Log("List of Supported Versions was faulty: " + e.Message, true);
                                }
                            }

                            if (this.HasProperty(kcd_mod, "merged_files") &&
                                this.HasProperty(kcd_mod.merged_files, "file"))
                            {
                                Logging.Logger.Log("Found List of Merged Files!");
                                try
                                {
                                    string s = string.Join(",", kcd_mod.merged_files.file);
                                    Logging.Logger.Log("Files:" + s, true);
                                    MergedFiles = s.Split(',');
                                }
                                catch (Exception e)
                                {
                                    Logging.Logger.Log("List of Merged Files was faulty: " + e.Message, true);
                                }
                            }
                        }
                    }
                else
                    Logging.Logger.Log("Manifest does not exist!");
            }
            else
            {
                Logging.Logger.Log("No Manifest found!");
            }

            HumanReadableInfo = GenerateHumanReadableInfo();
        }

        [LogInterceptor]
        private string GenerateHumanReadableInfo()
        {
            Logging.Logger.Log("Generating Human Readable Information for " + DisplayName);

            var sb = new StringBuilder();

            sb.AppendLine(DisplayName);

            if (!string.IsNullOrEmpty(Version)) sb.AppendLine(string.Concat("Version: ", Version));

            if (!string.IsNullOrEmpty(Author)) sb.AppendLine(string.Concat("Author: ", Author));

            if (!string.IsNullOrEmpty(CreatedOn)) sb.AppendLine(string.Concat("Created On: ", CreatedOn));

            if (!string.IsNullOrEmpty(Description)) sb.AppendLine(Description);

            if (VersionsSupported.Length > 0)
                sb.AppendLine(string.Concat("Supported Versions:", Environment.NewLine, Environment.NewLine,
                    string.Join(Environment.NewLine + Environment.NewLine, VersionsSupported)));

            if (MergedFiles.Length > 0)
                sb.AppendLine(string.Concat("Merged Files:", Environment.NewLine, Environment.NewLine,
                    string.Join(Environment.NewLine + Environment.NewLine, MergedFiles)));

            return sb.ToString();
        }

        /// <summary>
        ///     Determines whether the specified dynamic has property.
        /// </summary>
        /// <param name="dyn">The dynamic.</param>
        /// <param name="property">The property.</param>
        /// <returns>
        ///     <c>true</c> if the specified dynamic has property; otherwise, <c>false</c>.
        /// </returns>
        private bool HasProperty(dynamic dyn, string property)
        {
            if (dyn is string) return false;

            return ((IDictionary<string, object>) dyn).ContainsKey(property);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Properties

        private ModFile[] dataFiles = Array.Empty<ModFile>();
        public string Author { get; set; } = "";

        public string CreatedOn { get; set; } = "";

        public string Description { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public ModFile[] Files { get; set; } = Array.Empty<ModFile>();

        public string FolderName { get; set; } = "";

        public string[] Folders { get; set; } = Array.Empty<string>();

        public string Version { get; set; } = "";

        public string[] VersionsSupported { get; set; } = Array.Empty<string>();

        public string[] MergedFiles { get; set; } = Array.Empty<string>();

        public ModFile[] DataFiles
        {
            get => dataFiles;
            set
            {
                dataFiles = value;
                OnPropertyChanged();
            }
        }

        #endregion
    }
}