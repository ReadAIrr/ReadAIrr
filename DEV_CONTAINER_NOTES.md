# ReadAIrr Dev Container Notes

## Network Storage Limitations

The VS Code dev container environment has security restrictions that prevent mounting SMB/CIFS shares directly. The network storage configuration in `.env.local` is designed for the Docker container environment, not the dev container.

### Current Status:
- ✅ ReadAIrr is running on port 8246
- ✅ SMB share is accessible via smbclient
- ❌ SMB share cannot be mounted to `/mnt/smb-share` due to container privileges
- ❌ Network storage features require Docker environment

### SMB Share Access:
- **Host**: 192.168.1.151
- **Share**: readairr
- **Mount Point (intended)**: /mnt/smb-share
- **Status**: Accessible via smbclient but not mountable in dev container

### Solutions:

#### Option 1: Use Docker Environment
For full network storage functionality, use the Docker environment instead:
```bash
# Exit dev container and run on host
docker compose -f docker-compose.dev.yml up -d
# Access at http://localhost:8246
```

#### Option 2: Manual File Transfer
Use smbclient to manually transfer files:
```bash
# Browse share
smbclient //192.168.1.151/readairr -U toby%Flive678 -c "ls"

# Download files
smbclient //192.168.1.151/readairr -U toby%Flive678 -c "get filename.epub /tmp/"
```

#### Option 3: Local Testing
For development testing, use local directories:
- Create test files in `/workspaces/Readairr/docker-data/media/`
- Use this as your library path in ReadAIrr settings

### Recommendation:
For development that requires network storage integration, use the Docker environment rather than the dev container.
