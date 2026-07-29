using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Cache narration.mp3 trong phiên Showcase — tái render không gọi ElevenLabs nếu kịch bản/voice không đổi.</summary>
    public static class ShowcaseNarrationCacheHelper
    {
        public const string NarrationFileName = "narration.mp3";
        public const string FingerprintFileName = "narration_script.fingerprint";

        /// <summary>Lấy thư mục phiên (cha của veo_clips) từ đường dẫn clip scene_XX.</summary>
        public static string TryResolveSessionBaseFromClips(IList<AiVideoGenInputItem> orderedScenes)
        {
            var clipPath = orderedScenes?
                .Select(s => (s?.ClipPath ?? string.Empty).Trim())
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                return null;
            }

            var clipsDir = Path.GetDirectoryName(clipPath);
            if (string.IsNullOrWhiteSpace(clipsDir))
            {
                return null;
            }

            if (string.Equals(Path.GetFileName(clipsDir), "veo_clips", StringComparison.OrdinalIgnoreCase))
            {
                return Directory.GetParent(clipsDir)?.FullName;
            }

            return clipsDir;
        }

        public static string GetAudioDirectory(string sessionBaseDir)
        {
            return Path.Combine(sessionBaseDir ?? string.Empty, "audio");
        }

        public static string GetOutputDirectory(string sessionBaseDir)
        {
            return Path.Combine(sessionBaseDir ?? string.Empty, "output");
        }

        public static string BuildNarrationScript(IList<AiVideoGenInputItem> scenes)
        {
            var parts = new List<string>();
            foreach (var scene in scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
            {
                if (scene != null && !scene.ShowcaseSceneSilent && !string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    parts.Add(scene.SceneVoiceover.Trim());
                }
            }

            return string.Join(" ", parts);
        }

        public static string BuildNarrationScript(
            string hookText,
            IList<AiVideoGenInputItem> scenes,
            string ctaText)
        {
            return BuildNarrationScript(scenes);
        }

        /// <summary>Hash voiceover từng cảnh + cấu hình TTS — không tính hook/CTA riêng (chỉ khớp clip phân cảnh).</summary>
        public static string ComputeFingerprint(
            IList<AiVideoGenInputItem> scenes,
            AppSettings settings,
            ShowcaseTtsRenderOptions ttsOptions = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("timeline-hook-body-v21-hook-style-custom-voice");
            ttsOptions = ttsOptions ?? new ShowcaseTtsRenderOptions();
            var hookSeg = ttsOptions.ForSegment(true);
            var bodySeg = ttsOptions.ForSegment(false);
            var hookPreset = hookSeg.Preset;
            var bodyPreset = bodySeg.Preset;
            sb.AppendLine("hookEngine:" + ttsOptions.HookEngine);
            sb.AppendLine("bodyEngine:" + ttsOptions.BodyEngine);
            sb.AppendLine("bodyMode:per-scene-no-gemini");
            sb.AppendLine("hookPreset:" + (ttsOptions.VoicePresetId ?? string.Empty).Trim());
            sb.AppendLine("bodyPreset:" + (ttsOptions.BodyVoicePresetId ?? string.Empty).Trim());
            sb.AppendLine("hookVoiceAge:" + ShowcaseVoicePresetDimensions.NormalizeAgeId(ttsOptions.VoiceAgeId));
            sb.AppendLine("hookVoiceLang:" + ShowcaseVoicePresetDimensions.NormalizeLanguageId(ttsOptions.VoiceLanguageId));
            sb.AppendLine("bodyVoiceAge:" + ShowcaseVoicePresetDimensions.NormalizeAgeId(ttsOptions.BodyVoiceAgeId));
            sb.AppendLine("bodyVoiceLang:" + ShowcaseVoicePresetDimensions.NormalizeLanguageId(ttsOptions.BodyVoiceLanguageId));
            sb.AppendLine("hookStyle:" + ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(ttsOptions.HookStyleKey));
            sb.AppendLine("bodyStyle:" + ShowcaseEdgeProsodyHelper.NormalizeStoredHookStyleKey(ttsOptions.BodyStyleKey));
            ShowcaseEdgeProsodyHelper.ResolveEffectiveOffsets(
                ttsOptions.HookStyleKey,
                ttsOptions.EdgeRateOffsetPercent,
                ttsOptions.EdgePitchOffsetHz,
                out var effHookRateOff,
                out var effHookPitchOff);
            sb.AppendLine("hookEdgeRateOff:" + effHookRateOff);
            sb.AppendLine("hookEdgePitchOff:" + effHookPitchOff);
            ShowcaseEdgeProsodyHelper.ResolveEffectiveOffsets(
                ttsOptions.BodyStyleKey,
                ttsOptions.BodyEdgeRateOffsetPercent,
                ttsOptions.BodyEdgePitchOffsetHz,
                out var effBodyRateOff,
                out var effBodyPitchOff);
            sb.AppendLine("bodyEdgeRateOff:" + effBodyRateOff);
            sb.AppendLine("bodyEdgePitchOff:" + effBodyPitchOff);
            if (!ShowcaseEdgeProsodyHelper.IsCustomStyle(ttsOptions.HookStyleKey))
            {
                sb.AppendLine("hookEdgeProsodyPreset:1");
            }
            else
            {
                sb.AppendLine("hookEdgeProsodyPreset:0");
            }

            if (!ShowcaseEdgeProsodyHelper.IsCustomStyle(ttsOptions.BodyStyleKey))
            {
                sb.AppendLine("bodyEdgeProsodyPreset:1");
            }
            else
            {
                sb.AppendLine("bodyEdgeProsodyPreset:0");
            }

            if (ttsOptions.BodyEngine == TtsEngineKind.EdgeTts || ttsOptions.HookEngine == TtsEngineKind.EdgeTts)
            {
                var edgeHook = ShowcaseEdgeTtsVoiceResolver.Resolve(ttsOptions.ForSegment(true), emphaticHook: true, showcaseExpressiveBody: false);
                var edgeBody = ShowcaseEdgeTtsVoiceResolver.Resolve(ttsOptions.ForSegment(false), emphaticHook: false, showcaseExpressiveBody: true);
                sb.AppendLine("edgeVoice:" + (edgeBody.VoiceShortName ?? string.Empty));
                sb.AppendLine("edgeHookRate:" + (edgeHook.Rate ?? string.Empty));
                sb.AppendLine("edgeHookPitch:" + (edgeHook.Pitch ?? string.Empty));
                sb.AppendLine("edgeHookVol:" + (edgeHook.Volume ?? string.Empty));
                sb.AppendLine("edgeBodyRate:" + (edgeBody.Rate ?? string.Empty));
                sb.AppendLine("edgeBodyPitch:" + (edgeBody.Pitch ?? string.Empty));
                sb.AppendLine("edgeBodyVol:" + (edgeBody.Volume ?? string.Empty));
            }

            sb.AppendLine("hookVoiceLen:" + hookPreset.PiperLengthScale.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("bodyVoiceLen:" + bodyPreset.PiperLengthScale.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("hookElMood:" + (hookPreset?.ElevenLabsVoiceMood ?? string.Empty));
            sb.AppendLine("bodyElMood:" + (bodyPreset?.ElevenLabsVoiceMood ?? string.Empty));
            sb.AppendLine("elHook:" + (hookPreset != null && hookPreset.ElevenLabsEmphaticHook ? "1" : "0"));
            sb.AppendLine("elBody:" + (bodyPreset != null && bodyPreset.ElevenLabsExpressiveBody ? "1" : "0"));
            sb.AppendLine("hookElPersona:" + hookSeg.ElevenPersona);
            sb.AppendLine("bodyElPersona:" + bodySeg.ElevenPersona);
            sb.AppendLine("hookElTone:" + hookSeg.ElevenToneId);
            sb.AppendLine("bodyElTone:" + bodySeg.ElevenToneId);
            sb.AppendLine("hookElVoiceSettings:" + DescribeHookVoiceSettings(hookSeg));
            sb.AppendLine("bodyElVoiceSettings:" + DescribeBodyVoiceSettings(bodySeg));
            sb.AppendLine("hookElVoice:" + (ShowcaseTtsHelper.ResolveElevenLabsVoiceId(hookSeg, settings) ?? string.Empty));
            sb.AppendLine("bodyElVoice:" + (ShowcaseTtsHelper.ResolveElevenLabsVoiceId(bodySeg, settings) ?? string.Empty));
            sb.AppendLine("wps:" + ShowcaseVoiceoverFitHelper.WordsPerSecond.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("hook:" + ElevenLabsTtsHelper.HookStability.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("body:" + ElevenLabsTtsHelper.ShowcaseBodyStability.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("bodyStyle:" + ElevenLabsTtsHelper.ShowcaseBodyStyleExaggeration.ToString(CultureInfo.InvariantCulture));
            foreach (var scene in scenes ?? Enumerable.Empty<AiVideoGenInputItem>())
            {
                sb.AppendLine((scene?.SceneVoiceover ?? string.Empty).Trim());
                sb.AppendLine(scene != null && scene.ShowcaseSceneSilent ? "silent" : "voice");
                sb.AppendLine((scene?.ClipPath ?? string.Empty).Trim());
            }

            sb.AppendLine((settings?.TtsEndpoint ?? string.Empty).Trim());
            sb.AppendLine(ElevenLabsTtsHelper.ResolveModelId(settings));
            sb.AppendLine((settings?.TtsLanguageCode ?? string.Empty).Trim());

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>Hook: "Tone giọng" đã bỏ — trigger là HookStyleKey = ⚙ Tùy chỉnh giọng. «role-default» nếu không.</summary>
        private static string DescribeHookVoiceSettings(ShowcaseTtsRenderOptions segmentTts)
        {
            if (!ShowcaseElevenToneHelper.IsHookCustomVoiceStyle(segmentTts?.HookStyleKey))
            {
                return "role-default";
            }

            return segmentTts.ElevenCustomStabilityPercent + "/" +
                   segmentTts.ElevenCustomSimilarityPercent + "/" +
                   segmentTts.ElevenCustomStylePercent;
        }

        /// <summary>Thân: giữ cơ chế Tone giọng — «role-default» nếu Tone = Tự nhiên (không ghi đè, tránh cache đổi vô cớ).</summary>
        private static string DescribeBodyVoiceSettings(ShowcaseTtsRenderOptions segmentTts)
        {
            if (!ShowcaseElevenToneHelper.HasVoiceSettingsOverride(segmentTts?.ElevenToneId))
            {
                return "role-default";
            }

            ShowcaseElevenToneHelper.ResolveEffectiveVoiceSettings(
                segmentTts.ElevenToneId,
                segmentTts.ElevenCustomStabilityPercent,
                segmentTts.ElevenCustomSimilarityPercent,
                segmentTts.ElevenCustomStylePercent,
                out var stabilityPct,
                out var similarityPct,
                out var stylePct);
            return stabilityPct + "/" + similarityPct + "/" + stylePct;
        }

        /// <summary>Hash kịch bản + cấu hình TTS — đổi hook/voiceover/CTA hoặc endpoint/model thì hash mới.</summary>
        public static string ComputeFingerprint(
            string hookText,
            IList<AiVideoGenInputItem> scenes,
            string ctaText,
            AppSettings settings,
            double hookBrollDurationSeconds = 0d)
        {
            return ComputeFingerprint(scenes, settings);
        }

        public static bool TryReuseCachedNarration(string audioDir, string fingerprint, out string narrationPath)
        {
            narrationPath = Path.Combine(audioDir ?? string.Empty, NarrationFileName);
            if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(audioDir))
            {
                return false;
            }

            var fingerprintPath = Path.Combine(audioDir, FingerprintFileName);
            if (!File.Exists(narrationPath) || !File.Exists(fingerprintPath))
            {
                return false;
            }

            try
            {
                var saved = File.ReadAllText(fingerprintPath, TextFileEncoding.Utf8).Trim();
                if (!string.Equals(saved, fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var info = new FileInfo(narrationPath);
                return info.Exists && info.Length > 512;
            }
            catch
            {
                return false;
            }
        }

        public static void SaveFingerprint(string audioDir, string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(audioDir) || string.IsNullOrWhiteSpace(fingerprint))
            {
                return;
            }

            Directory.CreateDirectory(audioDir);
            File.WriteAllText(
                Path.Combine(audioDir, FingerprintFileName),
                fingerprint.Trim(),
                TextFileEncoding.Utf8NoBom);
        }

        public static bool HasNarrationFile(string sessionBaseDir)
        {
            if (string.IsNullOrWhiteSpace(sessionBaseDir))
            {
                return false;
            }

            var path = Path.Combine(GetAudioDirectory(sessionBaseDir), NarrationFileName);
            try
            {
                var info = new FileInfo(path);
                return info.Exists && info.Length > 512;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Xóa narration cache sau khi đổi thoại — render sẽ gọi TTS lại.</summary>
        public static void ClearCachedNarration(string sessionBaseDir)
        {
            if (string.IsNullOrWhiteSpace(sessionBaseDir))
            {
                return;
            }

            var audioDir = GetAudioDirectory(sessionBaseDir);
            TryDelete(Path.Combine(audioDir, NarrationFileName));
            TryDelete(Path.Combine(audioDir, FingerprintFileName));
            TryDelete(ShowcaseNarrationTimingManifest.GetPath(audioDir));
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try { File.Delete(path); } catch { /* non-critical */ }
        }
    }
}
