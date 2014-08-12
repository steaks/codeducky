- problem: want to run Node.exe from .NET
	- spoiler: we'll make this easy with MedallionShell
- start info: start the process, wait for exit, read stdin/out (already kind of verbose)
- problem: need to dispose of the process
- problem: escaping command-line args
	- solution: link to SO post
- problem: will hang if stdin/out not ready
	- can use Begin, but there is no great way to know about completion
	- solution: use tasks
- problem: what if the process just hangs?
	- solution: wait w/timeout + kill
- problem: blocking -> can use TaskCompletionSource + event to go fully async
- problem: what if the output/input data is large => use a custom task (e. g. a read-write loop)

- use MedallionShell!
- show auto-dispose, redirection syntax, if syntax, timeout via options
- mention: can do a lot more (e. g. other pipes, &&)

Scripting and shell languages are often built around the ability for one process to easily launch and work with the results of others. In .NET, this is typically done via the [ TODO link ] System.Diagnostics.Process API. The Process API is quite general and powerful, but it can be clunky and difficult to use correctly in some of the most common use cases. As a spoiler, I ended up wrapping much of this complexity into a new .NET library: [TODO link]; I'll show how that library greatly simplifies this task at the end. As an example, I recently wanted my application to launch an instance of NodeJS from .NET. I needed to write to Node's standard input, and capture the standard output text, standard error text, and exit code. Here's the code I started out with:

<pre>
// Non-functional code
using (var process = new Process
	{
		StartInfo = 
		{
			FileName = [Path to node],
			// in my case, these were some file paths and options
			Arguments = string.Join(" ", [arguments]),
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
	process.StandardInput.Write([input data]);
	// signals to the process that there's no more input coming
	process.StandardInput.Close();
	var outText = process.StandardOutput.ReadToEnd();
	var errText = process.StandardError.ReadToEnd();
	process.WaitForExit();
	var exitCode = process.ExitCode;
}	
</pre>

This code is quite verbose; unfortunately it's quite buggy as well.

One of the first problems we notice with this code is that the Arguments property on ProcessStartInfo is just a string. If the arguments we are passing are dynamic, we'll need to provide the appropriate escape logic before concatenating to prevent things like spaces in file paths from breaking. Escaping windows command line arguments is ridiculously complex; luckily, the code needed to implement it is well documented in [TODO link] this StackOverflow post. Thus, the first change we'll make is to add escaping logic:

<pre>
...
// Escape() implementation based on the SO post
Arguments = string.Join(" ", arguments.Select(Escape)),
...
</pre>

A less-obvious problem is that of deadlocking. All three process streams (in, out, and error) are finite in how much content they can buffer. If the internal buffer fills up, then whoever is writing to the stream will block. In this code, for example, we don't read from the out and error streams until after the process has exited. That means that we could find ourselves in a case where Node exhausts it's error buffer. In that case, Node would block on writing to standard error, while our .NET process is blocked reading to the end of standard out. Thus, we've found ourselves in a deadlock!

The Process API provides a method that seems designed for dealing with this: [TODO name] [TODO link]. With this method, you can subscribe to asynchronous "DataReceived" events instead of reading from the output streams directly. That way, you can listen to both streams at once. Unfortunately, this method provides no way to know when the last bit of data has been received. Because everything is asynchronous, it is possible (and I have observed this) for events to fire after WaitForExit() has returned.

Luckily, we can provide our own workaround using Tasks:

<pre>
...
var outTask = process.StandardOutput.ReadToEndAsync();
var errTask = process.StandardError.ReadToEndAsync();
process.WaitForExit();
var outText = outTask.Result;
var errText = errTask.Result;
...
<pre>

