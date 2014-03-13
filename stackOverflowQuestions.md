http://stackoverflow.com/questions/19742304/why-should-you-not-use-entity-framework-or-an-orm/19742480#19742480
	- add blog post and note in answer (e. g. could cover shortcomings of the active record pattern)

http://stackoverflow.com/questions/12682696/removing-duplicates-from-bottom-of-generic-list/12682752#12682752
	- the fact that all LINQ operators preserve order could be the making of a short post, which we could link to in the beginning of the answer
	
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
		
