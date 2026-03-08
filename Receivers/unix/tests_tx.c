#include <arpa/inet.h>
#include <errno.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/time.h>
#include <unistd.h>

#include "tx_packetizer.h"
#include "tx_protocol.h"
#include "tx_silence.h"
#include "tx_udp.h"

#define TEST_ASSERT(cond, msg)                   \
  do {                                           \
    if (!(cond)) {                               \
      fprintf(stderr, "FAIL: %s\n", (msg));      \
      return 1;                                  \
    }                                            \
  } while (0)

static int test_protocol_marker(void)
{
  uint8_t marker = 0;

  TEST_ASSERT(tx_protocol_encode_sample_rate_marker(44100, &marker) == 0, "44100 marker");
  TEST_ASSERT(marker == 129, "44100 marker value");
  TEST_ASSERT(tx_protocol_encode_sample_rate_marker(48000, &marker) == 0, "48000 marker");
  TEST_ASSERT(marker == 1, "48000 marker value");
  TEST_ASSERT(tx_protocol_encode_sample_rate_marker(88200, &marker) == 0, "88200 marker");
  TEST_ASSERT(marker == 130, "88200 marker value");
  TEST_ASSERT(tx_protocol_encode_sample_rate_marker(96000, &marker) == 0, "96000 marker");
  TEST_ASSERT(marker == 2, "96000 marker value");
  TEST_ASSERT(tx_protocol_encode_sample_rate_marker(192000, &marker) == 0, "192000 marker");
  TEST_ASSERT(marker == 4, "192000 marker value");
  TEST_ASSERT(tx_protocol_encode_sample_rate_marker(176400, &marker) != 0, "176400 should be rejected");
  return 0;
}

static int test_protocol_header(void)
{
  tx_audio_format_t format = {
    .sample_rate = 48000,
    .sample_size = 16,
    .channels = 2,
    .channel_map = 0x0003
  };
  uint8_t header[TX_HEADER_SIZE];

  TEST_ASSERT(tx_protocol_build_header(&format, header) == 0, "header build");
  TEST_ASSERT(header[0] == 1, "header rate marker");
  TEST_ASSERT(header[1] == 16, "header bits");
  TEST_ASSERT(header[2] == 2, "header channels");
  TEST_ASSERT(header[3] == 0x03, "header map lsb");
  TEST_ASSERT(header[4] == 0x00, "header map msb");
  return 0;
}

typedef struct packet_counter {
  int packets;
  uint8_t first_header[TX_HEADER_SIZE];
} packet_counter_t;

static int test_packet_send_cb(const uint8_t *packet, size_t len, void *userdata)
{
  packet_counter_t *counter = (packet_counter_t *)userdata;
  if (counter->packets == 0) {
    memcpy(counter->first_header, packet, TX_HEADER_SIZE);
  }
  if (len != TX_PACKET_SIZE) {
    return -1;
  }
  counter->packets++;
  return 0;
}

static int test_packetizer(void)
{
  tx_packetizer_t packetizer;
  packet_counter_t counter;
  tx_audio_format_t format = {
    .sample_rate = 48000,
    .sample_size = 16,
    .channels = 2,
    .channel_map = 0x0003
  };
  uint8_t data_a[1200];
  uint8_t data_b[1104];

  memset(&counter, 0, sizeof(counter));
  memset(data_a, 0xAB, sizeof(data_a));
  memset(data_b, 0xCD, sizeof(data_b));

  TEST_ASSERT(tx_packetizer_init(&packetizer, test_packet_send_cb, &counter) == 0, "packetizer init");
  TEST_ASSERT(tx_packetizer_set_format(&packetizer, &format) == 0, "packetizer format");
  TEST_ASSERT(tx_packetizer_write(&packetizer, data_a, sizeof(data_a)) == 0, "packetizer write A");
  TEST_ASSERT(counter.packets == 1, "packetizer expected one packet");
  TEST_ASSERT(tx_packetizer_write(&packetizer, data_b, sizeof(data_b)) == 0, "packetizer write B");
  TEST_ASSERT(counter.packets == 2, "packetizer expected two packets");
  TEST_ASSERT(counter.first_header[0] == 1, "packetizer header marker");
  TEST_ASSERT(counter.first_header[1] == 16, "packetizer header bits");
  TEST_ASSERT(counter.first_header[2] == 2, "packetizer header channels");
  return 0;
}

