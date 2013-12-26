using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VSWindowTitleChanger.ExpressionEvaluator;
using System.Windows.Forms;
using System.Text;

namespace VSWindowTitleChanger
{
	class PackageGlobals
	{
		// Replace every backslash chars to slashes in the solution pathname before regex matching.
		// This makes it easier to write regex patterns that often overuse backslash chars.
		public const bool SlashPathSeparator = true;

		static PackageGlobals m_Globals;

		public static void InitInstance(VSWindowTitleChangerPackage package)
		{
			Debug.Assert(m_Globals == null);
			if (m_Globals == null)
				m_Globals = new PackageGlobals(package);
		}

		public static void DeinitInstance()
		{
			Debug.Assert(m_Globals != null);
			if (m_Globals != null)
			{
				m_Globals.Cleanup();
				m_Globals = null;
			}
		}

		public static PackageGlobals Instance()
		{
			Debug.Assert(m_Globals != null);
			return m_Globals;
		}

		public delegate void Job();


		class UIThreadDispatcher : UserControl
		{
			public UIThreadDispatcher()
			{
				CreateHandle();
			}
		}
		static Control m_UIThreadDispatcher = new UIThreadDispatcher();

		// Poor man's UI Thread Dispatcher for framework v2
		public static void BeginInvokeOnUIThread(Job action)
		{
			m_UIThreadDispatcher.BeginInvoke(action);
		}

		public TitleSetup TitleSetup
		{
			get
			{
				return m_TitleSetup;
			}
		}

		public void ShowTitleExpressionEditor()
		{
			if (m_TitleSetupEditor.Visible)
			{
				m_TitleSetupEditor.BringToFront();
				return;
			}
			m_TitleSetupEditor.TitleSetup = m_TitleSetup;
			m_TitleSetupEditor.Show();
		}

		internal VariableValueResolver CompileTimeConstants
		{
			get
			{
				return m_CompileTimeConstants;
			}
		}

		public TitleSetupEditor TitleSetupEditor
		{
			get
			{
				return m_TitleSetupEditor;
			}
		}

		VSWindowTitleChangerPackage m_Package;
		TitleSetup m_TitleSetup;
		TitleSetupEditor m_TitleSetupEditor;

		VariableValueResolver m_CompileTimeConstants;

		PackageGlobals(VSWindowTitleChangerPackage package)
		{
			m_Package = package;
			CreateCompileTimeConstants();

			m_TitleSetup = package.GetTitleSetupFromOptions();
			m_TitleSetupEditor = new TitleSetupEditor();
			m_TitleSetupEditor.SaveEditedSetup += SaveEditedSetup;
			m_TitleSetupEditor.RevertToOriginalSetup += RevertToOriginalSetup;

			m_TitleSetupEditor.CustomTabbingEnabled = true;
		}

		void SaveEditedSetup(TitleSetup title_setup)
		{
			m_TitleSetup = title_setup;
			m_Package.SaveTitleSetupToOptions(m_TitleSetup);
		}

		void RevertToOriginalSetup(TitleSetup title_setup)
		{
			m_TitleSetup = title_setup;
		}

		void Cleanup()
		{
			m_TitleSetupEditor.Dispose();
			m_TitleSetupEditor = null;
		}

		void CreateCompileTimeConstants()
		{
			DTE2 dte = (DTE2)m_Package.GetInterface(typeof(DTE));
			m_CompileTimeConstants = new VariableValueResolver();
			m_CompileTimeConstants.SetVariable("true", true);
			m_CompileTimeConstants.SetVariable("false", false);
			m_CompileTimeConstants.SetVariable("dte_version", dte.Version);
			m_CompileTimeConstants.SetVariable("vs_version", DTEVersionToVSYear(dte.Version));
			m_CompileTimeConstants.SetVariable("vs_edition", dte.Edition);
		}

		static string DTEVersionToVSYear(string dte_version)
		{
			switch (dte_version)
			{
				case "8.0": return "2005";
				case "9.0": return "2008";
				case "10.0": return "2010";
				case "11.0": return "2012";
				case "12.0": return "2013";
				default: return dte_version;
			}
		}

		[DllImport("user32.dll")]
		static extern IntPtr GetActiveWindow();

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

