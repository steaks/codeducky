This post is part two of a series on general purpose C# utilities. Part one is here[TODO link]. As a note, the implementations shown below are missing things argument validation in favor of brevity. For more complete implementations, check out <a href="https://gist.github.com/madelson/9178059">https://gist.github.com/madelson/9178059</a>. This also includes the utilities from part 1.

<strong>EqualityComparers.Create</strong>
Most of the built-in .NET collections and LINQ extension methods come with the option of specifying a custom IEqualityComparer. This adds a ton of flexibility, meaning that you can, for example, trivially customize OrderBy to be case-insensitive or Distinct() to compare only select properties of an object. Unfortunately, this flexibility is burdened by the  cumbersome effort of actually implementing IEqualityComparer, which is hard to do in under ~15 lines of code. Enter EqualityComparers.Create(), which makes it easy to define custom comparers on the fly using concise lambda expressions:

<pre>
public static class EqualityComparers
{
	public static IEqualityComparer<T> Create<T>(
		Func<T, T, bool> equals, 
		Func<T, int> hash = null)
	{
		return new FuncEqualityComparer<T>(equals, hash ?? (t => 1));
	}

    private class FuncEqualityComparer<T> : EqualityComparer<T>
	{
		private readonly Func<T, T, bool> equals;
		private readonly Func<T, int> hash;
	
		public FuncEqualityComparer(
			Func<T, T, bool> equals, 
			Func<T, int> hash)
		{
		    this.equals = equals;
			this.hash = hash;
		}
	
		public override bool Equals(T a, T b)
		{
			return a == null 
				? b == null 
				: b != null && this.equals(a, b);
		}
		
		public override int GetHashCode(T obj)
		{
			return obj == null ? 0 : this.hash(obj);
		}
	}
}
</pre>

For example, to create a dictionary that compares Tuples by only their first member, you can do:
<pre>
var dict = new Dictionary<Tuple<int, int>, object>(EqualityComparers.Create<int>(equals: (a, b) => a.Item1 == b.Item1, hash: t => t.Item1);
</pre>

Null values are handled automatically, which simplifies the implementation. Providing a hash function is optional, since implementing GetHashCode() may or may not be useful depending on the algorithm using the comparer. Even when it is useful, a custom hash function is only an optimization, which may not be necessary in some cases. Finally, the common case of creating a comparer that compares values by some key selector function can be implemented on top of this as a 1-liner:

<pre>
public static EqualityComparer<T> Create<T, TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey> comparer = null)
{
	var cmp = comparer ?? EqualityComparer<TKey>.Default;
	return Create<T>(
		equals: (a, b) => cmp.Equals(keySelector(a), keySelector(b)),
		hash: obj => cmp.GetHashCode(keySelector(obj))
	);
}
</pre>

<string>CollectionEquals</strong>
Frustratingly, the BCL doesn't contain a nice method for comparing collections. ISet has a SetEquals method that, by definition, ignores duplicates, while Enumerable.SequenceEqual is useful only when order matters. Here's a simple implementation of CollectionEquals, which checks whether two sequences have the same elements independent of order:

<pre>
public static bool CollectionEquals<T>(this IEnumerable<T> @this, IEnumerable<T> that, IEqualityComparer<T> comparer = null)
{
	// this is optional; if you want to be consistent with SequenceEqual, just throw exceptions if either argument is null instead
	if (@this == null) { return that == null; }
	else if (that == null) { return false; }
	
	var countedItems = @this.GroupBy(t => t, comparer).ToDictionary(
		g => g.Key, 
		g => g.Count(), 
		comparer);
	foreach (var item in that)
	{
		int count;
		if (!countedItems.TryGetValue(item, out count)) { return false; }
		if (count - 1 == 0) { countedItems.Remove(item); }
		else { countedItems[item] = count - 1; }
	}
	return countedItems.Count == 0;
}
</pre>

There are obviously many ways to write this; this one is fairly concise and O(N + M) time assuming that we have a good hash function for the collection elements. The other nice thing about this implementation is that it's very easy to evolve into an Assert utility for checking collection equality in unit tests. With natural points for capturing (1) items in @this and not in that and (2) items in that and not in @this, it's easy to fail with a very helpful error message explaining why the mismatch occurred.

<strong>GetOrAdd</strong>
Dictionaries are frequently used for caching and memoization. You've no doubt come across the following pattern in code:

<pre>
SomeType value;
if (!dictionary.ContainsKey(key))
{
	value = ComputeValue(key);
	dictionary[key] = value;
}
else 
{
	value = dictionary[key];
}
</pre>

