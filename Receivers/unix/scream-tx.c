#include <errno.h>
#include <getopt.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "tx_packetizer.h"
#include "tx_pipewire.h"
#include "tx_protocol.h"
#include "tx_silence.h"
#include "tx_udp.h"

typedef struct tx_app_config {
  char dest_ip[64];
  uint16_t dest_port;
  char bind_ip[64];
  uint16_t bind_port;
  uint8_t ttl;
  uint8_t dscp;
  uint32_t silence_threshold_samples;
  uint32_t sample_rate;
  uint8_t sample_size;
  uint8_t channels;
  char sink_name[128];
  char stream_name[128];
  int verbose;
} tx_app_config_t;

typedef struct tx_runtime {
  tx_packetizer_t packetizer;
  tx_silence_gate_t silence;
  tx_udp_context_t udp;
  tx_audio_format_t current_format;
  int have_format;
  int verbose;
} tx_runtime_t;

enum {
  OPT_DEST_IP = 1000,
  OPT_DEST_PORT,
  OPT_BIND_IP,
  OPT_BIND_PORT,
  OPT_TTL,
  OPT_DSCP,
  OPT_SILENCE_THRESHOLD,
  OPT_SAMPLE_RATE,
  OPT_SAMPLE_SIZE,
  OPT_CHANNELS,
  OPT_SINK_NAME,
  OPT_STREAM_NAME,
  OPT_VERBOSE
};

static const struct option tx_long_options[] = {
  { "dest-ip", required_argument, NULL, OPT_DEST_IP },
  { "dest-port", required_argument, NULL, OPT_DEST_PORT },
  { "bind-ip", required_argument, NULL, OPT_BIND_IP },
  { "bind-port", required_argument, NULL, OPT_BIND_PORT },
  { "ttl", required_argument, NULL, OPT_TTL },
  { "dscp", required_argument, NULL, OPT_DSCP },
  { "silence-threshold-samples", required_argument, NULL, OPT_SILENCE_THRESHOLD },
  { "sample-rate", required_argument, NULL, OPT_SAMPLE_RATE },
  { "sample-size", required_argument, NULL, OPT_SAMPLE_SIZE },
  { "channels", required_argument, NULL, OPT_CHANNELS },
  { "sink-name", required_argument, NULL, OPT_SINK_NAME },
  { "stream-name", required_argument, NULL, OPT_STREAM_NAME },
  { "verbose", no_argument, NULL, OPT_VERBOSE },
  { "help", no_argument, NULL, 'h' },
  { NULL, 0, NULL, 0 }
};

