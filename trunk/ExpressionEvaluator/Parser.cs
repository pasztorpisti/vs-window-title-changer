using System.Diagnostics;
using System.Text.RegularExpressions;


namespace VSWindowTitleChanger.ExpressionEvaluator
{
	// Operators, higher precedence first (you can use parentheses to modify the evaluation order):
	// ternary
	// =~ !~                         string regex match and not match (case insensitive)
	// not upcase locase lcap        logical not, convert string to uppercase, convert string to lowercase, convert string to have a leading capital
	// +                             string concatenation
	// == !=                         binary/string equals and not equals (case insensitive)
	// and                           logical and
	// xor                           logical xor
	// or                            logical or
	//
	// If both operands of == or != have string type then these perform string comparison.
	// If one of the operands is bool and the other is string then the string is automatically
	// converted to bool before performing the comparison.
	//
	// Ternary operator:
	// [ cond_expression | expression_0 | expression_1 ]
	// cond_expression is evaluated and converted into bool (even if the type of the expression is string) and in case of
	// a true cond_expression the value of expression_0 is used otherwise the value of expression_1.
	//
	// Simplified "Ternary operator":
	// [ cond_expression | expression_0 ]
	// cond_expression is evaluated and converted into bool (even if the type of the expression is string) and in case of
	// a true cond_expression the value of expression_0 is used otherwise the value of the expression is an empty string
	// (that can be converted to false if needed by the context).
	//
	// In case of ternary operators if cond_expression is a regex match (=~) then expression_0 of the ternary operator can
	// access the captured groups of the regex as $0, $1, ... Named groups can be accessed as $group_name.
	// In case of the long ternary operator if cond_expression is a regex not match (!~) then expression_1 of the ternary operator can
	// access the captured groups of the regex as $0, $1, ... Named groups can be accessed as $group_name.
	//
	// Operands/literals:
	// builtin_var_name              refers to the value of the builtin variable with the specified name, for example solution_path, case insensitive
	// true                          a bool constant, case insensitive. you can use for example TRUE or True
	// false                         a bool constant, case insensitive. you can use for example FALSE or False
	// "str literal"                 a string constant, there is no escape character. if you want a quotation mark inside the string then
	//                               use double quotation marks: "a double quote in the string: "" <- here"
	//
	// Builtin variable names:
	// sln_0, sln_1, ...             string: the captured groups of the solution pathname regex. an empty string if the group doesn't exist.
	// sln_groupname                 string: a captured named group of the solution pathname regex. an empty string if the group doesn't exist.
	// sln_open                      bool: true if we have a solution open
	// sln_path                      string: the full pathname of the solution file
	// sln_dir                       string: solution directory, there is no trailing path separator char
	// sln_file                      string: solution filename without directory but with the extension included
	// sln_filename                  string: solution filename without directory and extension
	// sln_dirty                     bool
	// doc_open                      bool: true if we have an active document
	// doc_path                      string: the full pathname of the active document file
	// doc_dir                       string: active document directory, there is no trailing path separator char
	// doc_file                      string: active document filename without directory but with the extension included
	// doc_filename                  string: active document filename without directory and extension
	// doc_ext                       string: active document extension, e.g.: "txt"
	// doc_dirty                     bool
	// startup_proj                  string: name of the startup project or empty string if there is no startup project
	//                                       (this name is usually the same as startup_proj_filename but it can also be different from
	//                                       startup_proj_filename and in that case statup_proj is what you see in the solution explorer).
	// startup_proj_path             string: the full pathname of the startup project or empty string if there is no startup project
	// startup_proj_dir              string: the directory of the startup project or empty string if there is no startup project
	// startup_proj_file             string: the name of the startup project file without directory
	// startup_proj_filename         string: the name of the startup project file without directory and extension
	// startup_proj_ext              string: the extension of the startup project file
	// startup_proj_dirty            bool
	// wnd_minimized                 bool: true if VS is minimized
	// wnd_foreground                bool: true if this instance of VS is in the foreground
	// debugging                     bool
	// debug_mode                    string: "" or "Running" or "Debugging". Can be used as a bool because it is empty string when not debugging.
	// configuration                 string: Configuration name or an empty string (false) if there is no active platform. e.g.: "Release"
	// platform                      string: Platform name or an empty string (false) if there is no active platform. e.g.: "Win32"
	// dte_version                   string: e.g.: "8.0" in case of VS2005, "9.0" in case of VS2008, "10.0" for Visual Studio 2010, ....
	// vs_version                    string: e.g.: "2005" in case of VS2005, "2008" in case of Visual Studio 2008,... vs_version is mapped from dte_version by the plugin for convenience
	//                                       If the plugin can't recognize a dte_version then vs_version is the same as dte_version.
	// vs_edition                    string: e.g.: "Ultimate"
	// orig_title                    string: the original titlebar text Visual Studio wants to set.
	// multi_instances               bool: true if at least one other instance of Visual Studio is running simultaneously. The other instances are allowed to be different versions of Visual Studio.
	// multi_instances_same_ver      bool: true if at least one other Visual Studio instances is running with the same version number as our instance.

