using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;

namespace VSWindowTitleChanger
{
	// An editor that assits with some smart features like auto-indenting.
	[ToolboxBitmap(typeof(RichTextBox))]
	class ExpressionTextBox : ColorizedPlainTextBox
	{
		public const int TAB_SIZE = 4;

		int m_LastZeroLengthSelStart;

		SyntaxHighlighter m_SyntaxHighlighter;

		public ExpressionTextBox()
		{
			m_SyntaxHighlighter = new SyntaxHighlighter(this);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (m_SyntaxHighlighter != null)
				{
					m_SyntaxHighlighter.Dispose();
					m_SyntaxHighlighter = null;
				}
			}
			base.Dispose(disposing);
		}

		protected override void OnSelectionChanged(EventArgs e)
		{
			if (SelectionLength == 0)
				m_LastZeroLengthSelStart = SelectionStart;
			base.OnSelectionChanged(e);
		}

		bool HandleBeepingNavigationKeyStrokes(KeyEventArgs e)
		{
			int char_pos;
			if (m_LastZeroLengthSelStart == SelectionStart)
				char_pos = SelectionStart + SelectionLength;
			else
				char_pos = SelectionStart;

			switch (e.KeyCode)
			{
				case Keys.Home:
					if (0 != (e.Modifiers & Keys.Control) && 0 == GetLineNumberFromCharIndex(char_pos))
						return true;
					return false;
				case Keys.Up:
				case Keys.PageUp:
					if (0 == GetLineNumberFromCharIndex(char_pos))
						return true;
					return false;
				case Keys.Down:
				case Keys.PageDown:
					if (char_pos >= TextLength)
						return true;
					if (GetLineNumberFromCharIndex(char_pos) >= LineCount - 1)
						return true;
					return false;
				case Keys.End:
					if (0 == (e.Modifiers & Keys.Control))
					{
						int line = GetLineNumberFromCharIndex(char_pos);
						if (line < 0)
							return true;
						int first_char_idx = GetFirstCharIndexFromLine(line);
						if (char_pos - first_char_idx >= Lines[line].Length)
							return true;
						return false;
					}
					else
					{
						if (char_pos >= TextLength)
							return true;
						return false;
					}
				case Keys.Back:
				case Keys.Left:
					if (char_pos == 0)
						return true;
					return false;
				case Keys.Right:
					if (char_pos >= TextLength)
						return true;
					return false;
				default:
					return false;
			}
		}

		// The control tries to be smart and auto-detects this property.
		bool m_UseTabsToIndent = true;

		int TabForward(int indent_column_index)
		{
			indent_column_index += TAB_SIZE;
			indent_column_index -= indent_column_index % TAB_SIZE;
			return indent_column_index;
		}

		int TabBackward(int indent_column_index)
		{
			indent_column_index += TAB_SIZE - 1;
			indent_column_index -= indent_column_index % TAB_SIZE;
			indent_column_index -= TAB_SIZE;
			return Math.Max(0, indent_column_index);
		}

		int GetLineIndentCharCount(string text, int begin, int end)
		{
			int i = begin;
			for (; i<end; ++i)
			{
				char c = text[i];
				if (c != '\t' && c != ' ')
					break;
			}
			return i - begin;
		}

		struct IndentInfo
		{
			public int line_start_char_index;
			public string line;
			public int indent_char_count;
			public int indent_column_index;

			public bool IsValid()
			{
				return line != null;
			}
			public bool IsSpaceOnlyLine()
			{
				return indent_char_count == line.Length;
			}
		}

		void GetLineIndentInfo(int line_number, out IndentInfo info)
		{
			info.line_start_char_index = GetLineStartCharIndexFromLineNumber(line_number);
			int ln;
			info.line = GetLineFromCharIndex(info.line_start_char_index, out ln);
			Debug.Assert(line_number == ln);
			info.indent_char_count = GetLineIndentCharCount(info.line, 0, info.line.Length);
			info.indent_column_index = GetColumnIndexFromTabbedText(info.line, 0, info.indent_char_count, TAB_SIZE);
			if (info.indent_char_count > 0)
				m_UseTabsToIndent = info.line[0] == '\t';
		}

		int AUTO_INDENT_MAX_PREV_LINES_TO_ANALYZE = 50;

		void GetAutoIndentForLine(int line_number, out IndentInfo info)
		{
			info = new IndentInfo();
			for (int i = line_number - 1, e = Math.Max(0, line_number - 1 - AUTO_INDENT_MAX_PREV_LINES_TO_ANALYZE); i > e; --i)
			{
				IndentInfo indent_info;
				GetLineIndentInfo(i, out indent_info);
				if (indent_info.IsSpaceOnlyLine())
				{
					if (i == line_number - 1)
						info = indent_info;
				}
				else
				{
					info = indent_info;
					break;
				}
			}
		}

