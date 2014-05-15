When writing reflection-based code, it is often desirable to determine whether one type can be cast or converted to another. Type.IsAssignableFrom is far from sufficient here; as this fails to handle things like primitive type conversions (e. g. int => double) or <a href="http://msdn.microsoft.com/en-us/library/aa288476(v=vs.71).aspx">user-defined conversions</a>. It also doesn't differentiate between explicit conversions (requiring casts) and implicit conversions. Thus, I set out to code up a more robust approach that worked correctly for the various gnarly edge-cases. The final code is <a href="https://gist.github.com/madelson/cbb9786d227c511619ae">here</a>.

First, let's define the problem. We'd like to implement two methods:

<pre>
// Returns true iff the code To x = Get<From>(); compiles
public static bool IsImplicitlyCastableTo(this Type from, Type to) { ... }

// and

// Returns true iff the code var x = (To)Get<From>(); compiles
public static bool IsCastableTo(this Type from Type to) { ... }

// Get<T> is just a stand-in for an expression of static type T
// in this case, we can code it up as static T Get<T>() { return default(T); }
</pre>

Note that in all cases we are looking for whether the code compiles, which indicates only whether the cast or conversion <i>might</i> succeed rather than whether it <i>will</i> succeed. The reason for this is that it is impossible (especially given user-defined conversions) to know whether any given conversion will succeed by looking at the types alone. For example, a value of type int? is explicitly castable to int, but the cast will generate an InvalidOperationException if the value is null.

<strong>Verifying the solution</strong>

Given the complexity of C#'s casting and conversion semantics, it's difficult to even get started on a solution. Thus, I began by creating a unit test based on the ultimate authority: the C# compiler. Essentially, we can gather a number of interesting possible conversions, and feed the C# compiler the relevant line of code, and see if it compiles. Here's my test code:

<pre>
/// <summary>
/// Validates the given implementation function for either implicit or explicit conversion
/// </summary>
private void RunTests(Func<Type, Type, bool> func, bool @implicit)
{
	// gather types
	var primitives = typeof(object).Assembly.GetTypes().Where(t => t.IsPrimitive).ToArray();
	var simpleTypes = new[] { typeof(string), typeof(DateTime), typeof(decimal), typeof(object), typeof(DateTimeOffset), typeof(TimeSpan), typeof(StringSplitOptions), typeof(DateTimeKind) };
	var variantTypes = new[] { typeof(string[]), typeof(object[]), typeof(IEnumerable<string>), typeof(IEnumerable<object>), typeof(Func<string>), typeof(Func<object>), typeof(Action<string>), typeof(Action<object>) };
	var conversionOperators = new[] { typeof(Operators), typeof(Operators2), typeof(DerivedOperators), typeof(OperatorsStruct) };
	var typesToConsider = primitives.Concat(simpleTypes).Concat(variantTypes).Concat(conversionOperators).ToArray();
	var allTypesToConsider = typesToConsider.Concat(typesToConsider.Where(t => t.IsValueType).Select(t => typeof(Nullable<>).MakeGenericType(t)));

	// generate test cases
	var cases = this.GenerateTestCases(allTypesToConsider, @implicit);

	// collect errors
	var mistakes = new List<string>();
	foreach (var @case in cases)
	{
		var result = func(@case.Item1, @case.Item2);
		if (result != (@case.Item3 == null))
		{
		   // func(@case.Item1, @case.Item2); // break here for easy debugging
			mistakes.Add(string.Format("{0} => {1}: got {2} for {3} cast", @case.Item1, @case.Item2, result, @implicit ? "implicit" : "explicit"));
		}
	}
	Assert.IsTrue(mistakes.Count == 0, string.Join(Environment.NewLine, new[] { mistakes.Count + " errors" }.Concat(mistakes)));
}

private List<Tuple<Type, Type, CompilerError>> GenerateTestCases(IEnumerable<Type> types, bool @implicit)
{
	// gather all pairs
	var typeCrossProduct = types.SelectMany(t => types, (from, to) => new { from, to })
		.Select((t, index) => new { t.from, t.to, index })
		.ToArray();

	// create the code to pass to the compiler
	var code = string.Join(
		Environment.NewLine,
		new[] { "namespace A { public class B { static T Get<T>() { return default(T); } public void C() {" }
		.Concat(typeCrossProduct.Select(t => string.Format("{0} var{1} = {2}Get<{3}>();", GetName(t.to), t.index, @implicit ? string.Empty : "(" + GetName(t.to) + ")", GetName(t.from))))
			.Concat(new[] { "}}}" })
	);                

	// compile the code
	var provider = new CSharpCodeProvider();
	var compilerParams = new CompilerParameters();
	compilerParams.ReferencedAssemblies.Add(this.GetType().Assembly.Location); // reference the current assembly!
	compilerParams.GenerateExecutable = false;
	compilerParams.GenerateInMemory = true;
	var compilationResult = provider.CompileAssemblyFromSource(compilerParams, code);

	// determine the outcome of each conversion by matching compiler errors with conversions by line #
	var cases = typeCrossProduct.GroupJoin(
			compilationResult.Errors.Cast<CompilerError>(),
			t => t.index,
			e => e.Line - 2,
			(t, e) => Tuple.Create(t.from, t.to, e.FirstOrDefault())
		)
		.ToList();

	// add a special case
	// this can't be verified by the normal means, since it's a private class
	cases.Add(Tuple.Create(typeof(PrivateOperators), typeof(int), default(CompilerError)));

	return cases;
}

