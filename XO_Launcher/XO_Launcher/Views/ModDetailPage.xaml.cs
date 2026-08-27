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
    public sealed partial class ModDetailPage : UserControl
    {
        private readonly ModrinthApi _api = new();
        private CancellationTokenSource? _cts;
        private string _currentTitle = "";
        private string _projectType = "";
        private string _projectId = "";
        private string _channel = "release";
        private IReadOnlyList<ModrinthVersion> _allVersions = Array.Empty<ModrinthVersion>();

        public event EventHandler? BackRequested;

        public ModDetailPage()
        {
            InitializeComponent();
            IconImage.ImageFailed += (_, _) =>
            {
                IconImage.Source = null;
                IconFallback.Visibility = Visibility.Visible;
            };
            BackButton.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
            UpdateChannelButtons();
            UpdateLibraryPath();
        }

        /// <summary>加载 mod 详情并展示。在用户点击列表项后调用。</summary>
        public async Task ShowAsync(DownloadItem item)
        {
            _currentTitle = item.Title;
            _projectType = item.ProjectType;
            _channel = "release";
            _allVersions = Array.Empty<ModrinthVersion>();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            TitleText.Text = item.Title;
            BadgeText.Text = item.HasTranslatedTitle
                ? item.OriginalTitle + (string.IsNullOrWhiteSpace(item.Badge) ? "" : "  ·  " + item.Badge)
                : item.Badge;
            StatsText.Text = item.Meta;
            DescriptionText.Text = item.Description;
            BodyMarkdown.Blocks.Clear();
            LoaderItems.ItemsSource = null;
            GameVersionItems.ItemsSource = null;
            VersionGroups.ItemsSource = null;
            EmptyVersionText.Visibility = Visibility.Collapsed;
            QueueTipBorder.Visibility = Visibility.Collapsed;
            IconFallback.Visibility = Visibility.Visible;
            IconFallback.Text = item.IconText;
            if (IntroExpander is not null)
                IntroExpander.IsExpanded = false;
            UpdateChannelButtons();
            UpdateLibraryPath();

            if (item.Icon is not null)
            {
                IconImage.Source = item.Icon;
                IconFallback.Visibility = Visibility.Collapsed;
            }
            else
            {
                IconImage.Source = null;
                IconBorder.Background = new SolidColorBrush(ParseColor(item.IconColor));
            }

            LoadingPanel.Visibility = Visibility.Visible;
            ContentScrollViewer.Visibility = Visibility.Collapsed;

            try
            {
                var project = await _api.GetProjectAsync(item.Slug, token);
                var versions = await _api.GetVersionsAsync(item.Slug, token);

                if (project is not null)
                {
                    if (!string.IsNullOrWhiteSpace(project.Id))
                        _projectId = project.Id;
                    if (!string.IsNullOrWhiteSpace(project.ProjectType))
                        _projectType = project.ProjectType;

                    StatsText.Text = $"下载 {FormatHelper.FormatCompactNumber(project.Downloads)} · 关注 {FormatHelper.FormatCompactNumber(project.Followers)} · 更新于 {FormatHelper.FormatDate(project.Updated)}";
                    DescriptionText.Text = project.Description;
                    MarkdownRenderer.Render(BodyMarkdown, project.Body);
                    LoaderItems.ItemsSource = project.Loaders;
                    GameVersionItems.ItemsSource = project.GameVersions.Take(12).ToList();
                    UpdateLibraryPath();
                }

                _allVersions = versions;
                ApplyChannelFilter();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LauncherLogService.Error("ModDetail", "加载模组版本列表失败", ex);
                MarkdownRenderer.Render(BodyMarkdown, "");
                EmptyVersionText.Text = "加载失败：" + ex.Message;
                EmptyVersionText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ContentScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private void ChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string channel }) return;
            if (_channel == channel) return;
            _channel = channel;
            UpdateChannelButtons();
            ApplyChannelFilter();
        }

        private void UpdateChannelButtons()
        {
            VersionSectionTitle.Text = _channel == "snapshot"
                ? "按游戏版本与加载器选择快照版"
                : "按游戏版本与加载器选择正式版";

            foreach (var button in new[] { ChannelReleaseButton, ChannelSnapshotButton })
            {
                var active = button.Tag?.ToString() == _channel;
                button.Foreground = active
                    ? ThemeService.Brush("AccentBrush")
                    : ThemeService.Brush("TextSecondaryBrush");
            }
        }

        private void UpdateLibraryPath()
        {
            var noun = ContentKind.DisplayName(_projectType);
            var dir = ModLibraryService.GetDirectory(_projectType);
            LibraryPathText.Text = $"保存位置：{dir}";
            VersionSectionHint.Text = $"文件将保存至资源下载目录，不会自动安装。请在版本设置的「{ContentKind.ManagementTitle(_projectType)}」中选择并安装。进度请在「消息」中查看。";
            if (IntroTitleText is not null)
                IntroTitleText.Text = noun + "介绍";
            if (LoadingStatusText is not null)
                LoadingStatusText.Text = "正在加载详情…";
        }

        private void ApplyChannelFilter()
        {
            var groups = BuildGroups(_allVersions, _channel);
            VersionGroups.ItemsSource = groups;
            if (groups.Count == 0)
            {
                EmptyVersionText.Text = _channel == "snapshot"
                    ? "暂无适用于快照版的文件"
                    : "暂无适用于 Minecraft 正式版的文件";
                EmptyVersionText.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyVersionText.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>仅在详情页内选择具体文件版本后加入下载队列，进度在「消息」页展示。</summary>
        private void DownloadVersionButton_Click(object sender, RoutedEventArgs e)
        {
            var version = (sender as Button)?.Tag as ModrinthVersion
                          ?? (sender as Button)?.DataContext as ModrinthVersion;
            if (version is null)
            {
                ShowQueueTip("无法识别所选文件。");
                return;
            }

            var file = version.PrimaryFile;
            if (file is null || string.IsNullOrWhiteSpace(file.Filename))
            {
                ShowQueueTip("该版本暂无可下载文件。");
                return;
            }

            var url = ResolveFileUrl(version, file);
            if (string.IsNullOrWhiteSpace(url))
            {
                ShowQueueTip("下载地址为空，请稍后重试。");
                return;
            }

            if (DownloadService.Instance.IsDuplicate(file.Filename))
            {
                ShowQueueTip("该文件已在下载队列中，请至「消息」查看进度。");
                return;
            }

            var dest = ModLibraryService.GetDirectory(_projectType);
            try
            {
                DownloadService.Instance.Enqueue(
                    _currentTitle,
                    file.Filename,
                    url,
                    file.Size,
                    dest,
                    _projectType);
                ShowQueueTip($"已加入下载队列：{file.Filename}。完成后将保存至下载库，请在版本设置中安装。");
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("ModDetail", "加入资源下载失败", ex);
                ShowQueueTip("无法开始下载：" + ex.Message);
            }
        }

        private string ResolveFileUrl(ModrinthVersion version, ModrinthVersionFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.Url)) return file.Url;
            var projectId = !string.IsNullOrWhiteSpace(version.ProjectId) ? version.ProjectId : _projectId;
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(version.Id) || string.IsNullOrWhiteSpace(file.Filename))
                return "";
            return $"https://cdn.modrinth.com/data/{projectId}/versions/{version.Id}/{Uri.EscapeDataString(file.Filename)}";
        }

        private void ShowQueueTip(string message)
        {
            QueueTip.Text = message;
            QueueTipBorder.Visibility = Visibility.Visible;
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        private static List<McVersionGroup> BuildGroups(IReadOnlyList<ModrinthVersion> versions, string channel)
        {
            bool includeMc(string mc) => channel == "snapshot"
                ? FormatHelper.IsMinecraftSnapshot(mc)
                : FormatHelper.IsMinecraftRelease(mc);

            var filtered = versions
                .Where(v => v.PrimaryFile is not null
                            && v.Status is "listed" or "approved" or "archived" or "")
                .ToList();

            var groups = filtered
                .SelectMany(v =>
                {
                    var mcs = v.GameVersions.Count > 0 ? v.GameVersions : new List<string> { "其他版本" };
                    var loaders = v.Loaders.Count > 0 ? v.Loaders : new List<string> { "其他加载器" };
                    return mcs
                        .Where(includeMc)
                        .SelectMany(mc => loaders.Select(loader => (Mc: mc, Loader: loader, Version: v)));
                })
                .GroupBy(x => x.Mc, StringComparer.OrdinalIgnoreCase)
                .Select(mcGroup => new McVersionGroup
                {
                    MinecraftVersion = mcGroup.Key,
                    Loaders = mcGroup
                        .GroupBy(x => x.Loader, StringComparer.OrdinalIgnoreCase)
                        .Select(lg => new LoaderVersionGroup
                        {
                            LoaderId = lg.Key,
                            LoaderName = FormatHelper.FormatLoaderName(lg.Key),
                            Versions = lg.Select(x => x.Version)
                                .DistinctBy(v => v.Id)
                                .OrderByDescending(v => v.DatePublished)
                                .ToList()
                        })
                        .OrderBy(lg => FormatHelper.LoaderSortOrder(lg.LoaderId))
                        .ThenBy(lg => lg.LoaderName, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .OrderBy(g => g.MinecraftVersion == "其他版本")
                .ThenByDescending(g => g.MinecraftVersion, Comparer<string>.Create(FormatHelper.CompareMcVersion))
                .ToList();

            var installedMc = new HashSet<string>(
                InstalledVersionService.Scan().Select(v => v.MinecraftVersion),
                StringComparer.OrdinalIgnoreCase);

            var anyInstalled = false;
            for (var i = 0; i < groups.Count; i++)
            {
                groups[i].IsExpanded = installedMc.Contains(groups[i].MinecraftVersion);
                if (groups[i].IsExpanded) anyInstalled = true;
            }

            if (!anyInstalled)
            {
                for (var i = 0; i < groups.Count && i < 2; i++)
                    groups[i].IsExpanded = true;
            }

            foreach (var group in groups.Where(g => g.IsExpanded))
            {
                foreach (var loader in group.Loaders)
                    loader.IsExpanded = true;
            }

            return groups;
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
            return Color.FromArgb(255, 45, 45, 45);
        }
    }
}
