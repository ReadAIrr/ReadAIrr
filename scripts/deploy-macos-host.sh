#!/bin/bash
# ReadAIrr Docker Deployment Script for macOS Host
# Run this script on your macOS machine (not in the dev container)

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[DEPLOY] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[DEPLOY] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[DEPLOY] ERROR: $1${NC}"
}

info() {
    echo -e "${BLUE}[DEPLOY] INFO: $1${NC}"
}

step() {
    echo -e "${CYAN}[DEPLOY] STEP: $1${NC}"
}

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Function to check prerequisites
check_prerequisites() {
    step "Checking deployment prerequisites..."
    
    # Check if Docker is available
    if ! command -v docker &> /dev/null; then
        error "Docker is not installed or not in PATH"
        info "Please install Docker Desktop for macOS"
        return 1
    fi
    
    # Check if Docker is running
    if ! docker info &> /dev/null; then
        error "Docker is not running"
        info "Please start Docker Desktop and try again"
        return 1
    fi
    
    # Check for Docker Compose
    local compose_cmd=""
    if command -v "docker compose" &> /dev/null; then
        compose_cmd="docker compose"
    elif command -v "docker-compose" &> /dev/null; then
        compose_cmd="docker-compose"
    else
        error "Docker Compose is not available"
        info "Please install Docker Compose or update Docker Desktop"
        return 1
    fi
    
    # Check if lsof is available for port checking
    if ! command -v lsof &> /dev/null; then
        warn "lsof not available - port conflict detection will be limited"
    fi
    
    export COMPOSE_CMD="$compose_cmd"
    log "✓ Docker and Docker Compose are available"
    info "Using: $compose_cmd"
}

# Function to validate project structure
validate_project() {
    step "Validating project structure..."
    
    cd "$PROJECT_DIR"
    
    # Check required files
    local required_files=(
        ".env.local"
        "docker-compose.dev.yml"
        "Dockerfile.dev"
        "_output/net6.0/linux-x64/publish/Readarr"
    )
    
    for file in "${required_files[@]}"; do
        if [[ -f "$file" ]]; then
            info "✓ Found: $file"
        else
            error "✗ Missing: $file"
            return 1
        fi
    done
    
    log "✓ Project structure is valid"
}

