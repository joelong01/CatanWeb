# Generic Document Service

**Status:** Draft
**Last Updated:** 2026-01-20

## Overview

A **generic, reusable document storage service** implemented in Go that provides:

- Document CRUD by primary key (no queries - that's the Query Service's job)
- Type registration with optional schema validation
- Environment-based data isolation
- Optimistic concurrency via ETags
- Audit logging for all operations
- Policy enforcement at a single chokepoint

## Why This Service?

See [state.md](state.md) for the full rationale. In short: this service solves the "every app
reinvents Cosmos access" problem by providing a managed, policy-enforced abstraction.

## Technology Stack

| Component | Technology |
|-----------|------------|
| Language | Go |
| Cosmos SDK | `github.com/Azure/azure-sdk-for-go/sdk/data/azcosmos` |
| HTTP Router | `chi` or `gin` |
| JSON Schema | `github.com/santhosh-tekuri/jsonschema` |
| Logging | `slog` (structured) |
| Config | Environment variables |

## API Specification

Base URL: `https://storage.internal/docs/v1`

### Versioning

All endpoints are versioned via URL path (`/docs/v1/...`). See [state.md](state.md) for versioning
rules.

### Authentication

Internal service - no public access. Authentication via:

- Azure Managed Identity (service-to-service)
- API key for local development

### Common Headers

| Header | Required | Description |
|--------|----------|-------------|
| `X-Environment` | Yes | `production`, `staging`, or `test` |
| `X-Request-Id` | No | Correlation ID for tracing |
| `If-Match` | For PUT | ETag for optimistic concurrency |

### Type Registration

Register a document type before storing documents of that type.

```text
POST /docs/v1/types
```

**Request:**

```json
{
  "name": "player",
  "description": "Catan player profile",
  "partitionKeyPattern": "{type}:{id}",
  "ttlSeconds": null,
  "schema": {
    "type": "object",
    "required": ["name"],
    "properties": {
      "name": { "type": "string", "maxLength": 100 },
      "email": { "type": "string", "format": "email" },
      "wins": { "type": "integer", "minimum": 0 }
    }
  },
  "policies": {
    "maxDocumentSizeBytes": 102400,
    "maxDocumentsPerEnvironment": 100000
  }
}
```

**Response:** `201 Created`

```json
{
  "name": "player",
  "createdAt": "2026-01-20T10:30:00Z",
  "version": 1
}
```

**List types:**

```text
GET /docs/v1/types
```

**Get type:**

```text
GET /docs/v1/types/{name}
```

### Document Operations

#### Create Document

```text
POST /docs/v1/{type}
```

**Request:**

```json
{
  "data": {
    "name": "Alice",
    "email": "alice@example.com",
    "wins": 42
  }
}
```

**Response:** `201 Created`

```json
{
  "_id": "550e8400-e29b-41d4-a716-446655440000",
  "_type": "player",
  "_partitionKey": "player:550e8400-e29b-41d4-a716-446655440000",
  "_version": 1,
  "_etag": "\"0x8D9F2A3B4C5D6E7F\"",
  "_createdAt": "2026-01-20T10:30:00Z",
  "_updatedAt": "2026-01-20T10:30:00Z",
  "_createdBy": "system",
  "_ttl": null,
  "data": {
    "name": "Alice",
    "email": "alice@example.com",
    "wins": 42
  }
}
```

**Headers:**

- `ETag: "0x8D9F2A3B4C5D6E7F"`
- `Location: /docs/v1/player/550e8400-e29b-41d4-a716-446655440000`

#### Hydrate Document (Read by PK)

```text
GET /docs/v1/{type}/{pk}
```

**Response:** `200 OK` with document JSON and `ETag` header.

**Not found:** `404 Not Found`

#### Update Document

```text
PUT /docs/v1/{type}/{pk}
```

**Required header:** `If-Match: "{etag}"`

**Request:**

```json
{
  "data": {
    "name": "Alice Updated",
    "email": "alice@example.com",
    "wins": 43
  }
}
```

**Response:** `200 OK` with updated document.

**Conflict (ETag mismatch):** `412 Precondition Failed`

```json
{
  "error": "precondition_failed",
  "message": "Document was modified by another request",
  "currentEtag": "\"0x8D9F2A3B4C5D6E8F\""
}
```

#### Delete Document

```text
DELETE /docs/v1/{type}/{pk}
```

**Optional header:** `If-Match: "{etag}"` (for conditional delete)

**Response:** `204 No Content`

#### Batch Hydrate

```text
POST /docs/v1/{type}/batch
```

**Request:**

```json
{
  "pks": [
    "player:abc123",
    "player:def456",
    "player:ghi789"
  ]
}
```

**Response:** `200 OK`

```json
{
  "documents": [
    { "_id": "abc123", "_type": "player", "data": { ... } },
    { "_id": "def456", "_type": "player", "data": { ... } }
  ],
  "notFound": ["player:ghi789"]
}
```

**Limits:** Max 100 PKs per batch request.

### Health

```text
GET /health
```

**Response:** `200 OK`

```json
{
  "status": "healthy",
  "cosmos": "connected",
  "version": "1.0.0",
  "environment": "production"
}
```

### Admin Operations

```text
POST /docs/v1/admin/environments/{env}/seed
DELETE /docs/v1/admin/environments/{env}/clear
```

**Note:** These require additional authentication and are disabled for `production`.

## Base Document Schema

Every document stored has this structure:

