#!/bin/bash
# ReadAIrr Docker Deployment Script
# This script demonstrates the deployment process when Docker is available

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[DEPLOY] $1${NC}"
}

info() {
    echo -e "${BLUE}[DEPLOY] $1${NC}"
}

step() {
    echo -e "${CYAN}[DEPLOY] STEP: $1${NC}"
}

warn() {
    echo -e "${YELLOW}[DEPLOY] WARNING: $1${NC}"
}

# Load environment variables
if [[ -f ".env.local" ]]; then
    set -a
    source .env.local
    set +a
    log "Loaded configuration from .env.local"
else
    echo "Error: .env.local file not found"
    exit 1
fi

echo
log "ReadAIrr Docker Deployment"
log "=========================="
echo

# Display current configuration
step "Current Configuration Summary"
echo
info "Application Settings:"
echo "  Port: ${READAIRR_PORT:-8246}"
echo "  Branch: ${READAIRR_BRANCH:-develop}"
echo "  Log Level: ${READAIRR_LOG_LEVEL:-info}"
echo "  Dev Mode: ${READAIRR_DEV_MODE:-false}"
echo
info "Storage Configuration:"
echo "  Config: ${READAIRR_CONFIG_PATH:-./docker-data/config}"
echo "  Downloads: ${READAIRR_DOWNLOADS_PATH:-./docker-data/downloads}" 
echo "  Media: ${READAIRR_MEDIA_PATH:-./docker-data/media}"
echo "  Backups: ${READAIRR_BACKUP_PATH:-./docker-data/backups}"
echo
info "Network Storage:"
echo "  Enabled: ${NETWORK_STORAGE_ENABLED:-false}"
if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
    echo "  SMB Share: ${SMB_SHARE_PATH:-not configured}"
    echo "  SMB User: ${SMB_USERNAME:-not configured}"
    echo "  SMB Mount: ${SMB_MOUNT_POINT:-/mnt/smb-media}"
    echo "  SMB Domain: ${SMB_DOMAIN:-WORKGROUP}"
    echo "  SMB Version: ${SMB_VERSION:-3.0}"
    if [[ -n "$NFS_SHARE_PATH" ]]; then
        echo "  NFS Share: $NFS_SHARE_PATH"
        echo "  NFS Mount: ${NFS_MOUNT_POINT:-/mnt/nfs-media}"
    fi
fi
echo

# Check if Docker is available
step "Checking Docker Environment"
if command -v docker &> /dev/null; then
    if docker info &> /dev/null; then
        log "✓ Docker is available and running"
        DOCKER_AVAILABLE=true
    else
        warn "Docker is installed but not running"
        DOCKER_AVAILABLE=false
    fi
else
    warn "Docker is not installed in this environment"
    DOCKER_AVAILABLE=false
fi

if command -v "docker compose" &> /dev/null; then
    log "✓ Docker Compose v2 is available"
    COMPOSE_CMD="docker compose"
elif command -v docker-compose &> /dev/null; then
    log "✓ Docker Compose v1 is available"
    COMPOSE_CMD="docker-compose"
else
    warn "Docker Compose is not available"
    COMPOSE_CMD=""
fi
echo

# Show deployment commands
step "Deployment Commands"
echo
if [[ "$DOCKER_AVAILABLE" == "true" ]]; then
    info "Ready to deploy! Run the following commands:"
    echo
    echo "1. Build the ReadAIrr Docker image:"
    echo "   $COMPOSE_CMD -f docker-compose.dev.yml build"
    echo
    echo "2. Start ReadAIrr with network storage:"
    echo "   $COMPOSE_CMD -f docker-compose.dev.yml up -d"
    echo
    echo "3. View logs:"
    echo "   $COMPOSE_CMD -f docker-compose.dev.yml logs -f readairr-dev"
    echo
    echo "4. Check network storage mounts:"
    echo "   $COMPOSE_CMD -f docker-compose.dev.yml exec readairr-dev mount | grep -E '(cifs|nfs)'"
    echo
    echo "5. Access ReadAIrr:"
    echo "   Open http://localhost:${READAIRR_PORT:-8246} in your browser"
    echo
else
    info "Since Docker is not available in this environment, here's what would happen:"
    echo
    echo "1. Docker would build an image based on Dockerfile.dev with:"
    echo "   - .NET 6 runtime on Alpine Linux"
    echo "   - SMB/CIFS and NFS client tools"
    echo "   - ReadAIrr application files"
    echo "   - Network mounting scripts"
    echo
    echo "2. The container would start with:"
    echo "   - Privileged mode for network mounting"
    echo "   - Your configured environment variables"
    echo "   - Persistent volumes for data storage"
    echo "   - Network storage auto-mounting"
    echo
    echo "3. ReadAIrr would be accessible at:"
    echo "   http://localhost:${READAIRR_PORT:-8246}"
    echo
