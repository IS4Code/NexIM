using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace NexIM.Primitives.Xml;

internal sealed class XmlAsyncOverSyncWriter(XmlWriter inner) : XmlWriter
{
    static readonly ConditionalWeakTable<XmlWriterSettings, XmlWriterSettings> settingsTable = new();

    public override XmlWriterSettings Settings {
        get {
            var settings = settingsTable.GetOrCreateValue(inner.Settings);
            settings.Async = true;
            settings.CheckCharacters = inner.Settings.CheckCharacters;
            settings.CloseOutput = inner.Settings.CloseOutput;
            settings.ConformanceLevel = inner.Settings.ConformanceLevel;
            settings.DoNotEscapeUriAttributes = inner.Settings.DoNotEscapeUriAttributes;
            settings.Encoding = inner.Settings.Encoding;
            settings.Indent = inner.Settings.Indent;
            settings.IndentChars = inner.Settings.IndentChars;
            settings.NamespaceHandling = inner.Settings.NamespaceHandling;
            settings.NewLineChars = inner.Settings.NewLineChars;
            settings.NewLineHandling = inner.Settings.NewLineHandling;
            settings.NewLineOnAttributes = inner.Settings.NewLineOnAttributes;
            settings.OmitXmlDeclaration = inner.Settings.OmitXmlDeclaration;
            settings.WriteEndDocumentOnClose = inner.Settings.WriteEndDocumentOnClose;
            return settings;
        }
    }

