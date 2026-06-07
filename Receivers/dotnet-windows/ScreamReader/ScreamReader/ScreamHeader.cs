using NAudio.Wave;

namespace ScreamReader
{
    /// <summary>
    /// The 5-byte header prepended to every Scream UDP packet, describing the PCM payload.
    /// Layout: [0] sample-rate code, [1] bit depth, [2] channel count, [3..4] channel mask.
    /// </summary>
    internal readonly struct ScreamHeader
    {
        /// <summary>Size of the header in bytes.</summary>
        public const int Size = 5;

        public readonly byte RateCode;
        public readonly byte BitDepth;
        public readonly byte Channels;
        public readonly byte ChannelMaskLsb;
        public readonly byte ChannelMaskMsb;

        public ScreamHeader(byte rateCode, byte bitDepth, byte channels, byte maskLsb, byte maskMsb)
        {
            RateCode = rateCode;
            BitDepth = bitDepth;
            Channels = channels;
            ChannelMaskLsb = maskLsb;
            ChannelMaskMsb = maskMsb;
        }

        /// <summary>Reads the header from the first <see cref="Size"/> bytes of a packet.</summary>
        public static ScreamHeader Parse(byte[] packet)
        {
            return new ScreamHeader(packet[0], packet[1], packet[2], packet[3], packet[4]);
        }

        /// <summary>
        /// Sample rate in Hz. The high bit of the rate code selects the base (44.1 kHz vs 48 kHz);
        /// the low 7 bits are the multiplier. Returns 0 for an unset/invalid code.
        /// </summary>
        public int SampleRate => ((RateCode & 0x80) != 0 ? 44100 : 48000) * (RateCode & 0x7F);

        /// <summary>True when this header describes a different PCM format than <paramref name="other"/>.</summary>
        public bool FormatDiffers(ScreamHeader other)
        {
            return RateCode != other.RateCode
                || BitDepth != other.BitDepth
                || Channels != other.Channels
                || ChannelMaskLsb != other.ChannelMaskLsb
                || ChannelMaskMsb != other.ChannelMaskMsb;
        }

        /// <summary>
        /// Builds the matching NAudio <see cref="WaveFormat"/>, falling back to 48 kHz/16-bit/stereo
        /// for any field that is unset. NAudio cannot apply the channel mask, so it is ignored.
        /// </summary>
        public WaveFormat ToWaveFormat()
        {
            int rate = SampleRate > 0 ? SampleRate : 48000;
            int bits = BitDepth > 0 ? BitDepth : 16;
            int channels = Channels > 0 ? Channels : 2;
            return new WaveFormat(rate, bits, channels);
        }

        public override string ToString() => $"{SampleRate} Hz, {BitDepth}-bit, {Channels} ch";
    }
}
