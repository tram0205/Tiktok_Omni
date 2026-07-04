using System.Windows.Forms;
using tiktok_Omni.Controls;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private AiVideoGenControls _aiVideoGenControls;

        private void InitializeAiVideoGenToolbarControls()
        {
            _aiVideoGenControls = new AiVideoGenControls();
            _aiVideoGenControls.BindHost(this);

            pnlSlideshowActionBar = _aiVideoGenControls.SlideshowActionBar;
            flpSlideshowData = _aiVideoGenControls.SlideshowDataFlow;
            flpSlideshowExecute = _aiVideoGenControls.SlideshowExecuteFlow;
            flpAffiliateDeepHeaderActions = _aiVideoGenControls.AffiliateDeepHeaderActions;
        }

        private Button btnGenerateGeminiPrompt => _aiVideoGenControls?.GenerateGeminiPromptButton;

        private Button btnReviewScriptBeforeRender => _aiVideoGenControls?.ReviewScriptBeforeRenderButton;

        private Button btnAffiliateGenerateScript => _aiVideoGenControls?.AffiliateGenerateScriptButton;

        private Button btnAffiliateEditScript => _aiVideoGenControls?.AffiliateEditScriptButton;

        private Button btnAffiliateBatchPipeline => _aiVideoGenControls?.AffiliateBatchPipelineButton;

        private Button btnCopyAiVideoPrompt => _aiVideoGenControls?.CopyAiVideoPromptButton;

        private Button btnSaveAiVideoPrompt => _aiVideoGenControls?.SaveAiVideoPromptButton;

        private Button btnProcessVideo => _aiVideoGenControls?.ProcessVideoButton;

        private Button btnOpenOutputFolder => _aiVideoGenControls?.OpenOutputFolderButton;

        private Button btnSlideshowOpenApproval => _aiVideoGenControls?.SlideshowOpenApprovalButton;

        private Button btnRunAffiliateDeepVideo => _aiVideoGenControls?.RunAffiliateDeepVideoButton;

        private Button btnDeepGenerateScript => _aiVideoGenControls?.DeepGenerateScriptButton;

        private Button btnDeepEditScript => _aiVideoGenControls?.DeepEditScriptButton;

        private Button btnAffiliateDeepOpenOutput => _aiVideoGenControls?.AffiliateDeepOpenOutputButton;

        private ComboBox cbGeminiStyleTemplate => _aiVideoGenControls?.GeminiStyleTemplateCombo;

        private CheckBox chkUseMultiVoiceNarration => _aiVideoGenControls?.UseMultiVoiceNarrationCheckBox;

        private NumericUpDown numAiTextSize => _aiVideoGenControls?.TextSizeNumeric;

        private NumericUpDown numAiMusicVolume => _aiVideoGenControls?.MusicVolumeNumeric;

        private NumericUpDown numAiTransitionDuration => _aiVideoGenControls?.TransitionDurationNumeric;
    }
}
