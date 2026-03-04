using System.Text;
using System.Xml;
using Unicord.Xmpp.Protocol;

namespace Unicord.Xmpp.Server;

public abstract class XmppXmlListener<TSession> : XmppListener<TSession> where TSession : XmppXmlSession
{
    readonly XmppNameTable nametable;

    protected XmlReaderSettings ReaderSettings { get; }
    protected XmlWriterSettings WriterSettings { get; }

    protected virtual bool PrettyOutput => false;

    public XmppXmlListener(IXmppReceiver<TSession> receiver, ConformanceLevel conformanceLevel) : base(receiver)
    {
        nametable = new();

        ReaderSettings = new()
        {
            Async = true,
            CheckCharacters = false,
            CloseInput = false,
            ConformanceLevel = conformanceLevel,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            NameTable = nametable,
            ValidationType = ValidationType.None,
            XmlResolver = XmlResolver.ThrowingResolver
        };

        WriterSettings = new()
        {
            Async = true,
            CheckCharacters = false,
            CloseOutput = false,
            ConformanceLevel = conformanceLevel,
            Encoding = new UTF8Encoding(false),
            Indent = PrettyOutput,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize,
            OmitXmlDeclaration = true,
            NewLineOnAttributes = PrettyOutput,
            WriteEndDocumentOnClose = false
        };
    }
}
