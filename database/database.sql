CREATE DATABASE IF NOT EXISTS live_odds_wallet_db;
USE live_odds_wallet_db;

CREATE TABLE wallets (
    wallet_id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    balance DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

CREATE TABLE live_events (
    event_id INT AUTO_INCREMENT PRIMARY KEY,
    match_name VARCHAR(100) NOT NULL,
    home_odds DECIMAL(6, 2) NOT NULL,
    draw_odds DECIMAL(6, 2) NOT NULL,
    away_odds DECIMAL(6, 2) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

CREATE TABLE bets (
    bet_id INT AUTO_INCREMENT PRIMARY KEY,
    correlation_id VARCHAR(36) NOT NULL,
    wallet_id INT NOT NULL,
    event_id INT NOT NULL,
    selection ENUM('HOME', 'DRAW', 'AWAY') NOT NULL,
    stake DECIMAL(18, 2) NOT NULL,
    locked_odds DECIMAL(6, 2) NOT NULL,
    potential_payout DECIMAL(18, 2) NOT NULL,
    status ENUM('ACCEPTED', 'REJECTED') NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (wallet_id) REFERENCES wallets(wallet_id),
    FOREIGN KEY (event_id) REFERENCES live_events(event_id)
);

INSERT INTO wallets (username, balance) VALUES
('angel_trader', 10000.00);

INSERT INTO live_events (match_name, home_odds, draw_odds, away_odds) VALUES
('Sharks vs Stormers (URC)', 1.85, 18.00, 2.10),
('Bafana Bafana vs Nigeria', 2.40, 3.10, 2.95);
