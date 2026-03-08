#ifndef TX_PACKETIZER_H
#define TX_PACKETIZER_H

#include <stddef.h>
#include <stdint.h>

#include "tx_protocol.h"

typedef int (*tx_packet_send_fn)(const uint8_t *packet, size_t len, void *userdata);

typedef struct tx_packetizer {
  tx_audio_format_t format;
  uint8_t packet[TX_PACKET_SIZE];
  size_t payload_used;
  int format_set;
  tx_packet_send_fn send_fn;
  void *send_userdata;
} tx_packetizer_t;

int tx_packetizer_init(tx_packetizer_t *packetizer, tx_packet_send_fn send_fn, void *send_userdata);
int tx_packetizer_set_format(tx_packetizer_t *packetizer, const tx_audio_format_t *format);
int tx_packetizer_write(tx_packetizer_t *packetizer, const uint8_t *data, size_t size);
void tx_packetizer_reset(tx_packetizer_t *packetizer);

#endif
