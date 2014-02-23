using System;
using System.Diagnostics;
using System.Text;
using System.Reflection;


namespace VSWindowTitleChanger.ExpressionEvaluator
{

	class WorkspaceInfoGetter
	{
		static WorkspaceInfoGetter m_Instance;

		public static WorkspaceInfoGetter Instance()
		{
			if (m_Instance == null)
				m_Instance = new WorkspaceInfoGetter();
			return m_Instance;
		}

		public string GetOwner(string path)
		{
			return GetStringWorkspaceProperty(path, m_PIName);
		}

		public string GetName(string path)
		{
			return GetStringWorkspaceProperty(path, m_PIOwnerName);
		}

		string GetStringWorkspaceProperty(string path, PropertyInfo pi)
		{
			if (path.Length == 0 || pi == null)
				return "";
			try
			{
				object workstation = m_PICurrent.GetValue(null, null);
				if (workstation == null)
					return "";
				object workspace_info = m_MIGetLocalWorkspaceInfo.Invoke(workstation, new object[] { path });
				if (workspace_info == null)
					return "";
				object val = pi.GetValue(workspace_info, null);
				return val.ToString();
			}
			catch (System.Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				return "";
			}
		}

		PropertyInfo m_PICurrent;
		MethodInfo m_MIGetLocalWorkspaceInfo;
		PropertyInfo m_PIName;
		PropertyInfo m_PIOwnerName;

		WorkspaceInfoGetter()
		{
			Init();
		}

		void Init()
		{
			try
			{
				string workstation_class_asm_ref = "Microsoft.TeamFoundation.VersionControl.Client.Workstation, Microsoft.TeamFoundation.VersionControl.Client, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL";
				string workspaceinfo_class_asm_ref = "Microsoft.TeamFoundation.VersionControl.Client.WorkspaceInfo, Microsoft.TeamFoundation.VersionControl.Client, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL";
				int dte_major_version = PackageGlobals.Instance().DTEMajorVersion;

				System.Type workstation_class = System.Type.GetType(string.Format(workstation_class_asm_ref, PackageGlobals.Instance().DTEMajorVersion));
				if (workstation_class != null)
				{
					m_PICurrent = workstation_class.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
					if (m_PICurrent != null)
					{
						m_MIGetLocalWorkspaceInfo = workstation_class.GetMethod("GetLocalWorkspaceInfo", new System.Type[] { typeof(string) });
						if (m_MIGetLocalWorkspaceInfo != null)
						{
							System.Type workspaceinfo_class = System.Type.GetType(string.Format(workspaceinfo_class_asm_ref, PackageGlobals.Instance().DTEMajorVersion));
							if (workspaceinfo_class != null)
							{
								m_PIName = workspaceinfo_class.GetProperty("Name");
								m_PIOwnerName = workspaceinfo_class.GetProperty("OwnerName");
							}
						}
					}
				}
			}
			catch (System.Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
		}
	}


	class FuncWorkspaceName : NeverConstExpression
	{
		public FuncWorkspaceName(Expression operand) : base(operand) { }
		public override Value Evaluate(IEvalContext ctx)
		{
			string path = SubExpressions[0].Evaluate(ctx).ToString();
			string workspace_name = "";
			if (path.Length > 0)
			{
				PackageGlobals.InvokeOnUIThread(delegate()
				{
					workspace_name = WorkspaceInfoGetter.Instance().GetName(path);
				});
			}

			return new StringValue(workspace_name);
		}
	}

	class FuncWorkspaceOwner : NeverConstExpression
	{
		public FuncWorkspaceOwner(Expression operand) : base(operand) { }
		public override Value Evaluate(IEvalContext ctx)
		{
			string path = SubExpressions[0].Evaluate(ctx).ToString();
			string owner = "";
			if (path.Length > 0)
			{
				PackageGlobals.InvokeOnUIThread(delegate()
				{
					owner = WorkspaceInfoGetter.Instance().GetOwner(path);
				});
			}

			return new StringValue(owner);
		}
	}