		string GetIndentString(int indent_column_index)
		{
			if (!m_UseTabsToIndent)
				return new string(' ', indent_column_index);
			return new string('\t', indent_column_index / TAB_SIZE) + new string(' ', indent_column_index % TAB_SIZE);
		}

		int AutoIndentLine(int char_index)
		{
			int line_number = GetLineNumberFromCharIndex(char_index);

			IndentInfo auto_indent_info;
			GetAutoIndentForLine(line_number, out auto_indent_info);
			if (!auto_indent_info.IsValid())
				return -1;

			IndentInfo indent_info;
			GetLineIndentInfo(line_number, out indent_info);

			int target_indent_column_index = auto_indent_info.indent_column_index;
			bool add_extra_tab = auto_indent_info.indent_char_count < auto_indent_info.line.Length &&
				auto_indent_info.line[auto_indent_info.line.Length-1] == '{';

			bool remove_tab = false;

			bool indent_line_starts_with_close_brace = indent_info.line.Length > indent_info.indent_char_count && indent_info.line[indent_info.indent_char_count] == '}';
			if (add_extra_tab)
			{
				if (indent_line_starts_with_close_brace)
					add_extra_tab = false;
			}
			else if (indent_line_starts_with_close_brace)
			{
				remove_tab = true;
			}

			if (add_extra_tab)
				target_indent_column_index = TabForward(target_indent_column_index);
			else if (remove_tab)
				target_indent_column_index = TabBackward(target_indent_column_index);
	
			if (indent_info.indent_column_index > target_indent_column_index)
				return - 1;

			Select(indent_info.line_start_char_index, indent_info.indent_char_count);
			string indent_string = GetIndentString(target_indent_column_index);
			SetSelectedText(indent_string);
			return indent_string.Length;
		}

		void Smart_IndentedNewLine(KeyEventArgs e)
		{
			PausePainting();
			try
			{
				e.Handled = true;
				if (SelectionLength > 0)
				{
					if (!SetSelectedText(""))
						return;
				}
				int char_index = SelectionStart;
				int line_number = GetLineNumberFromCharIndex(char_index);
				IndentInfo indent_info;
				GetLineIndentInfo(line_number, out indent_info);
				int caret_line_index = char_index - indent_info.line_start_char_index;
				if (caret_line_index > 0 && indent_info.line[caret_line_index - 1] == '{')
				{
					int indent_column_index = TabForward(indent_info.indent_column_index);
					string indent0 = GetIndentString(indent_column_index);
					if (caret_line_index < indent_info.line.Length && indent_info.line[caret_line_index] == '}')
					{
						string indent1 = GetIndentString(indent_info.indent_column_index);
						if (SetSelectedText("\n" + indent0 + "\n" + indent1))
							SelectionStart = SelectionStart - indent1.Length - 1;
					}
					else
					{
						SetSelectedText("\n" + indent0);
					}
				}
				else
				{
					if (SetSelectedText("\n"))
						AutoIndentLine(char_index + 1);
				}
			}
			finally
			{
				ResumePainting();
				DetectAndSaveChange();
			}
		}

		void Smart_Tab_Singleline(bool forward)
		{
			int char_index = SelectionStart;
			int line_number = GetLineNumberFromCharIndex(char_index);
			IndentInfo info;
			GetLineIndentInfo(line_number, out info);
			if (forward)
			{
				if (m_UseTabsToIndent)
				{
					if (!SetSelectedText("\t"))
						return;
				}
				else
				{
					int indent_column_index = GetColumnIndexFromTabbedText(info.line, 0, SelectionStart-info.line_start_char_index, TAB_SIZE);
					int new_indent_column_index = TabForward(indent_column_index);
					if (!SetSelectedText(new string(' ', new_indent_column_index-indent_column_index)))
						return;
				}
				if (info.indent_char_count < char_index - info.line_start_char_index)
					return;
				AutoIndentLine(char_index);
			}
			else
			{
				int new_indent_column_index = TabBackward(info.indent_column_index);
				Select(info.line_start_char_index, info.indent_char_count);
				SetSelectedText(GetIndentString(new_indent_column_index));
			}
		}

