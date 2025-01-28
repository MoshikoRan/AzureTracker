using AzureTracker.Properties;
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
                return Settings.Default.LicenseText;
            }
        }

        public string LicenseLink
        {
            get
            {
                return Settings.Default.LicenseLink;
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
            Helpers.OpenChrome(e.Uri);
            e.Handled = true;
        }
    }
}
