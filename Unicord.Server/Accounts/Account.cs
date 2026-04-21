using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unicord.Server.Accounts.VCards;
using Unicord.Server.Database;
using Unicord.Server.Events;
using Unicord.Server.Tools;

namespace Unicord.Server.Accounts;

public partial class Account
{
    public Server Server { get; }

    public Guid Identifier { get; }

    [NotMapped]
    public AccountName Name => new(User, Host);

    internal byte[] PasswordHash { get; }

    public string User { get; private set; }
    public string Host { get; private set; }

    public VCard? VCard { get; set; }

    SnapshotDictionary<AccountName, Contact> contacts = default;

    [NotMapped]
    public ICollection<Contact> Contacts => contacts.Snapshot.Values;

    SnapshotDictionary<XName, PrivateStorageData> privateStorage = default;

    [NotMapped]
    public ICollection<PrivateStorageData> PrivateStorage => privateStorage.Snapshot.Values;

    SnapshotDictionary<Guid, UploadedFile> uploadedFiles = default;

    [NotMapped]
    public ICollection<UploadedFile> UploadedFiles => uploadedFiles.Snapshot.Values;

    internal Account(Server server, Guid identifier, string user, string host, byte[] passwordHash)
    {
        Events = default;
        Collections = default;

        Server = server;
        Identifier = identifier;
        User = user;
        Host = host;
        PasswordHash = passwordHash;
    }

    internal Account(AccountsContext context, Guid identifier, string user, string host, byte[] passwordHash) : this(context.Server, identifier, user, host, passwordHash)
    {

    }

    private async ValueTask<StatusReports> Save()
    {
        await Server.SaveDatabase();
        return Report(StatusCode.Success);
    }

    internal void AddUploadedFile(UploadedFile file)
    {
        uploadedFiles.SetItem(file.Identifier, file);
        Server.AddUploadedFile(file);
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
}