		void Smart_Tab_Multiline(bool forward)
		{
			int start_char_index = SelectionStart;
			int end_char_index = start_char_index + SelectionLength;
			int start_line_num = GetLineNumberFromCharIndex(start_char_index);
			int end_line_num = GetLineNumberFromCharIndex(end_char_index);

			if (forward)
			{
				if ((end_line_num - start_line_num + 1) * (m_UseTabsToIndent ? 1 : TAB_SIZE) + TextLength > MaxLength)
					return;
			}

			IndentInfo last_line_info;
			GetLineIndentInfo(end_line_num, out last_line_info);

			int text_start_char_index = GetLineStartCharIndexFromLineNumber(start_line_num);
			int chars_selected_from_last_line = end_char_index - last_line_info.line_start_char_index;
			int text_end_char_index = last_line_info.line_start_char_index;
			if (chars_selected_from_last_line > 0)
				text_end_char_index += last_line_info.indent_char_count + 1;

			string text = GetTextRange(text_start_char_index, text_end_char_index);

			int idx2 = text.Length;
			while (idx2 > 0)
			{
				int idx = text.LastIndexOf('\n', idx2-1);
				if (idx < 0)
				{
					idx = 0;
				}
				else
				{
					++idx;
					if (idx == text.Length)
					{
						idx2 = idx - 1;
						continue;
					}
				}

				int indent_char_count = GetLineIndentCharCount(text, idx, idx2);
				int tabbed_line_idx = idx + indent_char_count;
				int indent_column_index = GetColumnIndexFromTabbedText(text, idx, tabbed_line_idx, TAB_SIZE);
				int new_indent_column_index;
				if (forward)
					new_indent_column_index = TabForward(indent_column_index);
				else
					new_indent_column_index = TabBackward(indent_column_index);

				// When I handled the whole selected multiline text as one big chunk and replaced it with one textchange
				// I had problems when the text contained characters substituted from a guest font. From the character
				// that came from a substitute font file the characters have changed to the guest font even if that wasnt necessary.
				// With some japanese cahracters this totally screwed up the look and the y coordinate of the lines that
				// made the line number y offsets invalid and this incorrect font usage resolved only at the next syntax highlighting.
				Select(text_start_char_index + idx, indent_char_count);
				SelectedText = GetIndentString(new_indent_column_index);

				idx2 = idx - 1;
			}

			if (chars_selected_from_last_line > 0)
			{
				GetLineIndentInfo(end_line_num, out last_line_info);
				Select(text_start_char_index, last_line_info.line_start_char_index - text_start_char_index + last_line_info.indent_char_count + 1);
			}
			else
			{
				end_char_index = GetLineStartCharIndexFromLineNumber(end_line_num);
				Select(text_start_char_index, end_char_index - text_start_char_index);
			}
		}

		void Smart_Tab(KeyEventArgs e)
		{
			e.Handled = true;
			bool forward = 0 == (e.Modifiers & Keys.Shift);
			PausePainting();
			try
			{
				if (SelectionLength == 0)
					Smart_Tab_Singleline(forward);
				else
					Smart_Tab_Multiline(forward);
			}
			finally
			{
				ResumePainting();
				DetectAndSaveChange();
			}
		}

		void Smart_GoToLineBegin(KeyEventArgs e)
		{
			int sel_start = SelectionStart;
			if (sel_start < TextLength)
			{
				int line_number = GetLineFromCharIndex(sel_start);
				if (line_number < 0)
				{
					e.Handled = true;
				}
				else if (line_number < Lines.Length)
				{
					int line_start = GetFirstCharIndexFromLine(line_number);
					if (line_start >= 0)
					{
						string line = Lines[line_number];
						int idx = 0;
						for (int i = 0, ie = line.Length; i < ie; ++i)
						{
							char c = line[i];
							if (c != ' ' && c != '\t')
							{
								idx = i;
								break;
							}
						}
						idx += line_start;
						int new_sel_start = SelectionStart == idx ? line_start : idx;
						if (0 == (e.Modifiers & Keys.Shift))
						{
							Select(new_sel_start, 0);
						}
						else
						{
							int sel_length = Math.Max(0, SelectionLength + SelectionStart - new_sel_start);
							Select(new_sel_start, sel_length);
						}
						e.Handled = true;
					}
				}
			}
		}

		void Smart_CutLineNoSelection(KeyEventArgs e)
		{
			if (SelectionLength != 0)
				return;
			int sel_start = SelectionStart;
			if (sel_start >= 0 && sel_start < TextLength)
			{
				int line_number = GetLineFromCharIndex(sel_start);
				if (line_number >= 0 && line_number < Lines.Length)
				{
					int line_start = GetFirstCharIndexFromLine(line_number);
					if (line_start >= 0)
					{
						string line = Lines[line_number];
						Select(line_start, line.Length + 1);
						Cut();
						e.Handled = true;
					}
				}
			}
		}

