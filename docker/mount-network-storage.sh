#!/bin/bash
# Network Storage Mounting Script for ReadAIrr Docker Container

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[$(date +'%Y-%m-%d %H:%M:%S')] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}"
}

# Function to mount SMB share
mount_smb() {
    local share_path="$1"
    local mount_point="$2"
    local username="$3"
    local password="$4"
    local domain="$5"
    local version="$6"
    
    if [[ -z "$share_path" ]]; then
        warn "SMB share path not configured, skipping SMB mount"
        return 0
    fi
    
    log "Mounting SMB share: $share_path -> $mount_point"
    
    # Create mount point if it doesn't exist
    sudo mkdir -p "$mount_point"
    
    # Build mount options
    local mount_opts="rw,uid=1000,gid=1000,iocharset=utf8,file_mode=0664,dir_mode=0775"
    
    if [[ -n "$version" ]]; then
        mount_opts="$mount_opts,vers=$version"
    fi
    
    if [[ -n "$username" ]]; then
        mount_opts="$mount_opts,username=$username"
        
        if [[ -n "$password" ]]; then
            mount_opts="$mount_opts,password=$password"
        fi
        
        if [[ -n "$domain" ]]; then
            mount_opts="$mount_opts,domain=$domain"
        fi
    else
        mount_opts="$mount_opts,guest"
    fi
    
    # Attempt to mount
    if sudo mount -t cifs "$share_path" "$mount_point" -o "$mount_opts"; then
        log "Successfully mounted SMB share: $mount_point"
        return 0
    else
        error "Failed to mount SMB share: $share_path"
        return 1
    fi
}

# Function to mount NFS share
mount_nfs() {
    local share_path="$1"
    local mount_point="$2"
    local options="$3"
    
    if [[ -z "$share_path" ]]; then
        warn "NFS share path not configured, skipping NFS mount"
        return 0
    fi
    
    log "Mounting NFS share: $share_path -> $mount_point"
    
    # Create mount point if it doesn't exist
    sudo mkdir -p "$mount_point"
    
    # Default NFS options if not provided
    if [[ -z "$options" ]]; then
        options="rw,hard,intr,rsize=8192,wsize=8192,timeo=14"
    fi
    
    # Attempt to mount
    if sudo mount -t nfs "$share_path" "$mount_point" -o "$options"; then
        log "Successfully mounted NFS share: $mount_point"
        return 0
    else
        error "Failed to mount NFS share: $share_path"
        return 1
    fi
}

# Function to check if network storage is enabled
is_network_storage_enabled() {
    [[ "${NETWORK_STORAGE_ENABLED,,}" == "true" ]]
}

# Main mounting logic
main() {
    if ! is_network_storage_enabled; then
        log "Network storage mounting is disabled"
        return 0
    fi
    
    log "Network storage mounting enabled, proceeding with mounts..."
    
    # Mount primary SMB share
    if [[ -n "$SMB_SHARE_PATH" ]]; then
        mount_smb "$SMB_SHARE_PATH" "$SMB_MOUNT_POINT" "$SMB_USERNAME" "$SMB_PASSWORD" "$SMB_DOMAIN" "$SMB_VERSION"
    fi
    
    # Mount primary NFS share
    if [[ -n "$NFS_SHARE_PATH" ]]; then
        mount_nfs "$NFS_SHARE_PATH" "$NFS_MOUNT_POINT" "$NFS_OPTIONS"
    fi
    
    # Mount additional SMB shares
    if [[ -n "$SMB_SHARE_PATH_2" ]]; then
        mount_smb "$SMB_SHARE_PATH_2" "$SMB_MOUNT_POINT_2" "$SMB_USERNAME" "$SMB_PASSWORD" "$SMB_DOMAIN" "$SMB_VERSION"
    fi
    
    # Mount additional NFS shares
    if [[ -n "$NFS_SHARE_PATH_2" ]]; then
        mount_nfs "$NFS_SHARE_PATH_2" "$NFS_MOUNT_POINT_2" "$NFS_OPTIONS"
    fi
    
    # List mounted network shares
    log "Current network mounts:"
    mount | grep -E "(cifs|nfs)" || log "No network shares mounted"
}

# Unmount function for cleanup
unmount_all() {
    log "Unmounting all network shares..."
    
    # Unmount all CIFS/SMB shares
    mount | grep cifs | awk '{print $3}' | while read -r mount_point; do
        log "Unmounting SMB share: $mount_point"
        sudo umount "$mount_point" || warn "Failed to unmount $mount_point"
    done
    
    # Unmount all NFS shares
    mount | grep nfs | awk '{print $3}' | while read -r mount_point; do
        log "Unmounting NFS share: $mount_point"
        sudo umount "$mount_point" || warn "Failed to unmount $mount_point"
    done
}

# Handle script arguments
case "${1:-mount}" in
    "mount")
        main
        ;;
    "unmount")
        unmount_all
        ;;
    "remount")
        unmount_all
        sleep 2
        main
        ;;
    *)
        echo "Usage: $0 [mount|unmount|remount]"
        echo "  mount   - Mount configured network shares (default)"
        echo "  unmount - Unmount all network shares"
        echo "  remount - Unmount and remount all network shares"
        exit 1
        ;;
esac
