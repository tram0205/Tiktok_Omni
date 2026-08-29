namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseCtaBrollLibraryPaths
    {
        public static string ResolveParentLibraryId(ShowcaseVideoItem video) =>
            ShowcaseProductTypePresets.ResolveBrollLibraryId(video?.ShowcaseProductTypePrompt);

        public static string ResolveFullLibraryPath(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return ShowcaseCtaBrollLibraryService.SharedLibraryId;
            }

            var overrideId = ShowcaseCtaBrollLibraryService.NormalizeLibraryPath(video.ShowcaseCtaBrollLibraryId);
            if (overrideId.Length > 0)
            {
                return overrideId;
            }

            var parent = ResolveParentLibraryId(video);
            return ShowcaseCtaBrollLibraryService.CombineLibraryPath(parent, video.ShowcaseCtaBrollSubLibraryId);
        }

        public static string ResolveFullLibraryPath(string productTypePrompt, string subLibraryId)
        {
            var parent = ShowcaseProductTypePresets.ResolveBrollLibraryId(productTypePrompt);
            return ShowcaseCtaBrollLibraryService.CombineLibraryPath(parent, subLibraryId);
        }
    }
}
