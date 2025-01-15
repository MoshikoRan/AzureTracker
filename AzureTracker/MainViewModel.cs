using AzureTracker.Properties;
using CefSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Input;
using static AzureTracker.AzureProvider;

namespace AzureTracker
{
    public class MainViewModel : ViewModelBase
    {
        public Dictionary<AzureObject, AzureObjectListViewModel> AzureObjectVMDictionary { get; private set; } =
            new Dictionary<AzureObject, AzureObjectListViewModel>();
        public MainViewModel()
        {
            Logger.Instance.NewLog += NewLog;
            CreateVMDictionary();
        }

        public void Init()
        {
            m_azureProvider = new AzureProvider(
                new AzureProviderConfig
                {
                    Organization = Settings.Default.Organization,
                    PAT = Settings.Default.PAT,
                    WorkItemTypes = Settings.Default.WorkItemTypes.Split(";", StringSplitOptions.RemoveEmptyEntries),
                    BuildNotOlderThanDays = Settings.Default.BuildNotOlderThanDays
                });
            m_azureProvider.DataFetchEvent += AzureProveiderDataFetchHandler;

            ResetFilters();
            Sync(AzureObject.None);
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

        string m_syncAbortBtnText = string.Empty;
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
                    Logger.Instance.Info(m_sStatus);
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
                        Status = "Error!";
                        Logger.Instance.Error(ex.Message);
                    }
                });
        }


        public AzureObject SelectedAzureObject { get; set; } = AzureObject.None;
        private void Sync(AzureObject azureObject)
        {
            if (!EnableSelection)
                return;

            EnableSelection = false;
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
                        Status = "Error!";
                        Logger.Instance.Error(ex.Message);
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

        internal void OpenChrome(Uri? uri)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
                psi.WorkingDirectory = @"C:\Program Files (x86)\Google\Chrome\Application";
                psi.Arguments = uri?.ToString();
                Process.Start(psi);
                Logger.Instance.Info($"opened link = {uri}");
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e.Message);
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

        IDownloadHandler m_downloadHandler = new DownloadHandler();
        public IDownloadHandler ChromeDownloadHandler
        {
            get { return m_downloadHandler; }
        }

    }

    internal class DownloadHandler : IDownloadHandler
    {
        public bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod)
        {
            return true;
        }

        public bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            if (!callback.IsDisposed)
            {
                using (callback)
                {
                    string sPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "downloads",
                            downloadItem.SuggestedFileName);
                    callback.Continue(
                        sPath,
                        showDialog: false);
                    Logger.Instance.Info($"Downloding {sPath}");
                }
            }
            return true;
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
        {
            Logger.Instance.Info($"OnDownloadUpdated...");
        }
    }

    internal class CommandHandler : ICommand
    {
        public delegate void ActionWithParam(object? parameter);
        private Action? _action;
        private ActionWithParam? _actionWithParam;
        private bool _canExecute;
        public CommandHandler(Action action, bool canExecute = true)
        {
            _action = action;
            _canExecute = canExecute;
            CanExecuteChanged = null;
        }
        public CommandHandler(ActionWithParam action, bool canExecute = true)
        {
            _actionWithParam = action;
            _canExecute = canExecute;
            CanExecuteChanged = null;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute;
        }
        public event EventHandler? CanExecuteChanged;
        public void Execute(object? parameter)
        {
            _action?.Invoke();
            _actionWithParam?.Invoke(parameter);
        }

        public void SetCanExecute(bool canExecute)
        {
            if (canExecute != _canExecute)
            {
                _canExecute = canExecute;
                CanExecuteChanged?.Invoke(this, new EventArgs());
            }
        }
    }

}
