# ReadAIrr Media Processing & External Integrations

This document outlines ReadAIrr's media processing capabilities and external system integrations.

## Table of Contents
- [Supported Media Formats](#supported-media-formats)
- [Media Processing Workflow](#media-processing-workflow)
- [Quality Detection System](#quality-detection-system)
- [External Integrations](#external-integrations)
- [Tag Processing System](#tag-processing-system)
- [Download Client Integration](#download-client-integration)
- [Calibre Integration](#calibre-integration)

## Supported Media Formats

ReadAIrr supports a wide range of ebook and audiobook formats with comprehensive metadata extraction and management capabilities.

### Format Support Matrix

| Format | Extension | Read Tags | Write Tags | Quality Detection | Calibre Support | Notes |
|--------|-----------|-----------|------------|-------------------|-----------------|-------|
| **EPUB** | `.epub`, `.kepub` | ✅ | ✅ | ✅ | ✅ | Full metadata support via VersOne.Epub |
| **AZW3** | `.azw3` | ✅ | ✅ | ✅ | ✅ | Amazon format with DRM-free support |
| **MOBI** | `.mobi` | ✅ | ✅ | ✅ | ✅ | Legacy Kindle format |
| **PDF** | `.pdf` | ✅ | ❌ | ✅ | ✅ | Limited metadata via PdfSharpCore |
| **TXT** | `.txt` | ❌ | ❌ | ✅ | ✅ | Plain text format |
| **RTF** | `.rtf` | ❌ | ❌ | ✅ | ✅ | Rich text format |
| **HTML** | `.html`, `.htm` | ❌ | ❌ | ✅ | ✅ | Web format support |
| **LIT** | `.lit` | ❌ | ❌ | ✅ | ⚠️ | Microsoft Reader (legacy) |
| **PDB** | `.pdb` | ❌ | ❌ | ✅ | ⚠️ | Palm Database format |

### Quality Hierarchy

ReadAIrr uses a quality hierarchy system for format preferences:

```mermaid
graph TD
    A[Unknown/Undefined] --> B[TXT - Plain Text]
    B --> C[HTML/RTF - Formatted Text]
    C --> D[PDF - Portable Document]
    D --> E[LIT/PDB - Legacy eBook]
    E --> F[MOBI - Kindle Legacy]
    F --> G[AZW3 - Kindle Modern]
    G --> H[EPUB - Standard eBook]
    
    style A fill:#ff6b6b
    style B fill:#ffa500
    style C fill:#ffff00
    style D fill:#9acd32
    style E fill:#87ceeb
    style F fill:#dda0dd
    style G fill:#98fb98
    style H fill:#90ee90
```

## Media Processing Workflow

### Book Import Process

```mermaid
sequenceDiagram
    participant DC as Download Client
    participant RA as ReadAIrr Core
    participant ETS as EBookTagService
    participant CP as CalibreProxy
    participant FS as File System
    participant DB as Database
    
    DC->>RA: Download completed
    RA->>FS: Scan downloaded file
    RA->>ETS: ReadTags(file)
    ETS->>ETS: Detect format (.epub/.pdf/.azw3)
    
    alt EPUB Format
        ETS->>ETS: VersOne.Epub.Read()
        ETS->>RA: Return ParsedTrackInfo
    else PDF Format
        ETS->>ETS: PdfReader.Open()
        ETS->>RA: Return ParsedTrackInfo
    else AZW3/MOBI Format
        ETS->>ETS: Azw3File.Parse()
        ETS->>RA: Return ParsedTrackInfo
    else Unknown Format
        ETS->>ETS: Parser.ParseTitle()
        ETS->>RA: Return basic info
    end
    
    RA->>RA: Match to Book/Edition
    RA->>FS: Move to library location
    RA->>DB: Update BookFile record
    RA->>CP: WriteTags(if configured)
    RA->>RA: PublishEvent(BookImportedEvent)
```

### Tag Synchronization Process

```mermaid
sequenceDiagram
    participant UI as User Interface
    participant BS as BookService
    participant ETS as EBookTagService
    participant CP as CalibreProxy
    participant RF as RootFolder
    participant FS as File System
    
    UI->>BS: Retag Books Request
    BS->>ETS: WriteTags(bookFile, updateCovers, embedMetadata)
    ETS->>ETS: Check if CalibreId exists
    
    alt Has CalibreId
        ETS->>RF: GetBestRootFolder(path)
        ETS->>CP: SetFields(bookFile, settings, updateCovers, embedMetadata)
        CP->>FS: Update metadata in Calibre library
        CP->>ETS: Success response
    else No CalibreId
        ETS->>ETS: Log skip message
    end
    
    ETS->>BS: Operation complete
    BS->>UI: Updated book information
```

## Quality Detection System

### Quality Detection Sources

1. **TagLib Detection** - Primary method using embedded metadata
2. **Extension Detection** - Fallback based on file extension
3. **Content Analysis** - Deep inspection for ambiguous files

### Detection Implementation

```typescript
// Quality detection logic from EBookTagService.cs
public ParsedTrackInfo ReadTags(IFileInfo file)
{
    var extension = file.Extension.ToLower();
    
    switch (extension)
    {
        case ".pdf":
            return ReadPdf(file.FullName);
        case ".epub":
        case ".kepub":
            return ReadEpub(file.FullName);
        case ".azw3":
        case ".mobi":
            return ReadAzw3(file.FullName);
        default:
            return Parser.Parser.ParseTitle(file.FullName);
    }
}
```

### Quality Definitions

| Quality | Typical Size | Use Case | Priority |
|---------|-------------|----------|----------|
| **EPUB** | 1-5 MB | Standard ebooks, best compatibility | Highest |
| **AZW3** | 1-8 MB | Kindle-optimized with enhanced features | High |
| **MOBI** | 2-6 MB | Legacy Kindle devices | Medium |
| **PDF** | 5-50 MB | Fixed layout, academic texts | Medium |
| **TXT** | 100KB-1MB | Plain text, minimal formatting | Low |

## External Integrations

### Calibre Content Server Integration

ReadAIrr provides comprehensive integration with Calibre Content Server for professional ebook management.

#### Calibre Features
- **Library Management** - Add/remove books from Calibre database
- **Format Conversion** - Automatic format conversion via Calibre
- **Metadata Sync** - Bidirectional metadata synchronization
- **Cover Management** - Cover image updates and synchronization
- **Custom Columns** - Support for Calibre custom columns
- **Virtual Libraries** - Integration with Calibre virtual library system

#### Configuration Options
```json
{
  "host": "calibre.example.com",
  "port": 8080,
  "username": "readairr",
  "password": "secure_password",
  "library": "Main Library",
  "urlBase": "/calibre",
  "useSsl": true,
  "outputFormat": "EPUB",
  "outputProfile": "tablet",
  "convertToFormat": true
}
```

### Download Client Integration

ReadAIrr supports multiple download clients for automated book acquisition.

#### Supported Download Clients

| Client | Protocol | Features | Status |
|--------|----------|----------|--------|
| **SABnzbd** | Usenet | Categories, priorities, post-processing | ✅ Full |
| **NZBGet** | Usenet | Scripting, categories, RSS | ✅ Full |
| **qBittorrent** | BitTorrent | WebUI API, categories, tags | ✅ Full |
| **Deluge** | BitTorrent | WebUI/Daemon API, labels | ✅ Full |
| **rTorrent** | BitTorrent | XMLRPC API, custom ratios | ✅ Full |
| **Transmission** | BitTorrent | RPC API, bandwidth management | ✅ Full |
| **uTorrent** | BitTorrent | WebAPI, labels, priorities | ⚠️ Limited |
| **Download Station** | Both | Synology NAS integration | ✅ Full |

#### Download Client Workflow

```mermaid
flowchart TD
    A[Release Found] --> B{Download Client Available?}
    B -->|Yes| C[Send to Download Client]
    B -->|No| D[Queue for Later]
    
    C --> E[Monitor Download Progress]
    E --> F{Download Complete?}
    F -->|Yes| G[Import to Library]
    F -->|Failed| H[Retry or Blacklist]
    
    G --> I[Update Metadata]
    I --> J[Trigger Calibre Sync]
    J --> K[Notify User]
    
    H --> L{Retry Attempts Left?}
    L -->|Yes| C
    L -->|No| M[Mark as Failed]
```

### Metadata Provider Integration

#### Primary Metadata Sources
- **Goodreads** - Primary source for book metadata
- **Amazon** - ASIN-based lookups for Kindle content
- **Google Books** - ISBN-based metadata
- **Open Library** - Open-source book database
- **WorldCat** - Library catalog integration

#### Metadata Refresh Process

```mermaid
sequenceDiagram
    participant S as Scheduler
    participant AS as AuthorService
    participant MP as MetadataProvider
    participant DB as Database
    participant ES as EventSystem
    
    S->>AS: Refresh Author Metadata
    AS->>MP: Request updated author info
    MP->>MP: Query external APIs
    MP->>AS: Return updated metadata
    
    AS->>DB: Update author information
    AS->>DB: Update book information
    AS->>ES: PublishEvent(AuthorUpdatedEvent)
    
    ES->>AS: Trigger book refresh
    AS->>MP: Request book metadata
    MP->>AS: Return book details
    AS->>DB: Update book records
    AS->>ES: PublishEvent(BookUpdatedEvent)
```

### Indexer Integration

ReadAIrr supports both Usenet indexers and BitTorrent trackers.

#### Indexer Types

| Type | Protocol | Authentication | Features |
|------|----------|----------------|-----------|
| **Newznab** | Usenet | API Key | RSS, Search, Categories |
| **Torznab** | BitTorrent | API Key | RSS, Search, Categories |
| **Generic Torrent** | BitTorrent | None/Cookie | RSS feeds only |
| **Private Trackers** | BitTorrent | Passkey/Cookie | Enhanced metadata |

#### Search Priority System

```mermaid
graph LR
    A[Search Request] --> B{Check Indexer Priority}
    B --> C[Priority 1 - High]
    B --> D[Priority 2 - Medium] 
    B --> E[Priority 3 - Low]
    
    C --> F[Execute Search]
    D --> F
    E --> F
    
    F --> G{Results Found?}
    G -->|Yes| H[Process Results]
    G -->|No| I[Try Next Priority Level]
    
    I --> D
    I --> E
    I --> J[Search Complete]
    
    H --> K[Filter by Quality]
    K --> L[Apply Release Profiles]
    L --> M[Send to Download Client]
```

## Tag Processing System

### Metadata Tag Mapping

ReadAIrr maintains a comprehensive mapping between internal metadata and external format specifications.

#### EPUB Metadata Mapping

| ReadAIrr Field | EPUB DC Element | EPUB Meta Property | Notes |
|----------------|-----------------|-------------------|-------|
| `Title` | `dc:title` | - | Primary title |
| `Authors` | `dc:creator` | - | Creator list |
| `Publisher` | `dc:publisher` | - | Publisher name |
| `Language` | `dc:language` | - | Language code |
| `Isbn` | `dc:identifier` | `scheme="isbn"` | ISBN identification |
| `Asin` | `dc:identifier` | `scheme="asin"` | Amazon ASIN |
| `SeriesTitle` | - | `calibre:series` | Series information |
| `SeriesIndex` | - | `calibre:series_index` | Series position |
| `ReleaseDate` | `dc:date` | - | Publication date |

#### PDF Metadata Mapping

| ReadAIrr Field | PDF Info Key | XMP Property | Notes |
|----------------|--------------|--------------|-------|
| `Title` | `Title` | `dc:title` | Document title |
| `Authors` | `Author` | `dc:creator` | Creator information |
| `Subject` | `Subject` | `dc:subject` | Document subject |
| `Keywords` | `Keywords` | `pdf:Keywords` | Keyword tags |
| `Creator` | `Creator` | `xmp:CreatorTool` | Creation application |
| `Producer` | `Producer` | `pdf:Producer` | PDF generator |

### Tag Write Operations

ReadAIrr supports three tag writing modes:

1. **Never** - No tag writing (preserve original metadata)
2. **New Downloads Only** - Tag newly imported files only
3. **All Files (Sync)** - Keep all files synchronized with database

#### Tag Write Configuration

```mermaid
graph TD
    A[Book Import/Update] --> B{Write Tags Enabled?}
    B -->|No| C[Skip Tag Writing]
    B -->|Yes| D{New Download?}
    
    D -->|Yes| E{Mode: New Only?}
    D -->|No| F{Mode: Sync All?}
    
    E -->|Yes| G[Write Tags]
    E -->|No| H[Skip Writing]
    
    F -->|Yes| G
    F -->|No| H
    
    G --> I{Update Covers?}
    I -->|Yes| J[Update Cover Art]
    I -->|No| K[Complete]
    
    J --> K
    H --> K
    C --> K
```

## Performance Considerations

### Caching Strategy
- **Metadata Cache** - 24-hour cache for external API responses
- **Quality Cache** - Persistent quality detection results
- **Cover Cache** - Local cover image storage
- **Search Cache** - Temporary search result caching

### Processing Optimization
- **Batch Operations** - Process multiple files simultaneously
- **Lazy Loading** - Load metadata on demand
- **Background Tasks** - Non-blocking metadata updates
- **Rate Limiting** - Respect external API limits

### Storage Efficiency
- **Hard Linking** - Preserve disk space for duplicates
- **Compression** - Automatic archive extraction
- **Cleanup Tasks** - Remove orphaned files and metadata
- **Database Optimization** - Regular VACUUM and ANALYZE operations

This comprehensive media processing system enables ReadAIrr to handle diverse ebook formats while maintaining high-quality metadata and seamless integration with external systems like Calibre and various download clients.
