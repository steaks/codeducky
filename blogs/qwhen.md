$q.when() great.  $q.whenFn() opens a new way to think about promises.
<!--more-->
whenFn:

<pre>
function whenFn(obj) {
  return $q.when(typeof obj === "function" ? obj() : obj);
}
</pre>
<br/>

$q.whenFn:

<pre>
angular.module("qImproved", [])
.config(function ($provide) {
  $provide.decorator("$q", function ($delegate) {
    function whenFn() {
      $q.when(typeof obj === "function" ? obj() : obj);
    }
    $delegate.whenFn = whenFn;
    return $delegate;
  });
});
</pre>
<br/>

$q.whenFn is simple, but it can help you create cleaner and more powerful APIs.

Recently I've been writing a lot of API's that deal with large amounts of data.  When dealing with potentially large amounts of data you need to consider when you load data carefully.

$q.whenFn provides great flexibility for eagerly or lazily loading data.  Let's take a look at an example.

Imagine you're building a list component.  The list is collapsed by default and expands when clicked.  We have two choices for loading the data:
<ol>
<li>Load the list items before the list has been clicked</li>
<li>Load the list items when the list is clicked (i.e. is being expanded)</li>
</ol>

I say, let's expose both options to consumers of our list component because $q.whenFn makes exposing both easy!

<pre>
angular.module("myModule")
.directive("myList", function ($q) {
  return {
    scope: {
      values: "="
    },
    link: function (scope) {
      scope.expand = function () {
        $q.whenFn(values)
          .then(function (items) { 
            scope.items = items; 
            scope.isExpanded = true;
          });
      };
    }
  };
});
</pre>
<pre>
<button ng-click="expand()">
<ul ng-show="isExpanded">
  <li ng-repeat="item in items">{{item}}</li>
</ul>
</pre>
<br/>
With $q.whenFn you can pass values an array or a function.  Passing an array implies eager loading.  Passing a function implies lazy loading because the function will not be executed until $q.whenFn is invoked.  Now myList is super flexible because you can eagerly or lazily load your list values with the same API!

<pre>
angular.module("myModule")
.controller("MyController", function () {
  $scope.retrieveFooValues = function () {
    return $http.get("/foovalues");
  };
  $scope.barValues = ["Moe", "Larry", "Curly"];
});
</pre>
<pre>
  <my-list values="retrieveFooValues"></my-list>
  <my-list values="barValues"></my-list>
</pre>
<br/>

So that's $q.whenFn.  I use it in tons of components in my apps.  It's particularly useful in core/commonly used components that require flexible APIs.