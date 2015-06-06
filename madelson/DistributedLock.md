Locks are probably the most prolific synchronization construct in all of programming. The premise is simple: carve out a region of code that only one thread can enter at a time. Most programming languages provide support for locking mechanisms in their standard libraries if not (as in C#) in the languages themselves.

Unfortunately, these default tools often start to break down when we start to think about <strong>distributed locking</strong>. That is, when we don't want to limit execution just to one thread in the current process but instead to one thread in an entire system or even in a cluster of systems. 

<!--more-->

This kind of locking has a number of usages, such as:
<ul>
	<li>Synchronizing access to a shared file</li>
	<li>Implementing complex operations that require talking to multiple services and/or databases in a thread-safe way</li>
	<li>Managing a pool of shared out-of-process resources (e. g. processes or cache file space)</li>
</ul>

Recently, I faced just such a challenge which I solved by implementing a small <a href="https://www.nuget.org/packages/DistributedLock/">distributed locking library</a> for .NET. 

<h3 id="problem">The Problem</h3>

I was recently reworking some code that establishes a user's session in several legacy sub-systems of an application. Because this process can be expensive and isn't always needed, I wanted to do it lazily. The initialization code looked something like this:

<pre>
var existingSessionInfo = GetLegacySessionInformation(); // HTTP GET request to authentication service
if (existingSessionInfo != null) {
	return existingSessionInfo;
}

var token1 = LogIntoLegacySystem1(); // HTTP POST request to one legacy system

var token2 = LogIntoLegacySystem2(token1); // HTTP POST request to another legacy system

SaveLegacySessionInfo(new LegacySessionInfo(token1, token2)); // HTTP POST request to authentication service
</pre>

The problem, of course, is that this code isn't thread-safe. If two threads get past the initial check for information, both will end up trying to establish sessions for the same user. If all steps were thread-safe and idempotent we might still be okay. Unfortunately, as is so often true with legacy code, that was not the case here.

<h3 id="the solution">The Solution</h3>

It shouldn't come as a surprise that the solution I chose was to employ distributed locking. Using the <a href="https://www.nuget.org/packages/DistributedLock/">DistributedLock</a> library, here's what the code looks like:

<pre>
// don't bother taking the lock if we don't need to
var existingSessionInfo = GetLegacySessionInformation(); // HTTP GET request to authentication service
if (existingSessionInfo != null) {
	return existingSessionInfo;
}

var distributedLock = new SqlDistributedLock(
	// a distributed lock's name is it's identity. Our lock incorporates the user ID so
	// that we are synchronizing access only for that one user
	lockName: "LegacySessionInitializationFor" + userId, 
	// ConnectionString here is a connection string to a SQLServer database. Note that 
	// this need not be the same database as is used by the various services we're talking to!
	connectionString: ConnectionString
);

using (distributedLock.Acquire())
{
	// check again in case someone did this while we were waiting for the lock
	existingSessionInfo = GetLegacySessionInformation();
	if (existingSessionInfo != null) {
		return existingSessionInfo;
	}

	var token1 = LogIntoLegacySystem1(); // HTTP POST request to one legacy system

	var token2 = LogIntoLegacySystem2(token1); // HTTP POST request to another legacy system

	SaveLegacySessionInfo(new LegacySessionInfo(token1, token2)); // HTTP POST request to authentication service
}
</pre>

How does it work? Under the hood, the SqlDistributedLock class uses SQLServer's <a href="https://msdn.microsoft.com/en-us/library/ms189823.aspx">application lock</a> functionality to create and synchronize on a lock named, in this case, for the user whose session we're initializing. When we try to acquire the lock, DistributedLock begins a transaction on the
database specified by our connection string and executes sp_getapplock. This will block until the lock is available. SQLServer will hold the lock for us for the duration of that transaction, which ends at the end of the using block when the handle object returned by Acquire() is disposed.

<h3 id="procon">Pros and cons</h3>

SQLServer-based locking is powerful and quite easy to use, allowing us to synchronize across any number of systems so long as they can all talk to the same SQLServer instance. Another nice feature is that, because our hold on the lock is tied to an open transaction, we don't have to worry about the lock being held forever if the process holding it terminates unexpectedly. 

However, this method is not without its disadvantages. For one thing, each held lock requires an open connection to SQLServer, so we need to be mindful of connection pool exhaustion. A trickier issue is what happens if the SQLServer database goes down, ending our connection after we've acquired the lock. In the worst case, the server will come back up while we are still in the critical section of code, allowing another thread to obtain the lock at the same time. This is very unlikely, and in the case above the consequences just aren't that severe. All the same, this is probably not a great technique for an application that, say, operates a nuclear power plant.

Note that this issue can be solved in for the most part by having the lock acquisition actually persist some information on the database which other potential acquirers would be able to see even if the original connection had been lost. Indeed, this is similar to the approach taken by <a href="http://redis.io/topics/distlock">RedLock</a>, a distributed locking technique using a Redis database as the synchronization point. Something to think about for a future release!