using System;

using System.Collections.Generic;

using System.Linq;



namespace tiktok_Omni.Services.Showcase

{

    public sealed class ShowcaseClipModePreset

    {

        public ShowcaseClipModePreset(string id, string displayLabel, string promptHint)

        {

            Id = id ?? string.Empty;

            DisplayLabel = displayLabel ?? string.Empty;

            PromptHint = promptHint ?? string.Empty;

        }



        public string Id { get; }



        public string DisplayLabel { get; }



        public string PromptHint { get; }



        public override string ToString() => DisplayLabel;

    }



    /// <summary>Chế độ tạo clip Showcase — flatlay dùng Veo; on-model dùng Zoom hoặc Kling tùy preset.</summary>

    public static class ShowcaseClipModePresets

    {

        public const string DefaultId = "veo_zoom";

        public const string ZoomOnlyId = "zoom_only";

        public const string VeoOnlyId = "veo_only";

        public const string KlingOnlyId = "kling_only";

        public const string ZoomKlingId = "zoom_kling";

        public const string GeminiSuggestId = "gemini_suggest";
        public const string KlingVeoZoomId = "kling_veo_zoom";



        public static ShowcaseClipModePreset ZoomOnly { get; } =

            new ShowcaseClipModePreset(

                ZoomOnlyId,

                "Chỉ Zoom",

                "Mọi cảnh → Zoom Ken Burns trong app (FFmpeg). Không Veo/Kling — bấm «Tạo clip Zoom» sau Gemini.");



        public static ShowcaseClipModePreset VeoZoom { get; } =

            new ShowcaseClipModePreset(

                "veo_zoom",

                "Veo + Zoom",

                "Flatlay → Veo I2V. On-model → Zoom Ken Burns trong app (không qua Flow).");



        public static ShowcaseClipModePreset VeoKling { get; } =

            new ShowcaseClipModePreset(

                "veo_kling",

                "Veo + Kling",

                "Flatlay → Veo I2V. On-model → Kling I2V (có chuyển động người, tốn credit).");



        public static ShowcaseClipModePreset ZoomKling { get; } =

            new ShowcaseClipModePreset(

                ZoomKlingId,

                "Zoom + Kling",

                "Flatlay → Zoom Ken Burns trong app. On-model → Kling I2V — không dùng Veo.");



        public static ShowcaseClipModePreset VeoOnly { get; } =

            new ShowcaseClipModePreset(

                VeoOnlyId,

                "Chỉ Veo",

                "Mọi cảnh → Veo I2V (on-model dùng prompt Flow-safe). Không Zoom/Kling trong app.");



        public static ShowcaseClipModePreset KlingOnly { get; } =

            new ShowcaseClipModePreset(

                KlingOnlyId,

                "Chỉ Kling",

                "Mọi cảnh → Kling I2V. Không Veo, không Zoom Ken Burns.");



        public static ShowcaseClipModePreset GeminiSuggest { get; } =

            new ShowcaseClipModePreset(

                GeminiSuggestId,

                "Gemini gợi ý",

                "Gemini tự chọn clip_tool từng cảnh (veo, zoom hoặc kling) theo ảnh và mạch quảng cáo — linh hoạt nhất.");



        public static ShowcaseClipModePreset KlingVeoZoom { get; } =

            new ShowcaseClipModePreset(

                KlingVeoZoomId,

                "Kling + Veo + Zoom",

                "Gemini chọn clip_tool từng cảnh trong veo, kling và zoom (Ken Burns) — dùng đủ ba công cụ khi phù hợp ảnh/mạch.");



        /// <summary>Id cũ <c>custom</c> — dùng <see cref="GeminiSuggestId"/>.</summary>
        public static ShowcaseClipModePreset Custom => GeminiSuggest;



        public static IReadOnlyList<ShowcaseClipModePreset> All { get; } = new[]

