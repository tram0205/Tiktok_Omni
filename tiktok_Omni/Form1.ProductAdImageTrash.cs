using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private readonly ProductAdImageTrashStore _productAdImageTrashStore = new ProductAdImageTrashStore();

        private void InitializeProductAdImageTrash()
        {
            RefreshProductAdImageTrashButtonLabel();
        }

        private void RefreshProductAdImageTrashButtonLabel()
        {
            if (btnProductAdImageTrash == null || btnProductAdImageTrash.IsDisposed)
            {
                return;
            }

            var count = _productAdImageTrashStore.CountActive();
            btnProductAdImageTrash.Text = count > 0 ? "♻ Thùng rác (" + count + ")" : "♻ Thùng rác";
        }

        private void BtnProductAdImageTrash_Click(object sender, System.EventArgs e)
        {
            using (var dlg = new ProductAdImageTrashForm(_productAdImageTrashStore))
            {
                dlg.ShowDialog(this);
                if (dlg.RestoredItems.Count == 0)
                {
                    RefreshProductAdImageTrashButtonLabel();
                    return;
                }

                EnsureProductAdImageBindingList();
                AttachProductAdImageBindingListEvents();
                ProductAdImageBatchItem last = null;
                foreach (var restored in dlg.RestoredItems)
                {
                    if (restored == null)
                    {
                        continue;
                    }

                    if (_productAdImageBindingList.Any(i => i != null && i.RowId == restored.RowId))
                    {
                        restored.RowId = System.Guid.NewGuid();
                    }

                    _productAdImageBindingList.Add(restored);
                    last = restored;
                }

                RenumberProductAdImageRows();
                SelectProductAdImageItem(last);
                NotifyProductAdImageDraftDirty();
                RefreshProductAdImageTrashButtonLabel();
                LogProductAdImage("Đã khôi phục " + dlg.RestoredItems.Count + " dòng từ thùng rác.");
            }
        }

        private void MoveProductAdImageItemsToTrash(System.Collections.Generic.IEnumerable<ProductAdImageBatchItem> items)
        {
            if (items == null)
            {
                return;
            }

            var count = 0;
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                _productAdImageTrashStore.AddFromItem(item);
                count++;
            }

            if (count > 0)
            {
                RefreshProductAdImageTrashButtonLabel();
            }
        }
    }
}