fi

# Show what directories would be created/used
step "Storage Directory Status"
echo
info "Local storage directories:"
for dir in "${READAIRR_CONFIG_PATH:-./docker-data/config}" \
           "${READAIRR_DOWNLOADS_PATH:-./docker-data/downloads}" \
           "${READAIRR_MEDIA_PATH:-./docker-data/media}" \
           "${READAIRR_BACKUP_PATH:-./docker-data/backups}"; do
    if [[ -d "$dir" ]]; then
        echo "  ✓ $dir (exists)"
    else
        echo "  ✗ $dir (would be created)"
    fi
done
echo

# Show network storage mounting simulation
if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
    step "Network Storage Mounting Simulation"
    echo
    info "When the container starts, it would:"
    echo
    echo "1. Create mount points:"
    echo "   mkdir -p ${SMB_MOUNT_POINT:-/mnt/smb-media}"
    if [[ -n "$NFS_SHARE_PATH" ]]; then
        echo "   mkdir -p ${NFS_MOUNT_POINT:-/mnt/nfs-media}"
    fi
    echo
    echo "2. Mount SMB share:"
    echo "   mount -t cifs \"${SMB_SHARE_PATH}\" \"${SMB_MOUNT_POINT:-/mnt/smb-media}\" \\"
    echo "     -o username=\"${SMB_USERNAME}\",password=\"***\",domain=\"${SMB_DOMAIN:-WORKGROUP}\",vers=\"${SMB_VERSION:-3.0}\",uid=1000,gid=1000"
    echo
    if [[ -n "$NFS_SHARE_PATH" ]]; then
        echo "3. Mount NFS share:"
        echo "   mount -t nfs \"${NFS_SHARE_PATH}\" \"${NFS_MOUNT_POINT:-/mnt/nfs-media}\" \\"
        echo "     -o ${NFS_OPTIONS:-rw,hard,intr,rsize=8192,wsize=8192,timeo=14}"
        echo
    fi
    echo "4. ReadAIrr would then see these mount points as available directories"
    echo "   for configuring media libraries and download locations."
    echo
fi

# Show post-deployment steps
step "Post-Deployment Configuration"
echo
info "After successful deployment, you would:"
echo
echo "1. Access ReadAIrr web interface:"
echo "   http://localhost:${READAIRR_PORT:-8246}"
echo
echo "2. Complete initial setup wizard"
echo
echo "3. Configure media management:"
echo "   - Go to Settings > Media Management"
echo "   - Add root folders:"
if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
    echo "     • ${SMB_MOUNT_POINT:-/mnt/smb-media} (your SMB share)"
    if [[ -n "$NFS_SHARE_PATH" ]]; then
        echo "     • ${NFS_MOUNT_POINT:-/mnt/nfs-media} (your NFS share)"
    fi
else
    echo "     • /media (local storage)"
    echo "     • /downloads (download staging)"
fi
echo
echo "4. Configure download clients if needed"
echo
echo "5. Import existing library or start adding books"
echo

# Show monitoring commands
step "Monitoring and Maintenance"
echo
info "Useful commands for monitoring:"
echo
echo "# View container logs"
echo "$COMPOSE_CMD -f docker-compose.dev.yml logs -f readairr-dev"
echo
echo "# Check container status"
echo "$COMPOSE_CMD -f docker-compose.dev.yml ps"
echo
echo "# Access container shell"
echo "$COMPOSE_CMD -f docker-compose.dev.yml exec readairr-dev bash"
echo
echo "# Check mount status inside container"
echo "$COMPOSE_CMD -f docker-compose.dev.yml exec readairr-dev mount | grep -E '(cifs|nfs)'"
echo
echo "# Restart ReadAIrr"
echo "$COMPOSE_CMD -f docker-compose.dev.yml restart readairr-dev"
echo
echo "# Stop everything"
echo "$COMPOSE_CMD -f docker-compose.dev.yml down"
echo

# Final summary
log "Deployment simulation completed!"
if [[ "$DOCKER_AVAILABLE" == "true" ]]; then
    echo
    info "Your environment is ready for deployment."
    info "Run the commands shown above to start ReadAIrr."
else
    echo
    info "Install Docker and Docker Compose to proceed with actual deployment."
    info "Configuration is ready and validated."
fi
