using System.Windows.Forms;

namespace tiktok_Omni.Helpers
{
    public static class UiConfirmHelper
    {
        /// <summary>Hỏi xác nhận trước khi xóa n dòng trên lưới.</summary>
        public static bool ConfirmDeleteRows(IWin32Window owner, int count)
        {
            if (count <= 0)
            {
                return false;
            }

            var message = count == 1
                ? "Bạn có chắc chắn muốn xóa 1 dòng đã chọn?"
                : $"Bạn có chắc chắn muốn xóa {count} dòng đã chọn?";

            return MessageBox.Show(
                       owner,
                       message,
                       "Xoá dòng đã chọn",
                       MessageBoxButtons.YesNo,
                       MessageBoxIcon.Question,
                       MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
    }
}
