using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;


namespace VSWindowTitleChanger.ExpressionEvaluator
{
	class ExpressionEvaluatorException : Exception
	{
		public ExpressionEvaluatorException(string error_message)
			: base(error_message) { }
	}


	enum Type
	{
		Bool,
		String,
	}

	abstract class Value : Expression
	{
		public Value() : base(null) { }
		public abstract bool ToBool();
		new public abstract Type GetType();
	}

	class BoolValue : Value
	{
		public BoolValue(bool val)
		{
			m_Value = val;
		}

		public override Type GetType()
		{
			return Type.Bool;
		}
		public override Value Evaluate(IEvalContext ctx)
		{
			return this;
		}

		public override bool ToBool()
		{
			return m_Value;
		}

		public override string ToString()
		{
			return m_Value ? "true" : "false";
		}

		private bool m_Value;
	}

	class StringValue : Value
	{
		public StringValue(string val)
		{
			m_Value = val;
		}

		public override Type GetType()
		{
			return Type.String;
		}

		public override Value Evaluate(IEvalContext ctx)
		{
			return this;
		}

		public override bool ToBool()
		{
			return m_Value.Length > 0;
		}

		public override string ToString()
		{
			return m_Value;
		}

		private string m_Value;
	}


	class Variable : Expression
	{
		public Variable(string name, int position)
			: base(null)
		{
			m_Name = name.ToLower();
			m_Position = position;
		}

		public override Value Evaluate(IEvalContext ctx)
		{
			Value v = ctx.GetVariableValue(this);
			if (v != null)
				return v;
			throw new ExpressionEvaluatorException(string.Format("Error retrieving the value of variable '{0}' during expression evaluation!", m_Name));
		}

		protected internal override Value RecursiveCollectUnresolvedVariables(IEvalContext ctx)
		{
			return Evaluate(ctx);
		}

		public override string DebugToString()
		{
			return string.Format("{0}({1}) [{2}]", GetType().Name, m_Name, m_Position);
		}

		public string Name { get { return m_Name; } }
		public int Position { get { return m_Position; } }

		private string m_Name;
		private int m_Position;
	}


	//-----------------------------------------------------------------------------------------


	interface IVariableValueResolver
	{
		// Returns null if the variable_name was not found.
		Value GetVariableValue(Variable variable);
	}

	interface IVariableValueSetter
	{
		void SetVariable(string variable_name, bool val);
		void SetVariable(string variable_name, string val);
	}

	class VariableValueResolver : IVariableValueResolver, IVariableValueSetter
	{
		public virtual Value GetVariableValue(Variable variable)
		{
			Value v;
			if (m_VariableValues.TryGetValue(variable.Name, out v))
				return v;
			return null;
		}

		public virtual void SetVariable(string variable_name, bool val)
		{
			m_VariableValues[variable_name.ToLower()] = new BoolValue(val);
		}

		public virtual void SetVariable(string variable_name, string val)
		{
			m_VariableValues[variable_name.ToLower()] = new StringValue(val);
		}

		public Dictionary<string, Value> VariableValues { get { return m_VariableValues; } }
		private Dictionary<string, Value> m_VariableValues = new Dictionary<string, Value>();
	}

	interface IEvalContext : IVariableValueResolver
	{
		void PushLocalContext(IVariableValueResolver local_ctx);
		void PopLocalContext();
	}

	abstract class EvalContextDecorator : IEvalContext
	{
		protected EvalContextDecorator(IEvalContext ctx)
		{
			m_Ctx = ctx;
		}

		public virtual Value GetVariableValue(Variable variable)
		{
			return m_Ctx.GetVariableValue(variable);
		}

		public virtual void PushLocalContext(IVariableValueResolver local_ctx)
		{
			m_Ctx.PushLocalContext(local_ctx);
		}

		public virtual void PopLocalContext()
		{
			m_Ctx.PopLocalContext();
		}

		private IEvalContext m_Ctx;
	}

	class EvalContext : IEvalContext, IVariableValueSetter
	{
		public virtual Value GetVariableValue(Variable variable)
		{
			Value v;
			if (m_LocalContextStack.Count > 0)
			{
				IVariableValueResolver local_ctx = m_LocalContextStack.Peek();
				if (local_ctx != null)
				{
					v = local_ctx.GetVariableValue(variable);
					if (v != null)
						return v;
				}
			}

			if (m_VariableValues.TryGetValue(variable.Name, out v))
				return v;
			return null;
		}

