using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using XO_Launcher.Models;

namespace XO_Launcher.Api
{
    /// <summary>
    /// 模组加载器元数据。接口与 HMCL / Prism / XMCL 相同：
    /// Fabric Meta、Quilt Meta、Forge Maven、NeoForge Maven。
    /// </summary>
    public sealed class LoaderMetaApi
    {
        private static readonly HttpClient Client = CreateClient();
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XO-Launcher/1.0 (Minecraft launcher)");
            return client;
        }

        public Task<IReadOnlyList<LoaderBuild>> GetBuildsAsync(string loader, string minecraftVersion, CancellationToken ct = default)
            => loader switch
            {
                "fabric" => GetFabricAsync(minecraftVersion, ct),
                "quilt" => GetQuiltAsync(minecraftVersion, ct),
                "forge" => GetForgeAsync(minecraftVersion, ct),
                "neoforge" => GetNeoForgeAsync(minecraftVersion, ct),
                _ => Task.FromResult<IReadOnlyList<LoaderBuild>>(Array.Empty<LoaderBuild>())
            };

        public static string FabricProfileUrl(string mc, string loader)
            => $"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(mc)}/{Uri.EscapeDataString(loader)}/profile/json";

        public static string QuiltProfileUrl(string mc, string loader)
            => $"https://meta.quiltmc.org/v3/versions/loader/{Uri.EscapeDataString(mc)}/{Uri.EscapeDataString(loader)}/profile/json";

        private async Task<IReadOnlyList<LoaderBuild>> GetFabricAsync(string mc, CancellationToken ct)
        {
            var url = $"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(mc)}";
            using var response = await Client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<LoaderBuild>();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var rows = await JsonSerializer.DeserializeAsync<List<FabricLoaderRow>>(stream, JsonOptions, ct)
                       ?? new List<FabricLoaderRow>();
            return rows
                .Where(r => r.Loader is not null && !string.IsNullOrWhiteSpace(r.Loader.Version))
                .Select(r => new LoaderBuild
                {
                    Loader = "fabric",
                    Version = r.Loader!.Version,
                    MavenVersion = r.Loader.Version,
                    Stable = r.Loader.Stable,
                    Display = r.Loader.Stable ? $"{r.Loader.Version}  ·  稳定" : r.Loader.Version
                })
                .ToList();
        }

        private async Task<IReadOnlyList<LoaderBuild>> GetQuiltAsync(string mc, CancellationToken ct)
        {
            var url = $"https://meta.quiltmc.org/v3/versions/loader/{Uri.EscapeDataString(mc)}";
            using var response = await Client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<LoaderBuild>();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var rows = await JsonSerializer.DeserializeAsync<List<QuiltLoaderRow>>(stream, JsonOptions, ct)
                       ?? new List<QuiltLoaderRow>();
            return rows
                .Where(r => r.Loader is not null && !string.IsNullOrWhiteSpace(r.Loader.Version))
                .Select(r => new LoaderBuild
                {
                    Loader = "quilt",
                    Version = r.Loader!.Version,
                    MavenVersion = r.Loader.Version,
                    Display = r.Loader.Version
                })
                .ToList();
        }

        private async Task<IReadOnlyList<LoaderBuild>> GetForgeAsync(string mc, CancellationToken ct)
        {
            const string meta = "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
            var versions = await ReadMavenVersionsAsync(meta, ct);
            var prefix = mc + "-";
            return versions
                .Where(v => v.StartsWith(prefix, StringComparison.Ordinal))
                .Reverse()
                .Take(40)
                .Select(v =>
                {
                    var build = v[prefix.Length..];
                    return new LoaderBuild
                    {
                        Loader = "forge",
                        Version = build,
                        MavenVersion = v,
                        Display = build,
                        InstallerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{v}/forge-{v}-installer.jar"
                    };
                })
                .ToList();
        }

        private async Task<IReadOnlyList<LoaderBuild>> GetNeoForgeAsync(string mc, CancellationToken ct)
        {
            var modern = await ReadMavenVersionsAsync(
                "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml", ct);
            var prefix = NeoForgePrefix(mc);
            var builds = modern
                .Where(v => prefix.Length > 0 && v.StartsWith(prefix, StringComparison.Ordinal)
                            && !v.Contains("beta", StringComparison.OrdinalIgnoreCase))
                .Reverse()
                .Take(40)
                .Select(v => new LoaderBuild
                {
                    Loader = "neoforge",
                    Version = v,
                    MavenVersion = v,
                    Display = v,
                    InstallerUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{v}/neoforge-{v}-installer.jar"
                })
                .ToList();

            if (builds.Count > 0) return builds;

            // 1.20.1 早期 NeoForge 使用 net.neoforged:forge
            var legacy = await ReadMavenVersionsAsync(
                "https://maven.neoforged.net/releases/net/neoforged/forge/maven-metadata.xml", ct);
            var legacyPrefix = mc + "-";
            return legacy
                .Where(v => v.StartsWith(legacyPrefix, StringComparison.Ordinal))
                .Reverse()
                .Take(40)
                .Select(v => new LoaderBuild
                {
                    Loader = "neoforge",
                    Version = v[legacyPrefix.Length..],
                    MavenVersion = v,
                    Display = v[legacyPrefix.Length..],
                    InstallerUrl = $"https://maven.neoforged.net/releases/net/neoforged/forge/{v}/forge-{v}-installer.jar"
                })
                .ToList();
        }

        private static string NeoForgePrefix(string mcVersion)
        {
            var core = mcVersion.Split('-')[0];
            var parts = core.Split('.');
            if (parts.Length < 2 || parts[0] != "1") return "";
            var minor = parts[1];
            var patch = parts.Length > 2 ? parts[2] : "0";
            return $"{minor}.{patch}.";
        }

        private static async Task<List<string>> ReadMavenVersionsAsync(string url, CancellationToken ct)
        {
            try
            {
                var xml = await Client.GetStringAsync(url, ct);
                var doc = XDocument.Parse(xml);
                return doc.Descendants("version").Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        private sealed class FabricLoaderRow
        {
            public FabricLoaderInfo? Loader { get; set; }
        }

        private sealed class FabricLoaderInfo
        {
            public string Version { get; set; } = "";
            public bool Stable { get; set; }
        }

        private sealed class QuiltLoaderRow
        {
            public QuiltLoaderInfo? Loader { get; set; }
        }

        private sealed class QuiltLoaderInfo
        {
            public string Version { get; set; } = "";
        }
    }
}
