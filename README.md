# OpenBVE-Android

OpenBVE train simulator ported to Android with touch controls, mod support, and scalable graphics.

## Features

- **Touch-optimized interface** - Virtual throttle, emergency brake, horn, reverser, lights, camera controls
- **Full mod support** - Load routes, trains, and objects from internal/external storage
- **Scalable graphics** - 4 quality presets (Low/Medium/High/Ultra) with auto-detection
- **OpenBVE compatibility** - Supports CSV/RW routes, BVE5, Mechanik DAT formats
- **Godot 4.x engine** - Modern rendering with GLES3/Vulkan, C# scripting

## Project Structure

```
OpenBVE-Android/
├── src/
│   ├── System/          # Core simulation (physics, route, input, mods, quality)
│   ├── Rendering/       # Track/train rendering, LOD, shaders
│   ├── UI/              # Touch controls, HUD, settings
│   └── Android/         # Android platform integration
├── scenes/
│   ├── UI/              # TouchControls, HUD
│   └── World/           # Track, Train
├── shaders/             # Track, train, post-process shaders
├── assets/              # Fonts, icons, default mods
├── android/             # Android manifest, resources, gradle
├── build/               # Output APK/AAB
├── build.sh / build.bat # Build scripts
└── export_presets.cfg   # Godot export configuration
```

## Requirements

- **Godot 4.3+** with .NET support
- **Android SDK** (API 24+)
- **NDK** (r26+)
- **Java 17+** (for Gradle)
- **.NET 8 SDK** (for C# compilation)

## Building

### Quick Start

```bash
# Clone and setup
git clone <repo>
cd OpenBVE-Android

# Install Godot export templates (one-time)
godot --headless --export-release "Android Release" build/OpenBVE-Android-Release.apk

# Build debug APK
./build.sh debug

# Build release APK
./build.sh release

# Build AAB for Play Store
./build.sh aab

# Build all
./build.sh all
```

### Windows

```cmd
build.bat debug
build.bat release
build.bat aab
```

### Manual Godot Export

1. Open project in Godot 4.3+
2. Install Android export templates: `Editor → Manage Export Templates → Install`
3. Configure export presets in `Project → Export`
4. Click "Export Project" for desired preset

## Configuration

### Quality Presets

| Preset | Render Scale | Shadows | Bloom | Target FPS | Best For |
|--------|-------------|---------|-------|------------|----------|
| Low    | 50%         | Off     | Off   | 30         | Older devices |
| Medium | 75%         | Low     | Off   | 30         | Mid-range |
| High   | 100%        | Medium  | On    | 60         | Modern phones |
| Ultra  | 100%        | High    | On    | 120        | Flagship/tablets |

Auto-quality adjusts based on device performance.

### Mod Installation

1. Place mod folders in:
   - Internal: `Android/data/com.openbve.android/files/mods/`
   - External: `Documents/OpenBVE/mods/` (via file picker)

2. Mod structure:
   ```
   MyRoute/
   ├── route.csv          # Route definition
   ├── mod.json           # Optional metadata
   ├── structure/         # Objects
   ├── object/            # 3D models
   ├── sound/             # Audio
   └── texture/           # Textures
   ```

3. Enable in Mod Browser (Settings → Mods)

### Touch Controls

| Control | Gesture | Description |
|---------|---------|-------------|
| Throttle | Vertical swipe (left) | Power notches P1-P8, Brake B1-B8 |
| Emergency Brake | Tap (bottom right) | Immediate stop |
| Horn | Tap (bottom right) | Sound horn |
| Reverser | Tap (top left) | Cycle FWD/N/REV |
| Lights | Tap (top left) | Cycle OFF/LOW/HIGH |
| Camera | Tap (top center) | Cycle CAB/CHASE/FREE |
| Pause | Tap (top right) | Pause menu |
| Free Look | Drag (center) | Look around in cab view |

### Gamepad Support

| Button | Action |
|--------|--------|
| Left Stick Y | Throttle |
| Right Stick | Camera look |
| Triggers | Fine throttle |
| A / Cross | Horn |
| B / Circle | Emergency brake |
| X / Square | Lights cycle |
| Y / Triangle | Reverser |
| LB | Camera cycle |
| D-Pad | Notch up/down |
| Start/Select | Pause |

## Development

### Running in Editor

1. Open `project.godot` in Godot 4.3+
2. Press F5 to run main scene
3. Use keyboard/mouse for testing (W/S for throttle, Space for e-brake)

### Adding Content

#### Default Routes/Trains
Place in `assets/default_mods/`:
```
assets/default_mods/
├── routes/
│   └── sample_route.csv
├── trains/
│   └── sample_train/
│       ├── train.dat
│       └── ...
└── objects/
    └── ...
```

### Architecture

#### Core Systems (Autoloads)
- `Simulation` - Main loop, train physics coordination
- `InputManager` - Unified touch/gamepad/keyboard input
- `QualityManager` - Graphics presets, auto-quality
- `PerformanceMonitor` - Frame timing, thermal state
- `ModManager` - Mod loading, dependencies
- `VirtualFS` - Case-insensitive path resolution
- `RouteManager` - CSV/RW/BVE5/DAT parsing
- `TrainPhysics` - Motor, brake, resistance simulation
- `LODManager` - Distance-based mesh swapping

#### Android Integration
- `AndroidBridge` - JNI to Java APIs (wake lock, audio focus, SAF)
- `StorageManager` - Scoped storage, mod import/export
- `LifecycleManager` - Pause/resume, config changes, state save

## License

OpenBVE-Android is based on OpenBVE (BSD-2-Clause).
Android port additions: MIT License.

## Credits

- **OpenBVE Original**: leezer3 and contributors
- **gt-openbve (Godot port)**: danielleighton
- **BVE-Reborn (Rust rewrite)**: BVE-Reborn team
- **Android port**: OpenBVE-Android team

## CI/CD (GitHub Actions)

Automated builds on every push/tag via `.github/workflows/android.yml`:

### Automatic Builds
- **Push to main** → Builds Debug + Release APK + AAB (artifacts retained 30 days)
- **Pull Request** → Builds Debug APK for verification
- **Tag push (v*)** → Builds Release APK + AAB, creates GitHub Release with artifacts

### Manual Build
```bash
# Trigger via GitHub CLI
gh workflow run android.yml -f build_type=debug
gh workflow run android.yml -f build_type=release
gh workflow run android.yml -f build_type=aab
gh workflow run android.yml -f build_type=all
```

### Release Process
```bash
# Bump version (patch/minor/major)
./scripts/version.sh patch    # or version.ps1 on Windows

# Commit and tag
git add version.txt android/src/main/AndroidManifest.xml android/app/build.gradle
git commit -m "chore: release v1.0.1"
git tag v1.0.1
git push origin main --tags
```
The workflow will build and attach APK/AAB to the GitHub Release.

### Artifacts
| Build Type | Output | Use Case |
|------------|--------|----------|
| Debug | `OpenBVE-Android-Debug.apk` | Testing, development |
| Release | `OpenBVE-Android-Release.apk` | Distribution, sideloading |
| AAB | `OpenBVE-Android.aab` | Google Play Store upload |

## Links

- [OpenBVE GitHub](https://github.com/leezer3/OpenBVE)
- [gt-openbve](https://github.com/danielleighton/gt-openbve)
- [BVE-Reborn](https://github.com/BVE-Reborn/BVE-Reborn)
- [Godot Android Export Docs](https://docs.godotengine.org/en/4.3/tutorials/export/exporting_for_android.html)