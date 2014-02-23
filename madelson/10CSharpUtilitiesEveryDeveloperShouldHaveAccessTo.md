One of this things I like most about C# is that features like generics and extension methods make it easy to build utility methods to fill most gaps in the language and .NET base class libraries. Here are a few of my favorites. As a note, the implementations shown below are missing things argument validation in favor of brevity. For more complete implementations, check out [TODO link].

<strong>Throw<TException>.If</strong>
Assertions, argument checks, and other validity tests are a vital part of programming, but they can be surprisingly painful in C#. Debug.Assert() comes out of the box, but only works in a DEBUG build and doesn't let you specify the type of exception to be thrown. Meanwhile, spending 3 lines per check on if (...) { throw new ... } with Allman braces gets cumbersome very quickly. Throw<T>.If is a simple method that makes it easy to do 1-line checks while still maintaining the flexibility of using different exception types:

<pre>
public static class Throw<TException> where TException : Exception
{
    public static void If(bool condition, string message) 
	{
		if (condition)
		{
			throw (TException)Activator.CreateInstance(typeof(TException), message);
		}
	}
}
</pre>

Why is the generic parameter on the type and not the method? It could clearly go in either place, but I chose to place it on the type since it leads to the most natural English reading of the code "Throw ArgumentException If a == null" instead of "Throw If ArgumentException a == null".

<strong>NullSafe</strong>
Null values are a frustrating fact of life in C#, and checking for them frequently breaks the natural cadence of coding. You just want to write foo.GetBar().Baz.SomeMethod(), but checking for null at each step along the way turns this concise chain of calls into an ugly sequence of ifs and curly braces. The NullSafe function allows these checks to happen more or less inline, maintaining the structure of the code while dodging NullReferenceExceptions:

<pre>
public static TResult NullSafe<TObj, TResult>(
	this TObj obj, 
	Func<TObj, TResult> func, 
	TResult ifNullReturn = default(TResult))
{
	return obj != null ? func(obj) : ifNullReturn;
}
</pre>

With this extension, the above chain might be re-written as:
<pre>
foo.NullSafe(f => f.GetBar())
	.NullSafe(b => b.Baz)
	.NullSafe(b => b.SomeMethod(), ifNullReturn: "whatever you would return in the null case");
</pre>
To support invoking void methods at the end of the chain, just add an overload that takes Action<TObj> instead of Func<TObj>.

<strong>Capped</strong>
For some reason, I always find Max() and Min() to be confusing when trying to bound values, likely because you need Min() to set an upper bound and Max() to set a lower bound. Hence, I use a Capped() extension method to handle the common case of bounding a value within a range:

<pre>
public static T Capped<T>(this T @this, T? min = null, T? max = null)
	where T : struct, IComparable<T>
{
	return min.HasValue && @this.CompareTo(min.Value) < 0 ? min.Value
		: max.HasValue && @this.CompareTo(max.Value) > 0 ? max.Value
		: @this;
}
</pre>

For example, bounding an integer to between 1 and 10 becomes:
<pre>var bound = value.Capped(min: 1, max: 10);</pre>
Instead of the (in my opinion) far-less-readable:
<pre>var bound = Math.Max(1, Math.Min(value, 10));</pre>


<strong>Traverse.Along</strong>
I've generally found that the LinkedList<T> data structure available in the BCL rarely comes up in everyday coding; in nearly all cases List<T> or IEnumerable<T> is preferable. That said, code I work with is often full of "natural" linked lists, such as the BaseType property on Type or the InnerException property on Exception. This handy method, which I adapted from some code I saw while browsing the <a href="https://code.google.com/p/autofac/">Autofac</a> codebase, makes it easy to work with these structures as IEnumerables without having to go through a cumbersome conversion loop each time:
<pre>
public static class Traverse
{
	public static IEnumerable<T> Along<T>(T node, Func<T, T> next)
		where T : class
	{
		for (var current = node; current != null; current = next(current))
		{
			yield return current;
		}
	}
}
</pre>

With Along(), finding a (possible) inner SqlException from an Exception becomes:

<pre>
catch (Exception ex)
{
    var sqlException = Traverse.Along(ex, e => e.InnerException)
		.OfType<SqlException>()
		.FirstOrDefault();
}
</pre>

Given this structure, it's easy to imagine similar utilities for doing depth-first, breadth-first, and other traversals of arbitrary tree-like data structures.

<strong>As<T></strong>
This method is essentially a type-safe, inline cast. Just looking at the implementation, it seems completely pointless:

<pre>
public static T As<T>(this T @this) { return @this; }
</pre>

However, there are a several places where this method can come in handy. One is type
inference. For example, you may have run into this problem:

<pre>
// the compiler complains that 'Type of conditional expression cannot be 
// determined because there is no implicit conversion between 
// 'System.Collections.Generic.List<int>' and 'System.Collections.Generic.HashSet<int>''
ICollection<int> collection = someCondition ? new List<int>() : new HashSet<int>();
</pre>

The common fix is to add a cast:

<pre>
ICollection<int> collection = someCondition 
	? (ICollection<int>)new List<int>() 
	: new HashSet<int>();
</pre>

However, this is ugly because we are using a cast for what is really a type-safe operation: we know that the cast is guaranteed to succeed. However, since there's no way to specify that, someone could swap out "new List<int>()" for "new object()" and things would still compile: we've essentially given up some type-safety!

As() fixes this for us:

<pre>
// compiles
ICollection<int> collection = someCondition 
	? new List<int>().As<ICollection<int>>() 
	: new HashSet<int>();

// does not compile (since the "this" parameter of As must be of type T... 
// in this case ICollection<int>)
ICollection<int> collection = someCondition 
	? new object().As<ICollection<int>>() 
	: new HashSet<int>();
</pre>

Another common use is when dealing with explicitly implemented interfaces. For example, let's say you wanted to access the ICollection.SyncRoot property on a List<int>:

<pre>
// doesn't compile: SyncRoot is explicitly implemented and therefore private
var syncRoot = new List<int>().SyncRoot;

// ugly and not type-safe
var syncRoot = ((ICollection)new List<int>()).SyncRoot;

// type-safe, but takes 2 lines and isn't chainable
ICollection collection = new List<int>();
var syncRoot = collection.SyncRoot

// type-safe, 1 line!
var syncRoot = new List<int>().As<ICollection>().SyncRoot;
</pre>