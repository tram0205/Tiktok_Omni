using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Ngữ cảnh mascot + thư viện B-roll đưa vào prompt Gemini khi sinh kịch bản Triết lý.</summary>
    public static class PhilosophyGeminiBackgroundContext
    {
        public sealed class MascotContext
        {
            public string ProfileName { get; set; } = string.Empty;

            public string MascotImagePath { get; set; } = string.Empty;

            public string TextSummary { get; set; } = string.Empty;

            public bool HasMascotImage =>
                !string.IsNullOrWhiteSpace(MascotImagePath) && File.Exists(MascotImagePath);
        }

        public static MascotContext BuildMascotContext(string profileName, AppSettings settings)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var brand = PhilosophyProfileAssets.ResolveProfile(settings, profile);
            var lines = new List<string> { "Profile kênh: «" + profile + "»." };

            var mascotStyle = (brand?.MascotStyle ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(mascotStyle))
            {
                lines.Add("Persona / phong cách nhân vật: " + mascotStyle + ".");
            }

            var videoStyle = (brand?.VideoStyle ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(videoStyle))
            {
                lines.Add("Phong cách video thương hiệu: " + videoStyle + ".");
            }

            var mascotPath = ResolvePrimaryMascotImagePath(profile);
            if (!string.IsNullOrEmpty(mascotPath))
            {
                lines.Add("Ảnh mascot tham chiếu: «" + Path.GetFileName(mascotPath) + "» (đính kèm khi gọi Gemini vision).");
                lines.Add("motion_prompt BẮT BUỘC mô tả CÙNG nhân vật trong ảnh — giữ identity khuôn mặt, tóc, trang phục, phong cách art.");
            }
            else
            {
                lines.Add("Chưa có ảnh mascot — motion_prompt vẫn mô tả nhân vật phù hợp persona/profile ở trên.");
            }

            return new MascotContext
            {
                ProfileName = profile,
                MascotImagePath = mascotPath ?? string.Empty,
                TextSummary = string.Join("\r\n", lines)
            };
        }

        public static string BuildBrollCatalogPromptSection(string profileName, int maxEntries = 80)
        {
            var entries = PhilosophyBRollSelection.EnumerateVideoCatalog(profileName);
            if (entries.Count == 0)
            {
                return "THƯ VIỆN B-ROLL: (trống) — đặt \"broll_video\": \"@random\" cho mọi câu.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("THƯ VIỆN B-ROLL — chỉ chọn tên file có trong danh sách (dựa vào TÊN file + mood/nội dung câu):");
            foreach (var entry in entries.Take(maxEntries))
            {
                sb.AppendLine("  • " + entry.FileName + "  [" + entry.Category + "]");
            }

            if (entries.Count > maxEntries)
            {
                sb.AppendLine("  … thêm " + (entries.Count - maxEntries) +
                              " file — ưu tiên tên file có từ khóa khớp mood (rain, forest, calm, city, …).");
            }

            sb.AppendLine("Trường \"broll_video\": tên file .mp4/.mov CHÍNH XÁC từ danh sách, hoặc \"@random\" nếu không có tên phù hợp.");
            return sb.ToString().TrimEnd();
        }

        public static string BuildMusicCatalogPromptSection(AppSettings settings, int maxEntries = 60)
        {
            OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);
            var names = OmniAudioLibrary.ListMusicFileNames(settings);
            var libraryPath = OmniAudioLibrary.GetSharedMusicDirectory(settings);
            if (names.Count == 0)
            {
                return "THƯ VIỆN NHẠC NỀN DÙNG CHUNG (" + libraryPath + "): (trống) — đặt \"music_file\": \"\" (không nhạc nền).";
            }

            var sb = new StringBuilder();
            sb.AppendLine("THƯ VIỆN NHẠC NỀN DÙNG CHUNG — " + libraryPath);
            sb.AppendLine("Chọn tên file khớp mood/nội dung câu (dựa vào TÊN file):");
            foreach (var name in names.Take(maxEntries))
            {
                sb.AppendLine("  • " + name);
            }

            if (names.Count > maxEntries)
            {
                sb.AppendLine("  … thêm " + (names.Count - maxEntries) +
                              " file — ưu tiên tên có từ khóa calm, sad, hope, piano, ambient, …");
            }

            sb.AppendLine("Trường \"music_file\": tên file .mp3/.wav/.m4a CHÍNH XÁC từ danh sách, hoặc \"\" nếu không cần nhạc nền.");
            return sb.ToString().TrimEnd();
        }

        public static string BuildSharedSfxCatalogPromptSection(AppSettings settings, int maxEntries = 60)
        {
            OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);
            var names = OmniAudioLibrary.ListSfxFileNames(settings);
            var libraryPath = OmniAudioLibrary.GetSharedSfxDirectory(settings);
            if (names.Count == 0)
            {
                return "THƯ VIỆN HIỆU ỨNG ÂM THANH DÙNG CHUNG (" + libraryPath + "): (trống) — đặt \"ambient_sfx\": \"none\".";
            }

            var sb = new StringBuilder();
            sb.AppendLine("THƯ VIỆN HIỆU ỨNG ÂM THANH / TIẾNG ĐỆM DÙNG CHUNG — " + libraryPath);
            sb.AppendLine("Chọn tên file khớp mood/nội dung câu (mưa, gió, rừng, biển, thành phố, … — dựa vào TÊN file):");
            foreach (var name in names.Take(maxEntries))
            {
                sb.AppendLine("  • " + name);
            }

            if (names.Count > maxEntries)
            {
                sb.AppendLine("  … thêm " + (names.Count - maxEntries) +
                              " file — ưu tiên tên có từ khóa rain, wind, forest, ocean, city, night, …");
            }

            sb.AppendLine("Trường \"ambient_sfx\": tên file .mp3/.wav/.m4a/.ogg CHÍNH XÁC từ danh sách, hoặc \"none\" nếu không cần tiếng đệm.");
            sb.AppendLine("Chỉ dùng tiếng đệm khi thật sự tăng cảm xúc — không bắt buộc mọi câu phải có.");
            return sb.ToString().TrimEnd();
        }

        public static string BuildMotionPromptRules(MascotContext mascot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("motion_prompt — mô tả cảnh quay dọc 9:16 cho Veo AI (tiếng Anh, không chữ trên màn hình):");
            sb.AppendLine(mascot?.TextSummary ?? string.Empty);
            sb.AppendLine("Cảnh phải khớp mood + nội dung quote; nhân vật mascot (nếu có) là trung tâm hoặc xuất hiện tự nhiên trong bối cảnh.");
            return sb.ToString().TrimEnd();
        }

        private static string ResolvePrimaryMascotImagePath(string profileName)
        {
            if (AvatarIdentityPackStore.TryGetMascotImagePath(profileName, out var vaultPath, out _))
            {
                return vaultPath;
            }

            foreach (var dir in new[]
                     {
                         PhilosophyProfileAssets.GetMascotImageLibraryDirectory(profileName),
                         Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(profileName), "images")
                     })
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                var image = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(PhilosophyBRollSelection.IsImageFile);
                if (!string.IsNullOrEmpty(image))
                {
                    return image;
                }
            }

            var identityFiles = AvatarIdentityPackStore.SyncIdentityImagesFromVault(profileName);
            return identityFiles.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f));
        }
    }
}
