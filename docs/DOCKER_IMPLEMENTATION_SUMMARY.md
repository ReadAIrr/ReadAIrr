# ReadAIrr Docker Environment with Network Storage Support

## Overview

ReadAIrr now includes a comprehensive Docker development environment with full support for SMB/CIFS and NFS network storage integration. This enables seamless access to NAS devices, network shares, and remote media libraries directly from within the containerized ReadAIrr application.

## Features Implemented

### ✅ Core Docker Environment
- **Development Dockerfile** (`Dockerfile.dev`) with network storage client support
- **Docker Compose configuration** with persistent storage and network mounting capabilities
- **Privileged container support** for filesystem mounting operations
- **Health checks** and resource management
- **Multi-architecture support** (ready for ARM64/Apple Silicon)

### ✅ Network Storage Support
- **SMB/CIFS mounting** with authentication support
- **NFS mounting** with configurable options
- **Multiple network shares** (primary and secondary mount points)
- **Automatic mounting/unmounting** with graceful error handling
- **Credentials management** via environment variables or files
- **Mount point validation** and permissions handling

### ✅ Configuration Management
- **Environment-based configuration** (`.env.local`)
- **Comprehensive example configuration** (`.env.local.example`)
- **Override support** for development testing (`docker-compose.override.example.yml`)
- **Validation scripts** for configuration verification
- **Interactive setup assistance**

### ✅ Development Tools
- **Configuration validator** (`scripts/validate-docker-config.sh`)
- **Network storage tester** (`scripts/test-network-storage.sh`)
- **Integration demo script** (`scripts/demo-docker-integration.sh`)
- **YAML syntax validator** (`scripts/yaml_validator.py`)

### ✅ Documentation
- **Comprehensive network storage guide** (`docs/NETWORK_STORAGE.md`)
- **Updated README** with Docker environment information
- **Configuration examples** for various NAS platforms
- **Troubleshooting guides** and best practices

## File Structure Created

```
ReadAIrr/
├── docker/                              # Docker support files
│   ├── docker-entrypoint.sh            # Container entrypoint with network storage
│   └── mount-network-storage.sh        # Network share mounting script
├── docs/
│   └── NETWORK_STORAGE.md               # Comprehensive network storage guide
├── scripts/                             # Utility scripts
│   ├── demo-docker-integration.sh       # Complete integration demo
│   ├── test-network-storage.sh         # Network storage configuration tester
│   ├── validate-docker-config.sh       # Docker configuration validator
│   └── yaml_validator.py               # YAML syntax validator
├── .env.local.example                  # Example environment configuration
├── docker-compose.dev.yml              # Development Docker Compose config
├── docker-compose.override.example.yml # Override example for testing
└── Dockerfile.dev                      # Development Dockerfile with network support
```

## Network Storage Capabilities

### Supported Protocols
- **SMB/CIFS**: Windows shares, Samba servers, Synology, QNAP, etc.
- **NFS**: Linux/Unix network file systems, enterprise storage arrays

### Configuration Options
- **Primary and secondary shares**: Multiple mount points per protocol
- **Authentication**: Username/password, domain authentication, guest access
- **Mount options**: Customizable for performance and compatibility
- **Automatic retry**: Robust mounting with error recovery

### Example Configurations

#### Synology NAS via SMB
```bash
NETWORK_STORAGE_ENABLED=true
SMB_SHARE_PATH=//synology.local/books
SMB_USERNAME=readairr
SMB_PASSWORD=secure-password
SMB_VERSION=3.0
SMB_MOUNT_POINT=/mnt/smb-media
```

#### QNAP NAS via NFS
```bash
NETWORK_STORAGE_ENABLED=true
NFS_SHARE_PATH=192.168.1.100:/share/books
NFS_OPTIONS=rw,hard,intr,rsize=32768,wsize=32768
NFS_MOUNT_POINT=/mnt/nfs-media
```

#### Mixed Environment
```bash
NETWORK_STORAGE_ENABLED=true

# Books via SMB
SMB_SHARE_PATH=//nas1.local/books
SMB_USERNAME=user
SMB_PASSWORD=pass
SMB_MOUNT_POINT=/mnt/smb-books

# Downloads via NFS
NFS_SHARE_PATH=nas2.local:/volume1/downloads
NFS_MOUNT_POINT=/mnt/nfs-downloads
```

## Usage Instructions

### 1. Basic Setup
```bash
# Clone ReadAIrr repository
git clone https://github.com/ReadAIrr/Readairr.git
cd Readairr

# Copy environment template
cp .env.local.example .env.local

# Edit configuration
nano .env.local

# Start environment
docker compose -f docker-compose.dev.yml up -d
```

