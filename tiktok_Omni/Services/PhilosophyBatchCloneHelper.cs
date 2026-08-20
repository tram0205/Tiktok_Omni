using System;
using System.Linq;
using Newtonsoft.Json;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    internal static class PhilosophyBatchCloneHelper
    {
        public static PhilosophyBatchItem CloneBatch(PhilosophyBatchItem source, bool forCopy)
        {
            if (source == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(source);
            var clone = JsonConvert.DeserializeObject<PhilosophyBatchItem>(json);
            if (clone == null)
            {
                return null;
            }

            clone.BatchId = Guid.NewGuid();
            if (forCopy)
            {
                var topic = (clone.Topic ?? string.Empty).Trim();
                clone.Topic = string.IsNullOrEmpty(topic) ? "Bản sao" : topic + " (bản sao)";
                foreach (var quote in clone.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>())
                {
                    if (quote == null)
                    {
                        continue;
                    }

                    quote.OutputPath = string.Empty;
                    quote.Status = "Nháp";
                    quote.LastError = string.Empty;
                }

                clone.Status = "Nháp";
                clone.LastError = string.Empty;
            }

            clone.RefreshDerivedFields();
            return clone;
        }
    }
}