	interface ExecFuncEvaluator
	{
		string Evaluate(int exec_period_secs, string command, string workdir);
	}

	class FuncExec : NeverConstExpression
	{
		// variable_name is allowed to be null
		public FuncExec(ExecFuncEvaluator evaluator, string variable_name, int exec_period_secs, Expression command, Expression workdir)
			: base(command, workdir)
		{
			m_Evaluator = evaluator;
			m_VariableName = variable_name;
			m_ExecPeriodSecs = exec_period_secs;
		}

		public override Value Evaluate(IEvalContext ctx)
		{
			string command = SubExpressions[0].Evaluate(ctx).ToString();
			string workdir = SubExpressions[1].Evaluate(ctx).ToString();

			string exec_output = m_Evaluator.Evaluate(m_ExecPeriodSecs, command, workdir);

			if (m_VariableName != null)
			{
				m_LocalContext = new VariableValueResolver();
				m_LocalContext.SetVariable(m_VariableName, exec_output);
			}

			return new StringValue(exec_output);
		}

		public override IVariableValueResolver GetLocalContext()
		{
			return m_LocalContext;
		}

		protected internal override Value RecursiveCollectUnresolvedVariables(IEvalContext ctx)
		{
			SubExpressions[0].RecursiveCollectUnresolvedVariables(ctx);
			SubExpressions[1].RecursiveCollectUnresolvedVariables(ctx);
			string output = "X";
			if (m_VariableName != null)
			{
				m_LocalContext = new VariableValueResolver();
				m_LocalContext.SetVariable(m_VariableName, output);
			}
			return new StringValue(output);
		}

		ExecFuncEvaluator m_Evaluator;
		string m_VariableName;
		int m_ExecPeriodSecs;
		VariableValueResolver m_LocalContext;
	}


	class FuncRelPath : Expression
	{
		public FuncRelPath(Expression dir, Expression path) : base(dir, path) { }

		static char[] PATH_SEPARATORS = new char[] { '\\', '/' };

		public override Value Evaluate(IEvalContext ctx)
		{
			string dir = SubExpressions[0].Evaluate(ctx).ToString();
			string path = SubExpressions[1].Evaluate(ctx).ToString();

			bool rooted_dir = System.IO.Path.IsPathRooted(dir);
			bool rooted_path = System.IO.Path.IsPathRooted(path);

			if (rooted_dir ^ rooted_path)
				return GetErrorReturnValue(path);

			string[] dir_components = dir.Split(PATH_SEPARATORS, StringSplitOptions.RemoveEmptyEntries);
			string[] path_components = path.Split(PATH_SEPARATORS, StringSplitOptions.RemoveEmptyEntries);

			int max_cmp_count = Math.Min(dir_components.Length, path_components.Length);
			int same;
			for (same = 0; same < max_cmp_count; ++same)
			{
				if (dir_components[same].ToLower() != path_components[same].ToLower())
					break;
			}

			if (same == 0 && rooted_dir && !dir.StartsWith("\\") && !dir.StartsWith("/"))
				return GetErrorReturnValue(path);

			StringBuilder sb = new StringBuilder();
			for (int i=0,e=dir_components.Length-same; i<e; ++i)
				sb.Append(i > 0 ? "/.." : "..");

			for (int i = same; i < path_components.Length; ++i)
			{
				if (sb.Length > 0)
					sb.Append('/');
				sb.Append(path_components[i]);
			}

			return new StringValue(sb.ToString());
		}

		Value GetErrorReturnValue(string path)
		{
			path = path.Replace('\\', '/');
			if (!path.EndsWith("/"))
				return new StringValue(path);
			int i = path.Length;
			while (i > 0 && path[i-1] == '/')
				--i;
			return new StringValue(path.Substring(0, i));
		}
	}
}
