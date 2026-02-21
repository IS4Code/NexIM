using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Unicord.Server.Model;

public class Account
{
    public AccountName Name { get; }
    internal byte[] PasswordHash { get; }
    ImmutableDictionary<AccountName, Contact> contacts = ImmutableDictionary<AccountName, Contact>.Empty;

    public ICollection<Contact> Contacts => ((IDictionary<AccountName, Contact>)contacts).Values;

    public Account(AccountName name, byte[] passwordHash)
    {
        Name = name;
        PasswordHash = passwordHash;
    }

    static readonly Func<AccountName, Contact, Contact> addContactFactory = (key, added) => added;
    static readonly Func<AccountName, Contact, Contact, Contact> updateContactFactory = (key, existing, added) => added with
    {
        SubscriptionState = existing.SubscriptionState
    };

    public Contact SetContact(Contact info, out ICollection<Contact> finalContacts)
    {
        var result = Immutable.AddOrUpdate(ref contacts, info.Account, addContactFactory, updateContactFactory, out var final, info);
        finalContacts = ((IDictionary<AccountName, Contact>)final).Values;
        return result;
    }

    public Contact? RemoveContact(AccountName target, out ICollection<Contact> finalContacts)
    {
        var result = Immutable.TryRemove(ref contacts, target, out var contact, out var final) ? contact : null;
        finalContacts = ((IDictionary<AccountName, Contact>)final).Values;
        return result;
    }
}
