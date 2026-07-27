using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void ShowReupColorGradeEditor(VideoReupRowItem row, int gridRowIndex)
        {
            if (row == null)
            {
                return;
            }

            AppSettings settings;
            try
            {
                settings = Task.Run(() => _configManager.LoadAsync()).GetAwaiter().GetResult();
            }
            catch
            {
                settings = new AppSettings();
            }

            ReupColorGradeHelper.EnsureRowDefaults(row, settings);
            using (var dlg = new ReupColorGradeEditorForm(row, settings))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }
            }

            row.ReupColorGradeLabel = ReupColorGradeHelper.FormatStyleSummary(row);
            _videoReupDraftDirty = true;
            _videoReupBindingList?.ResetBindings();
            if (gridRowIndex >= 0 && dgvVideoReupInput?.Rows.Count > gridRowIndex)
            {
                dgvVideoReupInput.InvalidateRow(gridRowIndex);
            }
        }
    }
}
