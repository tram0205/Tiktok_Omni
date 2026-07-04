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
            if (_philosophyScriptBindingList == null)
            {
                return;
            }

            var doc = new PhilosophyDraftDocument
            {
                Scripts = _philosophyScriptBindingList.ToList(),
                Topic = txtPhilosophyTopic?.Text?.Trim() ?? string.Empty,
                MinDurationSeconds = (int)(numPhilosophyDurationMin?.Value ?? 15),
                MaxDurationSeconds = (int)(numPhilosophyDurationMax?.Value ?? 60),
                PreRenderedVideoFolder = GetPhilosophyVideoInputFolder()
            };
            _philosophyDraftStore.Save(doc);
            _philosophyDraftDirty = false;
        }

        private void LoadPhilosophyDraftIntoGrid()
        {
            if (_philosophyScriptBindingList != null && _philosophyScriptBindingList.Count > 0)
            {
                return;
            }

            var doc = _philosophyDraftStore.Load();
            if (doc.Scripts == null || doc.Scripts.Count == 0)
            {
                return;
            }

            _philosophyScriptBindingList = new BindingList<PhilosophyScriptItem>(doc.Scripts);
            foreach (var script in _philosophyScriptBindingList)
            {
                PhilosophySubtitleStyleHelper.EnsureDefaults(script);
                PhilosophyAmbientCatalog.EnsureRowDefault(script);
            }

            if (dgvPhilosophyScripts != null && !dgvPhilosophyScripts.IsDisposed)
            {
                dgvPhilosophyScripts.DataSource = _philosophyScriptBindingList;
            }

            if (!string.IsNullOrWhiteSpace(doc.Topic) && txtPhilosophyTopic != null)
            {
                txtPhilosophyTopic.Text = doc.Topic;
            }

            if (numPhilosophyDurationMin != null)
            {
                numPhilosophyDurationMin.Value = Math.Max(numPhilosophyDurationMin.Minimum,
                    Math.Min(numPhilosophyDurationMin.Maximum, doc.MinDurationSeconds));
            }

            if (numPhilosophyDurationMax != null)
            {
                numPhilosophyDurationMax.Value = Math.Max(numPhilosophyDurationMax.Minimum,
                    Math.Min(numPhilosophyDurationMax.Maximum, doc.MaxDurationSeconds));
            }

            if (!string.IsNullOrWhiteSpace(doc.PreRenderedVideoFolder)
                && txtPhilosophyVideoInputFolder != null
                && !txtPhilosophyVideoInputFolder.IsDisposed)
            {
                txtPhilosophyVideoInputFolder.Text = doc.PreRenderedVideoFolder.Trim();
            }
            else
            {
                EnsurePhilosophyVideoInputFolderDefault();
            }

            RefreshPhilosophyFolderOptions();
            _philosophyDraftDirty = false;
            LogPhilosophy("Đã khôi phục " + doc.Scripts.Count + " dòng từ draft_philosophy.json.");
        }

        private void NotifyPhilosophyDraftDirty()
        {
            _philosophyDraftDirty = true;
        }
    }
}
