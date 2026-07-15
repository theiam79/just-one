namespace Party.Web.Services;

/// <summary>Evicts rooms that have been idle for over an hour. State is intentionally ephemeral.</summary>
public sealed class RoomJanitor(RoomManager rooms) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleLimit = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var handle in rooms.Rooms)
            {
                if (DateTimeOffset.UtcNow - handle.LastActivity > IdleLimit)
                {
                    rooms.Remove(handle.Code);
                    handle.Close();
                }
            }
        }
    }
}