		public virtual void PushLocalContext(IVariableValueResolver local_ctx)
		{
			Debug.Assert(local_ctx != null);
			m_LocalContextStack.Push(local_ctx);
		}

		public virtual void PopLocalContext()
		{
			m_LocalContextStack.Pop();
		}

		public virtual void SetVariable(string variable_name, bool val)
		{
			m_VariableValues[variable_name.ToLower()] = new BoolValue(val);
		}

		public virtual void SetVariable(string variable_name, string val)
		{
			m_VariableValues[variable_name.ToLower()] = new StringValue(val);
		}

		public Dictionary<string, Value> VariableValues { get { return m_VariableValues; } }

		private Stack<IVariableValueResolver> m_LocalContextStack = new Stack<IVariableValueResolver>();
		private Dictionary<string, Value> m_VariableValues = new Dictionary<string, Value>();
	}

	class SafeEvalContext : EvalContextDecorator
	{
		public SafeEvalContext(IEvalContext ctx) : base(ctx) { }

		public override Value GetVariableValue(Variable variable)
		{
			Value v = base.GetVariableValue(variable);
			if (v != null)
				return v;
			return m_DefaultValue;
		}

		public Value DefaultValue { get { return m_DefaultValue; } set { m_DefaultValue = value; } }
		private Value m_DefaultValue = new StringValue("");
	}


	abstract class Expression
	{
#if DEBUG
		// In debug we make sure that the user passes at least one parameter to the constructor even if it is null...
		protected Expression(Expression first_expr, params Expression[] sub_expressions)
		{
			if (first_expr == null)
			{
				Debug.Assert(sub_expressions.Length == 0);
				m_SubExpressions = new Expression[0];
			}
			else
			{
				m_SubExpressions = new Expression[1 + sub_expressions.Length];
				m_SubExpressions[0] = first_expr;
				Array.Copy(sub_expressions, 0, m_SubExpressions, 1, sub_expressions.Length);
			}
		}
#else
		protected Expression(params Expression[] sub_expressions)
		{
			m_SubExpressions = (sub_expressions == null) ? new Expression[0] : sub_expressions;
		}
#endif

		public abstract Value Evaluate(IEvalContext ctx);


		// Evaluates constant subexpressions and returns the constant Value if this expression is a constant, null otherwise.
		public Value EliminateConstSubExpressions()
		{
			return EliminateConstSubExpressions(new Private.ConstEvalContext());
		}

		protected internal virtual Value EliminateConstSubExpressions(Private.ConstEvalContext ctx)
		{
			bool has_variable_sub_expression = false;
			for (int i = 0; i < SubExpressions.Length; ++i)
			{
				Value v = TryEliminateConstSubExpressions(ctx, SubExpressions[i]);
				if (v == null)
					has_variable_sub_expression = true;
				else
					SubExpressions[i] = v;
			}

			if (has_variable_sub_expression)
				return null;

			try
			{
				return Evaluate(ctx);
			}
			catch (Private.ConstEvalContext.NotConstExpression)
			{
				return null;
			}
		}

		protected static Value TryEliminateConstSubExpressions(Private.ConstEvalContext ctx, Expression expr)
		{
			try
			{
				return expr.EliminateConstSubExpressions(ctx);
			}
			catch (Private.ConstEvalContext.NotConstExpression)
			{
				return null;
			}
		}

		public List<Variable> CollectUnresolvedVariables(IEvalContext ctx)
		{
			Debug.Assert(!(ctx is SafeEvalContext));
			Private.UnresolvedVariableCollectorEvalContext uvc_ctx = new Private.UnresolvedVariableCollectorEvalContext(ctx);
			SafeEvalContext safe_ctx = new SafeEvalContext(uvc_ctx);
			RecursiveCollectUnresolvedVariables(safe_ctx);
			return uvc_ctx.UnresolvedVariables;
		}


		protected internal static Value m_CollectUnresolvedVariableDefaultRetVal = new BoolValue(false);

		protected internal virtual Value RecursiveCollectUnresolvedVariables(IEvalContext ctx)
		{
			for (int i = 0; i < SubExpressions.Length; ++i)
				SubExpressions[i].RecursiveCollectUnresolvedVariables(ctx);
			return m_CollectUnresolvedVariableDefaultRetVal;
		}

		public virtual IVariableValueResolver GetLocalContext()
		{
			return null;
		}

		protected Expression[] SubExpressions { get { return m_SubExpressions; } }
		private Expression[] m_SubExpressions;


