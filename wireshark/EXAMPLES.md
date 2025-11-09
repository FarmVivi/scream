# SCReAM Packet Examples

This document provides examples of SCReAM packet structures to help understand how the protocol works.

## Packet Format

Every SCReAM packet consists of:
- 5-byte header
- Variable length PCM audio data (typically 1152 bytes)

## Example 1: Stereo 48kHz 16-bit

**Hexadecimal representation of header:**
```
00 10 02 03 00
```

**Breakdown:**
- `00`: Sample rate byte
  - Bit 7 = 0: Base rate is 48 kHz
  - Bits 6-0 = 0: No multiplier (1x)
  - Result: 48000 Hz
- `10`: Sample size = 16 bits (0x10 = 16 decimal)
- `02`: Channels = 2 (stereo)
- `03 00`: Channel mask = 0x0003 (little-endian)
  - Bit 0 (0x0001) = FRONT_LEFT
  - Bit 1 (0x0002) = FRONT_RIGHT

**Complete packet example (first 20 bytes):**
```
00 10 02 03 00 [PCM data starts here...]
               A4 B2 C3 D1 E5 F6 ...
```

## Example 2: Stereo 44.1kHz 16-bit

**Hexadecimal representation of header:**
```
80 10 02 03 00
```

**Breakdown:**
- `80`: Sample rate byte
  - Bit 7 = 1: Base rate is 44.1 kHz
  - Bits 6-0 = 0: No multiplier (1x)
  - Result: 44100 Hz
- `10`: Sample size = 16 bits
- `02`: Channels = 2 (stereo)
- `03 00`: Channel mask = 0x0003

## Example 3: 5.1 Surround 48kHz 24-bit

**Hexadecimal representation of header:**
```
00 18 06 3F 00
```

**Breakdown:**
- `00`: 48000 Hz (48 kHz base, no multiplier)
- `18`: Sample size = 24 bits (0x18 = 24 decimal)
- `06`: Channels = 6 (5.1 surround)
- `3F 00`: Channel mask = 0x003F
  - Bit 0 (0x0001) = FRONT_LEFT
  - Bit 1 (0x0002) = FRONT_RIGHT
  - Bit 2 (0x0004) = FRONT_CENTER
  - Bit 3 (0x0008) = LOW_FREQUENCY (subwoofer)
  - Bit 4 (0x0010) = BACK_LEFT
  - Bit 5 (0x0020) = BACK_RIGHT

## Example 4: Stereo 96kHz 16-bit (2x multiplier)

**Hexadecimal representation of header:**
```
01 10 02 03 00
```

**Breakdown:**
- `01`: Sample rate byte
  - Bit 7 = 0: Base rate is 48 kHz
  - Bits 6-0 = 1: 2x multiplier
  - Result: 48000 × 2 = 96000 Hz
- `10`: Sample size = 16 bits
- `02`: Channels = 2 (stereo)
- `03 00`: Channel mask = 0x0003

## Example 5: Mono 48kHz 16-bit

**Hexadecimal representation of header:**
```
00 10 01 04 00
```

**Breakdown:**
- `00`: 48000 Hz
- `10`: Sample size = 16 bits
- `01`: Channels = 1 (mono)
- `04 00`: Channel mask = 0x0004 (FRONT_CENTER)

## Typical Packet Sizes

For different configurations:

### Stereo 16-bit
- Header: 5 bytes
- Audio data: 1152 bytes (576 samples × 2 channels × 2 bytes)
- Total: 1157 bytes

### Stereo 24-bit
- Header: 5 bytes
- Audio data: 1152 bytes (384 samples × 2 channels × 3 bytes)
- Total: 1157 bytes

### 5.1 16-bit
- Header: 5 bytes
- Audio data: 1152 bytes (192 samples × 6 channels × 2 bytes)
- Total: 1157 bytes

### 5.1 24-bit
- Header: 5 bytes
- Audio data: 1152 bytes (128 samples × 6 channels × 3 bytes)
- Total: 1157 bytes

## UDP Packet Example

A typical SCReAM UDP packet on the network would look like:

```
Ethernet Header
IPv4 Header (dst: 239.255.77.77 for multicast)
UDP Header (dst port: 4010)
SCReAM Data:
  00 10 02 03 00  [5-byte header]
  A4 B2 C3 D1 ... [1152 bytes of PCM audio data]
```

## Testing with Wireshark

To test the dissector with these examples:

1. Create a test capture with `scapy` or similar tool:
```python
from scapy.all import *

# Create a SCReAM packet (stereo, 48kHz, 16-bit)
scream_header = bytes([0x00, 0x10, 0x02, 0x03, 0x00])
scream_audio = bytes([0x00] * 1152)  # Silent audio
scream_packet = scream_header + scream_audio

# Create UDP packet
pkt = IP(dst="239.255.77.77")/UDP(sport=12345, dport=4010)/Raw(load=scream_packet)

# Write to pcap file
wrpcap("scream_test.pcap", pkt)
```

2. Open the pcap file in Wireshark with the SCReAM dissector loaded
3. The packet should be decoded and show all fields correctly

## Sample Rate Encoding Reference

| Byte Value | Base Rate | Multiplier | Resulting Rate |
|------------|-----------|------------|----------------|
| 0x00 | 48 kHz | 1x | 48000 Hz |
| 0x01 | 48 kHz | 2x | 96000 Hz |
| 0x02 | 48 kHz | 4x | 192000 Hz |
| 0x80 | 44.1 kHz | 1x | 44100 Hz |
| 0x81 | 44.1 kHz | 2x | 88200 Hz |
| 0x82 | 44.1 kHz | 4x | 176400 Hz |

Note: The actual multiplier calculation may vary based on the implementation. Refer to the source code for exact details.
