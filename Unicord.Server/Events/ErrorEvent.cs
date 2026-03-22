namespace Unicord.Server.Events;

public record ErrorEvent : Event<ErrorData>
{
    public required EventData OriginalData { get; set; }
}

public record ErrorData : EventData
{

}
