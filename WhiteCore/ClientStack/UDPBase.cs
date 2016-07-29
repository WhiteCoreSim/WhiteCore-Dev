/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Net.Sockets;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.ClientStack
{
    /// <summary>
    ///     Base UDP server
    /// </summary>
    public abstract class UDPBase
    {
        /// <summary>
        ///     Flag to process packets asynchronously or synchronously
        /// </summary>
        bool m_asyncPacketHandling;

        /// <summary>
        ///     Local IP address to bind to in server mode
        /// </summary>
        protected IPAddress m_localBindAddress;

        /// <summary>
        ///     The all important shutdown flag
        /// </summary>
        volatile bool m_shutdownFlag = true;

        /// <summary>
        ///     UDP port to bind to in server mode
        /// </summary>
        protected int m_udpPort;

        /// <summary>
        ///     UDP socket, used in either client or server mode
        /// </summary>
        Socket m_udpSocket;

        /// <summary>
        ///     Returns true if the server is currently listening, otherwise false
        /// </summary>
        public bool IsRunning
        {
            get { return !m_shutdownFlag; }
        }

        /// <summary>
        ///     This method is called when an incoming packet is received
        /// </summary>
        /// <param name="buffer">Incoming packet buffer</param>
        protected abstract void PacketReceived(UDPPacketBuffer buffer);

        /// <summary>
        ///     Default Initialization
        /// </summary>
        /// <param name="bindAddress">Local IP address to bind the server to</param>
        /// <param name="port">Port to listening for incoming UDP packets on</param>
        public virtual void Initialise(IPAddress bindAddress, int port)
        {
            m_localBindAddress = bindAddress;
            m_udpPort = port;
        }

        /// <summary>
        ///     Start the UDP server
        /// </summary>
        /// <param name="recvBufferSize">
        ///     The size of the receive buffer for
        ///     the UDP socket. This value is passed up to the operating system
        ///     and used in the system networking stack. Use zero to leave this
        ///     value as the default
        /// </param>
        /// <param name="asyncPacketHandling">
        ///     Set this to true to start
        ///     receiving more packets while current packet handler callbacks are
        ///     still running. Setting this to false will complete each packet
        ///     callback before the next packet is processed
        /// </param>
        /// <remarks>
        ///     This method will attempt to set the SIO_UDP_CONNRESET flag
        ///     on the socket to get newer versions of Windows to behave in a sane
        ///     manner (not throwing an exception when the remote side resets the
        ///     connection). This call is ignored on Mono where the flag is not
        ///     necessary
        /// </remarks>
        public void Start(int recvBufferSize, bool asyncPacketHandling)
        {
            m_asyncPacketHandling = asyncPacketHandling;

            if (m_shutdownFlag)
            {
                const int SIO_UDP_CONNRESET = -1744830452;

                IPEndPoint ipep = new IPEndPoint(m_localBindAddress, m_udpPort);

                m_udpSocket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Dgram,
                    ProtocolType.Udp);

                try
                {
                    // This UDP socket flag is not supported under mono, 
                    // so we'll catch the exception and continue
                    if(Util.IsWindows())
                        m_udpSocket.IOControl(SIO_UDP_CONNRESET, new byte[] {0}, null);
                    //MainConsole.Instance.Debug("[UDPBASE]: SIO_UDP_CONNRESET flag set");
                }
                catch (SocketException)
                {
                    //MainConsole.Instance.Debug("[UDPBASE]: SIO_UDP_CONNRESET flag not supported on this platform, ignoring");
                }

                if (recvBufferSize != 0)
                    m_udpSocket.ReceiveBufferSize = recvBufferSize;

                m_udpSocket.Bind(ipep);

                // we're not shutting down, we're starting up
                m_shutdownFlag = false;

                // kick off an async receive.  The Start() method will return, the
                // actual receives will occur asynchronously and will be caught in
                // AsyncEndRecieve().
                AsyncBeginReceive();
            }
        }

        /// <summary>
        ///     Stops the UDP server
        /// </summary>
        public void Stop()
        {
            if (!m_shutdownFlag)
            {
                // wait indefinitely for a writer lock.  Once this is called, the .NET runtime
                // will deny any more reader locks, in effect blocking all other send/receive
                // threads.  Once we have the lock, we set shutdownFlag to inform the other
                // threads that the socket is closed.
                m_shutdownFlag = true;
                m_udpSocket.Close();
            }
        }

        void AsyncBeginReceive()
        {
            // allocate a packet buffer
            //WrappedObject<UDPPacketBuffer> wrappedBuffer = Pool.CheckOut();
            UDPPacketBuffer buf = new UDPPacketBuffer();

            if (!m_shutdownFlag)
            {
                try
                {
                    // kick off an async read
                    m_udpSocket.BeginReceiveFrom(
                        //wrappedBuffer.Instance.Data,
                        buf.Data,
                        0,
                        UDPPacketBuffer.BUFFER_SIZE,
                        SocketFlags.None,
                        ref buf.RemoteEndPoint,
                        AsyncEndReceive,
                        //wrappedBuffer);
                        buf);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        MainConsole.Instance.Warn(
                            "[UDPBASE]: SIO_UDP_CONNRESET was ignored, attempting to salvage the UDP listener on port " +
                            m_udpPort);
                        bool salvaged = false;
                        while (!salvaged)
                        {
                            try
                            {
                                m_udpSocket.BeginReceiveFrom(
                                    //wrappedBuffer.Instance.Data,
                                    buf.Data,
                                    0,
                                    UDPPacketBuffer.BUFFER_SIZE,
                                    SocketFlags.None,
                                    ref buf.RemoteEndPoint,
                                    AsyncEndReceive,
                                    //wrappedBuffer);
                                    buf);
                                salvaged = true;
                            }
                            catch (SocketException)
                            {
                            }
                            catch (ObjectDisposedException)
                            {
                                return;
                            }
                        }

                        MainConsole.Instance.Warn("[UDPBASE]: Salvaged the UDP listener on port " + m_udpPort);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        void AsyncEndReceive(IAsyncResult iar)
        {
            // Asynchronous receive operations will complete here through the call
            // to AsyncBeginReceive
            if (!m_shutdownFlag)
            {
                // Asynchronous mode will start another receive before the
                // callback for this packet is even fired. Very parallel :-)
                if (m_asyncPacketHandling)
                    AsyncBeginReceive();

                // get the buffer that was created in AsyncBeginReceive
                // this is the received data
                //WrappedObject<UDPPacketBuffer> wrappedBuffer = (WrappedObject<UDPPacketBuffer>)iar.AsyncState;
                //UDPPacketBuffer buffer = wrappedBuffer.Instance;
                UDPPacketBuffer buffer = (UDPPacketBuffer) iar.AsyncState;

                try
                {
                    // get the length of data actually read from the socket, store it with the
                    // buffer
                    buffer.DataLength = m_udpSocket.EndReceiveFrom(iar, ref buffer.RemoteEndPoint);

                    // call the abstract method PacketReceived(), passing the buffer that
                    // has just been filled from the socket read.
                    PacketReceived(buffer);
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.Error("[UDPBase]: Hit error: " + ex);
                }
                finally
                {
                    //wrappedBuffer.Dispose();

                    // Synchronous mode waits until the packet callback completes
                    // before starting the receive to fetch another packet
                    if (!m_asyncPacketHandling)
                        AsyncBeginReceive();
                }
            }
        }

        public void SyncSend(UDPPacketBuffer buf)
        {
            if (!m_shutdownFlag)
            {
                try {
                    // well not async but blocking 
                    m_udpSocket.SendTo (
                        buf.Data,
                        0,
                        buf.DataLength,
                        SocketFlags.None,
                        buf.RemoteEndPoint);
                } catch (SocketException) {
                } catch (ObjectDisposedException) {
                } catch (Exception) {
                }
            }
        }
    }
}