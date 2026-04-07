using System.Runtime.InteropServices;

namespace Unicord.Server.Accounts;

[StructLayout(LayoutKind.Auto)]
public readonly record struct SenderPresentation(
    string? Nickname
);
