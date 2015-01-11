Recently, I've been writing a lot of code which gets packaged up into libraries to be consumed by other developers. One of the key aspects of writing this sort of code effectively is to be stringent about argument validation. The earlier your code detects invalid arguments, the easier it is for consumers of your APIs to diagnose errors. Argument checking is fairly straight-forward, but I recently came upon an interesting subtlety when working with the C# <a href="http://msdn.microsoft.com/en-us/library/9k7k7cf0.aspx">yield</a> keyword. 

For example, let's consider implementing the LINQ method <a href="http://msdn.microsoft.com/en-us/library/vstudio/bb534804(v=vs.100).aspx">Enumerable.TakeWhile</a>. TakeWhile has two simple conditions to check for: either argument can be null. We can easily implement TakeWhile using yield:

<pre>
public static IEnumerable<TSource> TakeWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
{
    if (source == null) { throw new ArgumentNullException("source"); }
	if (predicate == null) { throw new ArgumentNullException("predicate"); }
	
	foreach (var element in source) 
	{
		if (!predicate(element)) { break; }
		yield return element; 
	}
}
</pre>

However, if we look at <a href="http://referencesource.microsoft.com/#System.Core/System/Linq/Enumerable.cs,28936f7582207b50">Microsoft's implementation of TakeWhile</a>, we see something a bit different:

<pre>
public static IEnumerable<TSource> TakeWhile<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) {
	if (source == null) throw Error.ArgumentNull("source");
	if (predicate == null) throw Error.ArgumentNull("predicate");
	return TakeWhileIterator<TSource>(source, predicate);
}

static IEnumerable<TSource> TakeWhileIterator<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate) {
	foreach (TSource element in source) {
		if (!predicate(element)) break;
		yield return element;
	}
}
</pre>

This is what I call the <strong>Wrapped Yield/Await Pattern</strong>: the method is split into a public "wrapper" method that just does argument validation and then calls an inner private method that implements the actual iterator. Why is this done? Let's try running each implementation with invalid arguments:

<pre>
// MSFT's implementation: this line fails with ArgumentNullException
var sequence = "abc".TakeWhile((Func<char, bool>)null);

// our implementation: this line succeeds!
var sequence = "abc".TakeWhile((Func<char, bool>)null);

// ok, now we get the exception
var array = sequence.ToArray()
</pre>

What's going on? The key is that the semantics of yield in C# are 100% lazy: none of the code in the iterator block gets executed until the returned sequence is actually enumerated (under the hood, <a href="http://csharpindepth.com/articles/chapter6/iteratorblockimplementation.aspx">yield methods are compiled into state machines</a>). While this is (often) great from a performance perspective, it's less ideal from an error handling perspective, since the exception may occur far from the source of the buggy code (imagine an IEnumerable constructed in one function and then passed around for awhile before being evaluated in another function). The solution is to split the method into two. The outer method does not use yield and is therefore not lazy, so the validation runs right away.

I called this the wrapped yield/<strong>await</strong> pattern, so async/await must come into this too, right? <a href="http://msdn.microsoft.com/en-us/library/hh191443.aspx">Async</a> methods are similar to yield iterators in that they are rewritten by the compiler to behave quite differently from normal control flow. Unlike yield, though, async methods are NOT lazy: when an async method is called, it executes synchronously until it hits an await. What's the issue, then? Let's imagine we have a method SkipLinesAsync(int) which is supposed to skip past lines in a <a href="http://msdn.microsoft.com/en-us/library/system.io.textreader%28v=vs.110%29.aspx">TextReader</a>. We could implement this as follows:

<pre>
public static async Task SkipLinesAsync(this TextReader reader, int linesToSkip)
{
	Console.WriteLine("validating arguments...");
	if (reader == null) { throw new ArgumentNullException("reader"); }
	if (linesToSkip < 0) { throw new ArgumentOutOfRangeException("linesToSkip"); }	
	
	for (var i = 0; i < linesToSkip; ++i)
	{
		var line = await reader.ReadLineAsync().ConfigureAwait(false);
		if (line == null) { break; }
	}
}
</pre>

What happens if we run this?

<pre>
var reader = new StringReader("abc");

// this line succeeds!
var task = reader.SkipLinesAsync(-1); 
// "validating arguments..." prints here. Still no failure
Console.WriteLine("----");

// finally, this line throws AggregateException (inner exception is ArgumentOutOfRangeException)
task.Wait();
</pre>

Again, this is not very desirable: the exception is only reported when the task is observed (which could happen much later or even never for fire-and-forget tasks). The issue here is that everything inside an async method is considered part of the "Task" returned by that method. Thus, even though the failure happens synchronously, it is caught by the task system and stored in the Task itself (to be rethrown on Wait()). As with yield, we can fix this using a wrapper:

<pre>
public static Task SkipLinesAsync(this TextReader reader, int linesToSkip)
{
	if (reader == null) { throw new ArgumentNullException("reader"); }
	if (linesToSkip < 0) { throw new ArgumentOutOfRangeException("linesToSkip"); }	
	
	return reader.SkipLinesInternalAsync(linesToSkip);
}

private static async Task SkipLinesInternalAsync(this TextReader reader, int linesToSkip)
{
	for (var i = 0; i < linesToSkip; ++i)
	{
		var line = await reader.ReadLineAsync().ConfigureAwait(false);
		if (line == null) { break; }
	}
}
</pre>

Because the outer method is no longer async, it executes the validation outside of the context of the async Task, so get the synchronous, unwrapped validation errors we'd like.

In some ways, it's unfortunate that we need the wrapped yield/await pattern at all. Couldn't the C# language designers have made it so that the more "intuitive" code would behave as expected? Thinking through the issue in more depth though, it's not clear that any alternative approach wouldn't suffer from even worse issues. For example, let's say we wanted code that comes before the first yield or await to be treated differently from further code. This behavior becomes hard to specify in cases like the following:

<pre>
for (var i = 0; i < x; ++i) {
	// code here is before the first await on the first iteration only
	await ...
}

// if x is 0, code here is also before the first await
</pre>

Creating a system where the same line of code can behave quite differently depending on the execution path seems potentially even less intuitive.