using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace tiktok_Omni.Services
{
    public static class YtDlpToolResolver
    {
        public static string Resolve(AppSettings settings, Action<string> logAction = null)
        {
            static void AddUnique(List<string> list, string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                var t = path.Trim();
                if (list.Exists(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                list.Add(t);
            }

            var candidates = new List<string>();
            AddUnique(candidates, settings?.YtDlpPath);

            try
            {
                var main = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(main))
                {
                    var dir = Path.GetDirectoryName(main);
                    AddUnique(candidates, Path.Combine(dir ?? string.Empty, "yt-dlp.exe"));
                }
            }
            catch
            {
                // ignored
            }

            AddUnique(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "yt-dlp.exe"));

            try
            {
                var loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var dir = Path.GetDirectoryName(loc);
                    AddUnique(candidates, Path.Combine(dir ?? string.Empty, "yt-dlp.exe"));
                }
            }
            catch
            {
                // ignored
            }

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                {
                    logAction?.Invoke("[yt-dlp] " + c);
                    return c;
                }
            }

            throw new InvalidOperationException(
                "Chưa tìm thấy yt-dlp.exe. Cấu hình trong tab Cài đặt hoặc đặt file cạnh tiktok_Omni.exe.");
        }
    }
}
