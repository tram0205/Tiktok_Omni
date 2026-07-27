using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using WebSocketSharp;

class Program
{
    const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    const string SecMsGecVersion = "1-143.0.3650.75";
    const string Origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";
    const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";

    static string GenerateSecMsGec()
    {
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 11644473600L;
        ticks -= ticks % 300;
        var winTicks = ticks * 10000000L;
        var str = winTicks.ToString() + TrustedClientToken;
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(str));
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }

    static string MuidValue()
    {
        var bytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("X2"));
        return sb.ToString().ToUpperInvariant();
    }

    static void Main()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        var connectId = Guid.NewGuid().ToString("N");
        var url = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1"
                  + "?TrustedClientToken=" + TrustedClientToken
                  + "&ConnectionId=" + connectId
                  + "&Sec-MS-GEC=" + GenerateSecMsGec()
                  + "&Sec-MS-GEC-Version=" + SecMsGecVersion;

        using (var ws = new WebSocket(url))
        {
            ws.Origin = Origin;
            ws.CustomHeaders = new Dictionary<string, string>
            {
                ["User-Agent"] = UserAgent,
                ["Accept-Language"] = "en-US,en;q=0.9",
                ["Pragma"] = "no-cache",
                ["Cache-Control"] = "no-cache",
                ["Cookie"] = "muid=" + MuidValue() + ";",
                ["Sec-WebSocket-Version"] = "13",
            };
            try
            {
                ws.Connect();
                Console.WriteLine("Sharp OK ready=" + ws.ReadyState);
                ws.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Sharp FAIL: " + ex.Message);
            }
        }
    }
}
