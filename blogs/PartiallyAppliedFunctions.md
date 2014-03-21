Partial function application is the processing of fixing a number of arguments to a function. This is an interesting and useful feature in functional programming languages such as Scala, R, and Javascript.  A basic example is a function that takes two numbers and adds them together. Here is some R code for this.  Conveniently, this code is also syntactically valid javascript. So if you aren't familiar with R you can just paste this into your javascript console as well.
<pre>addNumbers = function(numberOne, numberTwo) { return (numberOne + numberTwo) }

&gt; addNumbers(4,5)
[1] 9</pre>
R and javascript use closure to partially apply a function.
<pre>addFive = function(number) {
	return addNumbers(number, 5) 
}

&gt; addFive(5)
[1] 10</pre>
In Scala, we use to the _ notation to partially apply functions.
<pre>val addNumbers: ((Double, Double) =&gt; Double) = 
                     (numberOne, numberTwo) =&gt; numberOne + numberTwo

val addFive = addNumbers(_: Double, 5)</pre>
Lets dive into a more practical example of partial application. Lets say you have a data matrix with variables a, b, c, d. For this example I'm using R code and the values for a, b, c, and d are just random samples from a normal distribution.
<pre>data = as.data.frame(cbind(a = rnorm(100), b = rnorm(100), c = rnorm(100), d = rnorm(100)))</pre>
Lets say we interested in seeing how correlated variable "a" is with all the other variables. R provides a function call "cor" that takes two vectors and returns their correlation. The non-functional approach to this problem would be to write a for loop. Let's take a look at what that looks like:
<pre>correlations = rep(NA,ncol(data[,-1]))
names(correlations) = c("b", "c", "d")

# we want to skip the first column since that is "a"
for (colIndex in 1:(ncol(data[,-1]))) { 
	correlations[colIndex] = cor(data[,colIndex + 1], data$a)
}</pre>
The above code is messy. There is a lot of different indexing going on that makes the code appear complicated, and we have to initialize a correlations vector with NAs which also isn't ideal. Here's were partial function application comes in to save the day.
<pre>corWithA = function(variable) { return(cor(variable, data$a)) }</pre>
We partially apply data$a as one the arguments to the "cor" function to make another function that calculates the correlation of any vector with variable "a". Now instead of iterating through the data frame with a for loop we are going to use the "apply" function. The apply function is similar to Scala or Javascript's map function. Here's a quick example of javascript's "map" function.  Map in the case of an array takes a function and applies it to each element in the array.
<pre>arr = [1,2,3]
[1, 2, 3]
arr.map(function(x) { return (x + 1) })
[2, 3, 4]</pre>
"apply" takes a data frame or matrix , a number 1 (rows) or 2 (columns) to signify what to map over, and finally a function to apply to each row or column. Here are some simple examples on the data frame we are using:
<pre># Row Sums
apply(data, 1, sum)
# Column sums
apply(data, 2, sum)</pre>
Here is our use of "apply" and our partially applied function "corWithA" that shortens up the number of lines of code significantly:
<pre>correlations = apply(data[,-1], 2, corWithA)</pre>
We can EVEN make it one-liner if we pass corWithA in as an anonymous function
<pre>correlations = apply(data[,-1], 2, function(variable) { return cor(variable, data$a) })</pre>
And that my friends is the power of partially applied functions. We took what was 5 lines of messy code that used a for loop and shortened it down for a 1 liner.
