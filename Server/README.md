# Shared Lobby Server

This folder contains a minimal PHP backend for real-time shared lobby sync.

## Files

- `lobby_sync.php` - HTTP API endpoint used by Unity (`LobbyRealtimeSync`).
- `lobby_sync_store.json` - created automatically on first request.

## Deploy (XAMPP)

1. Copy `lobby_sync.php` into your web root, for example:
   - `C:\xampp\htdocs\Game\3Dminer\lobby_sync.php`
2. Start Apache in XAMPP.
3. Open in browser to verify endpoint exists:
   - `http://127.0.0.1/Game/3Dminer/lobby_sync.php` (POST only, GET returns method error).
4. In Unity `LobbyEditor` set:
   - `Enable Shared Lobby Sync` = true
   - `Shared Lobby Endpoint` = `https://your-domain/lobby_sync.php`
   - `Shared Lobby Room Id` = `global_lobby` (or any room key)

## Production notes

- Use HTTPS for Yandex Games production.
- Keep CORS enabled for your game domain.
- This is a simple file-based demo backend; for high traffic use a database.
