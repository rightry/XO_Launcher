using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XO_Launcher.Models;

namespace XO_Launcher.Api
{
    public sealed class ModrinthApi
    {
        private const string Endpoint = "https://api.modrinth.com/v2/search";
        private static readonly HttpClient Client = CreateClient();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XO-Launcher/1.0 (Minecraft launcher UI)");
            return client;
        }

        public async Task<IReadOnlyList<ModrinthHit>> SearchAsync(
            string projectType,
            string? query = null,
            CancellationToken cancellationToken = default)
        {
            var facets = Uri.EscapeDataString($"[[\"project_type:{projectType}\"]]");
            var encodedQuery = Uri.EscapeDataString(query?.Trim() ?? "");
            var url = $"{Endpoint}?facets={facets}&query={encodedQuery}&limit=24&index=downloads";

            using var response = await Client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<ModrinthSearchResponse>(stream, JsonOptions, cancellationToken);
            return result?.Hits ?? new List<ModrinthHit>();
        }

        /// <summary>获取项目详情（GET /v2/project/{id|slug}）。</summary>
        public async Task<ModrinthProject?> GetProjectAsync(
            string idOrSlug,
            CancellationToken cancellationToken = default)
        {
            var url = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(idOrSlug)}";
            using var response = await Client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<ModrinthProject>(stream, JsonOptions, cancellationToken);
        }

        /// <summary>获取项目的全部版本（GET /v2/project/{id|slug}/version）。</summary>
        public async Task<IReadOnlyList<ModrinthVersion>> GetVersionsAsync(
            string idOrSlug,
            CancellationToken cancellationToken = default)
        {
            var url = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(idOrSlug)}/version";
            return await GetVersionsFromUrlAsync(url, cancellationToken);
        }

        /// <summary>按游戏版本与加载器筛选，返回最新的兼容版本（优先正式版）。</summary>
        public async Task<ModrinthVersion?> GetLatestCompatibleAsync(
            string idOrSlug,
            string gameVersion,
            string loader,
            CancellationToken cancellationToken = default)
        {
            var gameVersions = Uri.EscapeDataString($"[\"{gameVersion}\"]");
            var loaders = Uri.EscapeDataString($"[\"{loader}\"]");
            var url = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(idOrSlug)}/version?game_versions={gameVersions}&loaders={loaders}";
            var versions = await GetVersionsFromUrlAsync(url, cancellationToken);
            return versions
                .Where(v => v.PrimaryFile is not null && !string.IsNullOrWhiteSpace(v.PrimaryFile.Url))
                .OrderBy(v => v.VersionType.ToLowerInvariant() switch
                {
                    "release" => 0,
                    "beta" => 1,
                    _ => 2
                })
                .ThenByDescending(v => DateTimeOffset.TryParse(v.DatePublished, out var date) ? date : DateTimeOffset.MinValue)
                .FirstOrDefault();
        }

        private async Task<IReadOnlyList<ModrinthVersion>> GetVersionsFromUrlAsync(string url, CancellationToken cancellationToken)
        {
            using var response = await Client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<List<ModrinthVersion>>(stream, JsonOptions, cancellationToken);
            return result ?? new List<ModrinthVersion>();
        }
    }
}
