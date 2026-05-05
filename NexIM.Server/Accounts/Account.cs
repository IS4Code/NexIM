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
    readonly DatabaseContext context;
    public Server Server => context.Server;

    internal Identity Identity { get; init; }
    internal Guid Identifier { get; }

    public AccountName Name => Identity.Name;

    internal byte[] PasswordHash { get; }

    public required DateTime Created { get; set; }
    public required MailAddress Email { get; set; }
    public required VCard VCard { get; set; }

    SnapshotDictionary<Guid, Contact> contacts = default;
    SnapshotDictionary<XName, PrivateStorageData> privateStorage = default;
    SnapshotDictionary<Guid, UploadedFile> uploadedFiles = default;

    [NotMapped] public IReadOnlyCollection<Contact> Contacts => contacts.Values;
    [NotMapped] public IReadOnlyCollection<PrivateStorageData> PrivateStorage => privateStorage.Values;
    [NotMapped] public IReadOnlyCollection<UploadedFile> UploadedFiles => uploadedFiles.Values;

    internal Account(AccountContext context, Identity identity, byte[] passwordHash)
    {
        this.context = context;
        Events = default;
        Collections = default;

        Identity = identity;
        Identifier = identity.Identifier;
        PasswordHash = passwordHash;
    }

    internal Account(DatabaseContext context, Guid identifier, byte[] passwordHash)
    {
        this.context = context;
        Events = default;
        Collections = default;

        Identity = null!;
        Identifier = identifier;
        PasswordHash = passwordHash;
    }

    private async ValueTask<StatusReports> Save()
    {
        await context.SaveChangesAsync();
        return Report(StatusCode.Success);
    }

    internal void AddUploadedFile(UploadedFile file)
    {
        uploadedFiles.SetItem(file.Identifier, file);
        context.UploadedFiles.Add(file);
    }

    internal Contact? GetContact(Identity id)
    {
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

    private bool AddOrUpdateContact<TArgs>(Identity identity, Func<Identity, TArgs, Contact?> addFactory, Func<Identity, Contact, TArgs, Contact?> updateFactory, TArgs args, out Contact? previous, out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        if(IsProhibitedContact(identity.Name))
        {
            previous = null;
            updated = null;
            finalContacts = contacts.Values;
            return false;
        }
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

    internal bool SetContact(Identity id, Contact info, out Contact? previous, out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(id, addContact, updateContact, (this, info), out previous, out updated, out finalContacts);
    }

    internal bool RemoveContact(Identity id, [MaybeNullWhen(false)] out Contact result, out IReadOnlyCollection<Contact> finalContacts)
    {
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

    private bool UpdateContactSubscriptionState(Identity id, Func<SubscriptionState, SubscriptionState> updater, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return AddOrUpdateContact(id, addContactWithSubscriptionState, updateContactWithSubscriptionState, (this, updater), out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetPendingSubscriptionTo = existingState => {
        if(existingState.AcceptedTo || existingState.PendingTo)
        {
            // Already subscribed or pending
            return existingState;
        }
        return existingState.WithPendingTo();
    };

    internal bool TrySetPendingSubscriptionTo(Identity id, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(id, updateTrySetPendingSubscriptionTo, out previous, out updated, out finalContacts);
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

    internal bool TrySetPendingSubscriptionFrom(Identity id, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(id, updateTrySetPendingSubscriptionFrom, out previous, out updated, out finalContacts);
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

    internal bool TrySetAcceptedSubscriptionFrom(Identity id, out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(id, updateTrySetAcceptedSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetAcceptedSubscriptionTo = existingState => {
        if(!existingState.PendingTo)
        {
            // Not pending
            return existingState;
        }
        return existingState.WithAcceptedTo();
    };

    internal bool TrySetAcceptedSubscriptionTo(Identity id, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(id, updateTrySetAcceptedSubscriptionTo, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetCancelledSubscriptionFrom = existingState => {
        if(!existingState.AcceptedFrom && !existingState.ApprovedFrom && !existingState.PendingFrom)
        {
            // Already unsubscribed from
            return existingState;
        }
        return existingState.WithoutFrom();
    };

    internal bool TrySetCancelledSubscriptionFrom(Identity id, [NotNullWhen(true)] out Contact? previous, out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(id, updateTrySetCancelledSubscriptionFrom, out previous, out updated, out finalContacts);
    }

    static readonly Func<SubscriptionState, SubscriptionState> updateTrySetCancelledSubscriptionTo = existingState => {
        if(!existingState.AcceptedTo && !existingState.PendingTo)
        {
            // No change needed
            return existingState;
        }
        return existingState.WithoutTo();
    };

    internal bool TrySetCancelledSubscriptionTo(Identity id, [NotNullWhen(true)] out Contact? previous, [NotNullWhen(true)] out Contact? updated, out IReadOnlyCollection<Contact> finalContacts)
    {
        return UpdateContactSubscriptionState(id, updateTrySetCancelledSubscriptionTo, out previous, out updated, out finalContacts);
    }
}
