#region usings

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KCDModMerger.Mods;
using KCDModMerger.Properties;
using Button = System.Windows.Controls.Button;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using DragEventHandler = System.Windows.DragEventHandler;
using Label = System.Windows.Controls.Label;
using ListBox = System.Windows.Controls.ListBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Timer = System.Threading.Timer;

#endregion

namespace KCDModMerger
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static Label CurrentActionLabel;
        internal static bool isMerging;
        internal static bool isInformationVisible;
        internal static Dispatcher dispatcherGlobal;
        private readonly Dictionary<string, List<ModFile>> conflicts = new Dictionary<string, List<ModFile>>();
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private PerformanceCounter availableRamRounter;
        private PerformanceCounter cpuCounter;
        private PerformanceCounter diskCounter;
        private PerformanceCounter ramCounter;
        private PerformanceCounter threadsCounter;
        private Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            Logger.Log("Initializing Main Window");

            // Self-documented
            InitializePerformanceWatchers();

            Logger.Log("Initializing Components");

            InitializeComponent();

            CurrentActionLabel = currentActionLabel;
            dispatcherGlobal = Dispatcher;

            Logger.Log("Initialized Components!");

            // Self-documented
            InitializeMergeWorker();

            Logger.Log("Starting ModManager");
            ModMana = new ModManager();
            Logger.Log("Started ModManager!");

            Logger.Log("Assigning ModManager Listeners");
            ModMana.PropertyChanged += ModManaChangeListener;
            Logger.Log("Assigned ModManager Listeners!");

            // Self-documented
            InitializeUI();

            // Self-documented
            UpdateConflicts();

            Logger.Log("Initialized Main Window!");
        }

        /// <summary>
        /// Gets the modmanager.
        /// </summary>
        /// <value>
        /// The ModManager.
        /// </value>
        public ModManager ModMana { get; }

        /// <summary>
        /// Initializes the merge worker.
        /// </summary>
        private void InitializeMergeWorker()
        {
            Logger.Log("Initializing Merge Worker");
            worker.DoWork += WorkerDoWork;
            worker.ProgressChanged += WorkerOnProgressChanged;
            worker.RunWorkerCompleted += WorkerOnRunWorkerCompleted;
            worker.WorkerReportsProgress = true;
            Logger.Log("Initialized Merge Worker!");
        }

        /// <summary>
        /// Initializes the UI.
        /// </summary>
        private void InitializeUI()
        {
            Logger.Log("Initializing UI");

            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Button), new FrameworkPropertyMetadata(true));

            conflictFilesList.DataContext = this;
            conflictFilesList.ItemsSource = ModMana.Conflicts;

            var source = FindResource("ModNamesVS") as CollectionViewSource;
            source.Source = ModMana.ModNames;

            //if statement to check for the future maybe
            conflictingModsList.Visibility = Visibility.Hidden;
            launchKdiff.DisableButton(
                "You need to select a conflicting file and a corresponding mod in the lists on the right to do this!");
            priorityLabel.Visibility = Visibility.Hidden;
            lowerPriorityLabel.Visibility = Visibility.Hidden;
            higherPriorityLabel.Visibility = Visibility.Hidden;
            mergeProgressBar.Visibility = Visibility.Hidden;
            mergingLabel.Visibility = Visibility.Hidden;

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
            Logger.Log("Initialized UI!");
        }

        /// <summary>
        /// Initializes the performance watchers.
        /// </summary>
        private void InitializePerformanceWatchers()
        {
            Logger.Log("Initializing Performance Watcher");
            try
            {
                cpuCounter = new PerformanceCounter("Process", "% Processor Time",
                    Process.GetCurrentProcess().ProcessName);
                ramCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
                availableRamRounter = new PerformanceCounter("Memory", "Available Bytes");
                diskCounter =
                    new PerformanceCounter("Process", "IO Data Bytes/sec", Process.GetCurrentProcess().ProcessName);
                threadsCounter = new PerformanceCounter(".Net CLR LocksAndThreads", "# of current logical Threads",
                    Process.GetCurrentProcess().ProcessName);
                cpuCounter.NextValue();
                ramCounter.NextValue();
                availableRamRounter.NextValue();
                diskCounter.NextValue();
                threadsCounter.NextValue();
                timer = new Timer(UpdateUsages, null, 0, 1000);
            }
            catch (InvalidOperationException e)
            {
                Logger.Log("Performance Watchers: " + e.Message);
            }
            finally
            {
                cpuCounter = null;
                ramCounter = null;
                availableRamRounter = null;
                diskCounter = null;
                threadsCounter = null;
            }
            Logger.Log("Initialized Performance Watcher!");
        }

        /// <summary>
        /// Invokes if required.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="priority">The priority.</param>
        private void InvokeIfRequired(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            if (!Dispatcher.CheckAccess())
                Dispatcher.Invoke(action, priority);
            else
                action.Invoke();
        }

        /// <summary>
        /// Workers the on run worker completed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="runWorkerCompletedEventArgs">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void WorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            Logger.Log("Finished Merging!");
            isMerging = false;
            mergeProgressBar.Visibility = Visibility.Hidden;
            mergeButton.Visibility = Visibility.Visible;
            var sb = FindResource("MergingAnimation") as Storyboard;
            sb.Stop();
            mergingLabel.Content = "Finished Merging!";
            InvokeIfRequired(() =>
            {
                ModMana.Update();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            });
            Task.Delay(5000).ContinueWith(t =>
            {
                Dispatcher.Invoke(() => { mergingLabel.Visibility = Visibility.Hidden; },
                    DispatcherPriority.ApplicationIdle);
            });
        }

        /// <summary>
        /// Workers the on progress changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="progressChangedEventArgs">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void WorkerOnProgressChanged(object sender, ProgressChangedEventArgs progressChangedEventArgs)
        {
            mergeProgressBar.Value = progressChangedEventArgs.ProgressPercentage;
        }

        /// <summary>
        /// Workers the do work.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            Logger.Log("Starting to merge");
            var done = 0;
            var filesToMerge = new List<ModFile>();

            foreach (var conflict in conflicts)
            {
                if (conflict.Value != null)
                {
                    filesToMerge.AddRange(conflict.Value);
                }
            }

            if (Settings.Default.CopyAllFiles)
            {
                foreach (ModFile modManaModFile in ModMana.ModFiles)
                {
                    filesToMerge.Add(modManaModFile);
                }
            }

            filesToMerge = filesToMerge.Distinct().ToList();

            Logger.Log("Merging " + filesToMerge.Count + " files");

            var modExtractedFiles = new Dictionary<string, string>();

            worker.ReportProgress(0);

            foreach (var fileToMerge in filesToMerge)
            {
                if (fileToMerge != null)
                {
                    string baseFile = !modExtractedFiles.ContainsKey(fileToMerge.FileName)
                        ? ModMana.ExtractVanillaFile(fileToMerge)
                        : modExtractedFiles[fileToMerge.FileName];

                    if (baseFile == "" || baseFile == "")
                    {
                        Logger.Log("No base file found for " + fileToMerge.FileName, true);
                    }

                    // Sufficiently documented in Subroutine
                    var extractedFile = ModMana.ExtractFile(fileToMerge);

                    // Sufficiently documented in Subroutine
                    var outputFile = ModMana.MergeFiles(baseFile, extractedFile);

                    modExtractedFiles[fileToMerge.FileName] = outputFile;

                    if (Settings.Default.DeleteOldFiles) fileToMerge.Delete();

                    done++;
                    worker.ReportProgress((int) (done / (float) filesToMerge.Count * 100.0f));
                }
                else
                {
                    Logger.Log("ModFile was null for some reason!");
                }
            }

            // Documented in Subroutines
            ModMana.PakData();
            done++;
            worker.ReportProgress((int) (done / (float) filesToMerge.Count * 100.0f));
            ModMana.PakLocalization();
            done++;
            worker.ReportProgress((int) (done / (float) filesToMerge.Count * 100.0f));
            ModMana.CopyMergedToMods();
            done++;
            worker.ReportProgress((int) (done / (float) filesToMerge.Count * 100.0f));

            Logger.Log("Finished Merge Job!");
        }

        /// <summary>
        /// Updates the usages.
        /// </summary>
        /// <param name="state">The state.</param>
        private void UpdateUsages(object state)
        {
            if (isInformationVisible && cpuCounter != null && ramCounter != null && availableRamRounter != null && diskCounter != null && threadsCounter != null)
            {
                var cpu = Math.Round(cpuCounter.NextValue()) + "%";
                var ram = App.ConvertToHighest((long) ramCounter.NextValue()) + " (" +
                          App.ConvertToHighest((long) availableRamRounter.NextValue()) + " Available)";
                var io = App.ConvertToHighest((long) diskCounter.NextValue()) + "/sec";
                var threads = threadsCounter.NextValue() + " Threads (" + Process.GetCurrentProcess().Threads.Count +
                              " Including CLR)";

                InvokeIfRequired(() =>
                {
                    cpuUsage.Content = cpu;
                    ramUsage.Content = ram;
                    ioUsage.Content = io;
                    threadsCounterValue.Content = threads;
                });
            }
        }

        /// <summary>
        /// Modsmanager change listener.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void ModManaChangeListener(object sender, PropertyChangedEventArgs args)
        {
            // We do not want any updates while merging. The stuff is updated afterwards anyways.
            if (!isMerging)
            {
                if (args.PropertyName == nameof(ModMana.ModFiles) || args.PropertyName == nameof(ModMana.Conflicts))
                {
                    UpdateConflicts();
                }
            }
        }

        /// <summary>
        /// Updates the conflicts.
        /// </summary>
        private void UpdateConflicts()
        {
            Logger.Log("Updating Conflicts");

            foreach (var selectedFile in ModMana.Conflicts)
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

            Logger.Log("Updated Conflicts to " + conflicts.Count + " different files!");

            if (conflicts.Count > 0)
            {
                mergeButton.EnableButton();
            }
            else
            {
                mergeButton.DisableButton("You don't have any conflicts to merge!");
            }
        }

        /// <summary>
        /// Handles the Click event of the KcdFolderDialogButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void KcdFolderDialogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog {SelectedPath = textBox.Text};

            var result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK || result == System.Windows.Forms.DialogResult.Yes)
            {
                Settings.Default.KCDPath = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Handles the SelectionChanged event of the modList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing the event data.</param>
        private void modList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (modList.SelectedIndex != -1)
            {
                //var selectedMod = ModMana.Mods[modList.SelectedIndex];
                if (e.RemovedItems.Count > 0)
                {
                    var lastIndex = ModMana.ModNames.IndexOf((string) e.RemovedItems[0]);
                    ModMana.Mods[lastIndex].manifest.PropertyChanged -= humanReadableInfoChanged;
                }

                var selectedMod =
                    ModMana.Mods.FirstOrDefault(mod => mod.manifest.DisplayName == (string) modList.SelectedItem);

                if (selectedMod != null)
                {
                    selectedMod.manifest.PropertyChanged += humanReadableInfoChanged;

                    modInfo.Text = selectedMod.manifest.HumanReadableInfo;
                }
            }
        }

        /// <summary>
        /// Humans readable information changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void humanReadableInfoChanged(object sender, PropertyChangedEventArgs e)
        {
            modInfo.Text = ModMana.Mods[modList.SelectedIndex].manifest.HumanReadableInfo;
        }

        /// <summary>
        /// Handles the SelectionChanged event of the conflictFilesList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing the event data.</param>
        private void conflictFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (conflictFilesList.SelectedIndex != -1)
            {
                var selectedFile = (string) conflictFilesList.SelectedItem;

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

        /// <summary>
        /// Handles the Click event of the launchKdiff control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
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

        /// <summary>
        /// Handles the SelectionChanged event of the conflictingModsList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs"/> instance containing the event data.</param>
        private void conflictingModsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (conflictingModsList.SelectedIndex != -1)
                launchKdiff.EnableButton();
            else
                launchKdiff.DisableButton(
                    "You need to select a conflicting file and a corresponding mod in the lists on the right to do this!");
        }

        /// <summary>
        /// Handles the Click event of the mergeButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void mergeButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Starting Merge Worker!");

            worker.RunWorkerAsync();
            isMerging = true;
            mergeProgressBar.Visibility = Visibility.Visible;
            mergingLabel.Visibility = Visibility.Visible;
            mergeButton.Visibility = Visibility.Hidden;
            options.IsExpanded = false;
            var sb = FindResource("MergingAnimation") as Storyboard;
            sb.Begin();
        }

        /// <summary>
        /// Handles the Click event of the clearCache control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void clearCache_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Reset();

            var originalContentTM = clearCache.Content;
            clearCache.Content = "Cleared!";

            Task.Delay(5000)
                .ContinueWith(t =>
                {
                    Dispatcher.Invoke(() => { clearCache.Content = originalContentTM; },
                        DispatcherPriority.ApplicationIdle);
                });
        }

        /// <summary>
        /// Handles the Click event of the openLogFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void openLogFile_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogToFile(null);

            Process.Start(@"" + Logger.LOG_FILE);
        }

        /// <summary>
        /// Handles the OnCollapsed event of the AdditonalInformationExpander control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void AdditonalInformationExpander_OnCollapsed(object sender, RoutedEventArgs e)
        {
            isInformationVisible = false;
            additonalInformationExpander.Header = "Expand";
            additonalInformationExpander.ToolTip = "Additional Information";
        }

        /// <summary>
        /// Handles the OnExpanded event of the AdditonalInformationExpander control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void AdditonalInformationExpander_OnExpanded(object sender, RoutedEventArgs e)
        {
            isInformationVisible = true;
            additonalInformationExpander.Header = "Collapse";
            additonalInformationExpander.ToolTip = null;
            UpdateUsages(null);
        }

        /// <summary>
        /// Handles the OnLostFocus event of the TextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            Settings.Default.KCDPath = textBox.Text;
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
            var selectedFile = (string) conflictFilesList.SelectedItem;
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

            conflictingModsList.ItemsSource = conflicts[selectedFile].Select(file => file.ModName);
        }

        #endregion
    }
}