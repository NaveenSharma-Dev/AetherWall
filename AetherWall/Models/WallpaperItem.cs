using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AetherWall.Models
{
    public class WallpaperItem : INotifyPropertyChanged
    {
        private string? _thumbnailPath;
        private string? _customName;
        private bool _isFavorite;

        public string Id { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? CustomName
        {
            get => _customName;
            set
            {
                if (_customName == value)
                    return;

                _customName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(CustomName)
                ? Name
                : CustomName;

        public string? ThumbnailPath
        {
            get => _thumbnailPath;
            set
            {
                if (_thumbnailPath == value)
                    return;

                _thumbnailPath = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan Duration { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public long ResolutionPixels => (long)Width * Height;

        public long FileSize { get; set; }

        public string Format { get; set; } = string.Empty;

        public DateTime LastModified { get; set; }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value)
                    return;

                _isFavorite = value;
                OnPropertyChanged();
            }
        }

        public string Resolution =>
            Width > 0 && Height > 0
                ? $"{Width} × {Height}"
                : "Unknown";

        public string DurationText
        {
            get
            {
                if (Duration.TotalSeconds <= 0)
                    return "--:--";

                return Duration.TotalHours >= 1
                    ? Duration.ToString(@"h\:mm\:ss")
                    : Duration.ToString(@"m\:ss");
            }
        }

        public string FileSizeText
        {
            get
            {
                if (FileSize <= 0)
                    return "Unknown";

                double size = FileSize;

                if (size >= 1024 * 1024 * 1024)
                    return $"{size / (1024 * 1024 * 1024):0.##} GB";

                return $"{size / (1024 * 1024):0.##} MB";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyMetadataUpdated()
        {
            OnPropertyChanged(nameof(Duration));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(FileSize));
            OnPropertyChanged(nameof(LastModified));
            OnPropertyChanged(nameof(Format));
            OnPropertyChanged(nameof(Resolution));
            OnPropertyChanged(nameof(ResolutionPixels));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(FileSizeText));
        }

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
