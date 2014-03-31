<style>
.section-header-footnote
{
    margin-bottom: 30px;
}
.section-header
{
    margin-bottom: 5px;
}
</style>

Translating SQL to LINQ can prove difficult for new and experienced C# developers.  This post contains common SQL queries written in LINQ.  I hope it'll serve as a reference when writing LINQ queries.  I'll use a MS SQL database and <a href="http://msdn.microsoft.com/en-us/data/ee712907">Entity Framework</a> for my examples.  However, most examples can be extracted to other ORMs and databases.

<a href="#select">Select</a>
<a href="#where">Where</a>
<a href="#inner-join">Inner join</a>
<a href="#left-right-outer-join">Left/right outer join</a>
<a href="#cross-join">Cross join</a>
<a href="#group-by">Group by</a>
<a href="#having">Having</a>
<a href="#distinct">Distinct</a>
<a href="#union">Union</a>
<a href="#order-by">Order by</a>
<a href="#case-statement">Case statement</a>
<a href="#coalesce">Coalesce</a>
<a href="#aggregate-functions">Aggregation functions (e.g. min, max, average, count)</a>

<h5><a name="data-model">Data model</a></h5>
We'll use a simple data model that contains books and authors for our examples.

<pre>
CREATE TABLE dbo.authors
(
    authorId INT NOT NULL IDENTITY(1,1) CONSTRAINT PK_authors PRIMARY KEY,
	name NVARCHAR(MAX) NOT NULL,
	birthday DATETIME NOT NULL
)

CREATE TABLE dbo.books
(
    bookId INT NOT NULL IDENTITY(1,1) CONSTRAINT PK_books PRIMARY KEY,
	title NVARCHAR(MAX) NOT NULL,
	numPages INT NOT NULL,
	genre NVARCHAR(MAX) NOT NULL,
	authorId INT NOT NULL CONSTRAINT FK_books_authors 
                              FOREIGN KEY REFERENCES dbo.authors(authorId)
)

CREATE TABLE dbo.articles
(
	articleId INT NOT NULL IDENTITY(1,1) CONSTRAINT PK_articles PRIMARY KEY,
	title NVARCHAR(MAX) NOT NULL,
	numWords INT NOT NULL,
	authorId INT NOT NULL CONSTRAINT FK_articles_authors 
                              FOREIGN KEY REFERENCES dbo.authors(authorId)
)
</pre>
<br/>
<a href="http://www.codeducky.org/wp-content/uploads/2014/03/booksAndAuthors1.png"><img src="http://www.codeducky.org/wp-content/uploads/2014/03/booksAndAuthors1.png" alt="Books authors and articles data model" width="557" height="338" class="aligncenter size-full wp-image-374" /></a>
<h5><a name="entity-framework-data-context">Entity Framework data context</a>

<pre>
public class DataContext : DbContext
{
    private const string ConnectionString = 
        @"Server=MyServer;Database=MyDatabase;Trusted_Connection=True;";
    public DataContext() : base(ConnectionString) { }

    public DbSet<Author> Authors { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Article> Articles { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>().HasKey(a => a.AuthorId);
        modelBuilder.Entity<Author>()
            .HasMany(a => a.Books)
            .WithRequired(b => b.Author);
        modelBuilder.Entity<Author>()
            .HasMany(a => a.Articles)
            .WithRequired(b => b.Author);

        modelBuilder.Entity<Book>().HasKey(b => b.BookId);
        modelBuilder.Entity<Book>()
            .HasRequired(b => b.Author)
            .WithMany()
            .HasForeignKey(b => b.AuthorId);

        modelBuilder.Entity<Article>().HasKey(art => art.ArticleId);
        modelBuilder.Entity<Article>()
            .HasRequired(art => art.Author)
            .WithMany()
            .HasForeignKey(art => art.AuthorId);
    }
}

public class Author
{
    public int AuthorId { get; set; }
    public string Name { get; set; }
    public DateTime Birthday { get; set; }
    public ICollection<Book> Books { get; set; }
    public ICollection<Articles> Articles { get; set; }
}

public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; }
    public int NumPages { get; set; }
    public string Genre { get; set; }
    public int AuthorId { get; set; }
    public Author Author { get; set; }
}

