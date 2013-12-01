using System;
using System.Diagnostics;
using System.Text;


namespace VSWindowTitleChanger.ExpressionEvaluator.Tokenizer
{
	enum TokenType
	{
		OpNot,
		OpUpcase,
		OpLocase,
		OpLcap,

		OpContains,
		OpStartsWith,
		OpEndsWith,

		OpConcat,
		OpEquals,
		OpNotEquals,
		OpRegexMatch,
		OpRegexNotMatch,
		OpAnd,
		OpXor,
		OpOr,

		String,
		Variable,

		Ternary,

		If,
		Then,
		Else,

		OpenBlock,
		CloseBlock,
		OpenBracket,
		CloseBracket,

		EOF,
	}

	struct Token
	{
		public TokenType type;
		public string data;
		public int pos;

		public static string TokenTypeToLiteral(TokenType token_type)
		{
			switch (token_type)
			{
				case TokenType.OpNot: return "\"not\"";
				case TokenType.OpUpcase: return "\"upcase\"";
				case TokenType.OpLocase: return "\"locase\"";
				case TokenType.OpLcap: return "\"lcap\"";
				case TokenType.OpContains: return "\"contains\"";
				case TokenType.OpStartsWith: return "\"startswith\"";
				case TokenType.OpEndsWith: return "\"endswith\"";
				case TokenType.OpConcat: return "\"+\"";
				case TokenType.OpEquals: return "\"==\"";
				case TokenType.OpNotEquals: return "\"!=\"";
				case TokenType.OpRegexMatch: return "\"=~\"";
				case TokenType.OpRegexNotMatch: return "\"!~\"";
				case TokenType.OpAnd: return "\"and\"";
				case TokenType.OpXor: return "\"xor\"";
				case TokenType.OpOr: return "\"or\"";
				case TokenType.String: return "<string_literal>";
				case TokenType.Variable: return "<variable>";
				case TokenType.If: return "\"if\"";
				case TokenType.Then: return "\"then\"";
				case TokenType.Else: return "\"else\"";
				case TokenType.OpenBlock: return "\"(\"";
				case TokenType.CloseBlock: return "\")\"";
				case TokenType.OpenBracket: return "\"(\"";
				case TokenType.CloseBracket: return "\")\"";
				case TokenType.Ternary: return "\"?\"";
				case TokenType.EOF: return "<EOF>";
				default:
					Debug.Assert(false, "Unhandled TokenType!");
					return "<Unhandled_TokenType>";
			}
		}
	}

	class TokenizerException : ParserException
	{
		public TokenizerException(string input_text, int error_pos, string error_message)
			: base(input_text, error_pos, error_message)
		{}
	}

	// Indicates a character on the input stream that isn't a valid variable name (not a unicode letter or digit or '_' or '$')
	// and isn't a valid token used by our expression language.
	class InvalidTokenException : TokenizerException
	{
		public InvalidTokenException(string input_text, int error_pos, string error_message)
			: base(input_text, error_pos, error_message)
		{}
	}


	class Tokenizer
	{
		public Tokenizer(string text)
		{
			m_Text = text;
		}

		public string Text { get { return m_Text; } }
		public int Pos { get { return m_Pos; } }

		public Token GetNextToken()
		{
			if (!m_NextTokenAvailable)
				ParseNextToken();
			m_NextTokenAvailable = false;
			return m_NextToken;
		}

		public Token PeekNextToken()
		{
			if (!m_NextTokenAvailable)
				ParseNextToken();
			return m_NextToken;
		}

		public void ConsumeNextToken()
		{
			if (!m_NextTokenAvailable)
				ParseNextToken();
			Debug.Assert(m_NextToken.type != TokenType.EOF);
			m_NextTokenAvailable = false;
		}

		private bool SetNextToken(TokenType type, int start_pos_offset)
		{
			m_NextToken.type = type;
			m_NextToken.data = null;
			m_NextToken.pos = m_Pos + start_pos_offset;
			++m_Pos;
			return true;
		}

		private bool SetNextToken(TokenType type)
		{
			return SetNextToken(type, 0);
		}

		private bool ParseNextToken()
		{
			m_NextTokenAvailable = true;

			SkipSpaces();
			switch (Lookahead())
			{
				case '\0':
					return SetNextToken(TokenType.EOF);
				case '+':
					return SetNextToken(TokenType.OpConcat);
				case '{':
					return SetNextToken(TokenType.OpenBlock);
				case '}':
					return SetNextToken(TokenType.CloseBlock);
				case '(':
					return SetNextToken(TokenType.OpenBracket);
				case ')':
					return SetNextToken(TokenType.CloseBracket);
				case '?':
					return SetNextToken(TokenType.Ternary);
				case '&':
					if (Lookahead(1) == '&')
					{
						++m_Pos;
						return SetNextToken(TokenType.OpAnd, -1);
					}
					return SetNextToken(TokenType.OpAnd);
				case '^':
					return SetNextToken(TokenType.OpXor);
				case '|':
					if (Lookahead(1) == '|')
					{
						++m_Pos;
						return SetNextToken(TokenType.OpOr, -1);
					}
					return SetNextToken(TokenType.OpOr);
				case '=':
					++m_Pos;
					switch (Lookahead())
					{
						case '=':
							return SetNextToken(TokenType.OpEquals, -1);
						case '~':
							return SetNextToken(TokenType.OpRegexMatch, -1);
						default:
							throw new InvalidTokenException(m_Text, m_Pos - 1, "Invalid or incomplete operator: '='");
					}
				case '!':
					switch (Lookahead())
					{
						case '=':
							++m_Pos;
							return SetNextToken(TokenType.OpNotEquals, -1);
						case '~':
							++m_Pos;
							return SetNextToken(TokenType.OpRegexNotMatch, -1);
						default:
							return SetNextToken(TokenType.OpNot);
					}
				case '"':
					return ParseString();
				default:
					return ParseVariableOrOperator();
			}
		}

