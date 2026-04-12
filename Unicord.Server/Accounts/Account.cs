using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unicord.Server.Accounts.VCards;
using Unicord.Server.Database;
using Unicord.Server.Tools;

namespace Unicord.Server.Accounts;

public partial class Account
{
    public Server Server { get; }

    public AccountName Name => new(User, Host);
    internal byte[] PasswordHash { get; }

    public string User { get; private set; }
    public string Host { get; private set; }

    public VCard? VCard { get; set; }

    SnapshotDictionary<AccountName, Contact> contacts = default;

    public ICollection<Contact> Contacts => contacts.Snapshot.Values;

    internal ContactsBuilderCollection ContactsBuilder => new(this);

    public Account(Server server, string user, string host, byte[] passwordHash)
    {
        Server = server;
        User = user;
        Host = host;
        PasswordHash = passwordHash;

        InitEvents(out router, out messageTarget, out presenceTarget, out generalTarget);
    }

    internal Account(AccountsContext context, string user, string host, byte[] passwordHash) : this(context.Server, user, host, passwordHash)
    {

    }

    public Contact? GetContact(AccountName name)
    {
        return contacts.TryGetValue(name, out var contact) ? contact : null;
    }

    private bool AddOrUpdateContact(AccountName name, Func<AccountName, ValueTuple, Contact?> addFactory, Func<AccountName, Contact, ValueTuple, Contact?> updateFactory, out Contact? previous, out Contact? updated, out ICollection<Contact> finalContacts)
    {
        if(IsProhibitedContact(name))
        {
            previous = null;
            updated = null;
            finalContacts = Contacts;
            return false;
        }
        var success = contacts.AddOrUpdate(name, addFactory, updateFactory, out previous, out updated, out var snapshot, default);
        finalContacts = snapshot.Values;
        return success;
    }

    private bool IsProhibitedContact(AccountName name)
    {
        // Can't have itself as contact
        return name == Name;
    }

    // New contact's subscription state "approved to" flag is set only if the request originates from the user.

    static readonly Func<AccountName, Contact, Contact> addContact = (name, added) => {
        var newState = added.SubscriptionState.ApprovedTo ? SubscriptionState.InitialApprovedTo : SubscriptionState.Initial;
        return added.WithSubscriptionState(newState);
    };

    static readonly Func<AccountName, Contact, Contact, Contact> updateContact = (name, existing, added) => {
        var newState = added.SubscriptionState.ApprovedTo ? existing.SubscriptionState.WithApprovedTo() : existing.SubscriptionState;
        return added.WithSubscriptionState(newState);
    };

    public bool SetContact(Contact info, out Contact? previous, out Contact? updated, out ICollection<Contact> finalContacts)
    {
        if(IsProhibitedContact(info.Account))
        {
            previous = null;
            updated = null;
            finalContacts = Contacts;
            return false;
        }
        var success = contacts.AddOrUpdate(info.Account, addContact, updateContact, out previous, out updated, out var snapshot, info);
        finalContacts = snapshot.Values;
        return success;
    }

    public bool RemoveContact(AccountName name, [MaybeNullWhen(false)] out Contact result, out ICollection<Contact> finalContacts)
    {
        var success = contacts.TryRemove(name, out result, out var snapshot);
        finalContacts = snapshot.Values;
        return success;
    }

    static readonly Func<AccountName, ValueTuple, Contact?> addTrySetPendingSubscriptionTo = (name, _) => new Contact {
        Account = name,
        SubscriptionState = SubscriptionState.InitialPendingTo
    };
    static readonly Func<AccountName, Contact, ValueTuple, Contact> updateTrySetPendingSubscriptionTo = (name, existing, _) => {
        var existingState = existing.SubscriptionState;
        if(existingState.AcceptedTo || existingState.PendingTo)
        {
            // Already subscribed or pending
            return existing;
        }
        return existing.WithSubscriptionState(existingState.WithPendingTo());
    };

    public bool TrySetPendingSubscriptionTo(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(name, addTrySetPendingSubscriptionTo, updateTrySetPendingSubscriptionTo, out previous, out updated, out finalContacts);
    }

    static readonly Func<AccountName, ValueTuple, Contact?> addTrySetPendingSubscriptionFrom = (name, _) => new Contact {
        Account = name,
        SubscriptionState = SubscriptionState.InitialPendingFrom
    };
    static readonly Func<AccountName, Contact, ValueTuple, Contact> updateTrySetPendingSubscriptionFrom = (name, existing, _) => {
        var existingState = existing.SubscriptionState;
        if(existingState.AcceptedFrom)
        {
            // Already accepted
            return existing;
        }
        if(existingState.ApprovedFrom)
        {
            // Auto-accept
            return existing with {
                SubscriptionState = existingState.WithAcceptedFrom()
            };
        }
        return existing with { SubscriptionState = existingState.WithPendingFrom() };
    };

