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
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.ClientStack
{
    //public delegate bool PacketMethod (IClientAPI simClient, Packet packet);

    /// <summary>
    ///     Handles new client connections
    ///     Constructor takes a single Packet and authenticates everything
    /// </summary>
    public sealed partial class LLClientView : IClientAPI
    {
        #region unimplemented handlers

        bool HandleViewerStats (IClientAPI sender, Packet pack)
        {
            //MainConsole.Instance.Warn("[Client]: unhandled ViewerStats packet");
            return true;
        }

        bool HandleUseCircuitCode (IClientAPI sender, Packet pack)
        {
            return true;
        }

        bool HandleAgentHeightWidth (IClientAPI sender, Packet pack)
        {
            return true;
        }

        #endregion unimplemeted handlers


        #region Helper Methods

        ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateImprovedTerseBlock (IEntity entity, bool sendTexture)
        {
            #region ScenePresence/SOP Handling

            bool avatar = (entity is IScenePresence);
            uint localID = entity.LocalId;
            int attachPoint;
            Vector4 collisionPlane;
            Vector3 position, velocity, acceleration, angularVelocity;
            Quaternion rotation;
            byte [] textureEntry;

            if (entity is IScenePresence) {
                IScenePresence presence = (IScenePresence)entity;

                attachPoint = 0;
                if (presence.PhysicsActor != null && !presence.PhysicsActor.IsColliding)
                    presence.CollisionPlane = Vector4.UnitW;
                // We have to do this, otherwise the last ground one will be what we have, 
                // and it can cause the client to think that it shouldn't fly down, 
                // which will cause the agent to fall instead
                collisionPlane = presence.CollisionPlane;
                position = presence.OffsetPosition;
                velocity = presence.Velocity;
                acceleration = presence.PhysicsActor == null ? Vector3.Zero : presence.PhysicsActor.Acceleration;
                angularVelocity = presence.AngularVelocity;
                rotation = presence.Rotation;
                IAvatarAppearanceModule appearance = presence.RequestModuleInterface<IAvatarAppearanceModule> ();
                textureEntry = sendTexture ? appearance.Appearance.Texture.GetBytes () : null;
            } else {
                ISceneChildEntity part = (ISceneChildEntity)entity;

                attachPoint = part.AttachmentPoint;
                collisionPlane = Vector4.Zero;
                position = part.RelativePosition;
                velocity = part.Velocity;
                acceleration = part.Acceleration;
                angularVelocity = part.AngularVelocity;
                rotation = part.GetRotationOffset ();

                textureEntry = sendTexture ? part.Shape.TextureEntry : null;
            }

            #endregion ScenePresence/SOP Handling

            int pos = 0;
            byte [] data = new byte [(avatar ? 60 : 44)];

            // LocalID
            Utils.UIntToBytes (localID, data, pos);
            pos += 4;

            // Avatar/CollisionPlane
            data [pos] = (byte)((attachPoint & 0x0f) << 4);
            data [pos++] += (byte)(attachPoint >> 4);

            if (avatar) {
                data [pos++] = 1;

                if (collisionPlane == Vector4.Zero)
                    collisionPlane = Vector4.UnitW;
                //MainConsole.Instance.DebugFormat("CollisionPlane: {0}",collisionPlane);
                collisionPlane.ToBytes (data, pos);
                pos += 16;
            } else {
                ++pos;
            }

            // Position
            position.ToBytes (data, pos);
            pos += 12;

            // Velocity
            //MainConsole.Instance.DebugFormat("Velocity: {0}", velocity);
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (velocity.X, -128.0f, 128.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (velocity.Y, -128.0f, 128.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (velocity.Z, -128.0f, 128.0f), data, pos);
            pos += 2;

            // Acceleration
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (acceleration.X, -64.0f, 64.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (acceleration.Y, -64.0f, 64.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (acceleration.Z, -64.0f, 64.0f), data, pos);
            pos += 2;

            // Rotation
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (rotation.X, -1.0f, 1.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (rotation.Y, -1.0f, 1.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (rotation.Z, -1.0f, 1.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (rotation.W, -1.0f, 1.0f), data, pos);
            pos += 2;

            // Angular Velocity
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (angularVelocity.X, -64.0f, 64.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (angularVelocity.Y, -64.0f, 64.0f), data, pos);
            pos += 2;
            Utils.UInt16ToBytes (Utils.FloatToUInt16 (angularVelocity.Z, -64.0f, 64.0f), data, pos);
            pos += 2;

            ImprovedTerseObjectUpdatePacket.ObjectDataBlock block =
                new ImprovedTerseObjectUpdatePacket.ObjectDataBlock { Data = data };

            if (textureEntry != null && textureEntry.Length > 0) {
                byte [] teBytesFinal = new byte [textureEntry.Length + 4];

                // Texture Length
                Utils.IntToBytes (textureEntry.Length, textureEntry, 0);
                // Texture
                Buffer.BlockCopy (textureEntry, 0, teBytesFinal, 4, textureEntry.Length);

                block.TextureEntry = teBytesFinal;
            } else {
                block.TextureEntry = Utils.EmptyBytes;
            }
            return block;
        }

        ObjectUpdatePacket.ObjectDataBlock CreateAvatarUpdateBlock (IScenePresence data)
        {
            byte [] objectData = new byte [76];

            //No Zero vectors, as it causes bent knee in the client! Replace with <0, 0, 0, 1>
            if (data.CollisionPlane == Vector4.Zero)
                data.CollisionPlane = Vector4.UnitW;
            //MainConsole.Instance.DebugFormat("CollisionPlane: {0}", data.CollisionPlane);
            data.CollisionPlane.ToBytes (objectData, 0);
            data.OffsetPosition.ToBytes (objectData, 16);
            data.Velocity.ToBytes (objectData, 28);
            //data.Acceleration.ToBytes(objectData, 40);
            data.Rotation.ToBytes (objectData, 52);
            //data.AngularVelocity.ToBytes(objectData, 64);
            string [] spl = data.Name.Split (' ');
            string first = spl [0], last = (spl.Length == 1 ? "" : Util.CombineParams (spl, 1));

            ObjectUpdatePacket.ObjectDataBlock update = new ObjectUpdatePacket.ObjectDataBlock {
                Data = Utils.EmptyBytes,
                ExtraParams = new byte [1],
                FullID = data.UUID,
                ID = data.LocalId,
                Material = (byte)Material.Flesh,
                MediaURL = Utils.EmptyBytes,
                NameValue =
                    Utils.StringToBytes ("FirstName STRING RW SV " + first + "\nLastName STRING RW SV " + last +
                                         "\nTitle STRING RW SV " +
                                         (m_GroupsModule == null ? "" : m_GroupsModule.GetGroupTitle (data.UUID))),
                ObjectData = objectData
            };

            if (data.ParentID == UUID.Zero)
                update.ParentID = 0;
            else {
                ISceneChildEntity part = Scene.GetSceneObjectPart (data.ParentID);
                update.ParentID = part.LocalId;
            }
            update.PathCurve = 16;
            update.PathScaleX = 100;
            update.PathScaleY = 100;
            update.PCode = (byte)PCode.Avatar;
            update.ProfileCurve = 1;
            update.PSBlock = Utils.EmptyBytes;
            update.Scale = new Vector3 (0.45f, 0.6f, 1.9f);
            update.Text = Utils.EmptyBytes;
            update.TextColor = new byte [4];
            update.TextureAnim = Utils.EmptyBytes;
            // Don't send texture entry for avatars here - this is accomplished via the AvatarAppearance packet
            update.TextureEntry = Utils.EmptyBytes;
            update.UpdateFlags = (uint)(PrimFlags.Physics |
                                        PrimFlags.ObjectModify |
                                        PrimFlags.ObjectCopy |
                                        PrimFlags.ObjectAnyOwner |
                                        PrimFlags.ObjectYouOwner |
                                        PrimFlags.ObjectMove |
                                        PrimFlags.InventoryEmpty |
                                        PrimFlags.ObjectTransfer |
                                        PrimFlags.ObjectOwnerModify);

            return update;
        }

        ObjectUpdateCachedPacket.ObjectDataBlock CreatePrimCachedUpdateBlock (ISceneChildEntity data, UUID recipientID)
        {
            ObjectUpdateCachedPacket.ObjectDataBlock odb = new ObjectUpdateCachedPacket.ObjectDataBlock { CRC = data.CRC, ID = data.LocalId };

            #region PrimFlags

            PrimFlags flags = (PrimFlags)m_scene.Permissions.GenerateClientFlags (recipientID, data);

            // Don't send the CreateSelected flag to everyone
            flags &= ~PrimFlags.CreateSelected;

            if (recipientID == data.OwnerID) {
                if (data.CreateSelected) {
                    // Only send this flag once, then unset it
                    flags |= PrimFlags.CreateSelected;
                    data.CreateSelected = false;
                }
            }

            #endregion PrimFlags

            odb.UpdateFlags = (uint)flags;
            return odb;
        }

        ObjectUpdatePacket.ObjectDataBlock CreatePrimUpdateBlock (ISceneChildEntity data, UUID recipientID)
        {
            byte [] objectData = new byte [60];
            data.RelativePosition.ToBytes (objectData, 0);
            data.Velocity.ToBytes (objectData, 12);
            data.Acceleration.ToBytes (objectData, 24);
            try {
                data.GetRotationOffset ().ToBytes (objectData, 36);
            } catch (Exception e) {
                MainConsole.Instance.Warn (
                    "[Client]: exception converting quaternion to bytes, using Quaternion.Identity. Exception: " + e);
                Quaternion.Identity.ToBytes (objectData, 36);
            }
            data.AngularVelocity.ToBytes (objectData, 48);

            ObjectUpdatePacket.ObjectDataBlock update = new ObjectUpdatePacket.ObjectDataBlock {
                ClickAction = data.ClickAction,
                CRC = data.CRC,
                ExtraParams = data.Shape.ExtraParams ?? Utils.EmptyBytes,
                FullID = data.UUID,
                ID = data.LocalId,
                Material = (byte)data.Material,
                MediaURL = Utils.StringToBytes (data.CurrentMediaVersion)
            };
            //update.JointAxisOrAnchor = Vector3.Zero; // These are deprecated
            //update.JointPivot = Vector3.Zero;
            //update.JointType = 0;
            if (data.IsAttachment) {
                update.NameValue = Util.StringToBytes256 ("AttachItemID STRING RW SV " + data.FromUserInventoryItemID);
                update.State = (byte)((data.AttachmentPoint % 16) * 16 + (data.AttachmentPoint / 16));
            } else {
                update.NameValue = Utils.EmptyBytes;
                // The root part state is the canonical state for all parts of the object.  The other part states in the
                // case for attachments may contain conflicting values that can end up crashing the viewer.
                update.State = data.ParentEntity.RootChild.Shape.State;
            }

            update.ObjectData = objectData;
            update.ParentID = data.ParentID;
            update.PathBegin = data.Shape.PathBegin;
            update.PathCurve = data.Shape.PathCurve;
            update.PathEnd = data.Shape.PathEnd;
            update.PathRadiusOffset = data.Shape.PathRadiusOffset;
            update.PathRevolutions = data.Shape.PathRevolutions;
            update.PathScaleX = data.Shape.PathScaleX;
            update.PathScaleY = data.Shape.PathScaleY;
            update.PathShearX = data.Shape.PathShearX;
            update.PathShearY = data.Shape.PathShearY;
            update.PathSkew = data.Shape.PathSkew;
            update.PathTaperX = data.Shape.PathTaperX;
            update.PathTaperY = data.Shape.PathTaperY;
            update.PathTwist = data.Shape.PathTwist;
            update.PathTwistBegin = data.Shape.PathTwistBegin;
            update.PCode = data.Shape.PCode;
            update.ProfileBegin = data.Shape.ProfileBegin;
            update.ProfileCurve = data.Shape.ProfileCurve;
            update.ProfileEnd = data.Shape.ProfileEnd;
            update.ProfileHollow = data.Shape.ProfileHollow;
            update.PSBlock = data.ParticleSystem ?? Utils.EmptyBytes;
            update.TextColor = data.GetTextColor ().GetBytes (false);
            update.TextureAnim = data.TextureAnimation ?? Utils.EmptyBytes;
            update.TextureEntry = data.Shape.TextureEntry ?? Utils.EmptyBytes;
            update.Scale = data.Shape.Scale;
            update.Text = Util.StringToBytes256 (data.Text);
            update.MediaURL = Util.StringToBytes256 (data.MediaUrl);

            #region PrimFlags

            PrimFlags flags = (PrimFlags)m_scene.Permissions.GenerateClientFlags (recipientID, data);

            // Don't send the CreateSelected flag to everyone
            flags &= ~PrimFlags.CreateSelected;

            if (recipientID == data.OwnerID) {
                if (data.CreateSelected) {
                    // Only send this flag once, then unset it
                    flags |= PrimFlags.CreateSelected;
                    data.CreateSelected = false;
                }
            }

            //            MainConsole.Instance.DebugFormat(
            //                "[Client]: Constructing client update for part {0} {1} with flags {2}, localId {3}",
            //                data.Name, update.FullID, flags, update.ID);

            update.UpdateFlags = (uint)flags;

            #endregion PrimFlags

            if (data.Sound != UUID.Zero) {
                update.Sound = data.Sound;
                update.OwnerID = data.OwnerID;
                update.Gain = (float)data.SoundGain;
                update.Radius = (float)data.SoundRadius;
                update.Flags = data.SoundFlags;
            }

            switch ((PCode)data.Shape.PCode) {
            case PCode.Grass:
            case PCode.Tree:
            case PCode.NewTree:
                update.Data = new [] { data.Shape.State };
                break;
            default:
                update.Data = Utils.EmptyBytes;
                break;
            }

            return update;
        }

        ObjectUpdateCompressedPacket.ObjectDataBlock CreateCompressedUpdateBlock (ISceneChildEntity part,
                                                                                  CompressedFlags updateFlags,
                                                                                  PrimUpdateFlags flags)
        {
            using (System.IO.MemoryStream objectData = new System.IO.MemoryStream ()) {
                byte [] byteData = new byte [16];
                objectData.Write (part.UUID.GetBytes (), 0, 16);
                Utils.UIntToBytes (part.LocalId, byteData, 0);
                objectData.Write (byteData, 0, 4);
                objectData.WriteByte (part.Shape.PCode); //Type of prim

                if (part.Shape.PCode == (byte)PCode.Tree || part.Shape.PCode == (byte)PCode.NewTree)
                    updateFlags |= CompressedFlags.Tree;

                objectData.WriteByte ((byte)part.AttachmentPoint);
                Utils.UIntToBytes (part.CRC, byteData, 0);
                objectData.Write (byteData, 0, 4);
                objectData.WriteByte ((byte)part.Material);
                objectData.WriteByte ((byte)part.ClickAction);
                objectData.Write (part.Shape.Scale.GetBytes (), 0, 12);
                objectData.Write (part.RelativePosition.GetBytes (), 0, 12);
                objectData.Write (part.GetRotationOffset ().GetBytes (), 0, 12);
                Utils.UIntToBytes ((uint)updateFlags, byteData, 0);
                objectData.Write (byteData, 0, 4);
                objectData.Write (part.OwnerID.GetBytes (), 0, 16);

                if ((updateFlags & CompressedFlags.HasAngularVelocity) != 0)
                    objectData.Write (part.AngularVelocity.GetBytes (), 0, 12);
                if ((updateFlags & CompressedFlags.HasParent) != 0) {
                    if (part.IsAttachment) {
                        IScenePresence us = m_scene.GetScenePresence (AgentId);
                        Utils.UIntToBytes (us.LocalId, byteData, 0);
                    } else
                        Utils.UIntToBytes (part.ParentID, byteData, 0);
                    objectData.Write (byteData, 0, 4);
                }
                if ((updateFlags & CompressedFlags.Tree) != 0) {
                    objectData.WriteByte (part.Shape.State); //Tree type
                } else if ((updateFlags & CompressedFlags.ScratchPad) != 0) {
                    //Remove the flag, we have no clue what to do with this
                    updateFlags &= ~(CompressedFlags.ScratchPad);
                }
                if ((updateFlags & CompressedFlags.HasText) != 0) {
                    byte [] text = Utils.StringToBytes (part.Text);
                    objectData.Write (text, 0, text.Length);

                    byte [] textcolor = part.GetTextColor ().GetBytes (false);
                    objectData.Write (textcolor, 0, textcolor.Length);
                }
                if ((updateFlags & CompressedFlags.MediaURL) != 0) {
                    byte [] text = Util.StringToBytes256 (part.CurrentMediaVersion);
                    objectData.Write (text, 0, text.Length);
                }

                if ((updateFlags & CompressedFlags.HasParticles) != 0) {
                    if (part.ParticleSystem.Length == 0) {
                        Primitive.ParticleSystem Sys = new Primitive.ParticleSystem ();
                        byte [] pdata = Sys.GetBytes ();
                        objectData.Write (pdata, 0, pdata.Length);
                        //updateFlags = updateFlags & ~CompressedFlags.HasParticles;
                    } else
                        objectData.Write (part.ParticleSystem, 0, part.ParticleSystem.Length);
                }

                byte [] ExtraData = part.Shape.ExtraParamsToBytes ();
                objectData.Write (ExtraData, 0, ExtraData.Length);

                if ((updateFlags & CompressedFlags.HasSound) != 0) {
                    objectData.Write (part.Sound.GetBytes (), 0, 16);
                    Utils.FloatToBytes ((float)part.SoundGain, byteData, 0);
                    objectData.Write (byteData, 0, 4);
                    objectData.WriteByte (part.SoundFlags);
                    Utils.FloatToBytes ((float)part.SoundRadius, byteData, 0);
                    objectData.Write (byteData, 0, 4);
                }
                if ((updateFlags & CompressedFlags.HasNameValues) != 0) {
                    if (part.IsAttachment) {
                        byte [] NV = Util.StringToBytes256 ("AttachItemID STRING RW SV " + part.FromUserInventoryItemID);
                        objectData.Write (NV, 0, NV.Length);
                    }
                }

                objectData.WriteByte (part.Shape.PathCurve);
                Utils.UInt16ToBytes (part.Shape.PathBegin, byteData, 0);
                objectData.Write (byteData, 0, 2);
                Utils.UInt16ToBytes (part.Shape.PathEnd, byteData, 0);
                objectData.Write (byteData, 0, 2);
                objectData.WriteByte (part.Shape.PathScaleX);
                objectData.WriteByte (part.Shape.PathScaleY);
                objectData.WriteByte (part.Shape.PathShearX);
                objectData.WriteByte (part.Shape.PathShearY);
                objectData.WriteByte ((byte)part.Shape.PathTwist);
                objectData.WriteByte ((byte)part.Shape.PathTwistBegin);
                objectData.WriteByte ((byte)part.Shape.PathRadiusOffset);
                objectData.WriteByte ((byte)part.Shape.PathTaperX);
                objectData.WriteByte ((byte)part.Shape.PathTaperY);
                objectData.WriteByte (part.Shape.PathRevolutions);
                objectData.WriteByte ((byte)part.Shape.PathSkew);
                objectData.WriteByte (part.Shape.ProfileCurve);
                Utils.UInt16ToBytes (part.Shape.ProfileBegin, byteData, 0);
                objectData.Write (byteData, 0, 2);
                Utils.UInt16ToBytes (part.Shape.ProfileEnd, byteData, 0);
                objectData.Write (byteData, 0, 2);
                Utils.UInt16ToBytes (part.Shape.ProfileHollow, byteData, 0);
                objectData.Write (byteData, 0, 2);

                if (part.Shape.TextureEntry != null && part.Shape.TextureEntry.Length > 0) {
                    // Texture Length
                    Utils.IntToBytes (part.Shape.TextureEntry.Length, byteData, 0);
                    objectData.Write (byteData, 0, 4);
                    // Texture
                    objectData.Write (part.Shape.TextureEntry, 0, part.Shape.TextureEntry.Length);
                } else {
                    Utils.IntToBytes (0, byteData, 0);
                    objectData.Write (byteData, 0, 4);
                }

                if ((updateFlags & CompressedFlags.TextureAnimation) != 0) {
                    Utils.UInt64ToBytes ((ulong)part.TextureAnimation.Length, byteData, 0);
                    objectData.Write (byteData, 0, 4);
                    objectData.Write (part.TextureAnimation, 0, part.TextureAnimation.Length);
                }

                ObjectUpdateCompressedPacket.ObjectDataBlock update = new ObjectUpdateCompressedPacket.ObjectDataBlock ();

                #region PrimFlags

                PrimFlags primflags = (PrimFlags)m_scene.Permissions.GenerateClientFlags (AgentId, part);

                // Don't send the CreateSelected flag to everyone
                primflags &= ~PrimFlags.CreateSelected;

                if (AgentId == part.OwnerID) {
                    if (part.CreateSelected) {
                        // Only send this flag once, then unset it
                        primflags |= PrimFlags.CreateSelected;
                        part.CreateSelected = false;
                    }
                }

                update.UpdateFlags = (uint)primflags;

                #endregion PrimFlags

                update.Data = objectData.ToArray ();

                return update;
            }
        }

        public void SendNameReply (UUID profileId, string name)
        {
            UUIDNameReplyPacket packet = (UUIDNameReplyPacket)PacketPool.Instance.GetPacket (PacketType.UUIDNameReply);
            // TODO: don't create new blocks if recycling an old packet
            packet.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock [1];
            string [] spl = name.Split (' ');
            string first = spl [0], last = (spl.Length == 1 ? "" : Util.CombineParams (spl, 1));
            packet.UUIDNameBlock [0] = new UUIDNameReplyPacket.UUIDNameBlockBlock {
                ID = profileId,
                FirstName = Util.StringToBytes256 (first),
                LastName = Util.StringToBytes256 (last)
            };

            OutPacket (packet, ThrottleOutPacketType.Asset);
        }

        /// <summary>
        ///     This is a utility method used by single states to not duplicate kicks and blue card of death messages.
        /// </summary>
        public bool ChildAgentStatus ()
        {
            IScenePresence Sp = m_scene.GetScenePresence (AgentId);
            if (Sp == null || (Sp.IsChildAgent))
                return true;
            return false;
        }

        #endregion


        bool HandleUUIDNameRequest (IClientAPI sender, Packet pack)
        {
            var incoming = (UUIDNameRequestPacket)pack;

            foreach (UUIDNameRequestPacket.UUIDNameBlockBlock UUIDBlock in incoming.UUIDNameBlock) {
                UUIDNameRequest handlerNameRequest = OnNameFromUUIDRequest;
                if (handlerNameRequest != null) {
                    handlerNameRequest (UUIDBlock.ID, this);
                }
            }
            return true;
        }

        #region GodPackets

        bool HandleRequestGodlikePowers (IClientAPI sender, Packet pack)
        {
            var rglpPack = (RequestGodlikePowersPacket)pack;
            RequestGodlikePowersPacket.RequestBlockBlock rblock = rglpPack.RequestBlock;
            UUID token = rblock.Token;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (rglpPack.AgentData.SessionID != SessionId || rglpPack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RequestGodlikePowersPacket.AgentDataBlock ablock = rglpPack.AgentData;

            RequestGodlikePowers handlerReqGodlikePowers = OnRequestGodlikePowers;

            if (handlerReqGodlikePowers != null) {
                handlerReqGodlikePowers (ablock.AgentID, ablock.SessionID, token, rblock.Godlike, this);
            }

            return true;
        }

        bool HandleGodUpdateRegionInfoUpdate (IClientAPI client, Packet pack)
        {
            var GodUpdateRegionInfo = (GodUpdateRegionInfoPacket)pack;

            GodUpdateRegionInfoUpdate handlerGodUpdateRegionInfo = OnGodUpdateRegionInfoUpdate;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (GodUpdateRegionInfo.AgentData.SessionID != SessionId || GodUpdateRegionInfo.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (handlerGodUpdateRegionInfo != null) {
                handlerGodUpdateRegionInfo (this,
                                           GodUpdateRegionInfo.RegionInfo.BillableFactor,
                                           GodUpdateRegionInfo.RegionInfo.PricePerMeter,
                                           GodUpdateRegionInfo.RegionInfo.EstateID,
                                           GodUpdateRegionInfo.RegionInfo.RegionFlags,
                                           GodUpdateRegionInfo.RegionInfo.SimName,
                                           GodUpdateRegionInfo.RegionInfo.RedirectGridX,
                                           GodUpdateRegionInfo.RegionInfo.RedirectGridY);
                return true;
            }
            return false;
        }

        bool HandleSimWideDeletes (IClientAPI client, Packet pack)
        {
            var SimWideDeletesRequest = (SimWideDeletesPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (SimWideDeletesRequest.AgentData.SessionID != SessionId ||
                    SimWideDeletesRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SimWideDeletesDelegate handlerSimWideDeletesRequest = OnSimWideDeletes;
            if (handlerSimWideDeletesRequest != null) {
                handlerSimWideDeletesRequest (this,
                                              (int)SimWideDeletesRequest.DataBlock.Flags,
                                              SimWideDeletesRequest.DataBlock.TargetID);
                return true;
            }
            return false;
        }

        bool HandleGodlikeMessage (IClientAPI client, Packet pack)
        {
            var GodlikeMessage = (GodlikeMessagePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (GodlikeMessage.AgentData.SessionID != SessionId ||
                    GodlikeMessage.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            GodlikeMessage handlerGodlikeMessage = OnGodlikeMessage;

            List<string> Parameters =
                GodlikeMessage.ParamList.Select (block => Utils.BytesToString (block.Parameter)).ToList ();

            if (handlerGodlikeMessage != null) {
                handlerGodlikeMessage (this,
                                      GodlikeMessage.MethodData.Invoice,
                                      Utils.BytesToString (GodlikeMessage.MethodData.Method),
                                      Parameters);
                return true;
            }
            return false;
        }

        bool HandleSaveStatePacket (IClientAPI client, Packet pack)
        {
            var SaveStateMessage = (StateSavePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (SaveStateMessage.AgentData.SessionID != SessionId ||
                    SaveStateMessage.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SaveStateHandler handlerSaveStatePacket = OnSaveState;
            if (handlerSaveStatePacket != null) {
                handlerSaveStatePacket (this, SaveStateMessage.AgentData.AgentID);
                return true;
            }
            return false;
        }

        bool HandleGodKickUser (IClientAPI sender, Packet pack)
        {
            var gkupack = (GodKickUserPacket)pack;

            if (gkupack.UserInfo.GodSessionID == SessionId && AgentId == gkupack.UserInfo.GodID) {
                GodKickUser handlerGodKickUser = OnGodKickUser;
                if (handlerGodKickUser != null) {
                    handlerGodKickUser (gkupack.UserInfo.GodID,
                                        gkupack.UserInfo.GodSessionID,
                                        gkupack.UserInfo.AgentID,
                                        gkupack.UserInfo.KickFlags,
                                        gkupack.UserInfo.Reason);
                }
            } else {
                SendAgentAlertMessage ("Kick request denied", false);
            }
            return true;
        }

        #endregion GodPackets

        #region Economy/Transaction Packets

        bool HandleMoneyBalanceRequest (IClientAPI sender, Packet pack)
        {
            var moneybalancerequestpacket = (MoneyBalanceRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (moneybalancerequestpacket.AgentData.SessionID != SessionId ||
                    moneybalancerequestpacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            MoneyBalanceRequest handlerMoneyBalanceRequest = OnMoneyBalanceRequest;

            if (handlerMoneyBalanceRequest != null) {
                handlerMoneyBalanceRequest (this,
                                            moneybalancerequestpacket.AgentData.AgentID,
                                            moneybalancerequestpacket.AgentData.SessionID,
                                            moneybalancerequestpacket.MoneyData.TransactionID);
            }

            return true;
        }

        bool HandleEconomyDataRequest (IClientAPI sender, Packet pack)
        {
            EconomyDataRequest handlerEconomoyDataRequest = OnEconomyDataRequest;
            if (handlerEconomoyDataRequest != null) {
                handlerEconomoyDataRequest (this);
            }
            return true;
        }

        bool HandleRequestPayPrice (IClientAPI sender, Packet pack)
        {
            var requestPayPricePacket = (RequestPayPricePacket)pack;

            RequestPayPrice handlerRequestPayPrice = OnRequestPayPrice;
            if (handlerRequestPayPrice != null) {
                handlerRequestPayPrice (this, requestPayPricePacket.ObjectData.ObjectID);
            }
            return true;
        }

        bool HandleObjectSaleInfo (IClientAPI sender, Packet pack)
        {
            var objectSaleInfoPacket = (ObjectSaleInfoPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (objectSaleInfoPacket.AgentData.SessionID != SessionId ||
                    objectSaleInfoPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ObjectSaleInfo handlerObjectSaleInfo = OnObjectSaleInfo;
            if (handlerObjectSaleInfo != null) {
                foreach (ObjectSaleInfoPacket.ObjectDataBlock d
                    in objectSaleInfoPacket.ObjectData) {
                    handlerObjectSaleInfo (this,
                                           objectSaleInfoPacket.AgentData.SessionID,
                                           d.LocalID,
                                           d.SaleType,
                                           d.SalePrice);
                }
            }
            return true;
        }

        bool HandleObjectBuy (IClientAPI sender, Packet pack)
        {
            var objectBuyPacket = (ObjectBuyPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (objectBuyPacket.AgentData.SessionID != SessionId ||
                    objectBuyPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ObjectBuy handlerObjectBuy = OnObjectBuy;

            if (handlerObjectBuy != null) {
                foreach (ObjectBuyPacket.ObjectDataBlock d
                    in objectBuyPacket.ObjectData) {
                    handlerObjectBuy (this,
                                      objectBuyPacket.AgentData.SessionID,
                                      objectBuyPacket.AgentData.GroupID,
                                      objectBuyPacket.AgentData.CategoryID,
                                      d.ObjectLocalID,
                                      d.SaleType,
                                      d.SalePrice);
                }
            }
            return true;
        }

        bool HandleViewerStartAuction (IClientAPI client, Packet pack)
        {
            var aPacket = (ViewerStartAuctionPacket)pack;
            ViewerStartAuction handlerStartAuction = OnViewerStartAuction;

            if (handlerStartAuction != null) {
                handlerStartAuction (this, aPacket.ParcelData.LocalID, aPacket.ParcelData.SnapshotID);
                return true;
            }
            return false;
        }

        #endregion Economy/Transaction Packets

        #region Script Packets

        public void SendScriptQuestion (UUID taskID, string taskName, string ownerName, UUID itemID, int question)
        {
            ScriptQuestionPacket scriptQuestion =
                (ScriptQuestionPacket)PacketPool.Instance.GetPacket (PacketType.ScriptQuestion);
            scriptQuestion.Data = new ScriptQuestionPacket.DataBlock {
                TaskID = taskID,
                ItemID = itemID,
                Questions = question,
                ObjectName = Util.StringToBytes256 (taskName),
                ObjectOwner = Util.StringToBytes256 (ownerName)
            };
            // TODO: don't create new blocks if recycling an old packet

            OutPacket (scriptQuestion, ThrottleOutPacketType.AvatarInfo);
        }


        bool HandleGetScriptRunning (IClientAPI sender, Packet pack)
        {
            var scriptRunning = (GetScriptRunningPacket)pack;

            GetScriptRunning handlerGetScriptRunning = OnGetScriptRunning;
            if (handlerGetScriptRunning != null) {
                handlerGetScriptRunning (this, scriptRunning.Script.ObjectID, scriptRunning.Script.ItemID);
            }
            return true;
        }

        bool HandleSetScriptRunning (IClientAPI sender, Packet pack)
        {
            var setScriptRunning = (SetScriptRunningPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (setScriptRunning.AgentData.SessionID != SessionId ||
                    setScriptRunning.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SetScriptRunning handlerSetScriptRunning = OnSetScriptRunning;
            if (handlerSetScriptRunning != null) {
                handlerSetScriptRunning (this,
                                         setScriptRunning.Script.ObjectID,
                                         setScriptRunning.Script.ItemID,
                                         setScriptRunning.Script.Running);
            }
            return true;
        }

        bool HandleScriptReset (IClientAPI sender, Packet pack)
        {
            var scriptResetPacket = (ScriptResetPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (scriptResetPacket.AgentData.SessionID != SessionId ||
                    scriptResetPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ScriptReset handlerScriptReset = OnScriptReset;
            if (handlerScriptReset != null) {
                handlerScriptReset (this, scriptResetPacket.Script.ObjectID, scriptResetPacket.Script.ItemID);
            }
            return true;
        }

        bool HandleScriptAnswerYes (IClientAPI sender, Packet pack)
        {
            var scriptAnswer = (ScriptAnswerYesPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (scriptAnswer.AgentData.SessionID != SessionId ||
                    scriptAnswer.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ScriptAnswer handlerScriptAnswer = OnScriptAnswer;
            if (handlerScriptAnswer != null) {
                handlerScriptAnswer (this, scriptAnswer.Data.TaskID, scriptAnswer.Data.ItemID,
                                    scriptAnswer.Data.Questions);
            }
            return true;
        }

        #endregion Script Packets

        #region Gesture Managment

        bool HandleActivateGestures (IClientAPI sender, Packet pack)
        {
            var activateGesturePacket = (ActivateGesturesPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (activateGesturePacket.AgentData.SessionID != SessionId ||
                    activateGesturePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ActivateGesture handlerActivateGesture = OnActivateGesture;
            if (handlerActivateGesture != null) {
                handlerActivateGesture (this,
                                       activateGesturePacket.Data [0].AssetID,
                                       activateGesturePacket.Data [0].ItemID);
            } else MainConsole.Instance.Error ("[Client]: Null pointer for activateGesture");

            return true;
        }

        bool HandleDeactivateGestures (IClientAPI sender, Packet pack)
        {
            var deactivateGesturePacket = (DeactivateGesturesPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (deactivateGesturePacket.AgentData.SessionID != SessionId ||
                    deactivateGesturePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DeactivateGesture handlerDeactivateGesture = OnDeactivateGesture;
            if (handlerDeactivateGesture != null) {
                handlerDeactivateGesture (this, deactivateGesturePacket.Data [0].ItemID);
            }
            return true;
        }

        bool HandleObjectOwner (IClientAPI sender, Packet pack)
        {
            var objectOwnerPacket = (ObjectOwnerPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (objectOwnerPacket.AgentData.SessionID != SessionId ||
                    objectOwnerPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            List<uint> localIDs = objectOwnerPacket.ObjectData.Select (d => d.ObjectLocalID).ToList ();

            ObjectOwner handlerObjectOwner = OnObjectOwner;
            if (handlerObjectOwner != null) {
                handlerObjectOwner (this,
                                    objectOwnerPacket.HeaderData.OwnerID,
                                    objectOwnerPacket.HeaderData.GroupID,
                                    localIDs);
            }
            return true;
        }

        #endregion Gesture Managment

        bool HandleAgentFOV (IClientAPI sender, Packet pack)
        {
            var fovPacket = (AgentFOVPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (fovPacket.AgentData.SessionID != SessionId ||
                    fovPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (fovPacket.FOVBlock.GenCounter > m_agentFOVCounter) {
                m_agentFOVCounter = fovPacket.FOVBlock.GenCounter;
                AgentFOV handlerAgentFOV = OnAgentFOV;
                if (handlerAgentFOV != null) {
                    handlerAgentFOV (this, fovPacket.FOVBlock.VerticalAngle);
                }
            }
            return true;
        }


        bool HandleMuteListRequest (IClientAPI sender, Packet pack)
        {
            var muteListRequest = (MuteListRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (muteListRequest.AgentData.SessionID != SessionId ||
                    muteListRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            MuteListRequest handlerMuteListRequest = OnMuteListRequest;
            if (handlerMuteListRequest != null) {
                handlerMuteListRequest (this, muteListRequest.MuteData.MuteCRC);
            } else {
                SendUseCachedMuteList ();
            }
            return true;
        }

        bool HandleUpdateMuteListEntry (IClientAPI client, Packet pack)
        {
            var UpdateMuteListEntry = (UpdateMuteListEntryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (UpdateMuteListEntry.AgentData.SessionID != SessionId ||
                    UpdateMuteListEntry.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            MuteListEntryUpdate handlerUpdateMuteListEntry = OnUpdateMuteListEntry;
            if (handlerUpdateMuteListEntry != null) {
                handlerUpdateMuteListEntry (this,
                                            UpdateMuteListEntry.MuteData.MuteID,
                                            Utils.BytesToString (UpdateMuteListEntry.MuteData.MuteName),
                                            UpdateMuteListEntry.MuteData.MuteType,
                                            UpdateMuteListEntry.AgentData.AgentID);
                return true;
            }
            return false;
        }

        bool HandleRemoveMuteListEntry (IClientAPI client, Packet pack)
        {
            var RemoveMuteListEntry = (RemoveMuteListEntryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (RemoveMuteListEntry.AgentData.SessionID != SessionId ||
                    RemoveMuteListEntry.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            MuteListEntryRemove handlerRemoveMuteListEntry = OnRemoveMuteListEntry;
            if (handlerRemoveMuteListEntry != null) {
                handlerRemoveMuteListEntry (this,
                                           RemoveMuteListEntry.MuteData.MuteID,
                                           Utils.BytesToString (RemoveMuteListEntry.MuteData.MuteName),
                                           RemoveMuteListEntry.AgentData.AgentID);
                return true;
            }
            return false;
        }

        bool HandleUserReport (IClientAPI client, Packet pack)
        {
            var UserReport = (UserReportPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (UserReport.AgentData.SessionID != SessionId ||
                    UserReport.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            NewUserReport handlerUserReport = OnUserReport;
            if (handlerUserReport != null) {
                handlerUserReport (this,
                                  Utils.BytesToString (UserReport.ReportData.AbuseRegionName),
                                  UserReport.ReportData.AbuserID,
                                  UserReport.ReportData.Category,
                                  UserReport.ReportData.CheckFlags,
                                  Utils.BytesToString (UserReport.ReportData.Details),
                                  UserReport.ReportData.ObjectID,
                                  UserReport.ReportData.Position,
                                  UserReport.ReportData.ReportType,
                                  UserReport.ReportData.ScreenshotID,
                                  Utils.BytesToString (UserReport.ReportData.Summary),
                                  UserReport.AgentData.AgentID);
                return true;
            }
            return false;
        }

        bool HandleSendPostcard (IClientAPI client, Packet pack)
        {
            var SendPostcard = (SendPostcardPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (SendPostcard.AgentData.SessionID != SessionId ||
                    SendPostcard.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SendPostcard handlerSendPostcard = OnSendPostcard;
            if (handlerSendPostcard != null) {
                handlerSendPostcard (this);
                return true;
            }
            return false;
        }

        bool HandleParcelDisableObjects (IClientAPI client, Packet pack)
        {
            var aPacket = (ParcelDisableObjectsPacket)pack;
            ParcelReturnObjectsRequest handlerParcelDisableObjectsRequest = OnParcelDisableObjectsRequest;

            if (handlerParcelDisableObjectsRequest != null) {
                handlerParcelDisableObjectsRequest (aPacket.ParcelData.LocalID,
                                                    aPacket.ParcelData.ReturnType,
                                                    aPacket.OwnerIDs.Select (block => block.OwnerID).ToArray (),
                                                    aPacket.TaskIDs.Select (block => block.TaskID).ToArray (),
                                                    this);
                return true;
            }
            return false;
        }

        bool HandleVelocityInterpolate (IClientAPI client, Packet pack)
        {
            VelocityInterpolateChangeRequest handlerVelocityInterpolateChangeRequest = OnVelocityInterpolateChangeRequest;

            if (handlerVelocityInterpolateChangeRequest != null) {
                handlerVelocityInterpolateChangeRequest (pack is VelocityInterpolateOnPacket, this);
                return true;
            }
            return false;
        }

        bool HandleTeleportCancel (IClientAPI client, Packet pack)
        {
            TeleportCancel handlerTeleportCancel = OnTeleportCancel;

            if (handlerTeleportCancel != null) {
                handlerTeleportCancel (this);
                return true;
            }
            return false;
        }

        #region Dir handlers

        bool HandleDirPlacesQuery (IClientAPI sender, Packet pack)
        {
            var dirPlacesQueryPacket = (DirPlacesQueryPacket)pack;
            //MainConsole.Instance.Debug(dirPlacesQueryPacket.ToString());

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dirPlacesQueryPacket.AgentData.SessionID != SessionId ||
                    dirPlacesQueryPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DirPlacesQuery handlerDirPlacesQuery = OnDirPlacesQuery;
            if (handlerDirPlacesQuery != null) {
                handlerDirPlacesQuery (this,
                                      dirPlacesQueryPacket.QueryData.QueryID,
                                      Utils.BytesToString (dirPlacesQueryPacket.QueryData.QueryText),
                                      (int)dirPlacesQueryPacket.QueryData.QueryFlags,
                                      dirPlacesQueryPacket.QueryData.Category,
                                      Utils.BytesToString (dirPlacesQueryPacket.QueryData.SimName),
                                      dirPlacesQueryPacket.QueryData.QueryStart);
            }
            return true;
        }

        bool HandleDirFindQuery (IClientAPI sender, Packet pack)
        {
            var dirFindQueryPacket = (DirFindQueryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dirFindQueryPacket.AgentData.SessionID != SessionId ||
                    dirFindQueryPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DirFindQuery handlerDirFindQuery = OnDirFindQuery;
            if (handlerDirFindQuery != null) {
                handlerDirFindQuery (this,
                                    dirFindQueryPacket.QueryData.QueryID,
                                    Utils.BytesToString (dirFindQueryPacket.QueryData.QueryText),
                                    dirFindQueryPacket.QueryData.QueryFlags,
                                    dirFindQueryPacket.QueryData.QueryStart);
            }
            return true;
        }

        bool HandleDirLandQuery (IClientAPI sender, Packet pack)
        {
            var dirLandQueryPacket = (DirLandQueryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dirLandQueryPacket.AgentData.SessionID != SessionId ||
                    dirLandQueryPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DirLandQuery handlerDirLandQuery = OnDirLandQuery;
            if (handlerDirLandQuery != null) {
                handlerDirLandQuery (this,
                                     dirLandQueryPacket.QueryData.QueryID,
                                     dirLandQueryPacket.QueryData.QueryFlags,
                                     dirLandQueryPacket.QueryData.SearchType,
                                     (uint)dirLandQueryPacket.QueryData.Price,
                                     (uint)dirLandQueryPacket.QueryData.Area,
                                     dirLandQueryPacket.QueryData.QueryStart);
            }
            return true;
        }

        bool HandleDirPopularQuery (IClientAPI sender, Packet pack)
        {
            var dirPopularQueryPacket = (DirPopularQueryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dirPopularQueryPacket.AgentData.SessionID != SessionId ||
                    dirPopularQueryPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DirPopularQuery handlerDirPopularQuery = OnDirPopularQuery;
            if (handlerDirPopularQuery != null) {
                handlerDirPopularQuery (this,
                                        dirPopularQueryPacket.QueryData.QueryID,
                                        dirPopularQueryPacket.QueryData.QueryFlags);
            }
            return true;
        }

        bool HandleDirClassifiedQuery (IClientAPI sender, Packet pack)
        {
            var dirClassifiedQueryPacket = (DirClassifiedQueryPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dirClassifiedQueryPacket.AgentData.SessionID != SessionId ||
                    dirClassifiedQueryPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DirClassifiedQuery handlerDirClassifiedQuery = OnDirClassifiedQuery;
            if (handlerDirClassifiedQuery != null) {
                handlerDirClassifiedQuery (this,
                                           dirClassifiedQueryPacket.QueryData.QueryID,
                                           Utils.BytesToString (dirClassifiedQueryPacket.QueryData.QueryText),
                                           dirClassifiedQueryPacket.QueryData.QueryFlags,
                                           dirClassifiedQueryPacket.QueryData.Category,
                                           dirClassifiedQueryPacket.QueryData.QueryStart);
            }
            return true;
        }

        bool HandleEventInfoRequest (IClientAPI sender, Packet pack)
        {
            var eventInfoRequestPacket = (EventInfoRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (eventInfoRequestPacket.AgentData.SessionID != SessionId ||
                    eventInfoRequestPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnEventInfoRequest != null) {
                OnEventInfoRequest (this, eventInfoRequestPacket.EventData.EventID);
            }
            return true;
        }

        #endregion

        #region Calling Card

        bool HandleOfferCallingCard (IClientAPI sender, Packet pack)
        {
            var offerCallingCardPacket = (OfferCallingCardPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (offerCallingCardPacket.AgentData.SessionID != SessionId ||
                    offerCallingCardPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnOfferCallingCard != null) {
                OnOfferCallingCard (this,
                                   offerCallingCardPacket.AgentBlock.DestID,
                                   offerCallingCardPacket.AgentBlock.TransactionID);
            }
            return true;
        }

        bool HandleAcceptCallingCard (IClientAPI sender, Packet pack)
        {
            var acceptCallingCardPacket = (AcceptCallingCardPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (acceptCallingCardPacket.AgentData.SessionID != SessionId ||
                    acceptCallingCardPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            // according to http://wiki.secondlife.com/wiki/AcceptCallingCard FolderData should
            // contain exactly one entry
            if (OnAcceptCallingCard != null && acceptCallingCardPacket.FolderData.Length > 0) {
                OnAcceptCallingCard (this,
                                    acceptCallingCardPacket.TransactionBlock.TransactionID,
                                    acceptCallingCardPacket.FolderData [0].FolderID);
            }
            return true;
        }

        bool HandleDeclineCallingCard (IClientAPI sender, Packet pack)
        {
            var declineCallingCardPacket = (DeclineCallingCardPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (declineCallingCardPacket.AgentData.SessionID != SessionId ||
                    declineCallingCardPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnDeclineCallingCard != null) {
                OnDeclineCallingCard (this, declineCallingCardPacket.TransactionBlock.TransactionID);
            }
            return true;
        }

        #endregion Calling Card

        #region Groups

        bool HandleActivateGroup (IClientAPI sender, Packet pack)
        {
            var activateGroupPacket = (ActivateGroupPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (activateGroupPacket.AgentData.SessionID != SessionId ||
                    activateGroupPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                m_GroupsModule.ActivateGroup (this, activateGroupPacket.AgentData.GroupID);
            }
            return true;
        }

        bool HandleGroupVoteHistoryRequest (IClientAPI client, Packet pack)
        {
            var GroupVoteHistoryRequest = (GroupVoteHistoryRequestPacket)pack;
            GroupVoteHistoryRequest handlerGroupVoteHistoryRequest = OnGroupVoteHistoryRequest;
            if (handlerGroupVoteHistoryRequest != null) {
                handlerGroupVoteHistoryRequest (this,
                                                GroupVoteHistoryRequest.AgentData.AgentID,
                                                GroupVoteHistoryRequest.AgentData.SessionID,
                                                GroupVoteHistoryRequest.GroupData.GroupID,
                                                GroupVoteHistoryRequest.TransactionData.TransactionID);
                return true;
            }
            return false;
        }

        bool HandleGroupProposalBallot (IClientAPI client, Packet pack)
        {
            var GroupProposalBallotRequest = (GroupProposalBallotPacket)pack;
            GroupProposalBallotRequest handlerGroupActiveProposalsRequest = OnGroupProposalBallotRequest;
            if (handlerGroupActiveProposalsRequest != null) {
                handlerGroupActiveProposalsRequest (this,
                                                    GroupProposalBallotRequest.AgentData.AgentID,
                                                    GroupProposalBallotRequest.AgentData.SessionID,
                                                    GroupProposalBallotRequest.ProposalData.GroupID,
                                                    GroupProposalBallotRequest.ProposalData.ProposalID,
                                                    Utils.BytesToString (GroupProposalBallotRequest.ProposalData.VoteCast));
                return true;
            }
            return false;
        }

        bool HandleGroupActiveProposalsRequest (IClientAPI client, Packet pack)
        {
            var GroupActiveProposalsRequest = (GroupActiveProposalsRequestPacket)pack;
            GroupActiveProposalsRequest handlerGroupActiveProposalsRequest = OnGroupActiveProposalsRequest;
            if (handlerGroupActiveProposalsRequest != null) {
                handlerGroupActiveProposalsRequest (this,
                                                    GroupActiveProposalsRequest.AgentData.AgentID,
                                                    GroupActiveProposalsRequest.AgentData.SessionID,
                                                    GroupActiveProposalsRequest.GroupData.GroupID,
                                                    GroupActiveProposalsRequest.TransactionData.TransactionID);
                return true;
            }
            return false;
        }

        bool HandleGroupAccountDetailsRequest (IClientAPI client, Packet pack)
        {
            var GroupAccountDetailsRequest = (GroupAccountDetailsRequestPacket)pack;
            GroupAccountDetailsRequest handlerGroupAccountDetailsRequest = OnGroupAccountDetailsRequest;
            if (handlerGroupAccountDetailsRequest != null) {
                handlerGroupAccountDetailsRequest (this,
                                                   GroupAccountDetailsRequest.AgentData.AgentID,
                                                   GroupAccountDetailsRequest.AgentData.GroupID,
                                                   GroupAccountDetailsRequest.MoneyData.RequestID,
                                                   GroupAccountDetailsRequest.AgentData.SessionID,
                                                   GroupAccountDetailsRequest.MoneyData.CurrentInterval,
                                                   GroupAccountDetailsRequest.MoneyData.IntervalDays);
                return true;
            }
            return false;
        }

        bool HandleGroupAccountSummaryRequest (IClientAPI client, Packet pack)
        {
            var GroupAccountSummaryRequest = (GroupAccountSummaryRequestPacket)pack;
            GroupAccountSummaryRequest handlerGroupAccountSummaryRequest = OnGroupAccountSummaryRequest;
            if (handlerGroupAccountSummaryRequest != null) {
                handlerGroupAccountSummaryRequest (this,
                                                   GroupAccountSummaryRequest.AgentData.AgentID,
                                                   GroupAccountSummaryRequest.AgentData.GroupID,
                                                   GroupAccountSummaryRequest.MoneyData.RequestID,
                                                   GroupAccountSummaryRequest.MoneyData.CurrentInterval,
                                                   GroupAccountSummaryRequest.MoneyData.IntervalDays);
                return true;
            }
            return false;
        }

        bool HandleGroupTransactionsDetailsRequest (IClientAPI client, Packet pack)
        {
            var GroupAccountTransactionsRequest = (GroupAccountTransactionsRequestPacket)pack;
            GroupAccountTransactionsRequest handlerGroupAccountTransactionsRequest = OnGroupAccountTransactionsRequest;
            if (handlerGroupAccountTransactionsRequest != null) {
                handlerGroupAccountTransactionsRequest (this,
                                                        GroupAccountTransactionsRequest.AgentData.AgentID,
                                                        GroupAccountTransactionsRequest.AgentData.GroupID,
                                                        GroupAccountTransactionsRequest.MoneyData.RequestID,
                                                        GroupAccountTransactionsRequest.AgentData.SessionID,
                                                        GroupAccountTransactionsRequest.MoneyData.CurrentInterval,
                                                        GroupAccountTransactionsRequest.MoneyData.IntervalDays);
                return true;
            }
            return false;
        }

        bool HandleGroupTitlesRequest (IClientAPI sender, Packet pack)
        {
            var groupTitlesRequest = (GroupTitlesRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupTitlesRequest.AgentData.SessionID != SessionId ||
                    groupTitlesRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                GroupTitlesReplyPacket groupTitlesReply =
                    (GroupTitlesReplyPacket)PacketPool.Instance.GetPacket (PacketType.GroupTitlesReply);

                groupTitlesReply.AgentData =
                    new GroupTitlesReplyPacket.AgentDataBlock {
                        AgentID = AgentId,
                        GroupID = groupTitlesRequest.AgentData.GroupID,
                        RequestID = groupTitlesRequest.AgentData.RequestID
                    };

                List<GroupTitlesData> titles =
                    m_GroupsModule.GroupTitlesRequest (this, groupTitlesRequest.AgentData.GroupID);
                if (titles != null) {
                    groupTitlesReply.GroupData = new GroupTitlesReplyPacket.GroupDataBlock [titles.Count];

                    int i = 0;
                    foreach (GroupTitlesData d in titles) {
                        groupTitlesReply.GroupData [i] = new GroupTitlesReplyPacket.GroupDataBlock {
                            Title = Util.StringToBytes256 (d.Name),
                            RoleID = d.UUID,
                            Selected = d.Selected
                        };

                        i++;
                    }
                }
                OutPacket (groupTitlesReply, ThrottleOutPacketType.Asset);
            }
            return true;
        }

        bool HandleGroupProfileRequest (IClientAPI sender, Packet pack)
        {
            var groupProfileRequest = (GroupProfileRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupProfileRequest.AgentData.SessionID != SessionId ||
                    groupProfileRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                GroupProfileReplyPacket groupProfileReply =
                    (GroupProfileReplyPacket)PacketPool.Instance.GetPacket (PacketType.GroupProfileReply);

                groupProfileReply.AgentData = new GroupProfileReplyPacket.AgentDataBlock ();
                groupProfileReply.GroupData = new GroupProfileReplyPacket.GroupDataBlock ();
                groupProfileReply.AgentData.AgentID = AgentId;

                GroupProfileData d = m_GroupsModule.GroupProfileRequest (this, groupProfileRequest.GroupData.GroupID);

                groupProfileReply.GroupData.GroupID = d.GroupID;
                groupProfileReply.GroupData.Name = Util.StringToBytes256 (d.Name);
                groupProfileReply.GroupData.Charter = Util.StringToBytes1024 (d.Charter);
                groupProfileReply.GroupData.ShowInList = d.ShowInList;
                groupProfileReply.GroupData.MemberTitle = Util.StringToBytes256 (d.MemberTitle);
                groupProfileReply.GroupData.PowersMask = d.PowersMask;
                groupProfileReply.GroupData.InsigniaID = d.InsigniaID;
                groupProfileReply.GroupData.FounderID = d.FounderID;
                groupProfileReply.GroupData.MembershipFee = d.MembershipFee;
                groupProfileReply.GroupData.OpenEnrollment = d.OpenEnrollment;
                groupProfileReply.GroupData.Money = d.Money;
                groupProfileReply.GroupData.GroupMembershipCount = d.GroupMembershipCount;
                groupProfileReply.GroupData.GroupRolesCount = d.GroupRolesCount;
                groupProfileReply.GroupData.AllowPublish = d.AllowPublish;
                groupProfileReply.GroupData.MaturePublish = d.MaturePublish;
                groupProfileReply.GroupData.OwnerRole = d.OwnerRole;

                OutPacket (groupProfileReply, ThrottleOutPacketType.Asset);
            }
            return true;
        }

        bool HandleGroupMembersRequest (IClientAPI sender, Packet pack)
        {
            var groupMembersRequestPacket = (GroupMembersRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupMembersRequestPacket.AgentData.SessionID != SessionId ||
                    groupMembersRequestPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                List<GroupMembersData> members =
                    m_GroupsModule.GroupMembersRequest (this, groupMembersRequestPacket.GroupData.GroupID);

                int memberCount = members.Count;

                while (true) {
                    int blockCount = members.Count;
                    if (blockCount > 40)
                        blockCount = 40;

                    GroupMembersReplyPacket groupMembersReply =
                        (GroupMembersReplyPacket)PacketPool.Instance.GetPacket (PacketType.GroupMembersReply);

                    groupMembersReply.AgentData = new GroupMembersReplyPacket.AgentDataBlock ();
                    groupMembersReply.GroupData = new GroupMembersReplyPacket.GroupDataBlock ();
                    groupMembersReply.MemberData = new GroupMembersReplyPacket.MemberDataBlock [blockCount];

                    groupMembersReply.AgentData.AgentID = AgentId;
                    groupMembersReply.GroupData.GroupID = groupMembersRequestPacket.GroupData.GroupID;
                    groupMembersReply.GroupData.RequestID = groupMembersRequestPacket.GroupData.RequestID;
                    groupMembersReply.GroupData.MemberCount = memberCount;

                    for (int i = 0; i < blockCount; i++) {
                        GroupMembersData m = members [0];
                        members.RemoveAt (0);

                        groupMembersReply.MemberData [i] =
                            new GroupMembersReplyPacket.MemberDataBlock {
                                AgentID = m.AgentID,
                                Contribution = m.Contribution,
                                OnlineStatus = Util.StringToBytes256 (m.OnlineStatus),
                                AgentPowers = m.AgentPowers,
                                Title = Util.StringToBytes256 (m.Title),
                                IsOwner = m.IsOwner
                            };
                    }
                    OutPacket (groupMembersReply, ThrottleOutPacketType.Asset);
                    if (members.Count == 0)
                        return true;
                }
            }
            return true;
        }

        bool HandleGroupRoleDataRequest (IClientAPI sender, Packet pack)
        {
            var groupRolesRequest = (GroupRoleDataRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupRolesRequest.AgentData.SessionID != SessionId ||
                    groupRolesRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                GroupRoleDataReplyPacket groupRolesReply =
                    (GroupRoleDataReplyPacket)PacketPool.Instance.GetPacket (PacketType.GroupRoleDataReply);

                groupRolesReply.AgentData = new GroupRoleDataReplyPacket.AgentDataBlock { AgentID = AgentId };

                groupRolesReply.GroupData =
                    new GroupRoleDataReplyPacket.GroupDataBlock {
                        GroupID = groupRolesRequest.GroupData.GroupID,
                        RequestID = groupRolesRequest.GroupData.RequestID
                    };


                List<GroupRolesData> titles = m_GroupsModule.GroupRoleDataRequest (this, groupRolesRequest.GroupData.GroupID);

                groupRolesReply.GroupData.RoleCount = titles.Count;

                groupRolesReply.RoleData = new GroupRoleDataReplyPacket.RoleDataBlock [titles.Count];

                int i = 0;
                foreach (GroupRolesData d in titles) {
                    groupRolesReply.RoleData [i] =
                        new GroupRoleDataReplyPacket.RoleDataBlock {
                            RoleID = d.RoleID,
                            Name = Util.StringToBytes256 (d.Name),
                            Title = Util.StringToBytes256 (d.Title),
                            Description = Util.StringToBytes1024 (d.Description),
                            Powers = d.Powers,
                            Members = (uint)d.Members
                        };


                    i++;
                }

                OutPacket (groupRolesReply, ThrottleOutPacketType.Asset);
            }
            return true;
        }

        bool HandleGroupRoleMembersRequest (IClientAPI sender, Packet pack)
        {
            var groupRoleMembersRequest = (GroupRoleMembersRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupRoleMembersRequest.AgentData.SessionID != SessionId ||
                    groupRoleMembersRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                List<GroupRoleMembersData> mappings =
                    m_GroupsModule.GroupRoleMembersRequest (this, groupRoleMembersRequest.GroupData.GroupID);

                int mappingsCount = mappings.Count;

                while (mappings.Count > 0) {
                    int pairs = mappings.Count;
                    if (pairs > 32)
                        pairs = 32;

                    GroupRoleMembersReplyPacket groupRoleMembersReply =
                        (GroupRoleMembersReplyPacket)PacketPool.Instance.GetPacket (PacketType.GroupRoleMembersReply);
                    groupRoleMembersReply.AgentData =
                        new GroupRoleMembersReplyPacket.AgentDataBlock {
                            AgentID = AgentId,
                            GroupID = groupRoleMembersRequest.GroupData.GroupID,
                            RequestID = groupRoleMembersRequest.GroupData.RequestID,
                            TotalPairs = (uint)mappingsCount
                        };


                    groupRoleMembersReply.MemberData =
                        new GroupRoleMembersReplyPacket.MemberDataBlock [pairs];

                    for (int i = 0; i < pairs; i++) {
                        GroupRoleMembersData d = mappings [0];
                        mappings.RemoveAt (0);

                        groupRoleMembersReply.MemberData [i] =
                            new GroupRoleMembersReplyPacket.MemberDataBlock { RoleID = d.RoleID, MemberID = d.MemberID };
                    }

                    OutPacket (groupRoleMembersReply, ThrottleOutPacketType.Asset);
                }
            }
            return true;
        }

        bool HandleCreateGroupRequest (IClientAPI sender, Packet pack)
        {
            var createGroupRequest = (CreateGroupRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (createGroupRequest.AgentData.SessionID != SessionId ||
                    createGroupRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                if (Utilities.IsSystemUser (AgentId)) {
                    SendAgentAlertMessage ("System users are for mainteneace tasks only!", false);
                    return false;
                }

                m_GroupsModule.CreateGroup (this,
                                            Utils.BytesToString (createGroupRequest.GroupData.Name),
                                            Utils.BytesToString (createGroupRequest.GroupData.Charter),
                                            createGroupRequest.GroupData.ShowInList,
                                            createGroupRequest.GroupData.InsigniaID,
                                            createGroupRequest.GroupData.MembershipFee,
                                            createGroupRequest.GroupData.OpenEnrollment,
                                            createGroupRequest.GroupData.AllowPublish,
                                            createGroupRequest.GroupData.MaturePublish);
            }
            return true;
        }

        bool HandleUpdateGroupInfo (IClientAPI sender, Packet pack)
        {
            var updateGroupInfo = (UpdateGroupInfoPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (updateGroupInfo.AgentData.SessionID != SessionId ||
                    updateGroupInfo.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                m_GroupsModule.UpdateGroupInfo (this,
                                                updateGroupInfo.GroupData.GroupID,
                                                Utils.BytesToString (updateGroupInfo.GroupData.Charter),
                                                updateGroupInfo.GroupData.ShowInList,
                                                updateGroupInfo.GroupData.InsigniaID,
                                                updateGroupInfo.GroupData.MembershipFee,
                                                updateGroupInfo.GroupData.OpenEnrollment,
                                                updateGroupInfo.GroupData.AllowPublish,
                                                updateGroupInfo.GroupData.MaturePublish);
            }

            return true;
        }

        bool HandleSetGroupAcceptNotices (IClientAPI sender, Packet pack)
        {
            var setGroupAcceptNotices = (SetGroupAcceptNoticesPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (setGroupAcceptNotices.AgentData.SessionID != SessionId ||
                    setGroupAcceptNotices.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                m_GroupsModule.SetGroupAcceptNotices (this,
                                                      setGroupAcceptNotices.Data.GroupID,
                                                      setGroupAcceptNotices.Data.AcceptNotices,
                                                      setGroupAcceptNotices.NewData.ListInProfile);
            }

            return true;
        }

        bool HandleGroupTitleUpdate (IClientAPI sender, Packet pack)
        {
            var groupTitleUpdate = (GroupTitleUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupTitleUpdate.AgentData.SessionID != SessionId ||
                    groupTitleUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                m_GroupsModule.GroupTitleUpdate (this,
                                                 groupTitleUpdate.AgentData.GroupID,
                                                 groupTitleUpdate.AgentData.TitleRoleID);
            }

            return true;
        }

        bool HandleParcelDeedToGroup (IClientAPI sender, Packet pack)
        {
            var parcelDeedToGroup = (ParcelDeedToGroupPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (parcelDeedToGroup.AgentData.SessionID != SessionId ||
                    parcelDeedToGroup.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                ParcelDeedToGroup handlerParcelDeedToGroup = OnParcelDeedToGroup;
                if (handlerParcelDeedToGroup != null) {
                    handlerParcelDeedToGroup (parcelDeedToGroup.Data.LocalID, parcelDeedToGroup.Data.GroupID, this);
                }
            }

            return true;
        }

        bool HandleGroupNoticesListRequest (IClientAPI sender, Packet pack)
        {
            var groupNoticesListRequest = (GroupNoticesListRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupNoticesListRequest.AgentData.SessionID != SessionId ||
                    groupNoticesListRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                GroupNoticeData [] gn =
                    m_GroupsModule.GroupNoticesListRequest (this, groupNoticesListRequest.Data.GroupID);

                GroupNoticesListReplyPacket groupNoticesListReply =
                    (GroupNoticesListReplyPacket)PacketPool.Instance.GetPacket (PacketType.GroupNoticesListReply);
                groupNoticesListReply.AgentData =
                    new GroupNoticesListReplyPacket.AgentDataBlock { AgentID = AgentId, GroupID = groupNoticesListRequest.Data.GroupID };

                groupNoticesListReply.Data = new GroupNoticesListReplyPacket.DataBlock [gn.Length];

                int i = 0;
                foreach (GroupNoticeData g in gn) {
                    groupNoticesListReply.Data [i] = new GroupNoticesListReplyPacket.DataBlock {
                        NoticeID = g.NoticeID,
                        Timestamp = g.Timestamp,
                        FromName = Util.StringToBytes256 (g.FromName),
                        Subject = Util.StringToBytes256 (g.Subject),
                        HasAttachment = g.HasAttachment,
                        AssetType = g.AssetType
                    };
                    i++;
                }

                OutPacket (groupNoticesListReply, ThrottleOutPacketType.Asset);
            }

            return true;
        }

        bool HandleGroupNoticeRequest (IClientAPI sender, Packet pack)
        {
            var groupNoticeRequest = (GroupNoticeRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupNoticeRequest.AgentData.SessionID != SessionId ||
                    groupNoticeRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                m_GroupsModule.GroupNoticeRequest (this, groupNoticeRequest.Data.GroupNoticeID);
            }
            return true;
        }

        bool HandleGroupRoleUpdate (IClientAPI sender, Packet pack)
        {
            var groupRoleUpdate = (GroupRoleUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupRoleUpdate.AgentData.SessionID != SessionId ||
                    groupRoleUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                foreach (GroupRoleUpdatePacket.RoleDataBlock d in
                    groupRoleUpdate.RoleData) {
                    m_GroupsModule.GroupRoleUpdate (this,
                                                    groupRoleUpdate.AgentData.GroupID,
                                                    d.RoleID,
                                                    Utils.BytesToString (d.Name),
                                                    Utils.BytesToString (d.Description),
                                                    Utils.BytesToString (d.Title),
                                                    d.Powers,
                                                    d.UpdateType);
                }
                m_GroupsModule.NotifyChange (groupRoleUpdate.AgentData.GroupID);
            }
            return true;
        }

        bool HandleGroupRoleChanges (IClientAPI sender, Packet pack)
        {
            var groupRoleChanges = (GroupRoleChangesPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (groupRoleChanges.AgentData.SessionID != SessionId ||
                    groupRoleChanges.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                foreach (GroupRoleChangesPacket.RoleChangeBlock d in
                    groupRoleChanges.RoleChange) {
                    m_GroupsModule.GroupRoleChanges (this,
                                                     groupRoleChanges.AgentData.GroupID,
                                                     d.RoleID,
                                                     d.MemberID,
                                                     d.Change);
                }
                m_GroupsModule.NotifyChange (groupRoleChanges.AgentData.GroupID);
            }
            return true;
        }

        bool HandleJoinGroupRequest (IClientAPI sender, Packet pack)
        {
            var joinGroupRequest = (JoinGroupRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (joinGroupRequest.AgentData.SessionID != SessionId ||
                    joinGroupRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                m_GroupsModule.JoinGroupRequest (this, joinGroupRequest.GroupData.GroupID);
            }
            return true;
        }

        bool HandleLeaveGroupRequest (IClientAPI sender, Packet pack)
        {
            var leaveGroupRequest = (LeaveGroupRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (leaveGroupRequest.AgentData.SessionID != SessionId ||
                    leaveGroupRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                m_GroupsModule.LeaveGroupRequest (this, leaveGroupRequest.GroupData.GroupID);
            }
            return true;
        }

        bool HandleEjectGroupMemberRequest (IClientAPI sender, Packet pack)
        {
            var ejectGroupMemberRequest = (EjectGroupMemberRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (ejectGroupMemberRequest.AgentData.SessionID != SessionId ||
                    ejectGroupMemberRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                foreach (EjectGroupMemberRequestPacket.EjectDataBlock e
                    in ejectGroupMemberRequest.EjectData) {
                    m_GroupsModule.EjectGroupMemberRequest (this,
                                                            ejectGroupMemberRequest.GroupData.GroupID,
                                                            e.EjecteeID);
                }
            }
            return true;
        }

        bool HandleInviteGroupRequest (IClientAPI sender, Packet pack)
        {
            var inviteGroupRequest = (InviteGroupRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (inviteGroupRequest.AgentData.SessionID != SessionId ||
                    inviteGroupRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (m_GroupsModule != null) {
                foreach (InviteGroupRequestPacket.InviteDataBlock b in
                    inviteGroupRequest.InviteData) {
                    m_GroupsModule.InviteGroupRequest (this,
                                                       inviteGroupRequest.GroupData.GroupID,
                                                       b.InviteeID,
                                                       b.RoleID);
                }
            }
            return true;
        }

        #endregion Groups

        bool HandleStartLure (IClientAPI sender, Packet pack)
        {
            var startLureRequest = (StartLurePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (startLureRequest.AgentData.SessionID != SessionId ||
                    startLureRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            StartLure handlerStartLure = OnStartLure;
            if (handlerStartLure != null)
                handlerStartLure (startLureRequest.Info.LureType,
                                  Utils.BytesToString (startLureRequest.Info.Message),
                                  startLureRequest.TargetData [0].TargetID,
                                  this);
            return true;
        }

        bool HandleTeleportLureRequest (IClientAPI sender, Packet pack)
        {
            var teleportLureRequest = (TeleportLureRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (teleportLureRequest.Info.SessionID != SessionId ||
                    teleportLureRequest.Info.AgentID != AgentId)
                    return true;
            }

            #endregion

            TeleportLureRequest handlerTeleportLureRequest = OnTeleportLureRequest;
            if (handlerTeleportLureRequest != null)
                handlerTeleportLureRequest (teleportLureRequest.Info.LureID,
                                            teleportLureRequest.Info.TeleportFlags,
                                            this);
            return true;
        }

        #region events/classified/picks

        bool HandleClassifiedInfoRequest (IClientAPI sender, Packet pack)
        {
            var classifiedInfoRequest = (ClassifiedInfoRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (classifiedInfoRequest.AgentData.SessionID != SessionId ||
                    classifiedInfoRequest.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ClassifiedInfoRequest handlerClassifiedInfoRequest = OnClassifiedInfoRequest;
            if (handlerClassifiedInfoRequest != null)
                handlerClassifiedInfoRequest (classifiedInfoRequest.Data.ClassifiedID, this);
            return true;
        }

        bool HandleClassifiedInfoUpdate (IClientAPI sender, Packet pack)
        {
            var classifiedInfoUpdate = (ClassifiedInfoUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (classifiedInfoUpdate.AgentData.SessionID != SessionId ||
                    classifiedInfoUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ClassifiedInfoUpdate handlerClassifiedInfoUpdate = OnClassifiedInfoUpdate;
            if (handlerClassifiedInfoUpdate != null)
                handlerClassifiedInfoUpdate (
                    classifiedInfoUpdate.Data.ClassifiedID,
                    classifiedInfoUpdate.Data.Category,
                    Utils.BytesToString (classifiedInfoUpdate.Data.Name),
                    Utils.BytesToString (classifiedInfoUpdate.Data.Desc),
                    classifiedInfoUpdate.Data.ParcelID,
                    classifiedInfoUpdate.Data.ParentEstate,
                    classifiedInfoUpdate.Data.SnapshotID,
                    new Vector3 (classifiedInfoUpdate.Data.PosGlobal),
                    classifiedInfoUpdate.Data.ClassifiedFlags,
                    classifiedInfoUpdate.Data.PriceForListing,
                    this);
            return true;
        }

        bool HandleClassifiedDelete (IClientAPI sender, Packet pack)
        {
            var classifiedDelete = (ClassifiedDeletePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (classifiedDelete.AgentData.SessionID != SessionId ||
                    classifiedDelete.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ClassifiedDelete handlerClassifiedDelete = OnClassifiedDelete;
            if (handlerClassifiedDelete != null)
                handlerClassifiedDelete (classifiedDelete.Data.ClassifiedID, this);
            return true;
        }

        bool HandleClassifiedGodDelete (IClientAPI sender, Packet pack)
        {
            var classifiedGodDelete = (ClassifiedGodDeletePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (classifiedGodDelete.AgentData.SessionID != SessionId ||
                    classifiedGodDelete.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ClassifiedDelete handlerClassifiedGodDelete = OnClassifiedGodDelete;
            if (handlerClassifiedGodDelete != null)
                handlerClassifiedGodDelete (classifiedGodDelete.Data.ClassifiedID, this);
            return true;
        }

        bool HandleEventGodDelete (IClientAPI sender, Packet pack)
        {
            var eventGodDelete = (EventGodDeletePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (eventGodDelete.AgentData.SessionID != SessionId ||
                    eventGodDelete.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            EventGodDelete handlerEventGodDelete = OnEventGodDelete;
            if (handlerEventGodDelete != null)
                handlerEventGodDelete (eventGodDelete.EventData.EventID,
                                       eventGodDelete.QueryData.QueryID,
                                       Utils.BytesToString (eventGodDelete.QueryData.QueryText),
                                       eventGodDelete.QueryData.QueryFlags,
                                       eventGodDelete.QueryData.QueryStart,
                                       this);
            return true;
        }

        bool HandleEventNotificationAddRequest (IClientAPI sender, Packet pack)
        {
            var eventNotificationAdd = (EventNotificationAddRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (eventNotificationAdd.AgentData.SessionID != SessionId ||
                    eventNotificationAdd.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            EventNotificationAddRequest handlerEventNotificationAddRequest = OnEventNotificationAddRequest;
            if (handlerEventNotificationAddRequest != null)
                handlerEventNotificationAddRequest (eventNotificationAdd.EventData.EventID, this);
            return true;
        }

        bool HandleEventNotificationRemoveRequest (IClientAPI sender, Packet pack)
        {
            var eventNotificationRemove = (EventNotificationRemoveRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (eventNotificationRemove.AgentData.SessionID != SessionId ||
                    eventNotificationRemove.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            EventNotificationRemoveRequest handlerEventNotificationRemoveRequest = OnEventNotificationRemoveRequest;
            if (handlerEventNotificationRemoveRequest != null)
                handlerEventNotificationRemoveRequest (eventNotificationRemove.EventData.EventID, this);
            return true;
        }

        bool HandleRetrieveInstantMessages (IClientAPI sender, Packet pack)
        {
            var rimpInstantMessagePack = (RetrieveInstantMessagesPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (rimpInstantMessagePack.AgentData.SessionID != SessionId ||
                    rimpInstantMessagePack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RetrieveInstantMessages handlerRetrieveInstantMessages = OnRetrieveInstantMessages;
            if (handlerRetrieveInstantMessages != null)
                handlerRetrieveInstantMessages (this);
            return true;
        }

        bool HandlePickDelete (IClientAPI sender, Packet pack)
        {
            var pickDelete = (PickDeletePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (pickDelete.AgentData.SessionID != SessionId ||
                    pickDelete.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            PickDelete handlerPickDelete = OnPickDelete;
            if (handlerPickDelete != null)
                handlerPickDelete (this, pickDelete.Data.PickID);
            return true;
        }

        bool HandlePickGodDelete (IClientAPI sender, Packet pack)
        {
            var pickGodDelete = (PickGodDeletePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (pickGodDelete.AgentData.SessionID != SessionId ||
                    pickGodDelete.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            PickGodDelete handlerPickGodDelete = OnPickGodDelete;
            if (handlerPickGodDelete != null)
                handlerPickGodDelete (this,
                                      pickGodDelete.AgentData.AgentID,
                                      pickGodDelete.Data.PickID,
                                      pickGodDelete.Data.QueryID);
            return true;
        }

        bool HandlePickInfoUpdate (IClientAPI sender, Packet pack)
        {
            var pickInfoUpdate = (PickInfoUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (pickInfoUpdate.AgentData.SessionID != SessionId ||
                    pickInfoUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            PickInfoUpdate handlerPickInfoUpdate = OnPickInfoUpdate;
            if (handlerPickInfoUpdate != null)
                handlerPickInfoUpdate (this,
                                       pickInfoUpdate.Data.PickID,
                                       pickInfoUpdate.Data.CreatorID,
                                       pickInfoUpdate.Data.TopPick,
                                       Utils.BytesToString (pickInfoUpdate.Data.Name),
                                       Utils.BytesToString (pickInfoUpdate.Data.Desc),
                                       pickInfoUpdate.Data.SnapshotID,
                                       pickInfoUpdate.Data.SortOrder,
                                       pickInfoUpdate.Data.Enabled,
                                       pickInfoUpdate.Data.PosGlobal);
            return true;
        }

        bool HandleAvatarNotesUpdate (IClientAPI sender, Packet pack)
        {
            var avatarNotesUpdate = (AvatarNotesUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (avatarNotesUpdate.AgentData.SessionID != SessionId ||
                    avatarNotesUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            AvatarNotesUpdate handlerAvatarNotesUpdate = OnAvatarNotesUpdate;
            if (handlerAvatarNotesUpdate != null)
                handlerAvatarNotesUpdate (this,
                                          avatarNotesUpdate.Data.TargetID,
                                          Utils.BytesToString (avatarNotesUpdate.Data.Notes));
            return true;
        }

        bool HandleAvatarInterestsUpdate (IClientAPI sender, Packet pack)
        {
            var avatarInterestUpdate = (AvatarInterestsUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (avatarInterestUpdate.AgentData.SessionID != SessionId ||
                    avatarInterestUpdate.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            AvatarInterestUpdate handlerAvatarInterestUpdate = OnAvatarInterestUpdate;
            if (handlerAvatarInterestUpdate != null)
                handlerAvatarInterestUpdate (this,
                                             avatarInterestUpdate.PropertiesData.WantToMask,
                                             Utils.BytesToString (avatarInterestUpdate.PropertiesData.WantToText),
                                             avatarInterestUpdate.PropertiesData.SkillsMask,
                                             Utils.BytesToString (avatarInterestUpdate.PropertiesData.SkillsText),
                                             Utils.BytesToString (avatarInterestUpdate.PropertiesData.LanguagesText));
            return true;
        }

        bool HandleGrantUserRights (IClientAPI sender, Packet pack)
        {
            var GrantUserRights = (GrantUserRightsPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (GrantUserRights.AgentData.SessionID != SessionId ||
                    GrantUserRights.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            GrantUserFriendRights GrantUserRightsHandler = OnGrantUserRights;
            if (GrantUserRightsHandler != null)
                GrantUserRightsHandler (this,
                                        GrantUserRights.AgentData.AgentID,
                                        GrantUserRights.Rights [0].AgentRelated,
                                        GrantUserRights.Rights [0].RelatedRights);
            return true;
        }

        bool HandlePlacesQuery (IClientAPI sender, Packet pack)
        {
            var placesQueryPacket = (PlacesQueryPacket)pack;

            PlacesQuery handlerPlacesQuery = OnPlacesQuery;

            if (handlerPlacesQuery != null)
                handlerPlacesQuery (placesQueryPacket.AgentData.QueryID,
                                    placesQueryPacket.TransactionData.TransactionID,
                                    Utils.BytesToString (placesQueryPacket.QueryData.QueryText),
                                    placesQueryPacket.QueryData.QueryFlags,
                                    (byte)placesQueryPacket.QueryData.Category,
                                    Utils.BytesToString (placesQueryPacket.QueryData.SimName),
                                    this);
            return true;
        }

        #endregion events/classified/picks

        void InitDefaultAnimations ()
        {
            using (XmlTextReader reader = new XmlTextReader ("data/avataranimations.xml")) {
                XmlDocument doc = new XmlDocument ();
                doc.Load (reader);
                if (doc.DocumentElement != null)
                    foreach (XmlNode nod in doc.DocumentElement.ChildNodes) {
                        if (nod.Attributes != null && nod.Attributes ["name"] != null) {
                            string name = nod.Attributes ["name"].Value.ToLower ();
                            string id = nod.InnerText;
                            m_defaultAnimations.Add (name, (UUID)id);
                        }
                    }
            }
        }

        public UUID GetDefaultAnimation (string name)
        {
            if (m_defaultAnimations.ContainsKey (name))
                return m_defaultAnimations [name];
            return UUID.Zero;
        }



        /// <summary>
        ///     Breaks down the genericMessagePacket into specific events
        /// </summary>
        /// <param name="gmMethod"></param>
        /// <param name="gmInvoice"></param>
        /// <param name="gmParams"></param>
        public void DecipherGenericMessage (string gmMethod, UUID gmInvoice,
                                           GenericMessagePacket.ParamListBlock [] gmParams)
        {
            switch (gmMethod) {
            case "autopilot":
                float locx;
                float locy;
                float locz;

                try {
                    uint regionX;
                    uint regionY;
                    Utils.LongToUInts (Scene.RegionInfo.RegionHandle, out regionX, out regionY);
                    locx = Convert.ToSingle (Utils.BytesToString (gmParams [0].Parameter)) - regionX;
                    locy = Convert.ToSingle (Utils.BytesToString (gmParams [1].Parameter)) - regionY;
                    locz = Convert.ToSingle (Utils.BytesToString (gmParams [2].Parameter));
                } catch (InvalidCastException) {
                    MainConsole.Instance.Error ("[Client]: Invalid autopilot request");
                    return;
                }

                UpdateVector handlerAutoPilotGo = OnAutoPilotGo;
                if (handlerAutoPilotGo != null) {
                    handlerAutoPilotGo (0, new Vector3 (locx, locy, locz), this);
                }
                MainConsole.Instance.InfoFormat ("[Client]: Client Requests autopilot to position <{0},{1},{2}>",
                                                locx, locy, locz);


                break;
            default:
                MainConsole.Instance.Debug ("[Client]: Unknown Generic Message, Method: " + gmMethod + ". Invoice: " +
                                           gmInvoice +
                                           ".  Dumping Params:");
                foreach (GenericMessagePacket.ParamListBlock t in gmParams) {
                    MainConsole.Instance.Debug (t.ToString ());
                }
                //gmpack.MethodData.
                break;
            }
        }


        static PrimitiveBaseShape GetShapeFromAddPacket (ObjectAddPacket addPacket)
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape {
                PCode = addPacket.ObjectData.PCode,
                State = addPacket.ObjectData.State,
                PathBegin = addPacket.ObjectData.PathBegin,
                PathEnd = addPacket.ObjectData.PathEnd,
                PathScaleX = addPacket.ObjectData.PathScaleX,
                PathScaleY = addPacket.ObjectData.PathScaleY,
                PathShearX = addPacket.ObjectData.PathShearX,
                PathShearY = addPacket.ObjectData.PathShearY,
                PathSkew = addPacket.ObjectData.PathSkew,
                ProfileBegin = addPacket.ObjectData.ProfileBegin,
                ProfileEnd = addPacket.ObjectData.ProfileEnd,
                Scale = addPacket.ObjectData.Scale,
                PathCurve = addPacket.ObjectData.PathCurve,
                ProfileCurve = addPacket.ObjectData.ProfileCurve,
                ProfileHollow = addPacket.ObjectData.ProfileHollow,
                PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset,
                PathRevolutions = addPacket.ObjectData.PathRevolutions,
                PathTaperX = addPacket.ObjectData.PathTaperX,
                PathTaperY = addPacket.ObjectData.PathTaperY,
                PathTwist = addPacket.ObjectData.PathTwist,
                PathTwistBegin = addPacket.ObjectData.PathTwistBegin
            };

            Primitive.TextureEntry ntex = new Primitive.TextureEntry (new UUID ("89556747-24cb-43ed-920b-47caed15465f"));
            shape.TextureEntry = ntex.GetBytes ();
            //shape.Textures = ntex;
            return shape;
        }

        #region Media Parcel Members

        public void SendParcelMediaCommand (uint flags, ParcelMediaCommandEnum command, float time)
        {
            var commandMessagePacket = new ParcelMediaCommandMessagePacket {
                CommandBlock = {
                    Flags = flags,
                    Command = (uint) command,
                    Time = time }
            };

            OutPacket (commandMessagePacket, ThrottleOutPacketType.Land);
        }

        public void SendParcelMediaUpdate (string mediaUrl, UUID mediaTextureID,
                                           byte autoScale, string mediaType, string mediaDesc, int mediaWidth,
                                           int mediaHeight, byte mediaLoop)
        {
            var updatePacket = new ParcelMediaUpdatePacket {
                DataBlock = {
                    MediaURL = Util.StringToBytes256(mediaUrl),
                    MediaID = mediaTextureID,
                    MediaAutoScale = autoScale
                },
                DataBlockExtended = {
                    MediaType = Util.StringToBytes256(mediaType),
                    MediaDesc = Util.StringToBytes256(mediaDesc),
                    MediaWidth = mediaWidth,
                    MediaHeight = mediaHeight,
                    MediaLoop = mediaLoop
                }
            };

            OutPacket (updatePacket, ThrottleOutPacketType.Land);
        }

        #endregion

        #region Camera

        public void SendSetFollowCamProperties (UUID objectID, SortedDictionary<int, float> parameters)
        {
            var packet =
                (SetFollowCamPropertiesPacket)PacketPool.Instance.GetPacket (PacketType.SetFollowCamProperties);
            packet.ObjectData.ObjectID = objectID;
            SetFollowCamPropertiesPacket.CameraPropertyBlock [] camPropBlock =
                new SetFollowCamPropertiesPacket.CameraPropertyBlock [parameters.Count];
            uint idx = 0;

            foreach (
                SetFollowCamPropertiesPacket.CameraPropertyBlock block in
                    parameters.Select (
                        pair =>
                        new SetFollowCamPropertiesPacket.CameraPropertyBlock { Type = pair.Key, Value = pair.Value })) {
                camPropBlock [idx++] = block;
            }

            packet.CameraProperty = camPropBlock;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendClearFollowCamProperties (UUID objectID)
        {
            var packet =
                (ClearFollowCamPropertiesPacket)PacketPool.Instance.GetPacket (PacketType.ClearFollowCamProperties);
            packet.ObjectData.ObjectID = objectID;
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        #endregion



        public static OSD BuildEvent (string eventName, OSD eventBody)
        {
            OSDMap osdEvent = new OSDMap (2) { { "message", new OSDString (eventName) }, { "body", eventBody } };

            return osdEvent;
        }

        public void SendAvatarInterestsReply (UUID avatarID, uint wantMask, string wantText, uint skillsMask,
                                              string skillsText, string languages)
        {
            AvatarInterestsReplyPacket packet =
                (AvatarInterestsReplyPacket)PacketPool.Instance.GetPacket (PacketType.AvatarInterestsReply);

            packet.AgentData = new AvatarInterestsReplyPacket.AgentDataBlock { AgentID = AgentId, AvatarID = avatarID };

            packet.PropertiesData = new AvatarInterestsReplyPacket.PropertiesDataBlock {
                WantToMask = wantMask,
                WantToText = Utils.StringToBytes (wantText),
                SkillsMask = skillsMask,
                SkillsText = Utils.StringToBytes (skillsText),
                LanguagesText = Utils.StringToBytes (languages)
            };
            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendChangeUserRights (UUID agentID, UUID friendID, int rights)
        {
            ChangeUserRightsPacket packet =
                (ChangeUserRightsPacket)PacketPool.Instance.GetPacket (PacketType.ChangeUserRights);

            packet.AgentData = new ChangeUserRightsPacket.AgentDataBlock { AgentID = agentID };

            packet.Rights = new ChangeUserRightsPacket.RightsBlock [1];
            packet.Rights [0] = new ChangeUserRightsPacket.RightsBlock { AgentRelated = friendID, RelatedRights = rights };

            OutPacket (packet, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendTextBoxRequest (string message, int chatChannel, string objectname, string ownerFirstName,
                                        string ownerLastName, UUID ownerID, UUID objectId)
        {
            ScriptDialogPacket dialog = (ScriptDialogPacket)PacketPool.Instance.GetPacket (PacketType.ScriptDialog);
            dialog.Data.ObjectID = objectId;
            dialog.Data.ChatChannel = chatChannel;
            dialog.Data.ImageID = UUID.Zero;
            dialog.Data.ObjectName = Util.StringToBytes256 (objectname);
            // this is the username of the *owner*
            dialog.Data.FirstName = Util.StringToBytes256 (ownerFirstName);
            dialog.Data.LastName = Util.StringToBytes256 (ownerLastName);
            dialog.Data.Message = Util.StringToBytes256 (message);

            ScriptDialogPacket.ButtonsBlock [] buttons = new ScriptDialogPacket.ButtonsBlock [1];
            buttons [0] = new ScriptDialogPacket.ButtonsBlock { ButtonLabel = Util.StringToBytes256 ("!!llTextBox!!") };
            dialog.OwnerData = new ScriptDialogPacket.OwnerDataBlock [1];
            dialog.OwnerData [0] = new ScriptDialogPacket.OwnerDataBlock ();
            dialog.OwnerData [0].OwnerID = ownerID;
            dialog.Buttons = buttons;
            OutPacket (dialog, ThrottleOutPacketType.AvatarInfo);
        }


        public void OnForceChatFromViewer (IClientAPI sender, OSChatMessage e)
        {
            OnChatFromClient (sender, e);
        }
    }
}
