A Stack Overflow <a href="http://stackoverflow.com/questions/21052437/are-these-two-lines-the-same-vs">question</a> encouraged me revisit how I choose between properties, fields, and methods. In general I take the nonchalant approach of what going with what feels right. However, it's nice to be reminded that poor decisions about how to expose data leads to real bugs.

With renewed resolve, I searched for developers' opinions about how to choose between properties, fields, and methods. A few articles raised interesting points that I never considered. I particularly enjoyed a MSDN article that covered <a href="http://msdn.microsoft.com/en-us/library/vstudio/ms229054(v=vs.100).aspx">choosing between properties and methods</a> and a post where <a href="http://stackoverflow.com/users/22656/jon-skeet">Jon Skeet</a> explains <a href="http://csharpindepth.com/Articles/Chapter8/PropertiesMatter.aspx">choices between properties and fields</a>. I recommend reading both articles; the MSDN article is particularly relevant to this post.

<!--more-->

<h3>Properties vs. Methods</h3>
I think the most important thing when deciding between properties and methods is to remember that developers treat properties more like fields than methods when they consume your code. So try to accommodate your consumers' tendency by ensuring your properties act like fields and not like methods.  The three biggest differences between fields and methods in my opinion are:
<h5>Accessing a field is quick. A method call can be long.</h5>
Developers tend to make this assumption about properties.  Let's take a look at how this assumption can lead to performance bugs.
<pre>
public class Foo
{
    public double? Result 
    {
        get { /*Execute a long calculation return a result */ }
    }

    public double? ComputeResult()
    {
        /*Execute a long calculation return a result */
    }
}
</pre>
<span style="background-color: #f5f5f5;">Result</span> and <span style="background-color: #f5f5f5;">ComputeResult()</span> accomplish the same functionality. However, they signal very different messages regarding performance. Consumers will tend to think that Result executes quickly because accessing fields is quick. They may not know to be concerned with with accessing Result repeatedly, but they may be more apprehensive about calling ComputeResult() repeatedly. Therefore, you probably want to use a method if treating a property like a field leads to noticeable performance bugs.
<pre>
//This doesn't look concerning from a performance perspective.
var result = foo.Result == null ? -1 : foo.Result;

//This code does look a bit worrisome
var result = foo.ComputeResult() == null ? -1 : foo.ComputeResult();</pre>
<h5>Accessing a fields doesn't produce side effects.  Method calls can produce side effects.</h5>
Incorrectly assuming a property doesn't produce side effects can lead to difficult to debug problems.  Properties that violate this assumption tend to also violate a third assumption I'll mention.
<h5>A fields returns the same value every access.  Method calls can return different values.</h5>
It's not required that fields return the same result with every access.  However, developers tend to assume they do because well...they look like they do because fields signal zero logic execution.

The following example shows how seemingly innocuous code get's developers in trouble when they don't realize a property changes state and/or returns inconsistent values.
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

    public int GetNextCustomer()
    {
        _currentCustomer++;
        return _currentCustomer;
    }
}
</pre>

Again, both the property and method accomplish the same functionality.  However, the <span style="background-color: #f5f5f5;">GetNextCustomer()</span> better signals that the function iterates through customers.  Consumers have less reason to suspect that <span style="background-color: #f5f5f5;">NextCustomer</span> moves to the next customer.  Therefore, you probably want to use a method if your property produces side effects.
<pre>
//This code looks reasonable because it doesn't look like
//the property should iterate through customers.
var nextCustomer = foo.NextCustomer != 100 ? foo.NextCustomer : -1;

//This code should at least signal the consumer to read comments about 
//GetNextCustomer() because it could be interpreted as an iterator.
var nextCustomer = foo.GetNextCustomer() != 100 ? foo.GetNextCustomer() : -1
</pre>

<h5>Ternary operator test</h5>
Since reading the aforementioned Stack Overflow question, I've started to use the ternary operator to expose properties that act more like methods than fields.  I think the ternary operator exposes misleading properties because it allows us to access a property for different reasons with little code.  We can access a property for condition evaluations in the first half and for value assignment in the second half of the operator.  The following ternary operator shows us why <span style="background-color: #f5f5f5;">DateTime.Now</span> <a href="http://stackoverflow.com/questions/5437972/why-is-datetime-now-a-property-and-not-a-method">should have been a method</a>.
<pre>
/// <summary>
/// Returns the DateTime of the submission if it was 
/// submitted on time and null if it was not submitted on time.
/// </summary>
public static DateTime? GetSubmissionDateIncorrect()
{
    //We first access DateTime.Now to assert the current time is before 
    //the deadline.  Then we access DateTime.Now to again to assign variable
    //submission because it looks like the property will return the same value.  
    //Oops...DateTime.Now will certainly return a different value which may even
    //be after the deadline.
    var deadline = new DateTime(2014, 3, 1);
    var submission = DateTime.Now < deadline
        ? DateTime.Now
        : default(DateTime?);
    return submission;
}
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
        get 
        { 
            return _numCustomers; 
        }
        // Throw.If defined in http://www.codeducky.org/?p=95
        set 
        { 
            Throw<ArgumentOutOfRangeException>.If(value < 0, "value"); 
            _numCustomers = value; 
        }
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
