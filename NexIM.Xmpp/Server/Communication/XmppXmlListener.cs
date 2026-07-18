using System.Text;
using System.Xml;
using NexIM.Tools;
using NexIM.Xmpp.Protocol;

namespace NexIM.Xmpp.Server.Communication;

/// <summary>
/// Represents an entity capable of accepting XMPP connections
/// as instances of <see cref="XmppXmlSession"/>.
/// </summary>
/// <typeparam name="TSession">
/// The type of accepted sessions.
/// </typeparam>
public abstract class XmppXmlListener<TSession> : XmppListener<TSession> where TSession : XmppXmlSession
{
    readonly XmlWeakNameTable nametable = Protocol.Grammar.Vocabulary.Instance.CreateNameTable<XmlWeakNameTable>();

    protected XmlReaderSettings ReaderSettings { get; }
    protected XmlWriterSettings WriterSettings { get; }

    protected abstract ConformanceLevel ConformanceLevel { get; }

    protected virtual bool PrettyOutput => false;

    public XmppXmlListener(IXmppReceiver<TSession> receiver) : base(receiver)
    {
        ReaderSettings = new() {
            Async = true,
            CheckCharacters = false,
            CloseInput = false,
            ConformanceLevel = ConformanceLevel,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            NameTable = nametable,
            ValidationType = ValidationType.None,
            XmlResolver = XmlResolver.ThrowingResolver
        };

        WriterSettings = new() {
            Async = true,
            CheckCharacters = false,
            CloseOutput = false,
            ConformanceLevel = ConformanceLevel,
            Encoding = new UTF8Encoding(false),
            Indent = PrettyOutput,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize,
            OmitXmlDeclaration = true,
            NewLineOnAttributes = false,
            WriteEndDocumentOnClose = false
        };
    }
}
