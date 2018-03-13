using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lens
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Original code found here: https://stackoverflow.com/a/16336563 </remarks>
    public static class Lens
    {
        [ThreadStatic]
        internal static bool _validationPaused;

        public static bool ValidationPaused => _validationPaused;

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
        private static T _set<T, V>(this T instance, List<MethodAndParameters> memberInfo, Func<V, V> valueFunc, bool validate)
            where T : class
        {
            var rest = memberInfo.Skip(1).ToList();

            if (instance.GetType().IsImplementationOf(typeof(IImmutableList<>)))
            {
                var index = (int)memberInfo[0].Parameters[0].DynamicInvoke();

                object value;
                if (memberInfo.Count == 1)
                {
                    value = valueFunc((V)GetProperty(instance, memberInfo[0]));
                }
                else
                {
                    value = GetProperty(instance, memberInfo[0])._set(rest, valueFunc, validate);
                }
                

                var setItem = instance.GetType().GetMethod("SetItem");
                
                var newList = setItem.Invoke(instance, new[] { index, value });

                return (T)newList;
            }
            else
            {
                var member = memberInfo.First();

                var clone = ShallowClone(instance);

                var value = memberInfo.Count == 1 ?
                    valueFunc((V)GetProperty(clone, member)) :
                    GetProperty(clone, member)._set(rest, valueFunc, validate);

                SetProperty(clone, member, value, validate);

                return clone;
            }
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
            return _set(instance, propertyChain, valueFunc, true);
        }

        public static T Set<T, V>(this T instance, Pav<T, V> propertyAndValue)
            where T : class, IRecord
        {
            return _set(instance, propertyAndValue.PropertyChain, propertyAndValue.ValueFunc, true);
        }

        private static T _set<T, V>(this T instance, Expression<Func<T, V>> propertyChain, Func<V, V> valueFunc, bool validate) 
            where T : class, IRecord
        {
            T newInstance;

            var expression = propertyChain.Body;
            bool isParameter = expression.NodeType == ExpressionType.Parameter;
            while (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                expression = ((UnaryExpression)expression).Operand;
                isParameter = expression.NodeType == ExpressionType.Parameter;
            }

            // If the property chain looks like p => p then we pass it directly into valueFunc.
            if (isParameter)
            {
                // Ugly, ugly, hack to cast T into V and back.
                newInstance = (T)(object)valueFunc((V)(object)instance);

                if (!_validationPaused && newInstance is IState state && state?.IsValid() == false)
                {
                    throw new InvalidStateException(state);
                }
            }
            else // Otherwise if it looks like p => p.Prop1.PropA then we do more involved stuff.
            {
                newInstance = instance._set(
                    GetMemberInfoChain(propertyChain),
                    valueFunc,
                    validate);
            }

            return newInstance;
        }

        private static Expression<Func<T, V>> StripConverters<T, V>(Expression<Func<T, V>> propertyChain)
        {
            Expression expression = propertyChain.Body;
            while (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return Expression.Lambda<Func<T, V>>(expression, propertyChain.Parameters);
        }

        /// <summary>
        /// Performs many set operations and only performs validation after they have all been completed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance">Instance to return modified copy of.</param>
        /// <param name="changes">Collection of changes to be made. Use the <see cref="Pav{T, V}"/> implementation of <see cref="IPropertyAndValue{T}"/>.</param>
        /// <returns></returns>
        public static T SetMany<T>(this T instance, params IPropertyAndValue<T>[] changes)
            where T : class, IRecord
        {
            var newInstance = instance;
            foreach (var change in changes)
            {
                newInstance = newInstance._set(change.PropertyChain, change.ValueFunc, false);
            }

            var memberInfos = changes.Select(item => GetMemberInfoChain(item.PropertyChain));
            foreach (var memberInfo in memberInfos)
            {
                instance.Validate(memberInfo);
            }

            return newInstance;
        }

        /// <summary>
        /// Prevents <see cref="IState.IsValid"/> from being called inside <see cref="Lens.Set"/>.
        /// </summary>
        public static T ChangeWithoutValidation<T>(this T instance, Func<T, T> changes, Action<T> validate = null) 
            where T : class
        {
            var previousValidation = _validationPaused;
            _validationPaused = true;
            try
            {
                var newInstance = changes?.Invoke(instance);
                _validationPaused = previousValidation;
                validate?.Invoke(newInstance);
                return newInstance;
            }
            finally
            {
                _validationPaused = previousValidation;
            }
        }

        private static void Validate<T>(this T instance, List<MethodAndParameters> memberInfo) 
            where T : class
        {
            if (!_validationPaused && instance is IState state && !state.IsValid())
            {
                throw new InvalidStateException(state);
            }

            var rest = memberInfo.Skip(1).ToList();

            if (!rest.Any())
            {
                return;
            }

            if (instance.GetType().IsImplementationOf(typeof(IImmutableList<>)))
            {
                var index = (int)memberInfo[0].Parameters[0].DynamicInvoke();

                GetProperty(instance, memberInfo[0]).Validate(rest);
            }
            else
            {
                var member = memberInfo.First();

                GetProperty(instance, member).Validate(rest);

            }
        }

        private static void SetProperty<T, V>(T instance, MethodAndParameters member, V value, bool validate)
            where T : class
        {
            if (member.Member != null)
            {
                instance
                    .GetType()
                    .GetProperty(member.Member.Name)
                    .SetValue(instance, value);
            }
            else
            {
                var parameterValues = member.Parameters.Select(item => item.DynamicInvoke()).ToArray();

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

            if (!_validationPaused && validate && instance is IState state && !state.IsValid())
            {
                throw new InvalidStateException(state);
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
                var parameters = member.Parameters.Select(item => item.DynamicInvoke()).ToArray();
                try
                {
                    return member.Method.Invoke(instance, parameters);
                }
                catch (TargetInvocationException e)
                {
                    // We use this instead of "throw e.InnerException" to make sure we don't lose the stack trace.
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    return null;
                }
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
                    case ExpressionType.Convert:
                    case ExpressionType.ConvertChecked:
                        var a = (UnaryExpression)expressionPart;
                        expressionPart = a.Operand;
                        continue;
                    default:
                        throw new Exception();
                }
                
                expressionParts.Add(methodAndParameters);
            }

            throw new Exception();
        }

        /// <remarks>Orignal code found here: https://bradhe.wordpress.com/2010/07/27/how-to-tell-if-a-type-implements-an-interface-in-net/ </remarks>
        private static bool IsImplementationOf(this Type baseType, Type interfaceType) =>
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
}
