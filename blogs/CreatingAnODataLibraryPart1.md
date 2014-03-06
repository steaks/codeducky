Creating a .NET OData library

I've recently been working on a project to create a .NET library for implementing and querying services using Microsoft's <a href="http://www.odata.org/">OData</a> standard. This post is Part 1 of a series walking through the implementation. For the source code, check out <a href="https://github.com/madelson/MedallionOData">the MedallionOData repository on github</a>.

<strong>What is OData?</strong>

OData has a rather large specification that covers everything you need to fully expose a data model through a web service layer. That said, by far the coolest and most useful aspect of OData, in my opinion, is its ability to empower service endpoints with extremely flexible LINQ-like query capabilities:

<pre>
// example OData URL for querying customers
www.myservice.com/Customers?$filter=(City eq 'Miami') and (Company.Name neq 'Abc')&$orderby=Name
</pre>

As a web developer, the prospect of being able to enable any endpoint with LINQ is quite powerful from a design perspective. Without such a capability, most REST APIs tend to have a random smattering of potentially useful options for this purpose; but for any given usage you are likely to find something missing. For example, <a href="https://dev.twitter.com/docs/api/1.1/get/search/tweets">this Twitter API</a> endpoint provides a number of useful options, but doesn't make it easy to efficiently query for "recent Monday queries" or "queries matching 'wizards' but not 'basketball'". With OData, each endpoint you create becomes enormously flexible from the get-go, with the additional benefit of ease-of-use through standardization. 

<strong>There is already lots of .NET support for working with OData; why do we need another library?</strong>

It's true that .NET provides some great facilities for working with OData out of the box. For example, ASP.NET WebApi has <a href="http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/creating-an-odata-endpoint">great facilities for creating complete OData endpoints</a>. On the other side, you can use <a href="http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/calling-an-odata-service-from-a-net-client">Visual Studio</a> to generate a strongly-typed service endpoint for querying an OData service from .NET. What's missing here, though, in my mind, is a lighter-weight and more flexible option. What if I want to add OData query capabilities to existing (possibly non-WebApi) endpoints in my application without a lot of refactoring? Or, what if I want a lightweight and possibly dynamic way query OData services without the clunky overhead of pre-generating proxy code in Visual Studio?

<strong>MedallionOData</strong>

To this end, I've set out to build <a href="https://github.com/madelson/MedallionOData">MedallionOData</a>, a lightweight library which both makes it easy to add OData query capability to any endpoint, regardless of web framework and makes it easy to query said endpoints from .NET using LINQ.

This involves implementing a multi-step request pipeline involving steps on both the remote service and the client:

<pre>
1. LINQ to OData conversion (client)
2. Making the HTTP request (client)
3. Parsing the OData query string (service)
4. Applying the OData query options to a LINQ IQueryable (service)
5. Serializing the results (service)
6. Deserializing the results (client)
</pre>

<strong>Getting started</strong>

Before jumping into step 1, I decided to build out an intermediate expression language to represent an OData query string. This allows the process of parsing and rendering query strings to be decoupled from the process of deconstructing and rebuilding LINQ queries. 

For creating the expression language, I followed the pattern used by .NET in System.Linq.Expressions. For what is essentially a C# version of an <a href="http://en.wikipedia.org/wiki/Algebraic_data_type">Algebraic or "case" data type</a>. Here's the basic pattern:

<pre>
// an enum that describes the set of possible expression types.
// This allows for efficient "switch-case" logic when processing expressions
public enum ODataExpressionKind
{
	BinaryOp,
	UnaryOp,
	Call,
	...
}

// the abstract expression type. Guarantees that all expressions have a Kind as well
// as a Type (another enum which represents the types supported in OData).
// The abstract type also contains factory methods for creating specific expression types
public abstract class ODataExpression
{
	protected ODataExpression(ODataExpressionKind kind, ODataExpressionType type) { ... }
	
	public ODataExpressionKind Kind { get; private set; }
	public ODataExpressionType Type { get; private set; }
	
	// ODataFunction is yet another enum of the available OData functions
	public static ODataCallExpression Call(ODataFunction function, IEnumerable<ODataExpression> arguments)
	{
		ODataFunctionSignature signature;
		// Throw.If defined in http://www.codeducky.org/?p=95
		Throw.If(!TryFindSignature(function, arguments), "invalid signature");
		
		var castArguments = signature.ArgumentTypes.Zip(arguments, (type, arg) => arg.Type == type ? arg : Convert(arg, type))
			.ToList()
			.AsReadOnly();
		return new ODataCallExpression(function, castArguments, signature.ReturnType);
	}
}

// an example concrete expression type, in this case for a method call.
// Note that this maintains the immutability of the base class. 
// Immutability is a great feature for expression trees since it allows them to easily be transformed (rebuilt)
// without having to worry about where else a given node might be referenced
public sealed class ODataCallExpression : ODataExpression
{
	internal ODataCallExpression(ODataFunction function, IReadOnlyList<ODataExpression> arguments, ODataExpressionType returnType)
		: base(ODataExpressionKind.Call, returnType)
	{
		this.Function = function;
		this.Arguments = arguments;
	}