static void tx_show_usage(const char *argv0)
{
  fprintf(stderr, "Usage: %s [options]\n", argv0);
  fprintf(stderr, "\n");
  fprintf(stderr, "Linux PipeWire sender for Scream protocol.\n");
  fprintf(stderr, "\n");
  fprintf(stderr, "Options:\n");
  fprintf(stderr, "  --dest-ip <IPv4>                     Destination IPv4 (default %s)\n", TX_DEFAULT_DEST_IP);
  fprintf(stderr, "  --dest-port <port>                   Destination UDP port (default %u)\n", TX_DEFAULT_DEST_PORT);
  fprintf(stderr, "  --bind-ip <IPv4>                     Source bind IPv4 (default %s)\n", TX_DEFAULT_BIND_IP);
  fprintf(stderr, "  --bind-port <port>                   Source bind UDP port (default 0)\n");
  fprintf(stderr, "  --ttl <0-255>                        TTL (0 disables explicit TTL)\n");
  fprintf(stderr, "  --dscp <0-63>                        DSCP value for IP_TOS\n");
  fprintf(stderr, "  --silence-threshold-samples <n>      Silence suppression threshold in samples (0 disables)\n");
  fprintf(stderr, "  --sample-rate <Hz>                   Audio sample rate (default 48000)\n");
  fprintf(stderr, "  --sample-size <16|24|32>             Audio sample size in bits (default 16)\n");
  fprintf(stderr, "  --channels <1-8>                     Channel count (default 2)\n");
  fprintf(stderr, "  --sink-name <name>                   Virtual PipeWire sink name\n");
  fprintf(stderr, "  --stream-name <name>                 PipeWire stream/client name\n");
  fprintf(stderr, "  --verbose                            Verbose logging\n");
  fprintf(stderr, "  --help                               Show this help\n");
  fprintf(stderr, "\n");
  fprintf(stderr, "Environment variable equivalents:\n");
  fprintf(stderr, "  SCREAM_TX_DEST_IP\n");
  fprintf(stderr, "  SCREAM_TX_DEST_PORT\n");
  fprintf(stderr, "  SCREAM_TX_BIND_IP\n");
  fprintf(stderr, "  SCREAM_TX_BIND_PORT\n");
  fprintf(stderr, "  SCREAM_TX_TTL\n");
  fprintf(stderr, "  SCREAM_TX_DSCP\n");
  fprintf(stderr, "  SCREAM_TX_SILENCE_THRESHOLD_SAMPLES\n");
  fprintf(stderr, "  SCREAM_TX_SAMPLE_RATE\n");
  fprintf(stderr, "  SCREAM_TX_SAMPLE_SIZE\n");
  fprintf(stderr, "  SCREAM_TX_CHANNELS\n");
  fprintf(stderr, "  SCREAM_TX_SINK_NAME\n");
  fprintf(stderr, "  SCREAM_TX_STREAM_NAME\n");
  fprintf(stderr, "  SCREAM_TX_VERBOSE\n");
}

static int tx_copy_string(char *dst, size_t dst_size, const char *src, const char *field)
{
  size_t len;

  if (dst == NULL || src == NULL || field == NULL) {
    return -1;
  }

  len = strlen(src);
  if (len == 0u || len >= dst_size) {
    fprintf(stderr, "Invalid value for %s: '%s'\n", field, src);
    return -1;
  }

  memcpy(dst, src, len + 1u);
  return 0;
}

static int tx_parse_u32(const char *str, uint32_t min, uint32_t max, uint32_t *out)
{
  char *end = NULL;
  unsigned long value;

  if (str == NULL || out == NULL) {
    return -1;
  }

  errno = 0;
  value = strtoul(str, &end, 10);
  if (errno != 0 || end == str || *end != '\0') {
    return -1;
  }

  if (value < min || value > max) {
    return -1;
  }

  *out = (uint32_t)value;
  return 0;
}

static int tx_parse_sample_size_u32(uint32_t value, uint8_t *out, const char *field)
{
  if (out == NULL || field == NULL) {
    return -1;
  }

  if (value == 16u || value == 24u || value == 32u) {
    *out = (uint8_t)value;
    return 0;
  }

  fprintf(stderr, "Invalid %s: %u (expected 16, 24 or 32)\n", field, value);
  return -1;
}

static int tx_load_numeric_env(
  const char *name,
  uint32_t min,
  uint32_t max,
  uint32_t *target)
{
  const char *value = getenv(name);
  uint32_t parsed = 0;

  if (value == NULL || value[0] == '\0') {
    return 0;
  }

  if (tx_parse_u32(value, min, max, &parsed) != 0) {
    fprintf(stderr, "Invalid value in %s='%s'\n", name, value);
    return -1;
  }

  *target = parsed;
  return 0;
}

