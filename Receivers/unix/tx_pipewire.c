#include "tx_pipewire.h"

#include <errno.h>
#include <math.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <pipewire/pipewire.h>
#include <spa/param/audio/format-utils.h>
#include <spa/param/audio/raw.h>
#include <spa/utils/defs.h>

typedef struct tx_pipewire_state {
  struct pw_main_loop *main_loop;
  struct pw_context *context;
  struct pw_core *core;
  struct pw_stream *stream;
  struct spa_hook stream_listener;

  tx_pipewire_audio_fn audio_fn;
  void *audio_userdata;

  tx_audio_format_t active_format;
  enum spa_audio_format spa_format;
  int have_format;
  int warned_unsupported;
  int exit_code;
  int verbose;

  uint8_t *scratch;
  size_t scratch_size;
} tx_pipewire_state_t;

static uint16_t tx_pipewire_channel_bit(uint32_t spa_channel)
{
  switch (spa_channel) {
    case SPA_AUDIO_CHANNEL_FL:
      return 1u << 0; /* Front Left */
    case SPA_AUDIO_CHANNEL_FR:
      return 1u << 1; /* Front Right */
    case SPA_AUDIO_CHANNEL_FC:
      return 1u << 2; /* Front Center */
    case SPA_AUDIO_CHANNEL_LFE:
      return 1u << 3; /* LFE */
    case SPA_AUDIO_CHANNEL_RL:
      return 1u << 4; /* Rear Left */
    case SPA_AUDIO_CHANNEL_RR:
      return 1u << 5; /* Rear Right */
    case SPA_AUDIO_CHANNEL_FLC:
      return 1u << 6; /* Front Left of Center */
    case SPA_AUDIO_CHANNEL_FRC:
      return 1u << 7; /* Front Right of Center */
    case SPA_AUDIO_CHANNEL_RC:
      return 1u << 8; /* Rear Center */
    case SPA_AUDIO_CHANNEL_SL:
      return 1u << 9; /* Side Left */
    case SPA_AUDIO_CHANNEL_SR:
      return 1u << 10; /* Side Right */
    default:
      return 0u;
  }
}

static uint16_t tx_pipewire_channel_mask(const struct spa_audio_info_raw *info)
{
  uint16_t mask = 0u;
  uint32_t i;

  for (i = 0u; i < info->channels && i < SPA_AUDIO_MAX_CHANNELS; ++i) {
    mask |= tx_pipewire_channel_bit(info->position[i]);
  }

  if (mask == 0u) {
    mask = tx_protocol_default_channel_map((uint8_t)info->channels);
  }

  return mask;
}

static uint8_t tx_pipewire_sample_size_for_spa(enum spa_audio_format format)
{
  switch (format) {
    case SPA_AUDIO_FORMAT_S16_LE:
      return 16u;
    case SPA_AUDIO_FORMAT_S24_LE:
      return 24u;
    case SPA_AUDIO_FORMAT_S24_32_LE:
    case SPA_AUDIO_FORMAT_S32_LE:
    case SPA_AUDIO_FORMAT_F32_LE:
      return 32u;
    default:
      return 0u;
  }
}

static size_t tx_pipewire_input_bytes_per_sample(enum spa_audio_format format)
{
  switch (format) {
    case SPA_AUDIO_FORMAT_S16_LE:
      return 2u;
    case SPA_AUDIO_FORMAT_S24_LE:
      return 3u;
    case SPA_AUDIO_FORMAT_S24_32_LE:
    case SPA_AUDIO_FORMAT_S32_LE:
    case SPA_AUDIO_FORMAT_F32_LE:
      return 4u;
    default:
      return 0u;
  }
}

static enum spa_audio_format tx_pipewire_spa_format_for_sample_size(uint8_t sample_size)
{
  switch (sample_size) {
    case 16u:
      return SPA_AUDIO_FORMAT_S16_LE;
    case 24u:
      return SPA_AUDIO_FORMAT_S24_LE;
    case 32u:
      return SPA_AUDIO_FORMAT_S32_LE;
    default:
      return SPA_AUDIO_FORMAT_UNKNOWN;
  }
}

