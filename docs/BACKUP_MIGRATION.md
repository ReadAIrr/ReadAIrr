# ReadAIrr Backup Migration Guide

## Overview

ReadAIrr includes intelligent backup migration functionality to help users seamlessly transition from legacy Readarr installations while preserving their configuration and data. The system automatically detects legacy Readarr backups and guides users through any necessary configuration changes.

## Key Features

### Automatic Legacy Detection
- **Port Detection**: Identifies legacy Readarr default ports (8787/6868)
- **Application Name Detection**: Recognizes legacy "Readarr" instance names
- **Configuration Analysis**: Examines backup contents for compatibility issues

### User-Guided Migration
- **Conflict Detection**: Identifies settings that differ between current and backup configurations
- **Intelligent Recommendations**: Provides smart migration suggestions with rationale
- **User Choice**: Allows users to decide how to handle each configuration conflict
- **Safe Defaults**: Uses ReadAIrr best practices when users don't specify preferences

## Migration Process

### Step 1: Backup Upload
When uploading a backup via the ReadAIrr web interface or API, the system:

1. **Analyzes the backup** for configuration conflicts
2. **Detects legacy Readarr** installations automatically
3. **Presents conflicts** to the user if any are found
4. **Requires user decisions** before proceeding with restoration

### Step 2: Conflict Resolution
For each detected conflict, users can choose from suggested actions:

#### Port Configuration Conflicts
- **Migrate to ReadAIrr ports** (8246/8247) - **Recommended for legacy backups**
- **Keep current ports** - Maintains existing ReadAIrr configuration
- **Use backup ports** - Adopts the backup's port settings
- **Use default ports** - Applies ReadAIrr defaults

#### Instance Name Conflicts
- **Use ReadAIrr instance name** - Adopts current ReadAIrr naming
- **Keep backup instance name** - Preserves original name from backup
- **Create new instance name** - Generates a hybrid name (e.g., "ReadAIrr-Migration")

#### URL Base and Path Conflicts
- **Keep current settings** - Maintains existing ReadAIrr configuration
- **Use backup settings** - Adopts backup's URL configuration

### Step 3: Automatic Migration
Once user decisions are provided:

1. **Applies user preferences** to the backup configuration
2. **Updates conflicting settings** according to user choices
3. **Preserves non-conflicting data** (library paths, indexers, download clients, etc.)
4. **Completes the restore process** with migrated configuration

## API Endpoints

### Analyze Backup for Conflicts
```http
GET /api/v1/system/backup/analyze/{id}
```

**Response Example:**
```json
{
  "hasConflicts": true,
  "isLegacyReadarrBackup": true,
  "conflicts": [
    {
      "setting": "Port",
      "currentValue": "8246",
      "backupValue": "8787",
      "description": "HTTP port configuration differs between current installation and backup",
      "type": "PortConfiguration",
      "suggestedActions": [
        "Migrate from legacy Readarr port 8787 to ReadAIrr port 8246 (Recommended)",
        "Keep current ReadAIrr port (8246)",
        "Use backup port (8787)",
        "Use ReadAIrr default port (8246)"
      ]
    }
  ]
}
```

### Upload and Restore with Migration
```http
POST /api/v1/system/backup/restore/upload
Content-Type: multipart/form-data
```

If conflicts are detected, returns:
```json
{
  "migrationRequired": true,
  "isLegacyReadarrBackup": true,
  "tempFileName": "readairr_backup_restore.zip",
  "conflicts": [...],
  "message": "Configuration conflicts detected while restoring backup from legacy Readarr. User intervention required for 3 setting(s)."
}
```

### Complete Migration with User Decisions
```http
POST /api/v1/system/backup/restore/upload/migrate
Content-Type: application/json

{
  "tempFileName": "readairr_backup_restore.zip",
  "decisions": {
    "Port": "migrate_legacy",
    "SslPort": "migrate_legacy",
    "InstanceName": "create_new"
  }
}
```

**User Decision Options:**
- **Port/SslPort**: `"migrate_legacy"`, `"keep_current"`, `"use_backup"`, `"use_default"`
- **InstanceName**: `"keep_current"`, `"use_backup"`, `"create_new"`
- **UrlBase**: `"keep_current"`, `"use_backup"`

