using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ScreamReader
{
    internal class MulticastUdpWaveStreamPlayer : UdpWaveStreamPlayer
    {
        #region instance variables
        /// <summary>
        /// The <see cref="IPAddress"/> in use.
        /// </summary>
        protected IPAddress multicastAddress { get; set; }

        /// <summary>
        /// The port to listen to.
        /// </summary>
        protected int multicastPort { get; set; }
        #endregion

        /// <summary>
        /// Initialize the client with the specific address, port and format.
        /// </summary>
        public MulticastUdpWaveStreamPlayer(IPAddress multicastAddress, int multicastPort) : base()
        {
            this.multicastAddress = multicastAddress;
            this.multicastPort = multicastPort;
        }

        /// <summary>
        /// Configure UDP client.
        /// </summary>
        protected override void ConfigureUdpClient(UdpClient udpClient, IPEndPoint localEp)
        {
            localEp = new IPEndPoint(IPAddress.Any, this.multicastPort);

            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(localEp);
            udpClient.JoinMulticastGroup(this.multicastAddress);
        }
    }
}
