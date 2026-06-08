using System.Collections.Generic;

namespace tiktok_Omni.Services.Mascot
{
    public sealed class MascotBatchRow
    {
        public string Profile { get; set; } = "default";
        public string Request { get; set; } = string.Empty;
        public string ExcelPath { get; set; } = string.Empty;
        public string Status { get; set; } = "Cho";
        public string OutputVideoPath { get; set; } = string.Empty;
    }

    public sealed class MascotScene
    {
        public int Order { get; set; }
        public string Voiceover { get; set; } = string.Empty;
        public string ImagePrompt { get; set; } = string.Empty;
    }

    public sealed class MascotSceneGeminiDto
    {
        public int scene { get; set; }
        public string voiceover { get; set; }
        public string image_prompt { get; set; }
    }

    public sealed class MascotBatchRenderResult
    {
        public bool Success { get; set; }
        public string FinalVideoPath { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public IList<string> SceneClipPaths { get; set; } = new List<string>();
    }
}