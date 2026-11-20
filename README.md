# Real-Time Live Odds & High-Concurrency Wallet Engine

A real-time **.NET 10 Web API**, **SignalR** WebSocket broadcaster, and **MySQL** transactional wallet engine engineered to handle high-concurrency wagering without race conditions, double-spending, or stale odds locks.

Includes a multi-threaded **Python concurrency stress-test suite** proving deterministic ACID isolation under simultaneous load.

---

## Key Engineering Highlights

- **Real-Time Odds Streaming (`SignalR` + `BackgroundService`):** Runs a hosted background worker pushing live market odds shifts every 3 seconds to connected WebSocket clients while persisting updates atomically to MySQL.
- **High-Concurrency Row Locking (`SELECT ... FOR UPDATE`):** Executes balance verification, odds locking, wallet debiting, and bet slip persistence inside explicit ACID transactions to prevent overdrafts under parallel requests.
- **Structured Observability (`Serilog`):** Tracks end-to-end execution latency in milliseconds with unique GUID `CorrelationId` tracing across console and daily rolling JSON logs.
- **Automated Concurrency & Unit Verification:** Verified via both `xUnit` unit tests and a 50-thread Python load script (`blast_bets.py`).

---

## Verified Concurrency Benchmark (`blast_bets.py`)

Fired **50 simultaneous R500.00 bet requests** (`R25,000.00` total concurrent demand) against a single wallet seeded with **`R10,000.00`**:

| Metric | Result |
| :--- | :--- |
| **Concurrent Threads** | `50` simultaneous HTTP POST requests |
| **Total Execution Time** | **`0.74 seconds`** |
| **Accepted Bets (`200 OK`)** | **`20`** (`20 × R500.00 = R10,000.00`) |
| **Rejected Bets (`400 Insufficient Funds`)** | **`30`** (Blocked cleanly by row lock) |
| **Unhandled / Race Errors** | **`0`** |
| **Final Wallet Balance** | **`R0.00`** *(Exact expected balance — zero overdrafts)* |

---

## Tech Stack

- **Backend & Real-Time:** C#, .NET 10 Minimal APIs, ASP.NET Core SignalR, Hosted `BackgroundService`
- **Database:** MySQL 8.0 (`MySqlConnector`, pessimistic row-level locking, parameterized queries)
- **Observability:** Serilog (Console + rolling JSON file sinks)
- **Testing & Load Verification:** xUnit (`LiveOddsWallet.Tests`) & Python (`concurrent.futures` stress tester)

---

## Project Structure

```text
LiveOddsWalletEngine/
├── database/
│   └── schema.sql                     # Normalized MySQL schema & seed data
├── LiveOddsWallet.Api/
│   ├── Hubs/OddsHub.cs                # SignalR real-time WebSocket hub
│   ├── Models/BettingModels.cs        # Immutable DTO records
│   ├── Services/LiveOddsBroadcaster.cs # Background live odds tick engine
│   ├── Program.cs                     # Atomic bet placement & REST endpoints
│   └── LiveOddsWallet.Api.http        # Integration test suite
├── LiveOddsWallet.Tests/
│   └── BettingDomainTests.cs          # Automated xUnit tests
├── stress-test/
│   └── blast_bets.py                  # 50-thread Python concurrency stress test
└── README.md
```

---

## Quick Start

1. Run `database/schema.sql` in MySQL Workbench to create `live_odds_wallet_db`.
2. Add your MySQL connection string to `LiveOddsWallet.Api/appsettings.Development.json`.
3. Run unit tests:
   ```bash
   dotnet test
   ```
4. Launch the API (`dotnet run --project LiveOddsWallet.Api`) and execute the concurrency stress test:
   ```bash
   python stress-test/blast_bets.py
   ```
