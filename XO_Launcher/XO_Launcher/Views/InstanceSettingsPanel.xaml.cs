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
using Windows.UI;
using Windows.UI.Text;
using WinRT.Interop;
using XO_Launcher.Models;
using XO_Launcher.Services;

namespace XO_Launcher.Views
{
    public sealed partial class InstanceSettingsPanel : UserControl
    {
        private string _versionId = "";
        private string _instanceDir = "";
        private bool _syncing;

        public InstanceSettingsPanel()
        {
            InitializeComponent();
        }

        public void Load(string versionId, string displayName)
        {
            _versionId = versionId;
            var global = LauncherSettings.Instance;
            _instanceDir = GameInstanceLayout.GetDirectory(global.GameDirectory, versionId);
            Directory.CreateDirectory(_instanceDir);
            if (GameInstanceLayout.UsesIsolatedContent(_instanceDir))
                GameInstanceLayout.Ensure(_instanceDir);
            var meta = GameInstanceLayout.GetOrCreateMeta(_instanceDir);

            _syncing = true;
            DisplayNameBox.Text = string.IsNullOrWhiteSpace(meta.DisplayName) ? displayName : meta.DisplayName;
            FolderIdHint.Text = "文件夹名称：" + versionId;
            IndependentToggle.IsOn = meta.IndependentSettings;
            MemorySlider.Value = meta.MemoryMB is > 0 ? meta.MemoryMB.Value : global.MemoryMB;
            UpdateMemoryLabel((int)MemorySlider.Value);
            AutoMatchToggle.IsOn = meta.AutoMatchJava ?? global.AutoMatchJava;
            JvmArgsBox.Text = meta.ExtraJvmArgs ?? "";
            GameArgsBox.Text = meta.ExtraGameArgs ?? "";
            WidthBox.Text = (meta.WindowWidth is > 0 ? meta.WindowWidth.Value : 854).ToString();
            HeightBox.Text = (meta.WindowHeight is > 0 ? meta.WindowHeight.Value : 480).ToString();
            RefreshJavaList(meta.JavaPath ?? global.JavaPath);
            ApplyIndependentState();
            _syncing = false;

            ShowSection("basic");
            BindContentPages();
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(_versionId)) return;
            var existing = GameInstanceLayout.ReadMeta(_instanceDir);
            if (IndependentToggle.IsOn)
                GameInstanceLayout.Ensure(_instanceDir);
            var meta = existing ?? new GameInstanceLayout.InstanceMeta();
            var name = DisplayNameBox.Text?.Trim();
            meta.DisplayName = string.IsNullOrWhiteSpace(name) ? null : name;
            meta.IndependentSettings = IndependentToggle.IsOn;
            meta.MemoryMB = (long)MemorySlider.Value;
            meta.AutoMatchJava = AutoMatchToggle.IsOn;
            meta.JavaPath = JavaCombo.SelectedItem is JavaInstallation java ? java.Path : meta.JavaPath;
            meta.ExtraJvmArgs = JvmArgsBox.Text?.Trim();
            meta.ExtraGameArgs = GameArgsBox.Text?.Trim();
            if (int.TryParse(WidthBox.Text, out var w) && w > 0) meta.WindowWidth = w;
            if (int.TryParse(HeightBox.Text, out var h) && h > 0) meta.WindowHeight = h;
            if (existing is not null || meta.IndependentSettings || !string.IsNullOrWhiteSpace(meta.DisplayName))
                GameInstanceLayout.WriteMeta(_instanceDir, meta);
        }

        private void NavBasic_Click(object sender, RoutedEventArgs e) => ShowSection("basic");
        private void NavMods_Click(object sender, RoutedEventArgs e) => ShowSection("mods");
        private void NavShaders_Click(object sender, RoutedEventArgs e) => ShowSection("shaders");
        private void NavPacks_Click(object sender, RoutedEventArgs e) => ShowSection("packs");
        private void NavMemory_Click(object sender, RoutedEventArgs e) => ShowSection("memory");
        private void NavJvm_Click(object sender, RoutedEventArgs e) => ShowSection("jvm");
        private void NavAnalyze_Click(object sender, RoutedEventArgs e) => ShowSection("basic");

        private void BindContentPages()
        {
            var gameDir = LauncherSettings.Instance.GameDirectory;
            ModsContent.Bind(gameDir, _instanceDir, ContentKind.Mod);
            ShadersContent.Bind(gameDir, _instanceDir, ContentKind.Shader);
            PacksContent.Bind(gameDir, _instanceDir, ContentKind.ResourcePack);
        }

