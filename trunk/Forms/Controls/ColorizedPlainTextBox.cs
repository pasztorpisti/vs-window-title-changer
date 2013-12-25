using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace VSWindowTitleChanger
{
	// WARNING!!! This is one of the biggest stack of hacks in the world!!!!!!!!
	// All I needed is a syntax highlighted multiline editbox that works with .Net v2.0.
	// Unfortunately a normal TextBox control can not be colorized and it has no undo buffer.
	// A RichTextBox control records color changes to its undo buffer so I can't use its undo/redo,
	// instead I just clear it all the time and I implement my own undo/redo functionality.
	// My undo/redo isn't optimal performancewise (because it often performs operations that work
	// with the whole text of the control) but I will allow only a few ten-thousand characters.
	// The colorization is also hacky. What I do in case of colorization:
	// - Pausing all drawing and state change event firing on the richtext: PausePainting()
	// - I move around the cursor, select the colorizable parts and colorize the selection.
	// - Unpausing control drawing and event firing: ResumePainting()
	// Colorization performance hack: Selecting small pieces of texts and then using the SelectionColor
	// and SelectionBackColor properties on these small selections was very slow. Instead I
	// decided to select all the colorizable text in one big chunk, reproducing the whole
	// selected text as colorized rtf and then using the SelectedRtf property. The performance of
	// this technique is only a fragment of changing the selection multiple times and using
	// SelectionColor + SelectionBackColor.
	// Problem: While colorizing the text we have to change the selection so before colorization
	// we save the current selection and then we have restore it when we are done. Changing the
	// selection unfortunately automatically changes the horizontal and vertical scroll position
	// of the richtext control. No problem, we save and restore that as well with EM_GETSCROLLPOS
	// and EM_SETSCROLLPOS. But there is one big problem: EM_SETSCROLLPOS is buggy since ancient
	// times! When the hight of the whole text of the RichText control exceeds 64K pixels
	// EM_SETSCROLLPOS stops working properly. This 64K pixel limit with my Consolas 9pt font is
	// enough until my text control reaches ~4600 lines. For me this is enough so I don't care....
	[ToolboxBitmap(typeof(RichTextBox))]
	class ColorizedPlainTextBox : RichTextBox
	{
		public delegate void OnPostPaint(ColorizedPlainTextBox sender, Graphics g);
		public event OnPostPaint PostPaint;

		public delegate void OnVerticalScrollPosChanged(ColorizedPlainTextBox sender);
		public event OnVerticalScrollPosChanged VerticalScrollPosChanged;

		const int WM_KEYDOWN = 0x0100;
		const int WM_KEYUP = 0x0101;
		const int WM_CHAR = 0x0102;

		const int WM_PAINT = 15;
		const int WM_MOUSEWHEEL = 0x20A;
		const int WM_VSCROLL = 0x115;

		const int SB_LINEUP = 0;
		const int SB_LINEDOWN = 1;
		const int SB_THUMBTRACK = 5;

		POINT m_PrevScrollPos = new POINT(Int32.MinValue, Int32.MinValue);

		protected override void WndProc(ref Message m)
		{
			switch (m.Msg)
			{
				case WM_MOUSEWHEEL:
					if (m_SmoothScroll)
						break;
					int delta = ((int)m.WParam >> 16) / 120;
					IntPtr msg = delta < 0 ? (IntPtr)SB_LINEDOWN : (IntPtr)SB_LINEUP;
					if (delta < 0)
						delta = -delta;
					for (int i = 0; i < delta; ++i)
						SendMessage(m.HWnd, WM_VSCROLL, msg, IntPtr.Zero);
					m.Result = IntPtr.Zero;
					return;
				case WM_PAINT:
					if (VerticalScrollPosChanged != null)
					{
						// Handling the VScroll event wasn't enough: When the user scrolls the scrollbar by grabbing the thumb of the
						// scrollbar then if you hold the thumb in one position for 1-2 seconds then the text is automatically scrolled
						// to a whole line vertically without sending any events. We have to know about every single Y scroll pos change
						// in order to correctly redraw the line number control.
						POINT scroll_pos = new POINT();
						SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scroll_pos);
						if (scroll_pos.Y != m_PrevScrollPos.Y)
						{
							m_PrevScrollPos = scroll_pos;
							VerticalScrollPosChanged(this);
						}
					}

					if (PostPaint == null)
						break;
					// HACK: couldn't get it work otherwise...
					Invalidate();
					base.WndProc(ref m);
					using (Graphics g = CreateGraphics())
						PostPaint(this, g);
					return;
			}
			base.WndProc(ref m);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if ((e.KeyCode == Keys.V && 0 != (e.Modifiers & Keys.Control)) ||
				(e.KeyCode == Keys.Insert && 0 != (e.Modifiers & Keys.Shift)))
			{
				e.Handled = true;
				PausePainting();
				try
				{
					// We do this in the middle of a paused painting because this way the text isn't
					// drawn twice: once without syntax highlighting and then with highlight
					if (Clipboard.ContainsText())
					{
						string text = Clipboard.GetText(TextDataFormat.UnicodeText);
						if (TextLength - SelectionLength + text.Length <= MaxLength)
							SelectedText = text;
					}
					DetectAndSaveChange();
				}
				finally
				{
					ResumePainting();
				}
			}
			else if (e.KeyCode == Keys.Z && 0 != (e.Modifiers & Keys.Control))
			{
				Undo();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Y && 0 != (e.Modifiers & Keys.Control))
			{
				Redo();
				e.Handled = true;
			}

			if (!e.Handled)
				base.OnKeyDown(e);
		}


		[DllImport("kernel32", CharSet = CharSet.Unicode)]
		static extern IntPtr LoadLibrary(string lpFileName);

		class MsftLoader
		{
			IntPtr MsftEditDLL;
			public MsftLoader()
			{
				try
				{
					MsftEditDLL = LoadLibrary("MsftEdit.dll");
				}
				catch
				{
				}
			}
			public bool IsMsftEditDllAvailable()
			{
				return MsftEditDLL != null;
			}
		}
		static MsftLoader m_MsftLoader = new MsftLoader();

		protected override CreateParams CreateParams
		{
			// This makes the framework 2.0 RichTextBox much faster. It handles well some other things too, for example
			// if we have substituted characters from a guest font file then the with of those characters (for example some
			// japanese characters in the middle of my fixed pitch Consolas fonted text) are kept when I perform my rtf
			// coloring trick.
			// The idea comes from here:
			// http://stackoverflow.com/questions/18668920/c-sharp-richeditbox-has-extremely-slow-performance-4-minutes-loading-solve
			get
			{
				CreateParams create_params = base.CreateParams;
				// Replace "RichEdit20W" with "RichEdit50W"
				if (m_MsftLoader.IsMsftEditDllAvailable())
					create_params.ClassName = "RichEdit50W";
				return create_params;
			}
		}

		string m_PrevText;
		int m_PrevSelStart;
		int m_PrevSelLength;

		public class UndoEntry
		{
			public int Id;
			public int PrevCaretPos;
			public int NewCaretPos;
			public int Pos;
			public string CutText;
			public string PastedText;
		}

		class UndoBuffer
		{
			public void AddChange(UndoEntry entry)
			{
				if (m_UndoPos < m_UndoBuffer.Count)
				{
					for (int i=0,e=m_UndoBuffer.Count; i<e; ++i)
						m_CurrentCost -= GetEntryCost(m_UndoBuffer[i]);
					m_UndoBuffer.RemoveRange(m_UndoPos, m_UndoBuffer.Count - m_UndoPos);
				}
				m_UndoBuffer.Add(entry);
				++m_UndoPos;
				m_CurrentCost += GetEntryCost(entry);
				if (m_CurrentCost > MAX_UNDO_BUFFER_COST)
				{
					int entries_to_remove;
					for (entries_to_remove=0; entries_to_remove<m_UndoPos-1; ++entries_to_remove)
					{
						m_CurrentCost -= GetEntryCost(m_UndoBuffer[entries_to_remove]);
						if (m_CurrentCost <= MAX_UNDO_BUFFER_COST)
						{
							entries_to_remove++;
							break;
						}
					}
					m_UndoBuffer.RemoveRange(0, entries_to_remove);
					m_UndoPos -= entries_to_remove;
				}
			}

			public bool CanUndo()
			{
				return m_UndoPos > 0;
			}

			public void Undo(out UndoEntry entry)
			{
				Debug.Assert(CanUndo());
				--m_UndoPos;
				entry = m_UndoBuffer[m_UndoPos];
			}

			public bool CanRedo()
			{
				return m_UndoPos < m_UndoBuffer.Count;
			}

			public void Redo(out UndoEntry entry)
			{
				Debug.Assert(CanRedo());
				entry = m_UndoBuffer[m_UndoPos];
				++m_UndoPos;
			}

			public void Clear()
			{
				m_UndoBuffer.Clear();
				m_UndoPos = 0;
				m_CurrentCost = 0;
			}

			private static int GetEntryCost(UndoEntry entry)
			{
				return FIX_COST_PER_ENTRY + entry.CutText.Length*2 + entry.PastedText.Length*2;
			}

			List<UndoEntry> m_UndoBuffer = new List<UndoEntry>();
			int m_UndoPos;
			int m_CurrentCost;

			const int MAX_UNDO_BUFFER_COST = 100 * 1024 * 1024;
			const int FIX_COST_PER_ENTRY = 64;
		}

		UndoBuffer m_UndoBuffer = new UndoBuffer();
		bool m_UndoingOrRedoing;

		int FindCommonPrefixLength(string a, string b, int max_len)
		{
			for (int i = 0; i < max_len; ++i)
			{
				if (a[i] != b[i])
					return i;
			}
			return max_len;
		}

		int FindCommonPostfixLen(string a, string b, int max_len)
		{
			for (int i = a.Length - 1, j = b.Length - 1, k = 0; k < max_len; --i, --j, ++k)
			{
				if (a[i] != b[j])
					return k;
			}
			return max_len;
		}

		protected void DetectAndSaveChange()
		{
			DetectAndSaveChange(Text, SelectionStart, SelectionLength);
		}

		int m_NextUndoEntryId;

		void DetectAndSaveChange(string new_text, int new_sel_start, int new_sel_length)
		{
			if (m_PrevText != null)
			{
				// We are doing brutalforce change detection as we will work with at most a few hundred kilobytes of text in this project.
				int max_len = Math.Min(m_PrevText.Length, new_text.Length);
				// The Math.Min(m_PrevSelStart, max_len) is there to fix a bug that detected wrongly the actual location of
				// multiple pasted lines. If we pasted lines that are equivalent to the lines starting at the actual paste
				// location then this change detector found our newly pasted text as unchanged text and detected the next
				// block as the change. This caused problems especially in case of the optimized syntax highlighter.
				int common_prefix_len = FindCommonPrefixLength(m_PrevText, new_text, Math.Min(m_PrevSelStart, max_len));
				if (m_PrevText.Length == new_text.Length && new_text.Length == common_prefix_len)
					return;
				int common_postfix_len = common_prefix_len >= max_len ? 0 : FindCommonPostfixLen(m_PrevText, new_text, max_len - common_prefix_len);

				UndoEntry entry = new UndoEntry();
				entry.Id = m_NextUndoEntryId++;
				entry.PrevCaretPos = m_PrevSelStart + m_PrevSelLength;
				entry.NewCaretPos = new_sel_start + new_sel_length;
				entry.Pos = common_prefix_len;
				entry.CutText = m_PrevText.Substring(common_prefix_len, m_PrevText.Length - common_prefix_len - common_postfix_len);
				entry.PastedText = new_text.Substring(common_prefix_len, new_text.Length - common_prefix_len - common_postfix_len);

				m_UndoBuffer.AddChange(entry);

				if (UndoEntryAdded != null)
					UndoEntryAdded(entry);
			}

			m_PrevText = new_text;
			m_PrevSelStart = new_sel_start;
			m_PrevSelLength = new_sel_length;
		}

		public delegate void UndoHandler(UndoEntry undo_entry);
		public event UndoHandler AfterUndo;
		public event UndoHandler AfterRedo;
		public event UndoHandler UndoEntryAdded;

		new public bool CanUndo
		{
			get
			{
				return m_UndoBuffer.CanUndo();
			}
		}

		new public void Undo()
		{
			if (!m_UndoBuffer.CanUndo())
				return;
			m_UndoingOrRedoing = true;
			try
			{
				PausePainting();
				try
				{
					UndoEntry entry;
					m_UndoBuffer.Undo(out entry);
					Select(entry.Pos, entry.PastedText.Length);
					SelectedText = entry.CutText;
					m_PrevText = Text;
					m_PrevSelStart = entry.PrevCaretPos;
					m_PrevSelLength = 0;
					Select(m_PrevSelStart, m_PrevSelLength);
					if (AfterUndo != null)
						AfterUndo(entry);
				}
				finally
				{
					ResumePainting();
				}
			}
			finally
			{
				m_UndoingOrRedoing = false;
			}
		}

		new public bool CanRedo
		{
			get
			{
				return m_UndoBuffer.CanRedo();
			}
		}

		new public void Redo()
		{
			if (!m_UndoBuffer.CanRedo())
				return;
			m_UndoingOrRedoing = true;
			try
			{
				PausePainting();
				try
				{
					UndoEntry entry;
					m_UndoBuffer.Redo(out entry);
					Select(entry.Pos, entry.CutText.Length);
					SelectedText = entry.PastedText;
					m_PrevText = Text;
					m_PrevSelStart = entry.NewCaretPos;
					m_PrevSelLength = 0;
					Select(m_PrevSelStart, m_PrevSelLength);
					if (AfterRedo != null)
						AfterRedo(entry);
				}
				finally
				{
					ResumePainting();
				}
			}
			finally
			{
				m_UndoingOrRedoing = false;
			}
		}

		new public void ClearUndo()
		{
			if (m_UndoingOrRedoing)
				return;
			m_UndoBuffer.Clear();
		}

		protected override void OnSelectionChanged(EventArgs e)
		{
			// In case of text change we receive OnSelectionChanged() before OnTextChanged() but
			// we want to handle only cursor position changes here and we leave text change
			// handling for OnTextChanged().
			if (m_PrevText != null && m_PrevText.Length == TextLength)
			{
				m_PrevSelStart = SelectionStart;
				m_PrevSelLength = SelectionLength;
			}
			base.OnSelectionChanged(e);
		}

		protected override void OnTextChanged(EventArgs e)
		{
			if (!m_UndoingOrRedoing)
				DetectAndSaveChange();
			base.OnTextChanged(e);
			while (base.CanUndo)
				base.ClearUndo();
		}

		public override string Text
		{
			get
			{
				return base.Text;
			}
			set
			{
				base.Text = value;
				// TODO: we could also put this change into the undo buffer...
				//DetectAndSaveChange();
				ClearUndo();
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct GETTEXTLENGTHEX
		{
			public uint flags;
			public uint codepage;
		}

		const int EM_GETTEXTLENGTHEX = WM_USER + 95;

		const uint GTL_DEFAULT = 0;		// Do default (return # of chars)		
		const uint GTL_USECRLF = 1;		// Compute answer using CRLFs for paragraphs
		const uint GTL_PRECISE = 2;		// Compute a precise answer					
		const uint GTL_CLOSE = 4;		// Fast computation of a "close" answer		
		const uint GTL_NUMCHARS	= 8;	// Return number of characters			
		const uint GTL_NUMBYTES = 16;	// Return number of _bytes_				


		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, ref GETTEXTLENGTHEX wParam, IntPtr lParam);

		public override int TextLength
		{
			get
			{
				GETTEXTLENGTHEX gtle;
				gtle.flags = GTL_DEFAULT | GTL_PRECISE | GTL_NUMCHARS;
				gtle.codepage = 1200; // unicode
				return (int)SendMessage(Handle, EM_GETTEXTLENGTHEX, ref gtle, IntPtr.Zero);
			}
		}

		public struct ColorChangeInfo
		{
			public int text_index;
			// A fore_color or back_color with value==-1 doesn't change the current color.
			// You shouldn't specify -1 for both fore_color_index and back_color_index.
			public int fore_color_index;
			public int back_color_index;

			public ColorChangeInfo(int _text_index, int _fore_color_index, int _back_color_index)
			{
				text_index = _text_index;
				fore_color_index = _fore_color_index;
				back_color_index = _back_color_index;
			}
		}


		public struct ColorTable
		{
			public string rtf_color_table;
			public List<string> fore_color_escapes;
			public List<string> back_color_escapes;

			public void Init(List<Color> color_table)
			{
				Debug.Assert(color_table.Count != 0);
				StringBuilder sb = new StringBuilder();
				fore_color_escapes = new List<string>();
				back_color_escapes = new List<string>();
				sb.Append(@"{\colortbl;");
				int next_color_idx = 1;
				for (int i = 0, e = color_table.Count; i < e; ++i)
				{
					Color c = color_table[i];
					if (c == Color.Empty)
					{
						fore_color_escapes.Add(@"\cf0");
						back_color_escapes.Add(@"\highlight0");
					}
					else
					{
						fore_color_escapes.Add(string.Format(@"\cf{0}", next_color_idx));
						back_color_escapes.Add(string.Format(@"\highlight{0}", next_color_idx));
						++next_color_idx;
						sb.Append(string.Format(@"\red{0}\green{1}\blue{2};", c.R, c.G, c.B));
					}
				}
				sb.Append('}');
				rtf_color_table = sb.ToString();
			}
		}

		static Regex regex = new Regex(@"\\deftab\d+");

		static bool SetDefTab(ref string rtf, int def_tab)
		{
			Match m = regex.Match(rtf);
			if (m.Success)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append(rtf, 0, m.Index);
				sb.AppendFormat(@"\deftab{0}", def_tab);
				int idx = m.Index + m.Length;
				sb.Append(rtf, idx, rtf.Length - idx);
				rtf = sb.ToString();
				return true;
			}
			else
			{
				string rtf_str = @"\rtf1";
				int idx = rtf.IndexOf(rtf_str);
				if (idx < 0)
					return false;
				idx += rtf_str.Length;
				StringBuilder sb = new StringBuilder();
				sb.Append(rtf, 0, idx);
				sb.AppendFormat(@"\deftab{0}", def_tab);
				sb.Append(rtf, idx, rtf.Length - idx);
				rtf = sb.ToString();
				return true;
			}
		}


		void SetDefaultTabSize(int tabsize_in_twips)
		{
			if (!IsHandleCreated || tabsize_in_twips <= 0)
				return;
			bool orig_colorized = m_Colorized;
			try
			{
				if (!orig_colorized)
					Colorized = true;
				string rtf = Rtf;
				if (SetDefTab(ref rtf, tabsize_in_twips))
					Rtf = rtf;
			}
			finally
			{
				if (orig_colorized != m_Colorized)
					Colorized = orig_colorized;
			}
		}

		int GetCharWidthInTwips()
		{
			using (RichTextBox rtb = new RichTextBox())
			{
				rtb.Font = Font;
				rtb.WordWrap = false;
				rtb.Text = "XX";

				Point p0 = rtb.GetPositionFromCharIndex(0);
				Point p1 = rtb.GetPositionFromCharIndex(1);
				using (Graphics g = rtb.CreateGraphics())
					return (p1.X - p0.X) * 72 * 20 / Convert.ToInt32(g.DpiX);
			}
		}


		// In order to maintain correct tabsize you have to call this after every font/fontsize/device_context_dpi change.
		public void SetTabStopChars(int tab_size_in_chars)
		{
			Debug.Assert(tab_size_in_chars > 0);
			if (tab_size_in_chars <= 0)
				return;
			if (!IsHandleCreated)
				CreateHandle();
			PausePainting();
			SaveSelectionAndScrollPos();
			try
			{
				int char_width_twips = GetCharWidthInTwips();
				SetDefaultTabSize(tab_size_in_chars * char_width_twips);
			}
			finally
			{
				RestoreSelectionAndScrollPos();
				ResumePainting();
			}
		}

		// The color info of last item of color_changes isn't used, the last item is there only to provide the end iterator of the affected text.
		public void Colorize(List<ColorChangeInfo> color_changes, ref ColorTable color_table)
		{
			if (!m_Colorized)
				return;
			if (color_changes.Count < 2)
				return;

			PausePainting();
			SaveSelectionAndScrollPos();
			try
			{
				int start = color_changes[0].text_index;
				int end = color_changes[color_changes.Count - 1].text_index;
				int length = end - start;
				string text = GetTextRange(start, end);
				RTFBuilder rtf_builder = new RTFBuilder(Font, text, -start, color_changes, ref color_table);
				Select(start, length);
				SelectedRtf = rtf_builder.RTF;
			}
			finally
			{
				RestoreSelectionAndScrollPos();
				ResumePainting();
			}
		}

		public void Colorize(int start, int end, string text, List<ColorChangeInfo> color_changes, ref ColorTable color_table)
		{
			Debug.Assert(text.Length == end - start);
			if (!m_Colorized)
				return;
			if (color_changes.Count < 2)
				return;

			PausePainting();
			SaveSelectionAndScrollPos();
			try
			{
				int length = end - start;
				RTFBuilder rtf_builder = new RTFBuilder(Font, text, 0, color_changes, ref color_table);
				Select(start, length);
				SelectedRtf = rtf_builder.RTF;
			}
			finally
			{
				RestoreSelectionAndScrollPos();
				ResumePainting();
			}
		}

		// Replacing a region of text with rtf if seems to be much faster than doing the same by changing
		// the selection and setting the selection color multiple times. The colorization of one of my
		// evil test cases took 20 seconds with the selchange+setselcolor method while the rtf took only
		// for 3 seconds to do the same job. Since then I've changed to richedit5 from richedit2 that
		// further improved performance.
		class RTFBuilder
		{

			string m_RTF;

			public string RTF
			{
				get
				{
					return m_RTF;
				}
			}

			// color_changes must contain a series of strictly increasing TextIndexes.
			// color_table can use Color.Empty to specify the default rtf color.
			// text_idx_delta offsets the text_index of items in the color_changes array.
			public RTFBuilder(Font font, string text, int text_idx_delta, List<ColorChangeInfo> color_changes, ref ColorTable color_table)
			{
				StringBuilder rtf = new StringBuilder();
				string font_name = font.Name;
				int font_size_in_half_points = (int)(font.SizeInPoints * 2.0f + 0.5f);
				rtf.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 " + font_name + ";}}");
				//rtf.Append(@"{\rtf1\ansi\deff0");
				rtf.Append(color_table.rtf_color_table);
				rtf.Append(@"\viewkind4\uc0\f0\fs" + font_size_in_half_points.ToString());
				//rtf.Append(@"\viewkind4\uc0");
				if (color_changes.Count == 0 || color_changes[0].text_index + text_idx_delta > 0)
				{
					rtf.Append(@"\cf0\highlight0");
				}
				else
				{
					if (color_changes[0].fore_color_index < 0)
						rtf.Append(@"\cf0");
					if (color_changes[0].back_color_index < 0)
						rtf.Append(@"\highlight0");
				}

				ProcessText(text, text_idx_delta, color_changes, ref color_table, rtf);
				rtf.Append('}');
				m_RTF = rtf.ToString();
			}

			void ProcessText(string text, int text_idx_delta, List<ColorChangeInfo> colore_changes, ref ColorTable color_table, StringBuilder rtf)
			{
				int text_idx = -text_idx_delta;
				int text_len = text.Length - text_idx_delta;
#if DEBUG
				int last_info_text_idx = -text_idx_delta - 1;
#endif
				int prev_color = -1;
				int prev_back_color = -1;
				for (int i = 0, e = colore_changes.Count; i < e; ++i)
				{
					ColorChangeInfo info = colore_changes[i];
#if DEBUG
					Debug.Assert(i == e - 1 || info.fore_color_index >= 0 || info.back_color_index >= 0);
					Debug.Assert(info.text_index > last_info_text_idx);
					last_info_text_idx = info.text_index;
#endif
					if (info.text_index >= text_len)
						break;
					if ((info.fore_color_index < 0 || info.fore_color_index == prev_color) &&
						(info.back_color_index < 0 || info.back_color_index == prev_back_color))
						continue;

					AppendEscapedText(text, text_idx + text_idx_delta, info.text_index + text_idx_delta, rtf);
					text_idx = info.text_index;
					if (info.fore_color_index >= 0 && info.fore_color_index != prev_color)
					{
						rtf.Append(color_table.fore_color_escapes[info.fore_color_index]);
						prev_color = info.fore_color_index;
					}
					if (info.back_color_index >= 0 && info.back_color_index != prev_back_color)
					{
						rtf.Append(color_table.back_color_escapes[info.back_color_index]);
						prev_back_color = info.back_color_index;
					}
				}
				AppendEscapedText(text, text_idx + text_idx_delta, text_len + text_idx_delta, rtf);
			}

			void AppendEscapedText(string text, int start_idx, int end_idx, StringBuilder rtf)
			{
				int idx = start_idx;
				bool need_space_after_prev_escape = true;
				bool need_space_after_escape = false;
				for (int i = start_idx; i < end_idx; ++i)
				{
					string escape = null;
					char c = text[i];
					switch (c)
					{
						case '\\':
							escape = @"\\";
							need_space_after_escape = false;
							break;
						case '{':
							escape = @"\{";
							need_space_after_escape = false;
							break;
						case '}':
							escape = @"\}";
							need_space_after_escape = false;
							break;
						case '\r':
						case '\n':
							escape = @"\par";
							need_space_after_escape = true;
							break;
						default:
							if (c < 32 || c > 126)
							{
								escape = string.Format(@"\u{0}", Convert.ToInt32(c));
								need_space_after_escape = true;
							}
							break;
					}
					if (escape != null)
					{
						if (idx < i)
						{
							if (need_space_after_prev_escape)
								rtf.Append(' ');
							rtf.Append(text, idx, i - idx);
						}
						idx = i + 1;
						rtf.Append(escape);
						need_space_after_prev_escape = need_space_after_escape;
					}
				}

				if (idx < end_idx)
				{
					if (need_space_after_prev_escape)
						rtf.Append(' ');
					rtf.Append(text, idx, end_idx - idx);
				}
			}
		}


		[StructLayout(LayoutKind.Sequential)]
		public struct POINT
		{
			public int X;
			public int Y;

			public POINT(int x, int y)
			{
				this.X = x;
				this.Y = y;
			}

			public Point AsPoint()
			{
				return new Point(X, Y);
			}
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, ref POINT lParam);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, out POINT wParam, IntPtr lParam);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, ref int lParam);
		static IntPtr SendMessage(IntPtr hWnd, int wMsg)
		{
			return SendMessage(hWnd, wMsg, IntPtr.Zero, IntPtr.Zero);
		}

		const int WM_USER = 0x400;
		const int WM_SETREDRAW = 0x000B;
	
		const int EM_GETEVENTMASK = WM_USER + 59;
		const int EM_SETEVENTMASK = WM_USER + 69;
		const int EM_GETSCROLLPOS = WM_USER + 221;
		const int EM_SETSCROLLPOS = WM_USER + 222;

		const int EM_SETTEXTMODE = WM_USER + 89;
		const int EM_GETTEXTMODE = WM_USER + 90;

		// EM_SETTEXTMODE flags
		const int TM_PLAINTEXT = 1;
		const int TM_RICHTEXT = 2;			// Default behavior 
		const int TM_SINGLELEVELUNDO = 4;
		const int TM_MULTILEVELUNDO = 8;	// Default behavior 
		const int TM_SINGLECODEPAGE = 16;
		const int TM_MULTICODEPAGE = 32;	// Default behavior 


		int m_SaveSelAndScrollCounter;
		POINT m_SavedScrollPoint;
		int m_SavedSelectionStart;
		int m_SavedSelectionLength;

		protected void SaveSelectionAndScrollPos()
		{
			Debug.Assert(m_SaveSelAndScrollCounter >= 0);
			++m_SaveSelAndScrollCounter;
			if (m_SaveSelAndScrollCounter != 1)
				return;
			m_SavedSelectionStart = SelectionStart;
			m_SavedSelectionLength = SelectionLength;
			SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref m_SavedScrollPoint);
		}

		protected void RestoreSelectionAndScrollPos()
		{
			Debug.Assert(m_SaveSelAndScrollCounter > 0);
			--m_SaveSelAndScrollCounter;
			if (m_SaveSelAndScrollCounter != 0)
				return;
			Select(m_SavedSelectionStart, m_SavedSelectionLength);
			// FIXME: Unfortunately EM_SETSCROLLPOS starts misbehaving when the total y size of the document exceeds
			// 64k pixels. I haven't found a good solution to this problem. Get/SetScrollInfo() didn't work either.
			SendMessage(Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref m_SavedScrollPoint);
		}

		int m_PausePaintingCounter;
		IntPtr m_SavedEventMask;

		protected void PausePainting()
		{
			Debug.Assert(m_PausePaintingCounter >= 0);
			++m_PausePaintingCounter;
			if (m_PausePaintingCounter != 1)
				return;
			m_SavedEventMask = SendMessage(Handle, EM_SETEVENTMASK, IntPtr.Zero, IntPtr.Zero);
			SendMessage(Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
		}

		protected void ResumePainting()
		{
			Debug.Assert(m_PausePaintingCounter > 0);
			--m_PausePaintingCounter;
			if (m_PausePaintingCounter != 0)
				return;
			SendMessage(Handle, EM_SETEVENTMASK, IntPtr.Zero, m_SavedEventMask);
			SendMessage(Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
			Invalidate();
		}


		// HACK: The below functions may seem redundant next to the existing TextBox methods but the Lines property and
		// some textbox methods misbehave when lines are wrapped in the edit control. Even if you set the WordWrap property
		// to false lines still seem to get wrapped at around 3500 characters...

		const int EM_GETLINECOUNT = 0x00BA;

		public int LineCount
		{
			get
			{
				return (int)SendMessage(Handle, EM_GETLINECOUNT);
			}
		}

		const int EM_GETTEXTRANGE = WM_USER + 75;

		[StructLayout(LayoutKind.Sequential)]
		struct CharRange
		{
			public int min;
			public int max;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		struct TextRange
		{
			public CharRange charRange;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string text;
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		extern static IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref TextRange lParam);

		public string GetTextRange(int begin, int end)
		{
			TextRange textRange = new TextRange();
			textRange.charRange.min = begin;
			textRange.charRange.max = end;
			textRange.text = new string('\0', end - begin);
			int length = (int)SendMessage(Handle, EM_GETTEXTRANGE, IntPtr.Zero, ref textRange);
			Debug.Assert(length == end - begin);
			return textRange.text.Replace('\r', '\n');
		}

		const int EM_GETLINE = 0xc4;
		const int EM_LINELENGTH = 0xc1;
		const int EM_LINEINDEX = 0xbb;
		const int EM_LINEFROMCHAR = 0xc9;
		const int WM_GETTEXTLENGTH = 0x000E;
		const int EM_GETFIRSTVISIBLELINE = 0x00CE;
		const int EM_POSFROMCHAR = 0x00D6;

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		extern static IntPtr SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, StringBuilder lParam);

		public int GetLineStartCharIndexFromLineNumber(int line_number)
		{
			return (int)SendMessage(Handle, EM_LINEINDEX, (IntPtr)line_number, IntPtr.Zero);
		}

		public int GetLineNumberFromCharIndex(int char_index)
		{
			return (int)SendMessage(Handle, EM_LINEFROMCHAR, (IntPtr)char_index, IntPtr.Zero);
		}

		public int GetLineLengthFromCharIndex(int char_index)
		{
			return (int)SendMessage(Handle, EM_LINELENGTH, (IntPtr)char_index, IntPtr.Zero);
		}

		public string GetLineFromCharIndex(int char_index, out int line_number)
		{
			line_number = GetLineNumberFromCharIndex(char_index);
			int line_length = GetLineLengthFromCharIndex(char_index);
			if (line_length <= 0)
				return string.Empty;

			StringBuilder sb = new StringBuilder(line_length);
			sb.Append((char)line_length);
			int len = (int)SendMessage(Handle, EM_GETLINE, (IntPtr)line_number, sb);
			sb.Length = len;
			return sb.ToString();
		}

		public int GetFirstVisibleLine()
		{
			Debug.Assert(IsHandleCreated);
			return (int)SendMessage(Handle, EM_GETFIRSTVISIBLELINE);
		}

		public new Point GetPositionFromCharIndex(int char_index)
		{
			Debug.Assert(IsHandleCreated);
			if (m_MsftLoader.IsMsftEditDllAvailable())
			{
				// riched 3 syntax
				POINT pos;
				SendMessage(Handle, EM_POSFROMCHAR, out pos, (IntPtr)char_index);
				return new Point(pos.X, pos.Y);
			}
			else
			{
				// riched 2 sytnax
				Debug.Assert(char_index < 0x10000);
				if (char_index >= 0x10000)
					return new Point(0, 0);
				int coord = (int)SendMessage(Handle, EM_POSFROMCHAR, (IntPtr)char_index, IntPtr.Zero);
				return new Point((short)coord, (short)(coord >> 16));
			}
		}

		bool m_SmoothScroll;

		public bool SmoothScroll
		{
			get
			{
				return m_SmoothScroll;
			}
			set
			{
				m_SmoothScroll = value;
			}
		}

		bool m_Colorized = true;

		[DefaultValue(true)]
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Colorized
		{
			get
			{
				return m_Colorized;
			}
			set
			{
				m_Colorized = value;
				if (IsHandleCreated)
				{
					IntPtr mode = value ? (IntPtr)TM_RICHTEXT : (IntPtr)TM_PLAINTEXT;
					SendMessage(Handle, EM_SETTEXTMODE, mode, IntPtr.Zero);
				}
			}
		}

		// returns zero based index
		public static int GetColumnIndexFromTabbedText(string line_text, int begin, int end, int tab_size)
		{
			int column = 0;
			for (int i=begin; i<end; ++i)
			{
				++column;
				if (line_text[i] == '\t')
				{
					column = column + tab_size - 1;
					column -= column % tab_size;
				}
			}
			return column;
		}

		// Unless SelectedText="Something" this method respects the max text length and
		// returns false if the insertion could not be performed.
		public bool SetSelectedText(string text)
		{
			if (TextLength - SelectionLength + text.Length > MaxLength)
				return false;
			SelectedText = text;
			return true;
		}
	}
}
