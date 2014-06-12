The <a href="https://github.com/madelson/MedallionOData">MedallionOData</a> library is intended to be a lightweight, zero-setup .NET library for creating and querying OData and OData-like services.

<!--more-->

<h3>What is OData?</h3>

OData has a rather large specification that covers everything you need to fully expose a data model through a web service layer. That said, by far the coolest and most useful aspect of OData, in my opinion, is its ability to empower service endpoints with extremely flexible LINQ-like query capabilities:

<pre>
// example OData URL for querying customers
www.myservice.com/Customers?$filter=(City eq 'Miami') and (Company/Name neq 'Abc')&$orderby=Name
</pre>

As a web developer, the prospect of being able to enable any endpoint with LINQ is quite desirable from a design perspective. Without such a capability, most web service APIs tend to have a random smattering of potentially useful options for this purpose; for any given usage, though, you are likely to find something missing. For example, <a href="https://dev.twitter.com/docs/api/1.1/get/search/tweets">this Twitter API</a> endpoint provides a number of useful options, but doesn't make it easy to efficiently query for "recent Monday queries" or "queries matching 'wizards' but not 'basketball'". With OData, each endpoint you create becomes enormously flexible from the get-go, with the additional benefit of ease-of-use through standardization. 

<h3>There is already lots of .NET support for working with OData; why do we need another library?</h3>

It's true that .NET provides powerful facilities for working with OData out of the box. For example, ASP.NET WebApi has <a href="http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/creating-an-odata-endpoint">great facilities for creating complete OData endpoints</a>. On the other side, you can use <a href="http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/calling-an-odata-service-from-a-net-client">Visual Studio</a> to generate a strongly-typed service endpoint for querying an OData service from .NET. What's missing here, though, is a lighter-weight and more flexible option. What if I want to add OData query capabilities to existing (possibly non-WebApi) endpoints in my application without a lot of refactoring? Or, what if I want a lightweight and possibly dynamic way to query OData services without the clunky overhead of pre-generating proxy code in Visual Studio?

<h3>Enter MedallionOData</h3>

With MedallionOData, such tasks become easy. Let's start on the server side. Say we are building a todo-list application and we want to expose "IQueryable" web service endpoints for various tasks that will be shared by our web and mobile front-ends. Here's how we might set up such an endpoint in an MVC app:

First, we'll define a simple EntityFramework setup to provide IQueryable access to a SQL database. Note that MedallionOData doesn't assume (or even reference) EntityFramework; it works purely off IQueryable:
<pre>
public class Task
{
	public int Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public DateTime DateCreated { get; set; }
	public DateTime? DueDate { get; set; }
	public virtual User Creator { get; set; }
}

public class User
{
	public int Id { get; set; }
	public string FirstName { get; set; }
	...
}

public class TaskContext : DbContext
{
	public DbSet<Task> Tasks { get; set; }
	public DbSet<User> Users { get; set; }
}
</pre>

Next, we'll define the controller to serve as the endpoint:
<pre>
public class TaskController : Controller
{
	// The ODataService class from MedallionOData provides a simple
	// API for authoring service methods
	private static readonly ODataService service = new ODataService();

	[Route("/tasks")]
	public ActionResult Tasks()
	{
		using (var db = new TaskContext())
		{
			IQuerable<Task> tasks = db.Tasks;
			// apply permissioning, projection or other logic 
			IQueryable<Task> permissionedTasks = db.Tasks...
			
			// use the OData service to apply additional filtering 
			// based on the OData query parameters
			// The Result class encapsulates both the result
			// data and the data format, and thus can be used to construct
			// an appropriate response
			ODataService.Result result = this.service.Execute(
				permissionedTasks, 
				HttpUtility.ParseQueryString(this.Request.Url.Query)
			);
			// return the results as a JSON string (the format 
			// MedallionOData supports out of the box)
			return this.Content(result.Results.ToString(), "application/json");
		}
	}
}
</pre>

Let's say we wanted to render a grid of tasks that are due this month in our UI. We might issue a request like this:
<pre>
	/tasks?
		$filter=month(DueDate) eq 6 and year(DueDate) eq 2014
		&$top=25
		&$orderby=DueDate,DateCreated
		&$select=Name,DueDate
		// we want to also return the count of all "pages" (ignoring top and skip)
		&$inlineCount=allpages
		&$format=json
</pre>

This will return a result like:
<pre>
	{
		"odata.count": 34, // all pages count
		"value": [
			{ "Name": "Turn in history paper", "DueDate": "2014-06-19" },
			... // 24 more
		]
	}
</pre>

Thus, with just a few lines of code, we've managed to create a fully queryable endpoint that is both flexible enough for our grid and for many other UI operations we may create, such as querying for details about a specific task that the user has selected.

<h3>Querying OData services</h3>