        private void ShowSection(string section)
        {
            BasicPage.Visibility = section == "basic" ? Visibility.Visible : Visibility.Collapsed;
            ModsPage.Visibility = section == "mods" ? Visibility.Visible : Visibility.Collapsed;
            ShadersPage.Visibility = section == "shaders" ? Visibility.Visible : Visibility.Collapsed;
            PacksPage.Visibility = section == "packs" ? Visibility.Visible : Visibility.Collapsed;
            MemoryPage.Visibility = section == "memory" ? Visibility.Visible : Visibility.Collapsed;
            JvmPage.Visibility = section == "jvm" ? Visibility.Visible : Visibility.Collapsed;
            HighlightNav(NavBasicButton, section == "basic");
            HighlightNav(NavModsButton, section == "mods");
            HighlightNav(NavShadersButton, section == "shaders");
            HighlightNav(NavPacksButton, section == "packs");
            HighlightNav(NavMemoryButton, section == "memory");
            HighlightNav(NavJvmButton, section == "jvm");
            ModsContent.HidePicker();
            ShadersContent.HidePicker();
            PacksContent.HidePicker();
            if (section == "mods") ModsContent.Refresh();
            if (section == "shaders") ShadersContent.Refresh();
            if (section == "packs") PacksContent.Refresh();
        }

        private static void HighlightNav(Button button, bool active)
        {
            if (ThemeService.IsLight)
            {
                button.Background = new SolidColorBrush(active ? Color.FromArgb(255, 226, 237, 214) : Color.FromArgb(0, 0, 0, 0));
                button.Foreground = new SolidColorBrush(active ? Color.FromArgb(255, 91, 135, 49) : Color.FromArgb(255, 80, 80, 80));
            }
            else
            {
                button.Background = new SolidColorBrush(active ? Color.FromArgb(255, 43, 61, 28) : Color.FromArgb(0, 0, 0, 0));
                button.Foreground = new SolidColorBrush(active ? Color.FromArgb(255, 124, 189, 75) : Color.FromArgb(255, 200, 200, 200));
            }
            button.FontWeight = active ? new FontWeight { Weight = 600 } : new FontWeight { Weight = 400 };
        }

        private void IndependentToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            ApplyIndependentState();
        }

        private void ApplyIndependentState()
        {
            var on = IndependentToggle.IsOn;
            MemoryHintText.Text = on
                ? "此版本将使用下方内存分配，不再跟随全局设置。"
                : "当前跟随全局内存。开启「独立启动设置」后才会使用本页数值。";
            JvmHintText.Text = on
                ? "此版本将使用下方 Java 与启动参数。"
                : "当前跟随全局 Java。开启「独立启动设置」后才会使用本页数值。";
        }

        private void MemorySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (MemoryValueText is null) return;
            UpdateMemoryLabel((int)e.NewValue);
        }

        private void UpdateMemoryLabel(int mb)
        {
            var gb = mb / 1024.0;
            MemoryValueText.Text = $"{mb} MB（{gb:0.#} GB）";
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string tag }) return;
            if (tag == "version")
            {
                OpenFolder(_instanceDir);
                return;
            }
            var contentRoot = GameInstanceLayout.ResolveContentRoot(
                LauncherSettings.Instance.GameDirectory, _instanceDir);
            OpenFolder(GameInstanceLayout.GetSubFolder(contentRoot, tag));
        }

        private void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception)
            {
                // 打开资源管理器失败时忽略
            }
        }

        private void RefreshJavaList(string? selectedPath)
        {
            var javas = JavaRuntimeService.ScanAll().ToList();
            if (!string.IsNullOrWhiteSpace(selectedPath) && File.Exists(selectedPath)
                && javas.TrueForAll(j => !string.Equals(j.Path, selectedPath, StringComparison.OrdinalIgnoreCase)))
            {
                javas.Insert(0, new JavaInstallation
                {
                    Path = selectedPath,
                    Major = JavaRuntimeService.QueryMajorVersion(selectedPath),
                    Vendor = "Java",
                    Source = "手动选择"
                });
            }
            JavaCombo.ItemsSource = javas;
            JavaCombo.DisplayMemberPath = nameof(JavaInstallation.Display);
            JavaCombo.SelectedItem = javas.FirstOrDefault(j =>
                string.Equals(j.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
        }

        private async void BrowseJava_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add(".exe");
            InitializeWithWindow.Initialize(picker, App.MainWindow is not null
                ? WindowNative.GetWindowHandle(App.MainWindow)
                : IntPtr.Zero);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            JavaRuntimeService.InvalidateScanCache();
            RefreshJavaList(file.Path);
        }
    }
}