static const char *tx_pipewire_audio_format_property(uint8_t sample_size)
{
  switch (sample_size) {
    case 16u:
      return "S16LE";
    case 24u:
      return "S24LE";
    case 32u:
      return "S32LE";
    default:
      return NULL;
  }
}

static int tx_pipewire_get_channel_layout(
  uint8_t channels,
  enum spa_audio_channel positions[SPA_AUDIO_MAX_CHANNELS],
  const char **position_property)
{
  if (positions == NULL) {
    return -1;
  }

  switch (channels) {
    case 1u:
      positions[0] = SPA_AUDIO_CHANNEL_FC;
      if (position_property != NULL) {
        *position_property = "[ FC ]";
      }
      return 0;
    case 2u:
      positions[0] = SPA_AUDIO_CHANNEL_FL;
      positions[1] = SPA_AUDIO_CHANNEL_FR;
      if (position_property != NULL) {
        *position_property = "[ FL FR ]";
      }
      return 0;
    case 3u:
      positions[0] = SPA_AUDIO_CHANNEL_FL;
      positions[1] = SPA_AUDIO_CHANNEL_FR;
      positions[2] = SPA_AUDIO_CHANNEL_FC;
      if (position_property != NULL) {
        *position_property = "[ FL FR FC ]";
      }
      return 0;
    case 4u:
      positions[0] = SPA_AUDIO_CHANNEL_FL;
      positions[1] = SPA_AUDIO_CHANNEL_FR;
      positions[2] = SPA_AUDIO_CHANNEL_RL;
      positions[3] = SPA_AUDIO_CHANNEL_RR;
      if (position_property != NULL) {
        *position_property = "[ FL FR RL RR ]";
      }
      return 0;
    case 5u:
      positions[0] = SPA_AUDIO_CHANNEL_FL;
      positions[1] = SPA_AUDIO_CHANNEL_FR;
      positions[2] = SPA_AUDIO_CHANNEL_FC;
      positions[3] = SPA_AUDIO_CHANNEL_RL;
      positions[4] = SPA_AUDIO_CHANNEL_RR;
      if (position_property != NULL) {
        *position_property = "[ FL FR FC RL RR ]";
      }
      return 0;
    case 6u:
      positions[0] = SPA_AUDIO_CHANNEL_FL;
      positions[1] = SPA_AUDIO_CHANNEL_FR;
      positions[2] = SPA_AUDIO_CHANNEL_FC;
      positions[3] = SPA_AUDIO_CHANNEL_LFE;
      positions[4] = SPA_AUDIO_CHANNEL_RL;
      positions[5] = SPA_AUDIO_CHANNEL_RR;
      if (position_property != NULL) {
        *position_property = "[ FL FR FC LFE RL RR ]";
      }
      return 0;
    case 7u:
      positions[0] = SPA_AUDIO_CHANNEL_FL;
      positions[1] = SPA_AUDIO_CHANNEL_FR;
      positions[2] = SPA_AUDIO_CHANNEL_FC;
      positions[3] = SPA_AUDIO_CHANNEL_LFE;
      positions[4] = SPA_AUDIO_CHANNEL_RC;
      positions[5] = SPA_AUDIO_CHANNEL_RL;
      positions[6] = SPA_AUDIO_CHANNEL_RR;
      if (position_property != NULL) {
        *position_property = "[ FL FR FC LFE RC RL RR ]";
      }
      return 0;
    case 8u:
      positions[0] = SPA_AUDIO_CHANNEL_FL;
      positions[1] = SPA_AUDIO_CHANNEL_FR;
      positions[2] = SPA_AUDIO_CHANNEL_FC;
      positions[3] = SPA_AUDIO_CHANNEL_LFE;
      positions[4] = SPA_AUDIO_CHANNEL_RL;
      positions[5] = SPA_AUDIO_CHANNEL_RR;
      positions[6] = SPA_AUDIO_CHANNEL_SL;
      positions[7] = SPA_AUDIO_CHANNEL_SR;
      if (position_property != NULL) {
        *position_property = "[ FL FR FC LFE RL RR SL SR ]";
      }
      return 0;
    default:
      return -1;
  }
}

