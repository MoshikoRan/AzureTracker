﻿using CefSharp;
using CefSharp.Wpf;
using CefSharp.Wpf.Handler;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            }
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
                    TabItem ti = new TabItem();
                    ti.Header = key.ToString();
                    ti.Content = aol;
                    ti.Width = 100;
                    AzureObjectTabCtrl.Items.Add(ti);
                }
            }
        }

        private void ItemDblClick(AzureObjectBase? aob)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                vm.SyncAzureObject(aob);
            }
            OnViewItem(aob);
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
            var uri = aob?.Uri?.AbsoluteUri;
            WebTabItem? found = GetWebTabItemByUri(uri);
            if (found == null)
            {
                found = CreatNewTab(uri, GetTabHeader(aob));
                ChromeTabCtrl.Items.Add(found);
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
            found.MouseLeftButtonDown += Tab_MouseLeftButtonDown;
            found.Header = headerTB;
            found.Content = chromeTab;
            found.Uri = uri;
            found.ToolTip = uri;

            //copy uri
            var menuItemCopy = new MenuItem();
            menuItemCopy.Header = "Copy Uri";
            menuItemCopy.Tag = uri;
            menuItemCopy.Click += CopyUri_Click;

            //close currnt tab
            var menuItemClose = new MenuItem();
            menuItemClose.Header = "Close";
            menuItemClose.Tag = found;
            menuItemClose.Click += Close_Click;

            //close all tabs
            var menuItemCloseAll = new MenuItem();
            menuItemCloseAll.Header = "Close all tabs";
            menuItemCloseAll.Tag = found;
            menuItemCloseAll.Click += CloseAllTabs;

            //close all tabs but current
            var menuItemCloseAllButThis = new MenuItem();
            menuItemCloseAllButThis.Header = "Close all tabs but this";
            menuItemCloseAllButThis.Tag = found;
            menuItemCloseAllButThis.Click += CloseAllTabsButThis;

            found.ContextMenu = new ContextMenu();
            found.ContextMenu.Items.Add(menuItemCopy);
            found.ContextMenu.Items.Add(menuItemClose);
            found.ContextMenu.Items.Add(menuItemCloseAll);
            found.ContextMenu.Items.Add(menuItemCloseAllButThis);

            return found;
        }

        private void CloseAllTabsButThis(object sender, RoutedEventArgs e)
        {
            TabItem? found = null;
            foreach (TabItem item in ChromeTabCtrl.Items)
            {
                var mi = sender as MenuItem;
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
                Clipboard.SetText(menuItem.Tag.ToString());
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                var item = menuItem.Tag as TabItem;
                if (item != null)
                {
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
            m_logWindow.Content = logTxt;
            m_logWindow.Show();
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
    }
}
