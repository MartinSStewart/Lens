using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Lens
{
    /// <summary>
    /// Container for a property chain and value.
    /// </summary>
    public class Pav<T, V> : IPropertyAndValue<T>
        where T : class, IRecord
    {
        public Expression<Func<T, V>> PropertyChain { get; }
        public Func<V, V> ValueFunc { get; }

        Expression<Func<T, object>> IPropertyAndValue<T>.PropertyChain => Cast(PropertyChain);

        Func<object, object> IPropertyAndValue<T>.ValueFunc => value => ValueFunc((V)value);

        public Pav(Expression<Func<T, V>> propertyChain, Func<V, V> valueFunc)
        {
            PropertyChain = propertyChain;
            ValueFunc = valueFunc;
        }

        public Pav(Expression<Func<T, V>> propertyChain, V value)
        {
            PropertyChain = propertyChain;
            ValueFunc = _ => value;
        }

        public static Expression<Func<T, object>> Cast(Expression<Func<T, V>> expression)
        {
            Expression converted = Expression.Convert(expression.Body, typeof(object));

            return Expression.Lambda<Func<T, object>>(converted, expression.Parameters);
        }
    }

    public interface IPropertyAndValue<T>
    {
        Expression<Func<T, object>> PropertyChain { get; }
        Func<object, object> ValueFunc { get; }
    }
}
