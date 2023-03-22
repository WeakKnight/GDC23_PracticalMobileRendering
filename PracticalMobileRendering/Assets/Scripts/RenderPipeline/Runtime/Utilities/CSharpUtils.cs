using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace PMRP
{
    public class CSharpUtils
    {
#if UNITY_EDITOR
        public static string FullPropertyName<T>(Expression<Func<T>> expression, int split = 1)
        {
            var memberExpression = expression.Body as MemberExpression;
            if (memberExpression == null)
            {
                var unaryExpression = expression.Body as UnaryExpression;
                if (unaryExpression != null && unaryExpression.NodeType == ExpressionType.Convert)
                    memberExpression = unaryExpression.Operand as MemberExpression;
            }

            var result = memberExpression.ToString();

            int indexCloseParenthesis = result.IndexOf(')');
            if (indexCloseParenthesis >= 0)
                result = result.Substring(indexCloseParenthesis + 2);

            while (split > 0)
            {
                result = result.Substring(result.IndexOf('.') + 1);
                split -= 1;
            }

            return result;
        }
#endif
    }
}