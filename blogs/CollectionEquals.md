I've <a href="http://www.codeducky.org/10-utilities-c-developers-should-know-part-two/">mentioned before</a> that <strong>collection equality</strong> is a relatively common and useful operation that has no built-in implementation in the .NET framework:

<pre>
bool CollectionEquals<T>(
	this IEnumerable<T> @this, 
	IEnumerable<T> that, 
	IEqualityComparer<T> comparer = null);
</pre>

Here I'm differentiating <em>collection</em> equality from <em><a href="https://msdn.microsoft.com/library/bb348567%28v=vs.100%29.aspx?f=255&MSPPError=-2147217396">sequence</a></em> or <em><a href="https://msdn.microsoft.com/en-us/library/dd412096(v=vs.110).aspx">set</a></em> equality: I want two collections to be considered equal if and only if they contain exact same elements, respecting duplicates but disregarding order. While I've <a href="https://gist.github.com/madelson/9178059#file-helpers-cs-L161">implemented this functionality</a> in the past, for a new <a href="https://github.com/madelson/MedallionUtilities/tree/master/MedallionCollections">utility library I'm creating</a> I wanted to see how much I could tune the implementation.

<!--more-->

<h1 id="quick-and-dirty">Quick and Dirty Solutions</h1>

Before jumping into my solution, it's worth going through various common approaches to this problem and the issues with each.

<h2 id="sort-method">Sorting + SequenceEqual</h2>

One popular option is to leverage <a href="https://msdn.microsoft.com/library/bb348567%28v=vs.100%29.aspx?f=255&MSPPError=-2147217396">Enumerable.SequenceEqual</a> by simply sorting the collections:

<pre>
return @this.OrderBy(x => x).SequenceEqual(@that.OrderBy(x => x))
</pre>

This one-liner is fine for many use-cases, but it is a poor generic solution. For one thing, it requires that the input elements be comparable, which is far from a given. It also makes it difficult for us to override the default <a href="https://msdn.microsoft.com/en-us/library/ms132151%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396">IEqualityComparer</a>: while SequenceEqual does have an overload that takes an IEqualityComparer, we'd also need a custom <a href="https://msdn.microsoft.com/en-us/library/8ehhxeaf(v=vs.110).aspx">IComparer</a> with the same equality semantics to pass to OrderBy. Finally, the sort calls mean that this method has an O(nlgn) runtime; we'll see that other solutions can do better.

<h2 id="except-method">Except</h2>

Another quick one-liner is to use <a href="https://msdn.microsoft.com/library/bb300779(v=vs.100).aspx">Except</a>:

<pre>
return @this.Except(that)
    .Concat(that.Except(@this))
	.Any();
</pre>

The main issue with this approach is that Except is <em>set-based</em>. That means this method considers [1, 1, 1] and [1, 1] to be equal: it actually implements set equality, not collection equality. While this is a linear approach, it has a lot of unnecessary overhead. The double Except calls do a lot of redundant work, and as written this will potentially enumerate each of the inputs twice, which can be problematic for lazy sequences.

<h2 id="dictionary-method">Counting Dictionary</h2>

A third common solution is to use a dictionary of item counts:

<pre>
var dict = new Dictionary<T, int>();
foreach (var item in @this) 
{
	int existingCount;
	dict[item] = dict.TryGetValue(item, out existingCount) ? existingCount + 1 : 1;
}

foreach (var item in that)
{
	int existingCount;
	if (!dict.TryGetValue(item, out existingCount))
	{
		return false;
	}
	if (existingCount == 1) { dict.Remove(item); }
	else { dict[item] = exitingCount - 1; }
}

return dict.Count == 0;
</pre>

While clunkier than the functional approaches, this implements the right semantics in linear time. For this reason, it will form the basis of our optimized solution. One odd caveat with the above code, though, is that .NET's dictionary does not, allow null keys. A generic solution should support collections with null elements, so we'll need to work around this limitation.

<h1 id="improvements-and-optimizations">Improvements and Optimizations</h1>

The dictionary-based approach above returns in linear time. Since in the worst case we'll have to examine every element in both collections to be sure that they are not equal, we can't really hope to improve on this. Instead, we'll be looking for ways to either (1) decrease the constant overhead factor or (2) optimize special cases that can be handled more quickly.

<h2 id="fast-count">Fast Counting</h2>

One of the best ways to short-circuit a collection equality check is to start by comparing the collection counts and bail out immediately if they don't match. We've defined out function to operate on IEnumerable as the most generic of the .NET collection interfaces. Thus, to take advantage of this optimization we'll need to resort to casting:

