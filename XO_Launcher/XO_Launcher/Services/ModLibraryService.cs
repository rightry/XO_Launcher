using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XO_Launcher.Models;

namespace XO_Launcher.Services
{
    /// <summary>
    /// 资源下载库。模组、光影与资源包先保存至此，再安装到具体游戏版本。
    /// 根目录由用户在设置中指定，默认 D:\XO_Launcher\Download。
    /// </summary>
    public static class ModLibraryService
    {
        public const string DefaultDirectory = @"D:\XO_Launcher\Download";

        public static string RootDirectory
        {
            get
            {
                var dir = ResolveDirectory(LauncherSettings.Instance.DownloadDirectory);
                EnsureLayout(dir);
                return dir;
            }
        }

        public static string GetDirectory(string? projectType = ContentKind.Mod)
        {
            var dir = Path.Combine(RootDirectory, ContentKind.LibraryFolder(projectType));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string ResolveDirectory(string? configured)
        {
            var preferred = string.IsNullOrWhiteSpace(configured) ? DefaultDirectory : configured.Trim();
            try
            {
                Directory.CreateDirectory(preferred);
                return preferred;
            }
            catch (Exception)
            {
                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XO_Launcher", "Download");
                Directory.CreateDirectory(fallback);
                return fallback;
            }
        }

        public static void EnsureLayout(string? root = null)
        {
            var dir = string.IsNullOrWhiteSpace(root) ? RootDirectory : root;
            Directory.CreateDirectory(dir);
            foreach (var type in new[] { ContentKind.Mod, ContentKind.Shader, ContentKind.ResourcePack, ContentKind.Modpack })
                Directory.CreateDirectory(Path.Combine(dir, ContentKind.LibraryFolder(type)));
        }

        public static IReadOnlyList<InstanceModItem> Scan(string? projectType = ContentKind.Mod)
        {
            var dir = GetDirectory(projectType);
            var items = new List<InstanceModItem>();
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;
                if (!IsContentFile(name, projectType)) continue;
                var info = new FileInfo(file);
                if (info.Length <= 0) continue;
                items.Add(new InstanceModItem
                {
                    FilePath = file,
                    Name = name,
                    DisplayName = ModNameTranslationService.ForFile(file, name),
                    IsEnabled = true,
                    IsLibraryItem = true,
                    SizeText = FormatSize(info.Length)
                });
            }

            if (projectType is ContentKind.Shader or ContentKind.ResourcePack)
            {
                foreach (var folder in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(folder);
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith(".", StringComparison.Ordinal)) continue;
                    items.Add(new InstanceModItem
                    {
                        FilePath = folder,
                        Name = name,
                        DisplayName = ModNameTranslationService.Translate(name, name),
                        IsEnabled = true,
                        IsDirectory = true,
                        IsLibraryItem = true,
                        SizeText = "文件夹"
                    });
                }
            }

            return items
                .OrderByDescending(m => File.GetLastWriteTime(m.FilePath))
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static (int Installed, int Skipped) InstallToInstance(
            string instanceDir,
            string? projectType,
            IReadOnlyList<InstanceModItem> selected)
        {
            var destDir = Path.Combine(instanceDir, ContentKind.LibraryFolder(projectType));
            Directory.CreateDirectory(destDir);
            var installed = 0;
            var skipped = 0;
            foreach (var item in selected)
            {
                if (string.IsNullOrWhiteSpace(item.FilePath)) continue;
                var exists = item.IsDirectory ? Directory.Exists(item.FilePath) : File.Exists(item.FilePath);
                if (!exists) continue;
                var dest = Path.Combine(destDir, item.Name);
                if (ExistsAtDestination(dest))
                {
                    skipped++;
                    continue;
                }
                CopyEntry(item, dest);
                installed++;
            }
            return (installed, skipped);
        }

        private static void CopyEntry(InstanceModItem item, string dest)
        {
            if (item.IsDirectory)
            {
                CopyDirectory(item.FilePath, dest);
                return;
            }
            File.Copy(item.FilePath, dest, overwrite: false);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: false);
            foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        private static bool ExistsAtDestination(string dest)
            => File.Exists(dest) || File.Exists(dest + ".disabled")
               || Directory.Exists(dest) || Directory.Exists(dest + ".disabled");

        private static bool IsContentFile(string name, string? projectType)
        {
            if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase))
                return false;
            if (name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".litemod", StringComparison.OrdinalIgnoreCase))
                return true;
            return projectType is ContentKind.Shader or ContentKind.ResourcePack;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
            return $"{bytes / (1024.0 * 1024.0):0.00} MB";
        }
    }
}
