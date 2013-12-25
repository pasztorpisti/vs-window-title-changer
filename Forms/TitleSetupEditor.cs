using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;
using VSWindowTitleChanger.ExpressionEvaluator;
using VSWindowTitleChanger.ExpressionEvaluator.Tokenizer;

namespace VSWindowTitleChanger
{
	public partial class TitleSetupEditor : Form
	{
		System.Windows.Forms.Timer m_Timer_UpdateVariables;
		TitleSetupEditorHelp m_HelpForm;

		TitleSetup m_TitleSetup = new TitleSetup();
		TitleSetup m_OrigTitleSetup = new TitleSetup();

		Dictionary<string, Value> m_Variables = new Dictionary<string, Value>();

		bool m_ClosingWithOK;

		public delegate void SetupEditEvent(TitleSetup original);
		public event SetupEditEvent SaveEditedSetup;
		public event SetupEditEvent RevertToOriginalSetup;

		BackgroundExpressionCompiler m_BackgroundExpressionCompiler;

		public TitleSetupEditor()
		{
			InitializeComponent();

			m_Timer_UpdateVariables = new System.Windows.Forms.Timer();
			m_Timer_UpdateVariables.Interval = 1000;
			m_Timer_UpdateVariables.Tick += new EventHandler(UpdateVariables_Tick);

			VisibleChanged += new EventHandler(TitleSetupEditor_VisibleChanged);
			Shown += new EventHandler(TitleSetupEditor_Shown);
			FormClosing += new FormClosingEventHandler(TitleSetupEditor_FormClosing);
			FormClosed += new FormClosedEventHandler(TitleSetupEditor_FormClosed);

			buttonOK.Click += new EventHandler(buttonOK_Click);
			buttonCancel.Click += new EventHandler(buttonCancel_Click);
			buttonHelp.Click += new EventHandler(buttonHelp_Click);
			buttonSave.Click += new EventHandler(buttonSave_Click);
			buttonRevert.Click += new EventHandler(buttonRevert_Click);

			editTitleExpression.SetTabStopChars(ExpressionTextBox.TAB_SIZE);

			editTitleExpression.SelectionChanged += new EventHandler(editTitleExpression_SelectionChanged);
			editTitleExpression.AfterUndo += new ColorizedPlainTextBox.UndoHandler(editTitleExpression_AfterUndo);
			editTitleExpression.AfterRedo += new ColorizedPlainTextBox.UndoHandler(editTitleExpression_AfterRedo);
			editTitleExpression.UndoEntryAdded += new ColorizedPlainTextBox.UndoHandler(editTitleExpression_UndoEntryAdded);

			m_BackgroundExpressionCompiler = new BackgroundExpressionCompiler();
			m_BackgroundExpressionCompiler.ExpressionTextBox = editTitleExpression;
			m_BackgroundExpressionCompiler.WarningsLabel = labelWarnings;
			m_BackgroundExpressionCompiler.CompileResultTextBox = titleOrCompileError;

#if DEBUG_GUI
			editTitleExpression.Text = "if (true)\t\ta\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\t0\n{\n\t\"this\"\n}\nelse\n{\n\t\"that\"\n}\nXXXXXXXXXWWWWXXXXXIIIIIIIII\u8a9e\u65e0\n";
#endif
		}


		public TitleSetup TitleSetup
		{
			get
			{
				return m_TitleSetup;
			}
			set
			{
				m_TitleSetup = value;
				m_OrigTitleSetup = Util.Clone(m_TitleSetup);
				SetupChanged();
			}
		}

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


		bool IsSetupModified()
		{
			return 0 != m_TitleSetup.CompareTo(m_OrigTitleSetup);
		}

		void SetupModifiedChanged()
		{
			bool modified = IsSetupModified();
			buttonSave.Enabled = modified;
			buttonRevert.Enabled = modified;
		}

		void SetupChanged()
		{
			editTitleExpression.Text = m_TitleSetup.TitleExpression;
			TitleExpressionChanged();
		}

		void TitleExpressionChanged()
		{
			SetupModifiedChanged();
			UpdateLineAndColumnLabels();
			m_BackgroundExpressionCompiler.ForceRecompile();
		}

