The "new" keyword in Javascript is too confusing!  More specifically it's too confusing for consumers of my code...well it's too confusing for at least one consumer if I count myself :)
<!--more-->

As explained in <a href="http://stackoverflow.com/questions/1646698/what-is-the-new-keyword-in-javascript/3658673#3658673">What is the 'new' keyword in JavaScript?</a>, the "new" keyword does the following:
<blockquote>
<ol>
<li>It creates a new object. The type of this object, is simply object.</li>
<li>It sets this new object's internal, inaccessible, [[prototype]] property to be the constructor function's external, accessible, prototype object (every function object automatically has a prototype property).</li>
<li>It executes the constructor function, using the newly created object whenever this is mentioned.</li>
<li>It returns the newly created object, unless the constructor function returns a non-primitive value. In this case, that non-primitive value will be returned.</li>
</ol>
</blockquote>

Less eloquently, the "new" keyword can entirely change the way a function behaves!  Furthermore, it's difficult for consumers know which functions should be called with the "new" keyword (aka constructors) and which functions should not be called with the new keyword.  As a Javascript library developer, I try to implement robust constructors upholding the following requirements: 
<ul>
<li>A constructor must always execute with "this" as an object created with the "new" keyword.</li>
<li>Developers should be explicitly notified if a constructors is incorrectly invoked without the "new" keyword</li>
</ul>

<h3>Code for robust constructors</h3>

I can force my constructor to be called with "new."
<pre>
function MyConstructor(args) {
    if (!(this instanceof MyConstructor)) {
        return new MyConstructor(args);
    }
}

//both execute with the same "this"
var foo = MyConstructor(args);
var bar = new MyConstructor(args);
</pre>
<br/>
Or I can throw an explicit error notifying developers that my constructor should be called with "new."
<pre>
function MyConstructor(args) {
    if (!(this instanceof MyConstructor)) {
        throw new Error("Please use the new keyword when you call MyConstructor");
    }
}

//throws an error
var foo = MyConstructor(args);
//executes with an appropriate "this" object
var bar = new MyConstructor(args);
</pre>
<br/>
Or I can expose a regular function.

<pre>
function CreateMyConstructor(args) {
    var ret = new MyInternalConstructor(args);
    return ret;
    
}

//both execute MyInternalConstructor with the same "this" and return the internally created object
var foo = CreateMyConstructor(args);
var bar = new CreateMyConstructor(args);
</pre>
<br/>
<h3>Who else uses robust constructors?</h3>
Many popular Javascript frameworks use this pattern, so you may be using robust constructors without even knowing!

<h4><a href="https://github.com/jashkenas/underscore/blob/master/underscore.js">underscore.js</a></h4>
<pre>
// Create a safe reference to the Underscore object for use below.
var _ = function(obj) {
  if (obj instanceof _) return obj;
  if (!(this instanceof _)) return new _(obj);
  this._wrapped = obj;
};
</pre>

<h4><a href="http://yuilibrary.com/yui/docs/api/files/yui_js_yui.js.html">YUI</a></h4>
<pre>
var YUI = function() {
    var i = 0,
        Y = this,
        instanceOf = function(o, type) {
            return (o && o.hasOwnProperty && (o instanceof type));
        },
        if (!(instanceOf(Y, YUI))) {
            Y = new YUI();
        }
    return Y;
}
</pre>
