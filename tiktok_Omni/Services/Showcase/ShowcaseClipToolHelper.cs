using System;



namespace tiktok_Omni.Services.Showcase

{

    public static class ShowcaseClipToolHelper

    {

        public const string ToolVeo = "veo";

        public const string ToolZoom = "zoom";

        public const string ToolKling = "kling";



        public const string KindFlatlay = "flatlay";

        public const string KindOnModel = "on_model";



        public static string NormalizeImageKind(string raw, string veoPromptFallback = null)

        {

            var s = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");

            if (s == "flatlay" || s == "flat_lay" || s == "flat")

            {

                return KindFlatlay;

            }



            if (s == "on_model" || s == "onmodel" || s == "model")

            {

                return KindOnModel;

            }



            var prompt = (veoPromptFallback ?? string.Empty).Trim();

            if (prompt.StartsWith("ON-MODEL", StringComparison.OrdinalIgnoreCase))

            {

                return KindOnModel;

            }



            if (prompt.StartsWith("FLATLAY", StringComparison.OrdinalIgnoreCase))

            {

                return KindFlatlay;

            }



            return KindFlatlay;

        }



        public static string NormalizeClipTool(string raw)

        {

            var s = (raw ?? string.Empty).Trim().ToLowerInvariant();

            if (s == ToolVeo || s == ToolZoom || s == ToolKling)

            {

                return s;

            }



            return string.Empty;

        }



        public static string ResolveDefaultTool(string clipModeId, string imageKind)

        {

            var mode = ShowcaseClipModePresets.ResolveIdForGemini(clipModeId);

            if (string.Equals(mode, ShowcaseClipModePresets.ZoomOnlyId, StringComparison.Ordinal))

            {

                return ToolZoom;

            }

            if (string.Equals(mode, ShowcaseClipModePresets.VeoOnlyId, StringComparison.Ordinal))

            {

                return ToolVeo;

            }

            if (string.Equals(mode, ShowcaseClipModePresets.KlingOnlyId, StringComparison.Ordinal))

            {

                return ToolKling;

            }

            var flatlay = string.Equals(NormalizeImageKind(imageKind), KindFlatlay, StringComparison.Ordinal);

            switch (mode)

            {

                case ShowcaseClipModePresets.ZoomKlingId:

                    return flatlay ? ToolZoom : ToolKling;

                case "veo_kling":

                    return flatlay ? ToolVeo : ToolKling;

                case "custom":
                case "gemini_suggest":
                case "kling_veo_zoom":

                    return flatlay ? ToolVeo : ToolZoom;

                case "veo_zoom":

                default:

                    return flatlay ? ToolVeo : ToolZoom;

            }

        }



        public static string EnforceToolForMode(string clipModeId, string imageKind, string suggestedTool)

        {

            var mode = ShowcaseClipModePresets.ResolveIdForGemini(clipModeId);

            if (string.Equals(mode, ShowcaseClipModePresets.ZoomOnlyId, StringComparison.Ordinal))

            {

                return ToolZoom;

            }

            if (string.Equals(mode, ShowcaseClipModePresets.VeoOnlyId, StringComparison.Ordinal))

            {

                return ToolVeo;

            }

            if (string.Equals(mode, ShowcaseClipModePresets.KlingOnlyId, StringComparison.Ordinal))

            {

                return ToolKling;

            }

            if (string.Equals(mode, ShowcaseClipModePresets.ZoomKlingId, StringComparison.Ordinal)
                || string.Equals(mode, "veo_kling", StringComparison.Ordinal))

            {

                return ResolveDefaultTool(mode, imageKind);

            }

            if (ShowcaseClipModePresets.IsPerSceneToolChoiceMode(mode))

            {

                var tool = NormalizeClipTool(suggestedTool);

                if (string.IsNullOrEmpty(tool))

                {

                    tool = ResolveDefaultTool(mode, imageKind);

                }

                return ApplyMandatoryToolRules(mode, imageKind, tool);

            }



            return ResolveDefaultTool(mode, imageKind);

        }



        /// <summary>Quy tắc cấm: flatlay không Kling; on-model không Veo — còn lại theo chế độ clip.</summary>

        public static string ApplyMandatoryToolRules(string clipModeId, string imageKind, string tool)

        {

            var mode = ShowcaseClipModePresets.ResolveIdForGemini(clipModeId);

            var flatlay = string.Equals(NormalizeImageKind(imageKind), KindFlatlay, StringComparison.Ordinal);

            var t = NormalizeClipTool(tool);

            if (string.IsNullOrEmpty(t))

            {

                t = ResolveDefaultTool(mode, imageKind);

            }



            if (flatlay && string.Equals(t, ToolKling, StringComparison.Ordinal))

            {

                t = ShowcaseClipModePresets.ModeAllowsInAppZoom(mode) ? ToolZoom : ToolVeo;

            }



            if (!flatlay && string.Equals(t, ToolVeo, StringComparison.Ordinal))

            {

                if (string.Equals(mode, "veo_kling", StringComparison.Ordinal)

                    || string.Equals(mode, ShowcaseClipModePresets.KlingOnlyId, StringComparison.Ordinal))

                {

                    t = ToolKling;

                }

                else

                {

                    t = ToolZoom;

                }

            }



            if (string.Equals(mode, "veo_zoom", StringComparison.Ordinal))

            {

                if (flatlay)

                {

                    return string.Equals(t, ToolZoom, StringComparison.Ordinal) ? ToolZoom : ToolVeo;

                }

                return ToolZoom;

            }



            if (string.Equals(mode, "veo_kling", StringComparison.Ordinal))

            {

                return flatlay ? ToolVeo : ToolKling;

            }



            if (string.Equals(mode, ShowcaseClipModePresets.ZoomKlingId, StringComparison.Ordinal))

            {

                return flatlay ? ToolZoom : ToolKling;

            }



            if (string.Equals(mode, ShowcaseClipModePresets.KlingVeoZoomId, StringComparison.Ordinal)

                || string.Equals(mode, ShowcaseClipModePresets.GeminiSuggestId, StringComparison.Ordinal))

            {

                if (flatlay)

                {

                    return string.Equals(t, ToolZoom, StringComparison.Ordinal) ? ToolZoom : ToolVeo;

                }

                return string.Equals(t, ToolKling, StringComparison.Ordinal) ? ToolKling : ToolZoom;

            }



            return t;

        }



        public static string GetToolDisplayLabel(string clipTool)

        {

            switch (NormalizeClipTool(clipTool))

            {

                case ToolVeo:

                    return "Veo";

                case ToolZoom:

                    return "Zoom";

                case ToolKling:

                    return "Kling";

                default:

                    return "?";

            }

        }



        public static string GetImageKindDisplayLabel(string imageKind)

        {

            return string.Equals(NormalizeImageKind(imageKind), KindOnModel, StringComparison.Ordinal)

                ? "On-model"

                : "Flatlay";

        }



        public static string ResolveScenePrompt(AiVideoGenInputItem scene)

        {

            if (scene == null)

            {

                return string.Empty;

            }



            switch (NormalizeClipTool(scene.ShowcaseClipTool))

            {

                case ToolKling:

                    return scene.KlingPrompt ?? string.Empty;

                case ToolZoom:

                    return scene.ZoomHint ?? string.Empty;

                default:

                    return scene.VeoPrompt ?? string.Empty;

            }

        }



        public static bool SceneHasClipPrompt(AiVideoGenInputItem scene)

        {

            return !string.IsNullOrWhiteSpace(ResolveScenePrompt(scene));

        }

    }

}