		void editTitleExpression_SelectionChanged(object sender, EventArgs e)
		{
			UpdateLineAndColumnLabels();
		}

		void editTitleExpression_UndoEntryAdded(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			m_TitleSetup.TitleExpression = editTitleExpression.Text;
			TitleExpressionChanged();
		}

		void editTitleExpression_AfterRedo(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			m_TitleSetup.TitleExpression = editTitleExpression.Text;
			TitleExpressionChanged();
		}

		void editTitleExpression_AfterUndo(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			m_TitleSetup.TitleExpression = editTitleExpression.Text;
			TitleExpressionChanged();
		}


		Dictionary<string, ListViewItem> m_VariableNameToLVI = new Dictionary<string, ListViewItem>();

		void UpdateVariables()
		{
#if !DEBUG_GUI
			PackageGlobals globals = PackageGlobals.Instance();
			EvalContext eval_ctx = globals.CreateFreshEvalContext();
			if (CompareVariables(eval_ctx.VariableValues, m_Variables))
				return;
			m_Variables = eval_ctx.VariableValues;

			bool first_time = m_VariableNameToLVI.Count == 0;
			List<string> variable_names = new List<string>(m_Variables.Keys);
			if (first_time)
				variable_names.AddRange(globals.CompileTimeConstants.VariableValues.Keys);
			variable_names.Sort();
			foreach (string name in variable_names)
			{
				if (first_time)
				{
					// Skipping the "true" and "false" compile time constants.
					if (name.Equals("true", StringComparison.OrdinalIgnoreCase) || name.Equals("false", StringComparison.OrdinalIgnoreCase))
						continue;
					Value val;
					if (!m_Variables.TryGetValue(name, out val))
						val = globals.CompileTimeConstants.VariableValues[name];
					ListViewItem lvi = new ListViewItem(name);
					lvi.Font = editTitleExpression.Font;
					ListViewItem.ListViewSubItem lvsi = lvi.SubItems.Add(val.ToString());
					lvsi.Font = lvi.Font;
					lvsi = lvi.SubItems.Add(val.GetType().ToString());
					lvsi.Font = lvi.Font;
					listVariables.Items.Add(lvi);
					m_VariableNameToLVI[name] = lvi;
				}
				else
				{
					Value val = m_Variables[name];
					ListViewItem lvi = m_VariableNameToLVI[name];
					lvi.SubItems[1].Text = val.ToString();
					lvi.SubItems[2].Text = val.GetType().ToString();
				}
			}
#endif

			m_BackgroundExpressionCompiler.ForceRecompile();
		}

		void UpdateLineAndColumnLabels()
		{
			int line, column;
			editTitleExpression.CharIdxToLineAndColumn(editTitleExpression.SelectionStart, out line, out column);
			labelLine.Text = (line + 1).ToString();
			labelColumn.Text = (column + 1).ToString();
		}

		void UpdateVariables_Tick(object sender, EventArgs e)
		{
			UpdateVariables();
		}

		void TitleSetupEditor_VisibleChanged(object sender, EventArgs e)
		{
			if (Visible)
			{
				m_ClosingWithOK = false;

#if !DEBUG_GUI
				m_BackgroundExpressionCompiler.CompileTimeConstants = PackageGlobals.Instance().CompileTimeConstants;
#endif
				m_BackgroundExpressionCompiler.Enabled = true;

				m_Timer_UpdateVariables.Start();
				UpdateVariables();
			}
			else
			{
				m_BackgroundExpressionCompiler.Enabled = false;
				m_Timer_UpdateVariables.Stop();

				TitleSetup = new TitleSetup();
				editTitleExpression.Text = "";
				editTitleExpression.ClearUndo();
			}
		}

		void TitleSetupEditor_Shown(object sender, EventArgs e)
		{
			m_ClosingWithOK = false;
		}

		void TitleSetupEditor_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (m_HelpForm != null)
				m_HelpForm.Close();
			if (!m_ClosingWithOK && RevertToOriginalSetup != null)
				RevertToOriginalSetup(m_OrigTitleSetup);
			Visible = false;
#if !DEBUG_GUI
			e.Cancel = true;
#endif
		}

