using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Threading;
using VSWindowTitleChanger.ExpressionEvaluator;

namespace VSWindowTitleChanger
{
	public partial class TitleSetupEditor : Form
	{
		Dispatcher m_UIThradDispatcher;
		private DispatcherTimer m_DispatcherTimer_UpdateVariables;

		TitleSetupEditorHelp m_HelpForm;

		TitleSetup m_TitleSetup = new TitleSetup();
		TitleSetup m_OrigTitleSetup = new TitleSetup();

		Dictionary<string, Value> m_Variables = new Dictionary<string, Value>();

		bool m_ClosingWithOK;

		public delegate void SetupEditEvent(TitleSetup original);
		public event SetupEditEvent SaveEditedSetup;
		public event SetupEditEvent RevertToOriginalSetup;

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
			ReEvaluate();
		}

		void ReEvaluate()
		{
			PackageGlobals.CompiledExpression compiled_expression = PackageGlobals.Instance().CompiledExpressionCache.GetEntry(editTitleExpression.Text);
			if (compiled_expression.expression == null)
			{
				if (compiled_expression.compile_error == null)
				{
					titleOrCompileError.Text = "";
				}
				else
				{
					titleOrCompileError.Text = compiled_expression.compile_error.Message;
				}
			}
			else
			{
				EvalContext eval_ctx = new EvalContext();
				SafeEvalContext safe_eval_ctx = new SafeEvalContext(eval_ctx);
				eval_ctx.VariableValues = m_Variables;
				Value title_value = compiled_expression.expression.Evaluate(safe_eval_ctx);
				titleOrCompileError.Text = title_value.ToString();
			}
		}

		public TitleSetupEditor()
		{
			InitializeComponent();

			m_UIThradDispatcher = Dispatcher.CurrentDispatcher;
			m_DispatcherTimer_UpdateVariables = new DispatcherTimer();
			m_DispatcherTimer_UpdateVariables.Interval = new TimeSpan(0, 0, 1);
			m_DispatcherTimer_UpdateVariables.Tick += new EventHandler(UpdateVariables_Tick);

			editTitleExpression.TabStopChars = 4;

			VisibleChanged += new EventHandler(TitleSetupEditor_VisibleChanged);
			Shown += new EventHandler(TitleSetupEditor_Shown);
			FormClosing += new FormClosingEventHandler(TitleSetupEditor_FormClosing);
			FormClosed += new FormClosedEventHandler(TitleSetupEditor_FormClosed);

			buttonOK.Click += new EventHandler(buttonOK_Click);
			buttonCancel.Click += new EventHandler(buttonCancel_Click);
			buttonHelp.Click += new EventHandler(buttonHelp_Click);
			buttonSave.Click += new EventHandler(buttonSave_Click);
			buttonRevert.Click += new EventHandler(buttonRevert_Click);

			editTitleExpression.TextChanged += new EventHandler(editTitleExpression_TextChanged);
		}

		Dictionary<string, ListViewItem> m_VariableNameToLVI = new Dictionary<string, ListViewItem>();

		void UpdateVariables()
		{
#if !DEBUG_GUI
			PackageGlobals globals = PackageGlobals.Instance();
			EvalContext eval_ctx = new EvalContext();
			PackageGlobals.VSMultiInstanceInfo multi_instance_info;
			globals.GetVSMultiInstanceInfo(out multi_instance_info);
			globals.SetVariableValuesFromIDEState(eval_ctx, multi_instance_info);
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

			ReEvaluate();
		}

		void UpdateVariables_Tick(object sender, EventArgs e)
		{
			UpdateVariables();
		}

		void editTitleExpression_TextChanged(object sender, EventArgs e)
		{
			m_TitleSetup.TitleExpression = editTitleExpression.Text;
			TitleExpressionChanged();
		}

		void TitleSetupEditor_VisibleChanged(object sender, EventArgs e)
		{
			if (Visible)
			{
				m_ClosingWithOK = false;
				m_DispatcherTimer_UpdateVariables.Start();
				UpdateVariables();
			}
			else
			{
				m_DispatcherTimer_UpdateVariables.Stop();
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
#if !DEBUG_GUI
			e.Cancel = true;
			Visible = false;
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
				if (m_HelpForm != null)
					m_HelpForm.Dispose();
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
		}

		void buttonSave_Click(object sender, EventArgs e)
		{
			m_OrigTitleSetup = Util.Clone(m_TitleSetup);
			SetupModifiedChanged();
			if (SaveEditedSetup != null)
				SaveEditedSetup(m_TitleSetup);
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


		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		static extern IntPtr SendMessage(IntPtr hWnd, Int32 Msg, IntPtr wParam, IntPtr lParam);

		private const int WM_GETDLGCODE = 0x0087;
		private const int DLGC_WANTTAB = 0x0002;
		private const int DLGC_WANTALLKEYS = 0x0004;


		// We are using ProcessKeyPreview because this works with both Windows Forms (pre-VS2010) and WPF.
		protected override bool ProcessKeyPreview(ref Message m)
		{
			if (m.Msg != WM_KEYDOWN && m.Msg != WM_KEYUP && m.Msg != WM_CHAR)
				return false;

			if (m_CustomTabbingEnabled && m_ConsumeTab && m.Msg == WM_CHAR)
			{
				if (m.WParam == (IntPtr)9)
					return true;
				return false;
			}

			bool key_down = m.Msg == WM_KEYDOWN;
			Keys key = (Keys)m.WParam;
			switch (key)
			{
				case Keys.F1:
					if (key_down)
						ShowHelp();
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
			return base.ProcessKeyPreview(ref m);
		}
	}
}