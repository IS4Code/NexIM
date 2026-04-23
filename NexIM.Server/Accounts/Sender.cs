using System.Runtime.InteropServices;

namespace NexIM.Server.Accounts;

[StructLayout(LayoutKind.Auto)]
public readonly record struct SenderPresentation(
    string? Nickname
);
