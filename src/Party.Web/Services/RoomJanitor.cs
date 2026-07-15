namespace Party.Web.Services;

/// <summary>Evicts rooms that have been idle for over an hour. State is intentionally ephemeral.</summary>
/// <remarks>
/// Knows nothing about which game a room is playing, and doesn't need to: a code and an idle
/// clock are all it ever looks at.
/// <para>
/// The intervals are injectable so a test can drive a real sweep in milliseconds rather than
/// reimplement the loop and assert on its own copy.
/// </para>
/// </remarks>
public sealed class RoomJanitor(RoomManager rooms, TimeSpan? sweepInterval = null, TimeSpan? idleLimit = null)
    : BackgroundService
{
    private static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultIdleLimit = TimeSpan.FromHours(1);

    private readonly TimeSpan _sweepInterval = sweepInterval ?? DefaultSweepInterval;
    private readonly TimeSpan _idleLimit = idleLimit ?? DefaultIdleLimit;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_sweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Sweep();
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    /// <summary>
    /// One look round the rooms. Public so a test can prove the eviction rule without racing a
    /// timer: whether activity holds a room open is the interesting part, and a keep-alive loop
    /// against a real clock tests the scheduler more than it tests this.
    /// </summary>
    public void Sweep()
    {
        foreach (var handle in rooms.Rooms)
        {
            if (DateTimeOffset.UtcNow - handle.LastActivity > _idleLimit)
            {
                rooms.Remove(handle.Code);
                handle.Close();
            }
        }
    }
}