	// Grammar:
	// Expression ->	OpOr
	// OpOr ->			OpXor ( "or" OpXor )*
	// OpXor ->			OpAnd ( "xor" OpAnd )*
	// OpAnd ->			OpCompare ( "and" OpCompare )*
	// OpCompare ->		OpConcat ( ( "==" | "!=" ) OpConcat )*
	// OpConcat ->		OpUnary ( "+" OpUnary )*
	// OpUnary ->		( "not" | "upcase" | "locase" | "lcap" ) OpUnary | OpRegex
	// OpRegex ->		Value ( ( "=~" | "!~" ) ConstValue )
	// Value ->			StringLiteral | Variable | "(" Expression ")" | Ternary
	// Ternary ->		"[" Expression "|" Expression "]" | "[" Expression "|" Expression "|" Expression "]"
	// StringLiteral ->	'"' ( UnicodeCharacter* ( EscapedQuote UnicodeCharacter* )* '"'
	// EscapedQuote ->	'"' '"'
	// ConstValue ->	<< A Value that doesn't contain any Variables >>

	using Private;
	using Tokenizer;


	class ParserException : ExpressionEvaluatorException
	{
		public ParserException(string input_text, int error_pos, string error_message)
			: base(error_message)
		{
			m_InputText = input_text;
			m_ErrorPos = error_pos;
			m_ErrorMessage = error_message;
		}

		public string InputText { get { return m_InputText; } }
		public int ErrorPos { get { return m_ErrorPos; } }
		public string ErrorMessage { get { return m_ErrorMessage; } }

		private string m_InputText;
		private int m_ErrorPos;
		private string m_ErrorMessage;
	}

	class ParserException_ExpectedTokens : ParserException
	{
		private static string TokenTypesToString(TokenType[] token_types)
		{
			string s = "";
			foreach (TokenType token_type in token_types)
			{
				if (s.Length > 0)
					s += ", ";
				s += Token.TokenTypeToLiteral(token_type);
			}
			return s;
		}

		public ParserException_ExpectedTokens(string input_text, int error_pos, params TokenType[] token_types)
			: base(input_text, error_pos, string.Format("Expected one of the following tokens: {0}", TokenTypesToString(token_types)))
		{
			m_ExpectedTokenTypes = token_types;
		}

		public TokenType[] ExpectedTokenTypes { get { return m_ExpectedTokenTypes; } }
		private TokenType[] m_ExpectedTokenTypes;
	}


	class Parser
	{
		public Parser(string text) : this(text, null, true) {}

		public Parser(string text, IVariableValueResolver compile_time_constants)
			: this(text, compile_time_constants, true) {}

		public Parser(string text, IVariableValueResolver compile_time_constants, bool consume_whole_text)
		{
			m_Tokenizer = new Tokenizer.Tokenizer(text);
			m_CompileTimeConstants = compile_time_constants==null ? (IVariableValueResolver)new VariableValueResolver() : compile_time_constants;
			m_ConsumeWholeText = consume_whole_text;
		}

		public Expression Parse()
		{
			Debug.Assert(m_Tokenizer.Pos == 0, "Don't call Parse() twice!");
			// an empty expression
			if (m_Tokenizer.PeekNextToken().type == TokenType.EOF)
				return null;
			Expression expr = Parse_Expression();
			Token token = m_Tokenizer.PeekNextToken();
			if (!m_ConsumeWholeText || token.type == TokenType.EOF)
				return expr;
			throw new ParserException(m_Tokenizer.Text, token.pos, "Expected an operator here.");
		}

		public string Text { get { return m_Tokenizer.Text; } }
		public int Pos { get { return m_Tokenizer.Pos; } }

		private Expression Parse_Expression()
		{
			return Parse_OpOr();
		}

