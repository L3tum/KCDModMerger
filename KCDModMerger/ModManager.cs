#region usings

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KCDModMerger.Annotations;
using KCDModMerger.Logging;
using KCDModMerger.Mods;
using KCDModMerger.Properties;

#endregion

namespace KCDModMerger
{
    /// <summary>
    /// Manages the Mod Interactions
    /// </summary>
    /// <seealso cref="System.ComponentModel.INotifyPropertyChanged" />
    [LogInterceptor]
    public class ModManager : INotifyPropertyChanged
    {
        internal const string VERSION = "1.4 'Ariadnes Threads'";
        internal static DirectoryManager directoryManager;
        private readonly List<string> _mergedFiles = new List<string>();

        internal readonly SortedDictionary<string, List<string>> Conflicts =
            new SortedDictionary<string, List<string>>();

        internal readonly List<ModFile> ModFiles = new List<ModFile>();
        internal readonly List<Mod> Mods = new List<Mod>();

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
            var shouldUpdate = !string.IsNullOrEmpty(Settings.Default.KCDPath) &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Mods") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Data") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Bin") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Localization") &&
                               Directory.Exists(Settings.Default.KCDPath + "\\Engine");


            if (shouldUpdate)
            {
                directoryManager = new DirectoryManager(Settings.Default.KCDPath);
                UpdateModList();
            }
            else
            {
                Logger.Log("Clearing previously found Stuff because root folder is not valid...");
                Conflicts.Clear();
                ModFiles.Clear();
                Mods.Clear();
                Logger.Log("Cleared previously found Stuff!");

                OnPropertyChanged();
            }

            return shouldUpdate;
        }

        internal void MergeFiles(bool copyAllFiles = false, bool deleteOldFiles = false)
        {
            var filesToMerge = new List<ModFile>();

            foreach (KeyValuePair<string, List<string>> conflict in Conflicts.Where(entry => entry.Value.Count > 1 && entry.Key != "Config"))
            {
                foreach (string s in conflict.Value)
                {
                    var modFile = ModFiles.FirstOrDefault(entry =>
                        entry.DisplayName == conflict.Key && entry.ModName == s);

                    if (modFile != null)
                    {
                        filesToMerge.Add(modFile);
                    }
                }
            }

            if (copyAllFiles)
            {
                foreach (ModFile modFile in ModFiles)
                {
                    if (!filesToMerge.Contains(modFile))
                    {
                        filesToMerge.Add(modFile);
                    }
                }
            }

            var modMerger = new ModMerger(directoryManager.kcdMerged, filesToMerge, _mergedFiles);

            if (deleteOldFiles)
            {
                foreach (ModFile modFile in filesToMerge)
                {
                    modFile.Delete();
                }
            }

            var configsToMerge = new List<string>();
            var configConflicts = Conflicts.Where(entry => entry.Key == "Config").ToList();

            if (configConflicts.Any())
            {
                foreach (var conflict in configConflicts[0].Value)
                {
                    configsToMerge.Add(Mods.Find(entry => entry.manifest.DisplayName == conflict).config.file);
                }
            }

            var baseFile = configsToMerge[0];
            var output = directoryManager.kcdTempMerged + "\\mod.cfg";

            foreach (string s in configsToMerge)
            {
                if (baseFile == s)
                {
                    File.Copy(s, output);
                    baseFile = output;

                    if (deleteOldFiles)
                    {
                        File.Delete(s);
                    }
                    continue;
                }

                Utilities.RunKDiff3("\"" + baseFile + "\" \"" + s + "\" -o \"" + output + "\" --auto");

                if (deleteOldFiles)
                {
                    File.Delete(s);
                }
            }

            File.Copy(output, directoryManager.kcdMerged + "\\mod.cfg", true);
        }

        internal void ChangeModStatus(string modName, ModStatus status)
        {
            var mod = Mods.FirstOrDefault(entry => entry.manifest.DisplayName == modName);

            if (mod != null)
            {
                mod.ChangeStatus(status);

                if (status == ModStatus.Disabled)
                {
                    foreach (ModFile modFile in ((IEnumerable<ModFile>) ModFiles).Reverse())
                    {
                        if (modFile.ModName == mod.manifest.DisplayName)
                        {
                            ModFiles.Remove(modFile);
                        }
                    }

                    foreach (KeyValuePair<string, List<string>> conflict in Conflicts.Reverse())
                    {
                        conflict.Value.Remove(mod.manifest.DisplayName);
                    }
                }
                else if (status == ModStatus.Enabled)
                {
                    ModFiles.AddRange(mod.DataFiles);

                    foreach (ModFile modDataFile in mod.DataFiles)
                    {
                        if (Conflicts.ContainsKey(modDataFile.DisplayName))
                        {
                            Conflicts[modDataFile.DisplayName].Add(mod.manifest.DisplayName);
                        }
                        else
                        {
                            Conflicts.Add(modDataFile.DisplayName, new List<string> {mod.manifest.DisplayName});
                        }
                    }

                    foreach (Mod md in Mods)
                    {
                        if (mod.config.Equals(md.config))
                        {
                            if (Conflicts.ContainsKey("Config"))
                            {
                                Conflicts["Config"].Add(mod.manifest.DisplayName);
                            }
                            else
                            {
                                Conflicts.Add("Config", new List<string> {mod.manifest.DisplayName});
                            }

                            break;
                        }
                    }
                }

                OnPropertyChanged(nameof(Conflicts));
            }
        }

        /// <summary>
        /// Updates the mod list.
        /// The actual Update is threaded!
        /// </summary>
        private void UpdateModList()
        {
            Conflicts.Clear();
            ModFiles.Clear();
            Mods.Clear();

            OnPropertyChanged(nameof(Conflicts));

            Task.Run(() =>
            {
                LegacyModLoader legacy =
                    new LegacyModLoader(Settings.Default.KCDPath + "//Data", directoryManager.modDirectory);
                legacy.UpdateLegacyMods();

                ModLoader loader = new ModLoader(directoryManager.modDirectory, directoryManager.disabledModDirectory);
                var mods = loader.LoadMods().ToList();
                mods.Sort((x, y) =>
                    string.CompareOrdinal(x.manifest.DisplayName, y.manifest.DisplayName));

                foreach (Mod mod in mods)
                {
                    foreach (ModFile modDataFile in mod.DataFiles)
                    {
                        if (Conflicts.ContainsKey(modDataFile.DisplayName))
                        {
                            Conflicts[modDataFile.DisplayName].Add(mod.manifest.DisplayName);
                        }
                        else
                        {
                            Conflicts.Add(modDataFile.DisplayName, new List<string> {mod.manifest.DisplayName});
                        }
                    }

                    if (mod.manifest.MergedFiles.Length > 0)
                    {
                        _mergedFiles.AddRange(mod.manifest.MergedFiles);
                    }

                    ModFiles.AddRange(mod.DataFiles);
                    Mods.Add(mod);
                }

                CheckConfigs();
            }).ContinueWith(t => { OnPropertyChanged(nameof(Conflicts)); });
        }

        private void CheckConfigs()
        {
            foreach (var t1 in Mods)
            {
                foreach (var t in Mods)
                {
                    if (t1.config.Equals(t.config))
                    {
                        if (Conflicts.ContainsKey("Config"))
                        {
                            Conflicts["Config"].Add(t1.manifest.DisplayName);
                        }
                        else
                        {
                            Conflicts.Add("Config", new List<string> {t1.manifest.DisplayName});
                        }

                        break;
                    }
                }
            }
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
        /// Called when [property changed].
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (timer == null)
            {
                timer = new Timer(state =>
                {
                    Logger.Log("Notifying ModManager Listeners!");
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    timer.Dispose();
                    timer = null;
                }, null, 1000, Timeout.Infinite);
            }
            else
            {
                timer.Change(1000, Timeout.Infinite);
            }
        }
    }
}