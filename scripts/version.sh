#!/bin/bash
# Bump version for OpenBVE-Android releases
# Usage: ./scripts/version.sh [major|minor|patch]

set -e

TYPE="${1:-patch}"
VERSION_FILE="version.txt"

if [[ ! -f "$VERSION_FILE" ]]; then
    echo "ERROR: $VERSION_FILE not found"
    exit 1
fi

CURRENT_VERSION=$(cat "$VERSION_FILE")
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

case "$TYPE" in
    major)
        NEW_VERSION="$((MAJOR + 1)).0.0"
        ;;
    minor)
        NEW_VERSION="${MAJOR}.$((MINOR + 1)).0"
        ;;
    patch)
        NEW_VERSION="${MAJOR}.${MINOR}.$((PATCH + 1))"
        ;;
    *)
        echo "Usage: $0 [major|minor|patch]"
        exit 1
        ;;
esac

echo "Bumping version: $CURRENT_VERSION -> $NEW_VERSION"
echo -n "$NEW_VERSION" > "$VERSION_FILE"

# Update AndroidManifest.xml versionCode/versionName
MANIFEST="android/src/main/AndroidManifest.xml"
if [[ -f "$MANIFEST" ]]; then
    VERSION_CODE=$((MAJOR * 10000 + MINOR * 100 + PATCH))
    # For patch, increment the version code
    if [[ "$TYPE" == "patch" ]]; then
        VERSION_CODE=$((VERSION_CODE + 1))
    elif [[ "$TYPE" == "minor" ]]; then
        VERSION_CODE=$((MAJOR * 10000 + (MINOR + 1) * 100))
    else
        VERSION_CODE=$(((MAJOR + 1) * 10000))
    fi

    sed -i "s/android:versionCode=\"[0-9]*\"/android:versionCode=\"$VERSION_CODE\"/" "$MANIFEST"
    sed -i "s/android:versionName=\"[^\"]*\"/android:versionName=\"$NEW_VERSION\"/" "$MANIFEST"
    echo "Updated $MANIFEST (versionCode: $VERSION_CODE)"
fi

# Update build.gradle version
BUILD_GRADLE="android/app/build.gradle"
if [[ -f "$BUILD_GRADLE" ]]; then
    sed -i "s/versionCode [0-9]*/versionCode $VERSION_CODE/" "$BUILD_GRADLE"
    sed -i "s/versionName \"[^\"]*\"/versionName \"$NEW_VERSION\"/" "$BUILD_GRADLE"
    echo "Updated $BUILD_GRADLE"
fi

echo "Done. Commit and tag:"
echo "  git add $VERSION_FILE $MANIFEST $BUILD_GRADLE"
echo "  git commit -m \"chore: release v$NEW_VERSION\""
echo "  git tag v$NEW_VERSION"
echo "  git push origin main --tags"