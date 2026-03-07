using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace Unicord.Primitives.Xml;

internal sealed class XmlAsyncOverSyncReader(XmlReader inner) : XmlReader
{
    static readonly ConditionalWeakTable<XmlReaderSettings, XmlReaderSettings> settingsTable = new();

    public override XmlReader ReadSubtree()
    {
        return inner.ReadSubtree().WithAsyncSupport();
    }

    public override XmlReaderSettings Settings {
        get {
            var settings = settingsTable.GetOrCreateValue(inner.Settings);
            settings.Async = true;
            settings.CheckCharacters = inner.Settings.CheckCharacters;
            settings.CloseInput = inner.Settings.CloseInput;
            settings.ConformanceLevel = inner.Settings.ConformanceLevel;
            settings.DtdProcessing = inner.Settings.DtdProcessing;
            settings.IgnoreComments = inner.Settings.IgnoreComments;
            settings.IgnoreProcessingInstructions = inner.Settings.IgnoreProcessingInstructions;
            settings.IgnoreWhitespace = inner.Settings.IgnoreWhitespace;
            settings.LineNumberOffset = inner.Settings.LineNumberOffset;
            settings.LinePositionOffset = inner.Settings.LinePositionOffset;
            settings.MaxCharactersFromEntities = inner.Settings.MaxCharactersFromEntities;
            settings.MaxCharactersInDocument = inner.Settings.MaxCharactersInDocument;
            settings.NameTable = inner.Settings.NameTable;
            settings.Schemas = inner.Settings.Schemas;
            settings.ValidationFlags = inner.Settings.ValidationFlags;
            settings.ValidationType = inner.Settings.ValidationType;
            return settings;
        }
    }

    public override Task<string> GetValueAsync()
    {
        try
        {
            return Task.FromResult(Value);
        }
        catch(Exception e)
        {
            return Task.FromException<string>(e);
        }
    }

    public override Task<XmlNodeType> MoveToContentAsync()
    {
        try
        {
            return Task.FromResult(MoveToContent());
        }
        catch(Exception e)
        {
            return Task.FromException<XmlNodeType>(e);
        }
    }

    public override Task<bool> ReadAsync()
    {
        try
        {
            return Task.FromResult(Read());
        }
        catch(Exception e)
        {
            return Task.FromException<bool>(e);
        }
    }

    public override Task<object> ReadContentAsAsync(Type returnType, IXmlNamespaceResolver namespaceResolver)
    {
        try
        {
            return Task.FromResult(ReadContentAs(returnType, namespaceResolver));
        }
        catch(Exception e)
        {
            return Task.FromException<object>(e);
        }
    }

    public override Task<int> ReadContentAsBase64Async(byte[] buffer, int index, int count)
    {
        try
        {
            return Task.FromResult(ReadContentAsBase64(buffer, index, count));
        }
        catch(Exception e)
        {
            return Task.FromException<int>(e);
        }
    }

    public override Task<int> ReadContentAsBinHexAsync(byte[] buffer, int index, int count)
    {
        try
        {
            return Task.FromResult(ReadContentAsBinHex(buffer, index, count));
        }
        catch(Exception e)
        {
            return Task.FromException<int>(e);
        }
    }

    public override Task<object> ReadContentAsObjectAsync()
    {
        try
        {
            return Task.FromResult(ReadContentAsObject());
        }
        catch(Exception e)
        {
            return Task.FromException<object>(e);
        }
    }

    public override Task<string> ReadContentAsStringAsync()
    {
        try
        {
            return Task.FromResult(ReadContentAsString());
        }
        catch(Exception e)
        {
            return Task.FromException<string>(e);
        }
    }

    public override Task<object> ReadElementContentAsAsync(Type returnType, IXmlNamespaceResolver namespaceResolver)
    {
        try
        {
            return Task.FromResult(ReadElementContentAs(returnType, namespaceResolver));
        }
        catch(Exception e)
        {
            return Task.FromException<object>(e);
        }
    }

    public override Task<int> ReadElementContentAsBase64Async(byte[] buffer, int index, int count)
    {
        try
        {
            return Task.FromResult(ReadElementContentAsBase64(buffer, index, count));
        }
        catch(Exception e)
        {
            return Task.FromException<int>(e);
        }
    }

    public override Task<int> ReadElementContentAsBinHexAsync(byte[] buffer, int index, int count)
    {
        try
        {
            return Task.FromResult(ReadElementContentAsBinHex(buffer, index, count));
        }
        catch(Exception e)
        {
            return Task.FromException<int>(e);
        }
    }

    public override Task<object> ReadElementContentAsObjectAsync()
    {
        try
        {
            return Task.FromResult(ReadElementContentAsObject());
        }
        catch(Exception e)
        {
            return Task.FromException<object>(e);
        }
    }

    public override Task<string> ReadElementContentAsStringAsync()
    {
        try
        {
            return Task.FromResult(ReadElementContentAsString());
        }
        catch(Exception e)
        {
            return Task.FromException<string>(e);
        }
    }

