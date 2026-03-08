#include "tx_protocol.h"

int tx_protocol_encode_sample_rate_marker(uint32_t sample_rate, uint8_t *marker)
{
  uint32_t factor = 0;
  uint8_t value = 0;

  if (marker == NULL) {
    return -1;
  }

  switch (sample_rate) {
    case 44100:
    case 48000:
    case 88200:
    case 96000:
    case 192000:
      break;
    default:
      return -1;
  }

  if ((sample_rate % 44100u) == 0u) {
    factor = sample_rate / 44100u;
    if (factor > 127u) {
      return -1;
    }
    value = (uint8_t)(0x80u | (uint8_t)factor);
  } else if ((sample_rate % 48000u) == 0u) {
    factor = sample_rate / 48000u;
    if (factor > 127u) {
      return -1;
    }
    value = (uint8_t)factor;
  } else {
    return -1;
  }

  if ((value & 0x7Fu) == 0u) {
    return -1;
  }

  *marker = value;
  return 0;
}

int tx_protocol_validate_format(const tx_audio_format_t *format)
{
  uint8_t marker;

  if (format == NULL) {
    return -1;
  }

  if (format->sample_size != 16u &&
      format->sample_size != 24u &&
      format->sample_size != 32u) {
    return -1;
  }

  if (format->channels < 1u || format->channels > 8u) {
    return -1;
  }

  if (tx_protocol_encode_sample_rate_marker(format->sample_rate, &marker) != 0) {
    return -1;
  }

  (void)marker;
  return 0;
}

int tx_protocol_build_header(const tx_audio_format_t *format, uint8_t header[TX_HEADER_SIZE])
{
  uint8_t marker = 0;

  if (format == NULL || header == NULL) {
    return -1;
  }

  if (tx_protocol_validate_format(format) != 0) {
    return -1;
  }

  if (tx_protocol_encode_sample_rate_marker(format->sample_rate, &marker) != 0) {
    return -1;
  }

  header[0] = marker;
  header[1] = format->sample_size;
  header[2] = format->channels;
  header[3] = (uint8_t)(format->channel_map & 0x00FFu);
  header[4] = (uint8_t)((format->channel_map >> 8) & 0x00FFu);

  return 0;
}

uint16_t tx_protocol_default_channel_map(uint8_t channels)
{
  switch (channels) {
    case 1:
      return 0x0004u; /* Front Center */
    case 2:
      return 0x0003u; /* Front Left + Front Right */
    case 3:
      return 0x0007u; /* FL + FR + FC */
    case 4:
      return 0x0033u; /* Quad */
    case 5:
      return 0x0037u; /* 5.0 */
    case 6:
      return 0x003Fu; /* 5.1 */
    case 7:
      return 0x013Fu; /* 6.1 */
    case 8:
      return 0x063Fu; /* 7.1 */
    default:
      return 0x0000u;
  }
}
