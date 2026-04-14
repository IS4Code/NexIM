using System.Threading.Tasks;
using Unicord.Server.Events;

namespace Unicord.Server;

/// <summary>
/// Represents an entity capable of accepting events,
/// as instances of <see cref="Event"/>.
/// </summary>
public interface IEventHandler
{
    /// <summary>
    /// Dispatches an event to be processed by the handler.
    /// </summary>
    /// <param name="evnt">The event representing the command.</param>
    /// <returns>A task representing the operation of handling <paramref name="evnt"/>.</returns>
    ValueTask<StatusCode> Post(Event evnt);
}
