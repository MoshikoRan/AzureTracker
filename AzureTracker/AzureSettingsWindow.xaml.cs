using AzureTracker.Utils;
using System.Windows;
using System.Windows.Controls;

namespace AzureTracker
{
    /// <summary>
    /// Interaction logic for AzureSettingsWindow.xaml
    /// </summary>
    public partial class AzureSettingsWindow : Window
    {
        public AzureSettingsWindow(AzureSettingsVM azureSettingsVM)
        {
            this.DataContext = azureSettingsVM;
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Helpers.OpenSystemBrowser(e.Uri);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void PasswordChangedHandler(object sender, RoutedEventArgs e)
        {
           (this.DataContext as AzureSettingsVM).PAT = (sender as PasswordBox).Password;
        }
    }
}
