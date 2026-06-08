using System;
using System.IO;
using Newtonsoft.Json;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private bool CheckAssetIntegrity(OmniJob job, out string errorMessage)
        {
            return AssetIntegrityService.CheckOmniJob(job, out errorMessage);
        }

        private bool CheckAssetIntegrity(ApprovalQueueItem item, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (item == null)
            {
                errorMessage = "Mục duyệt null.";
                return false;
            }

            if (item.JobType == ApprovalJobType.AutoPost)
            {
                try
                {
                    var payload = JsonConvert.DeserializeObject<AutoPostApprovalPayload>(item.PayloadJson ?? "{}")
                                  ?? new AutoPostApprovalPayload();
                    var videoPath = (payload.VideoFilePath ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(videoPath))
                    {
                        return AssetIntegrityService.CheckVideoFile(videoPath, out errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Payload Auto Post lỗi: " + ex.Message;
                    return false;
                }

                return true;
            }

            var path = ExtractVideoPathFromApprovalItem(item);
            if (string.IsNullOrWhiteSpace(path))
            {
                path = AssetIntegrityService.TryExtractVideoPathFromApprovalPayload(item);
            }

            if (!AssetIntegrityService.CheckVideoFile(path, out errorMessage))
            {
                return false;
            }

            if (item.JobType == ApprovalJobType.VideoReup)
            {
                try
                {
                    var payload = JsonConvert.DeserializeObject<VideoReupJobPayload>(item.PayloadJson ?? "{}");
                    var row = payload?.Row;
                    if (row != null && row.UseVisualHookSfx)
                    {
                        var sfx = (row.VisualHookSfxPath ?? payload.VisualHookSfxPath ?? string.Empty).Trim();
                        if (!AssetIntegrityService.CheckAudioFile(sfx, "Hook SFX 3s", out errorMessage))
                        {
                            return false;
                        }
                    }
                    else if (row != null)
                    {
                        var hookWav = (row.ReupHookAudioPath ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(hookWav)
                            && !AssetIntegrityService.CheckAudioFile(hookWav, "Hook voiceover", out errorMessage))
                        {
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Kiểm tra Reup: " + ex.Message;
                    return false;
                }
            }

            return true;
        }
    }
}