        {

            VeoZoom,

            VeoKling,

            ZoomKling,

            VeoOnly,

            KlingOnly,

            ZoomOnly,

            KlingVeoZoom,

            GeminiSuggest

        };



        public static string GetDisplayLabel(string clipModeId)

        {

            var preset = FindById(clipModeId);

            return preset?.DisplayLabel ?? VeoZoom.DisplayLabel;

        }



        public static string ResolveIdForGemini(string clipModeId)

        {

            var normalized = NormalizeId(clipModeId);

            return FindById(normalized) != null ? normalized : DefaultId;

        }



        public static string ResolvePromptForGemini(string clipModeId)

        {

            var preset = FindById(ResolveIdForGemini(clipModeId));

            return preset?.PromptHint ?? VeoZoom.PromptHint;

        }



        /// <summary>Chế độ có thể tạo clip Zoom Ken Burns trong app (FFmpeg).</summary>

        public static bool ModeAllowsInAppZoom(string clipModeId)

        {

            var mode = ResolveIdForGemini(clipModeId);

            if (string.Equals(mode, ZoomOnlyId, StringComparison.Ordinal))

            {

                return true;

            }

            if (string.Equals(mode, ZoomKlingId, StringComparison.Ordinal))

            {

                return true;

            }



            if (string.Equals(mode, "veo_zoom", StringComparison.Ordinal))

            {

                return true;

            }



            return string.Equals(mode, GeminiSuggestId, StringComparison.Ordinal)

                || string.Equals(mode, KlingVeoZoomId, StringComparison.Ordinal);

        }



        public static bool IsGeminiSuggestMode(string clipModeId)

        {

            var mode = ResolveIdForGemini(clipModeId);

            return string.Equals(mode, GeminiSuggestId, StringComparison.Ordinal)

                || string.Equals(mode, KlingVeoZoomId, StringComparison.Ordinal);

        }



        private static ShowcaseClipModePreset FindById(string clipModeId)

        {

            var normalized = NormalizeId(clipModeId);

            return All.FirstOrDefault(p => string.Equals(p.Id, normalized, StringComparison.Ordinal));

        }



        private static string NormalizeId(string clipModeId)

        {

            var s = (clipModeId ?? string.Empty).Trim().ToLowerInvariant();

            if (s == "veo-zoom" || s == "veo zoom")

            {

                return "veo_zoom";

            }



            if (s == "veo-kling" || s == "veo kling")

            {

                return "veo_kling";

            }

            if (s == "zoom_kling" || s == "zoom-kling" || s == "zoom kling" || s == "zoom+kling")

            {

                return ZoomKlingId;

            }

            if (s == "veo_only" || s == "veo-only" || s == "chi_veo" || s == "chỉ veo" || s == "chi veo" || s == "only_veo")

            {

                return VeoOnlyId;

            }

            if (s == "kling_only" || s == "kling-only" || s == "chi_kling" || s == "chỉ kling" || s == "chi kling" || s == "only_kling")

            {

                return KlingOnlyId;

            }

            if (s == "zoom_only" || s == "zoom-only" || s == "chi_zoom" || s == "all_zoom" || s == "zoom only" || s == "chỉ zoom")

            {

                return ZoomOnlyId;

            }



            if (s == "veo-kling-zoom" || s == "veo_kling_zoom" || s == "kling+veo+zoom" || s == "kling + veo + zoom"

                || s == "kling_veo_zoom" || s == "kling-veo-zoom" || s == "kling veo zoom")

            {

                return KlingVeoZoomId;

            }

            if (s == "custom" || s == "veo / zoom / kling")

            {

                return GeminiSuggestId;

            }

            if (s == "gemini_suggest" || s == "gemini-suggest" || s == "gemini suggest"

                || s == "gemini_goi_y" || s == "gemini gợi ý" || s == "gemini goi y" || s == "gemini gợi y")

            {

                return GeminiSuggestId;

            }



            return s;

        }

    }

}


