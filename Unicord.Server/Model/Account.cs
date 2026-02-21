using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Unicord.Server.Model;

public class Account
{
    public AccountName Name { get; }
    internal byte[] PasswordHash { get; }
    readonly ConcurrentDictionary<AccountName, Contact> contacts = new();

    public ICollection<Contact> Contacts => contacts.Values;

    public Account(AccountName name, byte[] passwordHash)
    {
        Name = name;
        PasswordHash = passwordHash;
    }

    public Contact? SetContact(AccountName target, string? name, string? group)
    {
        return contacts[target] = new Contact(target, name, group);
    }

    public Contact? RemoveContact(AccountName target)
    {
        return contacts.TryRemove(target, out var contact) ? contact : null;
    }
}