While this code can be made a bit more efficient with TryGetValue, it's still not chainable and ends up being written over and over again. Interestingly, the ConcurrentDictionary class has a nice wrapper for this logic called GetOrAdd(), which takes a key and a value factory function so that this operation becomes a chainable one-liner. I've written an extension which grants this functionality to all IDictionaries:

<pre>
public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> valueFactory)
{
	TValue value;
	if (!@this.TryGetValue(key, out value)) { @this.Add(key, value = valueFactory(key)); }
	return value;
}
</pre>

With this, the pattern above reduces to:
<pre>
var value = dictionary.GetOrAdd(key, ComputeValue);
</pre>

Note that I've explicitly made the signature of this function identical to the one on ConcurrentDictionary. That way, users of ConcurrentDictionary will not mistakenly use this function, which is not thread-safe (due to the use of Add) and would likely do more synchronization work than the builtin method. 

<strong>GetMethod</strong>
Reflection is a powerful aspect of .NET, and vital for many tasks like model binding, serialization, and processing expression trees. However, I've always been frustrated by the fact that using reflection makes code harder to refactor since your method and property names end up as strings. This method leverages Expression lambdas to allow for type-safe retrieval of properties.

<pre>
public static MethodInfo GetMethod<TInstance>(
	Expression<Action<TInstance>> expr)
{
	return (MethodInfo)((MethodCallExpression)expr.Body).Method;
}
</pre>

Not only does this add type-safety to many reflection scenarios, but it also simplifies the handling of overloads because the compiler will do the overload resolution for you. For example, let's say you wanted to retrieve the overload of
Queryable.Max that takes a selector function:

<pre>
var method = Helpers.GetMethod(
	(IQueryable<object> q) => q.Max(default(Func<object, object>))
);
</pre>

From here, it's easy to see how you can build out similar functions for retrieving PropertyInfos and non-extension static methods.

<strong>GetGenericArguments</strong>
System.Type already has a GetGenericArguments() method, so why do we need another one? Using the native GetGenericArguments() in generic code can be difficult because a single type can have multiple sets of generic parameters. For example, IDictionary<int, int> implements IEnumerable<KeyValuePair<int, int>>. Thus, if you use the following code to determine the element type of an IEnumerable<T>, it will fail if the given enumerable is a Dictionary<int, int>:

<pre>
// returns typeof(int) for a Dictionary<int, int>, instead of the desired
// typeof(KeyValuePair<int, int>)
var elementType = enumerable.GetType().GetGenericArguments()[0];
</pre>

The GetGenericArguments() below method makes it easy to do this kind of logic robustly by allowing you to specify which generic type definition you actually care about when fetching generic arguments:

<pre>
public static Type[] GetGenericArguments(
	this Type @this, 
	Type genericTypeDefinition)
{
	if (genericTypeDefinition.IsInterface)
	{
		var @interface = @this.GetInterfaces()
			.FirstOrDefault(
				i => i.IsGenericType 
				&& i.GetGenericTypeDefinition() == genericTypeDefinition
			);
		return @interface.NullSafe(i => i.GetGenericArguments(), Type.EmptyTypes);
	}
	if (@this.IsGenericType 
		&& @this.GetGenericTypeDefinition() == genericTypeDefinition)
	{
		return @this.GetGenericArguments();
	}
	return @this.BaseType.NullSafe(
		t => t.GetGenericArguments(genericTypeDefinition), 
		Type.EmptyTypes
	);
}
</pre>

Using this function, we can fix our element type-fetching code above:

<pre>
var elementType = enumerable.GetType().GetGenericArguments(typeof(IEnumerable<>));
</pre>

This functionality can also be used to test whether a given type extends or implements some generic type:

<pre>
// this doesn't work, since it returns false
var isIDictionary = typeof(IDictionary<,>)
	.IsAssignableFrom(typeof(Dictionary<int, int>))

// this works correctly
var isIDictionary = typeof(Dictionary<int, int>)
	.GetGenericArguments(typeof(IDictionary<,>))
	.Any();
</pre>

Even when the native GetGenericArguments() will always work correctly, I find myself using this version where possible for enhanced readability. Deep in reflection code, it quickly becomes easy to forget which types you're actually dealing with. Specifying the generic type definition makes the code more readable because it's more obvious what the result will be:

<pre>
// not that readable
var returnType = someParameterInfo.GetGenericArguments()[2]
// more readable
var returnType = someParameterInfo.GetGenericArguments(typeof(Func<,,>))[2] 
</pre>
