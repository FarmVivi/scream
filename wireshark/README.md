# Wireshark Dissector for SCReAM Audio Protocol

This directory contains a Wireshark dissector for analyzing SCReAM (Scream) audio packets. The dissector allows you to inspect and analyze the audio stream packets in Wireshark for debugging and analysis purposes.

## What is SCReAM?

SCReAM is a virtual audio device driver for Windows that publishes audio as PCM multicast/unicast streams over UDP. The default configuration uses:
- **Multicast address**: 239.255.77.77
- **UDP port**: 4010
- **Protocol**: Raw PCM audio with a 5-byte header

## Packet Structure

Each SCReAM packet consists of:

### Header (5 bytes)
1. **Byte 0 - Sample Rate** (encoded):
   - Bit 7: Base rate (0 = 48 kHz, 1 = 44.1 kHz)
   - Bits 6-0: Multiplier for the base rate
   
2. **Byte 1 - Sample Size**: Bits per sample (typically 16, 24, or 32)

3. **Byte 2 - Channels**: Number of audio channels (1-8)

4. **Bytes 3-4 - Channel Mask**: 16-bit little-endian value indicating speaker positions (from Microsoft's WAVEFORMATEXTENSIBLE structure)

### Payload (up to 1152 bytes)
Raw PCM audio data

## Installation

### Method 1: User Plugin Directory (Recommended)

1. Copy `scream.lua` to your Wireshark personal plugins directory:
   
   **Windows**:
   ```
   %APPDATA%\Wireshark\plugins\
   ```
   
   **Linux/Unix**:
   ```
   ~/.local/lib/wireshark/plugins/
   ```
   
   **macOS**:
   ```
   ~/.local/lib/wireshark/plugins/
   ```

2. Restart Wireshark or reload Lua plugins (Analyze → Reload Lua Plugins, or press Ctrl+Shift+L)

### Method 2: Global Plugin Directory

1. Find your Wireshark global plugins directory:
   - In Wireshark: Help → About Wireshark → Folders → Personal Plugins or Global Plugins
   
2. Copy `scream.lua` to the global plugins directory (requires admin/root privileges)

3. Restart Wireshark

### Method 3: Command Line

You can also load the dissector directly when starting Wireshark:

```bash
wireshark -X lua_script:scream.lua
```

## Usage

### Basic Capture

1. **Start capturing** on the network interface where SCReAM traffic is expected

2. **Apply a capture filter** (optional but recommended):
   ```
   udp port 4010
   ```

3. **Start your SCReAM audio stream** from the Windows machine

4. **Look for packets** with "SCReAM" in the Protocol column

### Display Filters

Once capturing, you can use these display filters:

- Show all SCReAM packets:
  ```
  scream
  ```

- Filter by sample rate (e.g., 48 kHz):
  ```
  scream.sample_rate_calculated contains "48000"
  ```

- Filter by number of channels (e.g., stereo):
  ```
  scream.channels == 2
  ```

- Filter by sample size (e.g., 16-bit):
  ```
  scream.sample_size == 16
  ```

- Show only packets with audio data larger than 1000 bytes:
  ```
  scream.audio_data_size > 1000
  ```

### Analyzing Packets

The dissector provides detailed information for each packet:

1. **Header Section**:
   - Sample Rate (Raw): The raw byte value
   - Base Rate: 48 kHz or 44.1 kHz
   - Rate Multiplier: Multiplier applied to base rate
   - Calculated Sample Rate: The actual sample rate in Hz
   - Sample Size: Bits per sample
   - Channels: Number of audio channels
   - Channel Mask: Hexadecimal mask value
   - Channel Positions: Human-readable speaker positions

2. **PCM Audio Data Section**:
   - Audio Data Size: Number of bytes of audio data
   - PCM Audio Data: Raw audio bytes

### Troubleshooting

**Dissector not loading:**
1. Check Wireshark console (View → Show Console) for Lua errors
2. Verify the file is in the correct plugins directory
3. Make sure the file has `.lua` extension
4. Try reloading Lua plugins (Ctrl+Shift+L)

**SCReAM packets not recognized:**
1. Verify you're capturing on the correct network interface
2. Check if SCReAM is using a non-standard port (check Windows registry)
3. The dissector automatically registers for port 4010 and includes heuristic detection

**Custom port configuration:**
If your SCReAM setup uses a different port, you can:
1. Decode as SCReAM: Right-click a packet → Decode As → select SCReAM protocol
2. Or modify the `scream.lua` file and add your port:
   ```lua
   udp_table:add(YOUR_PORT, scream_proto)
   ```

## Channel Mask Reference

The channel mask follows Microsoft's WAVEFORMATEXTENSIBLE specification:

| Bit | Value | Position |
|-----|-------|----------|
| 0 | 0x00001 | FRONT_LEFT |
| 1 | 0x00002 | FRONT_RIGHT |
| 2 | 0x00004 | FRONT_CENTER |
| 3 | 0x00008 | LOW_FREQUENCY |
| 4 | 0x00010 | BACK_LEFT |
| 5 | 0x00020 | BACK_RIGHT |
| 6 | 0x00040 | FRONT_LEFT_OF_CENTER |
| 7 | 0x00080 | FRONT_RIGHT_OF_CENTER |
| 8 | 0x00100 | BACK_CENTER |
| 9 | 0x00200 | SIDE_LEFT |
| 10 | 0x00400 | SIDE_RIGHT |
| 11 | 0x00800 | TOP_CENTER |
| 12 | 0x01000 | TOP_FRONT_LEFT |
| 13 | 0x02000 | TOP_FRONT_CENTER |
| 14 | 0x04000 | TOP_FRONT_RIGHT |
| 15 | 0x08000 | TOP_BACK_LEFT |
| 16 | 0x10000 | TOP_BACK_CENTER |
| 17 | 0x20000 | TOP_BACK_RIGHT |

Common configurations:
- **Stereo (0x0003)**: FRONT_LEFT | FRONT_RIGHT
- **5.1 (0x003F)**: FL | FR | FC | LFE | BL | BR
- **7.1 (0x063F)**: FL | FR | FC | LFE | BL | BR | SL | SR

## Examples

### Example 1: Basic Stereo Stream
```
Packet: SCReAM Audio: 48000 Hz, 16-bit, 2 ch, 1152 bytes
├─ Header
│  ├─ Sample Rate (Raw): 0x00
│  │  ├─ Base Rate: 48 kHz
│  │  ├─ Rate Multiplier: 0
│  │  └─ Calculated Sample Rate: 48000 Hz
│  ├─ Sample Size: 16 bits
│  ├─ Channels: 2
│  └─ Channel Mask: 0x0003
│     └─ Channel Positions: FRONT_LEFT, FRONT_RIGHT
└─ PCM Audio Data
   ├─ Audio Data Size: 1152 bytes
   └─ PCM Audio Data: [raw bytes]
```

### Example 2: 5.1 Surround Stream
```
Packet: SCReAM Audio: 48000 Hz, 24-bit, 6 ch, 1152 bytes
├─ Header
│  ├─ Sample Rate: 48000 Hz
│  ├─ Sample Size: 24 bits
│  ├─ Channels: 6
│  └─ Channel Mask: 0x003F
│     └─ Channel Positions: FRONT_LEFT, FRONT_RIGHT, FRONT_CENTER,
│                           LOW_FREQUENCY, BACK_LEFT, BACK_RIGHT
└─ PCM Audio Data: 1152 bytes
```

## Advanced Features

### Heuristic Detection
The dissector includes heuristic detection, which means it can identify SCReAM packets even on non-standard ports. The heuristic checks:
- Minimum packet size (5 bytes)
- Reasonable sample size (8-32 bits)
- Reasonable channel count (1-8)

### Statistics
You can use Wireshark's Statistics menu to analyze SCReAM traffic:
- **Statistics → Protocol Hierarchy**: See how much SCReAM traffic you're capturing
- **Statistics → Conversations**: Analyze UDP conversations carrying SCReAM data
- **Statistics → IO Graph**: Visualize SCReAM packet rate and throughput

## Contributing

If you find issues or want to improve the dissector, please contribute to the SCReAM project:
https://github.com/duncanthrax/scream

## License

This dissector follows the same license as the SCReAM project (MS-PL).

## References

- [SCReAM GitHub Repository](https://github.com/duncanthrax/scream)
- [Wireshark Lua API Documentation](https://www.wireshark.org/docs/wsdg_html_chunked/wsluarm.html)
- [Microsoft WAVEFORMATEXTENSIBLE](https://docs.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible)
