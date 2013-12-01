using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Forms;

namespace VSWindowTitleChanger
{

//warning CS0618: 'Microsoft.VisualStudio.Shell.InstalledProductRegistrationAttribute.InstalledProductRegistrationAttribute(bool, string, string, string)' is obsolete: 'This InstalledProductRegistrationAttribute constructor has been deprecated. Please use other constructor instead.'
#pragma warning disable 618

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
	[ProvideLoadKey("Standard", "1.1", "Visual Studio Window Title Changer", "WoofWoof", 1)]
	// This attribute is used to register the informations needed to show the this package
	// in the Help/About dialog of Visual Studio.
	// TODO: check if the first false parameter hides the aboutbox info in VS2010
	[InstalledProductRegistration(false, "#110", "#112", "1.1", IconResourceID = 400)]
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

		public object GetInterface(Type interface_type)
		{
			return GetService(interface_type);
		}

		internal VSMainWindow VSMainWindow
		{
			get
			{
				return m_VSMainWindow;
			}
		}

		public TitleSetup GetTitleSetupFromOptions()
		{
			ToolOptions options = (ToolOptions)GetDialogPage(typeof(ToolOptions));
			return options.TitleSetup;
		}

		public void SaveTitleSetupToOptions(TitleSetup title_setup)
		{
			ToolOptions options = (ToolOptions)GetDialogPage(typeof(ToolOptions));
			options.TitleSetup = title_setup;
			options.SaveSettingsToStorage();
		}

		private VSMainWindow m_VSMainWindow;
		private Dispatcher m_UIThradDispatcher;
		private DispatcherTimer m_DispatcherTimer;
		private bool m_Debug = false;

		// Some property changes are detected only through updates...
		private const int UPDATE_PERIOD_MILLISECS = 1000;
		private const int DEBUG_UPDATE_PERIOD_MILLISECS = 200;

		private void UpdateWindowTitle()
		{
			ToolOptions options = (ToolOptions)GetDialogPage(typeof(ToolOptions));
			if (m_Debug != options.Debug)
			{
				m_Debug = options.Debug;
				int update_millis = m_Debug ? DEBUG_UPDATE_PERIOD_MILLISECS : UPDATE_PERIOD_MILLISECS;
				m_DispatcherTimer.Interval = new TimeSpan(0, 0, 0, update_millis/1000, update_millis%1000);
			}

			PackageGlobals globals = PackageGlobals.Instance();

			PackageGlobals.VSMultiInstanceInfo multi_instance_info;
			globals.GetVSMultiInstanceInfo(out multi_instance_info);

			bool extension_active;
			switch (options.ExtensionActivationRule)
			{
				case EExtensionActivationRule.ActiveWithMultipleVSInstances:
					extension_active = multi_instance_info.multiple_instances;
					break;
				case EExtensionActivationRule.ActiveWithMultipleVSInstancesOfTheSameVersion:
					extension_active = multi_instance_info.multiple_instances_same_version;
					break;
				case EExtensionActivationRule.AlwaysInactive:
					extension_active = false;
					break;
				default:
					extension_active = true;
					break;
			}

			string title = null;

			if (extension_active)
			{
				ExpressionEvaluator.EvalContext eval_ctx = new ExpressionEvaluator.EvalContext();
				globals.SetVariableValuesFromIDEState(eval_ctx, multi_instance_info);
				TitleSetup title_setup = globals.TitleSetup;
				ExpressionEvaluator.Expression title_expr = globals.CompiledExpressionCache.GetEntry(title_setup.TitleExpression).expression;
				if (title_expr != null)
				{
					ExpressionEvaluator.SafeEvalContext safe_eval_ctx = new ExpressionEvaluator.SafeEvalContext(eval_ctx);
					ExpressionEvaluator.Value title_val = title_expr.Evaluate(safe_eval_ctx);
					title = title_val.ToString();
				}
			}

			if (title == null)
				title = m_VSMainWindow.OriginalTitle;

			if (options.Debug)
				title += " [VSWindowTitleChanger_DebugMode]";

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
			DTE dte = (DTE)GetService(typeof(DTE));
			if (dte == null)
			{
				// Usually this branch never executes but I want to make sure...
				m_UIThradDispatcher.BeginInvoke(new MyAction(delegate() { DelayedInit(); }));
				return;
			}

			m_VSMainWindow = new VSMainWindow();
			m_VSMainWindow.Initialize((IntPtr)dte.MainWindow.HWnd);

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

			PackageGlobals.InitInstance(this);

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

			PackageGlobals.DeinitInstance();

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
			Schedule_UpdateWindowTitle();
			return VSConstants.S_OK;
		}
		public virtual int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		{
			Schedule_UpdateWindowTitle();
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

#if DEBUG_GUI

		[STAThread]
		static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.Run(new TitleSetupEditor());
		}

#elif DEBUG_EXPRESSION_EVALUATOR

		static void Main(string[] args)
		{
			try
			{
				new VSWindowTitleChanger.ExpressionEvaluator.ExpressionEvaluatorTest().Execute();
			}
			catch (System.Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			Console.ReadLine();
		}

#endif
	}

}