static void tx_pipewire_on_signal(void *data, int signal_number)
{
  tx_pipewire_state_t *state = (tx_pipewire_state_t *)data;
  (void)signal_number;
  pw_main_loop_quit(state->main_loop);
}

static int tx_pipewire_prepare_audio(
  tx_pipewire_state_t *state,
  const uint8_t *src,
  size_t src_size,
  const uint8_t **out,
  size_t *out_size,
  tx_audio_format_t *out_format)
{
  size_t input_bytes_per_sample;
  size_t sample_count;
  size_t i;
  int32_t sample_i32;
  float sample_f;

  if (state == NULL || src == NULL || out == NULL || out_size == NULL || out_format == NULL) {
    return -1;
  }

  input_bytes_per_sample = tx_pipewire_input_bytes_per_sample(state->spa_format);
  if (input_bytes_per_sample == 0u) {
    return -1;
  }

  src_size -= (src_size % input_bytes_per_sample);
  if (src_size == 0u) {
    *out = NULL;
    *out_size = 0u;
    return 0;
  }

  *out_format = state->active_format;

  if (state->spa_format != SPA_AUDIO_FORMAT_F32_LE) {
    *out = src;
    *out_size = src_size;
    return 0;
  }

  if (state->scratch_size < src_size) {
    uint8_t *new_buf = (uint8_t *)realloc(state->scratch, src_size);
    if (new_buf == NULL) {
      perror("realloc() failed for F32 conversion buffer");
      return -1;
    }
    state->scratch = new_buf;
    state->scratch_size = src_size;
  }

  sample_count = src_size / sizeof(float);
  for (i = 0u; i < sample_count; ++i) {
    memcpy(&sample_f, src + (i * sizeof(float)), sizeof(float));
    if (sample_f > 1.0f) {
      sample_f = 1.0f;
    } else if (sample_f < -1.0f) {
      sample_f = -1.0f;
    }
    sample_i32 = (int32_t)lrintf(sample_f * 2147483647.0f);
    memcpy(state->scratch + (i * sizeof(sample_i32)), &sample_i32, sizeof(sample_i32));
  }

  *out = state->scratch;
  *out_size = src_size;
  out_format->sample_size = 32u;
  return 0;
}

static void tx_pipewire_stream_state_changed(
  void *data,
  enum pw_stream_state old_state,
  enum pw_stream_state new_state,
  const char *error)
{
  tx_pipewire_state_t *state = (tx_pipewire_state_t *)data;
  (void)old_state;

  if (state->verbose > 0) {
    fprintf(stderr, "PipeWire stream state: %s\n", pw_stream_state_as_string(new_state));
  }

  if (new_state == PW_STREAM_STATE_ERROR) {
    fprintf(stderr, "PipeWire stream error: %s\n", error ? error : "(unknown)");
    state->exit_code = 1;
    pw_main_loop_quit(state->main_loop);
  }
}