		private bool SetNextToken(TokenType type, string data, int start_pos)
		{
			m_NextToken.type = type;
			m_NextToken.data = data;
			m_NextToken.pos = start_pos;
			return true;
		}

		private bool ParseString()
		{
			int start_pos = m_Pos;
			++m_Pos;
			StringBuilder sb = new StringBuilder();
			char c = Lookahead();
			while (c != '\0')
			{
				if (c == '"')
				{
					if (Lookahead(1) != '"')
						break;
					++m_Pos;
				}
				sb.Append(c);
				++m_Pos;
				c = Lookahead();
			}

			if (c == '\0')
				throw new TokenizerException(m_Text, start_pos, "Reached the end of the stream while parsing the quoted string.");
			++m_Pos;
			return SetNextToken(TokenType.String, sb.ToString(), start_pos);
		}

		private bool ParseVariableOrOperator()
		{
			if (!IsValidVariableChar(Lookahead()))
				throw new InvalidTokenException(m_Text, m_Pos, "Invalid character in the input stream.");
			int start_pos = m_Pos;
			++m_Pos;
			while (IsValidVariableChar(Lookahead()))
				++m_Pos;
			string variable = m_Text.Substring(start_pos, m_Pos - start_pos);

			switch (variable.ToLower())
			{
				case "not":
					return SetNextToken(TokenType.OpNot, variable, start_pos);
				case "upcase":
					return SetNextToken(TokenType.OpUpcase, variable, start_pos);
				case "locase":
					return SetNextToken(TokenType.OpLocase, variable, start_pos);
				case "lcap":
					return SetNextToken(TokenType.OpLcap, variable, start_pos);
				case "contains":
					return SetNextToken(TokenType.OpContains, variable, start_pos);
				case "startswith":
					return SetNextToken(TokenType.OpStartsWith, variable, start_pos);
				case "endswith":
					return SetNextToken(TokenType.OpEndsWith, variable, start_pos);
				case "and":
					return SetNextToken(TokenType.OpAnd, variable, start_pos);
				case "xor":
					return SetNextToken(TokenType.OpXor, variable, start_pos);
				case "or":
					return SetNextToken(TokenType.OpOr, variable, start_pos);
				case "if":
					return SetNextToken(TokenType.If, variable, start_pos);
				case "then":
					return SetNextToken(TokenType.Then, variable, start_pos);
				case "else":
					return SetNextToken(TokenType.Else, variable, start_pos);
				default:
					return SetNextToken(TokenType.Variable, variable, start_pos);
			}
		}

		private bool IsValidVariableChar(char c)
		{
			return Char.IsLetterOrDigit(c) || (c == '_') || (c == '$');
		}

		private void SkipLine()
		{
			for (;;)
			{
				switch (Lookahead())
				{
					case '\0':
						return;
					case '\n':
						++m_Pos;
						return;
					default:
						++m_Pos;
						break;
				}
			}
		}

		private void SkipMultilineComment()
		{
			char prev_char = ' ';
			for (;;)
			{
				char c = Lookahead();
				switch (c)
				{
					case '\0':
						throw new TokenizerException(m_Text, m_Pos, "Unclosed multiline comment at the end of the expression.");
					case '/':
						++m_Pos;
						if (prev_char == '*')
							return;
						prev_char = c;
						break;
					default:
						++m_Pos;
						prev_char = c;
						break;
				}
			}
		}

		private void SkipSpaces()
		{
			for (;;)
			{
				switch (Lookahead())
				{
					case ' ':
					case '\t':
					case '\r':
					case '\n':
						++m_Pos;
						break;
					case '/':
						switch (Lookahead(1))
						{
							case '/':
								m_Pos += 2;
								SkipLine();
								break;
							case '*':
								m_Pos += 2;
								SkipMultilineComment();
								break;
							default:
								return;
						}
						break;
					default:
						return;
				}
			}
		}

		private char Lookahead()
		{
			return Lookahead(0);
		}

		// returns '\0' if we reached the end of stream
		private char Lookahead(int offset)
		{
			offset += m_Pos;
			if (offset >= m_Text.Length)
				return '\0';
			return m_Text[offset];
		}

		bool m_NextTokenAvailable;
		Token m_NextToken;
		private string m_Text;
		private int m_Pos;
	}
}
