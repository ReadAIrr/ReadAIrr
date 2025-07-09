#!/bin/bash
# Docker Entrypoint Script for ReadAIrr with Network Storage Support

set -e

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
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

# Cleanup function for graceful shutdown
cleanup() {
    log "Received shutdown signal, cleaning up..."
    
    # Unmount network shares if enabled
    if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
        log "Unmounting network storage..."
        /usr/local/bin/mount-network-storage.sh unmount || warn "Some network shares may not have unmounted cleanly"
    fi
    
    # Kill the ReadAIrr process if running
    if [[ -n "$READAIRR_PID" ]]; then
        log "Stopping ReadAIrr process (PID: $READAIRR_PID)..."
        kill -TERM "$READAIRR_PID" 2>/dev/null || true
        wait "$READAIRR_PID" 2>/dev/null || true
    fi
    
    log "Cleanup completed"
    exit 0
}

# Set up signal handlers
trap cleanup SIGTERM SIGINT

# Function to wait for network connectivity
wait_for_network() {
    local max_attempts=30
    local attempt=1
    
    log "Checking network connectivity..."
    
    while [[ $attempt -le $max_attempts ]]; do
        if ping -c 1 8.8.8.8 >/dev/null 2>&1; then
            log "Network connectivity confirmed"
            return 0
        fi
        
        log "Waiting for network... (attempt $attempt/$max_attempts)"
        sleep 2
        ((attempt++))
    done
    
    warn "Network connectivity check failed after $max_attempts attempts"
    return 1
}

# Function to create necessary directories
create_directories() {
    log "Creating necessary directories..."
    
    # Standard ReadAIrr directories
    mkdir -p /config /books /downloads /media /backups
    
    # Network mount points
    if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
        mkdir -p "$SMB_MOUNT_POINT" "$NFS_MOUNT_POINT"
        
        if [[ -n "$SMB_MOUNT_POINT_2" ]]; then
            mkdir -p "$SMB_MOUNT_POINT_2"
        fi
        
        if [[ -n "$NFS_MOUNT_POINT_2" ]]; then
            mkdir -p "$NFS_MOUNT_POINT_2"
        fi
    fi
}

# Function to set up network storage
setup_network_storage() {
    if [[ "${NETWORK_STORAGE_ENABLED,,}" != "true" ]]; then
        log "Network storage is disabled"
        return 0
    fi
    
    log "Setting up network storage..."
    
    # Wait for network if mounting network shares
    if [[ -n "$SMB_SHARE_PATH" ]] || [[ -n "$NFS_SHARE_PATH" ]]; then
        wait_for_network || warn "Proceeding without network connectivity confirmation"
    fi
    
    # Mount network shares
    /usr/local/bin/mount-network-storage.sh mount || warn "Some network shares failed to mount"
}

# Function to validate configuration
validate_config() {
    log "Validating configuration..."
    
    # Check if we have a valid configuration directory
    if [[ ! -d "/config" ]]; then
        error "Configuration directory /config does not exist"
        return 1
    fi
    
    # Validate network storage configuration if enabled
    if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
        local has_smb=false
        local has_nfs=false
        
        if [[ -n "$SMB_SHARE_PATH" ]] && [[ -n "$SMB_MOUNT_POINT" ]]; then
            has_smb=true
            log "SMB storage configured: $SMB_SHARE_PATH -> $SMB_MOUNT_POINT"
        fi
        
        if [[ -n "$NFS_SHARE_PATH" ]] && [[ -n "$NFS_MOUNT_POINT" ]]; then
            has_nfs=true
            log "NFS storage configured: $NFS_SHARE_PATH -> $NFS_MOUNT_POINT"
        fi
        
        if [[ "$has_smb" == "false" ]] && [[ "$has_nfs" == "false" ]]; then
            warn "Network storage is enabled but no shares are configured"
        fi
    fi
    
    return 0
}

# Function to display startup information
display_startup_info() {
    log "==============================================="
    log "ReadAIrr Docker Container Starting"
    log "==============================================="
    log "Version: ReadAIrr v11.0.0"
    log "Environment: Development"
    log "Config Directory: /config"
    log "Downloads Directory: /downloads"
    log "Media Directory: /media"
    log "Network Storage: ${NETWORK_STORAGE_ENABLED:-false}"
    
    if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
        [[ -n "$SMB_SHARE_PATH" ]] && log "SMB Share: $SMB_SHARE_PATH"
        [[ -n "$NFS_SHARE_PATH" ]] && log "NFS Share: $NFS_SHARE_PATH"
    fi
    
    log "==============================================="
}

# Main entrypoint logic
main() {
    display_startup_info
    
    # Validate configuration
    validate_config || exit 1
    
    # Create necessary directories
    create_directories
    
    # Set up network storage
    setup_network_storage
    
    # Start ReadAIrr
    log "Starting ReadAIrr application..."
    
    # Execute the provided command (ReadAIrr)
    exec "$@" &
    READAIRR_PID=$!
    
    log "ReadAIrr started with PID: $READAIRR_PID"
    log "ReadAIrr is now running and accessible on port 8787"
    
    # Wait for the process to finish
    wait $READAIRR_PID
    READAIRR_EXIT_CODE=$?
    
    log "ReadAIrr process exited with code: $READAIRR_EXIT_CODE"
    
    # Clean up network mounts on exit
    if [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]; then
        log "Cleaning up network storage..."
        /usr/local/bin/mount-network-storage.sh unmount || warn "Failed to unmount some network shares"
    fi
    
    exit $READAIRR_EXIT_CODE
}

# Run main function with all arguments
main "$@"