static int tx_load_environment(tx_app_config_t *cfg)
{
  const char *value;
  uint32_t parsed = 0;

  if (cfg == NULL) {
    return -1;
  }

  value = getenv("SCREAM_TX_DEST_IP");
  if (value != NULL && value[0] != '\0') {
    if (tx_copy_string(cfg->dest_ip, sizeof(cfg->dest_ip), value, "SCREAM_TX_DEST_IP") != 0) {
      return -1;
    }
  }

  if (tx_load_numeric_env("SCREAM_TX_DEST_PORT", 1u, 65535u, &parsed) != 0) {
    return -1;
  }
  if (parsed != 0u) {
    cfg->dest_port = (uint16_t)parsed;
  }

  parsed = 0u;
  value = getenv("SCREAM_TX_BIND_IP");
  if (value != NULL && value[0] != '\0') {
    if (tx_copy_string(cfg->bind_ip, sizeof(cfg->bind_ip), value, "SCREAM_TX_BIND_IP") != 0) {
      return -1;
    }
  }

  if (tx_load_numeric_env("SCREAM_TX_BIND_PORT", 0u, 65535u, &parsed) != 0) {
    return -1;
  }
  cfg->bind_port = (uint16_t)parsed;

  parsed = 0u;
  if (tx_load_numeric_env("SCREAM_TX_TTL", 0u, 255u, &parsed) != 0) {
    return -1;
  }
  cfg->ttl = (uint8_t)parsed;

  parsed = 0u;
  if (tx_load_numeric_env("SCREAM_TX_DSCP", 0u, 63u, &parsed) != 0) {
    return -1;
  }
  cfg->dscp = (uint8_t)parsed;

  parsed = 0u;
  if (tx_load_numeric_env("SCREAM_TX_SILENCE_THRESHOLD_SAMPLES", 0u, UINT32_MAX, &parsed) != 0) {
    return -1;
  }
  cfg->silence_threshold_samples = parsed;

  parsed = 0u;
  if (tx_load_numeric_env("SCREAM_TX_SAMPLE_RATE", 1u, UINT32_MAX, &parsed) != 0) {
    return -1;
  }
  if (parsed != 0u) {
    cfg->sample_rate = parsed;
  }

  parsed = 0u;
  if (tx_load_numeric_env("SCREAM_TX_SAMPLE_SIZE", 16u, 32u, &parsed) != 0) {
    return -1;
  }
  if (parsed != 0u) {
    if (tx_parse_sample_size_u32(parsed, &cfg->sample_size, "SCREAM_TX_SAMPLE_SIZE") != 0) {
      return -1;
    }
  }

  parsed = 0u;
  if (tx_load_numeric_env("SCREAM_TX_CHANNELS", 1u, 8u, &parsed) != 0) {
    return -1;
  }
  if (parsed != 0u) {
    cfg->channels = (uint8_t)parsed;
  }

  value = getenv("SCREAM_TX_SINK_NAME");
  if (value != NULL && value[0] != '\0') {
    if (tx_copy_string(cfg->sink_name, sizeof(cfg->sink_name), value, "SCREAM_TX_SINK_NAME") != 0) {
      return -1;
    }
  }

  value = getenv("SCREAM_TX_STREAM_NAME");
  if (value != NULL && value[0] != '\0') {
    if (tx_copy_string(cfg->stream_name, sizeof(cfg->stream_name), value, "SCREAM_TX_STREAM_NAME") != 0) {
      return -1;
    }
  }

  parsed = 0u;
  if (tx_load_numeric_env("SCREAM_TX_VERBOSE", 0u, 1u, &parsed) != 0) {
    return -1;
  }
  if (parsed > 0u) {
    cfg->verbose = 1;
  }

  return 0;
}

static void tx_set_default_config(tx_app_config_t *cfg)
{
  memset(cfg, 0, sizeof(*cfg));
  memcpy(cfg->dest_ip, TX_DEFAULT_DEST_IP, sizeof(TX_DEFAULT_DEST_IP));
  cfg->dest_port = TX_DEFAULT_DEST_PORT;
  memcpy(cfg->bind_ip, TX_DEFAULT_BIND_IP, sizeof(TX_DEFAULT_BIND_IP));
  cfg->bind_port = 0u;
  cfg->ttl = 0u;
  cfg->dscp = 0u;
  cfg->silence_threshold_samples = 0u;
  cfg->sample_rate = 48000u;
  cfg->sample_size = 16u;
  cfg->channels = 2u;
  memcpy(cfg->sink_name, "scream_tx_sink", sizeof("scream_tx_sink"));
  memcpy(cfg->stream_name, "Scream TX", sizeof("Scream TX"));
  cfg->verbose = 0;
}

