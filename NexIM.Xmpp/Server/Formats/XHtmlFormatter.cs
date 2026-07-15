using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using NexIM.Primitives;
using NexIM.Primitives.Text;
using NexIM.Primitives.Text.Styles;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Grammar;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Tools;

namespace NexIM.Xmpp.Server.Formats;

using static StructuredString;

internal static class XHtmlFormatter
{
    public static async ValueTask WriteTo(this StructuredString str, IXHtmlHandler handler, LanguageCode? language)
    {
        using var reader = str.Instructions.GetEnumerator();

        int position = 0;
        var text = str.Content;

        bool exitedBody = false;

        while(exitedBody || reader.MoveNext())
        {
            exitedBody = false;

            var current = reader.Current;
            if(current is not { Command: InstructionCommand.NewBody })
            {
                // Skip commands outside body
                position += current.AdvanceBy;
                continue;
            }

            // Inside body
            await using var handlers = new PayloadHandlers<IXHtmlContentHandler>();

            IXHtmlContentHandler top;
            handlers.Push(top = await handler.Body(language?.Value, GetStyle(current.Style)));
            await Advance();

            while(reader.MoveNext())
            {
                current = reader.Current;

                if(current is { Command: InstructionCommand.NewBody })
                {
                    // Exit current body
                    exitedBody = true;
                    while(position < text.Length && text[position] == '\0')
                    {
                        // Skip NUL separators
                        position++;
                    }
                    break;
                }

                var style = GetStyle(current.Style);

                // Evaluate command
                var command = current.Command;
                current.Command = InstructionCommand.None;
                switch(command)
                {
                    case InstructionCommand.MePlaceholder:
                        // Encode and ignore content
                        if(top is IMixedPayloadHandler mixedHandler)
                        {
                            await mixedHandler.TextContent("/me ".AsMemory());
                        }

                        handlers.Push(top = NullHandler.Instance);
                        break;

                    case InstructionCommand.ImageSrcFirst:
                    {
                        var src = ReadString();
                        var alt = ReadString();
                        await using(var img = await top.Image(src != null ? new(src) : null, current.Width, current.Height, alt, style))
                        {
                            // Empty
                        }
                    }
                    break;

                    case InstructionCommand.ImageAltFirst:
                    {
                        var alt = ReadString();
                        var src = ReadString();
                        await using(var img = await top.Image(src != null ? new(src) : null, current.Width, current.Height, alt, style))
                        {
                            // Empty
                        }
                    }
                    break;

                    case InstructionCommand.AnchorHrefFirst:
                    {
                        var href = ReadString();
                        handlers.Push(top = await top.Anchor(href != null ? new(href) : null, style));
                    }
                    break;

                    case InstructionCommand.AnchorContentFirst:
                    {
                        // Content must be captured first
                        var capture = new CapturingHandler<IXHtmlContentHandler>();

                        handlers.Push(top = new DelegatingXHtmlContentHandler<IXHtmlContentHandler, ActionDisposable, EmptyPayloadHandlerContext>(capture, new(async () => {
                            // Content popped
                            var href = ReadString();

                            // Replay content
                            await using var anchor = await handlers.Peek().Anchor(href != null ? new(href) : null, style);
                            await capture.Replay(anchor);
                        })));
                    }
                    break;

                    case InstructionCommand.Break:
                        await using(var br = await top.Break())
                        {
                            // Empty
                        }
                        break;

                    #region Normal elements
                    case InstructionCommand.Heading1:
                        handlers.Push(top = await top.Heading1(style));
                        break;

                    case InstructionCommand.Heading2:
                        handlers.Push(top = await top.Heading2(style));
                        break;

                    case InstructionCommand.Heading3:
                        handlers.Push(top = await top.Heading3(style));
                        break;

                    case InstructionCommand.Heading4:
                        handlers.Push(top = await top.Heading4(style));
                        break;

                    case InstructionCommand.Heading5:
                        handlers.Push(top = await top.Heading5(style));
                        break;

                    case InstructionCommand.Heading6:
                        handlers.Push(top = await top.Heading6(style));
                        break;

                    case InstructionCommand.Paragraph:
                        handlers.Push(top = await top.Paragraph(style));
                        break;

                    case InstructionCommand.Span:
                        handlers.Push(top = await top.Span(style));
                        break;

                    case InstructionCommand.Emphasis:
                        handlers.Push(top = await top.Emphasis(style));
                        break;

                    case InstructionCommand.Strong:
                        handlers.Push(top = await top.Strong(style));
                        break;

                    case InstructionCommand.Inserted:
                        handlers.Push(top = await top.Inserted(style));
                        break;

                    case InstructionCommand.Deleted:
                        handlers.Push(top = await top.Deleted(style));
                        break;

                    case InstructionCommand.Subscript:
                        handlers.Push(top = await top.Subscript(style));
                        break;

                    case InstructionCommand.Superscript:
                        handlers.Push(top = await top.Superscript(style));
                        break;

                    case InstructionCommand.Cite:
                        handlers.Push(top = await top.Cite(style));
                        break;

                    case InstructionCommand.Quote:
                        handlers.Push(top = await top.Quote(style));
                        break;

                    case InstructionCommand.BlockQuote:
                        handlers.Push(top = await top.BlockQuote(style));
                        break;

                    case InstructionCommand.Code:
                        handlers.Push(top = await top.Code(style));
                        break;

                    case InstructionCommand.BlockCode:
                        handlers.Push(top = await top.BlockCode(style));
                        break;

                    case InstructionCommand.OrderedList:
                        handlers.Push(top = await top.OrderedList(style));
                        break;

                    case InstructionCommand.UnorderedList:
                        handlers.Push(top = await top.UnorderedList(style));
                        break;

                    case InstructionCommand.ListItem:
                        handlers.Push(top = await top.ListItem(style));
                        break;
                    #endregion
                }

                await Advance();
            }

            async ValueTask Advance()
            {
                var advance = current.AdvanceBy;
                var copy = current.CopyCharacters;

                if(advance != 0)
                {
                    if(copy && top is IMixedPayloadHandler mixedHandler)
                    {
                        await mixedHandler.TextContent(text.AsMemory(position, advance));
                    }
                    position += advance;
                    current.AdvanceBy = 0;
                }

                while(current.CloseContexts > 0 && handlers.Count > 1)
                {
                    current.CloseContexts--;
                    await handlers.PopDisposeAsync();
                    top = handlers.Peek();
                }
            }

            string? ReadString()
            {
                StringBuilder? sb = null;
                int level = 0;

                while(true)
                {
                    switch(current.Command)
                    {
                        case InstructionCommand.NewBody:
                            // Exit without pop (bad format)
                            return sb?.ToString();

                        case InstructionCommand.None:
                        case InstructionCommand.Break:
                            // No nested level
                            break;

                        case InstructionCommand.ImageSrcFirst:
                        case InstructionCommand.ImageAltFirst:
                        case InstructionCommand.AnchorHrefFirst:
                        case InstructionCommand.AnchorContentFirst:
                            // Two nested levels
                            level += 2;
                            break;

                        default:
                            // One nested level
                            level++;
                            break;
                    }

                    var advance = current.AdvanceBy;
                    var copy = current.CopyCharacters;
                    if(advance != 0)
                    {
                        if(copy)
                        {
                            (sb ??= new()).Append(text, position, advance);
                        }
                        position += advance;
                        current.AdvanceBy = 0;
                    }

                    // Close any nested contexts
                    int close = Math.Min(current.CloseContexts, level);
                    current.CloseContexts -= close;
                    level -= close;

                    if(current.CloseContexts > 0)
                    {
                        // Result
                        current.CloseContexts--;
                        return sb?.ToString();
                    }

                    if(!reader.MoveNext())
                    {
                        // Ended without pop (bad format)
                        current = default;
                        return sb?.ToString();
                    }
                    current = reader.Current;
                }
            }
        }
    }

