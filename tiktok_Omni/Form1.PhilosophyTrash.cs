using System;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private readonly PhilosophyTrashStore _philosophyTrashStore = new PhilosophyTrashStore();

        private void InitializePhilosophyTrashMaintenance()
        {
            PurgePhilosophyTrashExpired(logWhenRemoved: false);
            RefreshPhilosophyTrashButtonLabel();
        }

        private void PurgePhilosophyTrashExpired(bool logWhenRemoved)
        {
            var removed = _philosophyTrashStore.PurgeExpired();
            if (removed > 0)
            {
                RefreshPhilosophyTrashButtonLabel();
                if (logWhenRemoved)
                {
                    LogPhilosophy("Thùng rác: đã xóa " + removed + " dòng quá 24 giờ.");
                }
            }
        }

        private void RefreshPhilosophyTrashButtonLabel()
        {
            if (btnPhilosophyTrash == null || btnPhilosophyTrash.IsDisposed)
            {
                return;
            }

            var count = _philosophyTrashStore.CountActive();
            btnPhilosophyTrash.Text = count > 0 ? "♻ Thùng rác (" + count + ")" : "♻ Thùng rác";
        }

        private void PhilosophyOpenTrash()
        {
            PurgePhilosophyTrashExpired(logWhenRemoved: true);

            using (var dlg = new PhilosophyTrashForm(_philosophyTrashStore))
            {
                dlg.ShowDialog(this);
                if (dlg.RestoredBatches.Count == 0)
                {
                    RefreshPhilosophyTrashButtonLabel();
                    return;
                }

                _philosophyBatchBindingList ??= new System.ComponentModel.BindingList<PhilosophyBatchItem>();
                PhilosophyBatchItem lastRestored = null;
                foreach (var restored in dlg.RestoredBatches)
                {
                    if (restored == null)
                    {
                        continue;
                    }

                    EnsureUniquePhilosophyBatchIdForRestore(restored, _philosophyBatchBindingList);
                    _philosophyBatchBindingList.Add(restored);
                    lastRestored = restored;
                }

                if (lastRestored == null)
                {
                    RefreshPhilosophyTrashButtonLabel();
                    return;
                }

                if (dgvPhilosophyScripts != null && !dgvPhilosophyScripts.IsDisposed)
                {
                    dgvPhilosophyScripts.DataSource = _philosophyBatchBindingList;
                }

                SelectPhilosophyBatchGridRow(lastRestored);
                NotifyPhilosophyDraftDirty();
                RefreshPhilosophyTrashButtonLabel();
                LogPhilosophy("Đã khôi phục " + dlg.RestoredBatches.Count +
                              " dòng từ thùng rác («" + lastRestored.Topic + "»).");
            }
        }

        private static void EnsureUniquePhilosophyBatchIdForRestore(
            PhilosophyBatchItem batch,
            System.ComponentModel.BindingList<PhilosophyBatchItem> buffer)
        {
            if (batch == null || buffer == null)
            {
                return;
            }

            if (buffer.Any(b => b != null && b.BatchId == batch.BatchId))
            {
                batch.BatchId = Guid.NewGuid();
            }
        }

        private void MovePhilosophyBatchesToTrash(System.Collections.Generic.IEnumerable<PhilosophyBatchItem> batches)
        {
            if (batches == null)
            {
                return;
            }

            var count = 0;
            foreach (var batch in batches)
            {
                if (batch == null)
                {
                    continue;
                }

                _philosophyTrashStore.AddFromBatch(batch);
                count++;
            }

            if (count > 0)
            {
                RefreshPhilosophyTrashButtonLabel();
            }
        }
    }
}
