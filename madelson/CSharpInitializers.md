Most C# programmers are familiar with the basics of <a href="http://msdn.microsoft.com/en-us/library/bb384062.aspx">object and collection initializers</a>. This handy piece of syntax sugar greatly reduces the need for custom constructors by letting you populate a object or collection with multiple values in one compound expression.





dictionary initializer
tuple list
anon arrays
skipping "new"
One of the annoying things about initializers as they're typically used is that they are not much help when you have an object that's already partially populated instead of freshly constructed. One place this comes up is when trying to provide defaults. Let's say we have the following classes:

<pre>

<pre>