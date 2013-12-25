using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace VSWindowTitleChanger
{
	class RichTextBoxLineNumbers : UserControl
	{
		Color m_RightLineColor = Color.LightGray;
		bool m_UseTextBoxFont = true;
		int m_RightPadding = 10;
		ColorizedPlainTextBox m_TextBox;

		int m_StartLine = -1;
		List<int> m_LineYOffsets = new List<int>();

		public RichTextBoxLineNumbers()
		{
			SetStyle(ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
			ForeColor = Color.Gray;
		}

		public Color RightLineColor
		{
			get
			{
				return m_RightLineColor;
			}
			set
			{
				m_RightLineColor = value;
				Invalidate();
			}
		}

		[DefaultValue(true)]
		public bool UseTextBoxFont
		{
			get
			{
				return m_UseTextBoxFont;
			}
			set
			{
				if (m_UseTextBoxFont == value)
					return;
				m_UseTextBoxFont = value;
				Invalidate();
			}
		}

		[DefaultValue(10)]
		public int RightPadding
		{
			get
			{
				return m_RightPadding;
			}
			set
			{
				if (m_RightPadding == value)
					return;
				m_RightPadding = value;
				Invalidate();
			}
		}

		public ColorizedPlainTextBox TextBox
		{
			get
			{
				return m_TextBox;
			}
			set
			{
				if (m_TextBox != null)
				{
					m_TextBox.AfterRedo -= m_TextBox_AfterUndoRedo;
					m_TextBox.AfterUndo -= m_TextBox_AfterUndoRedo;
					m_TextBox.UndoEntryAdded -= m_TextBox_AfterUndoRedo;
					m_TextBox.VerticalScrollPosChanged -= text_box_VerticalScrollPosChanged;
				}

				m_TextBox = value;

				if (m_TextBox != null)
				{
					m_TextBox.AfterRedo += m_TextBox_AfterUndoRedo;
					m_TextBox.AfterUndo += m_TextBox_AfterUndoRedo;
					m_TextBox.UndoEntryAdded += m_TextBox_AfterUndoRedo;
					m_TextBox.VerticalScrollPosChanged += text_box_VerticalScrollPosChanged;
				}

				m_StartLine = -1;
				if (m_TextBox != null)
					UpdateLineNumbers();
				else
					Invalidate();
			}
		}

		void text_box_VerticalScrollPosChanged(ColorizedPlainTextBox sender)
		{
			DelayedUpdateLineNumbers();
		}

		void m_TextBox_AfterUndoRedo(ExpressionTextBox.UndoEntry undo_entry)
		{
			DelayedUpdateLineNumbers();
		}

		void m_TextBox_VScroll(object sender, EventArgs e)
		{
			DelayedUpdateLineNumbers();
		}

		void m_TextBox_ClientSizeChanged(object sender, EventArgs e)
		{
			DelayedUpdateLineNumbers();
		}

		void m_TextBox_TextChanged(object sender, EventArgs e)
		{
			DelayedUpdateLineNumbers();
		}

		protected void OnClientSizeChanged()
		{
			DelayedUpdateLineNumbers();
		}

		delegate void Action();
		void DelayedUpdateLineNumbers()
		{
			if (!IsHandleCreated)
				CreateHandle();
			BeginInvoke(new Action(delegate() { UpdateLineNumbers(); }));
		}

		public void UpdateLineNumbers()
		{
			if (m_TextBox == null || !m_TextBox.IsHandleCreated)
				return;

			List<int> line_y_offsets = new List<int>();

			Size client_size = ClientSize;
			int start_line = m_TextBox.GetFirstVisibleLine();
			int num_lines = m_TextBox.LineCount;
			int text_length = m_TextBox.TextLength;
			int last_y = -10000;

			for (int i = start_line; i < num_lines; ++i)
			{
				int char_idx = m_TextBox.GetLineStartCharIndexFromLineNumber(i);
				if (char_idx < 0)
					break;
				Point pos = m_TextBox.GetPositionFromCharIndex(char_idx);
				if (last_y == pos.Y)
				{
					// If the last line is empty then EM_POSFROMCHAR returns the y coordinate of the previous line.
					pos.Y += (int)m_TextBox.Font.GetHeight();
				}
				else if (pos.Y < last_y)
				{
					break;
				}
				line_y_offsets.Add(pos.Y);
				last_y = pos.Y;
				if (pos.Y >= client_size.Height)
					break;
			}

			bool invalidate = m_StartLine != start_line || m_LineYOffsets.Count != line_y_offsets.Count;
			if (!invalidate)
			{
				for (int i = 0, e = line_y_offsets.Count; i < e; ++i)
				{
					if (m_LineYOffsets[i] != line_y_offsets[i])
					{
						invalidate = true;
						break;
					}
				}
			}

			if (invalidate && IsHandleCreated)
			{
				m_StartLine = start_line;
				m_LineYOffsets = line_y_offsets;
				Invalidate();
			}
		}


		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			if (m_TextBox == null)
				return;

			Size client_size = ClientSize;
			if (m_RightLineColor != Color.Empty)
			{
				int offset = m_RightPadding / 2 + 1;
				using (Pen pen = new Pen(m_RightLineColor))
					e.Graphics.DrawLine(pen, new Point(client_size.Width - offset, 0), new Point(client_size.Width - offset, client_size.Height));
			}

			Font font = m_UseTextBoxFont ? m_TextBox.Font : Font;

			if (m_StartLine < 0)
				return;
			Rectangle rt = new Rectangle(Point.Empty, client_size);
			rt.Width -= m_RightPadding;
			int line = m_StartLine + 1;
			foreach (int y in m_LineYOffsets)
			{
				rt.Y = y;
				TextRenderer.DrawText(e.Graphics, line.ToString(), font, rt, ForeColor, BackColor,
					TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Right | TextFormatFlags.Top);
				++line;
			}
		}

		protected override void OnClientSizeChanged(EventArgs e)
		{
			base.OnClientSizeChanged(e);
			UpdateLineNumbers();
		}
	}
}
