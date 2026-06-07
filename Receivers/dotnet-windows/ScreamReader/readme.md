# ScreamReader

A lightweight C# (.NET Framework / Windows Forms) **system-tray** application that receives and
plays back the audio streams broadcast by [`Scream`](https://github.com/duncanthrax/scream). It uses
`NAudio` with **WASAPI** for output and is designed for **low latency and very low CPU/battery use**.

## Features

- **Multicast or unicast** reception (default multicast `239.255.77.77:4010`).
- **Automatic format detection** — sample rate, bit depth and channel count are read from the stream;
  the output is re-initialised on the fly if the format changes. A manual mode is also available.
- **Low latency** — event-driven WASAPI in shared (default) or exclusive mode. Network buffer and
  WASAPI latency are configurable, or left on `Auto` (sensible low defaults: 30 ms / 20 ms).
- **Low CPU / battery friendly** — the UI does no work while the window is hidden in the tray (the
  normal mode), the receive path is allocation-free, and playback is paused when the stream stops so
  the audio device can idle.
- **Live status & logs** — connection, source, format, packets/s, bitrate, latency, network-buffer
  fill and a glitch (underrun/overflow) counter, plus an in-app log with a level filter.
- **Persistent settings** — the configuration is saved to the registry (`HKCU\Software\ScreamReader`)
  and the app auto-starts playback in the background on the next launch.

Closing the window minimises it to the tray; use the tray menu (or double-click) to show it again,
and **Exit** to quit.

## Volume

There is no in-app volume slider by design. In shared mode ScreamReader appears in the Windows
**Volume Mixer**, so use the system volume / per-app volume there.

## Building

Open `ScreamReader/ScreamReader.sln` in **Visual Studio 2022** (with the .NET Framework 4.7.2
targeting pack), restore the NuGet packages, and build.

From the command line with MSBuild:

```powershell
msbuild ScreamReader.sln -t:restore -p:RestorePackagesConfig=true
msbuild ScreamReader.sln -p:Configuration=Release
```

The build produces `ScreamReader/bin/Release/ScreamReader.exe`.

## Requirements

- Windows (WASAPI), .NET Framework 4.7.2
- NAudio 1.9
