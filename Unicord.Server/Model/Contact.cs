namespace Unicord.Server.Model;

public record Contact(
    AccountName Account,
    string? Name = null,
    string? Group = null,
    bool SubscribedTo = false,
    bool SubscribedFrom = false
);