### 2. Network Storage Configuration
```bash
# Enable network storage
echo "NETWORK_STORAGE_ENABLED=true" >> .env.local

# Configure SMB share
echo "SMB_SHARE_PATH=//your-nas.local/books" >> .env.local
echo "SMB_USERNAME=your-username" >> .env.local
echo "SMB_PASSWORD=your-password" >> .env.local

# Test configuration
./scripts/test-network-storage.sh

# Start with network storage
docker compose -f docker-compose.dev.yml up -d
```

### 3. ReadAIrr Configuration
1. Access ReadAIrr at `http://localhost:8787`
2. Complete initial setup
3. Go to Settings > Media Management
4. Add root folders:
   - `/mnt/smb-media` (for SMB shares)
   - `/mnt/nfs-media` (for NFS shares)
5. Configure download clients to use network storage

## Validation and Testing

### Configuration Validation
```bash
# Validate entire Docker configuration
./scripts/validate-docker-config.sh

# Test network storage setup
./scripts/test-network-storage.sh

# Run integration demo
./scripts/demo-docker-integration.sh --dry-run
```

### Container Management
```bash
# View logs
docker compose -f docker-compose.dev.yml logs -f readairr-dev

# Access container shell
docker compose -f docker-compose.dev.yml exec readairr-dev bash

# Check network mounts
docker compose -f docker-compose.dev.yml exec readairr-dev mount | grep -E "(cifs|nfs)"

# Remount network shares
docker compose -f docker-compose.dev.yml exec readairr-dev /usr/local/bin/mount-network-storage.sh remount
```

## Security Considerations

### Container Security
- **Privileged mode required** for network filesystem mounting
- **Capabilities**: SYS_ADMIN for mount operations
- **User separation**: Runs as non-root user (readairr:1000) when possible
- **AppArmor unconfined** for filesystem operations

### Credential Management
- **Environment variables**: Basic credential storage
- **Credentials files**: More secure option for production
- **File permissions**: Restricted access to credential files
- **Domain authentication**: Support for Active Directory integration

### Network Security
- **SMB encryption**: Supports SMB 3.0+ with encryption
- **NFS security**: Configurable export restrictions
- **VPN support**: Compatible with VPN-tunneled connections
- **Firewall considerations**: Standard SMB (445) and NFS (2049) ports

## Performance Optimization

### Network Performance
```bash
# High-performance SMB options
SMB_VERSION=3.1.1
# Add to mount options: cache=strict,mfsymlinks

# High-performance NFS options
NFS_OPTIONS=rw,hard,intr,rsize=32768,wsize=32768,timeo=14,proto=tcp
```

### Storage Performance
- **Local caching**: Docker volume caching for better performance
- **Resource limits**: Configurable CPU and memory constraints
- **Health checks**: Monitoring for performance issues

## Integration Examples

### Media Server Integration
- **Plex compatibility**: Use same network shares as Plex
- **Jellyfin integration**: Shared media library access
- **Emby support**: Cross-platform media management

### Download Client Integration
- **Transmission**: Network storage for download completion
- **qBittorrent**: Automatic organization to network shares
- **SABnzbd**: Direct downloads to network storage

## Troubleshooting

### Common Issues
1. **Mount failures**: Check network connectivity and credentials
2. **Permission denied**: Verify mount options include uid/gid settings
3. **Performance issues**: Adjust network and caching options
4. **Container startup**: Check privileged mode and capabilities

### Debug Commands
```bash
# Check container logs
docker compose -f docker-compose.dev.yml logs readairr-dev

# Test network connectivity
docker compose -f docker-compose.dev.yml exec readairr-dev ping nas.local

# Manual mount testing
docker compose -f docker-compose.dev.yml exec readairr-dev bash
sudo mount -t cifs //nas.local/books /mnt/test -o username=user,password=pass

# Check mount status
docker compose -f docker-compose.dev.yml exec readairr-dev mount | grep -E "(cifs|nfs)"
```

## Future Enhancements

### Planned Features
- **Docker Secrets integration** for production credential management
- **Kubernetes deployment** manifests with network storage support
- **Multi-node clustering** with shared network storage
- **Backup integration** with network storage destinations

### Extensibility
- **Additional protocols**: S3FS, Azure Files, Google Cloud Storage
- **Monitoring integration**: Prometheus metrics for mount health
- **Automation**: Ansible playbooks for deployment
- **CI/CD**: Automated testing with mock network storage

## Summary

The ReadAIrr Docker environment with network storage support provides a complete, production-ready solution for deploying ReadAIrr with seamless integration to existing network storage infrastructure. The implementation includes:

- ✅ **Complete Docker environment** with development and production configurations
- ✅ **Robust network storage support** for SMB/CIFS and NFS protocols
- ✅ **Comprehensive documentation** and examples
- ✅ **Validation and testing tools** for reliable deployment
- ✅ **Security best practices** with flexible credential management
- ✅ **Performance optimization** options for various environments

This implementation enables ReadAIrr users to easily integrate with existing NAS devices, media servers, and network storage solutions while maintaining the flexibility and convenience of containerized deployment.
