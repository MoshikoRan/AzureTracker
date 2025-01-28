﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AzureTracker.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.11.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Cathworks")]
        public string Organization {
            get {
                return ((string)(this["Organization"]));
            }
            set {
                this["Organization"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Bug;User Story;Task;Epic;Feature")]
        public string WorkItemTypes {
            get {
                return ((string)(this["WorkItemTypes"]));
            }
            set {
                this["WorkItemTypes"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("30")]
        public double BuildNotOlderThanDays {
            get {
                return ((double)(this["BuildNotOlderThanDays"]));
            }
            set {
                this["BuildNotOlderThanDays"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1234567890")]
        public string PAT {
            get {
                return ((string)(this["PAT"]));
            }
            set {
                this["PAT"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"
                  {
                  ""defaultfilters"": [
                  {
                  ""type"": ""WorkItem"",
                  ""fields"": [
                  {
                  ""Status"": ""Active;New;Ready""
                  }
                  ]
                  },
                  {
                  ""type"": ""PR"",
                  ""fields"": [
                  {
                  ""IsDraft"": ""false""
                  },
                  {
                  ""Status"": ""Active""
                  }
                  ]
                  },
                  {
                  ""type"": ""Build"",
                  ""fields"": [
                  {
                  ""RepoName"": ""Repo1;Repo2;Repo3""
                  }
                  ]
                  }
                  ]
                  }")]
        public string DefaultFilters {
            get {
                return ((string)(this["DefaultFilters"]));
            }
            set {
                this["DefaultFilters"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"Copyright © 2025 Moshe Ran

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.")]
        public string LicenseText {
            get {
                return ((string)(this["LicenseText"]));
            }
            set {
                this["LicenseText"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://opensource.org/license/mit")]
        public string LicenseLink {
            get {
                return ((string)(this["LicenseLink"]));
            }
            set {
                this["LicenseLink"] = value;
            }
        }
    }
}
