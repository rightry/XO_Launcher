using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;
using XO_Launcher.Models;
using XO_Launcher.Services;

namespace XO_Launcher.Views
{
    public sealed partial class SettingsPage : UserControl
    {
        private bool _syncing;
        private bool _ready;

        public SettingsPage()
        {
            InitializeComponent();
            CreditsList.ItemsSource = CreateCredits();
            LoadFromSettings();
            _ready = true;
            LauncherSettings.Instance.Changed += () =>
            {
                DispatcherQueue.TryEnqueue(LoadFromSettings);
            };
        }

        private void LoadFromSettings()
        {
            _syncing = true;
            var settings = LauncherSettings.Instance;
            GameDirBox.Text = settings.GameDirectory;
            GameDirHint.Text = GameDirectoryService.Describe(settings.GameDirectory);
            AutoMatchToggle.IsOn = settings.AutoMatchJava;
            RefreshJavaList();
            MemorySlider.Value = settings.MemoryMB;
            MemoryValueText.Text = $"{settings.MemoryMB} MB";
            DownloadDirBox.Text = settings.DownloadDirectory;
            TranslateNamesToggle.IsOn = settings.TranslateModNames;
            UpdateThemeCards();
            _syncing = false;
        }

        private static List<CreditItem> CreateCredits()
        {
            return new List<CreditItem>
            {
                new()
                {
                    Name = "HMCL",
                    Description = "Hello Minecraft! Launcher。跨平台开源启动器，完整覆盖版本管理、模组安装与账号体系，是中文社区最常用的参考实现之一。",
                    IconText = "H",
                    IconColor = "#1F6FEB",
                    Url = "https://github.com/HMCL-dev/HMCL",
                    RepoText = "HMCL-dev/HMCL"
                },
                new()
                {
                    Name = "Prism Launcher",
                    Description = "基于 MultiMC 的现代化开源启动器，原生对接 Modrinth / CurseForge，实例隔离与模组管理职责划分清晰。",
                    IconText = "P",
                    IconColor = "#7CBD4B",
                    Url = "https://github.com/PrismLauncher/PrismLauncher",
                    RepoText = "PrismLauncher/PrismLauncher"
                },
                new()
                {
                    Name = "MultiMC",
                    Description = "开创实例（Instance）隔离体系的经典开源启动器，后续 Prism、PolyMC 等项目都建立在它的架构思路上。",
                    IconText = "M",
                    IconColor = "#A371F7",
                    Url = "https://github.com/MultiMC/Launcher",
                    RepoText = "MultiMC/Launcher"
                },
                new()
                {
                    Name = "PCL2",
                    Description = "Plain Craft Launcher 2。面向中文用户的开源启动器，在版本下载、模组管理与界面交互上提供了大量可参考的实践。",
                    IconText = "P",
                    IconColor = "#3D8BDB",
                    Url = "https://github.com/Meloong-Git/PCL",
                    RepoText = "Meloong-Git/PCL"
                },
                new()
                {
                    Name = "XMCL",
                    Description = "X Minecraft Launcher。对 Modrinth 资源浏览、版本解析与实例管理有完整实现，本启动器的下载/详情结构参考了它的职责拆分。",
                    IconText = "X",
                    IconColor = "#5B8731",
                    Url = "https://github.com/Voxelum/x-minecraft-launcher",
                    RepoText = "Voxelum/x-minecraft-launcher"
                },
                new()
                {
                    Name = "Fold Craft Launcher",
                    Description = "面向 Android 的开源 Minecraft 启动器，在跨平台启动、运行时管理与模组加载器安装上提供了可借鉴的方案。",
                    IconText = "F",
                    IconColor = "#E07A3D",
                    Url = "https://github.com/FCL-Team/FoldCraftLauncher",
                    RepoText = "FCL-Team/FoldCraftLauncher"
                },
                new()
                {
                    Name = "ATLauncher",
                    Description = "成熟的整合包与模组启动器，包管理、快速更新通道和实例配置对社区启动器设计影响很大。",
                    IconText = "A",
                    IconColor = "#E3B341",
                    Url = "https://github.com/ATLauncher/ATLauncher",
                    RepoText = "ATLauncher/ATLauncher"
                },
                new()
                {
                    Name = "GDLauncher",
                    Description = "开源桌面启动器，强调整合包浏览与一键安装，模组平台对接方式值得参考。",
                    IconText = "G",
                    IconColor = "#4EA8DE",
                    Url = "https://github.com/gorilla-devs/GDLauncher",
                    RepoText = "gorilla-devs/GDLauncher"
                },
                new()
                {
                    Name = "Modrinth App",
                    Description = "Modrinth 官方开源客户端，项目详情、按游戏版本筛选文件以及下载队列的交互是本详情页的重要参考。",
                    IconText = "M",
                    IconColor = "#1BD96A",
                    Url = "https://github.com/modrinth/code",
                    RepoText = "modrinth/code"
                }
            };
        }

