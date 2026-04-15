using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Unicord.Server.Events;
using Unicord.Server.Tools;

namespace Unicord.Server.Accounts;

using ContactsBuilder = Account.SnapshotCollectionBuilder<AccountName, Contact>;
using PrivateStorageBuilder = Account.SnapshotCollectionBuilder<XName, PrivateStorageData>;

partial class Account
{
    readonly ContactsBuilder.Accessor contactsAccessor;
    static readonly Func<Contact, AccountName> contactsKeySelector = x => x.Account;

    readonly PrivateStorageBuilder.Accessor privateStorageAccessor;
    static readonly Func<PrivateStorageData, XName> privateStorageKeySelector = x => x.Key;

    private ValueTuple Collections {
        [MemberNotNull(nameof(contactsAccessor))]
        init {
            contactsAccessor = () => ref contacts;
            privateStorageAccessor = () => ref privateStorage;
        }
    }

    internal ContactsBuilder ContactsBuilder => new(contactsAccessor, contactsKeySelector);

    internal PrivateStorageBuilder PrivateStorageBuilder => new(privateStorageAccessor, privateStorageKeySelector);

    internal sealed class SnapshotCollectionBuilder<TKey, TValue> : ICollection<TValue> where TKey : notnull
    {
        public delegate ref SnapshotDictionary<TKey, TValue> Accessor();

        readonly Accessor accessor;
        readonly Func<TValue, TKey> keySelector;

        readonly SnapshotDictionary<TKey, TValue>.Builder builder;

        public int Count => builder.Count;

        bool ICollection<TValue>.IsReadOnly => false;

        public SnapshotCollectionBuilder(Accessor accessor, Func<TValue, TKey> keySelector)
        {
            this.accessor = accessor;
            this.keySelector = keySelector;

            builder = accessor().ToBuilder();
        }

        private void Update()
        {
            // TODO Avoid replacing every time
            accessor() = builder.ToDictionary();
        }

        public void Add(TValue item)
        {
            builder.Add(keySelector(item), item);
            Update();
        }

        public void Clear()
        {
            builder.Clear();
            Update();
        }

        public bool Contains(TValue item)
        {
            return builder.Contains(new(keySelector(item), item));
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
            builder.Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(TValue item)
        {
            if(builder.Remove(new KeyValuePair<TKey, TValue>(keySelector(item), item)))
            {
                Update();
                return true;
            }
            return false;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return builder.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
