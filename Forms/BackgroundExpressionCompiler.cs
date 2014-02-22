using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Windows.Threading;
using System.Threading;
using VSWindowTitleChanger.ExpressionEvaluator;
using System.Windows.Forms;
using System.Drawing;

namespace VSWindowTitleChanger
{
	// Glues together an expression compiler thread with an expression textbox and some other
	// controls to display compile errors and compile results.
	class BackgroundExpressionCompiler : IDisposable
	{
		ExpressionTextBox m_ExpressionTextBox;
		Label m_WarningsLabel;

		ExpressionCompilerJob m_PrevFinishedJob;
		ExpressionCompilerJob m_DelayedCompileErrorJob;
		DateTime m_DelayedCompileErrorDeadline;

		public enum ECompileResultHandling
		{
			Success,
			DelayedError,
			Error,
		}
		ECompileResultHandling m_CompileResultHandling = ECompileResultHandling.Success;

		IVariableValueResolver m_CompileTimeConstants;

		TextBox m_CompileResultTextBox;

		Pen m_UnderlinePen;

		public ECompileResultHandling CompileResultHandling { get { return m_CompileResultHandling; } }
		public int WarningCount
		{
			get
			{
				if (m_CompileResultHandling != ECompileResultHandling.Success || m_PrevFinishedJob == null)
					return 0;
				return m_PrevFinishedJob.SortedUnresolvedVariables.Count;
			}
		}

		public ExpressionTextBox ExpressionTextBox
		{
			get
			{
				return m_ExpressionTextBox;
			}
			set
			{
				if (m_ExpressionTextBox != null)
				{
					m_ExpressionTextBox.AfterUndo -= AfterUndo;
					m_ExpressionTextBox.AfterRedo -= AfterRedo;
					m_ExpressionTextBox.UndoEntryAdded -= UndoEntryAdded;
					m_ExpressionTextBox.SelectionChanged -= ExpressionTextBox_SelectionChanged;
					m_ExpressionTextBox.PostPaint -= ExpressionTextBox_PostPaint;
				}
				m_ExpressionTextBox = value;
				if (m_ExpressionTextBox != null)
				{
					m_ExpressionTextBox.AfterUndo += AfterUndo;
					m_ExpressionTextBox.AfterRedo += AfterRedo;
					m_ExpressionTextBox.UndoEntryAdded += UndoEntryAdded;
					m_ExpressionTextBox.SelectionChanged += ExpressionTextBox_SelectionChanged;
					m_ExpressionTextBox.PostPaint += ExpressionTextBox_PostPaint;
				}
			}
		}

		public Label WarningsLabel
		{
			get
			{
				return m_WarningsLabel;
			}
			set
			{
				m_WarningsLabel = value;
				if (m_WarningsLabel != null)
					m_WarningsLabel.Text = UnderlineData.Underlines.Count.ToString();
			}
		}

		public IVariableValueResolver CompileTimeConstants { get { return m_CompileTimeConstants; } set { m_CompileTimeConstants = value; } }

		public TextBox CompileResultTextBox { get { return m_CompileResultTextBox; } set { m_CompileResultTextBox = value; } }


		ExpressionCompilerThread m_CompilerThread;
		System.Windows.Forms.Timer m_ErrorRecompileTimer = new System.Windows.Forms.Timer();
		System.Windows.Forms.Timer m_ExecFuncForceReEvaluateTimer = new System.Windows.Forms.Timer();

		public BackgroundExpressionCompiler()
		{
			m_ErrorRecompileTimer.Interval = 100;
			m_ErrorRecompileTimer.Tick += ErrorRecompileTimer_Tick;
			m_ExecFuncForceReEvaluateTimer.Interval = 500;
			m_ExecFuncForceReEvaluateTimer.Tick += ExecFuncForceReEvaluateTimer_Tick;
			m_UnderlinePen = new Pen(Color.Orange);
		}

		public virtual void Dispose()
		{
			Enabled = false;
			if (m_UnderlinePen != null)
			{
				m_UnderlinePen.Dispose();
				m_UnderlinePen = null;
			}
		}

		public void JumpToErrorLocation()
		{
			if (!Enabled || m_ExpressionTextBox == null || m_PrevFinishedJob == null || m_PrevFinishedJob.Error == null ||
				m_PrevFinishedJob.UserData != (IntPtr)m_MostRecentUndoEntryId)
				return;

			ParserException pex = m_PrevFinishedJob.Error as ParserException;
			m_ExpressionTextBox.Select(Math.Min(m_ExpressionTextBox.TextLength, pex.ErrorPos), 0);
		}

