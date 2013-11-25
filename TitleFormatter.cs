using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSWindowTitleChanger
{
	using ExpressionEvaluator;
	using ExpressionEvaluator.Tokenizer;


	class TitleFormatter
	{
		public TitleFormatter(VSWindowTitleChangerPackage package, VSMainWindow vs_main_window)
		{
			m_Package = package;
			m_VSMainWindow = vs_main_window;

			m_CompiledRegexCache = new Cache<string, Regex>(CompileRegex);
			CreateCompileTimeConstants();
			m_CompiledExpressionCache = new Cache<string, Expression>(CompileExpression);
		}

		public void UpdateWindowTitle(ToolOptions options)
		{
			DTE2 dte = (DTE2)m_Package.GetInterface(typeof(DTE));

			VSMultiInstanceInfo multi_instance_info;
			GetVSMultiInstanceInfo(out multi_instance_info, dte.Version);

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
				EvalContext eval_ctx = new EvalContext();
				SafeEvalContext safe_eval_ctx = new SafeEvalContext(eval_ctx);
				string solution_path;
				SetVariableValuesFromIDEState(eval_ctx, dte, options, multi_instance_info, out solution_path);

				foreach (WindowTitlePattern wtp in options.WindowTitlePatterns)
				{
					Regex regex = m_CompiledRegexCache.GetEntry(wtp.Regex);
					if (regex == null)
						continue;
					Match m = regex.Match(solution_path);
					if (!m.Success)
						continue;
					for (int i = 0, e = m.Groups.Count; i < e; ++i)
					{
						if (m.Groups[i].Success)
							eval_ctx.SetVariable(string.Format("$sln_{0}", i), m.Groups[i].Value);
					}
					foreach (string name in regex.GetGroupNames())
					{
						Group g = m.Groups[name];
						if (g.Success)
							eval_ctx.SetVariable("$sln_" + name, g.Value);
					}
					title = TryConditionalPatterns(safe_eval_ctx, wtp.ConditionalPatterns);
					if (title != null)
						break;
				}
			}

			if (title == null)
				title = m_VSMainWindow.OriginalTitle;

			if (options.Debug)
				title += " [VSWindowTitleChanger_DebugMode]";

			m_VSMainWindow.SetTitle(title);
		}

		private string TryConditionalPatterns(IEvalContext eval_ctx, string[] conditional_patterns)
		{
			for (int i = 0, e = conditional_patterns.Length; i < e; ++i)
			{
				try
				{
					string result = TryConditionalPattern(eval_ctx, conditional_patterns[i]);
					if (result != null)
						return result;
				}
				catch (ExpressionEvaluatorException)
				{
				}
			}
			return null;
		}

		private string TryConditionalPattern(IEvalContext eval_ctx, string conditional_pattern)
		{
			Tokenizer tokenizer = new Tokenizer(conditional_pattern);
			Token begin_token = tokenizer.GetNextToken();
			// We skip empty strings.
			if (begin_token.type == TokenType.EOF)
				return null;

			string cond_expr_str;
			string title_expr_str;
			if (begin_token.type == TokenType.Variable && begin_token.data.ToLower() == "if")
			{
				Token end_token;
				for (;;)
				{
					end_token = tokenizer.GetNextToken();
					if (end_token.type == TokenType.EOF)
						return null;
					if (end_token.type == TokenType.Variable && end_token.data.ToLower() == "then")
					{
						int begin_idx = begin_token.pos + begin_token.data.Length;
						cond_expr_str = conditional_pattern.Substring(begin_idx, end_token.pos - begin_idx);
						title_expr_str = conditional_pattern.Substring(end_token.pos + end_token.data.Length);
						break;
					}
				}
			}
			else
			{
				cond_expr_str = null;
				title_expr_str = conditional_pattern;
			}

			if (cond_expr_str != null)
			{
				Expression cond_expr = m_CompiledExpressionCache.GetEntry(cond_expr_str);
				if (cond_expr == null)
					return null;
				Value cond_val = cond_expr.Evaluate(eval_ctx);
				if (!cond_val.ToBool())
					return null;
			}

			Expression title_expr = m_CompiledExpressionCache.GetEntry(title_expr_str);
			if (title_expr == null)
				return "";
			Value title_val = title_expr.Evaluate(eval_ctx);
			return title_val.ToString();
		}


		private void SetVariableValuesFromIDEState(IVariableValueSetter var_value_setter, DTE2 dte, ToolOptions options, VSMultiInstanceInfo multi_instance_info, out string solution_path)
		{
			IVsSolution vs_solution = (IVsSolution)m_Package.GetInterface(typeof(IVsSolution));
			string temp_solution_dir, temp_solution_options;
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

			AddFilePathVars(var_value_setter, ref solution_path, options.SlashPathSeparator, "sln_");
			var_value_setter.SetVariable("sln_open", solution_path.Length > 0);
			var_value_setter.SetVariable("sln_dirty", !dte.Solution.Saved);

			string active_document_path = active_document == null ? "" : active_document.FullName;
			AddFilePathVars(var_value_setter, ref active_document_path, options.SlashPathSeparator, "doc_");
			var_value_setter.SetVariable("doc_open", active_document_path.Length > 0);
			var_value_setter.SetVariable("doc_dirty", active_document != null && !active_document.Saved);

			string startup_project_path = startup_project == null ? "" : startup_project.FullName;
			AddFilePathVars(var_value_setter, ref startup_project_path, options.SlashPathSeparator, "startup_proj_");
			var_value_setter.SetVariable("startup_proj", startup_project == null ? "" : startup_project.Name);
			var_value_setter.SetVariable("startup_proj_dirty", startup_project != null && !startup_project.Saved);

			var_value_setter.SetVariable("wnd_minimized", m_VSMainWindow.Minimized);
			var_value_setter.SetVariable("wnd_foreground", m_VSMainWindow.IsForegroundWindow());

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

			var_value_setter.SetVariable("orig_title", m_VSMainWindow.OriginalTitle);

			var_value_setter.SetVariable("multi_instances", multi_instance_info.multiple_instances);
			var_value_setter.SetVariable("multi_instances_same_ver", multi_instance_info.multiple_instances_same_version);
		}


		[DllImport("ole32.dll")]
		private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);
		[DllImport("ole32.dll")]
		private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);


		private static Regex m_DTEComObjectNameRegex = new Regex(@"^!VisualStudio\.DTE\.(?<dte_version>\d+\.\d+).*$");

		struct VSMultiInstanceInfo
		{
			public bool multiple_instances;
			public bool multiple_instances_same_version;
		}

		private static void GetVSMultiInstanceInfo(out VSMultiInstanceInfo vs_instance_info, string our_dte_version)
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

		private void AddFilePathVars(IVariableValueSetter var_value_setter, ref string path, bool user_forward_slashes, string var_name_prefix)
		{
			FilenameParts parts = new FilenameParts();
			ProcessFilePath(path, user_forward_slashes, ref parts);
			var_value_setter.SetVariable(var_name_prefix + "path", parts.path);
			var_value_setter.SetVariable(var_name_prefix + "dir", parts.dir);
			var_value_setter.SetVariable(var_name_prefix + "file", parts.file);
			var_value_setter.SetVariable(var_name_prefix + "filename", parts.filename);
			var_value_setter.SetVariable(var_name_prefix + "ext", parts.ext);
			path = parts.path;
		}

		private void CreateCompileTimeConstants()
		{
			DTE2 dte = (DTE2)m_Package.GetInterface(typeof(DTE));
			m_CompileTimeConstants = new VariableValueResolver();
			m_CompileTimeConstants.SetVariable("true", true);
			m_CompileTimeConstants.SetVariable("false", false);
			m_CompileTimeConstants.SetVariable("dte_version", dte.Version);
			m_CompileTimeConstants.SetVariable("vs_version", DTEVersionToVSYear(dte.Version));
			m_CompileTimeConstants.SetVariable("vs_edition", dte.Edition);
		}

		private static string DTEVersionToVSYear(string dte_version)
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


		private string NormalizePath(string path, char path_separator)
		{
			// Changing path separators and eliminating duplicate path separator chars.
			StringBuilder sb = new StringBuilder();
			sb.Capacity = path.Length;
			bool prev_was_slash = false;
			foreach (char c in path)
			{
				switch (c)
				{
					case '\\':
					case '/':
						if (prev_was_slash)
							continue;
						prev_was_slash = true;
						sb.Append(path_separator);
						break;
					default:
						prev_was_slash = false;
						sb.Append(c);
						break;
				}
			}
			return sb.ToString();
		}

		struct FilenameParts
		{
			public string path;				// the full pathname of the file
			public string dir;				// directory, there is no trailing path separator char
			public string file;				// filename without directory but with the extension included
			public string filename;			// filename without directory and extension
			public string ext;				// extension, e.g.: "txt"
		}

		private void ProcessFilePath(string path, bool use_forward_slashes, ref FilenameParts parts)
		{
			if (path == null)
				path = "";
			char sep = use_forward_slashes ? '/' : '\\';
			path = NormalizePath(path, sep);
			parts.path = path;

			int idx = path.LastIndexOf(sep);
			if (idx < 0)
			{
				parts.dir = "";
				parts.file = path;
			}
			else
			{
				parts.dir = path.Substring(0, idx);
				parts.file = path.Substring(idx + 1);
			}

			idx = parts.file.LastIndexOf('.');
			if (idx < 0)
			{
				parts.filename = parts.file;
				parts.ext = "";
			}
			else
			{
				parts.filename = parts.file.Substring(0, idx);
				parts.ext = parts.file.Substring(idx + 1);
			}
		}

		private Regex CompileRegex(string regex_string)
		{
			try
			{
				return new Regex(regex_string, RegexOptions.IgnoreCase);
			}
			catch (System.Exception)
			{
				return null;
			}
		}

		private Expression CompileExpression(string expression_string)
		{
			try
			{
				Parser expression_parser = new Parser(expression_string, m_CompileTimeConstants);
				return expression_parser.Parse();
			}
			catch (ExpressionEvaluatorException)
			{
				return null;
			}
		}

		VariableValueResolver m_CompileTimeConstants;

		Cache<string, Regex> m_CompiledRegexCache;
		Cache<string, Expression> m_CompiledExpressionCache;

		VSWindowTitleChangerPackage m_Package;
		VSMainWindow m_VSMainWindow;
	}
}