<pre>
private static bool TryFastCount<T>(IEnumerable<T> sequence, out int count)
{
	var collection = sequence as ICollection<T>;
	if (collection != null) { count = collection.Count; return true; }
	var readOnlyCollection = sequence as IReadOnlyCollection<T>;
	if (readOnlyCollection != null) { count = readOnlyCollection.Count; return true; }
	
	count = -1;
	return false;
}

// then in CollectionEquals
int thisCount, thatCount;
if (TryFastCount(@this, out thisCount)
	&& TryFastCount(that, out thatCount)
	&& thisCount != thatCount)
{
	return false;
}
</pre>

While casting is always unsavory, this optimization can make a huge difference in performance because in many real cases we can expect that this will be called with non-lazy collections. Cast-free alternatives (such as defining an overload of CollectionEquals() that operates on ICollection) tend to equally or more verbose, and also fail to apply the optimization in cases where one of the arguments is a materialized collection at runtime but has a static type of IEnumerable. This is likely why a very similar cast strategy is also used by <a href="http://referencesource.microsoft.com/#System.Core/System/Linq/Enumerable.cs,41ef9e39e54d0d0b">Enumerable.Count</a>.

<h2 id="build-probe-choice">Choosing the Build and Probe Sides</h2>

In the dictionary-based comparison approach, the two arguments play different roles. As in a <a href="https://en.wikipedia.org/wiki/Hash_join">hash join</a> in a relational database, one of the collections is used to <em>build</em> the dictionary of element counts, while the other is used to <em>probe</em> the dictionary, checking for matches. Previously, we arbitrarily chose the first collection to be the build side. Can we be smarter about this? Does it even matter? 

First, note that, if the collections end up being equal, then the choice is irrelevant because we'll do the same operations either way. However, when the collections are unequal then this choice can impact performance. As an example, imagine we're comparing the collections [1] and [1 .. 1000000]. If we pick the singleton collection as the build side, we create a dictionary of size one, and only have to enumerate two elements of the big collection to discover that the two are unequal. If, on the other hand, we use the singleton as the probe side, then we waste a lot of time building a dictionary of size 1000000 before returning. 

Unfortunately, as discussed above we may not know the counts of the collections beforehand, so it is difficult to make the right choice. However, in many cases we might know the count of one of the collections using the FastCount technique. In this case, <em>by picking the collection with unknown count as the build side we can guarantee that we short-circuit as early as possible</em>. This can be done by keeping track of the build side count as we build the dictionary, and breaking out early if we either underflow or overflow the known count. Here's the code:

<pre>
IEnumerable<T> buildSide, probeSide;
int count;
int? probeSideCount;
if (TryFastCount(@this, out count))
{
	buildSide = that;
	probeSide = @this;
	probeSideCount = count;
}
else if (TryFastCount(that, out count))
{
	buildSide = @this;
	probeSide = @that;
	probeSideCount = count;
}
else
{
	// fall back to arbitrary choice
	buildSide = @this;
	probeSide = that;
	probeSideCount = null;
}

var dict = new Dictionary<T, int>(comparer);
var buildSideCount = 0;
foreach (var item in buildSide)
{
	if (probeSideCount.HasValue && ++buildSideCount > probeSideCount.Value)
	{
		// quit early if we discover build side is bigger than probe side
		return false;
	}

	int existingCount;
	dict[item] = dict.TryGetValue(item, out existingCount) ? existingCount + 1 : 1;	
}

if (probeSideCount.HasValue && buildSideCount < probeSideCount.Value)
{
	// quit early if we discover build side is smaller than probe side
	return false;
}

// probe code does not change
</pre>

This approach builds on the FastCount technique by ensuring that we take maximum advantage of any count information we have.

<h2 id="sequence-equal">Assuming Sequence Equality</h2>

The previous two optimizations affect cases where the sequences are not equal. What about when they are? One thing we can do is to assume that the sequences are not only equal but also in the same order (which probably happens quite often in real use-cases). This optimistic approach allows us to defer allocating memory for the dictionary or computing hash codes until we know that doing so is actually necessary:

<pre>
using (var thisEnumerator = @this.GetEnumerator())
using (var thatEnumerator = that.GetEnumerator())
{
	while (true)
	{
		var thisFinished = thisEnumerator.MoveNext();
		var thatFinished = thatEnumerator.MoveNext();
		
		// when one enumerator finishes, we know the result
		if (thisFinished) { return thatFinished; }
		if (thatFinished) { return false; }
		
		// when we find unequal elements, break out of the sequence equality
		// logic and fall back to the dictionary approach
		if (!comparer.Equals(thisEnumerator.Current, thatEnumerator.Current)) { break; }	
	}
	
	// original dictionary-based code here, except using the enumerators instead of @this and that
}
</pre>

