On the surface, .NET's <a href="https://msdn.microsoft.com/en-us/library/system.random%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396">Random</a> class seems comparable to the random number APIs in other frameworks. However, it has a number of quirks that can make it tricky to use correctly. With randomness, bugs can manifest quite subtley: rather than an exception you might just end up slightly biased load balancing or a playlist shuffle that picks the same song first more often than not. Therefore, it's worth taking the time to make sure you know what you're doing! This post covers the basic usage patterns for the random class, as well as the gotchas and how the <a href="https://github.com/madelson/MedallionUtilities/tree/master/MedallionRandom">MedallionRandom</a> NuGet Package can help fill in the gaps.

<!--more-->

<h3 id="doubles">Random doubles</h3>

The one method that all random libraries seem to implement is generating a random double in [0, 1). The <a href="https://msdn.microsoft.com/en-us/library/system.random.nextdouble(v=vs.110).aspx">NextDouble()</a> method on Random does this admirably. MedallionRandom provides an overload of NextDouble which makes it easy to shift and scale the range, as well as a static NextDouble() API similar to <a href="https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Math/random">Math.random()</a> in Javascript and Java:

<pre>
random.NextDouble(min: -.5, max: 5);
Rand.NextDouble();
</pre>

<h3 id="integers">Random integers</h3>

Doubles were easy but integers are a good deal more tricky. 

The simplest method for generating a random integer is <a href="https://msdn.microsoft.com/en-us/library/9b3ta19y(v=vs.110).aspx">Next()</a>. The documentation says that this returns "a non-negative random integer", which seems fairly straightforward. However, there is also another restriction: the returned value is also <em>less</em> than int.MaxValue. Not less than or equal to: less than. This odd range makes the Next() method quite useless for most real purposes.

Next, we have two methods for generating integers in specified ranges: <a href="https://msdn.microsoft.com/en-us/library/zd1bc8e5(v=vs.110).aspx">Next(maxValue)</a> and <a href="https://msdn.microsoft.com/en-us/library/2dx6wyd4(v=vs.110).aspx">Next(minValue, maxValue)</a>. These methods are quite useful for everything from simulating die roles to selecting random elements from an array. The one thing to be aware of is that minValue is <em>inclusive</em> while maxValue is <em>exclusive</em>. The one "exception" to this is that Next(0) and Next(0, 0) both return 0.

<pre>
random.Next(6); // roll a six-sided die
var values = new[] { "a", "b", "c", "d" };
values[random.Next(values.Length)]; // select a random value from the array
</pre>

What if we just want a full set 32 or 64 random bits? Unfortunately, Random has nothing to offer here other than manually re-assembling these values from individual bytes. Luckily, MedallionRandom has us covered with its <strong>NextInt32()</strong> and <strong>NextInt64()</strong> extension methods.

<h3 id="booleans">Random booleans (coin flips)</h3>

Random number generation is frequently used to make a binary choice. Typically, this is done by comparing the result of NextDouble() to a threshold probability. MedallionRandom makes this even cleaner by providing a <strong>NextBoolean()</strong> extension:

<pre>
random.NextBoolean(); // fair coin flip
random.NextBoolean(probability: .75); // weighted coin flip (75% true)
</pre>

<h3 id="bytes">Random bytes</h3>

Sometimes you just need raw random data. This can be achieved using the <a href="https://msdn.microsoft.com/en-us/library/system.random.nextbytes(v=vs.110).aspx">NextBytes(buffer)</a> method, which takes in an array of bytes and fills it with random values. With MedallionRandom, you can also generate an infinite stream of bytes using the <strong>NextBytes()</strong> extension.

<h3 id="initialization">Thread-safety and the initialization problem</h3>

If there is one odd behavior of Random which trips of more people than anything else, it is the way that Random initializes itself. When an instance of Random is created, it uses an integer seed value to initialize its internal state. This state exactly determines the pseudo-random sequence that will be output by the Random going forward. Unfortunately, the seed used for Random's default constructor is <a href="https://msdn.microsoft.com/en-us/library/system.environment.tickcount(v=vs.110).aspx">Environment.TickCount</a>, which is the number of milliseconds since the system started. That means that <strong>Random instances created in close succession with the default constructor will produce exactly the same sequence of numbers</strong>. This burns people who do things like this:

