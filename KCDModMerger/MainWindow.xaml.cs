using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KCDModMerger.Properties;
using Newtonsoft.Json;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace KCDModMerger
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> files = new ObservableCollection<string>();
        private readonly ObservableCollection<string> modNames = new ObservableCollection<string>();
        private readonly Dictionary<string, List<ModFile>> conflicts;
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private ModManager ModMana { get; } = new ModManager();
        private bool isMerging = false;

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
                foreach (var s in value)
                {
                    files.Add(s);
                }

                conflictFilesList.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Hidden;
                conflictFilesLabel.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }


        public MainWindow()
        {
            conflicts =
                JsonConvert.DeserializeObject<Dictionary<string, List<ModFile>>>(Properties.Settings.Default
                    .Conflicts);
            if (conflicts == null)
            {
                conflicts = new Dictionary<string, List<ModFile>>();
            }

            DataContext = this;

            InitializeComponent();


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
            style.Setters.Add(new Setter(ListBoxItem.AllowDropProperty, true));
            style.Setters.Add(
                new EventSetter(
                    ListBoxItem.PreviewMouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(modsList_PreviewMouseLeftButtonDown)));
            style.Setters.Add(
                new EventSetter(
                    ListBoxItem.DropEvent,
                    new System.Windows.DragEventHandler(modsList_Drop)));
            conflictingModsList.ItemContainerStyle = style;

            UpdateStuff(null, null);
        }

        private void WorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            isMerging = false;
            mergeProgressBar.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;
            Storyboard sb = this.FindResource("MergingAnimation") as Storyboard;
            sb.Stop();
            mergingLabel.Content = "Finished Merging!";
        }

        private void WorkerOnProgressChanged(object sender, ProgressChangedEventArgs progressChangedEventArgs)
        {
            mergeProgressBar.Value = progressChangedEventArgs.ProgressPercentage;
        }

        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            var count = 3;
            var done = 0;

            foreach (KeyValuePair<string, List<ModFile>> keyValuePair in conflicts)
            {
                count += keyValuePair.Value.Count;
            }

            worker.ReportProgress(0);

            foreach (KeyValuePair<string, List<ModFile>> keyValuePair in conflicts)
            {
                var foundModFile = this.ModMana.ModFiles.First(modfile =>
                    modfile.ModName == keyValuePair.Value[0].ModName &&
                    modfile.FileName == keyValuePair.Value[0].FileName);
                var vanillaExtractedFile = ModMana.ExtractVanillaFile(foundModFile);
                List<string> modExtractedFiles = new List<string>();

                foreach (ModFile modFile in keyValuePair.Value)
                {
                    var actualFile = this.ModMana.ModFiles.First(file =>
                        file.ModName == modFile.ModName && file.FileName == modFile.FileName);
                    modExtractedFiles.Add(ModMana.ExtractFile(actualFile));
                }

                var outputFile = ModMana.MergeFiles(vanillaExtractedFile, modExtractedFiles[0]);
                done++;
                worker.ReportProgress((int) (((float) done / (float) count) * 100.0f));

                for (int i = 1; i < modExtractedFiles.Count; i++)
                {
                    outputFile = ModMana.MergeFiles(outputFile, modExtractedFiles[i]);
                    done++;
                    worker.ReportProgress((int) (((float) done / (float) count) * 100.0f));
                }
            }

            ModMana.PakData();
            done++;
            worker.ReportProgress((int) (((float) done / (float) count) * 100.0f));
            ModMana.PakLocalization();
            done++;
            worker.ReportProgress((int) (((float) done / (float) count) * 100.0f));
            ModMana.CopyMergedToMods();
            done++;
            worker.ReportProgress((int) (((float) done / (float) count) * 100.0f));
        }

        #region DragNDrop

        private Point _dragStartPoint;

        private T FindVisualParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            return FindVisualParent<T>(parentObject);
        }

        private void modsList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point point = e.GetPosition(null);
            Vector diff = _dragStartPoint - point;
            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var lb = sender as System.Windows.Controls.ListBox;
                var lbi = FindVisualParent<ListBoxItem>(((DependencyObject) e.OriginalSource));
                if (lbi != null)
                {
                    DragDrop.DoDragDrop(lbi, lbi.DataContext, DragDropEffects.Move);
                }
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
                var target = ((ListBoxItem) (sender)).DataContext as string;

                int sourceIndex = conflictingModsList.Items.IndexOf(source);
                int targetIndex = conflictingModsList.Items.IndexOf(target);

                Move(source, sourceIndex, targetIndex);
            }
        }

        private void Move(string source, int sourceIndex, int targetIndex)
        {
            var selectedFile = files[conflictFilesList.SelectedIndex];
            var sourceModFile = conflicts[selectedFile].First(file => file.ModName == source);
            if (sourceIndex < targetIndex)
            {
                conflicts[selectedFile].Insert(targetIndex + 1, sourceModFile);
                conflicts[selectedFile].RemoveAt(sourceIndex);
            }
            else
            {
                int removeIndex = sourceIndex + 1;
                if (conflicts[selectedFile].Count + 1 > removeIndex)
                {
                    conflicts[selectedFile].Insert(targetIndex, sourceModFile);
                    conflicts[selectedFile].RemoveAt(removeIndex);
                }
            }

            Properties.Settings.Default.Conflicts = JsonConvert.SerializeObject(conflicts);
            Settings.Default.Save();

            conflictingModsList.ItemsSource = conflicts[selectedFile].Select((file) => file.ModName);
        }

        #endregion

        private void UpdateStuff(object sender, PropertyChangedEventArgs e)
        {
            ModNames = ModMana.ModNames;
            Files = ModMana.Conflicts;
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

                modInfo.Text = selectedMod.DisplayName + " " + selectedMod.Version + "\n" + "Author: " +
                               selectedMod.Author + "\n" + "Created On: " + selectedMod.CreatedOn + "\n" +
                               selectedMod.Description;
            }
        }

        private void conflictFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (conflictFilesList.SelectedIndex != -1)
            {
                var selectedFile = files[conflictFilesList.SelectedIndex];
                var modF = ModMana.ModFiles.Where((file => file.FileName.Equals(selectedFile)))
                    .Select((file => file));

                if (!conflicts.ContainsKey(selectedFile))
                {
                    conflicts.Add(selectedFile, new List<ModFile>(modF));
                }
                else
                {
                    foreach (ModFile file in modF)
                    {
                        if (!conflicts[selectedFile].Contains(file))
                        {
                            conflicts[selectedFile].Add(file);
                        }
                    }
                }

                Properties.Settings.Default.Conflicts = JsonConvert.SerializeObject(conflicts);
                Settings.Default.Save();

                conflictingModsList.ItemsSource = conflicts[selectedFile].Select((file => file.ModName));
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
            var modFile = this.ModMana.ModFiles.First(modfile =>
                modfile.ModName == (string) conflictingModsList.SelectedItem &&
                modfile.FileName == (string) conflictFilesList.SelectedItem);
            var modExtractedFile = ModMana.ExtractFile(modFile);
            var vanillaExtractedFile = ModMana.ExtractVanillaFile(modFile);

            if (modExtractedFile == String.Empty || vanillaExtractedFile == String.Empty)
            {
                this.modInfo.Text = "Unable to locate File!";
                return;
            }

            ModMana.RunKDiff3("\"" + vanillaExtractedFile + "\" \"" + modExtractedFile + "\"");
        }

        private void conflictingModsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (conflictingModsList.SelectedIndex != -1)
            {
                launchKdiff.Visibility = Visibility.Visible;
            }
            else
            {
                launchKdiff.Visibility = Visibility.Hidden;
            }
        }

        private void mergeButton_Click(object sender, RoutedEventArgs e)
        {
            worker.RunWorkerAsync();
            isMerging = true;
            mergeProgressBar.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;
            mergingLabel.Visibility = isMerging ? Visibility.Visible : Visibility.Hidden;
            Storyboard sb = this.FindResource("MergingAnimation") as Storyboard;
            sb.Begin();
        }
    }
}