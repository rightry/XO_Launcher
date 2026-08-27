using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using XO_Launcher.Api;
using XO_Launcher.Models;
using XO_Launcher.Services;

namespace XO_Launcher.Views
{
    public sealed partial class VanillaDetailPage : UserControl
    {
        private readonly LoaderMetaApi _loaderApi = new();
        private readonly GameInstallService _installer = new();
        private CancellationTokenSource? _loaderCts;
        private DownloadItem? _item;
        private bool _installing;
        private bool _nameTouched;
        private bool _updatingName;
        private string _selectedLoader = "vanilla";
        private IReadOnlyList<LoaderBuild> _loaderBuilds = Array.Empty<LoaderBuild>();

        public event EventHandler? BackRequested;

        public VanillaDetailPage()
        {
            InitializeComponent();
            BackButton.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Show(DownloadItem item)
        {
            _item = item;
            _installing = false;
            _selectedLoader = "vanilla";
            _loaderBuilds = Array.Empty<LoaderBuild>();
            QueueTipBorder.Visibility = Visibility.Collapsed;
            TitleText.Text = item.Title;
            TypeText.Text = item.Badge;
            TypeText.Foreground = item.IsAlpha
                ? new SolidColorBrush(Color.FromArgb(255, 229, 72, 77))
                : ThemeService.Brush("AccentBrush");
            TypeChip.Background = item.IsAlpha
                ? new SolidColorBrush(Color.FromArgb(255, 58, 21, 24))
                : new SolidColorBrush(Color.FromArgb(255, 27, 42, 27));
            MetaText.Text = string.IsNullOrWhiteSpace(item.Meta) ? "Mojang 官方版本清单" : item.Meta + "  ·  Mojang 官方";
            DescriptionText.Text = item.IsAlpha
                ? "这是远古 Alpha 版本，可能无法在现代 Java 上运行。将与所选加载器写入同一个游戏文件夹。"
                : "游戏本体与加载器会安装到同一个 versions 文件夹。资源文件在首次启动时也可自动补齐。";
            IconBorder.Background = new SolidColorBrush(ParseColor(item.IconColor));
            HighlightLoaderCards();
            LoaderVersionPanel.Visibility = Visibility.Collapsed;
            FabricApiPanel.Visibility = Visibility.Collapsed;
            ShowAllLoaderToggle.Visibility = Visibility.Collapsed;
            LoaderVersionBox.ItemsSource = null;
            _nameTouched = false;
            SuggestGameName();
            InstallButton.IsEnabled = true;
            InstallButtonText.Text = "下载并安装";
        }

        public void Cancel()
        {
            _loaderCts?.Cancel();
        }

        private void LoaderCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string tag }) return;
            _selectedLoader = tag;
            HighlightLoaderCards();
            _ = LoadLoaderBuildsAsync();
        }

        private void HighlightLoaderCards()
        {
            StyleCard(CardVanilla, _selectedLoader == "vanilla");
            StyleCard(CardFabric, _selectedLoader == "fabric");
            StyleCard(CardForge, _selectedLoader == "forge");
            StyleCard(CardNeoForge, _selectedLoader == "neoforge");
            StyleCard(CardQuilt, _selectedLoader == "quilt");
        }

        private static void StyleCard(Button button, bool active)
        {
            button.Background = new SolidColorBrush(active ? Color.FromArgb(255, 27, 42, 27) : Color.FromArgb(255, 26, 26, 26));
            button.BorderBrush = new SolidColorBrush(active ? Color.FromArgb(255, 124, 189, 75) : Color.FromArgb(255, 42, 42, 42));
            button.BorderThickness = new Thickness(active ? 2 : 1);
        }

        private async Task LoadLoaderBuildsAsync()
        {
            if (_item is null) return;
            var loader = _selectedLoader;
            if (loader is "vanilla" or null)
            {
                LoaderVersionPanel.Visibility = Visibility.Collapsed;
                FabricApiPanel.Visibility = Visibility.Collapsed;
                ShowAllLoaderToggle.Visibility = Visibility.Collapsed;
                SuggestGameName();
                return;
            }

            LoaderVersionPanel.Visibility = Visibility.Visible;
            FabricApiPanel.Visibility = loader == "fabric" ? Visibility.Visible : Visibility.Collapsed;
            ShowAllLoaderToggle.Visibility = loader is "fabric" or "quilt" ? Visibility.Visible : Visibility.Collapsed;
            LoaderVersionLabel.Text = FormatHelper.FormatLoaderName(loader) + " 版本";
            LoaderVersionBox.ItemsSource = null;
            LoaderStatusText.Text = "正在查询该游戏版本可用的加载器...";
            _loaderCts?.Cancel();
            _loaderCts = new CancellationTokenSource();
            var token = _loaderCts.Token;

            try
            {
                var builds = await _loaderApi.GetBuildsAsync(loader, _item.Title, token);
                if (token.IsCancellationRequested) return;
                _loaderBuilds = builds;
                BindLoaderVersions();
                SuggestGameName();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoaderStatusText.Text = "加载器列表获取失败：" + ex.Message;
            }
        }

        private void ShowAllLoaderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loaderBuilds.Count == 0) return;
            BindLoaderVersions();
        }

        private void BindLoaderVersions()
        {
            IEnumerable<LoaderBuild> list = _loaderBuilds;
            if (_selectedLoader is "fabric" or "quilt" && ShowAllLoaderToggle.IsOn == false)
            {
                var stable = _loaderBuilds.Where(b => b.Stable || !b.Version.Contains("beta", StringComparison.OrdinalIgnoreCase)
                    && !b.Version.Contains("pre", StringComparison.OrdinalIgnoreCase)).ToList();
                if (_selectedLoader == "fabric")
                    stable = _loaderBuilds.Where(b => b.Stable).ToList();
                if (stable.Count > 0) list = stable;
            }

            var bound = list.ToList();
            if (bound.Count == 0)
            {
                LoaderStatusText.Text = $"该 Minecraft 版本暂无 {FormatHelper.FormatLoaderName(_selectedLoader)}";
                LoaderVersionBox.ItemsSource = null;
                return;
            }

            LoaderVersionBox.ItemsSource = bound;
            LoaderVersionBox.DisplayMemberPath = nameof(LoaderBuild.Display);
            LoaderVersionBox.SelectedIndex = 0;
            LoaderStatusText.Text = ShowAllLoaderToggle.IsOn || _selectedLoader is not ("fabric" or "quilt")
                ? $"共 {bound.Count} 个 {FormatHelper.FormatLoaderName(_selectedLoader)} 版本，可单独选择"
                : $"共 {bound.Count} 个稳定版，可单独选择；打开开关可看测试版";
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (_item is null || _installing) return;

            var loader = _selectedLoader;
            LoaderBuild? build = null;
            if (loader != "vanilla")
            {
                build = LoaderVersionBox.SelectedItem as LoaderBuild;
                if (build is null)
                {
                    ShowTip("请先选择加载器版本");
                    return;
                }
            }

            if (_item.IsAlpha && !await ConfirmAlphaAsync())
                return;

            var instanceName = GameNameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(instanceName))
                instanceName = SuggestedName();

            _installing = true;
            InstallButton.IsEnabled = false;
            InstallButtonText.Text = "正在加入队列...";

            var vanilla = new MinecraftVersion
            {
                Id = _item.Title,
                Type = _item.VersionType,
                Url = _item.DownloadUrl,
                ReleaseTime = _item.Meta
            };

            var title = loader == "vanilla"
                ? instanceName
                : $"{instanceName}  ·  {FormatHelper.FormatLoaderName(loader)}";
            var task = DownloadService.Instance.StartInstall(title, vanilla.Id);

            ShowTip("已开始安装（多线程下载），进度请到顶部「消息」查看");

            try
            {
                var installedId = await _installer.InstallAsync(
                    vanilla, loader, build, task, instanceName,
                    installFabricApi: loader == "fabric" && FabricApiToggle.IsOn,
                    cancellationToken: task.Token);
                LauncherSettings.Instance.SelectedVersion = installedId;
                ShowTip($"安装完成：{instanceName}（目录 versions\\{installedId}）。可返回首页启动。");
            }
            catch (Exception ex) when (DownloadService.IsCanceled(ex, task.Token))
            {
                DownloadService.Instance.MarkCancelled(task);
                ShowTip("已终止下载");
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("Install", $"安装失败：{title}", ex);
                DownloadService.Instance.UpdateTask(task, t =>
                {
                    t.Status = "下载失败";
                    t.Progress = 0;
                    t.Detail = ex.Message;
                    t.ProgressHint = "安装失败";
                });
                ShowTip("安装失败：" + ex.Message);
            }
            finally
            {
                _installing = false;
                InstallButton.IsEnabled = true;
                InstallButtonText.Text = "下载并安装";
            }
        }

        private async Task<bool> ConfirmAlphaAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "远古 / Alpha 版本提示",
                Content = "该版本属于远古 Alpha，可能无法在当前 Java 上运行，也更容易出现兼容性问题。确定仍要下载吗？",
                PrimaryButtonText = "仍要下载",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
                RequestedTheme = ThemeService.Current
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private void LoaderVersionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => SuggestGameName();

        private void GameNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_updatingName) _nameTouched = true;
            UpdateFolderHint();
        }

        private void SuggestGameName()
        {
            if (_nameTouched || _item is null) { UpdateFolderHint(); return; }
            _updatingName = true;
            GameNameBox.Text = SuggestedName();
            _updatingName = false;
            UpdateFolderHint();
        }

        private string SuggestedName()
        {
            if (_item is null) return "Minecraft";
            var loader = _selectedLoader;
            var build = LoaderVersionBox.SelectedItem as LoaderBuild;
            return GameInstanceLayout.MakeId(_item.Title, loader, build?.Version);
        }

        private void UpdateFolderHint()
        {
            var folder = GameInstanceLayout.Sanitize(GameNameBox.Text ?? "");
            if (string.IsNullOrWhiteSpace(folder)) folder = SuggestedName();
            FolderHintText.Text = $"游戏目录将同步为  versions\\{folder}";
        }

        private void ShowTip(string message)
        {
            QueueTip.Text = message;
            QueueTipBorder.Visibility = Visibility.Visible;
        }

        private static Color ParseColor(string hex)
        {
            if (hex.StartsWith("#")) hex = hex[1..];
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex[..2], 16);
                var g = Convert.ToByte(hex[2..4], 16);
                var b = Convert.ToByte(hex[4..], 16);
                return Color.FromArgb(255, r, g, b);
            }
            return Color.FromArgb(255, 91, 135, 49);
        }
    }
}