| Field | Type | Description |
|-------|------|-------------|
| `_id` | string | UUID v4, auto-generated |
| `_type` | string | Registered type name |
| `_partitionKey` | string | Cosmos partition key (from pattern) |
| `_version` | integer | Incremented on each update |
| `_etag` | string | Cosmos ETag for concurrency |
| `_createdAt` | datetime | ISO 8601 timestamp |
| `_updatedAt` | datetime | ISO 8601 timestamp |
| `_createdBy` | string | Identity that created document |
| `_ttl` | integer | Time-to-live in seconds (null = forever) |
| `data` | object | Domain-specific payload |

## Type Registry

The type registry stores metadata about each document type:

```json
{
  "name": "player",
  "description": "Catan player profile",
  "partitionKeyPattern": "{type}:{id}",
  "ttlSeconds": null,
  "schema": { ... },
  "policies": {
    "maxDocumentSizeBytes": 102400,
    "maxDocumentsPerEnvironment": 100000
  },
  "createdAt": "2026-01-20T10:00:00Z",
  "version": 1
}
```

**Partition key patterns:**

| Pattern | Example Result |
|---------|----------------|
| `{type}:{id}` | `player:abc123` |
| `{id}` | `abc123` |
| `{data.tenantId}` | Extract from data field |

## Policy Engine

Policies are enforced on every operation:

| Policy | Description | Enforcement |
|--------|-------------|-------------|
| Schema validation | Validate `data` against JSON Schema | On create/update |
| Size limits | Reject documents over max size | On create/update |
| Document count | Reject if environment exceeds limit | On create |
| TTL | Set Cosmos TTL for auto-expiry | On create |
| Rate limiting | Per-type, per-environment limits | On all operations |

## Audit Logging

Every operation is logged with:

```json
{
  "timestamp": "2026-01-20T10:30:00Z",
  "operation": "create",
  "type": "player",
  "documentId": "abc123",
  "environment": "production",
  "requestId": "req-xyz",
  "principal": "gameservice-managed-identity",
  "durationMs": 45,
  "statusCode": 201
}
```

Logs are written to:

- Azure Monitor (structured logs)
- Optionally: Event Hub for real-time analytics

## Environment Routing

Based on `X-Environment` header, operations are routed to different Cosmos containers:

| Environment | Container |
|-------------|-----------|
| `production` | `prod_documents` |
| `staging` | `staging_documents` |
| `test` | `test_documents` |

**Missing header:** `400 Bad Request`
**Invalid header:** `400 Bad Request`

## Error Responses

All errors follow this format:

```json
{
  "error": "error_code",
  "message": "Human readable message",
  "details": { ... }
}
```

| Status | Error Code | Description |
|--------|------------|-------------|
| 400 | `bad_request` | Invalid request body or headers |
| 400 | `validation_failed` | Schema validation failed |
| 404 | `not_found` | Document or type not found |
| 409 | `conflict` | Document already exists |
| 412 | `precondition_failed` | ETag mismatch |
| 429 | `rate_limited` | Too many requests |
| 500 | `internal_error` | Server error |

## Configuration

Environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `COSMOS_ENDPOINT` | Cosmos DB endpoint URL | Required |
| `COSMOS_KEY` | Cosmos DB key (dev only) | - |
| `COSMOS_DATABASE` | Database name | `catan` |
| `PORT` | HTTP port | `8080` |
| `LOG_LEVEL` | `debug`, `info`, `warn`, `error` | `info` |
| `ALLOWED_ENVIRONMENTS` | Comma-separated list | `production,staging,test` |

## Cosmos DB Structure

```text
Cosmos DB Account: catan-cosmos
└── Database: catan
    ├── prod_documents      (partition key: /_partitionKey)
    ├── staging_documents   (partition key: /_partitionKey)
    ├── test_documents      (partition key: /_partitionKey)
    └── _types              (partition key: /name) - type registry
```

## Local Development

**With Cosmos DB Emulator:**

```bash
# Start emulator (Docker)
docker run -p 8081:8081 -p 10251-10254:10251-10254 \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator

# Run service
COSMOS_ENDPOINT=https://localhost:8081 \
COSMOS_KEY=<emulator-key> \
go run ./cmd/document-service
```

**With in-memory store (testing):**

```bash
STORAGE_BACKEND=memory go run ./cmd/document-service
```

## Project Structure

```text
storage-service/
├── cmd/
│   └── document-service/
│       └── main.go
├── internal/
│   ├── api/
│   │   ├── handlers.go
│   │   ├── middleware.go
│   │   └── routes.go
│   ├── cosmos/
│   │   └── client.go
│   ├── document/
│   │   ├── service.go
│   │   └── schema.go
│   ├── policy/
│   │   └── engine.go
│   ├── registry/
│   │   └── types.go
│   └── audit/
│       └── logger.go
├── pkg/
│   └── models/
│       └── document.go
├── go.mod
└── go.sum
```

## Implementation Checklist

- [ ] Project setup with Go modules
- [ ] Cosmos DB client wrapper
- [ ] Type registry (in-memory → Cosmos)
- [ ] Document CRUD endpoints
- [ ] Environment routing middleware
- [ ] ETag/version handling
- [ ] JSON Schema validation
- [ ] Audit logging
- [ ] Batch hydrate endpoint
- [ ] Health endpoint
- [ ] Docker build
- [ ] Azure Container Apps deployment
- [ ] Integration tests
