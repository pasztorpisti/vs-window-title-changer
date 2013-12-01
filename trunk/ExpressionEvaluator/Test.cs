using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VSWindowTitleChanger.ExpressionEvaluator
{
	class ExpressionEvaluatorTest
	{
		Expression Parse(string s)
		{
			try
			{
				VariableValueResolver compile_time_constants = new VariableValueResolver();
				compile_time_constants.SetVariable("true", true);
				compile_time_constants.SetVariable("false", false);
				Parser parser = new Parser(s, compile_time_constants);
				return parser.Parse();
			}
			catch (ParserException ex)
			{
				Console.WriteLine("Parser Exception:");
				Console.WriteLine(ex.InputText);
				string indent = new string(' ', ex.ErrorPos);
				Console.WriteLine("{0}^", indent);
				Console.WriteLine("{0}{1}", indent, ex.ErrorMessage);
				throw ex;
			}
		}

		void Evaluate(string expression_str, Expression expr, string label)
		{
			EvalContext ctx = new EvalContext();
			ctx.SetVariable("x", "XXX");
			ctx.SetVariable("y", "YYY");
			ctx.SetVariable("true", "SSS");
			SafeEvalContext safe_ctx = new SafeEvalContext(ctx);
			Value v = expr.Evaluate(safe_ctx);
			Console.WriteLine("{0}: {1}", label, v.ToString());

			List<Variable> unresolved_variables = expr.CollectUnresolvedVariables(ctx);
			if (unresolved_variables.Count > 0)
			{
				Console.WriteLine("Unresolved variables in the expression: {0}", unresolved_variables.Count);
				Console.WriteLine(expression_str);
				foreach (Variable var in unresolved_variables)
				{
					Console.WriteLine(new string(' ', var.Position) + "^");
					Console.WriteLine(new string(' ', var.Position) + var.Name);
				}
			}
		}

		void DebugPrint(Expression expr, string label)
		{
			Console.WriteLine("\n{0}", label);
			expr.DebugPrintTree(0);
			Console.WriteLine();
		}

		public void Execute()
		{
			//string expression = @" [ true !~ (""^(t)"" + ""ru(e)$"") | [ ""asd"" !~ ""(a)"" | $1 | $1 ] + $1 | $1 + [ true or true + y | ""YES"" | ""NO"" ] ] ";

			//string expression = @" if ( ""true"" =~ ""^tr(u)e$"" ) then { (""DD"" + $1 ) if false ? ""0"" : ""1"" } else if true ""OtherIfTrue"" else ""OtherIfFalse"" ";
			//string expression = @" ""0"" + ""1"" if false else ""2"" + false ? ""3"" ? ""4"" ? upcase  false ? true ";

			string expression = " \"0\" + \"1\" if false else \"2\" + false ? \"3\" ? \"4\" ? upcase //vasnvoienve4\r\n" +
				" false ? true /*fwioejf\r\n" +
				" sdlfkjsdf \r\n" +
				"dlkfnwe wefkw \n" +

				"//asdfasdf*/\r\n";

			//string expression = @" ""|"" + ""sf"" =~ ""^dsf$"" + ""|"" + not [true | true | false] =~ ""^tru$"" ";
			//string expression = @" [ ""sf"" =~ ""^(s)(f)$"" | [ false | $1 | $2 ] | ""asdf"" ] ";
			//string expression = @"true";

			Expression expr = Parse(expression);
			DebugPrint(expr, "Before ConstExprElim");
			Evaluate(expression, expr, "Before ConstExprElim");

			Value ev = expr.EliminateConstSubExpressions();
			if (ev != null)
				expr = ev;
			DebugPrint(expr, "After ConstExprElim");
			Evaluate(expression, expr, "After ConstExprElim");


			Regex regex = new Regex(@"a(?<myname>a(?<5>b))b(bb)$");
			Match m = regex.Match("aaabbbb");

			Console.WriteLine("GroupCount: {0}", m.Groups.Count);

			Console.WriteLine("Group numbers: {0}", regex.GetGroupNumbers().Length);
			for (int i = 0, e = regex.GetGroupNumbers().Length; i < e; ++i)
				Console.WriteLine("  {0}: {1}", i, regex.GetGroupNumbers()[i]);

			Console.WriteLine("Group names: {0}", regex.GetGroupNames().Length);
			for (int i = 0, e = regex.GetGroupNames().Length; i < e; ++i)
				Console.WriteLine("  {0}: {1}", i, regex.GetGroupNames()[i]);

			Console.WriteLine("Gourps: {0}", m.Groups.Count);
			for (int i = 0, e = m.Groups.Count; i < e; ++i)
				Console.WriteLine("  {0}: {1} {2}", i, m.Groups[i].Success, m.Groups[i].Value);
			Console.WriteLine("XX {0}", m.Groups[2].Value);
		}
	}
}
