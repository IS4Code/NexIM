using System.Threading.Tasks;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.App.Configuration;

[ComplexType]
public interface IDatabaseHandler : IPayloadHandler
{
    [Name("SQLite")]
    ValueTask SQLite(string? configString);
}
