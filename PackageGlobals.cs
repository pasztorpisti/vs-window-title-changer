using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VSWindowTitleChanger.ExpressionEvaluator;

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

		public ExecFuncEvaluatorThread ExecFuncEvaluator
		{
			get
			{
				return m_ExecFuncEvaluatorThread;
			}
		}

		VSWindowTitleChangerPackage m_Package;
		TitleSetup m_TitleSetup;
		TitleSetupEditor m_TitleSetupEditor;

		VariableValueResolver m_CompileTimeConstants;
		ExecFuncEvaluatorThread m_ExecFuncEvaluatorThread;

		PackageGlobals(VSWindowTitleChangerPackage package)
		{
			m_Package = package;
			CreateCompileTimeConstants();

			m_ExecFuncEvaluatorThread = new ExecFuncEvaluatorThread();

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

			m_ExecFuncEvaluatorThread.Dispose();
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



		public static Project GetStartupProject(Solution solution)
		{
			Project ret = null;

			if (solution != null &&
				solution.SolutionBuild != null &&
				solution.SolutionBuild.StartupProjects != null)
			{
				string uniqueName = (string)((object[])solution.SolutionBuild.StartupProjects)[0];

				// Can't use the solution.Item(uniqueName) here since that doesn't work 
				// for projects under solution folders. 

				ret = GetProject(solution, uniqueName);
			}

			return ret;
		}

		public static Project GetProject(Solution solution, string uniqueName)
		{
			Project ret = null;

			if (solution != null && uniqueName != null)
			{
				foreach (Project p in solution.Projects)
				{
					ret = GetSubProject(p, uniqueName);

					if (ret != null)
						break;
				}
			}

			return ret;
		}

		public static Project GetSubProject(Project project, string uniqueName)
		{
			Project ret = null;

			if (project != null)
			{
				if (project.UniqueName == uniqueName)
				{
					ret = project;
				}
				else if (project.Kind == EnvDTE.Constants.vsProjectKindSolutionItems)
				{
					// Solution folder 

					foreach (ProjectItem projectItem in project.ProjectItems)
					{
						ret = GetSubProject(projectItem.SubProject, uniqueName);

						if (ret != null)
							break;
					}
				}
			}

			return ret;
		}

		class VarValues
		{
			public string sln_path = "";
			public DBGMODE dbgmode = DBGMODE.DBGMODE_Design;
			public bool sln_dirty = false;
			public string configuration = "";
			public string platform = "";
			public string startup_proj = "";
			public string startup_proj_path = "";
			public bool startup_proj_dirty = false;
			public string doc_path = "";
			public bool doc_dirty = false;
			public bool any_doc_dirty = false;
			public bool any_proj_dirty = false;
			public bool wnd_minimized = false;
			public bool wnd_foreground = false;
			public bool app_active = false;
			public string active_wnd_title = "";
			public string active_wnd_class = "";
			public string orig_title = "";

			public List<Exception> exceptions = new List<Exception>();
		}

		Project FindProjectObjectInSolutionFolder(Project solution_folder, object proj_id)
		{
			Project proj;
			try
			{
				proj = solution_folder.ProjectItems.Item(proj_id).SubProject;
			}
			catch
			{
				proj = null;
			}

			if (proj != null)
				return proj;

			for (int i=1,e=solution_folder.ProjectItems.Count; i<=e; ++i)
			{
				Project sub_proj = solution_folder.ProjectItems.Item(i).SubProject;
				if (sub_proj == null || sub_proj.Kind != ProjectKinds.vsProjectKindSolutionFolder)
					continue;
				proj = FindProjectObjectInSolutionFolder(sub_proj, proj_id);
				if (proj != null)
					return proj;
			}

			return null;
		}

		void GetVariableValues(VarValues var_values)
		{
			DTE2 dte = (DTE2)m_Package.GetInterface(typeof(DTE));
			IVsSolution vs_solution = (IVsSolution)m_Package.GetInterface(typeof(IVsSolution));
			string temp_solution_dir, temp_solution_options;
			if (VSConstants.S_OK != vs_solution.GetSolutionInfo(out temp_solution_dir, out var_values.sln_path, out temp_solution_options) || var_values.sln_path == null)
				var_values.sln_path = "";

			IVsDebugger debugger = (IVsDebugger)m_Package.GetInterface(typeof(IVsDebugger));
			DBGMODE[] adbgmode = new DBGMODE[] { DBGMODE.DBGMODE_Design };
			if (VSConstants.S_OK != debugger.GetMode(adbgmode))
				adbgmode[0] = DBGMODE.DBGMODE_Design;
			var_values.dbgmode = adbgmode[0] & ~DBGMODE.DBGMODE_EncMask;

			var_values.sln_dirty = !dte.Solution.Saved;

			try
			{
				SolutionConfiguration2 active_cfg = (SolutionConfiguration2)dte.Solution.SolutionBuild.ActiveConfiguration;
				if (active_cfg != null)
				{
					var_values.configuration = active_cfg.Name == null ? "" : active_cfg.Name; ;
					var_values.platform = active_cfg.PlatformName == null ? "" : active_cfg.PlatformName;
				}
			}
			catch (System.Exception ex)
			{
				var_values.exceptions.Add(ex);
			}

			try
			{
				Project startup_project = GetStartupProject(dte.Solution);
				if (startup_project != null)
				{
					var_values.startup_proj = startup_project.Name;
					var_values.startup_proj_path = startup_project.FullName;
					var_values.startup_proj_dirty = !startup_project.Saved;
				}
			}
			catch (System.Exception ex)
			{
				var_values.exceptions.Add(ex);
			}

			try
			{
				Document active_document = dte.ActiveDocument;
				if (active_document != null)
				{
					var_values.doc_path = active_document.FullName;
					var_values.doc_dirty = !active_document.Saved;
				}
			}
			catch (System.Exception ex)
			{
				var_values.exceptions.Add(ex);
			}

			try
			{
				foreach (Document doc in dte.Documents)
				{
					if (!doc.Saved)
					{
						var_values.any_doc_dirty = true;
						break;
					}
				}
			}
			catch (System.Exception ex)
			{
				var_values.exceptions.Add(ex);
			}

			try
			{
				foreach (Project proj in dte.Solution.Projects)
				{
					if (!proj.Saved)
					{
						var_values.any_proj_dirty = true;
						break;
					}
				}
			}
			catch (System.Exception ex)
			{
				var_values.exceptions.Add(ex);
			}

			try
			{
				var_values.wnd_minimized = m_Package.VSMainWindow.Minimized;
				var_values.wnd_foreground = m_Package.VSMainWindow.IsForegroundWindow();
				var_values.app_active = m_Package.VSMainWindow.IsAppActive;
			}
			catch (System.Exception ex)
			{
				var_values.exceptions.Add(ex);
			}

			IntPtr active_wnd = GetActiveWindow();
			if (active_wnd != IntPtr.Zero)
			{
				var_values.active_wnd_title = GetWindowText(active_wnd);
				var_values.active_wnd_class = GetWindowClassName(active_wnd);
			}

			var_values.orig_title = m_Package.VSMainWindow.OriginalTitle;
		}


		void SetVariableValuesFromIDEState(IVariableValueSetter var_value_setter, VSMultiInstanceInfo multi_instance_info)
		{
			VarValues var_values = new VarValues();
			try
			{
				GetVariableValues(var_values);
			}
			catch (System.Exception ex)
			{
				var_values.exceptions.Add(ex);
			}

			if (var_values.exceptions.Count > 0)
			{
				OutputWindow output_window = null;
				try
				{
					DTE2 dte = (DTE2)m_Package.GetInterface(typeof(DTE));
					Window window = dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
					output_window = (OutputWindow)window.Object;
				}
				catch
				{
				}

				foreach (Exception ex in var_values.exceptions)
				{
					string msg = "----------------- VSWindowTitleChanger Exception: " + ex.ToString();
					Debug.WriteLine(msg);
					if (output_window != null)
					{
						try
						{
							output_window.ActivePane.OutputString("\n" + msg + "\n");
						}
						catch
						{
						}
					}
				}
			}

			AddFilePathVars(var_value_setter, ref var_values.sln_path, SlashPathSeparator, "sln_");
			var_value_setter.SetVariable("sln_open", var_values.sln_path.Length > 0);
			var_value_setter.SetVariable("sln_dirty", var_values.sln_dirty ? "*" : "");

			AddFilePathVars(var_value_setter, ref var_values.doc_path, SlashPathSeparator, "doc_");
			var_value_setter.SetVariable("doc_open", var_values.doc_path.Length > 0);
			var_value_setter.SetVariable("doc_dirty", var_values.doc_dirty ? "*" : "");
			var_value_setter.SetVariable("any_doc_dirty", var_values.any_doc_dirty ? "*" : "");

			AddFilePathVars(var_value_setter, ref var_values.startup_proj_path, SlashPathSeparator, "startup_proj_");
			var_value_setter.SetVariable("startup_proj", var_values.startup_proj);
			var_value_setter.SetVariable("startup_proj_dirty", var_values.startup_proj_dirty ? "*" : "");

			var_value_setter.SetVariable("any_proj_dirty", var_values.any_proj_dirty ? "*" : "");
			var_value_setter.SetVariable("anything_dirty", (var_values.sln_dirty || var_values.any_proj_dirty || var_values.any_doc_dirty) ? "*" : "");

			var_value_setter.SetVariable("wnd_minimized", var_values.wnd_minimized);
			var_value_setter.SetVariable("wnd_foreground", var_values.wnd_foreground);
			var_value_setter.SetVariable("app_active", var_values.app_active);

			bool debugging = false;
			string debug_mode = "";
			switch (var_values.dbgmode)
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

			var_value_setter.SetVariable("configuration", var_values.configuration);
			var_value_setter.SetVariable("platform", var_values.platform);

			var_value_setter.SetVariable("orig_title", var_values.orig_title);

			var_value_setter.SetVariable("multi_instances", multi_instance_info.multiple_instances);
			var_value_setter.SetVariable("multi_instances_same_ver", multi_instance_info.multiple_instances_same_version);

			var_value_setter.SetVariable("active_wnd_title", var_values.active_wnd_title);
			var_value_setter.SetVariable("active_wnd_class", var_values.active_wnd_class);
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

			try
			{
				IRunningObjectTable running_object_table;
				if (VSConstants.S_OK != GetRunningObjectTable(0, out running_object_table))
					return;
				IEnumMoniker moniker_enumerator;
				running_object_table.EnumRunning(out moniker_enumerator);
				moniker_enumerator.Reset();

				IMoniker[] monikers = new IMoniker[1];
				IntPtr num_fetched = IntPtr.Zero;
				int dte_count = 0;
				int dte_count_our_version = 0;
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
						++dte_count;
						Match m = m_DTEComObjectNameRegex.Match(name);
						if (m.Success)
						{
							Group g = m.Groups["dte_version"];
							if (g.Success && g.Value == our_dte_version)
								++dte_count_our_version;
						}
					}
				}
				vs_instance_info.multiple_instances = dte_count > 1;
				vs_instance_info.multiple_instances_same_version = dte_count_our_version > 1;
			}
			catch
			{
				vs_instance_info.multiple_instances = false;
				vs_instance_info.multiple_instances_same_version = false;
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
