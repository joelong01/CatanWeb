# State Management Architecture

**Status:** Draft
**Last Updated:** 2026-01-20

## Executive Summary

A **two-tier storage architecture** that separates generic document management from domain-specific
APIs, with a separate query service for search operations:

1. **Generic Document Service (Go)** - Stores/retrieves documents by primary key
2. **Domain Front Door** - Translates domain APIs (players, games) to document operations
3. **Query Service (Go)** - Handles search/filter, returns PKs for hydration

**Key decisions:**

- **Database:** Azure Cosmos DB (Serverless)
- **Implementation language:** Go (official `azcosmos` SDK)
- **Pattern:** CQRS - storage by PK, query via separate service
- **Environment isolation:** `X-Environment` header routing

## Why Not "Just Use Cosmos"?

| "Just Use Cosmos" | This Architecture |
|-------------------|-------------------|
| Every app reinvents connection management | Solved once in Document Service |
| Schema implicit in code | Type registry with validation |
| Environment isolation per-app | Centralized header routing |
| Cosmos SDK in every service | Simple REST API, any language |
| N services with N connection strings | One service, one credential |
| Query and storage coupled | CQRS - scale independently |

## Architecture Overview

```text
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              Client / GameService                                │
│                                                                                  │
│   "Find players named Alice"              "Get player 123"                       │
│            │                                     │                               │
│            ▼                                     ▼                               │
│   ┌─────────────────┐                   ┌─────────────────┐                     │
│   │  Query Service  │                   │  Domain Front   │                     │
│   │     (Go)        │                   │     Door        │                     │
│   │                 │                   │                 │                     │
│   │ POST /query/v1/ │                   │ /api/v1/players │                     │
│   │   {type}        │                   │ /api/v1/games   │                     │
│   │                 │                   │                 │                     │
│   │ Returns: [PKs]  │                   │    Translates   │                     │
│   └────────┬────────┘                   │        ↓        │                     │
│            │                            │ /docs/v1/{type} │                     │
│            │                            └────────┬────────┘                     │
│            │                                     │                               │
│            │        ┌────────────────────────────┘                               │
│            │        │                                                            │
│            │        ▼                                                            │
│            │  ┌─────────────────────────────────────────────────────────────┐   │
│            │  │            Generic Document Service (Go)                     │   │
│            │  │                                                              │   │
│            │  │  Type Registry │ Policy Engine │ Audit │ Environment Router │   │
│            │  │                                                              │   │
│            │  │  POST   /docs/v1/{type}         (create)                     │   │
│            │  │  GET    /docs/v1/{type}/{pk}    (hydrate)                    │   │
│            │  │  PUT    /docs/v1/{type}/{pk}    (update)                     │   │
│            │  │  DELETE /docs/v1/{type}/{pk}    (delete)                     │   │
│            │  └──────────────────────────┬──────────────────────────────────┘   │
│            │                             │                                       │
│            ▼                             ▼                                       │
│   ┌─────────────────────┐       ┌─────────────────┐                             │
│   │   Search Index      │◀──────│   Cosmos DB     │                             │
│   │ (Elasticsearch /    │ Change│                 │                             │
│   │  Cognitive Search)  │ Feed  │ prod_documents  │                             │
│   └─────────────────────┘       │ staging_docs    │                             │
│                                 │ test_documents  │                             │
│                                 └─────────────────┘                             │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## Component Details

| Component | Purpose | Details |
|-----------|---------|---------|
| Generic Document Service | Store/retrieve documents by PK | See [document-service.md](document-service.md) |
| Query Service | Search/filter, return PKs | See [query-service.md](query-service.md) |
| Domain Front Door | Translate domain API to documents | Thin layer, could be embedded in GameService |

## Base Document Schema

Every document has system fields (underscore prefix) managed by the Document Service:

```json
{
  "_id": "550e8400-e29b-41d4-a716-446655440000",
  "_type": "player",
  "_partitionKey": "player:550e8400-e29b-41d4-a716-446655440000",
  "_version": 3,
  "_etag": "\"0x8D9F2A3B4C5D6E7F\"",
  "_createdAt": "2026-01-20T10:30:00Z",
  "_updatedAt": "2026-01-20T11:45:00Z",
  "_createdBy": "user:alice",
  "_ttl": null,

  "data": {
    "name": "Alice",
    "email": "alice@example.com",
    "wins": 42
  }
}
```

## CQRS Flow

**Storage path (write + read-by-PK):**

```text
Client → Domain Front Door → Document Service → Cosmos DB
                                    ↓
                              (Change Feed)
                                    ↓
                            Query Service Index
```

**Query path (search):**

```text
Client → Query Service → Search Index
           ↓
     Returns PKs
           ↓
Client → Document Service → Cosmos DB (hydrate)
```

## Environment Routing

The `X-Environment` header determines data isolation:

| Header Value | Container | Use Case |
|--------------|-----------|----------|
| `production` | `prod_documents` | Live user data |
| `staging` | `staging_documents` | Pre-production testing |
| `test` | `test_documents` | Automated tests, "Run Test" UI |

**GameService enforces environment based on its own context:**

- Production GameService → forces `production`
- Staging GameService → forces `staging`
- Dev/Test → allows client override

## Implementation Phases

### Phase 1: Generic Document Service (Go)

- Type registry
- Document CRUD by PK only
- Base schema with system fields
- Environment routing
- ETag/version handling
- Audit logging

### Phase 2: Catan Domain Layer

- Thin translation from `/api/v1/players` to `/docs/v1/player`
- Domain validation
- Could be separate service OR embedded in GameService

### Phase 3: Query Service (Go)

- Change Feed consumer
- Search index (start with Cosmos SQL, migrate to Elasticsearch later)
- Query API returning PKs
- Batch hydrate support

### Phase 4: Analytics (Future)

- Change Feed → Azure Fabric OneLake
- Power BI dashboards for game stats
- "Stats" button opens Power BI

## Data Migration

| Data Type | Migration |
|-----------|-----------|
| Players | Export from SQL, import to Cosmos |
| Stats | Export/import |
| Recordings | Import from `Tests/Data/*.catan_test` |
| Games | **None** - ephemeral, no migration needed |

## Related Documents

- [document-service.md](document-service.md) - Generic Document Service specification
- [query-service.md](query-service.md) - Query Service specification
- [deployment.md](deployment.md) - Azure deployment strategy
