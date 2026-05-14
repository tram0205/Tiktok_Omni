using System;
using System.Net;
using System.Net.Mail;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public class NotificationService
    {
        public async Task SendAsync(
            AppSettings settings,
            NotificationMessage message,
            Action<string> logAction,
            CancellationToken cancellationToken = default)
        {
            if (settings == null || message == null || !settings.NotificationEnabled)
            {
                return;
            }

            var sentAny = false;
            if (settings.NotificationEmailEnabled)
            {
                sentAny = await TrySendEmailAsync(settings, message, logAction, cancellationToken).ConfigureAwait(false) || sentAny;
            }

            if (settings.NotificationWebhookEnabled)
            {
                sentAny = await TrySendWebhookAsync(settings, message, logAction, cancellationToken).ConfigureAwait(false) || sentAny;
            }

            if (!sentAny)
            {
                logAction?.Invoke("[NOTIFY] Notification enabled nhưng chưa cấu hình được kênh hợp lệ (email/webhook).");
            }
        }

        private static async Task<bool> TrySendEmailAsync(
            AppSettings settings,
            NotificationMessage message,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var host = (settings.NotificationSmtpHost ?? string.Empty).Trim();
            var user = (settings.NotificationSmtpUser ?? string.Empty).Trim();
            var pass = (settings.NotificationSmtpPassword ?? string.Empty).Trim();
            var to = (settings.NotificationToEmail ?? string.Empty).Trim();
            var from = (settings.NotificationFromEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(to))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(from))
            {
                from = user;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var client = new SmtpClient(host, settings.NotificationSmtpPort))
                using (var mail = new MailMessage(from, to))
                {
                    client.EnableSsl = settings.NotificationUseSsl;
                    client.Credentials = new NetworkCredential(user, pass);
                    mail.Subject = message.Title;
                    mail.Body = message.Body;
                    mail.IsBodyHtml = false;
                    foreach (var attachmentPath in message.AttachmentPaths ?? new List<string>())
                    {
                        if (string.IsNullOrWhiteSpace(attachmentPath) || !File.Exists(attachmentPath))
                        {
                            continue;
                        }

                        mail.Attachments.Add(new Attachment(attachmentPath));
                    }
                    await client.SendMailAsync(mail).ConfigureAwait(false);
                }

                logAction?.Invoke("[NOTIFY] Email sent: " + to);
                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[NOTIFY] Email send failed: " + ex.Message);
                return false;
            }
        }

        private static async Task<bool> TrySendWebhookAsync(
            AppSettings settings,
            NotificationMessage message,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var webhookUrl = (settings.NotificationWebhookUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                return false;
            }

            try
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    title = message.Title,
                    body = message.Body,
                    eventType = message.EventType,
                    severity = message.Severity,
                    createdAt = DateTime.Now.ToString("O"),
                    profileName = message.ProfileName,
                    attachmentPaths = message.AttachmentPaths
                });
                using (var webClient = new WebClient())
                {
                    webClient.Headers[HttpRequestHeader.ContentType] = "application/json";
                    cancellationToken.Register(() => webClient.CancelAsync());
                    await webClient.UploadDataTaskAsync(new Uri(webhookUrl), "POST", Encoding.UTF8.GetBytes(payload)).ConfigureAwait(false);
                }

                logAction?.Invoke("[NOTIFY] Webhook sent.");
                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke("[NOTIFY] Webhook send failed: " + ex.Message);
                return false;
            }
        }
    }

    public class NotificationMessage
    {
        public string EventType { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public List<string> AttachmentPaths { get; set; } = new List<string>();
    }
}
