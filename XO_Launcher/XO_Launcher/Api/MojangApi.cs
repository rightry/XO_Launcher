using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XO_Launcher.Models;

namespace XO_Launcher.Api
{
    public sealed class MojangApi
    {
        private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XO-Launcher/1.0 (Minecraft launcher)");
            return client;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<MojangVersionManifest> GetVersionManifestAsync(CancellationToken cancellationToken = default)
        {
            using var response = await Client.GetAsync(ManifestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<MojangVersionManifest>(stream, JsonOptions, cancellationToken)
                   ?? new MojangVersionManifest();
        }
    }
}
