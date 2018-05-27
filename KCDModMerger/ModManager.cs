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
        internal readonly Dictionary<string, List<string>> Conflicts = new Dictionary<string, List<string>>();
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
                Logging.Logger.Log("Clearing previously found Stuff because root folder is not valid...");
                Conflicts.Clear();
                ModFiles.Clear();
                Mods.Clear();
                Logging.Logger.Log("Cleared previously found Stuff!");

                OnPropertyChanged();
            }

            return shouldUpdate;
        }

        internal void MergeFiles()
        {
            var filesToMerge = new List<ModFile>();

            foreach (KeyValuePair<string, List<string>> conflict in Conflicts)
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

            var modMerger = new ModMerger(directoryManager.kcdMerged, filesToMerge, _mergedFiles);
        }

        internal void ChangeModStatus(string modName, ModStatus status)
        {
            var mod = Mods.FirstOrDefault(entry => entry.manifest.DisplayName == modName);

            if (mod != null)
            {
                mod.ChangeStatus(status);

                if (status == ModStatus.Disabled)
                {
                    foreach (ModFile modFile in ((IEnumerable<ModFile>)ModFiles).Reverse())
                    {
                        if (modFile.ModName == mod.manifest.DisplayName)
                        {
                            ModFiles.Remove(modFile);
                        }
                    }

                    foreach (KeyValuePair<string, List<string>> conflict in Conflicts.Reverse())
                    {
                        conflict.Value.Remove(mod.manifest.DisplayName);

                        if (conflict.Value.Count < 2)
                        {
                            Conflicts.Remove(conflict.Key);
                        }
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

                    CheckConflicts();
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

                CheckConflicts();
            }).ContinueWith(t => { OnPropertyChanged(nameof(Conflicts)); });
        }

        private void CheckConflicts()
        {
            foreach (KeyValuePair<string, List<string>> conflict in Conflicts.Reverse())
            {
                if (conflict.Value.Count < 2)
                {
                    Conflicts.Remove(conflict.Key);
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
                Logging.Logger.Log("KCD Root Path changed to " + Settings.Default.KCDPath, true);

                if (!MainWindow.isMerging)
                {
                    var isValid = Update();

                    if (!isValid)
                    {
                        Logging.Logger.Log("Chosen KCD Path is not the KCD root folder!");
                        MessageBox.Show("Chosen Path is not the root folder of KCD! " + Environment.NewLine +
                                        "It should be something like ...\\KingdomComeDeliverance!", "KCDModMerger",
                            MessageBoxButton.OK);
                    }
                }
                else
                {
                    Logging.Logger.Log("Update rejected because of ongoing merge operation!");
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
                    Logging.Logger.Log("Notifying ModManager Listeners!");
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