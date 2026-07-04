using System.Threading.Tasks;
using tiktok_Omni.Services;

namespace tiktok_Omni.Controls
{
    /// <summary>Form1 implements this so <see cref="AiVideoGenControls"/> can delegate service work.</summary>
    public interface IAiVideoGenControlsHost
    {
        Task GenerateGeminiPromptAsync();

        Task ReviewScriptBeforeRenderAsync();

        Task GenerateAffiliateScriptAsync();

        Task EditAffiliateScriptAsync();

        Task RunBatchPipelineAsync();

        void CopyAiVideoPrompt();

        void SaveAiVideoPrompt();

        Task ProcessSlideshowVideoAsync();

        Task OpenSlideshowOutputFolderAsync();

        Task OpenAffiliateDeepOutputFolderAsync();

        void OpenApprovalQueue();

        void ClearActiveGrid();

        Task RunAffiliateDeepVideoAsync();

        Task SaveGeminiStyleTemplateAsync(GeminiStyleTemplate template);
    }
}
