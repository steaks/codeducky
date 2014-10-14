C# <a href="http://msdn.microsoft.com/en-us/library/bb384062.aspx">object, array, and collection initializers</a> provide a concise and convenient syntax for building objects in a single, JSON-like code statement. For example, many examples of constructing a Process object show something like the following:

<pre>
var process = new Process { StartInfo = new ProcessStartInfo { FileName = "a.exe" } };
</pre>

However, a less common form of object initialization can make such code even more terse.

<h1>"New-less" initialization</h1>

What I'll call "new-less" initializers allow you to eschew the "new XXX" part of the initialization statement for nested initializers, leading to increased brevity: 

<pre>
var process = new Process { StartInfo = { FileName = "a.exe" } };
</pre>

Given how long class names can get in .NET, this can be a big win! 

<h1>Semantic differences</h1>

However, before you go and shorten all your initializer statements, it's important to point out that the semantics of the new-less initializer are different from those of the more common form. Specifically, with this initializer we don't construct a new ProcessStartInfo object; instead, we just assign values to the properties of the current one. To clarify, here's how the two examples above would look without an initializer:

<pre>
// classic initializer
// var process = new Process { StartInfo = new ProcessStartInfo { FileName = "a.exe" } };
var process = new Process();
var startInfo = new ProcessStartInfo();
startInfo.FileName = "a.exe";
process.StartInfo = startInfo;

// new-less initializer
// var process = new Process { StartInfo = { FileName = "a.exe" } };
var process = new Process();
process.StartInfo.FileName = "a.exe";
</pre>

Because of this difference, we can't use the new-less initializer if the default value of the property we're working with is null. On the other hand, it means that we CAN use these initializers with read-only properties. For example, the <a href="http://msdn.microsoft.com/en-us/library/system.net.mail.mailmessage(v=vs.110).aspx">System.Net.Mail.MailMessage</a> class defines a To property for the collection of destination addresses. While the collection itself is mutable, the property is read-only, so you can't populate it with a traditional initializer. The new-less initializer works just fine, though:

<pre>
var message = new MailMessage 
{
	// won't compile: To is read-only
	To = new MailAddressCollection { "madelson@codeducky.org" },
}

// works as desired
var message = new MailMessage { To = { "madelson@codeducky.org" } };
</pre>

<h1>Conclusion</h1>

New-less initializer syntax allows you to make your code a bit more concise and to use initialization syntax for configuring read-only properties. Indeed, since most base class library and popular .NET package classes follow the <a href="http://stackoverflow.com/questions/1969993/is-it-better-to-return-null-or-empty-collection">empty over null</a> pattern for collection properties, you can nearly always take advantage of new-less syntax for them. Finally, using new-less initialization also means that you benefit from leaving in place any defaults an object was initialized with.

