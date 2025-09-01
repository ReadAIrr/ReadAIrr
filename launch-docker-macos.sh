#!/bin/bash
# ReadAIrr Docker Launch Script for macOS
# This script helps you build and run ReadAIrr in a Docker container

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[ReadAIrr Docker] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[ReadAIrr Docker] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[ReadAIrr Docker] ERROR: $1${NC}"
}

info() {
    echo -e "${BLUE}[ReadAIrr Docker] $1${NC}"
}

# Check if Docker is running
check_docker() {
    if ! docker info >/dev/null 2>&1; then
        error "Docker is not running. Please start Docker Desktop on your macOS."
        exit 1
    fi
    log "Docker is running ✓"
}

# Create necessary directories
create_directories() {
    log "Creating Docker data directories..."
    
    mkdir -p docker-data/{config,downloads,media,backups}
    
    # Set proper permissions (important for Docker on macOS)
    chmod -R 755 docker-data/
    
    log "Created directories:"
    log "  - docker-data/config (ReadAIrr configuration)"
    log "  - docker-data/downloads (Downloaded books)"
    log "  - docker-data/media (Organized library)"
    log "  - docker-data/backups (Database backups)"
}

# Build the ReadAIrr application
build_app() {
    log "Building ReadAIrr application..."
    
    # Ensure output directory exists
    rm -rf _output/app
    mkdir -p _output/app
    
    # Build and publish the .NET application
    dotnet publish src/NzbDrone.Console/Readarr.Console.csproj \
        --configuration Release \
        --framework net6.0 \
        --runtime linux-x64 \
        --self-contained false \
        --output _output/app \
        -p:PublishSingleFile=false \
        -p:PublishTrimmed=false
    
    # Copy frontend build
    if [ -d "frontend/build" ]; then
        log "Copying frontend assets..."
        cp -r frontend/build/* _output/app/UI/ 2>/dev/null || {
            warn "Frontend build not found. Building frontend..."
            cd frontend
            npm install
            npm run build
            cd ..
            cp -r frontend/build/* _output/app/UI/
        }
    else
        warn "Frontend not built. The web UI may not work properly."
    fi
    
    log "Application built successfully ✓"
}

# Build Docker image
build_docker_image() {
    log "Building Docker image..."
    
    docker build \
        -f Dockerfile.simple \
        -t readairr-local:latest \
        .
    
    log "Docker image built successfully ✓"
}

# Create environment file
create_env_file() {
    if [ ! -f ".env.local" ]; then
        log "Creating environment configuration..."
        
        cat > .env.local << 'EOF'
# ReadAIrr Docker Configuration for macOS

# Application settings
READAIRR_PORT=8246
READAIRR_LOG_LEVEL=info
READAIRR_ANALYTICS_ENABLED=false
READAIRR_AUTH_REQUIRED=false

# Docker settings
READAIRR_IMAGE_NAME=readairr-local
READAIRR_CONTAINER_NAME=readairr-macos

# Storage paths (relative to project directory)
READAIRR_CONFIG_PATH=./docker-data/config
READAIRR_DOWNLOADS_PATH=./docker-data/downloads  
READAIRR_MEDIA_PATH=./docker-data/media
READAIRR_BACKUP_PATH=./docker-data/backups

# Network storage (SMB share integration)
NETWORK_STORAGE_ENABLED=false
# Uncomment and configure for your SMB shares:
# SMB_SHARE_PATH=//your-nas.local/books
# SMB_USERNAME=your-username
# SMB_PASSWORD=your-password
# SMB_MOUNT_POINT=/mnt/smb-media
EOF
        
        log "Created .env.local configuration file"
        log "Edit .env.local to configure SMB shares if needed"
    else
        log "Using existing .env.local configuration"
    fi
}

# Create docker-compose file for macOS
create_docker_compose() {
    log "Creating docker-compose configuration for macOS..."
    
    cat > docker-compose.macos.yml << 'EOF'
version: '3.8'

services:
  readairr:
    image: readairr-local:latest
    container_name: readairr-macos
    restart: unless-stopped
    
    ports:
      - "8246:8246"
    
    environment:
      - READAIRR_LOG_LEVEL=info
      - READAIRR_ANALYTICS_ENABLED=false
      - READAIRR_AUTH_REQUIRED=false
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8246
      # Network storage settings (sourced from .env.local)
      - NETWORK_STORAGE_ENABLED=${NETWORK_STORAGE_ENABLED:-false}
      - SMB_SHARE_PATH=${SMB_SHARE_PATH:-}
      - SMB_USERNAME=${SMB_USERNAME:-}
      - SMB_PASSWORD=${SMB_PASSWORD:-}
      - SMB_DOMAIN=${SMB_DOMAIN:-}
      - SMB_VERSION=${SMB_VERSION:-3.0}
      - SMB_MOUNT_POINT=${SMB_MOUNT_POINT:-/mnt/smb-media}
    
    volumes:
      - "${READAIRR_CONFIG_PATH:-./docker-data/config}:/config"
      - "${READAIRR_DOWNLOADS_PATH:-./docker-data/downloads}:/downloads"
      - "${READAIRR_MEDIA_PATH:-./docker-data/media}:/media"
      - "${READAIRR_BACKUP_PATH:-./docker-data/backups}:/backups"
    
    # Required for network storage mounting
    privileged: true
    cap_add:
      - SYS_ADMIN
    devices:
      - /dev/fuse
    
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8246/api/v1/system/status"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s

networks:
  default:
    name: readairr-network
    driver: bridge
EOF
    
    log "Created docker-compose.macos.yml"
}

# Run the container
run_container() {
    log "Starting ReadAIrr container..."
    
    # Load environment variables
    if [ -f ".env.local" ]; then
        set -a
        source .env.local
        set +a
    fi
    
    # Start the container
    docker-compose -f docker-compose.macos.yml up -d
    
    log "ReadAIrr container started successfully ✓"
    log ""
    log "🚀 ReadAIrr is now running!"
    log "   Web UI: http://localhost:8246"
    log "   Container: readairr-macos"
    log ""
    log "📁 Data directories:"
    log "   Config: $(pwd)/docker-data/config"
    log "   Downloads: $(pwd)/docker-data/downloads"
    log "   Media: $(pwd)/docker-data/media"
    log "   Backups: $(pwd)/docker-data/backups"
    log ""
    log "🔧 Management commands:"
    log "   View logs: docker logs -f readairr-macos"
    log "   Stop: docker-compose -f docker-compose.macos.yml down"
    log "   Restart: docker-compose -f docker-compose.macos.yml restart"
}

# Show help
show_help() {
    echo "ReadAIrr Docker Launch Script for macOS"
    echo ""
    echo "Usage: $0 [command]"
    echo ""
    echo "Commands:"
    echo "  build     - Build the application and Docker image"
    echo "  run       - Run the container (builds if needed)"
    echo "  stop      - Stop the container"
    echo "  restart   - Restart the container" 
    echo "  logs      - Show container logs"
    echo "  clean     - Remove container and image"
    echo "  setup     - Complete setup (build + run)"
    echo "  help      - Show this help"
    echo ""
    echo "Examples:"
    echo "  $0 setup     # Complete setup and launch"
    echo "  $0 build     # Just build without running"
    echo "  $0 run       # Run (will build if needed)"
}

# Main script logic
case "${1:-setup}" in
    "build")
        check_docker
        create_directories
        build_app
        build_docker_image
        ;;
    
    "run")
        check_docker
        create_directories
        create_env_file
        create_docker_compose
        
        # Build if image doesn't exist
        if ! docker images -q readairr-local:latest >/dev/null 2>&1; then
            log "Docker image not found. Building..."
            build_app
            build_docker_image
        fi
        
        run_container
        ;;
    
    "setup")
        check_docker
        create_directories
        create_env_file
        create_docker_compose
        build_app
        build_docker_image
        run_container
        ;;
    
    "stop")
        log "Stopping ReadAIrr container..."
        docker-compose -f docker-compose.macos.yml down
        log "Container stopped ✓"
        ;;
    
    "restart")
        log "Restarting ReadAIrr container..."
        docker-compose -f docker-compose.macos.yml restart
        log "Container restarted ✓"
        ;;
    
    "logs")
        docker logs -f readairr-macos
        ;;
    
    "clean")
        warn "This will remove the container and image. Data in docker-data/ will be preserved."
        read -p "Continue? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            docker-compose -f docker-compose.macos.yml down
            docker rmi readairr-local:latest 2>/dev/null || true
            log "Cleanup completed ✓"
        fi
        ;;
    
    "help"|*)
        show_help
        ;;
esac
