using System;

namespace NexIM.Primitives.Text.Styles;

public enum FontStyle : byte
{
    Inherit = 0,
    Normal = 1,
    Italic = 2,
    Oblique = 3
}

public enum FontWeight : byte
{
    Inherit = 0,
    Normal = 1,
    Bold = 2,
    Light = 3
}

public enum FontFamily : byte
{
    Inherit = 0,
    Serif = 1,
    SansSerif = 2,
    Cursive = 3,
    Fantasy = 4,
    Monospace = 5
}

public enum FontVariant : byte
{
    Inherit = 0,
    Normal = 1,
    SmallCaps = 2
}

public enum TextAlignment : byte
{
    Inherit = 0,
    Left = 1,
    Right = 2,
    Center = 3,
    Justify = 4
}

[Flags]
public enum TextDecoration : byte
{
    None = 0,
    Underline = 1,
    Overline = 2,
    LineThrough = 4
}
