Software design patterns are really, really important!  From intuitive to illusive, simple to complex, identifying useful design patterns helps us write more idiomatic and organized code.  Both qualities lead to faster development and less bugs.  With that said, I want to share a simple but valuable design that my teammates and I use everyday.  We don't have a formal name for it.  The closest terminology we use is the "view model - data provider" pattern.

The view model - data provider pattern accomplishes three goals:

<ul>
    <li><strong><span style=color:#339966;">It allows us to mix and match view logic with data pulling/calculation logic.</span></strong></li>
    <li><strong><span style=color:#339966;">It allows us to reuse view logic and data pulling/calculation effectively.</span></strong></li>    
    <li><strong><span style=color:#339966;">It encourages us to write contained and object oriented code.</span></strong></li>
</ul>

By following four rules:

<ul>
    <li><strong>The view model exposes information for the display tier.</strong></li>
    <li><strong>The view model uses a data provider to pull/calculate information and should strictly use the data provider for non display logic.</strong></li>
    <li><strong>The view model and data provider are simple (i.e. easy for developers) to construct.</span></strong></li>
    <li><strong>The view model and data provider are cheap (i.e. performant) to construct.</strong></li>
</ul>

<!--more-->

I'll explain how the view model - data provider pattern accomplishes its goals by explaining the empowering parts as I build an example.  Imagine we're writing a program which calculates and displays information about two dimensional shapes.  Specifically, we will display information about a shape's area, perimeter, and a summary sentence that describes the shape.  

Let's begin with the view model and work our way back to the data providers.  We want the <strong>view model to expose information for the display tier</strong>.  Therefore, the view model must expose information about a shape's area, perimeter, and a summary sentence.  So the view model may look something like:

<pre>
class ShapeViewModel
{
    public string AreaDisplay { get; set; }
    public string PerimeterDisplay { get; set; }
    public string Summary { get; set; }
}
</pre>

We don't want the view model to calculate the shape's area nor perimeter.  Instead, the <strong>view model will use a data provider to handle the calculations</strong> like illustrated below:


<pre>
interface IShapeDataProvider
{
    double Area { get; }
    double Perimeter { get; }
}

class ShapeViewModel
{
    public ShapeViewModel(IShapeDataProvider dataProvider)
    {
        this.DataProvider = dataProvider;
    }
	
    public IShapeDataProvider DataProvider { get; private set; }

    public string AreaDisplay
    { 
        get { return "Area: " + this.DataProvider.Area; } 
    }

    public string PerimeterDisplay 
    { 
        get { return "Perimeter: " + this.DataProvider.Perimeter; } 
    }

    public string Summary 
    { 
        get 
        { 
            return String.Format(
                "This shape's area is {0} and perimeter is {1}",
                this.DataProvider.Area,
                this.DataProvider.Perimeter); 
        }
    }
}
</pre>

Finally, let's look at a data provider example so we can see the full picture.

<pre>
class TriangleDataProvider : IShapeDataProvider
{
    private readonly Lazy<double> _area;
    private readonly Lazy<double> _perimeter;

    public TriangleDataProvider(double a, double b, double c)
    {
        this._area = new Lazy<double>(
                        () => this.CalculateArea(a, b, c));
        this._perimeter = new Lazy<double>(
                        () => this.CalculatePerimeter(a, b, c));
    }

    public double Area { get { return this._area.Value; } }
    public double Perimeter { get { return this._perimeter.Value; } }

    private double CalculateArea(double a, double b, double c)
    {
        var s = this.Perimeter / 2;
        return Math.Sqrt(s * (s - a) * (s - b) * (s - c));
    }

    private double CalculatePerimeter(double a, double b, double c)
    {
        return a + b + c;
    }
}
</pre>

Notice, the <strong>view model and data provider are both simple for developers to construct</strong>.  To construct a view model, developers must provide the view model a data provider.  And developers must simply pass in appropriate values for the sides of a shape to construct this data provider.  More importantly, the <strong>view model and data provider are both cheap to construct</strong>.  Clearly the view model's initialization expense is driven by the data provider's initialization expense.  We minimize the data provider's initialization expense by relying on the <a href="http://en.wikipedia.org/wiki/Lazy_loading" target="_blank">lazy loading pattern</a> where the shape's area and perimeter are only calculated when accessed.

Now that we've constructed an example by following our four specified rules, let's look at how this pattern can be consumed and the value it provides.  See from the following example how we are able to <strong><span style=color:#339966;">mix data providers while reusing view logic</span></strong> by simply creating new data providers.

<pre>
var triangle = new ShapeViewModel(new TriangleDataProvider(5.0, 5.0, 5.0));
Console.Write(triangle.Summary);

var quadilateral = 
    new ShapeViewModel(new QuadilateralDataProvider(5.0, 5.0, 5.0, 5.0));
Console.Write(quadilateral.Summary);
</pre>

We also want our <strong><span style=color:#339966;">view logic and data provider logic to be easily reused</span></strong>.  The simplicity and cheapness of constructing view models and data providers allows us to reuse the view model and data provider logic thoughout our codebase without worrying about performance or complexity.  The following example illustrates how we display the areas of our shapes on one page and the perimeters on another page.

<pre>
//perhaps one page displays areas for our shapes
var triangle = new ShapeViewModel(new TriangleDataProvider(5.0, 5.0, 5.0));
Console.Write(triangle.AreaDisplay);

var quadilateral = 
    new ShapeViewModel(new QuadilateralDataProvider(5.0, 5.0, 5.0, 5.0));
Console.Write(quadilateral.AreaDisplay);


//and another page rendered by a different part 
//of the code displays perimeters for our shapes
var triangle = new ShapeViewModel(new TriangleDataProvider(5.0, 5.0, 5.0));
Console.Write(triangle.PerimeterDisplay);

var quadilateral = 
    new ShapeViewModel(new QuadilateralDataProvider(5.0, 5.0, 5.0, 5.0));
Console.Write(quadilateral.PerimeterDisplay);
</pre>

Finally, notice how this pattern <strong><span style=color:#339966;">encorages us to keep code associated with the view model contained to itself or its data provider making for a good object oriented design</span></strong>.  With this pattern, you'll never have to search for spaghetti code that calculates or propagates calculation/display information.  The view model - data provider pattern helps us write better organized and readable code by encouraging good object oriented practices.

Now that I've explained theoretically how to implement this pattern and the value it adds, let me more concretely say how my teammates and I use this pattern.  We primarily work on a web application with an ASP.NET MVC4 technology stack.  We implement an <a href="http://stackoverflow.com/questions/11064316/what-is-viewmodel-in-mvc">MVC - View Model</a> pattern where the view model is constructed in a controller and passed into the view.  The data provider is referenced by the view model, is in the data project.

ShapeController.cs
<pre>
public class ShapeController
{
    public ActionResult Triangle(double a, double b, double c)
    {
        var viewModel = new ShapeViewModel(new TriangleDataProvider(a, b, c));
        return this.View("Triangle", viewModel);
    }
}
</pre>

Triangle.cshtml
<pre>
@model Triangle

<span>@Model.Summary</span>
<span>@Model.PerimeterDisplay</span>
<span>@Model.AreaDisplay</span>

</pre>
