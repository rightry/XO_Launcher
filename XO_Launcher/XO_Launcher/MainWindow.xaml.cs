using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.Text;
using XO_Launcher.Models;
using XO_Launcher.Services;
using XO_Launcher.Views;

namespace XO_Launcher
{
    public sealed partial class MainWindow : Window
    {
        private readonly AppWindow _appWindow;
        private readonly DownloadPage _downloadPage = new();
        private readonly SettingsPage _settingsPage = new();
        private readonly MessagesPage _messagesPage = new();
        private bool _launching;
        private string _currentPage = "home";

        public MainWindow()
        {
            InitializeComponent();

            Title = "XO Launcher";
            _appWindow = AppWindow;
            _appWindow.Title = "XO Launcher";
            _appWindow.Resize(new SizeInt32(1280, 800));
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(255, 200, 200, 200);
            _appWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 120, 120, 120);
            SetTitleBar(TitleBar);
            SetWindowIcon();

            DownloadHost.Children.Add(_downloadPage);
            SettingsHost.Children.Add(_settingsPage);
            MessagesHost.Children.Add(_messagesPage);

            LauncherSettings.Instance.Changed += RefreshChrome;
            DownloadService.Instance.TaskAdded += UpdateMessagesBadge;
            DownloadService.Instance.Tasks.CollectionChanged += OnDownloadTasksChanged;
            foreach (var task in DownloadService.Instance.Tasks)
                task.PropertyChanged += OnDownloadTaskPropertyChanged;

            RefreshChrome();
            UpdateMessagesBadge();
        }

        private void SetWindowIcon()
        {
            try
            {
                var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
                var light = ThemeService.IsLight;
                var icoName = light ? "theme_light.ico" : "theme_dark.ico";
                var pngName = light ? "theme_light.png" : "theme_dark.png";

                var iconPath = Path.Combine(assets, icoName);
                if (!File.Exists(iconPath))
                    iconPath = Path.Combine(assets, "XO_Launcher.ico");
                if (File.Exists(iconPath))
                    _appWindow.SetIcon(iconPath);

                var pngPath = Path.Combine(assets, pngName);
                if (!File.Exists(pngPath))
                    pngPath = Path.Combine(assets, "xo_home.png");
                if (File.Exists(pngPath) && AppIconImage is not null)
                    AppIconImage.Source = new BitmapImage(new Uri(pngPath));
            }
            catch (Exception)
            {
                // 图标文件缺失时使用系统默认图标
            }
        }

