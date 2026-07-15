using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NexIM.Primitives;
using NexIM.Primitives.Text;
using NexIM.Primitives.Text.Styles;
using NexIM.Primitives.Xml.Handlers;
using NexIM.Xmpp.Protocol;
using NexIM.Xmpp.Protocol.Grammar;
using NexIM.Xmpp.Protocol.Handlers;
using NexIM.Xmpp.Server.Handlers;

namespace NexIM.Xmpp.Server.Formats;

using static StructuredString;

internal sealed class XHtmlParser<TContext>(LanguageCode defaultLanguage, Dictionary<LanguageCode, StructuredString> bodies) : BaseXHtmlHandler<TContext> where TContext : IPayloadHandlerContext
{
    protected async override ValueTask<IXHtmlContentHandler> OnBody(string? language, InlineStyle? style)
    {
        var lang = language is { } langValue ? new LanguageCode(langValue) : defaultLanguage;
        var content = new ContentParser(lang, bodies) { Context = Context };
        content.Enter(style);
        return content;
    }

    protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);
    public override ValueTask DisposeAsync() => default;

    private sealed class ContentParser(LanguageCode language, Dictionary<LanguageCode, StructuredString> bodies) : BaseXHtmlContentHandler<TContext>, IMixedPayloadHandler, ICustomPayloadHandler
    {
        readonly StringBuilder textBuilder = new();
        Encoder structureEncoder = CreateEncoder();

        int level;

        public void Enter(InlineStyle? style)
        {
            structureEncoder.Encode(new(Command: InstructionCommand.NewBody, Style: GetStyle(style)));
        }

        protected override ValueTask OnUnrecognized(XmlReader payloadReader) => this.Unrecognized(payloadReader);

        private IXHtmlContentHandler EnterNested()
        {
            Interlocked.Increment(ref level);
            return this;
        }

        static readonly Instruction popElement = new(CloseContexts: 1);

        public async override ValueTask DisposeAsync()
        {
            if(Interlocked.Decrement(ref level) >= 0)
            {
                // Nested element
                structureEncoder.Encode(popElement);
                return;
            }

            // Save structure
            bodies[language] = new(textBuilder.ToString(), structureEncoder.ToArray());
        }

        private StructuredStyle? GetStyle(InlineStyle? style)
        {
            if(style is not { Count: > 0 } value)
            {
                return null;
            }

            try
            {
                var result = new StructuredStyle();

                if(value.TryGetValue("color", out var textColor))
                {
                    result.TextColor = ParseColor(textColor);
                }
                if(value.TryGetValue("background-color", out var backColor))
                {
                    result.BackgroundColor = ParseColor(backColor);
                }
                if(value.TryGetValue("font-style", out var fontStyle))
                {
                    result.FontStyle = fontStyle.AsSpan().Trim() switch {
                        "normal" => FontStyle.Normal,
                        "italic" => FontStyle.Italic,
                        "oblique" => FontStyle.Oblique,
                        _ => FontStyle.Inherit
                    };
                }
                if(value.TryGetValue("font-weight", out var fontWeight))
                {
                    if(Int32.TryParse(fontWeight, out var weight))
                    {
                        result.FontWeight = weight switch {
                            < 400 => FontWeight.Light,
                            < 600 => FontWeight.Normal,
                            _ => FontWeight.Bold
                        };
                    }
                    else
                    {
                        result.FontWeight = fontWeight.AsSpan().Trim() switch {
                            "normal" => FontWeight.Normal,
                            "light" or "lighter" => FontWeight.Light,
                            "bold" or "bolder" => FontWeight.Bold,
                            _ => FontWeight.Inherit
                        };
                    }
                }
                if(value.TryGetValue("font-family", out var fontFamily))
                {
                    var span = fontFamily.AsSpan();
                    var range = span.Split(',');
                    while(range.MoveNext())
                    {
                        result.FontFamily = span[range.Current].Trim() switch {
                            "serif" => FontFamily.Serif,
                            "sans-serif" => FontFamily.SansSerif,
                            "cursive" => FontFamily.Cursive,
                            "monospace" => FontFamily.Monospace,
                            "fantasy" => FontFamily.Fantasy,
                            _ => FontFamily.Inherit
                        };
                        if(result.FontFamily != FontFamily.Inherit)
                        {
                            break;
                        }
                    }
                }
                if(value.TryGetValue("font-variant", out var fontVariant))
                {
                    result.FontVariant = fontVariant.AsSpan().Trim() switch {
                        "normal" => FontVariant.Normal,
                        "small-caps" => FontVariant.SmallCaps,
                        _ => FontVariant.Inherit
                    };
                }
                if(value.TryGetValue("text-align", out var textAlignment))
                {
                    result.TextAlignment = textAlignment.AsSpan().Trim() switch {
                        "left" => TextAlignment.Left,
                        "right" => TextAlignment.Right,
                        "center" => TextAlignment.Center,
                        "justify" => TextAlignment.Justify,
                        _ => TextAlignment.Inherit
                    };
                }
                if(value.TryGetValue("text-decoration", out var textDecoration))
                {
                    var span = textDecoration.AsSpan();
                    if(span.Trim() is "none")
                    {
                        result.TextDecoration = TextDecoration.None;
                    }
                    else
                    {
                        var range = span.Split(' ');
                        while(range.MoveNext())
                        {
                            var decoration = span[range.Current].Trim() switch {
                                "underline" => TextDecoration.Underline,
                                "overline" => TextDecoration.Overline,
                                "line-through" => TextDecoration.LineThrough,
                                _ => default
                            };
                            if(decoration != 0)
                            {
                                result.TextDecoration = (result.TextDecoration ?? 0) | decoration;
                            }
                        }
                    }
                }

                // Ignore if empty
                return result == default ? null : result;
            }
            catch
            {
                return null;
            }
        }

        private async ValueTask<IXHtmlContentHandler> OnSimpleCommand(InstructionCommand command, InlineStyle? style)
        {
            structureEncoder.Encode(new(Command: command, Style: GetStyle(style)));
            return EnterNested();
        }

        private void OnStringContent(string str)
        {
            textBuilder.Append(str);
            structureEncoder.Encode(new(AdvanceBy: str.Length, CopyCharacters: true, CloseContexts: 1));
        }

        async ValueTask IMixedPayloadHandler.TextContent(XmlReader textReader)
        {
            if(!textReader.CanReadValueChunk)
            {
                var text = await textReader.GetValueAsync();
                textBuilder.Append(text);
                structureEncoder.Encode(new(AdvanceBy: text.Length, CopyCharacters: true));
                return;
            }

            var pool = ArrayPool<char>.Shared;
            var array = pool.Rent(16);
            try
            {
                int read;
                while((read = await textReader.ReadValueChunkAsync(array, 0, array.Length)) != 0)
                {
                    // Append copied chunk
                    textBuilder.Append(array, 0, read);
                    structureEncoder.Encode(new(AdvanceBy: read, CopyCharacters: true));
                }
            }
            finally
            {
                pool.Return(array);
            }
        }

        ValueTask IMixedPayloadHandler.TextContent(ReadOnlyMemory<char> text)
        {
            try
            {
                textBuilder.Append(text);
                structureEncoder.Encode(new(AdvanceBy: text.Length, CopyCharacters: true));
                return ValueTask.CompletedTask;
            }
            catch(Exception e)
            {
                return ValueTask.FromException(e);
            }
        }

        async ValueTask<IPayloadHandler> ICustomPayloadHandler.CustomContent(XmlReader attributesReader)
        {
            // Ignore attributes (style is ignored too)
            return EnterNested();
        }

        protected async override ValueTask<IXHtmlContentHandler> OnAnchor(ValueUri? href, InlineStyle? style)
        {
            if(href is not { } hrefValue)
            {
                // Ignore semantics
                return await OnSpan(style);
            }

            structureEncoder.Encode(new(Command: InstructionCommand.AnchorHrefFirst, Style: GetStyle(style)));

            OnStringContent(hrefValue.ToString());
            return EnterNested();
        }

        protected async override ValueTask<IXHtmlContentHandler> OnImage(ValueUri? src, Number? width, Number? height, string? alt, InlineStyle? style)
        {
            if(src is not { } srcValue)
            {
                // Ignore
                return NullHandler.Instance;
            }

            structureEncoder.Encode(new(Command: InstructionCommand.ImageSrcFirst, Width: width, Height: height, Style: GetStyle(style)));

            OnStringContent(srcValue.ToString());
            OnStringContent(alt ?? "");
            return NullHandler.Instance;
        }

        static readonly Instruction breakElement = new(Command: InstructionCommand.Break);

        protected async override ValueTask<IXHtmlContentHandler> OnBreak()
        {
            structureEncoder.Encode(breakElement);
            return NullHandler.Instance;
        }

        #region Simple commands
        protected override ValueTask<IXHtmlContentHandler> OnBlockQuote(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.BlockQuote, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnCite(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Cite, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnEmphasis(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Emphasis, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnListItem(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.ListItem, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnOrderedList(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.OrderedList, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnUnorderedList(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.UnorderedList, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnParagraph(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Paragraph, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnSpan(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Span, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnStrong(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Strong, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnCode(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Code, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnBlockCode(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.BlockCode, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnInserted(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Inserted, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnDeleted(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Deleted, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnSubscript(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Subscript, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnSuperscript(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Superscript, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnQuote(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Quote, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnHeading1(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Heading1, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnHeading2(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Heading2, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnHeading3(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Heading3, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnHeading4(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Heading4, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnHeading5(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Heading5, style);
        }

        protected override ValueTask<IXHtmlContentHandler> OnHeading6(InlineStyle? style)
        {
            return OnSimpleCommand(InstructionCommand.Heading6, style);
        }
        #endregion

        static readonly Regex rgbRegex = new(@"^\s*rgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex rgbaRegex = new(@"^\s*rgba\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static Color? ParseColor(string color)
        {
            try
            {
                if(rgbRegex.Match(color) is { Success: true } rgbMatch)
                {
                    return Color.FromArgb(
                        Int32.Parse(rgbMatch.Groups[1].ValueSpan),
                        Int32.Parse(rgbMatch.Groups[2].ValueSpan),
                        Int32.Parse(rgbMatch.Groups[3].ValueSpan)
                    );
                }
                else if(rgbaRegex.Match(color) is { Success: true } rgbaMatch)
                {
                    return Color.FromArgb(
                        Int32.Parse(rgbaMatch.Groups[4].ValueSpan),
                        Int32.Parse(rgbaMatch.Groups[1].ValueSpan),
                        Int32.Parse(rgbaMatch.Groups[2].ValueSpan),
                        Int32.Parse(rgbaMatch.Groups[3].ValueSpan)
                    );
                }
            }
            catch
            {

            }
            return null;
        }
    }
}
