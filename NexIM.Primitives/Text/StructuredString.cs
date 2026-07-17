using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using NexIM.Primitives.Text.Styles;
using NexIM.Primitives.Tools;

namespace NexIM.Primitives.Text;

/// <summary>
/// Stores a string together with structured information.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly partial struct StructuredString
{
    public string Content { get; }

    readonly byte[]? _instructions;
    byte[] instructions => _instructions ?? Array.Empty<byte>();
    public IReadOnlyList<byte> RawInstructions => instructions;

    public IEnumerable<Instruction> Instructions => DecodeInstructions();

    public StructuredString(string content, byte[] rawInstructions)
    {
        Content = content;
        _instructions = rawInstructions;
    }

    public bool Equals(StructuredString other)
    {
        return
            Content == other.Content &&
            instructions.AsSpan().SequenceEqual(other.instructions.AsSpan());
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Content, StringComparer.Ordinal);
        hash.AddBytes(instructions);
        return hash.ToHashCode();
    }

    public static StructuredString Concat(StructuredString a, StructuredString b)
    {
        // Concatenate instructions
        var combinedInstructions = new byte[a.RawInstructions.Count + b.RawInstructions.Count];
        Copy(a.RawInstructions, combinedInstructions, 0);
        Copy(b.RawInstructions, combinedInstructions, a.RawInstructions.Count);

        static void Copy<T>(IReadOnlyList<T> source, T[] destination, int index)
        {
            if(source is IList<T> list)
            {
                list.CopyTo(destination, index);
                return;
            }
            int count = source.Count;
            for(int i = 0; i < count; i++)
            {
                destination[index++] = source[i];
            }
        }

        foreach(var instruction in b.Instructions)
        {
            if(instruction is { Command: InstructionCommand.NewBody })
            {
                // Second string is a new body, separate with NUL
                return new(a.Content + "\0" + b.Content, combinedInstructions);
            }
            break;
        }
        // Concatenate normally
        return new(a.Content + b.Content, combinedInstructions);
    }

    public static StructuredString operator +(StructuredString a, StructuredString b) => Concat(a, b);

    /// <summary>
    /// Stores information about a structured formatting or styling instruction for text.
    /// If multiple properties are set, they are evaluated according the order of
    /// parameters of the constructor.
    /// </summary>
    /// <param name="Command">The type of command to evaluate.</param>
    /// <param name="Style">The style attached to the command.</param>
    /// <param name="Width">The width attached to the command.</param>
    /// <param name="Height">The height attached to the command.</param>
    /// <param name="AdvanceBy">The number of UTF-16 code units to advance in <see cref="StructuredString.Content"/>.</param>
    /// <param name="CopyCharacters">Whether the read characters should be copied to output.</param>
    /// <param name="CloseContexts">The number of contexts this instruction is closing.</param>
    [StructLayout(LayoutKind.Auto)]
    public record struct Instruction(
        InstructionCommand Command = 0,
        StructuredStyle? Style = null,
        Number? Width = null,
        Number? Height = null,
        int Level = 0,
        LinkNamespace Namespace = LinkNamespace.Default,
        bool Reversed = false,
        int AdvanceBy = 0,
        bool CopyCharacters = false,
        int CloseContexts = 0
    );

    public enum InstructionCommand : byte
    {
        None = 0,
        NewBody = 1,
        MePlaceholder = 2,

        Heading = 6,
        Division,
        Paragraph,
        Span,
        Emphasis,
        Strong,
        Inserted,
        Deleted,
        Subscript,
        Superscript,
        Cite,
        Quote,
        BlockQuote,
        Code,
        BlockCode,
        OrderedList,
        UnorderedList,
        ListItem = 23,

        Image = 26,
        Link = 27,

        /// <remarks>
        /// Can reuse the code for pop because breaks are not styled.
        /// </remarks>
        Break = 31
    }

    public enum LinkNamespace : byte
    {
        Default
    }
}

[StructLayout(LayoutKind.Auto)]
public record struct StructuredStyle(
    Color? TextColor = null,
    Color? BackgroundColor = null,
    FontStyle FontStyle = 0,
    FontWeight FontWeight = 0,
    FontFamily FontFamily = 0,
    FontVariant FontVariant = 0,
    TextAlignment TextAlignment = 0,
    TextDecoration? TextDecoration = null
);

