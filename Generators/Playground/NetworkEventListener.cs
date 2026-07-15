using System.Diagnostics.Tracing;

namespace Playground;

public sealed class NetworkEventListener : EventListener
{
    // Triggered whenever a new EventSource becomes available in the application
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // Console.WriteLine(eventSource.Name);
        // Look for the specific networking providers
        if (eventSource.Name == "Private.InternalDiagnostics.System.Net.HttpListener")
        {
            // Enable events for this source at the Verbose level
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        }
    }

    // Triggered whenever an event is logged by the enabled source
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // Format and output the message safely
        string message = (eventData.Payload != null && eventData.Payload.Count > 0) 
            ? string.Join(", ", eventData.Payload) 
            : eventData.EventName;

        Console.WriteLine($"[{eventData.EventSource.Name} - {eventData.EventName}] {message}");
    }
}