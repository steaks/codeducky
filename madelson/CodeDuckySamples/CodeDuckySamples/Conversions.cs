using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CSharpBinder = Microsoft.CSharp.RuntimeBinder.Binder;

// http://stackoverflow.com/questions/292437/determine-if-a-reflected-type-can-be-cast-to-another-reflected-type
// http://stackoverflow.com/questions/2224266/how-to-tell-if-type-a-is-implicitly-convertible-to-type-b/2224421#2224421
// http://stackoverflow.com/questions/7042314/can-i-check-if-a-variable-can-be-cast-to-a-specified-type

namespace CodeDucky
{
    public static class TypeHelpers
    {
        public static bool IsCastableTo(this Type from, Type to)
        {
            Throw.IfNull(from, "from");
            Throw.IfNull(to, "to");

            // explicit conversion always works if to : from OR if 
            // there's an implicit conversion
            if (from.IsAssignableFrom(to) || from.IsImplicitlyCastableTo(to))
            {
                return true;
            }

            var key = new KeyValuePair<Type, Type>(from, to);
            bool cachedValue;
            if (CastCache.TryGetCachedValue(key, out cachedValue))
            {
                //return cachedValue;
            }

            // for nullable types, we can simply strip off the nullability and evaluate the underyling types
            var underlyingFrom = Nullable.GetUnderlyingType(from);
            var underlyingTo = Nullable.GetUnderlyingType(to);
            if (underlyingFrom != null || underlyingTo != null)
            {
                return (underlyingFrom ?? from).IsCastableTo(underlyingTo ?? to);
            }

            bool result;

            if (from.IsValueType)
            {
                try
                {
                    ReflectionHelpers.GetMethod(() => AttemptExplicitCast<object, object>())
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(from, to)
                        .Invoke(null, new object[0]);
                    result = true;
                }
                catch (TargetInvocationException ex)
                {
                    result = !(
                        ex.InnerException is RuntimeBinderException
                        && Regex.IsMatch(ex.InnerException.Message, @"^Cannot convert type '.*' to '.*'$")
                    );
                }
            }
            else
            {
                // if the from type is null, the dynamic logic below won't be of any help because either both types are nullable and thus
                // a runtime cast of null => null will succeed OR we get a runtime failure related to the inability to cast null to the desired
                // type, which may or may not indicate an actual issue. thus, we do the work manually
                result = from.IsNonValueTypeExplicitlyCastableTo(to);
            }
            
            CastCache.UpdateCache(key, result);
            return result;
        }

        private static bool IsNonValueTypeExplicitlyCastableTo(this Type from, Type to)
        {
            if (to.IsAssignableFrom(from))
            {
                // if to : from, then a cast might succeed so it's allowed
                return true;
            }

            if ((to.IsInterface && !from.IsSealed)
                || (from.IsInterface && !to.IsSealed))
            {
                // any non-sealed type can be cast to any interface since the runtime type MIGHT implement
                // that interface. The reverse is also true; we can cast to any non-sealed type from any interface
                // since the runtime type that implements the interface might be a derived type of to.
                return true;
            }

            // arrays are complex because of array covariance (see http://msmvps.com/blogs/jon_skeet/archive/2013/06/22/array-covariance-not-just-ugly-but-slow-too.aspx).
            // Thus, we have to allow for var x = (IEnumerable<string>)new object[0]; and var x = (object[])default(IEnumerable<string>);
            var arrayType = from.IsArray && !from.GetElementType().IsValueType ? from
                : to.IsArray && !to.GetElementType().IsValueType ? to
                : null;
            if (arrayType != null)
            {
                var genericInterfaceType = from.IsInterface && from.IsGenericType ? from
                    : to.IsInterface && to.IsGenericType ? to
                    : null;
                if (genericInterfaceType != null)
                {
                    return arrayType.GetInterfaces()
                        .Any(i => i.IsGenericType
                            && i.GetGenericTypeDefinition() == genericInterfaceType.GetGenericTypeDefinition()
                            && i.GetGenericArguments().Zip(to.GetGenericArguments(), (ia, ta) => ta.IsAssignableFrom(ia) || ia.IsAssignableFrom(ta)).All(b => b));
                }
            }

            // look for explicit conversions
            const BindingFlags conversionFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var conversionMethods = from.GetMethods(conversionFlags).Concat(to.GetMethods(conversionFlags))
                .Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit")
                    && m.Attributes.HasFlag(MethodAttributes.SpecialName)
                    && m.GetParameters().Length == 1 
                    && (
                        m.GetParameters()[0].ParameterType.IsAssignableFrom(from)
                        || from.IsAssignableFrom(m.GetParameters()[0].ParameterType)
                    )
                );

