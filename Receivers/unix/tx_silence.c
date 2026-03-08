#include "tx_silence.h"

#include <limits.h>
#include <stdlib.h>
#include <string.h>

#define TX_SILENCE_SAMPLE_LEVEL 5

static int tx_is_sample_silent(const uint8_t *data, size_t index, uint8_t bytes_per_sample)
{
  int16_t s16 = 0;
  int32_t s32 = 0;

  if (bytes_per_sample == 2u) {
    memcpy(&s16, data + (index * 2u), sizeof(s16));
    return abs((int)s16) < TX_SILENCE_SAMPLE_LEVEL;
  }

  if (bytes_per_sample == 4u) {
    memcpy(&s32, data + (index * 4u), sizeof(s32));
    if (s32 == INT_MIN) {
      return 0;
    }
    return abs(s32) < (65536 * TX_SILENCE_SAMPLE_LEVEL);
  }

  return 0;
}

void tx_silence_init(tx_silence_gate_t *gate, uint32_t threshold_samples)
{
  if (gate == NULL) {
    return;
  }

  gate->state = 0u;
  gate->threshold_samples = threshold_samples;
}

void tx_silence_reset(tx_silence_gate_t *gate)
{
  if (gate == NULL) {
    return;
  }

  gate->state = 0u;
}

int tx_silence_process(
  tx_silence_gate_t *gate,
  const uint8_t *data,
  size_t size,
  uint8_t bytes_per_sample,
  uint8_t channels,
  tx_silence_emit_fn emit_fn,
  void *emit_userdata)
{
  size_t sample_count;
  size_t frame_bytes;
  size_t start_copy_byte = 0u;
  size_t i;
  int current_sample_is_silent;
  size_t frame_start;
  size_t emit_size;

  if (gate == NULL || data == NULL || emit_fn == NULL) {
    return -1;
  }

  if (size == 0u) {
    return 0;
  }

  if (channels == 0u || bytes_per_sample == 0u) {
    return -1;
  }

  if (gate->threshold_samples == 0u) {
    return emit_fn(data, size, emit_userdata);
  }

  sample_count = size / bytes_per_sample;
  frame_bytes = (size_t)channels * bytes_per_sample;

  for (i = 0u; i < sample_count; ++i) {
    current_sample_is_silent = tx_is_sample_silent(data, i, bytes_per_sample);
    frame_start = (i / channels) * frame_bytes;

    if (gate->state > gate->threshold_samples) {
      /* Silent state */
      if (!current_sample_is_silent) {
        gate->state = 0u;
        start_copy_byte = frame_start;
      }
    } else if (gate->state > 0u) {
      /* Gap state */
      if (current_sample_is_silent) {
        gate->state++;
        if (gate->state > gate->threshold_samples) {
          emit_size = frame_start - start_copy_byte;
          if (emit_size > 0u) {
            if (emit_fn(data + start_copy_byte, emit_size, emit_userdata) != 0) {
              return -1;
            }
          }
        }
      } else {
        gate->state = 0u;
      }
    } else {
      /* Not-silent state */
      if (current_sample_is_silent) {
        gate->state++;
      }
    }
  }

  if (gate->state <= gate->threshold_samples) {
    emit_size = size - start_copy_byte;
    if (emit_size > 0u) {
      return emit_fn(data + start_copy_byte, emit_size, emit_userdata);
    }
  }

  return 0;
}
