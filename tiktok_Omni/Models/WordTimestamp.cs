namespace tiktok_Omni.Models

{

    /// <summary>Mốc thời gian cho một từ (Whisper hoặc ước lượng TTS).</summary>

    public sealed class WordTimestamp

    {

        public string Text { get; set; }



        /// <summary>Alias tương thích code cũ — đồng bộ với <see cref="Text"/>.</summary>

        public string Word

        {

            get => Text;

            set => Text = value;

        }



        public double StartTimeMs { get; set; }



        public double EndTimeMs { get; set; }

    }

}

