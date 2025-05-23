﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AzureTracker.Utils
{
    internal class Helpers
    {
        static public T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T? parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }
        static public IEnumerable<string> GetRecommendedPrograms(string ext)
        {
            //Search programs names:
            List<string> progs = GetProgs(ext);
            if (progs.Count == 0)
                return progs;

            //Search paths:
            List<string> progPaths = GetProgPaths(progs);

            if (progPaths.Count == 0)
            {
                //Search paths program files
                progPaths = GetProgPathsFromProgramFiles(progs);
            }

            if (progPaths.Count == 0)
            {
                //Search paths system folder
                progPaths = GetProgPathsFromSystem(progs);
            }
            return progPaths;
        }

        private static List<string> GetProgPathsFromSystem(List<string> progs)
        {
            List<string> progPaths = GetProgPathFromFolder(progs, Environment.GetFolderPath(Environment.SpecialFolder.System), false);

            if (progPaths.Count == 0)
            {
                progPaths = GetProgPathFromFolder(progs, Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), false);
            }

            return progPaths;
        }

        private static List<string> GetProgPathsFromProgramFiles(List<string> progs)
        {
            List<string> progPaths = GetProgPathFromFolder(progs,Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

            if (progPaths.Count == 0)
            {
                progPaths = GetProgPathFromFolder(progs, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            }

            return progPaths;
        }

        private static List<string> GetProgPathFromFolder(List<string> progs, string folder, bool bRecursive = true)
        {
            List<string> progPaths = new List<string>();

            foreach (string prog in progs)
            {
                foreach (string dir in 
                    bRecursive ? Directory.GetDirectories(folder) : new string[] { folder} )
                {
                    try
                    {
                        var files = Directory.GetFiles(dir, prog, bRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                        if (files.Length > 0)
                        {
                            progPaths.Add(files[0].ToString());
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error(e.Message + $" dir = {dir}, prog = {prog}");
                    }
                }
            }
            return progPaths;
        }

        const string FileExt = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\";
        static private List<string> GetProgs(string ext)
        {
            List<string> progs = new List<string>();
            string baseKey = FileExt + ext;

            using (RegistryKey? rkey = Registry.CurrentUser?.OpenSubKey(baseKey + @"\OpenWithList"))
            {
                if (rkey != null)
                {
                    string? mruList = (string?)rkey.GetValue("MRUList");
                    if (mruList != null)
                    {
                        foreach (char c in mruList)
                        {
                            string? s = (string?)rkey.GetValue(c.ToString());
                            if (s != null && s.ToLower().Contains(".exe"))
                                progs.Add(s);
                        }
                    }
                }
            }
            return progs;
        }

        static private List<string> GetProgPaths(List<string> progs)
        {
            List<string> progPaths = new List<string>();
            const string baseKey = @"Software\Classes\Applications\{0}\shell\open\command";

            foreach (string prog in progs)
            {
                using (RegistryKey? rkey = Registry.CurrentUser?.OpenSubKey(string.Format(baseKey, prog)))
                {
                    if (rkey != null)
                    {
                        string? s = (string?)rkey.GetValue("");
                        if (s != null)
                        {
                            //remove quotes
                            progPaths.Add(s.Substring(1, s.IndexOf("\"", 2) - 1));
                        }
                    }
                }
            }
            return progPaths;
        }

        static public void OpenSystemBrowser(Uri? uri)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = GetSystemDefaultBrowser();
                psi.Arguments = uri?.ToString();
                Process.Start(psi);
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e.Message);
            }
        }

        static string? GetSystemDefaultBrowser()
        {
            string? name = string.Empty;
            RegistryKey? regKey = null;

            try
            {
                //set the registry key we want to open
                regKey = Registry.ClassesRoot.OpenSubKey("HTTP\\shell\\open\\command", false);

                //get rid of the enclosing quotes
                name = regKey?.GetValue(null)?.ToString()?.ToLower()?.Replace("" + (char)34, "");

                //check to see if the value ends with .exe (this way we can remove any command line arguments)
                if (name != null && !name.EndsWith("exe"))
                    //get rid of all command line arguments (anything after the .exe must go)
                    name = name.Substring(0, name.LastIndexOf(".exe") + 4);

            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex.Message);
                name = string.Empty;
            }
            finally
            {
                //check and see if the key is still open, if so
                //then close it
                regKey?.Close();
            }
            //return the value
            return name;
        }

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;
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
