using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KCDModMerger.Properties;
using Newtonsoft.Json;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using DragEventHandler = System.Windows.DragEventHandler;
using Label = System.Windows.Controls.Label;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Timer = System.Threading.Timer;

namespace KCDModMerger
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static Label CurrentActionLabel;
        private readonly ObservableCollection<string> files = new ObservableCollection<string>();
        private readonly ObservableCollection<string> modNames = new ObservableCollection<string>();
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private readonly PerformanceCounter availableRamRounter;
        private Dictionary<string, List<ModFile>> conflicts;
        private readonly PerformanceCounter cpuCounter;
        private bool deleteOldFiles;
        private readonly PerformanceCounter diskCounter;
        private bool isMerging;
        private readonly PerformanceCounter ramCounter;
        private Timer timer;

        public MainWindow()
        {
            Logger.Log("Initializing Main Window");

            Logger.Log("Initializing Performance Watcher");
            cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
            ramCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
            availableRamRounter = new PerformanceCounter("Memory", "Available Bytes");
            diskCounter = new PerformanceCounter("Process", "IO Data Bytes/sec",
                Process.GetCurrentProcess().ProcessName);
            cpuCounter.NextValue();
            ramCounter.NextValue();
            availableRamRounter.NextValue();
            diskCounter.NextValue();
            timer = new Timer(UpdateUsages, null, 0, 1000);
            Logger.Log("Initialized Performance Watcher!");

            Logger.Log("Loading Saved Conflicts");
            conflicts =
                JsonConvert.DeserializeObject<Dictionary<string, List<ModFile>>>(Settings.Default
                    .Conflicts);
            Logger.Log("Loaded Saved Conflicts!");
            if (conflicts == null)
            {
                Logger.Log("No Saved Conflicts found!");
                conflicts = new Dictionary<string, List<ModFile>>();
            }

            Logger.Log("Initializing Components");

            InitializeComponent();

            CurrentActionLabel = currentActionLabel;

            Logger.Log("Initialized Components!");

            worker.DoWork += WorkerDoWork;
            worker.ProgressChanged += WorkerOnProgressChanged;
            worker.RunWorkerCompleted += WorkerOnRunWorkerCompleted;
            worker.WorkerReportsProgress = true;


            modList.DataContext = this;
            modList.ItemsSource = modNames;

            ModMana.PropertyChanged += UpdateStuff;

            conflictFilesList.DataContext = this;
            conflictFilesList.ItemsSource = files;

            //if statement to check for the future maybe
            conflictingModsList.Visibility = Visibility.Hidden;
            launchKdiff.Visibility = Visibility.Hidden;
            priorityLabel.Visibility = Visibility.Hidden;
            lowerPriorityLabel.Visibility = Visibility.Hidden;
            higherPriorityLabel.Visibility = Visibility.Hidden;
            mergeProgressBar.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;
            mergingLabel.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;

            conflictingModsList.PreviewMouseMove += modsList_PreviewMouseMove;

            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(AllowDropProperty, true));
            style.Setters.Add(
                new EventSetter(
                    PreviewMouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(modsList_PreviewMouseLeftButtonDown)));
            style.Setters.Add(
                new EventSetter(
                    DropEvent,
                    new DragEventHandler(modsList_Drop)));
            conflictingModsList.ItemContainerStyle = style;

            UpdateStuff(null, null);

            Logger.Log("Initialized Main Window!");
        }

        private ModManager ModMana { get; } = new ModManager();

        public IEnumerable<string> ModNames
        {
            get => modNames;
            set
            {
                modNames.Clear();
                foreach (var s in value) modNames.Add(s);

                modList.Visibility = modNames.Count > 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public IEnumerable<string> Files
        {
            get => files;
            set
            {
                files.Clear();
                foreach (var s in value) files.Add(s);

                conflictFilesList.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Hidden;
                conflictFilesLabel.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void WorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            Logger.Log("Finished Merging!");
            isMerging = false;
            mergeProgressBar.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;
            var sb = FindResource("MergingAnimation") as Storyboard;
            sb.Stop();
            mergingLabel.Content = "Finished Merging!";
        }

        private void WorkerOnProgressChanged(object sender, ProgressChangedEventArgs progressChangedEventArgs)
        {
            mergeProgressBar.Value = progressChangedEventArgs.ProgressPercentage;
        }

        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            Logger.Log("Starting to merge");
            var count = 3;
            var done = 0;

            foreach (var conflict in conflicts) count += conflict.Value.Count;

            Logger.Log("Merging " + count + " files");

            worker.ReportProgress(0);

            foreach (var keyValuePair in conflicts)
            {
                Logger.Log("Trying to find ModFile for " + keyValuePair.Value[0].ModName + " " +
                           keyValuePair.Value[0].FileName);
                var foundModFile = ModMana.ModFiles.FirstOrDefault(modfile =>
                    modfile.ModName == keyValuePair.Value[0].ModName &&
                    modfile.FileName == keyValuePair.Value[0].FileName);
                var vanillaExtractedFile = ModMana.ExtractVanillaFile(foundModFile);

                if (foundModFile == null) Logger.Log("Could not find ModFile...This is bad!");

                var modExtractedFiles = new List<string>();
                var actualFiles = new List<ModFile>();

                // Sufficiently documented in Subroutines
                foreach (var modFile in keyValuePair.Value)
                {
                    var actualFile = ModMana.ModFiles.FirstOrDefault(file =>
                        file.ModName == modFile.ModName && file.FileName == modFile.FileName);
                    actualFiles.Add(actualFile);
                    modExtractedFiles.Add(ModMana.ExtractFile(actualFile));
                }

                var outputFile = ModMana.MergeFiles(vanillaExtractedFile, modExtractedFiles[0]);

                if (deleteOldFiles) actualFiles[0].Delete();

                done++;
                worker.ReportProgress((int) (done / (float) count * 100.0f));

                for (var i = 1; i < modExtractedFiles.Count; i++)
                {
                    outputFile = ModMana.MergeFiles(outputFile, modExtractedFiles[i]);
                    actualFiles[i].Delete();
                    done++;
                    worker.ReportProgress((int) (done / (float) count * 100.0f));
                }
            }

            // Documented in Subroutines
            ModMana.PakData();
            done++;
            worker.ReportProgress((int) (done / (float) count * 100.0f));
            ModMana.PakLocalization();
            done++;
            worker.ReportProgress((int) (done / (float) count * 100.0f));
            ModMana.CopyMergedToMods();
            done++;
            worker.ReportProgress((int) (done / (float) count * 100.0f));
            Logger.Log("Finished Merge Job!");
            Dispatcher.Invoke(() => { ModMana.Update(); });
        }

        private void UpdateUsages(object state)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    cpuUsage.Content = Math.Round(cpuCounter.NextValue()) + "%";
                    ramUsage.Content = App.ConvertToHighest((long) ramCounter.NextValue()) + " (" +
                                       App.ConvertToHighest((long) availableRamRounter.NextValue()) + " Available)";
                    ioUsage.Content = App.ConvertToHighest((long) diskCounter.NextValue()) + "/sec";
                });
            }
            else
            {
                cpuUsage.Content = Math.Round(cpuCounter.NextValue()) + "%";
                ramUsage.Content = App.ConvertToHighest((long) ramCounter.NextValue()) + " (" +
                                   App.ConvertToHighest((long) availableRamRounter.NextValue()) + " Available)";
                ioUsage.Content = diskCounter.NextValue().ToString();
            }
        }

        private void UpdateStuff(object sender, PropertyChangedEventArgs e)
        {
            Logger.Log("Updating Stuff");
            ModNames = ModMana.ModNames;
            Files = ModMana.Conflicts;
            Logger.Log("Found " + ModMana.ModNames.Count + " Mods!");
            Logger.Log("Found " + ModMana.Conflicts.Count + " Conflicts!");

            Logger.Log("Updating saved conflicts");

            foreach (var selectedFile in files)
            {
                var modF = ModMana.ModFiles.Where(file => file.FileName.Equals(selectedFile));

                if (!conflicts.ContainsKey(selectedFile))
                {
                    conflicts.Add(selectedFile, new List<ModFile>(modF));
                    Logger.Log("Added " + selectedFile + " with " + conflicts[selectedFile].Count + " Conflicts!");
                }
                else
                {
                    foreach (var file in modF)
                        if (!conflicts[selectedFile].Contains(file))
                        {
                            Logger.Log("Adding " + file + " to " + selectedFile);
                            conflicts[selectedFile].Add(file);
                        }
                }
            }

            var tempConflicts = new Dictionary<string, List<ModFile>>();

            foreach (var conflict in conflicts)
                if (files.Contains(conflict.Key))
                {
                    tempConflicts.Add(conflict.Key, new List<ModFile>());

                    foreach (var modFile in conflict.Value)
                        if (ModMana.ModFiles.Contains(modFile))
                            tempConflicts[conflict.Key].Add(modFile);
                        else
                            Logger.Log(modFile.FileName + " has no longer any conflicts!");
                }
                else
                {
                    Logger.Log(conflict.Key + " has no longer any conflicts!");
                }

            conflicts = tempConflicts;

            Logger.Log("Updated Conflicts to " + conflicts.Count + " different files!");
            Logger.Log("Saving Conflicts");

            Settings.Default.Conflicts = JsonConvert.SerializeObject(conflicts);
            Settings.Default.Save();

            Logger.Log("Saved Conflicts!");
            Logger.Log("Updated Stuff!");
        }


        private void ShowFolderDialog()
        {
            var dialog = new FolderBrowserDialog {SelectedPath = textBox.Text};

            var result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK || result == System.Windows.Forms.DialogResult.Yes)
            {
                Settings.Default.KCDPath = dialog.SelectedPath;
                Settings.Default.Save();
            }
        }

        private void KcdFolderDialogButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFolderDialog();
        }

        private void modList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (modList.SelectedIndex != -1)
            {
                var selectedMod = ModMana.Mods[modList.SelectedIndex];

                modInfo.Text = selectedMod.DisplayName + Environment.NewLine + "Version: " + selectedMod.Version +
                               Environment.NewLine + "Author: " +
                               selectedMod.Author + Environment.NewLine + "Created On: " + selectedMod.CreatedOn +
                               Environment.NewLine +
                               selectedMod.Description + Environment.NewLine + "Merged Files:" + Environment.NewLine +
                               Environment.NewLine + (selectedMod.MergedFiles.Length > 0
                                   ? string.Join(Environment.NewLine + Environment.NewLine, selectedMod.MergedFiles)
                                   : "");
            }
        }

        private void conflictFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (conflictFilesList.SelectedIndex != -1)
            {
                var selectedFile = files[conflictFilesList.SelectedIndex];

                conflictingModsList.ItemsSource = conflicts[selectedFile].Select(file => file.ModName);
                conflictingModsList.Visibility = Visibility.Visible;
                priorityLabel.Visibility = Visibility.Visible;
                lowerPriorityLabel.Visibility = Visibility.Visible;
                higherPriorityLabel.Visibility = Visibility.Visible;
            }
            else
            {
                conflictingModsList.Visibility = Visibility.Hidden;
                priorityLabel.Visibility = Visibility.Hidden;
                lowerPriorityLabel.Visibility = Visibility.Hidden;
                higherPriorityLabel.Visibility = Visibility.Hidden;
            }
        }

        private void launchKdiff_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Launching KDiff on " + conflictFilesList.SelectedItem + "(" + conflictingModsList.SelectedItem +
                       ")");
            var modFile = ModMana.ModFiles.FirstOrDefault(modfile =>
                modfile.ModName == (string) conflictingModsList.SelectedItem &&
                modfile.FileName == (string) conflictFilesList.SelectedItem);
            var modExtractedFile = ModMana.ExtractFile(modFile);
            var vanillaExtractedFile = ModMana.ExtractVanillaFile(modFile);

            if (modExtractedFile == string.Empty || vanillaExtractedFile == string.Empty)
            {
                Logger.Log("Unable to locate File " + modFile.FileName + " for " +
                           (modExtractedFile == string.Empty && vanillaExtractedFile == string.Empty ? "both" :
                               modExtractedFile == string.Empty ? modFile.ModName : "Vanilla"), true);
                modInfo.Text = "Unable to locate File!";
                return;
            }

            ModMana.RunKDiff3("\"" + vanillaExtractedFile + "\" \"" + modExtractedFile + "\"");
        }

        private void conflictingModsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (conflictingModsList.SelectedIndex != -1)
                launchKdiff.Visibility = Visibility.Visible;
            else
                launchKdiff.Visibility = Visibility.Hidden;
        }

        private void mergeButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Starting Merge Worker!");
            Logger.Log("Asking user if they want to delete old files");
            var result = MessageBox.Show("Do you want to delete the files after they were merged?", "KCDModMerger",
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.OK || result == MessageBoxResult.Yes)
                deleteOldFiles = true;
            else
                deleteOldFiles = false;

            Logger.Log("Delete Old Files: " + deleteOldFiles, true);

            worker.RunWorkerAsync();
            isMerging = true;
            mergeProgressBar.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;
            mergingLabel.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;
            var sb = FindResource("MergingAnimation") as Storyboard;
            sb.Begin();
        }

        private void clearCache_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Reset();

            var originalContentTM = clearCache.Content;
            clearCache.Content = "Cleared!";

            Task.Delay(5000)
                .ContinueWith(t => { Dispatcher.Invoke(() => { clearCache.Content = originalContentTM; }); });
        }

        private void openLogFile_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogToFile(null);

            Process.Start(@"" + Logger.LOG_FILE);
        }

        #region DragNDrop

        private Point _dragStartPoint;

        private T FindVisualParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;
            var parent = parentObject as T;
            if (parent != null)
                return parent;
            return FindVisualParent<T>(parentObject);
        }

        private void modsList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var point = e.GetPosition(null);
            var diff = _dragStartPoint - point;
            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var lb = sender as ListBox;
                var lbi = FindVisualParent<ListBoxItem>((DependencyObject) e.OriginalSource);
                if (lbi != null) DragDrop.DoDragDrop(lbi, lbi.DataContext, DragDropEffects.Move);
            }
        }

        private void modsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void modsList_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem)
            {
                var source = e.Data.GetData(typeof(string)) as string;
                var target = ((ListBoxItem) sender).DataContext as string;

                var sourceIndex = conflictingModsList.Items.IndexOf(source);
                var targetIndex = conflictingModsList.Items.IndexOf(target);

                Move(source, sourceIndex, targetIndex);
            }
        }

        private void Move(string source, int sourceIndex, int targetIndex)
        {
            var selectedFile = files[conflictFilesList.SelectedIndex];
            var sourceModFile = conflicts[selectedFile].FirstOrDefault(file => file.ModName == source);
            if (sourceIndex < targetIndex)
            {
                conflicts[selectedFile].Insert(targetIndex + 1, sourceModFile);
                conflicts[selectedFile].RemoveAt(sourceIndex);
            }
            else
            {
                var removeIndex = sourceIndex + 1;
                if (conflicts[selectedFile].Count + 1 > removeIndex)
                {
                    conflicts[selectedFile].Insert(targetIndex, sourceModFile);
                    conflicts[selectedFile].RemoveAt(removeIndex);
                }
            }

            Settings.Default.Conflicts = JsonConvert.SerializeObject(conflicts);
            Settings.Default.Save();

            conflictingModsList.ItemsSource = conflicts[selectedFile].Select(file => file.ModName);
        }

        #endregion
    }
}