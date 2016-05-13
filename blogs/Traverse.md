When you think of traversing a tree, what comes to mind? Recursion? Looping with a stack? While both of these methods work perfectly well, neither has the elegance, convenience, composability, and laziness of IEnumerable-based traversal using LINQ. Luckily, it isn't hard to "factor out" the mechanics of traversing trees and tree-like data structures such that we can cleanly process such objects with LINQ without writing lots of boilerplate code first. In this post we'll look at a couple functions in the <a href="https://github.com/madelson/MedallionUtilities/tree/master/MedallionCollections#medallioncollections">MedallionCollections NuGet package</a> that do just that.

<!--more-->

<h2 id="linked-lists">Linked lists</h2>

Let's start with the simplest type of "tree": a singly-linked list. Such a list is traversed by following the sequence of "next" references until we reach a null. We want to define an IEnumerable<T> that executes this method of traversal. We could work with .NET's built-in LinkedList<T> class, but we want our approach to be more general. LinkedList<T> is rarely used, but "natural" linked lists occur commonly in programming. Think about a "scope" class which holds a reference to an outer scope, or an Exception which stores an inner Exception. <i>Any class which holds a reference to it's own type is a linked list</i>!

<h3 id="linked-list-loop">Enumerating a linked list</h3>

With that definition in mind, we can write a generic traversal loop as follows:

<pre>
T root = ...
Func<T, T> next = ...

for (var node = root; node != null; node = next(node))
{
	...
}
</pre>

First, let's look at how we've abstracted away the concept of a linked list. Since there is no general interface which all naturally-occurring linked lists implement, we simply work with (a) the first element of the list and (b) a function to retrieve the next element. From these, we can write a simple for-loop that follows the chain of next elements until we reach the terminating null reference.

<h3 id="linked-list-traverse">Encapsulating the enumeration</h3>

From there, all we need to do to genericize this is to turn it into an <a href="https://msdn.microsoft.com/en-us/library/mt639331.aspx">iterator block</a> using yield return:

<pre>
public static class Traverse
{
	public static IEnumerable<T> Along<T>(T root, Func<T, T> next)
		where T : class
	{
		if (next == null) { throw new ArgumentNullException(nameof(next)); }

		return AlongIterator(root, next);
	}

	private static IEnumerable<T> AlongIterator<T>(T root, Func<T, T> next)
		where T : class
	{
		for (var node = root; node != null; node = next(node))
		{
			yield return node;
		}
	}
}
</pre>

The separation of the code into two methods is just an example of <a href="http://www.codeducky.org/better-validation-yield-asyncawait-wrapper-pattern/">this pattern</a> to ensure eager argument validation. The real meat of the code is in AlongIterator. Under the hood, the C# compiler does the hard work of turning our simple for-loop into a stateful IEnumerator object which lazily advances as new elements are requested. This method may look familiar; I've mentioned it previously in <a href="http://www.codeducky.org/10-utilities-c-developers-should-know-part-one/">this post</a>.

<h3 id="linked-list-usage">Let's put it to work!</h3>

Here are some examples of how this can be used:

<pre>
// grabbing the innermost exception
var innermost = Traverse.Along(exception, ex => ex.InnerException).Last();

// traversing a LinkedList<T> backwards
var list = new LinkedList<int>(new[] { 1, 2, 3, 4 });
var reverse = Traverse.Along(list.Last, n => n.Previous).Select(n => n.Value);
</pre>

We can even use this method to "traverse" sequences that don't physically exist in memory but where each next element can be derived from the previous element. For example, here's a <a href="https://en.wikipedia.org/wiki/Random_walk">random walk</a>:

<pre>
var random = new Random(1);
var infiniteRandomWalk = Traverse.Along(new { value = 0 }, v => new { value = v.value + random.Next(-1, 2) }); 
</pre>

<h2 id="trees">Trees</h2>

Ok enough with linked lists. We started out by discussing arbitrary trees where each node may have multiple children. Much like before, we want to define trees in a super generic way:

<pre>
T root = ...; // the root node of the tree
Func<T, IEnumerable<T>> children = ...; // gets the children of a node
</pre>

Unlike with linked lists, there's no single standard order to traversing a tree. Should we start with the root nodes or the leaf nodes? Do we go level-by-level or subtree-by-subtree? Let's focus on one common approach: <a href="https://en.wikipedia.org/wiki/Depth-first_search">depth-first traversal</a>.

<h3 id="tree-recursion">Enumerating with recursion</h3>

In depth-first traversal, we exhaustively explore the subtree rooted at each child element before exploring the next child element at each level. Here's a simple recursive implementation:

<pre>
public static void DepthFirst<T>(T node, Func<T, IEnumerable<T>> children)
{
	// do something with node
	...
	
	// recurse
	foreach (var child in children(node))
	{
		DepthFirst(child, children);
	}
}
</pre>

<h3 id="tree-traverse">Encapsulating the enumeration</h3>

Unfortunately, recursion doesn't work very well with yield return. To convert this to an IEnumerable-based approach, we'll switch gears a bit by replacing the implicit call stack with an explicit stack data structure. Here's a first try:

<pre>
public static class Traverse
{
	public static IEnumerable<T> DepthFirst<T>(T root, Func<T, IEnumerable<T>> children)
	{
		if (children == null) { throw new ArgumentNullException(nameof(children)); }

		return DepthFirstIterator(root, children);
	}

