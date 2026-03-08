#ifndef TX_PROTOCOL_H
#define TX_PROTOCOL_H

#include <stdint.h>
#include <stddef.h>

#define TX_DEFAULT_DEST_IP "239.255.77.77"
#define TX_DEFAULT_DEST_PORT 4010
#define TX_DEFAULT_BIND_IP "0.0.0.0"

#define TX_HEADER_SIZE 5
#define TX_PCM_PAYLOAD_SIZE 1152
#define TX_PACKET_SIZE (TX_HEADER_SIZE + TX_PCM_PAYLOAD_SIZE)

typedef struct tx_audio_format {
  uint32_t sample_rate;
  uint8_t sample_size;
  uint8_t channels;
  uint16_t channel_map;
} tx_audio_format_t;

int tx_protocol_encode_sample_rate_marker(uint32_t sample_rate, uint8_t *marker);
int tx_protocol_validate_format(const tx_audio_format_t *format);
int tx_protocol_build_header(const tx_audio_format_t *format, uint8_t header[TX_HEADER_SIZE]);
uint16_t tx_protocol_default_channel_map(uint8_t channels);

#endif
