using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
        /// <summary>Chống double-click: tắt nút khi chạy, bật lại trong finally.</summary>
        protected async Task RunButtonActionAsync(Button button, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            if (button != null && !button.Enabled)
            {
                return;
            }

            if (button != null)
            {
                button.Enabled = false;
            }

            try
            {
                await action(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // caller logs if needed
            }
            catch (Exception ex)
            {
                Log("[UI] " + ex.Message);
            }
            finally
            {
                if (button != null && !button.IsDisposed)
                {
                    button.Enabled = true;
                }
            }
        }
    }
}
