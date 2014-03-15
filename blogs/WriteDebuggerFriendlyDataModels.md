Fixing bugs in production stinks!  Among the many reasons, it takes time away from feature development.  My mind always wanders towards the equation below while fixing bugs:
<pre>
X time fixing bugs = - X time feature development = Y fewer features
</pre> 
<br/>
First and foremost, I try to eliminate bugs from my code.  But for those that seep out, I try to write data models that make debugging easier so I can spend less time debugging and more time developing features.

I generally use relational databases to persist data.  So I'll explain my implementations in SQL, but these concepts can be applied to many technologies.  

I try achieve two goals by following three guidelines when building debugger friendly data models.

Goals:
<ul>
	<li><strong><span style=color:#339966;">Developers should be able to <i>definitively</i> deduce how, by whom, and when any important record was created.</span></strong></li>
	<li><strong><span style=color:#339966;">Developers should be able to <i>quickly</i> deduce how, by whom, and when any important record was created.</span></strong></li>
</ul>

Guidelines:
<ul>
	<li><strong>Make critical areas of the datamodels deterministic</strong></li>
	<li><strong>Place meta information in the datamodel where valuable</strong></li>
	<li><strong>Place meta information in logging tables where valuable</strong></li>
</ul>

<strong>Make critical areas of datamodels deterministic</strong>
If you're like me, you've encountered the following scenario:  problematic record "a" was generated using records "b" and "c."  So you look at records "b" and "c" only to find that they have completely mutated from their state when record "a" was created.  Now you have no way to determine how record "a" was created.  Said in other words, you were not able to definitively determine how record "a" was created because your data model is not deterministic.

I leverage immutability to preserve determinism in more sensitive areas of my data models.  I'll use a simple example to explain how I use immutability and the power it provides for debugging.  

Recently, I've spent too much time at <a href="http://en.wikipedia.org/wiki/Barnes_%26_Noble">Barnes and Noble</a>.  When I notice a price change for a book I want to purchase I think to myself: "wow I'm so smart for waiting!" if the price decreased or "dammit, I knew I should have bought it earlier" if the price increased.  After my euphoria or dismay I begin to wonder how Barnes and Noble prices their books.  

For our example, let's try to implement an ultra simplistic data model for book pricing calculations.  Imagine books prices are determined by the cost to manufacture a book, an author's popularity, and the season (prices spike during the winter holidays).  

First, let's construct the naive data model which does not leverage immutability to preserve determinism.  Later we'll contrast this data model with an improved version.

We need books, authors, and seasons.  We'll calculate book prices from book pricing settings that reference a book, author, and season.

<a href="http://www.codeducky.org/wp-content/uploads/2014/03/naiveDatabase1.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/03/naiveDatabase1.png" alt="database diagram for naive book pricing database" width="598" height="527" class="aligncenter size-full wp-image-203" /></a>

This data model provides everything we need to price books.  We have the manufacturing cost of a book, the author's popularity, and season.  However, remember that a book's manufacturing cost can change when <a href="http://en.wikipedia.org/wiki/Economies_of_scale">economies of scale</a> come into play.  And an author's popularity can vary.  Now imagine a store manager complains that <a href="http://en.wikipedia.org/wiki/Inferno_(Dan_Brown_novel)">Inferno</a> was overpriced this past holiday season.  He claims there's a bug in our pricing algorithm.  Naturally, we think our pricing algorithm works just fine so we set out to prove him wrong.  Uh oh!  We know what the book's price was in December.  It was higher in then than now.  But we can't say for sure why.  Was it only because of seasonality?  Did Dan Brown's popularity decrease recently?  Did the manufacturing cost of the book change?  We can't know what the author's popularity and manufacturing cost were in December because they both may have mutated.  Now, it's going to take a long time and creative debugging techniques to understand how we priced Inferno in December.

Let's try to help ourselves by expressing books and authors with immutability baked in.  We'll separate books and authors into two tables respectively.  One table will include data not sensitive enough to require immutability.  The second table will include data that we want to express in immutable concepts.

<a href="http://www.codeducky.org/wp-content/uploads/2014/03/improvedDefnitions.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/03/improvedDefnitions.png" alt="database diagram for improved book pricing database with immutability" width="581" height="753" class="aligncenter size-full wp-image-205" /></a>

Notice we've split books and authors into a wrapper table which points to a definition.  All records in the wrapper tables are mutable, and all records in the definition tables are immutable.  To update a book or author's definition we create a new record in the appropriate definition table and point the book or author to the new definition.  Also, we only use immutable definitions in our calculation tier and other sensitive areas.  Now when our store manager asks why Inferno was priced so high in December, we can quickly look at the book and author definitions used to calculate the price.

<strong>Place meta information in the datamodel where valuable</strong>
Remember that one of our goals is to help developers quickly determine how records in the database were created.  Useful meta information can speed up the debugging process dramatically.  Up to this point we haven't provided any meta information about when and by whom books or authors were created in our database.  Books, authors, and prices contain a lot of the same meta data, so we should store that information in common in a common table.  We'll call that table "metaInfo."

<a href="http://www.codeducky.org/wp-content/uploads/2014/03/meatInfo.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/03/meatInfo.png" alt="database diagram for meta info for books, authors, and book prices" width="529" height="579" class="aligncenter size-full wp-image-207" /></a>

<strong>Place meta information in logging tables where valuable</strong>
Again, remember that one of our goals is to help developers quickly determine how a record in the database was created.  There is often meta information that does not naturally fit into our data model.  Take care to not disregard this information immediately.  Instead use log tables to include information that will help debugging.   I suspect users will want to update author popularity often, so let's keep a log of when and who updated author definitions to help us for debugging.

<a href="http://www.codeducky.org/wp-content/uploads/2014/03/authorDefinitionChangeLog.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/03/authorDefinitionChangeLog.png" alt="database diagram for author definition change log" width="248" height="171" class="aligncenter size-full wp-image-210" /></a>

<strong>Final data model</strong>
Finally, we can put all the pieces to create a data model which has deterministic critical areas, meta information in strategic columns, and meta information in strategic logging tables.  Developers can <strong><span style=color:#339966;">quickly</span></strong> and <strong><span style=color:#339966;">definitively</span></strong> determine how, by whom and when we calculated prices for Inferno to be sold during the holiday season.

<a href="http://www.codeducky.org/wp-content/uploads/2014/03/fullDebuggerFriendlyDataModelsDiagram4.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/03/fullDebuggerFriendlyDataModelsDiagram4.png" alt="database diagram for debugger friendly data models" width="608" height="1214" class="aligncenter size-full wp-image-237" /></a>
