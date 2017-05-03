I'm probably one of the few programmers out there whose go-to scripting language is... C#. To many, especially those who don't use C# on a daily basis, this will sounds crazy. How can you possibly be productive if each script requires loading up Visual Studio, creating a new project with it's associated dozens of files and folders, and pounding out all of the namespace and class boilerplate required to run a few lines of code? Wouldn't something like Python or Powershell or even Bash be preferable?

The answer is that modern tooling and language features have greatly reduced the startup overhead to rapidly authoring, running, and maintaining C# scripts.

<!--more-->

<h2 id="linqpad">LinqPad</h2>

Scripting requires a development environment where you can rapidly write, execute, and iterate on scripts and snippets of code. As much as I love Visual Studio, it is waaaaaay too clunky to serve in this capacity. Enter <a href="https://www.linqpad.net/CodeSnippetIDE.aspx">LinqPad</a>.

LinqPad is a sort of hybrid between a text editor, IDE, and REPL loop. You can write C# (or VB or F#) code files with intellisense support and save them, but you can also quickly execute a file or highlighted portion of the file with a hotkey. Some of the most scripting-friendly features include:

* Not having to write using, namespace, or class boilerplate: you can compile and run any series of C# statements
* By default, scripts write to interactive HTML-based "console" output. The built-in Dump() extension method on any object pretty-prints in an expandable/collapsible view
* Easily install NuGet packages
* Easily auto-import required namespaces
* Configurable empty script template allows you to start with your preferred set of packages and imports
* Can maintain a LinqPad-global file of useful utility methods and invoke them from any script
* Can save files to be executed later from LinqPad or the command line. Referenced packages are part of the file format and will be re-downloaded if needed upon execution

With LinqPad freeing us from C#'s traditional project system baggage, it becomes easier to appreciate just how well-suited C# is for common scripting tasks. Here's a quick overview of some key building blocks.

<h2 id="finding-files">Finding files</h2>

Many scripts begin with locating a bunch of files to be processed. In C#, we have Directory.GetFiles (or the lazy version Directory.EnumerateFiles) to do this as a one-liner:

<pre>
// SearchOption.AllDirectories is key: it makes the search recursive
var cSharpFiles = Directory.GetFiles(@"C:\dev\MyProject", "*.cs", SearchOption.AllDirectories);
</pre>

<h2 id="read-write-files">Reading and writing files</h2>

While C# offers a bevy of Java-like file APIs based on Streams and Readers, it also includes the static File API which is perfect for scripting:

<pre>
// reading
byte[] bytes = File.ReadAllBytes(path);
string text = File.ReadAllText(path);
IEnumerable<string> lines = File.ReadLines(path);

// writing
File.WriteAllBytes(path, bytes);
File.WriteAllText(path, text);
File.AppendAllText(path, text);
File.WriteAllLines(path, lines);
File.AppendAllLines(path, lines);
</pre>

It's hard to imagine these operations being much more concise!

<h2 id="regex">Regular expressions</h2>

Now that we're reading file contents, the next step is likely processing it. Regular expressions are a classic tool for this. C#'s Regex class provides both scripting convenience through it's static API as well as power and usability through features like in-regex comments and named capturing groups:

<pre>
string code = ...
var comments = Regex.Matches(code, @"//\s*(?<comment>[^\n]*)\s*(\n|$)|/\*(?<comment>.*?)\*/", RegexOptions.Singleline)
	.Cast<Match>()
	.Select(m => m.Groups["comment"].Value);
</pre>

<h2 id="web-requests">Web requests</h2>

Another common scripting task involves hitting web endpoints for automation or scraping purposes. There are various APIs at our disposal for this, but my go-to for quick tasks is the easy-to-use WebClient:

<pre>
var codeDucky = new WebClient().DownloadString("http://codeducky.org");
</pre>

<h2 id="html-parsing">HTML parsing</h2>

Speaking of web scraping, we'll need to parse some HTML. Useful HTML parsing is tricky because browsers are notoriously lenient. While nothing is built into .NET for this, the excellent <a href="https://www.nuget.org/packages/HtmlAgilityPack/">HtmlAgilityPack NuGet package</a> is more than up for the task:

<pre>
// find all the links on codeducky!
var doc = new HtmlDocument();
doc.LoadHtml(new WebClient().DownloadString("http://codeducky.org"));
var urls = doc.DocumentNode.Descendants("a")
	.Select(anchor => anchor.Attributes["href"].Value)
	.Distinct();
