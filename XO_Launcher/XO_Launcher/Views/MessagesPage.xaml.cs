using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XO_Launcher.Models;
using XO_Launcher.Services;

namespace XO_Launcher.Views
{
    public sealed partial class MessagesPage : UserControl
    {
        public MessagesPage()
        {
            InitializeComponent();
            TasksList.ItemsSource = DownloadService.Instance.Tasks;
            foreach (var task in DownloadService.Instance.Tasks)
                task.PropertyChanged += OnTaskPropertyChanged;
            UpdateEmptyState();

            DownloadService.Instance.TaskAdded += OnTaskAdded;
            DownloadService.Instance.Tasks.CollectionChanged += OnTasksChanged;
            Loaded += (_, _) => RefreshSessionCrash();
        }

        public void RefreshSessionCrash()
        {
            if (CrashCard is null || SessionCrashPanel is null) return;
            var crash = GameCrashService.LastCrash;
            if (crash is null)
            {
                CrashCard.Visibility = Visibility.Collapsed;
                return;
            }

            CrashCard.Visibility = Visibility.Visible;
            SessionCrashPanel.ShowActions = false;
            SessionCrashPanel.SetCrash(crash);
        }

        private void OnTaskAdded()
        {
            UpdateEmptyState();
        }

        private void OnTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (DownloadTask task in e.NewItems)
                    task.PropertyChanged += OnTaskPropertyChanged;
            }
            UpdateEmptyState();
        }

        private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DownloadTask.CanCancel) or nameof(DownloadTask.Status) or nameof(DownloadTask.IsActive))
                UpdateEmptyState();
        }

        private void CancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: DownloadTask task })
                DownloadService.Instance.Cancel(task);
        }

        private void CancelAllButton_Click(object sender, RoutedEventArgs e)
            => DownloadService.Instance.CancelAll();

        private async void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = await LogExportHelper.PickAndExportAsync();
                if (path is null) return;
                var dialog = new ContentDialog
                {
                    Title = "错误日志已导出",
                    Content = path,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot,
                    RequestedTheme = ThemeService.Current
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Messages", "导出错误日志失败", ex);
                var dialog = new ContentDialog
                {
                    Title = "导出失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot,
                    RequestedTheme = ThemeService.Current
                };
                await dialog.ShowAsync();
            }
        }

        private void AnalyzeCrashButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSessionCrash();
        }

        private async void ExportCrashButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var crash = GameCrashService.LastCrash;
                if (crash is null)
                {
                    var empty = new ContentDialog
                    {
                        Title = "没有崩溃日志",
                        Content = "本次运行尚未检测到崩溃。故障分析仅在本次启动器运行期间保留。",
                        CloseButtonText = "确定",
                        XamlRoot = XamlRoot,
                        RequestedTheme = ThemeService.Current
                    };
                    await empty.ShowAsync();
                    return;
                }
                var path = await LogExportHelper.PickAndExportCrashAsync(crash);
                if (path is null) return;
                var dialog = new ContentDialog
                {
                    Title = "崩溃日志已导出",
                    Content = path,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot,
                    RequestedTheme = ThemeService.Current
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Messages", "导出崩溃日志失败", ex);
                var dialog = new ContentDialog
                {
                    Title = "导出失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot,
                    RequestedTheme = ThemeService.Current
                };
                await dialog.ShowAsync();
            }
        }

        private void UpdateEmptyState()
        {
            var hasTasks = DownloadService.Instance.Tasks.Any();
            EmptyPanel.Visibility = hasTasks ? Visibility.Collapsed : Visibility.Visible;
            TasksList.Visibility = hasTasks ? Visibility.Visible : Visibility.Collapsed;
            CancelAllButton.Visibility = DownloadService.Instance.Tasks.Any(t => t.CanCancel)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