		public void ForceRecompile()
		{
			if (!Enabled)
				return;
			StartCompilation(m_MostRecentUndoEntryId);
		}

		public bool Enabled
		{
			get
			{
				return m_CompilerThread != null;
			}
			set
			{
				if (value)
					Enable();
				else
					Disable();
			}
		}

		void Enable()
		{
			if (Enabled)
				return;
			m_CompilerThread = new ExpressionCompilerThread();
			m_ErrorRecompileTimer.Start();
			m_ExecFuncForceReEvaluateTimer.Start();

			m_PrevFinishedJob = null;
			m_CompileResultHandling = ECompileResultHandling.Success;
			StartCompilation(-1);
		}

		void Disable()
		{
			if (!Enabled)
				return;
			m_ErrorRecompileTimer.Stop();
			m_ExecFuncForceReEvaluateTimer.Stop();
			m_PrevFinishedJob = null;
			m_CompilerThread.Dispose();
			m_CompilerThread = null;
			UnderlineData = new UnderlineDataContainer();
		}

		void ErrorRecompileTimer_Tick(object sender, EventArgs e)
		{
			if (!Enabled || m_CompileResultHandling != ECompileResultHandling.DelayedError)
				return;
			if (DateTime.Now < m_DelayedCompileErrorDeadline)
				return;
			SetPrevFinishedJob(m_DelayedCompileErrorJob);
			m_CompileResultHandling = ECompileResultHandling.Error;
		}

		void ExecFuncForceReEvaluateTimer_Tick(object sender, EventArgs e)
		{
			if (!Enabled || m_PrevFinishedJob == null)
				return;
			if (m_PrevFinishedJob.ContainsExec)
				ForceRecompile();
		}

		int m_MostRecentUndoEntryId = -1;

		void StartCompilation(int undo_entry_id)
		{
			m_MostRecentUndoEntryId = undo_entry_id;
			if (m_CompilerThread == null || m_ExpressionTextBox == null || m_CompileTimeConstants == null)
				return;

			Parser parser = new Parser(m_ExpressionTextBox.Text, PackageGlobals.Instance().ExecFuncEvaluator, m_CompileTimeConstants);
			ExpressionCompilerJob job = new ExpressionCompilerJob(parser, PackageGlobals.Instance().CreateFreshEvalContext(), true, null);
			job.OnCompileFinished += OnCompileFinished;
			job.UserData = (IntPtr)m_MostRecentUndoEntryId;
			m_CompilerThread.RemoveAllJobs();
			m_CompilerThread.AddJob(job);
		}

		const int m_CompileErrorShowDelayMilliSecs = 1500;
		TimeSpan m_CompileErrorShowDelay = TimeSpan.FromMilliseconds(m_CompileErrorShowDelayMilliSecs);

		void UpdateDelayedCompileErrorDeadline()
		{
			if (m_CompileResultHandling == ECompileResultHandling.DelayedError)
				m_DelayedCompileErrorDeadline = DateTime.Now.Add(m_CompileErrorShowDelay);
		}

		void OnCompileFinished(ExpressionCompilerJob job)
		{
			if (!Enabled || job.UserData != (IntPtr)m_MostRecentUndoEntryId)
				return;

			if (job.Error == null)
			{
				SetPrevFinishedJob(job);
				m_CompileResultHandling = ECompileResultHandling.Success;
			}
			else
			{
				switch (m_CompileResultHandling)
				{
					case ECompileResultHandling.Success:
						m_DelayedCompileErrorJob = job;
						m_CompileResultHandling = ECompileResultHandling.DelayedError;
						UpdateDelayedCompileErrorDeadline();
						break;
					case ECompileResultHandling.DelayedError:
						m_DelayedCompileErrorJob = job;
						break;
					case ECompileResultHandling.Error:
						SetPrevFinishedJob(job);
						break;
				}
			}
		}

		void SetPrevFinishedJob(ExpressionCompilerJob job)
		{
			m_PrevFinishedJob = job;
			UpdateWarningUnderlineData();
			UpdateCompileResultTextBox();
		}

		void UpdateWarningUnderlineData()
		{
			if (m_PrevFinishedJob.Error != null || m_PrevFinishedJob.SortedUnresolvedVariables == null)
				UnderlineData = new UnderlineDataContainer();
			else
				UnderlineData = new UnderlineDataContainer(m_PrevFinishedJob.SortedUnresolvedVariables);

			if (m_ExpressionTextBox != null)
				m_ExpressionTextBox.Invalidate();
		}

