# Troubleshooting Guide As-Built

**Status:** As-Built
**Source:** `Docs/troubleshoot-ssl-errors.md`

## 1. SSL / HTTPS Errors
**Symptom**: `ERR_SSL_PROTOCOL_ERROR` or "Connection is not secure".
**Cause**: The GameService runs on **HTTP** (Port 8080) by default to simulate a simple local LAN environment, but modern browsers force HTTPS.
**Fix**:
*   Always use `http://localhost:8080`, never `https`.
*   Check "Always use Secure Connections" settings in your browser.
*   In Chrome: `chrome://net-internals/#hsts` -> Delete domain security policies for `localhost`.

## 2. Port Conflicts
**Symptom**: "Address already in use".
**Cause**: Another service is using port 8080 or 3000 (React).
**Fix**:
*   Stop other `dotnet` processes: `Stop-Process -Name "dotnet" -Force`
*   Use `./catan.ps1 stop` to kill background jobs started by the script.

## 3. Database Locks
**Symptom**: "SQLite Error 5: database is locked".
**Cause**: Multiple processes (Visual Studio + CLI + GameService) accessing the `catan.db`.
**Fix**:
*   Ensure only one instance of GameService is running.
*   Stop the app and run `./catan.ps1 clean` (if configured) or manually delete `app.db`.

## 4. SignalR Connection Failures
**Symptom**: "WebSocket connection failed".
**Cause**: CORS mismatch or Network binding issue.
**Fix**:
*   Run with `-Network` flag: `./catan.ps1 run -Network` to bind to `0.0.0.0` instead of `localhost`.
*   Check browser console for CORS errors. Valid origins are defined in `appsettings.json`.
