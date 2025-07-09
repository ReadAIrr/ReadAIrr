# ReadAIrr Network Storage Configuration Guide

This guide explains how to configure and use ReadAIrr's Docker development environment with SMB/CIFS and NFS network storage support.

## Overview

ReadAIrr's Docker development environment supports mounting remote network shares (SMB/CIFS and NFS) directly inside the container. This allows you to:

- Access media libraries stored on NAS devices
- Use network-attached storage for downloads
- Browse and manage remote book/audiobook collections
- Integrate with existing media server setups

## Quick Start

1. **Copy the environment template**:
   ```bash
   cp .env.local.example .env.local
   ```

2. **Configure network storage** in `.env.local`:
   ```bash
   # Enable network storage
   NETWORK_STORAGE_ENABLED=true
   
   # Configure SMB share
   SMB_SHARE_PATH=//nas.local/books
   SMB_USERNAME=your-username
   SMB_PASSWORD=your-password
   SMB_MOUNT_POINT=/mnt/smb-media
   ```

3. **Start the container**:
   ```bash
   docker-compose -f docker-compose.dev.yml up -d
   ```

4. **Access ReadAIrr** at `http://localhost:8787` and configure paths:
   - Navigate to Settings > Media Management
   - Add `/mnt/smb-media` as a root folder for your books

## Environment Variables

### Network Storage Control

| Variable | Default | Description |
|----------|---------|-------------|
| `NETWORK_STORAGE_ENABLED` | `false` | Enable/disable network storage mounting |

### SMB/CIFS Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `SMB_SHARE_PATH` | - | SMB share path (e.g., `//nas.local/books`) |
| `SMB_USERNAME` | - | SMB username for authentication |
| `SMB_PASSWORD` | - | SMB password for authentication |
| `SMB_DOMAIN` | - | SMB domain (optional) |
| `SMB_VERSION` | `3.0` | SMB protocol version (1.0, 2.0, 2.1, 3.0, 3.1.1) |
| `SMB_MOUNT_POINT` | `/mnt/smb-media` | Container path where SMB share is mounted |

### NFS Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `NFS_SHARE_PATH` | - | NFS share path (e.g., `192.168.1.100:/volume1/books`) |
| `NFS_OPTIONS` | `rw,hard,intr,rsize=8192,wsize=8192,timeo=14` | NFS mount options |
| `NFS_MOUNT_POINT` | `/mnt/nfs-media` | Container path where NFS share is mounted |

### Additional Shares

You can configure secondary network shares:

| Variable | Default | Description |
|----------|---------|-------------|
| `SMB_SHARE_PATH_2` | - | Second SMB share path |
| `SMB_MOUNT_POINT_2` | `/mnt/smb-audiobooks` | Mount point for second SMB share |
| `NFS_SHARE_PATH_2` | - | Second NFS share path |
| `NFS_MOUNT_POINT_2` | `/mnt/nfs-downloads` | Mount point for second NFS share |

## Configuration Examples

### Example 1: Synology NAS with SMB

```bash
# .env.local
NETWORK_STORAGE_ENABLED=true

# Primary media library on Synology NAS
SMB_SHARE_PATH=//synology.local/books
SMB_USERNAME=readairr
SMB_PASSWORD=secure-password
SMB_DOMAIN=
SMB_VERSION=3.0
SMB_MOUNT_POINT=/mnt/smb-media

# Secondary audiobooks share
SMB_SHARE_PATH_2=//synology.local/audiobooks
SMB_MOUNT_POINT_2=/mnt/smb-audiobooks
```

### Example 2: QNAP NAS with NFS

```bash
# .env.local
NETWORK_STORAGE_ENABLED=true

# Primary media library via NFS
NFS_SHARE_PATH=192.168.1.100:/share/multimedia/books
NFS_OPTIONS=rw,hard,intr,rsize=8192,wsize=8192,timeo=14
NFS_MOUNT_POINT=/mnt/nfs-media

# Downloads share
NFS_SHARE_PATH_2=192.168.1.100:/share/downloads
NFS_MOUNT_POINT_2=/mnt/nfs-downloads
```

### Example 3: Mixed SMB and NFS

```bash
# .env.local
NETWORK_STORAGE_ENABLED=true

# Books via SMB
SMB_SHARE_PATH=//windows-server/books
SMB_USERNAME=domain\\user
SMB_PASSWORD=password
SMB_DOMAIN=WORKGROUP
SMB_MOUNT_POINT=/mnt/smb-media

# Downloads via NFS  
NFS_SHARE_PATH=linux-server:/exports/downloads
NFS_MOUNT_POINT=/mnt/nfs-downloads
```

## Security Considerations

### Credential Management

**Option 1: Environment Variables (Simple)**
```bash
SMB_USERNAME=myuser
SMB_PASSWORD=mypassword
```

**Option 2: Credentials File (More Secure)**
```bash
# Create credentials file
echo "username=myuser" > ./docker-data/config/smb-credentials
echo "password=mypassword" >> ./docker-data/config/smb-credentials
echo "domain=mydomain" >> ./docker-data/config/smb-credentials
chmod 600 ./docker-data/config/smb-credentials

# Reference in .env.local
SMB_CREDENTIALS_FILE=/config/smb-credentials
```

### Network Security

- Use SMB 3.0 or higher for encryption in transit
- Configure NFS with proper export restrictions on the server
- Consider VPN if accessing shares over untrusted networks
- Use dedicated service accounts with minimal required permissions

