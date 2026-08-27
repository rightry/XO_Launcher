using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XO_Launcher.Api;
using XO_Launcher.Models;

namespace XO_Launcher.Services
{
    /// <summary>
    /// 安装 Minecraft 原版与模组加载器到同一个游戏文件夹（HMCL 版本隔离 / Prism instance）：
    /// versions/{id}/{id}.json（已合并，无 inheritsFrom）、{id}.jar（原版客户端），
    /// 以及 mods / config / saves 等；libraries 与 assets 仍放在共享的 .minecraft 下。
    /// </summary>
    public sealed class GameInstallService
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XO-Launcher/1.0 (Minecraft launcher)");
            return client;
        }

        public async Task<string> InstallAsync(
            MinecraftVersion vanilla,
            string? loader,
            LoaderBuild? loaderBuild,
            DownloadTask task,
            string? instanceName = null,
            bool installFabricApi = true,
            CancellationToken cancellationToken = default)
        {
            string? instanceDir = null;
            try
            {
            var gameDir = LauncherSettings.Instance.GameDirectory;
            Directory.CreateDirectory(gameDir);
            Directory.CreateDirectory(Path.Combine(gameDir, "libraries"));
            Directory.CreateDirectory(Path.Combine(gameDir, "assets", "indexes"));

            Report(task, "正在获取版本元数据...", 2);
            if (string.IsNullOrWhiteSpace(vanilla.Url))
                throw new InvalidOperationException("版本元数据地址为空");

            var vanillaJson = await Client.GetStringAsync(vanilla.Url, cancellationToken);
            var withLoader = !string.IsNullOrWhiteSpace(loader) && loader != "vanilla" && loaderBuild is not null;

            string? loaderJson = null;
            var stagedVanilla = false;
            string? installerVersionDir = null;

            if (withLoader)
            {
                if (loader == "fabric")
                {
                    loaderJson = await FetchMetaProfileAsync(
                        LoaderMetaApi.FabricProfileUrl(vanilla.Id, loaderBuild!.Version), "Fabric", task, cancellationToken);
                }
                else if (loader == "quilt")
                {
                    loaderJson = await FetchMetaProfileAsync(
                        LoaderMetaApi.QuiltProfileUrl(vanilla.Id, loaderBuild!.Version), "Quilt", task, cancellationToken);
                }
                else if (loader is "forge" or "neoforge")
                {
                    var installerResult = await InstallWithInstallerAsync(
                        vanilla, vanillaJson, loader, loaderBuild!, gameDir, task, cancellationToken);
                    loaderJson = installerResult.Json;
                    stagedVanilla = installerResult.StagedVanilla;
                    installerVersionDir = installerResult.InstallerVersionDir;
                }
            }

            var defaultId = GameInstanceLayout.MakeId(vanilla.Id, withLoader ? loader : "vanilla", loaderBuild?.Version);
            var displayName = string.IsNullOrWhiteSpace(instanceName) ? defaultId : instanceName.Trim();
            var instanceId = GameInstanceLayout.AllocateId(gameDir, displayName);
            instanceDir = GameInstanceLayout.GetDirectory(gameDir, instanceId);
            GameInstanceLayout.Ensure(instanceDir);

            Report(task, "正在写入合并后的版本配置...", 18);
            var merged = VersionJsonMerger.Merge(vanillaJson, loaderJson, instanceId);
            await File.WriteAllTextAsync(Path.Combine(instanceDir, instanceId + ".json"), merged, cancellationToken);

            var vanillaJar = Path.Combine(gameDir, "versions", vanilla.Id, vanilla.Id + ".jar");
            var instanceJar = Path.Combine(instanceDir, instanceId + ".jar");
            if (File.Exists(vanillaJar) && (!File.Exists(instanceJar) || new FileInfo(instanceJar).Length == 0))
                File.Copy(vanillaJar, instanceJar, overwrite: true);

            var files = new List<(string Url, string Path)>();
            using (var doc = JsonDocument.Parse(merged))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("downloads", out var downloads)
                    && downloads.TryGetProperty("client", out var client)
                    && client.TryGetProperty("url", out var clientUrl))
                {
                    files.Add((clientUrl.GetString() ?? "", instanceJar));
                }

                CollectLibraries(root, Path.Combine(gameDir, "libraries"), files);

                if (root.TryGetProperty("logging", out var logging)
                    && logging.TryGetProperty("client", out var clientLog)
                    && clientLog.TryGetProperty("file", out var logFile)
                    && logFile.TryGetProperty("url", out var logUrl)
                    && logFile.TryGetProperty("id", out var logId))
                {
                    files.Add((logUrl.GetString() ?? "", Path.Combine(gameDir, "assets", "log_configs", logId.GetString() ?? "client.xml")));
                }

                if (root.TryGetProperty("assetIndex", out var assetIndex)
                    && assetIndex.TryGetProperty("url", out var indexUrl)
                    && assetIndex.TryGetProperty("id", out var indexId))
                {
                    var indexPath = Path.Combine(gameDir, "assets", "indexes", indexId.GetString() + ".json");
                    files.Add((indexUrl.GetString() ?? "", indexPath));
                }
            }

            await DownloadAllAsync(files, task, withLoader ? "游戏与加载器文件" : "原版文件", 22, 70, cancellationToken);

            using (var doc = JsonDocument.Parse(merged))
            {
                Report(task, "正在补齐 Windows 游戏资源...", 72);
                await AssetDownloadService.EnsureAsync(doc.RootElement, gameDir, null, task, cancellationToken);
            }

            if (withLoader && stagedVanilla)
                TryDeleteDirectory(Path.Combine(gameDir, "versions", vanilla.Id));
            if (withLoader
                && !string.IsNullOrWhiteSpace(installerVersionDir)
                && !string.Equals(installerVersionDir, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteDirectory(Path.Combine(gameDir, "versions", installerVersionDir));
            }

            if (withLoader && loader == "fabric" && installFabricApi)
                await InstallFabricApiAsync(vanilla.Id, instanceDir, task, cancellationToken);

            GameOptionsDefaults.ApplyForNewInstance(instanceDir, vanilla.Id);
            GameInstanceLayout.WriteMeta(instanceDir, new GameInstanceLayout.InstanceMeta
            {
                DisplayName = string.Equals(displayName, defaultId, StringComparison.OrdinalIgnoreCase) ? null : displayName,
                MinecraftVersion = vanilla.Id,
                Loader = withLoader ? loader : "vanilla",
                LoaderVersion = loaderBuild?.Version
            });
            Complete(task, instanceDir);
            return instanceId;
            }
            catch (Exception ex) when (DownloadService.IsCanceled(ex, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(instanceDir))
                    TryDeleteDirectory(instanceDir);
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private async Task InstallFabricApiAsync(string minecraftVersion, string instanceDir, DownloadTask task, CancellationToken ct)
        {
            Report(task, "正在安装 Fabric API...", 96);
            var api = new ModrinthApi();
            var version = await api.GetLatestCompatibleAsync("fabric-api", minecraftVersion, "fabric", ct)
                          ?? throw new InvalidOperationException($"找不到适用于 Minecraft {minecraftVersion} 的 Fabric API，请稍后在「下载」页手动安装");

            var file = version.PrimaryFile
                       ?? throw new InvalidOperationException("Fabric API 版本没有可下载文件");
            var modsDir = Path.Combine(instanceDir, "mods");
            Directory.CreateDirectory(modsDir);
            var dest = Path.Combine(modsDir, GameInstanceLayout.Sanitize(file.Filename));
            await DownloadFileAsync(file.Url, dest, ct);
            if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
                throw new InvalidOperationException("Fabric API 下载失败");

            LauncherLogService.Info("Install", $"已安装 Fabric API {version.VersionNumber} → {dest}");
            Report(task, "已安装 Fabric API " + version.VersionNumber, 98, "Fabric API  " + version.VersionNumber);
        }

        private async Task<string> FetchMetaProfileAsync(string profileUrl, string label, DownloadTask task, CancellationToken ct)
        {
            Report(task, $"正在获取 {label} profile...", 10);
            return await Client.GetStringAsync(profileUrl, ct);
        }

        private sealed record InstallerResult(string Json, bool StagedVanilla, string? InstallerVersionDir);

        private async Task<InstallerResult> InstallWithInstallerAsync(
            MinecraftVersion vanilla,
            string vanillaJson,
            string loader,
            LoaderBuild build,
            string gameDir,
            DownloadTask task,
            CancellationToken ct)
        {
            var req = JavaRuntimeService.ReadRequirement(vanillaJson);
            var preferred = LauncherSettings.Instance.JavaPath;
            if (string.IsNullOrWhiteSpace(preferred) || !File.Exists(preferred))
                preferred = JavaRuntimeService.FindInstalled(gameDir, req.Component);
            if (string.IsNullOrWhiteSpace(preferred) || !File.Exists(preferred)
                || !JavaRuntimeService.MeetsRequirement(preferred, req.MajorVersion))
                Report(task, $"正在准备 Java {req.MajorVersion} 虚拟机...", 6);
            var java = await JavaRuntimeService.Instance.EnsureAsync(req, gameDir, null, null, preferred, cancellationToken: ct);
            if (string.IsNullOrWhiteSpace(java) || !File.Exists(java))
                throw new InvalidOperationException("安装 Forge / NeoForge 需要 Java，自动下载失败");

            if (string.IsNullOrWhiteSpace(build.InstallerUrl))
                throw new InvalidOperationException("找不到加载器安装包地址");

            var stagedVanilla = await StageVanillaForInstallerAsync(vanilla, vanillaJson, gameDir, task, ct);
            EnsureLauncherProfiles(gameDir);

            var tempDir = Path.Combine(Path.GetTempPath(), "XO_Launcher", "installers");
            Directory.CreateDirectory(tempDir);
            var installerName = $"{loader}-{build.MavenVersion}-installer.jar";
            var installerPath = Path.Combine(tempDir, GameInstanceLayout.Sanitize(installerName));

            Report(task, $"正在下载 {FormatHelper.FormatLoaderName(loader)} 安装器...", 12);
            await DownloadFileAsync(build.InstallerUrl, installerPath, ct);

            Report(task, $"正在运行 {FormatHelper.FormatLoaderName(loader)} 安装器...", 14);
            var extracted = TryReadInstallerVersionJson(installerPath);
            await RunInstallerAsync(ToJavaCli(java), installerPath, gameDir, ct);

            var guessed = GuessInstalledId(gameDir, vanilla.Id, loader, build);
            if (!string.Equals(guessed, vanilla.Id, StringComparison.OrdinalIgnoreCase))
            {
                var jsonPath = Path.Combine(gameDir, "versions", guessed, guessed + ".json");
                if (File.Exists(jsonPath))
                    return new InstallerResult(await File.ReadAllTextAsync(jsonPath, ct), stagedVanilla, guessed);
            }

            if (!string.IsNullOrWhiteSpace(extracted))
                return new InstallerResult(extracted, stagedVanilla, guessed == vanilla.Id ? null : guessed);

            throw new InvalidOperationException($"{FormatHelper.FormatLoaderName(loader)} 安装器未生成版本配置");
        }

        private static async Task RunInstallerAsync(string java, string installerPath, string gameDir, CancellationToken ct)
        {
            var start = new ProcessStartInfo
            {
                FileName = java,
                WorkingDirectory = gameDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("-Djava.awt.headless=true");
            start.ArgumentList.Add("-jar");
            start.ArgumentList.Add(installerPath);
            start.ArgumentList.Add("--installClient");
            start.ArgumentList.Add(gameDir);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("无法启动加载器安装器");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(4));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception) { }
                if (ct.IsCancellationRequested) throw;
                throw new InvalidOperationException("Forge 安装器运行超时，请检查网络后重试");
            }

            var output = "";
            var error = "";
            try { output = await stdoutTask; } catch (Exception) { }
            try { error = await stderrTask; } catch (Exception) { }

            if (!process.HasExited || process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(error) ? output : error;
                if (string.IsNullOrWhiteSpace(detail) && !process.HasExited)
                    throw new InvalidOperationException("Forge 安装器运行超时，请检查网络后重试");
                if (process.HasExited && process.ExitCode != 0 && !string.IsNullOrWhiteSpace(detail))
                    throw new InvalidOperationException(detail);
            }
        }

        private static string ToJavaCli(string javaPath)
        {
            if (javaPath.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase))
            {
                var cli = Path.Combine(Path.GetDirectoryName(javaPath)!, "java.exe");
                if (File.Exists(cli)) return cli;
            }
            return javaPath;
        }

        private static void EnsureLauncherProfiles(string gameDir)
        {
            Directory.CreateDirectory(gameDir);
            Directory.CreateDirectory(Path.Combine(gameDir, "versions"));
            Directory.CreateDirectory(Path.Combine(gameDir, "libraries"));

            const string profilesJson =
                """
                {
                  "profiles": {
                    "XO_Launcher": {
                      "name": "XO_Launcher",
                      "type": "custom",
                      "lastVersionId": "latest-release"
                    }
                  },
                  "selectedProfile": "XO_Launcher",
                  "clientToken": "00000000-0000-0000-0000-000000000000",
                  "authenticationDatabase": {},
                  "launcherVersion": {
                    "name": "XO_Launcher",
                    "format": 21
                  }
                }
                """;

            foreach (var name in new[] { "launcher_profiles.json", "launcher_profiles_microsoft_store.json" })
            {
                var path = Path.Combine(gameDir, name);
                if (File.Exists(path) && new FileInfo(path).Length > 2) continue;
                File.WriteAllText(path, profilesJson);
            }
        }

        private async Task<bool> StageVanillaForInstallerAsync(
            MinecraftVersion vanilla,
            string vanillaJson,
            string gameDir,
            DownloadTask task,
            CancellationToken ct)
        {
            var versionDir = Path.Combine(gameDir, "versions", vanilla.Id);
            var jsonPath = Path.Combine(versionDir, vanilla.Id + ".json");
            var jarPath = Path.Combine(versionDir, vanilla.Id + ".jar");
            var existed = File.Exists(jsonPath) && File.Exists(jarPath) && new FileInfo(jarPath).Length > 0;
            if (existed) return false;

            Directory.CreateDirectory(versionDir);
            await File.WriteAllTextAsync(jsonPath, vanillaJson, ct);

            using var doc = JsonDocument.Parse(vanillaJson);
            if (doc.RootElement.TryGetProperty("downloads", out var downloads)
                && downloads.TryGetProperty("client", out var client)
                && client.TryGetProperty("url", out var clientUrl)
                && clientUrl.GetString() is { Length: > 0 } url)
            {
                Report(task, "正在下载原版客户端（安装器需要）...", 8);
                await DownloadFileAsync(url, jarPath, ct);
            }

            return true;
        }

        private static string GuessInstalledId(string gameDir, string mcId, string loader, LoaderBuild build)
        {
            var versionsDir = Path.Combine(gameDir, "versions");
            if (!Directory.Exists(versionsDir)) return mcId;

            var candidates = Directory.GetDirectories(versionsDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name)
                               && !string.Equals(name, mcId, StringComparison.OrdinalIgnoreCase)
                               && name.Contains(mcId, StringComparison.OrdinalIgnoreCase)
                               && name.Contains(loader, StringComparison.OrdinalIgnoreCase)
                               && File.Exists(Path.Combine(versionsDir, name!, name + ".json")))
                .OrderByDescending(n => n)
                .ToList();
            if (candidates.Count > 0) return candidates[0]!;

            var forgeName = $"{mcId}-forge-{build.Version}";
            if (Directory.Exists(Path.Combine(versionsDir, forgeName))) return forgeName;
            var neoName = $"{mcId}-neoforge-{build.Version}";
            if (Directory.Exists(Path.Combine(versionsDir, neoName))) return neoName;
            return mcId;
        }

        private static string? TryReadInstallerVersionJson(string installerPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(installerPath);
                var entry = zip.GetEntry("version.json");
                if (entry is null) return null;
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch (Exception)
            {
                // 残留目录不影响启动，忽略清理失败
            }
        }

        private async Task DownloadAllAsync(
            List<(string Url, string Path)> files,
            DownloadTask task,
            string label,
            double progressStart,
            double progressEnd,
            CancellationToken ct)
        {
            await HttpDownloadHelper.DownloadManyAsync(files, ct, HttpDownloadHelper.FileParallelism,
                (done, total, name) =>
                {
                    var progress = progressStart + (progressEnd - progressStart) * done / Math.Max(1, total);
                    Report(task, $"正在下载 {name}", progress, $"{label}  {done} / {total}");
                });
        }

        private static void CollectLibraries(JsonElement root, string librariesDir, List<(string Url, string Path)> files)
        {
            if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
                return;

            foreach (var lib in libraries.EnumerateArray())
            {
                if (!WindowsLibraryFilter.Allows(lib)) continue;

                if (lib.TryGetProperty("downloads", out var downloads))
                {
                    AddArtifact(downloads, "artifact", librariesDir, files);
                    if (lib.TryGetProperty("natives", out var natives)
                        && GetNativeClassifier(natives) is { } classifier
                        && downloads.TryGetProperty("classifiers", out var classifiers)
                        && classifiers.TryGetProperty(classifier, out var nativeArtifact))
                    {
                        AddDirectArtifact(nativeArtifact, librariesDir, files);
                    }
                    continue;
                }

                if (lib.TryGetProperty("name", out var nameEl) && nameEl.GetString() is { } name)
                {
                    var relative = MavenPath(name);
                    var baseUrl = lib.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : "https://libraries.minecraft.net/";
                    if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "https://libraries.minecraft.net/";
                    if (!baseUrl.EndsWith('/')) baseUrl += "/";
                    files.Add((baseUrl + relative, Path.Combine(librariesDir, relative.Replace('/', Path.DirectorySeparatorChar))));
                }
            }
        }

        private static void AddArtifact(JsonElement downloads, string key, string librariesDir, List<(string Url, string Path)> files)
        {
            if (!downloads.TryGetProperty(key, out var artifact)) return;
            AddDirectArtifact(artifact, librariesDir, files);
        }

        private static void AddDirectArtifact(JsonElement artifact, string librariesDir, List<(string Url, string Path)> files)
        {
            var relative = artifact.TryGetProperty("path", out var p) ? p.GetString() : null;
            var url = artifact.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (string.IsNullOrEmpty(relative) || string.IsNullOrEmpty(url)) return;
            files.Add((url, Path.Combine(librariesDir, relative.Replace('/', Path.DirectorySeparatorChar))));
        }

        private static string MavenPath(string name)
        {
            var parts = name.Split(':');
            if (parts.Length < 3) return name.Replace('.', '/') + ".jar";
            var group = parts[0].Replace('.', '/');
            var artifact = parts[1];
            var version = parts[2];
            var classifier = parts.Length > 3 ? "-" + parts[3] : "";
            return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
        }

        private static bool RulesAllow(JsonElement element)
        {
            if (!element.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
                return true;
            var allow = false;
            var saw = false;
            foreach (var rule in rules.EnumerateArray())
            {
                saw = true;
                var action = rule.TryGetProperty("action", out var a) ? a.GetString() : "allow";
                if (!OsMatches(rule)) continue;
                allow = action == "allow";
            }
            return !saw || allow;
        }

        private static bool OsMatches(JsonElement rule)
        {
            if (rule.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Object)
            {
                foreach (var feature in features.EnumerateObject())
                {
                    if (feature.Value.ValueKind == JsonValueKind.True) return false;
                }
            }
            if (!rule.TryGetProperty("os", out var os) || os.ValueKind != JsonValueKind.Object)
                return true;
            if (os.TryGetProperty("name", out var nameProp) && nameProp.GetString() is { } name
                && !string.Equals(name, "windows", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        private static string? GetNativeClassifier(JsonElement natives)
            => natives.TryGetProperty("windows", out var value) ? value.GetString() : null;

        private static Task DownloadFileAsync(string url, string destination, CancellationToken ct)
            => HttpDownloadHelper.DownloadFileAsync(url, destination, ct);

        private static void Report(DownloadTask task, string detail, double progress, string? hint = null)
        {
            DownloadService.Instance.UpdateTask(task, t =>
            {
                t.Status = "下载中";
                t.Detail = detail;
                t.Progress = Math.Clamp(progress, 0, 99);
                if (hint is not null) t.ProgressHint = hint;
            });
        }

        private static void Complete(DownloadTask task, string path)
        {
            DownloadService.Instance.UpdateTask(task, t =>
            {
                t.Progress = 100;
                t.Status = "已完成";
                t.Detail = path;
                t.ProgressHint = "安装完成";
            });
        }
    }
}
