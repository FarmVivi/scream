# ScreamReader 🎵

A high-performance C# (.NET/Windows Forms) tray application for receiving and playing back audio streams broadcasted by `Scream`. Features **ultra-low latency** with intelligent adaptive buffer management.

## 🚀 Quick Start

### Basic Usage (Recommended)
```bash
ScreamReader.exe
```
The application will automatically optimize latency for your system!

### Gaming / Low Latency Mode
```bash
ScreamReader.exe --buffer-duration 20 --wasapi-latency 10
```

### Help
```bash
ScreamReader.exe --help
```

## 🔧 Building

### Prerequisites
- Visual Studio 2017 or later
- .NET Framework 4.7.2

### Steps
1. Restore all *NuGet* packages
2. Build the solution

```bash
nuget restore
msbuild ScreamReader.sln
```

## 🎮 Command Line Options

```
--ip <address>           IP address to listen on (default: 239.255.77.77)
--port <number>          Port to listen on (default: 4010)
--unicast               Use unicast mode
--multicast             Use multicast mode (default)
--buffer-duration <ms>   Network buffer size (default: adaptive ~30ms)
--wasapi-latency <ms>    Audio driver latency (default: adaptive ~20ms)
--exclusive-mode        Use exclusive audio mode (lowest latency)
--shared-mode           Use shared audio mode (default, visible in mixer)
--help                  Show help message
```

## 🏆 Technologies

- **NAudio**: Audio playback library
- **WASAPI**: Windows Audio Session API
- **.NET Framework 4.7.2**: Application framework

## 📝 License

Same as Scream project
