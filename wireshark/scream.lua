-- Scream Audio Protocol Dissector for Wireshark
-- This dissector decodes SCReAM virtual audio device packets
--
-- Protocol Description:
-- SCReAM is a virtual sound device for Windows that publishes audio
-- as PCM multicast/unicast streams over UDP (default port 4010).
--
-- Packet Structure:
-- - 5-byte header followed by up to 1152 bytes of PCM audio data
-- - Byte 0: Sample rate encoding
--   - Bit 7: Base rate (0=48kHz, 1=44.1kHz)
--   - Bits 6-0: Multiplier for base rate
-- - Byte 1: Sample size (bits per sample, e.g., 16, 24, 32)
-- - Byte 2: Number of channels (1-8)
-- - Bytes 3-4: Channel mask (little-endian DWORD from WAVEFORMATEXTENSIBLE)
-- - Bytes 5+: Raw PCM audio data

-- Create the protocol
local scream_proto = Proto("scream", "SCReAM Audio Protocol")

-- Define protocol fields
local f_sample_rate_raw = ProtoField.uint8("scream.sample_rate_raw", "Sample Rate (Raw)", base.HEX)
local f_sample_rate_base = ProtoField.uint8("scream.sample_rate_base", "Base Rate", base.DEC, {[0]="48 kHz", [1]="44.1 kHz"})
local f_sample_rate_multiplier = ProtoField.uint8("scream.sample_rate_multiplier", "Rate Multiplier", base.DEC)
local f_sample_rate_calculated = ProtoField.string("scream.sample_rate_calculated", "Calculated Sample Rate")
local f_sample_size = ProtoField.uint8("scream.sample_size", "Sample Size (bits)", base.DEC)
local f_channels = ProtoField.uint8("scream.channels", "Channels", base.DEC)
local f_channel_mask = ProtoField.uint16("scream.channel_mask", "Channel Mask", base.HEX)
local f_channel_positions = ProtoField.string("scream.channel_positions", "Channel Positions")
local f_audio_data = ProtoField.bytes("scream.audio_data", "PCM Audio Data")
local f_audio_data_size = ProtoField.uint32("scream.audio_data_size", "Audio Data Size (bytes)", base.DEC)

-- Register fields
scream_proto.fields = {
    f_sample_rate_raw,
    f_sample_rate_base,
    f_sample_rate_multiplier,
    f_sample_rate_calculated,
    f_sample_size,
    f_channels,
    f_channel_mask,
    f_channel_positions,
    f_audio_data,
    f_audio_data_size
}

-- Channel mask values (from Microsoft WAVEFORMATEXTENSIBLE)
local channel_positions = {
    [0x00001] = "FRONT_LEFT",
    [0x00002] = "FRONT_RIGHT",
    [0x00004] = "FRONT_CENTER",
    [0x00008] = "LOW_FREQUENCY",
    [0x00010] = "BACK_LEFT",
    [0x00020] = "BACK_RIGHT",
    [0x00040] = "FRONT_LEFT_OF_CENTER",
    [0x00080] = "FRONT_RIGHT_OF_CENTER",
    [0x00100] = "BACK_CENTER",
    [0x00200] = "SIDE_LEFT",
    [0x00400] = "SIDE_RIGHT",
    [0x00800] = "TOP_CENTER",
    [0x01000] = "TOP_FRONT_LEFT",
    [0x02000] = "TOP_FRONT_CENTER",
    [0x04000] = "TOP_FRONT_RIGHT",
    [0x08000] = "TOP_BACK_LEFT",
    [0x10000] = "TOP_BACK_CENTER",
    [0x20000] = "TOP_BACK_RIGHT"
}

-- Function to decode channel mask into human-readable string
local function decode_channel_mask(mask)
    if mask == 0 then
        return "Default mapping"
    end
    
    local positions = {}
    for bit, name in pairs(channel_positions) do
        if bit32.band(bit, mask) ~= 0 then
            table.insert(positions, name)
        end
    end
    
    if #positions == 0 then
        return "Unknown"
    end
    
    return table.concat(positions, ", ")
end

