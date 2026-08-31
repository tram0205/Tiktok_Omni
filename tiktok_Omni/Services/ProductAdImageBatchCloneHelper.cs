using System;
using System.IO;
using Newtonsoft.Json;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    internal static class ProductAdImageBatchCloneHelper
    {
        public static ProductAdImageBatchItem CloneRow(ProductAdImageBatchItem source, bool copyReferenceImage)
        {
            if (source == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(source);
            var clone = JsonConvert.DeserializeObject<ProductAdImageBatchItem>(json);
            if (clone == null)
            {
                return null;
            }

            clone.RowId = Guid.NewGuid();
            var name = (clone.ProductName ?? string.Empty).Trim();
            clone.ProductName = string.IsNullOrEmpty(name) ? "Bản sao" : name + " (bản sao)";
            clone.Status = clone.GeneratedShots != null && clone.GeneratedShots.Count > 0 ? "Xong" : "Chờ";

            if (copyReferenceImage && !string.IsNullOrWhiteSpace(clone.ModelImagePath) && File.Exists(clone.ModelImagePath))
            {
                try
                {
                    clone.ModelImagePath = ProductAdImageReferenceHelper.CopyMasterImage(
                        clone.ModelImagePath,
                        clone.ProfileName);
                }
                catch
                {
                    // giữ path gốc nếu copy thất bại
                }
            }

            return clone;
        }
    }
}
