using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null
#pragma warning disable CS0169 // The field is never used

        private Button btnRunMascotChannelPipeline;
        private PictureBox pbMascotMouthMarker;
        private Label lblMascotMouthCoord;
        private Button btnRunMascotProduction;
        private Button btnBrowseMascotImage;
        private TextBox txtAvatarIdentityPack;
        private Button btnSelectAvatarIdentityPack;
        private Button btnPreviewMascotVariants;
        private PictureBox pbMascotPreview1;
        private PictureBox pbMascotPreview2;
        private PictureBox pbMascotPreview3;
        private PictureBox pbMascotPreview4;
        private ContextMenuStrip cmsMascotPreview;
        private ToolStripMenuItem miRegenerateScene;
        private CheckBox chkMascotUseLipSync;
        private TextBox txtMouthClosedPath;
        private TextBox txtMouthOpenSmallPath;
        private TextBox txtMouthOpenPath;
        private Button btnBrowseMouthClosed;
        private Button btnBrowseMouthOpenSmall;
        private Button btnBrowseMouthOpen;
        private Button btnPreviewLipSyncOverlay;
        private Panel pnlLipSyncPreview;
        private PictureBox pbLipSyncBase;
        private Panel pnlMouthMarker;
        private Label lblMouthOverlayCoords;

#pragma warning restore CS0649
#pragma warning restore CS0169

        private int _selectedMascotPreviewSceneIndex = -1;
    }
}
