A Stack Overflow <a href="http://stackoverflow.com/questions/21052437/are-these-two-lines-the-same-vs">question</a> encouraged me revisit how I choose between properties, fields, and methods. In general I take the nonchalant approach of what going with what feels right. However, it's nice to be reminded that poor decisions when choosing how to expose data leads to real bugs.

With renewed resolve, I searched for developers' opinions about how to choose between properties, fields, and methods. A few articles raised interesting points that I never considered. I particularly enjoyed a MSDN article that covered <a href="http://msdn.microsoft.com/en-us/library/vstudio/ms229054(v=vs.100).aspx">choosing between properties and methods</a> and a post where <a href="http://stackoverflow.com/users/22656/jon-skeet">Jon Skeet</a> explains <a href="http://csharpindepth.com/Articles/Chapter8/PropertiesMatter.aspx">choices between properties and fields</a>. I recommend reading both articles, especially the MSDN article about properties vs. methods.

<h3>Properties vs. Methods</h3>
I think the most important thing when deciding between properties and methods is to remember that developers treat properties more like fields than methods when they consume your code. So try to accommodate your consumers' tendency ensuring your properties act like fields and not like methods. The two biggest differences between fields and methods in my opinion are:
<h5>Accessing a field is quick. A method call can be long.</h5>
Let's take a look at how this assumption can lead to performance bugs.
<pre>
public class Foo
{
    public double? Result 
    {
        get { /*Execute a long calculation return a result */ }
    }

    public double? RetrieveResult()
    {
        /*Execute a long calculation return a result */
    }
}
</pre>
<span style="background-color: #f5f5f5;">Result</span> and <span style="background-color: #f5f5f5;">RetrieveResult()</span> accomplish the same functionality. However, they signal very different messages regarding performance. Consumers will tend to think that Result executes quickly because accessing fields is quick. They may not know to be concerned with with accessing Result repeatedly, but they may be more apprehensive about calling RetrieveResult() repeatedly. Therefore, you probably want to use a method if treating a property like a field leads to noticeable performance bugs.
<pre>//This doesn't look concerning from a performance perspective.
var result = foo.Result == null ? -1 : foo.Result;

//This code does look a bit worrisome
var result = foo.RetrieveResult() == null ? -1 : foo.RetrieveResult();</pre>
<h5>Accessing a fields doesn't produce side effects.  Method calls can produce side effects.</h5>
This assumption can lead to bugs when properties produce side effects.
<pre>
public class Foo
{
    private int _currentCustomer = 0;

    public int NextCustomer
    {
        get
        {
            _currentCustomer++;
            return _currentCustomer;
        }
    }

    public int GetNextCustomer ()
    {
        _currentCustomer++;
        return _currentCustomer;
    }
}
</pre>

Again, both the property and method accomplish the same functionality.  However, the <span style="background-color: #f5f5f5;">GetNextCustomer()</span> better signals that the function iterates through customers.  Consumers have less reason to suspect that <span style="background-color: #f5f5f5;">NextCustomer</span> moves to the next customer.  Therefore, you probably want to use a method if your property produces side effects.
<pre>
//This code looks reasonable because it doesn't look like
//the property should iterate through customers
var nextCustomer = foo.NextCustomer != 100 ? foo.NextCustomer : -1;

//This code should at least signal the consumer to read comments about 
//GetNextCustomer() because it could be interpreted as an iterator.
var nextCustomer = foo.GetNextCustomer() != 100 ? foo.GetNextCustomer() : -1
</pre>

<h3>Properties vs. Fields</h3>
Honestly, 95% of the time this decision doesn't matter much. However, save yourself the rare headache by  avoiding non private fields.  Most of these headaches occur when you realize that you want to update your library to use a property instead of a field.

For example, you may realize you want to add a check in the next version of your library.

<pre>
public class Foo
{
    public int NumCustomers;
}

public class Foo
{
    private int _numCustomers;
    public int NumCustomers 
    {
        get { return _numCustomers; }
        // Throw.If defined in http://www.codeducky.org/?p=95
        set { Throw.If(value < 0); _numCustomers = value; }
    }
}
</pre>

This change seems innocuous because the simple case will still compile.
<pre>
var numCustomers = foo.NumCustomers;
</pre>

However, changing a field to a property is a breaking change.  Jon Skeet provides a <a href="http://csharpindepth.com/Articles/Chapter8/PropertiesMatter.aspx">nice list</a> of reasons why changing a field to a property is a breaking change.  I'll provide another:
<pre>
public void DoStuff(out numCustomers)
{
    //do stuff with num customers
}
//Compiles when Foo.NumCustomers is a field but
//not when Foo.NumCustomers is a property
DoStuff(out numCustomers);
</pre>
