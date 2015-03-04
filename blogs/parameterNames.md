C# 4 introduced named and optional parameters: the ability to set default values for arguments and explicitly label arguments by name instead of just position. Despite being a relatively minor features, named and optional parameters actually do quite a lot to make coding simpler to write, read, and consume. So much so, that I find myself sorely missing this feature when working in languages without it (aka Javascript).

<!--more-->

There are two basic sides to the feature. First, you can specify default values for parameters. This allows you to have flexible APIs without trying to created method overloads for all possible combinations of parameters someone might want to use:

<pre>
public static void DrawCircle(int radius, int x = 0, int y = 0, bool filled = true, Color? color = null)
{
}
</pre>

To specify just some of the arguments, you need the ability to name them explicitly:

<pre>
DrawCircle(10, x: 5, color: Color.Blue);
</pre>

While this is the most common scenario for named and optional arguments, I've found that there are other ways to use them to simply make code cleaner.

<h2 id="literals">Passing literals</h2>

I've always hated trying to mentally parse code that looks like this:

<pre>
ReadCustomer("Jerry", false);
</pre>

Without reading the method signature, this code is entirely unreadable. Is this ReadCustomer("Jerry", ignoreCase: false)? ReadCustomer("Jerry", throwIfNotFound: false)? Or, maybe ReadCustomer("Jerry", includeLocationData: false)? To avoid this issue, I've made it a general rule to always explicitly name parameters whenever it is not 100% clear what they mean in the context of the method call. Literals frequently fall into this bucket, with true and false being by far the worst (and at the same time most common) offenders.

<h2 id="defense">Coding defensively against parameter reordering</h2>
Another benefit of explicitly naming your parameters is that your code becomes more defensive both against refactors which change parameter order and against simple ordering mistakes. This is especially important for code which takes multiple parameters of the same or interchangeable types. For example, <a href="http://www.nunit.org/index.php?p=equalityAsserts&r=2.2.7">NUnit's equality asserts</a> are the reverse of how the values would be laid out in an English sentence, which leads to a common developer error. Naming the parameters not only lets you "fix" the order if you so choose, but it prevents accidents as well:

<pre>
// instead of 
Assert.AreEqual(foo, bar); // foo is expected, bar is actual

// consider
Assert.AreEqual(actual: bar, expected: foo);
</pre>

<h2 id="nullability>Highlighting parameter nullability</h2>
One of the most frustrating aspects of C# is that there is no great way to enforce or communicate when reference types might be null outside of runtime checks. That said, I've found that providing a null default can be a good way to indicate this, even if you don't expect callers to take advantage of the default argument very often. For example:

<pre>
// unclear whether null is OK
public Parser(ParserOptions options) { }

// very clear
public Parser(ParserOptions options = null) { }
</pre>

<h2 id="dynamic">Creating dynamic APIs</h2>

One of the more obscure benefits of named arguments is the fact that they extend the C# syntax. That means that they can be given arbitrary meaning when used with dynamic keywords. The <a href="https://github.com/robconery/massive#named-argument-query-syntax">Massive Micro ORM uses this to particularly good effect.</a>. Here's a quick example of a dynamic dictionary builder using "keyword arguments" syntax:

<pre>
public class DictionaryBuilder : DynamicObject
{
	private readonly Dictionary<string, object> _dictionary = new Dictionary<string, object>();

	public static dynamic Build()
	{
		return new DictionaryBuilder();
	}

	public Dictionary<string, object> ToDictionary() { return this._dictionary; }

	public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
	{
		if (binder.Name == "Add")
		{
			if (binder.CallInfo.ArgumentNames.Count != args.Length)
			{
				throw new ArgumentException("Named arguments must be used!");
			}

			for (var i = 0; i < args.Length; ++i)
			{
				this._dictionary[binder.CallInfo.ArgumentNames[i]] = args[i];
			}

			result = this;
			return true;
		}

		return base.TryInvokeMember(binder, args, out result);
	}
}

// usage
IDictionary<string, object> dict = DictionaryBuilder.Build()
	.Add(a: 2, b: "c", d: DateTime.Now)
	.ToDictionary();
Console.WriteLine(string.Join(", ", dict)); // prints [a, 2], [b, c], ...
</pre>

<h2 id="compatibility">A final note:</h2>
One thing that becomes clear if you adopt any of these practices is that, with the advent of named and optional arguments, parameter names have become as much a part of any public API as method, class, and property names. That means you should consider changing the names of parameters on a public method to be a breaking change! You never know if there might be existing code out there like this:

<pre>
YourMethod(clunkyOldParameterName: 2);
</pre>

So, if backwards compatibility is something you have to think about for your project, name your parameters with care!