static void tx_pipewire_stream_param_changed(void *data, uint32_t id, const struct spa_pod *param)
{
  tx_pipewire_state_t *state = (tx_pipewire_state_t *)data;
  struct spa_audio_info_raw info = { 0 };
  tx_audio_format_t format = { 0 };

  if (id != SPA_PARAM_Format || param == NULL) {
    return;
  }

  if (spa_format_audio_raw_parse(param, &info) != 0) {
    return;
  }

  format.sample_rate = info.rate;
  format.sample_size = tx_pipewire_sample_size_for_spa(info.format);
  format.channels = (uint8_t)info.channels;
  format.channel_map = tx_pipewire_channel_mask(&info);

  if (format.sample_size == 0u || tx_protocol_validate_format(&format) != 0) {
    state->have_format = 0;
    if (!state->warned_unsupported || state->verbose > 0) {
      fprintf(stderr,
        "Unsupported PipeWire format/rate/channels: spa_format=%u rate=%u channels=%u\n",
        info.format,
        info.rate,
        info.channels);
    }
    state->warned_unsupported = 1;
    return;
  }

  state->warned_unsupported = 0;
  state->spa_format = info.format;
  state->active_format = format;
  state->have_format = 1;

  if (state->verbose > 0) {
    fprintf(stderr,
      "PipeWire sink format: %u Hz, %u-bit, %u ch, mask=0x%04x\n",
      state->active_format.sample_rate,
      state->active_format.sample_size,
      state->active_format.channels,
      state->active_format.channel_map);
  }
}

static void tx_pipewire_stream_process(void *data)
{
  tx_pipewire_state_t *state = (tx_pipewire_state_t *)data;
  struct pw_buffer *pwbuf;
  struct spa_buffer *buffer;
  struct spa_data *spa_data;
  const uint8_t *src;
  const uint8_t *out;
  size_t src_size;
  size_t out_size;
  tx_audio_format_t emit_format;

  while ((pwbuf = pw_stream_dequeue_buffer(state->stream)) != NULL) {
    buffer = pwbuf->buffer;
    if (buffer->n_datas < 1u) {
      pw_stream_queue_buffer(state->stream, pwbuf);
      continue;
    }

    spa_data = &buffer->datas[0];
    if (spa_data->data == NULL || spa_data->chunk == NULL || spa_data->chunk->size == 0u) {
      pw_stream_queue_buffer(state->stream, pwbuf);
      continue;
    }

    if (!state->have_format) {
      pw_stream_queue_buffer(state->stream, pwbuf);
      continue;
    }

    src = SPA_MEMBER(spa_data->data, spa_data->chunk->offset, const uint8_t);
    src_size = spa_data->chunk->size;

    if (tx_pipewire_prepare_audio(state, src, src_size, &out, &out_size, &emit_format) != 0) {
      state->exit_code = 1;
      pw_main_loop_quit(state->main_loop);
      pw_stream_queue_buffer(state->stream, pwbuf);
      continue;
    }

    if (out != NULL && out_size > 0u) {
      if (state->audio_fn(&emit_format, out, out_size, state->audio_userdata) != 0) {
        state->exit_code = 1;
        pw_main_loop_quit(state->main_loop);
      }
    }

    pw_stream_queue_buffer(state->stream, pwbuf);
  }
}

static struct pw_properties *tx_pipewire_build_sink_properties(const tx_pipewire_config_t *config)
{
  enum spa_audio_channel positions[SPA_AUDIO_MAX_CHANNELS] = { 0 };
  const char *position_property = NULL;
  const char *format_property = NULL;
  char channels_property[5];
  char rate_property[12];
  struct pw_properties *props;

  if (tx_pipewire_get_channel_layout(config->channels, positions, &position_property) != 0) {
    fprintf(stderr, "Unsupported channel count for PipeWire stream: %u\n", config->channels);
    return NULL;
  }

  format_property = tx_pipewire_audio_format_property(config->sample_size);
  if (format_property == NULL) {
    fprintf(stderr, "Unsupported sample size for PipeWire stream: %u\n", config->sample_size);
    return NULL;
  }

  if (snprintf(channels_property, sizeof(channels_property), "%u", config->channels) >= (int)sizeof(channels_property)) {
    fprintf(stderr, "Invalid channel count for PipeWire stream: %u\n", config->channels);
    return NULL;
  }

  if (snprintf(rate_property, sizeof(rate_property), "%u", config->sample_rate) >= (int)sizeof(rate_property)) {
    fprintf(stderr, "Invalid sample rate for PipeWire stream: %u\n", config->sample_rate);
    return NULL;
  }

