The enum type is one of those features that exists in nearly every programming language but is implemented slightly differently in each. C# is no exception. While the declarations and typical usage patterns look fairly standard, there are a number of quirks and tricks to be aware of.

<!--more-->

<strong>Enums are just numbers, except when they're not</strong>
Every C# enum type is has a numeric "underlying type", with each enum value having a matching (but not necessarily exclusive) numeric value of that type. Enums can be explicitly cast to and from their underlying type. In fact, in most cases the runtime bit representation of an Enum value is <i>exactly</i> that of the underlying type, which means that C# enums have no real overhead to speak of. By default, the underlying type is int, although this can be overriden via the extends/implements syntax. 

<pre>
// an int enum with default incrementing values
enum Suit { Spades, Diamonds, Clubs, Hearts }

// using a long underlying type and explict number assignments
enum Suit64 : long { Spades = 10, Diamonds = 20, Clubs = 30, Hearts = 40 }

int intValue = (int)Suit.Diamonds; // 1
Suit64 suit64 = (Suit64)20L; // Diamonds
</pre>

However, while in many ways enums are just named numeric constants, there are some cases where their behavior diverges. For example, despite the use of ":" to declare the underlying type, the actual base type of any enum is System.Enum:

<pre>
var isInt = Suit.Spades is int; // false
var isEnum = Suit.Spades is Enum; // true
var baseType = typeof(Suit).BaseType; // Enum

// the underlying type is still accessible, though
var underlyingType = typeof(Suit).GetEnumUnderlyingType(); // int
// there is also the clunkier Enum.GetUnderlyingType(Type), but 
// on my machine the Type method is marginally faster
</pre>

<strong>Enums know their string value</strong>
Another (quite useful) way in which enums differ from numeric constants is that they retain knowledge of their string value:

<pre>
var text = Suit.Clubs.ToString(); // "Clubs"
// these methods are clunkier but run 25% faster on my machine
var text2 = Enum.GetName(typeof(Suit), Suit.Clubs); // "Clubs"
var text3 = typeof(Suit).GetEnumName(Suit.Clubs); // "Clubs"
</pre>

The reverse (string to enum) is also possible via the parse methods on Enum:

<pre>
Enum suit = Enum.Parse(typeof(Suit), "Spades"); // Suit.Spades
Enum suit2 = Enum.Parse(typeof(Suit), "0"); // Suit.Spades
// TryParse provides easier error management and is more strongly typed via
// the out parameter
Suit suit3;
var success = Enum.TryParse("NotASuit", out suit3); // false
</pre>

<strong>Enums can be enumerated</strong>
"Enum" is short for "<a href="http://en.wikipedia.org/wiki/Enumerated_type">enumerated type</a>", so we better be able to enumerate the values. Once again, the Enum class provides an API for this:

<pre>
Array values = Enum.GetValues(typeof(Suit)); // [Spades, Hearts, Diamonds, Clubs]
// the annoying Array return type can be easily fixed a LINQ .Cast, and
// it's easy enough to wrap these two calls up into a custom utility method
IEnumerable<Suit> suits = values.Cast<Suit>();
</pre>

<strong>You can write extension methods on enums</strong>
While you can't add custom methods to an enum type directly, it is often possible to achieve much the same effect with extension methods:

<pre>
public static Color GetColor(this Suit @this)
{
	switch (@this)
	{
		case Suit.Spades:
		case Suit.Clubs:
			return Color.Black;
		case Suit.Diamonds:
		case Suit.Hearts:
			return Color.Red;
		default:
			throw new ArgumentException("Unexpected suit " + @this, "this"); 
	}
}

var color = Suit.Hearts.GetColor(); // Color.Red
</pre>

Unfortunately, it's not so easy to write an extension method for an arbitrary enum type, since there is no enum type constraint. You can extend Enum, but <a href="http://stackoverflow.com/questions/7995606/boxing-occurrence-in-c-sharp">this will cause the arguments to be boxed</a> and loses the context of the original type. A typical approach is to use the struct constraint, which prevents boxing but allows other value types through as well.

<pre>
// won't compile: "Constraint cannot be special class 'System.Enum'"
public static void MyExtension<TEnum>(this TEnum @this)
	where TEnum : Enum
{ ... }

// works, but isn't generic and causes boxing
public static void MyExtension(this Enum @this)
{ ... }

// works, but allows non-enum structs like int
public static void MyExtension<TEnum>(this TEnum @this)
	where TEnum : struct
{ ... }
</pre>

<strong>Enums are not type-safe</strong>
While extension methods are a great example of how C# lets you take advantage of an enum's compile-time strong typing, it's important to remember that enums are not truly type-safe because <i>a variable of an enum value can hold any value of the underlying type, not just one of the named values</i>. 

<pre>
Suit notARealSuit = (Suit)5; // no error
var text = notARealSuit.ToString(); // "5" 

// enum values can be verified with Enum.IsDefined()
var isASuit = Enum.IsDefined(typeof(Suit), notARealSuit); // false
</pre>

This is the reason that switch statements on enums still require the default error case shown above.

<strong>Enums can represent bit flags</strong>
While this lack of type-safety may seem frustrating, the silver lining is that it allows enums to represent sets of bit flags. To set up such an enum, simply assign each value a different power of two. Everyone has their favorite way of writing this; I like to use shifts to emphasize that each value is just a bit at a different position.

<pre>
// this attribute isn't strictly necessary, but it provides
// useful metadata and greatly improves the ToString() representation
[Flags]
enum BookFlags
{
	// most flags enums provide a named None or Default
	// zero value to represent the empty set
	Default = 0,
	HardCover = 1,
	InStock = HardCover << 1, // 2
	HasEBook = InStock << 1, // 4
	OnSale = HasEBook << 1, // 8
}

// we can create and test flag sets with bitwise operations
BookFlags flags = BookFlags.HardCover | BookFlags.InStock;
var text = flags.ToString(); // HardCover, InStock
var isInStock = (flags & BookFlags.InStock) == BookFlags.InStock; // true
// testing for a particular flag is also simplified by the HasFlag extension
var isInStock2 = flags.HasFlag(BookFlags.InStock); // true
</pre>

This approach is used a number of places in the .NET framework libraries to provide an efficient, concise representation of a set of options. Two common examples are the RegexOptions and StringSplitOptions enums.

<strong>Enums can have attributes</strong>
We've discussed how enums have access to both a name and a numeric value, but if you want to attach more metadata one approach is to add attributes. Enum values are fields, so any attribute that can decorate fields can decorate them:

<pre>
[Flags]
enum BookFlags
{
	...
	[Description("Hard cover")]
	HardCover = 1
	...
}

// an extension to retrieve enum attributes
public static TAttribute GetAttribute<TAttribute>(this Enum @this)
	where TAttribute : Attribute
{
	var field = @this.GetType().GetField(
		@this.ToString(), 
		// BindingFlags is yet another flags enum!
		BindingFlags.Public | BindingFlags.Static
	);
	return field.GetCustomAttribute<TAttribute>();
}

var description = BookFlags.HardCover
	.GetAttribute<DescriptionAttribute>().Description; // "Hard cover"
</pre>

While this approach is very readable and declarative, it's use of reflection means that it's not the most efficient. It this is an issue, consider storing metadata in a private static dictionary and exposing it via an enum extension method.