public class Article
{
    public int ArticleId { get; set; }
    public string Title { get; set; }
    public int NumWords { get; set; }
    public int AuthorId { get; set; }
    public Author Author { get; set; }
}

//Construct and use a data context with the following
//code.  I exclude this code for simplicity in the examples.
using (DataContext db = new DataContext())
{
    //queries go here
}
</pre>

<h5 class="section-header"><a name="select">Select</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT *
FROM books

IQueryable<Book> books = db.Books;
</pre>
<br/>
<pre>
SELECT TOP 1 *
FROM books

//There are many ways to select the first row from a query.
//Each does something slightly differently.

//Select the first row.  Throw an exception if there are zero rows.
Book book = db.Books.First();

//Select the first row.  Return the
//default value if there are zero rows.
Book book = db.Books.FirstOrDefault();

//Select the first row.  Throw an exception
//if there is not exactly one row
Book book = db.Books.Single();

//Select the first row.  Throw an excpetion
//if there is more than one row.  Return the
//default value if there are zero rows.
Book book = db.Books.SingleOrDefault();

//Select the first row.  Don't throw exceptions
//if there is not exactly one row.  Return a
//queryable rather than actually pulling the value
//from the database.  This allows us to add extra
//query operations like Where or Select to the
//returned value.
IQueryable<Book> book = db.Books.Take(1);
</pre>
<br/>
<pre>
SELECT title
FROM books

//fluent syntax
IQueryable<string> titles = db.Books.Select(b => b.Title);

//query syntax
IQueryable<string> titles2 =
    from b in db.Books
    select b.Title;
</pre>
<br/>
<pre>
SELECT title, numPages
FROM books

//fluent syntax
var titlesAndNumPages = 
    db.Books.Select(b => new { b.Title, b.NumPages });

//query syntax
var titlesAndNumPages2 =
    from b in db.Books
    select new { b.Title, b.NumPages };
</pre>

<h5 class="section-header"><a name="where">Where</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT *
FROM books
WHERE title = 'Catch 22'

//Fluent syntax
IQueryable<Book> books = db.Books.Where(b => b.Title == "Catch 22");

//Query syntax
IQueryable<Book> books2 = 
    from b in db.Books
    where b.Title == "Catch 22"
    select b;
</pre>
<br/>
<pre>
SELECT *
FROM books
WHERE title = 'Catch 22 'AND numPages = 305

//fluent syntax with && operator
IQueryable<Book> books =
    db.Books.Where(b => b.Title == "Catch 22" && b.NumPages == 305);

//fluent syntax and chaining where operations
IQueryable<Book> books2 =
    db.Books.Where(b => b.Title == "Catch 22")
            .Where(b => b.NumPages == 305);

//query syntax with && operator
IQueryable<Book> books3 = 
    from b in db.Books
    where b.Title == "Catch 22" && b.NumPages == 305
    select b;

//query syntax chaining where operations
IQueryable<Book> books4 =
    from b in db.Books
    where b.Title == "Catch 22"
    where b.NumPages == 305
    select b;
</pre>
<br/>
<pre>
SELECT *
FROM books
WHERE title = 'Catch 22' OR numPages = 305

//fluent syntax
IQueryable<Book> books =
    db.Books.Where(b => b.Title == "Catch 22" || b.NumPages == 305);

//query syntax
IQueryable<Book> books2 =
    from b in db.Books
    where b.Title == "Catch 22" || b.NumPages == 305
    select b;
</pre>
<br/>
<pre>
SELECT *
FROM books
WHERE title IN('Catch 22', 'The Giver')

//fluent syntax
IQueryable<Book> books =
    db.Books.Where(b => new[] { "Catch 22", "The Giver" }.Contains(b.Title));

//query syntax
IQueryable<Book> books2 =
    from b in db.Books
    where new[] { "Catch 22", "The Giver" }.Contains(b.Title)
    select b;
</pre>
<br/>
<pre>
SELECT *
FROM books
WHERE numPages BETWEEN 200 AND 300

