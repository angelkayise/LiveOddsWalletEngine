import concurrent.futures
import time
import urllib.request
import urllib.error
import json

API_URL = "http://localhost:5189/api/bets"
WALLET_URL = "http://localhost:5189/api/wallets/angel_trader"

def place_bet(index):
    payload = json.dumps({
        "username": "angel_trader",
        "eventId": 1,
        "selection": "HOME",
        "stake": 500.00
    }).encode("utf-8")

    req = urllib.request.Request(
        API_URL,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST"
    )
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            return resp.status
    except urllib.error.HTTPError as e:
        return e.code
    except Exception:
        return 500

def main():
    with urllib.request.urlopen(WALLET_URL) as resp:
        start_wallet = json.loads(resp.read().decode())
    print(f"Starting Balance: R{start_wallet['balance']}")

    start_time = time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=50) as executor:
        results = list(executor.map(place_bet, range(50)))
    elapsed = time.perf_counter() - start_time

    accepted = results.count(200)
    rejected = results.count(400)
    errors = len(results) - accepted - rejected

    with urllib.request.urlopen(WALLET_URL) as resp:
        end_wallet = json.loads(resp.read().decode())

    print(f"Completed 50 concurrent R500 bets in {elapsed:.2f}s")
    print(f"Accepted (200 OK): {accepted} | Rejected (400 Insufficient Funds): {rejected} | Errors: {errors}")
    print(f"Final Balance: R{end_wallet['balance']} (Expected: R0.00)")

if __name__ == "__main__":
    main()
