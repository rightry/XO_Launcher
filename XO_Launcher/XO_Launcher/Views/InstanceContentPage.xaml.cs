using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XO_Launcher.Models;
using XO_Launcher.Services;

namespace XO_Launcher.Views
{
    public sealed partial class InstanceContentPage : UserControl
    {
        private string _instanceDir = "";
        private string _contentDir = "";
        private string _projectType = ContentKind.Mod;

        public InstanceContentPage()
        {
            InitializeComponent();
        }

        public void Bind(string instanceDir, string projectType)
            => Bind(LauncherSettings.Instance.GameDirectory, instanceDir, projectType);

        public void Bind(string gameDir, string instanceDir, string projectType)
        {
            _instanceDir = instanceDir;
            _contentDir = GameInstanceLayout.ResolveContentRoot(gameDir, instanceDir);
            _projectType = projectType;
            TitleText.Text = ContentKind.ManagementTitle(projectType);
            Refresh();
        }

        public void HidePicker()
        {
            if (LibraryPickerOverlay is null) return;
            LibraryPickerOverlay.Visibility = Visibility.Collapsed;
        }

        private string Noun => ContentKind.DisplayName(_projectType);

        private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

        public void Refresh()
        {
            if (string.IsNullOrWhiteSpace(_contentDir) || ContentList is null) return;
            HidePicker();
            var items = InstanceModService.Scan(_contentDir, _projectType);
            ContentList.ItemsSource = items;
            var empty = items.Count == 0;
            EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            ContentList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            EmptyText.Text = $"当前版本尚未安装{Noun}。请前往「下载」获取文件，再于此处从下载库安装。";
            var enabled = items.Count(m => m.IsEnabled);
            CountText.Text = empty
                ? $"请前往「下载」获取{Noun}，下载完成后在此安装。"
                : $"共 {items.Count} 项，已启用 {enabled} 项";
            StatusText.Text = "";
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = GameInstanceLayout.GetSubFolder(_contentDir, ContentKind.LibraryFolder(_projectType));
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusText.Text = "无法打开目录：" + ex.Message;
            }
        }

        private void InstallFromLibrary_Click(object sender, RoutedEventArgs e)
        {
            var library = ModLibraryService.Scan(_projectType)
                .Select(item => new LibraryPickItem { Item = item })
                .ToList();
            PickerList.ItemsSource = library;
            var empty = library.Count == 0;
            PickerEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            PickerList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            ConfirmPickerButton.IsEnabled = false;
            PickerTitle.Text = $"从下载库安装{Noun}";
            PickerHint.Text = empty
                ? $"下载库中暂无{Noun}。请先前往「下载」获取，文件将保存至资源下载目录。"
                : $"点击选择要安装的{Noun}，选中项将显示绿色边框，可多选。已存在的同名文件将被跳过。";
            PickerEmpty.Text = $"下载库中暂无{Noun}。请先前往「下载」获取。";
            LibraryPickerOverlay.Visibility = Visibility.Visible;
        }

        private void PickerList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not LibraryPickItem item) return;
            item.IsSelected = !item.IsSelected;
            ConfirmPickerButton.IsEnabled = PickerItems().Any(i => i.IsSelected);
        }

        private void CancelPicker_Click(object sender, RoutedEventArgs e) => HidePicker();

        private void ConfirmPicker_Click(object sender, RoutedEventArgs e)
        {
            var selected = PickerItems().Where(i => i.IsSelected).Select(i => i.Item).ToList();
            HidePicker();
            if (selected.Count == 0)
            {
                StatusText.Text = $"未选择要安装的{Noun}";
                return;
            }

            try
            {
                var (installed, skipped) = ModLibraryService.InstallToInstance(_contentDir, _projectType, selected);
                Refresh();
                StatusText.Text = skipped == 0
                    ? $"已安装 {installed} 项{Noun}"
                    : $"已安装 {installed} 项{Noun}，已跳过 {skipped} 个同名文件";
            }
            catch (Exception ex)
            {
                StatusText.Text = "安装失败：" + ex.Message;
            }
        }

        private IEnumerable<LibraryPickItem> PickerItems()
            => PickerList.ItemsSource as IEnumerable<LibraryPickItem> ?? Array.Empty<LibraryPickItem>();

        private void Enabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch { DataContext: InstanceModItem item } toggle) return;
            if (item.IsEnabled == toggle.IsOn) return;
            try
            {
                InstanceModService.SetEnabled(item, toggle.IsOn);
                Refresh();
            }
            catch (Exception ex)
            {
                StatusText.Text = "无法更改启用状态：" + ex.Message;
                Refresh();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: InstanceModItem item }) return;
            try
            {
                InstanceModService.Delete(item);
                Refresh();
                StatusText.Text = "已移除 " + item.Name;
            }
            catch (Exception ex)
            {
                StatusText.Text = "移除失败：" + ex.Message;
            }
        }
    }
}
