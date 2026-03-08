cmake_minimum_required(VERSION 3.7...4.0)

project(scream LANGUAGES C)

find_package(PkgConfig REQUIRED)

set(RX_TARGET ${PROJECT_NAME})
add_executable(${RX_TARGET} scream.c network.c shmem.c raw.c)

# find pulseaudio
option(PULSEAUDIO_ENABLE "Enable PulseAudio" ON)
if (PULSEAUDIO_ENABLE)
  pkg_check_modules(PULSEAUDIO libpulse-simple)
  if (PULSEAUDIO_FOUND)
    target_include_directories(${RX_TARGET} PRIVATE ${PULSEAUDIO_INCLUDE_DIRS})
    target_link_directories(${RX_TARGET} PRIVATE ${PULSEAUDIO_LIBRARY_DIRS})
    target_link_libraries(${RX_TARGET} ${PULSEAUDIO_LIBRARIES})
    target_sources(${RX_TARGET} PRIVATE pulseaudio.c)
  else ()
    set(PULSEAUDIO_ENABLE OFF)
  endif ()
endif ()

# find ALSA
option(ALSA_ENABLE "Enable ALSA" ON)
if (ALSA_ENABLE)
  pkg_check_modules(PC_ALSA alsa)
  if (PC_ALSA_FOUND)
    target_include_directories(${RX_TARGET} PRIVATE ${PC_ALSA_INCLUDEDIR})
    target_link_directories(${RX_TARGET} PRIVATE ${PC_ALSA_LIBRARY_DIRS})
    target_link_libraries(${RX_TARGET} ${PC_ALSA_LIBRARIES})
    target_sources(${RX_TARGET} PRIVATE alsa.c)
  else ()
    set(ALSA_ENABLE OFF)
  endif ()
endif ()

# find JACK
option(JACK_ENABLE "Enable JACK" ON)
if (JACK_ENABLE)
  pkg_check_modules(PC_JACK jack)
  pkg_check_modules(PC_LIBSOXR soxr)
  if(PC_JACK_FOUND AND PC_LIBSOXR_FOUND)
    target_include_directories(${RX_TARGET} PRIVATE ${PC_JACK_INCLUDEDIR} ${PC_LIBSOXR_INCLUDEDIR})
    target_link_directories(${RX_TARGET} PRIVATE ${PC_JACK_LIBRARY_DIRS})
    target_link_libraries(${RX_TARGET} ${PC_JACK_LIBRARIES} ${PC_LIBSOXR_LIBRARIES})
    target_sources(${RX_TARGET} PRIVATE jack.c)
  else ()
    set(JACK_ENABLE OFF)
  endif ()
endif ()


# find libpcap
option(PCAP_ENABLE "Enable PCAP" ON)
if (PCAP_ENABLE)
  pkg_check_modules(PC_PCAP libpcap)
  if (PC_PCAP_FOUND)
    target_include_directories(${RX_TARGET} PRIVATE ${PC_PCAP_INCLUDEDIR})
    target_link_directories(${RX_TARGET} PRIVATE ${PC_PCAP_LIBRARY_DIRS})
    target_link_libraries(${RX_TARGET} ${PC_PCAP_LIBRARIES})
    target_sources(${RX_TARGET} PRIVATE pcap.c)
  else ()
    set(PCAP_ENABLE OFF)
  endif ()
endif ()

option(SNDIO_ENABLE "Enable sndio" ON)
if (SNDIO_ENABLE)
  pkg_check_modules(PC_SNDIO sndio)
  if (PC_SNDIO_FOUND)
    target_include_directories(${RX_TARGET} PRIVATE ${PC_SNDIO_INCLUDEDIR})
    target_link_directories(${RX_TARGET} PRIVATE ${PC_SNDIO_LIBRARY_DIRS})
    target_link_libraries(${RX_TARGET} ${PC_SNDIO_LIBRARIES})
    target_sources(${RX_TARGET} PRIVATE sndio.c)
  else ()
    set(SNDIO_ENABLE OFF)
  endif ()
endif ()

option(SCREAM_TX_ENABLE "Build Linux sender scream-tx (PipeWire native)" ON)
if (SCREAM_TX_ENABLE)
  pkg_check_modules(PC_PIPEWIRE REQUIRED libpipewire-0.3)

  add_executable(scream-tx
    scream-tx.c
    tx_protocol.c
    tx_packetizer.c
    tx_silence.c
    tx_udp.c
    tx_pipewire.c)
  target_include_directories(scream-tx PRIVATE ${PC_PIPEWIRE_INCLUDE_DIRS} "${PROJECT_BINARY_DIR}")
  target_link_directories(scream-tx PRIVATE ${PC_PIPEWIRE_LIBRARY_DIRS})
  target_link_libraries(scream-tx ${PC_PIPEWIRE_LIBRARIES} m)
endif ()

configure_file(config.h.in config.h)
target_include_directories(${RX_TARGET} PUBLIC "${PROJECT_BINARY_DIR}")

include(GNUInstallDirs)
install(TARGETS ${RX_TARGET} RUNTIME DESTINATION "${CMAKE_INSTALL_BINDIR}")
if (SCREAM_TX_ENABLE)
  install(TARGETS scream-tx RUNTIME DESTINATION "${CMAKE_INSTALL_BINDIR}")
  install(FILES systemd/scream-tx.service DESTINATION "${CMAKE_INSTALL_DATAROOTDIR}/systemd/user")
endif ()

include(CTest)
if (BUILD_TESTING AND SCREAM_TX_ENABLE)
  add_executable(scream-tx-tests
    tests_tx.c
    tx_protocol.c
    tx_packetizer.c
    tx_silence.c
    tx_udp.c)
  target_include_directories(scream-tx-tests PRIVATE "${PROJECT_BINARY_DIR}")
  target_link_libraries(scream-tx-tests m)
  add_test(NAME scream-tx-tests COMMAND scream-tx-tests)
endif ()
