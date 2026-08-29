using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Preset zoom Ken Burns — Gemini chọn <c>zoom_style</c>, FFmpeg map trong <see cref="ShowcaseZoomClipService"/>.</summary>
    public static class ShowcaseZoomStyleCatalog
    {
        public const string PushIn = "push_in";
        public const string PullOut = "pull_out";
        public const string PanLeft = "pan_left";
        public const string PanRight = "pan_right";
        public const string PanUp = "pan_up";
        public const string PanDown = "pan_down";
        public const string Drift = "drift";

        public const string DefaultId = Drift;

        private static readonly string[] ValidIds =
        {
            PushIn, PullOut, PanLeft, PanRight, PanUp, PanDown, Drift
        };

        public static IReadOnlyList<string> ValidStyleIds => ValidIds;

        /// <summary>Chuỗi liệt kê cho prompt Gemini.</summary>
        public static string BuildGeminiStyleList() =>
            string.Join(", ", ValidIds.Select(id => "'" + id + "'"));

        public static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var s = raw.Trim().ToLowerInvariant()
                .Replace(' ', '_')
                .Replace('-', '_');

            if (s == "pushin" || s == "zoom_in" || s == "zoomin")
            {
                return PushIn;
            }

            if (s == "pullout" || s == "zoom_out" || s == "zoomout")
            {
                return PullOut;
            }

            if (s == "panleft" || s == "left")
            {
                return PanLeft;
            }

            if (s == "panright" || s == "right")
            {
                return PanRight;
            }

            if (s == "panup" || s == "up")
            {
                return PanUp;
            }

            if (s == "pandown" || s == "down")
            {
                return PanDown;
            }

            return ValidIds.Contains(s) ? s : string.Empty;
        }

        /// <summary>Ưu tiên zoom_style từ JSON; không hợp lệ thì suy từ zoom_hint; cuối cùng theo loại ảnh.</summary>
        public static string ResolveStyleId(string zoomStyleFromGemini, string zoomHint, string imageKind, int sceneOrderIndex)
        {
            var fromField = NormalizeId(zoomStyleFromGemini);
            if (!string.IsNullOrEmpty(fromField))
            {
                return fromField;
            }

            var fromHint = InferFromZoomHint(zoomHint);
            if (!string.IsNullOrEmpty(fromHint))
            {
                return fromHint;
            }

            if (string.Equals(imageKind, ShowcaseClipToolHelper.KindOnModel, StringComparison.Ordinal))
            {
                return sceneOrderIndex % 2 == 0 ? PushIn : Drift;
            }

            return sceneOrderIndex % 2 == 0 ? PushIn : PullOut;
        }

        public static string InferFromZoomHint(string zoomHint)
        {
            var h = (zoomHint ?? string.Empty).Trim().ToLowerInvariant();
            if (h.Length == 0)
            {
                return string.Empty;
            }

            if (h.Contains("pull") || h.Contains("pull-back") || h.Contains("pull back") || h.Contains("zoom out"))
            {
                return PullOut;
            }

            if (h.Contains("pan left") || h.Contains("pan-left") || h.Contains("left pan"))
            {
                return PanLeft;
            }

            if (h.Contains("pan right") || h.Contains("pan-right") || h.Contains("right pan"))
            {
                return PanRight;
            }

            if (h.Contains("pan up") || h.Contains("tilt up") || h.Contains("upward"))
            {
                return PanUp;
            }

            if (h.Contains("pan down") || h.Contains("tilt down") || h.Contains("downward"))
            {
                return PanDown;
            }

            if (h.Contains("drift") || h.Contains("parallax") || h.Contains("gentle pan"))
            {
                return Drift;
            }

            if (h.Contains("push") || h.Contains("push-in") || h.Contains("push in") || h.Contains("zoom in"))
            {
                return PushIn;
            }

            return string.Empty;
        }

        public static string BuildVideoFilter(double clipDurationSeconds, int clipIndex, string zoomStyleId)
        {
            return BuildVideoFilter(
                clipDurationSeconds,
                clipIndex,
                zoomStyleId,
                ShowcaseOutputAspectPresets.Vertical9x16);
        }

        public static string BuildVideoFilter(
            double clipDurationSeconds,
            int clipIndex,
            string zoomStyleId,
            ShowcaseOutputAspectPreset canvas)
        {
            return BuildVideoFilter(
                clipDurationSeconds,
                clipIndex,
                zoomStyleId,
                canvas,
                ShowcaseZoomSpeedCatalog.DefaultId);
        }

        public static string BuildVideoFilter(
            double clipDurationSeconds,
            int clipIndex,
            string zoomStyleId,
            ShowcaseOutputAspectPreset canvas,
            string zoomSpeedId,
            ShowcaseZoomAspectFitMode aspectFitMode = ShowcaseZoomAspectFitMode.Crop)
        {
            canvas = canvas ?? ShowcaseOutputAspectPresets.Vertical9x16;
            var w = canvas.Width;
            var h = canvas.Height;
            var wText = w.ToString(CultureInfo.InvariantCulture);
            var hText = h.ToString(CultureInfo.InvariantCulture);

            var style = NormalizeId(zoomStyleId);
            if (string.IsNullOrEmpty(style))
            {
                style = DefaultId;
            }

            var safeDuration = Math.Max(2.0d, clipDurationSeconds);
            var durationText = safeDuration.ToString("0.00", CultureInfo.InvariantCulture);
            var (delta, panTravel) = ShowcaseZoomSpeedCatalog.GetMotionParameters(zoomSpeedId, clipIndex);
            var deltaText = delta.ToString("0.00", CultureInfo.InvariantCulture);
            var progress = $"min(1\\,max(0\\,t/{durationText}))";

            string scaleExpr;
            string scaleExprY;
            string panX;
            string panY;

            var deltaPan = (delta * 0.92d).ToString("0.00", CultureInfo.InvariantCulture);
            var zoomFactor = $"1+{deltaText}*{progress}";
            var zoomFactorPullOut = $"1+{deltaText}*(1-{progress})";
            var zoomFactorPan = $"1+{deltaPan}*{progress}";

            switch (style)
            {
                case PullOut:
                    scaleExpr = $"iw*({zoomFactorPullOut})";
                    scaleExprY = $"ih*({zoomFactorPullOut})";
                    panX = "(in_w-out_w)/2";
                    panY = "(in_h-out_h)/2";
                    break;
                case PanLeft:
                    scaleExpr = $"iw*({zoomFactorPan})";
                    scaleExprY = $"ih*({zoomFactorPan})";
                    panX = $"(in_w-out_w)/2 + ((in_w-out_w)/{panTravel})*{progress}";
                    panY = "(in_h-out_h)/2";
                    break;
                case PanRight:
                    scaleExpr = $"iw*({zoomFactorPan})";
                    scaleExprY = $"ih*({zoomFactorPan})";
                    panX = $"(in_w-out_w)/2 - ((in_w-out_w)/{panTravel})*{progress}";
                    panY = "(in_h-out_h)/2";
                    break;
                case PanUp:
                    scaleExpr = $"iw*({zoomFactorPan})";
                    scaleExprY = $"ih*({zoomFactorPan})";
                    panX = "(in_w-out_w)/2";
                    panY = $"(in_h-out_h)/2 + ((in_h-out_h)/{panTravel})*{progress}";
                    break;
                case PanDown:
                    scaleExpr = $"iw*({zoomFactorPan})";
                    scaleExprY = $"ih*({zoomFactorPan})";
                    panX = "(in_w-out_w)/2";
                    panY = $"(in_h-out_h)/2 - ((in_h-out_h)/{panTravel})*{progress}";
                    break;
                case Drift:
                    scaleExpr = clipIndex % 2 == 0
                        ? $"iw*({zoomFactor})"
                        : $"iw*({zoomFactorPullOut})";
                    scaleExprY = clipIndex % 2 == 0
                        ? $"ih*({zoomFactor})"
                        : $"ih*({zoomFactorPullOut})";
                    panX = $"(in_w-out_w)/2 + ((in_w-out_w)/5)*sin(2*PI*t/{durationText})";
                    panY = $"(in_h-out_h)/2 + ((in_h-out_h)/6)*cos(2*PI*t/{durationText})";
                    break;
                case PushIn:
                default:
                    scaleExpr = $"iw*({zoomFactor})";
                    scaleExprY = $"ih*({zoomFactor})";
                    panX = "(in_w-out_w)/2";
                    panY = "(in_h-out_h)/2";
                    break;
            }

            var kenBurns = $"scale='{scaleExpr}':'{scaleExprY}':eval=frame," +
                           $"crop={wText}:{hText}:x='{panX}':y='{panY}',setsar=1";

            if (aspectFitMode == ShowcaseZoomAspectFitMode.BlurPad)
            {
                return ShowcaseZoomAspectFitHelper.BuildBlurPadCompositePrefix(w, h) + kenBurns;
            }

            return ShowcaseOutputAspectPresets.FormatScaleIncrease(w, h) + "," +
                   ShowcaseOutputAspectPresets.FormatScaleCrop(w, h) + ",setsar=1," +
                   kenBurns;
        }
    }
}
