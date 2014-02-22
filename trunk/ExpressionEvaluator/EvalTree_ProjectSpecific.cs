using System;
using System.Diagnostics;
using System.Text;
using Microsoft.TeamFoundation.VersionControl.Client;


namespace VSWindowTitleChanger.ExpressionEvaluator
{
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
					try
					{
						Workstation ws = Workstation.Current;
						if (ws != null)
						{
							WorkspaceInfo wsi = ws.GetLocalWorkspaceInfo(path);
							if (wsi != null)
								workspace_name = wsi.Name;
						}
					}
					catch (System.Exception ex)
					{
						Debug.WriteLine(ex.ToString());
					}
				});
			}

			return new StringValue(workspace_name);
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