/// <summary>
/// Gets a C# name for the given type
/// </summary>
private static string GetName(Type type)
{
	if (!type.IsGenericType)
	{
		return type.ToString();
	}

	return string.Format("{0}.{1}<{2}>", type.Namespace, type.Name.Substring(0, type.Name.IndexOf('`')), string.Join(", ", type.GetGenericArguments().Select(GetName)));
}
</pre>

The various Operators types contain a variety of implicit and explict conversion operators (for the full code, see the <a href="https://gist.github.com/madelson/cbb9786d227c511619ae">gist</a>). This gives us over 3000 tests covering a variety of interesting cases. At this point, you may be wondering: why not just use the C# complier as our implementation? While this may be possible with the release of <a href="http://roslyn.codeplex.com/">Roslyn</a>, we can't do it with CSharpCodeProvider because the generated code would need to be able to legally reference the types in question. That would rule out that approach for any non-public types. Furthermore, the cost of spinning up the compiler process is quite expensive, so we'd likely run into performance issues. With that settled, we're ready to start coding.

<strong>IsImplicitlyCastableTo</strong>

We'll start with implicit casts first, since this turns out to be far simpler and will be used in our IsCastableTo implementation. Rather than try to implement the rules of implicit casting directly, we can leverage C#'s dynamic feature to do much of the logic for us. For example, the following code will fail to resolve an overload for Add() because no implicit conversion exists between string and int:

<pre>
dynamic list = new List<string>();
list.Add(default(int));
</pre>

Here's the code, much of which was cobbled together by using <a href="https://www.linqpad.net/">LinqPad</a> to examine the IL for the above dynamic code.

<pre>
public static bool IsImplicitlyCastableTo(this Type from, Type to)
{
	// from http://www.codeducky.org/10-utilities-c-developers-should-know-part-one/ 
	Throw.IfNull(from, "from");
	Throw.IfNull(to, "to");

	// not strictly necessary, but speeds things up
	if (to.IsAssignableFrom(from))
	{
		return true;
	}

	try
	{
		// overload of GetMethod() from http://www.codeducky.org/10-utilities-c-developers-should-know-part-two/ 
		// that takes Expression<Action>
		ReflectionHelpers.GetMethod(() => AttemptImplicitCast<object, object>())
			.GetGenericMethodDefinition()
			.MakeGenericMethod(from, to)
			.Invoke(null, new object[0]);
		return true;
	}
	catch (TargetInvocationException ex)
	{
		return = !(
			ex.InnerException is RuntimeBinderException
			// if the code runs in an environment where this message is localized, we could attempt a known failure first and base the regex on it's message
			&& Regex.IsMatch(ex.InnerException.Message, @"^The best overloaded method match for 'System.Collections.Generic.List<.*>.Add(.*)' has some invalid arguments$")
		);
	}
}

private static void AttemptImplicitCast<TFrom, TTo>()
{
	// based on the IL produced by:
	// dynamic list = new List<TTo>();
	// list.Add(default(TFrom));
	// We can't use the above code because it will mimic a cast in a generic method
    // which doesn't have the same semantics as a cast in a non-generic method

	var list = new List<TTo>(capacity: 1);
	var binder = Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(
		flags: CSharpBinderFlags.ResultDiscarded, 
		name: "Add", 
		typeArguments: null, 
		context: typeof(TypeHelpers), // the current type
		argumentInfo: new[] 
		{ 
			CSharpArgumentInfo.Create(flags: CSharpArgumentInfoFlags.None, name: null), 
			CSharpArgumentInfo.Create(
				flags: CSharpArgumentInfoFlags.UseCompileTimeType, 
				name: null
			),
		}
	);
	var callSite = CallSite<Action<CallSite, object, TFrom>>.Create(binder);
	callSite.Target.Invoke(callSite, list, default(TFrom));
}
</pre>

This code is ugly; it uses exceptions for control flow, manually constructs dynamic call sites, and relies on specific exception messages. However, it passes all of the unit tests, so it is (at least mostly) correct, which is more than I can say for the majority of implementations I found on StackOverflow and elsewhere. Performance is a potential concern, but for most use cases we can fix this simply by adding a caching layer above this one (the <a href="https://gist.github.com/madelson/cbb9786d227c511619ae">gist</a> includes this feature). Even so, the implementation as-is can run all 3000+ unit tests in ~2 seconds on my machine, so it's not too bad.

<strong>IsCastableTo</strong>

For explicit casts, dynamic provides only a partial solution. While dynamic code like "var x = (TTo)(dynamic)default(TFrom);" works well when casting from value types, it breaks down when dealing with nulls. Fundamentally, dynamic operations work on an object's runtime type. When dealing only with implicit conversions, we were able to bypass this by hooking into overload resolution, a process that considers compile-time types, but this is not possible (so far as I can tell) when dealing with casts. Thus, we have to write out the casting rules manually for reference types which ends up being quite complex. Here's the code:

