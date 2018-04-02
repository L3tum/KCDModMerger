#region usings

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KCDModMerger.Annotations;
using Newtonsoft.Json;

#endregion

namespace KCDModMerger.Mods
{
    internal class ModManifest : INotifyPropertyChanged
    {
        private readonly string file;
        private readonly bool flush;
        private string humanReadableInfo = "";
        private readonly StringBuilder logBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModManifest"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="logger">The logger. Can be either null or a StringBuilder to append its output to the parent process.</param>
        public ModManifest(string file, StringBuilder logger = null)
        {
            this.file = file;
            if (logger != null)
            {
                logBuilder = logger;
            }
            else
            {
                logBuilder = new StringBuilder();
                flush = true;
            }
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
        /// Reads the manifest.
        /// </summary>
        public void ReadManifest()
        {
            if (file != "")
            {
                logBuilder.AppendLine(
                    Logger.BuildLogWithDate("Reading Manifest for " +
                                            file.Split('\\').ElementAt(file.Split('\\').Length - 2)));
                if (File.Exists(file))
                {
                    using (var sr = new StreamReader(file))
                    {
                        var doc = XDocument.Parse(sr.ReadToEnd()); //or XDocument.Load(baseFolder + "/mod.manifest")
                        var jsonText = JsonConvert.SerializeXNode(doc);
                        jsonText = Regex.Replace(jsonText, "\"\\?xml\":{[^}]+", "").Replace("{},", "{");
                        dynamic dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);

                        logBuilder.AppendLine(Logger.BuildLogWithDate("Manifest: " + jsonText, true));

                        if (this.HasProperty(dyn, "kcd_mod"))
                        {
                            logBuilder.AppendLine(Logger.BuildLogWithDate("Found kcd_mod directive!"));
                            var kcd_mod = dyn.kcd_mod;

                            if (this.HasProperty(kcd_mod, "info"))
                            {
                                logBuilder.AppendLine(Logger.BuildLogWithDate("Found info directive!"));
                                var info = kcd_mod.info;

                                var properties = (IDictionary<string, object>) info;

                                foreach (var property in properties)
                                    switch (property.Key)
                                    {
                                        case "name":
                                        {
                                            logBuilder.AppendLine(Logger.BuildLogWithDate("Found name: " + info.name,
                                                true));
                                            DisplayName = info.name;
                                            break;
                                        }
                                        case "description":
                                        {
                                            logBuilder.AppendLine(
                                                Logger.BuildLogWithDate("Found description: " + info.description,
                                                    true));
                                            Description = info.description;
                                            break;
                                        }
                                        case "author":
                                        {
                                            logBuilder.AppendLine(
                                                Logger.BuildLogWithDate("Found author: " + info.author, true));
                                            Author = info.author;
                                            break;
                                        }
                                        case "version":
                                        {
                                            logBuilder.AppendLine(
                                                Logger.BuildLogWithDate("Found version: " + info.version, true));
                                            Version = info.version;
                                            break;
                                        }
                                        case "created_on":
                                        {
                                            logBuilder.AppendLine(
                                                Logger.BuildLogWithDate("Found created_on: " + info.created_on, true));
                                            CreatedOn = info.created_on;
                                            break;
                                        }
                                    }
                            }
                            else
                            {
                                logBuilder.AppendLine(Logger.BuildLogWithDate("Could not find info directive!"));
                            }

                            if (this.HasProperty(kcd_mod, "supports") &&
                                this.HasProperty(kcd_mod.supports, "kcd_version"))
                            {
                                logBuilder.AppendLine(Logger.BuildLogWithDate("Found List of supported versions!"));
                                try
                                {
                                    string s = string.Join(",", kcd_mod.supports.kcd_version);
                                    logBuilder.AppendLine(Logger.BuildLogWithDate("Supported Versions: " + s, true));
                                    VersionsSupported = s.Split(',');
                                }
                                catch (Exception e)
                                {
                                    logBuilder.AppendLine(
                                        Logger.BuildLogWithDate("List of Supported Versions was faulty: " + e.Message,
                                            true));
                                }
                            }
                            else
                            {
                                logBuilder.AppendLine(Logger.BuildLogWithDate(
                                    "Could not find supports directive (it doesn't really work anyways)!"));
                            }

                            if (this.HasProperty(kcd_mod, "merged_files") &&
                                this.HasProperty(kcd_mod.merged_files, "file"))
                            {
                                logBuilder.AppendLine(Logger.BuildLogWithDate("Found List of Merged Files!"));
                                try
                                {
                                    string s = string.Join(",", kcd_mod.merged_files.file);
                                    logBuilder.AppendLine(Logger.BuildLogWithDate("Files:" + s, true));
                                    MergedFiles = s.Split(',');
                                }
                                catch (Exception e)
                                {
                                    logBuilder.AppendLine(
                                        Logger.BuildLogWithDate("List of Merged Files was faulty: " + e.Message, true));
                                }
                            }
                        }
                        else
                        {
                            logBuilder.AppendLine(Logger.BuildLogWithDate("Could not find kcd_mod directive!"));
                        }
                    }

                    logBuilder.AppendLine(Logger.BuildLogWithDate("Finished Reading Manifest!"));
                }
                else
                {
                    logBuilder.AppendLine(Logger.BuildLogWithDate("Manifest does not exist!"));
                }
            }
            else
            {
                logBuilder.AppendLine(Logger.BuildLogWithDate("No Manifest found!"));
            }

            HumanReadableInfo = GenerateHumanReadableInfo();

            if (flush)
            {
                Logger.Log(logBuilder);
            }
        }

