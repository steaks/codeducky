Hey awesome Javascript developers!  Tonight I wanted to look at the <a href="http://en.wikipedia.org/wiki/Lazy_initialization">Lazy initialization</a> pattern.  It's a useful pattern that unfortunately is not promoted much in Javascript culture.  Some languages go so far as making <a href="http://stackoverflow.com/questions/7484928/what-does-a-lazy-val-do/7484933#7484933">lazy a keyword</a>.

<!--more-->

The lazy pattern is simple:

1. Specify how to create something.
2. Create that something the first time you access it.
<br/>

Here's a simple example of how you can use the lazy pattern.
<pre>
var greeting = new Lazy(function () { /* much computing */ return "Very Hello"; });

function alertGreeting() {
    //slow because we have to compute the value
    //since we're accessing it for the first time
    alert(greeting.value);
    //quick because we use a cached version of the
    //value that we just computed.
    alert(greeting.value);
}
</pre>
<br/>
It can even work with <a href="https://docs.angularjs.org/api/ng/service/$q">promises</a>.

<pre>
var getGreeting = new Lazy(function () { return $http.get("wow.us"); } );

function alertGreeting() {
    //issues get request
    getGreeting.value.then(function (greeting) { alert(greeting); });
    //uses promise from the first issued request
    //so no new HTTP request is fired
    getGreeting.value.then(function (greeting) { alert(greeting); });
}
</pre>
<br/>
Underlying code for Lazy (<a href="https://gist.github.com/steaks/9a37c70c50dda4a4dce4">gist</a>):

<pre>
function Lazy(func) {
    this.isValueCreated = false;
    this.func = func;
}

Object.defineProperty(Lazy.prototype, "value", {
    get: function () {
        if (!this.isValueCreated) {
            this._value = this.func();
            this.isValueCreated = true;
        }
        return this._value;
    }
});
</pre>