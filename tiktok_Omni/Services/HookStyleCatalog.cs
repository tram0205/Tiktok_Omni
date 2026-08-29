using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Catalog video stock hook cho từng phong cách caption (noi_dau / boc_phot / huong_dan / fomo / ke_chuyen).
    /// Cấu trúc thư mục:
    ///   {CatalogRoot}\{styleKey}\{profileName}\*.mp4   ← clip riêng cho từng profile (ưu tiên)
    ///   {CatalogRoot}\{styleKey}\*.mp4                 ← clip dùng chung (fallback)
    /// </summary>
    public static class HookStyleCatalog
    {
        public const string StyleNoidau   = "noi_dau";
        public const string StyleBocphot  = "boc_phot";
        public const string StyleHuongdan = "huong_dan";
        public const string StyleFomo     = "fomo";
        public const string StyleKechuyen = "ke_chuyen";

        public static readonly string[] AllStyleKeys =
        {
            StyleNoidau,
            StyleBocphot,
            StyleHuongdan,
            StyleFomo,
            StyleKechuyen
        };

        public static readonly IReadOnlyDictionary<string, string> StyleDisplayNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { StyleNoidau,   "Nỗi đau" },
                { StyleBocphot,  "Bóc phốt" },
                { StyleHuongdan, "Hướng dẫn" },
                { StyleFomo,     "FOMO" },
                { StyleKechuyen, "Kể chuyện" },
            };

        public static string GetDisplayName(string styleKey)
        {
            var key = (styleKey ?? string.Empty).Trim();
            if (StyleDisplayNames.TryGetValue(key, out var name))
            {
                return name;
            }

            return string.IsNullOrEmpty(key) ? VideoReupStyleVariants.RandomHookStyleLabel : key;
        }

        public static string GetStyleIcon(string styleKey)
        {
            switch ((styleKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case StyleNoidau: return "⚠";
                case StyleBocphot: return "🔥";
                case StyleHuongdan: return "✦";
                case StyleFomo: return "⏱";
                case StyleKechuyen: return "✎";
                default: return "●";
            }
        }

        public static System.Drawing.Color GetStyleAccent(string styleKey)
        {
            switch ((styleKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case StyleNoidau: return System.Drawing.Color.FromArgb(255, 118, 118);
                case StyleBocphot: return System.Drawing.Color.FromArgb(255, 168, 72);
                case StyleHuongdan: return System.Drawing.Color.FromArgb(96, 198, 255);
                case StyleFomo: return System.Drawing.Color.FromArgb(255, 206, 84);
                case StyleKechuyen: return System.Drawing.Color.FromArgb(168, 140, 255);
                default: return System.Drawing.Color.FromArgb(130, 175, 255);
            }
        }

        public static System.Drawing.Color GetStyleRowSurface(string styleKey, bool selected)
        {
            var accent = GetStyleAccent(styleKey);
            const int baseR = 18;
            const int baseG = 20;
            const int baseB = 26;
            var mix = selected ? 0.24f : 0.10f;
            return System.Drawing.Color.FromArgb(
                (int)(baseR + (accent.R - baseR) * mix),
                (int)(baseG + (accent.G - baseG) * mix),
                (int)(baseB + (accent.B - baseB) * mix));
        }

        /// <summary>Chọn ngẫu nhiên một style (ưu tiên style có clip stock).</summary>
        public static string PickRandomStyleKey(AppSettings settings, string profileName = null)
        {
            var withClips = AllStyleKeys
                .Where(k => GetClipsForStyle(k, settings, profileName).Length > 0)
                .ToArray();
            var pool = withClips.Length > 0 ? withClips : AllStyleKeys;
            return pool[new Random().Next(pool.Length)];
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Root / folder helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Thư mục gốc chứa video stock hook. Mặc định: [exe]\VideoReup\HookClips.</summary>
        public static string GetCatalogRoot(AppSettings settings)
        {
            var root = (settings?.HookStockClipsRoot ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory ?? ".",
                    "VideoReup",
                    "HookClips");
            }

            return root;
        }

        /// <summary>Thư mục style chung: {CatalogRoot}\{styleKey}\</summary>
        public static string GetStyleFolder(string styleKey, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(styleKey))
            {
                return null;
            }

            return Path.Combine(GetCatalogRoot(settings), styleKey.Trim().ToLowerInvariant());
        }

        /// <summary>Thư mục clip riêng của profile: {CatalogRoot}\{styleKey}\{profileName}\</summary>
        public static string GetProfileStyleFolder(string styleKey, AppSettings settings, string profileName)
        {
            var styleFolder = GetStyleFolder(styleKey, settings);
            if (styleFolder == null || string.IsNullOrWhiteSpace(profileName))
            {
                return null;
            }

            return Path.Combine(styleFolder, SanitizeProfileFolderName(profileName));
        }

        /// <summary>Tạo sẵn tất cả 5 thư mục style cho một profile (dùng khi bấm nút «Hook Clips»).</summary>
        public static string EnsureProfileFolders(string profileName, AppSettings settings)
        {
            // Lấy thư mục gốc của tất cả các clip hook (ví dụ: ...\VideoReup\HookClips)
            var catalogRoot = GetCatalogRoot(settings);

            foreach (var key in AllStyleKeys)
            {
                // Lấy đường dẫn thư mục cụ thể cho style và profile (ví dụ: ...\HookClips\noi_dau\ProfileA)
                var folder = GetProfileStyleFolder(key, settings, profileName);
                if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // Đảm bảo thư mục chung cho style đó cũng tồn tại (ví dụ: ...\HookClips\noi_dau)
                var sharedStyleFolder = GetStyleFolder(key, settings);
                if (!string.IsNullOrWhiteSpace(sharedStyleFolder) && !Directory.Exists(sharedStyleFolder))
                {
                    Directory.CreateDirectory(sharedStyleFolder);
                }
            }

            // Luôn trả về thư mục gốc của catalog để người dùng có thể điều hướng
            return catalogRoot;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Clip lookup — profile-first, fallback to shared
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Danh sách clip cho style. Ưu tiên thư mục riêng của profile trước,
        /// fallback về thư mục style chung nếu chưa có clip riêng.
        /// </summary>
        public static string[] GetClipsForStyle(string styleKey, AppSettings settings, string profileName = null)
        {
            // 1. Thư mục clip riêng theo profile
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                var profileFolder = GetProfileStyleFolder(styleKey, settings, profileName);
                if (profileFolder != null && Directory.Exists(profileFolder))
                {
                    var profileClips = Directory.GetFiles(profileFolder, "*.mp4", SearchOption.TopDirectoryOnly);
                    if (profileClips.Length > 0)
                    {
                        return profileClips;
                    }
                }
            }

            // 2. Fallback: clip chung trong thư mục style
            var sharedFolder = GetStyleFolder(styleKey, settings);
            if (sharedFolder == null || !Directory.Exists(sharedFolder))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(sharedFolder, "*.mp4", SearchOption.TopDirectoryOnly);
        }

        /// <summary>Chọn ngẫu nhiên một clip. Ưu tiên clip của profile, fallback về clip chung. Tránh lặp <paramref name="excludePath"/>.</summary>
        public static string PickRandomClip(string styleKey, AppSettings settings, string excludePath = null, string profileName = null)
        {
            var clips = GetClipsForStyle(styleKey, settings, profileName);
            if (clips.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(excludePath) && clips.Length > 1)
            {
                var filtered = clips
                    .Where(c => !string.Equals(c, excludePath, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (filtered.Length > 0)
                {
                    clips = filtered;
                }
            }

            return clips[new Random().Next(clips.Length)];
        }

        /// <summary>Chọn bất kỳ clip từ bất kỳ style (dùng khi styleKey không xác định).</summary>
        public static string PickAnyClip(AppSettings settings, string profileName = null)
        {
            foreach (var key in AllStyleKeys)
            {
                var clip = PickRandomClip(key, settings, profileName: profileName);
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        /// <summary>Trả về true nếu catalog có ít nhất một clip (cho profile nếu chỉ định).</summary>
        public static bool CatalogHasAnyClips(AppSettings settings, string profileName = null)
        {
            return PickAnyClip(settings, profileName) != null;
        }

        /// <summary>
        /// Chọn clip phù hợp với style + profile. Fallback thứ tự:
        /// 1. Profile clip của style đã chọn
        /// 2. Shared clip của style đã chọn
        /// 3. Profile clip của style bất kỳ
        /// 4. Shared clip của style bất kỳ
        /// </summary>
        public static bool TryPickClip(string styleKey, AppSettings settings, string excludePath, string profileName, out string clipPath)
        {
            clipPath = null;
            if (!string.IsNullOrWhiteSpace(styleKey))
            {
                // Có style → ưu tiên clip của style đó (profile trước, rồi shared)
                clipPath = PickRandomClip(styleKey, settings, excludePath, profileName);
                if (clipPath != null)
                {
                    return true;
                }
            }

            // Không có style (hoặc không có clip cho style đó) → bất kỳ clip nào
            clipPath = PickAnyClip(settings, profileName);
            return clipPath != null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Status / health check
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Tóm tắt trạng thái catalog cho health check / log.</summary>
        public static string GetCatalogStatusSummary(AppSettings settings, string profileName = null)
        {
            var root = GetCatalogRoot(settings);
            if (!Directory.Exists(root))
            {
                return $"HookClips: thư mục không tồn tại ({root}).";
            }

            var sb = new System.Text.StringBuilder();
            var label = string.IsNullOrWhiteSpace(profileName) ? "HookClips (chung)" : $"HookClips [{profileName}]";
            sb.Append(label + ": ");
            var total = 0;
            foreach (var key in AllStyleKeys)
            {
                var clips = GetClipsForStyle(key, settings, profileName);
                var count = clips.Length;
                total += count;
                var displayName = StyleDisplayNames.TryGetValue(key, out var n) ? n : key;
                sb.Append($"{displayName}={count} ");
            }

            sb.Append($"(tổng {total} clip).");
            return sb.ToString().Trim();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string SanitizeProfileFolderName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var safe = new System.Text.StringBuilder();
            foreach (var c in profileName.Trim())
            {
                safe.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return safe.ToString();
        }
    }
}