		Color m_ErrorMessageBackgroundColor = Color.FromArgb(255, 224, 224);

		void UpdateCompileResultTextBox()
		{
			if (m_CompileResultTextBox == null)
				return;

			Exception ex = m_PrevFinishedJob.Error;
			if (ex != null)
			{
				m_CompileResultTextBox.BackColor = m_ErrorMessageBackgroundColor;
				StringBuilder sb = new StringBuilder();
				ParserException pex = ex as ParserException;
				if (pex != null)
				{
					if (m_ExpressionTextBox != null)
					{
						int line, column;
						m_ExpressionTextBox.CharIdxToLineAndColumn(pex.ErrorPos, out line, out column);
						sb.AppendFormat("line={0} column={1}: ", line+1, column+1);
					}
				}
				sb.Append(ex.Message);
				sb.Append("\r\nPress F4 to jump to the error location.");
				m_CompileResultTextBox.Text = sb.ToString();
				return;
			}

			m_CompileResultTextBox.BackColor = SystemColors.Control;
			Value v = m_PrevFinishedJob.EvalResult;
			m_CompileResultTextBox.Text = v == null ? "" : v.ToString();
		}

		class UnderlineBeginComparer : IComparer<UnderlineDataContainer.Underline>
		{
			public virtual int Compare(UnderlineDataContainer.Underline a, UnderlineDataContainer.Underline b)
			{
				return a.Begin - b.Begin;
			}

			public static UnderlineBeginComparer Instance = new UnderlineBeginComparer();
		}

		void ExpressionTextBox_PostPaint(ColorizedPlainTextBox sender, Graphics g)
		{
			Size client_size = m_ExpressionTextBox.ClientSize;
			if (client_size.IsEmpty)
				return;
			int start_char_index = m_ExpressionTextBox.GetCharIndexFromPosition(new Point(0, 0));
			int last_visible_line_start_char_index = m_ExpressionTextBox.GetCharIndexFromPosition(new Point(0, client_size.Height-1));
			int last_visible_line = m_ExpressionTextBox.GetLineFromCharIndex(last_visible_line_start_char_index);
			int end_char_index;
			if (last_visible_line + 1 >= m_ExpressionTextBox.LineCount)
				end_char_index = m_ExpressionTextBox.TextLength;
			else
				end_char_index = m_ExpressionTextBox.GetLineStartCharIndexFromLineNumber(last_visible_line + 1);
			Debug.Assert(start_char_index <= end_char_index);
			if (start_char_index > end_char_index)
				return;

			List<UnderlineDataContainer.Underline> underlines = UnderlineData.Underlines;
			int start_idx = underlines.BinarySearch(new UnderlineDataContainer.Underline(start_char_index, start_char_index), UnderlineBeginComparer.Instance);
			int end_idx = underlines.BinarySearch(new UnderlineDataContainer.Underline(end_char_index, end_char_index), UnderlineBeginComparer.Instance);
			if (start_idx < 0)
				start_idx = ~start_idx;
			if (end_idx < 0)
				end_idx = ~end_idx;

			int y_offset = m_ExpressionTextBox.Font.Height - 1;
			for (int i = start_idx; i < end_idx; ++i)
			{
				UnderlineDataContainer.Underline underline = underlines[i];
				Point pos0 = m_ExpressionTextBox.GetPositionFromCharIndex(underline.Begin);
				Point pos1 = m_ExpressionTextBox.GetPositionFromCharIndex(underline.End);
				int y = pos0.Y + y_offset;
				DrawUnderline(y, pos0.X, pos1.X, g);
			}
		}

		void DrawUnderline(int y, int x, int x_end, Graphics g)
		{
			List<Point> points = new List<Point>();
			for (int i = 0; x < x_end; ++i)
			{
				switch (i % 4)
				{
					case 0:
						points.Add(new Point(x, y));
						x += 1;
						break;
					case 1:
						points.Add(new Point(x, y - 2));
						x += 3;
						break;
					case 2:
						points.Add(new Point(x, y - 2));
						x += 1;
						break;
					case 3:
						points.Add(new Point(x, y));
						x += 3;
						break;
				}
			}
			g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			g.DrawLines(m_UnderlinePen, points.ToArray());
		}


		static char[] m_WhiteSpaces = new char[] { ' ', '\t', '\r', '\n' };

