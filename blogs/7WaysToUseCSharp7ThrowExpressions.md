One of the least-heralded new features in C# 7 is also one of my favorite: throw expressions. With this feature, you can now use a throw clause in a number of places where you couldn't previously. Like many of the recent C# changes, this won't revolutionize your coding by any means, but it will consistently make things a little bit cleaner and more concise. Here are a few of my favorite new ways to throw exceptions.

<!--more-->

<h2 id="switch">1. Ternary operators and "switch expressions"</h2>

Ternary operators are often far more concise than if-else blocks and switch statements. With C# 7, these constructs can easily throw exceptions as well:

<pre>
var customerInfo = HasPermission() 
	? ReadCustomer() 
	: throw new SecurityException("permission denied");

string timestamp = ...
var date = DateTimeOffset.TryParse(timestamp, out var dto) ? dto
	: long.TryParse(timestamp, out var unixSeconds) ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
	: throw new FormatException($"'{timestamp}' could not be parsed as a date");
</pre>

<h2 id="null-checks">2. Null check error messaging</h2>

"Object reference not set to an instance of an object" and "Nullable object must have a value" are two of the most common errors in C# applications. With throw expressions, it's easier to give a more detailed error message:

<pre>
decimal balance = account.Balance 
	?? throw new InvalidOperationException("account balance must be initialized");
</pre>

<h2 id="single">3. Single() error messaging</h2>

Competing with null check errors for the title of most common and unhelpful error message is "Sequence contains no elements". With the introduction of LINQ, C# programmers frequently use the Single() and First() methods to make assertions about the number of elements in a list or query. While these methods are concise, their failure provides little detail about what assertion was violated. Throw expressions provide an easy pattern for adding better error information without compromising brevity:

<pre>
var customer = dbContext.Orders.Where(o => o.Address == address)
	.Select(o => o.Customer)
	.Distinct()
	.SingleOrDefault() 
	?? throw new InvalidDataException($"Could not find an order for address '{address}'");	
</pre>

<h2 id="cast">4. Cast error messaging</h2>

C# 7 type patterns offer new ways to write casts. With throw expressions, we can also concisely provide specific error messages:

<pre>
var sequence = arg as IEnumerable 
	?? throw new ArgumentException("Must be a sequence type", nameof(arg));

var invariantString = arg is IConvertible c
	? c.ToString(CultureInfo.InvariantCulture)
	: throw new ArgumentException($"Must be a {nameof(IConvertible)} type", nameof(arg));
</pre>

<h2 id="expression-bodied-member">5. Expression bodied members</h2>

Throw expressions offer the most concise way yet to implement a method with a thrown error:

<pre>
class ReadStream : Stream
{
	...
	override void Write(byte[] buffer, int offset, int count) => 
		throw new NotSupportedException("read only");
	...
}
</pre>

<h2 id="disposal-checking">6. Disposal checks</h2>

Well-behaved IDisposable classes throw ObjectDisposedException on most operations after being disposed. Throw expressions can make these checks more convenient and less intrusive:

<pre>
class DatabaseContext : IDisposable
{
	private SqlConnection connection;
	
	private SqlConnection Connection => this.connection 
		?? throw new ObjectDisposedException(nameof(DatabaseContext));
	
	public T ReadById<T>(int id)
	{
		this.Connection.Open();
		
		...
	}
	
	public void Dispose()
	{
		this.connection?.Dispose();
		this.connection = null;
	}
}
</pre>

<h2 id="linq">7. LINQ</h2>

LINQ provides the perfect setting to combine many of the above usages. Ever since it was released as part of C# 3, LINQ has driven C# programming towards an expression-oriented, rather than statement-oriented style. Historically, LINQ has often forced developers to make trade-offs between adding meaningful assertions and exceptions to their code and staying within the concise expression syntax that works best with lambdas. Throw expressions solve this problem!

<pre>
var awardRecipients = customers.Where(c => c.ShouldReceiveAward)
	// concise inline LINQ assertion with .Select!
	.Select(
		c => c.Status == Status.None 
			? throw new InvalidDataException($"Customer {c.Id} has no status and should not be an award recipient") 
			: c
	)
	.ToList();
</pre> 

<h2 id="conclusion">Conclusion</h2>

As I've started to write code with C# 7, I keep finding more little ways in which throw expressions make code cleaner and more concise. Hopefully this post gives you a few ideas to try in your own code.