		public void DebugPrintTree(int indent)
		{
			Debug.WriteLine(new string(' ', indent) + DebugToString());
			Console.WriteLine(new string(' ', indent) + DebugToString());
			foreach (Expression expr in SubExpressions)
			{
				if (expr == null)
				{
					Debug.WriteLine(new string(' ', indent + 1) + "null");
					Console.WriteLine(new string(' ', indent + 1) + "null");
				}
				else
				{
					expr.DebugPrintTree(indent + 1);
				}
			}
		}

		public virtual string DebugToString()
		{
			return GetType().Name;
		}
	}



	namespace Private
	{

		class UnresolvedVariableCollectorEvalContext : EvalContextDecorator
		{
			public UnresolvedVariableCollectorEvalContext(IEvalContext ctx) : base(ctx) { }

			public override Value GetVariableValue(Variable variable)
			{
				Value v = base.GetVariableValue(variable);
				if (v == null)
					m_UnresolvedVariables.Add(variable);
				return v;
			}

			public List<Variable> UnresolvedVariables { get { return m_UnresolvedVariables; } }
			private List<Variable> m_UnresolvedVariables = new List<Variable>();
		}


		class ConstEvalContext : IEvalContext
		{
			public class NotConstExpression : ExpressionEvaluatorException
			{
				public NotConstExpression(Variable variable)
					: base(string.Format("The expression can not be evaluated into a constant because it contains at least one variable: {0}", variable.Name))
				{
					m_Variable = variable;
				}

				public Variable Variable { get { return m_Variable; } }
				private Variable m_Variable;
			}

			public virtual Value GetVariableValue(Variable variable)
			{
				Value v;
				if (m_LocalContextStack.Count > 0)
				{
					IVariableValueResolver local_ctx = m_LocalContextStack.Peek();
					if (local_ctx != null)
					{
						v = local_ctx.GetVariableValue(variable);
						if (v != null)
							return v;
					}
				}
				throw new NotConstExpression(variable);
			}

			public virtual void PushLocalContext(IVariableValueResolver local_ctx)
			{
				Debug.Assert(local_ctx != null);
				m_LocalContextStack.Push(local_ctx);
			}

			public virtual void PopLocalContext()
			{
				m_LocalContextStack.Pop();
			}

			private Stack<IVariableValueResolver> m_LocalContextStack = new Stack<IVariableValueResolver>();
		}


		class OpNot : Expression
		{
			public OpNot(Expression operand) : base(operand) { }
			public override Value Evaluate(IEvalContext ctx)
			{
				return new BoolValue(!SubExpressions[0].Evaluate(ctx).ToBool());
			}
		}

		class OpAnd : Expression
		{
			public OpAnd(Expression operand0, Expression operand1) : base(operand0, operand1) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				return new BoolValue(SubExpressions[0].Evaluate(ctx).ToBool() && SubExpressions[1].Evaluate(ctx).ToBool());
			}
		}

