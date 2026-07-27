using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Hook/script theo 5 phong cách — chọn khi render hoặc ngẫu nhiên/theo hook.</summary>
    public static class VideoReupStyleVariants
    {
        public const string RandomHookStyleLabel = "(Ngẫu nhiên)";
        public const string FollowHookScriptStyleLabel = "(Theo hook)";

        public static bool HasHookVariants(VideoReupRowItem row)
        {
            return CountNonEmpty(row?.ReupHookByStyle) > 0;
        }

        public static bool HasScriptVariants(VideoReupRowItem row)
        {
            return CountNonEmpty(row?.ReupScriptByStyle) > 0;
        }

        public static bool HasResolvableHook(VideoReupRowItem row)
        {
            if (row == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace((row.ReupHookDraft ?? string.Empty).Trim()))
            {
                return true;
            }

            return HasHookVariants(row);
        }

        public static bool HasResolvableScript(VideoReupRowItem row)
        {
            if (row == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace((row.ReupNarrationScript ?? string.Empty).Trim()))
            {
                return true;
            }

            return HasScriptVariants(row);
        }

        public static void ClearVariants(VideoReupRowItem row)
        {
            if (row == null)
            {
                return;
            }

            row.ReupHookByStyle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            row.ReupScriptByStyle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            row.SelectedHookStyleKey = string.Empty;
            row.SelectedScriptStyleKey = string.Empty;
        }

        public static void SetHookVariants(VideoReupRowItem row, IReadOnlyDictionary<string, string> hooksByStyle)
        {
            if (row == null)
            {
                return;
            }

            row.ReupHookByStyle = NormalizeMap(hooksByStyle);
            row.SelectedHookStyleKey = string.Empty;
            row.ReupHookDraft = string.Empty;
        }

        public static void SetVariants(
            VideoReupRowItem row,
            IReadOnlyDictionary<string, string> hooksByStyle,
            IReadOnlyDictionary<string, string> scriptsByStyle)
        {
            if (row == null)
            {
                return;
            }

            row.ReupHookByStyle = NormalizeMap(hooksByStyle);
            row.ReupScriptByStyle = NormalizeMap(scriptsByStyle);
            row.SelectedHookStyleKey = string.Empty;
            row.SelectedScriptStyleKey = string.Empty;
            row.ReupHookDraft = string.Empty;
            row.ReupNarrationScript = string.Empty;
        }

        /// <summary>
        /// Gán hook/script + clip style trước voiceover/render.
        /// Hook style rỗng → ngẫu nhiên; script style rỗng → theo hook.
        /// </summary>
        public static void ApplyActiveSelections(VideoReupRowItem row, AppSettings settings, Action<string> log = null)
        {
            if (row == null)
            {
                return;
            }

            if (!HasHookVariants(row) && !HasScriptVariants(row))
            {
                if (string.IsNullOrWhiteSpace(row.HookStyleKey))
                {
                    VideoReupRemixService.SeedHookStyleKeyIfEmpty(row, settings);
                }

                return;
            }

            var hookStyle = NormalizeStyleKey(row.SelectedHookStyleKey);
            if (string.IsNullOrEmpty(hookStyle))
            {
                hookStyle = HookStyleCatalog.PickRandomStyleKey(settings, row.ProfileName);
                log?.Invoke("[VideoReup] Hook style: ngẫu nhiên → "
                             + HookStyleCatalog.GetDisplayName(hookStyle) + ".");
            }

            var scriptStyle = NormalizeStyleKey(row.SelectedScriptStyleKey);
            if (string.IsNullOrEmpty(scriptStyle))
            {
                scriptStyle = hookStyle;
                log?.Invoke("[VideoReup] Script style: theo hook → "
                             + HookStyleCatalog.GetDisplayName(scriptStyle) + ".");
            }

            row.HookStyleKey = hookStyle;
            row.ReupHookIntroVideoPath = string.Empty;
            row.ReupHookIntroDurationSec = null;
            row.HookStockClipPath = string.Empty;

            if (TryGetVariant(row.ReupHookByStyle, hookStyle, out var hookText))
            {
                row.ReupHookDraft = hookText;
            }

            if (TryGetVariant(row.ReupScriptByStyle, scriptStyle, out var scriptText))
            {
                row.ReupNarrationScript = scriptText;
            }
        }

        public static string GetHookGridPreview(VideoReupRowItem row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            if (HasHookVariants(row))
            {
                var selected = NormalizeStyleKey(row.SelectedHookStyleKey);
                if (!string.IsNullOrEmpty(selected)
                    && TryGetVariant(row.ReupHookByStyle, selected, out var picked))
                {
                    return "[" + HookStyleCatalog.GetDisplayName(selected) + "] " + picked;
                }

                return "5 phong cách — bấm chọn…";
            }

            return row.ReupHookDraft ?? string.Empty;
        }

        public static string GetScriptGridPreview(VideoReupRowItem row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            if (HasScriptVariants(row))
            {
                var hookStyle = NormalizeStyleKey(row.SelectedHookStyleKey);
                var scriptStyle = NormalizeStyleKey(row.SelectedScriptStyleKey);
                if (string.IsNullOrEmpty(scriptStyle))
                {
                    scriptStyle = hookStyle;
                }

                if (!string.IsNullOrEmpty(scriptStyle)
                    && TryGetVariant(row.ReupScriptByStyle, scriptStyle, out var picked))
                {
                    var label = string.IsNullOrEmpty(NormalizeStyleKey(row.SelectedScriptStyleKey))
                        ? HookStyleCatalog.GetDisplayName(scriptStyle) + " (theo hook)"
                        : HookStyleCatalog.GetDisplayName(scriptStyle);
                    return "[" + label + "] " + picked;
                }

                return "5 phong cách — bấm chọn…";
            }

            return row.ReupNarrationScript ?? string.Empty;
        }

        public static bool TryGetVariant(
            IReadOnlyDictionary<string, string> map,
            string styleKey,
            out string text)
        {
            text = string.Empty;
            var key = NormalizeStyleKey(styleKey);
            if (string.IsNullOrEmpty(key) || map == null)
            {
                return false;
            }

            if (map.TryGetValue(key, out var direct) && !string.IsNullOrWhiteSpace(direct))
            {
                text = direct.Trim();
                return true;
            }

            foreach (var kv in map)
            {
                if (string.Equals(NormalizeStyleKey(kv.Key), key, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kv.Value))
                {
                    text = kv.Value.Trim();
                    return true;
                }
            }

            return false;
        }

        public static Dictionary<string, string> CloneMap(IReadOnlyDictionary<string, string> source)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return map;
            }

            foreach (var key in HookStyleCatalog.AllStyleKeys)
            {
                if (TryGetVariant(source, key, out var text))
                {
                    map[key] = text;
                }
            }

            return map;
        }

        public static string NormalizeStyleKey(string styleKey)
        {
            var key = (styleKey ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (HookStyleCatalog.AllStyleKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                return key;
            }

            foreach (var kv in HookStyleCatalog.StyleDisplayNames)
            {
                if (string.Equals(kv.Value, styleKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return kv.Key;
                }
            }

            return string.Empty;
        }

        private static Dictionary<string, string> NormalizeMap(IReadOnlyDictionary<string, string> source)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return map;
            }

            foreach (var kv in source)
            {
                var key = NormalizeStyleKey(kv.Key);
                var value = (kv.Value ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    map[key] = value;
                }
            }

            return map;
        }

        private static int CountNonEmpty(IReadOnlyDictionary<string, string> map)
        {
            if (map == null)
            {
                return 0;
            }

            return map.Values.Count(v => !string.IsNullOrWhiteSpace(v));
        }
    }
}
