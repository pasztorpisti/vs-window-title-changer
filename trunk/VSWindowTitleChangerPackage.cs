using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using VSWindowTitleChanger.ExpressionEvaluator;

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
	[ProvideLoadKey("Standard", "2.0", "Visual Studio Window Title Changer", "WoofWoof", 1)]
	// This attribute is used to register the informations needed to show the this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration(false, "#110", "#112", "2.1.0", IconResourceID = 400)]
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

		public object GetInterface(System.Type interface_type)
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


		ExpressionCompilerThread m_ExpressionCompilerThread;
		CompiledExpressionCache m_CompiledExpressionCache;

		VSMainWindow m_VSMainWindow;
		System.Windows.Forms.Timer m_UpdateTimer;

		// Some property changes are detected only through updates...
		const int UPDATE_PERIOD_MILLISECS = 1000;

		static bool CompareVariables(Dictionary<string, Value> vars0, Dictionary<string, Value> vars1)
		{
			if (vars0.Count != vars1.Count)
				return false;
			foreach (KeyValuePair<string, Value> kv in vars0)
			{
				Value val1;
				if (!vars1.TryGetValue(kv.Key, out val1))
					return false;
				if (0 != kv.Value.CompareTo(val1))
					return false;
			}
			return true;
		}

		string m_PrevTitleExpressionStr = "";
		bool m_ExpressionContainsExec;
		Dictionary<string, Value> m_PrevVariableValues = new Dictionary<string, Value>();
		EExtensionActivationRule m_PrevExtensionActivationRule = EExtensionActivationRule.AlwaysInactive;

		private void UpdateWindowTitle()
		{
			m_VSMainWindow.UpdateAppActive();

			PackageGlobals globals = PackageGlobals.Instance();
			EvalContext eval_ctx = globals.CreateFreshEvalContext();
			bool variables_changed = !CompareVariables(eval_ctx.VariableValues, m_PrevVariableValues);

			if (variables_changed)
			{
				m_PrevVariableValues = eval_ctx.VariableValues;
				globals.TitleSetupEditor.Variables = eval_ctx.VariableValues;
			}
	
			ToolOptions options = (ToolOptions)GetDialogPage(typeof(ToolOptions));

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

			if (extension_active)
			{
				TitleSetup title_setup = globals.TitleSetup;
				if (m_ExpressionContainsExec || variables_changed || m_PrevExtensionActivationRule != options.ExtensionActivationRule ||
					title_setup.TitleExpression != m_PrevTitleExpressionStr)
				{
					Parser parser = new Parser(title_setup.TitleExpression, globals.ExecFuncEvaluator, globals.CompileTimeConstants); ;
					ExpressionCompilerJob job = new ExpressionCompilerJob(parser,  globals.CreateFreshEvalContext(), false, m_CompiledExpressionCache);
					job.OnCompileFinished += OnCompileFinished;
					m_ExpressionCompilerThread.RemoveAllJobs();
					m_ExpressionCompilerThread.AddJob(job);
				}
				m_PrevTitleExpressionStr = title_setup.TitleExpression;
			}
			else
			{
				m_VSMainWindow.SetTitle(m_VSMainWindow.OriginalTitle);
			}

			m_PrevExtensionActivationRule = options.ExtensionActivationRule;
		}

		void OnCompileFinished(ExpressionCompilerJob job)
		{
			if (m_VSMainWindow == null)
				return;
			m_ExpressionContainsExec = job.ContainsExec;
			Value title_value = job.EvalResult;
			if (title_value != null)
			{
				string title = title_value.ToString();
				m_VSMainWindow.SetTitle(title);
				Debug.WriteLine("Updating the titlebar");
			}
			else
			{
				m_VSMainWindow.SetTitle(m_VSMainWindow.OriginalTitle);
			}
		}

		void Schedule_UpdateWindowTitle()
		{
			PackageGlobals.BeginInvokeOnUIThread(UpdateWindowTitle);
		}

		private void UpdateTimer_Tick(object sender, EventArgs e)
		{
			UpdateWindowTitle();
		}

		void m_VSMainWindow_OnWindowTitleUpdateNeeded()
		{
			Schedule_UpdateWindowTitle();
		}

		private uint m_SolutionEventsCookie;
		private uint m_DebuggerEventsCookie;

		private void DelayedInit()
		{
			DTE dte = (DTE)GetService(typeof(DTE));
			if (dte == null)
			{
				// Usually this branch never executes but I want to make sure...
				PackageGlobals.BeginInvokeOnUIThread(DelayedInit);
				return;
			}

			m_VSMainWindow = new VSMainWindow();
			m_VSMainWindow.Initialize((IntPtr)dte.MainWindow.HWnd);
			m_VSMainWindow.OnWindowTitleUpdateNeeded += m_VSMainWindow_OnWindowTitleUpdateNeeded;

			m_UpdateTimer = new System.Windows.Forms.Timer();
			m_UpdateTimer.Tick += UpdateTimer_Tick;
			m_UpdateTimer.Interval = UPDATE_PERIOD_MILLISECS;
			m_UpdateTimer.Start();

			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			vs_solution.AdviseSolutionEvents(this, out m_SolutionEventsCookie);

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			debugger.AdviseDebuggerEvents(this, out m_DebuggerEventsCookie);

			PackageGlobals.InitInstance(this);

			m_PrevVariableValues = PackageGlobals.Instance().CreateFreshEvalContext().VariableValues;

			m_ExpressionCompilerThread = new ExpressionCompilerThread();
			// During normal use the expression doesn't change except when configuring so a cache size of 1 does the job quite well.
			// Usually what changes is the variables.
			m_CompiledExpressionCache = new CompiledExpressionCache(PackageGlobals.Instance().ExecFuncEvaluator, PackageGlobals.Instance().CompileTimeConstants, 1);

			UpdateWindowTitle();
		}

		protected override void Initialize()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
			base.Initialize();

			// We do delayed initialization because DTE is currently null...
			PackageGlobals.BeginInvokeOnUIThread(DelayedInit);
		}

		protected override void Dispose(bool disposing)
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Dispose() of: {0}", this.ToString()));
			Debug.Assert(disposing);

			if (m_ExpressionCompilerThread != null)
			{
				m_ExpressionCompilerThread.Dispose();
				m_ExpressionCompilerThread = null;
			}

			PackageGlobals.DeinitInstance();

			if (m_UpdateTimer != null)
				m_UpdateTimer.Stop();

			IVsSolution vs_solution = (IVsSolution)GetService(typeof(IVsSolution));
			if (vs_solution != null)
				vs_solution.UnadviseSolutionEvents(m_SolutionEventsCookie);

			IVsDebugger debugger = (IVsDebugger)GetService(typeof(IVsDebugger));
			if (debugger != null)
				debugger.UnadviseDebuggerEvents(m_DebuggerEventsCookie);

			if (m_VSMainWindow != null)
			{
				m_VSMainWindow.Deinitialize();
				m_VSMainWindow = null;
			}

			base.Dispose(disposing);
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
				new ExpressionEvaluatorTest().Execute();
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
