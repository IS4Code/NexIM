using System;

namespace NexIM.Irc.Protocol;

[Flags]
public enum Capabilities
{
    None = 0,
    Sasl = 1,
    MessageTags = 2
}
