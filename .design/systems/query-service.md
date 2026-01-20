# Query Service

**Status:** Draft
**Last Updated:** 2026-01-20

## Overview

A **search and query service** implemented in Go that provides:

- Full-text and structured search across documents
- Returns primary keys (PKs) only - clients hydrate via Document Service
- Keeps index in sync via Cosmos DB Change Feed
- Scales independently from storage

## Why Separate Query from Storage?

| Coupled (anti-pattern) | Separated (CQRS) |
|------------------------|------------------|
| Complex queries slow down writes | Storage is O(1) PK lookups |
| Single scaling dimension | Scale search independently |
| Cosmos RU/s for everything | Purpose-built search index |
| Limited query capabilities | Full-text, facets, geo, etc. |

## Technology Stack

| Component | Technology |
|-----------|------------|
| Language | Go |
| Search Backend | Phase 1: Cosmos SQL, Phase 2: Elasticsearch/Azure Cognitive Search |
| Change Feed | Azure Function or embedded consumer |
| HTTP Router | `chi` or `gin` |
| Logging | `slog` (structured) |

## API Specification

Base URL: `https://query.internal/query/v1`

### Query Documents

```text
POST /query/v1/{type}
```

**Request:**

```json
{
  "filter": {
    "data.name": { "$contains": "Alice" },
    "data.wins": { "$gte": 10 }
  },
  "sort": [
    { "data.wins": "desc" },
    { "_createdAt": "asc" }
  ],
  "limit": 20,
  "offset": 0
}
```

**Response:** `200 OK`

```json
{
  "pks": [
    "player:abc123",
    "player:def456",
    "player:ghi789"
  ],
  "total": 142,
  "hasMore": true
}
```

**Note:** Response contains PKs only. Client calls Document Service to hydrate.

### Filter Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `$eq` | Equals (default) | `{ "data.name": "Alice" }` |
| `$ne` | Not equals | `{ "data.status": { "$ne": "deleted" } }` |
| `$gt` | Greater than | `{ "data.wins": { "$gt": 10 } }` |
| `$gte` | Greater than or equal | `{ "data.wins": { "$gte": 10 } }` |
| `$lt` | Less than | `{ "data.wins": { "$lt": 100 } }` |
| `$lte` | Less than or equal | `{ "data.wins": { "$lte": 100 } }` |
| `$in` | In array | `{ "data.status": { "$in": ["active", "pending"] } }` |
| `$contains` | String contains | `{ "data.name": { "$contains": "ali" } }` |
| `$startsWith` | String starts with | `{ "data.email": { "$startsWith": "alice" } }` |
| `$and` | Logical AND | `{ "$and": [{ ... }, { ... }] }` |
| `$or` | Logical OR | `{ "$or": [{ ... }, { ... }] }` |

### Count Documents

```text
GET /query/v1/{type}/count?filter=...
```

Or via POST:

```text
POST /query/v1/{type}/count
```

**Request:**

```json
{
  "filter": {
    "data.wins": { "$gte": 10 }
  }
}
```

**Response:**

```json
{
  "count": 142
}
```

### Faceted Search

```text
POST /query/v1/{type}/facets
```

**Request:**

```json
{
  "filter": {
    "data.status": "active"
  },
  "facets": ["data.region", "data.tier"]
}
```

**Response:**

```json
{
  "facets": {
    "data.region": [
      { "value": "us-west", "count": 45 },
      { "value": "us-east", "count": 32 },
      { "value": "eu-west", "count": 28 }
    ],
    "data.tier": [
      { "value": "premium", "count": 60 },
      { "value": "standard", "count": 45 }
    ]
  },
  "total": 105
}
```

### Health

```text
GET /health
```

**Response:**

```json
{
  "status": "healthy",
  "indexLag": "2s",
  "lastSync": "2026-01-20T10:30:00Z",
  "version": "1.0.0"
}
```

## Change Feed Synchronization

The Query Service index is kept in sync with Cosmos DB via Change Feed:

```text
┌─────────────┐     ┌──────────────┐     ┌─────────────────┐
│  Cosmos DB  │────▶│ Change Feed  │────▶│  Query Service  │
│             │     │  Consumer    │     │     Index       │
└─────────────┘     └──────────────┘     └─────────────────┘
```

### Change Feed Consumer

#### Option A: Azure Function

```text
Cosmos DB ──trigger──▶ Azure Function ──HTTP──▶ Query Service /index endpoint
```

