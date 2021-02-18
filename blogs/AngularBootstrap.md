This post walks through bootstrapping an angular app asynchronously. You may find yourself needing to bootstrap angular asynchronously if you need to load system wide configs for example.

We'll cover the following steps to asynchronously boostrap angular:

<ul>
  <li>Control when you bootstrap</li>
  <li>Retrieve data from the server</li>
  <li>Store data in an angular service</li>
  <li>Create a loader UI</li>
  <li>Handle errors</li>
</ul>

<h3 class="section-head">Control when you bootstrap</h3>

<a href="https://docs.angularjs.org/api/ng/directive/ngApp">Ng-app</a> is great for bootstrapping <a href="https://jsfiddle.net/038L2373/">simple apps</a>. However, <a href="https://docs.angularjs.org/api/ng/function/angular.bootstrap">angular.bootstrap</a> provides more control.  So we'll use angular.boostrap instead of ng-app.

<h3 class="section-head">Retrieve data from your server</h3>

We should use <a href="https://docs.angularjs.org/api/ng/service/$http">$http</a> to pull data from our server before bootstrapping. Grabbing $http before we bootstrap is a bit tricky.  We'll use <a href="https://docs.angularjs.org/api/ng/function/angular.injector">angular.injector</a> to grab $http before bootstrapping.

<h3 class="section-head">Store data in an angular service</h3>

And we need to store our data somewhere. Storing our data in a service during the <a href="https://docs.angularjs.org/guide/module">configuration block</a> seems as good a place as any. So we'll create a configServiceProvider and set that object in module.config.

<h3 class="section-head">Create a loader UI</h3>

While our app is loading, we should show a UI loader.  And we want to hide any angular-esq html before our app is bootstrapped.

<h3 class="section-head">Handle errors</h3>

Finally, we need to handle errors if our data retrieval fails.  I'll leave the implementation up to you.  But you should find onFailedToRetriveDataFromServer is a good place to handle your errors.

<h3 class="section-head">Put it all together</h3>

The code that puts all these concepts together is below.  And here's a <a href="https://jsfiddle.net/av1bdvw9/">jsfiddle</a>.

<strong>Javascript</strong>

<pre>
(function () {

  //Create the app, but don't bootstrap it yet.  Bootstrapping
  //will occur after we've loaded the data from our server.
  var createApp = function () {
    var app = angular
      .module("MyApp", [])
      .controller("MyController", function ($scope) {
        $scope.foo = "bar";
      })
      .provider("configService", function () {
        var configService = {
          config : {}
        };
        this.setConfigWithDataFromServer = function (dataFromServer) {
          configService.config = dataFromServer;
        };
        this.$get = function() { return configService; };
      });
    
    return app;
  };

 
  //Fetch the data from our server.  Notice we can access $http
  //by using angular.injector
  var fetchDataFromServer = function() {
    var $http = angular.injector(["ng"]).get("$http");
    return $http.get("http://your.domain.com/yourData");
  };


  //When we're ready we can bootstrap our app and pass in
  //data from our server to the config step for our app
  var bootstrap = function(app, dataFromServer) {
    app
      .config(function (configServiceProvider) {
        //Set up a config service with the data from our server
        //so the rest of the app can acces it
        configServiceProvider.setConfigWithDataFromServer(dataFromServer);
      })
      .run(function (configService) {
        //We can now access configService from anywhere in the app
        console.log("YAY! My config is set with data from my server: " + configService.config);
      });
    
    var $app =  $(".my-app");
    var $loader = $(".my-app-loader");
    $app.removeClass("is-loading");
    $loader.removeClass("is-loading");
    angular.bootstrap($app, ["MyApp"]);
  };
  
  
  //Handle errors if retrieving data from our server failed
  var onFailedToRetriveDataFromServer = function (app, err) {
    console.log("UH OH! Need to handle error.");
  };
  
  
  //Put it all together!
  var initialize = function () {
    var app = createApp();
    fetchDataFromServer().then(
      /*success*/function (dataFromServer) { bootstrap(app, dataFromServer); }, 
      /*failure*/function (err) { onFailedToRetriveDataFromServer(app, err); });
  };
  
  initialize();
  
})();
</pre>

<strong>HTML</strong>

<pre>
<div class="my-app is-loading">
  <div ng-controller="MyController">{{::foo}}</div>
</div>
<div class="my-app-loader is-loading">loading</div>
</pre>

<strong>CSS</strong>

<pre>
.my-app.is-loading {
  display: none;
}

.my-app-loader {
  display: none;
}

.my-app-loader.is-loading {
  display: block;
}
</pre>