		void SmartHandleCharPair(KeyPressEventArgs e, char first_char, char second_char)
		{
			int char_index = SelectionStart;
			int line_number;
			string line = GetLineFromCharIndex(char_index, out line_number);
			int line_start_char_index = GetLineStartCharIndexFromLineNumber(line_number);
			int line_idx = char_index - line_start_char_index;
			if (line_idx < line.Length && line[line_idx] == e.KeyChar)
			{
				SelectionStart = SelectionStart + 1;
				e.Handled = true;
			}
			else if (e.KeyChar == first_char)
			{
				bool double_char = line_idx >= line.Length || Char.IsWhiteSpace(line[line_idx]);
				if (double_char)
				{
					// We pause and resume painting because after text modification we change the cursor position too
					// and we want the undo buffer to treat this as a single change. Pausing/resuming eliminates filckering as well.
					PausePainting();
					try
					{
						if (SetSelectedText("" + first_char + second_char))
							SelectionStart = SelectionStart - 1;
						e.Handled = true;
					}
					finally
					{
						ResumePainting();
						DetectAndSaveChange();
					}
				}
			}
			else if (e.KeyChar == '}')
			{
				int indent_char_count = GetLineIndentCharCount(line, 0, line_idx);
				if (indent_char_count >= line_idx)
				{
					PausePainting();
					try
					{
						Select(line_start_char_index, char_index - line_start_char_index + SelectionLength);
						if (SetSelectedText("}"))
						{
							indent_char_count = AutoIndentLine(line_start_char_index);
							if (indent_char_count >= 0)
								Select(line_start_char_index + indent_char_count + 1, 0);
						}
						e.Handled = true;
					}
					finally
					{
						ResumePainting();
						DetectAndSaveChange();
					}
				}
			}
		}

		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			if (e.KeyChar == '\r' || e.KeyChar == '\t')
			{
				e.Handled = true;
			}
			else if (e.KeyChar == '{' || e.KeyChar == '}')
			{
				SmartHandleCharPair(e, '{', '}');
			}
			else if (e.KeyChar == '(' || e.KeyChar == ')')
			{
				SmartHandleCharPair(e, '(', ')');
			}
			else if (e.KeyChar == '"')
			{
				SmartHandleCharPair(e, '"', '"');
			}

			if (!e.Handled)
				base.OnKeyPress(e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (0 == (e.Modifiers & Keys.Shift))
			{
				if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
				{
					if (SelectionLength > 0)
					{
						Select(SelectionStart, 0);
						e.Handled = e.KeyCode == Keys.Left;
						return;
					}
				}
				else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
				{
					if (SelectionLength > 0)
					{
						Select(SelectionStart + SelectionLength, 0);
						e.Handled = e.KeyCode == Keys.Right;
						return;
					}
				}
			}

			if (HandleBeepingNavigationKeyStrokes(e))
			{
				if (0 == (e.Modifiers & Keys.Shift))
					SelectionLength = 0;
				e.Handled = true;
				return;
			}

			if (0 == (e.Modifiers & Keys.Control))
			{
				if (e.KeyCode == Keys.Enter)
					Smart_IndentedNewLine(e);
				else if (e.KeyCode == Keys.Tab)
					Smart_Tab(e);
				else if (e.KeyCode == Keys.Home)
					Smart_GoToLineBegin(e);
				else if (e.KeyCode == Keys.Delete && 0 != (e.Modifiers & Keys.Shift))
					Smart_CutLineNoSelection(e);
			}

			if (!e.Handled)
				base.OnKeyDown(e);
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Enter:
				case Keys.Home:
				case Keys.End:
				case Keys.Tab:
					e.Handled = true;
					break;
				default:
					base.OnKeyUp(e);
					break;
			}
		}

		public void CharIdxToLineAndColumn(int char_idx, out int line, out int column)
		{
			line = GetLineFromCharIndex(char_idx);
			// If the textbox isn't empty and the cursor is at the end of the whole text of
			// the textbox and the last line is empty then GetLineFromCharIndex() returns the
			// index of the previous line and we work around this by using LineCount-1
			// instead that seems to give the correct value.
			if (char_idx > 0 && char_idx >= TextLength)
				line = LineCount - 1;
			int line_first_char_idx = Math.Max(0, GetFirstCharIndexFromLine(line));
			string line_text = GetTextRange(line_first_char_idx, char_idx);
			column = ColorizedPlainTextBox.GetColumnIndexFromTabbedText(line_text, 0, line_text.Length, ExpressionTextBox.TAB_SIZE);
		}

	}
}