		static string GetWindowClassName(IntPtr hWnd)
		{
			// Pre-allocate 256 characters, since this is the maximum class name length.
			StringBuilder class_name = new StringBuilder(256);
			//Get the window class name
			int ret = GetClassName(hWnd, class_name, class_name.Capacity);
			if (ret <= 0)
				return "";
			return class_name.ToString();
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		private static string GetWindowText(IntPtr hWnd)
		{
			int length = GetWindowTextLength(hWnd);
			StringBuilder sb = new StringBuilder(length + 1);
			GetWindowText(hWnd, sb, sb.Capacity);
			return sb.ToString();
		}


		void SetVariableValuesFromIDEState(IVariableValueSetter var_value_setter, VSMultiInstanceInfo multi_instance_info)
		{
			DTE2 dte = (DTE2)m_Package.GetInterface(typeof(DTE));
			IVsSolution vs_solution = (IVsSolution)m_Package.GetInterface(typeof(IVsSolution));
			string solution_path, temp_solution_dir, temp_solution_options;
			if (VSConstants.S_OK != vs_solution.GetSolutionInfo(out temp_solution_dir, out solution_path, out temp_solution_options) || solution_path == null)
				solution_path = "";

			IVsDebugger debugger = (IVsDebugger)m_Package.GetInterface(typeof(IVsDebugger));
			DBGMODE[] adbgmode = new DBGMODE[] { DBGMODE.DBGMODE_Design };
			if (VSConstants.S_OK != debugger.GetMode(adbgmode))
				adbgmode[0] = DBGMODE.DBGMODE_Design;
			DBGMODE dbgmode = adbgmode[0] & ~DBGMODE.DBGMODE_EncMask;

			string active_configuration = "";
			string active_platform = "";
			SolutionConfiguration2 active_cfg = (SolutionConfiguration2)dte.Solution.SolutionBuild.ActiveConfiguration;
			if (active_cfg != null)
			{
				active_configuration = active_cfg.Name == null ? "" : active_cfg.Name; ;
				active_platform = active_cfg.PlatformName == null ? "" : active_cfg.PlatformName;
			}

			Project startup_project = null;
			if (dte.Solution.SolutionBuild.StartupProjects != null)
			{
				Array projects = (Array)dte.Solution.SolutionBuild.StartupProjects;
				if (projects.Length > 0)
					startup_project = dte.Solution.Item(projects.GetValue(0));
			}

			Document active_document = dte.ActiveDocument;

			AddFilePathVars(var_value_setter, ref solution_path, SlashPathSeparator, "sln_");
			var_value_setter.SetVariable("sln_open", solution_path.Length > 0);
			bool sln_dirty = !dte.Solution.Saved;
			var_value_setter.SetVariable("sln_dirty", sln_dirty);

			string active_document_path = active_document == null ? "" : active_document.FullName;
			AddFilePathVars(var_value_setter, ref active_document_path, SlashPathSeparator, "doc_");
			var_value_setter.SetVariable("doc_open", active_document_path.Length > 0);
			var_value_setter.SetVariable("doc_dirty", active_document != null && !active_document.Saved);

			bool any_doc_dirty = false;
			foreach (Document doc in dte.Documents)
			{
				if (!doc.Saved)
				{
					any_doc_dirty = true;
					break;
				}
			}
			var_value_setter.SetVariable("any_doc_dirty", any_doc_dirty);

			string startup_project_path = startup_project == null ? "" : startup_project.FullName;
			AddFilePathVars(var_value_setter, ref startup_project_path, SlashPathSeparator, "startup_proj_");
			var_value_setter.SetVariable("startup_proj", startup_project == null ? "" : startup_project.Name);
			var_value_setter.SetVariable("startup_proj_dirty", startup_project != null && !startup_project.Saved);

			bool any_proj_dirty = false;
			foreach (Project proj in dte.Solution.Projects)
			{
				if (!proj.Saved)
				{
					any_proj_dirty = true;
					break;
				}
			}
			var_value_setter.SetVariable("any_proj_dirty", any_proj_dirty);

			var_value_setter.SetVariable("anything_dirty", sln_dirty || any_proj_dirty || any_doc_dirty);

			var_value_setter.SetVariable("wnd_minimized", m_Package.VSMainWindow.Minimized);
			var_value_setter.SetVariable("wnd_foreground", m_Package.VSMainWindow.IsForegroundWindow());
			var_value_setter.SetVariable("app_active", m_Package.VSMainWindow.IsAppActive);

			bool debugging = false;
			string debug_mode = "";
			switch (dbgmode)
			{
				case DBGMODE.DBGMODE_Run:
					debugging = true;
					debug_mode = "running";
					break;
				case DBGMODE.DBGMODE_Break:
					debugging = true;
					debug_mode = "debugging";
					break;
			}

			var_value_setter.SetVariable("debugging", debugging);
			var_value_setter.SetVariable("debug_mode", debug_mode);

			var_value_setter.SetVariable("configuration", active_configuration);
			var_value_setter.SetVariable("platform", active_platform);

			var_value_setter.SetVariable("orig_title", m_Package.VSMainWindow.OriginalTitle);

			var_value_setter.SetVariable("multi_instances", multi_instance_info.multiple_instances);
			var_value_setter.SetVariable("multi_instances_same_ver", multi_instance_info.multiple_instances_same_version);

			string active_wnd_title = "";
			string active_wnd_class = "";
			IntPtr active_wnd = GetActiveWindow();
			if (active_wnd != IntPtr.Zero)
			{
				active_wnd_title = GetWindowText(active_wnd);
				active_wnd_class = GetWindowClassName(active_wnd);
			}
			var_value_setter.SetVariable("active_wnd_title", active_wnd_title);
			var_value_setter.SetVariable("active_wnd_class", active_wnd_class);
		}


		[DllImport("ole32.dll")]
		static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);
		[DllImport("ole32.dll")]
		static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);