//fluent syntax
IQueryable<Book> books =
    db.Books.Where(b => b.NumPages >= 200 && b.NumPages <= 300);

//query syntax
IQueryable<Book> books2 =
    from b in db.Books
    where b.NumPages >= 200 && b.NumPages <= 300
    select b;
</pre>

<h5 class="section-header"><a name="inner-join">Inner join</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT name
FROM books b
INNER JOIN authors a
    b.authorId = a.authorId

//fluent syntax with association properties
var booksAndTheirAuthors = 
    db.Books.Select(b => new { Book = b, b.Author });

//fluent syntax without association properties
var booksAndTheirAuthors2 =
    db.Books.Join(db.Authors,
                    a => a.AuthorId,
                    b => b.AuthorId,
                    (b, a) => new { Book = b, Author = a });

//query syntax with association properties
var booksAndTheirAuthors3 =
    from b in db.Books
    select new { Book = b, Author = b.Author };

//query syntax without association properties
var authorsAndTheirBooks2 =
    from b in db.Books
    join a in db.Authors
        on b.AuthorId equals a.AuthorId
    select new { Book = b, Author = a };
</pre>

<h5 class="section-header"><a name="left-right-outer-join">Left/right outer join</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT *
FROM authors a
LEFT OUTER JOIN books b
    a.authorId = b.authorId

//fluent syntax with association properties
var authorsAndTheirBooks =
    db.Authors.Select(a => new { Author = a, a.Books });

//fluent syntax without association properties
var authorsAndTheirBooks2 =
    db.Authors.GroupJoin(db.Books,
                            a => a.AuthorId,
                            b => b.AuthorId,
                            (a, b) => new { Author = a, Books = b });

//query syntax with association properties
var authorsAndTheirBook4 =
    from a in db.Authors
    select new { Author = a, a.Books };

//query syntax without association properties
var authorsAndTheirBooks3 =
    from a in db.Authors
    join b in db.Books
        on a.AuthorId equals b.AuthorId into g
    select g.DefaultIfEmpty();
</pre>
<br/>
<pre>
SELECT *
FROM books b
RIGHT OUTER JOIN authors a
    b.authorId = a.authorId

//fluent syntax with association properties
var authorsAndTheirBooks =
    db.Authors.Select(a => new { Author = a, a.Books });

//fluent syntax without association properties
var authorsAndTheirBooks2 =
    db.Authors.GroupJoin(db.Books,
                            a => a.AuthorId,
                            b => b.AuthorId,
                            (a, b) => new { Author = a, Books = b });

//query syntax with association properties
var authorsAndTheirBook4 =
    from a in db.Authors
    select new { Author = a, a.Books };

//query syntax without association properties
var authorsAndTheirBooks3 =
    from a in db.Authors
    join b in db.Books
        on a.AuthorId equals b.AuthorId into g
    select g.DefaultIfEmpty();
</pre>

<h5 class="section-header"><a name="cross-join">Cross join</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT *
FROM books b
CROSS JOIN authors a

//fluent syntax
var booksAndAuthors =
    db.Books.SelectMany(b => new [] { new { Book = b, b.Author } });

//query syntax
var booksAndAuthors2 =
    from b in db.Books
    from a in db.Authors
    select new { Book = b, Author = a };
</pre>

<h5 class="section-header"><a name="group-by">Group by</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT authorId, COUNT(*) AS count
FROM books
GROUP BY authorId

//fluent syntax
var numBooksPerAuthor = 
    db.Books.GroupBy(b => b.AuthorId)
            .Select(g => new { AuthorId = g.Key, Count = g.Count() });

//query syntax
var numBooksPerAuthor2 = 
    from b in db.Books
    group b by b.AuthorId into g
    select new { AuthorId = g.Key, Count = g.Count() };
</pre>
<br/>
<pre>
SELECT authorId, genre, COUNT(*) AS count
FROM books
GROUP BY authorId, genre

//fluent syntax
var numBooksPerAuthorGenre =
    db.Books.GroupBy(b => new { b.AuthorId, b.Genre })
            .Select(g => new { g.Key.AuthorId, g.Key.Genre, Count = g.Count() });

