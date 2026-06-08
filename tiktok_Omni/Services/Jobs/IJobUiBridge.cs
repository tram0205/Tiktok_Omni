using System;
using System.Collections.Generic;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Jobs
{
    /// <summary>Cầu nối UI ↔ worker (log, cập nhật lưới affiliate, v.v.).</summary>
    public interface IJobUiBridge
    {
        void Log(string message);
        void OnHuntKeywordStarted(int index, int total, string keyword, string profileName);
        void OnHuntKeywordCompleted(int index, int total, string keyword, string profileName, List<AffiliateCandidate> batch);
        void OnHuntFinished(bool success, string summary);
        void OnRenderProgress(VideoRenderProgress progress);
        void OnRenderFinished(bool success, List<string> outputPaths, string error);
        void OnAutoPostFinished(bool success, string summary, string error);
        void OnPhilosophyProgress(string statusText, int percent);
        void OnPhilosophyFinished(bool success, PhilosophyVideoResult result, string error);
        void OnMascotSceneScriptsReady(List<string> sceneScripts);
        void OnMascotProgress(int percent, string stage);
        void OnMascotFinished(bool success, MascotChannelVideoPipelineResult result, string error);
        void OnAffiliateDeepDiveFinished(Guid jobId, bool success, AffiliateCandidate candidate, VideoDeepAnalysisResult result, string error);
        void OnAffiliateDeepRenderFinished(Guid jobId, bool success, string outputPath, string error);
        void OnAffiliateCategoriesUpdated();
        void OnVideoReupJobFinished(Guid jobId, bool success, string videoUrl, VideoReupRemixResult result, string error);
    }
}
