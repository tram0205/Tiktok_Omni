using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Pipeline video Triết lý/Quote — Gemini + Veo/TTS + FFmpeg; dùng Cài đặt app (không cần Python/env).</summary>
    public sealed class PhilosophyVideoPipelineService
    {
        private const int TargetWidth = 1080;
        private const int TargetHeight = 1920;
        private const double DefaultBackgroundSeconds = 18d;
        private const double MusicBedVolume = 0.18d;

        private readonly GeminiService _gemini = new GeminiService();
        private readonly VideoService _videoService = new VideoService();

        public static string GetOutputRootDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "PhilosophyVideo", "Output");
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
                ? "Sẵn sàng — nhập quote hoặc link rồi bấm «Tạo video Triết lý»."
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

        public async Task<PhilosophyVideoResult> RunAsync(
            string inputTextOrUrl,
            AppSettings settings,
            AutomationProfile profile,
            Action<string> log,
            Action<string, int> progress,
            CancellationToken cancellationToken)
        {
            if (!TryValidatePrerequisites(settings, out var pre))
            {
                throw new InvalidOperationException(pre);
            }

            var input = (inputTextOrUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(input))
            {
                throw new InvalidOperationException("Chưa nhập quote hoặc link bài viết.");
            }

            var brandProfile = profile ?? new AutomationProfile { Name = "default" };
            if (string.IsNullOrWhiteSpace(brandProfile.Name))
            {
                brandProfile.Name = "default";
            }

            var nick = ProfileScopedPaths.ResolveProfileName(brandProfile.Name);
            brandProfile.Name = nick;
            log?.Invoke("[Triết lý] Profile: «" + nick + "»" +
                        (string.IsNullOrWhiteSpace(brandProfile.VoiceId) ? "" : " | Voice: " + brandProfile.VoiceId) +
                        (string.IsNullOrWhiteSpace(brandProfile.VideoStyle) ? "" : " | Style: " + brandProfile.VideoStyle));

            await VideoReupRemixService.EnsureFfmpegToolkitAsync(settings, log, cancellationToken).ConfigureAwait(false);
            if (!FfmpegToolkitService.TryResolve(settings, out var toolkit, out var ffResolveErr))
            {
                throw new InvalidOperationException(ffResolveErr);
            }

            var ffmpeg = toolkit.FfmpegExe;
            var stage = Path.Combine(
                GetOutputRootDirectory(),
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(stage);
            var assets = Path.Combine(stage, "assets");
            Directory.CreateDirectory(assets);

            progress?.Invoke("Bước 1/4: xử lý nội dung (Gemini)…", 10);
            log?.Invoke("[Triết lý] Bước 1/4: trích quote + mood…");
            var sourceText = await ExtractSourceTextAsync(input, cancellationToken).ConfigureAwait(false);
            var quote = await ResolveQuoteTextAsync(input, sourceText, settings, cancellationToken).ConfigureAwait(false);
            var mood = await DetectMoodAsync(quote, settings, cancellationToken).ConfigureAwait(false);
            var visualPrompt = PhilosophyProfileAssets.EnhanceVisualPrompt(BuildVisualPrompt(mood), brandProfile);
            log?.Invoke("[Triết lý] Quote: " + quote);
            log?.Invoke("[Triết lý] Mood: " + mood);

            progress?.Invoke("Bước 2/4: video nền (Assets/Veo/FFmpeg)…", 35);
            log?.Invoke("[Triết lý] Bước 2/4: tạo video nền…");
            var bgPath = Path.Combine(assets, "background.mp4");
            var usedDynamicBg = TryCopyDynamicBackgroundVideo(bgPath, log);
            var usedProfileBg = !usedDynamicBg && TryCopyProfileBackgroundVideo(nick, bgPath, log);
            var usedVeo = usedProfileBg || usedDynamicBg;
            if (!usedProfileBg && !usedDynamicBg)
            {
                usedVeo = await TryGenerateBackgroundVideoAsync(
                    visualPrompt,
                    bgPath,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!usedVeo)
            {
                var gradientColor = PhilosophyProfileAssets.ResolveGradientColor(brandProfile.VideoStyle, mood);
                await CreateGradientBackgroundAsync(ffmpeg, bgPath, DefaultBackgroundSeconds, gradientColor, log, cancellationToken)
                    .ConfigureAwait(false);
                log?.Invoke("[Triết lý] Dùng nền gradient FFmpeg (profile/Veo không có).");
            }

            progress?.Invoke("Bước 3/4: giọng đọc (TTS)…", 60);
            log?.Invoke("[Triết lý] Bước 3/4: TTS đọc quote…");
            var voicePath = Path.Combine(assets, "voice.mp3");
            await BuildTtsVoiceAsync(quote, voicePath, settings, brandProfile.VoiceId, log, cancellationToken).ConfigureAwait(false);
            var voiceWav = Path.Combine(assets, "voice.wav");
            await ConvertToWavAsync(ffmpeg, voicePath, voiceWav, log, cancellationToken).ConfigureAwait(false);
            var voiceDur = await ProbeDurationAsync(toolkit.FfprobeExe, voiceWav, cancellationToken).ConfigureAwait(false);
            if (voiceDur < 0.5d)
            {
                throw new InvalidOperationException("Không đọc được thời lượng giọng TTS.");
            }

            progress?.Invoke("Bước 4/4: ghép MP4 + phụ đề…", 85);
            log?.Invoke("[Triết lý] Bước 4/4: render MP4…");
            var outputPath = Path.Combine(stage, "philosophy_video.mp4");
            await RenderFinalAsync(
                ffmpeg,
                toolkit.FfprobeExe,
                bgPath,
                voiceWav,
                voiceDur,
                quote,
                settings,
                outputPath,
                mood,
                nick,
                log,
                cancellationToken).ConfigureAwait(false);

            progress?.Invoke("Overlay Logo/CTA…", 92);
            outputPath = await ApplyProfileBrandOverlayAsync(
                ffmpeg,
                outputPath,
                nick,
                log,
                cancellationToken).ConfigureAwait(false);

            var outDur = await ProbeDurationAsync(toolkit.FfprobeExe, outputPath, cancellationToken).ConfigureAwait(false);
            progress?.Invoke("Hoàn tất", 100);
            log?.Invoke("[Triết lý] Xong → " + outputPath);

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
                log?.Invoke("[Triết lý] DynamicBackground: " + src);
                return new FileInfo(destPath).Length > 10_000L;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Triết lý] DynamicBackground lỗi: " + ex.Message);
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
                log?.Invoke("[Triết lý] Nền từ Assets/" + ProfileScopedPaths.ResolveProfileName(profileName) + ": " + Path.GetFileName(src));
                return new FileInfo(destPath).Length > 10_000L;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Triết lý] Không copy nền profile: " + ex.Message);
                return false;
            }
        }

        private async Task<string> ResolveQuoteTextAsync(
            string rawInput,
            string sourceText,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var raw = (rawInput ?? string.Empty).Trim();
            if (!LooksLikeUrl(raw) && raw.Length > 0 && raw.Length <= 280)
            {
                return raw;
            }

            return await SummarizeToQuoteAsync(sourceText, settings, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> ExtractSourceTextAsync(string input, CancellationToken cancellationToken)
        {
            if (!LooksLikeUrl(input))
            {
                return input;
            }

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                var html = await http.GetStringAsync(input).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var title = string.Empty;
                var mTitle = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
                if (mTitle.Success)
                {
                    title = WebUtility.HtmlDecode(mTitle.Groups[1].Value.Trim());
                }

                var paragraphs = Regex.Matches(html, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                    .Cast<Match>()
                    .Select(m => StripTags(WebUtility.HtmlDecode(m.Groups[1].Value)))
                    .Where(p => p.Length > 30)
                    .Take(12);
                var body = string.Join(" ", paragraphs);
                var merged = (title + ". " + body).Trim();
                merged = Regex.Replace(merged, @"\s+", " ");
                if (merged.Length < 40)
                {
                    merged = StripTags(WebUtility.HtmlDecode(html));
                    merged = Regex.Replace(merged, @"\s+", " ");
                    if (merged.Length > 4000)
                    {
                        merged = merged.Substring(0, 4000);
                    }
                }

                return string.IsNullOrWhiteSpace(merged) ? input : merged;
            }
        }

        private static string StripTags(string html)
        {
            return Regex.Replace(html ?? string.Empty, "<[^>]+>", " ").Trim();
        }

        private static bool LooksLikeUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var u) &&
                   (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
        }

        private async Task<string> SummarizeToQuoteAsync(string sourceText, AppSettings settings, CancellationToken cancellationToken)
        {
            var text = (sourceText ?? string.Empty).Trim();
            if (text.Length > 3500)
            {
                text = text.Substring(0, 3500);
            }

            var prompt =
                "Tóm tắt nội dung sau thành MỘT câu quote triết lý tiếng Việt, sâu sắc, cảm xúc, tối đa 25 từ. " +
                "Chỉ trả câu quote, không markdown, không giải thích.\r\n\r\nNội dung:\r\n" + text;

            var raw = await _gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);
            var quote = (raw ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(quote))
            {
                quote = "Im lặng dạy ta những điều ồn ào không bao giờ nói được.";
            }

            return quote;
        }

        private async Task<string> DetectMoodAsync(string quote, AppSettings settings, CancellationToken cancellationToken)
        {
            var prompt =
                "Phân loại mood của quote sau thành MỘT nhãn: calm, hopeful, melancholic, intense, reflective. " +
                "Chỉ trả nhãn tiếng Anh, không giải thích.\r\n\r\nQuote: " + quote;
            var raw = await _gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);
            var mood = (raw ?? string.Empty).Trim().ToLowerInvariant();
            var valid = new[] { "calm", "hopeful", "melancholic", "intense", "reflective" };
            return valid.Contains(mood) ? mood : "reflective";
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

        private async Task<bool> TryGenerateBackgroundVideoAsync(
            string visualPrompt,
            string outputPath,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings.VeoApiKey) || string.IsNullOrWhiteSpace(settings.VeoEndpoint))
            {
                return false;
            }

            try
            {
                var videoUrl = await _videoService.GenerateVideoAsync(
                    visualPrompt + ". No text on screen. Ambient cinematic loop.",
                    settings.VeoApiKey.Trim(),
                    settings.VeoEndpoint.Trim(),
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(videoUrl))
                {
                    return false;
                }

                log?.Invoke("[Triết lý] Veo: đang tải video nền…");
                await DownloadUrlToFileAsync(videoUrl, outputPath, cancellationToken).ConfigureAwait(false);
                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 10_000L;
            }
            catch (Exception ex)
            {
                log?.Invoke("[Triết lý] Veo bỏ qua: " + ex.Message);
                return false;
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
            log?.Invoke("[Triết lý] FFmpeg: tạo nền gradient " + dur + "s…");
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private async Task BuildTtsVoiceAsync(
            string quote,
            string outputMp3,
            AppSettings settings,
            string voiceId,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var voiceHint = string.IsNullOrWhiteSpace(voiceId)
                ? "Giọng đọc tiếng Việt nữ, trầm ấm, triết lý, chậm rãi và rõ chữ"
                : "Giọng đọc theo voiceId «" + voiceId.Trim() + "», triết lý, chậm rãi";
            var ttsScript = voiceHint + ", phong cách quote video TikTok. Chỉ đọc một lần, không thêm lời: " + quote;
            var audioUrl = await _videoService.GenerateAudioAsync(
                ttsScript,
                settings.TtsApiKey.Trim(),
                settings.TtsEndpoint.Trim(),
                cancellationToken,
                voiceId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(audioUrl))
            {
                throw new InvalidOperationException("TTS API trả audioUrl rỗng.");
            }

            if (File.Exists(outputMp3))
            {
                File.Delete(outputMp3);
            }

            await DownloadUrlToFileAsync(audioUrl, outputMp3, cancellationToken).ConfigureAwait(false);
            log?.Invoke("[Triết lý] TTS OK → " + Path.GetFileName(outputMp3));
        }

        private async Task RenderFinalAsync(
            string ffmpeg,
            string ffprobe,
            string backgroundPath,
            string voiceWav,
            double voiceDuration,
            string quote,
            AppSettings settings,
            string outputPath,
            string mood,
            string profileName,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var stage = Path.GetDirectoryName(voiceWav) ?? ".";
            var scaledBg = Path.Combine(stage, "bg_scaled.mp4");
            var durStr = voiceDuration.ToString("0.#####", CultureInfo.InvariantCulture);
            var scaleArgs = "-y -i \"" + backgroundPath + "\" -t " + durStr +
                            " -vf \"scale=" + TargetWidth + ":" + TargetHeight +
                            ":force_original_aspect_ratio=increase,crop=" + TargetWidth + ":" + TargetHeight +
                            ",setsar=1,format=yuv420p\" -an -c:v libx264 -preset fast -crf 22 \"" + scaledBg + "\"";
            await RunFfmpegAsync(ffmpeg, scaleArgs, log, cancellationToken).ConfigureAwait(false);

            var fullAudio = Path.Combine(stage, "full_audio.wav");
            var musicPath = PhilosophyProfileAssets.TryPickMusicFile(profileName, mood);
            if (string.IsNullOrEmpty(musicPath))
            {
                musicPath = TryPickMusicByMood(settings, mood);
            }

            if (!string.IsNullOrEmpty(musicPath) && File.Exists(musicPath))
            {
                log?.Invoke("[Triết lý] Nhạc nền: " + Path.GetFileName(musicPath));
                await BuildVoicePlusMusicWavAsync(ffmpeg, voiceWav, musicPath, voiceDuration, fullAudio, log, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                File.Copy(voiceWav, fullAudio, true);
            }

            KaraokeAssSubtitleService.KaraokeAssBurnInResult quoteKaraoke = null;
            try
            {
                quoteKaraoke = await KaraokeAssSubtitleService.TryCreateBurnInAsync(
                    ffmpeg,
                    quote,
                    voiceWav,
                    stage,
                    log,
                    cancellationToken,
                    settings?.AiApiKey).ConfigureAwait(false);
                var vf = quoteKaraoke?.VideoFilterFragment ?? string.Empty;
                var vfArg = string.IsNullOrWhiteSpace(vf) ? string.Empty : " -vf \"" + vf + "\"";
                var muxArgs = "-y -i \"" + scaledBg + "\" -i \"" + fullAudio + "\"" +
                              vfArg +
                              " -c:v libx264 -preset fast -crf 22 -c:a aac -b:a 192k -shortest \"" +
                              outputPath + "\"";
                await RunFfmpegAsync(ffmpeg, muxArgs, log, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(quoteKaraoke?.AssFilePath);
            }
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

        private static async Task BuildVoicePlusMusicWavAsync(
            string ffmpeg,
            string voiceWav,
            string musicMp3,
            double voiceSec,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var musicSec = Math.Max(0.5d, voiceSec);
            var hookStr = voiceSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var musicStr = musicSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var volStr = MusicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var filter =
                "[0:a]atrim=0:" + hookStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=1[vo];" +
                "[1:a]atrim=0:" + musicStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + volStr + "[bg];" +
                "[vo][bg]amix=inputs=2:duration=first:dropout_transition=2[aout]";
            var args = "-y -i \"" + voiceWav + "\" -i \"" + musicMp3 + "\"" +
                       " -filter_complex \"" + filter + "\" -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";
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

        private static string EscapePathForFfmpegSubtitleFilter(string filePath)
        {
            var full = Path.GetFullPath(filePath).Replace('\\', '/');
            if (full.Length >= 2 && full[1] == ':')
            {
                full = char.ToUpperInvariant(full[0]) + "\\:" + full.Substring(2);
            }

            return full.Replace("'", "\\'");
        }

        private static async Task RunFfmpegAsync(string ffmpegExecutable, string args, Action<string> log, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken).ConfigureAwait(false);

                var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("FFmpeg lỗi: " + err);
                }
            }
        }

        private static async Task<string> ApplyProfileBrandOverlayAsync(
            string ffmpeg,
            string videoPath,
            string profileName,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var dir = Path.GetDirectoryName(videoPath) ?? string.Empty;
            var outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(videoPath) + "_branded.mp4");
            var overlay = PhilosophyProfileAssets.TryPickBrandOverlayImage(profileName);
            var hasLogo = !string.IsNullOrWhiteSpace(overlay) && File.Exists(overlay);
            var ctaText = EscapeFfmpegDrawText("Follow de nghe moi ngay");

            try
            {
                string args;
                if (hasLogo)
                {
                    args =
                        "-y -i \"" + videoPath + "\" -i \"" + overlay + "\" -filter_complex \"" +
                        "[0:v][1:v]overlay=W-w-40:40:format=auto[vl];" +
                        "[vl]drawtext=text='" + ctaText + "':x=(w-text_w)/2:y=h-th-48:fontsize=36:fontcolor=white:borderw=2:bordercolor=black@0.6[vout]\" " +
                        "-map \"[vout]\" -map 0:a? -c:v libx264 -preset fast -pix_fmt yuv420p -c:a copy \"" + outPath + "\"";
                }
                else
                {
                    args =
                        "-y -i \"" + videoPath + "\" -vf \"drawtext=text='" + ctaText +
                        "':x=(w-text_w)/2:y=h-th-48:fontsize=36:fontcolor=white:borderw=2:bordercolor=black@0.6\" " +
                        "-c:v libx264 -preset fast -pix_fmt yuv420p -c:a copy \"" + outPath + "\"";
                }

                await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
                if (File.Exists(outPath))
                {
                    log?.Invoke(hasLogo
                        ? "[Triết lý] Watermark logo + CTA dưới cùng → " + outPath
                        : "[Triết lý] CTA dưới cùng (không có logo.png) → " + outPath);
                    return outPath;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("[Triết lý] Overlay lỗi — giữ bản gốc: " + ex.Message);
            }

            return videoPath;
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
