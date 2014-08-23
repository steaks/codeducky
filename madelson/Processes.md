Scripting and shell languages are often built around the ability for one process to easily launch and work with the results of others. This is the primary mode of processing in bash, while <a href="http://mentalized.net/journal/2010/03/08/5_ways_to_run_commands_from_ruby/">ruby supports at least 5 built-in approaches</a> with varying levels of flexibility and conciseness.

In .NET, this is kind of operation is typically done via the <a href="http://msdn.microsoft.com/en-us/library/system.diagnostics.process(v=vs.110).aspx">System.Diagnostics.Process</a> API. The Process API is quite general and powerful, but it can be clunky and difficult to use correctly in the common use cases that are handled so well by the languages above. As a spoiler, I ended up wrapping much of this complexity into a new .NET library: <a href="https://github.com/madelson/MedallionShell">MedallionShell</a>; I'll show how that library greatly simplifies this task <a href="#medallion-shell">at the end of this post</a>. 

<!--more-->

As an example, I recently wanted my application to launch an instance of NodeJS from .NET to run the <a href="http://lesscss.org/">less css</a> compiler. I needed to write to Node's standard input while capturing the standard output text, standard error text, and exit code. 

<h2 id="initial-attempt">An initial attempt</h2>

Here's the code I started out with:

<pre>
// not-quite-functional code
using (var process = new Process
	{
		StartInfo = 
		{
			FileName = /* Path to node */,
			// in my case, these were some file paths and options
			Arguments = string.Join(" ", new[] { arg1, arg2, ... }),
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			UseShellExecute = true,
		}
	}
)
{
	process.Start();
	process.StandardInput.Write(/* input data */);
	// signals to the process that there's no more input coming
	process.StandardInput.Close();
	var outText = process.StandardOutput.ReadToEnd();
	var errText = process.StandardError.ReadToEnd();
	process.WaitForExit();
	var exitCode = process.ExitCode;
}	
</pre>

This code is quite verbose; unfortunately it's quite buggy as well.

<h2 id="arguments">Dealing with process arguments</h2>

One of the first problems we notice with this code is that the Arguments property on ProcessStartInfo is just a string. If the arguments we are passing are dynamic, we'll need to provide the appropriate escape logic before concatenating to prevent things like spaces in file paths from breaking. Escaping windows command line arguments is oddly complex; luckily, the code needed to implement it is well documented in <a href="http://stackoverflow.com/questions/5510343/escape-command-line-arguments-in-c-sharp">this StackOverflow post</a>. Thus, the first change we'll make is to add escaping logic:

<pre>
...
// Escape() implementation based on the SO post
Arguments = string.Join(" ", new[] { arg1, arg2, ... }.Select(Escape)),
...
</pre>

<h2 id="deadlocks">Dealing with deadlocks</h2>

A less-obvious problem is that of deadlocking. All three process streams (in, out, and error) are finite in how much content they can buffer. If the internal buffer fills up, then whoever is writing to the stream will block. In this code, for example, we don't read from the out and error streams until after the process has exited. That means that we could find ourselves in a case where Node exhausts it's error buffer. In that case, Node would block on writing to standard error, while our .NET app is blocked reading to the end of standard out. Thus, we've found ourselves in a deadlock!

The Process API provides a method that seems designed for dealing with this: <a href="http://msdn.microsoft.com/en-us/library/system.diagnostics.process.beginoutputreadline(v=vs.110).aspx">BeginOutput/ErrorReadLine</a>. With this method, you can subscribe to asynchronous "<a href="http://msdn.microsoft.com/en-us/library/system.diagnostics.process.outputdatareceived(v=vs.110).aspx">DataReceived</a>" events instead of reading from the output streams directly. That way, you can listen to both streams at once. Unfortunately, this method provides no way to know when the last bit of data has been received. Because everything is asynchronous, it is possible (and I have observed this) for events to fire after WaitForExit() has returned.

Luckily, we can provide our own workaround using Tasks to asynchronously read from the streams while we wait for Node to exit:

<pre>
...
var outTask = process.StandardOutput.ReadToEndAsync();
var errTask = process.StandardError.ReadToEndAsync();
process.WaitForExit();
var outText = outTask.Result;
var errText = errTask.Result;
...
</pre>

<h2 id="timeout">Adding a timeout</h2>

Another issue we'd like to handle is that of a process hanging. Rather than waiting forever for the process to exit, our code would be more robust if we enforced a timeout instead:

<pre>
...
if (!process.WaitForExit(TimeoutMillis))
{
	process.Kill();
	throw new TimeoutException(...);
}
...
<pre>

<h2 id="async">Async all the way!</h2>

While we are now using async IO to read from the process streams, we are still blocking one .NET thread while waiting for the process to complete. We can further improve efficiency here by going fully async:

<pre>
// now inside an async method
using (var process = new Process
	{
		...
		EnableRaisingEvents = true,
		...
	}
)
{
	...	
	var processExitedSource = new TaskCompletionSource<bool>();
	process.Exited += (o, e) => processExitedSource.SetResult(true);
	
	var exitOrTimeout = Task.WhenAny(processExitedSource.Task, Task.Delay(Timeout));
	if (await exitOrTimeout.ConfigureAwait(false) != processExitedSource.Task)
	{
		process.Kill();
		throw new TimeoutException(...);
	}
	...
}
</pre>

<h2 id="data-volume">Adapting to larger data volumes</h2>

Another question that might come up when trying to generalize this approach is that of data volume. If we are piping a large amount of data through the process, we'll likely want to replace the convenient ReadToEndAsync() calls with async read loops that process each piece of data as it comes in.

<h2 id="medallion-shell">Switching to <a href="https://github.com/madelson/MedallionShell">MedallionShell</a></h2>

We've now built out a (hopefully) correct, robust, and efficient piece of code for working with a process from .NET. However, hopefully this example has convinced you that the .NET Process API is not quite up to the job when it comes to ease-of-use. To that end, I'm going to present an alternative: the <a href="https://github.com/madelson/MedallionShell">MedallionShell</a> library (<a href="https://www.nuget.org/packages/medallionshell">available on NuGet</a>). Here's the equivalent logic using MedallionShell:

<pre>
var command = Command.Run(
	PathToNode, 
	new[] { arg1, arg2, ... }, 
	options => options.Timeout(timeout)
);
command.StandardInput.PipeFromAsync([input data]);

// in an async method, we could use var result = await command.Task;
var outText = command.Result.StandardOutput;
var errText = command.Result.StandardError;
var exitCode = command.Result.ExitCode;
</pre>

With MedallionShell, arguments are automatically escaped, process streams are automatically buffered to prevent deadlock, and everything is wrapped nicely in an async-friendly Task-based API. We don't even have to worry about calling Dispose(): by default the Process is disposed automatically upon completion of the command.

MedallionShell also offers operator overloads to enable bash-like redirection of standard input and standard output. That means you can use "<" and ">" to pipe data to and from streams, files, and collections. You can even use "|" to pipe data from one Command object to another.

<pre>
var command = Command.Run(...) < [input data];
var result = await command.Task;
</pre> 


