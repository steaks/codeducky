As developers, most of us are inducted into the cult of common code and <a href="https://en.wikipedia.org/wiki/Don't_repeat_yourself">Don't Repeat Yourself</a> (DRY) early on in our software engineering educations and careers. This was certainly the case for me. The idea sticks with us because it just makes so much sense: why write more code when you could write less? Why slog through making changes in dozens of places when you could have updated a single bit of centralized logic? Why re-invent the wheel when you can use a battle-tested implementation that already exists?

Over time, however, I've come to see cracks in this philosophy, or at least the absolutist version of it. DRY is a powerful software engineering principle, but it is not the only software engineering principle. As codebases and teams get larger, a religious adherence to DRY can be crippling to maintainability and development time.

<!--more-->

<h2 id="total-logic">The Total Logic Rule</h2>

The whole point of factoring out and re-using common code is to reduce the total amount of fallible logic and complexity in our applications. A typical DRY-based design or refactor affects a program's logic in the following ways:

(A) We may require additional logic to make the common logic "generic". For example, we might need new optional parameters to account for differing needs. We may have to handle more different edge cases or create new abstractions
(B) We may need additional logic in each caller to make use of the common code, since the API for that code is no longer custom-tailored to the caller's specific use case
(C) We can delete all redundant implementations of the common logic

The key here is the word <i>total</i>: if the gains from C don't exceed the losses from A and B, we've made our appliction more complex, not less.

Does this ever happen in practice? In my experience, this kind of complexity-increasing refactor happens all the time. All it takes is for one well-intentioned and devoutly-DRY developer to notice that some bit of logic is "repeated" in several places. Unfortunately, closer examination reveals that each usage is just a little bit different. The end result: a new "common" utility that is more complex than any of the original implementations. Furthermore, we end up with a correspondingly complex API that offsets much of the gain we'd otherwise see in each place the utility is used.

<h2 id="change">Harder to Change</h2>

One of the basic premises of DRY is that it should make our code easier to change. If each piece of logic lives in exactly one place in the codebase, then any update should only require changing that once place. Right?

In overly-DRY designs, however, the opposite is true. Going to great lengths to avoid repitition can mean that we end up with layers of abstraction and genericity separating the different pieces of code that make up a feature. Sometimes, two very different features end up relying on the same common code.

This kind of code can be difficult to adapt to changing requirements. What happens when we need a new piece of contextual information to make a decision deep in the call stack? Do we refactor the entire system to account for this? Do we simply hack in a fix by "flowing through" some arbitrary data through the layers, littering our APIs with optional parameters and nullable fields along the way?

My personal experience has been that as developers we are far more eager to merge separate implementations into common utilities than we are to abandon shared pieces of code when they no longer serve our purposes.

This problem only gets worse when the common code in question is shared very broadly, perhaps even as part of a shared library. Now, future changes to the code are made more difficult by concerns about backwards compatibility. There is also a heightened risk of instability when changing a piece of logic that is used in many different places.

<h2 id="blindness">Common Code is Hard</h2>

We've touched on this already, but it bears repeating. <strong>Writing useful common code is surprisingly difficult</strong>. In part this is because we are often quite bad at recognizing general-purpose functionality when we see it. <a href="https://blog.codinghorror.com/rule-of-three/">Jeff Atwood says it well</a>:

<blockquote>
Every programmer ever born thinks whatever idea just popped out of their head into their editor is the most generalized, most flexible, most one-size-fits all solution that has ever been conceived. We think we've built software that is a general purpose solution to some set of problems, but we are almost always wrong. We have the delusion of reuse.
</blockquote>

Even when we're on the right track, it's easy to forget that writing good common code is significantly more expensive than writing code that supports a specific feature. This cost increases with the number and diversity of consumers. Eric Lippert's post on <a href="https://blogs.msdn.microsoft.com/ericlippert/2003/10/28/how-many-microsoft-employees-does-it-take-to-change-a-lightbulb/">how many Microsoft employees it takes to change a lightbulb</a> provides an excellent illustration of this. Few organizations have Microsoft's level of scale, but even for a modestly large project there are a number of issues that come into play. For example, we might have to:

* Enhance the code to handle corner cases that weren't relevant in its original usage
* Refactor the code to expose additional configuration options that different users will need
* Design for or make decisions based on backwards-compatibility
* Add additional validation and detailed error messages so that users can diagnose when things go wrong
* Implement additional test-cases
* Write additional documentation
* Create a new shared library to distribute the code or find an appropriate home for it in an existing library

Of course, we can always choose to skip these steps and just deal with issues later as they come up. My personal experience suggests that this is risky at best. Slow, buggy, or inflexible common code can consume a surprising amount of developer time, often more than it is saving.

<h2 id="conclusion">Wet or DRY?</h2>

Despite these disadvantages, in many cases DRY is still going to be the right choice. What I am really advocating for is thinking critically about decisions motivated by the DRY principle. Here are a few questions we can and should ask about any such design choice:

* <strong>What is the scope of exposure?</strong> Micro-applications of DRY such as folding repeated calls into a loop or factoring out a private utility method don't require much scrutiny. As we scale up to creating application-wide common functionality or libraries to be shared by multiple applications the potential costs get higher.
* <strong>How complex/trivial is the shared logic?</strong> By following DRY we hope to reduce total complexity. We net the greatest benefit when the shared logic is lengthy and/or tricky to get right.
* <strong>How bad is being out of sync?</strong> With DRY comes the promise of updating an entire application by changing only one piece of code. This is critically important for some shared logic (e. g. session validation checks) and of limited value to others (e. g. a string formatting utility).
* <strong>How many different consumers will their be?</strong> When it comes to shared libraries, the benefits scale with the number of different consumers. Furthermore, without a good sampling of different use cases it is difficult to know whether the code and API are truly useful in a general purpose sense.
* <strong>Is there a clear alignment of purpose?</strong> Sometimes two pieces of code can share logic because they both want the same thing. Sometimes it's more accidental: a templating language designed for HTML pages might also happen to work for code generation. Sharing code when purposes are misaligned can be risky, because future enhancements to the common code are unlikely to benefit the odd-duck consumer. They may even be problematic.

We should also remember that DRY is not a yes or no design choice. In many cases there is logic that is worth sharing, but it may be smaller in scope than was initially envisioned. For example, imagine if we had an application that apply custom arithmetic expressions (e. g. <em>2 * (a + b)</em>) to either in-memory objects or database rows. We might be tempted to create a single shared component that handles both cases. We'd need a complex abstraction layer to manage the inherent differences between in-memory and database evaluation. A better answer is likely to separate out just the expression parsing component and let each consumer handle evaluation separately.

Consider carefully, and don't be afraid to get a little <a href="https://en.wikipedia.org/wiki/Don't_repeat_yourself#DRY_vs_WET_solutions">wet</a> when the situation calls for it!
