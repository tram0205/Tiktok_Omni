using System;



namespace tiktok_Omni.Services

{

    public static class TtsAvailabilityHelper

    {

        public static bool IsElevenLabsConfigured(AppSettings settings)

        {

            return !string.IsNullOrWhiteSpace(settings?.TtsApiKey)

                   && !string.IsNullOrWhiteSpace(settings?.TtsEndpoint)

                   && VideoService.IsElevenLabsEndpoint(settings.TtsEndpoint);

        }



        public static bool IsAnyShowcaseTtsConfigured(AppSettings settings)

        {

            if (IsElevenLabsConfigured(settings))

            {

                return true;

            }



            return Environment.OSVersion.Platform == PlatformID.Win32NT;

        }



        public static void ValidateEngine(AppSettings settings, TtsEngineKind engine)

        {

            switch (engine)

            {

                case TtsEngineKind.ElevenLabs:

                    if (!IsElevenLabsConfigured(settings))

                    {

                        throw new InvalidOperationException(

                            "Cần cấu hình TTS ElevenLabs (API Key + URL elevenlabs.io) trong tab Cài đặt.");

                    }



                    return;

                case TtsEngineKind.EdgeTts:

                    if (Environment.OSVersion.Platform != PlatformID.Win32NT)

                    {

                        throw new InvalidOperationException("Edge TTS cần Windows và kết nối mạng.");

                    }



                    return;

                default:

                    throw new ArgumentOutOfRangeException(nameof(engine), engine, null);

            }

        }

    }

}