static int tx_parse_cli(int argc, char **argv, tx_app_config_t *cfg)
{
  int opt;
  uint32_t parsed;

  while ((opt = getopt_long(argc, argv, "h", tx_long_options, NULL)) != -1) {
    switch (opt) {
      case OPT_DEST_IP:
        if (tx_copy_string(cfg->dest_ip, sizeof(cfg->dest_ip), optarg, "--dest-ip") != 0) {
          return -1;
        }
        break;
      case OPT_DEST_PORT:
        if (tx_parse_u32(optarg, 1u, 65535u, &parsed) != 0) {
          fprintf(stderr, "Invalid --dest-port: %s\n", optarg);
          return -1;
        }
        cfg->dest_port = (uint16_t)parsed;
        break;
      case OPT_BIND_IP:
        if (tx_copy_string(cfg->bind_ip, sizeof(cfg->bind_ip), optarg, "--bind-ip") != 0) {
          return -1;
        }
        break;
      case OPT_BIND_PORT:
        if (tx_parse_u32(optarg, 0u, 65535u, &parsed) != 0) {
          fprintf(stderr, "Invalid --bind-port: %s\n", optarg);
          return -1;
        }
        cfg->bind_port = (uint16_t)parsed;
        break;
      case OPT_TTL:
        if (tx_parse_u32(optarg, 0u, 255u, &parsed) != 0) {
          fprintf(stderr, "Invalid --ttl: %s\n", optarg);
          return -1;
        }
        cfg->ttl = (uint8_t)parsed;
        break;
      case OPT_DSCP:
        if (tx_parse_u32(optarg, 0u, 63u, &parsed) != 0) {
          fprintf(stderr, "Invalid --dscp: %s\n", optarg);
          return -1;
        }
        cfg->dscp = (uint8_t)parsed;
        break;
      case OPT_SILENCE_THRESHOLD:
        if (tx_parse_u32(optarg, 0u, UINT32_MAX, &parsed) != 0) {
          fprintf(stderr, "Invalid --silence-threshold-samples: %s\n", optarg);
          return -1;
        }
        cfg->silence_threshold_samples = parsed;
        break;
      case OPT_SAMPLE_RATE:
        if (tx_parse_u32(optarg, 1u, UINT32_MAX, &parsed) != 0) {
          fprintf(stderr, "Invalid --sample-rate: %s\n", optarg);
          return -1;
        }
        cfg->sample_rate = parsed;
        break;
      case OPT_SAMPLE_SIZE:
        if (tx_parse_u32(optarg, 16u, 32u, &parsed) != 0) {
          fprintf(stderr, "Invalid --sample-size: %s\n", optarg);
          return -1;
        }
        if (tx_parse_sample_size_u32(parsed, &cfg->sample_size, "--sample-size") != 0) {
          return -1;
        }
        break;
      case OPT_CHANNELS:
        if (tx_parse_u32(optarg, 1u, 8u, &parsed) != 0) {
          fprintf(stderr, "Invalid --channels: %s\n", optarg);
          return -1;
        }
        cfg->channels = (uint8_t)parsed;
        break;
      case OPT_SINK_NAME:
        if (tx_copy_string(cfg->sink_name, sizeof(cfg->sink_name), optarg, "--sink-name") != 0) {
          return -1;
        }
        break;
      case OPT_STREAM_NAME:
        if (tx_copy_string(cfg->stream_name, sizeof(cfg->stream_name), optarg, "--stream-name") != 0) {
          return -1;
        }
        break;
      case OPT_VERBOSE:
        cfg->verbose = 1;
        break;
      case 'h':
        tx_show_usage(argv[0]);
        exit(0);
      default:
        return -1;
    }
  }

  if (optind < argc) {
    fprintf(stderr, "Unexpected extra argument: %s\n", argv[optind]);
    return -1;
  }

  return 0;
}

static int tx_packet_emit(const uint8_t *audio, size_t audio_size, void *userdata)
{
  tx_runtime_t *runtime = (tx_runtime_t *)userdata;
  return tx_packetizer_write(&runtime->packetizer, audio, audio_size);
}