		private Expression Parse_OpOr()
		{
			Expression expr = Parse_OpXor();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpOr)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpOr(expr, Parse_OpXor());
			}
			return expr;
		}

		private Expression Parse_OpXor()
		{
			Expression expr = Parse_OpAnd();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpXor)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpXor(expr, Parse_OpAnd());
			}
			return expr;
		}

		private Expression Parse_OpAnd()
		{
			Expression expr = Parse_OpCompare();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpAnd)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpAnd(expr, Parse_OpCompare());
			}
			return expr;
		}

		private Expression Parse_OpCompare()
		{
			Expression expr = Parse_OpConcat();
			for (;;)
			{
				TokenType op = m_Tokenizer.PeekNextToken().type;
				if (op != TokenType.OpEquals && op != TokenType.OpNotEquals)
					return expr;
				m_Tokenizer.ConsumeNextToken();
				if (op == TokenType.OpEquals)
					expr = new OpEquals(expr, Parse_OpConcat());
				else
					expr = new OpNotEquals(expr, Parse_OpConcat());
			}
		}

		private Expression Parse_OpConcat()
		{
			Expression expr = Parse_OpUnary();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpConcat)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpConcat(expr, Parse_OpUnary());
			}
			return expr;
		}

		private Expression Parse_OpUnary()
		{
			TokenType token_type = m_Tokenizer.PeekNextToken().type;
			switch (token_type)
			{
				case TokenType.OpNot:
					m_Tokenizer.ConsumeNextToken();
					return new OpNot(Parse_OpUnary());
				case TokenType.OpUpcase:
					m_Tokenizer.ConsumeNextToken();
					return new OpUpperCase(Parse_OpUnary());
				case TokenType.OpLocase:
					m_Tokenizer.ConsumeNextToken();
					return new OpLowerCase(Parse_OpUnary());
				case TokenType.OpLcap:
					m_Tokenizer.ConsumeNextToken();
					return new OpLeadingCapitalCase(Parse_OpUnary());
				default:
					return Parse_OpRegex();
			}
		}

		private Expression Parse_OpRegex()
		{
			Expression expr = Parse_Value();
			TokenType op = m_Tokenizer.PeekNextToken().type;
			if (op != TokenType.OpRegexMatch && op != TokenType.OpRegexNotMatch)
				return expr;
			m_Tokenizer.ConsumeNextToken();

			Token token = m_Tokenizer.PeekNextToken();
			Expression regex_expr = Parse_Value();
			Value const_val = regex_expr.EliminateConstSubExpressions();
			if (const_val == null)
				throw new ParserException(m_Tokenizer.Text, token.pos, "Expected a constant expression that evaluates into a regex string. This expression isn't constant.");
			if (!(const_val is StringValue))
				throw new ParserException(m_Tokenizer.Text, token.pos, "Expected a constant expression that evaluates into a regex string. This expression evaluates to a bool constant.");

			Regex regex;
			try
			{
				regex = new Regex(const_val.ToString(), RegexOptions.IgnoreCase);
			}
			catch (System.Exception ex)
			{
				throw new ParserException(m_Tokenizer.Text, token.pos, "Invalid regex! " + ex.Message);
			}

			return new OpRegexMatch(expr, regex_expr, regex, op == TokenType.OpRegexNotMatch);
		}

		private Expression Parse_Value()
		{
			Token token = m_Tokenizer.GetNextToken();
			switch (token.type)
			{
				case TokenType.String:
					return new StringValue(token.data);
				case TokenType.Variable:
					{
						Variable variable = new Variable(token.data, token.pos);
						Value v = m_CompileTimeConstants.GetVariableValue(variable);
						if (v != null)
							return v;
						return variable;
					}
				case TokenType.OpenBracket:
					{
						Expression expr = Parse_Expression();
						Expect(TokenType.CloseBracket);
						return expr;
					}
				case TokenType.OpenTernary:
					return Parse_Ternary();
				default:
					throw new ParserException(m_Tokenizer.Text, token.pos, "Expected a value here.");
			}
		}

		private Expression Parse_Ternary()
		{
			Expression cond_expr = Parse_Expression();
			Expect(TokenType.TernarySeparator);
			Expression true_expr = Parse_Expression();
			Expression false_expr;
			TokenType token_type = Expect(TokenType.TernarySeparator, TokenType.CloseTernary);
			if (token_type == TokenType.TernarySeparator)
			{
				false_expr = Parse_Expression();
				Expect(TokenType.CloseTernary);
			}
			else
			{
				false_expr = new StringValue("");
			}
			return new Ternary(cond_expr, true_expr, false_expr);
		}

		private TokenType Expect(params TokenType[] token_types)
		{
			Token token = m_Tokenizer.GetNextToken();
			foreach (TokenType t in token_types)
			{
				if (t == token.type)
					return t;
			}
			throw new ParserException_ExpectedTokens(m_Tokenizer.Text, token.pos, token_types);
		}

		Tokenizer.Tokenizer m_Tokenizer;
		IVariableValueResolver m_CompileTimeConstants;
		bool m_ConsumeWholeText;
	}
}
