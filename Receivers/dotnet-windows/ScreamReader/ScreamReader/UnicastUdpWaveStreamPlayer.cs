using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Remoting.Channels;

namespace ScreamReader
{
    internal class UnicastUdpWaveStreamPlayer : UdpWaveStreamPlayer
    {
        #region Instance Variables
        /// <summary>
        /// The port to listen to.
        /// </summary>
        protected int unicastPort { get; set; }

        /// <summary>
        /// The local IP address to bind to (optional).
        /// </summary>
        protected IPAddress localAddress { get; set; }
        #endregion

        /// <summary>
        /// Initialize the client with the specific port and optional IP address.
        /// </summary>
        public UnicastUdpWaveStreamPlayer(int bitWidth, int rate, int channels, int unicastPort, IPAddress localAddress = null) : base(bitWidth, rate, channels)
        {
            this.unicastPort = unicastPort;
            this.localAddress = localAddress ?? IPAddress.Any; // Default to any local address
        }

        /// <summary>
        /// Initialize the client with audio optimization parameters.
        /// </summary>
        public UnicastUdpWaveStreamPlayer(int bitWidth, int rate, int channels, int unicastPort, IPAddress localAddress, int bufferDuration, int wasapiLatency, bool useExclusiveMode) : base(bitWidth, rate, channels, bufferDuration, wasapiLatency, useExclusiveMode)
        {
            this.unicastPort = unicastPort;
            this.localAddress = localAddress ?? IPAddress.Any; // Default to any local address
        }

        /// <summary>
        /// Configure UDP client for unicast communication.
        /// </summary>
        protected override void ConfigureUdpClient(UdpClient udpClient, IPEndPoint localEp)
        {
            localEp = new IPEndPoint(this.localAddress, this.unicastPort);

            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(localEp);

            Console.WriteLine($"Listening for unicast packets on {localEp.Address}:{localEp.Port}");
        }
    }
}