		void UndoEntryAdded(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			UpdateDelayedCompileErrorDeadline();
			StartCompilation(undo_entry.Id);
			if (undo_entry.CutText.Length > 0)
				UnderlineData.TextCut(undo_entry.Pos, undo_entry.CutText.Length);
			if (undo_entry.PastedText.Length > 0)
				UnderlineData.TextPasted(undo_entry.Pos, undo_entry.PastedText.Length, undo_entry.PastedText.IndexOfAny(m_WhiteSpaces) >= 0);
		}

		void AfterUndo(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			UpdateDelayedCompileErrorDeadline();
			StartCompilation(undo_entry.Id);
			if (undo_entry.PastedText.Length > 0)
				UnderlineData.TextCut(undo_entry.Pos, undo_entry.PastedText.Length);
			if (undo_entry.CutText.Length > 0)
				UnderlineData.TextPasted(undo_entry.Pos, undo_entry.CutText.Length, undo_entry.CutText.IndexOfAny(m_WhiteSpaces) >= 0);
		}

		void AfterRedo(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			UpdateDelayedCompileErrorDeadline();
			StartCompilation(undo_entry.Id);
			if (undo_entry.CutText.Length > 0)
				UnderlineData.TextCut(undo_entry.Pos, undo_entry.CutText.Length);
			if (undo_entry.PastedText.Length > 0)
				UnderlineData.TextPasted(undo_entry.Pos, undo_entry.PastedText.Length, undo_entry.PastedText.IndexOfAny(m_WhiteSpaces) >= 0);
		}

		void ExpressionTextBox_SelectionChanged(object sender, EventArgs e)
		{
			UpdateDelayedCompileErrorDeadline();
		}

		UnderlineDataContainer UnderlineData
		{
			get
			{
				return m_UnderlineData;
			}
			set
			{
				m_UnderlineData = value;
				if (m_WarningsLabel != null)
					m_WarningsLabel.Text = UnderlineData.Underlines.Count.ToString();
			}
		}

		UnderlineDataContainer m_UnderlineData = new UnderlineDataContainer();

		class UnderlineDataContainer
		{
			public class Underline
			{
				public int Begin;
				public int End;

				public Underline(int begin, int end)
				{
					Begin = begin;
					End = end;
				}
			}

			public List<Underline> Underlines { get { return m_Underlines; } }

			public UnderlineDataContainer()
			{
				m_Underlines = new List<Underline>();
			}

			public UnderlineDataContainer(List<Variable> sorted_variables)
			{
				m_Underlines = new List<Underline>();
				Variable prev_var = null;
				foreach (Variable v in sorted_variables)
				{
					if (prev_var != null)
						Debug.Assert(prev_var.Position < v.Position);
					m_Underlines.Add(new Underline(v.Position, v.Position + v.Length));
					prev_var = v;
				}
			}

			public void TextPasted(int pos, int length, bool contains_whitespace)
			{
				Debug.Assert(length > 0);
				int idx;
				for (idx = 0; idx < m_Underlines.Count; ++idx)
				{
					Underline u = m_Underlines[idx];
					if (u.Begin >= pos)
						break;
					if (u.End > pos)
					{
						if (contains_whitespace)
							u.End = pos;
						else
							u.End += length;
						++idx;
						break;
					}
				}

				for (; idx < m_Underlines.Count; ++idx)
				{
					Underline u = m_Underlines[idx];
					u.Begin += length;
					u.End += length;
				}
			}

			public void TextCut(int pos, int length)
			{
				Debug.Assert(length > 0);
				int end = pos + length;
				int idx;
				for (idx = 0; idx < m_Underlines.Count; ++idx)
				{
					Underline u = m_Underlines[idx];
					if (u.Begin >= pos)
						break;
					if (u.End > pos)
					{
						if (pos + length >= u.End)
							u.End = pos;
						else
							u.End -= length;
						++idx;
						break;
					}
				}

				int del_range_begin = idx;

				for (; idx < m_Underlines.Count; ++idx)
				{
					Underline u = m_Underlines[idx];
					if (u.Begin >= end)
						break;
					if (u.End > end)
					{
						u.Begin = end;
						break;
					}
				}

				int del_range_end = idx;

				for (; idx < m_Underlines.Count; ++idx)
				{
					Underline u = m_Underlines[idx];
					u.Begin -= length;
					u.End -= length;
				}

				m_Underlines.RemoveRange(del_range_begin, del_range_end - del_range_begin);
			}

			List<Underline> m_Underlines;
		}
	}
}
