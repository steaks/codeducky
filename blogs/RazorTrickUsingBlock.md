Using blocks are a very useful tool when rendering HTML markup with razor.  You can use them to consolidate duplicated markup into simple, readable, and reusable C# methods.

<!--more-->
For example, you can centralize code for footnote sections that require special css styling.

Without using block:

<pre>
<div class="footnote">
    <span>foo footnote</span>
</div>

<div class="footnote">
    <span>bar footnote</span>
</div>
</pre>
<br/>
Centralized code with using blocks:

<pre>
@using (Html.FootnoteSection())
{
    <span>foo footnote</span>
}

@using (Html.FootnoteSection())
{
    <span>bar footnote</span>
}
</pre>
<br/>
First, create a common base class class that makes writing blocks easy.
<br/>
<pre>
public abstract class Block : IDisposable
{
    private int _isDisposed;
    private const int IS_DISPOSED = 1;

    protected HtmlHelper HtmlHelper { get; private set;}

    public Block(HtmlHelper htmlHelper)
    {
        this.HtmlHelper = htmlHelper;
    }

    public abstract void BeginBlock();

    protected abstract void EndBlock();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, IS_DISPOSED) != IS_DISPOSED)
        {
            this.EndBlock();
        }
    }
}
</pre>

<em>Side note, read why we must use an int rather than a bool when using Interlocked.Exchange here: <a href="http://stackoverflow.com/questions/6164751/why-interlocked-exchange-does-not-support-boolean-type">why-interlocked-exchange-does-not-support-boolean-type</a>.</em>

Second, create a specific block for your footnote sections.
<br/>
<pre>
public class FootnoteSection : Block
{
    private TagBuilder _div;

    public FootnoteSection(HtmlHelper htmlHelper) : base(htmlHelper) { }

    public override void BeginBlock()
    {
        _div = new TagBuilder("div");
        _div.AddCssClass("footnote");
        this.HtmlHelper.ViewContext.Writer
            .Write(_div.ToString(TagRenderMode.StartTag));
    }

    protected override void EndBlock()
    {
        this.HtmlHelper.ViewContext.Writer
            .Write(_div.ToString(TagRenderMode.EndTag));
    }
}
</pre>
<br/>
Third, add an extension method on HtmlHelper for ease of use.
<br/>
<pre>
public static class HtmlHelperUtilities
{
    public static FootnoteSection FootnoteSection(this HtmlHelper self)
    {
        var specialBlock = new FootnoteSection(self);
        specialBlock.BeginBlock();
        return specialBlock;
    }
}
</pre>
<br/>
That's all.  Now you've just centralized all html markup for creating footnote sections.  Take a look at <a href="http://msdn.microsoft.com/en-us/library/system.web.mvc.html.formextensions.beginform(v=vs.118).aspx">BeginForm</a> to see an example of how a builtin .NET utility leverages the using block in Razor. Also, see <a href="http://www.codeducky.org/razor-trick-pass-razor-code-into-methods/">Razor trick: pass razor code into methods</a> to learn how to implement HTML custom helpers with C# methods.