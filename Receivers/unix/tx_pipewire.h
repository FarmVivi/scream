#ifndef TX_PIPEWIRE_H
#define TX_PIPEWIRE_H

#include <stddef.h>
#include <stdint.h>

#include "tx_protocol.h"

typedef struct tx_pipewire_config {
  const char *sink_name;
  const char *stream_name;
  uint32_t sample_rate;
  uint8_t sample_size;
  uint8_t channels;
  int verbose;
} tx_pipewire_config_t;

typedef int (*tx_pipewire_audio_fn)(
  const tx_audio_format_t *format,
  const uint8_t *audio,
  size_t audio_size,
  void *userdata);

int tx_pipewire_run(
  const tx_pipewire_config_t *config,
  tx_pipewire_audio_fn audio_fn,
  void *audio_userdata);

#endif
