#region

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KCDModMerger.Annotations;

#endregion

namespace KCDModMerger.UI
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ItemVM> _modListItems = new ObservableCollection<ItemVM>();
        private ObservableCollection<string> conflictingFilesList = new ObservableCollection<string>();
        private ObservableCollection<string> conflictingModsList = new ObservableCollection<string>();
        private string kcdPath = "";

        public ObservableCollection<ItemVM> ModListItems
        {
            get => _modListItems;
            set
            {
                if (Equals(value, _modListItems)) return;
                _modListItems = value;
                OnPropertyChanged(nameof(ModListItems));
            }
        }

        public string KcdPath
        {
            get => kcdPath;
            set
            {
                if (value == kcdPath) return;
                kcdPath = value;
                OnPropertyChanged(nameof(KcdPath));
            }
        }

        public ObservableCollection<string> ConflictingFilesList
        {
            get => conflictingFilesList;
            set
            {
                if (Equals(value, conflictingFilesList)) return;
                conflictingFilesList = value;
                OnPropertyChanged(nameof(ConflictingFilesList));
            }
        }

        public ObservableCollection<string> ConflictingModsList
        {
            get => conflictingModsList;
            set
            {
                if (Equals(value, conflictingModsList)) return;
                conflictingModsList = value;
                OnPropertyChanged(nameof(ConflictingModsList));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}