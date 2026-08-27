using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XO_Launcher.Services
{
    /// <summary>从模组 jar 中读取模组 ID 与显示名称。</summary>
    public static class ModMetadataReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static (string Id, string Name) Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return ("", "");
            var ext = Path.GetExtension(path);
            if (!ext.Equals(".jar", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".litemod", StringComparison.OrdinalIgnoreCase))
                return ("", "");

            try
            {
                using var zip = ZipFile.OpenRead(path);
                var fabric = ReadJson(zip, "fabric.mod.json");
                if (fabric is not null)
                    return (fabric.Id ?? "", fabric.Name ?? "");

                var quilt = ReadJson(zip, "quilt.mod.json");
                if (quilt is not null)
                    return (quilt.Id ?? "", quilt.Name ?? "");

                var toml = ReadToml(zip, "META-INF/neoforge.mods.toml")
                           ?? ReadToml(zip, "META-INF/mods.toml");
                if (toml is not null)
                    return toml.Value;

                var legacy = ReadMcmodInfo(zip);
                if (legacy is not null)
                    return legacy.Value;
            }
            catch (Exception)
            {
                // 损坏或不完整的压缩包忽略
            }

            return ("", "");
        }

        private static FabricInfo? ReadJson(ZipArchive zip, string entryName)
        {
            var entry = zip.GetEntry(entryName);
            if (entry is null) return null;
            using var stream = entry.Open();
            return JsonSerializer.Deserialize<FabricInfo>(stream, JsonOptions);
        }

        private static (string Id, string Name)? ReadToml(ZipArchive zip, string entryName)
        {
            var entry = zip.GetEntry(entryName);
            if (entry is null) return null;
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var text = reader.ReadToEnd();
            var id = MatchToml(text, "modId");
            var name = MatchToml(text, "displayName");
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                return null;
            return (id, name);
        }

        private static (string Id, string Name)? ReadMcmodInfo(ZipArchive zip)
        {
            var entry = zip.GetEntry("mcmod.info");
            if (entry is null) return null;
            using var stream = entry.Open();
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                root = root[0];
            var id = root.TryGetProperty("modid", out var idEl) ? idEl.GetString() ?? "" : "";
            var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                return null;
            return (id, name);
        }

        private static string MatchToml(string text, string key)
        {
            var match = Regex.Match(text, $@"^{key}\s*=\s*""([^""]*)""", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private sealed class FabricInfo
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
        }
    }
}
