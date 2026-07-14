using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NexIM.Primitives.Text.Styles;

namespace NexIM.Primitives.Text;

partial struct StructuredString
{
    private IEnumerable<Instruction> DecodeInstructions()
    {
        var instructions = this.instructions;
        int index = 0, length = instructions.Length;
        while(index < length)
        {
            // Read current instruction
            var instruction = instructions[index++];

            if(((InstructionFlags)instruction & InstructionFlags.IsNotCopy) == 0)
            {
                // Copy command, take lower 7 bits
                yield return new(CopyCharacters: true, AdvanceBy: ReadAdvance(7, 0));
                continue;
            }

            if(((InstructionFlags)instruction & InstructionFlags.IsNotSkip) == InstructionFlags.IsNotCopy)
            {
                // Skip command, take lower 6 bits
                yield return new(CopyCharacters: false, AdvanceBy: ReadAdvance(6, (byte)InstructionFlags.IsNotCopy));
                continue;
            }

            int ReadAdvance(int bitsCount, byte flagCheck)
            {
                byte valueMask = (byte)((1 << bitsCount) - 1);
                byte flagMask = (byte)~valueMask;

                int shift = 0;
                int maxSafeShift = sizeof(int) * 8 - bitsCount;

                int value = instruction & valueMask;

                while(index < length && instructions[index] is byte nextCurrent && (nextCurrent & flagMask) == flagCheck)
                {
                    // Combine with further bytes
                    index++;
                    shift += bitsCount;

                    nextCurrent &= valueMask;
                    if(shift < maxSafeShift)
                    {
                        // Shift cannot cause signed overflow with any of the bits
                        value |= nextCurrent << shift;
                    }
                    else if(shift < sizeof(int) * 8 - 1)
                    {
                        // Introduce overflow check
                        value |= checked(nextCurrent * (1 << shift));
                    }
                    else
                    {
                        // Shifting past maximum signed value
                        throw new OverflowException();
                    }
                }
                return value;
            }

            if(instruction == popContext)
            {
                // Pop command
                int count = 1;
                while(index < length && instructions[index] == popContext)
                {
                    count++;
                    index++;
                }
                yield return new(CloseContexts: count);
                continue;
            }

            // Normal command
            bool hasStyle = ((InstructionFlags)instruction & InstructionFlags.HasStyle) == InstructionFlags.HasStyle;

            StructuredStyle? style;
            if(hasStyle)
            {
                // Followed by a style byte
                var styleFlags = (StyleFlags)instructions[index++];

                bool hasAlpha = (styleFlags & StyleFlags.ColorsHaveAlpha) != 0;

                Color ReadColor() => hasAlpha ? Color.FromArgb(
                    instructions[index++],
                    instructions[index++],
                    instructions[index++],
                    instructions[index++]
                ) : Color.FromArgb(
                    instructions[index++],
                    instructions[index++],
                    instructions[index++]
                );

                StructuredStyle styleData = default;

                if((styleFlags & StyleFlags.TextColor) != 0)
                {
                    // Followed by (A)RGB
                    styleData.TextColor = ReadColor();
                }

                if((styleFlags & StyleFlags.BackgroundColor) != 0)
                {
                    // Followed by (A)RGB
                    styleData.BackgroundColor = ReadColor();
                }

                if((styleFlags & StyleFlags.FontDrawing) != 0)
                {
                    // Followed by font drawing bitflags
                    var fontDrawing = (FontDrawing)instructions[index++];
                    styleData.FontStyle = (FontStyle)(fontDrawing & FontDrawing.FontStyleMask);
                    styleData.FontWeight = (FontWeight)((byte)(fontDrawing & FontDrawing.FontWeightMask) >> 2);
                    styleData.FontFamily = (FontFamily)((byte)(fontDrawing & FontDrawing.FontFamilyMask) >> 5);
                }

                if((styleFlags & StyleFlags.TextDrawing) != 0)
                {
                    // Followed by text drawing bitflags
                    var textDrawing = (TextDrawing)instructions[index++];

                    // 0 to 8, 9 to 17, 18 to 26 (27 to 31 unused)
                    int combined = (int)(textDrawing & TextDrawing.FontVariantTextDecorationCombinedMask);
                    int fontVariant = Math.DivRem(combined, (int)inheritDecoration + 1, out int textDecoration);

                    styleData.FontVariant = (FontVariant)fontVariant;

                    if(textDecoration != (int)inheritDecoration)
                    {
                        // Not `inherit`
                        styleData.TextDecoration = (TextDecoration)textDecoration;
                    }

                    styleData.TextAlignment = (TextAlignment)((byte)(textDrawing & TextDrawing.TextAlignMask) >> 5);
                }

                style = styleData;
            }
            else
            {
                style = null;
            }

            var command = (InstructionCommand)(instruction & (byte)(~InstructionFlags.HasStyle));
            Number? width = null, height = null;

            switch(command)
            {
                case InstructionCommand.ImageSrcFirst:
                case InstructionCommand.ImageAltFirst:
                    // Followed by attribute selectors
                    var imageAttributes = (ImageAttributes)instructions[index++];
                    var widthUnit = (Number.Unit)(imageAttributes & ImageAttributes.WidthMask);
                    var heightUnit = (Number.Unit)((byte)(imageAttributes & ImageAttributes.HeightMask) >> 2);
                    // Zero indicates not present, not exact zero
                    if(widthUnit != Number.Unit.Zero)
                    {
                        width = new(widthUnit, ReadFloat());
                    }
                    if(heightUnit != Number.Unit.Zero)
                    {
                        height = new(heightUnit, ReadFloat());
                    }

                    float ReadFloat()
                    {
                        int start = index;
                        index += 4;
                        return BitConverter.IsLittleEndian
                            ? BitConverter.ToSingle(instructions, start)
                            : Int32ToSingle(BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(instructions, start)));

                        static float Int32ToSingle(int value) => Unsafe.As<int, float>(ref value);
                    }
                    break;
            }

            yield return new(Style: style, Command: command, Width: width, Height: height);
        }
    }

    public static Encoder CreateEncoder()
    {
        return new(new List<byte>());
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Encoder(List<byte> buffer)
    {
        int advancingCount;
        bool advancingCopy;

        private void EncodeAdvance()
        {
            int shift;
            byte flag;
            if(advancingCopy)
            {
                shift = 7;
                flag = 0;
            }
            else
            {
                shift = 6;
                flag = (byte)InstructionFlags.IsNotCopy;
            }
            int mask = (1 << shift) - 1;
            while(advancingCount != 0)
            {
                // Encode little-endian
                buffer.Add((byte)((advancingCount & mask) | flag));
                advancingCount >>= shift;
            }
        }

        public byte[] ToArray()
        {
            if(advancingCount != 0)
            {
                EncodeAdvance();
            }
            return buffer.ToArray();
        }

        public void Encode(in Instruction instruction)
        {
            if(instruction.AdvanceBy < 0 || instruction.CloseContexts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instruction));
            }

            if(instruction.Width is { Kind: 0 } || instruction.Height is { Kind: 0 })
            {
                throw new ArgumentException("Dimensions must not have Kind set to Zero.", nameof(instruction));
            }

            var command = instruction.Command;

            if(command != 0)
            {
                // Command is given
                switch(command)
                {
                    case InstructionCommand.Break:
                        if(instruction.Style != null)
                        {
                            throw new ArgumentException("The Break command must not have an attached style.", nameof(instruction));
                        }
                        goto default;
                    case InstructionCommand.ImageAltFirst:
                    case InstructionCommand.ImageSrcFirst:
                        break;
                    case > InstructionCommand.Break:
                        throw new ArgumentOutOfRangeException(nameof(instruction));
                    default:
                        if(instruction.Width != null || instruction.Height != null)
                        {
                            throw new ArgumentException("Dimensions must not be given for a non-image command.", nameof(instruction));
                        }
                        break;
                }

                if(advancingCount != 0)
                {
                    // Finish advance before writing the command
                    EncodeAdvance();
                }

                // Encode command
                buffer.Add((byte)((int)command | (int)(instruction.Style != null ? InstructionFlags.HasStyle : InstructionFlags.IsNotSkip)));
                
                if(instruction.Style is { } style)
                {
                    // Style is present

                    StyleFlags flags = 0;
                    bool hasFontDrawing = ((int)style.FontStyle | (int)style.FontWeight | (int)style.FontFamily) != 0;
                    bool hasTextDrawing = ((int)style.FontVariant | (int)style.TextAlignment) != 0 || style.TextDecoration != null;
                    bool hasAlpha = style.TextColor is { A: not 255 } || style.BackgroundColor is { A: not 255 };

                    void EncodeColor(List<byte> buffer, Color color)
                    {
                        if(hasAlpha)
                        {
                            buffer.Add(color.A);
                        }
                        buffer.Add(color.R);
                        buffer.Add(color.G);
                        buffer.Add(color.B);
                    }

                    // Prepare all flags
                    if(style.TextColor != null)
                    {
                        flags |= StyleFlags.TextColor;
                    }
                    if(style.BackgroundColor != null)
                    {
                        flags |= StyleFlags.BackgroundColor;
                    }
                    if(hasFontDrawing)
                    {
                        flags |= StyleFlags.FontDrawing;
                    }
                    if(hasTextDrawing)
                    {
                        flags |= StyleFlags.TextDrawing;
                    }
                    if(hasAlpha)
                    {
                        flags |= StyleFlags.ColorsHaveAlpha;
                    }

                    buffer.Add((byte)flags);

                    if(style.TextColor is { } textColor)
                    {
                        EncodeColor(buffer, textColor);
                    }
                    if(style.BackgroundColor is { } backColor)
                    {
                        EncodeColor(buffer, backColor);
                    }
                    if(hasFontDrawing)
                    {
                        var fontDrawing = (FontDrawing)(
                            (int)style.FontStyle |
                            ((int)style.FontWeight << 2) |
                            ((int)style.FontFamily << 5)
                        );
                        buffer.Add((byte)fontDrawing);
                    }
                    if(hasTextDrawing)
                    {
                        var textDrawing = (TextDrawing)(
                            ((int)style.FontVariant * (int)(inheritDecoration + 1) + (int)(style.TextDecoration ?? inheritDecoration)) |
                            ((int)style.TextAlignment << 5)
                        );
                        buffer.Add((byte)textDrawing);
                    }
                }

                switch(command)
                {
                    case InstructionCommand.ImageAltFirst:
                    case InstructionCommand.ImageSrcFirst:
                        // Followed by attributes byte
                        var attributes = (ImageAttributes)(
                            (int)(instruction.Width?.Kind ?? 0) |
                            ((int)(instruction.Height?.Kind ?? 0) << 2)
                        );
                        buffer.Add((byte)attributes);

                        if(instruction.Width is { } width)
                        {
                            WriteFloat(buffer, width.Value);
                        }
                        if(instruction.Height is { } height)
                        {
                            WriteFloat(buffer, height.Value);
                        }

                        void WriteFloat(List<byte> buffer, float value)
                        {
                            int bits = Unsafe.As<float, int>(ref value);
                            buffer.Add((byte)(bits & 0xFF));
                            bits >>= 8;
                            buffer.Add((byte)(bits & 0xFF));
                            bits >>= 8;
                            buffer.Add((byte)(bits & 0xFF));
                            bits >>= 8;
                            buffer.Add((byte)(bits & 0xFF));
                        }

                        break;
                }

                // Set up advancing if present (previous state was already written)
                advancingCount = instruction.AdvanceBy;
                advancingCopy = instruction.CopyCharacters;
            }
            else if(instruction.Style != null)
            {
                throw new ArgumentException("Instruction has an attached style but no command.", nameof(instruction));
            }
            else if(instruction.AdvanceBy != 0)
            {
                if(advancingCount != 0 && advancingCopy != instruction.CopyCharacters)
                {
                    // We should advance but a different advance is pending
                    EncodeAdvance();
                }
                advancingCount += instruction.AdvanceBy;
                advancingCopy = instruction.CopyCharacters;
            }

            int popCount = instruction.CloseContexts;
            if(popCount != 0)
            {
                if(advancingCount != 0)
                {
                    // Finish advance
                    EncodeAdvance();
                }

                while(popCount != 0)
                {
                    buffer.Add(popContext);
                    popCount--;
                }
            }
        }
    }

    /// <summary>
    /// Identifies the type of an instruction.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>If <see cref="IsNotCopy"/> is not set, the byte is a copy instruction and lower 7 bits form the bits of the count to copy.</item>
    /// <item>Otherwise, if <see cref="IsNotSkip"/> is not set, the byte is a skip instruction and lower 6 bits form the bits of the count to skip.</item>
    /// <item>Otherwise, the byte starts a command. If <see cref="HasStyle"/> is set, the byte is immediately followed by style information.</item>
    /// </list>
    /// </remarks>
    [Flags]
    enum InstructionFlags : byte
    {
        IsNotCopy = 0x80,
        IsNotSkip = IsNotCopy | 0x40,
        HasStyle = IsNotSkip | 0x20
    }

    const byte popContext = 0xFF;

    /// <summary>
    /// The flags enabling styles applied to a new context.
    /// </summary>
    [Flags]
    enum StyleFlags : byte
    {
        TextColor = 1,
        BackgroundColor = 2,
        FontDrawing = 4,
        TextDrawing = 8,

        ColorsHaveAlpha = 128
    }

    [Flags]
    enum FontDrawing : byte
    {
        FontStyleMask = 3,
        FontWeightMask = 7 << 2,
        FontFamilyMask = 7 << 5
    }

    [Flags]
    enum TextDrawing : byte
    {
        FontVariantTextDecorationCombinedMask = 31,
        TextAlignMask = 7 << 5
    }

    const TextDecoration inheritDecoration = 1 + (TextDecoration.Underline | TextDecoration.Overline | TextDecoration.LineThrough);

    [Flags]
    enum ImageAttributes : byte
    {
        None = 0,
        WidthMask = 3,
        HeightMask = 3 << 2
    }
}