typedef struct silence_capture {
  uint8_t bytes[256];
  size_t size;
} silence_capture_t;

static int test_silence_emit_cb(const uint8_t *data, size_t size, void *userdata)
{
  silence_capture_t *capture = (silence_capture_t *)userdata;
  if ((capture->size + size) > sizeof(capture->bytes)) {
    return -1;
  }
  memcpy(capture->bytes + capture->size, data, size);
  capture->size += size;
  return 0;
}

static int test_silence_gate(void)
{
  tx_silence_gate_t gate;
  silence_capture_t capture;
  int16_t samples[] = { 0, 0, 0, 0, 1000, 1000 };

  memset(&capture, 0, sizeof(capture));
  tx_silence_init(&gate, 2u);

  TEST_ASSERT(
    tx_silence_process(
      &gate,
      (const uint8_t *)samples,
      sizeof(samples),
      2u,
      1u,
      test_silence_emit_cb,
      &capture) == 0,
    "silence process");

  /* 2 initial silent samples + 2 non-silent samples emitted, middle silence removed */
  TEST_ASSERT(capture.size == 8u, "silence emitted size");
  return 0;
}

static int test_udp_unicast_loopback(void)
{
  int recvfd = -1;
  struct sockaddr_in recv_addr;
  socklen_t recv_len = sizeof(recv_addr);
  struct timeval tv;
  tx_udp_context_t sender;
  tx_udp_config_t cfg;
  uint8_t packet[TX_PACKET_SIZE];
  uint8_t recv_buf[TX_PACKET_SIZE];
  ssize_t recv_size;

  memset(&sender, 0, sizeof(sender));
  sender.sockfd = -1;

  recvfd = socket(AF_INET, SOCK_DGRAM, 0);
  if (recvfd < 0) {
    if (errno == EPERM || errno == EACCES || errno == EAFNOSUPPORT) {
      printf("SKIP: UDP loopback test (socket unavailable in this environment)\n");
      return 0;
    }
    TEST_ASSERT(0, "create recv socket");
  }

  memset(&recv_addr, 0, sizeof(recv_addr));
  recv_addr.sin_family = AF_INET;
  recv_addr.sin_port = 0;
  recv_addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
  TEST_ASSERT(bind(recvfd, (struct sockaddr *)&recv_addr, sizeof(recv_addr)) == 0, "bind recv socket");
  TEST_ASSERT(getsockname(recvfd, (struct sockaddr *)&recv_addr, &recv_len) == 0, "getsockname recv socket");

  tv.tv_sec = 1;
  tv.tv_usec = 0;
  TEST_ASSERT(setsockopt(recvfd, SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof(tv)) == 0, "set recv timeout");

  memset(&cfg, 0, sizeof(cfg));
  cfg.dest_ip = "127.0.0.1";
  cfg.dest_port = ntohs(recv_addr.sin_port);
  cfg.bind_ip = "0.0.0.0";
  cfg.bind_port = 0;
  cfg.ttl = 0;
  cfg.dscp = 0;
  cfg.verbose = 0;

  TEST_ASSERT(tx_udp_init(&sender, &cfg) == 0, "tx_udp_init loopback");

  for (size_t i = 0; i < sizeof(packet); ++i) {
    packet[i] = (uint8_t)i;
  }

  TEST_ASSERT(tx_udp_send(&sender, packet, sizeof(packet)) == 0, "tx_udp_send loopback");

  recv_size = recvfrom(recvfd, recv_buf, sizeof(recv_buf), 0, NULL, NULL);
  TEST_ASSERT(recv_size == (ssize_t)sizeof(packet), "recvfrom size");
  TEST_ASSERT(memcmp(packet, recv_buf, sizeof(packet)) == 0, "recvfrom payload");

  tx_udp_destroy(&sender);
  close(recvfd);
  return 0;
}

int main(void)
{
  int rc = 0;

  rc |= test_protocol_marker();
  rc |= test_protocol_header();
  rc |= test_packetizer();
  rc |= test_silence_gate();
  rc |= test_udp_unicast_loopback();

  if (rc != 0) {
    fprintf(stderr, "scream-tx tests failed\n");
    return 1;
  }

  printf("scream-tx tests passed\n");
  return 0;
}
