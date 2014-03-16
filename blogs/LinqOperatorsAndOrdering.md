LINQ Operators and Ordering

One of the lesser-known, but quite useful, features of LINQ to objects is that each of the operators is in some way order-preserving. For operations like Select(), Where(), and Concat() this is fairly obvious. It is less so for things like Distinct() or Join() which involve hashing under the hood, since that can easily destroy ordering. As a general rule, the LINQ extensions preserve order with respect to the "this" argument of the operator. For example:

<pre>
// prints 1, 2, 3, 4, 5 as opposed to 5, 4, 3, 2, 1
new[] { 1, 2, 3, 4, 5 }.Join(new[] { 5, 4, 3, 2, 1 }, i => i, i => i, (i1, i2) => i1).Dump();
// If you're unfamiliar with the .Dump() extension , this is available when writing snippets of code in the super-handy tool <a href="https://www.linqpad.net/">LinqPad</a>. If you // work regularly with C#, VB, or F# and you don't use LinqPad, <a href="https://www.linqpad.net/">download it</a> and give it a try. It's free!
</pre>

What does this mean for the various LINQ operators? Here's a quick summary:

<strong>Join</strong>
As mentioned previously, the output of Join maintains the order of the first (outer) sequence.

<strong>GroupBy</strong>
GroupBy gives us two forms of order preservation. First, the order of the IGroupings matches the order of the first appearance of each key within the original sequence. Second, the elements within each grouping maintain their relative order with respect to the original sequence. For example:

<pre>
var numbers = Enumerable.Range(1, count: 10);
var grouped = numbers.GroupBy(i => i % 3);
// gives:
// 1: { 1, 4, 7, 10 }
// 2: { 2, 5, 8 }
// 3: { 3, 6, 9 }
grouped.Dump();
</pre>

<strong>GroupJoin</strong>
This is essentially a combination of GroupBy and Join, and has the expected combined order preservation. Thus, the IGroupings are ordered by first appearance of the value in the outer sequence that matches the grouping key. The values within the groupings are ordered by their relative positions in the inner sequence:

<pre>
var numbers = Enumerable.Range(1, count: 5);
var groupJoined = numbers.Reverse()
	.GroupJoin(numbers, i => i % 3, i => i % 3, (outer, inner) => new { outer, inner });
// gives:
// { outer = 5, inner =  2: { 2, 5 } }
// { outer = 4, inner =  1: { 1, 4 } } 
// { outer = 3, inner =  0: { 3 } }
// { outer = 2, inner =  2: { 2, 5 } }
// { outer = 1, inner =  1: { 1, 4 } }
groupJoined.Dump();
</pre>

<strong>OrderBy</strong>
OrderBy performs a <a href="http://en.wikipedia.org/wiki/Category:Stable_sorts">stable sort</a> of the underlying sequence, so order is preserved among equal elements. Interestingly, that means that the outcome of sequence.OrderBy(x => x.A).ThenBy(x => x.B) is equivalent to that of sequence.OrderBy(x => x.B).OrderBy(x => x.A) (that said, using ThenBy() is be more efficient since it won't actually perform multiple sorts).

<strong>Distinct</strong>
Distinct returns maintains relative order by returning the first matching element for each group of equal elements.

<strong>Union</strong>
From an ordering perspective, a.Union(b) is equivalent to a.Concat(b).Distinct(): the first element from each group of equal elements in the combined sequence is returned.

<strong>Intersect</strong>
Intersect() simply maintains the ordering of the first sequence, removing all elements that are not in the second sequence.

<strong>SelectMany</strong>
With SelectMany(), each element in the original sequence is projected into a new sequence. Those sequences are then concatenated together to form the result such that the projected sequences maintain the same order as the elements that created them:

<pre>
var selectMany = new[] { 1, 2, 3 }.SelectMany(i => new[] { i + "a", i + "b" });
// gives { 1a, 1b, 2a, 2b, 3a, 3b }
selectMany.Dump();
</pre>

<strong>Why is this useful?</strong>
Order preservation may seem like a minor detail, but knowing about this behavior can simplify certain tasks. For example, if you are removing duplicates from a list created by a user, it is nice to be able to just run .Distinct() without worrying about scrambling the original list.

<strong>A note on collections</strong>
In addition to LINQ methods, C#'s two primary hash-based collections (HashSet<T> and Dictionary<TKey, TValue>) also seem to have undocumented support for order-preservation. For example:

<pre>
var random = new Random();
var numbers = Enumerable.Range(0, 10000);

// create a crazy custom comparer just in case this is related to a particular
// GetHashCode() implementation
// EqualityComparers.Create is defined <a href="http://www.codeducky.org/10-utilities-c-developers-should-know-part-two/">here</a>
var mapping = numbers.ToDictionary(i => i, i => random.Next());
var comparer = EqualityComparers.Create((int i) => mapping[i]);

var shuffled = numbers.OrderBy(i => random.Next()).ToList();

var hashSet = new HashSet<int>(shuffled, comparer);
Console.WriteLine(hashSet.SequenceEqual(shuffled)); // true

var dict = new Dictionary<int, bool>(comparer);
shuffled.ForEach(i => dict.Add(i, false));
Console.WriteLine(dict.Keys.SequenceEqual(shuffled)); // true
</pre>

Unfortunately, this behavior is not documented and thus likely cannot be relied upon.