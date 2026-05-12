using System;
using System.Collections.Generic;
using System.Xml;

namespace NexIM.Primitives.Xml;

/// <summary>
/// Represents a collection of atomized XML vocabulary elements.
/// </summary>
public abstract class XmlVocabulary
{
    readonly List<string> data = new();

    public TNameTable CreateNameTable<TNameTable>() where TNameTable : XmlNameTable, new()
    {
        var table = new TNameTable();
        foreach(var item in data)
        {
            if(!Object.ReferenceEquals(item, table.Add(item)))
            {
                throw new NotSupportedException($"The key reference '{item}' is already present in the table.");
            }
        }
        return table;
    }

    protected void AddKey(string key)
    {
        data.Add(key);
    }

    protected abstract void Initialize();
}