            if (to.IsPrimitive && typeof(IConvertible).IsAssignableFrom(to))
            {
                return conversionMethods.Any(m => m.ReturnType.IsCastableTo(to));
            }

            return conversionMethods.Any(m => m.ReturnType == to);
        }

        private static void AttemptExplicitCast<TFrom, TTo>()
        {
            var binder = CSharpBinder.Convert(CSharpBinderFlags.ConvertExplicit, typeof(TTo), typeof(TypeHelpers));
            var callSite = CallSite<Func<CallSite, TFrom, TTo>>.Create(binder);
            callSite.Target(callSite, default(TFrom));
        }

        public static bool IsImplicitlyCastableTo(this Type from, Type to)
        {
            Throw.IfNull(from, "from");
            Throw.IfNull(to, "to");

            // not strictly necessary, but speeds things up and avoids polluting the cache
            if (to.IsAssignableFrom(from))
            {
                return true;
            }

            var key = new KeyValuePair<Type, Type>(from, to);
            bool cachedValue;
            if (ImplicitCastCache.TryGetCachedValue(key, out cachedValue))
            {
                return cachedValue;
            }

            bool result;
            try
            {
                // overload of GetMethod() from http://www.codeducky.org/10-utilities-c-developers-should-know-part-two/ 
                // that takes Expression<Action>
                ReflectionHelpers.GetMethod(() => AttemptImplicitCast<object, object>())
                    .GetGenericMethodDefinition()
                    .MakeGenericMethod(from, to)
                    .Invoke(null, new object[0]);
                result = true;
            }
            catch (TargetInvocationException ex)
            {
                result = !(
                    ex.InnerException is RuntimeBinderException
                    && Regex.IsMatch(ex.InnerException.Message, @"^The best overloaded method match for 'System.Collections.Generic.List<.*>.Add(.*)' has some invalid arguments$")
                );
            }

            ImplicitCastCache.UpdateCache(key, result);
            return result;
        }

        private static void AttemptImplicitCast<TFrom, TTo>()
        {
            // based on the IL produced by:
            // dynamic list = new List<StringSplitOptions>();
            // list.Add(default(decimal));

            var list = new List<TTo>();
            var binder = Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(
                flags: CSharpBinderFlags.ResultDiscarded, 
                name: "Add", 
                typeArguments: null, 
                context: typeof(TypeHelpers),
                argumentInfo: new[] 
                { 
                    CSharpArgumentInfo.Create(flags: CSharpArgumentInfoFlags.None, name: null), 
                    CSharpArgumentInfo.Create(
                        flags: typeof(TFrom).IsPrimitive || !typeof(TFrom).IsValueType || typeof(TFrom) == typeof(decimal)
                            ? CSharpArgumentInfoFlags.UseCompileTimeType | CSharpArgumentInfoFlags.Constant
                            : CSharpArgumentInfoFlags.UseCompileTimeType, 
                        name: null
                    ),
                }
            );
            var callSite = CallSite<Action<CallSite, object, TFrom>>.Create(binder);
            callSite.Target.Invoke(callSite, list, default(TFrom));
        }

        private class ImplicitCastHelper<TTo>
        {
            public void Noop(TTo value) { }
        }

        #region ---- Caching ----
        private const int MaxCacheSize = 5000;
        private static readonly Dictionary<KeyValuePair<Type, Type>, bool> CastCache = new Dictionary<KeyValuePair<Type, Type>, bool>(),
            ImplicitCastCache = new Dictionary<KeyValuePair<Type, Type>, bool>();

        private static bool TryGetCachedValue<TKey, TValue>(this Dictionary<TKey, TValue> cache, TKey key, out TValue value)
        {
            lock (cache.As<ICollection>().SyncRoot)
            {
                return cache.TryGetValue(key, out value);
            }
        }

        private static void UpdateCache<TKey, TValue>(this Dictionary<TKey, TValue> cache, TKey key, TValue value)
        {
            lock (cache.As<ICollection>().SyncRoot)
            {
                if (cache.Count > MaxCacheSize)
                {
                    cache.Clear();
                }
                cache[key] = value;
            }
        }
        #endregion
    }
}
