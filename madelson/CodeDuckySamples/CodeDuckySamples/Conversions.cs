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

namespace CodeDucky
{
    public static class TypeHelpers
    {
        public static bool IsCastableTo(this Type from, Type to)
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
            if (CastCache.TryGetCachedValue(key, out cachedValue))
            {
                return cachedValue;
            }

            bool result;
            try
            {
                Expression.Convert(Expression.Parameter(from), to);
                result = true;
            }
            catch (Exception)
            {
                result = false;
            }

            CastCache.UpdateCache(key, result);
            return result;
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
                //return cachedValue;
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
            var list = new List<TTo>();
            var flags = CSharpBinderFlags.ResultDiscarded;
            var name = "Add";
            IEnumerable<Type> arg = null;
            var context = typeof(TypeHelpers);
            var secondArgumentFlags = typeof(TFrom).IsPrimitive || !typeof(TFrom).IsValueType || typeof(TFrom) == typeof(decimal)
                ? CSharpArgumentInfoFlags.UseCompileTimeType | CSharpArgumentInfoFlags.Constant
                : CSharpArgumentInfoFlags.UseCompileTimeType;
            var args = new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null), CSharpArgumentInfo.Create(secondArgumentFlags, null), };
            var binder = Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(flags, name, arg, context, args);
            var callSite = System.Runtime.CompilerServices.CallSite<System.Action<System.Runtime.CompilerServices.CallSite, System.Object, TFrom>>.Create(binder);
            callSite.Target.Invoke(callSite, list, default(TFrom));

            //dynamic helper = new ImplicitCastHelper<TTo>();
            //helper.Noop(default(TFrom));
            //CSharpBinderFlags.
            //var list = new List<TFrom>();
            //var args = new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null), CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null), };
            //var binder = CSharpBinder.InvokeMember(CSharpBinderFlags.None, "Add", Type.EmptyTypes, typeof(TypeHelpers), args);
            //var callSite = CallSite<Action<CallSite, object, TTo>>.Create(binder);
            //callSite.Target(callSite, list, default(TTo));
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
