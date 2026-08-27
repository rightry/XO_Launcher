namespace XO_Launcher.Models
{
    /// <summary>某个 Minecraft 版本可用的模组加载器构建（Fabric / Forge / NeoForge / Quilt）。</summary>
    public sealed class LoaderBuild
    {
        public string Loader { get; set; } = "";
        public string Version { get; set; } = "";
        public string Display { get; set; } = "";
        public bool Stable { get; set; }
        public string MavenVersion { get; set; } = "";
        public string InstallerUrl { get; set; } = "";
    }
}
