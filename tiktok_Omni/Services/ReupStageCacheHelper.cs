using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>Cache artifact trong thư mục stage Video reup — tránh gọi lại ElevenLabs/Gemini khi render lại.</summary>
    public static class ReupStageCacheHelper
    {
        public const string HookTtsKeyFile = "hook_tts.key";
        public const string HookTtsRawMp3 = "hook_tts_raw.mp3";
        public const string NarrationTtsKeyFile = "narration_tts.key";
        public const string NarrationRawMp3 = "narration_raw.mp3";
        public const string HookSceneImageKeyFile = "hook_scene_image.key";

        public static string BuildElevenLabsTtsCacheKey(
            string synthesisText,
            AppSettings settings,
            bool emphaticHook,
            string hookStyleKey)
        {
            var chain = ReupElevenLabsVoiceHelper.BuildVoiceIdFallbackChain(emphaticHook, settings);
            var payload = new StringBuilder();
            payload.Append("v1|");
            payload.Append(emphaticHook ? "hook|" : "narr|");
            payload.Append((synthesisText ?? string.Empty).Trim()).Append('|');
            payload.Append(string.Join(",", chain)).Append('|');
            payload.Append(ElevenLabsTtsHelper.ResolveModelId(settings)).Append('|');
            payload.Append(ElevenLabsTtsHelper.ResolveLanguageCode(settings)).Append('|');
            payload.Append((hookStyleKey ?? string.Empty).Trim().ToLowerInvariant()).Append('|');
            payload.Append(ReupElevenLabsVoiceHelper.DescribeVoiceSettings(emphaticHook)).Append('|');

            if (VideoReupRemixService.IsElevenLabsConfigured(settings))
            {
                payload.Append((settings?.TtsEndpoint ?? string.Empty).Trim().ToLowerInvariant());
            }
            else
            {
                payload.Append("google|").Append((settings?.AiApiKey ?? string.Empty).Trim());
            }

            return ComputeSha256Hex(payload.ToString());
        }

        public static string BuildHookSceneImageCacheKey(string hookText, string mascotPath)
        {
            var payload =
                (hookText ?? string.Empty).Trim() + "|img|" +
                (mascotPath ?? string.Empty).Trim().ToLowerInvariant();
            return ComputeSha256Hex(payload);
        }

        public static string BuildGeminiHookIntroVideoCacheKey(
            string hookText,
            string mascotPath,
            double hookDurationSec,
            VideoReupRowItem row,
            AppSettings settings)
        {
            var payload =
                BuildHookSceneImageCacheKey(hookText, mascotPath) + "|vid|" +
                hookDurationSec.ToString("0.###", CultureInfo.InvariantCulture) + "|" +
                BuildSubtitleStyleFingerprint(row, settings);
            return ComputeSha256Hex(payload);
        }

        public static string BuildStockClipCacheKey(string hookText, string stockClipPath)
        {
            var payload =
                (hookText ?? string.Empty).Trim() + "|stock|" +
                (stockClipPath ?? string.Empty).Trim().ToLowerInvariant();
            return ComputeSha256Hex(payload);
        }

        public static string BuildStockHookIntroVideoCacheKey(
            string hookText,
            string stockClipPath,
            double hookDurationSec,
            VideoReupRowItem row,
            AppSettings settings)
        {
            var payload =
                BuildStockClipCacheKey(hookText, stockClipPath) + "|vid|" +
                hookDurationSec.ToString("0.###", CultureInfo.InvariantCulture) + "|" +
                BuildSubtitleStyleFingerprint(row, settings);
            return ComputeSha256Hex(payload);
        }

        public static string BuildSubtitleStyleFingerprint(VideoReupRowItem row, AppSettings settings)
        {
            var opt = ReupSubtitleStyleHelper.BuildOptions(row, settings);
            return string.Join("|",
                opt.FontName ?? string.Empty,
                opt.FontSize.ToString(CultureInfo.InvariantCulture),
                opt.Alignment.ToString(CultureInfo.InvariantCulture),
                opt.Animation.ToString(),
                opt.WordsPerLine.ToString(CultureInfo.InvariantCulture),
                opt.RhythmicLineBreaks ? "1" : "0",
                opt.PrimaryColourAss ?? string.Empty,
                opt.SecondaryColourAss ?? string.Empty);
        }

        public static bool TryReuseCachedArtifacts(
            string stageFolder,
            string keyFileName,
            string expectedKey,
            params string[] artifactPaths)
        {
            if (string.IsNullOrWhiteSpace(stageFolder) || string.IsNullOrWhiteSpace(expectedKey))
            {
                return false;
            }

            var keyPath = Path.Combine(stageFolder, keyFileName);
            if (!File.Exists(keyPath))
            {
                return false;
            }

            var stored = File.ReadAllText(keyPath, TextFileEncoding.Utf8NoBom).Trim();
            if (!string.Equals(stored, expectedKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (artifactPaths == null || artifactPaths.Length == 0)
            {
                return true;
            }

            foreach (var path in artifactPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return false;
                }

                try
                {
                    if (new FileInfo(path).Length < 800L)
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        public static void WriteCacheKey(string stageFolder, string keyFileName, string key)
        {
            if (string.IsNullOrWhiteSpace(stageFolder) || string.IsNullOrWhiteSpace(keyFileName))
            {
                return;
            }

            Directory.CreateDirectory(stageFolder);
            File.WriteAllText(Path.Combine(stageFolder, keyFileName), key ?? string.Empty, TextFileEncoding.Utf8NoBom);
        }

        private static string ComputeSha256Hex(string payload)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload ?? string.Empty));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }
    }
}
