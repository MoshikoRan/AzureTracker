using System;
using System.Collections.Generic;
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
    /// Interaction logic for CustomFilterWindow.xaml
    /// </summary>
    public partial class CustomFilterWindow : Window
    {
        public CustomFilterWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string CustomFilterName
        {
            get; set;
        } = string.Empty;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtFilterName.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
