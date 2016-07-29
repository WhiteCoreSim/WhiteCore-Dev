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
using OpenMetaverse;
using OpenMetaverse.Imaging;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;

namespace WhiteCore.ClientStack
{
    /// <summary>
    ///     Stores information about a current texture download and a reference to the texture asset
    /// </summary>
    public class J2KImage
    {
        const int IMAGE_PACKET_SIZE = 1000;
        const int FIRST_PACKET_SIZE = 600;

        byte [] m_asset;
        bool m_assetRequested;
        uint m_currentPacket;
        bool m_decodeRequested;
        bool m_sentInfo;
        uint m_stopPacket;

        public UUID AgentID;
        public IAssetService AssetService;
        public sbyte DiscardLevel;
        public bool HasAsset;
        public IInventoryAccessModule InventoryAccessModule;
        public bool IsDecoded;
        public IJ2KDecoder J2KDecoder;
        public uint LastSequence;
        public OpenJPEG.J2KLayerInfo[] Layers;
        public float Priority;
        public uint StartPacket;
        public UUID TextureID;

        public void Dispose()
        {
            m_asset = null;
        }

        /// <summary>
        ///     Sends packets for this texture to a client until packetsToSend is
        ///     hit or the transfer completes
        /// </summary>
        /// <param name="client">Reference to the client that the packets are destined for</param>
        /// <param name="packetsToSend">Maximum number of packets to send during this call</param>
        /// <param name="packetsSent">Number of packets sent during this call</param>
        /// <returns>True if the transfer completes at the current discard level, otherwise false</returns>
        public bool SendPackets(LLClientView client, int packetsToSend, out int packetsSent)
        {
            packetsSent = 0;

            if (m_currentPacket <= m_stopPacket)
            {
                bool sendMore = true;

                if (!m_sentInfo || (m_currentPacket == 0))
                {
                    sendMore = !SendFirstPacket(client);

                    m_sentInfo = true;
                    ++m_currentPacket;
                    ++packetsSent;
                }
                if (m_currentPacket < 2)
                {
                    m_currentPacket = 2;
                }

                while (sendMore && packetsSent < packetsToSend && m_currentPacket <= m_stopPacket)
                {
                    sendMore = SendPacket(client);
                    ++m_currentPacket;
                    ++packetsSent;
                }
            }

            return (m_currentPacket > m_stopPacket);
        }

        public void RunUpdate()
        {
            //This is where we decide what we need to update
            //and assign the real discardLevel and packetNumber
            //assuming of course that the connected client might be bonkers

            if (!HasAsset)
            {
                if (!m_assetRequested)
                {
                    m_assetRequested = true;
                    AssetService.Get(TextureID.ToString(), this, AssetReceived);
                }
            }
            else
            {
                if (!IsDecoded)
                {
                    //We need to decode the requested image first
                    if (!m_decodeRequested)
                    {
                        //Request decode
                        m_decodeRequested = true;
                        // Do we have a jpeg decoder?
                        if (J2KDecoder != null)
                        {
                            if (m_asset == null)
                            {
                                J2KDecodedCallback(TextureID, new OpenJPEG.J2KLayerInfo[0]);
                            }
                            else
                            {
                                // Send it off to the jpeg decoder
                                J2KDecoder.BeginDecode(TextureID, m_asset, J2KDecodedCallback);
                            }
                        }
                        else
                        {
                            J2KDecodedCallback(TextureID, new OpenJPEG.J2KLayerInfo[0]);
                        }
                    }
                }
                else
                {
                    // Check for missing image asset data
                    if (m_asset == null)
                    {
                        MainConsole.Instance.Debug(
                            "[J2K Image]: RunUpdate() called with missing asset data (no missing image texture?). Canceling texture transfer");
                        m_currentPacket = m_stopPacket;
                        return;
                    }

                    if (DiscardLevel >= 0 || m_stopPacket == 0)
                    {
                        // This shouldn't happen, but if it does, we really can't proceed
                        if (Layers == null)
                        {
                            MainConsole.Instance.Warn(
                                "[J2K Image]: RunUpdate() called with missing Layers. Canceling texture transfer");
                            m_currentPacket = m_stopPacket;
                            return;
                        }

                        int maxDiscardLevel = Math.Max(0, Layers.Length - 1);

                        // Treat initial texture downloads with a DiscardLevel of -1 a request for the highest DiscardLevel
                        if (DiscardLevel < 0 && m_stopPacket == 0)
                            DiscardLevel = (sbyte) maxDiscardLevel;

                        // Clamp at the highest discard level
                        DiscardLevel = (sbyte) Math.Min(DiscardLevel, maxDiscardLevel);

                        //Calculate the m_stopPacket
                        if (Layers.Length > 0)
                        {
                            m_stopPacket =
                                (uint) GetPacketForBytePosition(Layers[(Layers.Length - 1) - DiscardLevel].End);
                            //I don't know why, but the viewer seems to expect the final packet if the file
                            //is just one packet bigger.
                            if (TexturePacketCount() == m_stopPacket + 1)
                            {
                                m_stopPacket = TexturePacketCount();
                            }
                        }
                        else
                        {
                            m_stopPacket = TexturePacketCount();
                        }

                        m_currentPacket = StartPacket;
                    }
                }
            }
        }

