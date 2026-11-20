using LiveOddsWallet.Api.Hubs;
using LiveOddsWallet.Api.Models;
using Microsoft.AspNetCore.SignalR;
using MySqlConnector;

namespace LiveOddsWallet.Api.Services;

public class LiveOddsBroadcaster : BackgroundService
{
    private readonly IHubContext<OddsHub> _hubContext;
    private readonly IConfiguration _config;
    private readonly Random _rng = new();

    public LiveOddsBroadcaster(IHubContext<OddsHub> hubContext, IConfiguration config)
    {
        _hubContext = hubContext;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connString = _config.GetConnectionString("DefaultConnection")!;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            try
            {
                await using var conn = new MySqlConnection(connString);
                await conn.OpenAsync(stoppingToken);

                var events = new List<(int Id, string Name, decimal Home, decimal Draw, decimal Away)>();
                await using (var cmd = new MySqlCommand(
                    "SELECT event_id, match_name, home_odds, draw_odds, away_odds FROM live_events WHERE is_active = TRUE", conn))
                await using (var reader = await cmd.ExecuteReaderAsync(stoppingToken))
                {
                    while (await reader.ReadAsync(stoppingToken))
                    {
                        events.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetDecimal(2),
                            reader.GetDecimal(3),
                            reader.GetDecimal(4)
                        ));
                    }
                }

                foreach (var ev in events)
                {
                    decimal delta = Math.Round((decimal)(_rng.NextDouble() * 0.10 - 0.05), 2);
                    decimal newHome = Math.Max(1.05m, ev.Home + delta);
                    decimal newAway = Math.Max(1.05m, ev.Away - delta);

                    await using var update = new MySqlCommand(
                        "UPDATE live_events SET home_odds = @h, away_odds = @a WHERE event_id = @id", conn);
                    update.Parameters.AddWithValue("@h", newHome);
                    update.Parameters.AddWithValue("@a", newAway);
                    update.Parameters.AddWithValue("@id", ev.Id);
                    await update.ExecuteNonQueryAsync(stoppingToken);

                    var dto = new OddsUpdateDto(ev.Id, ev.Name, newHome, ev.Draw, newAway, DateTime.UtcNow);
                    await _hubContext.Clients.All.SendAsync("ReceiveOddsUpdate", dto, stoppingToken);
                }
            }
            catch
            {
            }
        }
    }
}
