using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using KCDModMerger.Annotations;

namespace KCDModMerger.UI
{
    public class ItemVM : INotifyPropertyChanged
    {
        private string _text;
        private Color _backgroundColor;

        public ItemVM(string text, Color backgroundColor)
        {
            Text = text;
            BackgroundColor = backgroundColor;
        }

        public string Text
        {
            get => _text;
            set
            {
                if (value == _text) return;
                _text = value;
                OnPropertyChanged();
            }
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (value.Equals(_backgroundColor)) return;
                _backgroundColor = value;
                OnPropertyChanged();
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