#### Option B: Embedded Consumer

Query Service runs its own Change Feed processor:

```go
processor, _ := container.NewChangeFeedProcessor("query-service", ...)
processor.Start()
```

### Index Update Flow

1. Document created/updated/deleted in Cosmos DB
2. Change Feed delivers change event
3. Consumer extracts indexed fields
4. Consumer upserts/deletes in search index
5. Index is eventually consistent (typically < 5 seconds)

### Indexed Fields

By default, these fields are indexed:

| Field | Indexed As |
|-------|------------|
| `_id` | Keyword |
| `_type` | Keyword |
| `_createdAt` | Date |
| `_updatedAt` | Date |
| `data.*` | Dynamic (based on type registration) |

Type registration can specify additional indexed fields:

```json
{
  "name": "player",
  "indexes": [
    { "field": "data.name", "type": "text" },
    { "field": "data.email", "type": "keyword" },
    { "field": "data.wins", "type": "integer" }
  ]
}
```

## Search Backend Options

### Phase 1: Cosmos SQL Queries

For initial implementation, use Cosmos DB's SQL query capability:

```sql
SELECT c._partitionKey FROM c
WHERE c._type = 'player'
  AND CONTAINS(c.data.name, 'Alice')
  AND c.data.wins >= 10
ORDER BY c.data.wins DESC
OFFSET 0 LIMIT 20
```

**Pros:** Simple, no additional infrastructure
**Cons:** Limited full-text search, cross-partition queries expensive

### Phase 2: Elasticsearch / Azure Cognitive Search

For advanced search needs:

```json
{
  "query": {
    "bool": {
      "must": [
        { "match": { "data.name": "Alice" } },
        { "range": { "data.wins": { "gte": 10 } } }
      ]
    }
  }
}
```

**Pros:** Full-text search, facets, geo queries, relevance scoring
**Cons:** Additional infrastructure, sync complexity

## Environment Routing

Like Document Service, queries are environment-scoped:

| Header | Index |
|--------|-------|
| `X-Environment: production` | `prod_index` |
| `X-Environment: staging` | `staging_index` |
| `X-Environment: test` | `test_index` |

## Error Responses

| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `bad_request` | Invalid query syntax |
| 400 | `unknown_field` | Querying non-indexed field |
| 404 | `type_not_found` | Unknown document type |
| 429 | `rate_limited` | Too many queries |
| 503 | `index_unavailable` | Index not ready |

## Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `COSMOS_ENDPOINT` | Cosmos DB endpoint | Required |
| `INDEX_BACKEND` | `cosmos` or `elasticsearch` | `cosmos` |
| `ELASTICSEARCH_URL` | Elasticsearch endpoint | - |
| `CHANGE_FEED_MODE` | `function` or `embedded` | `embedded` |
| `PORT` | HTTP port | `8081` |

## Project Structure

```text
query-service/
├── cmd/
│   └── query-service/
│       └── main.go
├── internal/
│   ├── api/
│   │   ├── handlers.go
│   │   └── routes.go
│   ├── index/
│   │   ├── cosmos.go      # Cosmos SQL backend
│   │   └── elastic.go     # Elasticsearch backend
│   ├── changefeed/
│   │   └── consumer.go
│   └── query/
│       ├── parser.go      # Parse filter DSL
│       └── builder.go     # Build backend query
├── go.mod
└── go.sum
```

## Implementation Checklist

- [ ] Project setup with Go modules
- [ ] Query DSL parser
- [ ] Cosmos SQL query builder (Phase 1)
- [ ] Query endpoint
- [ ] Count endpoint
- [ ] Change Feed consumer (embedded)
- [ ] Environment routing
- [ ] Health endpoint with index lag
- [ ] Docker build
- [ ] Integration tests
- [ ] (Phase 2) Elasticsearch backend
- [ ] (Phase 2) Faceted search
- [ ] (Phase 2) Azure Function change feed consumer

## Client Usage Example

```typescript
// TypeScript client example

// 1. Search for players
const searchResult = await queryService.query("player", {
  filter: { "data.name": { $contains: "Alice" } },
  sort: [{ "data.wins": "desc" }],
  limit: 10
});

// searchResult = { pks: ["player:abc", "player:def"], total: 42 }

// 2. Hydrate from Document Service
const players = await documentService.batchHydrate("player", searchResult.pks);

// players = [{ _id: "abc", data: { name: "Alice", ... } }, ...]
```
