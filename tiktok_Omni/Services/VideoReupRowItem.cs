using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>Hàng làm việc cho tab «Video reup» (nguồn Săn Affiliate; remix FFmpeg + Lyria; phụ đề hook trong render).</summary>
    public class VideoReupRowItem
    {
        [Browsable(false)]
        public string SourceKeyword { get; set; } = string.Empty;

        /// <summary>Chrome profile (nick) sở hữu dòng — tránh trộn file giữa các nick.</summary>
        public string ProfileName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        [Browsable(false)]
        public string Price { get; set; } = string.Empty;

        public string VideoUrl { get; set; } = string.Empty;

        [Browsable(false)]
        public string ImageUrl { get; set; } = string.Empty;

        public string Hashtags { get; set; } = string.Empty;
        public string VideoScript { get; set; } = string.Empty;

        /// <summary>Câu hook thoại (Gemini / chỉnh tay) trước khi voiceover — hiển thị/sửa trong bảng Video reup.</summary>
        public string ReupHookDraft { get; set; } = string.Empty;

        /// <summary>Hook theo từng phong cách (boc_phot, fomo, …) — Gemini sinh 5 bản.</summary>
        [Browsable(false)]
        public Dictionary<string, string> ReupHookByStyle { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Script thuyết minh theo từng phong cách.</summary>
        [Browsable(false)]
        public Dictionary<string, string> ReupScriptByStyle { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Phong cách hook người dùng chọn — rỗng = ngẫu nhiên khi render.</summary>
        [Browsable(false)]
        public string SelectedHookStyleKey { get; set; } = string.Empty;

        /// <summary>Phong cách script người dùng chọn — rỗng = theo hook khi render.</summary>
        [Browsable(false)]
        public string SelectedScriptStyleKey { get; set; } = string.Empty;

        /// <summary>Alias pipeline: hook text gửi ElevenLabs.</summary>
        [Browsable(false)]
        public string HookText
        {
            get => ReupHookDraft;
            set => ReupHookDraft = value ?? string.Empty;
        }

        /// <summary>Transcript từ ElevenLabs (hoặc hook text) — dùng tạo phụ đề ASS.</summary>
        [Browsable(false)]
        public string Transcript { get; set; }

        /// <summary>MP3 hook TTS (temp_downloads\hook_audio_*.mp3) trước khi chuẩn hóa WAV.</summary>
        [Browsable(false)]
        public string HookAudioPath { get; set; } = string.Empty;

        /// <summary>Alias pipeline: file audio output từ ElevenLabs.</summary>
        [Browsable(false)]
        public string AudioPath
        {
            get => HookAudioPath;
            set => HookAudioPath = value ?? string.Empty;
        }

        /// <summary>Kiểu phụ đề ASS cho pipeline burn-in.</summary>
        [Browsable(false)]
        public AssSubtitleGeneratorOptions SubtitleStyle { get; set; }

        /// <summary>Gemini gợi ý tên file .mp3 (có thể chọn trong Combo).</summary>
        [Browsable(false)]
        public string ReupSuggestedMusicFile { get; set; } = string.Empty;

        /// <summary>File nhạc nền đã chọn (tên file trong VideoReup\Music, hoặc <see cref="NoMusicSelectionLabel"/>).</summary>
        public string ReupSelectedMusicFile { get; set; } = string.Empty;

        /// <summary>Giá trị combo cột «Nhạc nền» khi không trộn nhạc.</summary>
        public const string NoMusicSelectionLabel = "(Không có nhạc)";

        public static bool IsNoMusicSelection(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(), NoMusicSelectionLabel, StringComparison.Ordinal);
        }

        public static bool HasMusicBedSelected(string value)
        {
            var pick = (value ?? string.Empty).Trim();
            return pick.Length > 0 && !IsNoMusicSelection(pick);
        }

        /// <summary>File SFX hook overlay đã chọn (tên file trong Assets\Audio\Sfx, hoặc <see cref="NoHookSfxSelectionLabel"/>).</summary>
        public string ReupSelectedHookSfxFile { get; set; } = string.Empty;

        /// <summary>Giá trị combo cột «SFX Hook» khi không trộn hiệu ứng.</summary>
        public const string NoHookSfxSelectionLabel = "(Không có SFX)";

        public static bool IsNoHookSfxSelection(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(), NoHookSfxSelectionLabel, StringComparison.Ordinal);
        }

        public static bool HasHookSfxSelected(string value)
        {
            var pick = (value ?? string.Empty).Trim();
            return pick.Length > 0 && !IsNoHookSfxSelection(pick);
        }

        /// <summary>Sau hook: nhạc nền hay giữ tiếng gốc video (chế độ phim).</summary>
        [Browsable(false)]
        public VideoReupAudioMode ReupAudioMode { get; set; } = VideoReupAudioMode.AffiliateBed;

        /// <summary>Thư mục làm việc bền cho từng video (tải nguồn, hook Lyria, wav).</summary>
        [Browsable(false)]
        public string ReupStageFolder { get; set; } = string.Empty;

        /// <summary>Đường dẫn file video nguồn đã tải (thường …\source.mp4 trong stage).</summary>
        [Browsable(false)]
        public string ReupDownloadedVideoPath { get; set; } = string.Empty;

        /// <summary>Alias pipeline: video nguồn trước burn-in.</summary>
        [Browsable(false)]
        public string VideoPath
        {
            get => ReupDownloadedVideoPath;
            set => ReupDownloadedVideoPath = value ?? string.Empty;
        }

        /// <summary>File WAV hook đã chuẩn hóa (sau Lyria + FFmpeg), dùng khi render.</summary>
        [Browsable(false)]
        public string ReupHookAudioPath { get; set; } = string.Empty;

        /// <summary>File MP4 remix gần nhất (cắt, lật, hook Lyria + nhạc).</summary>
        [Browsable(false)]
        public string LastRemixOutputPath { get; set; } = string.Empty;

        /// <summary>Alias pipeline: đích MP4 sau burn-in.</summary>
        [Browsable(false)]
        public string OutputPath
        {
            get => LastRemixOutputPath;
            set => LastRemixOutputPath = value ?? string.Empty;
        }

        /// <summary>Thời lượng video nguồn đã tải (giây) — set sau remix thành công.</summary>
        [Browsable(false)]
        public double? LastSourceVideoDurationSec { get; set; }

        /// <summary>Thời lượng MP4 remix (giây).</summary>
        [Browsable(false)]
        public double? LastRemixOutputVideoDurationSec { get; set; }

        /// <summary>Độ dài hook thoại đã dùng (giây).</summary>
        [Browsable(false)]
        public double? LastHookDurationUsedSec { get; set; }

        /// <summary>Đã hoàn tất pipeline render & đóng gói (dùng tô màu dòng trên lưới).</summary>
        [Browsable(false)]
        public bool IsProcessed { get; set; }

        /// <summary>Trạng thái remix gần nhất (ví dụ: Đang xử lý, Xong, Lỗi).</summary>
        public string RemixStatus { get; set; } = string.Empty;

        /// <summary>Thông báo lỗi remix gần nhất (nếu có).</summary>
        public string RemixLastError { get; set; } = string.Empty;

        /// <summary>Nhãn hiển thị trên lưới — gộp trạng thái và lỗi.</summary>
        public string GetReupStatusDisplayLabel()
        {
            var status = (RemixStatus ?? string.Empty).Trim();
            var error = (RemixLastError ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(status) && string.IsNullOrEmpty(error))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(error))
            {
                return status;
            }

            if (string.IsNullOrEmpty(status))
            {
                return error;
            }

            if (string.Equals(status, error, StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }

            if (status.IndexOf("lỗi", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(status, "Đã hủy", StringComparison.OrdinalIgnoreCase))
            {
                return status + ": " + error;
            }

            return status + " — " + error;
        }

        public const string ReupRemixModeAffiliateBedLabel = "Hook + Nhạc nền";
        public const string ReupRemixModeFilmLabel = "Hook + Phim";
        public const string ReupRemixModeNarrationLabel = "Hook + Thuyết minh";

        /// <summary>Kịch bản thuyết minh (Gemini) — đọc sau hook, giọng ElevenLabs khác hook.</summary>
        public string ReupNarrationScript { get; set; } = string.Empty;

        /// <summary>WAV thuyết minh đã TTS (phần sau hook).</summary>
        [Browsable(false)]
        public string ReupNarrationAudioPath { get; set; } = string.Empty;

        /// <summary>Chế độ remix trên lưới — map tới <see cref="ReupAudioMode"/>.</summary>
        public string ReupMode
        {
            get
            {
                switch (ReupAudioMode)
                {
                    case VideoReupAudioMode.FilmKeepOriginal:
                        return ReupRemixModeFilmLabel;
                    case VideoReupAudioMode.NarrationScript:
                        return ReupRemixModeNarrationLabel;
                    default:
                        return ReupRemixModeAffiliateBedLabel;
                }
            }
            set
            {
                if (string.Equals(value, ReupRemixModeFilmLabel, StringComparison.Ordinal))
                {
                    ReupAudioMode = VideoReupAudioMode.FilmKeepOriginal;
                }
                else if (string.Equals(value, ReupRemixModeNarrationLabel, StringComparison.Ordinal))
                {
                    ReupAudioMode = VideoReupAudioMode.NarrationScript;
                }
                else
                {
                    ReupAudioMode = VideoReupAudioMode.AffiliateBed;
                }
            }
        }

        public bool IsNarrationScriptMode => ReupAudioMode == VideoReupAudioMode.NarrationScript;

        /// <summary>OCR phát hiện chữ trong video nguồn — nếu true thì render KHÔNG lật ngang.</summary>
        [Browsable(false)]
        public bool OcrDetectedText { get; set; }

        /// <summary>Đường dẫn MP3 thuyết minh gốc (trước khi trim/speedup) — dùng để tính slowdown video.</summary>
        [Browsable(false)]
        public string ReupNarrationRawMp3Path { get; set; } = string.Empty;

        /// <summary>Thời lượng TTS thuyết minh tự nhiên (giây) trước khi bị trim/speedup cho khớp slot.</summary>
        [Browsable(false)]
        public double ReupNarrationRawDurationSec { get; set; }

        /// <summary>Bật hook SFX 3s (file âm thanh) thay voiceover hook dài.</summary>
        public bool UseVisualHookSfx { get; set; }

        /// <summary>Đường dẫn file SFX hook (mp3/wav) — tiếng cười, giật mình, …</summary>
        [Browsable(false)]
        public string VisualHookSfxPath { get; set; } = string.Empty;

        /// <summary>Font phụ đề hook (mỗi dòng — bấm cột «Phụ đề» để sửa).</summary>
        [Browsable(false)]
        public string ReupSubtitleFontName { get; set; } = string.Empty;

        [Browsable(false)]
        public int ReupSubtitleFontSize { get; set; }

        [Browsable(false)]
        public string ReupSubtitlePosition { get; set; } = string.Empty;

        [Browsable(false)]
        public string ReupSubtitleAnimation { get; set; } = string.Empty;

        [Browsable(false)]
        public bool ReupSubtitleBold { get; set; } = true;

        [Browsable(false)]
        public bool ReupSubtitleItalic { get; set; }

        [Browsable(false)]
        public int ReupSubtitleWordsPerLine { get; set; }

        /// <summary>Tóm tắt hiển thị trên lưới (cập nhật sau khi chỉnh phụ đề).</summary>
        public string ReupSubtitleStyleLabel { get; set; } = string.Empty;

        /// <summary>Preset chỉnh màu: default | natural | vivid | warm | cool | cinematic | custom.</summary>
        [Browsable(false)]
        public string ReupColorPreset { get; set; } = string.Empty;

        [Browsable(false)]
        public double ReupColorBrightness { get; set; }

        [Browsable(false)]
        public double ReupColorContrast { get; set; }

        [Browsable(false)]
        public double ReupColorSaturation { get; set; }

        [Browsable(false)]
        public double ReupColorGamma { get; set; }

        /// <summary>Tóm tắt filter màu trên lưới — bấm cột «Màu» để sửa.</summary>
        public string ReupColorGradeLabel { get; set; } = string.Empty;

        /// <summary>Ảnh hook AI (Gemini) lưu trong stage — hook_scene.png.</summary>
        [Browsable(false)]
        public string ReupHookIntroImagePath { get; set; } = string.Empty;

        /// <summary>Video intro hook (ảnh/clip + phụ đề karaoke + TTS) — hook_intro.mp4.</summary>
        [Browsable(false)]
        public string ReupHookIntroVideoPath { get; set; } = string.Empty;

        /// <summary>Thời lượng clip hook intro (giây).</summary>
        [Browsable(false)]
        public double? ReupHookIntroDurationSec { get; set; }

        /// <summary>
        /// Phong cách hook video stock (noi_dau | boc_phot | huong_dan | fomo | ke_chuyen).
        /// Rỗng = dùng Gemini sinh ảnh (hành vi cũ).
        /// </summary>
        [Browsable(false)]
        public string HookStyleKey { get; set; } = string.Empty;

        /// <summary>Nhãn "Gemini" khi HookStyleKey rỗng.</summary>
        public const string HookStyleGeminiLabel = "Gemini (ảnh AI)";

        /// <summary>Label hiển thị trên lưới — map qua lại với <see cref="HookStyleKey"/>.</summary>
        [JsonIgnore]
        public string HookStyleDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(HookStyleKey))
                {
                    return HookStyleGeminiLabel;
                }

                if (HookStyleCatalog.StyleDisplayNames.TryGetValue(HookStyleKey, out var name))
                {
                    return name;
                }

                return HookStyleKey;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)
                    || string.Equals(value, HookStyleGeminiLabel, StringComparison.Ordinal))
                {
                    HookStyleKey = string.Empty;
                    return;
                }

                foreach (var kv in HookStyleCatalog.StyleDisplayNames)
                {
                    if (string.Equals(kv.Value, value, StringComparison.Ordinal))
                    {
                        HookStyleKey = kv.Key;
                        return;
                    }
                }

                HookStyleKey = value;
            }
        }

        /// <summary>Đường dẫn clip stock đã chọn cho hook intro — tránh dùng lại clip cũ khi render lại.</summary>
        [Browsable(false)]
        public string HookStockClipPath { get; set; } = string.Empty;
    }
}
