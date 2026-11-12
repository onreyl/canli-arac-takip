using VehicleTracking.Application.Live;

namespace VehicleTracking.UnitTests.Fakes;

internal sealed class FakeLiveUpdatePublisher(List<string> calls) : ILiveUpdatePublisher
{
    public List<LiveUpdate> Updates { get; } = [];
    public Exception? Failure { get; init; }

    public void Publish(LiveUpdate update)
    {
        calls.Add("publish");
        Updates.Add(update);
        if (Failure is { } failure)
        {
            throw failure;
        }
    }
}