<pre>
public static T SelectRandom(this List<T> list)
{
	return list[new Random().Next(list.Count)];
}
</pre>

When this function is called in a loop, we'll likely get the same value over and over again! How can we fix this?

One approach is to always provide Random with an explicit unpredictable seed such as <a href="https://msdn.microsoft.com/en-us/library/system.guid.newguid(v=vs.110).aspx">Guid.NewGuid().GetHashCode()</a>. Better yet, we can re-use an instance of Random across many calls to Next(). Unfortunately, this can prove inconvenient in multi-threaded scenarios because the Random class is not thread-safe. Luckily, MedallionRandom has a few utilities which can help us: <strong>Rand.Current</strong> is a static property which contains a thread-safe Random instance which is perfect for one-off use. We can also use <strong>Rand.Create()</strong> to generate a new Random instance with a more unpredictable seed value.

<h3 id="security">Security</h3>

This wouldn't be a proper discussion of random number generation if it didn't contain a warning that <strong>the Random class is not suitable for cryptographic purposes</strong>. Specifically, Random is designed for high performance and good statistical properties rather than unpredictability. This is fine for things like simulations, shuffling a song list, and load balancing, but makes it a poor choice for generating encryption keys, creating random passwords, or even selecting a random prize winner when money is on the line.

The secure alternative to Random is the <a href="https://msdn.microsoft.com/en-us/library/system.security.cryptography.rngcryptoserviceprovider(v=vs.110).aspx">RNGCryptoServiceProvider</a> class, which implements the <a href="https://msdn.microsoft.com/en-us/library/system.security.cryptography.randomnumbergenerator(v=vs.110).aspx">RandomNumberGenerator</a> abstract class. While this provides secure randomness, the API is very limited: it only supports generating random bytes. With MedallionRandom, we can create a bridge between the limited RandomNumberGenerator interface and the richer Random one:

<pre>
using (var rng = new RNGCryptoServiceProvider())
{
	Random random = rng.AsRandom();
	random.NextDouble(); // secure random doubles
}
</pre>

Another frustration with RNGCryptoServiceProvider is that it does not allow seeding. While this is a good thing from a security perspective, it can be limiting in tests. One way to address this is to use the Random interface throughout, feeding in an RNGCryptoServiceProvider-backed Random in production and a seeded Random instance in tests. Another approach is to implement your own testing instance of RandomNumberGenerator backed by a seeded Random.

<h3 id="repeatability">Repeatability</h3>

Speaking of seeding and repeatability, one disadvantage of Random is that <strong>the API does not guarantee that the underlying algorithm will stay the same over time</strong>. In particular, the <a href="https://msdn.microsoft.com/en-us/library/system.random%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396">documentation</a> says that <em>Random objects in processes running under different versions of the .NET Framework may return different series of random numbers even if they're instantiated with identical seed values</em>. This is fine in most cases; in all likelihood it's unlikely to change any time soon. That said, if you have an application that depends on this kind of consistency (e. g. an online game with a local client), you might want to look elsewhere. 

One good alternative is MedallionRandom's implementation of <a href="https://docs.oracle.com/javase/8/docs/api/java/util/Random.html">Java's random number generation algorithm</a>. Unlike .NET, Java guarantees the exact behavior of it's Random API across versions. Using <strong>Rand.CreateJavaRandom</strong>, you can create a Random instance which produces values that, for any given seed, match Java's output bit-for-bit. This provides framework version-independent consistency and can also be of help when porting applications from Java to .NET.

<h3 id="shuffling">Shuffling</h3>

Randomly ordering a sequence of elements is a simple process that is <a href="https://blog.codinghorror.com/the-danger-of-naivete/">surprisingly easy to get wrong</a>. One quick and correct way to shuffle in .NET is:

<pre>
Random random = ...
var shuffled = myList.OrderBy(_ => random.Next()).ToArray();
</pre>

