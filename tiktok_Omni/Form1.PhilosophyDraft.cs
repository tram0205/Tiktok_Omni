using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private readonly PhilosophyDraftStore _philosophyDraftStore = new PhilosophyDraftStore();

        private System.Windows.Forms.Timer _philosophyDraftTimer;

        private bool _philosophyDraftDirty;

        private void InitializePhilosophyDraftAutoSave()
        {
            _philosophyDraftTimer?.Stop();
            _philosophyDraftTimer?.Dispose();
            _philosophyDraftTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            _philosophyDraftTimer.Tick += PhilosophyDraftTimer_Tick;
            _philosophyDraftTimer.Start();
        }

        private void PhilosophyDraftTimer_Tick(object sender, EventArgs e)
        {
            if (!_philosophyDraftDirty)
            {
                return;
            }

            FlushPhilosophyDraftToDisk();
        }

        private void FlushPhilosophyDraftToDisk()
        {
            if (_philosophyBatchBindingList == null)
            {
                return;
            }

            var doc = new PhilosophyDraftDocument
            {
                Batches = _philosophyBatchBindingList.ToList()
            };
            _philosophyDraftStore.Save(doc);
            _philosophyDraftDirty = false;
        }

        private void LoadPhilosophyDraftIntoGrid()
        {
            if (_philosophyBatchBindingList != null && _philosophyBatchBindingList.Count > 0)
            {
                return;
            }

            var doc = _philosophyDraftStore.Load();
            if (doc.Batches == null || doc.Batches.Count == 0)
            {
                return;
            }

            _philosophyBatchBindingList = new BindingList<PhilosophyBatchItem>(doc.Batches);
            foreach (var batch in _philosophyBatchBindingList)
            {
                if (batch?.Quotes == null)
                {
                    continue;
                }

                foreach (var script in batch.Quotes)
                {
                    PhilosophySubtitleStyleHelper.EnsureDefaults(script);
                    PhilosophyAmbientCatalog.EnsureRowDefault(script);
                }

                PhilosophyBatchHelper.EnsureBatchDefaults(batch);
                batch.RefreshDerivedFields();
            }

            if (dgvPhilosophyScripts != null && !dgvPhilosophyScripts.IsDisposed)
            {
                dgvPhilosophyScripts.DataSource = _philosophyBatchBindingList;
            }

            _philosophyDraftDirty = false;
            var quoteCount = doc.Batches.Sum(b => b?.Quotes?.Count ?? 0);
            LogPhilosophy("Đã khôi phục " + doc.Batches.Count + " batch (" + quoteCount + " câu) từ draft_philosophy.json.");
        }

        private void NotifyPhilosophyDraftDirty()
        {
            _philosophyDraftDirty = true;
        }
    }
}