## Migration Scenarios

### Scenario 1: Legacy Readarr Migration (Most Common)
**Situation**: User has a backup from original Readarr (ports 8787/6868)

**Automatic Actions:**
- Detects legacy Readarr backup
- Identifies port conflicts (8787→8246, 6868→8247)
- Suggests migrating to ReadAIrr ports
- Offers to update instance name to "ReadAIrr"

**Recommended User Choices:**
```json
{
  "Port": "migrate_legacy",
  "SslPort": "migrate_legacy", 
  "InstanceName": "create_new"
}
```

### Scenario 2: ReadAIrr-to-ReadAIrr Migration
**Situation**: Restoring backup from another ReadAIrr installation

**Automatic Actions:**
- No legacy detection (modern ports detected)
- Only conflicts if port/path differences exist
- Minimal user intervention required

### Scenario 3: Custom Port Configuration
**Situation**: User wants to maintain custom ports from backup

**User Choices:**
```json
{
  "Port": "use_backup",
  "SslPort": "use_backup",
  "InstanceName": "use_backup"
}
```

## Implementation Details

### Configuration Detection Logic
```csharp
// Detects legacy Readarr backups
public bool IsLegacyReadarrBackup(string configPath)
{
    // Check for legacy ports (8787/6868)
    // Check for "Readarr" instance names (without "ReadAIrr")
    // Analyze URL bases and other legacy indicators
}
```

### Migration Application
```csharp
// Applies user decisions to backup config
private void ApplyConfigMigration(
    string configPath, 
    string backupConfigPath, 
    List<ConfigurationConflict> conflicts, 
    Dictionary<string, string> userDecisions, 
    bool isLegacy)
{
    // Loads backup configuration XML
    // Applies user decisions to conflicting settings
    // Preserves non-conflicting configuration
    // Saves modified backup for restoration
}
```

## Best Practices

### For Users Migrating from Readarr
1. **Use recommended migration options** for seamless transition
2. **Migrate to ReadAIrr ports** (8246/8247) to avoid conflicts
3. **Update instance names** to reflect ReadAIrr branding
4. **Review migrated settings** after restoration completes

### For Advanced Users
1. **Analyze before migrating** using the `/analyze` endpoint
2. **Plan port assignments** to avoid conflicts with other services
3. **Backup current config** before restoring legacy backups
4. **Test connectivity** after migration completes

### For Developers
1. **Handle BackupMigrationConflictException** in API clients
2. **Present conflict information** clearly to users
3. **Provide sensible defaults** for automated migrations
4. **Validate decisions** before submitting to migration endpoint

## Troubleshooting

### Common Issues

#### "Configuration conflicts detected" Error
- **Cause**: Backup contains settings that differ from current installation
- **Solution**: Use the migration endpoints to resolve conflicts with user input

#### "Legacy Readarr backup detected" Warning
- **Cause**: Backup originated from original Readarr installation
- **Solution**: Follow recommended migration path to ReadAIrr ports and naming

#### "Unable to restore database file" Error
- **Cause**: Backup corruption or incompatible database format
- **Solution**: Verify backup integrity and ReadAIrr compatibility

### Migration Validation
After completing a migration:

1. **Verify ReadAIrr starts** on the configured ports
2. **Check web interface accessibility** at `http://localhost:[PORT]`
3. **Validate API functionality** using the configured API key
4. **Review library paths** and download client configurations
5. **Test indexer connections** and search functionality

## Security Considerations

### API Key Handling
- **Backup API keys are preserved** during migration
- **Current API keys are replaced** with backup values
- **Generate new API keys** if security is a concern after migration

### Port Security
- **Validate port availability** before migration
- **Consider firewall rules** when changing ports
- **Update reverse proxy configurations** if applicable

### Data Privacy
- **Temporary files are cleaned up** after migration
- **Migration decisions are not logged** in plain text
- **User choices are processed in memory** and not persisted

---

*This migration system ensures that ReadAIrr provides a smooth transition path for users moving from legacy Readarr installations while maintaining full control over configuration decisions.*
