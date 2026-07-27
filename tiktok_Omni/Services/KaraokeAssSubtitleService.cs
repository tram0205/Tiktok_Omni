using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Tạo file ASS tạm và chuỗi filter FFmpeg <c>subtitles='...'</c>.</summary>
    public static class KaraokeAssSubtitleService
    {
        public const string DefaultAssFileName = "temp_subs.ass";

        public sealed class KaraokeAssBurnInResult
        {
            public string AssFilePath { get; set; }

            /// <summary>Fragment cho -vf, ví dụ <c>subtitles='C\:/path/temp_subs.ass'</c>.</summary>
            public string VideoFilterFragment { get; set; }
        }

        public static async Task<KaraokeAssBurnInResult> TryCreateBurnInAsync(
            string ffmpegExecutablePath,
            string narrationText,
            string audioFilePath,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            string openAiApiKey = null,
            AssSubtitleGeneratorOptions assOptions = null,
            string assFileName = null,
            IReadOnlyList<WordTimestamp> precomputedWordTimestamps = null,
            double? knownAudioDurationSeconds = null,
            ShowcaseNarrationTimingManifest showcaseTiming = null)
        {
            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
            {
                return null;
            }

            try
            {
                List<WordTimestamp> timestamps = null;
                if (precomputedWordTimestamps != null && precomputedWordTimestamps.Count > 0)
                {
                    timestamps = new List<WordTimestamp>(precomputedWordTimestamps);
                    log?.Invoke("[Karaoke ASS] Dùng " + timestamps.Count + " từ Whisper đã bóc sẵn.");
                }

                var apiKey = LooksLikeOpenAiApiKey(openAiApiKey) ? openAiApiKey.Trim() : null;
                if ((timestamps == null || timestamps.Count == 0) && !string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        log?.Invoke("Đang bóc băng Whisper...");
                        var whisper = new WhisperTranscriptionService();
                        timestamps = await whisper.GetWordTimestampsAsync(
                            audioFilePath,
                            apiKey,
                            cancellationToken).ConfigureAwait(false);
                        if (timestamps != null && timestamps.Count > 0)
                        {
                            log?.Invoke("[Karaoke ASS] Whisper: " + timestamps.Count + " từ (word timestamps).");
                        }
                        else
                        {
                            log?.Invoke("[Karaoke ASS] Whisper không trả về từ — dùng ước lượng thời gian.");
                            timestamps = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke("Lỗi gọi API Whisper: " + ex.Message);
                        timestamps = null;
                    }
                }

                if (timestamps == null || timestamps.Count == 0)
                {
                    if (showcaseTiming != null)
                    {
                        timestamps = await ShowcaseKaraokeTimingHelper.EstimateWordTimestampsAsync(
                                showcaseTiming,
                                audioFilePath,
                                ffmpegExecutablePath,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (timestamps != null && timestamps.Count > 0)
                        {
                            log?.Invoke("[Karaoke ASS] Showcase: " + timestamps.Count +
                                        " từ — timing hook/thân khớp audio render.");
                        }
                    }

                    if (timestamps == null || timestamps.Count == 0)
                    {
                        if (string.IsNullOrWhiteSpace(narrationText))
                        {
                            return null;
                        }

                        var words = SubtitleTimingHelper.SplitWords(narrationText);
                        if (words.Count == 0)
                        {
                            return null;
                        }

                        var durationMs = await SubtitleTimingHelper.GetAudioDurationMsAsync(
                                ffmpegExecutablePath,
                                audioFilePath,
                                cancellationToken).ConfigureAwait(false);
                        if (durationMs < 50d)
                        {
                            log?.Invoke("[Karaoke ASS] Bỏ qua phụ đề — không đọc được thời lượng audio.");
                            return null;
                        }

                        timestamps = SubtitleTimingHelper.EstimateWordTimestamps(narrationText, durationMs);
                        if (timestamps.Count == 0)
                        {
                            return null;
                        }

                        log?.Invoke("[Karaoke ASS] Ước lượng " + timestamps.Count + " từ (fallback, không Whisper).");
                    }
                }

                var dir = string.IsNullOrWhiteSpace(workDirectory)
                    ? Path.GetDirectoryName(audioFilePath) ?? "."
                    : workDirectory;
                Directory.CreateDirectory(dir);
                var assPath = Path.Combine(
                    dir,
                    string.IsNullOrWhiteSpace(assFileName) ? DefaultAssFileName : assFileName.Trim());
                AssSubtitleGenerator.WriteAssFile(assPath, timestamps, assOptions);
                var esc = EscapePathForFfmpegSubtitleFilter(assPath);
                return new KaraokeAssBurnInResult
                {
                    AssFilePath = assPath,
                    VideoFilterFragment = "subtitles='" + esc + "'"
                };
            }
            catch (Exception ex)
            {
                log?.Invoke("[Karaoke ASS] Không tạo được phụ đề: " + ex.Message);
                return null;
            }
        }

        /// <summary>Showcase: hook + thân với style/toggle riêng.</summary>
        public static async Task<KaraokeAssBurnInResult> TryCreateShowcaseBurnInAsync(
            string ffmpegExecutablePath,
            string narrationText,
            string audioFilePath,
            string workDirectory,
            Action<string> log,
            CancellationToken cancellationToken,
            string openAiApiKey,
            ShowcaseSubtitleRenderPlan renderPlan,
            ShowcaseNarrationTimingManifest showcaseTiming = null,
            ShowcaseSubtitleDisplayPlan subtitleDisplayPlan = null,
            IList<AiVideoGenInputItem> orderedScenes = null,
            string showcaseCtaText = null,
            ShowcasePerVideoRenderSettings renderSettingsForStyle = null,
            AppSettings appSettings = null,
            string assFileName = null)
        {
            if (renderPlan == null || !renderPlan.HasAnyEnabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
            {
                return null;
            }

            try
            {
                List<WordTimestamp> timestamps = null;
                var apiKey = LooksLikeOpenAiApiKey(openAiApiKey) ? openAiApiKey.Trim() : null;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        log?.Invoke("Đang bóc băng Whisper...");
                        var whisper = new WhisperTranscriptionService();
                        timestamps = await whisper.GetWordTimestampsAsync(
                            audioFilePath,
                            apiKey,
                            cancellationToken).ConfigureAwait(false);
                        if (timestamps != null && timestamps.Count > 0)
                        {
                            log?.Invoke("[Karaoke ASS] Whisper: " + timestamps.Count + " từ.");
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke("Lỗi gọi API Whisper: " + ex.Message);
                    }
                }

                if (timestamps == null || timestamps.Count == 0)
                {
                    if (showcaseTiming != null)
                    {
                        timestamps = await ShowcaseKaraokeTimingHelper.EstimateWordTimestampsAsync(
                                showcaseTiming,
                                audioFilePath,
                                ffmpegExecutablePath,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (timestamps != null && timestamps.Count > 0)
                        {
                            log?.Invoke("[Karaoke ASS] Showcase timing: " + timestamps.Count + " từ.");
                        }
                    }
                }

                if (timestamps == null || timestamps.Count == 0)
                {
                    log?.Invoke("[Karaoke ASS] Không có timestamp — bỏ qua phụ đề Showcase.");
                    return null;
                }

                var dir = string.IsNullOrWhiteSpace(workDirectory)
                    ? Path.GetDirectoryName(audioFilePath) ?? "."
                    : workDirectory;
                Directory.CreateDirectory(dir);
                var assPath = Path.Combine(
                    dir,
                    string.IsNullOrWhiteSpace(assFileName) ? DefaultAssFileName : assFileName.Trim());
                var styleVideo = ShowcaseSubtitleStyleHelper.CreateStyleVideoFromRenderSettings(renderSettingsForStyle);
                var settings = appSettings ?? new AppSettings();
                if (subtitleDisplayPlan != null
                    && orderedScenes != null
                    && orderedScenes.Count > 0
                    && ShowcaseSubtitleDisplayHelper.NeedsPerLineAssProcessing(subtitleDisplayPlan))
                {
                    AssSubtitleGenerator.WriteShowcaseAssFile(
                        assPath,
                        timestamps,
                        showcaseTiming,
                        renderPlan,
                        subtitleDisplayPlan,
                        orderedScenes,
                        showcaseCtaText,
                        styleVideo,
                        settings);
                }
                else
                {
                    timestamps = ShowcaseSubtitleDisplayHelper.FilterForBurnIn(
                        timestamps,
                        showcaseTiming,
                        orderedScenes,
                        showcaseCtaText,
                        subtitleDisplayPlan);
                    if (timestamps.Count == 0)
                    {
                        log?.Invoke("[Karaoke ASS] Không còn chữ hiển thị sau lọc overlay — bỏ phụ đề.");
                        return null;
                    }

                    AssSubtitleGenerator.WriteShowcaseAssFile(assPath, timestamps, showcaseTiming, renderPlan);
                }
                var layers = (renderPlan.HookEnabled ? "hook" : string.Empty) +
                             (renderPlan.HookEnabled && renderPlan.BodyEnabled ? "+" : string.Empty) +
                             (renderPlan.BodyEnabled ? "thân" : string.Empty);
                log?.Invoke("[Showcase ASS] Burn-in phụ đề (" + layers + ").");
                var esc = EscapePathForFfmpegSubtitleFilter(assPath);
                return new KaraokeAssBurnInResult
                {
                    AssFilePath = assPath,
                    VideoFilterFragment = "subtitles='" + esc + "'"
                };
            }
            catch (Exception ex)
            {
                log?.Invoke("[Showcase ASS] Không tạo được phụ đề: " + ex.Message);
                return null;
            }
        }

        public static bool LooksLikeOpenAiApiKey(string apiKey)
        {
            var trimmed = (apiKey ?? string.Empty).Trim();
            return trimmed.StartsWith("sk-", StringComparison.OrdinalIgnoreCase);
        }

        public static void SafeDeleteAssFile(string assFilePath)
        {
            if (string.IsNullOrWhiteSpace(assFilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(assFilePath))
                {
                    File.Delete(assFilePath);
                }
            }
            catch
            {
                // ignored
            }
        }

        public static string MergeVideoFilters(string primaryFilter, string subtitleFilterFragment)
        {
            if (string.IsNullOrWhiteSpace(subtitleFilterFragment))
            {
                return primaryFilter ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(primaryFilter))
            {
                return subtitleFilterFragment;
            }

            return primaryFilter + "," + subtitleFilterFragment;
        }

        public static string EscapePathForFfmpegSubtitleFilter(string filePath)
        {
            var full = Path.GetFullPath(filePath).Replace('\\', '/');
            if (full.Length >= 2 && full[1] == ':')
            {
                full = char.ToUpperInvariant(full[0]) + "\\:" + full.Substring(2);
            }

            return full.Replace("'", "\\'");
        }
    }
}
