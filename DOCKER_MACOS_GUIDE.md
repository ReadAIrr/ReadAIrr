# ReadAIrr Docker Deployment for macOS

This guide will help you run ReadAIrr as a Docker container on your macOS system with support for SMB network shares.

## 🚀 Quick Start

1. **Prerequisites**
   - Docker Desktop for Mac (installed and running)
   - At least 2GB free disk space
   - macOS 10.15 or newer

2. **One-Command Setup**
   ```bash
   ./launch-docker-macos.sh setup
   ```

3. **Access ReadAIrr**
   - Open http://localhost:8246 in your browser
   - Complete the initial setup wizard

## 📋 Detailed Setup

### Step 1: Prepare Your Environment

Ensure Docker Desktop is running:
```bash
docker --version
docker info
```

### Step 2: Build and Launch

```bash
# Complete setup (recommended for first time)
./launch-docker-macos.sh setup

# Or build and run separately
./launch-docker-macos.sh build
./launch-docker-macos.sh run
```

### Step 3: Configure ReadAIrr

1. Open http://localhost:8246
2. Complete the setup wizard:
   - **Media Management**: Point to `/media` inside the container
   - **Downloads**: Point to `/downloads` inside the container
   - **Quality Profiles**: Configure as needed
   - **Metadata Profiles**: Configure as needed

## 🗂️ Directory Structure

After setup, you'll have:

```
ReadAIrr/
├── docker-data/
│   ├── config/          # ReadAIrr configuration and database
│   ├── downloads/       # Downloaded books (temporary)
│   ├── media/           # Organized book library
│   └── backups/         # Database backups
├── launch-docker-macos.sh
├── docker-compose.macos.yml
└── .env.local
```

## 🔧 Management Commands

```bash
# View real-time logs
./launch-docker-macos.sh logs

# Stop the container
./launch-docker-macos.sh stop

# Restart the container
./launch-docker-macos.sh restart

# Clean up (removes container and image, keeps data)
./launch-docker-macos.sh clean
```

## 📡 SMB Network Storage Integration

ReadAIrr can mount your SMB/CIFS network shares directly inside the container.

### Configure SMB Access

1. Edit `.env.local`:
   ```bash
   # Network storage (SMB share integration)
   NETWORK_STORAGE_ENABLED=true
   SMB_SHARE_PATH=//your-nas.local/books
   SMB_USERNAME=your-username
   SMB_PASSWORD=your-password
   SMB_MOUNT_POINT=/mnt/smb-media
   ```

2. Restart the container:
   ```bash
   ./launch-docker-macos.sh restart
   ```

3. In ReadAIrr's web UI, configure:
   - **Root Folders**: Add `/mnt/smb-media` as a root folder
   - **Download Client**: Point downloads to `/downloads`
   - **Media Management**: Enable file management

### SMB Configuration Examples

**Synology NAS:**
```bash
SMB_SHARE_PATH=//synology.local/books
SMB_USERNAME=readairr
SMB_VERSION=3.0
```

**QNAP NAS:**
```bash
SMB_SHARE_PATH=//qnap.local/Multimedia/Books
SMB_USERNAME=admin
SMB_VERSION=3.0
```

**Windows Share:**
```bash
SMB_SHARE_PATH=//windows-pc.local/SharedBooks
SMB_USERNAME=username
SMB_DOMAIN=WORKGROUP
SMB_VERSION=3.0
```

**macOS Sharing:**
```bash
SMB_SHARE_PATH=//mac-mini.local/Books
SMB_USERNAME=mac-user
SMB_VERSION=3.0
```

## 🔒 Security Considerations

### SMB Credentials
- Store credentials in `.env.local` (never commit to git)
- Use dedicated service accounts with minimal permissions
- Consider using credential files for enhanced security

### File Permissions
- The container runs as user ID 1000
- Ensure your SMB shares allow read/write access
- Docker on macOS handles permission mapping automatically

