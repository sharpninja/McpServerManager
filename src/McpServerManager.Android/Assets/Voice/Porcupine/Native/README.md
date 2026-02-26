Porcupine Android native libraries

Place Picovoice Porcupine Android native libraries here using ABI folders:

- `arm64-v8a/libpv_porcupine.so`
- `x86_64/libpv_porcupine.so` (optional, emulator/dev)
- `armeabi-v7a/libpv_porcupine.so` (optional, if you target 32-bit ARM)

Expected path pattern in this repo:

- `src/McpServerManager.Android/Assets/Voice/Porcupine/Native/<abi>/libpv_porcupine.so`

The project file packages any matching files as `AndroidNativeLibrary`.