MedallionOData also makes it easy to query such endpoints from C# code. In this example, we'll query the Microsoft's sample OData service:

First, we construct a query context. Like EntityFramework's DbContext, ODataQueryContext acts as a starting point for all queries. Unlike DbContext, however, objects read from a query context are not tracked, so you don't have to worry about disposal. Creating a query context is as simple as "newing" one:

<pre>
// the query context is thread-safe, so we can re-use a single instance
private static readonly ODataQueryContext context = new ODataQueryContext();
</pre>

To get an IQueryable from a context, simply provide a url and the query element type:
<pre>
private class Category
{
	public int ID { get; set; }
	public string Name { get; set; }
}

...

var baseUrl = @"http://services.odata.org/v3/odata/odata.svc/";
IQuerable<Category> query = context.Query<Category>(baseUrl + "Categories");
</pre>

Now, we can query the endpoint using strongly-typed LINQ operators. For example, we could get the the categories containing "Food" with:
<pre>
var foodCategories = query.Where(c => c.Name.Contains("Food")).ToArray();
</pre>

<h3>LINQ operator support</h3>

MedallionOData supports most LINQ operators that can be reasonably and efficiently be translated to OData. This includes: Where, Select, Skip, Take, most variants of OrderBy, First and Single variants, Any, All, and Count variants. Operators like Sum() which have no efficient translation to OData can still be used on the client side by pulling the results into memory first using AsEnumerable(). Similarly, some combinations of supported operators are not supported because they don't fit the rigid structure of OData query expressions. For example, Where cannot be applied after Skip, because the $skip operator in OData happens after filtering. On the other hand, some operations that do not have a natural mapping to OData are supported because they can be restructured to a form that does have a natural translation. A simple case is that multiple uses of Where will be merged into a single OData $filter operator. A more complex example is Select. In OData, $select simply lets you limit/expand your results to a subset of entity properties. In LINQ, however, Select serves many purposes, such as reshaping the results or computing a temporary result to be used later in the query. To support as broad usage of Select as possible, MedallionOData uses a mix of inlining and client-side projection to translate Select calls into OData format. Consider the following code:
<pre>
var longCategoryNameLengths = context.Query<Category>(baseUrl + "Categories")
	.Select(c => c.Name.Length)
	.Where(len => len > 10)
	.ToArray();
</pre>

OData has no direct support for intermediate projections, nor does it support selecting "computed" values. Here's the query MedallionOData will issue for the above:
<pre>
http://services.odata.org/v3/odata/odata.svc/Categories?
	$select=Name
	&$filter=length(Name) gt 10
</pre>

The remaining projection (selecting name => name.Length) is separated out and performed on the client side.

In all cases, performance is a guiding design principle: by being strict about matching what the OData protocol can support, MedallionOData prevents you from unknowingly pulling large amounts of data to perform an operation that cannot be done on the server.

<h3>Dynamic queries</h3>

In the previous example, we defined a local Category type to use with the service. If your applicatin makes heavy use of a single service, this is likely the route you'll want to go (especially if the service provides a library that defines these types). However, having to map the service schema to POCO classes is a frustrating amount of overhead in many cases. To address this, MedallionOData supports a "dynamic" entity type: ODataEntity. Here's how the above query would look using ODataEntity:
<pre>
IQueryable<ODataEntity> query = context.Query(baseUrl + "Categories");
var foodCategories = query.Where(c => c.Get<string>("Name").Contains("Food")).ToArray();
</pre>

<h3>Special query operators</h3>

MedallionOData also contains a handful of special query operators which only work with OData queries. For example, OData has the ability to pull a page of data and the count of the total (unpaginated) data set in a single request. While there is no equivalent LINQ operator for this, it can be achieved with the ExecuteQuery extension method:
<pre>
var pageOne = context.Query<Category>(baseUrl + "Categories")
	.OrderBy(c => c.Name)
	.Take(10)
	.ExecuteQuery(new ODataQueryOptions(inlineCount: ODataInlineCountOption.AllPages));
Console.WriteLine(pageOne.TotalCount); // count across pages
Console.WriteLine(pageOne.Results.Count); // count on page one
</pre>

<a href="http://msdn.microsoft.com/en-us/library/hh191443.aspx">Task-based asynchrony</a> is also available through a set of async methods:
<pre>
var firstFoodCategory = await query.Where(c => c.Name.Contains("Food"))
	.ExecuteAsync(q => q.First());
</pre>

<h3>Conclusion</h3>

Hopefully, this post has illustrated how MedallionOData can be used to take advantage of the OData protocol on both the server and client side with little to no setup or boilerplate. To try out MedallionOData for yourself, simply install the package using <a href="http://www.nuget.org/packages/MedallionOData/">NuGet</a>. You can also peruse the source, file an issue, or submit a pull request via the <a href="https://github.com/madelson/MedallionOData">github repository</a>.