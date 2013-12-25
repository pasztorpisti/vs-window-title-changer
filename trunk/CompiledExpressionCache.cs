using System;
using VSWindowTitleChanger.ExpressionEvaluator;

namespace VSWindowTitleChanger
{
	struct CompiledExpression
	{
		public ExpressionEvaluator.Expression expression;
		public Exception compile_error;
	}

	class CompiledExpressionCache
	{
		IVariableValueResolver m_CompileTimeConstants;
		Cache<string, CompiledExpression> m_Cache;

		public CompiledExpressionCache(IVariableValueResolver compile_time_constants, int max_size)
		{
			m_Cache = new Cache<string, CompiledExpression>(CompileExpression, max_size);
			m_CompileTimeConstants = compile_time_constants;
		}

		public CompiledExpression GetEntry(string expression_str)
		{
			return m_Cache.GetEntry(expression_str);
		}

		CompiledExpression CompileExpression(string expression_string)
		{
			CompiledExpression compiled = new CompiledExpression();
			try
			{
				Parser expression_parser = new Parser(expression_string, m_CompileTimeConstants);
				compiled.expression = expression_parser.Parse();
			}
			catch (Exception ex)
			{
				compiled.compile_error = ex;
			}
			return compiled;
		}
	}
}
