using AzureTracker.Properties;
using AzureTracker.Utils;
using CefSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using static AzureTracker.AzureProvider;

namespace AzureTracker
{
    public class MainViewModel : ViewModelBase, IDownloadHandlerOwner
    {
        public Dictionary<AzureObject, AzureObjectListViewModel> AzureObjectVMDictionary { get; private set; } =
            new Dictionary<AzureObject, AzureObjectListViewModel>();
        public MainViewModel()
        {
            Logger.Instance.NewLog += NewLog;
            CreateVMDictionary();
        }

        bool m_isInitialized = false;
        public void Init()
        {
            try
            {
                m_azureProvider = new AzureProvider();

                m_isInitialized = m_azureProvider.Init(
                            new AzureProviderConfig
                            {
                                Organization = Settings.Default.Organization,
                                PAT = Settings.Default.PAT,
                                WorkItemTypes = Settings.Default.WorkItemTypes.Split(";", StringSplitOptions.RemoveEmptyEntries),
                                BuildNotOlderThanDays = Settings.Default.BuildNotOlderThanDays,
                                MaxBuildsPerDefinition = Settings.Default.MaxBuildsPerDefinition,
                                UseCaching = Settings.Default.UseCaching
                            });

                if (m_isInitialized)
                {
                    m_azureProvider.DataFetchEvent += AzureProveiderDataFetchHandler;

                    ResetFilters();
                    Sync(AzureObject.None);
                }
                else
                {
                    Abort();
                    OpenSettingsDialog();
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus(ex);
            }
        }

        internal void UnInit()
        {
            if (m_azureProvider != null)
            {
                m_azureProvider.DataFetchEvent -= AzureProveiderDataFetchHandler;
                m_azureProvider.Save();
                m_azureProvider = null;
            }
        }
        private void CreateVMDictionary()
        {
            foreach (var obj in Enum.GetValues(typeof(AzureObject)))
            {
                var val = (AzureObject)obj;
                if (val != AzureObject.None)
                {
                    AzureObjectVMDictionary.Add(val, new AzureObjectListViewModel());
                }
            }
        }
        private void AzureProveiderDataFetchHandler(AzureObject azureObject, string? projName, string? param)
        {
            Logger.Instance.Info($"Fetching {azureObject} from {projName} {param}...");
        }

        private void NewLog()
        {
            OnPropertyChanged(nameof(LogBuffer));
        }

        Dictionary<AzureObject, Dictionary<string, string>> dicFilter = new Dictionary<AzureObject, Dictionary<string, string>>();
        JsonArray? m_jsonDefaultFilters = null;

        private void ResetFilters()
        {
            try
            {
                if (m_jsonDefaultFilters == null)
                {
                    JsonNode? json = JsonNode.Parse(Settings.Default.DefaultFilters);
                    if (json != null)
                    {
                        JsonNode? value = json["defaultfilters"];
                        m_jsonDefaultFilters = value?.AsArray();
                    }
                }

                for (int i = 0; i < m_jsonDefaultFilters?.Count; ++i)
                {
                    JsonNode? jsonFilter = m_jsonDefaultFilters[i];
                    var type = jsonFilter?["type"]?.ToString();
                    if (type != null)
                    {
                        AzureObject ao = Enum.Parse<AzureObject>(type);
                        ResetFilterByJsonFilter(ao, jsonFilter);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex.Message);
            }
        }

        private void ResetFilterByJsonFilter(AzureObject ao, JsonNode? jsonFilter)
        {
            var dicFilter = new Dictionary<string, string>();
            JsonArray? fields = jsonFilter?["fields"]?.AsArray();
            ResetFilterByType(dicFilter, ao, fields);
        }

        private void ResetFilterByType(Dictionary<string, string> dicFilter, AzureObject ao, JsonArray? fields)
        {
            for (int j = 0; j < fields?.Count; ++j)
            {
                JsonObject? node = fields[j]?.AsObject();
                ResetFieldrByType(dicFilter, ao, node);
            }
            AzureObjectVMDictionary[ao].ResetFilter(dicFilter);
        }
        private void ResetFieldrByType(Dictionary<string, string> dicFilter, AzureObject ao, JsonObject? node)
        {
            if (node != null)
            {
                IEnumerable<string> propertyNames = node.Select(p => p.Key);

                if (propertyNames != null)
                {
                    foreach (string sKey in propertyNames)
                    {
                        var field = node?[sKey]?.ToString();
                        if (field != null) dicFilter[sKey] = field;
                    }
                }
            }
        }

        string m_syncAbortBtnText = "Sync";
        public string SyncAbortBtnText
        {
            get { return m_syncAbortBtnText; }
            set
            {
                if (m_syncAbortBtnText != value)
                {
                    m_syncAbortBtnText = value;
                    OnPropertyChanged(nameof(SyncAbortBtnText));
                }
            }
        }

        ICommand? m_cmdSyncAbort = null;
        public ICommand CmdSyncAbort
        {
            get
            {
                if (m_cmdSyncAbort == null)
                {
                    m_cmdSyncAbort = new CommandHandler(
                        new Action(SyncAbort));
                }
                return m_cmdSyncAbort;
            }
        }

        ICommand? m_cmdSwitchContent = null;
        public ICommand CmdSwitchContent
        {
            get
            {
                if (m_cmdSwitchContent == null)
                {
                    m_cmdSwitchContent = new CommandHandler(
                        new Action(SwitchContent));
                }
                return m_cmdSwitchContent;
            }
        }

        ICommand? m_cmdAbout = null;
        public ICommand CmdAbout
        {
            get
            {
                if (m_cmdAbout == null)
                {
                    m_cmdAbout = new CommandHandler(
                        new Action(ShowAboutWindow));
                }
                return m_cmdAbout;
            }
        }

        ICommand? m_cmdClearFilter = null;
        public ICommand CmdClearFilter
        {
            get
            {
                if (m_cmdClearFilter == null)
                {
                    m_cmdClearFilter = new CommandHandler(
                        new Action(ClearFilter));
                }
                return m_cmdClearFilter;
            }
        }


        ICommand? m_cmdSettings = null;
        public ICommand CmdSettings
        {
            get
            {
                if (m_cmdSettings == null)
                {
                    m_cmdSettings = new CommandHandler(
                        new Action(OpenSettingsDialog));
                }
                return m_cmdSettings;
            }
        }

        private void OpenSettingsDialog()
        {
            var azureSettingsVM = new AzureSettingsVM(
                Settings.Default.Organization,
                Settings.Default.PAT);
            var azureSettingsWindow = new AzureSettingsWindow(azureSettingsVM);
            var res = azureSettingsWindow.ShowDialog();
            if (res.HasValue && res.Value == true)
            {
                Settings.Default.Organization = azureSettingsVM.OrganizationName;
                Settings.Default.PAT = azureSettingsVM.PAT;
                Settings.Default.Save();
                Init();
            }
        }

        private void ClearFilter()
        {
            AzureObjectVMDictionary[SelectedAzureObject].ClearFilter();
        }

        ICommand? m_cmdResetFilter = null;
        public ICommand CmdResetFilter
        {
            get
            {
                if (m_cmdResetFilter == null)
                {
                    m_cmdResetFilter = new CommandHandler(
                        new Action(ResetFilter));
                }
                return m_cmdResetFilter;
            }
        }

        private void ResetFilter()
        {
            for (int i = 0; i < m_jsonDefaultFilters?.Count; ++i)
            {
                JsonNode? jsonFilter = m_jsonDefaultFilters[i];
                var type = jsonFilter?["type"]?.ToString();
                if (type != null)
                {
                    AzureObject ao = Enum.Parse<AzureObject>(type);

                    if (ao == SelectedAzureObject)
                    {
                        ResetFilterByJsonFilter(ao, jsonFilter);
                        AzureObjectVMDictionary[ao].RefreshView();
                    }
                }
            }
        }

        private void ShowAboutWindow()
        {
            new About().ShowDialog();
        }

        private void SwitchContent()
        {
            DisplayWebView = !DisplayWebView;
            Logger.Instance.Info($"SwitchContent DisplayWebView = {DisplayWebView}");
        }

        public bool DisplayWebView { get; set; } = true;

        AzureProvider? m_azureProvider = null;

        string m_sStatus = string.Empty;
        public string Status {
            get
            {
                return m_sStatus;
            }
            set
            {
                if (value != m_sStatus)
                {
                    m_sStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        bool m_bEnableSelection = true;
        public bool EnableSelection
        {
            get
            {
                return m_bEnableSelection;
            }
            set
            {
                if (value != m_bEnableSelection)
                {
                    m_bEnableSelection = value;
                    OnPropertyChanged();
                }
            }
        }
        public string LogBuffer
        {
            get { return Logger.Instance.LogBuffer; }
        }

        private void SyncAbort()
        {
            if (EnableSelection)
                Sync(SelectedAzureObject);
            else
                Abort();
        }

        private void Abort()
        {
            Task.Run(
                () =>
                {
                    try
                    {
                        m_azureProvider?.Abort();
                        Status = "Aborting...";
                    }
                    catch (Exception ex)
                    {
                        SetErrorStatus(ex);
                    }
                });
        }

        void SetErrorStatus(Exception e)
        {
            Logger.Instance.Error(e.Message);
            Status = "Error! Click 'View Log' for more details.";
        }

        public AzureObject SelectedAzureObject { get; set; } = AzureObject.None;
        private void Sync(AzureObject azureObject)
        {
            if (!EnableSelection)
                return;

            EnableSelection = false;

            if (!m_isInitialized)
                OpenSettingsDialog();

            Status = "Synchronizing. Please wait...";
            var t = new Task(
                () =>
                {
                    try
                    {
                        SyncAbortBtnText = "Abort Sync";
                        m_azureProvider?.Sync(azureObject);
                        Status = "Done!";
                    }
                    catch (Exception ex)
                    {
                        SetErrorStatus(ex);
                    }
                    finally
                    {
                        SyncAbortBtnText = "Sync";
                        EnableSelection = true;
                    }
                });

            t.GetAwaiter().OnCompleted(() => 
            {
                SetVMDictionaryData();
            });
            t.Start();
        }

        private void SetVMDictionaryData()
        {
            foreach (var obj in Enum.GetValues(typeof(AzureObject)))
            {
                var val = (AzureObject)obj;
                SetVMDictionaryData(val);
            }
        }

        private void SetVMDictionaryData(AzureObject ao)
        {
            if (ao != AzureObject.None && m_azureProvider != null)
            {
                AzureObjectVMDictionary[ao].SetData(data: m_azureProvider.Get(ao));
            }
        }

        internal void ClearLog()
        {
            Logger.Instance.Clear();
        }

        internal void SyncAzureObject(AzureObjectBase? aob)
        {
            try
            {
                if (aob != null && m_azureProvider != null)
                {
                    if (m_azureProvider.SyncAzureObject(aob))
                    {
                        AzureObject ao;
                        if (Enum.TryParse(aob.GetType().Name, out ao))
                        {
                            SetVMDictionaryData(ao);
                        }
                        else
                        {
                            Logger.Instance.Error($"SyncAzureObject: could not parse azure object type");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e.Message);
            }
        }

        public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem)
        {
            EnableSelection = false;
        }

        public void OnDownloadCompleted(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, bool bSuccess)
        {
            if (bSuccess)
            {
                browser.CloseBrowser(true);
            }
            else
            {
                Helpers.ShowWindow(browser.GetHost().GetWindowHandle(), Helpers.SW_SHOW);
                // did not find a program to open the file, try in browser
                browser?.MainFrame?.LoadUrl(DownloadHandler.DownloadsFolder);
            }
            EnableSelection = true;
        }

        IDownloadHandler? m_downloadHandler = null;
        public IDownloadHandler ChromeDownloadHandler
        {
            get 
            { 
                if (m_downloadHandler == null)
                {
                    m_downloadHandler = new DownloadHandler(this);
                }
                return m_downloadHandler; 
            }
        }
    }

    internal class DownloadHandler : IDownloadHandler
    {
        IDownloadHandlerOwner? m_owner = null;
        public DownloadHandler(IDownloadHandlerOwner owner)
        {
            m_owner = owner;
        }
        public bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod)
        {
            return true;
        }

        public static string DownloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"downloads");
        public bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            if (!callback.IsDisposed)
            {
                using (callback)
                {
                    string sPath = Path.Combine(DownloadsFolder, downloadItem.SuggestedFileName);
                    callback.Continue(
                        sPath,
                        showDialog: false);
                    Logger.Instance.Info($"Downloding {sPath}");
                    m_owner?.OnBeforeDownload(chromiumWebBrowser, browser, downloadItem);
                }
            }
            return true;
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
        {
            if (downloadItem.IsComplete)
            {
                bool bSuccess = false;
                Logger.Instance.Info($"Download of {downloadItem.FullPath} completed.");
                try
                {
                    Helpers.ShowWindow(browser.GetHost().GetWindowHandle(), Helpers.SW_HIDE);
                    var progs = Helpers.GetRecommendedPrograms(Path.GetExtension(downloadItem.FullPath));
                    var fullPath = "\"" + downloadItem.FullPath + "\"";
                    var count = 0;
                    foreach (var prog in progs)
                    {
                        try
                        {
                            var pi = new ProcessStartInfo
                            {
                                FileName = "\"" + prog + "\"",
                                Arguments = fullPath,
                                Verb = "runas"
                            };
                            Process.Start(pi);
                            bSuccess = true;
                        }
                        catch (Exception e)
                        {
                            count++;
                            Logger.Instance.Error($"could not open using {prog}\n{e.Message}");
                        } 
                    }
                }
                catch (Exception e)
                {
                    Logger.Instance.Error(e.Message);
                }
                finally
                {
                    m_owner?.OnDownloadCompleted(chromiumWebBrowser, browser, downloadItem, bSuccess);
                }
            }
        }
    }

    public interface IDownloadHandlerOwner
    {
        void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem);
        void OnDownloadCompleted(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, bool bSuccess);
    }
}
