using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.UnitTests.Fakes;

internal sealed class FakeFeedBuffer(List<string> calls) : IFeedBuffer
{
    public List<PositionBatch> Batches { get; } = [];
    public int Count => Batches.Count;

    public ValueTask EnqueueAsync(PositionBatch batch, CancellationToken cancellationToken)
    {
        calls.Add("enqueue");
        Batches.Add(batch);
        return ValueTask.CompletedTask;
    }

    public ValueTask<PositionBatch> DequeueAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public bool TryDequeue(out PositionBatch? batch) => throw new NotSupportedException();
}
