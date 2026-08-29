using System.Collections.Generic;
using System.Threading.Tasks;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

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

        /// <summary>Showcase sản phẩm: mở (và tạo nếu chưa có) thư mục clips_render của phiên hiện tại.</summary>
        Task OpenShowcaseClipsFolderAsync();

        /// <summary>Duyệt file video từ máy → copy vào clips_render/scene_XX của phiên video đang chọn.</summary>
        Task ImportShowcaseClipsForVideoAsync(ShowcaseVideoItem video);

        /// <summary>Copy clip từ thư viện quay tay vào clips_render/scene_XX (bảng chờ render).</summary>
        Task AddShowcaseLibraryClipsToRenderQueueAsync(ShowcaseVideoItem video, IList<string> clipPaths);

        /// <summary>Showcase sản phẩm: quét lại thư mục clips_render, cập nhật trạng thái ✓/✗ từng cảnh mà không cần Render.</summary>
        Task RefreshShowcaseClipStatusAsync();

        /// <summary>Showcase: Gemini xem clip phân cảnh (nén) → viết hook/voiceover/CTA khớp hình và chủ đề.</summary>
        Task GenerateShowcaseVoiceoverAsync();

        /// <summary>Showcase: «Tạo lời thoại» cho đúng một dòng video (dialog cột Lời thoại).</summary>
        Task GenerateShowcaseVoiceoverForVideoAsync(ShowcaseVideoItem video);

        /// <summary>Ghi ngay draft Showcase — tránh mất thoại nếu đóng app trước auto-save.</summary>
        void FlushShowcaseDraftToDisk();

        /// <summary>Showcase: tạo narration.mp3 (lần đầu) hoặc tạo lại từ kịch bản hiện tại.</summary>
        Task BuildShowcaseNarrationAsync();

        /// <summary>Showcase: mở nghe narration.mp3 đã tạo (không gọi TTS lại).</summary>
        Task ListenShowcaseNarrationAsync();

        /// <summary>Showcase sản phẩm: thêm một dòng video mới trên lưới (mỗi dòng = 1 video, cảnh nằm trên storyboard).</summary>
        void AddShowcaseVideoRow();

        /// <summary>Showcase sản phẩm: sao chép dòng đang chọn — bản sao thêm ở cuối lưới.</summary>
        void CopyShowcaseVideoRow();

        /// <summary>Showcase sản phẩm: đưa dòng đang chọn lên một vị trí trên lưới.</summary>
        void MoveShowcaseVideoRowUp();

        /// <summary>Showcase sản phẩm: đưa dòng đang chọn xuống một vị trí trên lưới.</summary>
        void MoveShowcaseVideoRowDown();

        /// <summary>Showcase sản phẩm: mở thùng rác — khôi phục dòng đã xóa (giữ 24 giờ).</summary>
        void OpenShowcaseTrash();

        /// <summary>Showcase: quét thư mục phiên trên đĩa → gắn clip/ảnh/kịch bản vào dòng lưới (khi draft mất liên kết).</summary>
        void RestoreShowcaseVideosFromDisk();

        /// <summary>Showcase: quản lý thư viện clip quay tay theo loại SP (Assets\CtaBRolls\).</summary>
        void OpenShowcaseRealClipLibrary();

        /// <summary>Showcase: quản lý nhạc nền dùng chung (Assets\Audio\Music).</summary>
        void OpenShowcaseMusicLibrary();

        /// <summary>Showcase: quản lý hiệu ứng âm thanh (Assets\Audio\Sfx).</summary>
        void OpenShowcaseSfxLibrary();

        /// <summary>Showcase: quản lý logo thương hiệu (Assets\Logos).</summary>
        void OpenShowcaseLogoLibrary();

        /// <summary>Showcase: đẩy dòng đã render sang tab Đăng tự động (TikTok + Facebook + YouTube).</summary>
        void PushShowcaseSelectionToAutoPost();

        /// <summary>Showcase sản phẩm: chọn ảnh trực tiếp từ máy (không cần link sản phẩm) để thêm cảnh mới.</summary>
        Task AddShowcaseImagesFromFilesAsync();

        /// <summary>False khi tab Showcase đang ở trạng thái «Tiếp tục» (đã dừng).</summary>
        bool TryBeginShowcaseTabWork();

        void EndShowcaseTabWork();

        Task HandleShowcaseStopResumeAsync();

        void RefreshShowcaseStopButton();

        /// <summary>True sau khi bấm «Dừng» — workflow bị khóa đến khi «Tiếp tục».</summary>
        bool IsShowcaseTabPaused { get; }
    }
}