### Network Security
- ReadAIrr runs on port 8246 (localhost only by default)
- SMB traffic is not encrypted unless you enable SMB encryption on your NAS
- Consider VPN if accessing over the internet

## 🛠️ Troubleshooting

### Container Won't Start
```bash
# Check Docker status
docker ps -a

# View container logs
docker logs readairr-macos

# Check for port conflicts
lsof -i :8246
```

### SMB Mount Issues
```bash
# Check mount status inside container
docker exec readairr-macos mount | grep smb

# Test SMB connectivity
docker exec readairr-macos ping your-nas.local

# View detailed SMB logs
docker logs readairr-macos | grep SMB
```

### Permission Problems
```bash
# Check directory ownership
ls -la docker-data/

# Fix permissions if needed (run from project directory)
sudo chown -R $(id -u):$(id -g) docker-data/
```

### Web UI Not Loading
1. Verify container is running: `docker ps`
2. Check logs: `./launch-docker-macos.sh logs`
3. Test network: `curl http://localhost:8246/api/v1/system/status`
4. Verify port isn't blocked by macOS firewall

### Database Issues
```bash
# Backup current database
cp docker-data/config/readarr.db docker-data/config/readarr.db.backup

# Reset database (will lose settings)
rm docker-data/config/readarr.db
./launch-docker-macos.sh restart
```

## 🔄 Updates and Backups

### Updating ReadAIrr
```bash
# Pull latest code and rebuild
git pull
./launch-docker-macos.sh stop
./launch-docker-macos.sh build
./launch-docker-macos.sh run
```

### Backup Strategy
Your important data is in `docker-data/`:
- `config/` - Database and settings (backup regularly)
- `media/` - Your organized library (usually on NAS)
- `downloads/` - Temporary files (can be deleted)

```bash
# Backup configuration
tar -czf readairr-backup-$(date +%Y%m%d).tar.gz docker-data/config/

# Restore configuration
tar -xzf readairr-backup-YYYYMMDD.tar.gz
```

## 🎯 Advanced Configuration

### Multiple SMB Shares
Edit `.env.local`:
```bash
# Primary books share
SMB_SHARE_PATH=//nas.local/books
SMB_MOUNT_POINT=/mnt/smb-media

# Audiobooks share  
SMB_SHARE_PATH_2=//nas.local/audiobooks
SMB_MOUNT_POINT_2=/mnt/smb-audiobooks
```

### Custom Docker Networks
```bash
# Create custom network
docker network create readairr-custom

# Modify docker-compose.macos.yml to use custom network
```

### Resource Limits
Edit `docker-compose.macos.yml`:
```yaml
services:
  readairr:
    # ... other settings ...
    deploy:
      resources:
        limits:
          memory: 2G
          cpus: '1.0'
```

## 📊 Monitoring and Logs

### Log Levels
Adjust in `.env.local`:
```bash
READAIRR_LOG_LEVEL=debug  # trace, debug, info, warn, error
```

### Health Checks
The container includes health checks:
```bash
# Check container health
docker inspect readairr-macos | grep -A 20 "Health"

# Manual health check
curl http://localhost:8246/api/v1/system/status
```

### Performance Monitoring
```bash
# View container resource usage
docker stats readairr-macos

# View disk usage
docker exec readairr-macos df -h
```

## 🆘 Support

1. **Check Logs**: Always check container logs first
2. **Test Components**: Test SMB mounts, network connectivity separately
3. **Community**: Join the ReadAIrr community discussions
4. **Documentation**: Refer to original Readarr documentation for app-specific issues

## 🔄 Migration from Existing Readarr

If you have an existing Readarr installation:

1. **Backup existing config**:
   ```bash
   # From your old Readarr config directory
   cp -r /path/to/old/readarr/config/* docker-data/config/
   ```

2. **Update database paths** (if needed):
   - Edit paths in ReadAIrr settings to match container paths
   - `/mnt/smb-media` instead of local paths

3. **Test thoroughly** before removing old installation

---

*This Docker setup provides a privacy-respecting, containerized ReadAIrr deployment with optional AI features disabled by default.*
