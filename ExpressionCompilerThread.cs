using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using VSWindowTitleChanger.ExpressionEvaluator;

namespace VSWindowTitleChanger
{
	class ExpressionCompilerThread : IDisposable
	{
		Thread m_Thread;
		AutoResetEvent m_WakeUpEvent = new AutoResetEvent(false);
		LinkedList<Job> m_Jobs = new LinkedList<Job>();
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

		public void AddJob(Job job)
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
				Job job;
				lock (this)
				{
					if (m_StopRequest)
						break;
					job = m_Jobs.First.Value;
					m_Jobs.RemoveFirst();
				}
				job.Compile();
			}
		}

		public class Job
		{
			Parser m_Parser;
			IEvalContext m_EvalContext;

			// The variable_value_resolver is used only to find the unused variables, the variable values aren't used.
			// variable_value_resolver can be null, in that case unused variables arent explored.
			public Job(Parser parser, IEvalContext eval_ctx)
			{
				m_Parser = parser;
				m_EvalContext = eval_ctx;
			}

			public Job(Parser parser)
				: this(parser, null)
			{ }

			public delegate void CompileFinishedHandler(Job job);
			public event CompileFinishedHandler OnCompileFinished;

			IntPtr m_UserData;
			public IntPtr UserData { get { return m_UserData; } set { m_UserData = value; } }

			Exception m_Error;
			Expression m_Expression;
			Value m_EvalResult;
			List<Variable> m_SortedUnresolvedVariables;

			public Exception Error { get { return m_Error; } }
			// Only if there is no error:
			public Expression Expession { get { return m_Expression; } }
			public Value EvalResult { get { return m_EvalResult; } }
			public List<Variable> SortedUnresolvedVariables { get { return m_SortedUnresolvedVariables; } }

			class VariablePosComparer : IComparer<Variable>
			{
				public virtual int Compare(Variable a, Variable b)
				{
					return a.Position - b.Position;
				}
			}

			public void Compile()
			{
				Debug.Assert(m_Error == null && m_Expression == null);
				try
				{
					m_Expression = m_Parser.Parse();
					if (m_Expression != null && m_EvalContext != null)
					{
						m_EvalResult = m_Expression.Evaluate(new SafeEvalContext(m_EvalContext));
						m_SortedUnresolvedVariables = m_Expression.CollectUnresolvedVariables(m_EvalContext);
						m_SortedUnresolvedVariables.Sort(new VariablePosComparer());
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
}
