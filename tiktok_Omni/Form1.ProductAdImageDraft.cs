using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Models;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private readonly ProductAdImageDraftStore _productAdImageDraftStore = new ProductAdImageDraftStore();
        private System.Windows.Forms.Timer _productAdImageDraftTimer;
        private bool _productAdImageDraftDirty;
        private bool _productAdImageDraftHydrated;

        private void InitializeProductAdImageDraftAutoSave()
        {
            if (_productAdImageDraftTimer != null)
            {
                return;
            }

            _productAdImageDraftTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _productAdImageDraftTimer.Tick += ProductAdImageDraftTimer_Tick;
            _productAdImageDraftTimer.Start();
        }

        private void ProductAdImageDraftTimer_Tick(object sender, EventArgs e)
        {
            if (!_productAdImageDraftDirty)
            {
                return;
            }

            FlushProductAdImageDraftToDisk();
        }

        private void FlushProductAdImageDraftToDisk()
        {
            if (_productAdImageBindingList == null)
            {
                return;
            }

            _productAdImageDraftStore.SaveItems(_productAdImageBindingList);
            _productAdImageDraftDirty = false;
        }

        private void NotifyProductAdImageDraftDirty()
        {
            _productAdImageDraftDirty = true;
        }

        private void AttachProductAdImageBindingListEvents()
        {
            if (_productAdImageBindingList == null)
            {
                return;
            }

            _productAdImageBindingList.ListChanged -= ProductAdImageBindingList_ListChanged;
            _productAdImageBindingList.ListChanged += ProductAdImageBindingList_ListChanged;
        }

        private void ProductAdImageBindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            NotifyProductAdImageDraftDirty();
        }

        private async Task HydrateProductAdImageDraftAsync()
        {
            await Task.Yield();
            if (IsDisposed)
            {
                return;
            }

            await RefreshProductAdImageSettingsSnapAsync().ConfigureAwait(true);

            if (_productAdImageDraftHydrated)
            {
                return;
            }

            if (_productAdImageBindingList != null && _productAdImageBindingList.Count > 0)
            {
                _productAdImageDraftHydrated = true;
                AttachProductAdImageBindingListEvents();
                return;
            }

            var doc = _productAdImageDraftStore.Load();
            var items = doc.Items ?? new System.Collections.Generic.List<ProductAdImageBatchItem>();
            _productAdImageBindingList = new BindingList<ProductAdImageBatchItem>(items);
            AttachProductAdImageBindingListEvents();
            if (dgvProductAdImage != null && !dgvProductAdImage.IsDisposed)
            {
                dgvProductAdImage.DataSource = _productAdImageBindingList;
                ApplyProductAdImageProfileComboColumn();
            }

            RenumberProductAdImageRows();
            _productAdImageDraftHydrated = true;
            _productAdImageDraftDirty = false;

            if (items.Count > 0)
            {
                LogProductAdImage("Đã khôi phục " + items.Count + " dòng từ draft.");
            }
        }
    }
}
