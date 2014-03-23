using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace CodeDucky
{
    #region ---- Core interfaces ----
    public interface ICache
    {
        /// <summary>
        /// Invokes the given method, using a cached result if possible. If a result is not cached, the computed result
        /// is cached using the given policy. For example, use as:
        /// <code>
        /// ICache cache = ...
        /// string path = ...
        /// var text = cache.InvokeCached(() => File.ReadAllText(path), new CachePolicy(...));
        /// </code>
        /// </summary>
        TResult InvokeCached<TResult>(Expression<Func<TResult>> expression, CachePolicy policy);
        /// <summary>
        /// Invokes the given method, using a cached result if possible. If a result is not cached, the computed result
        /// is cached using the given policy. For example, use as:
        /// <code>
        /// ICache cache = ...
        /// string path = ...
        /// var text = cache.InvokeCached(() => File.ReadAllText(path), new CachePolicy(...));
        /// </code>
        /// </summary>
        Task<TResult> InvokeCachedAsync<TResult>(Expression<Func<Task<TResult>>> expression, CachePolicy policy);
    }
    #endregion

    #region ---- Cache policy ----
    public sealed class CachePolicy 
    {
        private readonly TimeSpan expiresAfter;
        private readonly bool renewLeaseOnAccess;

        public CachePolicy(TimeSpan expiresAfter, bool renewLeaseOnAccess = false)
        {
            this.expiresAfter = expiresAfter;
            this.renewLeaseOnAccess = renewLeaseOnAccess;
        }

        public TimeSpan ExpiresAfter { get { return this.expiresAfter; } }
        /// <summary>
        /// If specified, each read of the item from the cache will reset the expiration time
        /// </summary>
        public bool RenewLeaseOnAccess { get { return this.renewLeaseOnAccess; } }
    }
    #endregion

    #region ---- Cache key ----
    public sealed class CacheKeyBuilder
    {
        private static readonly string NullString = Guid.NewGuid().ToString();
        private readonly StringBuilder builder = new StringBuilder();

        /// <summary>
        /// Adds the given value to the key
        /// </summary>
        public CacheKeyBuilder By(object value)
        {
            this.builder.Append('{'); // wrap each value in curly braces
            if (value == null)
            {
                this.builder.Append(NullString);
            }

            DateTime? dateTimeValue;
            IConvertible convertibleValue;
            Type typeValue;
            IEnumerable enumerableValue;
            ICacheKey cacheKeyValue;

            // DateTime is convered by IConvertible, but the default ToString() implementation
            // doesn't have enough granularity to distinguish between unequal DateTimes
            if ((dateTimeValue = value as DateTime?).HasValue) 
            {
                this.builder.Append(dateTimeValue.Value.Ticks);
            }
            else if ((convertibleValue = value as IConvertible) != null)
            {
                this.builder.Append(convertibleValue.ToString(CultureInfo.InvariantCulture));
            }
            else if ((typeValue = value as Type) != null)
            {
                this.builder.Append(typeValue.GUID);
            }
            else if ((enumerableValue = value as IEnumerable) != null)
            {
                foreach (object element in enumerableValue)
                {
                    this.By(element);
                }
            }
            else if ((cacheKeyValue = value as ICacheKey) != null)
            {
                cacheKeyValue.BuildCacheKey(this);
            }
            else
		    {
                throw new ArgumentException(value.GetType() + " cannot be a cache key");
		    }

            this.builder.Append('}');
            return this;
        }

        public override string ToString()
        {
            return this.builder.ToString();
        }
    }

    /// <summary>
    /// This interface allows custom types to be cache keys
    /// </summary>
    public interface ICacheKey
    {
        void BuildCacheKey(CacheKeyBuilder builder);
    }
    #endregion

    #region ---- Base implementation ----
    public abstract class CacheBase : ICache
    {
        public TResult InvokeCached<TResult>(Expression<Func<TResult>> expression, CachePolicy policy)
        {
            Throw.IfNull(expression, "expression");
            Throw.IfNull(policy, "policy");

            string cacheKey;
            MethodInfo method;
            object instance;
            object[] arguments;
            ParseExpression(expression, out cacheKey, out method, out instance, out arguments);

            TResult cachedValue;
            if (this.TryFind(cacheKey, policy, out cachedValue))
            {
                return cachedValue;
            }

            var computedValue = (TResult)method.Invoke(instance, arguments);
            ThrowIfNotCacheable(computedValue);
            this.Add(cacheKey, computedValue, policy);
            return computedValue;
        }

        public async Task<TResult> InvokeCachedAsync<TResult>(Expression<Func<Task<TResult>>> expression, CachePolicy policy)
        {
            Throw.IfNull(expression, "expression");
            Throw.IfNull(policy, "policy");

            string cacheKey;
            MethodInfo method;
            object instance;
            object[] arguments;
            ParseExpression(expression, out cacheKey, out method, out instance, out arguments);

            TResult cachedValue;
            if (this.TryFind(cacheKey, policy, out cachedValue))
            {
                return cachedValue;
            }

            var computedValue = await ((Task<TResult>)method.Invoke(instance, arguments)).ConfigureAwait(false);
            ThrowIfNotCacheable(computedValue);
            this.Add(cacheKey, computedValue, policy);
            return computedValue;
        }

        private static void ParseExpression(LambdaExpression expression, out string cacheKey, out MethodInfo method, out object instance, out object[] arguments)
        {
            var methodCall = expression.Body as MethodCallExpression;
            Throw<ArgumentException>.If(methodCall == null, "expression: body must be a method call");

            method = methodCall.Method;
            instance = methodCall.Object != null ? GetValue(methodCall.Object) : null;
            arguments = new object[methodCall.Arguments.Count];

            var keyBuilder = new CacheKeyBuilder();
            keyBuilder.By(method.DeclaringType).By(method.MetadataToken).By(method.GetGenericArguments()).By(instance);
            for (var i = 0; i < methodCall.Arguments.Count; ++i)
            {
                keyBuilder.By(arguments[i] = GetValue(methodCall.Arguments[i]));
            }
            cacheKey = keyBuilder.ToString();
        }

        private static void ThrowIfNotCacheable(object value)
        {
            if (value != null
                && !(value is IConvertible)
                && !(value is ICacheable))
            {
                throw new InvalidOperationException("value of type " + value.GetType() + " is safe to cache");
            }
        }

        private static object GetValue(Expression expression)
        {
            switch (expression.NodeType)
            {
                // we special-case constant and member access because these handle the majority of common cases.
                // For example, passing a local variable as an argument translates to a field reference on the closure
                // object in expression land
                case ExpressionType.Constant:
                    return ((ConstantExpression)expression).Value;
                case ExpressionType.MemberAccess:
                    var memberExpression = (MemberExpression)expression;
                    var instance = memberExpression.Expression != null ? GetValue(memberExpression.Expression) : null;
                    var field = memberExpression.Member as FieldInfo;
                    return field != null
                        ? field.GetValue(instance)
                        : ((PropertyInfo)memberExpression.Member).GetValue(instance);
                default:
                    // this should always work if the expression CAN be evaluated (it can't if it references unbound parameters, for example)
                    // however, compilation is slow so the cases above provide valuable performance
                    var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object)));
                    return lambda.Compile()();
            }
        }

        #region ---- Abstract methods ----
        protected abstract bool TryFind<TResult>(string key, CachePolicy policy, out TResult value);
        protected abstract void Add<TResult>(string key, TResult value, CachePolicy policy);
        #endregion
    }

    public interface ICacheable
    {
    }
    #endregion

    #region ---- System.Runtime.Caching implementation ----
    public sealed class Cache : CacheBase
    {
        private readonly MemoryCache cache = new MemoryCache(typeof(Cache).ToString());

        protected override bool TryFind<TResult>(string key, CachePolicy policy, out TResult value)
        {
            var item = this.cache.GetCacheItem(key);
            if (item != null)
            {
                value = (TResult)item.Value;
                return true;
            }

            value = default(TResult);
            return false;
        }

        protected override void Add<TResult>(string key, TResult value, CachePolicy policy)
        {
            var cacheItem = new CacheItem(key, value);
            var cachePolicy = new CacheItemPolicy();
            if (policy.RenewLeaseOnAccess)
            {
                cachePolicy.SlidingExpiration = policy.ExpiresAfter;
            }
            else
            {
                cachePolicy.AbsoluteExpiration = DateTimeOffset.UtcNow + policy.ExpiresAfter;
            }

            this.cache.Add(cacheItem, cachePolicy);
        }
    }
    #endregion
}