-- Function to calculate actual sample rate
local function calculate_sample_rate(rate_byte)
    local base_rate = bit32.rshift(bit32.band(rate_byte, 0x80), 7)
    local multiplier = bit32.band(rate_byte, 0x7F)
    
    local base_hz = (base_rate == 0) and 48000 or 44100
    
    -- Handle multiplier
    -- 0 = base rate, 1 = 2x, 2 = 4x, etc.
    local actual_rate
    if multiplier == 0 then
        actual_rate = base_hz
    elseif multiplier == 1 then
        actual_rate = base_hz * 2
    elseif multiplier == 2 then
        actual_rate = base_hz * 4
    else
        actual_rate = base_hz * multiplier
    end
    
    return base_hz, multiplier, actual_rate
end

-- Dissector function
function scream_proto.dissector(buffer, pinfo, tree)
    -- Check minimum packet length (header is 5 bytes)
    if buffer:len() < 5 then
        return 0
    end
    
    -- Set protocol column
    pinfo.cols.protocol = "SCReAM"
    
    -- Create protocol tree
    local subtree = tree:add(scream_proto, buffer(), "SCReAM Audio Protocol")
    
    -- Parse header (5 bytes)
    local sample_rate_raw = buffer(0, 1):uint()
    local sample_size = buffer(1, 1):uint()
    local channels = buffer(2, 1):uint()
    local channel_mask = buffer(3, 2):le_uint()  -- Little-endian 16-bit
    
    -- Calculate sample rate
    local base_rate, multiplier, actual_rate = calculate_sample_rate(sample_rate_raw)
    
    -- Add header fields to tree
    local header_tree = subtree:add(buffer(0, 5), "Header")
    
    -- Sample rate
    local rate_tree = header_tree:add(f_sample_rate_raw, buffer(0, 1))
    rate_tree:add(f_sample_rate_base, buffer(0, 1))
    rate_tree:add(f_sample_rate_multiplier, buffer(0, 1), multiplier)
    rate_tree:add(f_sample_rate_calculated, buffer(0, 1), string.format("%d Hz", actual_rate))
    
    -- Sample size
    header_tree:add(f_sample_size, buffer(1, 1))
    
    -- Channels
    header_tree:add(f_channels, buffer(2, 1))
    
    -- Channel mask
    local mask_tree = header_tree:add(f_channel_mask, buffer(3, 2))
    local channel_pos_str = decode_channel_mask(channel_mask)
    mask_tree:add(f_channel_positions, buffer(3, 2), channel_pos_str)
    
    -- Audio data (remaining bytes after header)
    local audio_size = buffer:len() - 5
    if audio_size > 0 then
        local audio_tree = subtree:add(buffer(5), "PCM Audio Data")
        audio_tree:add(f_audio_data_size, audio_size)
        audio_tree:add(f_audio_data, buffer(5))
    end
    
    -- Update info column with summary
    pinfo.cols.info = string.format(
        "SCReAM Audio: %d Hz, %d-bit, %d ch, %d bytes",
        actual_rate, sample_size, channels, audio_size
    )
    
    return buffer:len()
end

-- Register the dissector for UDP port 4010 (default SCReAM port)
local udp_table = DissectorTable.get("udp.port")
udp_table:add(4010, scream_proto)

-- Also allow for heuristic dissection (for custom ports)
function scream_proto.heuristic_checker(buffer, pinfo, tree)
    -- Minimum packet size check
    if buffer:len() < 5 then
        return false
    end
    
    -- Check if this looks like a SCReAM packet
    -- Sample size should be reasonable (8, 16, 24, or 32 bits typically)
    local sample_size = buffer(1, 1):uint()
    if sample_size < 8 or sample_size > 32 then
        return false
    end
    
    -- Channels should be reasonable (1-8 typically)
    local channels = buffer(2, 1):uint()
    if channels < 1 or channels > 8 then
        return false
    end
    
    -- If checks pass, try to dissect
    scream_proto.dissector(buffer, pinfo, tree)
    return true
end

-- Register heuristic dissector
scream_proto:register_heuristic("udp", scream_proto.heuristic_checker)

-- Info message when loaded
if gui_enabled() then
    local function show_info()
        local msg = [[
SCReAM Audio Protocol Dissector loaded successfully!

This dissector decodes SCReAM virtual audio packets (default UDP port 4010).

To use:
1. Capture UDP traffic on port 4010 (or the configured SCReAM port)
2. The dissector will automatically decode SCReAM packets
3. Look for "SCReAM" in the protocol column

For more information about SCReAM, visit:
https://github.com/duncanthrax/scream
        ]]
        -- This would show a dialog, but we'll just print to console
        print("SCReAM dissector loaded")
    end
    show_info()
end
