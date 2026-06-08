using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Chuẩn UTF-8 cho mọi thao tác đọc/ghi file text (log, config, JSON).
    /// </summary>
    internal static class TextFileEncoding
    {
        internal static readonly Encoding Utf8 = Encoding.UTF8;

        /// <summary>UTF-8 không BOM — phù hợp log và JSON.</summary>
        internal static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
