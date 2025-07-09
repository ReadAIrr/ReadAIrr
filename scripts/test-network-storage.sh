#!/bin/bash
# ReadAIrr Network Storage Test Script
#
# This script helps test and validate network storage configuration
# before starting the ReadAIrr container.

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

log() {
    echo -e "${GREEN}[$(date +'%H:%M:%S')] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[$(date +'%H:%M:%S')] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[$(date +'%H:%M:%S')] ERROR: $1${NC}"
}

info() {
    echo -e "${BLUE}[$(date +'%H:%M:%S')] INFO: $1${NC}"
}

# Function to load environment variables
load_env() {
    local env_file="$PROJECT_DIR/.env.local"
    
    if [[ ! -f "$env_file" ]]; then
        error "Environment file not found: $env_file"
        error "Please copy .env.local.example to .env.local and configure it"
        return 1
    fi
    
    log "Loading environment from: $env_file"
    set -a
    source "$env_file"
    set +a
}

# Function to check prerequisites
check_prerequisites() {
    log "Checking prerequisites..."
    
    # Check if Docker is installed and running
    if ! command -v docker &> /dev/null; then
        error "Docker is not installed"
        return 1
    fi
    
    if ! docker info &> /dev/null; then
        error "Docker is not running"
        return 1
    fi
    
    # Check if Docker Compose is available
    if ! docker-compose --version &> /dev/null; then
        error "Docker Compose is not installed"
        return 1
    fi
    
    info "Docker and Docker Compose are available"
}

# Function to validate network storage configuration
validate_network_config() {
    log "Validating network storage configuration..."
    
    if [[ "${NETWORK_STORAGE_ENABLED,,}" != "true" ]]; then
        warn "Network storage is disabled"
        return 0
    fi
    
    local has_valid_config=false
    
    # Check SMB configuration
    if [[ -n "$SMB_SHARE_PATH" ]]; then
        info "SMB configuration found:"
        info "  Share: $SMB_SHARE_PATH"
        info "  Mount point: ${SMB_MOUNT_POINT:-/mnt/smb-media}"
        info "  Username: ${SMB_USERNAME:-<not set>}"
        info "  Domain: ${SMB_DOMAIN:-<not set>}"
        info "  Version: ${SMB_VERSION:-3.0}"
        
        if [[ -z "$SMB_USERNAME" ]]; then
            warn "SMB username not set - will attempt guest access"
        fi
        
        has_valid_config=true
    fi
    
    # Check NFS configuration
    if [[ -n "$NFS_SHARE_PATH" ]]; then
        info "NFS configuration found:"
        info "  Share: $NFS_SHARE_PATH"
        info "  Mount point: ${NFS_MOUNT_POINT:-/mnt/nfs-media}"
        info "  Options: ${NFS_OPTIONS:-rw,hard,intr,rsize=8192,wsize=8192,timeo=14}"
        
        has_valid_config=true
    fi
    
    # Check additional shares
    if [[ -n "$SMB_SHARE_PATH_2" ]]; then
        info "Additional SMB share: $SMB_SHARE_PATH_2 -> ${SMB_MOUNT_POINT_2:-/mnt/smb-audiobooks}"
        has_valid_config=true
    fi
    
    if [[ -n "$NFS_SHARE_PATH_2" ]]; then
        info "Additional NFS share: $NFS_SHARE_PATH_2 -> ${NFS_MOUNT_POINT_2:-/mnt/nfs-downloads}"
        has_valid_config=true
    fi
    
    if [[ "$has_valid_config" == "false" ]]; then
        error "Network storage is enabled but no shares are configured"
        return 1
    fi
    
    info "Network storage configuration looks valid"
}

# Function to test network connectivity to shares
test_network_connectivity() {
    log "Testing network connectivity to configured shares..."
    
    if [[ "${NETWORK_STORAGE_ENABLED,,}" != "true" ]]; then
        info "Network storage disabled, skipping connectivity tests"
        return 0
    fi
    
    # Test SMB connectivity
    if [[ -n "$SMB_SHARE_PATH" ]]; then
        local smb_host
        smb_host=$(echo "$SMB_SHARE_PATH" | sed 's|//||' | cut -d'/' -f1)
        
        info "Testing connectivity to SMB host: $smb_host"
        
        if ping -c 3 -W 5 "$smb_host" &> /dev/null; then
            log "✓ SMB host $smb_host is reachable"
        else
            warn "✗ SMB host $smb_host is not reachable"
        fi
        
        # Test SMB port
        if nc -z -w 5 "$smb_host" 445 &> /dev/null; then
            log "✓ SMB service on $smb_host:445 is accessible"
        else
            warn "✗ SMB service on $smb_host:445 is not accessible"
        fi
    fi
    
    # Test NFS connectivity
    if [[ -n "$NFS_SHARE_PATH" ]]; then
        local nfs_host
        nfs_host=$(echo "$NFS_SHARE_PATH" | cut -d':' -f1)
        
        info "Testing connectivity to NFS host: $nfs_host"
        
        if ping -c 3 -W 5 "$nfs_host" &> /dev/null; then
            log "✓ NFS host $nfs_host is reachable"
        else
            warn "✗ NFS host $nfs_host is not reachable"
        fi
        
        # Test NFS port
        if nc -z -w 5 "$nfs_host" 2049 &> /dev/null; then
            log "✓ NFS service on $nfs_host:2049 is accessible"
        else
            warn "✗ NFS service on $nfs_host:2049 is not accessible"
        fi
    fi
}