    public override Task FlushAsync()
    {
        try
        {
            Flush();
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteAttributesAsync(XmlReader reader, bool defattr)
    {
        try
        {
            WriteAttributes(reader, defattr);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteCharsAsync(char[] buffer, int index, int count)
    {
        try
        {
            WriteChars(buffer, index, count);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteDocTypeAsync(string name, string pubid, string sysid, string subset)
    {
        try
        {
            WriteDocType(name, pubid, sysid, subset);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    protected override Task WriteEndAttributeAsync()
    {
        try
        {
            WriteEndAttribute();
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteEndDocumentAsync()
    {
        try
        {
            WriteEndDocument();
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteEndElementAsync()
    {
        try
        {
            WriteEndElement();
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteFullEndElementAsync()
    {
        try
        {
            WriteFullEndElement();
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteNmTokenAsync(string name)
    {
        try
        {
            WriteNmToken(name);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteNodeAsync(XmlReader reader, bool defattr)
    {
        if(reader.Settings.Async)
        {
            // Read asynchronously but write synchronously
            return CopyNodeAsync(reader, defattr);
        }
        try
        {
            WriteNode(reader, defattr);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    private async Task CopyNodeAsync(XmlReader reader, bool defattr)
    {
        int depth = reader.NodeType == XmlNodeType.None ? -1 : reader.Depth;
        while(await reader.ReadAsync() && depth < reader.Depth)
        {
            switch(reader.NodeType)
            {
                case XmlNodeType.Element:
                    WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    WriteAttributes(reader, defattr);
                    if(reader.IsEmptyElement)
                    {
                        WriteEndElement();
                    }
                    break;
                case XmlNodeType.Text:
                    if(!reader.CanReadValueChunk)
                    {
                        WriteString(await reader.GetValueAsync());
                        break;
                    }
                    var pool = ArrayPool<char>.Shared;
                    var array = pool.Rent(256);
                    try
                    {
                        int read;
                        while((read = await reader.ReadValueChunkAsync(array, 0, array.Length)) != 0)
                        {
                            WriteChars(array, 0, read);
                        }
                    }
                    finally
                    {
                        pool.Return(array);
                    }
                    break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    WriteWhitespace(await reader.GetValueAsync());
                    break;
                case XmlNodeType.CDATA:
                    WriteCData(await reader.GetValueAsync());
                    break;
                case XmlNodeType.EntityReference:
                    WriteEntityRef(reader.Name);
                    break;
                case XmlNodeType.XmlDeclaration:
                case XmlNodeType.ProcessingInstruction:
                    WriteProcessingInstruction(reader.Name, await reader.GetValueAsync());
                    break;
                case XmlNodeType.DocumentType:
                    WriteDocType(reader.Name, reader.GetAttribute("PUBLIC"), reader.GetAttribute("SYSTEM"), await reader.GetValueAsync());
                    break;
                case XmlNodeType.Comment:
                    WriteComment(await reader.GetValueAsync());
                    break;
                case XmlNodeType.EndElement:
                    WriteFullEndElement();
                    break;
            }
        }
        if(depth == reader.Depth && reader.NodeType == XmlNodeType.EndElement)
        {
            await reader.ReadAsync();
        }
    }

    public override Task WriteNodeAsync(XPathNavigator navigator, bool defattr)
    {
        try
        {
            WriteNode(navigator, defattr);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteProcessingInstructionAsync(string name, string text)
    {
        try
        {
            WriteProcessingInstruction(name, text);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteQualifiedNameAsync(string localName, string ns)
    {
        try
        {
            WriteQualifiedName(localName, ns);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteRawAsync(char[] buffer, int index, int count)
    {
        try
        {
            WriteRaw(buffer, index, count);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteRawAsync(string data)
    {
        try
        {
            WriteRaw(data);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    protected override Task WriteStartAttributeAsync(string prefix, string localName, string ns)
    {
        try
        {
            WriteStartAttribute(prefix, localName, ns);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteStartDocumentAsync()
    {
        try
        {
            WriteStartDocument();
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteStartDocumentAsync(bool standalone)
    {
        try
        {
            WriteStartDocument(standalone);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteStartElementAsync(string prefix, string localName, string ns)
    {
        try
        {
            WriteStartElement(prefix, localName, ns);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteStringAsync(string text)
    {
        try
        {
            WriteString(text);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteSurrogateCharEntityAsync(char lowChar, char highChar)
    {
        try
        {
            WriteSurrogateCharEntity(lowChar, highChar);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override Task WriteWhitespaceAsync(string ws)
    {
        try
        {
            WriteWhitespace(ws);
            return Task.CompletedTask;
        }
        catch(Exception e)
        {
            return Task.FromException(e);
        }
    }

    #region Delegated
    public override WriteState WriteState => inner.WriteState;

    public override string XmlLang => inner.XmlLang;

    public override XmlSpace XmlSpace => inner.XmlSpace;

    public override void Flush()
    {
        inner.Flush();
    }

    public override string LookupPrefix(string ns)
    {
        return inner.LookupPrefix(ns);
    }

    public override void WriteBase64(byte[] buffer, int index, int count)
    {
        inner.WriteBase64(buffer, index, count);
    }

    public override void WriteCData(string text)
    {
        inner.WriteCData(text);
    }

    public override void WriteCharEntity(char ch)
    {
        inner.WriteCharEntity(ch);
    }

    public override void WriteChars(char[] buffer, int index, int count)
    {
        inner.WriteChars(buffer, index, count);
    }

    public override void WriteComment(string text)
    {
        inner.WriteComment(text);
    }

    public override void WriteDocType(string name, string pubid, string sysid, string subset)
    {
        inner.WriteDocType(name, pubid, sysid, subset);
    }

    public override void WriteEndAttribute()
    {
        inner.WriteEndAttribute();
    }

    public override void WriteEndDocument()
    {
        inner.WriteEndDocument();
    }

    public override void WriteEndElement()
    {
        inner.WriteEndElement();
    }

    public override void WriteEntityRef(string name)
    {
        inner.WriteEntityRef(name);
    }

    public override void WriteFullEndElement()
    {
        inner.WriteFullEndElement();
    }

    public override void WriteProcessingInstruction(string name, string text)
    {
        inner.WriteProcessingInstruction(name, text);
    }

    public override void WriteRaw(char[] buffer, int index, int count)
    {
        inner.WriteRaw(buffer, index, count);
    }

    public override void WriteRaw(string data)
    {
        inner.WriteRaw(data);
    }

    public override void WriteStartAttribute(string prefix, string localName, string ns)
    {
        inner.WriteStartAttribute(prefix, localName, ns);
    }

    public override void WriteStartDocument()
    {
        inner.WriteStartDocument();
    }

    public override void WriteStartDocument(bool standalone)
    {
        inner.WriteStartDocument(standalone);
    }

    public override void WriteStartElement(string prefix, string localName, string ns)
    {
        inner.WriteStartElement(prefix, localName, ns);
    }

    public override void WriteString(string text)
    {
        inner.WriteString(text);
    }

    public override void WriteSurrogateCharEntity(char lowChar, char highChar)
    {
        inner.WriteSurrogateCharEntity(lowChar, highChar);
    }

    public override void WriteWhitespace(string ws)
    {
        inner.WriteWhitespace(ws);
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

    public override void WriteAttributes(XmlReader reader, bool defattr)
    {
        inner.WriteAttributes(reader, defattr);
    }

    public override Task WriteBase64Async(byte[] buffer, int index, int count)
    {
        return inner.WriteBase64Async(buffer, index, count);
    }

    public override void WriteBinHex(byte[] buffer, int index, int count)
    {
        inner.WriteBinHex(buffer, index, count);
    }

    public override Task WriteBinHexAsync(byte[] buffer, int index, int count)
    {
        return inner.WriteBinHexAsync(buffer, index, count);
    }

    public override Task WriteCDataAsync(string text)
    {
        return inner.WriteCDataAsync(text);
    }

    public override Task WriteCharEntityAsync(char ch)
    {
        return inner.WriteCharEntityAsync(ch);
    }

    public override Task WriteCommentAsync(string text)
    {
        return inner.WriteCommentAsync(text);
    }

    public override Task WriteEntityRefAsync(string name)
    {
        return inner.WriteEntityRefAsync(name);
    }

    public override void WriteName(string name)
    {
        inner.WriteName(name);
    }

    public override Task WriteNameAsync(string name)
    {
        return inner.WriteNameAsync(name);
    }

    public override void WriteNmToken(string name)
    {
        inner.WriteNmToken(name);
    }

    public override void WriteNode(XPathNavigator navigator, bool defattr)
    {
        inner.WriteNode(navigator, defattr);
    }

    public override void WriteNode(XmlReader reader, bool defattr)
    {
        inner.WriteNode(reader, defattr);
    }

    public override void WriteQualifiedName(string localName, string ns)
    {
        inner.WriteQualifiedName(localName, ns);
    }

    public override void WriteValue(bool value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(decimal value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(double value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(float value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(int value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(long value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(object value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(string value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(DateTime value)
    {
        inner.WriteValue(value);
    }

    public override void WriteValue(DateTimeOffset value)
    {
        inner.WriteValue(value);
    }
    #endregion
}