        private async void BrowseGameDir_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, GetHwnd());
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;
            ApplyGameDirectory(folder.Path);
        }

        private async void BrowseJava_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add(".exe");
            InitializeWithWindow.Initialize(picker, GetHwnd());
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            LauncherSettings.Instance.JavaPath = file.Path;
            JavaRuntimeService.InvalidateScanCache();
            RefreshJavaList();
        }

        private void DetectJava_Click(object sender, RoutedEventArgs e)
        {
            JavaRuntimeService.InvalidateScanCache();
            RefreshJavaList();
            if (JavaCombo.Items.Count == 0)
                JavaStatusText.Text = "未检测到 Java，请浏览 javaw.exe 或下载官方运行时。";
        }

        private void ApplyGameDirectory(string path)
        {
            try
            {
                var resolved = GameDirectoryService.Apply(path, out var summary);
                GameDirBox.Text = resolved;
                GameDirHint.Text = summary;
                RefreshJavaList();
            }
            catch (Exception ex)
            {
                GameDirBox.Text = LauncherSettings.Instance.GameDirectory;
                GameDirHint.Text = "无法使用该目录：" + ex.Message;
            }
        }

        private void AutoMatchToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            LauncherSettings.Instance.AutoMatchJava = AutoMatchToggle.IsOn;
        }

        private void JavaCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing) return;
            if (JavaCombo.SelectedItem is JavaInstallation java)
                LauncherSettings.Instance.JavaPath = java.Path;
        }

        private void RefreshJavaList()
        {
            var javas = JavaRuntimeService.ScanAll();
            JavaCombo.ItemsSource = javas;
            JavaCombo.DisplayMemberPath = nameof(JavaInstallation.Display);
            var current = LauncherSettings.Instance.JavaPath;
            var selected = javas.FirstOrDefault(j => string.Equals(j.Path, current, StringComparison.OrdinalIgnoreCase));
            JavaCombo.SelectedItem = selected;
            if (selected is null && !string.IsNullOrWhiteSpace(current) && File.Exists(current))
            {
                var major = JavaRuntimeService.QueryMajorVersion(current);
                JavaStatusText.Text = "当前指定：" + (major > 0 ? $"Java {major}" : current);
            }
            else if (javas.Count == 0)
                JavaStatusText.Text = "尚未检测到 Java，启动时会按游戏版本自动下载官方运行时。";
            else
            {
                var labels = javas.Select(j => j.Display).Distinct().ToList();
                var list = string.Join("、", labels);
                if (LauncherSettings.Instance.AutoMatchJava)
                    JavaStatusText.Text = $"已检测到 {javas.Count} 个 Java：{list}。启动时会按游戏所需版本自动选择。";
                else if (selected is not null)
                    JavaStatusText.Text = "当前使用：" + selected.Display;
                else
                    JavaStatusText.Text = $"已检测到 {javas.Count} 个 Java：{list}，请选择一个或开启自动匹配。";
            }
        }

        private async void DownloadJava_Click(object sender, RoutedEventArgs e)
        {
            DownloadJavaButton.IsEnabled = false;
            JavaStatusText.Text = "正在下载 Mojang 官方 Java 21，进度请到「消息」查看…";
            var task = DownloadService.Instance.StartInstall("Java 虚拟机", "Java 21");
            try
            {
                var path = await JavaRuntimeService.Instance.EnsureAsync(
                    new JavaRequirement("java-runtime-delta", 21),
                    LauncherSettings.Instance.GameDirectory,
                    null,
                    task,
                    null,
                    cancellationToken: task.Token);
                LauncherSettings.Instance.JavaPath = path;
                JavaRuntimeService.InvalidateScanCache();
                RefreshJavaList();
                var major = JavaRuntimeService.QueryMajorVersion(path);
                JavaStatusText.Text = major > 0 ? $"已安装：Java {major}" : "已安装官方 Java 运行时。";
            }
            catch (Exception ex) when (DownloadService.IsCanceled(ex, task.Token))
            {
                DownloadService.Instance.MarkCancelled(task);
                JavaStatusText.Text = "已终止 Java 下载";
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Java", "下载官方 Java 失败", ex);
                JavaStatusText.Text = "下载失败：" + ex.Message;
                DownloadService.Instance.UpdateTask(task, t =>
                {
                    t.Status = "下载失败";
                    t.Detail = ex.Message;
                    t.ProgressHint = "安装失败";
                });
            }
            finally
            {
                DownloadJavaButton.IsEnabled = true;
            }
        }

        private void GameDirBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_syncing || string.IsNullOrWhiteSpace(GameDirBox.Text)) return;
            ApplyGameDirectory(GameDirBox.Text.Trim());
        }

        private void DownloadDirBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_syncing || string.IsNullOrWhiteSpace(DownloadDirBox.Text)) return;
            ApplyDownloadDirectory(DownloadDirBox.Text.Trim());
        }

        private async void BrowseDownloadDir_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, GetHwnd());
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;
            ApplyDownloadDirectory(folder.Path);
        }

        private void ApplyDownloadDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                ModLibraryService.EnsureLayout(path);
                LauncherSettings.Instance.DownloadDirectory = path;
                DownloadDirBox.Text = path;
                DownloadDirHint.Text = "模组、光影与资源包将保存至此目录下的 mods、shaderpacks、resourcepacks 子目录。下载完成后不会自动安装，请在版本设置中选择并安装。";
            }
            catch (Exception ex)
            {
                DownloadDirBox.Text = LauncherSettings.Instance.DownloadDirectory;
                DownloadDirHint.Text = "无法使用该目录：" + ex.Message;
            }
        }

        private async void ExportLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = await LogExportHelper.PickAndExportAsync();
                LogStatusText.Text = path is null ? "已取消导出。" : "已导出：" + path;
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Settings", "导出错误日志失败", ex);
                LogStatusText.Text = "导出失败：" + ex.Message;
            }
        }

        private async void AnalyzeCrash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await CrashAnalysisPanel.ShowLatestDialogAsync(XamlRoot);
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Settings", "故障分析失败", ex);
                LogStatusText.Text = "故障分析失败：" + ex.Message;
            }
        }

        private async void ExportCrash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var crash = GameCrashService.FindLatest();
                if (crash is null)
                {
                    LogStatusText.Text = "没有找到崩溃报告。启动游戏并在崩溃后可在此导出。";
                    return;
                }
                var path = await LogExportHelper.PickAndExportCrashAsync(crash);
                LogStatusText.Text = path is null ? "已取消导出。" : "已导出崩溃日志：" + path;
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Settings", "导出崩溃日志失败", ex);
                LogStatusText.Text = "导出失败：" + ex.Message;
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(LauncherLogService.LogDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe", LauncherLogService.LogDirectory) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogStatusText.Text = "无法打开日志文件夹：" + ex.Message;
            }
        }

        private void ThemeCard_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            if (sender is FrameworkElement { Tag: string theme })
                LauncherSettings.Instance.AppTheme = theme;
        }

        private void TranslateNamesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            LauncherSettings.Instance.TranslateModNames = TranslateNamesToggle.IsOn;
        }

        private void UpdateThemeCards()
        {
            if (DarkThemeButton is null || LightThemeButton is null) return;
            var light = ThemeService.IsLight;
            SetThemeCard(DarkThemeButton, !light);
            SetThemeCard(LightThemeButton, light);
        }

        private static void SetThemeCard(Button button, bool selected)
        {
            button.BorderBrush = selected
                ? ThemeService.Brush("AccentBrush")
                : ThemeService.Brush("DividerBrush");
        }

        private void OpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = ModLibraryService.RootDirectory;
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception)
            {
                // 打开资源管理器失败时忽略
            }
        }

        private void MemorySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            MemoryValueText.Text = $"{(int)e.NewValue} MB";
            if (_syncing || !_ready) return;
            LauncherSettings.Instance.MemoryMB = (long)e.NewValue;
        }

        private static IntPtr GetHwnd()
        {
            return App.MainWindow is not null ? WindowNative.GetWindowHandle(App.MainWindow) : IntPtr.Zero;
        }
    }

    internal static class LogExportHelper
    {
        public static async Task<string?> PickAndExportAsync()
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("日志文件", new List<string> { ".txt" });
            picker.SuggestedFileName = $"XO-Launcher-log-{DateTime.Now:yyyyMMdd-HHmmss}";
            InitializeWithWindow.Initialize(picker, App.MainWindow is not null
                ? WindowNative.GetWindowHandle(App.MainWindow)
                : IntPtr.Zero);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return null;
            LauncherLogService.ExportTo(file.Path);
            return file.Path;
        }

        public static async Task<string?> PickAndExportCrashAsync(GameCrashInfo crash)
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("崩溃日志", new List<string> { ".txt" });
            picker.SuggestedFileName = $"XO-crash-{GameInstanceLayout.Sanitize(crash.VersionId)}-{DateTime.Now:yyyyMMdd-HHmmss}";
            InitializeWithWindow.Initialize(picker, App.MainWindow is not null
                ? WindowNative.GetWindowHandle(App.MainWindow)
                : IntPtr.Zero);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return null;
            GameCrashService.ExportTo(file.Path, crash);
            return file.Path;
        }
    }
}
