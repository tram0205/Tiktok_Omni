using System;



namespace tiktok_Omni.Services

{

    public static class TtsEngineChoiceHelper

    {

        public static TtsEngineKind ParseStoredChoice(string stored)

        {

            stored = (stored ?? string.Empty).Trim();

            if (string.Equals(stored, TtsEngineKind.ElevenLabs.ToString(), StringComparison.OrdinalIgnoreCase))

            {

                return TtsEngineKind.ElevenLabs;

            }



            if (string.Equals(stored, "PiperOffline", StringComparison.OrdinalIgnoreCase))

            {

                return TtsEngineKind.EdgeTts;

            }



            return TtsEngineKind.EdgeTts;

        }

    }

}