        bool SendFirstPacket(LLClientView client)
        {
            if (client == null)
                return false;

            if (m_asset == null)
            {
                MainConsole.Instance.Warn("[J2K Image]: Sending ImageNotInDatabase for texture " + TextureID);
                client.SendImageNotFound(TextureID);
                return true;
            }
            if (m_asset.Length <= FIRST_PACKET_SIZE)
            {
                // We have less then one packet's worth of data
                client.SendImageFirstPart(1, TextureID, (uint) m_asset.Length, m_asset, 2);
                m_stopPacket = 0;
                return true;
            }
            // This is going to be a multi-packet texture download
            byte[] firstImageData = new byte[FIRST_PACKET_SIZE];

            try
            {
                Buffer.BlockCopy(m_asset, 0, firstImageData, 0, FIRST_PACKET_SIZE);
            }
            catch (Exception)
            {
                MainConsole.Instance.ErrorFormat(
                    "[J2K Image]: Texture block copy for the first packet failed. textureid={0}, assetlength={1}",
                    TextureID, m_asset.Length);
                return true;
            }

            client.SendImageFirstPart(TexturePacketCount(), TextureID, (uint) m_asset.Length, firstImageData,
                                      (byte) ImageCodec.J2C);
            return false;
        }

        bool SendPacket(LLClientView client)
        {
            if (client == null || m_asset == null)
                return false;

            bool complete = false;
            int imagePacketSize = ((int) m_currentPacket == (TexturePacketCount()))
                                      ? LastPacketSize()
                                      : IMAGE_PACKET_SIZE;

            try
            {
                if ((CurrentBytePosition() + IMAGE_PACKET_SIZE) > m_asset.Length)
                {
                    imagePacketSize = LastPacketSize();
                    complete = true;
                    if ((CurrentBytePosition() + imagePacketSize) > m_asset.Length)
                    {
                        imagePacketSize = m_asset.Length - CurrentBytePosition();
                        complete = true;
                    }
                }

                // It's concievable that the client might request packet one
                // from a one packet image, which is really packet 0,
                // which would leave us with a negative imagePacketSize..
                if (imagePacketSize > 0)
                {
                    byte[] imageData = new byte[imagePacketSize];
                    int currentPosition = CurrentBytePosition();

                    try
                    {
                        Buffer.BlockCopy(m_asset, currentPosition, imageData, 0, imagePacketSize);
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.ErrorFormat(
                            "[J2K Image]: Texture block copy for the first packet failed. textureid={0}, assetlength={1}, currentposition={2}, imagepacketsize={3}, exception={4}",
                            TextureID, m_asset.Length, currentPosition, imagePacketSize, e.Message);
                        return false;
                    }

                    //Send the packet
                    client.SendImageNextPart((ushort) (m_currentPacket - 1), TextureID, imageData);
                }

                return !complete;
            }
            catch (Exception)
            {
                return false;
            }
        }

        ushort TexturePacketCount()
        {
            if (!IsDecoded)
                return 0;

            if (m_asset == null)
                return 0;

            if (m_asset.Length <= FIRST_PACKET_SIZE)
                return 1;

            return (ushort) (((m_asset.Length - FIRST_PACKET_SIZE + IMAGE_PACKET_SIZE - 1)/IMAGE_PACKET_SIZE) + 1);
        }

        int GetPacketForBytePosition(int bytePosition)
        {
            return ((bytePosition - FIRST_PACKET_SIZE + IMAGE_PACKET_SIZE - 1)/IMAGE_PACKET_SIZE) + 1;
        }

        int LastPacketSize()
        {
            if (m_currentPacket == 1)
                return m_asset.Length;
            int lastsize = (m_asset.Length - FIRST_PACKET_SIZE)%IMAGE_PACKET_SIZE;
            //If the last packet size is zero, it's really cImagePacketSize, it sits on the boundary
            if (lastsize == 0)
            {
                lastsize = IMAGE_PACKET_SIZE;
            }
            return lastsize;
        }

        int CurrentBytePosition()
        {
            if (m_currentPacket == 0)
                return 0;
            if (m_currentPacket == 1)
                return FIRST_PACKET_SIZE;

            int result = FIRST_PACKET_SIZE + ((int) m_currentPacket - 2)*IMAGE_PACKET_SIZE;
            if (result < 0)
            {
                result = FIRST_PACKET_SIZE;
            }
            return result;
        }

        void J2KDecodedCallback(UUID AssetId, OpenJPEG.J2KLayerInfo[] layers)
        {
            Layers = layers;
            IsDecoded = true;
            RunUpdate();
        }

        void AssetDataCallback(UUID AssetID, AssetBase asset)
        {
            HasAsset = true;

            if (asset == null || asset.Data == null || asset.Type == (int) AssetType.Mesh)
            {
                m_asset = null;
                IsDecoded = true;
            }
            else
            {
                m_asset = asset.Data;
                asset = null;
            }

            RunUpdate();
        }

        void AssetReceived(string id, object sender, AssetBase asset)
        {
            UUID assetID = UUID.Zero;
            if (asset != null)
                assetID = asset.ID;

            AssetDataCallback(assetID, asset);
        }
    }
}