    public override Task<string> ReadInnerXmlAsync()
    {
        try
        {
            return Task.FromResult(ReadInnerXml());
        }
        catch(Exception e)
        {
            return Task.FromException<string>(e);
        }
    }

    public override Task<string> ReadOuterXmlAsync()
    {
        try
        {
            return Task.FromResult(ReadOuterXml());
        }
        catch(Exception e)
        {
            return Task.FromException<string>(e);
        }
    }

    public override Task<int> ReadValueChunkAsync(char[] buffer, int index, int count)
    {
        try
        {
            return Task.FromResult(ReadValueChunk(buffer, index, count));
        }
        catch(Exception e)
        {
            return Task.FromException<int>(e);
        }
    }

    public override Task SkipAsync()
    {
        try
        {
            Skip();
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    #region Delegated
    public override int AttributeCount => inner.AttributeCount;

    public override string BaseURI => inner.BaseURI;

    public override int Depth => inner.Depth;

    public override bool EOF => inner.EOF;

    public override bool IsEmptyElement => inner.IsEmptyElement;

    public override string LocalName => inner.LocalName;

    public override string NamespaceURI => inner.NamespaceURI;

    public override XmlNameTable NameTable => inner.NameTable;

    public override XmlNodeType NodeType => inner.NodeType;

    public override string Prefix => inner.Prefix;

    public override ReadState ReadState => inner.ReadState;

    public override string Value => inner.Value;

    public override bool CanReadBinaryContent => inner.CanReadBinaryContent;

    public override bool CanReadValueChunk => inner.CanReadValueChunk;

    public override bool CanResolveEntity => inner.CanResolveEntity;

    public override bool HasAttributes => inner.HasAttributes;

    public override bool HasValue => inner.HasValue;

    public override bool IsDefault => inner.IsDefault;

    public override string Name => inner.Name;

    public override char QuoteChar => inner.QuoteChar;

    public override IXmlSchemaInfo SchemaInfo => inner.SchemaInfo;

    public override string this[int i] => inner[i];

    public override string this[string name, string namespaceURI] => inner[name, namespaceURI];

    public override string this[string name] => inner[name];

    public override Type ValueType => inner.ValueType;

    public override string XmlLang => inner.XmlLang;

    public override XmlSpace XmlSpace => inner.XmlSpace;

    public override string GetAttribute(int i)
    {
        return inner.GetAttribute(i);
    }

    public override string GetAttribute(string name)
    {
        return inner.GetAttribute(name);
    }

    public override string GetAttribute(string name, string namespaceURI)
    {
        return inner.GetAttribute(name, namespaceURI);
    }

    public override string LookupNamespace(string prefix)
    {
        return inner.LookupNamespace(prefix);
    }

    public override bool MoveToAttribute(string name)
    {
        return inner.MoveToAttribute(name);
    }

    public override bool MoveToAttribute(string name, string ns)
    {
        return inner.MoveToAttribute(name, ns);
    }

    public override bool MoveToElement()
    {
        return inner.MoveToElement();
    }

    public override bool MoveToFirstAttribute()
    {
        return inner.MoveToFirstAttribute();
    }

    public override bool MoveToNextAttribute()
    {
        return inner.MoveToNextAttribute();
    }

    public override bool Read()
    {
        return inner.Read();
    }

    public override bool ReadAttributeValue()
    {
        return inner.ReadAttributeValue();
    }

    public override void ResolveEntity()
    {
        inner.ResolveEntity();
    }

    public override void Close()
    {
        inner.Close();
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            inner.Dispose();
        }
    }

    public override bool IsStartElement()
    {
        return inner.IsStartElement();
    }

    public override bool IsStartElement(string localname, string ns)
    {
        return inner.IsStartElement(localname, ns);
    }

    public override bool IsStartElement(string name)
    {
        return inner.IsStartElement(name);
    }

    public override void MoveToAttribute(int i)
    {
        inner.MoveToAttribute(i);
    }

    public override XmlNodeType MoveToContent()
    {
        return inner.MoveToContent();
    }

    public override object ReadContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver)
    {
        return inner.ReadContentAs(returnType, namespaceResolver);
    }

    public override int ReadContentAsBase64(byte[] buffer, int index, int count)
    {
        return inner.ReadContentAsBase64(buffer, index, count);
    }

    public override int ReadContentAsBinHex(byte[] buffer, int index, int count)
    {
        return inner.ReadContentAsBinHex(buffer, index, count);
    }

    public override bool ReadContentAsBoolean()
    {
        return inner.ReadContentAsBoolean();
    }

    public override DateTime ReadContentAsDateTime()
    {
        return inner.ReadContentAsDateTime();
    }

    public override DateTimeOffset ReadContentAsDateTimeOffset()
    {
        return inner.ReadContentAsDateTimeOffset();
    }

    public override decimal ReadContentAsDecimal()
    {
        return inner.ReadContentAsDecimal();
    }

