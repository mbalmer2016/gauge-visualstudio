﻿// Copyright [2014, 2015] [ThoughtWorks Inc.](www.thoughtworks.com)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Gauge.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [Guid(GuidList.GuidGaugeVsPackagePkgString)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideEditorFactory(typeof(GaugeEditorFactory), 112)]
    [ProvideEditorLogicalView(typeof(GaugeEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
    [ProvideEditorExtension(typeof(GaugeEditorFactory), GaugeContentTypeDefinitions.SpecFileExtension, 32)]
    [ProvideEditorExtension(typeof(GaugeEditorFactory), GaugeContentTypeDefinitions.ConceptFileExtension, 32)]
    [ProvideEditorExtension(typeof(GaugeEditorFactory), GaugeContentTypeDefinitions.MarkdownFileExtension, 32)]
    [ProvideLanguageService(typeof(GaugeLanguageInfo), GaugeLanguageInfo.LanguageName, GaugeLanguageInfo.LanguageResourceId,
        DefaultToInsertSpaces = true,
        EnableLineNumbers = true,
        RequestStockColors = true)]
    public class GaugePackage : Package, IDisposable
    {
        private Events2 _DTEEvents;
        private SolutionsEventListener _solutionsEventListener;
        private FormatMenuCommand formatMenuCommand;
        private bool _disposed;

        protected override void Initialize()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", ToString()));
            base.Initialize();

            DTE = (DTE) GetService(typeof (DTE));

            // Add our command handlers for menu (commands must exist in the .vsct file)
            formatMenuCommand = new FormatMenuCommand(this);
            formatMenuCommand.Register();

            RegisterEditorFactory(new GaugeEditorFactory(this));

            _solutionsEventListener = new SolutionsEventListener();
        }

        public static DTE DTE { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _solutionsEventListener.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
