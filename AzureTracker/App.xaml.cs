using CefSharp.Wpf;
using CefSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
using System.IO;

namespace PRTracker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (!Cef.IsInitialized)
            {
                string? sExePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                if (!string.IsNullOrEmpty(sExePath))
                {
                    var settings = new CefSettings();
                    settings.CachePath = Path.Combine(sExePath, "cache");
                    settings.LogFile = Path.Combine(sExePath, "log.log");
                    Cef.Initialize(settings);
                }
            }
        }
    }
}
