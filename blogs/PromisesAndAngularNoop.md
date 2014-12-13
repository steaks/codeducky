Recently I started working with <a href="https://angularjs.org/">AngularJS</a>.  One of things I enjoy most about AngularJS are <a href="https://docs.angularjs.org/api/ng/service/$q">promises</a>.  Promises are a design pattern / module useful for asynchronous tasks.  Normally, I rave about how awesome promises are, but instead of raving I wanted to talk about something that tripped me up when I first started using promises.

The following promises are not equivalent.  And I'm going to explain why.

<pre>
var deferred = $q.defer();
deferred.promise.then(mySuccessCallback, angular.noop, myNotifyCallback);
deferred.promise.then(mySuccessCallback, null, myNotifyCallback);
</pre><br/>
Let's begin with a simple promise:
<pre>
var deferred = $q.defer();
deferred.promise
.then(
  /*success*/ function (data) { console.log("Success: " + data); },
  /*error*/ function (reason) { console.log("Error: " + reason); });

deferred.reject("My Error");
</pre>
<pre>
Error My Error
</pre><br/>
But what happens here?
<pre>
var deferred = $q.defer();

deferred.promise
.then(
  /*success*/ function (data) { console.log("Success: " + data); },
  /*error*/ function (reason) { console.log("Error: " + reason); })
.then(
  /*success*/ function (data) { console.log("Success 2: " + data); },
  /*error*/ function (reason) { console.log("Error 2: " + reason); });

deferred.reject("My Error");
</pre>
<pre>
Error: My Error
Success 2: undefined
</pre><br/>
Why is the second success callback being invoked?  And why is our data undefined? Documentation for promise.then tells us why.
<blockquote>
then(successCallback, errorCallback, notifyCallback) – regardless of when the promise was or will be resolved or rejected, then calls one of the success or error callbacks asynchronously as soon as the result is available. The callbacks are called with a single argument: the result or rejection reason. Additionally, the notify callback may be called zero or more times to provide a progress indication, before the promise is resolved or rejected.

<span style="font-weight:bold">This method returns a new promise which is resolved or rejected via the return value of the successCallback, errorCallback.</span> It also notifies via the return value of the notifyCallback method. The promise cannot be resolved or rejected from the notifyCallback method.
</blockquote>

Notice that our first error callback does not return a value (i.e. it returns undefined).  Therefore, our error callback is returning a resolved promise with undefined as the value!  The proper way to propagate errors down the chain is to use $q.reject;

<pre>
var deferred = $q.defer();

deferred.promise
.then(
  /*success*/ function (data) { console.log("Success: " + data); },
  /*error*/ function (reason) { console.log("Error: " + reason); return $q.reject(reason); })
.then(
  /*success*/ function (data) { console.log("Success 2: " + data); },
  /*error*/ function (reason) { console.log("Error 2: " + reason); return $q.reject(reason); });

deferred.reject("My Error");
</pre>
<pre>
Error: My Error
Error 2: My Error	
</pre><br/>
But there's still another bug, and it's in our success callbacks.  You can probably guess by now that the second success callback would not log the expected data if we resolve our promise.

<pre>
var deferred = $q.defer();

deferred.promise
.then(
  /*success*/ function (data) { console.log("Success: " + data); },
  /*error*/ function (reason) { console.log("Error: " + reason); return $q.reject(reason); })
.then(
  /*success*/ function (data) { console.log("Success 2: " + data); },
  /*error*/ function (reason) { console.log("Error 2: " + reason); return $q.reject(reason); });

deferred.resolve("My Data");
</pre>
<pre>
Success: My Data
Success 2: undefined	
</pre><br/>
The corrected code returns data in the success callbacks.

<pre>
var deferred = $q.defer();

deferred.promise
.then(
  /*success*/ function (data) { console.log("Success: " + data); return data; },
  /*error*/ function (reason) { console.log("Error: " + reason); return $q.reject(reason); })
.then(
  /*success*/ function (data) { console.log("Success 2: " + data); return data; },
  /*error*/ function (reason) { console.log("Error 2: " + reason); return $q.reject(reason); });

deferred.resolve("My Data");
</pre>
<pre>
Success: My Data
Success 2: My Data	
</pre><br/>
Now that our code is bug free, let's circle back to the original question: why is angular.noop different from null in the world of promises?  Simple, AngularJS correctly continues resolving or rejecting the promise if we don't pass in a function.  If we do pass in a function AngularJS assumes that we're handling resolving/rejecting our data appropriately.  angular.noop incorrectly handles resolving/rejecting correctly because it always returns undefined.

<pre>
var deferred = $q.defer();
deferred.promise.then(mySuccessCallback, angular.noop, myNotifyCallback);
deferred.promise.then(mySuccessCallback, null, myNotifyCallback);
</pre><br/>
Here's the snippet of angularJS code that handles null success/error callbacks.

<pre>
if (isFunction(fn)) {
  promise.resolve(fn(state.value));
} else if (state.status === 1) {
  promise.resolve(state.value);
} else {
  promise.reject(state.value);
}
</pre>