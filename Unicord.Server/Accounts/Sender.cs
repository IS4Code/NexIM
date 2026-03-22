using System.Runtime.InteropServices;

namespace Unicord.Server.Accounts;

[StructLayout(LayoutKind.Auto)]
public readonly record struct Sender(
    AccountName Account, 
    string? Identifier = null,
    SenderPresentation Presentation = default
);

[StructLayout(LayoutKind.Auto)]
public readonly record struct SenderPresentation(
    string? Nickname
);
