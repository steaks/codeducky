Recently, a user reported some difficulty using our software. Investigating our error logs, we determined that the culprit was a classic one: SQLServer deadlocks. What made less sense were the two deadlocking queries: one query was an insert into two tables (one via trigger). The other query was a read which joined those same two tables. While there are <a href="http://stackoverflow.com/questions/847139/is-it-possible-to-create-a-deadlock-with-read-only-access">various ways that this can cause a problem</a>, it shouldn't have been a persistant issue here and hadn't ever been one in the past. What was going on?

<!--more-->

<h2 id="isolation-levels">Isolation levels</h2>

Digging in a bit further with SQLServer Profiler, we determined that the real cause was the fact that the read was executing under the SERIALIZABLE <a href="https://docs.microsoft.com/en-us/sql/t-sql/statements/set-transaction-isolation-level-transact-sql">isolation level</a>. By default, SQL server uses the READ COMMITTED isolation level. Under this model, SQL takes a shared lock on each row/page as it is being read, and releases that lock as it moves on to the next row. In contrast, SERIALIZABLE causes SQL to take range locks on the entire range of keys being read and these locks are held until the transaction completes. This behavior is far more prone to causing deadlocks.

Armed with this information, we investigated the code to locate the problematic transaction, only to find that the query in question was not executing within a transaction! In fact, the problematic read query was even using it's own isolated connection!

<pre>
using (var connection = new SqlConnection(connectionString))
{
	connection.Open();
	using (var command = connection.CreateCommand())
	{
		command.CommandText = "... /* no BEGIN TRAN or SET TRANSACTION ISOLATION LEVEL here! */ ..."; 
		...
		using (var reader = command.ExecuteReader())
		{
			...
		}
	}
}
</pre>

<h2 id="missing-transaction">The case of the missing transaction</h2>

Absent a transaction, why were we running into transaction isolation level issues? The first hint was that SQL server executes every statement in a transaction, even if none is specified. This behavior is known as "<a href="https://technet.microsoft.com/en-us/library/ms187878(v=sql.105).aspx">autocommit</a>", and it makes a lot of sense: any single query (e. g. a bulk update) should either happen or not. It shouldn't be able to crash out halfway through its work. 

Like transactions declared with BEGIN TRAN in SQL, autocommit transactions operate under whatever isolation level was last set on the connection (using SET TRANSACTION ISOLATION LEVEL).

<h2 id="leak">Leaking isolation levels</h2>

The behavior we were seeing thus makes sense if the connection's isolation level were somehow being set or defaulting to SERIALIZABLE. But how can that happen given that we JUST opened the connection? The next part of the explanation lies in an obscure behavior of the SqlConnection class. According to the <a href="https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.begintransaction?view=netframework-4.7#System_Data_SqlClient_SqlConnection_BeginTransaction_System_Data_IsolationLevel_">MSDN docs</a>:

<blockquote>
After a transaction is committed or rolled back, the isolation level of the transaction persists for all subsequent commands that are in autocommit mode (the SQL Server default). This can produce unexpected results, such as an isolation level of REPEATABLE READ persisting and locking other users out of a row.
</blockquote>

That means that if we open and close a transaction with a SERIALIZABLE isolation level, the connection will retain that isolation level until something else resets it. But still, we're using a brand new connection. Hmmm...

<h2 id="final-piece">The final piece of the puzzle</h2>

The final key is connection pooling. Even though the .NET APIs give us the impression that we are creating and destroying connections, under the hood closing or disposing connections returns them to a shared pool by default. Future opens will, if possible, draw from the pool of existing connections rather than creating new ones. This is an important optimization for many applications.

When a pooled connection is "re-opened", .NET issues the special <a href="http://web.archive.org/web/20100730003952/http://sqldev.net/articles/sp_reset_connection/default.html">sp_reset_connection</a> stored procedure. sp_reset_connection's job is to clear the connection state such that the new user will have the experience of a fresh connection. Unfortunately, there is one particular connection property which does not get reset: the isolation level.

<strong>That means that a connection with a leaked isolation level can be returned to the connection pool and then re-used by an unsuspecting consumer!</strong>

This behavior is easy to reproduce yourself:

<pre>
var connectionString = new SqlConnectionStringBuilder 
	{
		// using a max pool size of 1 guarantees that we'll
		// get the same connection when we close and re-open
		MaxPoolSize = 1,
		DataSource = @".\SQLEXPRESS", 
		IntegratedSecurity = true 
	}
	.ConnectionString;

using (var conn = new SqlConnection(connectionString))
{
	conn.Open();
	PrintIsolationLevel(conn); // ReadCommitted
	using (conn.BeginTransaction(System.Data.IsolationLevel.Serializable))
	{
	}
	PrintIsolationLevel(conn); // Serializable (in-connection leak)
}

using (var conn = new SqlConnection(connectionString))
{
	conn.Open();
	PrintIsolationLevel(conn); // Serializable cross-connection leak
}

void PrintIsolationLevel(SqlConnection connection)
{
	using (var command = connection.CreateCommand())
	{
		command.CommandText = @"
			SELECT CASE transaction_isolation_level 
			WHEN 0 THEN 'Unspecified' 
			WHEN 1 THEN 'ReadUncommitted' 
			WHEN 2 THEN 'ReadCommitted' 
			WHEN 3 THEN 'Repeatable' 
			WHEN 4 THEN 'Serializable' 
			WHEN 5 THEN 'Snapshot' END AS TRANSACTION_ISOLATION_LEVEL 
			FROM sys.dm_exec_sessions 
			WHERE session_id = @@SPID";
		Console.WriteLine(command.ExecuteScalar());
	}
}
</pre>

<h2 id="fix">The fix</h2>

Once we had everything figured out, fixing the problem was relatively easy. We took a few different steps:
- Where possible, use the default isolation level for transactions. We found that in some cases developers had been using SERIALIZABLE unnecessarily.
- After closing a transaction with a non-default isolation level, issue a query of <em>SET TRANSACTION ISOLATION LEVEL READ COMMITTED</em> on that connection.
- Remove usages of the TransactionScope class, which can affect multiple underlying connections and therefore can be difficult to clean up after.

While this problem only manifested itself in a noticeable issue recently, much of the code in question has been around for years. As the code runs through different paths on the web server, connections are constantly being flipped to different isolation levels. It's hard not to wonder how many irreproducible transient deadlock errors might have had this as the underlying cause! If you use transactions and isolation levels in your application and weren't aware of this behavior, it's probably worth a look through your codebase.