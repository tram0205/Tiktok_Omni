namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Số cảnh tối thiểu Showcase — người dùng tự chọn bao nhiêu ảnh/clip cũng được.</summary>
    internal static class ShowcaseWorkflowConstants
    {
        public const int MinScenes = 1;

        public static bool HasEnoughScenes(int sceneCount) => sceneCount >= MinScenes;
    }
}
