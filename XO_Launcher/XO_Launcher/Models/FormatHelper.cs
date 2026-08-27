using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace XO_Launcher.Models
{
    public static class FormatHelper
    {
        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int i = 0;
            while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
            return $"{value:0.##} {units[i]}";
        }

        public static string FormatCompactNumber(long value)
        {
            return value switch
            {
                >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
                >= 1_000 => $"{value / 1_000d:0.#}K",
                _ => value.ToString("N0")
            };
        }

        public static string FormatDate(string iso)
        {
            return DateTimeOffset.TryParse(iso, out var d) ? d.ToString("yyyy-MM-dd") : "";
        }

        /// <summary>比较 Minecraft 版本号（如 1.21.5 > 1.20.6）。</summary>
        public static int CompareMcVersion(string a, string b)
        {
            var pa = ParseNumericParts(a);
            var pb = ParseNumericParts(b);
            if (pa is not null && pb is not null)
            {
                var n = Math.Max(pa.Length, pb.Length);
                for (int i = 0; i < n; i++)
                {
                    var x = i < pa.Length ? pa[i] : 0;
                    var y = i < pb.Length ? pb[i] : 0;
                    if (x != y) return x.CompareTo(y);
                }
                return 0;
            }
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>将 Modrinth loader id 转为界面显示名。</summary>
        public static string FormatLoaderName(string loader)
        {
            if (string.IsNullOrWhiteSpace(loader) || loader == "其他加载器")
                return "其他加载器";

            return loader.Trim().ToLowerInvariant() switch
            {
                "fabric" => "Fabric",
                "forge" => "Forge",
                "neoforge" => "NeoForge",
                "quilt" => "Quilt",
                "liteloader" => "LiteLoader",
                "rift" => "Rift",
                "vanilla" => "原版",
                "minecraft" => "Minecraft",
                "paper" => "Paper",
                "spigot" => "Spigot",
                "bukkit" => "Bukkit",
                "purpur" => "Purpur",
                "sponge" => "Sponge",
                "velocity" => "Velocity",
                "bungeecord" => "BungeeCord",
                "waterfall" => "Waterfall",
                _ => char.ToUpperInvariant(loader[0]) + loader[1..]
            };
        }

        /// <summary>加载器栏排序：常见加载器靠前，未知项按名称，无加载器垫底。</summary>
        public static int LoaderSortOrder(string loader)
        {
            return loader.Trim().ToLowerInvariant() switch
            {
                "fabric" => 0,
                "quilt" => 1,
                "forge" => 2,
                "neoforge" => 3,
                "liteloader" => 4,
                "rift" => 5,
                "vanilla" or "minecraft" => 6,
                "其他加载器" => 100,
                _ => 50
            };
        }

        private static int[]? ParseNumericParts(string s)
        {
            var parts = s.Split('.');
            if (!parts.All(p => int.TryParse(p, out _))) return null;
            return parts.Select(int.Parse).ToArray();
        }

        public static bool IsMinecraftRelease(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "其他版本") return false;
            return Regex.IsMatch(id.Trim(), @"^\d+\.\d+(?:\.\d+)?$");
        }

        public static bool IsMinecraftSnapshot(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "其他版本") return false;
            return !IsMinecraftRelease(id);
        }
    }
}