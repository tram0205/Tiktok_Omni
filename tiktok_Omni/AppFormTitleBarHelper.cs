using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace tiktok_Omni
{
    /// <summary>Windows 10/11 — tô thanh tiêu đề (non-client) cho dialog tối.</summary>
    internal static class AppFormTitleBarHelper
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void ApplyShowcaseAudioDialogTitleBar(Form form)
        {
            Apply(
                form,
                caption: Color.FromArgb(62, 54, 88),
                captionText: Color.FromArgb(235, 238, 245));
        }

        private static void Apply(Form form, Color caption, Color captionText)
        {
            if (form == null)
            {
                return;
            }

            void ApplyCore()
            {
                if (form.IsDisposed || !form.IsHandleCreated)
                {
                    return;
                }

                try
                {
                    var dark = 1;
                    DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
                    DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeLegacy, ref dark, sizeof(int));

                    var captionColor = ToDwmColor(caption);
                    DwmSetWindowAttribute(form.Handle, DwmwaCaptionColor, ref captionColor, sizeof(int));
                    var textColor = ToDwmColor(captionText);
                    DwmSetWindowAttribute(form.Handle, DwmwaTextColor, ref textColor, sizeof(int));
                }
                catch
                {
                    // Windows cũ hoặc DWM không hỗ trợ — bỏ qua.
                }
            }

            if (form.IsHandleCreated)
            {
                ApplyCore();
            }
            else
            {
                form.HandleCreated += (_, __) => ApplyCore();
            }
        }

        private static int ToDwmColor(Color color) =>
            color.R | (color.G << 8) | (color.B << 16);
    }
}
