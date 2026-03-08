#include "tx_packetizer.h"

#include <string.h>

int tx_packetizer_init(tx_packetizer_t *packetizer, tx_packet_send_fn send_fn, void *send_userdata)
{
  if (packetizer == NULL || send_fn == NULL) {
    return -1;
  }

  memset(packetizer, 0, sizeof(*packetizer));
  packetizer->send_fn = send_fn;
  packetizer->send_userdata = send_userdata;
  return 0;
}

void tx_packetizer_reset(tx_packetizer_t *packetizer)
{
  if (packetizer == NULL) {
    return;
  }

  packetizer->payload_used = 0u;
}

int tx_packetizer_set_format(tx_packetizer_t *packetizer, const tx_audio_format_t *format)
{
  uint8_t header[TX_HEADER_SIZE];

  if (packetizer == NULL || format == NULL) {
    return -1;
  }

  if (tx_protocol_build_header(format, header) != 0) {
    return -1;
  }

  if (packetizer->format_set && memcmp(&packetizer->format, format, sizeof(*format)) != 0) {
    /* Drop any pending payload when the format changes to avoid mixed packets. */
    packetizer->payload_used = 0u;
  }

  memcpy(packetizer->packet, header, TX_HEADER_SIZE);
  memcpy(&packetizer->format, format, sizeof(*format));
  packetizer->format_set = 1;
  return 0;
}

int tx_packetizer_write(tx_packetizer_t *packetizer, const uint8_t *data, size_t size)
{
  size_t to_copy;
  size_t payload_offset;
  size_t remaining;
  int rc;

  if (packetizer == NULL || data == NULL) {
    return -1;
  }

  if (!packetizer->format_set) {
    return -1;
  }

  remaining = size;
  while (remaining > 0u) {
    payload_offset = TX_HEADER_SIZE + packetizer->payload_used;
    to_copy = TX_PCM_PAYLOAD_SIZE - packetizer->payload_used;
    if (to_copy > remaining) {
      to_copy = remaining;
    }

    memcpy(packetizer->packet + payload_offset, data + (size - remaining), to_copy);
    packetizer->payload_used += to_copy;
    remaining -= to_copy;

    if (packetizer->payload_used == TX_PCM_PAYLOAD_SIZE) {
      rc = packetizer->send_fn(packetizer->packet, TX_PACKET_SIZE, packetizer->send_userdata);
      if (rc != 0) {
        return rc;
      }
      packetizer->payload_used = 0u;
    }
  }

  return 0;
}
