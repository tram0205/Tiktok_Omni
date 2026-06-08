using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public static class AvatarIdentityPackStore
    {
        private const string PackFileName = "identity_pack.json";

        public static string GetVaultRoot()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "AvatarVault");
        }

        public static string GetProfileDirectory(string profileName)
        {
            var safe = ProfileScopedPaths.ResolveProfileName(profileName);
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }

            var dir = Path.Combine(GetVaultRoot(), safe);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetPackFilePath(string profileName)
        {
            return Path.Combine(GetProfileDirectory(profileName), PackFileName);
        }

        public static AvatarIdentityPackConfig LoadOrCreate(string profileName)
        {
            var path = GetPackFilePath(profileName);
            if (!File.Exists(path))
            {
                return new AvatarIdentityPackConfig();
            }

            try
            {
                var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                return JsonConvert.DeserializeObject<AvatarIdentityPackConfig>(json) ?? new AvatarIdentityPackConfig();
            }
            catch
            {
                return new AvatarIdentityPackConfig();
            }
        }

        public static void Save(string profileName, AvatarIdentityPackConfig config)
        {
            if (config == null)
            {
                return;
            }

            var path = GetPackFilePath(profileName);
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json, TextFileEncoding.Utf8NoBom);
        }

        /// <summary>Quét thư mục vault và đồng bộ danh sách ảnh identity_*.jpg|png…</summary>
        public static List<string> SyncIdentityImagesFromVault(string profileName)
        {
            var dir = GetProfileDirectory(profileName);
            return Directory.GetFiles(dir)
                .Where(x =>
                {
                    var name = Path.GetFileName(x) ?? string.Empty;
                    if (name.Equals(PackFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var ext = Path.GetExtension(x).ToLowerInvariant();
                    return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".gif";
                })
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool HasValidMouthAssets(AvatarIdentityPackConfig pack)
        {
            if (pack == null)
            {
                return false;
            }

            if (File.Exists(pack.MouthOpenPath))
            {
                return true;
            }

            return File.Exists(pack.MouthClosedPath)
                   && (File.Exists(pack.MouthOpenPath) || File.Exists(pack.MouthOpenSmallPath));
        }

        /// <summary>Đồng bộ mouth_open.png từ AvatarVault và tạo closed ảo nếu thiếu.</summary>
        public static void EnsureVaultMouthAssets(string profileName, AvatarIdentityPackConfig pack)
        {
            if (pack == null)
            {
                return;
            }

            var dir = GetProfileDirectory(profileName);
            var openVault = Path.Combine(dir, "mouth_open.png");
            if (File.Exists(openVault))
            {
                pack.MouthOpenPath = openVault;
            }

            if (!File.Exists(pack.MouthOpenPath))
            {
                return;
            }

            if (!File.Exists(pack.MouthClosedPath))
            {
                pack.MouthClosedPath = pack.MouthOpenPath;
            }

            if (!File.Exists(pack.MouthOpenSmallPath))
            {
                pack.MouthOpenSmallPath = pack.MouthOpenPath;
            }
        }
    }
}
