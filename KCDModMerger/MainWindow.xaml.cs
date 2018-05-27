#region

using System;
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
using System.Windows.Threading;
using KCDModMerger.Logging;
using KCDModMerger.Mods;
using KCDModMerger.Properties;
using KCDModMerger.UI;
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
        public readonly ObservableCollection<ItemVM> ModListItems = new ObservableCollection<ItemVM>();
        private readonly MainViewModel viewModel;
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private PerformanceCounter availableRamRounter;
        private PerformanceCounter cpuCounter;
        private PerformanceCounter diskCounter;
        private PerformanceCounter ramCounter;
        private PerformanceCounter threadsCounter;
        private Timer timer;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MainWindow" /> class.
        /// </summary>
        public MainWindow()
        {
            Logging.Logger.Log("Initializing Main Window");

            viewModel = new MainViewModel();
            DataContext = viewModel;

            // Self-documented
            InitializePerformanceWatchers();

            Logging.Logger.Log("Initializing Components");

            InitializeComponent();

            CurrentActionLabel = currentActionLabel;
            dispatcherGlobal = Dispatcher;

#if DEBUG
            testThrowExceptionButton.Visibility = Visibility.Visible;
#else
            testThrowExceptionButton.Visibility = Visibility.Hidden;
#endif

            Logging.Logger.Log("Initialized Components!");

            // Self-documented
            InitializeMergeWorker();

            ModMana = new ModManager();
            ModMana.PropertyChanged += ModManaChangeListener;

            // Self-documented
            InitializeUI();

            Logging.Logger.Log("Initialized Main Window!");
        }

        /// <summary>
        ///     Gets the modmanager.
        /// </summary>
        /// <value>
        ///     The ModManager.
        /// </value>
        public ModManager ModMana { get; }

        /// <summary>
        ///     Initializes the merge worker.
        /// </summary>
        private void InitializeMergeWorker()
        {
            Logging.Logger.Log("Initializing Merge Worker");
            worker.DoWork += WorkerDoWork;
            worker.ProgressChanged += WorkerOnProgressChanged;
            worker.RunWorkerCompleted += WorkerOnRunWorkerCompleted;
            worker.WorkerReportsProgress = true;
            Logging.Logger.Log("Initialized Merge Worker!");
        }

        /// <summary>
        ///     Initializes the UI.
        /// </summary>
        private void InitializeUI()
        {
            Logging.Logger.Log("Initializing UI");

            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Button), new FrameworkPropertyMetadata(true));

            //if statement to check for the future maybe
            conflictingModsList.Visibility = Visibility.Hidden;
            launchKdiff.DisableButton(
                "You need to select a conflicting file and a corresponding mod in the lists on the right to do this!");
            mergeButton.DisableButton("You need conflicts to merge them!");
            priorityLabel.Visibility = Visibility.Hidden;
            lowerPriorityLabel.Visibility = Visibility.Hidden;
            higherPriorityLabel.Visibility = Visibility.Hidden;
            mergeProgressBar.Visibility = Visibility.Hidden;
            mergingLabel.Visibility = Visibility.Hidden;
            toggleModButton.Visibility = Visibility.Hidden;

            conflictingModsList.PreviewMouseMove += ModsListPreviewMouseMove;

            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(AllowDropProperty, true));
            style.Setters.Add(
                new EventSetter(
                    PreviewMouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(ModsListPreviewMouseLeftButtonDown)));
            style.Setters.Add(
                new EventSetter(
                    DropEvent,
                    new DragEventHandler(ModsListDrop)));
            conflictingModsList.ItemContainerStyle = style;
            Logging.Logger.Log("Initialized UI!");
        }

        /// <summary>
        ///     Initializes the performance watchers.
        /// </summary>
        private void InitializePerformanceWatchers()
        {
            Logging.Logger.Log("Initializing Performance Watcher");
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
                Logging.Logger.LogWarn("Performance Watchers: " + e.Message, WarnSeverity.Mid);
                cpuCounter = null;
                ramCounter = null;
                availableRamRounter = null;
                diskCounter = null;
                threadsCounter = null;
            }

            Logging.Logger.Log("Initialized Performance Watcher!");
        }

        /// <summary>
        ///     Invokes if required.
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
        ///     Workers the on run worker completed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="runWorkerCompletedEventArgs">
        ///     The <see cref="RunWorkerCompletedEventArgs" /> instance containing the event
        ///     data.
        /// </param>
        private void WorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            Logging.Logger.Log("Finished Merging!");
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
        ///     Workers the on progress changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="progressChangedEventArgs">The <see cref="ProgressChangedEventArgs" /> instance containing the event data.</param>
        private void WorkerOnProgressChanged(object sender, ProgressChangedEventArgs progressChangedEventArgs)
        {
            mergeProgressBar.Value = progressChangedEventArgs.ProgressPercentage;
        }

        /// <summary>
        ///     Workers the do work.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs" /> instance containing the event data.</param>
        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            Logging.Logger.Log("Starting to merge");

            ModMana.MergeFiles(this.copyAllFilesButton.IsChecked, this.deleteOldFilesButton.IsChecked);

            Logging.Logger.Log("Finished Merge Job!");
        }

        /// <summary>
        ///     Updates the usages.
        /// </summary>
        /// <param name="state">The state.</param>
        private void UpdateUsages(object state)
        {
            if (isInformationVisible && cpuCounter != null && ramCounter != null && availableRamRounter != null &&
                diskCounter != null && threadsCounter != null)
            {
                var cpu = Math.Round(cpuCounter.NextValue()) + "%";
                var ram = Utilities.ConvertToHighest((long) ramCounter.NextValue()) + " (" +
                          Utilities.ConvertToHighest((long) availableRamRounter.NextValue()) + " Available)";
                var io = Utilities.ConvertToHighest((long) diskCounter.NextValue()) + "/sec";
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
        ///     Modsmanager change listener.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="PropertyChangedEventArgs" /> instance containing the event data.</param>
        private void ModManaChangeListener(object sender, PropertyChangedEventArgs args)
        {
            // We do not want any updates while merging. The stuff is updated afterwards anyways.
            if (!isMerging)
                if (args.PropertyName == nameof(ModMana.Mods) || args.PropertyName == nameof(ModMana.Conflicts))
                    InvokeIfRequired(UpdateUI, DispatcherPriority.Input);
        }

        /// <summary>
        ///     Updates the conflicts.
        /// </summary>
        private void UpdateUI()
        {
            viewModel.ConflictingFilesList.Clear();

            foreach (var selectedFile in ModMana.Conflicts.Keys) viewModel.ConflictingFilesList.Add(selectedFile);

            Logging.Logger.Log("Updated Conflicts to " + viewModel.ConflictingFilesList.Count + " different files!");

            if (viewModel.ConflictingFilesList.Count > 0)
                mergeButton.EnableButton();
            else
                mergeButton.DisableButton("You don't have any conflicts to merge!");

            viewModel.ModListItems.Clear();

            foreach (var modManaMod in ModMana.Mods)
                viewModel.ModListItems.Add(new ItemVM(modManaMod.manifest.DisplayName,
                    modManaMod.Status == ModStatus.Disabled ? Colors.Red : Colors.Green));
        }

        /// <summary>
        ///     Handles the Click event of the KcdFolderDialogButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void KcdFolderDialogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog {SelectedPath = textBox.Text};

            var result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK || result == System.Windows.Forms.DialogResult.Yes)
                Settings.Default.KCDPath = dialog.SelectedPath;
        }

        /// <summary>
        ///     Handles the SelectionChanged event of the modList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs" /> instance containing the event data.</param>
        private void ModListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (modList.SelectedIndex != -1)
            {
                //var selectedMod = ModMana.Mods[modList.SelectedIndex];
                if (e.RemovedItems.Count > 0)
                {
                    var modName = ((ItemVM) e.RemovedItems[0]).Text;
                    var lastMod = ModMana.Mods.FirstOrDefault(
                        entry => entry.manifest.DisplayName == modName);

                    if (lastMod != null) lastMod.manifest.PropertyChanged -= HumanReadableInfoChanged;
                }

                var nextModName = ((ItemVM) modList.SelectedItem).Text;
                var selectedMod =
                    ModMana.Mods.FirstOrDefault(mod => mod.manifest.DisplayName == nextModName);

                if (selectedMod != null)
                {
                    selectedMod.manifest.PropertyChanged += HumanReadableInfoChanged;

                    modInfo.Text = "Status: " +
                                   (ModMana.Mods[modList.SelectedIndex].Status == ModStatus.Enabled
                                       ? "Enabled"
                                       : "Disabled") + Environment.NewLine + selectedMod.manifest.HumanReadableInfo;
                    toggleModButton.Visibility = Visibility.Visible;
                    toggleModButton.Content = ModMana.Mods[modList.SelectedIndex].Status == ModStatus.Disabled
                        ? "Enable Mod"
                        : "Disable Mod";
                }
            }
            else
            {
                modInfo.Text = "";
                toggleModButton.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        ///     Humans readable information changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs" /> instance containing the event data.</param>
        private void HumanReadableInfoChanged(object sender, PropertyChangedEventArgs e)
        {
            modInfo.Text = "Status: " +
                           (ModMana.Mods[modList.SelectedIndex].Status == ModStatus.Enabled ? "Enabled" : "Disabled") +
                           Environment.NewLine + ModMana.Mods[modList.SelectedIndex].manifest.HumanReadableInfo;
        }

        /// <summary>
        ///     Handles the SelectionChanged event of the conflictFilesList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs" /> instance containing the event data.</param>
        private void ConflictFilesListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            viewModel.ConflictingModsList.Clear();

            if (conflictFilesList.SelectedIndex != -1)
            {
                var selectedFile = (string) conflictFilesList.SelectedItem;

                var conflicts = ModMana.Conflicts[selectedFile];

                foreach (var conflict in conflicts) viewModel.ConflictingModsList.Add(conflict);

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
        ///     Handles the Click event of the launchKdiff control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void LaunchKdiffClick(object sender, RoutedEventArgs e)
        {
            Logger.Log("Launching KDiff on " + conflictFilesList.SelectedItem + "(" + conflictingModsList.SelectedItem +
                       ")");
            var modFile = ModMana.ModFiles.FirstOrDefault(modfile =>
                modfile.ModName == (string) conflictingModsList.SelectedItem &&
                modfile.FileName == (string) conflictFilesList.SelectedItem);
            var modExtractedFile = ModManager.directoryManager.ExtractFile(modFile);
            var vanillaExtractedFile = new VanillaFileManager(Settings.Default.KCDPath).ExtractVanillaFile(modFile);

            if (string.IsNullOrEmpty(modExtractedFile) || string.IsNullOrEmpty(vanillaExtractedFile))
            {
                Logging.Logger.Log("Unable to locate File " + modFile.FileName + " for " +
                                   (string.IsNullOrEmpty(modExtractedFile) && string.IsNullOrEmpty(vanillaExtractedFile)
                                       ? "both"
                                       : string.IsNullOrEmpty(modExtractedFile)
                                           ? modFile.ModName
                                           : "Vanilla"), true);
                modInfo.Text = "Unable to locate File!";
                return;
            }

            Utilities.RunKDiff3("\"" + vanillaExtractedFile + "\" \"" + modExtractedFile + "\"");
        }

        /// <summary>
        ///     Handles the SelectionChanged event of the conflictingModsList control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SelectionChangedEventArgs" /> instance containing the event data.</param>
        private void ConflictingModsListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (conflictingModsList.SelectedIndex != -1)
                launchKdiff.EnableButton();
            else
                launchKdiff.DisableButton(
                    "You need to select a conflicting file and a corresponding mod in the lists on the right to do this!");
        }

        /// <summary>
        ///     Handles the Click event of the mergeButton control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void MergeButtonClick(object sender, RoutedEventArgs e)
        {
            Logger.Log("Starting Merge Worker!");

            worker.RunWorkerAsync();
            isMerging = true;
            mergingLabel.Visibility = Visibility.Visible;
            mergeButton.Visibility = Visibility.Hidden;
            options.IsExpanded = false;
            var sb = FindResource("MergingAnimation") as Storyboard;
            sb.Begin();
        }

        /// <summary>
        ///     Handles the Click event of the clearCache control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void ClearCacheClick(object sender, RoutedEventArgs e)
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
        ///     Handles the Click event of the openLogFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void OpenLogFileClick(object sender, RoutedEventArgs e)
        {
            Logging.Logger.LogToFile();
            Logging.Logger.OpenLogFile();
        }

        /// <summary>
        ///     Handles the OnCollapsed event of the AdditonalInformationExpander control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void AdditonalInformationExpander_OnCollapsed(object sender, RoutedEventArgs e)
        {
            isInformationVisible = false;
            additonalInformationExpander.Header = "Expand";
            additonalInformationExpander.ToolTip = "Additional Information";
        }

        /// <summary>
        ///     Handles the OnExpanded event of the AdditonalInformationExpander control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void AdditonalInformationExpander_OnExpanded(object sender, RoutedEventArgs e)
        {
            isInformationVisible = true;
            additonalInformationExpander.Header = "Collapse";
            additonalInformationExpander.ToolTip = null;
            UpdateUsages(null);
        }

        /// <summary>
        ///     Handles the OnLostFocus event of the TextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            Settings.Default.KCDPath = textBox.Text;
        }

        private void testThrowExceptionButton_Click(object sender, RoutedEventArgs e)
        {
            throw new Exception("This is a test");
        }

        private void toggleModButton_clicked(object sender, RoutedEventArgs e)
        {
            var selectedMod = ((ItemVM) modList.SelectedItem).Text;

            if (selectedMod != null)
            {
                ModMana.ChangeModStatus(selectedMod,
                    ((string) toggleModButton.Content).Contains("Enable") ? ModStatus.Enabled : ModStatus.Disabled);
                toggleModButton.Content = "Disable Mod";
            }
        }

        #region DragNDrop

        private Point _dragStartPoint;

        private T FindVisualParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;
            if (parentObject is T parent)
                return parent;
            return FindVisualParent<T>(parentObject);
        }

        private void ModsListPreviewMouseMove(object sender, MouseEventArgs e)
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

        private void ModsListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ModsListDrop(object sender, DragEventArgs e)
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

            if (selectedFile.Contains(".pak")) selectedFile = selectedFile.Split('\\').Last();

            if (sourceIndex < targetIndex)
            {
                ModMana.Conflicts[selectedFile].Insert(targetIndex + 1, source);
                ModMana.Conflicts[selectedFile].RemoveAt(sourceIndex);
            }
            else
            {
                var removeIndex = sourceIndex + 1;

                if (ModMana.Conflicts[selectedFile].Count + 1 > removeIndex)
                {
                    ModMana.Conflicts[selectedFile].Insert(targetIndex, source);
                    ModMana.Conflicts[selectedFile].RemoveAt(removeIndex);
                }
            }

            viewModel.ConflictingModsList.Clear();

            foreach (var s in ModMana.Conflicts[selectedFile]) viewModel.ConflictingModsList.Add(s);
        }

        #endregion
    }
}