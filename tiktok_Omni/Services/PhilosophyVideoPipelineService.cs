using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Mascot;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Pipeline video Triết lý/Quote — Gemini + Veo/TTS + FFmpeg; dùng Cài đặt app (không cần Python/env).</summary>
    public sealed class PhilosophyVideoPipelineService
    {
        private const int TargetWidth = 1080;
        private const int TargetHeight = 1920;
        private const double DefaultBackgroundSeconds = 18d;
        private const double VoiceMixGain = 1.65d;
        private const double AmbientBedVolume = 0.05d;
        private static readonly Random BrollRandom = new Random();

        private readonly VideoService _videoService = new VideoService();
        private readonly ElevenLabsTtsService _elevenLabsTtsService;
        private MascotWorker _mascotWorker;

        public PhilosophyVideoPipelineService()
        {
            _elevenLabsTtsService = new ElevenLabsTtsService(_videoService);
        }

        private MascotWorker MascotWorker => _mascotWorker ?? (_mascotWorker = new MascotWorker(_videoService));

        private const string LegacyOutputFolderName = "Output";

        /// <summary>Thư mục thành phẩm — mỗi profile một thư mục con.</summary>
        public static string GetOutputRootDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "PhilosophyVideo", "ThanhPham");
        }

        public static string GetLegacyOutputRootDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "PhilosophyVideo", LegacyOutputFolderName);
        }

        public static string GetProfileOutputDirectory(string profileName)
        {
            var profileDir = Path.Combine(
                GetOutputRootDirectory(),
                ProfileScopedPaths.ResolveProfileName(profileName));
            Directory.CreateDirectory(profileDir);
            return profileDir;
        }

        /// <summary>Tạo PhilosophyVideo\ThanhPham và thư mục con theo từng profile trong Cài đặt.</summary>
        public static void EnsureFinishedProductLayout(AppSettings settings)
        {
            Directory.CreateDirectory(GetOutputRootDirectory());
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "default" };
            if (settings?.Profiles != null)
            {
                foreach (var profile in settings.Profiles)
                {
                    if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
                    {
                        continue;
                    }

                    names.Add(ProfileScopedPaths.ResolveProfileName(profile.Name));
                }
            }

            foreach (var name in names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.Combine(GetOutputRootDirectory(), name));
            }
        }

        public static IEnumerable<string> EnumerateFinishedProductSearchRoots()
        {
            yield return GetOutputRootDirectory();
            var legacy = GetLegacyOutputRootDirectory();
            if (Directory.Exists(legacy))
            {
                yield return legacy;
            }
        }

        private static bool IsUnderPhilosophyOutputRoot(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            var full = Path.GetFullPath(directoryPath);
            foreach (var root in EnumerateFinishedProductSearchRoots())
            {
                var rootFull = Path.GetFullPath(root);
                if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Xóa thư mục stage output cũ trước khi render lại cùng một dòng.</summary>
        public static bool TryDeletePreviousOutput(string outputPath, Action<string> log)
        {
            var path = (outputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    return false;
                }

                var stageDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(stageDir)
                    && Directory.Exists(stageDir)
                    && IsUnderPhilosophyOutputRoot(stageDir))
                {
                    Directory.Delete(stageDir, recursive: true);
                    log?.Invoke("[Quote] Đã xóa kết quả render trước: " + Path.GetFileName(stageDir));
                    return true;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    log?.Invoke("[Quote] Đã xóa file render trước: " + Path.GetFileName(fullPath));
                    return true;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("[Quote] Không xóa được kết quả cũ: " + ex.Message);
            }

            return false;
        }

        public static string DescribeBlockers(AppSettings settings)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                lines.Add("① AI API Key (Cài đặt) — cần Gemini viết quote / mood.");
            }

            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.TtsApiKey) ||
                string.IsNullOrWhiteSpace(settings.TtsEndpoint))
            {
                lines.Add("② TTS API Key + Endpoint (Cài đặt) — giọng đọc quote.");
            }

            if (!FfmpegToolkitService.TryResolve(settings, out _, out var ffErr))
            {
                lines.Add("③ FFmpeg/ffprobe: " + ffErr.Replace("\r\n", " "));
            }

            var hasVeo = settings != null &&
                         !string.IsNullOrWhiteSpace(settings.VeoApiKey) &&
                         !string.IsNullOrWhiteSpace(settings.VeoEndpoint);
            if (!hasVeo)
            {
                lines.Add("④ Veo (tùy chọn): chưa cấu hình — app dùng nền FFmpeg gradient thay video AI.");
            }

            return lines.Count == 0
                ? "Sẵn sàng — nhập quote hoặc link rồi bấm «Tạo video Quote»."
                : string.Join("\r\n", lines);
        }

        public static bool TryValidatePrerequisites(AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                errorMessage = "Cần AI API Key trong Cài đặt (Gemini).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.TtsApiKey) || string.IsNullOrWhiteSpace(settings.TtsEndpoint))
            {
                errorMessage = "Cần TTS API Key + Endpoint trong Cài đặt.";
                return false;
            }

            if (!VideoReupRemixService.TryValidateFfmpegToolkit(settings, out errorMessage))
            {
                return false;
            }

            return true;
        }

        public async Task<PhilosophyVideoResult> RunScriptAsync(
            PhilosophyScriptItem item,
            PhilosophyRenderOptions renderOptions,
            AppSettings settings,
            AutomationProfile profile,
            Action<string> log,
            Action<string, int> progress,
            CancellationToken cancellationToken)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!TryValidatePrerequisites(settings, out var pre))
            {
                throw new InvalidOperationException(pre);
            }

            var quote = (item.Content ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(quote))
            {
                throw new InvalidOperationException("Script trống.");
            }

            var mood = NormalizePhilosophyMood(item.Mood);
            renderOptions = renderOptions ?? new PhilosophyRenderOptions();

            var brandProfile = profile ?? new AutomationProfile { Name = "default" };
            if (string.IsNullOrWhiteSpace(brandProfile.Name))
            {
                brandProfile.Name = "default";
            }

            var nick = ProfileScopedPaths.ResolveProfileName(
                string.IsNullOrWhiteSpace(renderOptions.ProfileName) ? brandProfile.Name : renderOptions.ProfileName);
            brandProfile.Name = nick;

            var resolvedVoiceId = ResolveVoiceIdByMood(mood, settings, brandProfile.VoiceId);
            brandProfile.VoiceId = resolvedVoiceId;
            if (renderOptions.TtsOptions != null)
            {
                log?.Invoke("[Quote] Profile: «" + nick + "» | Mood: " + mood
                            + " | TTS: popup Âm thanh ("
                            + (renderOptions.TtsOptions.BodyEngine == TtsEngineKind.ElevenLabs ? "ElevenLabs" : "Edge")
                            + ")");
            }
            else
            {
                log?.Invoke("[Quote] Profile: «" + nick + "» | Mood: " + mood +
                            (string.IsNullOrWhiteSpace(resolvedVoiceId) ? "" : " | Voice ID: " + resolvedVoiceId));
            }

            await VideoReupRemixService.EnsureFfmpegToolkitAsync(settings, log, cancellationToken).ConfigureAwait(false);
            if (!FfmpegToolkitService.TryResolve(settings, out var toolkit, out var ffResolveErr))
            {
                throw new InvalidOperationException(ffResolveErr);
            }

            var ffmpeg = toolkit.FfmpegExe;
            EnsureFinishedProductLayout(settings);
            var profileOutputDir = GetProfileOutputDirectory(nick);
            var stage = Path.Combine(
                profileOutputDir,
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(stage);
            log?.Invoke("[Quote] Thành phẩm → " + profileOutputDir);
            var assets = Path.Combine(stage, "assets");
            Directory.CreateDirectory(assets);

            progress?.Invoke("Bước 1/4: chuẩn bị nền…", 15);
            var bgPath = Path.Combine(assets, "background.mp4");
            var visualPrompt = PhilosophyProfileAssets.EnhanceVisualPrompt(BuildVisualPrompt(mood), brandProfile);
            var motionPrompt = ResolveMotionPrompt(item, visualPrompt);
            var visualMode = PhilosophyVisualModes.Normalize(renderOptions.VisualMode);
            var (minDurationSeconds, maxDurationSeconds) = NormalizeDurationBounds(renderOptions);
            var backgroundTargetSeconds = Math.Max(8d, Math.Min(120d, (minDurationSeconds + maxDurationSeconds) / 2d));
            log?.Invoke("[Quote] Chế độ hình ảnh: " + DescribeVisualMode(visualMode));
            log?.Invoke("[Quote] Thời lượng xuất: đọc hết quote + "
                        + PhilosophyRenderOptions.OutroPadMinSeconds.ToString("0", CultureInfo.InvariantCulture)
                        + "–"
                        + PhilosophyRenderOptions.OutroPadMaxSeconds.ToString("0", CultureInfo.InvariantCulture)
                        + "s thở (tùy video nền / phân cảnh cuối).");

            await EnsureBackgroundReadyAsync(
                visualMode,
                nick,
                motionPrompt,
                visualPrompt,
                renderOptions,
                bgPath,
                settings,
                backgroundTargetSeconds,
                log,
                cancellationToken).ConfigureAwait(false);

            var bgDuration = await ProbeDurationAsync(toolkit.FfprobeExe, bgPath, cancellationToken).ConfigureAwait(false);

            progress?.Invoke("Bước 2/4: TTS đọc script…", 45);
            var voicePath = Path.Combine(assets, "voice.mp3");
            var ttsWorkDir = Path.Combine(assets, "tts_work");
            if (renderOptions.TtsOptions != null)
            {
                log?.Invoke("[Quote] Bước 2/4: TTS theo cấu hình popup Âm thanh…");
                await PhilosophyBatchTtsHelper.GenerateQuoteVoiceMp3Async(
                    quote,
                    renderOptions.TtsOptions,
                    settings,
                    voicePath,
                    ttsWorkDir,
                    renderOptions.NarrationSpeedPercent,
                    ffmpeg,
                    toolkit.FfprobeExe,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                log?.Invoke("[Quote] Bước 2/4: TTS (eleven_v3, ngắt nghỉ sâu)…");
                var ttsText = ElevenLabsTtsHelper.ApplyDeepPauses(quote);
                await BuildTtsVoiceAsync(ttsText, voicePath, settings, mood, brandProfile.VoiceId, log, cancellationToken).ConfigureAwait(false);
            }

            var voiceWav = Path.Combine(assets, "voice.wav");
            await ConvertToWavAsync(ffmpeg, voicePath, voiceWav, log, cancellationToken).ConfigureAwait(false);
            var voiceDur = await ProbeDurationAsync(toolkit.FfprobeExe, voiceWav, cancellationToken).ConfigureAwait(false);
            if (voiceDur < 0.5d)
            {
                throw new InvalidOperationException("Không đọc được thời lượng giọng TTS.");
            }

            var outroPad = PhilosophyRenderOptions.ResolveOutroPadSeconds(voiceDur, bgDuration);
            var outputDuration = PhilosophyRenderOptions.ResolveOutputDuration(voiceDur, bgDuration);
            log?.Invoke("[Quote] TTS " + voiceDur.ToString("0.0", CultureInfo.InvariantCulture) + "s | nền "
                        + bgDuration.ToString("0.0", CultureInfo.InvariantCulture) + "s → thở "
                        + outroPad.ToString("0.0", CultureInfo.InvariantCulture) + "s → xuất "
                        + outputDuration.ToString("0.0", CultureInfo.InvariantCulture) + "s.");
            var voiceForRender = voiceWav;
            var voiceRenderDuration = voiceDur;

            progress?.Invoke("Bước 3/4: render MP4 (1-pass)…", 75);
            log?.Invoke("[Quote] Bước 3/4: render MP4 1-pass (nền + audio + phụ đề + CTA)…");
            var outputPath = Path.Combine(stage, "philosophy_video_branded.mp4");
            await RenderFinalAsync(
                ffmpeg,
                bgPath,
                voiceForRender,
                voiceRenderDuration,
                outputDuration,
                quote,
                settings,
                outputPath,
                mood,
                nick,
                renderOptions,
                log,
                cancellationToken).ConfigureAwait(false);

            var outDur = await ProbeDurationAsync(toolkit.FfprobeExe, outputPath, cancellationToken).ConfigureAwait(false);
            progress?.Invoke("Hoàn tất", 100);
            log?.Invoke("[Quote] Xong → " + outputPath);

            return new PhilosophyVideoResult
            {
                OutputPath = outputPath,
                Quote = quote,
                Mood = mood,
                VisualPrompt = visualPrompt,
                DurationSeconds = outDur,
                ProfileName = nick
            };
        }

        private async Task EnsureBackgroundReadyAsync(
            int visualMode,
            string nick,
            string motionPrompt,
            string visualPrompt,
            PhilosophyRenderOptions renderOptions,
            string bgPath,
            AppSettings settings,
            double backgroundTargetSeconds,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (visualMode == PhilosophyVisualModes.VeoMascot)
            {
                log?.Invoke("[Quote] Bước 1/4: Mode 2 — AI Nhân vật (Veo Image-to-Video)…");
                var (ok, reason) = await TryGenerateMascotBackgroundVideoAsync(
                    motionPrompt,
                    visualPrompt,
                    nick,
                    renderOptions?.ReferenceImagePath,
                    bgPath,
                    settings,
                    backgroundTargetSeconds,
                    log,
                    cancellationToken).ConfigureAwait(false);
                if (!ok)
                {
                    throw new InvalidOperationException(
                        "Bước 1/4 — không tạo được nền AI Nhân vật (Veo I2V).\r\n" +
                        (string.IsNullOrWhiteSpace(reason) ? "Không rõ lý do." : reason));
                }

                return;
            }

            if (visualMode == PhilosophyVisualModes.VeoScenery)
            {
                log?.Invoke("[Quote] Bước 1/4: Mode 1 — AI Cảnh vật (Veo Text-to-Video)…");
                var (ok, reason) = await TryGenerateSceneryBackgroundVideoAsync(
                    motionPrompt,
                    bgPath,
                    settings,
                    backgroundTargetSeconds,
                    log,
                    cancellationToken).ConfigureAwait(false);
                if (!ok)
                {
                    throw new InvalidOperationException(
                        "Bước 1/4 — không tạo được nền AI Cảnh vật (Veo T2V).\r\n" +
                        (string.IsNullOrWhiteSpace(reason) ? "Không rõ lý do." : reason));
                }

                return;
            }

            if (visualMode == PhilosophyVisualModes.PreRendered)
            {
                log?.Invoke("[Quote] Bước 1/4: Mode 3 — Video phân cảnh tự làm sẵn…");
                var (ok, reason) = await TryAssemblePreRenderedBackgroundAsync(
                    renderOptions,
                    bgPath,
                    log,
                    cancellationToken).ConfigureAwait(false);
                if (!ok)
                {
                    throw new InvalidOperationException(
                        "Bước 1/4 — không ghép được video phân cảnh tự làm.\r\n" +
                        (string.IsNullOrWhiteSpace(reason) ? "Kiểm tra thư mục video và tên file." : reason));
                }

                return;
            }

            log?.Invoke("[Quote] Bước 1/4: Mode 0 — kho B-Roll…");
            if (TryResolveBrollBackground(nick, renderOptions, bgPath, log))
            {
                return;
            }

            throw new InvalidOperationException(BuildBrollMissingHelpMessage(nick, renderOptions));
        }

        /// <summary>
        /// Mode 3: Quét thư mục PreRenderedFolder tìm file theo tên quy ước,
        /// kiểm tra dãy liên tục, concat bằng FFmpeg.
        /// </summary>
        private async Task<(bool Success, string FailureReason)> TryAssemblePreRenderedBackgroundAsync(
            PhilosophyRenderOptions renderOptions,
            string outputPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var folder = (renderOptions?.PreRenderedFolder ?? string.Empty).Trim();
            var quote = (renderOptions?.QuoteForSceneMatch ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return (false, "Chưa chọn thư mục video phân cảnh (Chế độ 3). Nhập đường dẫn vào ô «Thư mục video».");
            }

            if (string.IsNullOrEmpty(quote))
            {
                return (false, "Không có nội dung quote để tìm tên file quy ước.");
            }

            var files = PhilosophySceneHelper.FindPreRenderedVideoFiles(folder, quote);
            if (files.Count == 0)
            {
                var prefix = PhilosophySceneHelper.BuildFilePrefix(quote);
                return (false, "Không tìm thấy file nào khớp tiền tố «" + prefix + "» trong: " + folder);
            }

            // Ước tính số phân cảnh từ câu triết lý
            var expectedScenes = PhilosophySceneHelper.SplitIntoScenes(quote).Count;
            var missing = PhilosophySceneHelper.FindMissingSceneNumbers(files, expectedScenes);
            if (missing.Count > 0)
            {
                var missingStr = string.Join(", ", missing.Select(n => n.ToString("D2")));
                log?.Invoke("[Quote] Cảnh thiếu: " + missingStr + " — sẽ render không đủ phân cảnh.");
                // Không ném exception — bỏ qua cảnh thiếu, concat những cảnh có
            }

            if (files.Count == 1)
            {
                // Chỉ 1 file — copy thẳng
                File.Copy(files[0], outputPath, overwrite: true);
                log?.Invoke("[Quote] Mode 3: 1 file → copy trực tiếp.");
                return (true, string.Empty);
            }

            // Concat với FFmpeg
            try
            {
                await FfmpegConcatAsync(renderOptions?.FfmpegExe ?? string.Empty, files, outputPath, log, cancellationToken)
                    .ConfigureAwait(false);
                if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 10_000L)
                {
                    log?.Invoke("[Quote] Mode 3: concat " + files.Count + " cảnh → " + Path.GetFileName(outputPath));
                    return (true, string.Empty);
                }

                return (false, "FFmpeg concat xong nhưng file output rỗng.");
            }
            catch (Exception ex)
            {
                return (false, "FFmpeg concat thất bại: " + ex.Message);
            }
        }

        /// <summary>Ghép danh sách file video thành 1 bằng FFmpeg concat demuxer.</summary>
        private static async Task FfmpegConcatAsync(
            string ffmpeg,
            List<string> inputFiles,
            string outputPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
            {
                if (!FfmpegToolkitService.TryResolve(null, out var tk, out _))
                {
                    throw new InvalidOperationException("Không tìm thấy FFmpeg.");
                }

                ffmpeg = tk.FfmpegExe;
            }

            var listFile = Path.Combine(Path.GetTempPath(), "concat_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");
            try
            {
                var lines = inputFiles.Select(f => "file '" + f.Replace("'", "'\\''") + "'");
                File.WriteAllLines(listFile, lines, TextFileEncoding.Utf8NoBom);

                var args = "-y -f concat -safe 0 -i \"" + listFile + "\" -c copy \"" + outputPath + "\"";
                log?.Invoke("[Quote] FFmpeg concat " + inputFiles.Count + " file…");
                await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try { if (File.Exists(listFile)) File.Delete(listFile); } catch { /* ignored */ }
            }
        }

        private static string BuildBrollMissingHelpMessage(string profileName, PhilosophyRenderOptions renderOptions)
        {
            var folder = (renderOptions?.BRollFolder ?? string.Empty).Trim();
            var assetsBroll = Path.Combine(PhilosophyProfileAssets.GetAssetsRoot(profileName), "broll");
            var sharedNature = Path.Combine(ProfileScopedPaths.GetSharedBackgroundsDirectory(), "Nature");
            var lines = new List<string>
            {
                "Bước 1/4 — chế độ B-Roll nhưng không tìm thấy file video .mp4."
            };
            if (PhilosophyBRollSelection.IsRandomToken(folder))
            {
                lines.Add("Đã chọn «Ngẫu nhiên» nhưng kho Assets chưa có video .mp4.");
            }
            else if (!string.IsNullOrEmpty(folder))
            {
                lines.Add("Video/thư mục đã chọn không hợp lệ: " + folder);
            }
            else
            {
                lines.Add("Cột «Nền» đang trống — bấm cột «Nền» → «Ngẫu nhiên» hoặc «Chọn video…».");
            }

            lines.Add(string.Empty);
            lines.Add("Cách thêm B-Roll:");
            lines.Add("1. Bấm «Mở Assets» → copy file .mp4 vào:");
            lines.Add("   " + assetsBroll);
            lines.Add("2. Hoặc dùng kho chung: " + sharedNature);
            lines.Add("3. Bấm cột «Nền» trên bảng → «Chọn video…».");
            return string.Join("\r\n", lines);
        }

        private static bool TryCopyUserBRollVideo(string selection, string profileName, string destPath, Action<string> log)
        {
            var path = (selection ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (PhilosophyBRollSelection.IsRandomToken(path))
            {
                path = PhilosophyBRollSelection.PickRandomVideoPath(profileName);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                return TryCopyVideoFile(path, destPath, log, "B-Roll ngẫu nhiên");
            }

            if (File.Exists(path))
            {
                return TryCopyVideoFile(path, destPath, log, "B-Roll: " + Path.GetFileName(path));
            }

            var src = TryPickRandomVideoFile(path);
            if (string.IsNullOrWhiteSpace(src))
            {
                return false;
            }

            return TryCopyVideoFile(src, destPath, log, "B-Roll người dùng: " + Path.GetFileName(src));
        }

        private static bool TryCopyVideoFile(string src, string destPath, Action<string> log, string logLabel)
        {
            try
            {
                File.Copy(src, destPath, true);
                log?.Invoke("[Quote] " + logLabel);
                return new FileInfo(destPath).Length > 10_000L;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Quote] B-Roll lỗi: " + ex.Message);
                return false;
            }
        }

        private static string TryPickRandomVideoFile(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return string.Empty;
            }

            var files = Directory.GetFiles(folder, "*.mp4", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(folder, "*.mov", SearchOption.TopDirectoryOnly))
                .Where(f => new FileInfo(f).Length > 10_000L)
                .ToArray();
            if (files.Length == 0)
            {
                return string.Empty;
            }

            lock (BrollRandom)
            {
                return files[BrollRandom.Next(files.Length)];
            }
        }

        private static string NormalizePhilosophyMood(string mood)
        {
            var m = (mood ?? string.Empty).Trim().ToLowerInvariant();
            var valid = new[] { "calm", "hopeful", "melancholic", "intense", "reflective", "sad" };
            return valid.Contains(m) ? m : "reflective";
        }

        /// <summary>Chọn ElevenLabs voice_id theo mood — fallback profile rồi endpoint TTS.</summary>
        public static string ResolveVoiceIdByMood(string mood, AppSettings settings, string profileFallbackVoiceId = null)
        {
            var moodVoice = ElevenLabsTtsService.GetVoiceIdForMood(mood, settings);
            if (!string.IsNullOrWhiteSpace(moodVoice))
            {
                return moodVoice.Trim();
            }

            var profileVoice = (profileFallbackVoiceId ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(profileVoice))
            {
                return profileVoice;
            }

            return ElevenLabsTtsHelper.ExtractVoiceIdFromEndpoint(settings?.TtsEndpoint);
        }

        public static AssSubtitleGeneratorOptions CreatePhilosophyKaraokeOptions()
        {
            return new AssSubtitleGeneratorOptions
            {
                FontName = "Times New Roman",
                FontSize = 76,
                MarginV = 300,
                Alignment = 5,
                Bold = true,
                WordsPerLine = 8,
                RhythmicLineBreaks = true,
                Animation = ReupKaraokeAnimationMode.Highlight,
                PrimaryColourAss = "&H00FFFFFF",
                SecondaryColourAss = "&H00D7FF00"
            };
        }

        private static bool TryCopyDynamicBackgroundVideo(string destPath, Action<string> log)
        {
            var src = PhilosophyProfileAssets.TryPickDynamicBackgroundVideo();
            if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
            {
                return false;
            }

            try
            {
                File.Copy(src, destPath, true);
                log?.Invoke("[Quote] DynamicBackground: " + src);
                return new FileInfo(destPath).Length > 10_000L;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Quote] DynamicBackground lỗi: " + ex.Message);
                return false;
            }
        }

        private static bool TryCopyProfileBackgroundVideo(string profileName, string destPath, Action<string> log)
        {
            var src = PhilosophyProfileAssets.TryPickBackgroundVideo(profileName);
            if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
            {
                return false;
            }

            try
            {
                File.Copy(src, destPath, true);
                log?.Invoke("[Quote] Nền từ Assets/" + ProfileScopedPaths.ResolveProfileName(profileName) + ": " + Path.GetFileName(src));
                return new FileInfo(destPath).Length > 10_000L;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Quote] Không copy nền profile: " + ex.Message);
                return false;
            }
        }

        private static string BuildVisualPrompt(string mood)
        {
            switch ((mood ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "calm":
                    return "cinematic nature, soft sunrise, misty mountain, slow motion, gentle camera drift, vertical 9:16";
                case "hopeful":
                    return "golden hour cityscape, cinematic lens flare, uplifting atmosphere, smooth dolly shot, vertical 9:16";
                case "melancholic":
                    return "rainy streets at dusk, moody lighting, shallow depth of field, slow cinematic pan, vertical 9:16";
                case "intense":
                    return "dramatic clouds, high contrast cinematic look, powerful motion, deep shadows, vertical 9:16";
                default:
                    return "quiet forest path, moody lighting, cinematic composition, slow motion, vertical 9:16";
            }
        }

        private static (int Min, int Max) NormalizeDurationBounds(PhilosophyRenderOptions renderOptions)
        {
            return PhilosophyRenderOptions.NormalizeDurationBounds(
                renderOptions?.MinDurationSeconds ?? 15,
                renderOptions?.MaxDurationSeconds ?? 60);
        }

        private static string ResolveMotionPrompt(PhilosophyScriptItem item, string visualPrompt)
        {
            var motion = (item?.MotionPrompt ?? string.Empty).Trim();
            return !string.IsNullOrEmpty(motion) ? motion : visualPrompt;
        }

        private static string DescribeVisualMode(int visualMode)
        {
            switch (visualMode)
            {
                case PhilosophyVisualModes.VeoScenery:
                    return "1 — AI tự sinh Cảnh vật (Veo Text-to-Video)";
                case PhilosophyVisualModes.VeoMascot:
                    return "2 — AI có Nhân vật (Veo Image-to-Video)";
                case PhilosophyVisualModes.PreRendered:
                    return "3 — Video phân cảnh tự làm sẵn";
                default:
                    return "0 — Kho B-Roll có sẵn (Tiết kiệm)";
            }
        }

        private static bool TryResolveBrollBackground(
            string nick,
            PhilosophyRenderOptions renderOptions,
            string bgPath,
            Action<string> log)
        {
            return TryCopyUserBRollVideo(renderOptions?.BRollFolder, nick, bgPath, log)
                   || TryCopyDynamicBackgroundVideo(bgPath, log)
                   || TryCopyProfileBackgroundVideo(nick, bgPath, log);
        }

        private static bool IsVeoQuotaError(Exception ex)
        {
            var msg = (ex?.Message ?? string.Empty).ToLowerInvariant();
            return msg.Contains("429") || msg.Contains("quota") || msg.Contains("rate limit");
        }

        private async Task<(bool Success, string FailureReason)> TryGenerateMascotBackgroundVideoAsync(
            string motionPrompt,
            string visualPrompt,
            string profileName,
            string referenceImagePath,
            string outputPath,
            AppSettings settings,
            double clipDurationSeconds,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings.VeoEndpoint))
            {
                return (false, "Chưa cấu hình RapidAPI Veo key hoặc endpoint Video AI trong tab Cài đặt.");
            }

            try
            {
                var prompt = string.IsNullOrWhiteSpace(motionPrompt) ? visualPrompt : motionPrompt.Trim();
                string firstIdentity = null;
                var refPath = (referenceImagePath ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(refPath) && File.Exists(refPath))
                {
                    firstIdentity = refPath;
                    log?.Invoke("[Quote] Mode 2: dùng ảnh tham chiếu từ popup «Nền»…");
                }
                else
                {
                    AvatarIdentityPackStore.LoadOrCreate(profileName);
                    var identityFiles = AvatarIdentityPackStore.SyncIdentityImagesFromVault(profileName);
                    firstIdentity = identityFiles.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f));
                    if (!string.IsNullOrWhiteSpace(firstIdentity))
                    {
                        log?.Invoke("[Quote] Mode 2: dùng Identity Image từ AvatarVault/«" + profileName + "»…");
                    }
                }

                if (string.IsNullOrWhiteSpace(firstIdentity))
                {
                    return (false, "Chưa có ảnh tham chiếu (popup «Nền») hoặc Identity Image trong AvatarVault/«"
                                 + profileName + "».");
                }

                var identityDataUrl = MascotMediaHelper.BuildImageDataUrl(firstIdentity);
                log?.Invoke("[Quote] Mode 2: sinh ảnh cảnh có nhân vật từ ảnh tham chiếu…");

                var contextImageUrl = await MascotWorker.GenerateContextImageWithPollingAsync(
                    identityDataUrl,
                    prompt + ". Cinematic vertical 9:16, character in philosophical scene, no text on screen.",
                    settings.VeoApiKey.Trim(),
                    settings.VeoEndpoint.Trim(),
                    null,
                    cancellationToken,
                    log).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(contextImageUrl))
                {
                    return (false, "Veo không trả ảnh cảnh (context image).");
                }

                log?.Invoke("[Quote] Mode 2: Veo Image-to-Video từ ảnh cảnh…");
                var videoUrl = await MascotWorker.GenerateVideoFromImageWithPollingAsync(
                    contextImageUrl,
                    prompt + ". Subtle cinematic motion, vertical 9:16, no text.",
                    settings.VeoApiKey.Trim(),
                    settings.VeoEndpoint.Trim(),
                    clipDurationSeconds,
                    cancellationToken,
                    log).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    return (false, "Veo không trả URL video nền.");
                }

                log?.Invoke("[Quote] Mode 2: đang tải clip nền…");
                await DownloadUrlToFileAsync(videoUrl, outputPath, cancellationToken).ConfigureAwait(false);
                if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 10_000L)
                {
                    return (true, string.Empty);
                }

                return (false, "File video tải về trống hoặc quá nhỏ.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (IsVeoQuotaError(ex))
                {
                    return (false, "Veo hết quota / rate limit (429). Thử lại sau hoặc đổi chế độ nền.");
                }

                return (false, ex.Message);
            }
        }

        private async Task<(bool Success, string FailureReason)> TryGenerateSceneryBackgroundVideoAsync(
            string motionPrompt,
            string outputPath,
            AppSettings settings,
            double clipDurationSeconds,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings.VeoEndpoint))
            {
                return (false, "Chưa cấu hình RapidAPI Veo key hoặc endpoint Video AI trong tab Cài đặt.");
            }

            try
            {
                var prompt = string.IsNullOrWhiteSpace(motionPrompt)
                    ? "cinematic nature, slow motion, vertical 9:16"
                    : motionPrompt.Trim();
                var videoUrl = await _videoService.GenerateVideoAsync(
                    prompt + ". No text on screen. Ambient cinematic loop.",
                    settings.VeoApiKey.Trim(),
                    settings.VeoEndpoint.Trim(),
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    return (false, "Veo không trả URL video (Text-to-Video).");
                }

                log?.Invoke("[Quote] Mode 1: đang tải video nền Veo…");
                await DownloadUrlToFileAsync(videoUrl, outputPath, cancellationToken).ConfigureAwait(false);
                if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 10_000L)
                {
                    return (true, string.Empty);
                }

                return (false, "File video tải về trống hoặc quá nhỏ.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (IsVeoQuotaError(ex))
                {
                    return (false, "Veo hết quota / rate limit (429). Thử lại sau hoặc đổi chế độ nền.");
                }

                return (false, ex.Message);
            }
        }

        private static async Task CreateGradientBackgroundAsync(
            string ffmpeg,
            string outputPath,
            double durationSeconds,
            string gradientColor,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var color = string.IsNullOrWhiteSpace(gradientColor) ? "0x1a1a2e" : gradientColor.Trim();
            var dur = durationSeconds.ToString("0.##", CultureInfo.InvariantCulture);
            var vf = "scale=" + TargetWidth + ":" + TargetHeight + ":force_original_aspect_ratio=increase," +
                     "crop=" + TargetWidth + ":" + TargetHeight + ",format=yuv420p";
            var args = "-y -f lavfi -i color=c=" + color + ":s=" + TargetWidth + "x" + TargetHeight + ":d=" + dur +
                       " -vf \"" + vf + "\" -c:v libx264 -preset fast -crf 23 -an \"" + outputPath + "\"";
            log?.Invoke("[Quote] FFmpeg: tạo nền gradient " + dur + "s…");
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private async Task BuildTtsVoiceAsync(
            string quote,
            string outputMp3,
            AppSettings settings,
            string mood,
            string profileVoiceId,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var ttsScript = (quote ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ttsScript))
            {
                throw new InvalidOperationException("Script trống — không gọi TTS.");
            }

            log?.Invoke("[Quote] ElevenLabs chỉ đọc nội dung quote (" + ttsScript.Length + " ký tự).");
            var audioResult = await _elevenLabsTtsService.GenerateAudioWithFallbackAsync(
                ttsScript,
                settings,
                mood,
                profileVoiceId,
                emphaticHook: false,
                log,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(audioResult))
            {
                throw new InvalidOperationException("TTS API trả audio rỗng.");
            }

            if (File.Exists(outputMp3))
            {
                File.Delete(outputMp3);
            }

            await SaveTtsAudioResultAsync(audioResult, outputMp3, cancellationToken).ConfigureAwait(false);
            log?.Invoke("[Quote] TTS OK → " + Path.GetFileName(outputMp3));
        }

        private static async Task SaveTtsAudioResultAsync(string audioPathOrUrl, string outputMp3, CancellationToken cancellationToken)
        {
            var source = (audioPathOrUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(source))
            {
                throw new InvalidOperationException("TTS không trả đường dẫn audio.");
            }

            if (File.Exists(source))
            {
                File.Copy(source, outputMp3, overwrite: true);
                return;
            }

            if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                await DownloadUrlToFileAsync(source, outputMp3, cancellationToken).ConfigureAwait(false);
                return;
            }

            throw new InvalidOperationException("TTS trả path không hợp lệ: " + source);
        }

        private async Task RenderFinalAsync(
            string ffmpeg,
            string backgroundPath,
            string voiceWav,
            double voiceSourceDuration,
            double outputDuration,
            string quote,
            AppSettings settings,
            string outputPath,
            string mood,
            string profileName,
            PhilosophyRenderOptions renderOptions,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var stage = Path.GetDirectoryName(voiceWav) ?? ".";
            var durStr = outputDuration.ToString("0.#####", CultureInfo.InvariantCulture);
            var fullAudio = Path.Combine(stage, "full_audio.wav");
            var musicPath = PhilosophyProfileAssets.ResolveMusicPath(
                renderOptions?.MusicFolder,
                profileName,
                settings,
                mood);
            if (string.IsNullOrEmpty(musicPath))
            {
                musicPath = PhilosophyProfileAssets.TryPickMusicFile(profileName, mood);
            }

            if (string.IsNullOrEmpty(musicPath))
            {
                musicPath = TryPickMusicByMood(settings, mood);
            }

            var ambientPath = renderOptions?.AmbientFolder ?? string.Empty;
            if (!string.IsNullOrEmpty(ambientPath) && Directory.Exists(ambientPath))
            {
                ambientPath = TryPickAmbientFromFolder(ambientPath, mood);
            }
            if (!string.IsNullOrEmpty(musicPath) && File.Exists(musicPath))
            {
                var musicPct = renderOptions?.MusicVolumePercent ?? PhilosophyBatchHelper.DefaultMusicVolumePercent;
                log?.Invoke("[Quote] Nhạc nền (" + musicPct + "%): " + Path.GetFileName(musicPath));
                await BuildVoicePlusMusicWavAsync(
                    ffmpeg,
                    voiceWav,
                    musicPath,
                    ambientPath,
                    voiceSourceDuration,
                    outputDuration,
                    fullAudio,
                    PhilosophyBatchHelper.ResolveMusicBedLinearVolume(musicPct),
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrEmpty(ambientPath) && File.Exists(ambientPath))
            {
                log?.Invoke("[Quote] Chỉ voice + ambient (~5%): " + Path.GetFileName(ambientPath));
                await BuildVoicePlusMusicWavAsync(
                    ffmpeg,
                    voiceWav,
                    null,
                    ambientPath,
                    voiceSourceDuration,
                    outputDuration,
                    fullAudio,
                    0d,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (outputDuration > voiceSourceDuration + 0.05d)
            {
                await PadVoiceWavAsync(ffmpeg, voiceWav, fullAudio, voiceSourceDuration, outputDuration, log, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                File.Copy(voiceWav, fullAudio, true);
            }

            log?.Invoke("[Quote] Mix audio xong → " + Path.GetFileName(fullAudio));

            var assOptions = renderOptions?.SubtitleOptions ?? CreatePhilosophyKaraokeOptions();
            KaraokeAssSubtitleService.KaraokeAssBurnInResult quoteKaraoke = null;
            try
            {
                log?.Invoke("[Quote] Phụ đề: ước lượng timing từ giọng TTS (bỏ qua Whisper).");
                quoteKaraoke = await KaraokeAssSubtitleService.TryCreateBurnInAsync(
                    ffmpeg,
                    quote,
                    voiceWav,
                    stage,
                    log,
                    cancellationToken,
                    openAiApiKey: null,
                    assOptions: assOptions,
                    knownAudioDurationSeconds: voiceSourceDuration).ConfigureAwait(false);
                log?.Invoke(quoteKaraoke != null
                    ? "[Quote] Phụ đề ASS OK."
                    : "[Quote] Không có phụ đề ASS.");

                var logoPlan = PhilosophyBatchHelper.BuildLogoRenderPlan(renderOptions, profileName, settings, TargetWidth);
                var hasLogo = logoPlan.IsActive;
                var subtitleFilter = quoteKaraoke?.VideoFilterFragment ?? string.Empty;

                // ── Time-matching: bóp/giãn tốc độ hình ảnh nền khớp với audio ──────
                var bgDuration = await ProbeDurationAsync(
                    FfmpegToolkitService.TryResolve(settings, out var tkMatch, out _)
                        ? tkMatch.FfprobeExe
                        : ffmpeg.Replace("ffmpeg.exe", "ffprobe.exe"),
                    backgroundPath,
                    cancellationToken).ConfigureAwait(false);

                var setptsFilter = BuildSetPtsFilter(bgDuration, outputDuration, log);

                string args;
                // Dùng -stream_loop chỉ khi video nền (sau setpts) vẫn ngắn hơn output
                var needLoop = bgDuration < 0.1d || string.IsNullOrEmpty(setptsFilter) && bgDuration < outputDuration - 0.5d;
                var loopFlag = needLoop ? "-stream_loop -1 " : string.Empty;

                if (hasLogo)
                {
                    var filterComplex = BuildPhilosophyOnePassFilterComplex(subtitleFilter, logoPlan, setptsFilter);
                    args = "-y " + loopFlag + "-i \"" + backgroundPath + "\" -i \"" + fullAudio + "\" -i \"" + logoPlan.LogoPath +
                           "\" -filter_complex \"" + filterComplex + "\" -map \"[vout]\" -map 1:a -t " + durStr +
                           " -c:v libx264 -preset fast -crf 22 -pix_fmt yuv420p -c:a aac -b:a 192k \"" + outputPath + "\"";
                    log?.Invoke("[Quote] FFmpeg 1-pass (nền + logo + phụ đề + CTA) → " + durStr + "s…");
                }
                else
                {
                    var videoFilters = BuildPhilosophyOnePassVideoFilters(subtitleFilter, setptsFilter);
                    args = "-y " + loopFlag + "-i \"" + backgroundPath + "\" -i \"" + fullAudio + "\" -vf \"" + videoFilters +
                           "\" -map 0:v -map 1:a -t " + durStr +
                           " -c:v libx264 -preset fast -crf 22 -pix_fmt yuv420p -c:a aac -b:a 192k \"" + outputPath + "\"";
                    log?.Invoke("[Quote] FFmpeg 1-pass (nền + phụ đề + CTA) → " + durStr + "s…");
                }

                await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
                log?.Invoke("[Quote] Render 1-pass xong → " + Path.GetFileName(outputPath));
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(quoteKaraoke?.AssFilePath);
            }
        }

        /// <summary>Tạo setpts filter để bóp/giãn tốc độ hình nền cho khớp outputDuration.</summary>
        private static string BuildSetPtsFilter(double bgDuration, double outputDuration, Action<string> log)
        {
            if (bgDuration < 0.1d || outputDuration < 0.1d)
            {
                return string.Empty;
            }

            var ratio = bgDuration / outputDuration;
            if (ratio > 0.98d && ratio < 1.02d)
            {
                // Gần bằng nhau — không cần setpts
                return string.Empty;
            }

            var pts = ratio.ToString("0.######", CultureInfo.InvariantCulture);
            log?.Invoke("[Quote] Time-match: bgDur=" + bgDuration.ToString("0.0") + "s / audioDur=" +
                        outputDuration.ToString("0.0") + "s → setpts=" + pts + "*PTS");
            return "setpts=" + pts + "*PTS";
        }

        private static string BuildPhilosophyBaseVideoFilterChain(string setptsFilter = null)
        {
            var baseChain = "scale=" + TargetWidth + ":" + TargetHeight +
                            ":force_original_aspect_ratio=increase,crop=" + TargetWidth + ":" + TargetHeight +
                            ",eq=brightness=-0.15,setsar=1,format=yuv420p";
            if (!string.IsNullOrWhiteSpace(setptsFilter))
            {
                // setpts применяется ПЕРЕД scale для корректной скорости
                baseChain = setptsFilter + "," + baseChain;
            }

            return baseChain;
        }

        private static string BuildPhilosophyCtaDrawTextFilter()
        {
            var ctaText = EscapeFfmpegDrawText("Follow de nghe moi ngay");
            const int ctaBottomOffset = 110;
            return "drawtext=text='" + ctaText + "':x=(w-text_w)/2:y=h-th-" + ctaBottomOffset +
                   ":fontsize=36:fontcolor=white:borderw=2:bordercolor=black@0.6";
        }

        private static string BuildPhilosophyOnePassVideoFilters(string subtitleFilterFragment, string setptsFilter = null)
        {
            var chain = BuildPhilosophyBaseVideoFilterChain(setptsFilter);
            chain = KaraokeAssSubtitleService.MergeVideoFilters(chain, subtitleFilterFragment);
            return KaraokeAssSubtitleService.MergeVideoFilters(chain, BuildPhilosophyCtaDrawTextFilter());
        }

        private static string BuildPhilosophyOnePassFilterComplex(
            string subtitleFilterFragment,
            ShowcaseBrandLogoRenderPlan logoPlan,
            string setptsFilter = null)
        {
            var baseChain = BuildPhilosophyBaseVideoFilterChain(setptsFilter);
            var cta = BuildPhilosophyCtaDrawTextFilter();
            var parts = new List<string> { "[0:v]" + baseChain + "[bg]" };
            var current = "[bg]";
            if (!string.IsNullOrWhiteSpace(subtitleFilterFragment))
            {
                parts.Add(current + subtitleFilterFragment + "[vsub]");
                current = "[vsub]";
            }

            if (logoPlan != null && logoPlan.IsActive)
            {
                var targetWidth = Math.Max(32, logoPlan.CanvasWidth * ShowcaseBrandOverlayHelper.ClampScaleWidthPercent(logoPlan.ScaleWidthPercent) / 100);
                var alpha = ShowcaseBrandOverlayHelper.ClampOpacityPercent(logoPlan.OpacityPercent) / 100d;
                var overlay = ShowcaseBrandLogoPositionCatalog.BuildOverlayExpression(
                    logoPlan.PositionId,
                    logoPlan.MarginX,
                    logoPlan.MarginY);
                var logoChain = "[2:v]scale=w=" + targetWidth + ":h=-1";
                if (alpha < 0.995d)
                {
                    var alphaText = alpha.ToString("0.##", CultureInfo.InvariantCulture);
                    logoChain += ",format=rgba,colorchannelmixer=aa=" + alphaText;
                }

                logoChain += "[logo]";
                parts.Add(logoChain);
                parts.Add(current + "[logo]overlay=" + overlay + "[vl]");
                current = "[vl]";
            }

            parts.Add(current + cta + "[vout]");
            return string.Join(";", parts);
        }

        private static string TryPickMusicFromFolder(string folder, string mood)
        {
            return TryPickAudioByMood(folder, mood, "*.mp3", "*.wav", "*.m4a");
        }

        private static string TryPickAmbientFromFolder(string folder, string mood)
        {
            return TryPickAudioByMood(folder, mood, "*.mp3", "*.wav", "*.m4a", "*.ogg");
        }

        private static string TryPickAudioByMood(string folder, string mood, params string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return string.Empty;
            }

            var files = new List<string>();
            foreach (var pattern in patterns)
            {
                files.AddRange(Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly));
            }

            if (files.Count == 0)
            {
                return string.Empty;
            }

            var key = (mood ?? string.Empty).ToLowerInvariant();
            var match = files.FirstOrDefault(f =>
                Path.GetFileName(f).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            return match ?? files[0];
        }

        private static string TryPickMusicByMood(AppSettings settings, string mood)
        {
            try
            {
                var dir = VideoReupRemixService.GetMusicLibraryDirectory(settings);
                if (!Directory.Exists(dir))
                {
                    return string.Empty;
                }

                var files = Directory.GetFiles(dir, "*.mp3", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    return string.Empty;
                }

                var key = (mood ?? string.Empty).ToLowerInvariant();
                var match = files.FirstOrDefault(f =>
                    Path.GetFileName(f).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                return match ?? files[0];
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task PadVoiceWavAsync(
            string ffmpeg,
            string voiceWav,
            string outputWav,
            double voiceSourceSeconds,
            double outputSeconds,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var voiceStr = voiceSourceSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var outStr = outputSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var args = "-y -i \"" + voiceWav + "\" -filter:a \"apad=whole_dur=" + outStr + "\" -t " + outStr +
                       " \"" + outputWav + "\"";
            log?.Invoke("[Quote] Kéo dài audio từ " + voiceStr + "s → " + outStr + "s…");
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task BuildVoicePlusMusicWavAsync(
            string ffmpeg,
            string voiceWav,
            string musicMp3,
            string ambientPath,
            double voiceSourceSeconds,
            double outputSeconds,
            string outputWav,
            double musicBedVolume,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var voiceStr = voiceSourceSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var outStr = outputSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var musicVol = musicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var ambientVol = AmbientBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var voiceGain = VoiceMixGain.ToString("0.###", CultureInfo.InvariantCulture);
            var hasMusic = !string.IsNullOrWhiteSpace(musicMp3) && File.Exists(musicMp3);
            var hasAmbient = !string.IsNullOrWhiteSpace(ambientPath) && File.Exists(ambientPath);
            var padVoice = outputSeconds > voiceSourceSeconds + 0.05d;
            var voiceFilter = padVoice
                ? "[0:a]atrim=0:" + voiceStr + ",asetpts=PTS-STARTPTS,aresample=48000,apad=whole_dur=" + outStr + ",volume=" + voiceGain + "[vo]"
                : "[0:a]atrim=0:" + voiceStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + voiceGain + "[vo]";

            string filter;
            string inputs;
            if (hasMusic && hasAmbient)
            {
                inputs = "-y -i \"" + voiceWav + "\" -i \"" + musicMp3 + "\" -i \"" + ambientPath + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + musicVol + "[bg];" +
                    "[2:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + ambientVol + "[amb];" +
                    "[vo][bg][amb]amix=inputs=3:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else if (hasMusic)
            {
                inputs = "-y -i \"" + voiceWav + "\" -i \"" + musicMp3 + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + musicVol + "[bg];" +
                    "[vo][bg]amix=inputs=2:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else if (hasAmbient)
            {
                inputs = "-y -i \"" + voiceWav + "\" -i \"" + ambientPath + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + ambientVol + "[amb];" +
                    "[vo][amb]amix=inputs=2:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else
            {
                await PadVoiceWavAsync(ffmpeg, voiceWav, outputWav, voiceSourceSeconds, outputSeconds, log, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var args = inputs + " -filter_complex \"" + filter + "\" -map \"[aout]\" -t " + outStr +
                       " -c:a pcm_s16le \"" + outputWav + "\"";
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task ConvertToWavAsync(
            string ffmpeg,
            string inputAudio,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var args = "-y -i \"" + inputAudio + "\" -ac 1 -ar 48000 \"" + outputWav + "\"";
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<double> ProbeDurationAsync(string ffprobe, string mediaPath, CancellationToken cancellationToken)
        {
            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + mediaPath + "\"";
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    return 0d;
                }

                var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                if (!ProcessCancellationHelper.WaitForExit(process, 15000, cancellationToken))
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch
                    {
                        // ignored
                    }

                    return 0d;
                }

                if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    return Math.Max(0d, seconds);
                }
            }

            return 0d;
        }

        private static async Task DownloadUrlToFileAsync(string url, string outputPath, CancellationToken cancellationToken)
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", "Mozilla/5.0");
                cancellationToken.Register(() => webClient.CancelAsync());
                await webClient.DownloadFileTaskAsync(new Uri(url), outputPath).ConfigureAwait(false);
            }
        }

        private static Task RunFfmpegAsync(string ffmpegExecutable, string args, Action<string> log, CancellationToken cancellationToken)
        {
            return VideoReupRemixService.RunFfmpegPublicAsync(ffmpegExecutable, args, log, cancellationToken);
        }

        private static string EscapeFfmpegDrawText(string text)
        {
            return (text ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace(":", "\\:")
                .Replace("%", "\\%");
        }
    }
}
