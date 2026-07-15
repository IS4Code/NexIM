using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xmpp.Protocol.Grammar;

namespace NexIM.Xmpp.Protocol;

[ComplexType, Namespace(XHtml)]
public interface IXHtmlHandler : IPayloadHandler
{
    [Name("body")]
    ValueTask<IXHtmlContentHandler> Body([Name("lang", "http://www.w3.org/XML/1998/namespace")] string? language, [Name("style")] InlineStyle? style);
}

[ComplexType, Namespace(XHtml)]
public interface IXHtmlContentHandler : IPayloadHandler
{
    [Name("a")]
    ValueTask<IXHtmlContentHandler> Anchor([Name("href")] ValueUri? href, [Name("style")] InlineStyle? style);

    [Name("blockquote")]
    ValueTask<IXHtmlContentHandler> BlockQuote([Name("style")] InlineStyle? style);

    [Name("br")]
    ValueTask<IXHtmlContentHandler> Break();

    [Name("cite")]
    ValueTask<IXHtmlContentHandler> Cite([Name("style")] InlineStyle? style);

    [Name("em")]
    ValueTask<IXHtmlContentHandler> Emphasis([Name("style")] InlineStyle? style);

    [Name("img")]
    ValueTask<IXHtmlContentHandler> Image([Name("src")] ValueUri? src, [Name("width")] Number? width, [Name("height")] Number? height, [Name("alt")] string? alt, [Name("style")] InlineStyle? style);

    [Name("li")]
    ValueTask<IXHtmlContentHandler> ListItem([Name("style")] InlineStyle? style);

    [Name("ol")]
    ValueTask<IXHtmlContentHandler> OrderedList([Name("style")] InlineStyle? style);

    [Name("ul")]
    ValueTask<IXHtmlContentHandler> UnorderedList([Name("style")] InlineStyle? style);

    [Name("p")]
    ValueTask<IXHtmlContentHandler> Paragraph([Name("style")] InlineStyle? style);

    [Name("span")]
    ValueTask<IXHtmlContentHandler> Span([Name("style")] InlineStyle? style);

    [Name("strong")]
    ValueTask<IXHtmlContentHandler> Strong([Name("style")] InlineStyle? style);

    // Not recommended but sensible

    [Name("code")]
    ValueTask<IXHtmlContentHandler> Code([Name("style")] InlineStyle? style);

    [Name("pre")]
    ValueTask<IXHtmlContentHandler> BlockCode([Name("style")] InlineStyle? style);

    [Name("ins")]
    ValueTask<IXHtmlContentHandler> Inserted([Name("style")] InlineStyle? style);

    [Name("del")]
    ValueTask<IXHtmlContentHandler> Deleted([Name("style")] InlineStyle? style);

    [Name("sub")]
    ValueTask<IXHtmlContentHandler> Subscript([Name("style")] InlineStyle? style);

    [Name("sup")]
    ValueTask<IXHtmlContentHandler> Superscript([Name("style")] InlineStyle? style);

    [Name("q")]
    ValueTask<IXHtmlContentHandler> Quote([Name("style")] InlineStyle? style);

    [Name("h1")]
    ValueTask<IXHtmlContentHandler> Heading1([Name("style")] InlineStyle? style);

    [Name("h2")]
    ValueTask<IXHtmlContentHandler> Heading2([Name("style")] InlineStyle? style);

    [Name("h3")]
    ValueTask<IXHtmlContentHandler> Heading3([Name("style")] InlineStyle? style);

    [Name("h4")]
    ValueTask<IXHtmlContentHandler> Heading4([Name("style")] InlineStyle? style);

    [Name("h5")]
    ValueTask<IXHtmlContentHandler> Heading5([Name("style")] InlineStyle? style);

    [Name("h6")]
    ValueTask<IXHtmlContentHandler> Heading6([Name("style")] InlineStyle? style);
}
