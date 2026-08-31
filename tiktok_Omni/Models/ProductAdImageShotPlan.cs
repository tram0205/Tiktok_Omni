using System.ComponentModel;
using Newtonsoft.Json;

namespace tiktok_Omni.Models
{
    /// <summary>Một prompt con do Gemini lập — dùng ngoài app để sinh ảnh.</summary>
    public sealed class ProductAdImageShotPlan : INotifyPropertyChanged
    {
        private int _index;
        private string _shotType = string.Empty;
        private string _title = string.Empty;
        private string _prompt = string.Empty;
        private string _aspectRatio = "original";
        private string _outputFileName = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Index
        {
            get => _index;
            set => SetField(ref _index, value, nameof(Index));
        }

        /// <summary>solo_female | solo_male | couple | group | flatlay | fabric_closeup | detail_highlight</summary>
        public string ShotType
        {
            get => _shotType;
            set => SetField(ref _shotType, value ?? string.Empty, nameof(ShotType), nameof(ShotTypeDisplay));
        }

        /// <summary>Tiêu đề tiếng Việt ngắn — hiển thị trên lưới.</summary>
        public string Title
        {
            get => _title;
            set => SetField(ref _title, value ?? string.Empty, nameof(Title));
        }

        /// <summary>Prompt tiếng Anh cho image model ngoài app.</summary>
        public string Prompt
        {
            get => _prompt;
            set => SetField(ref _prompt, value ?? string.Empty, nameof(Prompt));
        }

        public string AspectRatio
        {
            get => _aspectRatio;
            set => SetField(ref _aspectRatio, string.IsNullOrWhiteSpace(value) ? "original" : value.Trim(), nameof(AspectRatio));
        }

        public string OutputFileName
        {
            get => _outputFileName;
            set => SetField(ref _outputFileName, value ?? string.Empty, nameof(OutputFileName));
        }

        [JsonIgnore]
        public string ShotTypeDisplay => ProductAdImageShotPlan.FormatShotType(_shotType);

        public static string FormatShotType(string shotType)
        {
            switch ((shotType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "solo_female":
                    return "Đơn nữ";
                case "solo_male":
                    return "Đơn nam";
                case "couple":
                    return "Cặp nam–nữ";
                case "group":
                    return "Hội nhóm";
                case "flatlay":
                    return "Flatlay";
                case "fabric_closeup":
                    return "Cận vải";
                case "detail_highlight":
                    return "Điểm nhấn";
                default:
                    return string.IsNullOrWhiteSpace(shotType) ? "—" : shotType.Trim();
            }
        }

        private void SetField<T>(ref T field, T value, string propertyName, string extraProperty = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (!string.IsNullOrEmpty(extraProperty))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(extraProperty));
            }
        }
    }
}