    public bool TrySetPendingSubscriptionFrom(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(name, addTrySetPendingSubscriptionFrom, updateTrySetPendingSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<AccountName, ValueTuple, Contact?> addTrySetAcceptedSubscriptionFrom = (name, _) => new Contact {
        Account = name,
        SubscriptionState = SubscriptionState.InitialApprovedFrom
    };
    static readonly Func<AccountName, Contact, ValueTuple, Contact> updateTrySetAcceptedSubscriptionFrom = (name, existing, _) => {
        var existingState = existing.SubscriptionState;
        if(existingState.AcceptedFrom || existingState.ApprovedFrom)
        {
            // Already subscribed or approved from
            return existing;
        }
        if(existingState.PendingFrom)
        {
            // Requested previously, consider accepted from now
            return existing with {
                SubscriptionState = existingState.WithAcceptedFrom()
            };
        }
        return existing with { SubscriptionState = existingState.WithApprovedFrom() };
    };

    public bool TrySetAcceptedSubscriptionFrom(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(name, addTrySetAcceptedSubscriptionFrom, updateTrySetAcceptedSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<AccountName, ValueTuple, Contact?> addTrySetAcceptedSubscriptionTo = delegate { return null; };
    static readonly Func<AccountName, Contact, ValueTuple, Contact> updateTrySetAcceptedSubscriptionTo = (name, existing, _) => {
        var existingState = existing.SubscriptionState;
        if(!existingState.PendingTo)
        {
            // Not pending
            return existing;
        }
        return existing with {
            SubscriptionState = existingState.WithAcceptedTo()
        };
    };

    public bool TrySetAcceptedSubscriptionTo(AccountName name, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(name, addTrySetAcceptedSubscriptionTo, updateTrySetAcceptedSubscriptionTo, out previous, out updated, out finalContacts);
    }

    static readonly Func<AccountName, ValueTuple, Contact?> addTrySetCancelledSubscriptionFrom = delegate { return null; };
    static readonly Func<AccountName, Contact, ValueTuple, Contact?> updateTrySetCancelledSubscriptionFrom = (name, existing, _) => {
        var existingState = existing.SubscriptionState;
        if(!existingState.AcceptedFrom && !existingState.ApprovedFrom && !existingState.PendingFrom)
        {
            // Already unsubscribed from
            return existing;
        }
        if(!existingState.ApprovedTo)
        {
            // Not interested in the contact at all
            return null;
        }
        return existing with {
            SubscriptionState = existingState.WithoutFrom()
        };
    };

    public bool TrySetCancelledSubscriptionFrom(AccountName name, [NotNullWhen(true)] out Contact? previous, out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(name, addTrySetCancelledSubscriptionFrom, updateTrySetCancelledSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<AccountName, ValueTuple, Contact?> addTrySetCancelledSubscriptionTo = delegate { return null; };
    static readonly Func<AccountName, Contact, ValueTuple, Contact> updateTrySetCancelledSubscriptionTo = (name, existing, _) => {
        var existingState = existing.SubscriptionState;
        if(!existingState.AcceptedTo && !existingState.PendingTo)
        {
            // No change needed
            return existing;
        }
        return existing with {
            SubscriptionState = existingState.WithoutTo()
        };
    };

    public bool TrySetCancelledSubscriptionTo(AccountName name, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(name, addTrySetCancelledSubscriptionTo, updateTrySetCancelledSubscriptionTo, out previous, out updated, out finalContacts);
    }

    internal sealed class ContactsBuilderCollection(Account account) : ICollection<Contact>
    {
        readonly IDictionary<AccountName, Contact> data = account.contacts.CreateBuilder();

        public int Count => data.Count;

        public bool IsReadOnly => false;

        private void Update()
        {
            // TODO Avoid replacing every time
            account.contacts = new(data);
        }

        public void Add(Contact item)
        {
            data.Add(item.Account, item);
            Update();
        }

        public void Clear()
        {
            data.Clear();
            Update();
        }

        public bool Contains(Contact item)
        {
            return data.Contains(new(item.Account, item));
        }

        public void CopyTo(Contact[] array, int arrayIndex)
        {
            data.Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(Contact item)
        {
            if(data.Remove(new KeyValuePair<AccountName, Contact>(item.Account, item)))
            {
                Update();
                return true;
            }
            return false;
        }

        public IEnumerator<Contact> GetEnumerator()
        {
            return data.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
