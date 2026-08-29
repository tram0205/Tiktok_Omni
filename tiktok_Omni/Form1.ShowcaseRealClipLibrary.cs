using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Controls;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        void IAiVideoGenControlsHost.OpenShowcaseRealClipLibrary()
        {
            OpenShowcaseRealClipLibraryAsync();
        }

        private async void OpenShowcaseRealClipLibraryAsync()
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            using (var dlg = new ShowcaseRealClipLibraryForm(settings))
            {
                dlg.ShowDialog(this);
            }
        }

        void IAiVideoGenControlsHost.OpenShowcaseMusicLibrary()
        {
            OpenShowcaseAudioLibraryAsync(ShowcaseAudioLibraryForm.LibraryKind.BackgroundMusic);
        }

        void IAiVideoGenControlsHost.OpenShowcaseSfxLibrary()
        {
            OpenShowcaseAudioLibraryAsync(ShowcaseAudioLibraryForm.LibraryKind.SoundEffects);
        }

        private async void OpenShowcaseAudioLibraryAsync(ShowcaseAudioLibraryForm.LibraryKind kind)
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            using (var dlg = new ShowcaseAudioLibraryForm(kind, settings))
            {
                dlg.ShowDialog(this);
            }
        }

        void IAiVideoGenControlsHost.OpenShowcaseLogoLibrary()
        {
            OpenShowcaseLogoLibraryAsync();
        }

        private async void OpenShowcaseLogoLibraryAsync()
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(true);
            using (var dlg = new ShowcaseLogoLibraryForm(settings))
            {
                dlg.ShowDialog(this);
            }
        }
    }
}
