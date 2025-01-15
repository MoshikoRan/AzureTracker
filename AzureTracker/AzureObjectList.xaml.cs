using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Media;

namespace AzureTracker
{
    /// <summary>
    /// Interaction logic for AzureObjectList.xaml
    /// </summary>
    public partial class AzureObjectList : UserControl
    {
        public AzureObjectList()
        {
            InitializeComponent();
        }

        public delegate void ItemClickEventHandler(AzureObjectBase? aob);
        public event ItemClickEventHandler? ItemClickEvent;

        public delegate void ItemDoubleClickEventHandler(AzureObjectBase? aob);
        public event ItemDoubleClickEventHandler? ItemDoubleClickEvent;
        private void AzureObjectList_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == AzureObjectListViewModel.INIT_VIEW)
            {
                if (lvGridView.Columns.Count == 0)
                {
                    GenerateGridViewColumns();
                }
                if (_lastHeaderClicked != null)
                    SortByHeader(_lastHeaderClicked, _lastDirection);
            }
            else if (e.PropertyName == AzureObjectListViewModel.CLEAR_FILTER)
            {
                GenerateGridViewColumns();
            }
            else if (e.PropertyName == AzureObjectListViewModel.REFRESH_VIEW)
            {
                var lstCtrl = AllChildren(lv).Where(c => c?.Name == "txtFilter");
                foreach (var ctrl in lstCtrl)
                {
                    if (ctrl != null)
                        GVColFilter_Loaded(ctrl, new RoutedEventArgs());
                }
            }
        }
        private List<Control?> AllChildren(DependencyObject parent)
        {
            var list = new List<Control?>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Control)
                {
                    var ctrl = child as Control;
                    list.Add(ctrl);
                }
                list.AddRange(AllChildren(child));
            }
            return list;
        }

        private void GenerateGridViewColumns()
        {
            var vm = this.DataContext as AzureObjectListViewModel;
            if (vm?.AzureObjectsCount > 0)
            {
                AzureObjectBase? aob = vm.AzureObjectsBase?.First();
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    CreateGridViewColumns(aob?.GetType());
                });
            }
        }

        private void ListViewItem_Click(object sender, MouseButtonEventArgs e)
        {
            ItemClickEvent?.Invoke(((ListViewItem)sender).DataContext as AzureObjectBase);
        }

        private void ListViewItem_DblClick(object sender, MouseButtonEventArgs e)
        {
            ItemDoubleClickEvent?.Invoke(((ListViewItem)sender).DataContext as AzureObjectBase);
        }

        GridViewColumnHeader? _lastHeaderClicked;
        ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    ListSortDirection direction;
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }
                    SortByHeader(headerClicked, direction);
                }
            }
        }

        private void SortByHeader(GridViewColumnHeader headerClicked, ListSortDirection direction)
        {
            var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
            var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

            Sort(sortBy, direction);

            if (direction == ListSortDirection.Ascending)
            {
                headerClicked.Column.HeaderTemplate =
                  Resources["HeaderTemplateArrowUp"] as DataTemplate;
            }
            else
            {
                headerClicked.Column.HeaderTemplate =
                  Resources["HeaderTemplateArrowDown"] as DataTemplate;
            }

            // Remove arrow from previously sorted header
            if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
            {
                _lastHeaderClicked.Column.HeaderTemplate = null;
            }

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }
        private void Sort(string? sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
              CollectionViewSource.GetDefaultView(lv.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void CreateGridViewColumns(Type? type)
        {
            lvGridView.Columns.Clear();

            PropertyInfo[]? propertyInfos = type?.GetProperties();
            for (int i = 0; i < propertyInfos?.Length; i++)
            {
                if (propertyInfos[i].Name == "Uri")
                    continue; 

                GridViewColumn col = new GridViewColumn();
                col.Header = propertyInfos[i].Name;
                col.DisplayMemberBinding = new Binding(propertyInfos[i].Name);
                lvGridView.Columns.Add(col);
            }
        }

        private void GVColFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (tb != null && tb.Tag != null)
            {
                string sTag = (string)tb.Tag;
                var vm = this.DataContext as AzureObjectListViewModel;
                vm?.SetFilter(sTag, tb.Text);
            }
        }

        private void GVColFilter_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (tb != null && tb.Tag != null)
            {
                string sTag = (string)tb.Tag;
                var vm = this.DataContext as AzureObjectListViewModel;
                tb.Text = vm?.GetFilter(sTag);
            }
        }

        private void AzureObjectList_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var vm = this.DataContext as AzureObjectListViewModel;
            if (vm != null)
            {
                vm.PropertyChanged += AzureObjectList_PropertyChanged;
            }
        }
        const int MIN_COL_WIDTH = 100;
        internal void ResetView()
        {
            foreach (var col in lvGridView.Columns)
            {
                if (col.Width < MIN_COL_WIDTH)
                    col.Width = MIN_COL_WIDTH;
            }
        }
    }
}
