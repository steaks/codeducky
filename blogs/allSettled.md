<a href="https://github.com/kriskowal/q">Q</a> has <a href="https://github.com/kriskowal/q/wiki/API-Reference#promiseallsettled">Q.allSettled</a>.  <a href="https://docs.angularjs.org/api/ng/service/$q">$q</a> does not.  I've noticed a lot of AngularJS devs request $q.allSettled.  So here it is!  <a href="http://jsfiddle.net/0sr65m7t/">JS Fiddle</a>

<!--more-->
<pre>
angular.module("qImproved", [])
.config(function ($provide) {
  $provide.decorator("$q", function ($delegate) {
    function allSettled(promises) {
      var deferred = $delegate.defer(),
          counter = 0,
          results = angular.isArray(promises) ? [] : {};

      angular.forEach(promises, function(promise, key) {
        counter++;
        $delegate.when(promise).then(function(value) {
          if (results.hasOwnProperty(key)) return;
          results[key] = { status: "fulfilled", value: value };
          if (!(--counter)) deferred.resolve(results);
        }, function(reason) {
          if (results.hasOwnProperty(key)) return;
          results[key] = { status: "rejected", reason: reason };
          if (!(--counter)) deferred.resolve(results);
        });
      });

      if (counter === 0) {
        deferred.resolve(results);
      }

      return deferred.promise;
    }
    $delegate.allSettled = allSettled;
    return $delegate;
  });
});
</pre>
<br/>
Example usage:
<pre>
$q.allSettled([promise0, promise1])
.then(function (data) {
  var result0 = data[0];
  var result1 = data[1];
  if (result0.status === "fulfilled") { console.log(result0.value); }
  if (result1.status === "rejected") { console.log(result1.reason); }
});
</pre>