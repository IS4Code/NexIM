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

    internal Identity Identity { get; init; }
    internal Guid Identifier { get; }

    public AccountName Name => Identity.Name;

    internal byte[] PasswordHash { get; }

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

    internal Account(Server server, Identity identity, byte[] passwordHash)
    {
        Events = default;
        Collections = default;

        Server = server;
        Identity = identity;
        Identifier = identity.Identifier;
        PasswordHash = passwordHash;
    }

    internal Account(AccountsContext context, Guid identifier, byte[] passwordHash)
    {
        Events = default;
        Collections = default;

        Server = context.Server;
        Identity = null!;
        Identifier = identifier;
        PasswordHash = passwordHash;
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

    private bool AddOrUpdateContact<TArgs>(AccountName name, Func<AccountName, TArgs, Contact?> addFactory, Func<AccountName, Contact, TArgs, Contact?> updateFactory, TArgs args, out Contact? previous, out Contact? updated, out ICollection<Contact> finalContacts)
    {
        if(IsProhibitedContact(name))
        {
            previous = null;
            updated = null;
            finalContacts = Contacts;
            return false;
        }
        var success = contacts.AddOrUpdate(name, addFactory, updateFactory, out previous, out updated, out var snapshot, args);
        finalContacts = snapshot.Values;
        return success;
    }

    private bool IsProhibitedContact(AccountName name)
    {
        // Can't have itself as contact
        return name == Name;
    }

    static readonly Func<AccountName, (Account, Contact), Contact> addContact = (name, info) => {
        var (self, added) = info;
        var identity = self.Server.GetAccountIdentity(added.Account, out _);
        return Contact.Create(identity, added, self);
    };

    static readonly Func<AccountName, Contact, (Account, Contact), Contact> updateContact = (name, existing, info) => {
        var (self, added) = info;
        return existing.Update(added);
    };

    public bool SetContact(Contact info, out Contact? previous, out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(info.Account, addContact, updateContact, (this, info), out previous, out updated, out finalContacts);
    }

    public bool RemoveContact(AccountName name, [MaybeNullWhen(false)] out Contact result, out ICollection<Contact> finalContacts)
    {
        var success = contacts.TryRemove(name, out result, out var snapshot);
        finalContacts = snapshot.Values;
        return success;
    }

    static readonly Func<AccountName, (Account, Func<SubscriptionState, SubscriptionState>), Contact?> addContactWithSubscriptionState = (name, info) => {
        var (self, update) = info;
        var state = update(default);
        if(state.IsEmpty)
        {
            // No reason to add
            return null;
        }
        var identity = self.Server.GetAccountIdentity(name, out _);
        return Contact.Create(identity, state, self);
    };

    static readonly Func<AccountName, Contact, (Account, Func<SubscriptionState, SubscriptionState>), Contact?> updateContactWithSubscriptionState = (name, existing, info) => {
        var (_, update) = info;
        var state = update(existing.SubscriptionState);
        if(state.IsEmpty)
        {
            // No reason to keep
            return null;
        }
        return existing.WithSubscriptionState(state);
    };

    private bool UpdateContactSubscriptionState(AccountName name, Func<SubscriptionState, SubscriptionState> updater, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(name, addContactWithSubscriptionState, updateContactWithSubscriptionState, (this, updater), out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetPendingSubscriptionTo = existingState => {
        if(existingState.AcceptedTo || existingState.PendingTo)
        {
            // Already subscribed or pending
            return existingState;
        }
        return existingState.WithPendingTo();
    };

    public bool TrySetPendingSubscriptionTo(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(name, updateTrySetPendingSubscriptionTo, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetPendingSubscriptionFrom = existingState => {
        if(existingState.AcceptedFrom)
        {
            // Already accepted
            return existingState;
        }
        if(existingState.ApprovedFrom)
        {
            // Auto-accept
            return existingState.WithAcceptedFrom();
        }
        return existingState.WithPendingFrom();
    };

    public bool TrySetPendingSubscriptionFrom(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(name, updateTrySetPendingSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetAcceptedSubscriptionFrom = existingState => {
        if(existingState.AcceptedFrom || existingState.ApprovedFrom)
        {
            // Already subscribed or approved from
            return existingState;
        }
        if(existingState.PendingFrom)
        {
            // Requested previously, consider accepted from now
            return existingState.WithAcceptedFrom();
        }
        return existingState.WithApprovedFrom();
    };

    public bool TrySetAcceptedSubscriptionFrom(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(name, updateTrySetAcceptedSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetAcceptedSubscriptionTo = existingState => {
        if(!existingState.PendingTo)
        {
            // Not pending
            return existingState;
        }
        return existingState.WithAcceptedTo();
    };

    public bool TrySetAcceptedSubscriptionTo(AccountName name, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(name, updateTrySetAcceptedSubscriptionTo, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetCancelledSubscriptionFrom = existingState => {
        if(!existingState.AcceptedFrom && !existingState.ApprovedFrom && !existingState.PendingFrom)
        {
            // Already unsubscribed from
            return existingState;
        }
        return existingState.WithoutFrom();
    };

    public bool TrySetCancelledSubscriptionFrom(AccountName name, [NotNullWhen(true)] out Contact? previous, out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(name, updateTrySetCancelledSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetCancelledSubscriptionTo = existingState => {
        if(!existingState.AcceptedTo && !existingState.PendingTo)
        {
            // No change needed
            return existingState;
        }
        return existingState.WithoutTo();
    };

    public bool TrySetCancelledSubscriptionTo(AccountName name, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out ICollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(name, updateTrySetCancelledSubscriptionTo, out previous, out updated, out finalContacts);
    }
}
