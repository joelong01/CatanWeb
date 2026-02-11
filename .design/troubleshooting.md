# Troubleshooting

**Last verified:** January 30, 2026

## SSL / HTTPS Errors

**Symptom:** `ERR_SSL_PROTOCOL_ERROR` or "Connection is not secure"

**Cause:** GameService runs on **HTTP** (port 8080), not HTTPS. Browsers
may force HTTPS via HSTS or security settings.

**Fix:**

1. Use `http://localhost:8080` -- never `https://`
2. Clear browser HSTS cache:
   - Chrome/Edge: `chrome://net-internals/#hsts` -> Delete domain
     security policies for `localhost`
3. Try incognito/private mode
4. Check "Always use Secure Connections" in browser settings

## Port Conflicts

**Symptom:** "Address already in use" on port 8080 or 3000

**Fix:**

```powershell
# Stop services started by catan.ps1
./catan.ps1 stop

# Or kill dotnet processes directly
Stop-Process -Name "dotnet" -Force

# Check what's using a port (Windows)
netstat -an | findstr :8080
```

## Database Locks

**Symptom:** "SQLite Error 5: database is locked"

**Cause:** Multiple processes accessing `catan.db` simultaneously (e.g.,
Visual Studio + CLI + GameService).

**Fix:**

1. Ensure only one GameService instance is running
2. Stop the app and restart: `./catan.ps1 restart`
3. If persistent: `./catan.ps1 database install` (recreates database)

## SignalR Connection Failures

**Symptom:** "WebSocket connection failed" in browser console

**Cause:** CORS mismatch or network binding issue.

**Fix:**

1. Use `-Network` flag: `./catan.ps1 run -Network` to bind to `0.0.0.0`
2. Check CORS origins in `appsettings.json`
3. Verify GameService is running: `http://localhost:8080/api/database/health`

## React UI Not Updating

**Symptom:** UI shows stale data after code changes

**Fix:**

1. Hard refresh: Ctrl+Shift+R (bypasses browser cache)
2. If hot-reload fails: `./catan.ps1 update`
3. For SVG/board changes: create a new game or restart GameService

## Network Access (Other Devices)

To access the app from phones or other computers on the same network:

```powershell
./catan.ps1 run -Network
```

Then use `http://<your-ip>:8080` from other devices. Find your IP with
`ipconfig` (Windows) or `ifconfig` (macOS).
