using System.Collections.Generic;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private AvatarIdentityPackConfig _mascotIdentityPackConfig = new AvatarIdentityPackConfig();

        private void LoadMascotIdentityPackForSelectedProfile()
        {
            var profile = GetSelectedMascotProfileName();
            _mascotIdentityPackConfig = AvatarIdentityPackStore.LoadOrCreate(profile);
            var files = AvatarIdentityPackStore.SyncIdentityImagesFromVault(profile);
            if (files.Count >= 3)
            {
                _mascotIdentityPackConfig.IdentityImagePaths = files;
            }
        }

        private void SaveMascotIdentityPackConfig()
        {
            var profile = GetSelectedMascotProfileName();
            if (_mascotIdentityPackConfig == null)
            {
                _mascotIdentityPackConfig = new AvatarIdentityPackConfig();
            }

            _mascotIdentityPackConfig.IdentityImagePaths =
                AvatarIdentityPackStore.SyncIdentityImagesFromVault(profile);
            AvatarIdentityPackStore.Save(profile, _mascotIdentityPackConfig);
        }

        private AvatarIdentityPackConfig BuildMascotLipSyncPackForEnqueue()
        {
            SaveMascotIdentityPackConfig();
            return _mascotIdentityPackConfig ?? new AvatarIdentityPackConfig();
        }

        private void SyncMascotIdentityPackUi(string profile, List<string> files)
        {
            // Legacy UI removed — identity pack loaded via AvatarVault batch tab.
        }

        private void SyncMouthPathsToUi()
        {
            // Legacy lip-sync UI removed.
        }
    }
}
