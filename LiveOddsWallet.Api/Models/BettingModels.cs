namespace LiveOddsWallet.Api.Models;

public record PlaceBetRequest(
    string Username,
    int EventId,
    string Selection,
    decimal Stake
);

public record PlaceBetResponse(
    string CorrelationId,
    string Status,
    decimal LockedOdds,
    decimal PotentialPayout,
    decimal RemainingBalance
);

public record OddsUpdateDto(
    int EventId,
    string MatchName,
    decimal HomeOdds,
    decimal DrawOdds,
    decimal AwayOdds,
    DateTime TimestampUtc
);
