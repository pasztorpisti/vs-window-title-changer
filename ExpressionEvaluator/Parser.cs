using System.Diagnostics;
using System.Text.RegularExpressions;


namespace VSWindowTitleChanger.ExpressionEvaluator
{
	// Operators, higher precedence first (you can use parentheses to modify the evaluation order):
	// =~ !~                                            string regex match and not match (case insensitive)
	// not ! upcase locase lcap bool str backslashize   logical not, convert string to uppercase, convert string to lowercase, convert string to have a leading capital
	//                                                  convert to bool, convert to string, convert to string if needed and replace all '/' chars to '\' chars.
	// +                                                string concatenation
	// contains startswith endswith                     string: contains substring, starts with string, ends with string
	// == !=                                            binary/string equals and not equals (case insensitive)
	// and && &                                         logical and
	// xor ^                                            logical xor
	// or || |                                          logical or
	// low precedence ternary ?:
	//
	// If both operands of == or != have string type then these perform string comparison.
	// If one of the operands is bool and the other is string then the string is automatically
	// converted to bool before performing the comparison.
	//
	// Ternary operator:
	//   cond_value ? true_operand
	//   expression_0 if cond_expression else expression_1
	// cond_expression is evaluated and converted into bool (even if the type of the expression is string) and in case of
	// a true cond_expression the value of expression_0 is used otherwise the value of expression_1.
	//
	// In case of ternary operators if the condition expression is a regex match (=~) then expression_0 of 
	// the ternary operator can access the captured groups of the regex as $0, $1, ... Named groups can be accessed as $group_name.
	// In case of the regex not match (!~) the expression_1 of the ternary operator can access the captured groups of the regex as
	// $0, $1, ... Named groups can be accessed as $group_name.
	// The above statement holds for normal "if (cond_expr) [then] { true_expr } else { false_expr }" constructs as well.
	//
	// Operands/literals:
	// builtin_var_name              refers to the value of the builtin variable with the specified name, for example solution_path, case insensitive
	// true                          a bool constant, case insensitive. you can use for example TRUE or True
	// false                         a bool constant, case insensitive. you can use for example FALSE or False
	// "str literal"                 a string constant, there is no escape character. if you want a quotation mark inside the string then
	//                               use double quotation marks: "a double quote in the string: "" <- here"
	//
	// Builtin variable names:
	// active_wnd_class              string: the classname of the active VS window. Empty string if the currently focused foreground window isn't a window of VS.
	// active_wnd_title              string: the title of the active VS window. Empty string if the currently focused foreground window isn't a window of VS.
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
	// any_proj_dirty                bool: true if any of the project files is modified
	// any_doc_dirty                 bool: true if any of the open documents/files is modified
	// anything_dirty                bool: true if the solution file or any of the project files or any of the open documents is modified
	// wnd_minimized                 bool: true if the main window of VS is minimized
	// wnd_foreground                bool: true if the main window of VS is in the foreground
	// app_active                    bool: true if one of the windows of this VS instance is the foreground window.
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
	// Expression ->				LowPrecedenceTernary
	// LowPrecedenceTernary ->		OpOr "?" OpOr ":" OpOr | OpOr "?" OpOr
	// OpOr ->						OpXor ( "or" OpXor )*
	// OpXor ->						OpAnd ( "xor" OpAnd )*
	// OpAnd ->						OpCompare ( "and" OpCompare )*
	// OpCompare ->					OpSubstring ( ( "==" | "!=" ) OpSubstring )*
	// OpSubstring ->				OpConcat ( ( "contains" | "startswith" | "endswith" ) OpConcat )*
	// OpConcat ->					OpRegex ( "+" OpRegex )*
	// OpRegex ->					FunctionCall ( ( "=~" | "!~" ) Const-FunctionCall )
	// FunctionCall ->				OpUnary | FuncExec | Value
	// Value ->						StringLiteral | Variable | BracketExpression | IfElse
	// IfElse ->					"if" BracketExpression BraceExpression "else" ( BraceExpression | IfElse )
	// BracketExpression ->			"(" Expression ")"
	// BraceExpression ->			"{" Expression "}"