    public override double ReadContentAsDouble()
    {
        return inner.ReadContentAsDouble();
    }

    public override float ReadContentAsFloat()
    {
        return inner.ReadContentAsFloat();
    }

    public override int ReadContentAsInt()
    {
        return inner.ReadContentAsInt();
    }

    public override long ReadContentAsLong()
    {
        return inner.ReadContentAsLong();
    }

    public override object ReadContentAsObject()
    {
        return inner.ReadContentAsObject();
    }

    public override string ReadContentAsString()
    {
        return inner.ReadContentAsString();
    }

    public override object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver)
    {
        return inner.ReadElementContentAs(returnType, namespaceResolver);
    }

    public override object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver, string localName, string namespaceURI)
    {
        return inner.ReadElementContentAs(returnType, namespaceResolver, localName, namespaceURI);
    }

    public override int ReadElementContentAsBase64(byte[] buffer, int index, int count)
    {
        return inner.ReadElementContentAsBase64(buffer, index, count);
    }

    public override int ReadElementContentAsBinHex(byte[] buffer, int index, int count)
    {
        return inner.ReadElementContentAsBinHex(buffer, index, count);
    }

    public override bool ReadElementContentAsBoolean()
    {
        return inner.ReadElementContentAsBoolean();
    }

    public override bool ReadElementContentAsBoolean(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsBoolean(localName, namespaceURI);
    }

    public override DateTime ReadElementContentAsDateTime()
    {
        return inner.ReadElementContentAsDateTime();
    }

    public override DateTime ReadElementContentAsDateTime(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsDateTime(localName, namespaceURI);
    }

    public override decimal ReadElementContentAsDecimal()
    {
        return inner.ReadElementContentAsDecimal();
    }

    public override decimal ReadElementContentAsDecimal(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsDecimal(localName, namespaceURI);
    }

    public override double ReadElementContentAsDouble()
    {
        return inner.ReadElementContentAsDouble();
    }

    public override double ReadElementContentAsDouble(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsDouble(localName, namespaceURI);
    }

    public override float ReadElementContentAsFloat()
    {
        return inner.ReadElementContentAsFloat();
    }

    public override float ReadElementContentAsFloat(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsFloat(localName, namespaceURI);
    }

    public override int ReadElementContentAsInt()
    {
        return inner.ReadElementContentAsInt();
    }

    public override int ReadElementContentAsInt(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsInt(localName, namespaceURI);
    }

    public override long ReadElementContentAsLong()
    {
        return inner.ReadElementContentAsLong();
    }

    public override long ReadElementContentAsLong(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsLong(localName, namespaceURI);
    }

    public override object ReadElementContentAsObject()
    {
        return inner.ReadElementContentAsObject();
    }

    public override object ReadElementContentAsObject(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsObject(localName, namespaceURI);
    }

    public override string ReadElementContentAsString()
    {
        return inner.ReadElementContentAsString();
    }

    public override string ReadElementContentAsString(string localName, string namespaceURI)
    {
        return inner.ReadElementContentAsString(localName, namespaceURI);
    }

    public override string ReadElementString()
    {
        return inner.ReadElementString();
    }

    public override string ReadElementString(string localname, string ns)
    {
        return inner.ReadElementString(localname, ns);
    }

    public override string ReadElementString(string name)
    {
        return inner.ReadElementString(name);
    }

    public override void ReadEndElement()
    {
        inner.ReadEndElement();
    }

    public override string ReadInnerXml()
    {
        return inner.ReadInnerXml();
    }

    public override string ReadOuterXml()
    {
        return inner.ReadOuterXml();
    }

    public override void ReadStartElement()
    {
        inner.ReadStartElement();
    }

    public override void ReadStartElement(string localname, string ns)
    {
        inner.ReadStartElement(localname, ns);
    }

    public override void ReadStartElement(string name)
    {
        inner.ReadStartElement(name);
    }

    public override string ReadString()
    {
        return inner.ReadString();
    }

    public override bool ReadToDescendant(string localName, string namespaceURI)
    {
        return inner.ReadToDescendant(localName, namespaceURI);
    }

    public override bool ReadToDescendant(string name)
    {
        return inner.ReadToDescendant(name);
    }

    public override bool ReadToFollowing(string localName, string namespaceURI)
    {
        return inner.ReadToFollowing(localName, namespaceURI);
    }

    public override bool ReadToFollowing(string name)
    {
        return inner.ReadToFollowing(name);
    }

    public override bool ReadToNextSibling(string localName, string namespaceURI)
    {
        return inner.ReadToNextSibling(localName, namespaceURI);
    }

    public override bool ReadToNextSibling(string name)
    {
        return inner.ReadToNextSibling(name);
    }

    public override int ReadValueChunk(char[] buffer, int index, int count)
    {
        return inner.ReadValueChunk(buffer, index, count);
    }

    public override void Skip()
    {
        inner.Skip();
    }
    #endregion
}