		static Regex m_DTEComObjectNameRegex = new Regex(@"^!VisualStudio\.DTE\.(?<dte_version>\d+\.\d+).*$");

		public struct VSMultiInstanceInfo
		{
			public bool multiple_instances;
			public bool multiple_instances_same_version;
		}

		public void GetVSMultiInstanceInfo(out VSMultiInstanceInfo vs_instance_info)
		{
			DTE2 dte = (DTE2)m_Package.GetInterface(typeof(DTE));
			GetVSMultiInstanceInfo(out vs_instance_info, dte.Version);
		}

		public static void GetVSMultiInstanceInfo(out VSMultiInstanceInfo vs_instance_info, string our_dte_version)
		{
			vs_instance_info.multiple_instances = false;
			vs_instance_info.multiple_instances_same_version = false;

			IRunningObjectTable running_object_table;
			if (VSConstants.S_OK != GetRunningObjectTable(0, out running_object_table))
				return;
			IEnumMoniker moniker_enumerator;
			running_object_table.EnumRunning(out moniker_enumerator);
			moniker_enumerator.Reset();

			IMoniker[] monikers = new IMoniker[1];
			IntPtr num_fetched = IntPtr.Zero;
			while (VSConstants.S_OK == moniker_enumerator.Next(1, monikers, num_fetched))
			{
				IBindCtx ctx;
				if (VSConstants.S_OK != CreateBindCtx(0, out ctx))
					continue;

				string name;
				monikers[0].GetDisplayName(ctx, null, out name);
				if (!name.StartsWith("!VisualStudio.DTE."))
					continue;

				object com_object;
				if (VSConstants.S_OK != running_object_table.GetObject(monikers[0], out com_object))
					continue;

				DTE2 dte = com_object as DTE2;
				if (dte != null)
				{
					Match m = m_DTEComObjectNameRegex.Match(name);
					if (m.Success)
					{
						Group g = m.Groups["dte_version"];
						if (g.Success)
						{
							if (g.Value == our_dte_version)
							{
								vs_instance_info.multiple_instances = true;
								vs_instance_info.multiple_instances_same_version = true;
								return;
							}
						}
					}
					vs_instance_info.multiple_instances = true;
				}
			}
		}

		public EvalContext CreateFreshEvalContext()
		{
			PackageGlobals.VSMultiInstanceInfo multi_instance_info;
			GetVSMultiInstanceInfo(out multi_instance_info);
			EvalContext eval_ctx = new EvalContext();
			SetVariableValuesFromIDEState(eval_ctx, multi_instance_info);
			return eval_ctx;
		}

		void AddFilePathVars(IVariableValueSetter var_value_setter, ref string path, bool user_forward_slashes, string var_name_prefix)
		{
			Util.FilenameParts parts = new Util.FilenameParts();
			Util.ProcessFilePath(path, user_forward_slashes, ref parts);
			var_value_setter.SetVariable(var_name_prefix + "path", parts.path);
			var_value_setter.SetVariable(var_name_prefix + "dir", parts.dir);
			var_value_setter.SetVariable(var_name_prefix + "file", parts.file);
			var_value_setter.SetVariable(var_name_prefix + "filename", parts.filename);
			var_value_setter.SetVariable(var_name_prefix + "ext", parts.ext);
			path = parts.path;
		}
	}
}