  props = pw_properties_new(
    PW_KEY_MEDIA_TYPE, "Audio",
    PW_KEY_MEDIA_CATEGORY, "Playback",
    PW_KEY_MEDIA_ROLE, "Music",
    PW_KEY_MEDIA_CLASS, "Audio/Sink",
    PW_KEY_NODE_NAME, config->sink_name,
    PW_KEY_NODE_DESCRIPTION, config->sink_name,
    PW_KEY_MEDIA_NAME, config->stream_name,
    PW_KEY_NODE_VIRTUAL, "true",
    "priority.session", "1",
    PW_KEY_NODE_AUTOCONNECT, "true",
    PW_KEY_AUDIO_FORMAT, format_property,
    PW_KEY_AUDIO_RATE, rate_property,
    PW_KEY_AUDIO_CHANNELS, channels_property,
    "audio.position", position_property,
    NULL);

  if (props == NULL) {
    fprintf(stderr, "Failed to allocate PipeWire sink stream properties.\n");
    return NULL;
  }

  return props;
}

int tx_pipewire_run(
  const tx_pipewire_config_t *config,
  tx_pipewire_audio_fn audio_fn,
  void *audio_userdata)
{
  tx_pipewire_state_t state;
  struct pw_stream_events stream_events = {
    PW_VERSION_STREAM_EVENTS,
    .state_changed = tx_pipewire_stream_state_changed,
    .param_changed = tx_pipewire_stream_param_changed,
    .process = tx_pipewire_stream_process
  };
  struct pw_properties *stream_props;
  struct spa_source *sigint_source;
  struct spa_source *sigterm_source;
  uint8_t stream_param_buffer[1024];
  struct spa_pod_builder stream_param_builder =
    SPA_POD_BUILDER_INIT(stream_param_buffer, sizeof(stream_param_buffer));
  const struct spa_pod *stream_params[1];
  tx_audio_format_t requested_format = { 0 };
  struct spa_audio_info_raw requested_info = { 0 };
  enum spa_audio_channel requested_positions[SPA_AUDIO_MAX_CHANNELS] = { 0 };
  enum spa_audio_format requested_spa_format;
  uint32_t i;
  int rc = 0;

  if (config == NULL || config->sink_name == NULL || config->stream_name == NULL || audio_fn == NULL) {
    return -1;
  }

  requested_format.sample_rate = config->sample_rate;
  requested_format.sample_size = config->sample_size;
  requested_format.channels = config->channels;
  requested_format.channel_map = tx_protocol_default_channel_map(config->channels);
  if (tx_protocol_validate_format(&requested_format) != 0) {
    fprintf(stderr,
      "Invalid sender format: sample_rate=%u sample_size=%u channels=%u\n",
      config->sample_rate,
      config->sample_size,
      config->channels);
    return -1;
  }

  if (tx_pipewire_get_channel_layout(config->channels, requested_positions, NULL) != 0) {
    fprintf(stderr, "Unsupported channel layout: %u\n", config->channels);
    return -1;
  }

  requested_spa_format = tx_pipewire_spa_format_for_sample_size(config->sample_size);
  if (requested_spa_format == SPA_AUDIO_FORMAT_UNKNOWN) {
    fprintf(stderr, "Unsupported sample size for PipeWire sink: %u\n", config->sample_size);
    return -1;
  }

  memset(&state, 0, sizeof(state));
  state.audio_fn = audio_fn;
  state.audio_userdata = audio_userdata;
  state.verbose = config->verbose;
  state.exit_code = 0;

  pw_init(NULL, NULL);

  state.main_loop = pw_main_loop_new(NULL);
  if (state.main_loop == NULL) {
    fprintf(stderr, "Failed to create PipeWire main loop.\n");
    rc = -1;
    goto cleanup;
  }

  state.context = pw_context_new(pw_main_loop_get_loop(state.main_loop), NULL, 0);
  if (state.context == NULL) {
    fprintf(stderr, "Failed to create PipeWire context.\n");
    rc = -1;
    goto cleanup;
  }

  state.core = pw_context_connect(state.context, NULL, 0);
  if (state.core == NULL) {
    fprintf(stderr, "Failed to connect to PipeWire core.\n");
    rc = -1;
    goto cleanup;
  }

  stream_props = tx_pipewire_build_sink_properties(config);
  if (stream_props == NULL) {
    rc = -1;
    goto cleanup;
  }

  state.stream = pw_stream_new(state.core, config->sink_name, stream_props);
  if (state.stream == NULL) {
    fprintf(stderr, "Failed to create PipeWire sink stream '%s'.\n", config->sink_name);
    rc = -1;
    goto cleanup;
  }

  pw_stream_add_listener(state.stream, &state.stream_listener, &stream_events, &state);

  /* Request an explicit sink format so applications can link without remapping surprises. */
  requested_info.format = requested_spa_format;
  requested_info.rate = config->sample_rate;
  requested_info.channels = config->channels;
  for (i = 0u; i < requested_info.channels && i < SPA_AUDIO_MAX_CHANNELS; ++i) {
    requested_info.position[i] = requested_positions[i];
  }

  /* Some PipeWire setups do not emit an initial Format param event reliably.
   * Preload the requested format so process() can immediately forward PCM. */
  state.spa_format = requested_info.format;
  state.active_format = requested_format;
  state.active_format.channel_map = tx_pipewire_channel_mask(&requested_info);
  state.have_format = 1;
  if (state.verbose > 0 && state.have_format) {
    fprintf(stderr,
      "PipeWire preloaded format: %u Hz, %u-bit, %u ch, mask=0x%04x\n",
      state.active_format.sample_rate,
      state.active_format.sample_size,
      state.active_format.channels,
      state.active_format.channel_map);
    fprintf(stderr, "PipeWire sink node ready: %s (media.name: %s)\n", config->sink_name, config->stream_name);
  }

  stream_params[0] = spa_format_audio_raw_build(
    &stream_param_builder,
    SPA_PARAM_EnumFormat,
    &requested_info);

  if (pw_stream_connect(
      state.stream,
      PW_DIRECTION_INPUT,
      PW_ID_ANY,
      PW_STREAM_FLAG_AUTOCONNECT | PW_STREAM_FLAG_MAP_BUFFERS | PW_STREAM_FLAG_RT_PROCESS,
      stream_params,
      1) != 0) {
    fprintf(stderr, "Failed to connect PipeWire sink stream.\n");
    rc = -1;
    goto cleanup;
  }

  sigint_source = pw_loop_add_signal(pw_main_loop_get_loop(state.main_loop), SIGINT, tx_pipewire_on_signal, &state);
  sigterm_source = pw_loop_add_signal(pw_main_loop_get_loop(state.main_loop), SIGTERM, tx_pipewire_on_signal, &state);
  if (sigint_source == NULL || sigterm_source == NULL) {
    fprintf(stderr, "Failed to attach signal handlers to PipeWire loop.\n");
    rc = -1;
    goto cleanup;
  }

  if (state.verbose > 0) {
    fprintf(stderr, "Scream TX sink running. Press Ctrl+C to stop.\n");
  }

  pw_main_loop_run(state.main_loop);
  if (state.exit_code != 0) {
    rc = -1;
  }

cleanup:
  if (state.stream != NULL) {
    pw_stream_destroy(state.stream);
    state.stream = NULL;
  }

  if (state.core != NULL) {
    pw_core_disconnect(state.core);
    state.core = NULL;
  }

  if (state.context != NULL) {
    pw_context_destroy(state.context);
    state.context = NULL;
  }

  if (state.main_loop != NULL) {
    pw_main_loop_destroy(state.main_loop);
    state.main_loop = NULL;
  }

  free(state.scratch);
  state.scratch = NULL;
  state.scratch_size = 0u;

  pw_deinit();
  return rc;
}
