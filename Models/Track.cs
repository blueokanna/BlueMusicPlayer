// Models/Track.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlueMusicPlayer.Models
{
    public class Track : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string FilePath { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string AlbumArtUrl { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