Note that, in order to combine this approach with counting-based optimizations, we simply need to add a counter to the while loop so we know how many elements have already been processed. How much overhead does optimizing for this case cost us? As it turns out, not very much. We have to create the enumerators anyway, and once some elements have been successfully determined to match by this loop we can exclude them from the dictionary. Thus, the only overhead is the last Equals call that causes the while loop to break.

<h2 id="double-check">Reducing Key Lookups</h2>

Assuming that we do get past the SequenceEqual check, one of the remaining innefficiencies occurs in creating and deconstructing the dictionary. Both the increment and decrement operations require two key lookups, one to fetch the existing count value and one to set the new value. This means two calls to GetHashCode() and at least two calls to Equals() for each element. We can address this issue by using a custom, simpler datastructure that offers built-in increment and decrement operations using a single key lookup. Here is an outline of the code (see the full code <a href="https://gist.github.com/madelson/6378650cbf3e0148d5ef#file-collectionequals-cs-L199">here</a>):

<pre>
private sealed class CountingSet<T>
{
	// this acts as the internal hash table
	private struct Bucket { internal uint HashCode; internal T Value; internal int Count }
	private Bucket[] buckets;
	private int populatedBucketCount; // tracks the # of buckets with Count > 0

	private int FindBucket(T value)
	{
		// finds the bucket index where we should store the count value
		// using the hash code for value
	}
	
	public void Increment(T value)
	{
		var bucketIndex = this.FindBucket(value);
		// increment count
		// if table is too full, resize the buckets array and re-hash all entries
	}
	
	public bool TryDecrement(T value)
	{
		var bucketIndex = this.FindBucket(value);
		// if the bucket's count > 0, decrement and return true
		// else return false
	}
	
	public bool IsEmpty { return this.populatedBucketCount == 0; }
}

// in CollectionEquals, the dictionary code becomes:
var dict = new CountingSet<T>();
foreach (var item in buildSide) { dict.Increment(item); }
foreach (var item in probeSide)
{
	if (!dict.TryDecrement(item)) { return false; }
}
return dict.IsEmpty;
</pre>

Since we are using a custom data structure, we can also do away with the pesky null key restriction.

<h1 id="results">Results</h1>

So, how well did we do? Here are some benchmarks I ran. Note that all results are displayed as a percentage vs. the dictionary method code. Thus, <strong>lower numbers are better, and any number less than 100% represents an improvement</strong>. The collections used were of of length ~1000 and contained integers.

<style>
	table.results th {
		vertical-align: top;
		text-transform: none;
		padding-left: 8px;
		padding-right: 8px;
	}
	table.results td {
		padding-left: 8px;
		padding-right: 8px;
	}
</style>
<table class="results">
	<tr>
		<th>Test Case</th>
		<th>Runtime</th>
		<th>Elements Enumerated</th>
		<th>Equals() Calls</th>
		<th>GetHashCode() Calls</th>
		<th>Key Optimization</th>
	</tr>
	<tr>
		<td>arrays with different lengths</td>
		<td>1.3%</td>
		<td>0.00%</td>
		<td>0.00%</td>
		<td>0.00%</td>
		<td>Fast Count</td>
	</tr>
	<tr>
		<td>larger array and smaller lazy sequence</td>
		<td>24.1%</td>
		<td>33.4%</td>
		<td>0.1%</td>
		<td>16.7%</td>
		<td>Build/Probe Choice</td>
	</tr>
	<tr>
		<td>smaller array and larger lazy sequence</td>
		<td>30.4%</td>
		<td>33.5%</td>
		<td>0.1%</td>
		<td>16.7%</td>
		<td>Build/Probe Choice</td>
	</tr>
	<tr>
		<td>sequence-equal lazy sequences</td>
		<td>18.4%</td>
		<td>100%</td>
		<td>33.3%</td>
		<td>0.0%</td>
		<td>Sequence Equality Check</td>
	</tr>
	<tr>
		<td>almost sequence-equal lazy sequences</td>
		<td>29.2%</td>
		<td>100%</td>
		<td>33.3%</td>
		<td>0.0%</td>
		<td>Sequence Equality Check</td>
	</tr>
	<tr>
		<td>equal but out-of-order lazy sequences</td>
		<td>59.2%</td>
		<td>100%</td>
		<td>50.1%</td>
		<td>50.0%</td>
		<td>Key Lookup Reduction</td>
	</tr>
</table>

<h1 id="conclusion">Conclusion</h1>

Obviously, there are many more potential benchmarks we could run, but hopefully this suite demonstrates the efficacy of our optimizations. 

I plan to publish this functionality shortly as part of a new <a href="https://github.com/madelson/MedallionUtilities/tree/master/MedallionCollections">utility library</a>, but for now I've made the complete code available <a href="https://gist.github.com/madelson/6378650cbf3e0148d5ef">here</a>.

If you have other ideas for how to improve this code still further, don't hesitate to post them in the comments!