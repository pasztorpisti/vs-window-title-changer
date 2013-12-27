using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using VSWindowTitleChanger.ExpressionEvaluator.Tokenizer;

namespace VSWindowTitleChanger
{
	class SyntaxHighlighter : IDisposable
	{
		ExpressionTextBox m_TextBox;

		public SyntaxHighlighter(ExpressionTextBox textbox)
		{
			m_TextBox = textbox;

			m_TextBox.AfterUndo += m_TextBox_AfterUndo;
			m_TextBox.AfterRedo += m_TextBox_AfterRedo;
			m_TextBox.UndoEntryAdded += m_TextBox_UndoEntryAdded;
			
			m_ColorInfo = new ColorInfo(m_TextBox.BackColor);
		}

		public virtual void Dispose()
		{
			if (m_TextBox != null)
			{
				m_TextBox.AfterUndo -= m_TextBox_AfterUndo;
				m_TextBox.AfterRedo -= m_TextBox_AfterRedo;
				m_TextBox.UndoEntryAdded -= m_TextBox_UndoEntryAdded;
				m_TextBox = null;
			}
		}

		public void HighlightAll()
		{
			HighlightRange(0, m_TextBox.TextLength);
		}

		void m_TextBox_UndoEntryAdded(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			HighlightRange(undo_entry.Pos, undo_entry.PastedText.Length);
		}

		void m_TextBox_AfterRedo(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			HighlightRange(undo_entry.Pos, undo_entry.PastedText.Length);
		}

		void m_TextBox_AfterUndo(ColorizedPlainTextBox.UndoEntry undo_entry)
		{
			HighlightRange(undo_entry.Pos, undo_entry.CutText.Length);
		}

		void HighlightRange(int start_pos, int length)
		{
			int end_pos = start_pos + length;

			int start_line = m_TextBox.GetLineFromCharIndex(start_pos);

			int end_line = m_TextBox.GetLineFromCharIndex(end_pos);
			int end_line_first_char_idx = m_TextBox.GetLineStartCharIndexFromLineNumber(end_line);
			if (end_pos == end_line_first_char_idx)
				--end_line;
			if (start_line > end_line)
				end_line = start_line;
			if (end_pos == end_line_first_char_idx)
				end_line_first_char_idx = m_TextBox.GetLineStartCharIndexFromLineNumber(end_line);
			int end_line_length = m_TextBox.GetLineLengthFromCharIndex(end_line_first_char_idx);

			start_pos = m_TextBox.GetLineStartCharIndexFromLineNumber(start_line);
			end_pos = end_line_first_char_idx + end_line_length;

			string text = m_TextBox.GetTextRange(start_pos, end_pos);

			List<ExpressionTextBox.ColorChangeInfo> highlight_info;
			CreateSyntaxHighlightInfo(text, out highlight_info);
			m_TextBox.Colorize(start_pos, end_pos, text, highlight_info, ref m_ColorInfo.color_table);
		}

		class ColorInfo
		{
			public ExpressionTextBox.ColorTable color_table;

			public int color_default;
			public int color_keyword;
			public int color_operator;
			public int color_string_literal;
			public int color_const_or_variable;
			public int color_brackets;
			public int color_comment;
			public int color_bad_token;
			public int back_color_normal;
			public int back_color_error;

			public ColorInfo(Color normal_edit_back_color)
			{
				List<Color> color_list = new List<Color>();

				color_default = color_list.Count;
				color_list.Add(Color.Black);
				color_keyword = color_list.Count;
				color_list.Add(Color.Blue);
				color_operator = color_list.Count;
				color_list.Add(Color.Black);
				color_string_literal = color_list.Count;
				color_list.Add(Color.Purple);
				color_const_or_variable = color_list.Count;
				color_list.Add(Color.FromArgb(96, 96, 96));
				color_brackets = color_list.Count;
				color_list.Add(Color.Black);
				color_comment = color_list.Count;
				color_list.Add(Color.Green);
				color_bad_token = color_list.Count;
				color_list.Add(Color.White);
				back_color_normal = color_list.Count;
				color_list.Add(normal_edit_back_color);
				back_color_error = color_list.Count;
				color_list.Add(Color.Red);

				color_table.Init(color_list);
			}
		}

