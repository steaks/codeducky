<a href="https://docs.angularjs.org/api/ng/service/$q">AngularJS promises</a> make working with asynchronous tasks easier.  In this post I'll follow a simple example that explains how you can use promises with multiple asynchronous tasks.  First, I'll explain how you can use promises with asynchronous tasks run in parallel.  Then I'll explain how you can use promises with asynchronous tasks run serially.

<!--more-->

Let's imagine that we need to download user data for three users (Larry, Moe, and Curly).
<pre>
function retrieveUserData(name) {
  return $http.get("localhost/userData")
  .success(function (data) { 
    console.log("Retrieved data for " + name); 
    //For simplicity, let's pretend data = "Data for {name}"
    return data;
  });
}
</pre><br/>
First, let's use $q.all to handle asynchronous tasks run in parallel.
<pre>
function parallelMultipleAsync() {
  var promise1 = retrieveUserData("Larry").then(function (data) { 
    console.log("Parsing data: " + data); 
    return data; 
  });
  var promise2 = retrieveUserData("Moe").then(function (data) { 
    console.log("Parsing data: " + data); 
    return data; 
  });
  var promise3 = retrieveUserData("Curly").then(function (data) { 
    console.log("Parsing data: " + data); 
    return data; 
  });
  return $q.all({ Larry: promise1, Moe: promise2, Curly: promise3 });
}

parallelMultipleAsync()
.then(function (data) { 
  console.log(data); 
  return data; 
});
</pre>
<pre>
Retrieved data for Larry
Parsing data for Larry
Retrieved data for Moe
Parsing data for Moe
Retrieved data for Curly
Parsing data for Curly
{Larry: "Data for Larry", Moe: "Data for Moe", Curly: "Data for Curly"}
</pre><br/>
<i>Note: because we executed the retrieve user data tasks in parallel, the logs are not guaranteed to always be in the same order.</i>

Second, we'll use promise.then to execute asynchronous tasks serially.  Notice that we return a promise in our first two success callbacks.
<pre>
function serialMultipleAsync() {
  var dataForLarry, dataForMoe, dataForCurly;
  return retrieveUserData("Larry")
  .then(function (data) { 
    console.log("Parsing data: " + data); 
    dataForLarry = data;
    return retrieveUserData("Moe"); 
  })
  .then(function (data) { 
    console.log("Parsing data: " + data); 
    dataForMoe = data; 
    return retrieveUserData("Curly"); 
  })
  .then(function (data) { 
    console.log("Parsing data: " + data); 
    return { Larry: dataForLarry, Moe: dataForMoe, Curly: data } 
  });
}

serialMultipleAsync()
.then(function (data) { 
  console.log(data); 
  return data; 
});
</pre>
<pre>
Retrieved data for Larry
Parsing data for Larry
Retrieved data for Moe
Parsing data for Moe
Retrieved data for Curly
Parsing data for Curly
{Larry: "Data for Larry", Moe: "Data for Moe", Curly: "Data for Curly"}
</pre>