// Copyright [2014, 2015] [ThoughtWorks Inc.](www.thoughtworks.com)
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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Gauge.VisualStudio.LangaugeService;
using Gauge.VisualStudio.Loggers;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;
using Process = System.Diagnostics.Process;

namespace Gauge.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [Guid(GuidList.GuidGaugeVsPackagePkgString)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideServiceAttribute(typeof(GaugeLanguageService), ServiceName = "Gauge Language Service")]
    [ProvideLanguageServiceAttribute(typeof(GaugeLanguageService),
        "Gauge Language",
        106,
        CodeSense = false,
        RequestStockColors = false,
        EnableCommenting = true,
        EnableAsyncCompletion = true)]
    [ProvideLanguageExtensionAttribute(typeof(GaugeLanguageService), ".spec")]
    [ProvideLanguageExtensionAttribute(typeof(GaugeLanguageService), ".cpt")]
    [ProvideService(typeof(GaugeLanguageService))]
    [ProvideLanguageExtension(typeof(GaugeLanguageService), ".spec")]
    [ProvideLanguageExtension(typeof(GaugeLanguageService), ".cpt")]
    [ProvideLanguageService(typeof(GaugeLanguageService),
        "Gauge Langauge",
        0,
        AutoOutlining = true,
        EnableCommenting = true,
        MatchBraces = true,
        ShowMatchingBrace = true)]
    public sealed class GaugePackage : Package, IOleComponent
    {
        private Events2 _DTEEvents;
        private IVsSolution _solution;
        private readonly SolutionsEventListener _solutionsEventListener = new SolutionsEventListener();
        private uint _solutionCookie;
        private uint m_componentID;

        protected override void Initialize()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", ToString()));
            ErrorListLogger.Initialize(this);

            DTE = (DTE) GetService(typeof (DTE));

            _solution = GetService(typeof(SVsSolution)) as IVsSolution;
            _solution.AdviseSolutionEvents(_solutionsEventListener, out _solutionCookie);

            // Add our command handlers for menu (commands must exist in the .vsct file)
            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                var menuCommandId = new CommandID(GuidList.GuidGaugeVsPackageCmdSet, (int)PkgCmdIdList.formatCommand);
                var menuItem = new OleMenuCommand(MenuItemCallback, menuCommandId);
                menuItem.BeforeQueryStatus += delegate(object sender, EventArgs args)
                {
                    var menuCommand = sender as OleMenuCommand;

                    if (menuCommand == null) return;

                    menuCommand.Visible = false;
                    menuCommand.Enabled = false;

                    string itemFullPath;

                    IVsHierarchy hierarchy;
                    uint itemid;

                    if (!IsSingleProjectItemSelection(out hierarchy, out itemid)) return;

                    ((IVsProject)hierarchy).GetMkDocument(itemid, out itemFullPath);
                    var transformFileInfo = new FileInfo(itemFullPath);

                    var isGaugeFile = string.Compare(".spec", transformFileInfo.Extension, StringComparison.OrdinalIgnoreCase) == 0;
                    if (transformFileInfo.Directory == null) return;

                    if (!isGaugeFile) return;

                    menuCommand.Visible = true;
                    menuCommand.Enabled = true;
                };
                mcs.AddCommand(menuItem);
            }
            base.Initialize();

            // Proffer the Gauge language service.
            IServiceContainer serviceContainer = this as IServiceContainer;
            GaugeLanguageService langService = new GaugeLanguageService();
            langService.SetSite(this);
            serviceContainer.AddService(typeof(GaugeLanguageService), langService, true);

            // Register a timer to call our language service during idle periods.
            IOleComponentManager mgr = GetService(typeof(SOleComponentManager)) as IOleComponentManager;
            if (m_componentID == 0 && mgr != null)
            {
                OLECRINFO[] crinfo = new OLECRINFO[1];
                crinfo[0].cbSize = (uint)Marshal.SizeOf(typeof(OLECRINFO));
                crinfo[0].grfcrf = (uint)_OLECRF.olecrfNeedIdleTime |
                                              (uint)_OLECRF.olecrfNeedPeriodicIdleTime;
                crinfo[0].grfcadvf = (uint)_OLECADVF.olecadvfModal |
                                              (uint)_OLECADVF.olecadvfRedrawOff |
                                              (uint)_OLECADVF.olecadvfWarningsOff;
                crinfo[0].uIdleTimeInterval = 1000;
                int hr = mgr.FRegisterComponent(this, crinfo, out m_componentID);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (m_componentID != 0)
            {
                IOleComponentManager mgr = GetService(typeof(SOleComponentManager))
                                           as IOleComponentManager;
                if (mgr != null)
                {
                    int hr = mgr.FRevokeComponent(m_componentID);
                }
                m_componentID = 0;
            }

            base.Dispose(disposing);
        }

        public static DTE DTE { get; private set; }

        //TODO : Move to a separate class
        private static void MenuItemCallback(object sender, EventArgs e)
        {
            string itemFullPath;

            IVsHierarchy hierarchy;
            uint itemid;

            if (!IsSingleProjectItemSelection(out hierarchy, out itemid)) return;

            ((IVsProject)hierarchy).GetMkDocument(itemid, out itemFullPath);
            var gaugeFile = new FileInfo(itemFullPath);

            var arguments = string.Format(@"--simple-console --format {0}", gaugeFile.Name);
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = gaugeFile.Directory.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    FileName = "gauge.exe",
                    RedirectStandardError = true,
                    Arguments = arguments,
                }
            };

            p.Start();
            p.WaitForExit();
        }

        public static bool IsSingleProjectItemSelection(out IVsHierarchy hierarchy, out uint itemid)
        {
            hierarchy = null;
            itemid = VSConstants.VSITEMID_NIL;

            var monitorSelection = GetGlobalService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            var solution = GetGlobalService(typeof(SVsSolution)) as IVsSolution;
            if (monitorSelection == null || solution == null)
            {
                return false;
            }

            var hierarchyPtr = IntPtr.Zero;
            var selectionContainerPtr = IntPtr.Zero;

            try
            {
                IVsMultiItemSelect multiItemSelect = null;
                var hr = monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid, out multiItemSelect,
                    out selectionContainerPtr);

                if (ErrorHandler.Failed(hr) || hierarchyPtr == IntPtr.Zero || itemid == VSConstants.VSITEMID_NIL)
                {
                    return false;
                }

                if (multiItemSelect != null) return false;

                if (itemid == VSConstants.VSITEMID_ROOT) return false;

                hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
                if (hierarchy == null) return false;

                Guid guidProjectId;

                return !ErrorHandler.Failed(solution.GetGuidOfProject(hierarchy, out guidProjectId));
            }
            finally
            {
                if (selectionContainerPtr != IntPtr.Zero)
                {
                    Marshal.Release(selectionContainerPtr);
                }

                if (hierarchyPtr != IntPtr.Zero)
                {
                    Marshal.Release(hierarchyPtr);
                }
            }
        }


        #region IOleComponent Members

        public int FDoIdle(uint grfidlef)
        {
            bool bPeriodic = (grfidlef & (uint)_OLEIDLEF.oleidlefPeriodic) != 0;
            // Use typeof(GaugeLanguageService) because we need to reference the GUID for our language service.
            LanguageService service = GetService(typeof(GaugeLanguageService)) as LanguageService;
            if (service != null)
            {
                service.OnIdle(bPeriodic);
            }
            return 0;
        }

        public int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked)
        {
            return 1;
        }

        public int FPreTranslateMessage(MSG[] pMsg)
        {
            return 0;
        }

        public int FQueryTerminate(int fPromptUser)
        {
            return 1;
        }

        public int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam)
        {
            return 1;
        }

        public IntPtr HwndGetWindow(uint dwWhich, uint dwReserved)
        {
            return IntPtr.Zero;
        }

        public void OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating,
            OLECHOSTINFO[] pchostinfo, uint dwReserved)
        {
        }

        public void OnEnterState(uint uStateID, int fEnter)
        {
        }

        public void OnAppActivate(int fActive, uint dwOtherThreadID)
        {
        }

        public void OnLoseActivation()
        {
        }

        public void Terminate()
        {
        }
        #endregion
    }
}
