#!/bin/bash
# ReadAIrr Docker Configuration Validator
# This script validates the Docker configuration files without requiring Docker

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[VALIDATE] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[VALIDATE] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[VALIDATE] ERROR: $1${NC}"
}

info() {
    echo -e "${BLUE}[VALIDATE] INFO: $1${NC}"
}

# Function to check if required files exist
check_required_files() {
    log "Checking required files..."
    
    local required_files=(
        "docker-compose.dev.yml"
        "Dockerfile.dev"
        ".env.local.example"
        "docker/docker-entrypoint.sh"
        "docker/mount-network-storage.sh"
    )
    
    local missing_files=()
    
    for file in "${required_files[@]}"; do
        local filepath="$PROJECT_DIR/$file"
        if [[ -f "$filepath" ]]; then
            info "✓ Found: $file"
        else
            error "✗ Missing: $file"
            missing_files+=("$file")
        fi
    done
    
    if [[ ${#missing_files[@]} -gt 0 ]]; then
        error "Missing required files: ${missing_files[*]}"
        return 1
    fi
    
    log "All required files are present"
}

# Function to validate environment file structure
validate_env_file() {
    log "Validating environment file structure..."
    
    local env_file="$PROJECT_DIR/.env.local.example"
    
    # Check for required environment variables
    local required_vars=(
        "NETWORK_STORAGE_ENABLED"
        "SMB_SHARE_PATH"
        "SMB_USERNAME" 
        "SMB_PASSWORD"
        "SMB_MOUNT_POINT"
        "NFS_SHARE_PATH"
        "NFS_MOUNT_POINT"
        "READAIRR_CONFIG_PATH"
        "READAIRR_DOWNLOADS_PATH"
        "READAIRR_MEDIA_PATH"
        "READAIRR_PORT"
    )
    
    local missing_vars=()
    
    for var in "${required_vars[@]}"; do
        if grep -q "^#*${var}=" "$env_file" || grep -q "^${var}=" "$env_file"; then
            info "✓ Found environment variable: $var"
        else
            warn "Environment variable not found: $var"
            missing_vars+=("$var")
        fi
    done
    
    if [[ ${#missing_vars[@]} -gt 0 ]]; then
        warn "Some environment variables are missing from the example file"
        warn "Missing: ${missing_vars[*]}"
    else
        log "All expected environment variables are present"
    fi
}

# Function to validate Docker Compose YAML syntax
validate_compose_syntax() {
    log "Validating Docker Compose syntax..."
    
    local compose_file="$PROJECT_DIR/docker-compose.dev.yml"
    
    # Basic YAML syntax checks
    if command -v yq &> /dev/null; then
        if yq eval '.' "$compose_file" > /dev/null 2>&1; then
            log "✓ Docker Compose YAML syntax is valid"
        else
            error "✗ Docker Compose YAML syntax is invalid"
            return 1
        fi
    elif python3 "$PROJECT_DIR/scripts/yaml_validator.py" "$compose_file" >/dev/null 2>&1; then
        log "✓ Docker Compose YAML syntax is valid"
    else
        error "✗ Docker Compose YAML syntax is invalid"
        python3 "$PROJECT_DIR/scripts/yaml_validator.py" "$compose_file"
        return 1
    fi
    
    # Check for required services
    if grep -q "readairr-dev:" "$compose_file"; then
        info "✓ Found readairr-dev service"
    else
        error "✗ readairr-dev service not found"
        return 1
    fi
    
    # Check for required configuration
    local required_sections=(
        "privileged: true"
        "cap_add:"
        "SYS_ADMIN"
        "environment:"
        "volumes:"
    )
    
    for section in "${required_sections[@]}"; do
        if grep -q "$section" "$compose_file"; then
            info "✓ Found required section: $section"
        else
            warn "Required section not found: $section"
        fi
    done
}

# Function to validate Dockerfile syntax
validate_dockerfile() {
    log "Validating Dockerfile syntax..."
    
    local dockerfile="$PROJECT_DIR/Dockerfile.dev"
    
    # Check for required instructions
    local required_instructions=(
        "FROM"
        "cifs-utils"
        "nfs-utils"
        "mount-network-storage.sh"
        "docker-entrypoint.sh"
        "EXPOSE 8246"
        "ENTRYPOINT"
    )
    
    for instruction in "${required_instructions[@]}"; do
        if grep -q "$instruction" "$dockerfile"; then
            info "✓ Found required instruction: $instruction"
        else
            warn "Required instruction not found: $instruction"
        fi
    done
    
    # Check for potential issues
    if grep -q "USER root" "$dockerfile"; then
        warn "Dockerfile runs as root - this may be required for mounting"
    fi
    
    if ! grep -q "USER readairr" "$dockerfile"; then
        warn "Dockerfile should switch to readairr user for security"
    fi
}

# Function to validate script permissions and syntax
validate_scripts() {
    log "Validating script files..."
    
    local scripts=(
        "docker/docker-entrypoint.sh"
        "docker/mount-network-storage.sh"
        "scripts/test-network-storage.sh"
    )
    
    for script in "${scripts[@]}"; do
        local script_path="$PROJECT_DIR/$script"
        
        if [[ -f "$script_path" ]]; then
            # Check if executable
            if [[ -x "$script_path" ]]; then
                info "✓ Script is executable: $script"
            else
                warn "Script is not executable: $script"
            fi
            
            # Basic shell syntax check
            if bash -n "$script_path" 2>/dev/null; then
                info "✓ Script syntax is valid: $script"
            else
                error "✗ Script syntax error in: $script"
                bash -n "$script_path"
                return 1
            fi
        else
            error "✗ Script not found: $script"
            return 1
        fi
    done
}

# Function to check documentation
validate_documentation() {
    log "Validating documentation..."
    
    local docs=(
        "docs/NETWORK_STORAGE.md"
        "README.md"
    )
    
    for doc in "${docs[@]}"; do
        local doc_path="$PROJECT_DIR/$doc"
        
        if [[ -f "$doc_path" ]]; then
            info "✓ Documentation found: $doc"
            
            # Check for key sections in network storage docs
            if [[ "$doc" == "docs/NETWORK_STORAGE.md" ]]; then
                local required_sections=(
                    "SMB/CIFS Configuration"
                    "NFS Configuration"
                    "Environment Variables"
                    "Troubleshooting"
                )
                
                for section in "${required_sections[@]}"; do
                    if grep -q "$section" "$doc_path"; then
                        info "  ✓ Section found: $section"
                    else
                        warn "  Section not found: $section"
                    fi
                done
            fi
        else
            warn "Documentation not found: $doc"
        fi
    done
}

# Function to simulate environment loading
test_env_loading() {
    log "Testing environment loading..."
    
    local env_file="$PROJECT_DIR/.env.local.example"
    
    # Create a temporary environment for testing
    local temp_env=$(mktemp)
    
    # Copy example file and set test values
    cp "$env_file" "$temp_env"
    echo "NETWORK_STORAGE_ENABLED=true" >> "$temp_env"
    echo "SMB_SHARE_PATH=//test.local/books" >> "$temp_env"
    echo "SMB_USERNAME=testuser" >> "$temp_env"
    
    # Test loading
    set -a
    source "$temp_env" 2>/dev/null || {
        error "Failed to load environment file"
        rm -f "$temp_env"
        return 1
    }
    set +a
    
    # Verify some variables were loaded
    if [[ "$NETWORK_STORAGE_ENABLED" == "true" ]]; then
        info "✓ Environment loading test successful"
    else
        error "✗ Environment loading test failed"
        rm -f "$temp_env"
        return 1
    fi
    
    rm -f "$temp_env"
}

# Main validation function
main() {
    echo
    log "ReadAIrr Docker Configuration Validator"
    log "======================================"
    echo
    
    cd "$PROJECT_DIR"
    
    local exit_code=0
    
    # Run all validation checks
    check_required_files || exit_code=1
    echo
    
    validate_env_file || exit_code=1
    echo
    
    validate_compose_syntax || exit_code=1
    echo
    
    validate_dockerfile || exit_code=1
    echo
    
    validate_scripts || exit_code=1
    echo
    
    validate_documentation || exit_code=1
    echo
    
    test_env_loading || exit_code=1
    echo
    
    if [[ $exit_code -eq 0 ]]; then
        log "✅ All validation checks passed!"
        echo
        info "Your ReadAIrr Docker configuration appears to be valid."
        info "To test with actual Docker environment:"
        echo "  1. Ensure Docker and Docker Compose are installed"
        echo "  2. Copy .env.local.example to .env.local"
        echo "  3. Configure your network storage settings"
        echo "  4. Run: docker compose -f docker-compose.dev.yml up -d"
    else
        error "❌ Some validation checks failed!"
        echo
        error "Please review the errors above and fix the configuration."
    fi
    
    return $exit_code
}

# Run main function
main "$@"