<pre>
public static bool IsCastableTo(this Type from, Type to)
{
	// from http://www.codeducky.org/10-utilities-c-developers-should-know-part-one/ 
	Throw.IfNull(from, "from");
	Throw.IfNull(to, "to");

	// explicit conversion always works if to : from OR if 
	// there's an implicit conversion
	if (from.IsAssignableFrom(to) || from.IsImplicitlyCastableTo(to))
	{
		return true;
	}

	// for nullable types, we can simply strip off the nullability and evaluate the underyling types
	var underlyingFrom = Nullable.GetUnderlyingType(from);
	var underlyingTo = Nullable.GetUnderlyingType(to);
	if (underlyingFrom != null || underlyingTo != null)
	{
		return (underlyingFrom ?? from).IsCastableTo(underlyingTo ?? to);
	}

	if (from.IsValueType)
	{
		try
		{
			ReflectionHelpers.GetMethod(() => AttemptExplicitCast<object, object>())
				.GetGenericMethodDefinition()
				.MakeGenericMethod(from, to)
				.Invoke(null, new object[0]);
			return true;
		}
		catch (TargetInvocationException ex)
		{
			return !(
				ex.InnerException is RuntimeBinderException
				// if the code runs in an environment where this message is localized, we could attempt a known failure first and base the regex on it's message
				&& Regex.IsMatch(ex.InnerException.Message, @"^Cannot convert type '.*' to '.*'$")
			);
		}
	}
	else
	{
		// if the from type is null, the dynamic logic above won't be of any help because 
		// either both types are nullable and thus a runtime cast of null => null will 
		// succeed OR we get a runtime failure related to the inability to cast null to 
		// the desired type, which may or may not indicate an actual issue. thus, we do 
		// the work manually
		return from.IsNonValueTypeExplicitlyCastableTo(to);
	}
}

private static bool IsNonValueTypeExplicitlyCastableTo(this Type from, Type to)
{
	if ((to.IsInterface && !from.IsSealed)
		|| (from.IsInterface && !to.IsSealed))
	{
		// any non-sealed type can be cast to any interface since the runtime type MIGHT implement
		// that interface. The reverse is also true; we can cast to any non-sealed type from any interface
		// since the runtime type that implements the interface might be a derived type of to.
		return true;
	}

	// arrays are complex because of array covariance 
	// (see http://msmvps.com/blogs/jon_skeet/archive/2013/06/22/array-covariance-not-just-ugly-but-slow-too.aspx).
	// Thus, we have to allow for things like var x = (IEnumerable<string>)new object[0];
	// and var x = (object[])default(IEnumerable<string>);
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

	// look for conversion operators. Even though we already checked for implicit conversions, we have to look
	// for operators of both types because, for example, if a class defines an implicit conversion to int then it can be explicitly
	// cast to uint
	const BindingFlags conversionFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
	var conversionMethods = from.GetMethods(conversionFlags)
		.Concat(to.GetMethods(conversionFlags))
		.Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit")
			&& m.Attributes.HasFlag(MethodAttributes.SpecialName)
			&& m.GetParameters().Length == 1 
			&& (
				// the from argument of the conversion function can be an indirect match to from in
				// either direction. For example, if we have A : B and Foo defines a conversion from B => Foo,
				// then C# allows A to be cast to Foo
				m.GetParameters()[0].ParameterType.IsAssignableFrom(from)
				|| from.IsAssignableFrom(m.GetParameters()[0].ParameterType)
			)
		);

	if (to.IsPrimitive && typeof(IConvertible).IsAssignableFrom(to))
	{
		// as mentioned above, primitive convertible types (i. e. not IntPtr) get special 
		// treatment in the sense that if you can convert from Foo => int, you can convert
		// from Foo => double as well
		return conversionMethods.Any(m => m.ReturnType.IsCastableTo(to));
	}

	return conversionMethods.Any(m => m.ReturnType == to);
}

private static void AttemptExplicitCast<TFrom, TTo>()
{
	// based on the IL generated from
	// var x = (TTo)(dynamic)default(TFrom);

	var binder = Microsoft.CSharp.RuntimeBinder.Binder.Convert(CSharpBinderFlags.ConvertExplicit, typeof(TTo), typeof(TypeHelpers));
	var callSite = CallSite<Func<CallSite, TFrom, TTo>>.Create(binder);
	callSite.Target(callSite, default(TFrom));
}
</pre>

As with ImplicitlyCastableTo, we can boost performance by adding a caching layer to re-use commonly requested results.

<strong>Conclusion</strong>
We now have (hopefully) correct code for determining both implicit and explicit casts at runtime. While the code itself is ugly, the APIs are straight-forward and easy to use. And, since we have a massive test suite, it should be easy to verify or debunk alternate implementations. If you have an additional test case which breaks the above code or a cleaner/faster implementation that passes the full suite, don't hesitate to post in in the comments!