	// FuncExec ->					"exec" Integer FunctionCall FunctionCall | "exec" Variable Integer FunctionCall FunctionCall
	// OpUnary ->					( "not" | "upcase" | "locase" | "lcap" ) FunctionCall

	// Integer ->					Digit Digit*
	// Digit ->						"0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"
	// StringLiteral ->				'"' ( UnicodeCharacter* ( EscapedQuote UnicodeCharacter* )* '"'
	// EscapedQuote ->				'"' '"'

	// Const- ->					<< Doesn't contain any Variables >>

	using Private;
	using Tokenizer;
	using System;


	class ParserException : ExpressionEvaluatorException
	{
		public ParserException(string input_text, int error_pos, string error_message)
			: this(input_text, error_pos, error_message, null) {}
		public ParserException(string input_text, int error_pos, string error_message, Exception inner_exception)
			: base(error_message, inner_exception)
		{
			m_InputText = input_text;
			m_ErrorPos = error_pos;
			m_ErrorMessage = error_message;
		}

		public string InputText { get { return m_InputText; } }
		public int ErrorPos { get { return m_ErrorPos; } }
		public string ErrorMessage { get { return m_ErrorMessage; } }

		string m_InputText;
		int m_ErrorPos;
		string m_ErrorMessage;
	}

	class ParserException_ExpectedTokens : ParserException
	{
		static string TokenTypesToString(TokenType[] token_types)
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
		TokenType[] m_ExpectedTokenTypes;
	}


	class Parser
	{
		public Parser(string text, ExecFuncEvaluator exec_func_evaluator) : this(text, exec_func_evaluator, null, true) {}

		public Parser(string text, ExecFuncEvaluator exec_func_evaluator, IVariableValueResolver compile_time_constants)
			: this(text, exec_func_evaluator, compile_time_constants, true) {}

		public Parser(string text, ExecFuncEvaluator exec_func_evaluator, IVariableValueResolver compile_time_constants, bool consume_whole_text)
		{
			m_Tokenizer = new Tokenizer.Tokenizer(text);
			m_ExecFuncEvaluator = exec_func_evaluator;
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
		public bool ContainsExec { get { return m_ContainsExec; } }

		Expression Parse_Expression()
		{
			return Parse_LowPrecedenceTernary();
		}


		Value m_DefaultValue = new StringValue("");

		Expression Parse_LowPrecedenceTernary()
		{
			Expression expr = Parse_OpOr();
			while (m_Tokenizer.PeekNextToken().type == TokenType.Ternary)
			{
				m_Tokenizer.ConsumeNextToken();
				Expression true_expr = Parse_OpOr();
				Expression false_expr;
				if (m_Tokenizer.PeekNextToken().type == TokenType.TernarySeparator)
				{
					m_Tokenizer.ConsumeNextToken();
					false_expr = Parse_OpOr();
				}
				else
				{
					false_expr = m_DefaultValue;
				}
				expr = new Ternary(expr, true_expr, false_expr);
			}
			return expr;
		}

		Expression Parse_OpOr()
		{
			Expression expr = Parse_OpXor();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpOr)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpOr(expr, Parse_OpXor());
			}
			return expr;
		}