</pre>

<h2 id="parsing">More parsing</h2>

While we're on the topic of parsing, here are some good tools for working with other common data formats:

<style>
	table.formats th {
		vertical-align: top;
		text-transform: none;
		padding-left: 8px;
		padding-right: 8px;
	}
	table.formats td {
		padding-left: 8px;
		padding-right: 8px;
	}
</style>
<table class="formats">
<tr>
	<th>Format</th>
	<th>Tool</th>
</tr>
<tr>
	<td>JSON</td>
	<td><a href="https://www.nuget.org/packages/newtonsoft.json/">Json.NET</a></td>
</tr>
<tr>
	<td>XML</td>
	<td><a href="https://msdn.microsoft.com/en-us/library/mt693072.aspx">LINQ to XML</a></td>
</tr>
<tr>
	<td>CSV</td>
	<td><a href="https://www.nuget.org/packages/CsvHelper/">CsvHelper</a></td>
</tr>
<tr>
	<td>XLSX</td>
	<td><a href="https://www.nuget.org/packages/EPPlus/">EPPlus</a></td>
</tr>
</table>

<h2 id="processes">Invoking other processes</h2>

Scripts frequently have to execute other programs and processes. C#'s native Process API is about <a href="http://www.codeducky.org/process-handling-net/">as hard to use as it gets</a>. Luckily, I've written a little NuGet package called <a href="https://github.com/madelson/MedallionShell">MedallionShell</a> which makes this task quite easy:

<pre>
Command.Run("git", "clone", "git@github.com:madelson/MedallionShell.git").Wait();
</pre>

<h2 id="sql">Querying databases</h2>

Scripts frequently need to query databases, so it's important to be able to do this in a low-ceremony way that doesn't involve extensive modeling or configuration. The built-in ADO.NET APIs are easy to use but can be kind of verbose. When I have a script that makes more than a few queries, I tend to pull in a micro-ORM like <a href="https://github.com/StackExchange/Dapper">Dapper</a>:

<pre>
var conn = new SqlConnection(connectionString);
conn.Open();

// while Dapper works great with static types, it's dynamic support is handy for quick scripts
var account = conn.Query<dynamic>(@"
		SELECT Name, Address, Country FROM Account WHERE Id = @Id", 
		new { Id = Id }
	)
	.FirstOrDefault();
Console.WriteLine(account.Name);
</pre>

<h2 id="linq">LINQ</h2>

Unix command line scripting showed how the concept of a pipeline is a powerful model for thinking about many scripting tasks. In C#, LINQ provides a concise and convenient means of flowing data through a series of operations and transformations. Furthermore, the AsParallel() extension provides a trivial way of throwing more CPU resources at a slow script. As an example, here's a quick LINQ script for finding duplicate images on disk:

<pre>
var directoriesToSearch = new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) }
var duplicates = directoriesToSearch
	// this results in a ~5x speedup on my machine!
	.AsParallel()
	// walk the file system
	.SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
	// as an optimization, first look for duplicates by size only
	.GroupBy(f => new FileInfo(f).Length)
	// throw away files of unique size
	.Where(g => g.Count() > 1)
	// re-flatten to a sequence of files
	.SelectMany(g => g)
	// now do the real check using an MD5 hash
	.GroupBy(f => BitConverter.ToString(MD5.Create().ComputeHash(File.ReadAllBytes(f))))
	// throw away files of unique hash
	.Where(g => g.Count() > 1);
</pre>

This functional approach is concise, performant, and (for those well-versed in LINQ) easy to understand. For comparison, here's a <a href="http://pythoncentral.io/finding-duplicate-files-with-python/">Python implementation</a> that shows the equivalent algorithm expressed imperatively (and without parallelism).

<h2 id="conclusion">Conclusion</h2>

Maybe I haven't sold you on C# as a scripting language, but hopefully I've at least convinced you that I'm not crazy for using it this way. In addition to the standalone merits, another benefit which pushes it over the edge for me is the ability to use the same language for scripting tasks as I use for day-to-day application development. This lets me easily transform POC scripts into application features. Furthermore, I find that the scripting experience of trying to get something done quickly in a particular language makes you a more agile programmer in that environment. Next time you need to write a quick script, consider giving C# a try!