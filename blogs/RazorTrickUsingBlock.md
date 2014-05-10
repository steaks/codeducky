Using blocks are a very useful tool when rendering HTML markup with razor.  You can use them to consolidate duplicated markup into simple, readable, and reusable C# methods.

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

First, create a common base class class that makes writing blocks easy.
<pre>
public abstract class Block : IDisposable
{
    protected HtmlHelper HtmlHelper { get; private set;}

    public Block(HtmlHelper htmlHelper)
    {
        this.HtmlHelper = htmlHelper;
    }

    public abstract void BeginBlock();

    protected abstract void EndBlock();

    public void Dispose()
    {
        this.EndBlock();
    }
}
</pre>
Second, create a specific block for your footnote sections.
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
Third, add an extension method on HtmlHelper for ease of use.
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

That's all.  Now you've just centralized all html markup for creating footnote sections.  (See <a href="http://www.codeducky.org/razor-trick-pass-razor-code-into-methods/">Razor trick: pass razor code into methods</a> to learn how to implement common components with C# methods.)