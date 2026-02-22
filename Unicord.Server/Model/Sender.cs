namespace Unicord.Server.Model;

public readonly record struct Sender(
    AccountName Account, 
    string? Identifier,
    string? Nickname
);
