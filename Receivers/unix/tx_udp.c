#include "tx_udp.h"

#include <arpa/inet.h>
#include <errno.h>
#include <stdio.h>
#include <string.h>
#include <sys/socket.h>
#include <unistd.h>

static int tx_udp_set_sockopts(tx_udp_context_t *ctx, const tx_udp_config_t *cfg)
{
  int reuse = 1;
  int tos = 0;
  int ttl = 0;

  if (setsockopt(ctx->sockfd, SOL_SOCKET, SO_REUSEADDR, &reuse, sizeof(reuse)) != 0) {
    perror("setsockopt(SO_REUSEADDR) failed");
    return -1;
  }

  if (cfg->dscp > 0u) {
    tos = ((int)(cfg->dscp & 0x3Fu)) << 2;
    if (setsockopt(ctx->sockfd, IPPROTO_IP, IP_TOS, &tos, sizeof(tos)) != 0) {
      perror("setsockopt(IP_TOS) failed");
      return -1;
    }
  }

  if (cfg->ttl > 0u) {
    ttl = (int)cfg->ttl;
    if (ctx->is_multicast) {
      if (setsockopt(ctx->sockfd, IPPROTO_IP, IP_MULTICAST_TTL, &ttl, sizeof(ttl)) != 0) {
        perror("setsockopt(IP_MULTICAST_TTL) failed");
        return -1;
      }
    } else {
      if (setsockopt(ctx->sockfd, IPPROTO_IP, IP_TTL, &ttl, sizeof(ttl)) != 0) {
        perror("setsockopt(IP_TTL) failed");
        return -1;
      }
    }
  }

  return 0;
}

int tx_udp_init(tx_udp_context_t *ctx, const tx_udp_config_t *cfg)
{
  struct sockaddr_in bind_addr;
  struct in_addr in = { 0 };

  if (ctx == NULL || cfg == NULL || cfg->dest_ip == NULL || cfg->bind_ip == NULL) {
    return -1;
  }

  memset(ctx, 0, sizeof(*ctx));
  ctx->sockfd = -1;

  ctx->sockfd = socket(AF_INET, SOCK_DGRAM, 0);
  if (ctx->sockfd < 0) {
    perror("socket(AF_INET, SOCK_DGRAM) failed");
    return -1;
  }

  memset(&ctx->dest_addr, 0, sizeof(ctx->dest_addr));
  ctx->dest_addr.sin_family = AF_INET;
  ctx->dest_addr.sin_port = htons(cfg->dest_port);
  if (inet_pton(AF_INET, cfg->dest_ip, &ctx->dest_addr.sin_addr) != 1) {
    fprintf(stderr, "Invalid destination IPv4 address: %s\n", cfg->dest_ip);
    tx_udp_destroy(ctx);
    return -1;
  }

  ctx->is_multicast = IN_MULTICAST(ntohl(ctx->dest_addr.sin_addr.s_addr)) ? 1 : 0;

  memset(&bind_addr, 0, sizeof(bind_addr));
  bind_addr.sin_family = AF_INET;
  bind_addr.sin_port = htons(cfg->bind_port);
  if (inet_pton(AF_INET, cfg->bind_ip, &in) != 1) {
    fprintf(stderr, "Invalid bind IPv4 address: %s\n", cfg->bind_ip);
    tx_udp_destroy(ctx);
    return -1;
  }
  bind_addr.sin_addr = in;

  if (tx_udp_set_sockopts(ctx, cfg) != 0) {
    tx_udp_destroy(ctx);
    return -1;
  }

  if (bind(ctx->sockfd, (const struct sockaddr *)&bind_addr, sizeof(bind_addr)) != 0) {
    perror("bind() failed");
    tx_udp_destroy(ctx);
    return -1;
  }

  if (cfg->verbose > 0) {
    fprintf(stderr, "UDP sender ready: %s:%u (bind %s:%u)\n",
      cfg->dest_ip,
      (unsigned int)cfg->dest_port,
      cfg->bind_ip,
      (unsigned int)cfg->bind_port);
  }

  return 0;
}

void tx_udp_destroy(tx_udp_context_t *ctx)
{
  if (ctx == NULL) {
    return;
  }

  if (ctx->sockfd >= 0) {
    close(ctx->sockfd);
    ctx->sockfd = -1;
  }
}

int tx_udp_send(tx_udp_context_t *ctx, const uint8_t *packet, size_t len)
{
  ssize_t sent;

  if (ctx == NULL || packet == NULL || len == 0u) {
    return -1;
  }

  do {
    sent = sendto(
      ctx->sockfd,
      packet,
      len,
      0,
      (const struct sockaddr *)&ctx->dest_addr,
      sizeof(ctx->dest_addr));
  } while (sent < 0 && errno == EINTR);

  if (sent < 0) {
    perror("sendto() failed");
    return -1;
  }

  if ((size_t)sent != len) {
    fprintf(stderr, "sendto() short write: %zd/%zu\n", sent, len);
    return -1;
  }

  return 0;
}

int tx_udp_send_callback(const uint8_t *packet, size_t len, void *userdata)
{
  return tx_udp_send((tx_udp_context_t *)userdata, packet, len);
}
