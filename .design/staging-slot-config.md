# Staging Slot Configuration

All configuration required for staging slots to function. If a slot
is deleted and recreated, every item below must be applied.

## GameService (`catan-api` staging slot)

### Runtime Stack

| Setting | Value |
| ------- | ----- |
| `linuxFxVersion` | `DOTNETCORE\|9.0` |
| `startupCommand` | (empty — Oryx auto-detects) |

### Connection Strings

| Name | Type | Value |
| ---- | ---- | ----- |
| `AzureSql` | SQLAzure | `Server=tcp:sql-catan.database.windows.net,1433;Database=catan;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Pooling=True;Min Pool Size=1;Max Pool Size=30;` |

### App Settings

| Name | Value | Notes |
| ---- | ----- | ----- |
| `DATABASE_MODE` | `azure` | Tells app to use Azure SQL, not SQLite |
| `AZURE_STORAGE_ACCOUNT` | `stcatan` | For game data storage |
| `AZURE_STORAGE_CONTAINER` | `data` | Storage container name |
| `WEBSITES_CONTAINER_START_TIME_LIMIT` | `600` | 10 min startup grace period |

### Identity

- System-assigned managed identity must be enabled
- Identity must have `db_datareader`, `db_datawriter`, `db_ddladmin`
  roles on `sql-catan/catan` database (granted as
  `catan-api/slots/staging`)

## React UI (`catan` staging slot)

### Runtime Stack

| Setting | Value |
| ------- | ----- |
| `linuxFxVersion` | `NODE\|22-lts` |
| `startupCommand` | `node server.js` |

### App Settings

| Name | Value | Notes |
| ---- | ----- | ----- |
| `WEBSITE_NODE_DEFAULT_VERSION` | `~22` | Node.js runtime version |
| `NEXT_PUBLIC_GAME_SERVICE_URL` | `https://catan-api-staging.azurewebsites.net` | Points at staging GameService |

### Identity

- No managed identity needed (no database access)

## Settings NOT copied to staging

These are deployment-specific and set automatically by the deploy
script after each push:

- `DEPLOY_COMMIT` — git commit hash of deployed code
- `DEPLOY_BUILD_TIME` — ISO 8601 timestamp of build
- `APPLICATIONINSIGHTS_CONNECTION_STRING` — optional, can be
  added later for monitoring
- `WEBSITE_HTTPLOGGING_RETENTION_DAYS` — set by log config
  command
