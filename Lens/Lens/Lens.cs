using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        private static T _set<T, V>(this T instance, List<MethodAndParameters> memberInfo, Func<V, V> valueFunc)
            where T : class
        {

            var member = memberInfo.First();
            var rest = memberInfo.Skip(1).ToList();

            var clone = instance.GetType().IsImplementationOf(typeof(IImmutableList<>)) ? 
                instance : 
                ShallowClone(instance);

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

        private static void SetProperty<T, V>(T instance, MethodAndParameters member, V value)
            where T : class
        {
            if (member.Member != null)
            {
                ((PropertyInfo)member.Member).SetValue(instance, value);
            }
            else
            {
                var parameterValues = member.Parameters.Select(item => item.DynamicInvoke()).ToArray();

                if (!instance.GetType().IsImplementationOf(typeof(IImmutableList<>)))
                {
                    var setItem = instance
                        .GetType()
                        .GetProperties()
                        .Single(item => item.GetIndexParameters()
                            .Select(parameter => parameter.ParameterType)
                            .SequenceEqual(
                                member.Parameters
                                    .Select(item2 => item2.Method.ReturnType)));

                    setItem.SetValue(instance, value, parameterValues);
                }
            }

            if (instance is IState state)
            {
                DebugEx.Assert(state.IsValid());
            }
        }

        private static object GetProperty<T>(T instance, MethodAndParameters member)
            where T : class
        {
            if (member.Member != null)
            {
                return ((PropertyInfo)member.Member).GetValue(instance);
            }
            else
            {
                return member.Method.Invoke(instance, member.Parameters.Select(item => item.DynamicInvoke()).ToArray());
            }
        }

        private static readonly MethodInfo _memberwiseClone = 
            typeof(object).GetMethod(nameof(MemberwiseClone), BindingFlags.Instance | BindingFlags.NonPublic);

        private static T ShallowClone<T>(T instance) => 
            (T)_memberwiseClone.Invoke(instance, new object[0]);

        /// <summary>
        /// </summary>
        /// <remarks>Original code found here: https://stackoverflow.com/a/1667533 </remarks>
        private static List<MethodAndParameters> GetMemberInfoChain<T, P>(Expression<Func<T, P>> expr)
        {

            var expressionPart = expr.Body;
            //switch (expr.Body.NodeType)
            //{
            //    case ExpressionType.Convert:
            //    case ExpressionType.ConvertChecked:
            //        var ue = expr.Body as UnaryExpression;
            //        expressionPart = (ue?.Operand) as MemberExpression;
            //        break;
            //}


            var expressionParts = new List<MethodAndParameters>();
            while (expressionPart != null)
            {
                MethodAndParameters methodAndParameters;
                switch (expressionPart.NodeType)
                {
                    case ExpressionType.Call:
                        var call = (MethodCallExpression)expressionPart;
                        if (call.Method.Name == "get_Item")
                        {
                            methodAndParameters = new MethodAndParameters(
                                call.Method,
                                call.Arguments.Select(item => Expression.Lambda(item).Compile()).ToArray());
                        }
                        else
                        {
                            throw new Exception();
                        }
                        expressionPart = call.Object;
                        break;
                    case ExpressionType.MemberAccess:
                        var member = (MemberExpression)expressionPart;
                        methodAndParameters = new MethodAndParameters(member.Member);
                        expressionPart = member.Expression;
                        break;
                    case ExpressionType.Parameter:
                        expressionParts.Reverse();
                        return expressionParts;
                    default:
                        throw new Exception();
                }
                
                expressionParts.Add(methodAndParameters);
            }

            throw new Exception();
        }

        /// <remarks>Orignal code found here: https://bradhe.wordpress.com/2010/07/27/how-to-tell-if-a-type-implements-an-interface-in-net/ </remarks>
        public static bool IsImplementationOf(this Type baseType, Type interfaceType) =>
            baseType.GetInterfaces().Any(item => item.Name == interfaceType.Name && item.Assembly.FullName == interfaceType.Assembly.FullName);

        private class MethodAndParameters
        {
            public MemberInfo Member { get; }
            public MethodInfo Method { get; }
            public Delegate[] Parameters { get; }

            public MethodAndParameters(MemberInfo member)
            {
                Member = member;
            }

            public MethodAndParameters(MethodInfo method, Delegate[] parameters = null)
            {
                Method = method;
                Parameters = parameters ?? new Delegate[0];
            }
        }
    }

    /// <summary>
    /// Represents an immutable type.
    /// </summary>
    public interface IRecord
    {
    }
}
