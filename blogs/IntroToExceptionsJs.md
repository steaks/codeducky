I want to introduce <a href="https://www.exceptionsjs.com/">exceptions.js</a>, a library that makes Javascript errors useful. Include stacktraces, screenshots, DOM dumps, browser information, etc. when users hit Javascript errors. The library can be used as a standalone <a href="https://github.com/steaks/exceptions.js">open source project</a> or can be used with the <a href="https://www.exceptionsjs.com/">exceptionsjs platform</a> which translates reported exceptions into emails and delivers them to registered developers.

Use exceptions.js to setup error reporting with one line of code.

<pre>
<script type="text/javascript" src="path/to/exceptions.js"></script>
</pre>
<pre>
//Setup the exceptions handler to report errors when 
//you invoke Exception.report() or window.onerror executes
exceptions.handler
    //Reporting to exceptionsjs platform is the easiest way to track your exceptions.
    //Register for free at https://www.exceptionsjs.com.
    .reportToExceptionsJsPlatform({ clientId: "CLIENT_ID" })
    //Set a custom report post request that will be issued when an exception is reported.
    //if you want to bypass the exceptionsjs platform and handle the exception yourself.
    .reportPost({ url: "http://localhost/path/to/errorhandler/" });
</pre>
<br/>
Code below illustrates common and powerful uses for the library. 

<pre>
//exceptions.js will report any error.
var foo = {}, oops = foo.uhoh.doesNotExist;
throw new Error("Something went wrong!");
throw "Something went wrong!";

//you can also report exceptions.
new exceptions.Exception("Something went wrong!").report();

//or throw an exception.
throw new exceptions.Exception("Something went wrong!");

//exceptions.js provides convienence methods that make code more readable.
function myFunc(requiredArg) {
    exceptions.throwIf(!requiredArg, "The requiredArg argument was not provided!!!"); 
}

//and types that make errors more explicit
function WillWriteInTheFuture() {
    throw new exceptions.NotImplementedException();
}
</pre>
<br/>

Please browse the project on <a href="https://github.com/steaks/exceptions.js">github</a> or at <a href="https://www.exceptionsjs.com/">exceptionsjs.com</a> to learn more about how you can leverage the project in your development cycle.
