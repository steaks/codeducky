<h1>Creating a .NET OData Library</h1>
<h2>Part 2: Lexing and Parsing</h2>

I've recently been working on a project to create a .NET library for implementing and querying services using Microsoft's <a href="http://www.odata.org/">OData</a> standard. This post is Part 2 of a series walking through the implementation (<a href="[TODO link]">Part 1 is here</a>). For the source code, check out <a href="https://github.com/madelson/MedallionOData">the MedallionOData repository on github</a>.

In <a href="[TODO link]">Part 1</a>, I discussed building out a set of classes modelling the OData expression language. The next step I undertook was to create a parser to convert OData URL strings to this abstract syntax tree form. This is generally achieved via a two-stage process: lexing (parsing the raw string into a series of simple tokens) and parsing (organizing the stream of tokens into a syntax tree). There are tons of libraries that will generate lexers and parsers for you based on a grammar specification. In this case, though, I decided to build my lexer and parser by hand.

<strong>The lexer</strong>

In C#, building a simple lexer "by hand" is a lot easier than it sounds, since the Regex class will do 90% of the work for you. Here are the basic steps:

1. Enumerate the token types
First, I created an enum representing the various types of tokens in the OData expression language, along with a whitespace token (ignored after lexing) and a catch-all error token (to aid in reporting lexing errors):

<pre>
internal enum ODataTokenKind
{
	// literals for various types
	NullLiteral,
	BinaryLiteral,
	BooleanLiteral,
	DateTimeLiteral,
	// ...

	// operators
	Eq,
	Ne,
	Gt,
	Ge,
	Lt,
	Le,
	And,
	// ...

	// sort directions
	Asc,
	Desc,

	// other symbols
	Identifier,
	LeftParen,
	RightParen,
	Comma,
	Slash,
	Star,

	WhiteSpace,
	/// <summary>
	/// Represents an unexpected character
	/// </summary>
	Error,
	/// <summary>
	/// Represents the end of the token stream
	/// </summary>
	Eof,
}
</pre>

2. Build up a list matching each token type to a matching regex pattern 
I built the following list, which is in descending order of precedence. This means that "eq" matches as the equality operator before matching as the identifier "eq":

<pre>
const string followedByNonWord = @"(?=\W|$)";
var tokenToRegex = new TupleList<ODataTokenKind, string>
{
	{ ODataTokenKind.NullLiteral, "null" },
	{ ODataTokenKind.BinaryLiteral, "(binary|X)'[A-Fa-f0-9]+'" },
	{ ODataTokenKind.BooleanLiteral, "true|false" },
	{ ODataTokenKind.DateTimeLiteral, @"datetime'(?<year>\d\d\d\d)-(?<month>\d\d)-(?<day>\d\d)T(?<hour>\d\d):(?<minute>\d\d)(:(?<second>\d\d)((?<fraction>\.\d+))?)?'" },
	{ ODataTokenKind.Int64Literal, "-?[0-9]+L" },
	{ ODataTokenKind.DecimalLiteral, @"-?[0-9]+(\.[0-9]+)?(M|m)" },
	{ ODataTokenKind.SingleLiteral, @"-?[0-9]+\.[0-9]+f" },
	{ ODataTokenKind.DoubleLiteral, @"-?[0-9]+((\.[0-9]+)|(E[+-]?[0-9]+))" },
	{ ODataTokenKind.Int32Literal, "-?[0-9]+" },
	{ ODataTokenKind.GuidLiteral, @"guid'(?<digits>DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD)'".Replace("D", "[A-Fa-f0-9]") },
	{ ODataTokenKind.StringLiteral, "'(?<chars>(''|[^'])*)'"},
	// we use the "followedByNonWord" lookahead here so that operators like eq won't match for identifiers that start with them (e. g. "get")
	{ ODataTokenKind.Eq, @"eq" + followedByNonWord },
	{ ODataTokenKind.Ne, @"ne" + followedByNonWord },
	{ ODataTokenKind.Gt, @"gt" + followedByNonWord },
	{ ODataTokenKind.Ge, @"ge" + followedByNonWord },
	{ ODataTokenKind.Lt, @"lt" + followedByNonWord },
	{ ODataTokenKind.Le, @"le" + followedByNonWord },
	{ ODataTokenKind.And, @"and" + followedByNonWord },
	{ ODataTokenKind.Or, @"or" + followedByNonWord },
	{ ODataTokenKind.Not, @"not" + followedByNonWord },
	{ ODataTokenKind.Add, @"add" + followedByNonWord },
	{ ODataTokenKind.Sub, @"sub" + followedByNonWord },
	{ ODataTokenKind.Mul, @"mul" + followedByNonWord },
	{ ODataTokenKind.Div, @"div" + followedByNonWord },
	{ ODataTokenKind.Mod, @"mod" + followedByNonWord },
	{ ODataTokenKind.Asc, @"asc" + followedByNonWord },
	{ ODataTokenKind.Desc, @"desc" + followedByNonWord },
	// TODO time, date-time offset
	{ ODataTokenKind.LeftParen, @"\(" },
	{ ODataTokenKind.RightParen, @"\)" },
	{ ODataTokenKind.Star, @"\*" },
	{ ODataTokenKind.Identifier, @"[a-zA-z_][a-zA-Z_0-9]*" },
	{ ODataTokenKind.WhiteSpace, @"\s+" },
	{ ODataTokenKind.Comma, "," },
	{ ODataTokenKind.Slash, "/" },
	{ ODataTokenKind.Error, @"." }, // matches any character not already matched
	{ ODataTokenKind.Eof, "$" }, // matches an empty string positioned at the end of the string
};
</pre>

