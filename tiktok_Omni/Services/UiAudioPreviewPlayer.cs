using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>Phát thử file audio ngắn trong UI (WinForms / MCI).</summary>
    internal static class UiAudioPreviewPlayer
    {
        private const string MciAlias = "omni_ui_audio_preview";

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int MciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        public static void Stop()
        {
            try
            {
                MciSendString($"stop {MciAlias}", null, 0, IntPtr.Zero);
            }
            catch
            {
                // ignored
            }

            try
            {
                MciSendString($"close {MciAlias}", null, 0, IntPtr.Zero);
            }
            catch
            {
                // ignored
            }
        }

        public static bool IsPlaying()
        {
            var sb = new StringBuilder(64);
            if (MciSendString($"status {MciAlias} mode", sb, sb.Capacity, IntPtr.Zero) != 0)
            {
                return false;
            }

            return sb.ToString().IndexOf("playing", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Play(string filePath, int volumePercent = 100)
        {
            Stop();
            var path = (filePath ?? string.Empty).Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                throw new FileNotFoundException("Không tìm thấy file âm thanh.", path);
            }

            volumePercent = Math.Max(0, Math.Min(100, volumePercent));
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var mciType = ext == ".wav" ? "waveaudio" : "mpegvideo";
            var escaped = path.Replace("\"", "\\\"");

            var openCode = MciSendString($"open \"{escaped}\" type {mciType} alias {MciAlias}", null, 0, IntPtr.Zero);
            if (openCode != 0 && ext != ".wav")
            {
                openCode = MciSendString($"open \"{escaped}\" alias {MciAlias}", null, 0, IntPtr.Zero);
            }

            if (openCode != 0)
            {
                throw new InvalidOperationException("Không mở được file âm thanh (MCI " + openCode + ").");
            }

            var mciVolume = (int)Math.Round(volumePercent * 10.0, MidpointRounding.AwayFromZero);
            MciSendString($"setaudio {MciAlias} volume to {mciVolume}", null, 0, IntPtr.Zero);
            var playCode = MciSendString($"play {MciAlias}", null, 0, IntPtr.Zero);
            if (playCode != 0)
            {
                MciSendString($"close {MciAlias}", null, 0, IntPtr.Zero);
                throw new InvalidOperationException("Không phát được âm thanh (MCI " + playCode + ").");
            }
        }
    }
}