static int tx_on_audio(
  const tx_audio_format_t *format,
  const uint8_t *audio,
  size_t audio_size,
  void *userdata)
{
  tx_runtime_t *runtime = (tx_runtime_t *)userdata;
  uint8_t bytes_per_sample;

  if (format == NULL || audio == NULL || runtime == NULL) {
    return -1;
  }

  if (!runtime->have_format || memcmp(&runtime->current_format, format, sizeof(*format)) != 0) {
    if (tx_packetizer_set_format(&runtime->packetizer, format) != 0) {
      fprintf(stderr, "Failed to apply new audio format.\n");
      return -1;
    }
    runtime->current_format = *format;
    runtime->have_format = 1;
    tx_silence_reset(&runtime->silence);

    if (runtime->verbose > 0) {
      fprintf(stderr,
        "TX format: %u Hz, %u-bit, %u ch, mask=0x%04x\n",
        format->sample_rate,
        format->sample_size,
        format->channels,
        format->channel_map);
    }
  }

  bytes_per_sample = (uint8_t)(format->sample_size / 8u);
  return tx_silence_process(
    &runtime->silence,
    audio,
    audio_size,
    bytes_per_sample,
    format->channels,
    tx_packet_emit,
    runtime);
}

int main(int argc, char **argv)
{
  tx_app_config_t cfg;
  tx_runtime_t runtime;
  tx_udp_config_t udp_cfg;
  tx_pipewire_config_t pw_cfg;
  tx_audio_format_t requested_format;
  int rc;

  tx_set_default_config(&cfg);
  if (tx_load_environment(&cfg) != 0) {
    return 1;
  }
  if (tx_parse_cli(argc, argv, &cfg) != 0) {
    tx_show_usage(argv[0]);
    return 1;
  }

  requested_format.sample_rate = cfg.sample_rate;
  requested_format.sample_size = cfg.sample_size;
  requested_format.channels = cfg.channels;
  requested_format.channel_map = tx_protocol_default_channel_map(cfg.channels);
  if (tx_protocol_validate_format(&requested_format) != 0) {
    fprintf(stderr,
      "Invalid sender format. Supported rates: 44100, 48000, 88200, 96000, 192000 Hz; "
      "sample sizes: 16/24/32; channels: 1..8.\n");
    return 1;
  }

  if (cfg.verbose > 0) {
    fprintf(stderr,
      "Configured TX format: %u Hz, %u-bit, %u ch\n",
      cfg.sample_rate,
      cfg.sample_size,
      cfg.channels);
  }

  memset(&runtime, 0, sizeof(runtime));
  runtime.verbose = cfg.verbose;

  udp_cfg.dest_ip = cfg.dest_ip;
  udp_cfg.dest_port = cfg.dest_port;
  udp_cfg.bind_ip = cfg.bind_ip;
  udp_cfg.bind_port = cfg.bind_port;
  udp_cfg.ttl = cfg.ttl;
  udp_cfg.dscp = cfg.dscp;
  udp_cfg.verbose = cfg.verbose;

  if (tx_udp_init(&runtime.udp, &udp_cfg) != 0) {
    return 1;
  }

  if (tx_packetizer_init(&runtime.packetizer, tx_udp_send_callback, &runtime.udp) != 0) {
    fprintf(stderr, "Failed to initialize packetizer.\n");
    tx_udp_destroy(&runtime.udp);
    return 1;
  }

  tx_silence_init(&runtime.silence, cfg.silence_threshold_samples);

  pw_cfg.sink_name = cfg.sink_name;
  pw_cfg.stream_name = cfg.stream_name;
  pw_cfg.sample_rate = cfg.sample_rate;
  pw_cfg.sample_size = cfg.sample_size;
  pw_cfg.channels = cfg.channels;
  pw_cfg.verbose = cfg.verbose;

  rc = tx_pipewire_run(&pw_cfg, tx_on_audio, &runtime);
  tx_udp_destroy(&runtime.udp);

  if (rc != 0) {
    fprintf(stderr, "scream-tx exited with error.\n");
    return 1;
  }

  return 0;
}