The TupleList class is a simple convenience class which allows you to concisely declare an ordered list of tuples using collection initializer syntax. The full implementation is:

<pre>
public class TupleList<T1, T2> : List<Tuple<T1, T2>> { public void Add(T1 t1, T2 t2) { this.Add(Tuple.Create(t1, t2)); } }
</pre>

3. Combine the patterns into a regex:
I created the following Regex and wrapping Lexer class to split a string into OData tokens:

<pre>
static class Lexer
{
	private static readonly IReadOnlyList<ODataTokenKind> Kinds = Enum.GetValues(typeof(ODataTokenKind))
		.Cast<ODataTokenKind>()
		.ToArray();
	private static readonly Regex Pattern;
	static Lexer()
	{
		var tokenToRegex = ...
		Pattern = new Regex(
			// constructs a pattern like (?<NullLiteral>null)|(?<BooleanLiteral>true|false)|...
			string.Join("|", tokenToRegex.Select(t => string.Format("(?<{0}>{1})", t.Item1, t.Item2))),
			// these two options help with performance. ExplicitCapture means that only named capturing groups will be recorded as captures,
			// while compiled means that the regex will be compiled to an assembly on the fly
			RegexOptions.ExplicitCapture | RegexOptions.Compiled
		);
	}
	
	// Note that we return the entire Match object instead of just the text of the match. This is because a Match knows its index,
	// which is very useful for creating error messages
	public static IEnumerable<Tuple<ODataTokenKind, Match>> Lex(string text)
	{
		return Pattern.Matches(text).Cast<Match>()
			// each match of the pattern represents a match of one of the token types. However, we then have to figure out
			// which token type matched based on which named capturing group in the pattern matched. Here, .NET's Regex offers
			// us little help; we have to simply enumerate all token kinds and check each group for success
			.Select(m => Tuple.Create(Kinds.First(k => m.Groups[k.ToString()].Success), m))
			.Where(t => t.Kind != ODataTokenKind.Whitespace);
	}
}
</pre>

For the complete lexer implementation, check out <a href="https://github.com/madelson/MedallionOData/blob/master/MedallionOData/Parser/ODataExpressionLanguageTokenizer.cs">the source</a>.

<strong>The parser</strong>

With the lexer in place, we can begin building a parser. I chose to go with a <a href="http://en.wikipedia.org/wiki/Recursive_descent_parser">recursive descent</a> approach, which is likely the simplest type of parser you can build by hand. A recursive descent parser always has to know exactly what it will parse next (or be able to decide this via looking ahead at the token stream). That means that it starts by attempting to parse the lowest-priority type of expression, and then moves recursively down to higher priority constructs. For this reason, the first step is to build out the set of priority-ordered constructs:

<pre>
// precedence described by https://tools.oasis-open.org/issues/browse/ODATA-203
// * highest priority *
// group = ( expression )
// call = id ( expressionList )
// memberaccess = id [/ id]*
// simple = [literal | call | memberaccess | group]
// unary = [not]? simple
// factor = unary [[+ | -] unary]*
// term = factor [[* | / | %] factor]*
// comparison = term [[eq | ne | ...]* term]
// andExpression = comparison [and comparison]*
// orExpression = andExpression [or andExpression]*
// expression = orExpression
// expressionList = expression [, expression]*
</pre>

Now, we can translate each rule in the list into a parse function which calls the previous rule's function recursively. We'll get to the actual implementation of such a function in a moment, but first we have to define some basic utility methods that allow our parser to work with the token stream. The basic methods are Next() (peeks at and returns the next token in the stream) and Eat() which consumes a token from the stream. One common way to write these is to represent the token stream as an immutable linked list. Next just peeks at the head of the list, while Eat() "consumes" the head of the token by returning the tail of the list. In my parser, I actually chose to go with a less-functional approach of maintaining two instance variables: the list of tokens and a counter pointing to the current token. Thus, eat simply advances the counter. 

<strong>Conclusion</strong>

The lexer and parser complete the first step of implementing an OData service endpoint: parsing the OData query string into strongly-typed expressions. Next time, we'll look at how these expressions can be converted to LINQ so that they can actually be used to perform the filtering, sorting, and paging they represent.