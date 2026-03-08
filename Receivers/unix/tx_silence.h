#ifndef TX_SILENCE_H
#define TX_SILENCE_H

#include <stddef.h>
#include <stdint.h>

typedef int (*tx_silence_emit_fn)(const uint8_t *data, size_t size, void *userdata);

typedef struct tx_silence_gate {
  uint64_t state;
  uint32_t threshold_samples;
} tx_silence_gate_t;

void tx_silence_init(tx_silence_gate_t *gate, uint32_t threshold_samples);
void tx_silence_reset(tx_silence_gate_t *gate);
int tx_silence_process(
  tx_silence_gate_t *gate,
  const uint8_t *data,
  size_t size,
  uint8_t bytes_per_sample,
  uint8_t channels,
  tx_silence_emit_fn emit_fn,
  void *emit_userdata);

#endif
