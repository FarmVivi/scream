# Quick Reference - SCReAM Wireshark Dissector

## Installation (Quick)

**Windows:**
```
Copy scream.lua to: %APPDATA%\Wireshark\plugins\
```

**Linux/macOS:**
```bash
mkdir -p ~/.local/lib/wireshark/plugins/
cp scream.lua ~/.local/lib/wireshark/plugins/
```

**Restart Wireshark or press Ctrl+Shift+L to reload Lua plugins.**

## Quick Start

1. **Capture Filter:** `udp port 4010`
2. **Display Filter:** `scream`
3. Look for "SCReAM" in the Protocol column

## Common Display Filters

```
scream                                    # All SCReAM packets
scream.channels == 2                      # Stereo only
scream.sample_size == 16                  # 16-bit audio only
scream.sample_rate_calculated contains "48000"  # 48kHz only
scream.audio_data_size > 1000             # Large packets only
```

## Protocol Details

### Default Configuration
- **UDP Port:** 4010
- **Multicast:** 239.255.77.77
- **Packet Size:** 5-byte header + up to 1152 bytes audio

### Packet Header (5 bytes)
```
+--------+-------------+-----------+--------------+
| Byte 0 | Byte 1      | Byte 2    | Bytes 3-4    |
+--------+-------------+-----------+--------------+
| Rate   | Sample Size | Channels  | Channel Mask |
+--------+-------------+-----------+--------------+
```

### Sample Rate Encoding (Byte 0)
- **Bit 7:** Base rate (0=48kHz, 1=44.1kHz)
- **Bits 6-0:** Multiplier

### Common Configurations

| Rate  | Bits | Ch | Header (hex)    |
|-------|------|----|-----------------| 
| 48kHz | 16   | 2  | `00 10 02 03 00` |
| 44.1k | 16   | 2  | `80 10 02 03 00` |
| 48kHz | 24   | 6  | `00 18 06 3F 00` |
| 96kHz | 16   | 2  | `01 10 02 03 00` |

## Troubleshooting

**Dissector not working?**
- Check: Help → About → Folders → Personal Plugins
- Reload: Analyze → Reload Lua Plugins (Ctrl+Shift+L)
- Console: View → Show Console (check for errors)

**Using custom port?**
- Right-click packet → Decode As... → SCReAM

## Channel Mask Quick Reference

| Value  | Configuration |
|--------|---------------|
| 0x0003 | Stereo (L+R) |
| 0x0007 | 2.1 (L+R+C) |
| 0x003F | 5.1 Surround |
| 0x063F | 7.1 Surround |

## More Information

- Full documentation: [README.md](README.md)
- Packet examples: [EXAMPLES.md](EXAMPLES.md)
- SCReAM project: https://github.com/duncanthrax/scream
