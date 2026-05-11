using System;
using System.Threading.Tasks;
using System.Xml;

namespace NexIM.App.Configuration;

sealed class ConfigurationReader : BaseHandler
{
    private ConfigurationReader()
    {

    }

    static readonly XmlReaderSettings readerSettings = new() {
        Async = true,
        CheckCharacters = false,
        DtdProcessing = DtdProcessing.Ignore,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        NameTable = new StaticNameTable()
    };

    public static async ValueTask<ConfigurationHandler> Read(string file)
    {
        var configurationHandler = new ConfigurationHandler();

        using var reader = XmlReader.Create(file, readerSettings);
        await reader.MoveToContentAsync();
        if(reader.Name != "Server" || !String.IsNullOrEmpty(reader.NamespaceURI))
        {
            throw new ApplicationException("The configuration root element must be <Server>.");
        }
        await reader.ReadAsync();

        // Read contents
        await reader.MoveToContentAsync();
        await configurationHandler.ReadFrom(reader);
        return configurationHandler;
    }
}
