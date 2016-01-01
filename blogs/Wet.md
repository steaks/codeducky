References:

rule of three: http://blog.codinghorror.com/the-delusion-of-reuse/
eric lippert: https://blogs.msdn.microsoft.com/ericlippert/2003/10/28/how-many-microsoft-employees-does-it-take-to-change-a-lightbulb/
guava: https://code.google.com/p/guava-libraries/wiki/HowToContribute, https://code.google.com/p/guava-libraries/wiki/PhilosophyExplained#Utility_Times_Ubiquity

dry overview: http://code.tutsplus.com/tutorials/3-key-software-principles-you-must-understand--net-25161

General points:
* what is DRY
* DRY as a religion

The cost of DRY:

- DRY doesn't scale: in a small system (use a for), DRY is obvious and easy to achieve. In a large system, it's not so easy to recover from.

- Identifying common abstractions is harder than it seems

- Getting common code right is hard and expensive

- DRY code is less nimble, since changes to common code have to consider a broader set of users

- DRY code prevents specialization, but sometimes that specialization is useful

- Sometimes the code is not the source of truth (constants, standard protocols)

- Sometimes more code is written to be able to use the common code than would take to re-implement the functionality

When to be WET vs. DRY?

- utility * ubiquity * certainty (on both your end and the common code's end)

- you can write your code as if DRY without making it so

Example: genetic algorithm
_________________________________________________

It's ok to get WET: when DRY isn't all it's cracked up to be

It's hard to think of any tenet in the software engineering gospel that is more widely known and more respected than the principle of "DRY" (Don't Repeat Yourself). The premise is that, by breaking the logic in our code down into re-usable bits, we make the code more concise, more extensible, and less prone to errors caused by updating a piece of logic in one place and failing to do so in others.

This sounds quite reasonable, and in many situations, it is. However, I've increasingly come to feel that the DRY approach simply doesn't make sense in all situations. Futhermore, blind adherence to DRY for it's own sake can actually affect the opposite of many of it's proported benefits, making code needlessly verbose, expensive to develop, and difficult to change.

<!--more-->

In particular, DRY begins to break down as projects get larger. Keeping a single code file or set of files DRY is relatively straightforward; doing the same in a large codebase means splitting out and identifying common utilities and APIs.
