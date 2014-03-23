http://stackoverflow.com/questions/19742304/why-should-you-not-use-entity-framework-or-an-orm/19742480#19742480
	- add blog post and note in answer (e. g. could cover shortcomings of the active record pattern)
	
http://stackoverflow.com/questions/16741956/entity-framework-5-performance-concerns/18209346#18209346
	- could reference a post on optimizing EF. In addition to the techniques mentioned in the post, there are:
		- avoid abstract with entities
		- watch out for large in-memory runtimes
		- use simpler constructs
		
http://stackoverflow.com/questions/12683006/c-can-you-add-generic-property-to-all-data-entry-controls-without-inheriting/12683017#12683017
	- ConditionalWeakTable is really cool, and definitely deserving of a post. This is a great way to extend an API over which you have no control
	- there might be other questions out there regarding "extension properties"
	
http://stackoverflow.com/questions/8818207/how-should-one-unit-test-a-net-mvc-controller
	- short post about where I came out on this issue & why
	- test IOC config instead
	
http://stackoverflow.com/questions/12341439/best-practice-design-for-a-multi-page-form-in-net-mvc3
	- could talk about how we went with one, but now prefer the other. The validation redirect was the biggest issue (carrying form keys)
	
http://stackoverflow.com/questions/12611167/why-does-concurrentdictionary-getoraddkey-valuefactory-allow-the-valuefactory
	- this is a pretty interesting design issue. There are locks to think of, for example, but also performance. A brief post could discuss this pattern (e. g. in comparison to how Cache.cs works)
	
http://stackoverflow.com/questions/14878997/why-does-razorengine-compile-templates-with-anonymously-typed-models-dynamically
	- could be a good one for BR
	
http://stackoverflow.com/questions/12073001/where-does-entity-framework-store-the-mapping-between-property-names-and-the-col
	- could do a post on runtime-bound tables
	
http://stackoverflow.com/questions/19930135/f-limitations-of-discriminated-unions
	- make my OData pt. 1 post part of the initial question
	
http://stackoverflow.com/questions/17053064/what-is-the-difference-between-passing-it-isanyint-and-the-value-of-it-isany/17057047#17057047
	- for BR: could do a quick post about the use of methods that exist only to be used as data. The SqlFunction in EF and It.IsAny<> are both examples
		another example is LinqHelpers.Inline() in Apt.Core. This is a cool pattern "when should you write a method that can't be called?"
		
========
Steven's SO questions and answers that could provide good opportunities for our blog.  They are ordered by what questions I think attacking would provide the most benefit.

http://stackoverflow.com/questions/16865972/reflection-call-a-method-in-a-class-which-is-in-a-list/16873846#16873846
    We should link this post to GetMethod.  130 views

http://stackoverflow.com/questions/16977176/refactor-safe-way-to-retrieve-a-method-name
    We should link to GetMethod becaus that's what this question is about.  200 views

http://stackoverflow.com/questions/16683121/git-diff-between-two-different-files/16683184#16683184
    We could write a post about useful git aliases, git shortcuts, and things you should know about git. 3239 views

http://stackoverflow.com/questions/16825849/git-choose-merge-strategy-for-specific-files-ours-mine-theirs
    We could write a post about common git aliases, commands, or concepts and link this question.  5931 views

http://stackoverflow.com/questions/21052437/are-these-two-lines-the-same-vs/21052507#21052507
    We could write about immutablity or C# properties.  4021 views.

http://stackoverflow.com/questions/16377025/base-method-with-multiple-levels-of-inheritance-not-being-called/16377838#16377838
    We could write a post about good and bad inheritance patterns in C#.  780 views

http://stackoverflow.com/questions/5006328/opposite-of-ajax
    Ben or Dan could write a post about long polling.  380 views

http://stackoverflow.com/questions/16286309/insert-new-entity-to-context-with-identity-primary-key/16286540#16286540
    We could write a post about entity framework's fluent api and attributes.  1696 views

http://stackoverflow.com/questions/16952901/how-to-get-the-distinct-elements/16952918#16952918
    We could write a post about common entity framework examples.  This one in particular is about select distinct by column. 96 views

http://stackoverflow.com/questions/17890729/how-can-i-write-take1-in-query-syntax
    We could write a post about common entity framework examples.  171 views

http://stackoverflow.com/questions/19473996/get-count-of-array-that-converted-to-dynamic-in-c-sharp/19474230#19474230
    We could write about interface implementation and its uses.  302 views.

http://stackoverflow.com/questions/18789855/razor-not-compiling-in-using-block
    I could see us writing a post about cute uses with the using block.  104 views.

http://stackoverflow.com/questions/22007025/the-objectcontext-instance-has-been-disposed-and-can-no-longer-be-used-for-opera/22007094#22007094
    We could write a post about how data contexts work with entity framework.  49 views

http://stackoverflow.com/questions/20436976/entity-framework-thread-safe-create-entity-if-doesnt-exist
    I could see use writing a post or two about inserts with entity framework.  70 views

http://stackoverflow.com/questions/17227249/static-files-and-django-templates/17227738#17227738
    We could write a post about view generation engines...razor vs. ASP pages vs. django templates vs. php etc.  228 views

http://stackoverflow.com/questions/16291590/listmyobject-contains/16291630#16291630
    We could write a post about the speed of common Enumerable operations in C#.  138 views

http://stackoverflow.com/questions/16391167/tricky-idisposable-issue/16391197#16391197
    We could write a post about disposables having to execute code before the disposable is disposed. 133 views.

http://stackoverflow.com/questions/20604129/when-passing-functions-to-a-method-why-does-using-an-anonymous-function-contain/20604189#20604189
    We could write a simple post about how setTimeout and setInterval in javascript work.  35 views

http://stackoverflow.com/questions/17961813/immediately-called-functions-are-undefined/17961872#17961872
    We could write about functions in javascript 71 views

http://stackoverflow.com/questions/16304415/how-can-i-get-the-maximum-sequential-in-number-range/16306128#16306128
   We could write a post about recursive common table expressions.  111 views

DONE: http://stackoverflow.com/questions/12682696/removing-duplicates-from-bottom-of-generic-list/12682752#12682752
- the fact that all LINQ operators preserve order could be the making of a short post, which we could link to in the beginning of the answer