        private void HomeNavButton_Click(object sender, RoutedEventArgs e) => ShowPage("home");
        private void DownloadNavButton_Click(object sender, RoutedEventArgs e) => ShowPage("download");
        private void SettingsNavButton_Click(object sender, RoutedEventArgs e) => ShowPage("settings");
        private void MessagesNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("messages");
            _messagesPage.RefreshSessionCrash();
            UpdateMessagesBadge();
        }

        private void ShowPage(string page)
        {
            _currentPage = page;
            HomeView.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
            DownloadHost.Visibility = page == "download" ? Visibility.Visible : Visibility.Collapsed;
            SettingsHost.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
            MessagesHost.Visibility = page == "messages" ? Visibility.Visible : Visibility.Collapsed;

            HomeIndicator.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
            DownloadIndicator.Visibility = page == "download" ? Visibility.Visible : Visibility.Collapsed;
            SettingsIndicator.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
            MessagesIndicator.Visibility = page == "messages" ? Visibility.Visible : Visibility.Collapsed;

            var primary = ThemeService.Brush("TextPrimaryBrush");
            var secondary = ThemeService.Brush("TextSecondaryBrush");

            SetNavButton(HomeNavButton, page == "home", primary, secondary);
            SetNavButton(DownloadNavButton, page == "download", primary, secondary);
            SetNavButton(SettingsNavButton, page == "settings", primary, secondary);
            SetNavButton(MessagesNavButton, page == "messages", primary, secondary);
        }

        private static void SetNavButton(Button button, bool active, Brush primary, Brush secondary)
        {
            button.Foreground = active ? primary : secondary;
            button.FontWeight = active ? new FontWeight { Weight = 600 } : new FontWeight { Weight = 400 };
        }

        private void RefreshChrome()
        {
            ApplyTheme();
            var settings = LauncherSettings.Instance;
            var launch = GameInstanceLayout.ResolveLaunch(settings.SelectedVersion);
            AccountNameText.Text = string.IsNullOrWhiteSpace(settings.Username) ? "Player" : settings.Username;
            GameDirectoryText.Text = settings.GameDirectory;
            BottomMemoryText.Text = launch.Independent
                ? $"{launch.MemoryMB} MB · 本版本"
                : $"{launch.MemoryMB} MB";
            JavaVersionText.Text = FormatJavaLabel(launch);

            var installed = InstalledVersionService.Find(settings.SelectedVersion);
            if (installed is null)
            {
                var first = InstalledVersionService.Scan().FirstOrDefault();
                if (first is not null && !string.Equals(settings.SelectedVersion, first.Id, StringComparison.Ordinal))
                {
                    settings.SelectedVersion = first.Id;
                    return;
                }
                VersionText.Text = "未安装版本";
                VersionDescText.Text = "请先前往「下载」安装游戏版本";
                PlayButton.IsEnabled = !_launching;
                InstanceSettingsButton.IsEnabled = false;
                return;
            }

            VersionText.Text = installed.DisplayName;
            VersionDescText.Text = installed.LoaderId == "vanilla"
                ? "原版 Minecraft"
                : $"{installed.MinecraftVersion}  +  {installed.LoaderLabel}";
            PlayButton.IsEnabled = !_launching;
            InstanceSettingsButton.IsEnabled = !_launching;
        }

        private void ApplyTheme()
        {
            ThemeService.Apply(RootGrid);
            var light = ThemeService.IsLight;
            _appWindow.TitleBar.ButtonForegroundColor = light
                ? Color.FromArgb(255, 40, 40, 40)
                : Color.FromArgb(255, 200, 200, 200);
            _appWindow.TitleBar.ButtonInactiveForegroundColor = light
                ? Color.FromArgb(255, 120, 120, 120)
                : Color.FromArgb(255, 120, 120, 120);
            _appWindow.TitleBar.ButtonHoverForegroundColor = light
                ? Color.FromArgb(255, 20, 20, 20)
                : Color.FromArgb(255, 255, 255, 255);
            SetWindowIcon();
            ShowPage(_currentPage);
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_launching) return;

            var settings = LauncherSettings.Instance;
            if (!InstalledVersionService.Exists(settings.SelectedVersion))
            {
                LaunchStatusText.Text = "尚未安装游戏版本，请先前往「下载」安装";
                ShowPage("download");
                return;
            }

            var launch = GameInstanceLayout.ResolveLaunch(settings.SelectedVersion);
            var java = launch.JavaPath;
            var versionId = settings.SelectedVersion;
            var instanceDir = GameInstanceLayout.GetDirectory(settings.GameDirectory, versionId);
            var runtimeDir = GameInstanceLayout.ResolveContentRoot(settings.GameDirectory, instanceDir);
            var snapshot = GameCrashService.Snapshot(runtimeDir);
            var launchedAt = DateTime.UtcNow;

            _launching = true;
            PlayButton.IsEnabled = false;
            InstanceSettingsButton.IsEnabled = false;
            PlayButtonText.Text = "正在启动...";
            LaunchStatusText.Text = "正在准备启动...";

            try
            {
                var progress = new Progress<string>(msg => LaunchStatusText.Text = msg);
                var launcher = new GameLaunchService(progress);
                LauncherLogService.Info("Launch", $"正在启动 {versionId}" + (launch.Independent ? "（使用版本独立设置）" : ""));
                var process = await launcher.LaunchAsync(
                    versionId,
                    java,
                    settings.GameDirectory,
                    settings.Username,
                    launch.MemoryMB,
                    autoMatchJava: launch.AutoMatchJava,
                    extraJvmArgs: launch.Independent ? launch.ExtraJvmArgs : null,
                    extraGameArgs: launch.Independent ? launch.ExtraGameArgs : null,
                    windowWidth: launch.Independent ? launch.WindowWidth : null,
                    windowHeight: launch.Independent ? launch.WindowHeight : null);
                LaunchStatusText.Text = process.HasExited ? "正在检查游戏状态..." : "游戏已启动";
                if (!launch.Independent && string.IsNullOrWhiteSpace(settings.JavaPath))
                {
                    var installed = JavaRuntimeService.FindAnyInstalled(settings.GameDirectory);
                    if (!string.IsNullOrWhiteSpace(installed) && File.Exists(installed))
                        settings.JavaPath = installed;
                }
                _ = MonitorGameAsync(process, versionId, runtimeDir, snapshot, launchedAt);
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Launch", $"启动失败：{versionId}", ex);
                LaunchStatusText.Text = "启动失败：" + ex.Message;
            }
            finally
            {
                _launching = false;
                PlayButtonText.Text = "启动游戏";
                RefreshChrome();
            }
        }

        private async void InstanceSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = LauncherSettings.Instance;
            var installed = InstalledVersionService.Find(settings.SelectedVersion);
            if (installed is null)
            {
                LaunchStatusText.Text = "请先安装并选择一个版本";
                return;
            }

            var panel = new InstanceSettingsPanel();
            panel.Load(installed.Id, installed.DisplayName);
            var dialog = new ContentDialog
            {
                Title = "版本设置",
                Content = panel,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = RootGrid.XamlRoot,
                RequestedTheme = ThemeService.Current
            };
            dialog.Resources["ContentDialogMaxWidth"] = 900.0;
            dialog.Resources["ContentDialogMinWidth"] = 840.0;

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            panel.Save();
            RefreshChrome();
            LaunchStatusText.Text = GameInstanceLayout.ResolveLaunch(installed.Id).Independent
                ? "已保存本版本的独立设置"
                : "本版本将跟随全局设置";
        }

        private async Task MonitorGameAsync(
            Process process,
            string versionId,
            string instanceDir,
            HashSet<string> snapshot,
            DateTime launchedAtUtc)
        {
            try
            {
                if (!process.HasExited)
                    await process.WaitForExitAsync();
                var exitCode = process.ExitCode;
                await Task.Delay(1200);

                var crash = GameCrashService.Detect(versionId, instanceDir, snapshot, exitCode, launchedAtUtc);
                DispatcherQueue.TryEnqueue(() => HandleGameExit(crash));
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Crash", "监视游戏进程失败", ex);
            }
        }

        private void HandleGameExit(GameCrashInfo? crash)
        {
            if (crash is null)
            {
                if (LaunchStatusText.Text.StartsWith("游戏已启动", StringComparison.Ordinal)
                    || LaunchStatusText.Text.StartsWith("正在检查", StringComparison.Ordinal))
                    LaunchStatusText.Text = "游戏已退出";
                return;
            }

            LaunchStatusText.Text = crash.Kind + "，请查看「消息」中的最近一次故障分析";
            _messagesPage.RefreshSessionCrash();
            ShowPage("messages");
        }

        private async void SwitchVersionButton_Click(object sender, RoutedEventArgs e)
        {
            var installed = InstalledVersionService.Scan();
            if (installed.Count == 0)
            {
                var empty = new ContentDialog
                {
                    Title = "选择版本",
                    Content = "尚未安装游戏版本。请先前往「下载」安装原版；如需模组加载器，可在同一详情页一并安装。",
                    PrimaryButtonText = "前往下载",
                    CloseButtonText = "取消",
                    XamlRoot = RootGrid.XamlRoot,
                    RequestedTheme = ThemeService.Current
                };
                if (await empty.ShowAsync() == ContentDialogResult.Primary)
                    ShowPage("download");
                return;
            }

            var list = new ListView
            {
                ItemsSource = installed,
                Height = 420,
                SelectionMode = ListViewSelectionMode.Single,
                ItemTemplate = (DataTemplate)RootGrid.Resources["InstalledVersionItemTemplate"]
            };
            var current = installed.FirstOrDefault(v => v.Id == LauncherSettings.Instance.SelectedVersion);
            list.SelectedItem = current ?? installed[0];

            var dialog = new ContentDialog
            {
                Title = "已下载的版本",
                Content = list,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = RootGrid.XamlRoot,
                RequestedTheme = ThemeService.Current
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary || list.SelectedItem is not InstalledVersion selected)
                return;

            LauncherSettings.Instance.SelectedVersion = selected.Id;
            LaunchStatusText.Text = "";
        }

        private async void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            var box = new TextBox
            {
                Text = LauncherSettings.Instance.Username,
                PlaceholderText = "离线玩家名称"
            };
            var dialog = new ContentDialog
            {
                Title = "管理账户",
                Content = box,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                XamlRoot = RootGrid.XamlRoot,
                RequestedTheme = ThemeService.Current
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text))
                LauncherSettings.Instance.Username = box.Text.Trim();
        }

        private void OnDownloadTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (DownloadTask task in e.NewItems)
                    task.PropertyChanged += OnDownloadTaskPropertyChanged;
            }
            UpdateMessagesBadge();
        }

        private void OnDownloadTaskPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateMessagesBadge();

        private void UpdateMessagesBadge()
        {
            var active = DownloadService.Instance.Tasks.Any(t => t.IsActive);
            MessagesBadge.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string FormatJavaLabel(ResolvedLaunchSettings launch)
        {
            var settings = LauncherSettings.Instance;
            var javas = JavaRuntimeService.ScanAll(settings.GameDirectory);
            var suffix = launch.Independent ? " · 本版本" : "";
            if (launch.AutoMatchJava)
                return (javas.Count > 0 ? $"自动匹配 · {javas.Count} 个" : "自动匹配") + suffix;

            var javaPath = launch.JavaPath;
            if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
                javaPath = javas.FirstOrDefault()?.Path ?? "";
            if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
                return "将自动下载" + suffix;

            var found = javas.FirstOrDefault(j => string.Equals(j.Path, javaPath, StringComparison.OrdinalIgnoreCase));
            if (found is not null) return $"Java {found.Major}" + suffix;

            var major = JavaRuntimeService.QueryMajorVersion(javaPath);
            if (major > 0) return $"Java {major}" + suffix;
            return "已配置" + suffix;
        }
    }
}
