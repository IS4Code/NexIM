using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Unicord.Server.Tools;

namespace Unicord.Server.Accounts;

using ContactsBuilder = Account.SnapshotCollectionBuilder<AccountName, Contact>;
using PrivateStorageBuilder = Account.SnapshotCollectionBuilder<XName, PrivateStorageData>;
using UploadedFilesBuilder = Account.SnapshotCollectionBuilder<Guid, UploadedFile>;

partial class Account
{
    private ValueTuple Collections {
        [MemberNotNull(nameof(ContactsBuilder))]
        [MemberNotNull(nameof(PrivateStorageBuilder))]
        [MemberNotNull(nameof(UploadedFilesBuilder))]
        init {
            ContactsBuilder = new(() => ref contacts, x => x.Account);
            PrivateStorageBuilder = new(() => ref privateStorage, x => x.EventData.Key);
            UploadedFilesBuilder = new(() => ref uploadedFiles, x => x.Identifier);
        }
    }

    internal ContactsBuilder ContactsBuilder { get; init; }
    internal PrivateStorageBuilder PrivateStorageBuilder { get; init; }
    internal UploadedFilesBuilder UploadedFilesBuilder { get; init; }

    internal sealed class SnapshotCollectionBuilder<TKey, TValue> : ICollection<TValue> where TKey : notnull where TValue : class
    {
        public delegate ref SnapshotDictionary<TKey, TValue> Accessor();

        readonly Accessor accessor;
        readonly Func<TValue, TKey> keySelector;

        public int Count => accessor().Count;

        bool ICollection<TValue>.IsReadOnly => false;

        public SnapshotCollectionBuilder(Accessor accessor, Func<TValue, TKey> keySelector)
        {
            this.accessor = accessor;
            this.keySelector = keySelector;
        }

        public void Add(TValue item)
        {
            accessor().SetItem(keySelector(item), item);
        }

        public void Clear()
        {
            accessor().Clear();
        }

        public bool Contains(TValue item)
        {
            return accessor().TryGetValue(keySelector(item), out var value) && value == item;
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            accessor().Snapshot.Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(TValue item)
        {
            return accessor().TryRemove(new KeyValuePair<TKey, TValue>(keySelector(item), item));
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return accessor().Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