        private string GenerateHumanReadableInfo()
        {
            logBuilder.AppendLine(
                Logger.BuildLogWithDate("Generating Human Readable Information for " + DisplayName));

            var sb = new StringBuilder();

            sb.AppendLine(DisplayName);

            if (Version != "")
            {
                sb.AppendLine(string.Concat("Version: ", Version));
            }

            if (Author != "")
            {
                sb.AppendLine(string.Concat("Author: ", Author));
            }

            if (CreatedOn != "")
            {
                sb.AppendLine(string.Concat("Created On: ", CreatedOn));
            }

            if (Description != "")
            {
                sb.AppendLine(Description);
            }

            if (VersionsSupported.Length > 0)
            {
                sb.AppendLine(string.Concat("Supported Versions:", Environment.NewLine, Environment.NewLine,
                    string.Join(Environment.NewLine + Environment.NewLine, VersionsSupported)));
            }

            if (MergedFiles.Length > 0)
            {
                sb.AppendLine(string.Concat("Merged Files:", Environment.NewLine, Environment.NewLine,
                    string.Join(Environment.NewLine + Environment.NewLine, MergedFiles)));
            }

            logBuilder.AppendLine(Logger.BuildLogWithDate("Generated Human Readable Information for " + DisplayName,
                true));

            return sb.ToString();
        }

        /// <summary>
        /// Determines whether the specified dynamic has property.
        /// </summary>
        /// <param name="dyn">The dynamic.</param>
        /// <param name="property">The property.</param>
        /// <returns>
        ///   <c>true</c> if the specified dynamic has property; otherwise, <c>false</c>.
        /// </returns>
        private bool HasProperty(dynamic dyn, string property)
        {
            if (dyn is string)
            {
                return false;
            }

            return ((IDictionary<string, object>) dyn).ContainsKey(property);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Properties

        private ModFile[] dataFiles = new ModFile[0];
        public string Author { get; set; } = "";

        public string CreatedOn { get; set; } = "";

        public string Description { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public ModFile[] Files { get; set; } = new ModFile[0];

        public string FolderName { get; set; } = "";

        public string[] Folders { get; set; } = new string[0];

        public string Version { get; set; } = "";

        public string[] VersionsSupported { get; set; } = new string[0];

        public string[] MergedFiles { get; set; } = new string[0];

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