		TitleSetupEditorHelp GetHelpForm()
		{
			if (m_HelpForm == null)
				m_HelpForm = new TitleSetupEditorHelp();
			return m_HelpForm;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (components != null)
					components.Dispose();
				m_Timer_UpdateVariables.Stop();
				if (m_HelpForm != null)
					m_HelpForm.Dispose();
				if (m_BackgroundExpressionCompiler != null)
				{
					m_BackgroundExpressionCompiler.Dispose();
					m_BackgroundExpressionCompiler = null;
				}
			}
			base.Dispose(disposing);
		}

		private void ShowHelp()
		{
			TitleSetupEditorHelp help_form = GetHelpForm();
			if (help_form.Visible)
				help_form.BringToFront();
			else
				help_form.Show();
		}

		private void TitleSetupEditor_FormClosed(object sender, FormClosedEventArgs e)
		{
			if (m_HelpForm != null)
				m_HelpForm.Close();
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
			m_ClosingWithOK = true;
			if (IsSetupModified() && SaveEditedSetup != null)
				SaveEditedSetup(m_TitleSetup);
			Close();
		}

		private void buttonCancel_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void buttonHelp_Click(object sender, EventArgs e)
		{
			ShowHelp();
		}

		void buttonRevert_Click(object sender, EventArgs e)
		{
			m_TitleSetup = Util.Clone(m_OrigTitleSetup);
			if (RevertToOriginalSetup != null)
				RevertToOriginalSetup(m_TitleSetup);
			SetupChanged();
			editTitleExpression.Focus();
		}

		void buttonSave_Click(object sender, EventArgs e)
		{
			m_OrigTitleSetup = Util.Clone(m_TitleSetup);
			SetupModifiedChanged();
			if (SaveEditedSetup != null)
				SaveEditedSetup(m_TitleSetup);
			editTitleExpression.Focus();
		}


		private bool m_CustomTabbingEnabled;

		public bool CustomTabbingEnabled
		{
			get
			{
				return m_CustomTabbingEnabled;
			}
			set
			{
				m_CustomTabbingEnabled = value;
			}
		}


		[DllImport("USER32.dll")]
		static extern short GetKeyState(Keys key);

		const int WM_KEYDOWN = 0x0100;
		const int WM_KEYUP = 0x0101;
		const int WM_CHAR = 0x0102;

		bool m_ConsumeTab;


		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr SendMessage(IntPtr hWnd, Int32 Msg, IntPtr wParam, IntPtr lParam);

		private const int WM_GETDLGCODE = 0x0087;
		private const int DLGC_WANTTAB = 0x0002;
		private const int DLGC_WANTALLKEYS = 0x0004;


		protected override bool ProcessKeyPreview(ref Message m)
		{
			if (m.Msg != WM_KEYDOWN && m.Msg != WM_KEYUP && m.Msg != WM_CHAR)
				return false;

			if (m.Msg == WM_CHAR)
			{
				if (m_CustomTabbingEnabled && m_ConsumeTab)
				{
					if (m.WParam == (IntPtr)9)
						return true;
					return false;
				}
			}
			else
			{
				bool key_down = m.Msg == WM_KEYDOWN;
				Keys key = (Keys)m.WParam;
				switch (key)
				{
					case Keys.F1:
						if (key_down)
							ShowHelp();
						return true;
					case Keys.F4:
						if (key_down)
							m_BackgroundExpressionCompiler.JumpToErrorLocation();
						return true;
					case Keys.Escape:
						if (key_down)
							Close();
						return true;
					case Keys.Tab:
						if (m_CustomTabbingEnabled)
						{
							if (key_down)
							{
								int code = (int)SendMessage(m.HWnd, WM_GETDLGCODE, m.WParam, IntPtr.Zero);
								m_ConsumeTab = 0 == (code & (DLGC_WANTALLKEYS | DLGC_WANTTAB));
								if (m_ConsumeTab)
								{
									bool forward = GetKeyState(Keys.ShiftKey) >= 0;
									ProcessTabKey(forward);
								}
							}
							return m_ConsumeTab;
						}
						break;
				}
			}

			return base.ProcessKeyPreview(ref m);
		}
	}
}