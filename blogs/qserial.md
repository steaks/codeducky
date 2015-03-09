Hey AngularJS devs!  Let's talk about executing async tasks serially with promises.

First, we notice .then allows our success/fail callbacks to return a promise.  $q treats success/fail callbacks that return promises specially.  We'll look at two examples to see how $q treats success/fail functions that return promises differently from those that don't.
<!--more-->

Normal success function:
<pre>
$q.when()  
.then(function () { console.log("foo"); })
.then(function () { console.log("bar"); });
</pre>
<pre>
foo
bar
</pre>
<br/>
Success function that returns a promise:
<pre>
$q.when()
.then(function () { 
  console.log("foo"); 
  var bazDeferred = $q.defer();
  var bazPromise = bazDeferred.promise;
  return bazPromise;
})
.then(function () { console.log("bar"); });
</pre>
<pre>
foo  
</pre>
<br/>
As expected, our first example logs "foo" then "bar".  However, our second example only logs "foo".  $q does not continue executing the chain of promises until bazPromise has been resolved.  So "bar" is never logged.

This is good!  $q allows us to execute chained promises sequentially by returning promises in our success functions.

Let's see this $q feature in action:
<pre>
$q.when()
.then(function () {
  var deferred = $q.defer();
  console.log("foo"); 
  $timeout(function () { deferred.resolve(); }, 5000);
  return deferred.promise;
})
.then(function () { console.log("bar"); });
</pre>
<pre>
foo  
//5 seconds
bar
</pre>
<br/>
Great, so now we see how we can execute promises sequentially.  How do we pass data through to the next promise?  Easy, we pass arguments to .resolve() or .reject().

<pre>
$q.when()
.then(function () {
  var deferred = $q.defer();
  console.log("foo"); 
  $timeout(function () { deferred.resolve("bar"); }, 5000);
  return deferred.promise;
})
.then(function (data) { console.log(data); })
</pre>
<pre>
foo  
//5 seconds
bar
</pre>
<br/>
Ok, so we've only chained two sequential promises together.  What if I want to chain 5, 10, or 100?  Can we generalize this construct?  A little harder, but sure we can!

<pre>
function serial(tasks) {
  var prevPromise;
  angular.forEach(tasks, function (task) {
    //First task
    if (!prevPromise) { 
      prevPromise = task(); 
    } else {
      prevPromise = prevPromise.then(task); 
    }
  });
  return prevTask;
}

var task1 = function () {
  var task1Deferred = $q.defer();
  $timeout(function () {
    task1Deferred.resolve("foo");
  }, 5000);
  return task1Deferred.promise;
};

var task2 = function (data) { console.log(data); };

serial([task1, task2]);
</pre>  

<pre>
foo
//after 5 seconds
bar  
</pre>  
<br/>
Great, task1 is executed.  Then task2 is executed after task1Promise is resolved.  

Hold up, I just used a new word "task."  IMO we in the AngularJS community don't use the word "task" as often as we should.  So let me define how I imagine "task."  In plain english a task is a unit of work to be done.  With reference to AngularJS, I often of a task as a function that returns a promise.

Why? Because a function performs a unit of work (or at least kicks off a unit of work).  And a promise let's me know the status of the task (in progress, succeeded, failed).  Knowing the status of a task is very useful!  

The term isn't perfect, but it helps me organize my thoughts when thinking about promises.  So let's stick with it.

<pre>
function washDishesTask() {
  var deferred = $q.defer();
  $timeout(function () { 
    washDishes(); 
    $q.defer(); 
  }, 1000);
  return deferred.promise;
}
</pre>
<br/>
Now that we have serial() and have defined "tasks," let's finish creating $q.serial by adding handy error checking and decorating $q.

<pre>
angular.module("qImproved", [])
.config(function ($provide) {
  $provide.decorator("$q", function ($delegate) {
    //Helper method copied from q.js.
    var isPromiseLike = function (obj) { return obj && angular.isFunction(obj.then); }

    /*
     * @description Execute a collection of tasks serially.  A task is a function that returns a promise
     *
     * @param {Array.<Function>|Object.<Function>} tasks An array or hash of tasks.  A tasks is a function
     *   that returns a promise.  You can also provide a collection of objects with a success tasks, failure task, and/or notify function
     * @returns {Promise} Returns a single promise that will be resolved or rejected when the last task
     *   has been resolved or rejected.
     */
    function serial(tasks) {
      //Fake a "previous task" for our initial iteration
      var prevPromise;
      var error = new Error();
      angular.forEach(tasks, function (task, key) {
        var success = task.success || task;
        var fail = task.fail;
        var notify = task.notify;
        var nextPromise;

        //First task
        if (!prevPromise) {
          nextPromise = success();
          if (!isPromiseLike(nextPromise)) {
            error.message = "Task " + key + " did not return a promise.";
            throw error;
          }
        } else {
          //Wait until the previous promise has resolved or rejected to execute the next task
          nextPromise = prevPromise.then(
            /*success*/function (data) {
              if (!success) { return data; }
              var ret = success(data);
              if (!isPromiseLike(ret)) {
                error.message = "Task " + key + " did not return a promise.";
                throw error;
              }
              return ret;
            },
            /*failure*/function (reason) {
              if (!fail) { return $delegate.reject(reason); }
              var ret = fail(reason);
              if (!isPromiseLike(ret)) {
                error.message = "Fail for task " + key + " did not return a promise.";
                throw error;
              }
              return ret;
            },
            notify);
        }
        prevPromise = nextPromise;
      });

      return prevPromise;
    }

    $delegate.serial = serial;
    return $delegate;
  });
});
</pre>
<br/>
Now that we have the full $q.serial API, let's investigate its power.  $q.serial is most useful when you have a tasks that are expensive, tie up resources, and are not time sensitive.

At <a href="http://hurdlr.com/">Hurdlr</a> we encountered this exact criteria the other day.  We build a mobile app.  Internet tends to be less reliable on phones than on computers.  So we try to minimize server calls.  

One way we minimize server calls is by eagerly loading bulk data.  So depending on module, user's config, etc. we execute a set of tasks that load bulk data.  We don't want to execute all the tasks at once because it can tie up more database resources and phone resources when caching than we'd like.
 
<pre>
var tasks = [];
if (user.config.foo) {
    tasks.push(retrieveFooData);
}
if (module.bar) {
    tasks.push(retrieveBarData);
}
$q.serial(tasks);
</pre>
<br/>
Ok, so we've got 99% of the functionality we need.  But I should mention that $q.serial supports reject and notify also.  Oh, and $q.serial can accept a hash instead of an array.

<pre>
var task1 = function () {
  var deferred = $q.defer();
  console.log("one");
  $timeout(function () { deferred.notify(); });
  $timeout(function () { deferred.reject(); }, 5000);
  return deferred.promise;
};
var task2 = {
  success: function () {
    console.log("two");
    return $q.when();
  }, fail: function () {
    console.log("fail two");
    return $q.reject();
  }, notify: function () {
    console.log("notified two");
  }
};

$q.serial({ one: task1, two: task2 });
</pre>
<pre>
one
notified two
//5 seconds
fail two
</pre>
<br/>

So that's $q.serial, a generalized way to execute async tasks serially in AngularJS.