# Function to check if container images need to be built
check_container_images() {
    log "Checking container images..."
    
    local image_name="${READAIRR_IMAGE_NAME:-readairr-dev}"
    
    if docker images "$image_name" | grep -q "$image_name"; then
        info "Container image $image_name exists"
    else
        warn "Container image $image_name not found - will be built on first run"
    fi
}

# Function to validate persistent storage paths
validate_storage_paths() {
    log "Validating persistent storage paths..."
    
    local paths=(
        "${READAIRR_CONFIG_PATH:-./docker-data/config}"
        "${READAIRR_DOWNLOADS_PATH:-./docker-data/downloads}"
        "${READAIRR_MEDIA_PATH:-./docker-data/media}"
        "${READAIRR_BACKUP_PATH:-./docker-data/backups}"
    )
    
    for path in "${paths[@]}"; do
        # Convert relative paths to absolute
        if [[ "$path" != /* ]]; then
            path="$PROJECT_DIR/$path"
        fi
        
        if [[ -d "$path" ]]; then
            info "✓ Storage path exists: $path"
        else
            info "Creating storage path: $path"
            mkdir -p "$path" || {
                error "Failed to create storage path: $path"
                return 1
            }
        fi
        
        # Check write permissions
        if [[ -w "$path" ]]; then
            info "✓ Storage path is writable: $path"
        else
            warn "Storage path is not writable: $path"
        fi
    done
}

# Function to test Docker Compose configuration
test_compose_config() {
    log "Testing Docker Compose configuration..."
    
    cd "$PROJECT_DIR"
    
    if docker-compose -f docker-compose.dev.yml config &> /dev/null; then
        log "✓ Docker Compose configuration is valid"
    else
        error "Docker Compose configuration validation failed:"
        docker-compose -f docker-compose.dev.yml config
        return 1
    fi
}

# Function to display startup command
show_startup_commands() {
    log "Network storage test completed successfully!"
    echo
    info "To start ReadAIrr with network storage:"
    echo "  cd $PROJECT_DIR"
    echo "  docker-compose -f docker-compose.dev.yml up -d"
    echo
    info "To view logs:"
    echo "  docker-compose -f docker-compose.dev.yml logs -f readairr-dev"
    echo
    info "To access ReadAIrr:"
    echo "  http://localhost:${READAIRR_PORT:-8246}"
    echo
    
    if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
        info "Network storage mount points (accessible in ReadAIrr UI):"
        [[ -n "$SMB_SHARE_PATH" ]] && echo "  SMB: ${SMB_MOUNT_POINT:-/mnt/smb-media}"
        [[ -n "$NFS_SHARE_PATH" ]] && echo "  NFS: ${NFS_MOUNT_POINT:-/mnt/nfs-media}"
        [[ -n "$SMB_SHARE_PATH_2" ]] && echo "  SMB (secondary): ${SMB_MOUNT_POINT_2:-/mnt/smb-audiobooks}"
        [[ -n "$NFS_SHARE_PATH_2" ]] && echo "  NFS (secondary): ${NFS_MOUNT_POINT_2:-/mnt/nfs-downloads}"
    fi
}

# Function to run interactive setup
interactive_setup() {
    log "Starting interactive network storage setup..."
    
    # Ask if user wants to enable network storage
    echo
    read -p "Enable network storage? (y/n): " -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        info "Network storage disabled"
        return 0
    fi
    
    # Configure SMB
    echo
    read -p "Configure SMB/CIFS share? (y/n): " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo
        read -p "SMB share path (e.g., //nas.local/books): " smb_path
        read -p "SMB username: " smb_user
        read -p "SMB password: " -s smb_pass
        echo
        
        echo "NETWORK_STORAGE_ENABLED=true" >> "$PROJECT_DIR/.env.local"
        echo "SMB_SHARE_PATH=$smb_path" >> "$PROJECT_DIR/.env.local"
        echo "SMB_USERNAME=$smb_user" >> "$PROJECT_DIR/.env.local"
        echo "SMB_PASSWORD=$smb_pass" >> "$PROJECT_DIR/.env.local"
        
        log "SMB configuration added to .env.local"
    fi
    
    # Configure NFS
    echo
    read -p "Configure NFS share? (y/n): " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo
        read -p "NFS share path (e.g., 192.168.1.100:/volume1/books): " nfs_path
        
        echo "NETWORK_STORAGE_ENABLED=true" >> "$PROJECT_DIR/.env.local"
        echo "NFS_SHARE_PATH=$nfs_path" >> "$PROJECT_DIR/.env.local"
        
        log "NFS configuration added to .env.local"
    fi
}

# Main function
main() {
    echo
    log "ReadAIrr Network Storage Test Script"
    log "===================================="
    echo
    
    # Change to project directory
    cd "$PROJECT_DIR"
    
    # Check prerequisites
    check_prerequisites || exit 1
    
    # Check for environment file
    if [[ ! -f ".env.local" ]]; then
        warn "No .env.local file found"
        
        if [[ -f ".env.local.example" ]]; then
            read -p "Copy .env.local.example to .env.local? (y/n): " -n 1 -r
            echo
            
            if [[ $REPLY =~ ^[Yy]$ ]]; then
                cp ".env.local.example" ".env.local"
                log "Created .env.local from example"
                
                # Offer interactive setup
                interactive_setup
            else
                error "Please create .env.local configuration file"
                exit 1
            fi
        else
            error "No .env.local.example found"
            exit 1
        fi
    fi
    
    # Load environment
    load_env || exit 1
    
    # Run validation tests
    validate_network_config || exit 1
    validate_storage_paths || exit 1
    test_compose_config || exit 1
    check_container_images
    test_network_connectivity
    
    # Show startup commands
    show_startup_commands
}

# Run main function
main "$@"
