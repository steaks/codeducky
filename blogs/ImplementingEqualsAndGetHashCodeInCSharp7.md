Much like C# 6, C# 7 seems to be all about making common tasks just a little bit easier. Recently, I blogged about some of the ways I'd found myself using <a href="http://www.codeducky.org/7-ways-use-c-7-throw-expressions/">throw expressions</a>. This time, it's implementing the old standbys Equals() and GetHashCode().

<!-- more -->

<h2>The old way</h2>

Let's say we had a simple class:

<pre>
class ProductInfo
{
	public Customer(int productId, string style)
	{
		this.ProductId = productId;
		this.Style = style;
	}

	pubilc int ProductId { get; }
	public string Style { get; }
}
</pre>

In C# 6, I would have implemented Equals() and GetHashCode() like so:

<pre>
public override bool Equals(object obj)
{
	var that = obj as ProductInfo;
	return that != null
		&& this.ProductId == that.ProductId
		&& this.Style == that.Style;
}

public override int GetHashCode()
{
	return unchecked((3 * this.ProductId) + EqualityComparer<string>.Default.GetHashCode(this.Style));
}
</pre>

<h2>The new way</h2>

Here's what it looks like in C# 7:

<pre>
public override bool Equals(object obj)
{
	return obj is ProductInfo that 
		&& (this.ProductId, this.Style).Equals((that.ProductId, that.Style));
}

public override int GetHashCode() => (this.ProductId, this.Style).GetHashCode();
</pre>

Just a little bit nicer!

Yes, the tuple trick could have been done previously using System.Tuple or anonymous types. However, those are both classes which means creating them requires a heap memory allocation. This might not be ideal for a type which is heavily used as a dictionary key or set member. The new tuple literals are value types, which means that no heap allocation is required. And of course, creating a tuple literal is just a tad bit more concise as well.