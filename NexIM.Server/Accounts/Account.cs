using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Xml.Linq;
using NexIM.Server.Accounts.VCards;
using NexIM.Server.Database;
using NexIM.Server.Events;
using NexIM.Server.Tools;

namespace NexIM.Server.Accounts;

public partial class Account
{
    public Server Server { get; }

    internal Identity Identity { get; init; }
    internal Guid Identifier { get; }

    public AccountName Name => Identity.Name;

    internal byte[] PasswordHash { get; }

    public required MailAddress Email { get; set; }

    public required VCard VCard { get; set; }

    SnapshotDictionary<Guid, Contact> contacts = default;
    SnapshotDictionary<XName, PrivateStorageData> privateStorage = default;
    SnapshotDictionary<Guid, UploadedFile> uploadedFiles = default;

    [NotMapped] public IReadOnlyCollection<Contact> Contacts => contacts.Values;
    [NotMapped] public IReadOnlyCollection<PrivateStorageData> PrivateStorage => privateStorage.Values;
    [NotMapped] public IReadOnlyCollection<UploadedFile> UploadedFiles => uploadedFiles.Values;

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
        if(Server.TryGetAccountIdentity(name) is not { } id)
        {
            return null;
        }
        return contacts.TryGetValue(id.Identifier, out var contact) ? contact : null;
    }

    static class Storage<TArgs>
    {
        public static readonly Func<Guid, (Identity identity, Func<Identity, TArgs, Contact?> addFactory, Func<Identity, Contact, TArgs, Contact?> updateFactory, TArgs args), Contact?> Add = (_, info) => {
            return info.addFactory(info.identity, info.args);
        };

        public static readonly Func<Guid, Contact, (Identity identity, Func<Identity, TArgs, Contact?> addFactory, Func<Identity, Contact, TArgs, Contact?> updateFactory, TArgs args), Contact?> Update = (_, previous, info) => {
            return info.updateFactory(info.identity, previous, info.args);
        };
    }

    private bool AddOrUpdateContact<TArgs>(AccountName name, Func<Identity, TArgs, Contact?> addFactory, Func<Identity, Contact, TArgs, Contact?> updateFactory, TArgs args, out Contact? previous, out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        if(IsProhibitedContact(name))
        {
            previous = null;
            updated = null;
            finalContacts = contacts.Values;
            return false;
        }
        var identity = Server.GetAccountIdentity(name, out _);
        var success = contacts.AddOrUpdate(identity.Identifier, Storage<TArgs>.Add, Storage<TArgs>.Update, out previous, out updated, out var snapshot, (identity, addFactory, updateFactory, args));
        finalContacts = snapshot.Values;
        return success;
    }

    private bool IsProhibitedContact(AccountName name)
    {
        // Can't have itself as contact
        return name == Name;
    }

    static readonly Func<Identity, (Account, Contact), Contact> addContact = (identity, info) => {
        var (self, added) = info;
        return Contact.Create(identity, added, self);
    };

    static readonly Func<Identity, Contact, (Account, Contact), Contact> updateContact = (identity, existing, info) => {
        var (self, added) = info;
        return existing.Update(added);
    };

    public bool SetContact(Contact info, out Contact? previous, out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(info.Account, addContact, updateContact, (this, info), out previous, out updated, out finalContacts);
    }

    public bool RemoveContact(AccountName name, [MaybeNullWhen(false)] out Contact result, out IReadOnlyCollection<Contact> finalContacts)
    {
        if(Server.TryGetAccountIdentity(name) is not { } id)
        {
            result = null;
            finalContacts = contacts.Values;
            return false;
        }
        var success = contacts.TryRemove(id.Identifier, out var resultValue, out var snapshot);
        result = resultValue;
        finalContacts = snapshot.Values;
        return success;
    }

    static readonly Func<Identity, (Account, Func<SubscriptionState, SubscriptionState>), Contact?> addContactWithSubscriptionState = (identity, info) => {
        var (self, update) = info;
        var state = update(default);
        if(state.IsEmpty)
        {
            // No reason to add
            return null;
        }
        return Contact.Create(identity, state, self);
    };

    static readonly Func<Identity, Contact, (Account, Func<SubscriptionState, SubscriptionState>), Contact?> updateContactWithSubscriptionState = (name, existing, info) => {
        var (_, update) = info;
        var state = update(existing.SubscriptionState);
        if(state.IsEmpty)
        {
            // No reason to keep
            return null;
        }
        return existing.WithSubscriptionState(state);
    };

    private bool UpdateContactSubscriptionState(AccountName name, Func<SubscriptionState, SubscriptionState> updater, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
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

    public bool TrySetPendingSubscriptionTo(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
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

    public bool TrySetPendingSubscriptionFrom(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
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

    public bool TrySetAcceptedSubscriptionFrom(AccountName name, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
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

    public bool TrySetAcceptedSubscriptionTo(AccountName name, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
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

    public bool TrySetCancelledSubscriptionFrom(AccountName name, [NotNullWhen(true)] out Contact? previous, out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
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

    public bool TrySetCancelledSubscriptionTo(AccountName name, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(name, updateTrySetCancelledSubscriptionTo, out previous, out updated, out finalContacts);
    }
}
