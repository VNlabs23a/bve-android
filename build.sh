#!/bin/bash
# Build script for OpenBVE-Android
# Usage: ./build.sh [debug|release|aab]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

BUILD_TYPE="${1:-debug}"

echo "=========================================="
echo "OpenBVE-Android Build Script"
echo "Build type: $BUILD_TYPE"
echo "=========================================="

# Check for Godot
if ! command -v godot &> /dev/null; then
    echo "ERROR: Godot not found in PATH"
    echo "Please install Godot 4.3+ and add to PATH"
    exit 1
fi

GODOT_VERSION=$(godot --version)
echo "Godot version: $GODOT_VERSION"

# Check for Android SDK
if [ -z "$ANDROID_HOME" ] && [ -z "$ANDROID_SDK_ROOT" ]; then
    echo "WARNING: ANDROID_HOME not set. Godot will use its own SDK if configured."
fi

# Export based on build type
case "$BUILD_TYPE" in
    debug)
        echo "Building Debug APK..."
        godot --headless --export-debug "Android Debug" build/OpenBVE-Android-Debug.apk
        ;;
    release)
        echo "Building Release APK..."
        godot --headless --export-release "Android Release" build/OpenBVE-Android-Release.apk
        ;;
    aab)
        echo "Building AAB for Play Store..."
        godot --headless --export-release "Android AAB (Play Store)" build/OpenBVE-Android.aab
        ;;
    all)
        echo "Building all variants..."
        godot --headless --export-debug "Android Debug" build/OpenBVE-Android-Debug.apk
        godot --headless --export-release "Android Release" build/OpenBVE-Android-Release.apk
        godot --headless --export-release "Android AAB (Play Store)" build/OpenBVE-Android.aab
        ;;
    *)
        echo "Usage: $0 [debug|release|aab|all]"
        exit 1
        ;;
esac

# Verify output
if [ "$BUILD_TYPE" = "debug" ] || [ "$BUILD_TYPE" = "all" ]; then
    if [ -f "build/OpenBVE-Android-Debug.apk" ]; then
        echo "✓ Debug APK created: build/OpenBVE-Android-Debug.apk"
        ls -lh build/OpenBVE-Android-Debug.apk
    fi
fi

if [ "$BUILD_TYPE" = "release" ] || [ "$BUILD_TYPE" = "all" ]; then
    if [ -f "build/OpenBVE-Android-Release.apk" ]; then
        echo "✓ Release APK created: build/OpenBVE-Android-Release.apk"
        ls -lh build/OpenBVE-Android-Release.apk
    fi
fi

if [ "$BUILD_TYPE" = "aab" ] || [ "$BUILD_TYPE" = "all" ]; then
    if [ -f "build/OpenBVE-Android.aab" ]; then
        echo "✓ AAB created: build/OpenBVE-Android.aab"
        ls -lh build/OpenBVE-Android.aab
    fi
fi

echo "=========================================="
echo "Build complete!"
echo "=========================================="