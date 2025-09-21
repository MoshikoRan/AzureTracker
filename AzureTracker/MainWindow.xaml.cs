using AzureTracker.Utils;
using CefSharp;
using CefSharp.Wpf;
using CefSharp.Wpf.Handler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace AzureTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainViewModel();
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                CreateTabItems();
                vm.PropertyChanged += OnPropertyChanged;
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CustomFilters")
            {
                UpdateCustomFilters();
            }
        }

        private void UpdateCustomFilters()
        {
            CUSTOM_FILTERS.Items.Clear();
            var vm = DataContext as MainViewModel;
            if (vm != null && vm.CustomFilters != null
                && vm.CustomFilters.ContainsKey(vm.SelectedAzureObject))
            {
                foreach (var filter in vm.CustomFilters[vm.SelectedAzureObject])
                {
                    MenuItem item = new MenuItem();
                    item.Header = filter;
                    item.Click += CustomFilterMenuItem_Click;
                    item.ContextMenu = new ContextMenu();

                    var removeItem = new MenuItem();
                    removeItem.Header = "Remove";
                    removeItem.Tag = filter;
                    removeItem.Click += CustomFilterRemoveItem_Click;

                    item.ContextMenu.Items.Add(removeItem);
                    CUSTOM_FILTERS.Items.Add(item);
                }
            }
            //<MenuItem Header="Add Custom Filter" Command="{Binding CmdAddCustomFilter}" />
            CUSTOM_FILTERS.Items.Add(new MenuItem()
            {
                Header = "Add Custom Filter",
                Command = vm?.CmdAddCustomFilter
            });
        }

        private void CustomFilterRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            vm?.RemoveCustomFilter((sender as MenuItem).Tag.ToString());
        }

        private void CustomFilterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            vm?.SetCustomFilter((sender as MenuItem).Header.ToString());
        }

        private void CreateTabItems()
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                foreach (AzureObject key in vm.AzureObjectVMDictionary.Keys)
                {
                    AzureObjectList aol = new AzureObjectList();
                    aol.DataContext = vm.AzureObjectVMDictionary[key];
                    aol.ItemDoubleClickEvent += ItemDblClick;
                    aol.UpdateItemsEvent += UpdateItems;
                    TabItem ti = new TabItem();
                    ti.Header = key.ToString();
                    ti.Content = aol;
                    ti.Width = 100;
                    AzureObjectTabCtrl.Items.Add(ti);
                }
            }
        }

        private void UpdateItems(List<AzureObjectBase> listAOB)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.SyncAzureObjects(listAOB);
            }
        }

        private bool m_bItemDblClick = false;
        private void ItemDblClick(AzureObjectBase? aob)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.SyncAzureObject(aob);
            }
            OnViewItem(aob);
            m_bItemDblClick = true;
        }

        private void AzureObjectTabCtrl_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem)
            {
                var vm = DataContext as MainViewModel;
                if (vm != null)
                {
                    var ti = e.AddedItems[0] as TabItem;
                    if (ti != null)
                    {
                        var header = ti.Header.ToString();
                        if (header != null)
                        {
                            vm.SelectedAzureObject = Enum.Parse<AzureObject>(header);
                            UpdateCustomFilters();
                        }
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.Init();
            }
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.UnInit();
                vm.SaveLog();
            }

            Application.Current.Shutdown();
        }

        private string GetTabHeader(AzureObjectBase? aob)
        {
            return aob?.GetType().Name + " " + aob?.ID.ToString();
        }

        public class WebTabItem : TabItem
        {
            public string? Uri { get; set; }
        }


        private WebTabItem? GetWebTabItemByUri(string? uri)
        {
            WebTabItem? found = null;
            foreach (WebTabItem item in ChromeTabCtrl.Items)
            {
                if (item.Uri == uri)
                {
                    found = item; break;
                }
            }
            return found;
        }
        private void OnViewItem(AzureObjectBase? aob)
        {
            AddOrSelectTab(aob?.Uri?.AbsoluteUri, GetTabHeader(aob));
        }

        private void AddOrSelectTab(string? uri, string header)
        {
            WebTabItem? found = GetWebTabItemByUri(uri);
            if (found == null)
            {
                found = CreatNewTab(uri, header);
                ChromeTabCtrl.Items.Add(found);
            }
            else
            {
                var tb = found.Header as TextBlock;
                Logger.Instance.Info($"Switching to tab {tb?.Text}");
            }

            ChromeTabCtrl.SelectedItem = found;
        }

        private WebTabItem CreatNewTab(string? uri, string header)
        {
            var chromeTab = new ChromiumWebBrowser(uri);
            var menuItemOpenNewTab = new MenuItem();
            var menuHandler = new ChromeTabMenuHandler();
            chromeTab.MenuHandler = menuHandler;

            var vm = DataContext as MainViewModel;
            chromeTab.DownloadHandler = vm?.ChromeDownloadHandler;
            var found = new WebTabItem();
            TextBlock headerTB = new TextBlock();
            headerTB.TextDecorations.Add(TextDecorations.Underline);
            headerTB.Foreground = System.Windows.Media.Brushes.Blue;
            headerTB.Text = header;

            Logger.Instance.Info($"Creating new tab {header}");

            found.MouseLeftButtonDown += Tab_MouseLeftButtonDown;
            found.Header = headerTB;
            found.Content = chromeTab;
            found.Uri = uri;
            found.ToolTip = uri;

            found.ContextMenu = new ContextMenu();
            //copy uri
            CreateTabMenuItem(found, "Copy Url", uri, CopyUri_Click);

            //open System browser
            CreateTabMenuItem(found, "Open System Browser", uri, MenuItemOpenChrome_Click);

            //close currnt tab
            CreateTabMenuItem(found, "Close", found, MenuClose_Click);

            //close all tabs
            CreateTabMenuItem(found, "Close all tabs", found, CloseAllTabs);

            //close all tabs but current
            CreateTabMenuItem(found, "Close all tabs but this", found, CloseAllTabsButThis);

            return found;
        }

        private void CreateTabMenuItem(WebTabItem tab, string header, object? tag, RoutedEventHandler clickEvent)
        {
            var menuItem = new MenuItem();
            menuItem.Header = header;
            menuItem.Tag = tag;
            menuItem.Click += clickEvent;

            tab.ContextMenu.Items.Add(menuItem);
        }

        private void MenuItemOpenChrome_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                var url = menuItem.Tag.ToString();
                if (!string.IsNullOrEmpty(url))
                {
                    Logger.Instance.Info($"Opening system browser, link = {url}");
                    Helpers.OpenSystemBrowser(new Uri(url));
                }
                else
                {
                    Logger.Instance.Error("Can't open system browser, url is null or empty");
                }
            }
        }

        private void CloseAllTabsButThis(object sender, RoutedEventArgs e)
        {
            TabItem? found = null;
            var mi = sender as MenuItem;
            var tb = (mi?.Tag as TabItem)?.Header as TextBlock;
            Logger.Instance.Info($"Closing all tabs but {tb?.Text}");
            foreach (TabItem item in ChromeTabCtrl.Items)
            {
                if (mi!= null && item == mi.Tag)
                {
                    found = item; break;
                }
                else
                {
                    ReleaseTab(item);
                }
            }

            ChromeTabCtrl.Items.Clear();

            if (found != null)
            {
                ChromeTabCtrl.Items.Add(found);
                ChromeTabCtrl.SelectedItem = found;
            }
        }

        private void CloseAllTabs(object sender, RoutedEventArgs e)
        {
            Logger.Instance.Info("Closing all tabs");
            foreach (TabItem item in ChromeTabCtrl.Items)
            {
                ReleaseTab(item);
            }
            ChromeTabCtrl.Items.Clear();
        }

        private void CopyUri_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                var uri = menuItem.Tag.ToString();
                Logger.Instance.Info($"Copy Uri {uri}");
                Clipboard.SetText(uri);
            }
        }

        private void Tab_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && ChromeTabCtrl.SelectedItem == sender)
            {
                WebTabItem? found = sender as WebTabItem;
                if (found != null)
                {
                    var browser = found.Content as ChromiumWebBrowser;
                    if (browser != null)
                        browser.Address = found.Uri;
                }
            }
        }

        private void MenuClose_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                var item = menuItem.Tag as TabItem;
                if (item != null)
                {
                    var tb = item.Header as TextBlock;
                    Logger.Instance.Info($"Closing tab. {tb?.Text}");
                    ReleaseTab(item);
                    ChromeTabCtrl.Items.Remove(menuItem.Tag);
                }
            }
        }

        private void ReleaseTab(TabItem item)
        {
            item.MouseLeftButtonDown -= Tab_MouseLeftButtonDown;
            var chrome = item.Content as ChromiumWebBrowser;
            if (chrome != null)
            {
                var menuHandler = chrome.MenuHandler as ChromeTabMenuHandler;
                chrome.MenuHandler = null;
            }
            item.Content = null;
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            var selectedTab = (AzureObjectTabCtrl.SelectedItem as TabItem)?.Content as AzureObjectList;
            selectedTab?.ResetView();
        }

        Window? m_logWindow = null;
        private void ViewLog_Click(object sender, RoutedEventArgs e)
        {
            if (m_logWindow != null)
            {
                m_logWindow.Close();
                m_logWindow = null;
            }

            m_logWindow = new Window();
            var logTxt = new TextBox();
            logTxt.IsReadOnly = true;
            logTxt.DataContext = DataContext;
            logTxt.SetBinding(TextBox.TextProperty, new Binding("LogBuffer") { Mode = BindingMode.OneWay });
            logTxt.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            logTxt.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            m_logWindow.Content = logTxt;
            m_logWindow.Show();
            logTxt.ScrollToEnd();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        Rect m_notMaximizedPosition = new Rect();
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
            }
            else
            {
                m_notMaximizedPosition = new Rect(Left, Top, Width, Height);
                SystemCommands.MaximizeWindow(this);
            }
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DragMoveInternal()
        {
            if (Mouse.DirectlyOver is not GridSplitter)
            {
                if (WindowState == WindowState.Maximized)
                {
                    var pos = Mouse.GetPosition(this);

                    Top = m_notMaximizedPosition.Top * pos.Y / m_notMaximizedPosition.Height;
                    Left = m_notMaximizedPosition.Left * pos.X / m_notMaximizedPosition.Width;
                    Width = m_notMaximizedPosition.Width;
                    Height = m_notMaximizedPosition.Height;
                    SystemCommands.RestoreWindow(this);
                }
                else
                {
                    DragMove();
                }
            }
        }

        int mouseMoveEventCount = 0;
        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                mouseMoveEventCount++;

                if (mouseMoveEventCount > 2)
                {
                    DragMoveInternal();
                }
            }
            else
            {
                mouseMoveEventCount = 0;
            }
        }

        private void OpenAzure_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;

            if (vm != null)
            {
                AddOrSelectTab(vm.OrganizationUrl, "Azure");
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var depObj = e.MouseDevice.DirectlyOver as DependencyObject;
            if (depObj != null)
            {
                var listView = Helpers.FindParent<ListView>(depObj);
                var webBrowser = Helpers.FindParent<WebBrowser>(depObj);

                if (listView == null && webBrowser == null)
                {
                    if (!m_bItemDblClick)
                    {
                        Maximize_Click(sender, e);
                    }
                    else
                    {
                        m_bItemDblClick = false;
                    }
                }
            }
        }
    }
}

internal class ChromeTabMenuHandler : ContextMenuHandler
{
    public ChromeTabMenuHandler():base()
    {
    }

    protected override void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
    {
        model.Remove(CefMenuCommand.ViewSource);
        model.Remove(CefMenuCommand.Print);
        model.AddItem(CefMenuCommand.CustomFirst, "Copy Url");
        model.AddItem(CefMenuCommand.CustomFirst+1, "Open System Browser");
    }

    protected override void ExecuteCommand(IBrowser browser, ContextMenuExecuteModel model)
    {
        if (model.MenuCommand == CefMenuCommand.CustomFirst) //copy url
        {
            Clipboard.SetText(browser.MainFrame.Url);
        }
        else if (model.MenuCommand == CefMenuCommand.CustomFirst + 1)
        {
            Helpers.OpenSystemBrowser(new Uri(browser.MainFrame.Url));
        }
        else
        {
            base.ExecuteCommand(browser, model);
        }
    }
}

