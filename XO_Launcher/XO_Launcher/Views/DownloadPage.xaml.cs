using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using XO_Launcher.Api;
using XO_Launcher.Models;
using XO_Launcher.Services;

namespace XO_Launcher.Views
{
    public sealed partial class DownloadPage : UserControl
    {
        private readonly ModrinthApi _modrinthApi = new();
        private readonly MojangApi _mojangApi = new();
        private CancellationTokenSource? _loadCancellation;
        private string _currentCategory = "vanilla";
        private string _vanillaTypeFilter = "all";
        private IReadOnlyList<DownloadItem> _allVanillaItems = Array.Empty<DownloadItem>();

        public DownloadPage()
        {
            InitializeComponent();
            VanillaButton.Click += CategoryButton_Click;
            LoaderButton.Click += CategoryButton_Click;
            ModsButton.Click += CategoryButton_Click;
            ShadersButton.Click += CategoryButton_Click;
            ResourcePacksButton.Click += CategoryButton_Click;
            ModpacksButton.Click += CategoryButton_Click;
            SearchButton.Click += SearchButton_Click;
            SearchBox.KeyDown += SearchBox_KeyDown;
            SearchBox.TextChanged += SearchBox_TextChanged;
            SearchBox.GotFocus += SearchBox_GotFocus;
            SearchBox.LostFocus += SearchBox_LostFocus;
            DetailPage.BackRequested += (_, _) => CloseDetail();
            VanillaDetail.BackRequested += (_, _) => CloseDetail();
            Loaded += (_, _) => _ = LoadCategoryAsync("vanilla");
            LauncherSettings.Instance.Changed += () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_currentCategory is "mod" or "shader" or "resourcepack" or "modpack")
                        _ = LoadCategoryAsync(_currentCategory);
                });
            };
        }

        /// <summary>原版进入版本详情；Modrinth 的 mod 进入详情页。</summary>
        private async void ItemsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not DownloadItem item)
                return;

            if (item.ItemKind == "vanilla")
            {
                DetailPage.Visibility = Visibility.Collapsed;
                VanillaDetail.Visibility = Visibility.Visible;
                DetailOverlay.Visibility = Visibility.Visible;
                VanillaDetail.Show(item);
                return;
            }

            if (item.Slug.Length == 0)
                return;

            VanillaDetail.Visibility = Visibility.Collapsed;
            DetailPage.Visibility = Visibility.Visible;
            DetailOverlay.Visibility = Visibility.Visible;
            await DetailPage.ShowAsync(item);
        }

        private void CloseDetail()
        {
            DetailPage.Cancel();
            VanillaDetail.Cancel();
            DetailOverlay.Visibility = Visibility.Collapsed;
            VanillaDetail.Visibility = Visibility.Collapsed;
            DetailPage.Visibility = Visibility.Visible;
        }

        private void VanillaFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string filter })
            {
                _vanillaTypeFilter = filter;
                ApplyVanillaFilter();
            }
        }

        private async void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string category)
                await LoadCategoryAsync(category);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
            => await RunSearchAsync();

        private async void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                await RunSearchAsync();
        }

        private async void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            await RunSearchAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrWhiteSpace(SearchBox.Text);
            ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
            if (_currentCategory == "vanilla")
                ApplyVanillaFilter();
            else if (_currentCategory == "loader")
                ApplyLoaderFilter();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchBarBorder.BorderBrush = ThemeService.Brush("AccentBrush");
            SearchBarBorder.BorderThickness = new Thickness(1.5);
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SearchBarBorder.BorderBrush = ThemeService.Brush("DividerBrush");
            SearchBarBorder.BorderThickness = new Thickness(1);
        }

        private async Task RunSearchAsync()
        {
            if (_currentCategory == "vanilla")
            {
                ApplyVanillaFilter();
                return;
            }
            if (_currentCategory == "loader")
            {
                ApplyLoaderFilter();
                return;
            }
            await LoadCategoryAsync(_currentCategory);
        }

        private async Task LoadCategoryAsync(string category)
        {
            CloseDetail();
            _currentCategory = category;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var token = _loadCancellation.Token;

            UpdateCategoryState(category);
            ItemsGrid.ItemsSource = null;
            VanillaList.ItemsSource = null;
            var isVanilla = category == "vanilla";
            VanillaFilterPanel.Visibility = isVanilla ? Visibility.Visible : Visibility.Collapsed;
            VanillaList.Visibility = Visibility.Collapsed;
            ItemsGrid.Visibility = isVanilla ? Visibility.Collapsed : Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Visible;
            SourceStatus.Text = "正在获取最新内容...";

            try
            {
                var query = SearchBox.Text;
                if (category is "mod" or "shader" or "resourcepack" or "modpack")
                    query = ModNameTranslationService.ResolveSearchQuery(query);
                var items = category switch
                {
                    "vanilla" => await LoadVanillaAsync(token),
                    "loader" => CreateLoaderItems(),
                    _ => await LoadModrinthItemsAsync(category, query, token)
                };

                if (category == "vanilla")
                {
                    _allVanillaItems = items;
                    ApplyVanillaFilter();
                    SourceStatus.Text = $"Mojang 官方清单 · {items.Count} 个版本";
                }
                else
                {
                    ItemsGrid.ItemsSource = items;
                    ItemsGrid.Visibility = Visibility.Visible;
                    VanillaList.Visibility = Visibility.Collapsed;
                    SourceStatus.Text = category == "loader" ? "加载器可在原版详情页与游戏一起安装" : "Modrinth · 按下载量排序";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LauncherLogService.Error("DownloadPage", "获取版本或模组列表失败", ex);
                SourceStatus.Text = "连接失败，请检查网络后重试";
                ItemsGrid.ItemsSource = new[]
                {
                    new DownloadItem
                    {
                        Title = "暂时无法获取内容",
                        Description = ex.Message,
                        Badge = "网络错误",
                        Meta = "点击搜索按钮重试",
                        IconText = "!",
                        IconColor = "#6B3030"
                    }
                };
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyVanillaFilter()
        {
            IEnumerable<DownloadItem> filtered = _allVanillaItems;
            filtered = _vanillaTypeFilter switch
            {
                "release" => filtered.Where(v => v.VersionType == "release"),
                "snapshot" => filtered.Where(v => v.VersionType == "snapshot"),
                "legacy" => filtered.Where(v => v.VersionType is "old_alpha" or "old_beta"),
                _ => filtered
            };

            var search = SearchBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(v =>
                    v.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (v.Badge?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (v.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var list = filtered.ToList();
            VanillaList.ItemsSource = list;
            VanillaList.Visibility = Visibility.Visible;
            ItemsGrid.Visibility = Visibility.Collapsed;
            VanillaCountText.Text = string.IsNullOrWhiteSpace(search)
                ? $"共 {list.Count} 个版本，点击进入详情后下载"
                : $"找到 {list.Count} 个版本";

            foreach (var button in new[] { FilterAllButton, FilterReleaseButton, FilterSnapshotButton, FilterLegacyButton })
            {
                var active = button.Tag?.ToString() == _vanillaTypeFilter;
                button.Foreground = active
                    ? ThemeService.Brush("AccentBrush")
                    : ThemeService.Brush("TextSecondaryBrush");
            }
        }

        private async Task<IReadOnlyList<DownloadItem>> LoadVanillaAsync(CancellationToken token)
        {
            PageTitle.Text = "原版游戏";
            PageSubtitle.Text = "显示 Mojang 清单中的全部版本。点击进入详情后再下载，并可同时安装模组加载器。";
            var manifest = await _mojangApi.GetVersionManifestAsync(token);

            return manifest.Versions
                .Select(v =>
                {
                    var (badge, desc, color) = v.Type switch
                    {
                        "release" => ("正式版", "Minecraft Java Edition 正式版", "#5B8731"),
                        "snapshot" => ("快照版", "Minecraft Java Edition 快照版", "#496A88"),
                        "old_beta" => ("远古 Beta", "Minecraft 远古 Beta 版本", "#8A6A3A"),
                        "old_alpha" => ("远古 Alpha", "Minecraft 远古 Alpha 版本，可能存在兼容性问题", "#6B3030"),
                        _ => (v.Type, "Minecraft Java Edition", "#2D2D2D")
                    };
                    return new DownloadItem
                    {
                        Title = v.Id,
                        Description = desc,
                        Badge = badge,
                        Meta = FormatDate(v.ReleaseTime),
                        IconText = "MC",
                        IconColor = color,
                        DownloadUrl = v.Url,
                        ItemKind = "vanilla",
                        ProjectType = "vanilla",
                        VersionType = v.Type,
                        HasDetail = true,
                        IsAlpha = v.Type is "old_alpha"
                    };
                })
                .ToList();
        }

        private IReadOnlyList<DownloadItem> CreateLoaderItems()
        {
            PageTitle.Text = "模组加载器";
            PageSubtitle.Text = "在「原版游戏」中打开某个版本的详情页，即可与游戏本体一起安装 Fabric、Forge、NeoForge 或 Quilt。";
            return FilterLoaders(AllLoaderItems());
        }

        private void ApplyLoaderFilter()
        {
            var items = FilterLoaders(AllLoaderItems());
            ItemsGrid.ItemsSource = items;
            ItemsGrid.Visibility = Visibility.Visible;
            VanillaList.Visibility = Visibility.Collapsed;
            SourceStatus.Text = "加载器可在原版详情页与游戏一起安装";
        }

        private IReadOnlyList<DownloadItem> FilterLoaders(IReadOnlyList<DownloadItem> items)
        {
            var search = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(search)) return items;
            return items.Where(v =>
                    v.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (v.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (v.Badge?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        private static IReadOnlyList<DownloadItem> AllLoaderItems()
        {
            return new[]
            {
                new DownloadItem { Title = "Fabric", Description = "轻量、快速且模块化的模组加载器。请到原版详情页与游戏一起安装。", Badge = "推荐 · Fabric", Meta = "支持 1.14 - 1.21.x", IconText = "F", IconColor = "#DB7E35", ItemKind = "loader" },
                new DownloadItem { Title = "Forge", Description = "历史悠久、生态丰富的模组加载器。请到原版详情页与游戏一起安装。", Badge = "Forge", Meta = "支持 1.5 - 1.21.x", IconText = "F", IconColor = "#6B4F3A", ItemKind = "loader" },
                new DownloadItem { Title = "NeoForge", Description = "面向新版本的 Forge 社区分支。请到原版详情页与游戏一起安装。", Badge = "NeoForge", Meta = "支持 1.20.2 - 1.21.x", IconText = "N", IconColor = "#A65D35", ItemKind = "loader" },
                new DownloadItem { Title = "Quilt", Description = "兼容 Fabric 生态的现代模组加载器。请到原版详情页与游戏一起安装。", Badge = "Quilt", Meta = "支持 1.18 - 1.21.x", IconText = "Q", IconColor = "#8D6CC7", ItemKind = "loader" }
            };
        }

        private async Task<IReadOnlyList<DownloadItem>> LoadModrinthItemsAsync(string category, string query, CancellationToken token)
        {
            var typeName = category switch
            {
                "mod" => "模组",
                "shader" => "光影",
                "resourcepack" => "资源包",
                "modpack" => "整合包",
                _ => "资源"
            };
            PageTitle.Text = typeName;
            PageSubtitle.Text = $"从 Modrinth 获取热门{typeName}，按下载量排序。名称将尽量显示为中文。";

            var translation = ModNameTranslationService.EnsureLoadedAsync(token);
            var hits = await _modrinthApi.SearchAsync(category, query, token);
            await translation;
            return hits.Select(ToDownloadItem).ToList();
        }

        private static DownloadItem ToDownloadItem(ModrinthHit hit)
        {
            var color = hit.Color is null or 0 ? "#2D2D2D" : $"#{hit.Color.Value:X6}";
            var icon = string.IsNullOrWhiteSpace(hit.IconUrl) ? null : new BitmapImage(new Uri(hit.IconUrl));
            var translated = ModNameTranslationService.Translate(hit.Title, hit.Slug, hit.Title);
            return new DownloadItem
            {
                Title = translated,
                OriginalTitle = string.Equals(translated, hit.Title, StringComparison.Ordinal) ? "" : hit.Title,
                Description = hit.Description,
                Badge = $"{hit.Author} · {hit.ProjectType}",
                Meta = $"下载 {FormatDownloads(hit.Downloads)}  ·  关注 {FormatDownloads(hit.Follows)}",
                IconText = translated.Length > 0 ? translated[..1].ToUpperInvariant() : "?",
                IconColor = color,
                Icon = icon,
                DownloadUrl = $"https://modrinth.com/{hit.ProjectType}/{hit.Slug}",
                Slug = hit.Slug,
                ProjectType = hit.ProjectType,
                ItemKind = "modrinth",
                HasDetail = true
            };
        }

        private void UpdateCategoryState(string category)
        {
            SearchBox.PlaceholderText = category switch
            {
                "vanilla" => "搜索版本号，例如 1.21.5",
                "loader" => "搜索加载器名称，例如 Fabric",
                "mod" => "搜索模组名称、作者或中文译名",
                "shader" => "搜索光影名称或关键词",
                "resourcepack" => "搜索资源包名称或关键词",
                "modpack" => "搜索整合包名称或关键词",
                _ => "搜索…"
            };
            foreach (var button in new[] { VanillaButton, LoaderButton, ModsButton, ShadersButton, ResourcePacksButton, ModpacksButton })
            {
                button.Foreground = button.Tag?.ToString() == category
                    ? ThemeService.Brush("AccentBrush")
                    : ThemeService.Brush("TextSecondaryBrush");
            }
        }

        private static string FormatDownloads(long value)
        {
            return value switch
            {
                >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
                >= 1_000 => $"{value / 1_000d:0.#}K",
                _ => value.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static string FormatDate(string value)
        {
            return DateTimeOffset.TryParse(value, out var date) ? date.ToString("yyyy-MM-dd") : "官方版本";
        }
    }
}
