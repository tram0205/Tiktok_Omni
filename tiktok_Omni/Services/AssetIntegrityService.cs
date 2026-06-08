using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni.Services
{
    public static class AssetIntegrityService
    {
        public static bool CheckOmniJob(OmniJob job, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (job == null)
            {
                errorMessage = "Job null.";
                return false;
            }

            switch (job.Kind)
            {
                case OmniJobKind.AutoPost:
                    return CheckAutoPostPayload(job.PayloadJson, out errorMessage);
                case OmniJobKind.VideoReup:
                    return CheckVideoReupPayload(job.PayloadJson, out errorMessage);
                default:
                    return true;
            }
        }

        public static bool CheckVideoFile(string videoPath, out string errorMessage)
        {
            errorMessage = string.Empty;
            var path = (videoPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "Đường dẫn video trống.";
                return false;
            }

            if (!File.Exists(path))
            {
                errorMessage = "Không tìm thấy file video trên ổ cứng: " + path;
                return false;
            }

            return true;
        }

        public static bool CheckAudioFile(string audioPath, string label, out string errorMessage)
        {
            errorMessage = string.Empty;
            var path = (audioPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = label + ": đường dẫn âm thanh trống.";
                return false;
            }

            if (!File.Exists(path))
            {
                errorMessage = label + ": không tìm thấy file trên ổ cứng — " + path;
                return false;
            }

            return true;
        }

        private static bool CheckAutoPostPayload(string payloadJson, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                var payload = JsonConvert.DeserializeObject<AutoPostJobPayload>(payloadJson ?? "{}")
                              ?? new AutoPostJobPayload();
                var videoPath = (payload.VideoFilePath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(videoPath))
                {
                    return CheckVideoFile(videoPath, out errorMessage);
                }

                var folder = (payload.VideoFolder ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    errorMessage = "Thư mục video Auto Post không tồn tại: " + folder;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Payload Auto Post không hợp lệ: " + ex.Message;
                return false;
            }
        }

        private static bool CheckVideoReupPayload(string payloadJson, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                var payload = JsonConvert.DeserializeObject<VideoReupJobPayload>(payloadJson ?? "{}");
                if (payload?.Row == null)
                {
                    errorMessage = "Payload Video Reup thiếu dòng dữ liệu.";
                    return false;
                }

                var row = payload.Row;
                var source = (row.ReupDownloadedVideoPath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(source) && !File.Exists(source))
                {
                    errorMessage = "Video nguồn Reup không tồn tại: " + source;
                    return false;
                }

                var output = (row.LastRemixOutputPath ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    return CheckVideoFile(output, out errorMessage);
                }

                if (payload.UseVisualHookSfx)
                {
                    if (!CheckAudioFile(payload.VisualHookSfxPath, "Hook SFX 3s", out errorMessage))
                    {
                        return false;
                    }
                }
                else
                {
                    var hookWav = (row.ReupHookAudioPath ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(hookWav) && !File.Exists(hookWav))
                    {
                        errorMessage = "File hook voiceover không tồn tại: " + hookWav;
                        return false;
                    }
                }

                if (row.ReupAudioMode != VideoReupAudioMode.FilmKeepOriginal
                    && !string.IsNullOrWhiteSpace(row.ReupSelectedMusicFile))
                {
                    // Music file validated at render time in stage folder — skip strict path here
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Payload Video Reup không hợp lệ: " + ex.Message;
                return false;
            }
        }

        public static string TryExtractVideoPathFromApprovalPayload(ApprovalQueueItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.PayloadJson))
            {
                return (item?.EditedPreview ?? string.Empty).Trim();
            }

            try
            {
                var jo = JObject.Parse(item.PayloadJson);
                var paths = new[]
                {
                    jo["OutputPath"]?.ToString(),
                    jo["OutputVideoPath"]?.ToString(),
                    jo["VideoFilePath"]?.ToString(),
                    jo["LastRemixOutputPath"]?.ToString()
                };

                foreach (var p in paths)
                {
                    var t = (p ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        return t;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return (item.EditedPreview ?? item.OriginalPreview ?? string.Empty).Trim();
        }
    }
}
