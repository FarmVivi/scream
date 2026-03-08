with import <nixpkgs> {};
stdenv.mkDerivation {
    name = "dev-environment";
    buildInputs = [ pkgconfig libpcap alsaLib pulseaudio pipewire soxr cmake ];
}