	private static IEnumerable<T> DepthFirstIterator<T>(T root, Func<T, IEnumerable<T>> children)
	{
		var stack = new Stack<T>();
		stack.Push(root);
		
		while (stack.Count > 0)
		{
			var next = stack.Pop();
			yield return next;
			
			foreach (var child in children(next))
			{
				stack.Push(child);
			}
		}
	}
}
</pre>

This implementation is concise and simple to follow. We initialize a stack with just the first element, and then proceed to pop the current element and push it's children until we have nothing left. Unfortunately, this has two minor issues:

1. Order: with the above approach, we visit the last child of each node before the first child. That's still technically depth-first, but it's counter-intuitive. We can fix this by replacing "children(next)" with "children(next).Reverse()". Unfortunately, that still doesn't address
2. Laziness: this implementation is only partially lazy. When we visit each node, we fully enumerate the list of children before visiting any of them

We can fix both the ordering and laziness issues by storing a stack of enumerators instead of a stack of elements:

<pre>
public static class Traverse
{
	public static IEnumerable<T> DepthFirst<T>(T root, Func<T, IEnumerable<T>> children)
	{
		if (children == null) { throw new ArgumentNullException(nameof(children)); }

		return DepthFirstIterator(root, children);
	}

	private static IEnumerable<T> DepthFirstIterator<T>(T root, Func<T, IEnumerable<T>> children)
	{
		var current = root;
		var stack = new Stack<IEnumerator<T>>();

		try
		{
			while (true)
			{
				yield return current;
				
				var childrenEnumerator = children(current).GetEnumerator();
				if (childrenEnumerator.MoveNext())
				{
					// if we have children, the first child is our next current
					// and push the new enumerator
					current = childrenEnumerator.Current;
					stack.Push(childrenEnumerator);
				}
				else
				{
					// otherwise, cleanup the empty enumerator and...
					childrenEnumerator.Dispose();

					// ...search up the stack for an enumerator with elements left
					while (true)
					{
						if (stack.Count == 0)
						{
							// we didn't find one, so we're all done
							yield break;
						}

						// consider the next enumerator on the stack
						var topEnumerator = stack.Peek();
						if (topEnumerator.MoveNext())
						{
							// if it has an element, use it
							current = topEnumerator.Current;
							break;
						}
						else
						{
							// otherwise discard it
							stack.Pop().Dispose();
						}
					}
				}
			}
		}
		finally
		{
			// guarantee that everything is cleaned up even
			// if we don't enumerate all the way through
			while (stack.Count > 0)
			{
				stack.Pop().Dispose();
			}
		}
	}
}
</pre>

With this approach, we advance each enumerator of children just enough and just in time to get the elements we actually plan to use. Because we leave a bunch of IDisposable IEnumerators around, however, we need to add some extra cleanup logic in a finally block in case the caller doesn't enumerate the sequence all the way to the end.

<h3 id="tree-usage">Let's put it to work!</h3>

With Traverse.DepthFirst, we can now expand our exception chain search logic to work with AggregateExceptions that have multiple inner exceptions:

<pre>
// gather ALL innermost exceptions from an Exception tree
Exception exception = ...;
var innermostExceptions = Traverse.DepthFirst(
		exception, 
		// the children function here gets all inner exceptions if the node
		// is an AggregateException or just the one inner exception (if it exists) otherwise
		ex => (ex as AggregateException)?.InnerExceptions 
			?? new[] { ex.InnerException }.Where(e => e != null)
	)
	// filter down to leaf nodes
	.Where(ex => ex.InnerException == null);
</pre>

Another good example is exploring dependency trees. Here's some code to enumerate the dependency tree of a .NET assembly:

<pre>
Assembly assembly = ...;
var dependencyTree = Traverse.DepthFirst(assembly, a => a.GetReferencedAssemblies().Select(Assembly.Load));
</pre>

In the above example, an Assembly that appears twice in the expanded dependency tree will appear twice in the resulting sequence (meaning that the "tree" is actually a <a href="https://en.wikipedia.org/wiki/Directed_acyclic_graph">directed acyclic graph</a>). While we could deduplicate the output with .Distinct(), we'd still pay the cost of re-enumerating these subtrees. Here's a pattern that offers an easy fix:

<pre>
Assembly assembly = ...;
var seenAssemblies = new HashSet<Assembly> { assembly };
var dependencyTree = Traverse.DepthFirst(
	assembly,
	a => a.GetReferencedAssemblies()
		.Select(Assembly.Load)
		// only select children which we haven't already seen
		.Where(seenAssemblies.Add)
);
</pre>

This pattern both guarantees that the output sequence is distinct and saves us the trouble of exploring the same subtree multiple times.

<h2 id="conclusion">Conclusion</h2>

In this post we examined linked list and depth-first traversal, but it's just as easy to use the same approach to encapsulate other techniques such as breadth-first traversal or in-order traversal for binary trees. These methods make code cleaner and more concise by letting us re-use traversal logic and easily combine it with LINQ operators. Furthermore, the fact that these techniques avoid recursion means that we aren't vulnerable to stack overflow exceptions when working with large amounts of data. So, next time you find yourself writing a recursive traversal, consider trying to replace it with one of these generic traversal methods and seeing how it might simplify the code. If you don't want to write these methods yourself, you can always reference them from <a href="https://github.com/madelson/MedallionUtilities/tree/master/MedallionCollections#medallioncollections">MedallionCollections</a>!

