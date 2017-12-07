using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Lens
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Original code found here: https://stackoverflow.com/a/16336563 </remarks>
    public static class Lens
    {
        /// <summary>
        /// Perform an immutable persistent set on a sub
        /// property of the object. The object is not
        /// mutated rather a copy of the object with
        /// the required change is returned.
        /// </summary>
        /// <typeparam name="ConvertedTo">type of the target object</typeparam>
        /// <typeparam name="V">type of the value to be set</typeparam>
        /// <param name="instance">the target object</param>
        /// <param name="memberInfo">the list of property names composing the property path</param>
        /// <param name="valueFunc">the value to assign to the property</param>
        /// <returns>A new object with the required change implemented</returns>
        private static T _set<T, V>(this T instance, List<MemberInfo> memberInfo, Func<V, V> valueFunc)
            where T : class
        {

            var member = memberInfo.First();
            var rest = memberInfo.Skip(1).ToList();

            var clone = ShallowClone(instance);

            var value = memberInfo.Count == 1 ? 
                valueFunc((V)GetProperty(clone, member)) : 
                GetProperty(clone, member)._set(rest, valueFunc);

            SetProperty(clone, member, value);

            return clone;
        }

        /// <summary>
        /// Returns a copy of the instance with the given value assigned. 
        /// Note that this method uses reflection and might not be suitable for performance critical tasks.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="instance">Instance to return modified copy of.</param>
        /// <param name="propertyChain">Where to assign the value. Must be a property chain expression, i.e. a => a.B.C.D</param>
        /// <param name="value">The value to assign.</param>
        /// <returns></returns>
        public static T Set<T, V>(this T instance, Expression<Func<T, V>> propertyChain, V value)
            where T : class, IRecord
        {
            return instance.Set(propertyChain, _ => value);
        }

        /// <summary>
        /// Returns a copy of the instance with the given value assigned. 
        /// Note that this method uses reflection and might not be suitable for performance critical tasks.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="instance">Instance to return modified copy of.</param>
        /// <param name="propertyChain">Where to assign the value. Must be a property chain expression, i.e. a => a.B.C.D</param>
        /// <param name="valueFunc">A function that takes the old value and returns the new value.</param>
        /// <returns></returns>
        public static T Set<T, V>(this T instance, Expression<Func<T, V>> propertyChain, Func<V, V> valueFunc)
            where T : class, IRecord
        {
            T newInstance;
            // If the property chain looks like p => p then we pass it directly into valueFunc.
            if (propertyChain.Body.NodeType == ExpressionType.Parameter)
            {
                // Ugly, ugly, hack to cast T into V and back.
                newInstance = (T)(object)valueFunc((V)(object)instance);
            }
            else // Otherwise if it looks like p => p.Prop1.PropA then we do more involved stuff.
            {
                newInstance = instance._set(
                    GetMemberInfoChain(propertyChain),
                    valueFunc);
            }

            if (newInstance is IState state)
            {
                DebugEx.Assert(state?.IsValid() != false);
            }
            return newInstance;
        }

        private static void SetProperty<T, V>(T instance, MemberInfo member, V value)
            where T : class
        {
            ((PropertyInfo)member).SetValue(instance, value);
            if (instance is IState state)
            {
                DebugEx.Assert(state.IsValid());
            }
        }

        private static object GetProperty<T>(T instance, MemberInfo member)
            where T : class
        {
            return ((PropertyInfo)member).GetValue(instance);
        }

        private static readonly MethodInfo _memberwiseClone = 
            typeof(object).GetMethod(nameof(MemberwiseClone), BindingFlags.Instance | BindingFlags.NonPublic);

        private static T ShallowClone<T>(T instance) => 
            (T)_memberwiseClone.Invoke(instance, new object[0]);

        /// <summary>
        /// </summary>
        /// <remarks>Original code found here: https://stackoverflow.com/a/1667533 </remarks>
        public static List<MemberInfo> GetMemberInfoChain<T, P>(Expression<Func<T, P>> expr)
        {
            MemberExpression me;
            switch (expr.Body.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    var ue = expr.Body as UnaryExpression;
                    me = (ue?.Operand) as MemberExpression;
                    break;
                default:
                    me = expr.Body as MemberExpression;
                    break;
            }

            var memberInfo = new List<MemberInfo>();
            while (me != null)
            {
                var member = me.Member;
                memberInfo.Add(member);
                me = me.Expression as MemberExpression;
            }

            memberInfo.Reverse();
            return memberInfo;
        }
    }

    /// <summary>
    /// Represents an immutable type.
    /// </summary>
    public interface IRecord
    {
    }
}
