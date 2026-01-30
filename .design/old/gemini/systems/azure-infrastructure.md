# Azure Infrastructure As-Built

**Status:** As-Built
**Source:** `catan.ps1` (Deployment Logic)

## 1. Hosting Model

* **Compute**: **Azure App Service** (likely B1 or F1 tier for dev).
  * Hosts the `Catan3.GameService` container/assembly.
  * Runs on Port 8080.
* **Database**: **Azure SQL Database**.
  * Standard SQL connection string injected via Environment Variables.
  * Used for structured metadata and blob storage (via varbinary).

## 2. Configuration

Managed via `appsettings.json` and Environment Overrides in Azure Portal.

* `ConnectionStrings:CatanContext`: Pointing to SQL Azure.
* `SignalR`: Backplane not explicitly configured in `Program.cs` (uses default in-memory), implying a single instance (Scale Out not currently supported without Redis/Azure SignalR Service).

## 3. Client Delivery

* **React UI**: Served as a Next.js application. Likely deployed as a standalone Node app or via Static Web Apps depending on the specific build pipeline (Next.js can do both). *Correction*: `catan.ps1` suggests it runs locally via `dotnet run` or `npm`, but for Azure, it's likely part of the App Service or a separate Static Web App.

## 4. Diagnostics

* **AzureSqlDiagnosticService**: Custom service injected to probe SQL connectivity, likely to handle "cold start" issues common with Serverless Azure SQL or low-tier plans.
