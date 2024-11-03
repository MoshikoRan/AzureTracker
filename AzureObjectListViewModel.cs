using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AzureTracker
{
    public class AzureObjectListViewModel : ViewModelBase
    {
        public const string INIT_VIEW = "InitView";
        public const string CLEAR_FILTER = "ClearFilter";

        IEnumerable<AzureObjectBase>? m_lstAzureObjectsBase;
        public IEnumerable<AzureObjectBase>? AzureObjectsBase
        {
            get { return m_lstAzureObjectsBase; }
            set
            {
                if (m_lstAzureObjectsBase != value)
                {
                    m_lstAzureObjectsBase = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AzureObjectsCountStr));
                }
            }
        }

        public int AzureObjectsCount { get { return m_lstAzureObjectsBase == null ? 0 : m_lstAzureObjectsBase.Count(); } }
        public string AzureObjectsCountStr
        {
            get 
            { 
                var count = AzureObjectsCount;
                return $"{count} items";
            }
        }

        Dictionary<string, string> m_dicFilter = new Dictionary<string, string>();
        internal void ResetFilter(Dictionary<string, string> filter)
        {
            m_dicFilter = filter;
        }

        IEnumerable<AzureObjectBase>? m_data = null;

        public void SetData(IEnumerable<AzureObjectBase> data)
        {
            m_data = data;
            FilterData();
            InitView();
        }
        private void FilterData()
        {
            IEnumerable<AzureObjectBase>? filteredData = m_data;
            if ((filteredData != null) && (m_dicFilter != null))
            {
                foreach (var key in m_dicFilter.Keys)
                {
                    string[] values = m_dicFilter[key].Split(';', StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length > 0 && filteredData.Count() > 0)
                    {
                        PropertyInfo? pi = filteredData.First().GetType().GetProperty(key);
                        if (pi != null)
                        {
                            filteredData = filteredData.Where(
                                predicate: p => ApplyFilter(p, pi, values));
                        }
                    }
                }
            }
            AzureObjectsBase = filteredData;
        }

        private bool ApplyFilter(AzureObjectBase aob, PropertyInfo? pi, string[] values)
        {
            bool res = false;
            foreach (string val in values)
            {
                if (!string.IsNullOrWhiteSpace(val))
                {
                    var sVal = pi?.GetValue(aob)?.ToString();
                    if (!string.IsNullOrEmpty(sVal) &&
                        sVal.Contains(val, StringComparison.OrdinalIgnoreCase))
                    {
                        res = true;
                        break;
                    }
                }
            }

            return res;
        }

        internal void SetFilter(string tag, string text)
        {
            m_dicFilter[tag] = text;
            FilterData();
        }

        internal string GetFilter(string tag)
        {
            if (m_dicFilter.ContainsKey(tag))
            {
                return m_dicFilter[tag];
            }
            return string.Empty;
        }

        internal void InitView()
        {
            OnPropertyChanged(INIT_VIEW);
        }

        internal void ClearFilter()
        {
            if (m_dicFilter.Count > 0)
            {
                m_dicFilter.Clear();
                FilterData();
                OnPropertyChanged(CLEAR_FILTER);
            }
        }
    }
}
