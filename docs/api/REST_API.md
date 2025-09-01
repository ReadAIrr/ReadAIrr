# ReadAIrr REST API Documentation

This document provides comprehensive documentation for the ReadAIrr REST API v1.

## Table of Contents
- [API Overview](#api-overview)
- [Authentication](#authentication)
- [Core Resources](#core-resources)
- [API Endpoints](#api-endpoints)
- [Response Formats](#response-formats)
- [Error Handling](#error-handling)
- [Rate Limiting](#rate-limiting)

## API Overview

ReadAIrr exposes a RESTful API for managing ebook libraries, authors, books, downloads, and system configuration.

### Base Configuration
- **Base URL**: `{protocol}://{host}:{port}/api/v1/`
- **Default Port**: `8246`
- **Protocol**: HTTP/HTTPS
- **API Version**: v1
- **Content-Type**: `application/json`
- **License**: GPL-3.0

### OpenAPI Specification
The API is documented using OpenAPI 3.0 specification available at:
- **Development**: `/docs/v1/openapi.json` (debug builds only)
- **Swagger UI**: Available in debug builds

## Authentication

ReadAIrr uses API key authentication with two supported methods:

### API Key Header (Recommended)
```http
X-Api-Key: your-api-key-here
```

### API Key Query Parameter
```http
GET /api/v1/author?apikey=your-api-key-here
```

### Security Schemes
- **X-Api-Key**: API key passed as header
- **apikey**: API key passed as query parameter

## Core Resources

### Author Resource
Represents a book author with metadata and statistics.

```typescript
interface AuthorResource {
  id: number;
  authorName: string;
  authorNameLastFirst: string;
  sortName: string;
  foreignAuthorId: string;
  titleSlug: string;
  overview: string;
  status: AuthorStatusType;
  ended: boolean;
  authorType: string;
  disambiguation: string;
  links: AuthorLinkResource[];
  images: MediaCoverResource[];
  path: string;
  qualityProfileId: number;
  metadataProfileId: number;
  monitored: boolean;
  rootFolderPath: string;
  genres: string[];
  cleanName: string;
  sortNameLastFirst: string;
  lastBook?: BookResource;
  nextBook?: BookResource;
  statistics?: AuthorStatisticsResource;
  books?: BookResource[];
  series?: SeriesResource[];
  tags: number[];
  added: string; // ISO DateTime
  addOptions?: AuthorAddOptions;
}
```

### Book Resource
Represents an individual book with editions and metadata.

```typescript
interface BookResource {
  id: number;
  title: string;
  authorTitle: string;
  seriesTitle?: string;
  disambiguation?: string;
  overview?: string;
  releaseDate?: string; // ISO Date
  images: MediaCoverResource[];
  links: BookLinkResource[];
  genres: string[];
  ratings: RatingResource;
  cleanTitle: string;
  monitored: boolean;
  anyEditionOk: boolean;
  lastSearchTime?: string; // ISO DateTime
  added: string; // ISO DateTime
  addOptions?: BookAddOptions;
  authorId: number;
  foreignBookId: string;
  titleSlug: string;
  grabbed: boolean;
  author: AuthorResource;
  editions: EditionResource[];
  bookFiles: BookFileResource[];
  seriesLinks: SeriesBookLinkResource[];
  statistics?: BookStatisticsResource;
  tags: number[];
}
```

### BookFile Resource
Represents a physical book file on disk.

```typescript
interface BookFileResource {
  id: number;
  authorId: number;
  bookId: number;
  editionId: number;
  path: string;
  size: number;
  dateAdded: string; // ISO DateTime
  releaseGroup?: string;
  quality: QualityResource;
  mediaInfo?: MediaInfoResource;
  originalFilePath?: string;
  qualityCutoffNotMet: boolean;
  tags: number[];
}
```

### Command Resource
Represents a background command/task execution.

```typescript
interface CommandResource {
  id: number;
  name: string;
  commandName: string;
  message?: string;
  priority: CommandPriority;
  status: CommandStatus;
  result?: CommandResult;
  queued: string; // ISO DateTime
  started?: string; // ISO DateTime
  ended?: string; // ISO DateTime
  duration?: string; // TimeSpan
  exception?: string;
  trigger: CommandTrigger;
  clientUserAgent?: string;
  stateChangeTime?: string; // ISO DateTime
  sendUpdatesToClient: boolean;
  updateScheduledTask: boolean;
}
```

## API Endpoints

### Authors (`/api/v1/author`)

| Method | Endpoint | Description | Parameters |
|--------|----------|-------------|------------|
| `GET` | `/author` | Get all authors | - |
| `GET` | `/author/{id}` | Get author by ID | `id: integer` |
| `POST` | `/author` | Add new author | Body: `AuthorResource` |
| `PUT` | `/author/{id}` | Update author | `id: integer`, `moveFiles?: boolean` |
| `DELETE` | `/author/{id}` | Delete author | `id: integer`, `deleteFiles?: boolean`, `addImportListExclusion?: boolean` |

#### Author Sub-resources

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/author/lookup` | Search for authors |
| `POST` | `/author/editor` | Bulk edit authors |

### Books (`/api/v1/book`)

| Method | Endpoint | Description | Parameters |
|--------|----------|-------------|------------|
| `GET` | `/book` | Get books | `authorId?: integer`, `bookIds?: integer[]`, `titleSlug?: string`, `includeAllAuthorBooks?: boolean` |
| `GET` | `/book/{id}` | Get book by ID | `id: integer` |
| `GET` | `/book/{id}/overview` | Get book overview | `id: integer` |
| `POST` | `/book` | Add new book | Body: `BookResource` |
| `PUT` | `/book/{id}` | Update book | `id: integer`, Body: `BookResource` |
| `DELETE` | `/book/{id}` | Delete book | `id: integer`, `deleteFiles?: boolean`, `addImportListExclusion?: boolean` |
| `PUT` | `/book/monitor` | Set book monitoring | Body: `BooksMonitoredResource` |

#### Book Sub-resources

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/book/lookup` | Search for books |
| `POST` | `/book/editor` | Bulk edit books |
| `POST` | `/book/rename` | Rename book files |
| `POST` | `/book/retag` | Retag book files |

### Book Files (`/api/v1/bookfile`)

| Method | Endpoint | Description | Parameters |
|--------|----------|-------------|------------|
| `GET` | `/bookfile` | Get book files | `authorId?: integer`, `bookId?: integer`, `bookFileIds?: integer[]` |
| `GET` | `/bookfile/{id}` | Get book file by ID | `id: integer` |
| `DELETE` | `/bookfile/{id}` | Delete book file | `id: integer` |
| `PUT` | `/bookfile/editor` | Bulk edit book files | Body: `BookFileListResource` |
| `DELETE` | `/bookfile/bulk` | Bulk delete book files | Body: `BookFileListResource` |

### Commands (`/api/v1/command`)

| Method | Endpoint | Description | Parameters |
|--------|----------|-------------|------------|
| `GET` | `/command` | Get all commands | - |
| `GET` | `/command/{id}` | Get command by ID | `id: integer` |
| `POST` | `/command` | Execute command | Body: `CommandResource` |
| `DELETE` | `/command/{id}` | Cancel command | `id: integer` |

#### Available Commands

| Command | Description | Parameters |
|---------|-------------|------------|
| `RefreshAuthor` | Refresh author metadata | `authorId: integer` |
| `RefreshBook` | Refresh book metadata | `bookId?: integer`, `isNewBook?: boolean` |
| `RenameAuthor` | Rename author files | `authorId: integer` |
| `RenameBook` | Rename book files | `bookId: integer` |
| `RetagBook` | Retag book files | `bookId: integer` |
| `SearchMissing` | Search for missing books | - |
| `SearchCutoffUnmet` | Search for cutoff unmet | - |
| `RssSync` | RSS sync | - |
| `ImportListSync` | Import list sync | - |
| `CleanUpRecycleBin` | Clean up recycle bin | - |
| `DeleteLogFiles` | Delete log files | - |
| `DeleteUpdateLogFiles` | Delete update log files | - |
| `Housekeeping` | General housekeeping | - |
| `MessagingCleanup` | Messaging cleanup | - |
| `RefreshMonitoredDownloads` | Refresh monitored downloads | - |
| `ProcessMonitoredDownloads` | Process monitored downloads | - |
| `CheckHealth` | Check system health | - |
| `BackupDatabase` | Backup database | - |

### Queue (`/api/v1/queue`)

| Method | Endpoint | Description | Parameters |
|--------|----------|-------------|------------|
| `GET` | `/queue` | Get download queue | `page?: integer`, `pageSize?: integer`, `sortKey?: string`, `sortDirection?: string`, `includeUnknownAuthorItems?: boolean`, `includeAuthor?: boolean`, `includeBook?: boolean` |
| `GET` | `/queue/{id}` | Get queue item | `id: integer` |
| `DELETE` | `/queue/{id}` | Remove from queue | `id: integer`, `removeFromClient?: boolean`, `blocklist?: boolean` |
| `DELETE` | `/queue/bulk` | Bulk remove from queue | Body: `QueueBulkResource` |
| `GET` | `/queue/details` | Get queue details | `authorId?: integer`, `bookIds?: integer[]`, `includeAuthor?: boolean`, `includeBook?: boolean` |
| `GET` | `/queue/status` | Get queue status | - |

### Calendar (`/api/v1/calendar`)

| Method | Endpoint | Description | Parameters |
|--------|----------|-------------|------------|
| `GET` | `/calendar` | Get calendar events | `start?: date`, `end?: date`, `unmonitored?: boolean`, `premieresOnly?: boolean`, `asAllDay?: boolean`, `tags?: string` |
| `GET` | `/calendar/{id}` | Get calendar event | `id: integer` |

### System (`/api/v1/system`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/system/status` | Get system status |
| `GET` | `/system/health` | Get health check results |
| `GET` | `/system/task` | Get scheduled tasks |
| `GET` | `/system/task/{id}` | Get scheduled task |
| `GET` | `/system/logs` | Get log entries |
| `GET` | `/system/backup` | Get backups |

### Configuration (`/api/v1/config`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/config/host` | Get host configuration |
| `PUT` | `/config/host` | Update host configuration |
| `GET` | `/config/naming` | Get naming configuration |
| `PUT` | `/config/naming` | Update naming configuration |
| `GET` | `/config/mediamanagement` | Get media management config |
| `PUT` | `/config/mediamanagement` | Update media management config |
| `GET` | `/config/ui` | Get UI configuration |
| `PUT` | `/config/ui` | Update UI configuration |

### Download Clients (`/api/v1/downloadclient`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/downloadclient` | Get download clients |
| `GET` | `/downloadclient/{id}` | Get download client |
| `POST` | `/downloadclient` | Add download client |
| `PUT` | `/downloadclient/{id}` | Update download client |
| `DELETE` | `/downloadclient/{id}` | Delete download client |
| `POST` | `/downloadclient/test` | Test download client |
| `GET` | `/downloadclient/schema` | Get download client schemas |

### Indexers (`/api/v1/indexer`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/indexer` | Get indexers |
| `GET` | `/indexer/{id}` | Get indexer |
| `POST` | `/indexer` | Add indexer |
| `PUT` | `/indexer/{id}` | Update indexer |
| `DELETE` | `/indexer/{id}` | Delete indexer |
| `POST` | `/indexer/test` | Test indexer |
| `GET` | `/indexer/schema` | Get indexer schemas |

### Profiles

#### Quality Profiles (`/api/v1/qualityprofile`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/qualityprofile` | Get quality profiles |
| `GET` | `/qualityprofile/{id}` | Get quality profile |
| `POST` | `/qualityprofile` | Add quality profile |
| `PUT` | `/qualityprofile/{id}` | Update quality profile |
| `DELETE` | `/qualityprofile/{id}` | Delete quality profile |

#### Metadata Profiles (`/api/v1/metadataprofile`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/metadataprofile` | Get metadata profiles |
| `GET` | `/metadataprofile/{id}` | Get metadata profile |
| `POST` | `/metadataprofile` | Add metadata profile |
| `PUT` | `/metadataprofile/{id}` | Update metadata profile |
| `DELETE` | `/metadataprofile/{id}` | Delete metadata profile |

#### Delay Profiles (`/api/v1/delayprofile`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/delayprofile` | Get delay profiles |
| `GET` | `/delayprofile/{id}` | Get delay profile |
| `POST` | `/delayprofile` | Add delay profile |
| `PUT` | `/delayprofile/{id}` | Update delay profile |
| `DELETE` | `/delayprofile/{id}` | Delete delay profile |

## Response Formats

### Success Responses

#### Standard Resource Response
```json
{
  "id": 1,
  "resourceField": "value",
  "dateField": "2024-09-01T19:23:34Z",
  "nestedResource": {
    "id": 2,
    "field": "value"
  }
}
```

#### Paginated Response
```json
{
  "page": 1,
  "pageSize": 20,
  "sortKey": "title",
  "sortDirection": "ascending",
  "totalRecords": 150,
  "records": [
    {
      "id": 1,
      "field": "value"
    }
  ]
}
```

#### Created Response (201)
```json
{
  "id": 123
}
```

#### Accepted Response (202)
```json
{
  "id": 123
}
```

### Error Responses

#### Validation Error (400)
```json
{
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "fieldName": ["Field is required", "Field must be valid"]
  }
}
```

#### Not Found (404)
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Resource not found"
}
```

#### Server Error (500)
```json
{
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An error occurred processing your request"
}
```

## Error Handling

### HTTP Status Codes

| Code | Description | Usage |
|------|-------------|-------|
| `200` | OK | Successful GET, PUT operations |
| `201` | Created | Successful POST operations |
| `202` | Accepted | Asynchronous operations accepted |
| `400` | Bad Request | Validation errors, malformed requests |
| `401` | Unauthorized | Missing or invalid API key |
| `403` | Forbidden | Access denied |
| `404` | Not Found | Resource not found |
| `409` | Conflict | Resource conflict (duplicate, dependency) |
| `422` | Unprocessable Entity | Business logic validation errors |
| `429` | Too Many Requests | Rate limit exceeded |
| `500` | Internal Server Error | Server errors |
| `503` | Service Unavailable | System maintenance, overloaded |

### Error Response Format

All error responses follow RFC 7807 Problem Details format:

```json
{
  "type": "https://readairr.com/docs/problems/validation-failed",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred",
  "instance": "/api/v1/author",
  "errors": {
    "authorName": ["Author name is required"],
    "qualityProfileId": ["Quality profile does not exist"]
  }
}
```

## Rate Limiting

ReadAIrr implements rate limiting to prevent API abuse:

- **Default Limit**: 100 requests per minute per API key
- **Burst Limit**: 10 requests per second
- **Headers**: Rate limit information included in response headers

```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1630000000
```

### Rate Limit Exceeded Response

```json
{
  "title": "Rate Limit Exceeded",
  "status": 429,
  "detail": "API rate limit exceeded. Try again later.",
  "retryAfter": 60
}
```

## Request Examples

### Get All Authors
```http
GET /api/v1/author
X-Api-Key: your-api-key
```

### Add New Author
```http
POST /api/v1/author
X-Api-Key: your-api-key
Content-Type: application/json

{
  "authorName": "Stephen King",
  "foreignAuthorId": "3389",
  "qualityProfileId": 1,
  "metadataProfileId": 1,
  "monitored": true,
  "rootFolderPath": "/books"
}
```

### Search for Missing Books
```http
POST /api/v1/command
X-Api-Key: your-api-key
Content-Type: application/json

{
  "name": "SearchMissing"
}
```

### Monitor Books
```http
PUT /api/v1/book/monitor
X-Api-Key: your-api-key
Content-Type: application/json

{
  "bookIds": [1, 2, 3],
  "monitored": true
}
```

This comprehensive REST API provides full programmatic access to ReadAIrr's functionality, enabling integration with external tools, automation scripts, and custom applications.
