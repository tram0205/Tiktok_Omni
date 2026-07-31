namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Cách xử lý khi tỉ lệ ảnh storyboard ≠ khung video xuất.</summary>
    public enum ShowcaseZoomAspectFitMode
    {
        /// <summary>Phóng to phủ khung rồi cắt (cover) — mặc định hiện tại.</summary>
        Crop = 0,

        /// <summary>Thu nhỏ ảnh giữa khung + nền blur từ chính ảnh.</summary>
        BlurPad = 1
    }
}
