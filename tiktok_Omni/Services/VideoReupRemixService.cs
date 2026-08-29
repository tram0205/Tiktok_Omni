using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Pipeline «reup»: Gemini hook → hook intro AI (ảnh mascot) → voiceover → body lách → concat.</summary>
    public sealed class VideoReupRemixService
    {
        private const string StageSourceMp4 = "source.mp4";
        private const string StageHookRawMp3 = "hook_raw.mp3";
        private static readonly VideoService ReupHookTtsService = new VideoService();

        /// <summary>Dịch vụ OCR nhận diện chữ trong video — inject từ ngoài để tái sử dụng engine.</summary>
        public VideoOcrService OcrService { get; set; }

        /// <summary>ElevenLabs đã cấu hình trong tab Cài đặt (TTS API Key + Endpoint).</summary>
        public static bool IsElevenLabsConfigured(AppSettings settings)
        {
            var apiKey = (settings?.TtsApiKey ?? string.Empty).Trim();
            var endpoint = (settings?.TtsEndpoint ?? string.Empty).Trim();
            return !string.IsNullOrEmpty(apiKey)
                   && !string.IsNullOrEmpty(endpoint)
                   && VideoService.IsElevenLabsEndpoint(endpoint);
        }

        /// <summary>SFX 3s chỉ dùng khi có file thật — tránh nhầm với voiceover ElevenLabs.</summary>
        public static bool ShouldUseVisualHookSfx(VideoReupRowItem row)
        {
            if (row == null || !row.UseVisualHookSfx)
            {
                return false;
            }

            var path = (row.VisualHookSfxPath ?? string.Empty).Trim();
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }


        public static string GetMusicLibraryDirectory(AppSettings settings) =>
            OmniAudioLibrary.GetPrimaryMusicDirectory(settings);

        public static void EnsureMusicLibraryDirectoryExists(AppSettings settings)
        {
            OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);
            Directory.CreateDirectory(GetMusicLibraryDirectory(settings));
        }

        /// <summary>Thư mục quét nhạc nền — Assets\Audio\Music.</summary>
        public static IReadOnlyList<string> GetMusicSearchDirectories(AppSettings settings) =>
            OmniAudioLibrary.GetMusicSearchDirectories(settings);

        public static List<string> ListMusicFileNames(AppSettings settings)
        {
            EnsureMusicLibraryDirectoryExists(settings);
            return OmniAudioLibrary.ListMusicFileNames(settings);
        }

        public static string ResolveMusicFilePath(string fileName, AppSettings settings)
        {
            var pick = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(pick) || VideoReupRowItem.IsNoMusicSelection(pick))
            {
                return string.Empty;
            }

            return OmniAudioLibrary.ResolveMusicFilePath(pick, settings);
        }

        /// <summary>Thư mục SFX chính: Assets\Audio\Sfx.</summary>
        public static string GetHookSfxLibraryDirectory(AppSettings settings) =>
            OmniAudioLibrary.GetPrimarySfxDirectory(settings);

        public static void EnsureHookSfxLibraryDirectoryExists(AppSettings settings) =>
            OmniAudioLibrary.EnsureSharedDirectoriesExist(settings);

        /// <summary>Liệt kê SFX — Assets\Audio\Sfx.</summary>
        public static List<string> ListHookSfxFileNames(AppSettings settings)
        {
            EnsureHookSfxLibraryDirectoryExists(settings);
            return OmniAudioLibrary.ListSfxFileNames(settings);
        }

        public static List<string> ListHookSfxFilePaths(AppSettings settings)
        {
            var names = ListHookSfxFileNames(settings);
            var paths = new List<string>();
            foreach (var name in names)
            {
                var path = ResolveHookSfxFilePath(settings, name);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        public static string ResolveHookSfxFilePath(AppSettings settings, string fileName)
        {
            var want = (fileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(want) || VideoReupRowItem.IsNoHookSfxSelection(want))
            {
                return null;
            }

            var path = OmniAudioLibrary.ResolveSfxFilePath(want, settings);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }

        /// <summary>URL có vẻ là link http(s) tuyệt đối — dùng trước khi gọi TikWM tải video.</summary>
        public static bool LooksLikeHttpVideoUrl(string url)
        {
            var s = (url ?? string.Empty).Trim();
            if (s.Length < 12)
            {
                return false;
            }

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Xóa MP4 thành phẩm cũ trước khi render lại cùng một dòng.</summary>
        public static bool TryDeletePreviousOutput(string outputPath, string profileName, Action<string> log)
        {
            var path = (outputPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                var outputRoot = Path.GetFullPath(ProfileScopedPaths.GetVideoReupOutputRoot(profileName));
                if (!fullPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke("[VideoReup] Bỏ qua xóa file ngoài thư mục Reup: " + Path.GetFileName(fullPath));
                    return false;
                }

                File.Delete(fullPath);
                log?.Invoke("[VideoReup] Đã xóa kết quả render trước: " + Path.GetFileName(fullPath));
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("[VideoReup] Không xóa được kết quả cũ: " + ex.Message);
                return false;
            }
        }

        /// <summary>Xóa cache stage / file đã tải / WAV hook khi đổi URL hoặc làm mới nguồn.</summary>
        public static void ClearReupCachedMediaPaths(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            row.ReupStageFolder = string.Empty;
            row.ReupDownloadedVideoPath = string.Empty;
            row.ReupHookAudioPath = string.Empty;
            row.HookAudioPath = string.Empty;
            row.ReupHookIntroImagePath = string.Empty;
            row.ReupHookIntroVideoPath = string.Empty;
            row.ReupHookIntroDurationSec = null;
            row.LastSourceVideoDurationSec = null;
            row.LastHookDurationUsedSec = null;
        }

        /// <summary>Xóa đường dẫn stage/source cũ trong draft nếu file/thư mục không còn trên đĩa.</summary>
        public static void SanitizeStaleReupCache(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            var downloaded = (row.ReupDownloadedVideoPath ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(downloaded) && !File.Exists(downloaded))
            {
                row.ReupDownloadedVideoPath = string.Empty;
            }

            var stage = (row.ReupStageFolder ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(stage))
            {
                if (!Directory.Exists(stage))
                {
                    row.ReupStageFolder = string.Empty;
                    row.ReupDownloadedVideoPath = string.Empty;
                }
                else
                {
                    var canonical = Path.Combine(stage, StageSourceMp4);
                    if (!File.Exists(canonical) && string.Equals(downloaded, canonical, StringComparison.OrdinalIgnoreCase))
                    {
                        row.ReupDownloadedVideoPath = string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Nếu <see cref="VideoReupRowItem.HookStyleKey"/> chưa được chọn, tự gán ngẫu nhiên một style
        /// từ catalog (nếu catalog có ít nhất một clip).
        /// Gọi trước render để hàng mới import cũng được gán style tự động.
        /// </summary>
        public static void SeedHookStyleKeyIfEmpty(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(row.HookStyleKey))
            {
                return;
            }

            // Chọn ngẫu nhiên một style có ít nhất một clip (ưu tiên clip của profile)
            var profileName = (row.ProfileName ?? string.Empty).Trim();
            var rng = new Random();
            var keys = HookStyleCatalog.AllStyleKeys
                .OrderBy(_ => rng.Next())
                .ToArray();

            foreach (var key in keys)
            {
                var clips = HookStyleCatalog.GetClipsForStyle(key, settings, profileName);
                if (clips.Length > 0)
                {
                    row.HookStyleKey = key;
                    return;
                }
            }

            // Catalog rỗng → giữ nguyên (sẽ fallback sang Gemini khi render)
        }

        private static string GetOrCreateStageFolder(VideoReupRowItem row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (!string.IsNullOrWhiteSpace(row.ReupStageFolder) && Directory.Exists(row.ReupStageFolder))
            {
                return row.ReupStageFolder;
            }

            var safe = VideoReupCaptionService.SanitizeFileNameFragment(row.ProductName);
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = "video";
            }

            var h = (row.VideoUrl ?? string.Empty).GetHashCode().ToString("X8", CultureInfo.InvariantCulture);
            var stagesRoot = ProfileScopedPaths.GetVideoReupStagesRoot(row.ProfileName);
            var dir = Path.Combine(stagesRoot, safe + "_" + h);
            Directory.CreateDirectory(dir);
            row.ReupStageFolder = dir;
            return dir;
        }

        /// <summary>Gemini viết hook (và gợi ý nhạc) — chỉ cần AI Key.</summary>
        public static bool TryValidateHookGeminiStep(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                errorMessage = "Cần AI API Key (Cài đặt) để Gemini viết hook.";
                return false;
            }

            var musicDir = GetMusicLibraryDirectory(settings);
            EnsureMusicLibraryDirectoryExists(settings);
            var mp3Count = Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly).Length;
            var needsMusicLibrary = row.ReupAudioMode == VideoReupAudioMode.AffiliateBed;
            if (needsMusicLibrary && mp3Count == 0)
            {
                errorMessage =
                    "Cần ít nhất một file .mp3 trong Assets\\Audio\\Music (chế độ Affiliate — để Gemini gợi ý tên file):\r\n" +
                    musicDir +
                    "\r\nGợi ý: tab Video reup → «Thư mục nhạc».";
                return false;
            }

            return true;
        }

        /// <summary>Gemini viết script thuyết minh — cần chế độ Hook + Thuyết minh và AI Key.</summary>
        public static bool TryValidateNarrationScriptGeminiStep(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            if (row.ReupAudioMode != VideoReupAudioMode.NarrationScript)
            {
                errorMessage = "Chọn chế độ «Hook + Thuyết minh» ở cột «Chế độ» trước khi tạo script.";
                return false;
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                errorMessage = "Cần AI API Key (Cài đặt) để Gemini viết script thuyết minh.";
                return false;
            }

            return true;
        }

        /// <summary>Phân tích video (nếu cần) rồi sinh script thuyết minh bằng Gemini.</summary>
        public async Task GenerateNarrationScriptForRowAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            bool forceRegenerate,
            CancellationToken cancellationToken)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (!TryValidateNarrationScriptGeminiStep(row, settings, out var err))
            {
                throw new InvalidOperationException(err);
            }

            await EnsureVideoAnalysisContextAsync(
                row,
                settings,
                affiliateHunter,
                log,
                cancellationToken).ConfigureAwait(false);

            if (!forceRegenerate && !string.IsNullOrWhiteSpace((row.ReupNarrationScript ?? string.Empty).Trim()))
            {
                return;
            }

            row.ReupNarrationScript = await GenerateNarrationScriptAsync(
                row,
                settings,
                gemini,
                log,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Voiceover đọc hook — cần FFmpeg, URL video, câu hook không rỗng.</summary>
        public static bool TryValidateVoiceoverAudioStep(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.VideoUrl))
            {
                errorMessage = "Dòng chưa có URL video.";
                return false;
            }

            if (string.IsNullOrWhiteSpace((row.ReupHookDraft ?? string.Empty).Trim())
                && !VideoReupStyleVariants.HasResolvableHook(row))
            {
                errorMessage = "Chưa có câu hook — hãy tạo/chỉnh hook trước.";
                return false;
            }

            if (!IsElevenLabsConfigured(settings)
                && string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                errorMessage =
                    "Cần ElevenLabs trong Cài đặt (TTS API Key + Endpoint elevenlabs.io) hoặc AI API Key (Google TTS dự phòng).";
                return false;
            }

            if (!TryValidateFfmpegToolkit(settings, out errorMessage))
            {
                return false;
            }

            return true;
        }

        /// <summary>Tab Video reup có thể bấm Render khi FFmpeg + lưu trữ + (Gemini hoặc ElevenLabs).</summary>
        public static bool IsTabRenderReady(AppSettings settings, bool ffmpegOk, bool storageOk, out string blockerMessage)
        {
            blockerMessage = string.Empty;
            if (!ffmpegOk)
            {
                blockerMessage = "Chưa có FFmpeg — bấm «Tải FFmpeg» trong Cài đặt.";
                return false;
            }

            if (!storageOk)
            {
                blockerMessage = "Không ghi được thư mục lưu trữ — kiểm tra Storage trong Cài đặt.";
                return false;
            }

            if (!TryValidateFfmpegToolkit(settings, out var ffErr))
            {
                blockerMessage = ffErr;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                return true;
            }

            if (IsElevenLabsConfigured(settings))
            {
                return true;
            }

            // Nếu catalog có clip thì không cần Gemini key để render hook intro
            if (HookStyleCatalog.CatalogHasAnyClips(settings))
            {
                return true;
            }

            blockerMessage = "Cần AI API Key (Gemini) hoặc ElevenLabs (TTS API Key + Endpoint elevenlabs.io) trong Cài đặt, hoặc cấu hình thư mục video stock «HookClips».";
            return false;
        }

        /// <summary>Ghép video cuối — cần video nguồn đã tải, WAV hook, nhạc đã chọn, FFmpeg.</summary>
        public static bool TryValidateFinalRenderStep(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.VideoUrl))
            {
                errorMessage = "Dòng chưa có URL video.";
                return false;
            }

            if (!TryValidateFfmpegToolkit(settings, out errorMessage))
            {
                return false;
            }

            // Mascot + Gemini API key chỉ cần khi không có stock clip
            var profileNameForStock = (row?.ProfileName ?? string.Empty).Trim();
            var hasStockClip = HookStyleCatalog.CatalogHasAnyClips(settings, profileNameForStock)
                               || !string.IsNullOrEmpty((row.HookStyleKey ?? string.Empty).Trim());
            if (!hasStockClip)
            {
                if (!TryValidateHookIntroMascotStep(row, out errorMessage))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace((settings?.AiApiKey ?? string.Empty).Trim()))
                {
                    errorMessage = "Cần AI API Key (Gemini) để sinh ảnh hook intro (hoặc cấu hình thư mục video stock «HookClips»).";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace((row.ReupDownloadedVideoPath ?? string.Empty).Trim()) || !File.Exists(row.ReupDownloadedVideoPath))
            {
                errorMessage = "Chưa có video nguồn đã tải — nhập URL (rời ô để tải), hoặc đợi tải xong sau «Nhập từ Săn Affiliate», hoặc chạy «Voiceover đọc hook» (cũng tải video).";
                return false;
            }

            if (ShouldUseVisualHookSfx(row))
            {
                var sfx = (row.VisualHookSfxPath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(sfx) || !File.Exists(sfx))
                {
                    errorMessage = "Hook SFX 3s: chọn file âm thanh hợp lệ trong panel «Hook SFX 3 giây».";
                    return false;
                }
            }
            else if (string.IsNullOrWhiteSpace((row.ReupHookAudioPath ?? string.Empty).Trim()) || !File.Exists(row.ReupHookAudioPath))
            {
                errorMessage = "Chưa có file âm thanh hook (WAV) — chạy «Voiceover đọc hook» hoặc bật Hook SFX 3s.";
                return false;
            }

            var isFilm = row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal;
            var isNarration = row.ReupAudioMode == VideoReupAudioMode.NarrationScript;
            if (isFilm)
            {
                var ffmpegExe = ResolveFfmpegPath(settings);
                var audioSource = ResolveFilmAudioSourcePath(row);
                if (!MediaFileHasAudioStream(ffmpegExe, audioSource))
                {
                    errorMessage = "Chế độ «Phim» cần video nguồn có tiếng (ít nhất một track âm thanh) — file hiện tại không có audio.";
                    return false;
                }

                return true;
            }

            if (isNarration)
            {
                if (string.IsNullOrWhiteSpace((row.ReupNarrationAudioPath ?? string.Empty).Trim()) || !File.Exists(row.ReupNarrationAudioPath))
                {
                    errorMessage = "Chưa có audio thuyết minh (WAV) — «Render & Đóng gói» sẽ TTS script ở bước 3b; nếu vẫn lỗi, kiểm tra script + ElevenLabs.";
                    return false;
                }

                var musicName = (row.ReupSelectedMusicFile ?? string.Empty).Trim();
                if (!VideoReupRowItem.HasMusicBedSelected(musicName))
                {
                    return true;
                }

                var musicDirN = GetMusicLibraryDirectory(settings);
                EnsureMusicLibraryDirectoryExists(settings);
                var musicFilesN = Directory.GetFiles(musicDirN, "*.mp3", SearchOption.TopDirectoryOnly);
                if (musicFilesN.Length == 0)
                {
                    errorMessage = "Đã chọn nhạc nền nhưng thư mục nhạc trống:\r\n" + musicDirN;
                    return false;
                }

                var namesN = musicFilesN.Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                var pickN = PickMusicPath(musicFilesN, musicName, namesN, _ => { });
                if (string.IsNullOrWhiteSpace(pickN) || !File.Exists(pickN))
                {
                    errorMessage = "File nhạc đã chọn không tồn tại trong thư mục nhạc Video reup:\r\n" + musicDirN;
                    return false;
                }

                return true;
            }

            if (!VideoReupRowItem.HasMusicBedSelected(row.ReupSelectedMusicFile))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace((row.ReupSelectedMusicFile ?? string.Empty).Trim()))
            {
                errorMessage = "Hãy chọn nhạc nền (.mp3) hoặc «(Không có nhạc)» trong cột Nhạc nền.";
                return false;
            }

            var musicDir = GetMusicLibraryDirectory(settings);
            EnsureMusicLibraryDirectoryExists(settings);
            var musicFiles = Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly);
            if (musicFiles.Length == 0)
            {
                errorMessage = "Cần ít nhất một file .mp3 trong thư mục nhạc Video reup:\r\n" + musicDir;
                return false;
            }

            var names = musicFiles.Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var pick = PickMusicPath(musicFiles, row.ReupSelectedMusicFile, names, _ => { });
            if (string.IsNullOrWhiteSpace(pick) || !File.Exists(pick))
            {
                errorMessage = "File nhạc đã chọn không tồn tại trong thư mục nhạc Video reup:\r\n" + musicDir;
                return false;
            }

            if (VideoReupRowItem.HasHookSfxSelected(row.ReupSelectedHookSfxFile)
                && !ShouldUseVisualHookSfx(row))
            {
                var sfxPath = ResolveHookSfxFilePath(settings, row.ReupSelectedHookSfxFile);
                if (string.IsNullOrWhiteSpace(sfxPath) || !File.Exists(sfxPath))
                {
                    errorMessage = "File SFX hook đã chọn không tồn tại trong kho SFX (Assets\\Audio\\Sfx):\r\n"
                                   + GetHookSfxLibraryDirectory(settings);
                    return false;
                }
            }

            return true;
        }

        /// <summary>Kiểm tra trước khi render cuối (tương thích tên cũ).</summary>
        public static bool TryValidateRemixPrerequisites(VideoReupRowItem row, AppSettings settings, out string errorMessage)
        {
            return TryValidateFinalRenderStep(row, settings, out errorMessage);
        }

        /// <summary>ffmpeg + ffprobe phải chạy được (đọc thời lượng, render).</summary>
        public static bool TryValidateFfmpegToolkit(AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!FfmpegToolkitService.TryResolve(settings, out var toolkit, out errorMessage))
            {
                return false;
            }

            if (!FfmpegToolkitService.CanRunMediaTool(toolkit.FfprobeExe))
            {
                errorMessage = "Không chạy được ffprobe: " + toolkit.FfprobeExe;
                return false;
            }

            if (!FfmpegToolkitService.CanRunMediaTool(toolkit.FfmpegExe))
            {
                errorMessage = "Không chạy được ffmpeg: " + toolkit.FfmpegExe;
                return false;
            }

            return true;
        }

        /// <summary>Tự tải/cài FFmpeg nếu chưa có, cập nhật <see cref="AppSettings.FfmpegPath"/> khi thành công.</summary>
        public static async Task<string> EnsureFfmpegToolkitAsync(
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var path = await FfmpegToolkitService.EnsureAvailableAsync(settings, log, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(path))
            {
                settings.FfmpegPath = path;
            }

            return path;
        }

        /// <summary>Chẩn đoán nhanh — dòng đang kẹt ở bước nào (hiển thị log / cột Lỗi).</summary>
        public static string DescribePipelineBlockers(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return "Chưa chọn dòng trong bảng.";
            }

            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace((row.VideoUrl ?? string.Empty).Trim()))
            {
                lines.Add("① URL video: trống — dán link TikTok vào cột «URL video» hoặc ô bên dưới.");
            }
            else if (!LooksLikeHttpVideoUrl(row.VideoUrl))
            {
                lines.Add("① URL video: không hợp lệ (cần http/https).");
            }

            if (!TryValidateFfmpegToolkit(settings, out var ffErr))
            {
                lines.Add("② FFmpeg/ffprobe: " + ffErr.Replace("\r\n", " "));
            }
            else if (string.IsNullOrWhiteSpace((row.ReupDownloadedVideoPath ?? string.Empty).Trim()) ||
                     !File.Exists(row.ReupDownloadedVideoPath))
            {
                lines.Add("② Tải nguồn: chưa có source.mp4 — đợi tải xong hoặc bấm «Render & Đóng gói» (tự tải).");
            }

            // Mascot / Gemini chỉ cần khi không dùng stock clip
            var profileForChecklist = (row?.ProfileName ?? string.Empty).Trim();
            var hasStockForChecklist = HookStyleCatalog.CatalogHasAnyClips(settings, profileForChecklist)
                                       || !string.IsNullOrEmpty((row.HookStyleKey ?? string.Empty).Trim());
            if (!hasStockForChecklist)
            {
                if (!TryValidateHookIntroMascotStep(row, out var mascotErr))
                {
                    lines.Add("②b Ảnh profile: " + mascotErr.Replace("\r\n", " "));
                }
                else if (settings == null || string.IsNullOrWhiteSpace((settings.AiApiKey ?? string.Empty).Trim()))
                {
                    lines.Add("②c Gemini image: cần AI API Key để sinh ảnh hook intro (hoặc cấu hình stock «HookClips»).");
                }
            }

            if (ShouldUseVisualHookSfx(row))
            {
                var sfx = (row.VisualHookSfxPath ?? string.Empty).Trim();
                lines.Add("③ Hook SFX 3s: «" + Path.GetFileName(sfx) + "» — sẽ chèn vào đầu video.");
            }
            else if (row.UseVisualHookSfx)
            {
                lines.Add("③ Hook: «SFX 3s» bật nhưng chưa có file — sẽ dùng ElevenLabs voiceover hook.");
            }
            else if (!VideoReupStyleVariants.HasResolvableHook(row))
            {
                if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    lines.Add("③ Hook: trống — gõ câu hook (4–7s) vào cột «Hook» HOẶC bật Hook SFX 3s HOẶC cấu hình AI API Key.");
                }
                else if (row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
                {
                    lines.Add("③ Hook: trống — bấm «Gemini: tạo hook» hoặc gõ tay vào cột «Hook» (4–7s).");
                }
                else
                {
                    var musicDir = GetMusicLibraryDirectory(settings);
                    if (Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly).Length == 0)
                    {
                        lines.Add("③ Hook: trống — cần ≥1 file .mp3 trong thư mục nhạc để Gemini gợi ý hook/nhạc: " + musicDir);
                    }
                    else
                    {
                        lines.Add("③ Hook: trống — bấm «Gemini: tạo hook» hoặc «Render & Đóng gói» (tự gọi Gemini).");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace((row.ReupHookAudioPath ?? string.Empty).Trim()) ||
                !File.Exists(row.ReupHookAudioPath))
            {
                lines.Add("④ Âm hook: chưa có WAV — cần «Voiceover: đọc hook» hoặc «Render & Đóng gói» (tự chạy voiceover).");
            }
            else if (VideoReupRowItem.HasHookSfxSelected(row.ReupSelectedHookSfxFile)
                     && !ShouldUseVisualHookSfx(row))
            {
                var sfxPick = ResolveHookSfxFilePath(settings, row.ReupSelectedHookSfxFile);
                if (string.IsNullOrWhiteSpace(sfxPick))
                {
                    lines.Add("④b SFX hook: «" + row.ReupSelectedHookSfxFile + "» không tìm thấy — thêm .mp3/.wav vào Assets\\Audio\\Sfx.");
                }
                else
                {
                    lines.Add("④b SFX hook: «" + row.ReupSelectedHookSfxFile + "» (~"
                              + (ReupVideoSpec.HookOverlaySfxVolume * 100d).ToString("0", CultureInfo.InvariantCulture)
                              + "% overlay cùng giọng hook).");
                }
            }

            if (row.ReupAudioMode == VideoReupAudioMode.NarrationScript)
            {
                if (!VideoReupStyleVariants.HasResolvableScript(row))
                {
                    lines.Add("⑤ Script: trống — bấm «Tạo script» hoặc «Gemini: tạo hook» (sinh kèm script) hoặc để render tự sinh.");
                }

                if (string.IsNullOrWhiteSpace((row.ReupNarrationAudioPath ?? string.Empty).Trim()) ||
                    !File.Exists(row.ReupNarrationAudioPath))
                {
                    lines.Add("⑥ Âm thuyết minh: chưa có — «Render & Đóng gói» sẽ TTS script.");
                }

                if (VideoReupRowItem.IsNoMusicSelection((row.ReupSelectedMusicFile ?? string.Empty).Trim()))
                {
                    lines.Add("⑦ Nhạc: không dùng nhạc nền.");
                }
                else if (string.IsNullOrWhiteSpace((row.ReupSelectedMusicFile ?? string.Empty).Trim()))
                {
                    lines.Add("⑦ Nhạc (tùy chọn): chưa chọn — có thể chọn «(Không có nhạc)» hoặc file .mp3.");
                }
            }
            else if (row.ReupAudioMode != VideoReupAudioMode.FilmKeepOriginal)
            {
                if (VideoReupRowItem.IsNoMusicSelection((row.ReupSelectedMusicFile ?? string.Empty).Trim()))
                {
                    lines.Add("⑤ Nhạc: không dùng nhạc nền.");
                }
                else if (string.IsNullOrWhiteSpace((row.ReupSelectedMusicFile ?? string.Empty).Trim()))
                {
                    lines.Add("⑤ Nhạc nền: chưa chọn — chọn file .mp3 hoặc «(Không có nhạc)».");
                }
            }

            if (lines.Count == 0)
            {
                return "Sẵn sàng ghép MP4 — bấm «Render & Đóng gói».";
            }

            return string.Join("\r\n", lines);
        }

        public static bool TryResolveFfprobeExecutable(AppSettings settings, out string ffprobePath, out string errorMessage)
        {
            ffprobePath = string.Empty;
            errorMessage = string.Empty;
            if (!FfmpegToolkitService.TryResolve(settings, out var toolkit, out errorMessage))
            {
                return false;
            }

            ffprobePath = toolkit.FfprobeExe;
            return true;
        }
        private static readonly object RngLock = new object();
        private static readonly Random Rng = new Random();

        private static int NextCrf()
        {
            lock (RngLock)
            {
                return Rng.Next(19, 24);
            }
        }

        private static string RandomMetaToken(string prefix)
        {
            lock (RngLock)
            {
                return prefix + Guid.NewGuid().ToString("N");
            }
        }

        public static double ResolveHookMinSeconds(VideoReupRowItem row)
        {
            return ShouldUseVisualHookSfx(row)
                ? VisualHookService.HookDurationSeconds
                : ReupVideoSpec.HookMinSec;
        }

        /// <summary>Hook intro AI bắt buộc có ảnh mascot trong AvatarVault/{Profile}.</summary>
        public static bool TryValidateHookIntroMascotStep(VideoReupRowItem row, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (row == null)
            {
                errorMessage = "Không có dòng dữ liệu.";
                return false;
            }

            var profile = (row.ProfileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(profile))
            {
                errorMessage = "Dòng chưa gán profile — chọn nick trước khi render.";
                return false;
            }

            return AvatarIdentityPackStore.TryGetMascotImagePath(profile, out _, out errorMessage);
        }

        /// <summary>Tham số video cố định theo yêu cầu sản phẩm.</summary>
        private static class ReupVideoSpec
        {
            /// <summary>Cắt nhẹ đầu video (giây) — tránh watermark/frame lỗi, không rút ngắn clip quá nhiều.</summary>
            public const double TrimHeadSeconds = 0.1d;
            public const double TrimTailSeconds = 0.1d;
            public const double HookMinSec = 4d;
            public const double HookMaxSec = 7d;
            public const double MusicBedVolume = 0.22d;
            /// <summary>Nhạc nền chế độ thuyết minh (~10% — lót dưới giọng, không lấn át).</summary>
            public const double NarrationMusicBedVolume = 0.10d;
            /// <summary>SFX hook overlay (~45% — nhỏ hơn giọng hook 100%).</summary>
            public const double HookOverlaySfxVolume = 0.45d;
            public const double MinMusicTailSec = 0.5d;

            /// <summary>Bật tốc độ ~+5% (setpts). Mặc định tắt để khớp mô tả «chỉ cắt/lật/màu/tiếng».</summary>
            public const bool ApplySpeedChange = false;

            public const double SpeedFactor = 1.05d;
        }

        /// <summary>Một lần gọi Gemini: hook + script thuyết minh → ghi <see cref="VideoReupRowItem.ReupHookDraft"/> và <see cref="VideoReupRowItem.ReupNarrationScript"/>. Script chỉ dùng khi render ở chế độ «Hook + Thuyết minh».</summary>
        public async Task GenerateHookAndNarrationBundleAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            bool forceRegenerate,
            CancellationToken cancellationToken)
        {
            if (!TryValidateHookGeminiStep(row, settings, out var err))
            {
                throw new InvalidOperationException(err);
            }

            await EnsureVideoAnalysisContextAsync(
                row,
                settings,
                affiliateHunter,
                log,
                cancellationToken).ConfigureAwait(false);

            var needHook = forceRegenerate
                           || !VideoReupStyleVariants.HasResolvableHook(row);
            var needScript = forceRegenerate
                             || (row.ReupAudioMode == VideoReupAudioMode.NarrationScript
                                 && !VideoReupStyleVariants.HasResolvableScript(row));

            if (!needHook && !needScript)
            {
                return;
            }

            if (needHook)
            {
                var includeScripts = row.ReupAudioMode == VideoReupAudioMode.NarrationScript;
                if (includeScripts)
                {
                    log?.Invoke("[VideoReup] Chế độ «Hook + Thuyết minh» — 1 lần gọi Gemini: hook + script (5 phong cách).");
                }
                else
                {
                    log?.Invoke("[VideoReup] Chế độ «" + row.ReupMode + "» — 1 lần gọi Gemini: chỉ hook (5 phong cách).");
                }

                ReupHookNarrationBundle bundle;
                try
                {
                    bundle = await GenerateHookAndNarrationBundleFromGeminiAsync(
                        row,
                        settings,
                        gemini,
                        log,
                        includeScripts,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (IsGeminiQuotaError(ex))
                {
                    if (VideoReupStyleVariants.HasHookVariants(row))
                    {
                        log?.Invoke("[VideoReup] " + FormatGeminiQuotaShortMessage());
                        log?.Invoke("[VideoReup] Giữ nguyên 5 phong cách hook/script đã có — bấm cột Hook để chọn.");
                        return;
                    }

                    log?.Invoke("[VideoReup] " + FormatGeminiQuotaShortMessage());
                    log?.Invoke("[VideoReup] Dùng mẫu local — mở bảng 5 phong cách để chỉnh tay trước khi render.");
                    bundle = BuildLocalStyleFallbackBundle(row, includeScripts);
                }

                if (includeScripts)
                {
                    VideoReupStyleVariants.SetVariants(row, bundle.HooksByStyle, bundle.ScriptsByStyle);
                    log?.Invoke("[VideoReup] Gemini: đã sinh hook + script cho 5 phong cách.");
                }
                else
                {
                    VideoReupStyleVariants.SetHookVariants(row, bundle.HooksByStyle);
                    log?.Invoke("[VideoReup] Gemini: đã sinh hook cho 5 phong cách (chế độ không cần script).");
                }

                foreach (var key in HookStyleCatalog.AllStyleKeys)
                {
                    if (VideoReupStyleVariants.TryGetVariant(bundle.HooksByStyle, key, out var hookPreview))
                    {
                        log?.Invoke("  • " + HookStyleCatalog.GetDisplayName(key) + " hook: "
                                    + Trim(hookPreview, 72));
                    }
                }

                return;
            }

            if (needScript)
            {
                row.ReupNarrationScript = await GenerateNarrationScriptAsync(
                    row,
                    settings,
                    gemini,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task GenerateHookAndSuggestMusicAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            CancellationToken cancellationToken,
            bool forceRegenerate = false)
        {
            if (!TryValidateHookGeminiStep(row, settings, out var err))
            {
                throw new InvalidOperationException(err);
            }

            await GenerateHookAndNarrationBundleAsync(
                row,
                settings,
                gemini,
                affiliateHunter,
                log,
                forceRegenerate,
                cancellationToken).ConfigureAwait(false);

            EnsureMusicLibraryDirectoryExists(settings);
            List<string> musicNames;
            if (row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
            {
                musicNames = new List<string>();
            }
            else
            {
                musicNames = VideoReupRemixService.ListMusicFileNames(settings);
            }

            var existingMusic = (row.ReupSuggestedMusicFile ?? string.Empty).Trim();
            if (!forceRegenerate
                && !string.IsNullOrEmpty(existingMusic)
                && (musicNames.Count == 0
                    || musicNames.Any(n => string.Equals(n, existingMusic, StringComparison.OrdinalIgnoreCase))))
            {
                log?.Invoke("[VideoReup] Nhạc: giữ gợi ý «" + existingMusic + "» — bỏ qua Gemini.");
                return;
            }

            if (row.ReupAudioMode == VideoReupAudioMode.AffiliateBed)
            {
                await TrySuggestMusicFileSafeAsync(row, musicNames, settings, gemini, log, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (row.ReupAudioMode == VideoReupAudioMode.NarrationScript)
            {
                if (musicNames.Count == 0)
                {
                    log?.Invoke("[VideoReup] Thuyết minh: chưa có .mp3 — có thể chọn «(Không có nhạc)» trên lưới.");
                }
                else
                {
                    log?.Invoke("[VideoReup] Thuyết minh: gợi ý nhạc nền (~10% volume khi render)…");
                    await TrySuggestMusicFileSafeAsync(row, musicNames, settings, gemini, log, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else if (row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal)
            {
                row.ReupSuggestedMusicFile = string.Empty;
            }
        }

        private static async Task TrySuggestMusicFileSafeAsync(
            VideoReupRowItem row,
            List<string> musicNames,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            try
            {
                row.ReupSuggestedMusicFile = await SuggestMusicFileAsync(
                    row,
                    musicNames,
                    settings,
                    gemini,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsGeminiQuotaError(ex))
            {
                log?.Invoke("[VideoReup] Nhạc: bỏ qua gợi ý Gemini — hết quota API (429). Chọn file .mp3 tay trên lưới.");
            }
            catch (Exception ex)
            {
                log?.Invoke("[VideoReup] Nhạc: không gợi ý được — " + ex.Message);
            }
        }

        public async Task GenerateHashtagsAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            CancellationToken cancellationToken,
            bool forceRegenerate = false)
        {
            if (row == null || gemini == null)
            {
                return;
            }

            if (!forceRegenerate && !string.IsNullOrWhiteSpace((row.Hashtags ?? string.Empty).Trim()))
            {
                log?.Invoke("[VideoReup] Hashtag: giữ hashtag cũ — bỏ qua Gemini.");
                return;
            }

            var keyword = (row.SourceKeyword ?? string.Empty).Trim();
            var product = VideoReupProductLabel.GetShortLabel(row.ProductName ?? string.Empty);
            var hook = VideoReupProductLabel.NormalizeHookText(
                (row.ReupHookDraft ?? string.Empty).Trim(),
                row.ProductName ?? string.Empty,
                product);

            if (string.IsNullOrWhiteSpace(keyword) && string.IsNullOrWhiteSpace(product))
            {
                log?.Invoke("[VideoReup] Hashtag: bỏ qua — chưa có từ khóa săn hoặc tên sản phẩm.");
                return;
            }

            var prompt = BuildVideoReupHashtagPrompt(keyword, product, hook);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                log?.Invoke("[VideoReup] Hashtag: từ khóa săn «" + keyword + "» — Gemini tạo tag theo intent tìm kiếm…");
            }
            else
            {
                log?.Invoke("[VideoReup] Hashtag: không có từ khóa săn — dùng sản phẩm «" + product + "».");
            }

            log?.Invoke("[VideoReup] Gemini: đang tạo hashtag…");
            try
            {
                var raw = await gemini.GenerateScriptAsync(
                    prompt,
                    settings.AiProvider,
                    settings.AiApiKey,
                    settings.AiModel,
                    cancellationToken).ConfigureAwait(false);

                var hashtags = NormalizeGeminiHashtagLine(raw);
                if (!string.IsNullOrWhiteSpace(hashtags))
                {
                    row.Hashtags = hashtags;
                    log?.Invoke("[VideoReup] Hashtag: " + hashtags);
                }
                else
                {
                    log?.Invoke("[VideoReup] Hashtag: Gemini trả về rỗng.");
                }
            }
            catch (Exception ex) when (IsGeminiQuotaError(ex))
            {
                log?.Invoke("[VideoReup] Hashtag: bỏ qua Gemini — hết quota API (429).");
            }
            catch (Exception ex)
            {
                log?.Invoke("[VideoReup] Hashtag: lỗi — " + ex.Message);
            }
        }

        private static string BuildVideoReupHashtagPrompt(string sourceKeyword, string shortProduct, string hook)
        {
            var sb = new StringBuilder();
            sb.Append("Bạn là chuyên gia TikTok SEO Việt Nam. Tạo 8–12 hashtag cho video reup/affiliate.\n\n");

            if (!string.IsNullOrWhiteSpace(sourceKeyword))
            {
                sb.Append("TỪ KHÓA SĂN (ưu tiên cao nhất — intent người mua hay tìm): «")
                    .Append(sourceKeyword.Trim())
                    .Append("».\n");
            }

            if (!string.IsNullOrWhiteSpace(shortProduct)
                && !string.Equals(shortProduct, "sản phẩm này", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("Sản phẩm / ngách: «").Append(shortProduct.Trim()).Append("».\n");
            }

            if (!string.IsNullOrWhiteSpace(hook))
            {
                sb.Append("Hook video (bổ sung ngữ cảnh): «").Append(hook.Trim()).Append("».\n");
            }

            sb.Append(
                "\nYêu cầu:\n" +
                "- Hashtag phải gần với TỪ KHÓA SĂN — mô phỏng cụm khách hay gõ/tìm trên TikTok (long-tail, sát nhu cầu mua/xem).\n" +
                "- ~60% tag sát từ khóa & biến thể tìm kiếm, ~30% tag ngách liên quan, tối đa 1–2 tag reach rộng (#fyp hoặc #xuhuong).\n" +
                "- KHÔNG dùng tag chung chung không liên quan (#learnontiktok, #goclamdep, #thoitrangnu… nếu không khớp từ khóa).\n" +
                "- Mỗi hashtag bắt đầu #, viết liền KHÔNG DẤU tiếng Việt, không khoảng trắng trong một tag.\n" +
                "- Trả về CHỈ các hashtag trên MỘT dòng, cách nhau bằng dấu cách — không markdown, không giải thích, không đánh số.");

            return sb.ToString();
        }

        private static string NormalizeGeminiHashtagLine(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var end = line.LastIndexOf("```", StringComparison.Ordinal);
                if (end > 3)
                {
                    line = line.Substring(3, end - 3).Trim();
                }
            }

            line = line.Replace("\r\n", " ").Replace('\n', ' ').Replace('\t', ' ');
            while (line.Contains("  "))
            {
                line = line.Replace("  ", " ");
            }

            return line.Trim();
        }

        public static bool IsGeminiQuotaError(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                var msg = cur.Message ?? string.Empty;
                if (msg.IndexOf("429", StringComparison.Ordinal) >= 0
                    || msg.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static string FormatGeminiQuotaUserMessage(Exception ex)
        {
            return FormatGeminiQuotaShortMessage()
                   + "\r\n\r\n"
                   + Trim(ex?.Message ?? string.Empty, 280);
        }

        public static string FormatGeminiQuotaShortMessage()
        {
            return "Hết quota Gemini API (429) — free tier thường ~20 lần/ngày/model. "
                   + "Đợi reset, đổi model trong Cài đặt, hoặc bật billing Google AI Studio.";
        }

        private static ReupHookNarrationBundle BuildLocalStyleFallbackBundle(VideoReupRowItem row, bool includeScripts = true)
        {
            var rawProduct = (row?.ProductName ?? string.Empty).Trim();
            var product = VideoReupProductLabel.GetShortLabel(rawProduct);

            var hooks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                hooks[key] = GetLocalHookForStyle(key, product);
                if (includeScripts)
                {
                    scripts[key] = DefaultNarrationScriptFallbackForStyle(key, product);
                }
            }

            return new ReupHookNarrationBundle { HooksByStyle = hooks, ScriptsByStyle = scripts };
        }

        /// <summary>ElevenLabs đọc script thuyết minh (giọng kể chuyện) → WAV khớp phần sau hook.</summary>
        public async Task BuildNarrationVoiceoverAsync(
            VideoReupRowItem row,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (row == null || row.ReupAudioMode != VideoReupAudioMode.NarrationScript)
            {
                return;
            }

            var script = (row.ReupNarrationScript ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(script))
            {
                VideoReupStyleVariants.ApplyActiveSelections(row, settings, log);
                script = (row.ReupNarrationScript ?? string.Empty).Trim();
            }

            if (string.IsNullOrEmpty(script))
            {
                throw new InvalidOperationException("Chưa có script thuyết minh — bấm «Tạo script» trên toolbar hoặc render (Gemini sẽ sinh tự động).");
            }

            var ffmpeg = ResolveFfmpegPath(settings);
            var stage = row.ReupStageFolder;
            if (string.IsNullOrWhiteSpace(stage))
            {
                throw new InvalidOperationException("Chưa có thư mục stage — tải video trước.");
            }

            if (VideoReupRemixService.IsElevenLabsConfigured(settings))
            {
                script = await VietnameseTtsTextNormalizer.PrepareNarrationForElevenLabsAsync(
                    script,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                script = VietnameseTtsTextNormalizer.NormalizePunctuation(script);
            }

            row.ReupNarrationScript = script;

            // Lưu raw MP3 vào stage với tên cố định để render dùng lại (tính slowdown video)
            Directory.CreateDirectory(stage);
            var narrMp3 = Path.Combine(stage, ReupStageCacheHelper.NarrationRawMp3);
            var narrStyleKey = (row.SelectedHookStyleKey ?? row.HookStyleKey ?? string.Empty).Trim();
            var narrSynthText = ReupElevenLabsVoiceHelper.PrepareTextForSynthesis(
                script,
                settings,
                emphaticHook: false,
                narrStyleKey,
                log);
            var narrTtsCacheKey = ReupStageCacheHelper.BuildElevenLabsTtsCacheKey(
                narrSynthText,
                settings,
                emphaticHook: false,
                narrStyleKey);

            if (ReupStageCacheHelper.TryReuseCachedArtifacts(
                    stage,
                    ReupStageCacheHelper.NarrationTtsKeyFile,
                    narrTtsCacheKey,
                    narrMp3))
            {
                log?.Invoke("[VideoReup] Thuyết minh TTS: dùng cache " + ReupStageCacheHelper.NarrationRawMp3 + " (bỏ qua ElevenLabs).");
            }
            else
            {
                log?.Invoke("[VideoReup] Thuyết minh: ElevenLabs giọng kể chuyện (khác hook)…");
                await BuildVoiceoverMp3Async(
                    script,
                    narrMp3,
                    settings,
                    log,
                    cancellationToken,
                    emphaticHook: false,
                    hookStyleKey: narrStyleKey).ConfigureAwait(false);
                ReupStageCacheHelper.WriteCacheKey(stage, ReupStageCacheHelper.NarrationTtsKeyFile, narrTtsCacheKey);
            }

            // Đo thời lượng TTS tự nhiên (trước khi trim/speedup)
            var rawNarrDur = await ProbeMediaDurationSecondsAsync(ffmpeg, narrMp3, cancellationToken).ConfigureAwait(false);
            row.ReupNarrationRawMp3Path = narrMp3;
            row.ReupNarrationRawDurationSec = rawNarrDur;
            log?.Invoke($"[VideoReup] Thuyết minh TTS tự nhiên: {rawNarrDur:0.##}s.");

            var totalDur = row.LastSourceVideoDurationSec ?? 0d;
            if (totalDur <= 0 && !string.IsNullOrWhiteSpace(row.ReupDownloadedVideoPath))
            {
                totalDur = await ProbeMediaDurationSecondsAsync(ffmpeg, row.ReupDownloadedVideoPath, cancellationToken).ConfigureAwait(false);
            }

            var trimContentSeconds = totalDur - ReupVideoSpec.TrimHeadSeconds - ReupVideoSpec.TrimTailSeconds;
            var videoOutSeconds = ReupVideoSpec.ApplySpeedChange
                ? trimContentSeconds / ReupVideoSpec.SpeedFactor
                : trimContentSeconds;
            var hookUsed = row.LastHookDurationUsedSec ?? ResolveHookMinSeconds(row);
            // Thuyết minh khớp toàn bộ phần body (hook intro là clip riêng phía trước).
            var maxNarrSec = Math.Max(2d, videoOutSeconds);

            var narrWav = Path.Combine(stage, "narration_voice.wav");
            await PrepareNarrationWavAsync(
                ffmpeg,
                narrMp3,
                narrWav,
                maxNarrSec,
                log,
                cancellationToken).ConfigureAwait(false);

            row.ReupNarrationAudioPath = narrWav;
            var narrDur = await ProbeMediaDurationSecondsAsync(ffmpeg, narrWav, cancellationToken).ConfigureAwait(false);
            log?.Invoke($"[VideoReup] Thuyết minh WAV sẵn sàng ({narrDur:0.##}s / tối đa {maxNarrSec:0.##}s).");
        }

        /// <summary>Phân tích video bằng Gemini nếu chưa có tóm tắt nội dung (phục vụ script thuyết minh).</summary>
        public async Task EnsureVideoAnalysisContextAsync(
            VideoReupRowItem row,
            AppSettings settings,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (row == null || !string.IsNullOrWhiteSpace((row.VideoScript ?? string.Empty).Trim()))
            {
                return;
            }

            if (affiliateHunter == null)
            {
                log?.Invoke("[VideoReup] Chưa có phân tích video — script dựa vào tên sản phẩm.");
                return;
            }

            if (string.IsNullOrWhiteSpace((row.ReupDownloadedVideoPath ?? string.Empty).Trim()) || !File.Exists(row.ReupDownloadedVideoPath))
            {
                await EnsureVideoDownloadedAsync(row, settings, affiliateHunter, log, cancellationToken).ConfigureAwait(false);
            }

            log?.Invoke("[VideoReup] Gemini: đang phân tích nội dung video để viết script thuyết minh…");
            try
            {
                var analysis = await affiliateHunter.AnalyzeVideoContentAsync(
                    row.VideoUrl,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);

                var summary = (analysis?.VideoScript ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = (analysis?.VoiceoverTranscript ?? string.Empty).Trim();
                }

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    row.VideoScript = summary;
                    log?.Invoke("[VideoReup] Đã có phân tích video (" + summary.Length + " ký tự).");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke("[VideoReup] Phân tích video bỏ qua: " + ex.Message);
            }
        }

        /// <summary>Tải video nguồn về thư mục stage (source.mp4) và cập nhật độ dài.</summary>
        public async Task EnsureVideoDownloadedAsync(
            VideoReupRowItem row,
            AppSettings settings,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (string.IsNullOrWhiteSpace(row.VideoUrl))
            {
                throw new InvalidOperationException("Dòng chưa có URL video.");
            }

            var stage = GetOrCreateStageFolder(row);
            var target = Path.Combine(stage, StageSourceMp4);
            var ffmpeg = ResolveFfmpegPath(settings);

            if (File.Exists(target) && new FileInfo(target).Length > 10_000L)
            {
                row.ReupDownloadedVideoPath = target;
                row.LastSourceVideoDurationSec = await ProbeMediaDurationSecondsAsync(ffmpeg, target, cancellationToken).ConfigureAwait(false);
                log?.Invoke("[VideoReup] Đã có video nguồn trong stage — dùng lại.");
                return;
            }

            if (!TryValidateFfmpegToolkit(settings, out var ffErr))
            {
                throw new InvalidOperationException(ffErr);
            }

            log?.Invoke("[VideoReup] Đang tải video nguồn (TikWM)...");
            var dl = await affiliateHunter.DownloadAffiliateVideoAsync(row.VideoUrl, stage, log, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(dl))
            {
                throw new InvalidOperationException("TikWM tải xong nhưng không thấy file MP4: " + dl);
            }

            if (!string.Equals(dl, target, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(target))
                {
                    try
                    {
                        File.Delete(target);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                try
                {
                    File.Copy(dl, target, true);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Đã tải MP4 nhưng không copy được sang source.mp4:\r\nNguồn: " + dl + "\r\nĐích: " + target + "\r\n" + ex.Message,
                        ex);
                }

                try
                {
                    File.Delete(dl);
                }
                catch
                {
                    // ignored
                }
            }

            row.ReupDownloadedVideoPath = target;
            if (!File.Exists(target))
            {
                throw new InvalidOperationException("Không thấy file video nguồn sau tải: " + target);
            }

            log?.Invoke("[VideoReup] Đã tải — đang đọc thời lượng (ffprobe)...");
            var totalDur = await ProbeMediaDurationSecondsAsync(ffmpeg, target, cancellationToken).ConfigureAwait(false);
            if (totalDur < 0.5d)
            {
                throw new InvalidOperationException(
                    "Không đọc được thời lượng video (ffprobe). Kiểm tra FFmpeg Path trong Cài đặt — cần ffmpeg.exe và ffprobe.exe cùng thư mục.");
            }
            if (totalDur < ReupVideoSpec.TrimHeadSeconds + ReupVideoSpec.TrimTailSeconds + 1.5d)
            {
                throw new InvalidOperationException(
                    $"Video quá ngắn ({totalDur:0.0}s) — không đủ cắt đầu/cuối {ReupVideoSpec.TrimHeadSeconds:0.#}s mỗi bên.");
            }

            row.LastSourceVideoDurationSec = totalDur;
        }

        /// <summary>Voiceover (ElevenLabs ưu tiên, Google TTS dự phòng) đọc <see cref="VideoReupRowItem.ReupHookDraft"/> → MP3 temp + WAV chuẩn hóa.</summary>
        public async Task BuildVoiceoverHookAudioAsync(
            VideoReupRowItem row,
            AppSettings settings,
            AffiliateHunter affiliateHunter,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!TryValidateVoiceoverAudioStep(row, settings, out var err))
            {
                throw new InvalidOperationException(err);
            }

            await EnsureVideoDownloadedAsync(row, settings, affiliateHunter, log, cancellationToken).ConfigureAwait(false);
            VideoReupStyleVariants.ApplyActiveSelections(row, settings, log);

            var ffmpeg = ResolveFfmpegPath(settings);
            var stage = row.ReupStageFolder;
            Directory.CreateDirectory(stage);
            var hookRawMp3 = Path.Combine(stage, ReupStageCacheHelper.HookTtsRawMp3);

            var hookLine = (row.ReupHookDraft ?? string.Empty).Trim();
            if (VideoReupRemixService.IsElevenLabsConfigured(settings))
            {
                hookLine = await VietnameseTtsTextNormalizer.PrepareForElevenLabsAsync(
                    hookLine,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                hookLine = VietnameseTtsTextNormalizer.NormalizePunctuation(hookLine);
            }

            if (!string.IsNullOrWhiteSpace(hookLine))
            {
                row.ReupHookDraft = hookLine;
            }

            var hookStyleKey = (row.SelectedHookStyleKey ?? row.HookStyleKey ?? string.Empty).Trim();
            var hookSynthText = ReupElevenLabsVoiceHelper.PrepareTextForSynthesis(
                hookLine,
                settings,
                emphaticHook: true,
                hookStyleKey,
                log);
            var hookTtsCacheKey = ReupStageCacheHelper.BuildElevenLabsTtsCacheKey(
                hookSynthText,
                settings,
                emphaticHook: true,
                hookStyleKey);

            string hookRaw;
            if (ReupStageCacheHelper.TryReuseCachedArtifacts(
                    stage,
                    ReupStageCacheHelper.HookTtsKeyFile,
                    hookTtsCacheKey,
                    hookRawMp3))
            {
                log?.Invoke("[VideoReup] Hook TTS: dùng cache " + ReupStageCacheHelper.HookTtsRawMp3 + " (bỏ qua ElevenLabs).");
                hookRaw = hookRawMp3;
            }
            else
            {
                await BuildVoiceoverHookMp3Async(
                    row,
                    hookLine,
                    hookRawMp3,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);
                ReupStageCacheHelper.WriteCacheKey(stage, ReupStageCacheHelper.HookTtsKeyFile, hookTtsCacheKey);
                hookRaw = hookRawMp3;
            }

            row.HookAudioPath = hookRaw;

            var totalDur = row.LastSourceVideoDurationSec ?? 0d;
            var trimContentSeconds = totalDur - ReupVideoSpec.TrimHeadSeconds - ReupVideoSpec.TrimTailSeconds;
            var videoOutSeconds = ReupVideoSpec.ApplySpeedChange
                ? trimContentSeconds / ReupVideoSpec.SpeedFactor
                : trimContentSeconds;

            var reserveTailForMusic = row.ReupAudioMode == VideoReupAudioMode.AffiliateBed;
            var hookNormWav = await NormalizeHookAudioToWavAsync(
                ffmpeg,
                hookRaw,
                stage,
                videoOutSeconds,
                reserveTailForMusic,
                log,
                cancellationToken).ConfigureAwait(false);

            var hookUsed = await ProbeMediaDurationSecondsAsync(ffmpeg, hookNormWav, cancellationToken).ConfigureAwait(false);
            var hookMin = ResolveHookMinSeconds(row);
            if (hookUsed < hookMin - 0.15d)
            {
                throw new InvalidOperationException($"Hook sau chuẩn hóa quá ngắn ({hookUsed:0.##}s, tối thiểu {hookMin:0.#}s).");
            }

            row.ReupHookAudioPath = hookNormWav;
            row.LastHookDurationUsedSec = hookUsed;

            row.ReupHookAudioPath = await ApplyHookOverlaySfxIfSelectedAsync(
                ffmpeg,
                row,
                settings,
                hookNormWav,
                stage,
                log,
                cancellationToken).ConfigureAwait(false);

            log?.Invoke("[VideoReup] Hook âm thanh (WAV) sẵn sàng cho bước render.");
        }

        /// <summary>Ghép nhạc + video re-encode + mux MP4 cuối (dùng file đã chuẩn bị trong stage).</summary>
        public async Task<VideoReupRemixResult> RenderFinalVideoAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!TryValidateFinalRenderStep(row, settings, out var preErr))
            {
                throw new InvalidOperationException(preErr);
            }

            // Auto-gán style hook nếu chưa chọn và catalog có clip
            VideoReupStyleVariants.ApplyActiveSelections(row, settings, log);
            SeedHookStyleKeyIfEmpty(row, settings);

            var ffmpeg = ResolveFfmpegPath(settings);
            var isFilm = row.ReupAudioMode == VideoReupAudioMode.FilmKeepOriginal;
            var isNarration = row.ReupAudioMode == VideoReupAudioMode.NarrationScript;
            IReadOnlyList<string> musicFiles = Array.Empty<string>();
            IReadOnlyList<string> musicNames = Array.Empty<string>();
            if (!isFilm)
            {
                var musicDir = GetMusicLibraryDirectory(settings);
                EnsureMusicLibraryDirectoryExists(settings);
                musicFiles = Directory.GetFiles(musicDir, "*.mp3", SearchOption.TopDirectoryOnly);
                musicNames = musicFiles.Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            }

            var downloadedPath = row.ReupDownloadedVideoPath;
            var hookNormWav = row.ReupHookAudioPath;

            // Dùng thẳng nguồn gốc — không pre-scramble encode để tránh generation loss lần 1.
            // Toàn bộ filter (crop, hflip, color, speed, noise) được gộp 1 lần duy nhất ở bước body encode.
            var videoSourcePath = downloadedPath;

            var totalDur = await ProbeMediaDurationSecondsAsync(ffmpeg, videoSourcePath, cancellationToken).ConfigureAwait(false);
            if (totalDur < ReupVideoSpec.TrimHeadSeconds + ReupVideoSpec.TrimTailSeconds + 1.5d)
            {
                throw new InvalidOperationException(
                    $"Video quá ngắn ({totalDur:0.0}s) — không đủ cắt đầu/cuối {ReupVideoSpec.TrimHeadSeconds:0.#}s mỗi bên.");
            }

            var trimContentSeconds = totalDur - ReupVideoSpec.TrimHeadSeconds - ReupVideoSpec.TrimTailSeconds;
            var bodyVideoOutSeconds = ReupVideoSpec.ApplySpeedChange
                ? trimContentSeconds / ReupVideoSpec.SpeedFactor
                : trimContentSeconds;
            log?.Invoke(
                ReupVideoSpec.ApplySpeedChange
                    ? $"[VideoReup] Thời lượng gốc {totalDur:0.##}s → body sau cắt {trimContentSeconds:0.##}s → sau tốc độ x{ReupVideoSpec.SpeedFactor:0.##} ≈ {bodyVideoOutSeconds:0.##}s."
                    : $"[VideoReup] Thời lượng gốc {totalDur:0.##}s → body sau cắt đầu/đuôi {ReupVideoSpec.TrimHeadSeconds:0.#}+{ReupVideoSpec.TrimTailSeconds:0.#}s ≈ {bodyVideoOutSeconds:0.##}s.");

            var hookUsed = await ProbeMediaDurationSecondsAsync(ffmpeg, hookNormWav, cancellationToken).ConfigureAwait(false);
            var hookMinRender = ResolveHookMinSeconds(row);
            if (hookUsed < hookMinRender - 0.15d)
            {
                throw new InvalidOperationException($"Hook WAV quá ngắn ({hookUsed:0.##}s, tối thiểu {hookMinRender:0.#}s).");
            }

            double slowPtsFactor = 1.0d;
            if (isNarration)
            {
                var slowResult = await ComputeNarrationSlowPtsAsync(
                    row, ffmpeg, hookUsed, trimContentSeconds, bodyVideoOutSeconds, log, cancellationToken)
                    .ConfigureAwait(false);
                slowPtsFactor = slowResult.SlowFactor;
                bodyVideoOutSeconds = slowResult.NewVideoOutSeconds;
            }

            var totalOutSeconds = hookUsed + bodyVideoOutSeconds;
            log?.Invoke($"[VideoReup] Tổng output: hook intro {hookUsed:0.##}s + body {bodyVideoOutSeconds:0.##}s = {totalOutSeconds:0.##}s.");

            var stage = row.ReupStageFolder;
            var bodyNoAudio = Path.Combine(stage, VideoReupHookIntroService.BodyVideoNoAudioFileName);
            var combinedNoAudio = Path.Combine(stage, VideoReupHookIntroService.CombinedVideoNoAudioFileName);
            var fullAudioWav = Path.Combine(stage, "full_audio.wav");

            // ── Hook intro AI (ảnh Gemini + phụ đề + clip riêng) ─────────────────────
            var hookIntroService = new VideoReupHookIntroService();
            await hookIntroService.PrepareHookIntroVideoAsync(
                row,
                settings,
                gemini,
                ffmpeg,
                hookNormWav,
                hookUsed,
                stage,
                log,
                cancellationToken).ConfigureAwait(false);

            if (isNarration)
            {
                var narrWav = row.ReupNarrationAudioPath;
                string musicPath = null;
                if (VideoReupRowItem.HasMusicBedSelected(row.ReupSelectedMusicFile) && musicFiles.Count > 0)
                {
                    musicPath = PickMusicPath(musicFiles, row.ReupSelectedMusicFile, musicNames, log);
                }

                log?.Invoke(string.IsNullOrWhiteSpace(musicPath)
                    ? $"[VideoReup] Ghép audio TM: hook {hookUsed:0.##}s + thuyết minh body {bodyVideoOutSeconds:0.##}s."
                    : $"[VideoReup] Ghép audio TM: hook + thuyết minh body + nhạc ~{ReupVideoSpec.NarrationMusicBedVolume * 100:0.#}%.");
                await BuildNarrationModeCompositeAudioAsync(
                    ffmpeg,
                    hookNormWav,
                    narrWav,
                    musicPath,
                    hookUsed,
                    totalOutSeconds,
                    fullAudioWav,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (!isFilm)
            {
                if (!VideoReupRowItem.HasMusicBedSelected(row.ReupSelectedMusicFile))
                {
                    log?.Invoke($"[VideoReup] Ghép audio: hook {hookUsed:0.##}s + im lặng body {bodyVideoOutSeconds:0.##}s.");
                    await BuildHookOnlyOrSilenceTailAudioAsync(
                        ffmpeg,
                        hookNormWav,
                        hookUsed,
                        totalOutSeconds,
                        fullAudioWav,
                        log,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var musicPath = PickMusicPath(musicFiles, row.ReupSelectedMusicFile, musicNames, log);
                    log?.Invoke($"[VideoReup] Ghép audio: hook {hookUsed:0.##}s + nhạc body {bodyVideoOutSeconds:0.##}s.");
                    await BuildCompositeAudioAsync(
                        ffmpeg,
                        hookNormWav,
                        musicPath,
                        hookUsed,
                        bodyVideoOutSeconds,
                        fullAudioWav,
                        log,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                var filmAudioSource = ResolveFilmAudioSourcePath(row);
                log?.Invoke($"[VideoReup] Ghép audio (phim): hook {hookUsed:0.##}s + tiếng gốc body (≈{trimContentSeconds:0.##}s).");
                await BuildFilmCompositeAudioAsync(
                    ffmpeg,
                    filmAudioSource,
                    hookNormWav,
                    stage,
                    hookUsed,
                    trimContentSeconds,
                    fullAudioWav,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }

            var compositeDur = await ProbeMediaDurationSecondsAsync(ffmpeg, fullAudioWav, cancellationToken).ConfigureAwait(false);
            var drift = Math.Abs(compositeDur - totalOutSeconds);
            if (drift > 0.35d)
            {
                log?.Invoke($"[VideoReup] Cảnh báo: audio {compositeDur:0.##}s lệch video {totalOutSeconds:0.##}s khoảng {drift:0.##}s — mux dùng -t.");
            }

            // OCR body (không lật nếu có chữ)
            bool skipHflip = false;
            if (OcrService != null)
            {
                try
                {
                    log?.Invoke("[VideoReup] OCR: đang kiểm tra văn bản trong video nguồn…");
                    skipHflip = await OcrService.DetectTextInVideoAsync(videoSourcePath, log, cancellationToken).ConfigureAwait(false);
                    row.OcrDetectedText = skipHflip;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ocrEx)
                {
                    log?.Invoke("[VideoReup] OCR lỗi (bỏ qua, dùng hflip mặc định): " + ocrEx.Message);
                    skipHflip = false;
                }
            }

            // Tính setpts: slowdown > 1 khi script dài hơn video; ApplySpeedChange < 1 khi tăng tốc cố định
            // Ưu tiên: slowdown (từ script) > ApplySpeedChange (hằng số)
            string setptsExpr;
            if (slowPtsFactor > 1.001d)
            {
                // Làm chậm: setpts = factor*PTS  →  output_dur = input_dur * factor
                setptsExpr = "setpts=" + slowPtsFactor.ToString("0.#####", CultureInfo.InvariantCulture) + "*PTS";
            }
            else
            {
                setptsExpr = null;
            }

            // Color grading — preset/tùy chỉnh trên từng dòng (FFmpeg eq)
            var colorEq = ReupColorGradeHelper.BuildEqFilterChain(row, settings);
            string baseVfCore;
            if (skipHflip)
            {
                baseVfCore = setptsExpr != null ? setptsExpr + "," + colorEq : colorEq;
                log?.Invoke("[VideoReup] Video có chữ → BỎ QUA hflip, giữ nguyên chiều.");
            }
            else
            {
                baseVfCore = setptsExpr != null
                    ? setptsExpr + ",hflip," + colorEq
                    : "hflip," + colorEq;
            }

            log?.Invoke("[VideoReup] Color grade: «" + ReupColorGradeHelper.FormatStyleSummary(row) + "» → " + colorEq);

            var baseVf = RemixScramblerService.AppendVideoAntiDetectionFilters(baseVfCore);

            // CRF 17 cố định — chất lượng HD, không ngẫu nhiên để output nhất quán
            const int BodyCrf = 17;
            var metaArgs = RemixScramblerService.BuildFakeMetadataArgs();
            var encodeOutDur = bodyVideoOutSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var ssTrim = ReupVideoSpec.TrimHeadSeconds.ToString("0.#####", CultureInfo.InvariantCulture);

            var bgVideoPath = PhilosophyProfileAssets.TryPickDynamicBackgroundVideo();
            var filterComplex = RemixScramblerService.BuildBgBlendFilterComplex(baseVf, bgVideoPath, null);

            string bodyVArgs;
            if (filterComplex != null)
            {
                log?.Invoke($"[VideoReup] Body: overlay BG mờ 3% ({Path.GetFileName(bgVideoPath)})…");
                bodyVArgs = "-y -ss " + ssTrim +
                        " -i \"" + videoSourcePath + "\"" +
                        " -stream_loop -1 -i \"" + bgVideoPath + "\"" +
                        " -t " + encodeOutDur +
                        " -filter_complex \"" + filterComplex + "\"" +
                        " -map \"[vout]\" -an -c:v libx264 -preset slow -crf " + BodyCrf.ToString(CultureInfo.InvariantCulture) +
                        " -profile:v high -pix_fmt yuv420p" + metaArgs +
                        " \"" + bodyNoAudio + "\"";
            }
            else
            {
                bodyVArgs = "-y -ss " + ssTrim +
                        " -i \"" + videoSourcePath + "\" -t " + encodeOutDur +
                        " -vf \"" + baseVf + "\" -an -c:v libx264 -preset slow -crf " + BodyCrf.ToString(CultureInfo.InvariantCulture) +
                        " -profile:v high -pix_fmt yuv420p" + metaArgs +
                        " \"" + bodyNoAudio + "\"";
            }

            log?.Invoke(slowPtsFactor > 1.001d
                ? $"[VideoReup] FFmpeg body: cắt, chậm x{slowPtsFactor:0.###}, {(skipHflip ? "giữ chiều" : "lật ngang")}, lách anti-detect…"
                : skipHflip
                    ? $"[VideoReup] FFmpeg body: cắt, GIỮ chiều (có chữ), lách anti-detect…"
                    : $"[VideoReup] FFmpeg body: cắt, lật ngang, lách anti-detect…");
            await RunFfmpegAsync(ffmpeg, bodyVArgs, log, cancellationToken).ConfigureAwait(false);

            await VideoReupHookIntroService.ConcatIntroAndBodyAsync(
                ffmpeg,
                row.ReupHookIntroVideoPath,
                bodyNoAudio,
                combinedNoAudio,
                log,
                cancellationToken).ConfigureAwait(false);

            var outDir = ProfileScopedPaths.GetVideoReupOutputRoot(row.ProfileName);
            var safeName = VideoReupCaptionService.SanitizeFileNameFragment(row.ProductName);
            var finalPath = Path.Combine(outDir, "reup_" + safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".mp4");

            var muxMeta = RemixScramblerService.BuildFakeMetadataArgs();
            var muxDurStr = totalOutSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var muxArgs = "-y -i \"" + combinedNoAudio + "\" -i \"" + fullAudioWav + "\"" +
                          " -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -t " + muxDurStr +
                          muxMeta +
                          " \"" + finalPath + "\"";

            log?.Invoke(isFilm
                ? $"[VideoReup] FFmpeg: mux hook intro + body + audio (hook + tiếng gốc) — {totalOutSeconds:0.##}s..."
                : $"[VideoReup] FFmpeg: mux hook intro + body + audio — {totalOutSeconds:0.##}s...");
            await RunFfmpegAsync(ffmpeg, muxArgs, log, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(finalPath))
            {
                throw new InvalidOperationException("Không tạo được file output.");
            }

            var outFileDur = await ProbeMediaDurationSecondsAsync(ffmpeg, finalPath, cancellationToken).ConfigureAwait(false);
            log?.Invoke("[VideoReup] Xong: " + finalPath + $" (≈{outFileDur:0.##}s = hook {hookUsed:0.##}s + body {bodyVideoOutSeconds:0.##}s)");
            return new VideoReupRemixResult
            {
                OutputPath = finalPath,
                SourceDurationSeconds = totalDur,
                ProcessedVideoDurationSeconds = bodyVideoOutSeconds,
                HookDurationSecondsUsed = hookUsed,
                HookIntroDurationSeconds = hookUsed,
                BodyDurationSeconds = bodyVideoOutSeconds,
                TotalDurationSeconds = totalOutSeconds,
                OutputFileDurationSeconds = outFileDur
            };
        }

        private static async Task<string> NormalizeHookAudioToWavAsync(
            string ffmpegExe,
            string inputPath,
            string workRoot,
            double videoOutSeconds,
            bool reserveTailForMusic,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var rawDur = await ProbeMediaDurationSecondsAsync(ffmpegExe, inputPath, cancellationToken).ConfigureAwait(false);
            if (rawDur < 0.15d)
            {
                throw new InvalidOperationException("Không đọc được thời lượng hook audio (voiceover).");
            }

            // Hook intro là clip riêng — không cắt ngắn vì video body ngắn.
            var maxHook = ReupVideoSpec.HookMaxSec;
            var trimTarget = Math.Min(rawDur, maxHook);
            var trimmedWav = Path.Combine(workRoot, "hook_trim.wav");
            var trimStr = trimTarget.ToString("0.#####", CultureInfo.InvariantCulture);
            var trimArgs = "-y -i \"" + inputPath + "\" -af \"atrim=0:" + trimStr + ",asetpts=PTS-STARTPTS,aresample=48000\" \"" + trimmedWav + "\"";
            log?.Invoke($"[VideoReup] Chuẩn hóa hook: TTS raw {rawDur:0.##}s → cắt tối đa {trimTarget:0.##}s (trần {maxHook:0.##}s).");
            await RunFfmpegAsync(ffmpegExe, trimArgs, log, cancellationToken).ConfigureAwait(false);

            var durAfterTrim = await ProbeMediaDurationSecondsAsync(ffmpegExe, trimmedWav, cancellationToken).ConfigureAwait(false);
            var outWav = Path.Combine(workRoot, "hook_norm.wav");
            if (File.Exists(outWav))
            {
                try
                {
                    File.Delete(outWav);
                }
                catch
                {
                    // ignored
                }
            }

            if (durAfterTrim < ReupVideoSpec.HookMinSec - 0.08d)
            {
                var wholeSamples = (int)Math.Round(ReupVideoSpec.HookMinSec * 48000d, MidpointRounding.AwayFromZero);
                var padArgs = "-y -i \"" + trimmedWav + "\" -af \"apad=whole_len=" + wholeSamples.ToString(CultureInfo.InvariantCulture) + ",aresample=48000\" \"" + outWav + "\"";
                log?.Invoke($"[VideoReup] Pad hook im lặng lên tối thiểu {ReupVideoSpec.HookMinSec:0.#}s.");
                await RunFfmpegAsync(ffmpegExe, padArgs, log, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                File.Copy(trimmedWav, outWav, true);
            }

            return outWav;
        }

        /// <summary>Trộn SFX overlay (~45%) lên giọng hook nếu cột «SFX Hook» có chọn file.</summary>
        public static async Task<string> ApplyHookOverlaySfxIfSelectedAsync(
            string ffmpegExe,
            VideoReupRowItem row,
            AppSettings settings,
            string hookNormWav,
            string workRoot,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (row == null || ShouldUseVisualHookSfx(row)
                || !VideoReupRowItem.HasHookSfxSelected(row.ReupSelectedHookSfxFile))
            {
                return hookNormWav;
            }

            var sfxPath = ResolveHookSfxFilePath(settings, row.ReupSelectedHookSfxFile);
            if (string.IsNullOrWhiteSpace(sfxPath) || !File.Exists(sfxPath))
            {
                log?.Invoke("[VideoReup] Hook SFX: file «" + row.ReupSelectedHookSfxFile + "» không tồn tại — bỏ qua overlay.");
                return hookNormWav;
            }

            var outWav = Path.Combine(workRoot, "hook_with_sfx.wav");
            await MixHookOverlaySfxAsync(
                ffmpegExe,
                hookNormWav,
                sfxPath,
                outWav,
                log,
                cancellationToken).ConfigureAwait(false);
            return outWav;
        }

        private static async Task MixHookOverlaySfxAsync(
            string ffmpegExe,
            string hookWavPath,
            string sfxPath,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var volStr = ReupVideoSpec.HookOverlaySfxVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var filter =
                "[0:a]aresample=48000,volume=1.0[vo];" +
                "[1:a]aresample=48000,volume=" + volStr + "[sfx];" +
                "[vo][sfx]amix=inputs=2:duration=first:dropout_transition=0[aout]";
            var args = "-y -i \"" + hookWavPath + "\" -i \"" + sfxPath + "\"" +
                       " -filter_complex \"" + filter + "\"" +
                       " -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";
            log?.Invoke("[VideoReup] FFmpeg: trộn SFX hook «" + Path.GetFileName(sfxPath) + "» (~"
                        + (ReupVideoSpec.HookOverlaySfxVolume * 100d).ToString("0", CultureInfo.InvariantCulture)
                        + "%) cùng giọng hook…");
            await RunFfmpegAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Tính hệ số làm chậm video (setpts = factor*PTS) để video nền đủ dài chứa hook + script.
        /// Trả về (slowFactor, newVideoOutSeconds). slowFactor = 1.0 nếu không cần làm chậm.
        /// </summary>
        private static async Task<(double SlowFactor, double NewVideoOutSeconds)> ComputeNarrationSlowPtsAsync(
            VideoReupRowItem row,
            string ffmpegExe,
            double hookUsed,
            double trimContentSeconds,
            double videoOutSeconds,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            const double MaxSlowPts = 1.2d;    // tối đa 20% chậm hơn
            const double SlowBuffer = 0.5d;     // đệm thêm 0.5s sau script
            const double Threshold = 0.2d;      // bỏ qua nếu lệch < 0.2s

            var rawMp3 = (row.ReupNarrationRawMp3Path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(rawMp3) || !File.Exists(rawMp3))
            {
                var stageFallback = Path.Combine(row.ReupStageFolder ?? string.Empty, ReupStageCacheHelper.NarrationRawMp3);
                if (File.Exists(stageFallback))
                {
                    rawMp3 = stageFallback;
                    row.ReupNarrationRawMp3Path = stageFallback;
                }
                else
                {
                    return (1.0d, videoOutSeconds);
                }
            }

            // Thời lượng TTS tự nhiên (đã lưu khi voiceover, probe lại nếu thiếu)
            var rawNarrDur = row.ReupNarrationRawDurationSec > 0.1d
                ? row.ReupNarrationRawDurationSec
                : await ProbeMediaDurationSecondsAsync(ffmpegExe, rawMp3, cancellationToken).ConfigureAwait(false);

            if (rawNarrDur < 0.5d)
            {
                return (1.0d, videoOutSeconds);
            }

            var neededBodyOut = rawNarrDur + SlowBuffer;

            if (neededBodyOut <= videoOutSeconds + Threshold)
            {
                return (1.0d, videoOutSeconds);
            }

            if (trimContentSeconds < 0.5d)
            {
                return (1.0d, videoOutSeconds);
            }

            var neededFactor = neededBodyOut / trimContentSeconds;
            var actualFactor = Math.Min(MaxSlowPts, neededFactor);
            var newBodyOut = trimContentSeconds * actualFactor;

            if (neededFactor > MaxSlowPts)
            {
                log?.Invoke(
                    $"[VideoReup] Cảnh báo: cần làm chậm body x{neededFactor:0.##} nhưng giới hạn tối đa x{MaxSlowPts:0.##} " +
                    $"→ body ra {newBodyOut:0.##}s, script có thể bị cắt nhẹ cuối.");
            }
            else
            {
                log?.Invoke(
                    $"[VideoReup] Script ({rawNarrDur:0.##}s) > body ({videoOutSeconds:0.##}s) " +
                    $"→ làm chậm body x{actualFactor:0.###} (body ra {newBodyOut:0.##}s).");
            }

            var newMaxNarrSec = Math.Max(2d, newBodyOut);
            var narrWavSlow = Path.Combine(row.ReupStageFolder, "narration_voice.wav");
            log?.Invoke($"[VideoReup] Re-chuẩn hóa thuyết minh cho slot mới {newMaxNarrSec:0.##}s…");
            await PrepareNarrationWavAsync(
                ffmpegExe,
                rawMp3,
                narrWavSlow,
                newMaxNarrSec,
                log,
                cancellationToken).ConfigureAwait(false);

            row.ReupNarrationAudioPath = narrWavSlow;
            return (actualFactor, newBodyOut);
        }

        private static string PickMusicPath(
            IReadOnlyList<string> musicFiles,
            string requestedName,
            IReadOnlyList<string> musicNames,
            Action<string> log)
        {
            var want = (requestedName ?? string.Empty).Trim();
            if (VideoReupRowItem.IsNoMusicSelection(want))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(want))
            {
                foreach (var f in musicFiles)
                {
                    if (string.Equals(Path.GetFileName(f), want, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                }

                log?.Invoke("[VideoReup] Gemini chọn nhạc «" + want + "» không khớp file — dùng file đầu tiên.");
            }

            return musicFiles[0];
        }

        private sealed class ReupHookNarrationBundle
        {
            public Dictionary<string, string> HooksByStyle { get; set; }
                = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> ScriptsByStyle { get; set; }
                = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static int ComputeNarrationWordTarget(VideoReupRowItem row, out double bodySec)
        {
            var totalDur = row.LastSourceVideoDurationSec ?? 35d;
            bodySec = Math.Max(8d, totalDur - ReupVideoSpec.TrimHeadSeconds - ReupVideoSpec.TrimTailSeconds);
            var wordTarget = (int)Math.Round(bodySec * 2.4d, MidpointRounding.AwayFromZero);
            return Math.Max(40, Math.Min(220, wordTarget));
        }

        private static string DefaultReupHookFallback() =>
            "Sản phẩm này khiến mình phải nói thật luôn.";

        private static string DefaultNarrationScriptFallback(string productName) =>
            DefaultNarrationScriptFallbackForStyle(HookStyleCatalog.StyleHuongdan, productName);

        private static string GetLocalHookForStyle(string styleKey, string productName)
        {
            var product = string.IsNullOrWhiteSpace(productName) ? "sản phẩm này" : productName.Trim();
            switch (styleKey)
            {
                case HookStyleCatalog.StyleBocphot:
                    return "Thật ra " + product + " có điểm mà ít ai nói thẳng — coi hết clip.";
                case HookStyleCatalog.StyleFomo:
                    return "Loại " + product + " hay hết hàng lắm — xem nhanh kẻo lỡ.";
                case HookStyleCatalog.StyleKechuyen:
                    return "Mình từng bỏ qua " + product + " — đến khi thử mới thấy khác.";
                case HookStyleCatalog.StyleNoidau:
                    return "Ai hay gặp nỗi này với " + product + " thì xem clip này cho rõ.";
                default:
                    return "Cách chọn " + product + " cho người mới — 3 giây đầu quan trọng lắm.";
            }
        }

        private static string DefaultNarrationScriptFallbackForStyle(string styleKey, string productName)
        {
            var name = string.IsNullOrWhiteSpace(productName) ? "sản phẩm này" : productName.Trim();
            switch (styleKey)
            {
                case HookStyleCatalog.StyleNoidau:
                    return "Nếu bạn đang vướng khi chọn " + name
                           + ", mình chia sẻ cách mình đã thử trong video — xem kỹ từng bước nhé.";
                case HookStyleCatalog.StyleBocphot:
                    return "Nhiều người mua " + name
                           + " mà chưa biết điểm này — mình nói thẳng trong clip để bạn tránh tiếc tiền.";
                case HookStyleCatalog.StyleFomo:
                    return name + " đang được nhiều người săn — mình tóm lại trong video để bạn quyết nhanh, đỡ lỡ.";
                case HookStyleCatalog.StyleKechuyen:
                    return "Mình từng nghi ngờ " + name
                           + " cho đến khi xem kỹ clip này — phần thực tế mình thấy nằm ở giữa video.";
                default:
                    return "Trong clip này mình chỉ cách chọn và dùng " + name
                           + " cho người mới — bạn xem kỹ và thử nếu hợp nhu cầu nhé.";
            }
        }

        private static async Task<ReupHookNarrationBundle> GenerateHookAndNarrationBundleFromGeminiAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            bool includeScripts,
            CancellationToken cancellationToken)
        {
            var videoScript = Trim((row.VideoScript ?? string.Empty).Trim(), 2000);
            var rawProductName = (row.ProductName ?? string.Empty).Trim();
            var productName = VideoReupProductLabel.GetShortLabel(rawProductName);
            var wordTarget = ComputeNarrationWordTarget(row, out var bodySec);
            var reupRules =
                "QUY TẮC REUP (bắt buộc):\r\n" +
                "- Tên sản phẩm NGẮN (đã làm sạch): «" + productName + "» — KHÔNG copy tiêu đề TikTok gốc.\r\n" +
                "- KHÔNG nhắc tên shop/kênh/creator, «created by», «nhạc nền», branding video gốc.\r\n" +
                "- Hook chỉ mô tả sản phẩm ngắn gọn, tự nhiên, không emoji.\r\n\r\n";

            string prompt;
            if (includeScripts)
            {
                prompt =
                    "Bạn là chuyên gia Reup TikTok affiliate tiếng Việt.\r\n" +
                    "Dựa vào PHÂN TÍCH VIDEO:\r\n[" + videoScript + "]\r\n\r\n" +
                    reupRules +
                    "Viết hook + script thuyết minh cho ĐÚNG 5 PHONG CÁCH sau (mỗi style một cặp hook/script KHÁC NHAU):\r\n" +
                    "• boc_phot — Bóc phốt: chỉ ra sự thật, gây tranh cãi nhẹ, tò mò\r\n" +
                    "• fomo — FOMO: khan hiếm, sợ bỏ lỡ, urgency\r\n" +
                    "• huong_dan — Hướng dẫn: mẹo, cách dùng, tutorial ngắn\r\n" +
                    "• ke_chuyen — Kể chuyện: trải nghiệm, narrative tự nhiên\r\n" +
                    "• noi_dau — Nỗi đau: pain point, nỗi đau khách hàng\r\n\r\n" +
                    "Mỗi \"hook\": CHỈ 1 câu 15–20 từ tiếng Việt — đọc voiceover 4–7 giây, tự nhiên.\r\n" +
                    "Mỗi \"script\": kịch bản thuyết minh TOÀN BỘ phần body (~" + wordTarget + " từ, ~" +
                    bodySec.ToString("0", CultureInfo.InvariantCulture) +
                    " giây đọc — KHÔNG gồm hook intro). Giọng kể chuyện tin cậy — KHÁC hẳn hook cùng style.\r\n\r\n" +
                    "NGÔN NGỮ: tiếng Việt có dấu chuẩn; không emoji.\r\n" +
                    "Trả về DUY NHẤT JSON (không markdown):\r\n" +
                    "{\"styles\":{\"boc_phot\":{\"hook\":\"...\",\"script\":\"...\"},\"fomo\":{\"hook\":\"...\",\"script\":\"...\"}," +
                    "\"huong_dan\":{\"hook\":\"...\",\"script\":\"...\"},\"ke_chuyen\":{\"hook\":\"...\",\"script\":\"...\"}," +
                    "\"noi_dau\":{\"hook\":\"...\",\"script\":\"...\"}}}";
                log?.Invoke("[VideoReup] Gemini: đang viết hook + script (5 phong cách)…");
            }
            else
            {
                prompt =
                    "Bạn là chuyên gia Reup TikTok affiliate tiếng Việt.\r\n" +
                    "Dựa vào PHÂN TÍCH VIDEO:\r\n[" + videoScript + "]\r\n\r\n" +
                    reupRules +
                    "Viết hook cho ĐÚNG 5 PHONG CÁCH sau (mỗi style một hook KHÁC NHAU — KHÔNG cần script):\r\n" +
                    "• boc_phot — Bóc phốt: chỉ ra sự thật, gây tranh cãi nhẹ, tò mò\r\n" +
                    "• fomo — FOMO: khan hiếm, sợ bỏ lỡ, urgency\r\n" +
                    "• huong_dan — Hướng dẫn: mẹo, cách dùng, tutorial ngắn\r\n" +
                    "• ke_chuyen — Kể chuyện: trải nghiệm, narrative tự nhiên\r\n" +
                    "• noi_dau — Nỗi đau: pain point, nỗi đau khách hàng\r\n\r\n" +
                    "Mỗi \"hook\": CHỈ 1 câu 15–20 từ tiếng Việt — đọc voiceover 4–7 giây, tự nhiên.\r\n\r\n" +
                    "NGÔN NGỮ: tiếng Việt có dấu chuẩn; không emoji.\r\n" +
                    "Trả về DUY NHẤT JSON (không markdown):\r\n" +
                    "{\"styles\":{\"boc_phot\":{\"hook\":\"...\"},\"fomo\":{\"hook\":\"...\"}," +
                    "\"huong_dan\":{\"hook\":\"...\"},\"ke_chuyen\":{\"hook\":\"...\"}," +
                    "\"noi_dau\":{\"hook\":\"...\"}}}";
                log?.Invoke("[VideoReup] Gemini: đang viết hook (5 phong cách — không script)…");
            }

            var raw = await gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            return ParseHookNarrationBundle(raw, rawProductName, productName);
        }

        private static ReupHookNarrationBundle ParseHookNarrationBundle(
            string raw,
            string rawProductName,
            string shortProductLabel = null)
        {
            var label = shortProductLabel ?? VideoReupProductLabel.GetShortLabel(rawProductName);
            var json = ExtractJsonObject(raw);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var o = JObject.Parse(json);
                    var stylesNode = o["styles"] ?? o["Styles"];
                    if (stylesNode is JObject stylesObj)
                    {
                        var hooks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var key in HookStyleCatalog.AllStyleKeys)
                        {
                            if (!(stylesObj[key] is JObject styleObj))
                            {
                                continue;
                            }

                            var hook = VideoReupProductLabel.NormalizeHookText(
                                SanitizeHookLine((styleObj["hook"] ?? styleObj["Hook"])?.ToString()),
                                rawProductName,
                                label);
                            var script = VideoReupProductLabel.NormalizeScriptText(
                                SanitizeNarrationScript(
                                    (styleObj["script"] ?? styleObj["narrationScript"] ?? styleObj["narration"])?.ToString()),
                                rawProductName,
                                label);
                            if (!string.IsNullOrWhiteSpace(hook))
                            {
                                hooks[key] = hook;
                            }

                            if (!string.IsNullOrWhiteSpace(script))
                            {
                                scripts[key] = script;
                            }
                        }

                        if (hooks.Count > 0)
                        {
                            FillMissingStyleVariants(hooks, scripts, label);
                            return new ReupHookNarrationBundle
                            {
                                HooksByStyle = hooks,
                                ScriptsByStyle = scripts
                            };
                        }
                    }

                    // Legacy JSON: single hook + narrationScript
                    var legacyHook = VideoReupProductLabel.NormalizeHookText(
                        SanitizeHookLine((o["hook"] ?? o["Hook"])?.ToString()),
                        rawProductName,
                        label);
                    var legacyScript = VideoReupProductLabel.NormalizeScriptText(
                        SanitizeNarrationScript(
                            (o["narrationScript"] ?? o["narration"] ?? o["script"] ?? o["NarrationScript"])?.ToString()),
                        rawProductName,
                        label);
                    if (!string.IsNullOrWhiteSpace(legacyHook))
                    {
                        var hooks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var key in HookStyleCatalog.AllStyleKeys)
                        {
                            hooks[key] = legacyHook;
                            scripts[key] = string.IsNullOrWhiteSpace(legacyScript)
                                ? DefaultNarrationScriptFallbackForStyle(key, label)
                                : legacyScript;
                        }

                        return new ReupHookNarrationBundle
                        {
                            HooksByStyle = hooks,
                            ScriptsByStyle = scripts
                        };
                    }
                }
                catch
                {
                    // fall through
                }
            }

            var hookOnly = VideoReupProductLabel.NormalizeHookText(
                SanitizeHookLine(raw),
                rawProductName,
                label);
            var fallbackHook = string.IsNullOrWhiteSpace(hookOnly) ? DefaultReupHookFallback() : hookOnly;
            var hooksFallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scriptsFallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                hooksFallback[key] = fallbackHook;
                scriptsFallback[key] = DefaultNarrationScriptFallbackForStyle(key, label);
            }

            return new ReupHookNarrationBundle
            {
                HooksByStyle = hooksFallback,
                ScriptsByStyle = scriptsFallback
            };
        }

        private static void FillMissingStyleVariants(
            Dictionary<string, string> hooks,
            Dictionary<string, string> scripts,
            string productName)
        {
            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                if (!hooks.TryGetValue(key, out var hook) || string.IsNullOrWhiteSpace(hook))
                {
                    hooks[key] = GetLocalHookForStyle(key, productName);
                }

                if (!scripts.TryGetValue(key, out var script) || string.IsNullOrWhiteSpace(script))
                {
                    scripts[key] = DefaultNarrationScriptFallbackForStyle(key, productName);
                }
            }
        }

        private static async Task<string> GenerateNarrationScriptAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var videoScript = Trim((row.VideoScript ?? string.Empty).Trim(), 2000);
            var rawProductName = (row.ProductName ?? string.Empty).Trim();
            var productName = VideoReupProductLabel.GetShortLabel(rawProductName);
            var hook = (row.ReupHookDraft ?? string.Empty).Trim();
            var wordTarget = ComputeNarrationWordTarget(row, out var bodySec);

            var prompt =
                "Bạn là biên kịch video TikTok affiliate tiếng Việt.\r\n" +
                "Dựa vào PHÂN TÍCH VIDEO:\r\n[" + videoScript + "]\r\n" +
                "SẢN PHẨM (tên ngắn, không shop gốc): [" + productName + "]\r\n" +
                "HOOK ĐÃ CÓ (KHÔNG lặp lại, KHÔNG paraphrase hook): [" + hook + "]\r\n\r\n" +
                "KHÔNG nhắc tên shop/kênh/creator, «created by», branding video gốc.\r\n" +
                "Viết KỊCH BẢN THUYẾT MINH cho TOÀN BỘ phần video reup body (~" + wordTarget + " từ, ~" +
                bodySec.ToString("0", CultureInfo.InvariantCulture) + " giây đọc — KHÔNG gồm hook intro).\r\n" +
                "Giọng điệu: kể chuyện tự nhiên, tin cậy, mạch lạc — KHÁC hẳn hook (không hô hào, không câu hỏi gài).\r\n" +
                "Nội dung: mô tả video/sản phẩm, lợi ích, CTA nhẹ cuối.\r\n" +
                "NGÔN NGỮ: tiếng Việt có dấu chuẩn; văn phong tự nhiên, hay, sát nội dung video — không emoji, không markdown.\r\n" +
                "CHỈ in đoạn đọc, không giải thích, không ngoặc kép, không markdown.";

            log?.Invoke("[VideoReup] Gemini: đang viết script thuyết minh…");
            var raw = await gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            var script = SanitizeNarrationScript(raw);
            if (string.IsNullOrWhiteSpace(script))
            {
                script = DefaultNarrationScriptFallback(productName);
            }

            log?.Invoke("[VideoReup] Script: " + Trim(script, 120));
            return script;
        }

        private static string SanitizeNarrationScript(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var fence = s.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                var end = s.IndexOf("```", fence + 3, StringComparison.Ordinal);
                if (end > fence)
                {
                    s = s.Substring(fence + 3, end - fence - 3).Trim();
                }
            }

            s = s.Replace("\"", string.Empty).Replace("“", string.Empty).Replace("”", string.Empty);
            if (s.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    var o = JObject.Parse(ExtractJsonObject(s));
                    s = (o["script"] ?? o["narration"] ?? o["text"])?.ToString()?.Trim() ?? s;
                }
                catch
                {
                    // keep
                }
            }

            return s.Trim();
        }

        private static async Task<string> SuggestMusicFileAsync(
            VideoReupRowItem row,
            List<string> musicFileNames,
            AppSettings settings,
            GeminiService gemini,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (musicFileNames == null || musicFileNames.Count == 0)
            {
                return string.Empty;
            }

            if (musicFileNames.Count == 1)
            {
                return musicFileNames[0];
            }

            var list = string.Join(" | ", musicFileNames);
            var prompt =
                "Chọn ĐÚNG MỘT file nhạc nền từ danh sách (trùng ký tự, gồm .mp3) phù hợp sản phẩm «" +
                (row.ProductName ?? string.Empty).Trim() + "».\r\nDanh sách: " + list +
                "\r\nTrả về DUY NHẤT JSON: {\"musicFile\":\"tên-file.mp3\"} — không markdown.";

            log?.Invoke("[VideoReup] Gemini: gợi ý nhạc nền…");
            var raw = await gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            var json = ExtractJsonObject(raw);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var o = JObject.Parse(json);
                    var music = (o["musicFile"] ?? o["musicfile"] ?? o["music"])?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(music))
                    {
                        log?.Invoke("[VideoReup] Nhạc (Gemini): " + music);
                        return music;
                    }
                }
                catch
                {
                    // fall through
                }
            }

            log?.Invoke("[VideoReup] Nhạc: dùng file đầu tiên trong thư mục.");
            return musicFileNames[0];
        }

        private static string SanitizeHookLine(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var fence = s.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                var end = s.IndexOf("```", fence + 3, StringComparison.Ordinal);
                if (end > fence)
                {
                    s = s.Substring(fence + 3, end - fence - 3).Trim();
                }
            }

            s = s.Replace("\"", string.Empty).Replace("'", string.Empty).Replace("“", string.Empty).Replace("”", string.Empty);
            var lineBreak = s.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0)
            {
                s = s.Substring(0, lineBreak).Trim();
            }

            if (s.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    var o = JObject.Parse(ExtractJsonObject(s));
                    s = (o["hook"] ?? o["Hook"])?.ToString()?.Trim() ?? s;
                }
                catch
                {
                    // keep s
                }
            }

            return s.Trim();
        }

        private static string ExtractJsonObject(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var fence = s.IndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                var end = s.IndexOf("```", fence + 3, StringComparison.Ordinal);
                if (end > fence)
                {
                    s = s.Substring(fence + 3, end - fence - 3).Trim();
                    if (s.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    {
                        s = s.Substring(4).TrimStart();
                    }
                }
            }

            var i0 = s.IndexOf('{');
            var i1 = s.LastIndexOf('}');
            if (i0 >= 0 && i1 > i0)
            {
                return s.Substring(i0, i1 - i0 + 1);
            }

            return s;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
            {
                return s ?? string.Empty;
            }

            return s.Substring(0, max) + "...";
        }

        private static async Task BuildVoiceoverHookMp3Async(
            VideoReupRowItem row,
            string hookLine,
            string outputAudioFile,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var styleKey = (row?.SelectedHookStyleKey ?? row?.HookStyleKey ?? string.Empty).Trim();
            await BuildVoiceoverMp3Async(
                hookLine,
                outputAudioFile,
                settings,
                log,
                cancellationToken,
                emphaticHook: true,
                hookStyleKey: styleKey).ConfigureAwait(false);
        }

        private static async Task BuildVoiceoverMp3Async(
            string textLine,
            string outputAudioFile,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken,
            bool emphaticHook,
            string hookStyleKey = null)
        {
            var line = (textLine ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                line = emphaticHook ? "Cùng mình khám phá ngay nhé!" : "Hãy cùng xem chi tiết trong video nhé.";
            }

            if (File.Exists(outputAudioFile))
            {
                try
                {
                    File.Delete(outputAudioFile);
                }
                catch
                {
                    // ignored
                }
            }

            if (IsElevenLabsConfigured(settings))
            {
                if (!ElevenLabsTtsHelper.EndpointIncludesVoiceId(settings.TtsEndpoint)
                    && string.IsNullOrWhiteSpace(settings.VoiceId_ReupHook)
                    && string.IsNullOrWhiteSpace(settings.VoiceId_ReupNarration)
                    && string.IsNullOrWhiteSpace(settings.VoiceId_Intense)
                    && string.IsNullOrWhiteSpace(settings.VoiceId_Calm))
                {
                    log?.Invoke(
                        "[VideoReup] Cảnh báo: chưa có Voice ID — thêm «Voice Reup hook/thuyết minh» trong Cài đặt hoặc URL …/text-to-speech/{voice_id}.");
                }

                var role = emphaticHook ? "hook (nhấn mạnh)" : "thuyết minh (kể chuyện)";
                log?.Invoke(
                    "[VideoReup] Voiceover " + role + ": ElevenLabs model="
                    + ElevenLabsTtsHelper.ResolveModelId(settings)
                    + ", lang=" + ElevenLabsTtsHelper.ResolveLanguageCode(settings) + ".");

                line = ReupElevenLabsVoiceHelper.PrepareTextForSynthesis(
                    line,
                    settings,
                    emphaticHook,
                    hookStyleKey,
                    log);

                var tempMp3 = await ReupElevenLabsVoiceHelper.GenerateAudioWithFallbackAsync(
                    ReupHookTtsService,
                    line,
                    settings,
                    emphaticHook,
                    log,
                    cancellationToken).ConfigureAwait(false);

                CopyHookMp3ToOutput(tempMp3, outputAudioFile);
            }
            else
            {
                log?.Invoke("[VideoReup] Voiceover: chưa có ElevenLabs — fallback Edge TTS vi-VN.");
                var edge = new EdgeTtsService();
                await edge.SynthesizeVietnameseFemaleToMp3Async(
                    line,
                    outputAudioFile,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(outputAudioFile) || new FileInfo(outputAudioFile).Length < 800)
            {
                throw new InvalidOperationException("Voiceover trả file audio quá nhỏ hoặc rỗng.");
            }
        }

        private static void CopyHookMp3ToOutput(string sourceMp3, string outputAudioFile)
        {
            if (string.IsNullOrWhiteSpace(sourceMp3) || !File.Exists(sourceMp3))
            {
                throw new InvalidOperationException("ElevenLabs không trả file audio hook hợp lệ.");
            }

            var targetDir = Path.GetDirectoryName(outputAudioFile);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (File.Exists(outputAudioFile))
            {
                File.Delete(outputAudioFile);
            }

            if (!string.Equals(Path.GetFullPath(sourceMp3), Path.GetFullPath(outputAudioFile), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceMp3, outputAudioFile);
            }
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

        /// <summary>
        /// Tạo composite audio dài đúng <c>hookTargetSec + musicSec</c> giây (= videoOutSeconds).
        /// Cấu trúc:
        ///   Pha 1 (0 → hookUsed):  hook voice + nhạc nền ducked 20%
        ///   Pha 2 (hookUsed → videoOut): nhạc nền full vol + fade-out; nếu nhạc file ngắn hơn → padding im lặng
        /// </summary>
        private static async Task BuildCompositeAudioAsync(
            string ffmpegExe,
            string hookMp3Path,
            string musicMp3Path,
            double hookTargetSec,
            double musicTailSec,           // = videoOutSeconds - hookUsed
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var hookStr = hookTargetSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var totalSec = hookTargetSec + musicTailSec;           // = videoOutSeconds
            var totalStr = totalSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var tailStr = musicTailSec.ToString("0.#####", CultureInfo.InvariantCulture);

            // Trường hợp không có đuôi nhạc đáng kể → hook + im lặng
            if (musicTailSec <= 0.1d || musicMp3Path == null || !File.Exists(musicMp3Path))
            {
                var filter0 =
                    "[0:a]atrim=0:" + hookStr + ",aresample=48000,volume=1.0[ho];" +
                    "anullsrc=channel_layout=stereo:sample_rate=48000,atrim=0:" + tailStr + ",asetpts=PTS-STARTPTS,aresample=48000[sil];" +
                    "[ho][sil]concat=n=2:v=0:a=1[aout]";
                var args0 = "-y -i \"" + hookMp3Path + "\"" +
                            " -filter_complex \"" + filter0 + "\"" +
                            " -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";
                log?.Invoke($"[VideoReup] FFmpeg: hook ({hookTargetSec:0.##}s) + im lặng ({musicTailSec:0.##}s) → {totalSec:0.##}s.");
                await RunFfmpegAsync(ffmpegExe, args0, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            var volStr = ReupVideoSpec.MusicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var pitch = RemixScramblerService.GetMusicPitchFilterChain();
            var fadeDur = Math.Min(2d, Math.Max(0.25d, musicTailSec * 0.45d));
            var fadeOutStart = Math.Max(0d, musicTailSec - fadeDur);
            var fadeOutStartStr = fadeOutStart.ToString("0.#####", CultureInfo.InvariantCulture);
            var fadeDurStr = fadeDur.ToString("0.#####", CultureInfo.InvariantCulture);

            // Pha 1 (hookUsed giây): hook voice + nhạc ducked 20%
            // Pha 2 (musicTailSec giây): nhạc từ vị trí hookUsed trong file → pad im lặng nếu file ngắn hơn
            // Tổng = hookUsed + musicTailSec = videoOutSeconds ✓
            var filter =
                "[0:a]atrim=0:" + hookStr + ",aresample=48000,volume=1.0[vo];" +
                "[1:a]atrim=0:" + hookStr + ",asetpts=PTS-STARTPTS," + pitch + ",volume=0.2[bedduck];" +
                "[vo][bedduck]amix=inputs=2:duration=first:dropout_transition=0[hookmix];" +
                // Pha 2: nhạc tiếp tục từ vị trí hookStr → totalStr trong file nhạc
                "[1:a]atrim=" + hookStr + ":" + totalStr + ",asetpts=PTS-STARTPTS," + pitch +
                ",volume=" + volStr + ",afade=t=out:st=" + fadeOutStartStr + ":d=" + fadeDurStr + "[rawbgtail];" +
                // Pad im lặng đủ tailStr để đảm bảo pha 2 luôn = musicTailSec (dù file nhạc ngắn hơn)
                "anullsrc=channel_layout=stereo:sample_rate=48000,atrim=0:" + tailStr + ",asetpts=PTS-STARTPTS,aresample=48000[sil];" +
                "[rawbgtail][sil]amix=inputs=2:duration=longest:dropout_transition=2[bgtail];" +
                "[hookmix][bgtail]concat=n=2:v=0:a=1[aout]";

            var args = "-y -i \"" + hookMp3Path + "\" -i \"" + musicMp3Path + "\"" +
                       " -filter_complex \"" + filter + "\"" +
                       " -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";

            log?.Invoke($"[VideoReup] FFmpeg: hook ({hookTargetSec:0.##}s) + nhạc ducked + tail ({musicTailSec:0.##}s) → tổng {totalSec:0.##}s.");
            await RunFfmpegAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task PrepareNarrationWavAsync(
            string ffmpegExe,
            string narrationMp3Path,
            string outputWav,
            double maxSeconds,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var rawWav = outputWav + ".raw.wav";
            var toWav = "-y -i \"" + narrationMp3Path + "\" -ar 48000 -ac 2 \"" + rawWav + "\"";
            await RunFfmpegAsync(ffmpegExe, toWav, log, cancellationToken).ConfigureAwait(false);

            var dur = await ProbeMediaDurationSecondsAsync(ffmpegExe, rawWav, cancellationToken).ConfigureAwait(false);
            var maxStr = maxSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            string filter;
            if (dur > maxSeconds + 0.15d)
            {
                var tempo = Math.Min(1.35d, dur / maxSeconds);
                var tempoStr = tempo.ToString("0.#####", CultureInfo.InvariantCulture);
                filter = "atempo=" + tempoStr + ",atrim=0:" + maxStr + ",asetpts=PTS-STARTPTS,aresample=48000";
                log?.Invoke($"[VideoReup] Thuyết minh dài {dur:0.##}s → tăng tốc x{tempo:0.##} rồi cắt {maxSeconds:0.##}s.");
            }
            else if (dur < maxSeconds - 0.2d)
            {
                var samples = (int)Math.Round(maxSeconds * 48000d, MidpointRounding.AwayFromZero);
                filter = "apad=whole_len=" + samples.ToString(CultureInfo.InvariantCulture) + ",atrim=0:" + maxStr +
                         ",asetpts=PTS-STARTPTS,aresample=48000";
            }
            else
            {
                filter = "atrim=0:" + maxStr + ",asetpts=PTS-STARTPTS,aresample=48000";
            }

            var args = "-y -i \"" + rawWav + "\" -af \"" + filter + "\" \"" + outputWav + "\"";
            await RunFfmpegAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(rawWav))
                {
                    File.Delete(rawWav);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static async Task BuildNarrationModeCompositeAudioAsync(
            string ffmpegExe,
            string hookWavPath,
            string narrationWavPath,
            string musicMp3Path,
            double hookUsedSec,
            double videoOutSeconds,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var voiceOnly = Path.Combine(Path.GetDirectoryName(outputWav) ?? ".", "voice_hook_narration_tmp.wav");
            await ConcatHookWithTailAudioAsync(
                ffmpegExe,
                hookWavPath,
                narrationWavPath,
                hookUsedSec,
                videoOutSeconds,
                voiceOnly,
                log,
                cancellationToken,
                tailIsTimelineSegment: false).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(musicMp3Path) || !File.Exists(musicMp3Path))
            {
                if (File.Exists(outputWav))
                {
                    File.Delete(outputWav);
                }

                File.Copy(voiceOnly, outputWav);
                return;
            }

            var vidStr = videoOutSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var volStr = ReupVideoSpec.NarrationMusicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var pitch = RemixScramblerService.GetMusicPitchFilterChain();
            var filter =
                "[0:a]atrim=0:" + vidStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=1.0[voice];" +
                "[1:a]atrim=0:" + vidStr + ",asetpts=PTS-STARTPTS," + pitch + ",volume=" + volStr + "[bed];" +
                "[voice][bed]amix=inputs=2:duration=first:dropout_transition=2[aout]";
            var args = "-y -i \"" + voiceOnly + "\" -i \"" + musicMp3Path + "\"" +
                       " -filter_complex \"" + filter + "\"" +
                       " -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";
            log?.Invoke("[VideoReup] FFmpeg: trộn nhạc nền ~" + (ReupVideoSpec.NarrationMusicBedVolume * 100d).ToString("0", CultureInfo.InvariantCulture) + "% dưới giọng thuyết minh…");
            await RunFfmpegAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task BuildFilmCompositeAudioAsync(
            string ffmpegExe,
            string sourceVideoPath,
            string hookWavPath,
            string stageFolder,
            double hookUsedSec,
            double segmentDurationSec,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (segmentDurationSec <= 0.2d)
            {
                throw new InvalidOperationException("Đoạn video sau cắt quá ngắn — không ghép được tiếng gốc.");
            }

            var origTrim = Path.Combine(stageFolder, "film_orig_trim.wav");
            try
            {
                if (File.Exists(origTrim))
                {
                    File.Delete(origTrim);
                }
            }
            catch
            {
                // ignored
            }

            var trimT = segmentDurationSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var ss = ReupVideoSpec.TrimHeadSeconds.ToString("0.#####", CultureInfo.InvariantCulture);

            if (!MediaFileHasAudioStream(ffmpegExe, sourceVideoPath))
            {
                log?.Invoke("[VideoReup] Video nguồn không có track âm thanh — ghép hook + im lặng body.");
                await BuildHookOnlyOrSilenceTailAudioAsync(
                    ffmpegExe,
                    hookWavPath,
                    hookUsedSec,
                    hookUsedSec + segmentDurationSec,
                    outputWav,
                    log,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var extractArgs = "-y -ss " + ss + " -i \"" + sourceVideoPath + "\" -t " + trimT +
                              " -map 0:a:0 -vn -ac 2 -ar 48000 -c:a pcm_s16le \"" + origTrim + "\"";
            log?.Invoke("[VideoReup] FFmpeg: trích audio gốc từ source.mp4 (cùng -ss/-t với pipeline video)...");
            await RunFfmpegAsync(ffmpegExe, extractArgs, log, cancellationToken).ConfigureAwait(false);

            await ConcatHookWithTailAudioAsync(
                ffmpegExe,
                hookWavPath,
                origTrim,
                hookUsedSec,
                hookUsedSec + segmentDurationSec,
                outputWav,
                log,
                cancellationToken,
                tailIsTimelineSegment: false).ConfigureAwait(false);
        }

        private static async Task BuildHookOnlyOrSilenceTailAudioAsync(
            string ffmpegExe,
            string hookWavPath,
            double hookUsedSec,
            double segmentDurationSec,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var endStr = segmentDurationSec.ToString("0.#####", CultureInfo.InvariantCulture);
            if (hookUsedSec >= segmentDurationSec - 0.08d)
            {
                var hookOnly = "-y -i \"" + hookWavPath + "\" -af \"atrim=0:" + endStr +
                               ",asetpts=PTS-STARTPTS,aresample=48000,aformat=sample_fmts=s16:channel_layouts=stereo\" \"" +
                               outputWav + "\"";
                log?.Invoke("[VideoReup] FFmpeg: track audio chỉ hook (gần khớp hết đoạn video).");
                await RunFfmpegAsync(ffmpegExe, hookOnly, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            var hu = hookUsedSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var tailSec = (segmentDurationSec - hookUsedSec).ToString("0.#####", CultureInfo.InvariantCulture);
            const string Fmt = "sample_fmts=s16:channel_layouts=stereo";
            var filter =
                "[0:a]atrim=0:" + hu + ",asetpts=PTS-STARTPTS,aresample=48000,aformat=" + Fmt + "[h0];" +
                "anullsrc=channel_layout=stereo:sample_rate=48000,atrim=0:" + tailSec + ",asetpts=PTS-STARTPTS,aformat=" + Fmt + "[sil];" +
                "[h0][sil]concat=n=2:v=0:a=1[aout]";
            var args = "-y -i \"" + hookWavPath + "\"" +
                       " -filter_complex \"" + filter + "\"" +
                       " -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";
            log?.Invoke("[VideoReup] FFmpeg: concat hook + im lặng (đoạn còn lại) → full_audio.wav");
            await RunFfmpegAsync(ffmpegExe, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task ConcatHookWithTailAudioAsync(
            string ffmpegExe,
            string hookWavPath,
            string tailWavPath,
            double hookUsedSec,
            double segmentDurationSec,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken,
            bool tailIsTimelineSegment = true)
        {
            var endStr = segmentDurationSec.ToString("0.#####", CultureInfo.InvariantCulture);
            if (hookUsedSec >= segmentDurationSec - 0.08d)
            {
                var hookOnly = "-y -i \"" + hookWavPath + "\" -af \"atrim=0:" + endStr +
                               ",asetpts=PTS-STARTPTS,aresample=48000,aformat=sample_fmts=s16:channel_layouts=stereo\" \"" +
                               outputWav + "\"";
                log?.Invoke("[VideoReup] FFmpeg: track audio chỉ hook (gần khớp hết đoạn video).");
                await RunFfmpegAsync(ffmpegExe, hookOnly, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            var hu = hookUsedSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var tailSlotSec = segmentDurationSec - hookUsedSec;
            var tailTrimEnd = tailIsTimelineSegment
                ? endStr
                : tailSlotSec.ToString("0.#####", CultureInfo.InvariantCulture);
            var tailTrimStart = tailIsTimelineSegment ? hu : "0";
            const string Fmt = "sample_fmts=s16:channel_layouts=stereo";
            var filter =
                "[0:a]atrim=0:" + hu + ",asetpts=PTS-STARTPTS,aresample=48000,aformat=" + Fmt + "[h0];" +
                "[1:a]atrim=" + tailTrimStart + ":" + tailTrimEnd + ",asetpts=PTS-STARTPTS,aresample=48000,aformat=" + Fmt + "[r0];" +
                "[h0][r0]concat=n=2:v=0:a=1[aout]";
            var concatArgs = "-y -i \"" + hookWavPath + "\" -i \"" + tailWavPath + "\"" +
                             " -filter_complex \"" + filter + "\"" +
                             " -map \"[aout]\" -c:a pcm_s16le \"" + outputWav + "\"";
            log?.Invoke(tailIsTimelineSegment
                ? "[VideoReup] FFmpeg: concat hook + tiếng gốc (đoạn còn lại) → full_audio.wav"
                : $"[VideoReup] FFmpeg: concat hook ({hookUsedSec:0.##}s) + thuyết minh từ đầu script ({tailSlotSec:0.##}s) → full_audio.wav");
            await RunFfmpegAsync(ffmpegExe, concatArgs, log, cancellationToken).ConfigureAwait(false);
        }

        private static string ResolveFilmAudioSourcePath(VideoReupRowItem row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(row.ReupStageFolder))
            {
                var canonical = Path.Combine(row.ReupStageFolder, StageSourceMp4);
                if (File.Exists(canonical))
                {
                    return canonical;
                }
            }

            return row.ReupDownloadedVideoPath ?? string.Empty;
        }

        private static string ResolveFfmpegPath(AppSettings settings)
        {
            if (FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
            {
                return toolkit.FfmpegExe;
            }

            var bundled = FfmpegToolkitService.GetBundledFfmpegPath();
            if (File.Exists(bundled))
            {
                return bundled;
            }

            var p = (settings?.FfmpegPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                return p;
            }

            return bundled;
        }

        private static string ResolveFfprobePath(string ffmpegPath)
        {
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                return "ffprobe";
            }

            try
            {
                if (ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var probe = ffmpegPath.Substring(0, ffmpegPath.Length - "ffmpeg.exe".Length) + "ffprobe.exe";
                    if (File.Exists(probe))
                    {
                        return probe;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return "ffprobe";
        }

        private static bool MediaFileHasAudioStream(string ffmpegPath, string mediaPath)
        {
            if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
            {
                return false;
            }

            var ffprobe = ResolveFfprobePath(ffmpegPath);
            var args = "-v error -select_streams a:0 -show_entries stream=codec_type -of default=nw=1:nk=1 \"" + mediaPath + "\"";
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            })
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEnd();
                ProcessCancellationHelper.WaitForExit(process, 30000, CancellationToken.None);
                return process.ExitCode == 0 && stdout.Trim().Length > 0;
            }
        }

        private static async Task<double> ProbeMediaDurationSecondsAsync(string ffmpegPath, string mediaPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
            {
                throw new FileNotFoundException("Không tìm thấy file media để đo thời lượng.", mediaPath ?? string.Empty);
            }

            var ffprobe = ResolveFfprobePath(ffmpegPath);
            if (!string.Equals(ffprobe, "ffprobe", StringComparison.OrdinalIgnoreCase) && !File.Exists(ffprobe))
            {
                throw new InvalidOperationException(
                    "Không tìm thấy ffprobe.exe — cấu hình FFmpeg Path trỏ tới ffmpeg.exe (ffprobe.exe cùng thư mục).");
            }

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

            using (var process = new Process { StartInfo = psi })
            {
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Không chạy được ffprobe (" + ffprobe + "). Cài đặt → FFmpeg Path: chọn ffmpeg.exe (kèm ffprobe.exe).\r\n" +
                        ex.Message,
                        ex);
                }
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 80).ConfigureAwait(false);

                var output = (await outputTask.ConfigureAwait(false) ?? string.Empty).Trim();
                if (double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    return Math.Max(0d, seconds);
                }
            }

            return 0d;
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

        public static Task RunFfmpegPublicAsync(
            string ffmpegExecutable,
            string args,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            return RunFfmpegAsync(ffmpegExecutable, args, log, cancellationToken);
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
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Không chạy được ffmpeg (" + ffmpegExecutable + "). Kiểm tra FFmpeg Path trong Cài đặt.\r\n" + ex.Message,
                        ex);
                }
                var stdOut = process.StandardOutput.ReadToEndAsync();
                var stdErr = process.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken).ConfigureAwait(false);

                var outText = await stdOut.ConfigureAwait(false);
                var errText = await stdErr.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "FFmpeg lỗi: " + (string.IsNullOrWhiteSpace(errText) ? outText : errText));
                }

                if (!string.IsNullOrWhiteSpace(errText))
                {
                    var line = errText.Split('\n').LastOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        log?.Invoke("FFmpeg: " + line.Trim());
                    }
                }
            }
        }
    }
}