		Expression Parse_OpXor()
		{
			Expression expr = Parse_OpAnd();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpXor)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpXor(expr, Parse_OpAnd());
			}
			return expr;
		}

		Expression Parse_OpAnd()
		{
			Expression expr = Parse_OpCompare();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpAnd)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpAnd(expr, Parse_OpCompare());
			}
			return expr;
		}

		Expression Parse_OpCompare()
		{
			Expression expr = Parse_OpSubstring();
			for (;;)
			{
				TokenType op = m_Tokenizer.PeekNextToken().type;
				if (op != TokenType.OpEquals && op != TokenType.OpNotEquals)
					return expr;
				m_Tokenizer.ConsumeNextToken();
				if (op == TokenType.OpEquals)
					expr = new OpEquals(expr, Parse_OpSubstring());
				else
					expr = new OpNotEquals(expr, Parse_OpSubstring());
			}
		}

		Expression Parse_OpSubstring()
		{
			Expression expr = Parse_OpConcat();
			for (;;)
			{
				TokenType op = m_Tokenizer.PeekNextToken().type;
				switch (op)
				{
					case TokenType.OpContains:
						m_Tokenizer.ConsumeNextToken();
						expr = new OpContains(expr, Parse_OpConcat());
						break;
					case TokenType.OpStartsWith:
						m_Tokenizer.ConsumeNextToken();
						expr = new OpStartsWith(expr, Parse_OpConcat());
						break;
					case TokenType.OpEndsWith:
						m_Tokenizer.ConsumeNextToken();
						expr = new OpEndsWith(expr, Parse_OpConcat());
						break;
					default:
						return expr;
				}
			}
		}

		Expression Parse_OpConcat()
		{
			Expression expr = Parse_OpRegex();
			while (m_Tokenizer.PeekNextToken().type == TokenType.OpConcat)
			{
				m_Tokenizer.ConsumeNextToken();
				expr = new OpConcat(expr, Parse_OpRegex());
			}
			return expr;
		}

		Expression Parse_OpRegex()
		{
			Expression expr = Parse_FunctionCall();

			for (;;)
			{
				TokenType op = m_Tokenizer.PeekNextToken().type;
				if (op != TokenType.OpRegexMatch && op != TokenType.OpRegexNotMatch)
					break;

				m_Tokenizer.ConsumeNextToken();

				Token token = m_Tokenizer.PeekNextToken();
				Expression regex_expr = Parse_FunctionCall();
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

				expr = new OpRegexMatch(expr, regex_expr, regex, op == TokenType.OpRegexNotMatch);
			}
			return expr;
		}

		Expression Parse_FunctionCall()
		{
			TokenType token_type = m_Tokenizer.PeekNextToken().type;
			switch (token_type)
			{
				// More complex "Function Calls"
				case TokenType.FuncExec:
					m_Tokenizer.ConsumeNextToken();
					return Parse_FuncExec();
				case TokenType.FuncRelPath:
					m_Tokenizer.ConsumeNextToken();
					return new FuncRelPath(Parse_FunctionCall(), Parse_FunctionCall());
				case TokenType.FuncWorkspaceName:
					m_Tokenizer.ConsumeNextToken();
					return new FuncWorkspaceName(Parse_FunctionCall());
				case TokenType.FuncWorkspaceOwner:
					m_Tokenizer.ConsumeNextToken();
					return new FuncWorkspaceOwner(Parse_FunctionCall());

				// Unary Operators
				case TokenType.OpNot:
					m_Tokenizer.ConsumeNextToken();
					return new OpNot(Parse_FunctionCall());
				case TokenType.OpUpcase:
					m_Tokenizer.ConsumeNextToken();
					return new OpUpperCase(Parse_FunctionCall());
				case TokenType.OpLocase:
					m_Tokenizer.ConsumeNextToken();
					return new OpLowerCase(Parse_FunctionCall());
				case TokenType.OpLcap:
					m_Tokenizer.ConsumeNextToken();
					return new OpLeadingCapitalCase(Parse_FunctionCall());
				case TokenType.OpBool:
					m_Tokenizer.ConsumeNextToken();
					return new OpBool(Parse_FunctionCall());
				case TokenType.OpString:
					m_Tokenizer.ConsumeNextToken();
					return new OpString(Parse_FunctionCall());
				case TokenType.OpBackslashize:
					m_Tokenizer.ConsumeNextToken();
					return new OpBackslashize(Parse_FunctionCall());
				default:
					return Parse_Value();
			}
		}

		Expression Parse_FuncExec()
		{
			m_ContainsExec = true;

			Token token = m_Tokenizer.GetNextToken();
			if (token.type != TokenType.Variable)
				throw new ParserException(m_Tokenizer.Text, token.pos, "The first parameter of exec must be a variable name or a positive integer.");

			string variable_name = null;
			int exec_period_secs = 0;

			try
			{
				exec_period_secs = Convert.ToInt32(token.data);
			}
			catch (System.Exception)
			{
				variable_name = token.data;
				token = m_Tokenizer.GetNextToken();
				if (token.type != TokenType.Variable)
					throw new ParserException(m_Tokenizer.Text, token.pos, "If the first parameter of exec is a variable then the second must be an positive integer (exec period in seconds).");
				try
				{
					exec_period_secs = Convert.ToInt32(token.data);
				}
				catch (System.Exception)
				{
					throw new ParserException(m_Tokenizer.Text, token.pos, "If the first parameter of exec is a variable then the second must be an positive integer (exec period in seconds).");
				}
			}

			if (exec_period_secs <= 0)
				throw new ParserException(m_Tokenizer.Text, token.pos, "The exec_period parameter of exec must be a an integer that is greater than zero!");

			Expression command, workdir;
			try
			{
				command = Parse_FunctionCall();
			}
			catch (ParserException ex)
			{
				throw new ParserException(ex.InputText, ex.ErrorPos, "Error parsing the 'command' parameter of the exec function!", ex);
			}

			try
			{
				workdir = Parse_FunctionCall();
			}
			catch (ParserException ex)
			{
				throw new ParserException(ex.InputText, ex.ErrorPos, "Error parsing the 'workdir' parameter of the exec function!", ex);
			}

			return new FuncExec(m_ExecFuncEvaluator, variable_name, exec_period_secs, command, workdir);
		}

		Expression Parse_Value()
		{
			Token token = m_Tokenizer.PeekNextToken();
			switch (token.type)
			{
				case TokenType.String:
					m_Tokenizer.ConsumeNextToken();
					return new StringValue(token.data);
				case TokenType.Variable:
					{
						m_Tokenizer.ConsumeNextToken();
						Variable variable = new Variable(token.data, token.pos, token.length);
						Value v = m_CompileTimeConstants.GetVariableValue(variable);
						if (v != null)
							return v;
						return variable;
					}
				case TokenType.OpenBracket:
					return Parse_BracketExpression();
				case TokenType.If:
					return Parse_IfElse();
				default:
					throw new ParserException(m_Tokenizer.Text, token.pos, "Expected an expression here.");
			}
		}

		Expression Parse_IfElse()
		{
			Expect(TokenType.If);
			Expression cond_expr = Parse_BracketExpression();
			Expression true_expr = Parse_BraceExpression();
			Expect(TokenType.Else);
			Expression false_expr;
			Token token = m_Tokenizer.PeekNextToken();
			switch (token.type)
			{
				case TokenType.If:
					false_expr = Parse_IfElse();
					break;
				case TokenType.OpenBrace:
					false_expr = Parse_BraceExpression();
					break;
				default:
					throw new ParserException_ExpectedTokens(m_Tokenizer.Text, token.pos, new TokenType[] { TokenType.If, TokenType.OpenBrace });
			}

			return new Ternary(cond_expr, true_expr, false_expr);
		}

		Expression Parse_BracketExpression()
		{
			Expect(TokenType.OpenBracket);
			Expression expr = Parse_Expression();
			Expect(TokenType.CloseBracket);
			return expr;
		}

		Expression Parse_BraceExpression()
		{
			Expect(TokenType.OpenBrace);
			Expression expr = Parse_Expression();
			Expect(TokenType.CloseBrace);
			return expr;
		}

		TokenType Expect(params TokenType[] token_types)
		{
			Token token = m_Tokenizer.GetNextToken();
			foreach (TokenType t in token_types)
			{
				if (t == token.type)
					return t;
			}
			throw new ParserException_ExpectedTokens(m_Tokenizer.Text, token.pos, token_types);
		}

		ExecFuncEvaluator m_ExecFuncEvaluator;
		Tokenizer.Tokenizer m_Tokenizer;
		IVariableValueResolver m_CompileTimeConstants;
		bool m_ConsumeWholeText;
		bool m_ContainsExec;
	}
}
