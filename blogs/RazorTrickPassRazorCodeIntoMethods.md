One lesser known features about Razor is that you can pass razor code into C# methods.  This functionality is a useful tool when consolidating code.

It's useful when you want to store html content as a variable.
<pre>
@{
    var barSpan = Html.Content(@<span class="foo">@ViewBag.Bar</span>);
}
<span>Bar 1: @barSpan</span>
<span>Bar 2: @barSpan</span>
</pre>
<br/>
<pre>
public static IHtmlString Content(
    this HtmlHelper self, 
    Func<object, HelperResult> razorContent)
{
    return razorContent(null);
}
</pre>

It's also useful when you want to implement common components.  (See <a href="http://www.codeducky.org/razor-trick-using-block/">Razor trick: using block</a> to learn how to implement common components with using blocks.)

For example, you can leverage C# methods to centralize code for comment blocks that require special markup and css styling.

Without C# methods:
<pre>
<div class="comment-block">
    <div class="comment-block-header"> 
        <a href="http://www.codeducky.org">Link header</a>
    </div>
    <textarea>Write comment here!</textarea>
    <div class="comment-block-footer"> 
        <a href="http://www.codeducky.org">Link footer</a>
    </div>
</div>

<div class="comment-block">
    <div class="comment-block-header"> 
        <span>Normal header</span>
    </div>
    <textarea>Write comment here!</textarea>
    <div class="comment-block-footer"> 
        <span>Normal footer</span>
    </div>
</div>
</pre>

Centralized code with C# methods:
<pre>
@Html.CommentBlock(
    header: @<a href="http://www.codeducky.org">Link header</a>,
    footer: @<a href="http://www.codeducky.org">Link footer</a>
)

@Html.CommentBlock(
    header: @<span>Normal header</span>,
    footer: @<span>Normal footer</span>
)
</pre>
<br/>
<pre>
public static IHtmlString CommentBlock(
    this HtmlHelper self, 
    Func<object, HelperResult> header, 
    Func<object, HelperResult> footer)
{
    var commentBlock = new TagBuilder("div");
    commentBlock.AddCssClass("comment-block");

    var headerBlock = new TagBuilder("div");
    headerBlock.AddCssClass("comment-block-header");
    headerBlock.InnerHtml = header(null).ToHtmlString();

    var footerBlock = new TagBuilder("div");
    footerBlock.AddCssClass("comment-block-footer");
    footerBlock.InnerHtml = footer(null).ToHtmlString();

    var textArea = new TagBuilder("textarea");
    textArea.SetInnerText("Write comment here!");

    commentBlock.InnerHtml = 
        headerBlock.ToString() + 
        textArea.ToString() + 
        footerBlock.ToString();
    return new HtmlString(commentBlock.ToString());
}
</pre>