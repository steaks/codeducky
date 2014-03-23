Robust caching in .NET applications

<h2>Caching is hard</h2>
Caching is an excellent means of improving performance and reducing load on databases and other resources. However, it can be tricky to get right and very painful to debug when gotten wrong. Here are some of the potential pitfalls:

<strong>Key collisions</strong>
Most caching systems (for example the .NET framework's <a href=http://msdn.microsoft.com/en-us/library/system.runtime.caching(v=vs.110).aspx>System.Runtime.Caching APIs</a> rely on a key which is used for storing and retrieving cache values. However, it is typically up to the developer to generate this key in such a way that distinct values are mapped to distinct keys. If this is done incorrectly (or becomes incorrect due to subsequent code modifications), then we can end up with unnecessary cache misses or, far worse, false hits where the wrong value is returned. Here's an example:

<pre>
// let's say we write a function to compute our sales for a given product
double ComputeSales(int productId)
{
	// check the cache
    var cacheKey = "ComputeSales_" + productId;
	var cached = MemoryCache.Default.GetCacheItem(cacheKey);
	if (cached != null) 
	{
		return (double)cached.Value;
	}
	
	// code to do the actual computation goes here
	var sales = ...
	
	// cache the result
	var cacheItem = new CacheItem(cacheKey, sales);
	var policy = new CacheItemPolicy { ... };
	MemoryCache.Default.Add(cacheItem, policy);
}

// in the future, we update the API to:
double ComputeSales(int productId, DateTime? minDate = null, DateTime? maxDate = null)
{
    // if we forget to update the cache key creating appropriately, we introduce a transient bug where the wrong value is sometimes returned!
}
</pre>

<strong>Thread-safety</strong>
When caching objects in memory as with MemoryCache, it is vitally important that the cached values be thread-safe. Typically this means that cached objects should be immutable. This is a frequent issue when caching database results, since the entity objects in ORM frameworks like Entity Framework tend to be mutable and may even maintain references to an non-thread-safe database connection (e. g. EF does this to support lazy-loading of navigation properties).

<strong>Relying on cache removal</strong>
.NET's MemoryCache is not alone in offering an API for removing items from the cache. While this may seem useful, I consider it to be a trap, particularly in the case of web development. Developers who use this API can easily build systems which <i>rely</i> on this behavior for correctness. For example, consider a system which stores a user's preferences in a database. When the preferences are required, we read them from the database and then cache them in a MemoryCache. When the user updates her preferences, we update the database records and either clear or overwrite the old cache entry. This implementation may sound reasonable, and will likely work in most development/testing environments. However, if this gets deployed to a production environment where we have multiple servers running instances of the same application. In this environment, our cache clearing operation will only affect one server; if the next request goes to a different server, we'll serve up out-of-date preferences from the cache.

<strong>Relying on the existance of items in the cache</strong>
In my experience this is a less common mistake than the former issue, but it still comes up occasionally. Cache APIs that contain a Get(key) operation can be traps because they don't require developers to handle the cache miss case. Using this type of API, it is possible to write code which almost always works but exhibits occasional transient failures.

<h2>Building a better cache API</h2>
In an attempt to avoid some of these issues, I've built a "wrapper" API for MemoryCache (or any arbitrary caching system) which helps enforce correct usage. Here's the API:

<pre>
public interface ICache
{
	TResult InvokeCached<TResult>(Expression<Func<TResult>> expression, CachePolicy policy);
}

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
</pre>

Note that there is only 1 method which can be used to access the cache: InvokeCached. The signature looks a little odd, but when used the intent is quite clear. Here's an example:

<pre>
ICache cache = ...
string path = ...
var cachedText = cache.InvokeCached(() => File.ReadAllText(path), new CachePolicy(expiresAfter: TimeSpan.FromMinutes(30)));
</pre>

Essentially, InvokeCached expects to be passed an <a href="http://msdn.microsoft.com/en-us/library/bb335710(v=vs.110).aspx">Expression</a> representing a call to some method. The cache will then use the expression to both (1) build an appropriate cache key for the result and (2) invoke the actual computation if no result can be found for the key. Much of this work is done by a base implementation:

<pre>
public abstract class CacheBase : ICache
{
	public TResult InvokeCached<TResult>(Expression<Func<TResult>> expression, CachePolicy policy)
	{
		string cacheKey;
		MethodInfo method;
		object instance;
		object[] arguments;
		// extract the key as well as the items needed to invoke the expression from the arguments
		ParseExpression(expression, out cacheKey, out method, out instance, out arguments);

		// if the value is in the cache, return it
		TResult cachedValue;
		if (this.TryFind(cacheKey, policy, out cachedValue))
		{
			return cachedValue;
		}

		// compute the value
		var computedValue = (TResult)method.Invoke(instance, arguments);
		// check if if it's valid for caching
		ThrowIfNotCacheable(computedValue);
		// add it to the cache
		this.Add(cacheKey, computedValue, policy);
		return computedValue;
	}

	private static void ParseExpression(LambdaExpression expression, out string cacheKey, out MethodInfo method, out object instance, out object[] arguments)
	{
		// if the expression is of the form () => [instance.]SomeFunction(...), the body is a MethodCallExpression
		var methodCall = (MethodCallExpression)expression.Body;
		
		method = methodCall.Method;
		instance = methodCall.Object != null ? GetValue(methodCall.Object) : null;
		arguments = new object[methodCall.Arguments.Count];

		// build up a key for caching based on the method and it's parameters
		var keyBuilder = new CacheKeyBuilder();
		keyBuilder.By(method.DeclaringType).By(method.MetadataToken).By(method.GetGenericArguments()).By(instance);
		for (var i = 0; i < methodCall.Arguments.Count; ++i)
		{
			keyBuilder.By(arguments[i] = GetValue(methodCall.Arguments[i]));
		}
		cacheKey = keyBuilder.ToString();
	}

	protected abstract bool TryFind<TResult>(string key, CachePolicy policy, out TResult value);
	protected abstract void Add<TResult>(string key, TResult value, CachePolicy policy);
}
</pre>

This implementation makes use of several helper methods and classes. The first we hit is GetValue, which evaluates an expression. Here's a sample implementation:

<pre>
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
</pre>

The next utility is the CacheKeyBuilder. This class aids in generating a unique key string from a series of values. Here's a quick implementation:

<pre>
public sealed class CacheKeyBuilder
{
	private static readonly string NullString = Guid.NewGuid().ToString();
	private readonly StringBuilder builder = new StringBuilder();

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
</pre>

My builder implementation supports most simple types, Type, collections, and custom types (via ICacheKey). It also wraps each item in curly braces to help avoid collisions. That said, it's far from perfect. For example, it won't distinguish between an empty string and an empty enumerable. How complex you want to make the builder depends on your level of paranoia; even with this simple implementation, the chance of erroneous collision should be far lower than requiring each developer who uses caching to come up with their own key-generation scheme.

The final helper is ThrowIfNotCacheable. This attempts to handle the thread-safety issue mentioned above by doing a check on each cached object. Here's a sample implementation:

<pre>
private static void ThrowIfNotCacheable(object value)
{
	if (value != null
		&& !(value is IConvertible)
		&& !(value is ICacheable))
	{
		throw new InvalidOperationException("value of type " + value.GetType() + " is safe to cache");
	}
}

// this is just a marker interface that allows developers to denote that a custom type is safe for caching
public interface ICacheable { }
</pre>

Like the cache key builder, this check can be as simple or as sophisticated as you want. Above, I allow nulls, most simple types, and custom types marked as ICacheable. Other things which might be worth supporting are ReadOnlyCollections and Tuples with cacheable elements, as well as non-IConvertible simple types like Guid, TimeSpan, and DateTimeOffset. 

Now that we've built out a base class to do most of the heavy lifting, all that remains is to provide a concrete implementation. Here's one that uses MemoryCache under the hood:

<pre>
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
</pre>

Now, let's circle back to our compute sales example to see how this works end to end. Here's how we might write ComputeSales to take advantage of an ICache:

<pre>
class SalesCalculator : ICacheKey
{
	private readonly ICache cache;

	...
	
	double ComputeSales(int productId, DateTime? minDate, DateTime? maxDate, bool checkCache = true)
	{
		if (checkCache)
		{
			// with this pattern, we pass false for checkCache here to so that if the cache 
			// decides to invoke this method, we'll skip this branch
			return this.cache.InvokeCached(() => this.ComputeSales(productId, minDate, maxDate, false), new CachePolicy { ... });
		}
		
		// actually compute sales here
		var sales = ...
		return sales;
	}
	
	void ICacheKey.BuildCacheKey(CacheKeyBuilder builder) { builder.By(this.GetType()); }
}
</pre>

Now, let's imagine that the application calls salesCalculator.ComputeSales(100, minDate: DateTime.Parse("01/01/2014")). Here's what happens:

1. checkCache defaults to true, so we enter the if block in ComputesSales and call InvokeCached
2. We call ParseExpression on the given expression (() => this.ComputeSales(productId, minDate, maxDate, false)), which looks like:
	
	Lambda(
		body: Call(
			object: Constant(salesCalculator)
			method: SalesCalculator.ComputeSales
			arguments: [
				MemberAccess(field: productId, instance: Constant(closure)),
				MemberAccess(field: minDate, instance: Constant(closure)),
				MemberAccess(field: maxDate, instance: Constant(closure)),
				Constant(false)
			]
		)
		parameters: []
	)
	
	Thus, we extract the following:
		method: SalesCalculator.ComputeSales
		instance: salesCalculator
		arguments: [100, 01/01/2014, null, false]
		key: "{SalesCalculatorTypeGuid}{ComputeSales.MetadataToken}{}{SalesCalculatorTypeGuid (from salesCalculator.BuildCacheKey)}{100}{635241312000000000}{NullString}"
	Since the instance and argument values are all local variables/constants, this extraction won't require any calls to Expression<T>.Compile()
3. We call TryFind with the key, which calls MemoryCache.Get. If we find a value, we return it. Otherwise, we:
4. Invoke method with the given instance and argument values, generating a double result
5. Verify that the result is safe to cache. Double is IConvertible, so we're good
6. Call Add, which calls MemoryCache.Add() with the value and computed key
7. Return the value