		ColorInfo m_ColorInfo;


		void CreateSyntaxHighlightInfo(string expression_str, out List<ExpressionTextBox.ColorChangeInfo> color_changes)
		{
			color_changes = new List<ExpressionTextBox.ColorChangeInfo>();
			Tokenizer tokenizer = new Tokenizer(expression_str, true);
			ExpressionTextBox.ColorChangeInfo prev_color = new ExpressionTextBox.ColorChangeInfo(0, -1, -1);
			ExpressionTextBox.ColorChangeInfo color;

			for (; ; )
			{
				int token_length;
				try
				{
					Token token = tokenizer.GetNextToken();
					token_length = token.length;
					color.text_index = token.pos;
					color.back_color_index = m_ColorInfo.back_color_normal;

					switch (token.type)
					{
						case TokenType.OpNot:
						case TokenType.OpUpcase:
						case TokenType.OpLocase:
						case TokenType.OpLcap:
						case TokenType.OpContains:
						case TokenType.OpStartsWith:
						case TokenType.OpEndsWith:
						case TokenType.OpConcat:
						case TokenType.OpEquals:
						case TokenType.OpNotEquals:
						case TokenType.OpRegexMatch:
						case TokenType.OpRegexNotMatch:
						case TokenType.OpAnd:
						case TokenType.OpXor:
						case TokenType.OpOr:
						case TokenType.Ternary:
							color.fore_color_index = m_ColorInfo.color_operator;
							break;
						case TokenType.String:
							color.fore_color_index = m_ColorInfo.color_string_literal;
							break;
						case TokenType.Variable:
							color.fore_color_index = m_ColorInfo.color_const_or_variable;
							break;
						case TokenType.If:
						case TokenType.Else:
							color.fore_color_index = m_ColorInfo.color_keyword;
							break;
						case TokenType.OpenBrace:
						case TokenType.CloseBrace:
						case TokenType.OpenBracket:
						case TokenType.CloseBracket:
							color.fore_color_index = m_ColorInfo.color_brackets;
							break;
						case TokenType.SingleLineComment:
							color.fore_color_index = m_ColorInfo.color_comment;
							break;
						default:
							color.fore_color_index = m_ColorInfo.color_default;
							break;
						case TokenType.EOF:
							color.fore_color_index = -1;
							break;
					}
				}
				catch (TokenizerException ex)
				{
					int pos = ex.ErrorPos;
					token_length = 1;
					color.text_index = pos;
					color.fore_color_index = m_ColorInfo.color_bad_token;
					color.back_color_index = m_ColorInfo.back_color_error;
					tokenizer.ResetAndGoToPosition(pos + 1);
				}

				// color of space between tokens
				if (prev_color.text_index < color.text_index)
				{
					if (prev_color.back_color_index != m_ColorInfo.back_color_normal)
					{
						color_changes.Add(new ExpressionTextBox.ColorChangeInfo(prev_color.text_index, -1, m_ColorInfo.back_color_normal));
						prev_color.back_color_index = m_ColorInfo.back_color_normal;
					}
				}

				if (prev_color.fore_color_index != color.fore_color_index || prev_color.back_color_index != color.back_color_index)
				{
					ExpressionTextBox.ColorChangeInfo change;
					change.text_index = color.text_index;
					change.fore_color_index = prev_color.fore_color_index != color.fore_color_index ? color.fore_color_index : -1;
					change.back_color_index = prev_color.back_color_index != color.back_color_index ? color.back_color_index : -1;
					color_changes.Add(change);
				}

				// Check for eof
				if (color.fore_color_index < 0)
					break;

				prev_color.text_index = color.text_index + token_length;
				prev_color.fore_color_index = color.fore_color_index;
				prev_color.back_color_index = color.back_color_index;
			}
		}

	}
}
