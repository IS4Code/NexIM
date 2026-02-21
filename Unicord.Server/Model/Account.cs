using System;
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

    static readonly Func<AccountName, Contact, Contact> addContactFactory = (key, added) => added;
    static readonly Func<AccountName, Contact, Contact, Contact> updateContactFactory = (key, existing, added) => added with
    {
        SubscribedTo = existing.SubscribedTo,
        SubscribedFrom = existing.SubscribedFrom
    };

    public Contact? SetContact(Contact info)
    {
        return contacts.AddOrUpdate(info.Account, addContactFactory, updateContactFactory, info);
    }

    public Contact? RemoveContact(AccountName target)
    {
        return contacts.TryRemove(target, out var contact) ? contact : null;
    }
}
