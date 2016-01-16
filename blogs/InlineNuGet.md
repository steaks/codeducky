Developers love utility functions, and every developer seems to have <a href="http://www.codeducky.org/10-utilities-c-developers-should-know-part-one/">his or her favorite set</a>. In my various projects, I find myself using the same utilities over and over again, copying them from project to project. This gets frustrating and wastes time, especially when I find a bug or performance issue in a utility and want to get it updated everywhere. The obvious solution is to wrap my favorite utilities up in their own NuGet package, but this has several disadvantages which I'll describe further on. To address this issue, I've created a <a href="https://github.com/madelson/MedallionUtilities/tree/master/InlineNuGet">tool</a> which allows for the creation of "inline" NuGet packages which are, in my mind, a particularly appropriate way to distribute utilities.

<!--more-->

<h2 id="problem">Wait, why can't we just make a NuGet package?</h2>

Let me start by stating that the particular issues I'm about to describe apply primarily to the process of authoring libraries for consumption by others. Let's say that I wanted to factor out some common helper functions between two libraries (e. g. <a href="https://github.com/madelson/MedallionShell">MedallionShell</a> and <a href="https://github.com/madelson/DistributedLock">DistributedLock</a>) into a utility NuGet package. 

Under the traditional system, I'd create a new project "MedallionUtilities" and compile it to MedallionUtilities.dll. I would then publish this as a NuGet package and install that package into the projects for MedallionShell and DistributedLock. Then, when I published new versions of these packages, NuGet would capture the dependency on my Utilities package such that anyone who installed MedallionShell would see Utilities installed alongside. Sounds great, right?

This approach has two key downsides:

<h3 id="versioning-issues">Versioning issues</h3>

By taking a dependency on some version of Utilities I now open my packages up to potential version conflicts. For example, what if DistributedLock references Utilities 1.0 while MedallionShell references version 2.0? If you install both DistributedLock and MedallionShell in the same solution, there is at least some likelihood that things will fail at runtime because one of the two will be executing against a potentially incompatible version of Utilities.

This can be solved to some degree by careful consideration of <a href="http://www.sitepoint.com/semantic-versioning-why-you-should-using/">semantic versioning</a> and <a href="http://stackoverflow.com/questions/1456785/a-definitive-guide-to-api-breaking-changes-in-net">binary backwards compatibility</a>. I'm willing to do that and therefore might feel comfortable assuming this risk when it comes to packages I create consuming other packages I create. However, I'd be less likely to place the same trust in any arbitrary package without a proven track record, and at the same time I wouldn't expect others to be willing to risk the stability of their projects by referencing mine.

<h3 id="functionality-leakage">Functionality leakage</h3>

Perhaps an even bigger problem (at least for an API purist like myself) is that this approach "leaks" functionality. In order to allow MedallionShell to consume Utilities, <em>all the utility methods need to be publicly accessible</em>. This is frustrating because MedallionShell is supposed to be a library for <a href="http://www.codeducky.org/process-handling-net/">working with processes</a>. If when you install it you suddenly see utility methods for argument checking and async/await manipulation showing up in intellisense, this can be quite frustrating. 

Utilities often claim concise names, which means they are likely to conflict with utility methods defined in the consumer's code. At best, these force ugly full-qualification of class names. At worst, these conflicts can cause real errors when developers unknowingly invoke the wrong function. Extension methods defined on common types are among the worst offenders: we encountered several issues in our production codebase that were caused by developers accidentally importing the wrong namespace and thereby invoking an extension method leaked from an unrelated third-party library instead of the identically-named extension defined locally. Even when they aren't causing conflicts, extension methods can pollute intellisense with functions you never want to invoke.

<h2 id="solution">Inline NuGet packages to the rescue</h2>

As a way to work around these issues while still allowing utilities to be distributed, I decided to create what I call "inline" NuGet packages. Rather than compiling the code and distributing the DLL, these packages distribute the code in source format and <em>inline</em> it into the consuming project on installation. 

This sidesteps the concerns mentioned above: since each consumer has his or her own copy of the utilities we don't have to worry about versioning. Furthermore, since the code is compiled into the consuming project we are free to make everything internal, so there will be no leaking of utility functions.

This is not a novel concept: many packages (e. g. <a href="https://www.nuget.org/packages/jQuery/">JQuery</a>) use this same technique to distributed "content" files in C# or other languages. What might be more novel is that my inline packages are generated via a <a href="https://github.com/dotnet/roslyn">Roslyn</a>-powered <a href="https://github.com/madelson/MedallionUtilities/tree/master/InlineNuGet">command-line tool</a> which transforms the code in various ways before packing it. These include:

<ul>
	<li>Configuring the package to be a <a href="https://docs.nuget.org/Release-Notes/NuGet-2.7#development-only-dependencies">developmentDependency</a> so that it won't become a dependency of downstream consumers</li>
	<li>Merging all namespaces together into a single file</li>
	<li>Adding the <a href="https://msdn.microsoft.com/en-us/library/system.codedom.compiler.generatedcodeattribute(v=vs.110).aspx">GeneratedCodeAttribute</a> to each class to opt the file out of any style checkers in the consuming project</li>
	<li>Switching public types to internal to keep them out of the public API of the consuming project</li>
	<li>Making static types <a href="https://msdn.microsoft.com/en-us/library/wa80x488.aspx">partial</a> so that consumers can add their own utilities to the classes defined in the package</li>
	<li>Rewriting a number of <a href="http://developer.telerik.com/featured/essential-c-6-features-you-need-to-know/">C#6 syntax conveniences</a> so that consumers don't have to compile with the newest version of C#</li>
	<li>Injecting a number of preprocessor directives which, when specified in the consumer's build, toggle aspects of the generated code (e. g. switching internal classes back to public or disabling extension methods)</li>
</ul>

To get a sense of the output, check out the result of processing the MedallionCollections library: 

<a href="https://github.com/madelson/MedallionUtilities/tree/master/MedallionCollections">original codebase</a>
<a href="https://gist.github.com/madelson/9673c51c0f3fbfd6e79b">code file that would be installed by the inline package</a>

One nice outcome of using this automated process is that it is extremely easy to publish both normal and inline versions of the same package. For example, I've published both <a href="https://www.nuget.org/packages/MedallionCollections/">traditional</a> and <a href="https://www.nuget.org/packages/MedallionCollections.Inline/">inline</a> versions of my collections utilities.

<h2 id="conclusion">Conclusion</h2>

Inline NuGet packages aren't without their own problems. If used excessively they could cause code bloat by copying the same classes across many assemblies in the same process. This gives the JIT compiler more work to do, makes static caches less effective, and makes one-time initialization routines more costly. Furthermore, the versioning problem which inline NuGet packages work around can be a curse as well as a blessing: if some code in MedallionShell is failing because of a bug in a Utility function, there's no chance of fixing the bug by updating the local version of Utilities. Finally, inline NuGet packages can't really consume other inline NuGet packages without re-introducing the same issues they are designed to address, so writing utilities for your utilities is a non-starter.

Regardless, I think inline packages can be valuable, and I plan to publish more such packages with different sets of my favorite utilities in the near future.

If you'd like to create your own inline NuGet package, the <a href="https://github.com/madelson/MedallionUtilities/tree/master/InlineNuGet">package creation tool</a> is open source and freely available. I make no promises that it will support any arbitrary codebase, though!