namespace Unicord.Server.Model.Events;

public record ErrorEvent : Event<ErrorData>
{
    public required EventData OriginalData { get; set; }
}

public record ErrorData : EventData
{

}