		class OpOr : Expression
		{
			public OpOr(Expression operand0, Expression operand1) : base(operand0, operand1) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				return new BoolValue(SubExpressions[0].Evaluate(ctx).ToBool() || SubExpressions[1].Evaluate(ctx).ToBool());
			}
		}

		class OpXor : Expression
		{
			public OpXor(Expression operand0, Expression operand1) : base(operand0, operand1) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				return new BoolValue(SubExpressions[0].Evaluate(ctx).ToBool() ^ SubExpressions[1].Evaluate(ctx).ToBool());
			}
		}

		class OpEquals : Expression
		{
			public OpEquals(Expression operand0, Expression operand1) : base(operand0, operand1) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				Value val0 = SubExpressions[0].Evaluate(ctx);
				Value val1 = SubExpressions[1].Evaluate(ctx);
				if (val0.GetType() == Type.String && val1.GetType() == Type.String)
					return new BoolValue(val0.ToString().Equals(val1.ToString(), StringComparison.OrdinalIgnoreCase));
				return new BoolValue(val0.ToBool() == val1.ToBool());
			}
		}

		class OpNotEquals : Expression
		{
			public OpNotEquals(Expression operand0, Expression operand1) : base(operand0, operand1) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				Value val0 = SubExpressions[0].Evaluate(ctx);
				Value val1 = SubExpressions[1].Evaluate(ctx);
				if (val0.GetType() == Type.String && val1.GetType() == Type.String)
					return new BoolValue(!val0.ToString().Equals(val1.ToString(), StringComparison.OrdinalIgnoreCase));
				return new BoolValue(val0.ToBool() != val1.ToBool());
			}
		}

		class Ternary : Expression
		{
			public Ternary(Expression cond_expr, Expression true_expr, Expression false_expr) : base(cond_expr, true_expr, false_expr) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				Expression expr = SubExpressions[0].Evaluate(ctx).ToBool() ? SubExpressions[1] : SubExpressions[2];
				IVariableValueResolver local_ctx = SubExpressions[0].GetLocalContext();
				if (local_ctx == null)
				{
					return expr.Evaluate(ctx);
				}
				else
				{
					ctx.PushLocalContext(local_ctx);
					try
					{
						Value res = expr.Evaluate(ctx);
						return res;
					}
					finally
					{
						ctx.PopLocalContext();
					}
				}
			}

			protected internal override Value EliminateConstSubExpressions(Private.ConstEvalContext ctx)
			{
				Value cond_expr = TryEliminateConstSubExpressions(ctx, SubExpressions[0]);
				if (cond_expr == null)
				{
					Value true_expr = TryEliminateConstSubExpressions(ctx, SubExpressions[1]);
					if (true_expr != null)
						SubExpressions[1] = true_expr;
					Value false_expr = TryEliminateConstSubExpressions(ctx, SubExpressions[2]);
					if (false_expr != null)
						SubExpressions[2] = false_expr;
					return null;
				}

				bool cond = cond_expr.ToBool();
				IVariableValueResolver local_ctx = SubExpressions[0].GetLocalContext();
				SubExpressions[0] = new BoolValueWithLocalContext(cond, local_ctx);

				if (local_ctx != null)
					ctx.PushLocalContext(local_ctx);
				try
				{
					Value v = TryEliminateConstSubExpressions(ctx, cond ? SubExpressions[1] : SubExpressions[2]);
					if (v != null)
						return v;

					if (cond)
						SubExpressions[2] = null;
					else
						SubExpressions[1] = null;
				}
				finally
				{
					if (local_ctx != null)
						ctx.PopLocalContext();
				}

				return null;
			}

			class BoolValueWithLocalContext : BoolValue
			{
				public BoolValueWithLocalContext(bool val, IVariableValueResolver local_ctx)
					: base(val)
				{
					m_LocalCtx = local_ctx;
				}

				public override IVariableValueResolver GetLocalContext()
				{
					return m_LocalCtx;
				}

				private IVariableValueResolver m_LocalCtx;
			}

			protected internal override Value RecursiveCollectUnresolvedVariables(IEvalContext ctx)
			{
				Expression expr_ctx, expr_no_ctx;
				if (SubExpressions[0].RecursiveCollectUnresolvedVariables(ctx).ToBool())
				{
					expr_ctx = SubExpressions[1];
					expr_no_ctx = SubExpressions[2];
				}
				else
				{
					expr_ctx = SubExpressions[2];
					expr_no_ctx = SubExpressions[1];
				}

				if (expr_ctx != null)
				{
					IVariableValueResolver local_ctx = SubExpressions[0].GetLocalContext();
					if (local_ctx == null)
					{
						expr_ctx.RecursiveCollectUnresolvedVariables(ctx);
					}
					else
					{
						ctx.PushLocalContext(local_ctx);
						try
						{
							expr_ctx.RecursiveCollectUnresolvedVariables(ctx);
						}
						finally
						{
							ctx.PopLocalContext();
						}
					}
				}

				if (expr_no_ctx != null)
					expr_no_ctx.RecursiveCollectUnresolvedVariables(ctx);

				return new BoolValue(false);
			}
		}

		class OpConcat : Expression
		{
			public OpConcat(Expression operand0, Expression operand1) : base(operand0, operand1) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				return new StringValue(SubExpressions[0].Evaluate(ctx).ToString() + SubExpressions[1].Evaluate(ctx).ToString());
			}
		}

		class RegexMatchLocalContext : IVariableValueResolver
		{
			public const string REGEX_GROUP_VARNAME_PREFIX = "$";

			public RegexMatchLocalContext(int[] group_numbers, string[] group_names, GroupCollection group_collection)
			{
				m_GroupNumbers = group_numbers;
				m_GroupNames = group_names;
				m_GroupCollection = group_collection;
			}

			public virtual Value GetVariableValue(Variable variable)
			{
				string name = variable.Name;
				if (name.Length <= REGEX_GROUP_VARNAME_PREFIX.Length || !name.StartsWith(REGEX_GROUP_VARNAME_PREFIX))
					return null;
				name = name.Substring(REGEX_GROUP_VARNAME_PREFIX.Length);
				Group group;
				if (Char.IsDigit(name[0]))
				{
					try
					{
						int idx = Convert.ToInt32(name);
						if (idx < 0 || idx >= m_GroupNumbers.Length)
						{
							// someone might have used a group name like "30" when we have only 4 groups...
							group = m_GroupCollection[name];
						}
						else
						{
							// someone might have used a group name like "30" when we have only 4 groups...
							// In this case the item "30" is placed somewhere at the end of our m_GroupNumbers array.
							if (m_GroupNumbers[idx] != idx)
								return null;
							group = m_GroupCollection[idx];
							if (!group.Success)
								// The group exists but capturing it wasn't successful.
								return m_UnsuccessfulGroupCaptureValue;
							return new StringValue(group.Value);
						}
					}
					catch (System.Exception)
					{
						group = m_GroupCollection[name];
					}
				}
				else
				{
					group = m_GroupCollection[name];
				}

				if (group.Success)
					return new StringValue(group.Value);
				if (m_GroupNameSet == null)
					m_GroupNameSet = new MySet<string>(m_GroupNames);
				if (!m_GroupNameSet.Contains(name))
					return null;
				// The group exists but it capturing it wasn't successful.
				return m_UnsuccessfulGroupCaptureValue;
			}

			private static Value m_UnsuccessfulGroupCaptureValue = new StringValue("");

			private GroupCollection m_GroupCollection;
			int[] m_GroupNumbers;
			string[] m_GroupNames;


			class MySet<T>
			{
				public MySet()
				{
					m_Dict = new Dictionary<T, object>();
				}

				public MySet(T[] items) : this()
				{
					foreach (T item in items)
						m_Dict[item] = null;
				}

				public bool Contains(T item)
				{
					return m_Dict.ContainsKey(item);
				}

				IDictionary<T, object> m_Dict;
			}

			// This should be an ISet<string> but this must compile with .Net 2.0 so I simulate it with a dictionary. Really sad.
			private MySet<string> m_GroupNameSet;
		}

		class OpRegexMatch : Expression
		{
			public OpRegexMatch(Expression operand0, Expression operand1, Regex regex, bool invert_result_value) : base(operand0, operand1)
			{
				m_Regex = regex;
				m_InvertResultValue = invert_result_value;
			}

			public override Value Evaluate(IEvalContext ctx)
			{
				Match match = m_Regex.Match(SubExpressions[0].Evaluate(ctx).ToString());
				if (!match.Success)
				{
					m_LocalContext = null;
					return new BoolValue(m_InvertResultValue);
				}

				m_LocalContext = new RegexMatchLocalContext(m_Regex.GetGroupNumbers(), m_Regex.GetGroupNames(), match.Groups);
				return new BoolValue(!m_InvertResultValue);
			}

			public override IVariableValueResolver GetLocalContext()
			{
				return m_LocalContext;
			}

			protected internal override Value RecursiveCollectUnresolvedVariables(IEvalContext ctx)
			{
				SubExpressions[0].RecursiveCollectUnresolvedVariables(ctx);
				SubExpressions[1].RecursiveCollectUnresolvedVariables(ctx);
				Match m = m_Regex.Match("");
				m_LocalContext = new RegexMatchLocalContext(m_Regex.GetGroupNumbers(), m_Regex.GetGroupNames(), m.Groups);
				return new BoolValue(!m_InvertResultValue);
			}

			private Regex m_Regex;
			private bool m_InvertResultValue;
			private IVariableValueResolver m_LocalContext;
		}

		class OpUpperCase : Expression
		{
			public OpUpperCase(Expression operand) : base(operand) {}
			public override Value Evaluate(IEvalContext ctx)
			{
				return new StringValue(SubExpressions[0].Evaluate(ctx).ToString().ToUpper());
			}
		}

		class OpLowerCase : Expression
		{
			public OpLowerCase(Expression operand) : base(operand) { }
			public override Value Evaluate(IEvalContext ctx)
			{
				return new StringValue(SubExpressions[0].Evaluate(ctx).ToString().ToLower());
			}
		}

		class OpLeadingCapitalCase : Expression
		{
			public OpLeadingCapitalCase(Expression operand) : base(operand) { }
			public override Value Evaluate(IEvalContext ctx)
			{
				string s = SubExpressions[0].Evaluate(ctx).ToString();
				return new StringValue(Char.ToUpper(s[0]) + s.Substring(1).ToLower());
			}
		}
	}
}
