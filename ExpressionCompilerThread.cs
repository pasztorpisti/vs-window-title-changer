using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using VSWindowTitleChanger.ExpressionEvaluator;

namespace VSWindowTitleChanger
{
	// A simple job executor thread. Its queue can be cleared anytime and it can be
	// stopped without processing all jobs in the queue.
	class ExpressionCompilerThread : IDisposable
	{
		Thread m_Thread;
		AutoResetEvent m_WakeUpEvent = new AutoResetEvent(false);
		LinkedList<IJob> m_Jobs = new LinkedList<IJob>();
		bool m_StopRequest;

		public ExpressionCompilerThread()
		{
			m_Thread = new Thread(new ThreadStart(Run));
			m_Thread.Start();
		}

		public virtual void Dispose()
		{
			lock (this)
			{
				m_StopRequest = true;
				m_WakeUpEvent.Set();
			}
			m_Thread.Join();
		}

		public void AddJob(IJob job)
		{
			lock (this)
			{
				m_Jobs.AddLast(job);
				m_WakeUpEvent.Set();
			}
		}

		public void RemoveAllJobs()
		{
			lock (this)
			{
				m_Jobs.Clear();
			}
		}

		void Run()
		{
			for (;;)
			{
				m_WakeUpEvent.WaitOne();
				LinkedList<IJob> jobs;
				lock (this)
				{
					if (m_StopRequest)
						break;

					jobs = m_Jobs;
					m_Jobs = new LinkedList<IJob>();
				}
				foreach (IJob job in jobs)
				{
					job.Execute();
				}
			}
		}

		public interface IJob
		{
			void Execute();
		}
	}




	class ExpressionCompilerJob : ExpressionCompilerThread.IJob
	{
		Parser m_Parser;
		IEvalContext m_EvalContext;
		bool m_CollectUnresolvedVariables;
		CompiledExpressionCache m_Cache;

		// The variable_value_resolver is used only to find the unused variables, the variable values aren't used.
		// variable_value_resolver can be null, in that case unused variables aren't explored.
		// cache can be null.
		public ExpressionCompilerJob(Parser parser, IEvalContext eval_ctx, bool collect_unresolved_variables, CompiledExpressionCache cache)
		{
			m_Parser = parser;
			m_EvalContext = eval_ctx;
			m_CollectUnresolvedVariables = collect_unresolved_variables;
			m_Cache = cache;
		}

		public delegate void CompileFinishedHandler(ExpressionCompilerJob job);
		public event CompileFinishedHandler OnCompileFinished;

		IntPtr m_UserData;
		public IntPtr UserData { get { return m_UserData; } set { m_UserData = value; } }

		Exception m_Error;
		Expression m_Expression;
		Value m_EvalResult;
		List<Variable> m_SortedUnresolvedVariables;
		bool m_ContainsExec;

		public Exception Error { get { return m_Error; } }
		// Only if there is no error:
		public Expression Expession { get { return m_Expression; } }
		public Value EvalResult { get { return m_EvalResult; } }
		public bool ContainsExec { get { return m_ContainsExec; } }
		public List<Variable> SortedUnresolvedVariables { get { return m_SortedUnresolvedVariables; } }

		class VariablePosComparer : IComparer<Variable>
		{
			public virtual int Compare(Variable a, Variable b)
			{
				return a.Position - b.Position;
			}
		}

		public virtual void Execute()
		{
			Debug.Assert(m_Error == null && m_Expression == null);
			try
			{
				if (m_Cache != null)
				{
					CompiledExpression compiled_expression = m_Cache.GetEntry(m_Parser.Text);
					m_Expression = compiled_expression.expression;
					m_Error = compiled_expression.compile_error;
					m_ContainsExec = compiled_expression.contains_exec;
				}
				else
				{
					m_Expression = m_Parser.Parse();
					m_ContainsExec = m_Parser.ContainsExec;
				}

				if (m_Expression != null && m_EvalContext != null)
				{
					m_EvalResult = m_Expression.Evaluate(new SafeEvalContext(m_EvalContext));
					if (m_CollectUnresolvedVariables)
					{
						m_SortedUnresolvedVariables = m_Expression.CollectUnresolvedVariables(m_EvalContext);
						m_SortedUnresolvedVariables.Sort(new VariablePosComparer());
					}
				}
			}
			catch (Exception ex)
			{
				m_Error = ex;
			}

			PackageGlobals.BeginInvokeOnUIThread(delegate()
			{
				if (OnCompileFinished != null)
				{
					OnCompileFinished(this);
				}
			});
		}
	}


}
