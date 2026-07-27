using System;
using System.Collections.Generic;
using System.IO;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>SFX Showcase — id Gemini map tới kho dùng chung <see cref="OmniAudioLibrary"/>.</summary>
    public static class ShowcaseSfxCatalog
    {
        public const string NoneId = OmniAudioLibrary.NoneId;

        public const string PlacementSceneStart = "scene_start";
        public const string PlacementSceneMiddle = "scene_middle";
        public const string PlacementSceneEnd = "scene_end";

        public const int DefaultVolumePercent = 42;

        public static string GetLibraryDirectory(AppSettings settings) =>
            OmniAudioLibrary.GetPrimarySfxDirectory(settings);

        public static void EnsureLibraryDirectoryExists(AppSettings settings) =>
            OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);

        public static IReadOnlyList<string> GetSearchDirectories(AppSettings settings) =>
            OmniAudioLibrary.GetSfxSearchDirectories(settings);

        public static List<string> ListFileNames(AppSettings settings) =>
            OmniAudioLibrary.ListSfxFileNames(settings);

        public static string ResolveFilePath(AppSettings settings, string fileNameOrId) =>
            OmniAudioLibrary.ResolveSfxFilePath(fileNameOrId, settings);

        public static string ResolveIdToFileName(AppSettings settings, string sfxId)
        {
            if (IsNoneId(sfxId))
            {
                return string.Empty;
            }

            var path = ResolveFilePath(settings, sfxId);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path);
        }

        public static bool IsNoneId(string value) => OmniAudioLibrary.IsNoneId(value);

        public static string NormalizeId(string raw) => OmniAudioLibrary.NormalizeId(raw);

        public static string NormalizePlacement(string raw)
        {
            var p = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (p == "end" || p == "scene_end" || p == "cuoi" || p.StartsWith("cuối", StringComparison.Ordinal))
            {
                return PlacementSceneEnd;
            }

            if (p == "middle" || p == "scene_middle" || p == "giua" || p.StartsWith("giữa", StringComparison.Ordinal))
            {
                return PlacementSceneMiddle;
            }

            return PlacementSceneStart;
        }

        public static string BuildGeminiIdList(AppSettings settings) =>
            BuildGeminiSfxIdList(settings);

        public static string BuildGeminiSfxIdList(AppSettings settings)
        {
            var names = ListFileNames(settings);
            if (names.Count == 0)
            {
                return "none (chưa có file — đặt .mp3/.wav vào Assets\\Audio\\Sfx)";
            }

            var lines = new List<string> { "none" };
            foreach (var name in names)
            {
                var id = NormalizeId(Path.GetFileNameWithoutExtension(name));
                lines.Add(id + " → " + name);
            }

            return string.Join("; ", lines);
        }

        public static int ClampVolumePercent(int value)
        {
            if (value < 0)
            {
                return DefaultVolumePercent;
            }

            return Math.Min(100, value);
        }
    }
}
