# scream (Unix)

This folder now provides:

- `scream`: Linux/Unix receiver for Scream network audio streams.
- `scream-tx`: Linux sender (PipeWire native) that captures audio and sends it on the network using Scream UDP packets compatible with Windows clients.

## Build

Compilation is done using CMake.

```shell
$ mkdir build && cd build
$ cmake ..
$ make
```

The receiver (`scream`) enables optional outputs when headers are available.
The sender (`scream-tx`) is enabled by default and requires `libpipewire-0.3`.

### Dependency hints

PulseAudio:

```shell
$ sudo yum install pulseaudio-libs-devel   # Redhat, CentOS, etc.
or
$ sudo apt-get install libpulse-dev        # Debian, Ubuntu, etc.
```

ALSA:

```shell
$ sudo yum install alsa-lib-devel          # Redhat, CentOS, etc.
or
$ sudo apt-get install libasound2-dev      # Debian, Ubuntu, etc.
```

PipeWire (for `scream-tx`):

```shell
$ sudo yum install pipewire-devel          # Redhat, CentOS, etc.
or
$ sudo apt-get install libpipewire-0.3-dev # Debian, Ubuntu, etc.
```

## Receiver usage (`scream`)

You can see the accepted options with:

```shell
$ scream -h
```

### Network mode

```shell
$ scream
```

This starts multicast receive mode using default output.
Unicast mode is also supported:

```shell
$ scream -u -i eth0
```

### libpcap mode

Useful when packets are visible in `wireshark`/`tcpdump` but not delivered to the socket:

```shell
$ scream -P -i macvtap0
```

If you need to run as non-root while sniffing:

```shell
# setcap cap_net_raw,cap_net_admin=eip ./scream
```

### IVSHMEM mode

```shell
$ scream -m /dev/shm/scream-ivshmem
```

### ALSA output tips

If underruns occur, increase target latency:

```shell
$ scream -o alsa -t 100
```

Run with `-v` to dump ALSA setup.
Run with `env LIBASOUND_DEBUG=1` for ALSA diagnostics.

## Sender usage (`scream-tx`)

`scream-tx` creates a dedicated virtual PipeWire sink and captures from it.
It does not change the system default output automatically.

Show options:

```shell
$ scream-tx --help
```

Default multicast sender:

```shell
$ scream-tx --verbose
```

Default sender format is stereo, 16-bit, 48kHz.

Unicast sender example:

```shell
$ scream-tx --dest-ip 192.168.1.40 --dest-port 4010 --sink-name scream_tx_sink --stream-name "Scream TX"
```

Advanced example with source bind, TTL/DSCP and silence suppression:

```shell
$ scream-tx \
  --dest-ip 239.255.77.77 \
  --dest-port 4010 \
  --bind-ip 0.0.0.0 \
  --bind-port 0 \
  --ttl 1 \
  --dscp 46 \
  --silence-threshold-samples 10000 \
  --sample-rate 48000 \
  --sample-size 16 \
  --channels 2 \
  --sink-name scream_tx_sink \
  --stream-name "Scream TX" \
  --verbose
```

### Environment variables

Every sender CLI option has an env equivalent:

- `SCREAM_TX_DEST_IP`
- `SCREAM_TX_DEST_PORT`
- `SCREAM_TX_BIND_IP`
- `SCREAM_TX_BIND_PORT`
- `SCREAM_TX_TTL`
- `SCREAM_TX_DSCP`
- `SCREAM_TX_SILENCE_THRESHOLD_SAMPLES`
- `SCREAM_TX_SAMPLE_RATE`
- `SCREAM_TX_SAMPLE_SIZE`
- `SCREAM_TX_CHANNELS`
- `SCREAM_TX_SINK_NAME`
- `SCREAM_TX_STREAM_NAME`
- `SCREAM_TX_VERBOSE`

Priority is: CLI option > environment variable > built-in default.

## systemd user service

A template unit is provided at `systemd/scream-tx.service`.

Example:

```shell
$ install -Dm644 systemd/scream-tx.service ~/.config/systemd/user/scream-tx.service
$ systemctl --user daemon-reload
$ systemctl --user enable --now scream-tx.service
```

Then customize `Environment=` lines inside the unit for your destination host/group and sender settings.
