#ifndef TX_UDP_H
#define TX_UDP_H

#include <stdint.h>
#include <stddef.h>
#include <netinet/in.h>

typedef struct tx_udp_config {
  const char *dest_ip;
  uint16_t dest_port;
  const char *bind_ip;
  uint16_t bind_port;
  uint8_t ttl;
  uint8_t dscp;
  int verbose;
} tx_udp_config_t;

typedef struct tx_udp_context {
  int sockfd;
  struct sockaddr_in dest_addr;
  int is_multicast;
} tx_udp_context_t;

int tx_udp_init(tx_udp_context_t *ctx, const tx_udp_config_t *cfg);
void tx_udp_destroy(tx_udp_context_t *ctx);
int tx_udp_send(tx_udp_context_t *ctx, const uint8_t *packet, size_t len);
int tx_udp_send_callback(const uint8_t *packet, size_t len, void *userdata);

#endif