This approach is sub-optimal, taking O(Nlog(N)) time. With MedallionRandom, you have access to the O(N) time <a href="https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle">Fisher-Yates</a> shuffle. This is implemented as a lazy shuffle, so it does even less work when the sequence is only partially enumerated.

<pre>
Random random = ...
var shuffled = myList.Shuffled(random).ToArray();
var shuffled = myList.Shuffled().ToArray(); // uses Rand.Current
var top10Random = myList.Shuffled().Take(10).ToArray(); // leverage lazy shuffle

myList.Shuffle(random); // in-place shuffle for mutable IList
myList.Shuffle(); // in-place shuffle with Rand.Current
</pre>

<h3 href="distributions">Other distributions</h3>

Everything we've talked about so far has assumed that we want our random numbers to be <em>uniformly</em> distributed. This is appropriate for most applicationsbut sometimes you'll want to sample values from <a href="https://en.wikipedia.org/wiki/List_of_probability_distributions">other distributions</a>.

MedallionRandom provides some help here with the <strong>NextGuassian()</strong> extension which produces <a href="https://en.wikipedia.org/wiki/Normal_distribution">normally-distributed</strong> values. For working with other types of distributions, you'll likely want to use a more heavy-weight library like <a href="https://www.nuget.org/packages/MathNet.Numerics/">Math.NET</a> instead.

<h3 href="implementing">Implementing your own Random</h3>

There are <a href="https://en.wikipedia.org/wiki/List_of_random_number_generators#Pseudorandom_number_generators_.28PRNGs.29">numerous approaches to random number generation</a> which vary in speed, period (length of the sequence before it repeats), and statistical quality. The underlying algorithm used by Random is a <a href="https://rosettacode.org/wiki/Subtractive_generator">subtractive</a> algorithm. The subtractive approach makes for very inexpensive Next() calls but stores rather a lot of internal state (58 integers) and has a lengthy initialization routine relative to many common alternatives. Perhaps you'd like to implement the Random interface with a different algorithm.

Writing your own class that extends Random is actually rather difficult. You have to override not only the protected Sample() method but also Next(), Next(minValue, maxValue), and NextBytes() with your own implementations. If you miss one of these, you might find that some of the public methods end up calling through to the base class algorithm instead of your algorithm. Furthermore, most random algorithms only output an integer or double: more work is required to map this to other types of random outputs. In contrast, implementing your own random algorithm in Java requires <a href="https://docs.oracle.com/javase/8/docs/api/java/util/Random.html#next-int-">implementing only a single method</a>.

If you are looking to build your own implementation of Random, it may be worth looking at how <a href="https://github.com/madelson/MedallionUtilities/blob/efeed6adf0fd9b403b1db859c7114628447675cb/MedallionRandom/Rand.cs#L411">MedallionRandom does this internally</a> with a NextBits random base class that makes it easy to tack on alternative Random implementations. 

<h3 href="guid">A note about GUIDs</h3>

Globally Unique IDentifiers (GUIDs) are another potential source of randomness available in .NET. I even mentioned above how a Guid could be used to seed an instance of Random with a more unpredictable value. The important thing to remember about GUIDs is that they are designed to be unique rather than Random. Some older GUID-generation algorithms were partially composed of predictable values like the current machine's MAC address and the current time to help guarantee this uniqueness. While today's .NET GUIDs mostly consist of random bytes, it's important to note that <a href="https://en.wikipedia.org/wiki/Universally_unique_identifier#Version_4_.28random.29">certain bits in the GUID are non-random</a>. Thus, <strong><a href="https://msdn.microsoft.com/en-us/library/system.guid.tobytearray%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396">Guid.NewGuid().ToByteArray()</a> is <em>not</em> a good approach for generating random bytes</strong>.

<h3 href="conclusion">Conclusion</h3>

Whew, that was a long one. Hopefully, we've covered most of the basics with respect to working with random numbers in C# and .NET. While the built-in APIs have a number of shortcomings and even some pitfalls, they are easy enough to work around, particularly with the help of the MedallionRandom library. Happy coin flipping!
