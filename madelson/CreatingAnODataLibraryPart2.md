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

2. Build up a list matching each token type to a matching regex pattern. The list is in descending order of precedence, so that "eq" matches as the equality operator before matching as the identifier "eq":

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

3. We then can then use this list of patterns to build a regex which acts as our lexer:

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