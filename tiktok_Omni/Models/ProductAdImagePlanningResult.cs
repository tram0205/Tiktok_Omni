using System;
using System.Collections.Generic;

namespace tiktok_Omni.Models
{
    public sealed class ProductAdImagePlanningResult
    {
        public ProductAdImagePlanningResult(IReadOnlyList<ProductAdImageShotPlan> shots)
        {
            Shots = shots ?? Array.Empty<ProductAdImageShotPlan>();
        }

        public IReadOnlyList<ProductAdImageShotPlan> Shots { get; }
    }
}
