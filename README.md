# NUUTop

NUUTop is a lightweight terminal-based system monitor for the Nuu B20 or similar mediatek devices

It’s inspired by tools like `btop`.

---

## Features

- Live battery percentage and charging status
- Temperature trend indicators (↑ / ↓ / stable)
- Auto-detection of thermal zones from `/sys/class/thermal`

---

## Requirements

- Rooted device.
- Terminal that supports ANSI colors


### Run via Root terminal or [Magisk SSHModule](https://gitlab.com/d4rcm4rc/MagiskSSH/-/blob/main/module_data/README.md)
```bash
./NUUTop
```

## Usage Options
```md
Usage:
  NUUTop [options]

Options:
  -si, --show-invalid  Show thermal zones with invalid values. (-127)
  -dz, --debug-zones   Dump all detected thermal zones.
  -?, -h, --help       Show help and usage information
  --version            Show version information
```
---

## Building Instructions

NUUTop can be built using the .NET SDK with Android NDK support enabled.

This project targets Android/Linux environments using NativeAOT, so the Android NDK is required for compilation.

---

### 1. Install Requirements

Make sure you have the following installed:

- [**.NET 10 SDK**](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

- [**Android NDK**](https://developer.android.com/ndk/downloads)
---

### 2. Configure Android NDK

Follow the official [.NET NativeAOT Android setup guide](https://github.com/dotnet/runtime/blob/main/src/coreclr/nativeaot/docs/android-bionic.md)

This guide explains how to:
- Install and extract the NDK
- Set required environment variables
- Configure your system `PATH` so the NDK toolchain is accessible

Once completed, your terminal should be able to access the Android NDK toolchain from anywhere.

---

### 3. Build the Project

After the environment is set up, build NUUTop using:
```bash
./build.bat
```
