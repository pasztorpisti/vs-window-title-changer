using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.ComponentModel;

namespace VSWindowTitleChanger
{
	class ColoredRichTextBox : RichTextBox
	{
		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);
		}

		public class ColoredRange
		{
			public int Start;
			public int Length;
			// BackgroundColor == Color.Empty doesn't change the back color.
			public Color BackColor;
			// ForegroundColor == Color.Empty doesn't change the back color.
			public Color ForeColor;

			public ColoredRange(int start, int length, Color back_color, Color fore_color)
			{
				Start = start;
				Length = length;
				BackColor = back_color;
				ForeColor = fore_color;
			}
		}

		public void Colorize(IEnumerable<ColoredRange> ranges, bool reset_colors_first)
		{
			SuspendPainting();
			try
			{
				if (reset_colors_first)
				{
					ColoredRange full_range = new ColoredRange(0, Text.Length, DefaultBackColor, DefaultForeColor);
					ApplyRange(full_range);
				}
				foreach (ColoredRange range in ranges)
					ApplyRange(range);
			}
			finally
			{
				ResumePainting();
			}
		}

		private void ApplyRange(ColoredRange range)
		{
			SelectionStart = range.Start;
			SelectionLength = range.Length;
			if (range.BackColor != Color.Empty)
				SelectionBackColor = range.BackColor;
			if (range.ForeColor != Color.Empty)
				SelectionColor = range.ForeColor;
		}


		[DllImport("user32.dll")]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, ref Point lParam);
		[DllImport("user32.dll")]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, IntPtr lParam);
		[DllImport("user32.dll")]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, ref int lParam);

		const int WM_USER = 0x400;
		const int WM_SETREDRAW = 0x000B;
		const int EM_GETEVENTMASK = WM_USER + 59;
		const int EM_SETEVENTMASK = WM_USER + 69;
		const int EM_GETSCROLLPOS = WM_USER + 221;
		const int EM_SETSCROLLPOS = WM_USER + 222;

		Point m_ScrollPoint;
		int m_SuspendCounter;
		IntPtr m_EventMask;
		int m_SuspendIndex;
		int m_SuspendLength;

		public void SuspendPainting()
		{
			if (m_SuspendCounter == 0)
			{
				m_SuspendIndex = SelectionStart;
				m_SuspendLength = SelectionLength;
				SendMessage(Handle, EM_GETSCROLLPOS, 0, ref m_ScrollPoint);
				SendMessage(Handle, WM_SETREDRAW, 0, IntPtr.Zero);
				m_EventMask = SendMessage(Handle, EM_GETEVENTMASK, 0, IntPtr.Zero);
			}
			++m_SuspendCounter;
		}

		public void ResumePainting()
		{
			--m_SuspendCounter;
			Debug.Assert(m_SuspendCounter >= 0);
			if (m_SuspendCounter == 0)
			{
				Select(m_SuspendIndex, m_SuspendLength);
				SendMessage(Handle, EM_SETSCROLLPOS, 0, ref m_ScrollPoint);
				SendMessage(Handle, EM_SETEVENTMASK, 0, m_EventMask);
				SendMessage(Handle, WM_SETREDRAW, 1, IntPtr.Zero);
				Invalidate();
			}
		}

		int m_TabStopChars = 8;

		[Browsable(false)]
		public int TabStopChars
		{
			get
			{
				return m_TabStopChars;
			}
			set
			{
				const int EM_SETTABSTOPS = 0x00CB;
				m_TabStopChars = Math.Max(1, value);
				int dialog_units = m_TabStopChars * 4;
				SendMessage(Handle, EM_SETTABSTOPS, (IntPtr)1, ref dialog_units);
			}
		}
	}
}