//query syntax
var numBooksPerAuthorGenre2 =
    from b in db.Books
    group b by new { b.AuthorId, b.Genre } into g
    select new { g.Key.AuthorId, g.Key.Genre, Count = g.Count() };
</pre>

<h5 class="section-header"><a name="having">Having</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT authorId, COUNT(*) AS count
FROM books
GROUP BY authorId
HAVING COUNT(*) > 10

//fluent syntax
var authorsWithManyBooks =
    db.Books.GroupBy(b => b.AuthorId)
            .Select(g => new { AuthorId = g.Key, Count = g.Count() })
            .Where(g => g.Count > 10);

//query syntax
var authorsWithManyBook2 =
    from b in db.Books
    group b by b.AuthorId into g
    where g.Count() > 10
    select new { AuthorId = g.Key, Count = g.Count() };


<h5 class="section-header"><a name="distinct">Distinct</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT DISTINCT *
FROM books
IQueryable<Book> authors = db.Books.Distinct();
</pre>

<h5 class="section-header"><a name="union">Union</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT title
FROM books
UNION ALL
SELECT title
FROM articles

IQueryable<string> titles =
    db.Books.Select(b => b.Title)
            .Concat(db.Articles.Select(art => art.Title));
</pre>
<br/>
<pre>
SELECT title
FROM books
UNION
SELECT title
FROM articles

IQueryable<string> titles =
    db.Books.Select(b => b.Title)
            .Union(db.Articles.Select(art => art.Title));
</pre>

<h5 class="section-header"><a name="order-by">Order by</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT *
FROM books
ORDER BY title

//fluent syntax
IQueryable<Book> books = db.Books.OrderBy(b => b.Title);

//query syntax
IQueryable<Book> books2 =
    from b in db.Books
    orderby b.Title
    select b;
</pre>
<br/>
<pre>
SELECT *
FROM books
ORDER BY title DESC

//fluent syntax
IQueryable<Book> books = db.Books.OrderByDescending(b => b.Title);

//query syntax
IQueryable<Book> books2 =
    from b in db.Books
    orderby b.Title descending
    select b;
</pre>
<br/>
<pre>
SELECT *
FROM books
ORDER BY genre DESC, title

//fluent syntax
IQueryable<Book> books = 
    db.Books.OrderByDescending(b => b.Genre)
            .ThenBy(b => b.Title);

//query syntax
IQueryable<Book> books2 =
    from b in db.Books
    orderby b.Genre descending, b.Title
    select b;
</pre>

<h5 class="section-header"><a name="case-statement">Case statement</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT CASE 
    WHEN pages > 300 
    THEN 'long' 
    ELSE 'short' 
END AS bookLength
FROM books

//fluent syntax
IQueryable<string> bookLengths = 
    db.Books.Select(b => b.Pages > 300 ? "long" : "short");

//query syntax
IQueryable<string> bookLengths2 =
    from b in db.Books
    select b.Pages > 300 ? "long" : "short";
</pre>

<h5 class="section-header"><a name="coalesce">Coalesce</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT COALESCE(genre, 'unknown') AS genre
FROM books

//fluent syntax
IQueryable<string> genres = 
    db.Books.Select(b => b.Genre ?? "unknown");

//query syntax
IQueryable<string> genres2 =
    from b in db.Books
    select b.Genre ?? "unknown";
</pre>

<h5 class="section-header"><a name="aggregate-functions">Aggregation functions (e.g. min, max, average, count)</a></h5>
<p class="section-header-footnote"><i>See the <a href="#data-model">data model</a> and <a href="#entity-framework-data-context">Entity Framework data context</a> to understand the setup for the following examples.</i></p>

<pre>
SELECT MIN(pages)
FROM books

int minPages = db.Books.Min(b => b.Pages);
</pre>
<br/>
<pre>
SELECT MAX(pages)
FROM books

int maxPages = db.Books.Max(b => b.Pages);
</pre>
<br/>
<pre>
SELECT AVG(pages)
FROM books

double averagePages = db.Books.Average(b => b.Pages);
</pre>
<br/>
<pre>
SELECT COUNT(*)
FROM books

int count = db.Books.Count(b => b);
</pre>