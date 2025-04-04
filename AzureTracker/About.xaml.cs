﻿using AzureTracker.Properties;
using AzureTracker.Utils;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models.Process;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AzureTracker
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
        }

        public string PayPalDonationUrl
        {
            get
            {
                return @"https://www.paypal.com/donate/?business=MX9AV9RNQMFCW&no_recurring=0&item_name=If+you+like+this+application+and+would+like+to+support+me+in+developing+new+features+and+applications%2C+please+donate+%3A%29&currency_code=ILS";
            }
        }

        public string LicenseText
        {
            get
            {
                return @"Copyright © 2025. All rights reserved to Moshe Ran.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";
            }
        }

        public string LicenseLink
        {
            get
            {
                return @"https://opensource.org/license/mit";
            }
        }
        private void License_Click(object sender, RoutedEventArgs e)
        {
            License.Visibility = Visibility.Visible;
            Donate.Visibility = Visibility.Collapsed;
        }

        private void Donate_Click(object sender, RoutedEventArgs e)
        {
            License.Visibility = Visibility.Collapsed;
            Donate.Visibility = Visibility.Visible;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Helpers.OpenSystemBrowser(e.Uri);
            e.Handled = true;
        }
    }
}
