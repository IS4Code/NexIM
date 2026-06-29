using System.Threading.Tasks;
using NexIM.Primitives.Xml.Grammar;
using NexIM.Primitives.Xml.Handlers;

namespace NexIM.App.Configuration;

[ComplexType]
public interface IDatabaseHandler : IPayloadHandler
{
    [Name("ConnectionString")]
    ValueTask ConnectionString(string? configString);
}

[SimpleType]
public enum DatabaseType
{
    [Name("SQLite")]
    Sqlite,

    [Name("MySQL")]
    MySQL,

    [Name("PostgreSQL")]
    PostgreSQL
}
