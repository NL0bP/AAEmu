#!/bin/bash

# =============================================================================
# AAEmu Publish Script for .NET 10.0
# =============================================================================
# This script builds and publishes AAEmu.Login and AAEmu.Game projects
# for multiple target runtimes with self-contained deployment.
# =============================================================================

set -euo pipefail

# Switch to parent directory (project root)
cd "$(dirname "$0")/.." || exit

# Version configuration
VERSION_PREFIX=0.0.2.0
VERSION_SUFFIX=alpha

# Framework configuration
FRAMEWORK=net10.0

# Build configuration
CONFIGURATION=Debug
#CONFIGURATION=Release

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# =============================================================================
# Helper Functions
# =============================================================================

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if required SDK version is installed
check_dotnet_sdk() {
    log_info "Checking .NET SDK version..."
    
    if ! command -v dotnet &> /dev/null; then
        log_error "dotnet CLI is not installed or not in PATH"
        exit 1
    fi
    
    local sdk_version
    sdk_version=$(dotnet --version 2>/dev/null || echo "unknown")
    log_info "Detected .NET SDK version: $sdk_version"
    
    # Check if SDK is 10.0 or higher
    local major_version
    major_version=$(echo "$sdk_version" | cut -d. -f1)
    
    if [ "$major_version" -lt 10 ]; then
        log_error ".NET SDK 10.0 or higher is required, but found: $sdk_version"
        log_info "Please install .NET 10.0 SDK from: https://dotnet.microsoft.com/download"
        exit 1
    fi
    
    log_success ".NET SDK version check passed"
}

# Clean up previous build artifacts
cleanup() {
    log_info "Cleaning previous build artifacts..."
    
    for project in "AAEmu.Login" "AAEmu.Game"; do
        if [ -d "$project/bin" ]; then
            rm -rf "$project/bin"
        fi
        if [ -d "$project/obj" ]; then
            rm -rf "$project/obj"
        fi
    done
    
    log_success "Cleanup completed"
}

# Build project for specific runtime
build_project() {
    local runtime=$1
    
    log_info "Building for runtime: $runtime"
    
    if ! dotnet publish -c "$CONFIGURATION" -r "$runtime" --self-contained true --framework "$FRAMEWORK" -p:PublishSingleFile=true; then
        log_error "Failed to build for runtime: $runtime"
        return 1
    fi
    
    return 0
}

# Package build artifacts
package_artifacts() {
    local runtime=$1
    
    mkdir -p "publish/$CONFIGURATION/$runtime"
    
    for project in "AAEmu.Login" "AAEmu.Game"; do
        mkdir -p "publish/$CONFIGURATION/$runtime/$project"
        
        local source_path="$project/bin/$CONFIGURATION/$FRAMEWORK/$runtime/publish/"
        
        if [ ! -d "$source_path" ]; then
            log_error "Source path does not exist: $source_path"
            return 1
        fi
        
        cp -r "$source_path"* "publish/$CONFIGURATION/$runtime/$project/" 2>/dev/null || {
            log_error "Failed to copy files from $source_path"
            return 1
        }
        
        # Clean up build directory
        rm -rf "$project/bin/$CONFIGURATION/$FRAMEWORK/$runtime"
    done
    
    # Create zip archive
    cd "publish/$CONFIGURATION/$runtime"
    zip -r "../../../publish/$CONFIGURATION/AAEmu.$VERSION_PREFIX-$VERSION_SUFFIX+$runtime.zip" ./*
    cd "../../../"
    
    # Remove temporary directory
    rm -rf "publish/$CONFIGURATION/$runtime"
    
    log_success "Package created: AAEmu.$VERSION_PREFIX-$VERSION_SUFFIX+$runtime.zip"
}

# =============================================================================
# Main Script
# =============================================================================

main() {
    log_info "Starting AAEmu publish process..."
    log_info "Framework: $FRAMEWORK"
    log_info "Configuration: $CONFIGURATION"
    log_info "Version: $VERSION_PREFIX-$VERSION_SUFFIX"
    
    # Check prerequisites
    check_dotnet_sdk
    
    # Clean previous builds
    cleanup
    
    # Create publish directories
    mkdir -p "publish/$CONFIGURATION"
    
    # Define target runtimes
    # Modern runtime identifiers for .NET 10.0
    local runtimes=(
        "win-x64"
        "win-x86"
        "win-arm64"
        "linux-x64"
        "linux-arm"
        "linux-arm64"
        "osx-x64"
        "osx-arm64"
    )
    
    local success_count=0
    local fail_count=0
    local failed_runtimes=()
    
    for runtime in "${runtimes[@]}"; do
        log_info "========================================"
        log_info "Processing runtime: $runtime"
        log_info "========================================"
        
        if build_project "$runtime"; then
            if package_artifacts "$runtime"; then
                ((success_count++))
            else
                ((fail_count++))
                failed_runtimes+=("$runtime")
            fi
        else
            ((fail_count++))
            failed_runtimes+=("$runtime")
        fi
        
        echo ""
    done
    
    # Summary
    log_info "========================================"
    log_info "Publish Summary"
    log_info "========================================"
    log_success "Successfully built: $success_count runtime(s)"
    
    if [ $fail_count -gt 0 ]; then
        log_warning "Failed builds: $fail_count runtime(s)"
        log_warning "Failed runtimes: ${failed_runtimes[*]}"
    fi
    
    log_info "Output directory: publish/$CONFIGURATION/"
    log_info "Packages:"
    ls -lh "publish/$CONFIGURATION/"*.zip 2>/dev/null || true
    
    if [ $fail_count -eq 0 ]; then
        log_success "All builds completed successfully!"
        exit 0
    else
        log_warning "Some builds failed. Check the logs above."
        exit 1
    fi
}

# Run main function
main "$@"
