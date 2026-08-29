using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Thư mục con trong kho clip quay tay theo loại SP (vd. ao-dai/hoc-sinh).</summary>
    public static class ShowcaseCtaBrollSubLibraryPresets
    {
        public const string AoDaiParentId = "ao-dai";

        public sealed class SubLibraryPreset
        {
            public SubLibraryPreset(string id, string displayLabel)
            {
                Id = id ?? string.Empty;
                DisplayLabel = displayLabel ?? string.Empty;
            }

            public string Id { get; }

            public string DisplayLabel { get; }

            public override string ToString() => DisplayLabel;
        }

        private static readonly SubLibraryPreset AoDaiRoot = new SubLibraryPreset(string.Empty, "— Gốc —");

        private static readonly SubLibraryPreset[] AoDaiSubLibraries =
        {
            AoDaiRoot,
            new SubLibraryPreset("hoc-sinh", "Học sinh"),
            new SubLibraryPreset("cach-tan", "Cách tân"),
            new SubLibraryPreset("mau-tron", "Màu trơn"),
            new SubLibraryPreset("giao-vien", "Giáo viên")
        };

        public static IReadOnlyList<SubLibraryPreset> ForParent(string parentLibraryId)
        {
            var parent = ShowcaseCtaBrollLibraryService.NormalizeLibraryPath(parentLibraryId);
            if (string.Equals(parent, AoDaiParentId, StringComparison.Ordinal))
            {
                return AoDaiSubLibraries;
            }

            return Array.Empty<SubLibraryPreset>();
        }

        public static bool HasSubLibraries(string parentLibraryId) => ForParent(parentLibraryId).Count > 0;

        public static string NormalizeSubId(string subLibraryId) =>
            ShowcaseCtaBrollLibraryService.NormalizeLibrarySegment(subLibraryId);

        public static string GetDisplayLabel(string parentLibraryId, string subLibraryId)
        {
            var parent = ShowcaseCtaBrollLibraryService.NormalizeLibraryPath(parentLibraryId);
            var sub = NormalizeSubId(subLibraryId);
            if (sub.Length == 0)
            {
                return string.Empty;
            }

            foreach (var preset in ForParent(parent))
            {
                if (string.Equals(preset.Id, sub, StringComparison.Ordinal))
                {
                    return preset.DisplayLabel;
                }
            }

            return sub;
        }

        public static void EnsureDefaultFolders(string parentLibraryId)
        {
            var parent = ShowcaseCtaBrollLibraryService.NormalizeLibraryPath(parentLibraryId);
            if (!string.Equals(parent, AoDaiParentId, StringComparison.Ordinal))
            {
                return;
            }

            ShowcaseCtaBrollLibraryService.GetLibraryDirectory(parent, true);
            foreach (var preset in AoDaiSubLibraries)
            {
                if (preset.Id.Length == 0)
                {
                    continue;
                }

                ShowcaseCtaBrollLibraryService.GetLibraryDirectory(
                    ShowcaseCtaBrollLibraryService.CombineLibraryPath(parent, preset.Id),
                    true);
            }
        }
    }
}
