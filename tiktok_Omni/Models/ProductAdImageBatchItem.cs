using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Models
{
    /// <summary>Một dòng lưới Tạo ảnh AI — batch prompt QC sản phẩm thời trang.</summary>
    public sealed class ProductAdImageBatchItem : INotifyPropertyChanged
    {
        public const int MaxImagesPerRow = 30;

        private int _order;
        private string _profileName = string.Empty;
        private string _productName = string.Empty;
        private string _modelImagePath = string.Empty;
        private int _soloFemaleCount = 2;
        private int _soloMaleCount = 2;
        private int _coupleCount = 1;
        private int _groupCount = 1;
        private int _flatlayCount;
        private int _fabricCloseupCount;
        private int _detailHighlightCount;
        private string _aspectRatio = ProductAdImageAspectRatioHelper.Original;
        private string _productTypePrompt = string.Empty;
        private string _shootStylePrompt = string.Empty;
        private string _identityLockPrompt = string.Empty;
        private string _themePrompt = string.Empty;
        private string _productLockDescription = string.Empty;
        private List<ProductAdImageShotPlan> _generatedShots = new List<ProductAdImageShotPlan>();
        private string _status = "Chờ";

        public event PropertyChangedEventHandler PropertyChanged;

        public Guid RowId { get; set; } = Guid.NewGuid();

        public int Order
        {
            get => _order;
            set => SetField(ref _order, value, nameof(Order));
        }

        public string ProfileName
        {
            get => _profileName;
            set => SetField(ref _profileName, value ?? string.Empty, nameof(ProfileName));
        }

        public string ProductName
        {
            get => _productName;
            set
            {
                if (SetField(ref _productName, value ?? string.Empty, nameof(ProductName)))
                {
                    Notify(nameof(CanPlanPrompt));
                }
            }
        }

        /// <summary>1 ảnh master (người + SP) — copy vào folder refs của profile.</summary>
        public string ModelImagePath
        {
            get => _modelImagePath;
            set
            {
                if (SetField(ref _modelImagePath, value ?? string.Empty, nameof(ModelImagePath)))
                {
                    Notify(nameof(ImagesSummary));
                    Notify(nameof(HasReferenceImages));
                    Notify(nameof(CanPlanPrompt));
                }
            }
        }

        public int SoloFemaleCount
        {
            get => _soloFemaleCount;
            set => SetShotCount(ref _soloFemaleCount, value, nameof(SoloFemaleCount));
        }

        public int SoloMaleCount
        {
            get => _soloMaleCount;
            set => SetShotCount(ref _soloMaleCount, value, nameof(SoloMaleCount));
        }

        public int CoupleCount
        {
            get => _coupleCount;
            set => SetShotCount(ref _coupleCount, value, nameof(CoupleCount));
        }

        public int GroupCount
        {
            get => _groupCount;
            set => SetShotCount(ref _groupCount, value, nameof(GroupCount));
        }

        public int FlatlayCount
        {
            get => _flatlayCount;
            set => SetShotCount(ref _flatlayCount, value, nameof(FlatlayCount));
        }

        public int FabricCloseupCount
        {
            get => _fabricCloseupCount;
            set => SetShotCount(ref _fabricCloseupCount, value, nameof(FabricCloseupCount));
        }

        public int DetailHighlightCount
        {
            get => _detailHighlightCount;
            set => SetShotCount(ref _detailHighlightCount, value, nameof(DetailHighlightCount));
        }

        public string AspectRatio
        {
            get => _aspectRatio;
            set
            {
                var normalized = ProductAdImageAspectRatioHelper.Normalize(value);
                if (SetField(ref _aspectRatio, normalized, nameof(AspectRatio)))
                {
                    Notify(nameof(ShotCountSummary));
                }
            }
        }

        public string ProductTypePrompt
        {
            get => _productTypePrompt;
            set
            {
                if (SetField(ref _productTypePrompt, value ?? string.Empty, nameof(ProductTypePrompt)))
                {
                    Notify(nameof(PromptSetupSummary));
                }
            }
        }

        public string ShootStylePrompt
        {
            get => _shootStylePrompt;
            set
            {
                if (SetField(ref _shootStylePrompt, value ?? string.Empty, nameof(ShootStylePrompt)))
                {
                    Notify(nameof(PromptSetupSummary));
                }
            }
        }

        public string IdentityLockPrompt
        {
            get => _identityLockPrompt;
            set
            {
                if (SetField(ref _identityLockPrompt, value ?? string.Empty, nameof(IdentityLockPrompt)))
                {
                    Notify(nameof(PromptSetupSummary));
                }
            }
        }

        public string ThemePrompt
        {
            get => _themePrompt;
            set
            {
                if (SetField(ref _themePrompt, value ?? string.Empty, nameof(ThemePrompt)))
                {
                    Notify(nameof(PromptSetupSummary));
                }
            }
        }

        public string ProductLockDescription
        {
            get => _productLockDescription;
            set
            {
                if (SetField(ref _productLockDescription, value ?? string.Empty, nameof(ProductLockDescription)))
                {
                    Notify(nameof(PromptSetupSummary));
                }
            }
        }

        public List<ProductAdImageShotPlan> GeneratedShots
        {
            get => _generatedShots;
            set
            {
                _generatedShots = value ?? new List<ProductAdImageShotPlan>();
                Notify(nameof(GeneratedShots));
                Notify(nameof(PromptGridLabel));
            }
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, string.IsNullOrWhiteSpace(value) ? "Chờ" : value.Trim(), nameof(Status));
        }

        [JsonIgnore]
        public int TotalImageCount =>
            SoloFemaleCount + SoloMaleCount + CoupleCount + GroupCount
            + FlatlayCount + FabricCloseupCount + DetailHighlightCount;

        [JsonIgnore]
        public bool HasReferenceImages =>
            !string.IsNullOrWhiteSpace(ModelImagePath) && File.Exists(ModelImagePath);

        [JsonIgnore]
        public bool CanPlanPrompt =>
            !string.IsNullOrWhiteSpace(ProductName)
            && HasReferenceImages
            && TotalImageCount > 0;

        [JsonIgnore]
        public string ImagesSummary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ModelImagePath))
                {
                    return "➕ chọn   📂 mở";
                }

                var name = Path.GetFileName(ModelImagePath);
                return string.IsNullOrWhiteSpace(name) ? "✓ ảnh mẫu" : "✓ " + name;
            }
        }

        [JsonIgnore]
        public string ShotCountSummary =>
            TotalImageCount.ToString() + "/" + MaxImagesPerRow
            + " · " + ProductAdImageAspectRatioHelper.GetDisplayLabel(AspectRatio);

        [JsonIgnore]
        public string PromptSetupSummary
        {
            get
            {
                var typeLabel = ShowcaseProductTypePresets.GetDisplayLabel(ProductTypePrompt);
                var styleLabel = ProductAdImageShootStylePresets.GetDisplayLabel(ShootStylePrompt);
                var themeLabel = ProductAdImageThemePresets.GetDisplayLabel(ThemePrompt);
                var lockLabel = ProductAdImageIdentityLockPresets.GetDisplayLabel(IdentityLockPrompt);
                var lockNote = string.IsNullOrWhiteSpace(ProductLockDescription)
                    ? string.Empty
                    : " · khóa SP";
                return typeLabel + " · " + styleLabel + " · " + themeLabel + " · " + lockLabel + lockNote;
            }
        }

        [JsonIgnore]
        public string PromptGridLabel
        {
            get
            {
                var count = GeneratedShots?.Count(s => s != null) ?? 0;
                return count <= 0 ? "Chưa có prompt" : count + " prompt";
            }
        }

        public void ReplaceGeneratedShots(IEnumerable<ProductAdImageShotPlan> shots)
        {
            var list = (shots ?? Enumerable.Empty<ProductAdImageShotPlan>())
                .Where(s => s != null)
                .ToList();
            for (var i = 0; i < list.Count; i++)
            {
                list[i].Index = i + 1;
            }

            GeneratedShots = list;
        }

        public void ApplyShotCounts(
            int soloFemale,
            int soloMale,
            int couple,
            int group,
            int flatlay,
            int fabricCloseup,
            int detailHighlight)
        {
            _soloFemaleCount = ClampCount(soloFemale);
            _soloMaleCount = ClampCount(soloMale);
            _coupleCount = ClampCount(couple);
            _groupCount = ClampCount(group);
            _flatlayCount = ClampCount(flatlay);
            _fabricCloseupCount = ClampCount(fabricCloseup);
            _detailHighlightCount = ClampCount(detailHighlight);
            TrimCountsToBudget();
            NotifyShotCountProperties();
        }

        private void SetShotCount(ref int field, int value, string propertyName)
        {
            var clamped = ClampCount(value);
            if (field == clamped)
            {
                return;
            }

            field = clamped;
            Notify(propertyName);
            Notify(nameof(TotalImageCount));
            Notify(nameof(ShotCountSummary));
            Notify(nameof(CanPlanPrompt));
        }

        private void TrimCountsToBudget()
        {
            var overflow = TotalImageCount - MaxImagesPerRow;
            if (overflow <= 0)
            {
                return;
            }

            Reduce(ref _detailHighlightCount, ref overflow);
            Reduce(ref _fabricCloseupCount, ref overflow);
            Reduce(ref _flatlayCount, ref overflow);
            Reduce(ref _groupCount, ref overflow);
            Reduce(ref _coupleCount, ref overflow);
            Reduce(ref _soloMaleCount, ref overflow);
            Reduce(ref _soloFemaleCount, ref overflow);
        }

        private static void Reduce(ref int field, ref int overflow)
        {
            if (overflow <= 0 || field <= 0)
            {
                return;
            }

            var cut = Math.Min(field, overflow);
            field -= cut;
            overflow -= cut;
        }

        private static int ClampCount(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > MaxImagesPerRow ? MaxImagesPerRow : value;
        }

        private void NotifyShotCountProperties()
        {
            Notify(nameof(SoloFemaleCount));
            Notify(nameof(SoloMaleCount));
            Notify(nameof(CoupleCount));
            Notify(nameof(GroupCount));
            Notify(nameof(FlatlayCount));
            Notify(nameof(FabricCloseupCount));
            Notify(nameof(DetailHighlightCount));
            Notify(nameof(TotalImageCount));
            Notify(nameof(ShotCountSummary));
            Notify(nameof(CanPlanPrompt));
        }

        private bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            Notify(propertyName);
            return true;
        }

        private void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
