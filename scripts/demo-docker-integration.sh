#!/bin/bash
# ReadAIrr Docker Integration Demo Script
# 
# This script demonstrates the complete ReadAIrr Docker setup workflow
# including network storage configuration and testing.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[ReadAIrr] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[ReadAIrr] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[ReadAIrr] ERROR: $1${NC}"
}

info() {
    echo -e "${BLUE}[ReadAIrr] INFO: $1${NC}"
}

step() {
    echo -e "${CYAN}[ReadAIrr] STEP: $1${NC}"
}

# Function to display header
show_header() {
    echo
    log "======================================================"
    log "ReadAIrr Docker Environment Integration Demo"
    log "======================================================"
    echo
    info "This script demonstrates ReadAIrr's Docker environment"
    info "with SMB/NFS network storage support."
    echo
}

# Function to check prerequisites
check_prerequisites() {
    step "Checking prerequisites..."
    
    local missing_deps=()
    
    if ! command -v docker &> /dev/null; then
        missing_deps+=("docker")
    fi
    
    if ! docker compose version &> /dev/null && ! docker-compose --version &> /dev/null; then
        missing_deps+=("docker-compose")
    fi
    
    if [[ ${#missing_deps[@]} -gt 0 ]]; then
        error "Missing required dependencies: ${missing_deps[*]}"
        echo
        info "Please install the missing dependencies:"
        info "  - Docker: https://docs.docker.com/get-docker/"
        info "  - Docker Compose: https://docs.docker.com/compose/install/"
        return 1
    fi
    
    if ! docker info &> /dev/null; then
        error "Docker daemon is not running"
        info "Please start Docker and try again"
        return 1
    fi
    
    log "✓ All prerequisites are available"
}

# Function to validate configuration
validate_configuration() {
    step "Validating Docker configuration..."
    
    cd "$PROJECT_DIR"
    
    if ! ./scripts/validate-docker-config.sh > /dev/null 2>&1; then
        error "Configuration validation failed"
        info "Running detailed validation:"
        ./scripts/validate-docker-config.sh
        return 1
    fi
    
    log "✓ Docker configuration is valid"
}

# Function to show environment setup options
show_environment_options() {
    step "Environment configuration options..."
    echo
    info "ReadAIrr Docker environment supports:"
    echo "  • Persistent storage for config, downloads, and media"
    echo "  • SMB/CIFS network share mounting"
    echo "  • NFS network share mounting"
    echo "  • Multiple network shares"
    echo "  • Development mode with live reload"
    echo
    
    if [[ -f ".env.local" ]]; then
        info "Found existing .env.local configuration"
        
        if grep -q "NETWORK_STORAGE_ENABLED=true" .env.local; then
            log "✓ Network storage is enabled"
            
            # Show configured shares
            if grep -q "^SMB_SHARE_PATH=" .env.local; then
                local smb_path=$(grep "^SMB_SHARE_PATH=" .env.local | cut -d'=' -f2)
                [[ -n "$smb_path" ]] && info "  SMB share: $smb_path"
            fi
            
            if grep -q "^NFS_SHARE_PATH=" .env.local; then
                local nfs_path=$(grep "^NFS_SHARE_PATH=" .env.local | cut -d'=' -f2)
                [[ -n "$nfs_path" ]] && info "  NFS share: $nfs_path"
            fi
        else
            warn "Network storage is disabled"
            info "To enable network storage, edit .env.local and set:"
            info "  NETWORK_STORAGE_ENABLED=true"
        fi
    else
        warn "No .env.local configuration found"
        
        read -p "Create .env.local from example? (y/n): " -n 1 -r
        echo
        
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            cp .env.local.example .env.local
            log "Created .env.local from example"
            info "Edit .env.local to configure network storage settings"
        else
            info "You can create .env.local later:"
            info "  cp .env.local.example .env.local"
        fi
    fi
}

# Function to build and start containers
build_and_start() {
    step "Building and starting ReadAIrr containers..."
    
    local compose_cmd="docker compose"
    if ! command -v "docker compose" &> /dev/null; then
        compose_cmd="docker-compose"
    fi
    
    info "Building ReadAIrr development image..."
    $compose_cmd -f docker-compose.dev.yml build
    
    info "Starting ReadAIrr container..."
    $compose_cmd -f docker-compose.dev.yml up -d
    
    log "✓ ReadAIrr container started"
}

# Function to check service health
check_service_health() {
    step "Checking ReadAIrr service health..."
    
    local max_attempts=30
    local attempt=1
    local port=${READAIRR_PORT:-8246}
    
    while [[ $attempt -le $max_attempts ]]; do
        if curl -f "http://localhost:$port/api/v1/system/status" >/dev/null 2>&1; then
            log "✓ ReadAIrr is responding on port $port"
            return 0
        fi
        
        info "Waiting for ReadAIrr to start... (attempt $attempt/$max_attempts)"
        sleep 5
        ((attempt++))
    done
    
    error "ReadAIrr failed to start after $max_attempts attempts"
    return 1
}

# Function to show network storage status
show_network_storage_status() {
    step "Checking network storage status..."
    
    local compose_cmd="docker compose"
    if ! command -v "docker compose" &> /dev/null; then
        compose_cmd="docker-compose"
    fi
    
    # Check if network storage is enabled
    if ! grep -q "NETWORK_STORAGE_ENABLED=true" .env.local 2>/dev/null; then
        info "Network storage is disabled"
        return 0
    fi
    
    info "Network storage is enabled, checking mounts..."
    
    # Check mounts inside container
    if $compose_cmd -f docker-compose.dev.yml exec -T readairr-dev mount | grep -E "(cifs|nfs)" >/dev/null 2>&1; then
        log "✓ Network shares are mounted:"
        $compose_cmd -f docker-compose.dev.yml exec -T readairr-dev mount | grep -E "(cifs|nfs)" | while read -r line; do
            info "  $line"
        done
    else
        warn "No network shares are currently mounted"
        info "Check container logs for mount errors:"
        info "  $compose_cmd -f docker-compose.dev.yml logs readairr-dev"
    fi
}

# Function to show access information
show_access_info() {
    step "ReadAIrr access information..."
    
    local port=${READAIRR_PORT:-8246}
    
    echo
    log "🎉 ReadAIrr is ready!"
    echo
    info "Web Interface:"
    info "  URL: http://localhost:$port"
    info "  Default credentials: None (first-time setup)"
    echo
    info "API Access:"
    info "  Base URL: http://localhost:$port/api/v1"
    info "  Status: http://localhost:$port/api/v1/system/status"
    echo
    
    if grep -q "NETWORK_STORAGE_ENABLED=true" .env.local 2>/dev/null; then
        info "Network Storage Mount Points (in ReadAIrr UI):"
        info "  Primary SMB: /mnt/smb-media"
        info "  Primary NFS: /mnt/nfs-media"
        info "  Secondary SMB: /mnt/smb-audiobooks"
        info "  Secondary NFS: /mnt/nfs-downloads"
        echo
        info "To configure media libraries:"
        info "  1. Access ReadAIrr web interface"
        info "  2. Go to Settings > Media Management"
        info "  3. Add root folders using the mount points above"
    fi
}

# Function to show useful commands
show_useful_commands() {
    step "Useful commands..."
    
    local compose_cmd="docker compose"
    if ! command -v "docker compose" &> /dev/null; then
        compose_cmd="docker-compose"
    fi
    
    echo
    info "Container Management:"
    echo "  # View logs"
    echo "    $compose_cmd -f docker-compose.dev.yml logs -f readairr-dev"
    echo
    echo "  # Access container shell"
    echo "    $compose_cmd -f docker-compose.dev.yml exec readairr-dev bash"
    echo
    echo "  # Restart container"
    echo "    $compose_cmd -f docker-compose.dev.yml restart readairr-dev"
    echo
    echo "  # Stop all services"
    echo "    $compose_cmd -f docker-compose.dev.yml down"
    echo
    info "Network Storage:"
    echo "  # Test network storage configuration"
    echo "    ./scripts/test-network-storage.sh"
    echo
    echo "  # Remount network shares"
    echo "    $compose_cmd -f docker-compose.dev.yml exec readairr-dev /usr/local/bin/mount-network-storage.sh remount"
    echo
    echo "  # Check mount status"
    echo "    $compose_cmd -f docker-compose.dev.yml exec readairr-dev mount | grep -E '(cifs|nfs)'"
    echo
    info "Development:"
    echo "  # Build frontend"
    echo "    npm run build --prefix frontend"
    echo
    echo "  # Build backend"
    echo "    dotnet publish -c Release"
    echo
}

# Function to handle cleanup on exit
cleanup() {
    if [[ -n "$DEMO_STARTED" ]]; then
        echo
        warn "Demo interrupted"
        info "To stop ReadAIrr:"
        
        local compose_cmd="docker compose"
        if ! command -v "docker compose" &> /dev/null; then
            compose_cmd="docker-compose"
        fi
        
        echo "  $compose_cmd -f docker-compose.dev.yml down"
    fi
}

# Set up signal handlers
trap cleanup SIGINT SIGTERM

# Main function
main() {
    cd "$PROJECT_DIR"
    
    show_header
    
    # Check if this is a dry run
    if [[ "${1:-}" == "--dry-run" ]]; then
        info "Running in dry-run mode (no containers will be started)"
        echo
    fi
    
    # Run checks
    check_prerequisites || exit 1
    echo
    
    validate_configuration || exit 1
    echo
    
    show_environment_options
    echo
    
    # Ask user if they want to proceed
    if [[ "${1:-}" != "--dry-run" ]]; then
        read -p "Start ReadAIrr Docker environment? (y/n): " -n 1 -r
        echo
        
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            info "Skipping container startup"
            info "To start manually:"
            info "  docker compose -f docker-compose.dev.yml up -d"
            exit 0
        fi
        
        export DEMO_STARTED=1
        
        build_and_start
        echo
        
        check_service_health || {
            error "ReadAIrr failed to start properly"
            info "Check logs for details:"
            info "  docker compose -f docker-compose.dev.yml logs readairr-dev"
            exit 1
        }
        echo
        
        show_network_storage_status
        echo
        
        show_access_info
        echo
        
        show_useful_commands
        echo
        
        log "ReadAIrr Docker environment demo completed successfully!"
    else
        info "Dry run completed - configuration is valid"
        info "To start ReadAIrr:"
        info "  $0"
    fi
}

# Run main function
main "$@"
