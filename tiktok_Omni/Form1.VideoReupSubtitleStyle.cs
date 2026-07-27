using System;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void ShowReupSubtitleStyleEditor(VideoReupRowItem row, int gridRowIndex)
        {
            if (row == null)
            {
                return;
            }

            AppSettings defaults;
            try
            {
                defaults = _configManager.LoadAsync().ConfigureAwait(true).GetAwaiter().GetResult();
            }
            catch
            {
                defaults = new AppSettings();
            }

            ReupSubtitleStyleHelper.EnsureRowDefaults(row, defaults);
            using (var dlg = new ReupSubtitleStyleEditorForm(row, defaults))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }
            }

            _videoReupDraftDirty = true;
            _videoReupBindingList?.ResetBindings();
            if (dgvVideoReupInput != null && gridRowIndex >= 0 && gridRowIndex < dgvVideoReupInput.Rows.Count)
            {
                dgvVideoReupInput.InvalidateRow(gridRowIndex);
            }
        }

        private void EnsureVideoReupRowSubtitleDefaults(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return;
            }

            ReupSubtitleStyleHelper.EnsureRowDefaults(row, settings ?? new AppSettings());
        }
    }
}
