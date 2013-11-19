using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSWindowTitleChanger
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	///
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the 
	/// IVsPackage interface and uses the registration attributes defined in the framework to 
	/// register itself and its components with the shell.
	/// </summary>
	// This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
	// a package.
	[PackageRegistration(UseManagedResourcesOnly = true)]
	// In order be loaded inside Visual Studio in a machine that has not the VS SDK installed, 
	// package needs to have a valid load key (it can be requested at 
	// http://msdn.microsoft.com/vstudio/extend/). This attributes tells the shell that this 
	// package has a load key embedded in its resources.
	[ProvideLoadKey("Standard", "1.0", "Visual Studio Window Title Changer", "WoofWoof", 1)]
	// This attribute is used to register the informations needed to show the this package
	// in the Help/About dialog of Visual Studio.
	// TODO: check if the first false parameter hides the aboutbox info in VS2010
	[InstalledProductRegistration(false, "#110", "#112", "1.0", IconResourceID = 400)]
	[Guid(GuidList.guidVSWindowTitleChangerPkgString)]
	[ProvideOptionPage(typeof(ToolOptions), "VS Window Title Changer", "Settings", 0, 0, true)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.DesignMode)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.EmptySolution)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.NoSolution)]
	[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists)]
	public class VSWindowTitleChangerPackage : Package, IVsSolutionEvents, IVsDebuggerEvents
	{
		public VSWindowTitleChangerPackage()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
		}

		private TitleFormatter m_TitleFormatter;
		private VSMainWindow m_VSMainWindow;
		private Dispatcher m_UIThradDispatcher;
		private DispatcherTimer m_DispatcherTimer;
		private bool m_Debug = false;

		private const int UPDATE_PERIOD_MILLISECS = 3000;
		private const int DEBUG_UPDATE_PERIOD_MILLISECS = 200;

		private void UpdateWindowTitle()
		{
			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			string solution_path, temp_solution_dir, temp_solution_options;
			if (VSConstants.S_OK != vs_solution.GetSolutionInfo(out temp_solution_dir, out solution_path, out temp_solution_options) ||
				solution_path == null)
				solution_path = "";

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			DBGMODE[] adbgmode = new DBGMODE[] { DBGMODE.DBGMODE_Design };
			if (VSConstants.S_OK != debugger.GetMode(adbgmode))
				adbgmode[0] = DBGMODE.DBGMODE_Design;
			DBGMODE dbgmode = adbgmode[0] & ~DBGMODE.DBGMODE_EncMask;

			ToolOptions options = (ToolOptions)GetDialogPage(typeof(ToolOptions));
			if (m_Debug != options.Debug)
			{
				m_Debug = options.Debug;
				int update_millis = m_Debug ? DEBUG_UPDATE_PERIOD_MILLISECS : UPDATE_PERIOD_MILLISECS;
				m_DispatcherTimer.Interval = new TimeSpan(0, 0, 0, update_millis/1000, update_millis%1000);
			}

			TitleFormatter.Input input = new TitleFormatter.Input();
			input.options = options;
			input.solution_path = solution_path;
			input.dbgmode = dbgmode;
			string title = m_TitleFormatter.FormatTitle(ref input);

			DTE dte = (DTE)GetService(typeof(DTE));
			title += " ";
			Document doc = dte.ActiveDocument;
			if (doc != null)
				title += doc.FullName;

			m_VSMainWindow.SetTitle(title);
		}

		private delegate void MyAction();

		void Schedule_UpdateWindowTitle()
		{
			m_UIThradDispatcher.BeginInvoke(new MyAction(delegate() { UpdateWindowTitle(); }));
		}

		private void dispatcherTimer_Tick(object sender, EventArgs e)
		{
			UpdateWindowTitle();
		}

		private uint m_SolutionEventsCookie;
		private uint m_DebuggerEventsCookie;

		private void DelayedInit()
		{
			m_TitleFormatter = new TitleFormatter();

			m_DispatcherTimer = new DispatcherTimer();
			m_DispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
			// Update every X seconds to handle unexpected window title changes
			int update_millis = m_Debug ? DEBUG_UPDATE_PERIOD_MILLISECS : UPDATE_PERIOD_MILLISECS;
			m_DispatcherTimer.Interval = new TimeSpan(0, 0, 0, update_millis / 1000, update_millis % 1000);
			m_DispatcherTimer.Start();

			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			vs_solution.AdviseSolutionEvents(this, out m_SolutionEventsCookie);

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			debugger.AdviseDebuggerEvents(this, out m_DebuggerEventsCookie);

			DTE dte = (DTE)GetService(typeof(DTE));
			m_VSMainWindow = new VSMainWindow();
			m_VSMainWindow.Initialize((IntPtr)dte.MainWindow.HWnd);

			UpdateWindowTitle();
		}

		protected override void Initialize()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
			base.Initialize();

			m_UIThradDispatcher = Dispatcher.CurrentDispatcher;
			// We do delayed initialization because DTE is currently null...
			m_UIThradDispatcher.BeginInvoke(new MyAction(delegate() { DelayedInit(); }));
		}

		protected override void Dispose(bool disposing)
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Dispose() of: {0}", this.ToString()));

			if (m_DispatcherTimer != null)
				m_DispatcherTimer.Stop();

			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			if (vs_solution != null)
				vs_solution.UnadviseSolutionEvents(m_SolutionEventsCookie);

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			if (debugger != null)
				debugger.UnadviseDebuggerEvents(m_DebuggerEventsCookie);

			if (m_VSMainWindow != null)
				m_VSMainWindow.Deinitialize();
		}

		// IVsSolutionEvents
		public virtual int OnAfterCloseSolution(object pUnkReserved)
		{
			Schedule_UpdateWindowTitle();
			return VSConstants.S_OK;
		}
		public virtual int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
		{
			Schedule_UpdateWindowTitle();
			return VSConstants.S_OK;
		}
		public virtual int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnBeforeCloseSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}
		public virtual int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}
		// ~IVsSolutionEvents

		// IVsDebuggerEvents
		public virtual int OnModeChange(DBGMODE dbgmodeNew)
		{
			Schedule_UpdateWindowTitle();
			return VSConstants.S_OK;
		}
		// ~IVsDebuggerEvents
	}
}
