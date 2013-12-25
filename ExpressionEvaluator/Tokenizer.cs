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
		Else,

		OpenBrace,
		CloseBrace,
		OpenBracket,
		CloseBracket,

		// supporting only singleline comments to make it easier to perform partial syntax highlighting
		SingleLineComment,

		EOF,
	}

	struct Token
	{
		public TokenType type;
		public string data;
		public int pos;
		public int length;

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
				case TokenType.Else: return "\"else\"";
				case TokenType.OpenBrace: return "\"{\"";
				case TokenType.CloseBrace: return "\"}\"";
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
		public Tokenizer(string text) : this(text, false)
		{
		}

		public Tokenizer(string text, bool return_comment_tokens) : this(text, return_comment_tokens, 0)
		{
		}

		public Tokenizer(string text, bool return_comment_tokens, int start_pos)
		{
			m_Text = text;
			m_Pos = start_pos;
			m_ReturnCommentTokens = return_comment_tokens;
		}

		public void ResetAndGoToPosition(int pos)
		{
			m_Failed = false;
			m_NextTokenAvailable = false;
			m_Pos = pos;
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

		bool SetNextToken(TokenType type, int pos, int length)
		{
			m_NextToken.type = type;
			m_NextToken.data = null;
			m_NextToken.pos = pos;
			m_NextToken.length = length;
			m_Pos = pos + length;
			return true;
		}

		bool SetNextToken(TokenType type, int length)
		{
			return SetNextToken(type, m_Pos, length);
		}

		bool SetNextToken(TokenType type, string data, int pos, int length)
		{
			m_NextToken.type = type;
			m_NextToken.data = data;
			m_NextToken.pos = pos;
			m_NextToken.length = length;
			return true;
		}


		bool ParseNextToken()
		{
			if (m_Failed)
				throw new InvalidOperationException("You are trying to use a tokenizer that has already stopped tokenizing with an exception!");
			try
			{
				return ParseNextToken_NoCatch();
			}
			catch
			{
				m_Failed = true;
				throw;
			}
		}

		// The return value is always true, in case of error an exception is thrown.
		// The return value is there just to make the function body shorter.
		bool ParseNextToken_NoCatch()
		{
			m_NextTokenAvailable = true;

			if (SkipSpaces())
				return true;
			switch (Lookahead())
			{
				case '\0':
					return SetNextToken(TokenType.EOF, 0);
				case '+':
					return SetNextToken(TokenType.OpConcat, 1);
				case '{':
					return SetNextToken(TokenType.OpenBrace, 1);
				case '}':
					return SetNextToken(TokenType.CloseBrace, 1);
				case '(':
					return SetNextToken(TokenType.OpenBracket, 1);
				case ')':
					return SetNextToken(TokenType.CloseBracket, 1);
				case '?':
					return SetNextToken(TokenType.Ternary, 1);
				case '&':
					if (Lookahead(1) == '&')
						return SetNextToken(TokenType.OpAnd, 2);
					return SetNextToken(TokenType.OpAnd, 1);
				case '^':
					return SetNextToken(TokenType.OpXor, 1);
				case '|':
					if (Lookahead(1) == '|')
						return SetNextToken(TokenType.OpOr, 2);
					return SetNextToken(TokenType.OpOr, 1);
				case '=':
					switch (Lookahead(1))
					{
						case '=':
							return SetNextToken(TokenType.OpEquals, 2);
						case '~':
							return SetNextToken(TokenType.OpRegexMatch, 2);
						default:
							throw new InvalidTokenException(m_Text, m_Pos, "Invalid or incomplete operator: '='");
					}
				case '!':
					switch (Lookahead(1))
					{
						case '=':
							return SetNextToken(TokenType.OpNotEquals, 2);
						case '~':
							return SetNextToken(TokenType.OpRegexNotMatch, 2);
						default:
							return SetNextToken(TokenType.OpNot, 1);
					}
				case '"':
					return ParseString();
				default:
					return ParseVariableOrOperator();
			}
		}

		bool ParseString()
		{
			int start_pos = m_Pos;
			++m_Pos;
			StringBuilder sb = new StringBuilder();
			char c = Lookahead();
			while (c != '\0' && c!='\n')
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

			if (c == '\0' || c == '\n')
				throw new TokenizerException(m_Text, start_pos, "Reached the end of line while parsing the quoted string.");
			++m_Pos;
			string s = sb.ToString();
			return SetNextToken(TokenType.String, s, start_pos, m_Pos-start_pos);
		}

		bool ParseVariableOrOperator()
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
					return SetNextToken(TokenType.OpNot, variable, start_pos, variable.Length);
				case "upcase":
					return SetNextToken(TokenType.OpUpcase, variable, start_pos, variable.Length);
				case "locase":
					return SetNextToken(TokenType.OpLocase, variable, start_pos, variable.Length);
				case "lcap":
					return SetNextToken(TokenType.OpLcap, variable, start_pos, variable.Length);
				case "contains":
					return SetNextToken(TokenType.OpContains, variable, start_pos, variable.Length);
				case "startswith":
					return SetNextToken(TokenType.OpStartsWith, variable, start_pos, variable.Length);
				case "endswith":
					return SetNextToken(TokenType.OpEndsWith, variable, start_pos, variable.Length);
				case "and":
					return SetNextToken(TokenType.OpAnd, variable, start_pos, variable.Length);
				case "xor":
					return SetNextToken(TokenType.OpXor, variable, start_pos, variable.Length);
				case "or":
					return SetNextToken(TokenType.OpOr, variable, start_pos, variable.Length);
				case "if":
					return SetNextToken(TokenType.If, variable, start_pos, variable.Length);
				case "else":
					return SetNextToken(TokenType.Else, variable, start_pos, variable.Length);
				default:
					return SetNextToken(TokenType.Variable, variable, start_pos, variable.Length);
			}
		}

		bool IsValidVariableChar(char c)
		{
			return Char.IsLetterOrDigit(c) || (c == '_') || (c == '$');
		}

		void SkipLine()
		{
			for (;;)
			{
				switch (Lookahead())
				{
					case '\0':
					case '\n':
						return;
					default:
						++m_Pos;
						break;
				}
			}
		}

		// Returns true if the next token has been set to a comment token.
		bool SkipSpaces()
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
								if (m_ReturnCommentTokens)
								{
									int start_pos = m_Pos;
									m_Pos += 2;
									SkipLine();
									SetNextToken(TokenType.SingleLineComment, null, start_pos, m_Pos - start_pos);
									return true;
								}
								else
								{
									m_Pos += 2;
									SkipLine();
									break;
								}
							default:
								return false;
						}
						break;
					default:
						return false;
				}
			}
		}

		char Lookahead()
		{
			return Lookahead(0);
		}

		// returns '\0' if we reached the end of stream
		char Lookahead(int offset)
		{
			offset += m_Pos;
			if (offset >= m_Text.Length)
				return '\0';
			return m_Text[offset];
		}

		bool m_Failed;
		bool m_NextTokenAvailable;
		Token m_NextToken;
		bool m_ReturnCommentTokens;
		string m_Text;
		int m_Pos;
	}
}
