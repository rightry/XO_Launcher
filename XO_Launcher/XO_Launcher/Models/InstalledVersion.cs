namespace XO_Launcher.Models
{
    /// <summary>游戏目录 versions 下已安装的版本（原版与加载器共用同一套列表）。</summary>
    public sealed class InstalledVersion
    {
        public string Id { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
        public string LoaderId { get; set; } = "vanilla";
        public string LoaderVersion { get; set; } = "";
        public string Type { get; set; } = "";
        public string JsonPath { get; set; } = "";
        public string? InheritsFrom { get; set; }
        public string? CustomName { get; set; }

        public string LoaderLabel
        {
            get
            {
                if (LoaderId == "vanilla")
                    return Type switch
                    {
                        "snapshot" => "原版 · 快照",
                        "old_alpha" => "原版 · Alpha",
                        "old_beta" => "原版 · Beta",
                        _ => "原版"
                    };
                var name = FormatHelper.FormatLoaderName(LoaderId);
                return string.IsNullOrWhiteSpace(LoaderVersion) ? name : $"{name} {LoaderVersion}";
            }
        }

        public string DisplayName => !string.IsNullOrWhiteSpace(CustomName)
            ? CustomName
            : LoaderId == "vanilla"
                ? MinecraftVersion
                : $"{MinecraftVersion}  ·  {LoaderLabel}";
    }
}
