using System.Diagnostics;
using LiveOddsWallet.Api.Hubs;
using LiveOddsWallet.Api.Models;
using LiveOddsWallet.Api.Services;
using MySqlConnector;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/live-odds-wallet-.json", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddHostedService<LiveOddsBroadcaster>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHub<OddsHub>("/hubs/odds");

app.MapGet("/api/events", async (IConfiguration config) =>
{
    var connString = config.GetConnectionString("DefaultConnection")!;
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    var list = new List<OddsUpdateDto>();
    await using var cmd = new MySqlCommand(
        "SELECT event_id, match_name, home_odds, draw_odds, away_odds, updated_at FROM live_events WHERE is_active = TRUE", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new OddsUpdateDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDateTime(5)
        ));
    }
    return Results.Ok(list);
});

app.MapGet("/api/wallets/{username}", async (string username, IConfiguration config) =>
{
    var connString = config.GetConnectionString("DefaultConnection")!;
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    await using var cmd = new MySqlCommand(
        "SELECT username, balance FROM wallets WHERE username = @u", conn);
    cmd.Parameters.AddWithValue("@u", username);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.NotFound(new { Error = "Wallet not found." });

    return Results.Ok(new { Username = reader.GetString(0), Balance = reader.GetDecimal(1) });
});

app.MapPost("/api/bets", async (PlaceBetRequest req, IConfiguration config) =>
{
    var sw = Stopwatch.StartNew();
    var correlationId = Guid.NewGuid().ToString();
    var selection = req.Selection.Trim().ToUpperInvariant();

    if (req.Stake <= 0 || (selection != "HOME" && selection != "DRAW" && selection != "AWAY"))
        return Results.BadRequest(new { CorrelationId = correlationId, Error = "Invalid stake or selection." });

    var connString = config.GetConnectionString("DefaultConnection")!;
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    try
    {
        int walletId = 0;
        decimal balance = 0;
        await using (var cmd = new MySqlCommand(
            "SELECT wallet_id, balance FROM wallets WHERE username = @u FOR UPDATE", conn, tx))
        {
            cmd.Parameters.AddWithValue("@u", req.Username);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Results.NotFound(new { CorrelationId = correlationId, Error = "Wallet not found." });

            walletId = reader.GetInt32(0);
            balance = reader.GetDecimal(1);
        }

        if (balance < req.Stake)
        {
            await tx.RollbackAsync();
            return Results.BadRequest(new { CorrelationId = correlationId, Error = "Insufficient wallet funds." });
        }

        decimal lockedOdds = 0;
        await using (var cmd = new MySqlCommand(
            "SELECT home_odds, draw_odds, away_odds, is_active FROM live_events WHERE event_id = @id", conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", req.EventId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || !reader.GetBoolean(3))
                return Results.NotFound(new { CorrelationId = correlationId, Error = "Event inactive or not found." });

            lockedOdds = selection switch
            {
                "HOME" => reader.GetDecimal(0),
                "DRAW" => reader.GetDecimal(1),
                _ => reader.GetDecimal(2)
            };
        }

        decimal newBalance = balance - req.Stake;
        decimal potentialPayout = Math.Round(req.Stake * lockedOdds, 2);

        await using (var debit = new MySqlCommand(
            "UPDATE wallets SET balance = @bal WHERE wallet_id = @id", conn, tx))
        {
            debit.Parameters.AddWithValue("@bal", newBalance);
            debit.Parameters.AddWithValue("@id", walletId);
            await debit.ExecuteNonQueryAsync();
        }

        await using (var insertBet = new MySqlCommand(
            @"INSERT INTO bets (correlation_id, wallet_id, event_id, selection, stake, locked_odds, potential_payout, status)
              VALUES (@cid, @wid, @eid, @sel, @stake, @odds, @payout, 'ACCEPTED')", conn, tx))
        {
            insertBet.Parameters.AddWithValue("@cid", correlationId);
            insertBet.Parameters.AddWithValue("@wid", walletId);
            insertBet.Parameters.AddWithValue("@eid", req.EventId);
            insertBet.Parameters.AddWithValue("@sel", selection);
            insertBet.Parameters.AddWithValue("@stake", req.Stake);
            insertBet.Parameters.AddWithValue("@odds", lockedOdds);
            insertBet.Parameters.AddWithValue("@payout", potentialPayout);
            await insertBet.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        sw.Stop();

        Log.Information("Bet {CorrelationId} | Event={EventId} | Selection={Selection} | Stake=R{Stake} | Odds={Odds} | Payout=R{Payout} | {ElapsedMs}ms",
            correlationId, req.EventId, selection, req.Stake, lockedOdds, potentialPayout, sw.ElapsedMilliseconds);

        return Results.Ok(new PlaceBetResponse(correlationId, "ACCEPTED", lockedOdds, potentialPayout, newBalance));
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        Log.Error(ex, "Bet {CorrelationId} rolled back safely.", correlationId);
        return Results.Problem("Bet placement failed safely.");
    }
});

app.Run();