    private static InlineStyle? GetStyle(StructuredStyle? style)
    {
        if(style is not { } value || value == default)
        {
            return null;
        }

        return new(Inner());
        IEnumerable<KeyValuePair<string, string>> Inner()
        {
            if(value.TextColor is { } textColor)
            {
                yield return new("color", ColorToString(textColor));
            }

            if(value.BackgroundColor is { } backColor)
            {
                yield return new("background-color", ColorToString(backColor));
            }

            if(value.FontStyle switch {
                FontStyle.Normal => "normal",
                FontStyle.Italic => "italic",
                FontStyle.Oblique => "oblique",
                _ => null
            } is { } fontStyle)
            {
                yield return new("font-style", fontStyle);
            }

            if(value.FontWeight switch {
                FontWeight.Normal => "400",
                FontWeight.Bold => "700",
                FontWeight.Light => "100",
                _ => null
            } is { } fontWeight)
            {
                yield return new("font-weight", fontWeight);
            }

            if(value.FontFamily switch {
                FontFamily.Serif => "serif",
                FontFamily.SansSerif => "sans-serif",
                FontFamily.Cursive => "cursive",
                FontFamily.Monospace => "monospace",
                FontFamily.Fantasy => "fantasy",
                _ => null
            } is { } fontFamily)
            {
                yield return new("font-family", fontFamily);
            }

            if(value.FontVariant switch {
                FontVariant.Normal => "normal",
                FontVariant.SmallCaps => "small-caps",
                _ => null
            } is { } fontVariant)
            {
                yield return new("font-variant", fontVariant);
            }

            if(value.TextAlignment switch {
                TextAlignment.Left => "left",
                TextAlignment.Right => "right",
                TextAlignment.Center => "center",
                TextAlignment.Justify => "justify",
                _ => null
            } is { } textAlignment)
            {
                yield return new("text-alignment", textAlignment);
            }

            if(value.TextDecoration switch {
                TextDecoration.None => "none",
                TextDecoration.Underline => "underline",
                TextDecoration.Overline => "overline",
                TextDecoration.LineThrough => "line-through",
                TextDecoration.Underline | TextDecoration.Overline => "underline overline",
                TextDecoration.Underline | TextDecoration.LineThrough => "underline line-through",
                TextDecoration.Overline | TextDecoration.LineThrough => "overline line-through",
                TextDecoration.Underline | TextDecoration.Overline | TextDecoration.LineThrough => "underline overline line-through",
                _ => null
            } is { } textDecoration)
            {
                yield return new("text-decoration", textDecoration);
            }

            static string ColorToString(Color color)
            {
                if(color.A == 255)
                {
                    return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                }
                return $"rgba({color.R},{color.G},{color.B},{color.A})";
            }
        }
    }

    sealed class ActionDisposable(Func<ValueTask> action) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => action();
    }
}
