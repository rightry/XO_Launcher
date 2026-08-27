using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XO_Launcher.Models
{
    public sealed class InstanceModItem
    {
        public string FilePath { get; init; } = "";
        public string Name { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public bool IsEnabled { get; init; }
        public bool IsDirectory { get; init; }
        public bool IsLibraryItem { get; init; }
        public string SizeText { get; init; } = "";
        public string ShownName => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
        public string StatusText => IsEnabled ? "已启用" : "已禁用";
        public string DetailText
        {
            get
            {
                var extra = !string.IsNullOrWhiteSpace(DisplayName)
                            && !string.Equals(DisplayName, Name, StringComparison.OrdinalIgnoreCase)
                    ? Name + "  ·  "
                    : "";
                return IsLibraryItem ? extra + SizeText : extra + $"{SizeText}  ·  {StatusText}";
            }
        }
    }

    public sealed class LibraryPickItem : INotifyPropertyChanged
    {
        public InstanceModItem Item { get; init; } = new();
        public string Name => Item.ShownName;
        public string SizeText => Item.SizeText;

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

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