## Container Requirements

The container requires elevated privileges to mount network filesystems:

```yaml
privileged: true
cap_add:
  - SYS_ADMIN
devices:
  - /dev/fuse
security_opt:
  - apparmor:unconfined
```

This is automatically configured in the provided `docker-compose.dev.yml`.

## ReadAIrr Configuration

Once network shares are mounted, configure ReadAIrr to use them:

1. **Access ReadAIrr UI**: Navigate to `http://localhost:8787`

2. **Add Root Folders**: 
   - Go to Settings > Media Management
   - Click "Add Root Folder"
   - Browse to `/mnt/smb-media` or `/mnt/nfs-media`
   - Set the folder name and quality profile

3. **Configure Download Client**:
   - Go to Settings > Download Clients  
   - Point download paths to network storage if desired
   - Example: `/mnt/nfs-downloads/books`

4. **Import Existing Library**:
   - Use the bulk import feature to scan mounted network shares
   - ReadAIrr will detect and organize existing books

## Troubleshooting

### Check Mount Status

```bash
# View container logs
docker-compose -f docker-compose.dev.yml logs readairr-dev

# Execute shell in container
docker-compose -f docker-compose.dev.yml exec readairr-dev bash

# Check mounts inside container
mount | grep -E "(cifs|nfs)"

# Test network share access
ls -la /mnt/smb-media
```

### Common Issues

**SMB Authentication Errors**:
- Verify username/password are correct
- Check if SMB version is compatible with your server
- Ensure the SMB service account has proper permissions

**NFS Permission Denied**:
- Check NFS server export configuration
- Verify client IP is allowed in exports
- Ensure NFS services are running on server

**Network Connectivity**:
- Verify the Docker container can reach the NAS
- Check firewall rules on both client and server
- Test connectivity: `ping nas.local` from container

**Mount Point Permissions**:
- Ensure the ReadAIrr user (UID 1000) can access mounted shares
- Check mount options include appropriate uid/gid settings

### Manual Mount Testing

```bash
# Test SMB mount manually
sudo mount -t cifs //nas.local/books /mnt/test \
  -o username=user,password=pass,uid=1000,gid=1000

# Test NFS mount manually  
sudo mount -t nfs nas.local:/volume1/books /mnt/test \
  -o rw,hard,intr
```

## Performance Tuning

### SMB Performance

```bash
# High-performance SMB options
SMB_OPTIONS="rw,uid=1000,gid=1000,iocharset=utf8,cache=strict,vers=3.1.1"
```

### NFS Performance

```bash
# High-performance NFS options
NFS_OPTIONS="rw,hard,intr,rsize=32768,wsize=32768,timeo=14,proto=tcp"
```

### Network Optimization

- Use wired network connections for better stability
- Consider dedicated network interfaces for storage traffic
- Monitor network bandwidth usage during library scans

## Integration Examples

### Plex Media Server Integration

```bash
# Mount the same shares used by Plex
SMB_SHARE_PATH=//nas.local/plex-media/books
SMB_MOUNT_POINT=/mnt/plex-books

# Configure ReadAIrr to organize into Plex-compatible structure
# Settings > Media Management > File Naming
```

### Automated Download Setup

```bash
# Downloads to network storage
SMB_SHARE_PATH=//nas.local/downloads
SMB_MOUNT_POINT=/mnt/downloads

# Completed downloads moved to media library
SMB_SHARE_PATH_2=//nas.local/media/books  
SMB_MOUNT_POINT_2=/mnt/media
```

## Advanced Configuration

### Multiple NAS Devices

```bash
# Primary NAS (Synology)
SMB_SHARE_PATH=//synology.local/books
SMB_MOUNT_POINT=/mnt/synology-books

# Secondary NAS (QNAP) 
NFS_SHARE_PATH=192.168.1.200:/share/audiobooks
NFS_MOUNT_POINT=/mnt/qnap-audiobooks
```

### Load Balancing / Failover

Configure multiple shares pointing to the same content for redundancy:

```bash
# Primary path
SMB_SHARE_PATH=//nas1.local/books
SMB_MOUNT_POINT=/mnt/books-primary

# Backup path  
SMB_SHARE_PATH_2=//nas2.local/books-backup
SMB_MOUNT_POINT_2=/mnt/books-backup
```

## Monitoring and Maintenance

### Health Checks

The container includes health checks that verify:
- ReadAIrr service availability
- Network share mount status
- Storage space availability

### Log Monitoring

```bash
# Monitor ReadAIrr logs
docker-compose -f docker-compose.dev.yml logs -f readairr-dev

# Check network mount logs
docker-compose -f docker-compose.dev.yml exec readairr-dev \
  grep -i "mount" /var/log/syslog
```

### Backup Considerations

- Network storage is mounted read-write by default
- ReadAIrr configuration and database are stored in persistent volumes
- Consider backing up both local config and network storage
- Test restore procedures regularly

## Development Notes

When developing with network storage:

- Source code changes are reflected immediately (volume mounted)
- Database changes persist across container restarts  
- Network shares are remounted automatically on container restart
- Use `docker-compose down && docker-compose up` to reset everything

For production deployment, consider:
- Using Docker secrets for credentials
- Implementing proper backup strategies
- Monitoring storage performance and availability
- Setting up alerting for mount failures