	public ODataFunction Function { get; private set; }
	public IReadOnlyList<ODataExpression> Arguments { get; private set; }

	// since the OData expression language is very simple, it's easy to override ToString() in each concrete expression type
	// such that calling ToString() on any ODataExpression renders the complete query string representation
	public override string ToString()
	{
		return string.Format("{0}({1})", this.Function.ToODataString(), string.Join(", ", this.Arguments));
	}
}
</pre>

Hinted at but not shown in this example are a number of supporting enums and utility methods (e. g. the mapping between CLR types and OData types) which complete the model of the OData expression language. For a the complete view, check out <a href="https://github.com/madelson/MedallionOData/tree/master/MedallionOData/Trees">the relevant files on github</a>. With this basis, we now have an easy-to-use means of representing and manipulating the OData query language which will simplify many future tasks.

<strong>A note on algebraic data types in C#</strong>

At first glance, the specification for the ODataExpression type in C# seems rather clunky compared to other languages which support this pattern more directly. For example, in F# this would be (using <a href="http://msdn.microsoft.com/en-us/library/dd233226.aspx">F# discriminated union types</a>):

<pre>
type ODataExpression =
    | Call of function : ODataFunction * arguments : ODataExpression list
	| Constant of value : object * type : ODataExpressionType
	...
</pre>

This representation is far more concise. F# also provides a benefit when "switching" on these types. In C#, one might write:

<pre>
switch (expression.Kind)
{
	case ODataExpressionKind.Call:
		return ProcessCall((ODataCallExpression)expression);
	case ODataExpressionKind.Constant:
		return ProcessConstant((ODataConstantExpression)expression);
	default:
		// because C# enums can be a non-named value, this default case handling is always required
		throw Throw.UnexpectedCaseValue(expression.Kind);
}
</pre>

Whereas in F# this is:

<pre>
let result = 
	match expression with
	| Call(function, arguments) -> // do something with function and arguments
	| Constant(value, type) -> // do something with value and type
	| _ -> // the default case is only required if you are missing cases for all types... and the compiler will enforce this!
</pre>

Given these differences, when I first started implementing the expression language in C#, I took a detour over to F# to see whether that might offer a more elegant approach. However, I in doing so I found several shortcomings of F#'s discriminated unions. 

First, there is the issue of argument validation. In C#, we use internal constructors to enforce the use of static factories, which contain validation. If we were worried about callers within the assembly, we could even make the constructors private and move the factories to the derived classes. In contrast, the F# approach as written does nothing to stop someone from creating an invalid expression (e. g. a call to a two-argument function with an argument list of length one). You can and should provide static factories in F# much like the ones in C#, but there does not seem to be a good way of forcing callers to use these factories while maintaining the ability to pattern match:

<pre>
module Shapes =
	// adding the private keyword here makes the implementation of the type private to the module
    type Shape = private Square of int | Circle of int
    
	// validated factory method example
    let makeSquare side : Shape = 
        if side < 0 then raise (System.ArgumentException("side"))
        else Square(side)

// works
System.Console.WriteLine((Shapes.makeSquare 2).ToString())
// fails with argument exception
System.Console.WriteLine((Shapes.makeSquare -2).ToString())

let x = match Shapes.makeSquare 2 with 
    // won't compile if the implementation of Shape is private
    | Shapes.Square(side) -> side
    | _ -> 100

// will compile UNLESS the implementation of Shape is private
System.Console.WriteLine(Shapes.Square(-2).ToString())
</pre>

Another approach is make the component type of each case a strongly-typed class which itself has argument validation (e. g. | Square of SquareData). This solves the validation issue, but makes the expression type declaration a great deal more verbose and very similar to the C# approach (one type per case, with the discriminated union playing the roll of the enum).

A second issue I came across is that there's no way to reference the case types directly outside of construction and pattern matching. For example, that means that we can't have a method that takes a Call expression as an argument. This makes it more difficult to encapsulate logic for handling certain types of expressions than in the C# case. It also prevents us from doing something like the following:

<pre>
public sealed class ODataMemberAccessExpression
{
	// in OData, member access (e. g. Foo.Bar) is only allowed on the implicit query parameter or another member expression
	public ODataMemberAccessExpression Expression { get; private set; }
	public PropertyInfo Member { get; private set; }
}

// or in F#
type Expression =
	// not allowed: has to be MemberAccess of Expression option * PropertyInfo, which is less type-safe!
	| MemberAccess of MemberAccess option * PropertyInfo
	...
</pre>

Thus, F# discriminated unions have both advantages and disadvantages as compared to the analogous C# pattern with respect to verbosity, type-safety, and argument validation. In the end, I decided to go with C# for this project because I found that my code benefited more from being able to reference case types directly than from concise pattern matching.