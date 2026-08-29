using System;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services;

class Program
{
    static async Task Main()
    {
        var svc = new EdgeTtsService();
        try
        {
            var path = await svc.SynthesizeToTempMp3Async(
                "Xin chào.",
                new EdgeTtsSynthesisOptions { VoiceShortName = EdgeTtsVoices.HoaiMyNeural, Rate = "+8%" },
                Console.WriteLine,
                CancellationToken.None);
            Console.WriteLine("OK " + path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL " + ex.Message);
            if (ex.InnerException != null)
            {
                Console.WriteLine("  " + ex.InnerException.Message);
            }
        }
    }
}
