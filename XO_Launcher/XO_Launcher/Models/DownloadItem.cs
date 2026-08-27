using Microsoft.UI.Xaml.Media;
using System;

namespace XO_Launcher.Models
{
    /// <summary>
    /// 下载页列表项的统一展示模型：原版版本、模组加载器、Modrinth 项目均转换为该模型显示。
    /// </summary>
    public class DownloadItem
    {
        public string Title { get; set; } = "";
        public string OriginalTitle { get; set; } = "";
        public bool HasTranslatedTitle =>
            !string.IsNullOrWhiteSpace(OriginalTitle)
            && !string.Equals(OriginalTitle, Title, StringComparison.OrdinalIgnoreCase);
        public string Description { get; set; } = "";
        public string Badge { get; set; } = "";
        public string Meta { get; set; } = "";

        /// <summary>图标占位符文字（远程图标加载失败时的兜底）。</summary>
        public string IconText { get; set; } = "";

        /// <summary>图标占位符底色（Modrinth 项目直接使用其主题色）。</summary>
        public string IconColor { get; set; } = "#2D2D2D";

        /// <summary>远程图标（Modrinth CDN）。</summary>
        public ImageSource? Icon { get; set; }

        /// <summary>点击下载/打开时跳转的链接。</summary>
        public string DownloadUrl { get; set; } = "";

        /// <summary>Modrinth 项目标识（slug 或 id）。为空表示非 Modrinth 项目（原版/加载器）。</summary>
        public string Slug { get; set; } = "";

        /// <summary>Modrinth 项目类型（mod / shader / resourcepack / modpack）或 vanilla。</summary>
        public string ProjectType { get; set; } = "";

        /// <summary>列表项种类：modrinth / vanilla / loader。</summary>
        public string ItemKind { get; set; } = "";

        /// <summary>Mojang 版本类型：release / snapshot / old_beta / old_alpha。</summary>
        public string VersionType { get; set; } = "";

        public bool HasDetail { get; set; }
        public bool IsAlpha { get; set; }
    }
}
