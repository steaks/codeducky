LINQ providers such as <a href="[TODO link]>Entity Framework</a> (and therefore any OData services built on top of them) are heavily reliant on server-side static typing to define the model. This has a number of benefits, such as <a href="http://www.asp.net/web-api/overview/odata-support-in-aspnet-web-api/odata-v3/calling-an-odata-service-from-a-net-client">allowing for automatic strongly-typed endpoint code generation</a>, more robust parsing of OData queries, and added security. However, many practical applications don't fit nicely into the static query model. Luckily, the <a href="[TODO link]">MedallionOData</a> library provides an easy solution through it's ability to create dynamic OData service endpoints. We'll walk through the setup step-by-step in the context of a toy example.

<!--more-->

<h1 id="problem">The Problem</h1>

A classic case where a dynamic OData schema might be necessary is that of displaying a grid which is pivoted in some way. For example, let's say we have a database with customers, products, and orders. Our orders table might look like the following:

<pre>
orderId INT
customerId INT
productId INT
units INT
</pre>

Let's say we want to display an interactive table to the user that shows units ordered by customer for some user-determined set of products. Thus, the columns of the table are dynamic: we'll have one for each product selected by the user. We can easily represent this sort of query in SQL:

<pre>
-- user picked products 1, 2, and 3
SELECT customerId
	, SUM(CASE WHEN productId = 1 THEN units ELSE 0 END) AS units1
	, SUM(CASE WHEN productId = 2 THEN units ELSE 0 END) AS units2
	, SUM(CASE WHEN productId = 3 THEN units ELSE 0 END) AS units3
FROM orders
WHERE productId IN (1, 2, 3)
GROUP BY customerId
</pre>

However, it's more difficult to fit this into a strongly-typed IQueryable in a way that will still allow it to be further sorted and filtered. 

<h1 id="endpoint">Creating the Data Endpoint</h1>

This is where MedallionOData comes in. MedallionOData allows us to escape from the world of static schemas via the dictionary-like ODataEntity type. In fact, if our data is small enough to fit in memory, we can simply read all orders into memory and convert to the <a href="[TODO link]">ODataEntity</a> type. ODataEntity acts like a dictionary, allowing for queries with fully dynamic schemas:

<pre>
using (var context = new OrdersContext()) // OrdersContext is an EF DbContext
{
	var products = new[] { 1, 2, 3 };
    var inMemoryResults = context.Orders
		// select only orders for relevant products
		.Where(o => products.Contains(o.ProductId))
		// work in-memory from here
		.AsEnumerable()
		// the same group-by as in SQL
		.GroupBy(o => o.CustomerId)
		// select key-value pairs representing the column values
		.Select(
			g => products.Select(
				p => new KeyValuePair<string, object(
					p,
					g.Where(o => o.ProductId == p).Sum(o => o.Units)
				)
			)
			.Concat(new[] { new KeyValuePair<string, object>("customerId", g.Key) })
		)
		// convert to the ODataEntity type
		.Select(kvps => new ODataEntity(kvps))
		.ToArray();
		
	// MedallionOData can apply OData url filters to this query
	var queryable = inMemoryResults.AsQueryable();
}
</pre>

However, in most cases we want to avoid reading all results into memory and instead only pull what the user is asking for. For this, we can take advantage of MedallionOData's <a href="[TODO link]">ODataSqlContext</a> class (introduced in <a href="[TODO link]">v1.4</a>). ODataSqlContext provides a lightweight implementation of a LINQ query provider that supports only the subset of LINQ operations that map to OData. We can construct a context by providing it with a <a href="[TODO link]">syntax</a> (for generating the right dialect of SQL) along with an <a href="[TODO link]">executor</a> (for configuring database access). In this case, we'll use the out-of-the-box support for SqlServer:

<pre>
var sqlContext = new ODataSqlContext(
	new SqlServerSyntax(SqlServerSyntax.Version.Sql2012), 
	new DefaultSqlExecutor(
		() => new SqlConnection([connection string])
	)
);
</pre>

Using the sql context, we can create dynamic queryables which can be filtered by MedallionOData:

<pre>
var products = // read in products 1, 2, 3 using EF
var pivotQuery = string.Format(@"(
		SELECT c.name AS customer
			, {0}
		FROM orders o
		JOIN customers c ON c.id = o.customerId
		JOIN products p ON p.id = o.productId
		WHERE p.id IN ({1})
		GROUP BY c.id, c.name
	)",
	string.Join(", ", products.Select(p => string.Format("SUM(CASE WHEN p.id = {0} THEN o.units ELSE 0 END) AS [{1}]", p.Id, p.Name))),
	string.Join(", ", products.Select(p => p.Id))
);
IQueryable<ODataEntity> query = sqlContext.Query<ODataEntity>(pivotQuery);
</pre>

This query can then be used to create an OData endpoint in a web application. Here's an example using MVC:

<pre>
public class ODataMvcController : Controller
{
	[HttpGet]
	public ActionResult PivotedData(string productNames) 
	{
		var productNameList = productNames.Split(',');
		
		Product[] products;
		using (var context = new OrdersContext()) 
		{
			products = context.Products.Where(p => productNames.Contains(p.Name)).ToArray();

			IQueryable<ODataEntity> query = // generate dynamic query as shown above

			var service = new ODataService();
			var result = service.Execute(query, HttpUtility.ParseQueryString(this.Request.Url.Query));
			return this.Content(result.Results.ToString(), "application/json");
		}
	}
}
</pre>

With this, requesting the following url:
<pre>http://localhost:49884/ODataMvc/PivotedData?$format=json&productNames=wood,ore</pre>

returns the following data:
<pre>
{
    "value": [{
        "customer": "Blue",
        "Wood": 4,
        "Ore": 6
    }, {
        "customer": "Yellow",
        "Wood": null,
        "Ore": 8
    }, {
        "customer": "Red",
        "Wood": 10,
        "Ore": 5
    }, ...]
}
</pre>

<h1 id="grid">Creating a Grid</h1>

Now that we have an endpoint that can return and query our data set via OData, we can build an AJAX-driven client side grid to display this data. Many javascript grid libraries support OData. Here, we will use the free <a href="[TODO link]">JQuery DataTables</a> library with the <a href="[TODO link]">OData plugin</a>. Here's the relevant cshtml:

<pre>
<div>Enter product name columns here:</div>
<div>@Html.TextBox("productNames", string.Empty, new { id = "productNames" })</div>
<div id="grid"></div>
</pre>

and the relevant javascript:

<pre>
$('#productNames').change(function () {
	// clear the existing grid
	$('div#grid').empty();
	$('div#grid').append('<table id="grid" />');

	// create a new grid using data tables
	$('table#grid').dataTable({
		"sAjaxSource": "/ODataMvc/PivotedData?productNames=" + $('#productNames').val(),
		"iODataVersion": 3,
		"aoColumns": $.map(['customer'].concat($('#productNames').val().split(',')), function (name) { return { sTitle: name, mDataProp: name }; }),
		"fnServerData": fnServerOData, // required
		"bServerSide": true,  // optional
		"bUseODataViaJSONP": false,  // set to true if using cross-domain requests
	});
});
</pre>