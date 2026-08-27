using System;
using System.IO;
using System.Text.Json;

namespace XO_Launcher.Services
{
    /// <summary>
    /// 启动器全局设置。以 JSON 持久化到 %LocalAppData%\XO_Launcher\settings.json，
    /// 首页版本显示、底栏状态、启动游戏和设置页共享同一份数据。
    /// </summary>
    public sealed class LauncherSettings
    {
        private const string FileName = "settings.json";
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private string _gameDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        private string _javaPath = "";
        private long _memoryMB = 4096;
        private string _selectedVersion = "1.21.5";
        private string _username = "Player";
        private bool _autoMatchJava = true;
        private string _appTheme = "dark";
        private bool _translateModNames = true;
        private string _downloadDirectory = ModLibraryService.DefaultDirectory;

        private LauncherSettings() => Load();

        public static LauncherSettings Instance { get; } = new();

        /// <summary>任一设置项变更后触发（用于首页/底栏/设置页同步刷新）。</summary>
        public event Action? Changed;

        public string GameDirectory
        {
            get => _gameDirectory;
            set => SetValue(ref _gameDirectory, value);
        }

        public string JavaPath
        {
            get => _javaPath;
            set => SetValue(ref _javaPath, value);
        }

        public long MemoryMB
        {
            get => _memoryMB;
            set => SetValue(ref _memoryMB, value);
        }

        public string SelectedVersion
        {
            get => _selectedVersion;
            set => SetValue(ref _selectedVersion, value);
        }

        public string Username
        {
            get => _username;
            set => SetValue(ref _username, value);
        }

        /// <summary>启动时按游戏所需版本自动选择已检测到的 Java。</summary>
        public bool AutoMatchJava
        {
            get => _autoMatchJava;
            set => SetValue(ref _autoMatchJava, value);
        }

        /// <summary>界面主题：dark 或 light。</summary>
        public string AppTheme
        {
            get => _appTheme;
            set => SetValue(ref _appTheme, string.Equals(value, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark");
        }

        /// <summary>以中文显示模组、光影与资源包名称。</summary>
        public bool TranslateModNames
        {
            get => _translateModNames;
            set => SetValue(ref _translateModNames, value);
        }
        /// <summary>模组、光影与资源包的下载库根目录。</summary>
        public string DownloadDirectory
        {
            get => _downloadDirectory;
            set => SetValue(ref _downloadDirectory, string.IsNullOrWhiteSpace(value) ? ModLibraryService.DefaultDirectory : value.Trim());
        }

        private void SetValue<T>(ref T field, T value)
        {
            if (Equals(field, value)) return;
            field = value;
            Save();
            Changed?.Invoke();
        }

        private void Load()
        {
            try
            {
                var path = SettingsPath;
                if (!File.Exists(path)) return;
                var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path));
                if (data is null) return;
                if (!string.IsNullOrWhiteSpace(data.GameDirectory)) _gameDirectory = data.GameDirectory;
                _javaPath = data.JavaPath ?? "";
                if (data.MemoryMB is > 0) _memoryMB = data.MemoryMB.Value;
                if (!string.IsNullOrWhiteSpace(data.SelectedVersion)) _selectedVersion = data.SelectedVersion;
                if (!string.IsNullOrWhiteSpace(data.Username)) _username = data.Username;
                if (data.AutoMatchJava is not null) _autoMatchJava = data.AutoMatchJava.Value;
                if (!string.IsNullOrWhiteSpace(data.AppTheme))
                    _appTheme = string.Equals(data.AppTheme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
                if (!string.IsNullOrWhiteSpace(data.DownloadDirectory))
                    _downloadDirectory = data.DownloadDirectory.Trim();
                if (data.TranslateModNames is not null)
                    _translateModNames = data.TranslateModNames.Value;
            }
            catch (Exception)
            {
                // 设置文件损坏时静默回退到默认值
            }
        }

        private void Save()
        {
            try
            {
                var path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var data = new SettingsData
                {
                    GameDirectory = _gameDirectory,
                    JavaPath = _javaPath,
                    MemoryMB = _memoryMB,
                    SelectedVersion = _selectedVersion,
                    Username = _username,
                    AutoMatchJava = _autoMatchJava,
                    AppTheme = _appTheme,
                    DownloadDirectory = _downloadDirectory,
                    TranslateModNames = _translateModNames
                };
                File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
            }
            catch (Exception)
            {
                // 写入失败不影响运行
            }
        }

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XO_Launcher", FileName);

        private sealed class SettingsData
        {
            public string? GameDirectory { get; set; }
            public string? JavaPath { get; set; }
            public long? MemoryMB { get; set; }
            public string? SelectedVersion { get; set; }
            public string? Username { get; set; }
            public bool? AutoMatchJava { get; set; }
            public string? AppTheme { get; set; }
            public string? DownloadDirectory { get; set; }
            public bool? TranslateModNames { get; set; }
        }
    }
}
