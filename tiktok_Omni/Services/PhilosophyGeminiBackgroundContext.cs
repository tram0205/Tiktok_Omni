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

            /// <summary>Gợi ý ngoại hình nhân vật (tiếng Anh) cho motion_prompt / Veo — từ MascotBrain + brand.</summary>
            public string VisualStyleHint { get; set; } = string.Empty;

            public bool HasMascotImage =>
                !string.IsNullOrWhiteSpace(MascotImagePath) && File.Exists(MascotImagePath);
        }

        public static MascotContext BuildMascotContext(string profileName, AppSettings settings)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var brand = PhilosophyProfileAssets.ResolveProfile(settings, profile);
            var brain = MascotBrainStore.LoadOrCreate(
                profile,
                brand?.MascotStyle ?? brand?.MascotPersonality ?? string.Empty);
            var lines = new List<string> { "Profile kênh: «" + profile + "»." };

            var mascotStyle = (brand?.MascotStyle ?? brand?.MascotPersonality ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(mascotStyle))
            {
                lines.Add("Persona / phong cách nhân vật: " + mascotStyle + ".");
            }

            var videoStyle = (brand?.VideoStyle ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(videoStyle))
            {
                lines.Add("Phong cách video thương hiệu: " + videoStyle + ".");
            }

            var brainVisual = (brain?.VisualStyle ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(brainVisual))
            {
                lines.Add("MascotBrain VisualStyle: " + brainVisual + ".");
            }

            var brainPersonality = (brain?.Personality ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(brainPersonality) && !brainPersonality.Equals(brainVisual, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add("MascotBrain Personality: " + brainPersonality + ".");
            }

            var mascotPath = ResolvePrimaryMascotImagePath(profile);
            if (!string.IsNullOrEmpty(mascotPath))
            {
                lines.Add("Ảnh mascot tham chiếu profile «" + profile + "»: «" + Path.GetFileName(mascotPath) +
                          "» (đính kèm khi gọi Gemini vision — ĐỌC ẢNH trước khi viết motion_prompt).");
                lines.Add("motion_prompt BẮT BUỘC mô tả ĐÚNG nhân vật trong ảnh đính kèm của profile này — " +
                          "không dùng nhân vật generic, không đổi khuôn mặt/tóc/trang phục/phong cách art.");
            }
            else
            {
                lines.Add("Chưa có ảnh mascot profile «" + profile + "» — motion_prompt mô tả nhân vật khớp persona/MascotBrain ở trên.");
            }

            return new MascotContext
            {
                ProfileName = profile,
                MascotImagePath = mascotPath ?? string.Empty,
                TextSummary = string.Join("\r\n", lines),
                VisualStyleHint = BuildVisualStyleHint(mascotStyle, videoStyle, brainVisual, brainPersonality)
            };
        }

        private static string BuildVisualStyleHint(
            string mascotStyle,
            string videoStyle,
            string brainVisual,
            string brainPersonality)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(brainVisual))
            {
                parts.Add(brainVisual.Trim());
            }

            if (!string.IsNullOrWhiteSpace(mascotStyle)
                && !parts.Any(p => p.IndexOf(mascotStyle, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                parts.Add(mascotStyle.Trim());
            }

            if (!string.IsNullOrWhiteSpace(brainPersonality)
                && !parts.Any(p => p.IndexOf(brainPersonality, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                parts.Add(brainPersonality.Trim());
            }

            if (!string.IsNullOrWhiteSpace(videoStyle))
            {
                parts.Add("video style: " + videoStyle.Trim());
            }

            return string.Join("; ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
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

        public static string BuildZoomImageCatalogPromptSection(string profileName, int maxEntries = 80)
        {
            var profile = ProfileScopedPaths.ResolveProfileName(profileName);
            var libraryPath = PhilosophyProfileAssets.GetZoomImageLibraryDirectory(profile);
            var entries = PhilosophyBRollSelection.EnumerateZoomImageCatalog(profile);
            if (entries.Count == 0)
            {
                return "THƯ VIỆN ẢNH ZOOM (" + libraryPath + "): (trống) — đặt \"zoom_images\": [] và dùng broll_video thay thế.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("THƯ VIỆN ẢNH ZOOM (Ken Burns) — " + libraryPath);
            sb.AppendLine("Chọn 1–4 tên file ảnh khớp mood/nội dung quote (dựa vào TÊN file):");
            foreach (var entry in entries.Take(maxEntries))
            {
                sb.AppendLine("  • " + entry.FileName);
            }

            if (entries.Count > maxEntries)
            {
                sb.AppendLine("  … thêm " + (entries.Count - maxEntries) +
                              " file — ưu tiên tên có từ khóa rain, forest, calm, sunset, flower, …");
            }

            sb.AppendLine("Trường \"zoom_images\": JSON array tên file .jpg/.png/.webp CHÍNH XÁC từ danh sách (1–4 ảnh, theo thứ tự ghép video).");
            sb.AppendLine("Khi có zoom_images khớp quote → ưu tiên nền zoom ảnh; có thể để broll_video là \"@random\" hoặc bỏ trống.");
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

        public static string BuildEdgeStylePromptSection()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PHONG CÁCH EDGE TTS (edge_style) — giọng đọc Microsoft Edge, chọn ĐÚNG một key cho mỗi câu:");
            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                sb.AppendLine("  • " + key + " — " + HookStyleCatalog.GetDisplayName(key));
            }

            sb.AppendLine("Mặc định video triết lý: ke_chuyen. Khớp mood: melancholic→noi_dau, hopeful→huong_dan, intense→boc_phot, calm/reflective→ke_chuyen.");
            sb.AppendLine("Trường \"edge_style\": một trong các key trên (ví dụ \"ke_chuyen\").");
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
            sb.AppendLine(BuildMascotVeoCharacterBlock(mascot));
            sb.AppendLine("Cảnh phải khớp mood + nội dung quote; nhân vật mascot profile là trung tâm hoặc xuất hiện tự nhiên trong bối cảnh.");
            sb.AppendLine("Mỗi profile có mascot riêng — không copy mô tả nhân vật từ profile khác.");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Khối hướng dẫn Veo: nhân vật phải khớp ảnh mascot profile (đính kèm hoặc VisualStyleHint).</summary>
        public static string BuildMascotVeoCharacterBlock(MascotContext mascot)
        {
            if (mascot == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            if (mascot.HasMascotImage)
            {
                sb.AppendLine("ẢNH MASCOT ĐÍNH KÈM = nhân vật chuẩn của profile «" + mascot.ProfileName + "».");
                sb.AppendLine("Trước khi viết motion_prompt: quan sát ảnh — art style, giới tính/vai trò, kiểu tóc, trang phục, màu sắc, phụ kiện.");
                sb.AppendLine("Mỗi motion_prompt BẮT BUỘC:");
                sb.AppendLine("  1) Mở đầu bằng 1 câu mô tả ngoại hình nhân vật Y HỆT ảnh tham chiếu (ví dụ: art style, tóc, áo, màu).");
                sb.AppendLine("  2) Tiếp theo hành động/cảnh khớp mood + quote.");
                sb.AppendLine("  3) Kết bằng \"same face and outfit as reference, 9:16 vertical aspect ratio\".");
                sb.AppendLine("KHÔNG dùng cụm \"generic mascot\" / \"a character\" — phải mô tả cụ thể như trong ảnh profile.");
            }
            else if (!string.IsNullOrWhiteSpace(mascot.VisualStyleHint))
            {
                sb.AppendLine("Chưa có ảnh mascot — dùng VisualStyleHint profile: " + mascot.VisualStyleHint.Trim() + ".");
                sb.AppendLine("motion_prompt mô tả nhân vật nhất quán theo hint trên xuyên suốt mọi câu.");
            }

            if (!string.IsNullOrWhiteSpace(mascot.VisualStyleHint) && mascot.HasMascotImage)
            {
                sb.AppendLine("Bổ sung DNA thương hiệu (không mâu thuẫn ảnh): " + mascot.VisualStyleHint.Trim() + ".");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Tiền tố tiếng Anh cho prompt Veo fallback / regen khi chưa có motion_prompt từ Gemini.</summary>
        public static string BuildMascotVeoPromptPrefix(MascotContext mascot)
        {
            if (mascot == null)
            {
                return "Vertical 9:16 cinematic philosophy video. ";
            }

            if (mascot.HasMascotImage)
            {
                var hint = string.IsNullOrWhiteSpace(mascot.VisualStyleHint)
                    ? string.Empty
                    : " Brand visual DNA: " + mascot.VisualStyleHint.Trim() + ".";
                return "Vertical 9:16 cinematic philosophy video. Feature the profile «" + mascot.ProfileName +
                       "» mascot character exactly as in the reference image — same art style, face identity, hairstyle, outfit, and colors." +
                       hint + " ";
            }

            if (!string.IsNullOrWhiteSpace(mascot.VisualStyleHint))
            {
                return "Vertical 9:16 cinematic philosophy video. Feature the profile «" + mascot.ProfileName +
                       "» mascot character: " + mascot.VisualStyleHint.Trim() + ". ";
            }

            return "Vertical 9:16 cinematic philosophy video. Feature the profile «" + mascot.ProfileName +
                   "» mascot character with consistent identity. ";
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
