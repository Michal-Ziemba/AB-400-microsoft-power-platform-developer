namespace EquipmentRequestFunctions;

/// <summary>
/// Stands in for the organization's real external availability service, which this exercise doesn't call
/// directly. The delay represents the latency that makes this work a poor fit for a synchronous Dataverse
/// plug-in: a caller would have to block until the external service replies.
/// </summary>
public static class SlowAvailabilityService
{
    public static async Task<bool> CheckAsync(string requestNumber)
    {
        await Task.Delay(TimeSpan.FromSeconds(4));

        // This exercise always reports the device as available, so the ta-da is predictable for every
        // learner. A real external service would return true or false depending on current stock.
        return true;
    }
}