# Function to create storage directories
create_storage_dirs() {
    step "Creating storage directories..."
    
    # Load environment variables to get paths
    set -a
    source .env.local
    set +a
    
    local dirs=(
        "${READAIRR_CONFIG_PATH:-./docker-data/config}"
        "${READAIRR_DOWNLOADS_PATH:-./docker-data/downloads}"
        "${READAIRR_MEDIA_PATH:-./docker-data/media}"
        "${READAIRR_BACKUP_PATH:-./docker-data/backups}"
    )
    
    for dir in "${dirs[@]}"; do
        # Convert relative paths to absolute
        if [[ "$dir" != /* ]]; then
            dir="$PROJECT_DIR/$dir"
        fi
        
        if [[ -d "$dir" ]]; then
            info "✓ Directory exists: $dir"
        else
            info "Creating directory: $dir"
            mkdir -p "$dir"
            log "✓ Created: $dir"
        fi
    done
}

# Function to stop existing containers
stop_existing() {
    step "Stopping existing containers and freeing port 8246..."
    
    # Load environment to get the port
    set -a
    source .env.local
    set +a
    local port=${READAIRR_PORT:-8246}
    
    # Find and stop any containers using port 8246
    info "Checking for containers using port $port..."
    local containers_on_port=$(docker ps --format "table {{.ID}}\t{{.Names}}\t{{.Ports}}" | grep ":$port->" | awk '{print $1}' || true)
    
    if [[ -n "$containers_on_port" ]]; then
        info "Found containers using port $port:"
        docker ps --format "table {{.ID}}\t{{.Names}}\t{{.Ports}}" | grep ":$port->" || true
        
        echo "$containers_on_port" | while read -r container_id; do
            if [[ -n "$container_id" ]]; then
                local container_name=$(docker ps --format "{{.Names}}" --filter "id=$container_id")
                info "Stopping container: $container_name ($container_id)"
                docker stop "$container_id" || warn "Failed to stop container $container_id"
            fi
        done
        
        log "✓ Stopped containers using port $port"
    else
        info "No containers currently using port $port"
    fi
    
    # Stop any running ReadAIrr containers via compose
    if docker ps -q --filter "name=readairr" | grep -q .; then
        info "Stopping existing ReadAIrr containers via docker-compose..."
        $COMPOSE_CMD -f docker-compose.dev.yml down || warn "Failed to stop some ReadAIrr containers"
    else
        info "No existing ReadAIrr containers running"
    fi
    
    # Remove any existing ReadAIrr images to force rebuild
    if docker images -q readairr-dev | grep -q .; then
        info "Removing existing ReadAIrr images to force rebuild..."
        docker images -q readairr-dev | xargs docker rmi -f || warn "Failed to remove some images"
    fi
    
    # Double-check that port is free
    info "Verifying port $port is free..."
    if lsof -i :$port >/dev/null 2>&1; then
        warn "Port $port may still be in use by a non-Docker process"
        info "Processes using port $port:"
        lsof -i :$port || warn "Could not list processes using port $port"
    else
        log "✓ Port $port is now free"
    fi
}

# Function to build Docker image
build_image() {
    step "Building ReadAIrr Docker image..."
    
    info "Building development image with network storage support..."
    $COMPOSE_CMD -f docker-compose.dev.yml build --no-cache
    
    log "✓ Docker image built successfully"
}

# Function to start containers
start_containers() {
    step "Starting ReadAIrr containers..."
    
    # Load environment to get the port
    set -a
    source .env.local
    set +a
    local port=${READAIRR_PORT:-8246}
    
    # Final check that port is free before starting
    info "Final check that port $port is available..."
    if docker ps --format "{{.Ports}}" | grep -q ":$port->"; then
        error "Port $port is still in use by a Docker container"
        info "Containers currently using port $port:"
        docker ps --format "table {{.ID}}\t{{.Names}}\t{{.Ports}}" | grep ":$port->" || true
        return 1
    fi
    
    if command -v lsof &> /dev/null && lsof -i :$port >/dev/null 2>&1; then
        warn "Port $port appears to be in use by a non-Docker process"
        info "Processes using port $port:"
        lsof -i :$port || true
        
        read -p "Continue anyway? (y/n): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            error "Deployment cancelled due to port conflict"
            return 1
        fi
    fi
    
    info "Starting ReadAIrr with network storage configuration..."
    $COMPOSE_CMD -f docker-compose.dev.yml up -d
    
    log "✓ ReadAIrr containers started"
}

# Function to check service health
check_health() {
    step "Checking ReadAIrr service health..."
    
    local port=${READAIRR_PORT:-8246}
    local max_attempts=60  # 5 minutes
    local attempt=1
    
    info "Waiting for ReadAIrr to start on port $port..."
    
    while [[ $attempt -le $max_attempts ]]; do
        if curl -f "http://localhost:$port/api/v1/system/status" >/dev/null 2>&1; then
            log "✓ ReadAIrr is responding on port $port"
            return 0
        fi
        
        if [[ $((attempt % 6)) -eq 0 ]]; then
            info "Still waiting for ReadAIrr... (attempt $attempt/$max_attempts)"
        fi
        
        sleep 5
        ((attempt++))
    done
    
    error "ReadAIrr failed to start after $max_attempts attempts"
    info "Check logs with: $COMPOSE_CMD -f docker-compose.dev.yml logs readairr-dev"
    return 1
}

# Function to check network storage
check_network_storage() {
    step "Checking network storage status..."
    
    # Load environment to check if network storage is enabled
    set -a
    source .env.local
    set +a
    
    if [[ "${NETWORK_STORAGE_ENABLED,,}" != "true" ]]; then
        info "Network storage is disabled"
        return 0
    fi
    
    info "Network storage is enabled, checking mounts..."
    
    # Check network mounts
    if $COMPOSE_CMD -f docker-compose.dev.yml exec -T readairr-dev mount | grep -E "(cifs|nfs)" >/dev/null 2>&1; then
        log "✓ Network shares are mounted:"
        $COMPOSE_CMD -f docker-compose.dev.yml exec -T readairr-dev mount | grep -E "(cifs|nfs)" | while read -r line; do
            info "  $line"
        done
    else
        warn "No network shares are currently mounted"
        warn "Check container logs for mount errors:"
        warn "  $COMPOSE_CMD -f docker-compose.dev.yml logs readairr-dev"
    fi
}

# Function to show deployment info
show_deployment_info() {
    step "ReadAIrr deployment completed!"
    
    local port=${READAIRR_PORT:-8246}
    
    echo
    log "🎉 ReadAIrr is now running!"
    echo
    info "Access Information:"
    info "  Web Interface: http://localhost:$port"
    info "  API Base URL: http://localhost:$port/api/v1"
    info "  Status Check: http://localhost:$port/api/v1/system/status"
    echo
    
    # Show network storage info if enabled
    set -a
    source .env.local
    set +a
    
    if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
        info "Network Storage Mount Points (use these in ReadAIrr):"
        [[ -n "$SMB_SHARE_PATH" ]] && info "  SMB Share: ${SMB_MOUNT_POINT:-/mnt/smb-media}"
        [[ -n "$NFS_SHARE_PATH" ]] && info "  NFS Share: ${NFS_MOUNT_POINT:-/mnt/nfs-media}"
        echo
    fi
    
    info "Useful Commands:"
    echo "  # View logs"
    echo "    $COMPOSE_CMD -f docker-compose.dev.yml logs -f readairr-dev"
    echo
    echo "  # Access container shell"
    echo "    $COMPOSE_CMD -f docker-compose.dev.yml exec readairr-dev bash"
    echo
    echo "  # Check network mounts"
    echo "    $COMPOSE_CMD -f docker-compose.dev.yml exec readairr-dev mount | grep -E '(cifs|nfs)'"
    echo
    echo "  # Restart ReadAIrr"
    echo "    $COMPOSE_CMD -f docker-compose.dev.yml restart readairr-dev"
    echo
    echo "  # Stop all services"
    echo "    $COMPOSE_CMD -f docker-compose.dev.yml down"
    echo
}

# Function to show container logs
show_logs() {
    step "Showing recent container logs..."
    
    info "Last 50 lines of ReadAIrr logs:"
    echo "----------------------------------------"
    $COMPOSE_CMD -f docker-compose.dev.yml logs --tail=50 readairr-dev || warn "Failed to retrieve logs"
    echo "----------------------------------------"
}

# Main deployment function
main() {
    echo
    log "======================================================="
    log "ReadAIrr Docker Deployment Script"
    log "======================================================="
    echo
    info "Deploying ReadAIrr with network storage support..."
    echo
    
    # Change to project directory
    cd "$PROJECT_DIR"
    
    # Run deployment steps
    check_prerequisites || exit 1
    echo
    
    validate_project || exit 1
    echo
    
    create_storage_dirs
    echo
    
    stop_existing
    echo
    
    build_image || exit 1
    echo
    
    start_containers || exit 1
    echo
    
    check_health || {
        error "Deployment failed - ReadAIrr did not start properly"
        show_logs
        exit 1
    }
    echo
    
    check_network_storage
    echo
    
    show_deployment_info
    echo
    
    # Ask if user wants to see logs
    read -p "Would you like to see the live logs? (y/n): " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        info "Showing live logs (press Ctrl+C to stop):"
        $COMPOSE_CMD -f docker-compose.dev.yml logs -f readairr-dev
    else
        info "Deployment completed successfully!"
        info "Access ReadAIrr at: http://localhost:$port"
    fi
}

# Handle interruption gracefully
trap 'echo; warn "Deployment interrupted by user"; exit 1' INT

# Run main function
main "$@"
