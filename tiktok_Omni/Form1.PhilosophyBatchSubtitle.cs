using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private async void ShowPhilosophyBatchSubtitleStyleEditor(PhilosophyBatchItem batch, int gridRowIndex)
        {
            if (batch == null)
            {
                return;
            }

            var profile = ResolvePhilosophyBatchProfile(batch);
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            ProfileScopedPaths.SetConfiguredStorageRoot(settings.StorageRootPath);

            var video = PhilosophyBatchShowcaseSubtitleAdapter.ToShowcaseVideo(batch, profile);
            ShowcaseSubtitleStyleHelper.EnsureVideoDefaults(video, settings);

            using (var dlg = new ShowcaseSubtitleStyleEditorForm(video, settings, philosophyMode: true))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            PhilosophyBatchShowcaseSubtitleAdapter.ApplyFromShowcaseVideo(video, batch);
            ShowcaseSubtitleStyleHelper.RefreshStyleLabel(video);
            batch.SubtitleStyleLabel = video.ShowcaseSubtitleStyleLabel ?? batch.SubtitleStyleLabel;
            batch.RefreshDerivedFields();
            _philosophyBatchBindingList?.ResetBindings();
            dgvPhilosophyScripts?.InvalidateRow(gridRowIndex);
            NotifyPhilosophyDraftDirty();
        }
    }
}
