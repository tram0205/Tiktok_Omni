using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private bool _suppressUiNavigationPersist = true;
        private bool _applicationClosing;
        private bool _consoleFastExit;

        private void ApplyUiNavigationToSettings(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.LastAiVideoGenMode = _selectedAiVideoGenMode.ToString();
            settings.LastMainTabKey = GetMainTabPersistenceKey(tabMain?.SelectedTab);
        }

        private async Task SaveUiNavigationStateAsync()
        {
            if (_suppressUiNavigationPersist || _configManager == null)
            {
                return;
            }

            try
            {
                var settings = await _configManager.LoadAsync().ConfigureAwait(true);
                ApplyUiNavigationToSettings(settings);
                await _configManager.SaveAsync(settings).ConfigureAwait(true);
            }
            catch
            {
                // Không chặn UI nếu ghi settings thất bại.
            }
        }

        private void SaveUiNavigationStateSync()
        {
            if (_suppressUiNavigationPersist || _configManager == null)
            {
                return;
            }

            try
            {
                var settings = _configManager.LoadAsync().ConfigureAwait(true).GetAwaiter().GetResult();
                ApplyUiNavigationToSettings(settings);
                _configManager.SaveAsync(settings).ConfigureAwait(true).GetAwaiter().GetResult();
            }
            catch
            {
                // ignored on exit
            }
        }

        private void RestoreUiNavigationFromSettings(AppSettings settings)
        {
            if (settings == null || tabMain == null)
            {
                return;
            }

            _suppressUiNavigationPersist = true;
            try
            {
                var mainTab = ResolveMainTabByPersistenceKey(settings.LastMainTabKey);
                if (mainTab != null)
                {
                    SwitchToMainTab(mainTab);
                }

                if (TryParsePersistedAiVideoGenMode(settings.LastAiVideoGenMode, out var mode))
                {
                    SelectAiVideoGenMode(mode);
                }
            }
            finally
            {
                _suppressUiNavigationPersist = false;
            }
        }

        private static bool TryParsePersistedAiVideoGenMode(string value, out AiVideoGenMode mode)
        {
            mode = AiVideoGenMode.Slideshow;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Enum.TryParse(value.Trim(), true, out mode);
        }

        private TabPage ResolveMainTabByPersistenceKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || tabMain == null)
            {
                return null;
            }

            switch (key.Trim())
            {
                case "AiVideoGen":
                    return tabAiVideoGen;
                case "AffiliateHunter":
                    return tabAffiliateHunter;
                case "AutoPost":
                    return tabAutoPost;
                case "AutoWarmup":
                    return tabAutoWarmup;
                case "Setting":
                    return tabSetting;
                case "RevenueDashboard":
                    return tabRevenueDashboard;
                case "HealthDashboard":
                    return tabHealthDashboard;
            }

            foreach (TabPage page in tabMain.TabPages)
            {
                if (string.Equals(page.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return page;
                }
            }

            return null;
        }

        private string GetMainTabPersistenceKey(TabPage page)
        {
            if (page == null)
            {
                return string.Empty;
            }

            if (ReferenceEquals(page, tabAiVideoGen))
            {
                return "AiVideoGen";
            }

            if (ReferenceEquals(page, tabAffiliateHunter))
            {
                return "AffiliateHunter";
            }

            if (ReferenceEquals(page, tabAutoPost))
            {
                return "AutoPost";
            }

            if (ReferenceEquals(page, tabAutoWarmup))
            {
                return "AutoWarmup";
            }

            if (ReferenceEquals(page, tabSetting))
            {
                return "Setting";
            }

            if (ReferenceEquals(page, tabRevenueDashboard))
            {
                return "RevenueDashboard";
            }

            if (ReferenceEquals(page, tabHealthDashboard))
            {
                return "HealthDashboard";
            }

            return page.Name ?? string.Empty;
        }
    }
}
