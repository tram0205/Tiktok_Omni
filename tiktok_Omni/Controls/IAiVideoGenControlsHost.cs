using System.Threading.Tasks;
using tiktok_Omni.Services;

namespace tiktok_Omni.Controls
{
    /// <summary>Form1 implements this so <see cref="AiVideoGenControls"/> can delegate service work.</summary>
    public interface IAiVideoGenControlsHost
    {
        Task GenerateGeminiPromptAsync();

        Task ReviewScriptBeforeRenderAsync();

        Task GenerateAffiliateScriptAsync();

        Task EditAffiliateScriptAsync();

        Task RunBatchPipelineAsync();

        void CopyAiVideoPrompt();

        void SaveAiVideoPrompt();

        Task ProcessSlideshowVideoAsync();

        Task OpenSlideshowOutputFolderAsync();

        Task OpenAffiliateDeepOutputFolderAsync();

        void OpenApprovalQueue();

        void ClearActiveGrid();

        Task RunAffiliateDeepVideoAsync();

        /// <summary>Showcase: bảng tóm tắt thoại, cảnh, phụ đề, nhạc, audio trước render thành phẩm.</summary>
        Task PreviewShowcaseOverviewAsync();

        Task SaveGeminiStyleTemplateAsync(GeminiStyleTemplate template);

        /// <summary>Showcase sản phẩm: Gemini xem ảnh storyboard → tự suy chủ đề/thứ tự cảnh → voiceover + prompt Veo từng cảnh.</summary>
        Task GenerateShowcaseSceneScriptAsync();

        /// <summary>Showcase sản phẩm: xuất Excel prompt clip (1 dòng/cảnh) để tạo clip I2V bên ngoài app.</summary>
        Task ExportShowcaseExcelAsync();

        /// <summary>Showcase sản phẩm: tạo clip Zoom Ken Burns trong app cho các cảnh gán công cụ zoom.</summary>
        Task GenerateShowcaseZoomClipsAsync();

        /// <summary>Showcase sản phẩm: mở (và tạo nếu chưa có) thư mục veo_clips của phiên hiện tại.</summary>
        Task OpenShowcaseClipsFolderAsync();

        /// <summary>Showcase sản phẩm: quét lại thư mục veo_clips, cập nhật trạng thái ✓/✗ từng cảnh mà không cần Render.</summary>
        Task RefreshShowcaseClipStatusAsync();

        /// <summary>Showcase: Gemini xem clip phân cảnh (nén) → viết hook/voiceover/CTA khớp hình và chủ đề.</summary>
        Task GenerateShowcaseVoiceoverAsync();

        /// <summary>Showcase: tạo narration.mp3 (lần đầu) hoặc tạo lại từ kịch bản hiện tại.</summary>
        Task BuildShowcaseNarrationAsync();

        /// <summary>Showcase: mở nghe narration.mp3 đã tạo (không gọi TTS lại).</summary>
        Task ListenShowcaseNarrationAsync();

        /// <summary>Showcase sản phẩm: thêm một dòng video mới trên lưới (mỗi dòng = 1 video, cảnh nằm trên storyboard).</summary>
        void AddShowcaseVideoRow();

        /// <summary>Showcase sản phẩm: chọn ảnh trực tiếp từ máy (không cần link sản phẩm) để thêm cảnh mới.</summary>
        Task AddShowcaseImagesFromFilesAsync();

        /// <summary>False khi tab Showcase đang ở trạng thái «Tiếp tục» (đã dừng).</summary>
        bool TryBeginShowcaseTabWork();

        void EndShowcaseTabWork();

        Task HandleShowcaseStopResumeAsync();

        /// <summary>True sau khi bấm «Dừng» — workflow bị khóa đến khi «Tiếp tục».</summary>
        bool IsShowcaseTabPaused { get; }
    }
}
