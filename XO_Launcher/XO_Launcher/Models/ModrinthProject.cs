using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace XO_Launcher.Models
{
    /// <summary>Modrinth API v2 搜索结果（GET /v2/search）。</summary>
    public class ModrinthSearchResponse
    {
        public List<ModrinthHit> Hits { get; set; } = new();
        public int Offset { get; set; }
        public int Limit { get; set; }
        [JsonPropertyName("total_hits")]
        public long TotalHits { get; set; }
    }

    /// <summary>Modrinth 搜索结果中的单个项目（Project）。</summary>
    public class ModrinthHit
    {
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = "";

        [JsonPropertyName("project_type")]
        public string ProjectType { get; set; } = "";

        public string Slug { get; set; } = "";
        public string Author { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Categories { get; set; } = new();
        public long Downloads { get; set; }
        public long Follows { get; set; }

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("date_created")]
        public string DateCreated { get; set; } = "";

        [JsonPropertyName("date_modified")]
        public string DateModified { get; set; } = "";

        public int? Color { get; set; }
    }

    /// <summary>Modrinth 项目详情（GET /v2/project/{id}）。</summary>
    public class ModrinthProject
    {
        public string Id { get; set; } = "";
        public string Slug { get; set; } = "";

        [JsonPropertyName("project_type")]
        public string ProjectType { get; set; } = "";

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Body { get; set; } = "";

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        public long Downloads { get; set; }
        public long Followers { get; set; }
        public string Author { get; set; } = "";
        public List<string> Categories { get; set; } = new();

        [JsonPropertyName("game_versions")]
        public List<string> GameVersions { get; set; } = new();

        public List<string> Loaders { get; set; } = new();

        [JsonPropertyName("source_url")]
        public string SourceUrl { get; set; } = "";

        [JsonPropertyName("issues_url")]
        public string IssuesUrl { get; set; } = "";

        public string Published { get; set; } = "";
        public string Updated { get; set; } = "";
        public int? Color { get; set; }
    }

    /// <summary>Modrinth 项目版本（GET /v2/project/{id}/version）。</summary>
    public class ModrinthVersion
    {
        public string Id { get; set; } = "";

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = "";

        public string Name { get; set; } = "";

        [JsonPropertyName("version_number")]
        public string VersionNumber { get; set; } = "";

        [JsonPropertyName("version_type")]
        public string VersionType { get; set; } = "";

        public string Status { get; set; } = "";
        public string Changelog { get; set; } = "";

        [JsonPropertyName("date_published")]
        public string DatePublished { get; set; } = "";

        public long Downloads { get; set; }
        public bool Featured { get; set; }

        [JsonPropertyName("game_versions")]
        public List<string> GameVersions { get; set; } = new();

        public List<string> Loaders { get; set; } = new();
        public List<ModrinthVersionFile> Files { get; set; } = new();

        /// <summary>主要下载文件（标记 primary，否则取第一个）。</summary>
        public ModrinthVersionFile? PrimaryFile =>
            Files.FirstOrDefault(f => f.Primary) ?? Files.FirstOrDefault();

        public string LoaderText => string.Join(" · ", Loaders);
        public string FormattedDate =>
            System.DateTimeOffset.TryParse(DatePublished, out var d) ? d.ToString("yyyy-MM-dd") : "";
        public string FormattedSize => PrimaryFile is null ? "未知大小" : FormatHelper.FormatBytes(PrimaryFile.Size);
        public string FormattedDownloads => FormatHelper.FormatCompactNumber(Downloads);

        public bool IsAlpha =>
            string.Equals(VersionType, "alpha", System.StringComparison.OrdinalIgnoreCase)
            || VersionNumber.Contains("alpha", System.StringComparison.OrdinalIgnoreCase)
            || Name.Contains("alpha", System.StringComparison.OrdinalIgnoreCase);

        public string VersionTypeLabel
        {
            get
            {
                if (IsAlpha) return "Alpha";
                return VersionType.Trim().ToLowerInvariant() switch
                {
                    "beta" => "Beta",
                    "release" => "Release",
                    "" => "",
                    _ => char.ToUpperInvariant(VersionType[0]) + VersionType[1..]
                };
            }
        }
    }

    /// <summary>Modrinth 版本中的下载文件。</summary>
    public class ModrinthVersionFile
    {
        public Dictionary<string, string> Hashes { get; set; } = new();
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "";
        public bool Primary { get; set; }
        public long Size { get; set; }
    }

    /// <summary>详情页内某一加载器下的文件列表。</summary>
    public class LoaderVersionGroup
    {
        public string LoaderId { get; set; } = "";
        public string LoaderName { get; set; } = "";
        public List<ModrinthVersion> Versions { get; set; } = new();
        public bool IsExpanded { get; set; }
        public string VersionCountText => $"{Versions.Count} 个文件";
    }

    /// <summary>详情页按 Minecraft 版本分组，其下再按模组加载器分栏。</summary>
    public class McVersionGroup
    {
        public string MinecraftVersion { get; set; } = "";
        public List<LoaderVersionGroup> Loaders { get; set; } = new();
        public bool IsExpanded { get; set; }
        public string LoaderSummary => string.Join(" / ", Loaders.Select(l => l.LoaderName));
        public string VersionCountText => $"{Loaders.Sum(l => l.Versions.Count)} 个文件 · {Loaders.Count} 种加载器";
    }

    /// <summary>设置页开源致谢卡片模型。</summary>
    public class CreditItem
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconText { get; set; } = "";
        public string IconColor { get; set; } = "#2D2D2D";
        public string Url { get; set; } = "";
        public string RepoText { get; set; } = "";
        public System.Uri? GitHubUri =>
            System.Uri.TryCreate(Url, System.UriKind.Absolute, out var uri) ? uri : null;
    }
}
