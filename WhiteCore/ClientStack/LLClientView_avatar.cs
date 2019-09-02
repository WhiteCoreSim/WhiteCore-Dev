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
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;
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
        #region Appearance/ Wearables Methods

        public void SendWearables (AvatarWearable [] wearables, int serial)
        {
            AgentWearablesUpdatePacket aw =
                (AgentWearablesUpdatePacket)PacketPool.Instance.GetPacket (PacketType.AgentWearablesUpdate);
            aw.AgentData.AgentID = AgentId;
            aw.AgentData.SerialNum = (uint)serial;
            aw.AgentData.SessionID = m_sessionId;

            int count = wearables.Sum (t => t.Count);

            // TODO: don't create new blocks if recycling an old packet
            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock [count];
            int idx = 0;
            for (int i = 0; i < wearables.Length; i++) {
                for (int j = 0; j < wearables [i].Count; j++) {
                    AgentWearablesUpdatePacket.WearableDataBlock awb = new AgentWearablesUpdatePacket.WearableDataBlock {
                        WearableType = (byte)i,
                        AssetID = wearables [i] [j].AssetID,
                        ItemID = wearables [i] [j].ItemID
                    };
                    aw.WearableData [idx] = awb;
                    idx++;

                    //                                MainConsole.Instance.DebugFormat(
                    //                                    "[APPEARANCE]: Sending wearable item/asset {0} {1} (index {2}) for {3}",
                    //                                    awb.ItemID, awb.AssetID, i, Name);
                }
            }

            //            OutPacket(aw, ThrottleOutPacketType.Texture);
            OutPacket (aw, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAppearance (AvatarAppearance app)
        {
            AvatarAppearancePacket avp =
                (AvatarAppearancePacket)PacketPool.Instance.GetPacket (PacketType.AvatarAppearance);
            // TODO: don't create new blocks if recycling an old packet
            avp.VisualParam = new AvatarAppearancePacket.VisualParamBlock [app.VisualParams.Length];
            avp.ObjectData.TextureEntry = app.Texture.GetBytes ();
            for (int i = 0; i < app.VisualParams.Length; i++) {
                AvatarAppearancePacket.VisualParamBlock avblock = new AvatarAppearancePacket.VisualParamBlock {
                    ParamValue = app.VisualParams [i]
                };
                avp.VisualParam [i] = avblock;
            }
            avp.AppearanceData = new AvatarAppearancePacket.AppearanceDataBlock [1]
                                     {
                                         new AvatarAppearancePacket.AppearanceDataBlock()
                                         {
                                             CofVersion = app.Serial,
                                             AppearanceVersion = (int)RegionProtocols.None
                                         }
                                     };
            avp.AppearanceHover = new AvatarAppearancePacket.AppearanceHoverBlock [1] {
                new AvatarAppearancePacket.AppearanceHoverBlock () {
                    HoverHeight = Vector3.Zero
                }
            };

            avp.Sender.IsTrial = false;
            avp.Sender.ID = app.Owner;
            //MainConsole.Instance.InfoFormat("[Client]: Sending appearance for {0} to {1}", agentID.ToString(), AgentId.ToString());
            //            OutPacket(avp, ThrottleOutPacketType.Texture);
            OutPacket (avp, ThrottleOutPacketType.AvatarInfo);
        }

        public void SendAnimations (AnimationGroup animations)
        {
            //MainConsole.Instance.DebugFormat("[Client]: Sending animations to {0}", Name);

            AvatarAnimationPacket ani =
                (AvatarAnimationPacket)PacketPool.Instance.GetPacket (PacketType.AvatarAnimation);
            // TODO: don't create new blocks if recycling an old packet
            ani.AnimationSourceList = new AvatarAnimationPacket.AnimationSourceListBlock [animations.Animations.Length];
            ani.Sender = new AvatarAnimationPacket.SenderBlock { ID = animations.AvatarID };
            ani.AnimationList = new AvatarAnimationPacket.AnimationListBlock [animations.Animations.Length];
            ani.PhysicalAvatarEventList = new AvatarAnimationPacket.PhysicalAvatarEventListBlock [1];
            ani.PhysicalAvatarEventList [0] = new AvatarAnimationPacket.PhysicalAvatarEventListBlock { TypeData = new byte [0] };

            for (int i = 0; i < animations.Animations.Length; ++i) {
                ani.AnimationList [i] = new AvatarAnimationPacket.AnimationListBlock {
                    AnimID = animations.Animations [i],
                    AnimSequenceID = animations.SequenceNums [i] + ((i + 1) * 2)
                };

                ani.AnimationSourceList [i] = new AvatarAnimationPacket.AnimationSourceListBlock { ObjectID = animations.ObjectIDs [i] };
                //if (objectIDs[i] == UUID.Zero)
                //    ani.AnimationSourceList[i].ObjectID = sourceAgentId;
            }
            //We do this here to keep the numbers under control
            m_animationSequenceNumber += (animations.Animations.Length * 2);

            ani.Header.Reliable = true;
            ani.HasVariableBlocks = false;
            //            OutPacket(ani, ThrottleOutPacketType.Asset);
            OutPacket (ani, ThrottleOutPacketType.AvatarInfo, true, null,
                      delegate { m_scene.GetScenePresence (AgentId).SceneViewer.FinishedAnimationPacketSend (animations); });
        }

        #endregion

        #region Avatar Packet/Data Sending Methods

        /// <summary>
        ///     Send an ObjectUpdate packet with information about an avatar
        /// </summary>
        public void SendAvatarDataImmediate (IEntity avatar)
        {
            IScenePresence presence = avatar as IScenePresence;
            if (presence == null || presence.IsChildAgent)
                return;

            ObjectUpdatePacket objupdate = (ObjectUpdatePacket)PacketPool.Instance.GetPacket (PacketType.ObjectUpdate);
            objupdate.Header.Zerocoded = true;

            objupdate.RegionData.RegionHandle = presence.Scene.RegionInfo.RegionHandle;
            float TIME_DILATION = presence.Scene.TimeDilation;
            ushort timeDilation = Utils.FloatToUInt16 (TIME_DILATION, 0.0f, 1.0f);

            objupdate.RegionData.TimeDilation = timeDilation;
            objupdate.ObjectData = new ObjectUpdatePacket.ObjectDataBlock [1];
            objupdate.ObjectData [0] = CreateAvatarUpdateBlock (presence);

            OutPacket (objupdate, ThrottleOutPacketType.OutBand);
        }

        public void SendCoarseLocationUpdate (List<UUID> users, List<Vector3> CoarseLocations)
        {
            if (!IsActive) return; // We don't need to update inactive clients.

            CoarseLocationUpdatePacket loc =
                (CoarseLocationUpdatePacket)PacketPool.Instance.GetPacket (PacketType.CoarseLocationUpdate);
            loc.Header.Reliable = false;

            // Each packet can only hold around 60 avatar positions and the client clears the mini-map each time
            // a CoarseLocationUpdate packet is received. Oh well.
            int total = Math.Min (CoarseLocations.Count, 60);

            CoarseLocationUpdatePacket.IndexBlock ib = new CoarseLocationUpdatePacket.IndexBlock ();

            loc.Location = new CoarseLocationUpdatePacket.LocationBlock [total];
            loc.AgentData = new CoarseLocationUpdatePacket.AgentDataBlock [total];

            int selfindex = -1;
            for (int i = 0; i < total; i++) {
                CoarseLocationUpdatePacket.LocationBlock lb =
                    new CoarseLocationUpdatePacket.LocationBlock {
                        X = (byte)CoarseLocations [i].X,
                        Y = (byte)CoarseLocations [i].Y,
                        Z = CoarseLocations [i].Z > 1024 ? (byte)0 : (byte)(CoarseLocations [i].Z * 0.25f)
                    };

                loc.Location [i] = lb;
                loc.AgentData [i] = new CoarseLocationUpdatePacket.AgentDataBlock { AgentID = users [i] };
                if (users [i] == AgentId)
                    selfindex = i;
            }

            ib.You = (short)selfindex;
            ib.Prey = -1;
            loc.Index = ib;

            OutPacket (loc, ThrottleOutPacketType.AvatarInfo);
        }

        #endregion Avatar Packet/Data Sending Methods

        #region Primitive Packet/Data Sending Methods

        /// <summary>
        ///     Generate one of the object update packets based on PrimUpdateFlags
        ///     and broadcast the packet to clients
        /// </summary>
        /// again  presences update preiority was lost. recovering it  fast and dirty
        public void SendAvatarUpdate (IEnumerable<EntityUpdate> updates)
        {
            Framework.Utilities.Lazy<List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>> terseUpdateBlocks =
                new Framework.Utilities.Lazy<List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>> ();
            List<EntityUpdate> terseUpdates = new List<EntityUpdate> ();

            foreach (EntityUpdate update in updates) {
                terseUpdates.Add (update);
                terseUpdateBlocks.Value.Add (CreateImprovedTerseBlock (update.Entity,
                                                                       update.Flags.HasFlag (PrimUpdateFlags.Textures)));
            }

            ushort timeDilation = Utils.FloatToUInt16 (m_scene.TimeDilation, 0.0f, 1.0f);

            if (terseUpdateBlocks.IsValueCreated) {
                List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock> blocks = terseUpdateBlocks.Value;

                ImprovedTerseObjectUpdatePacket packet = new ImprovedTerseObjectUpdatePacket {
                    RegionData = {RegionHandle = m_scene.RegionInfo.RegionHandle, TimeDilation = timeDilation},
                    ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock [blocks.Count]
                };

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData [i] = blocks [i];

                OutPacket (packet, ThrottleOutPacketType.AvatarInfo, true,
                          p => ResendPrimUpdates (terseUpdates, p),
                          delegate {
                              IScenePresence presence = m_scene.GetScenePresence (AgentId);
                              if (presence != null)
                                  presence.SceneViewer.FinishedEntityPacketSend (terseUpdates);
                          });
            }
        }

        public void SendPrimUpdate (IEnumerable<EntityUpdate> updates)
        {
            Framework.Utilities.Lazy<List<ObjectUpdatePacket.ObjectDataBlock>> objectUpdateBlocks =
                new Framework.Utilities.Lazy<List<ObjectUpdatePacket.ObjectDataBlock>> ();
            Framework.Utilities.Lazy<List<ObjectUpdateCompressedPacket.ObjectDataBlock>> compressedUpdateBlocks =
                new Framework.Utilities.Lazy<List<ObjectUpdateCompressedPacket.ObjectDataBlock>> ();
            Framework.Utilities.Lazy<List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>> terseUpdateBlocks =
                new Framework.Utilities.Lazy<List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>> ();
            Framework.Utilities.Lazy<List<ObjectUpdateCachedPacket.ObjectDataBlock>> cachedUpdateBlocks =
                new Framework.Utilities.Lazy<List<ObjectUpdateCachedPacket.ObjectDataBlock>> ();
            List<EntityUpdate> fullUpdates = new List<EntityUpdate> ();
            List<EntityUpdate> compressedUpdates = new List<EntityUpdate> ();
            List<EntityUpdate> cachedUpdates = new List<EntityUpdate> ();
            List<EntityUpdate> terseUpdates = new List<EntityUpdate> ();

            foreach (EntityUpdate update in updates) {
                IEntity entity = update.Entity;
                PrimUpdateFlags updateFlags = update.Flags;

                bool canUseCompressed = true;
                bool canUseImproved = true;
                bool canUseCached = false;
                //Not possible at the moment without more viewer work... the viewer does some odd things with this

                IObjectCache module = Scene.RequestModuleInterface<IObjectCache> ();
                bool isTerse = updateFlags.HasFlag ((PrimUpdateFlags.TerseUpdate)) &&
                               !updateFlags.HasFlag (PrimUpdateFlags.FullUpdate) &&
                               !updateFlags.HasFlag (PrimUpdateFlags.ForcedFullUpdate);
                // Compressed and cached object updates only make sense for LL primitives
                if (entity is ISceneChildEntity) {
                    // Please do not remove this unless you can demonstrate on the mailing list that a client
                    // will never receive an update after a prim kill.  Even then, keeping the kill record may be a good
                    // safety measure.
                    //
                    // If a Linden Lab 1.23.5 client (and possibly later and earlier) receives an object update
                    // after a kill, it will keep displaying the deleted object until relog.  OpenSim currently performs
                    // updates and kills on different threads with different scheduling strategies, hence this protection.
                    //
                    // This doesn't appear to apply to child prims - a client will happily ignore these updates
                    // after the root prim has been deleted.
                    /*if (m_killRecord.Contains(entity.LocalId))
                        {
                        MainConsole.Instance.ErrorFormat(
                            "[Client]: Preventing update for prim with local id {0} after client for user {1} told it was deleted. Mantis this at http://mantis.WhiteCore-sim.org/bug_report_page.php !",
                            entity.LocalId, Name);
                        return;
                        }*/
                    ISceneChildEntity ent = (ISceneChildEntity)entity;
                    if (ent.Shape.PCode == 9 && ent.Shape.State != 0) {
                        //Don't send hud attachments to other avatars except for the owner
                        byte state = ent.Shape.State;
                        if ((state == (byte)AttachmentPoint.HUDBottom ||
                             state == (byte)AttachmentPoint.HUDBottomLeft ||
                             state == (byte)AttachmentPoint.HUDBottomRight ||
                             state == (byte)AttachmentPoint.HUDCenter ||
                             state == (byte)AttachmentPoint.HUDCenter2 ||
                             state == (byte)AttachmentPoint.HUDTop ||
                             state == (byte)AttachmentPoint.HUDTopLeft ||
                             state == (byte)AttachmentPoint.HUDTopRight)
                            && ent.OwnerID != AgentId)
                            continue;
                    }
                    if (updateFlags != PrimUpdateFlags.TerseUpdate && ent.ParentEntity.SitTargetAvatar.Count > 0) {
                        isTerse = false;
                        updateFlags = PrimUpdateFlags.ForcedFullUpdate;
                    }

                    if (canUseCached && !isTerse && module != null)
                        canUseCached = module.UseCachedObject (AgentId, entity.LocalId, ent.CRC);
                    else
                        //No cache module? Don't use cached then, or it won't stop sending ObjectUpdateCached even when the client requests prims
                        canUseCached = false;
                }

                if (updateFlags.HasFlag (PrimUpdateFlags.FullUpdate)) {
                    canUseCompressed = false;
                    canUseImproved = false;
                } else if (updateFlags.HasFlag (PrimUpdateFlags.ForcedFullUpdate)) {
                    // If a full update has been requested, DO THE FULL UPDATE.
                    // Don't try to get out of this.... the monster called RepeatObjectUpdateCachedFromTheServer will occur and eat all your prims!
                    canUseCached = false;
                    canUseCompressed = false;
                    canUseImproved = false;
                } else {
                    if (updateFlags.HasFlag (PrimUpdateFlags.Velocity) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Acceleration) ||
                        updateFlags.HasFlag (PrimUpdateFlags.CollisionPlane) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Joint) ||
                        updateFlags.HasFlag (PrimUpdateFlags.AngularVelocity)) {
                        canUseCompressed = false;
                    }

                    if (updateFlags.HasFlag (PrimUpdateFlags.PrimFlags) ||
                        updateFlags.HasFlag (PrimUpdateFlags.ParentID) ||
                        updateFlags.HasFlag (PrimUpdateFlags.AttachmentPoint) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Shape) ||
                        updateFlags.HasFlag (PrimUpdateFlags.PrimData) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Text) ||
                        updateFlags.HasFlag (PrimUpdateFlags.NameValue) ||
                        updateFlags.HasFlag (PrimUpdateFlags.ExtraData) ||
                        updateFlags.HasFlag (PrimUpdateFlags.TextureAnim) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Sound) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Particles) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Material) ||
                        updateFlags.HasFlag (PrimUpdateFlags.ClickAction) ||
                        updateFlags.HasFlag (PrimUpdateFlags.MediaURL) ||
                        updateFlags.HasFlag (PrimUpdateFlags.Joint) ||
                        updateFlags.HasFlag (PrimUpdateFlags.FindBest)) {
                        canUseImproved = false;
                    }
                }

                // Do NOT send cached updates for terse updates
                // ONLY send full updates for attachments unless you want to figure out all the little screwy things with sending compressed updates and attachments
                if (entity is ISceneChildEntity && ((ISceneChildEntity)entity).IsAttachment) {
                    canUseCached = false;
                    canUseImproved = false;
                    canUseCompressed = false;
                }

                // let's do it... 
                try {

                    if (canUseCached && !isTerse) {
                        cachedUpdates.Add (update);
                        cachedUpdateBlocks.Value.Add (CreatePrimCachedUpdateBlock ((ISceneChildEntity)entity, m_agentId));
                    } else if (!canUseImproved && !canUseCompressed) {
                        fullUpdates.Add (update);
                        if (entity is IScenePresence) {
                            objectUpdateBlocks.Value.Add (CreateAvatarUpdateBlock ((IScenePresence)entity));
                        } else {
                            objectUpdateBlocks.Value.Add (CreatePrimUpdateBlock ((ISceneChildEntity)entity, m_agentId));
                        }
                    } else if (!canUseImproved) {
                        ISceneChildEntity cEntity = (ISceneChildEntity)entity;
                        compressedUpdates.Add (update);
                        //We are sending a compressed, which the client will save, add it to the cache
                        if (module != null)
                            module.AddCachedObject (AgentId, entity.LocalId, cEntity.CRC);
                        CompressedFlags Flags = CompressedFlags.None;
                        if (updateFlags == PrimUpdateFlags.FullUpdate || updateFlags == PrimUpdateFlags.FindBest) {
                            //Add the defaults
                            updateFlags = PrimUpdateFlags.None;
                        }

                        updateFlags |= PrimUpdateFlags.ClickAction;
                        updateFlags |= PrimUpdateFlags.ExtraData;
                        updateFlags |= PrimUpdateFlags.Shape;
                        updateFlags |= PrimUpdateFlags.Material;
                        updateFlags |= PrimUpdateFlags.Textures;
                        updateFlags |= PrimUpdateFlags.Rotation;
                        updateFlags |= PrimUpdateFlags.PrimFlags;
                        updateFlags |= PrimUpdateFlags.Position;
                        updateFlags |= PrimUpdateFlags.AngularVelocity;

                        //Must send these as well
                        if (cEntity.Text != "")
                            updateFlags |= PrimUpdateFlags.Text;
                        if (cEntity.AngularVelocity != Vector3.Zero)
                            updateFlags |= PrimUpdateFlags.AngularVelocity;
                        if (cEntity.TextureAnimation != null && cEntity.TextureAnimation.Length != 0)
                            updateFlags |= PrimUpdateFlags.TextureAnim;
                        if (cEntity.Sound != UUID.Zero)
                            updateFlags |= PrimUpdateFlags.Sound;
                        if (cEntity.ParticleSystem != null && cEntity.ParticleSystem.Length != 0)
                            updateFlags |= PrimUpdateFlags.Particles;
                        if (!string.IsNullOrEmpty (cEntity.MediaUrl))
                            updateFlags |= PrimUpdateFlags.MediaURL;
                        if (cEntity.ParentEntity.RootChild.IsAttachment)
                            updateFlags |= PrimUpdateFlags.AttachmentPoint;

                        //Make sure that we send this! Otherwise, the client will only see one prim
                        if (cEntity.ParentEntity != null)
                            if (cEntity.ParentEntity.ChildrenEntities ().Count != 1)
                                updateFlags |= PrimUpdateFlags.ParentID;

                        if (updateFlags.HasFlag (PrimUpdateFlags.Text) && cEntity.Text == "")
                            updateFlags &= ~PrimUpdateFlags.Text; //Remove the text flag if we don't have text!

                        if (updateFlags.HasFlag (PrimUpdateFlags.AngularVelocity))
                            Flags |= CompressedFlags.HasAngularVelocity;
                        if (updateFlags.HasFlag (PrimUpdateFlags.MediaURL))
                            Flags |= CompressedFlags.MediaURL;
                        if (updateFlags.HasFlag (PrimUpdateFlags.ParentID))
                            Flags |= CompressedFlags.HasParent;
                        if (updateFlags.HasFlag (PrimUpdateFlags.Particles))
                            Flags |= CompressedFlags.HasParticles;
                        if (updateFlags.HasFlag (PrimUpdateFlags.Sound))
                            Flags |= CompressedFlags.HasSound;
                        if (updateFlags.HasFlag (PrimUpdateFlags.Text))
                            Flags |= CompressedFlags.HasText;
                        if (updateFlags.HasFlag (PrimUpdateFlags.TextureAnim))
                            Flags |= CompressedFlags.TextureAnimation;
                        if (updateFlags.HasFlag (PrimUpdateFlags.NameValue) || cEntity.IsAttachment)
                            Flags |= CompressedFlags.HasNameValues;

                        compressedUpdates.Add (update);
                        compressedUpdateBlocks.Value.Add (CreateCompressedUpdateBlock ((ISceneChildEntity)entity,
                                                                                       Flags,
                                                                                       updateFlags));
                    } else {
                        terseUpdates.Add (update);
                        terseUpdateBlocks.Value.Add (CreateImprovedTerseBlock (entity,
                                                                               updateFlags.HasFlag (PrimUpdateFlags.Textures)));
                    }
                } catch (Exception ex) {
                    MainConsole.Instance.Warn ("[Client]: Issue creating an update block " + ex);
                    return;
                }
            }

            ushort timeDilation = Utils.FloatToUInt16 (m_scene.TimeDilation, 0.0f, 1.0f);

            //
            // NOTE: These packets ARE being sent as Unknown for a reason
            //        This method is ONLY being called by the SceneViewer, which is being called by
            //        the LLUDPClient, which is attempting to send these packets out, they just have to 
            //        be created. So instead of sending them as task (which puts them back in the queue),
            //        we send them out immediately, as this is on a seperate thread anyway.
            //
            // SECOND NOTE: These packets are back as Task for now... we shouldn't send them out as unknown
            //        as we cannot be sure that the UDP server is ready for us to send them, so we will
            //        requeue them... even though we probably could send them out fine.
            //

            if (objectUpdateBlocks.IsValueCreated) {
                List<ObjectUpdatePacket.ObjectDataBlock> blocks = objectUpdateBlocks.Value;

                ObjectUpdatePacket packet = (ObjectUpdatePacket)PacketPool.Instance.GetPacket (PacketType.ObjectUpdate);
                packet.RegionData.RegionHandle = m_scene.RegionInfo.RegionHandle;
                packet.RegionData.TimeDilation = timeDilation;
                packet.ObjectData = new ObjectUpdatePacket.ObjectDataBlock [blocks.Count];

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData [i] = blocks [i];


                //ObjectUpdatePacket oo = new ObjectUpdatePacket(packet.ToBytes(), ref ii);

                OutPacket (packet, ThrottleOutPacketType.Task, true,
                          p => ResendPrimUpdates (fullUpdates, p),
                          delegate {
                              IScenePresence presence = m_scene.GetScenePresence (AgentId);
                              if (presence != null)
                                  presence.SceneViewer.FinishedEntityPacketSend (fullUpdates);
                          });
            }

            if (compressedUpdateBlocks.IsValueCreated) {
                List<ObjectUpdateCompressedPacket.ObjectDataBlock> blocks = compressedUpdateBlocks.Value;

                ObjectUpdateCompressedPacket packet =
                    (ObjectUpdateCompressedPacket)PacketPool.Instance.GetPacket (PacketType.ObjectUpdateCompressed);
                packet.RegionData.RegionHandle = m_scene.RegionInfo.RegionHandle;
                packet.RegionData.TimeDilation = timeDilation;
                packet.ObjectData = new ObjectUpdateCompressedPacket.ObjectDataBlock [blocks.Count];
                packet.Type = PacketType.ObjectUpdate;

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData [i] = blocks [i];

                OutPacket (packet, ThrottleOutPacketType.Task, true,
                          p => ResendPrimUpdates (compressedUpdates, p),
                          delegate {
                              IScenePresence presence = m_scene.GetScenePresence (AgentId);
                              if (presence != null)
                                  presence.SceneViewer.FinishedEntityPacketSend (compressedUpdates);
                          });
            }

            if (cachedUpdateBlocks.IsValueCreated) {
                List<ObjectUpdateCachedPacket.ObjectDataBlock> blocks = cachedUpdateBlocks.Value;

                ObjectUpdateCachedPacket packet =
                    (ObjectUpdateCachedPacket)PacketPool.Instance.GetPacket (PacketType.ObjectUpdateCached);
                packet.RegionData.RegionHandle = m_scene.RegionInfo.RegionHandle;
                packet.RegionData.TimeDilation = timeDilation;
                packet.ObjectData = new ObjectUpdateCachedPacket.ObjectDataBlock [blocks.Count];

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData [i] = blocks [i];

                OutPacket (packet, ThrottleOutPacketType.Task, true,
                          p => ResendPrimUpdates (cachedUpdates, p),
                          delegate {
                              IScenePresence presence = m_scene.GetScenePresence (AgentId);
                              if (presence != null)
                                  presence.SceneViewer.FinishedEntityPacketSend (cachedUpdates);
                          });
            }

            if (terseUpdateBlocks.IsValueCreated) {
                List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock> blocks = terseUpdateBlocks.Value;

                ImprovedTerseObjectUpdatePacket packet = new ImprovedTerseObjectUpdatePacket {
                    RegionData = {RegionHandle = m_scene.RegionInfo.RegionHandle, TimeDilation = timeDilation},
                    ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock [blocks.Count]
                };

                for (int i = 0; i < blocks.Count; i++)
                    packet.ObjectData [i] = blocks [i];

                OutPacket (packet, ThrottleOutPacketType.Task, true,
                          p => ResendPrimUpdates (terseUpdates, p),
                          delegate {
                              IScenePresence presence = m_scene.GetScenePresence (AgentId);
                              if (presence != null)
                                  presence.SceneViewer.FinishedEntityPacketSend (terseUpdates);
                          });
            }
        }

        void ResendPrimUpdates (IEnumerable<EntityUpdate> updates, OutgoingPacket oPacket)
        {
            // Remove the update packet from the list of packets waiting for acknowledgement
            // because we are requeuing the list of updates. They will be resent in new packets
            // with the most recent state and priority.
            m_udpClient.NeedAcks.Remove (oPacket.SequenceNumber);

            // Count this as a resent packet since we are going to requeue all of the updates contained in it
            Interlocked.Increment (ref m_udpClient.PacketsResent);

            IScenePresence sp = m_scene.GetScenePresence (AgentId);
            if (sp != null) {
                ISceneViewer viewer = sp.SceneViewer;
                foreach (EntityUpdate update in updates) {
                    if (update.Entity is ISceneChildEntity)
                        viewer.QueuePartForUpdate ((ISceneChildEntity)update.Entity, update.Flags);
                    else
                        viewer.QueuePresenceForUpdate ((IScenePresence)update.Entity, update.Flags);
                }
            }
        }

        public void DequeueUpdates (int nprimdates, int navadates)
        {
            IScenePresence sp = m_scene.GetScenePresence (AgentId);
            if (sp != null) {
                ISceneViewer viewer = sp.SceneViewer;
                viewer.SendPrimUpdates (nprimdates, navadates);
            }
        }

        #endregion Primitive Packet/Data Sending Methods



        void HandleQueueEmpty (object o)
        {
            // arraytmp  0 contains current number of packets in task
            // arraytmp  1 contains current number of packets in avatarinfo
            // arraytmp  2 contains current number of packets in texture

            int [] arraytmp = (int [])o;
            int ptmp = m_udpServer.PrimUpdatesPerCallback - arraytmp [0];
            int atmp = m_udpServer.AvatarUpdatesPerCallBack - arraytmp [1];

            if (ptmp < 0)
                ptmp = 0;
            if (atmp < 0)
                atmp = 0;

            if (ptmp + atmp != 0)
                DequeueUpdates (ptmp, atmp);

            if (m_udpServer.TextureSendLimit > arraytmp [2])
                ProcessTextureRequests (m_udpServer.TextureSendLimit);
        }

        void ProcessTextureRequests (int numPackets)
        {
            //note: tmp is never used
            //int tmp = m_udpClient.GetCurTexPacksInQueue();
            if (m_imageManager != null)
                m_imageManager.ProcessImageQueue (numPackets);
        }

        public void SendAssetUploadCompleteMessage (sbyte AssetType, bool Success, UUID AssetFullID)
        {
            AssetUploadCompletePacket newPack = new AssetUploadCompletePacket {
                AssetBlock = {Type = AssetType, Success = Success, UUID = AssetFullID},
                Header = { Zerocoded = true }
            };
            OutPacket (newPack, ThrottleOutPacketType.Asset);
        }

        public void SendXferRequest (ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte [] FileName)
        {
            RequestXferPacket newPack = new RequestXferPacket {
                XferID = { ID = XferID,
                           VFileType = AssetType,
                           VFileID = vFileID,
                           FilePath = FilePath,
                           Filename = FileName },
                Header = { Zerocoded = true }
            };
            OutPacket (newPack, ThrottleOutPacketType.Transfer);
        }

        public void SendConfirmXfer (ulong xferID, uint PacketID)
        {
            ConfirmXferPacketPacket newPack = new ConfirmXferPacketPacket {
                XferID = { ID = xferID, Packet = PacketID },
                Header = { Zerocoded = true }
            };
            OutPacket (newPack, ThrottleOutPacketType.Transfer);
        }

        public void SendInitiateDownload (string simFileName, string clientFileName)
        {
            InitiateDownloadPacket newPack = new InitiateDownloadPacket {
                AgentData = { AgentID = AgentId },
                FileData = { SimFilename = Utils.StringToBytes(simFileName),
                             ViewerFilename = Utils.StringToBytes(clientFileName) }
            };
            OutPacket (newPack, ThrottleOutPacketType.Transfer);
        }

        public void SendImageFirstPart (
            ushort numParts, UUID ImageUUID, uint ImageSize, byte [] ImageData, byte imageCodec)
        {
            ImageDataPacket im = new ImageDataPacket { Header = { Reliable = false }, ImageID = { Packets = numParts, ID = ImageUUID } };

            if (ImageSize > 0)
                im.ImageID.Size = ImageSize;

            im.ImageData.Data = ImageData;
            im.ImageID.Codec = imageCodec;
            im.Header.Zerocoded = true;
            OutPacket (im, ThrottleOutPacketType.Texture);
        }

        public void SendImageNextPart (ushort partNumber, UUID imageUuid, byte [] imageData)
        {
            ImagePacketPacket im = new ImagePacketPacket {
                Header = { Reliable = false },
                ImageID = { Packet = partNumber, ID = imageUuid },
                ImageData = { Data = imageData }
            };

            OutPacket (im, ThrottleOutPacketType.Texture);
        }

        public void SendImageNotFound (UUID imageid)
        {
            ImageNotInDatabasePacket notFoundPacket
                = (ImageNotInDatabasePacket)PacketPool.Instance.GetPacket (PacketType.ImageNotInDatabase);

            notFoundPacket.ImageID.ID = imageid;

            OutPacket (notFoundPacket, ThrottleOutPacketType.Texture);
        }

        volatile bool m_sendingSimStatsPacket;

        public void SendSimStats (SimStats stats)
        {
            if (m_sendingSimStatsPacket)
                return;

            m_sendingSimStatsPacket = true;

            SimStatsPacket pack = new SimStatsPacket {
                Region = stats.RegionBlock,
                RegionInfo = new SimStatsPacket.RegionInfoBlock [1] {
                                                      new SimStatsPacket.RegionInfoBlock() {
                                                              RegionFlagsExtended = stats.RegionBlock.RegionFlags }
                                                      },
                Stat = stats.StatsBlock,
                Header = { Reliable = false }
            };


            OutPacket (pack, ThrottleOutPacketType.Task, true, null,
                      delegate { m_sendingSimStatsPacket = false; });
        }

        public void SendObjectPropertiesFamilyData (uint requestFlags, UUID objectUUID, UUID ownerID, UUID groupID,
                                                    uint baseMask, uint ownerMask, uint groupMask, uint everyoneMask,
                                                    uint nextOwnerMask, int ownershipCost, byte saleType, int salePrice,
                                                    uint category,
                                                    UUID lastOwnerID, string objectName, string description)
        {
            ObjectPropertiesFamilyPacket objPropFamilyPack =
                (ObjectPropertiesFamilyPacket)PacketPool.Instance.GetPacket (PacketType.ObjectPropertiesFamily);
            // TODO: don't create new blocks if recycling an old packet

            ObjectPropertiesFamilyPacket.ObjectDataBlock objPropDB = new ObjectPropertiesFamilyPacket.ObjectDataBlock {
                RequestFlags = requestFlags,
                ObjectID = objectUUID,
                OwnerID = ownerID == groupID ? UUID.Zero : ownerID,
                GroupID = groupID,
                BaseMask = baseMask,
                OwnerMask = ownerMask,
                GroupMask = groupMask,
                EveryoneMask = everyoneMask,
                NextOwnerMask = nextOwnerMask,
                OwnershipCost = ownershipCost,
                SaleType = saleType,
                SalePrice = salePrice,
                Category = category,
                LastOwnerID = lastOwnerID,
                Name = Util.StringToBytes256 (objectName),
                Description = Util.StringToBytes256 (description)
            };

            objPropFamilyPack.ObjectData = objPropDB;
            objPropFamilyPack.Header.Zerocoded = true;
            objPropFamilyPack.HasVariableBlocks = false;
            OutPacket (objPropFamilyPack, ThrottleOutPacketType.Task);
        }

        public void SendObjectPropertiesReply (List<IEntity> parts)
        {
            //ObjectPropertiesPacket proper = (ObjectPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.ObjectProperties);
            // TODO: don't create new blocks if recycling an old packet

            //Theres automatic splitting, just let it go on through
            ObjectPropertiesPacket proper =
                (ObjectPropertiesPacket)PacketPool.Instance.GetPacket (PacketType.ObjectProperties);

            proper.ObjectData =
                parts.OfType<ISceneChildEntity> ()
                     .Select (entity => entity as ISceneChildEntity)
                     .Select (part => new ObjectPropertiesPacket.ObjectDataBlock {
                         ItemID = part.FromUserInventoryItemID,
                         CreationDate = (ulong)part.CreationDate * 1000000,
                         CreatorID = part.CreatorID,
                         FolderID = UUID.Zero,
                         FromTaskID = UUID.Zero,
                         GroupID = part.GroupID,
                         InventorySerial = (short)part.InventorySerial,
                         LastOwnerID = part.LastOwnerID,
                         ObjectID = part.UUID,
                         OwnerID = part.OwnerID == part.GroupID ? UUID.Zero : part.OwnerID,
                         TouchName = Util.StringToBytes256 (part.ParentEntity.RootChild.TouchName),
                         TextureID = new byte [0],
                         SitName = Util.StringToBytes256 (part.ParentEntity.RootChild.SitName),
                         Name = Util.StringToBytes256 (part.Name),
                         Description = Util.StringToBytes256 (part.Description),
                         OwnerMask = part.ParentEntity.RootChild.OwnerMask,
                         NextOwnerMask = part.ParentEntity.RootChild.NextOwnerMask,
                         GroupMask = part.ParentEntity.RootChild.GroupMask,
                         EveryoneMask = part.ParentEntity.RootChild.EveryoneMask,
                         BaseMask = part.ParentEntity.RootChild.BaseMask,
                         SaleType = part.ParentEntity.RootChild.ObjectSaleType,
                         SalePrice = part.ParentEntity.RootChild.SalePrice
                     }).ToArray ();

            proper.Header.Zerocoded = true;
            bool hasFinishedSending = false; //Since this packet will be split up, we only want to finish sending once
            OutPacket (proper, ThrottleOutPacketType.State, true, null, delegate {
                if (hasFinishedSending)
                    return;
                hasFinishedSending = true;
                m_scene.GetScenePresence (AgentId).SceneViewer.FinishedPropertyPacketSend (parts);
            });
        }

        #region Scene/Avatar

        bool HandleAgentUpdate (IClientAPI sender, Packet pack)
        {
            if (OnAgentUpdate != null) {
                bool update = false;
                //bool forcedUpdate = false;
                var agenUpdate = (AgentUpdatePacket)pack;

                #region Packet Session and User Check

                if (agenUpdate.AgentData.SessionID != SessionId || agenUpdate.AgentData.AgentID != AgentId)
                    return false;

                #endregion

                AgentUpdatePacket.AgentDataBlock x = agenUpdate.AgentData;

                // We can only check when we have something to check
                // against.

                if (lastarg != null) {
                    update =
                        (
                            (x.BodyRotation != lastarg.BodyRotation) ||
                            (x.CameraAtAxis != lastarg.CameraAtAxis) ||
                            (x.CameraCenter != lastarg.CameraCenter) ||
                            (x.CameraLeftAxis != lastarg.CameraLeftAxis) ||
                            (x.CameraUpAxis != lastarg.CameraUpAxis) ||
                            (x.ControlFlags != lastarg.ControlFlags) ||
                            (x.Flags != lastarg.Flags) ||
                            (x.State != lastarg.State) ||
                            (x.HeadRotation != lastarg.HeadRotation) ||
                            (x.SessionID != lastarg.SessionID) ||
                            (x.AgentID != lastarg.AgentID) ||
                            !Util.ApproxEqual (x.Far, lastarg.Far)       // was (x.Far != lastarg.Far)
                        );
                } else {
                    //forcedUpdate = true;
                    update = true;
                }

                // These should be ordered from most-likely to
                // least likely to change. I've made an initial
                // guess at that.

                if (update) {
                    AgentUpdateArgs arg = new AgentUpdateArgs {
                        AgentID = x.AgentID,
                        BodyRotation = x.BodyRotation,
                        CameraAtAxis = x.CameraAtAxis,
                        CameraCenter = x.CameraCenter,
                        CameraLeftAxis = x.CameraLeftAxis,
                        CameraUpAxis = x.CameraUpAxis,
                        ControlFlags = x.ControlFlags,
                        Far = x.Far,
                        Flags = x.Flags,
                        HeadRotation = x.HeadRotation,
                        SessionID = x.SessionID,
                        State = x.State
                    };
                    UpdateAgent handlerAgentUpdate = OnAgentUpdate;
                    lastarg = arg; // save this set of arguments for nexttime
                    if (handlerAgentUpdate != null)
                        OnAgentUpdate (this, arg);

                    handlerAgentUpdate = null;
                }
            }

            return true;
        }

        bool HandleMoneyTransferRequest (IClientAPI sender, Packet pack)
        {
            var money = (MoneyTransferRequestPacket)pack;
            // validate the agent owns the agentID and sessionID
            if (money.MoneyData.SourceID == sender.AgentId && money.AgentData.AgentID == sender.AgentId &&
                money.AgentData.SessionID == sender.SessionId) {
                MoneyTransferRequest handlerMoneyTransferRequest = OnMoneyTransferRequest;
                if (handlerMoneyTransferRequest != null) {
                    handlerMoneyTransferRequest (money.MoneyData.SourceID,
                                                 money.MoneyData.DestID,
                                                 money.MoneyData.Amount,
                                                 money.MoneyData.TransactionType,
                                                 Util.FieldToString (money.MoneyData.Description));
                }

                return true;
            }

            return false;
        }

        bool HandleParcelGodMarkAsContent (IClientAPI client, Packet pack)
        {
            var parcelGodMarkAsContent = (ParcelGodMarkAsContentPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (parcelGodMarkAsContent.AgentData.SessionID != SessionId ||
                    parcelGodMarkAsContent.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelGodMark parcelGodMarkAsContentHandler = OnParcelGodMark;
            if (parcelGodMarkAsContentHandler != null) {
                parcelGodMarkAsContentHandler (this,
                                              parcelGodMarkAsContent.AgentData.AgentID,
                                              parcelGodMarkAsContent.ParcelData.LocalID);
                return true;
            }
            return false;
        }

        bool HandleFreezeUser (IClientAPI client, Packet pack)
        {
            var freezeUser = (FreezeUserPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (freezeUser.AgentData.SessionID != SessionId || freezeUser.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            FreezeUserUpdate freezeUserHandler = OnParcelFreezeUser;
            if (freezeUserHandler != null) {
                freezeUserHandler (this,
                                  freezeUser.AgentData.AgentID,
                                  freezeUser.Data.Flags,
                                  freezeUser.Data.TargetID);
                return true;
            }
            return false;
        }

        bool HandleEjectUser (IClientAPI client, Packet pack)
        {
            var ejectUser = (EjectUserPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (ejectUser.AgentData.SessionID != SessionId || ejectUser.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            EjectUserUpdate ejectUserHandler = OnParcelEjectUser;
            if (ejectUserHandler != null) {
                ejectUserHandler (this,
                                 ejectUser.AgentData.AgentID,
                                 ejectUser.Data.Flags,
                                 ejectUser.Data.TargetID);
                return true;
            }
            return false;
        }

        bool HandleParcelBuyPass (IClientAPI client, Packet pack)
        {
            var parcelBuyPass = (ParcelBuyPassPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (parcelBuyPass.AgentData.SessionID != SessionId || parcelBuyPass.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            ParcelBuyPass parcelBuyPassHandler = OnParcelBuyPass;
            if (parcelBuyPassHandler != null) {
                parcelBuyPassHandler (this,
                                     parcelBuyPass.AgentData.AgentID,
                                     parcelBuyPass.ParcelData.LocalID);
                return true;
            }
            return false;
        }

        bool HandleParcelBuyRequest (IClientAPI sender, Packet pack)
        {
            var parcel = (ParcelBuyPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (parcel.AgentData.SessionID != SessionId || parcel.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (parcel.AgentData.AgentID == AgentId && parcel.AgentData.SessionID == SessionId) {
                ParcelBuy handlerParcelBuy = OnParcelBuy;
                if (handlerParcelBuy != null) {
                    handlerParcelBuy (parcel.AgentData.AgentID,
                                      parcel.Data.GroupID,
                                      parcel.Data.Final,
                                      parcel.Data.IsGroupOwned,
                                      parcel.Data.RemoveContribution,
                                      parcel.Data.LocalID,
                                      parcel.ParcelData.Area,
                                      parcel.ParcelData.Price,
                                      false);
                }
                return true;
            }
            return false;
        }

        bool HandleUUIDGroupNameRequest (IClientAPI sender, Packet pack)
        {
            var upack = (UUIDGroupNameRequestPacket)pack;

            foreach (UUIDGroupNameRequestPacket.UUIDNameBlockBlock t in upack.UUIDNameBlock) {
                UUIDNameRequest handlerUUIDGroupNameRequest = OnUUIDGroupNameRequest;
                if (handlerUUIDGroupNameRequest != null) {
                    handlerUUIDGroupNameRequest (t.ID, this);
                }
            }

            return true;
        }

        public bool HandleGenericMessage (IClientAPI sender, Packet pack)
        {
            var gmpack = (GenericMessagePacket)pack;
            if (m_genericPacketHandlers.Count == 0) return false;
            if (gmpack.AgentData.SessionID != SessionId) return false;

            GenericMessage handlerGenericMessage = null;

            string method = Util.FieldToString (gmpack.MethodData.Method).ToLower ().Trim ();

            if (m_genericPacketHandlers.TryGetValue (method, out handlerGenericMessage)) {
                var msg = new List<string> ();
                var msgBytes = new List<byte []> ();

                if (handlerGenericMessage != null) {
                    foreach (GenericMessagePacket.ParamListBlock block in gmpack.ParamList) {
                        msg.Add (Util.FieldToString (block.Parameter));
                        msgBytes.Add (block.Parameter);
                    }
                    try {
                        if (OnBinaryGenericMessage != null) {
                            OnBinaryGenericMessage (this, method, msgBytes.ToArray ());
                        }
                        handlerGenericMessage (sender, method, msg);
                        return true;
                    } catch (Exception e) {
                        MainConsole.Instance.ErrorFormat (
                            "[Client]: Exception when handling generic message {0}{1}", e.Message, e.StackTrace);
                    }
                }
            }

            //MainConsole.Instance.Debug("[Client]: Not handling GenericMessage with method-type of: " + method);
            return false;
        }

        public bool HandleObjectGroupRequest (IClientAPI sender, Packet pack)
        {
            var ogpack = (ObjectGroupPacket)pack;
            if (ogpack.AgentData.SessionID != SessionId) return false;

            RequestObjectPropertiesFamily handlerObjectGroupRequest = OnObjectGroupRequest;
            if (handlerObjectGroupRequest != null) {
                foreach (ObjectGroupPacket.ObjectDataBlock t in ogpack.ObjectData) {
                    handlerObjectGroupRequest (this, ogpack.AgentData.GroupID, t.ObjectLocalID, UUID.Zero);
                }
            }
            return true;
        }

        bool HandleViewerEffect (IClientAPI sender, Packet pack)
        {
            var viewer = (ViewerEffectPacket)pack;
            if (viewer.AgentData.SessionID != SessionId) return false;
            ViewerEffectEventHandler handlerViewerEffect = OnViewerEffect;
            if (handlerViewerEffect != null) {
                int length = viewer.Effect.Length;
                List<ViewerEffectEventHandlerArg> args = new List<ViewerEffectEventHandlerArg> (length);
                for (int i = 0; i < length; i++) {
                    //copy the effects block arguments into the event handler arg.
                    ViewerEffectEventHandlerArg argument = new ViewerEffectEventHandlerArg {
                        AgentID = viewer.Effect [i].AgentID,
                        Color = viewer.Effect [i].Color,
                        Duration = viewer.Effect [i].Duration,
                        ID = viewer.Effect [i].ID,
                        Type = viewer.Effect [i].Type,
                        TypeData = viewer.Effect [i].TypeData
                    };
                    args.Add (argument);
                }

                handlerViewerEffect (sender, args);
            }

            return true;
        }

        bool HandleAvatarPropertiesRequest (IClientAPI sender, Packet pack)
        {
            var avatarProperties = (AvatarPropertiesRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (avatarProperties.AgentData.SessionID != SessionId || avatarProperties.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RequestAvatarProperties handlerRequestAvatarProperties = OnRequestAvatarProperties;
            if (handlerRequestAvatarProperties != null) {
                handlerRequestAvatarProperties (this, avatarProperties.AgentData.AvatarID);
            }
            return true;
        }

        bool HandleChatFromViewer (IClientAPI sender, Packet pack)
        {
            ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (inchatpack.AgentData.SessionID != SessionId || inchatpack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            string fromName = string.Empty; //ClientAvatar.firstname + " " + ClientAvatar.lastname;
            byte [] message = inchatpack.ChatData.Message;
            byte type = inchatpack.ChatData.Type;
            Vector3 fromPos = new Vector3 (); // ClientAvatar.Pos;
                                              // UUID fromAgentID = AgentId;

            int channel = inchatpack.ChatData.Channel;

            if (OnChatFromClient != null) {
                OSChatMessage args = new OSChatMessage {
                    Channel = channel,
                    From = fromName,
                    Message = Utils.BytesToString (message),
                    Type = (ChatTypeEnum)type,
                    Position = fromPos,
                    Scene = Scene,
                    Sender = this,
                    SenderUUID = AgentId
                };

                HandleChatFromClient (args);
            }
            return true;
        }

        public void HandleChatFromClient (OSChatMessage args)
        {
            ChatMessage handlerChatFromClient = OnChatFromClient;
            if (handlerChatFromClient != null)
                handlerChatFromClient (this, args);
        }

        bool HandlerAvatarPropertiesUpdate (IClientAPI sender, Packet pack)
        {
            var avatarProps = (AvatarPropertiesUpdatePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (avatarProps.AgentData.SessionID != SessionId || avatarProps.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            UpdateAvatarProperties handlerUpdateAvatarProperties = OnUpdateAvatarProperties;
            if (handlerUpdateAvatarProperties != null) {
                AvatarPropertiesUpdatePacket.PropertiesDataBlock Properties = avatarProps.PropertiesData;

                handlerUpdateAvatarProperties (this,
                                              Utils.BytesToString (Properties.AboutText),
                                              Utils.BytesToString (Properties.FLAboutText),
                                              Properties.FLImageID,
                                              Properties.ImageID,
                                              Utils.BytesToString (Properties.ProfileURL),
                                              Properties.AllowPublish,
                                              Properties.MaturePublish);
            }
            return true;
        }

        bool HandlerScriptDialogReply (IClientAPI sender, Packet pack)
        {
            var rdialog = (ScriptDialogReplyPacket)pack;

            //MainConsole.Instance.DebugFormat("[Client]: Received ScriptDialogReply from {0}", rdialog.Data.ObjectID);

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (rdialog.AgentData.SessionID != SessionId || rdialog.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            int ch = rdialog.Data.ChatChannel;
            byte [] msg = rdialog.Data.ButtonLabel;
            if (OnChatFromClient != null) {
                OSChatMessage args = new OSChatMessage {
                    Channel = ch,
                    From = string.Empty,
                    Message = Utils.BytesToString (msg),
                    Type = ChatTypeEnum.Shout,
                    Position = new Vector3 (),
                    Scene = Scene,
                    Sender = this
                };
                ChatMessage handlerChatFromClient2 = OnChatFromClient;
                if (handlerChatFromClient2 != null)
                    handlerChatFromClient2 (this, args);
            }

            return true;
        }

        bool HandlerImprovedInstantMessage (IClientAPI sender, Packet pack)
        {
            var msgpack = (ImprovedInstantMessagePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (msgpack.AgentData.SessionID != SessionId || msgpack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            string IMfromName = Util.FieldToString (msgpack.MessageBlock.FromAgentName);
            string IMmessage = Utils.BytesToString (msgpack.MessageBlock.Message);

            GridInstantMessage im = new GridInstantMessage () {
                RegionID = Scene.RegionInfo.RegionID,
                FromAgentID = msgpack.AgentData.AgentID,
                FromAgentName = IMfromName,
                ToAgentID = msgpack.MessageBlock.ToAgentID,
                Dialog = msgpack.MessageBlock.Dialog,
                FromGroup = msgpack.MessageBlock.FromGroup,
                Message = IMmessage,
                SessionID = msgpack.MessageBlock.ID,
                Offline = msgpack.MessageBlock.Offline,
                Position = msgpack.MessageBlock.Position,
                BinaryBucket = msgpack.MessageBlock.BinaryBucket
            };
            IncomingInstantMessage (im);
            return true;
        }

        public void IncomingInstantMessage (GridInstantMessage im)
        {
            PreSendImprovedInstantMessage handlerPreSendInstantMessage = OnPreSendInstantMessage;
            if (handlerPreSendInstantMessage != null) {
                if (handlerPreSendInstantMessage.GetInvocationList ().Cast<PreSendImprovedInstantMessage> ().Any (
                    d => d (this, im))) {
                    return; //handled
                }
            }
            ImprovedInstantMessage handlerInstantMessage = OnInstantMessage;
            if (handlerInstantMessage != null)
                handlerInstantMessage (this, im);
        }

        bool HandlerAcceptFriendship (IClientAPI sender, Packet pack)
        {
            var afriendpack = (AcceptFriendshipPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (afriendpack.AgentData.SessionID != SessionId || afriendpack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            // My guess is this is the folder to stick the calling card into

            UUID agentID = afriendpack.AgentData.AgentID;
            UUID transactionID = afriendpack.TransactionBlock.TransactionID;

            List<UUID> callingCardFolders = afriendpack.FolderData.Select (t => t.FolderID).ToList ();

            FriendActionDelegate handlerApproveFriendRequest = OnApproveFriendRequest;
            if (handlerApproveFriendRequest != null) {
                handlerApproveFriendRequest (this, agentID, transactionID, callingCardFolders);
            }
            return true;
        }

        bool HandlerDeclineFriendship (IClientAPI sender, Packet pack)
        {
            var dfriendpack = (DeclineFriendshipPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dfriendpack.AgentData.SessionID != SessionId || dfriendpack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (OnDenyFriendRequest != null) {
                OnDenyFriendRequest (this,
                                    dfriendpack.AgentData.AgentID,
                                    dfriendpack.TransactionBlock.TransactionID,
                                    null);
            }
            return true;
        }

        bool HandlerTerminateFrendship (IClientAPI sender, Packet pack)
        {
            var tfriendpack = (TerminateFriendshipPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (tfriendpack.AgentData.SessionID != SessionId || tfriendpack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            UUID listOwnerAgentID = tfriendpack.AgentData.AgentID;
            UUID exFriendID = tfriendpack.ExBlock.OtherID;

            FriendshipTermination handlerTerminateFriendship = OnTerminateFriendship;
            if (handlerTerminateFriendship != null) {
                handlerTerminateFriendship (this, listOwnerAgentID, exFriendID);
            }
            return true;
        }

        bool HandleFindAgent (IClientAPI client, Packet pack)
        {
            var FindAgent = (FindAgentPacket)pack;

            FindAgentUpdate FindAgentHandler = OnFindAgent;
            if (FindAgentHandler != null) {
                FindAgentHandler (this, FindAgent.AgentBlock.Hunter, FindAgent.AgentBlock.Prey);
                return true;
            }
            return false;
        }

        bool HandleTrackAgent (IClientAPI client, Packet pack)
        {
            var TrackAgent = (TrackAgentPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (TrackAgent.AgentData.SessionID != SessionId || TrackAgent.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            TrackAgentUpdate TrackAgentHandler = OnTrackAgent;
            if (TrackAgentHandler != null) {
                TrackAgentHandler (this,
                                  TrackAgent.AgentData.AgentID,
                                  TrackAgent.TargetData.PreyID);
                return true;
            }
            return false;
        }

        bool HandlerRezObject (IClientAPI sender, Packet pack)
        {
            var rezPacket = (RezObjectPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (rezPacket.AgentData.SessionID != SessionId || rezPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RezObject handlerRezObject = OnRezObject;
            if (handlerRezObject != null) {
                handlerRezObject (this,
                                  rezPacket.InventoryData.ItemID,
                                  rezPacket.RezData.RayEnd,
                                  rezPacket.RezData.RayStart,
                                  rezPacket.RezData.RayTargetID,
                                  rezPacket.RezData.BypassRaycast,
                                  rezPacket.RezData.RayEndIsIntersection,
                                  rezPacket.RezData.RezSelected,
                                  rezPacket.RezData.RemoveItem,
                                  rezPacket.RezData.FromTaskID);
            }
            return true;
        }

        bool HandlerRezObjectFromNotecard (IClientAPI sender, Packet pack)
        {
            var rezPacket = (RezObjectFromNotecardPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (rezPacket.AgentData.SessionID != SessionId || rezPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            RezObject handlerRezObject = OnRezObject;
            if (handlerRezObject != null) {
                handlerRezObject (this,
                                  rezPacket.InventoryData [0].ItemID,
                                  rezPacket.RezData.RayEnd,
                                  rezPacket.RezData.RayStart,
                                  rezPacket.RezData.RayTargetID,
                                  rezPacket.RezData.BypassRaycast,
                                  rezPacket.RezData.RayEndIsIntersection,
                                  rezPacket.RezData.RezSelected,
                                  rezPacket.RezData.RemoveItem,
                                  rezPacket.RezData.FromTaskID);
            }
            return true;
        }

        bool HandlerDeRezObject (IClientAPI sender, Packet pack)
        {
            var DeRezPacket = (DeRezObjectPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (DeRezPacket.AgentData.SessionID != SessionId || DeRezPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            DeRezObject handlerDeRezObject = OnDeRezObject;
            if (handlerDeRezObject != null) {
                List<uint> deRezIDs = DeRezPacket.ObjectData.Select (data => data.ObjectLocalID).ToList ();

                // It just so happens that the values on the DeRezAction enumerator match the Destination
                // values given by a Second Life client
                handlerDeRezObject (this, 
                                    deRezIDs,
                                    DeRezPacket.AgentBlock.GroupID,
                                    (DeRezAction)DeRezPacket.AgentBlock.Destination,
                                    DeRezPacket.AgentBlock.DestinationID);
            }
            return true;
        }

        bool HandlerModifyLand (IClientAPI sender, Packet pack)
        {
            var modify = (ModifyLandPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (modify.AgentData.SessionID != SessionId || modify.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            //MainConsole.Instance.Info("[LAND]: LAND:" + modify.ToString());
            if (modify.ParcelData.Length > 0) {
                if (OnModifyTerrain != null) {
                    for (int i = 0; i < modify.ParcelData.Length; i++) {
                        ModifyTerrain handlerModifyTerrain = OnModifyTerrain;
                        if (handlerModifyTerrain != null) {
                            handlerModifyTerrain (AgentId,
                                                  modify.ModifyBlock.Height,
                                                  modify.ModifyBlock.Seconds,
                                                  modify.ModifyBlock.BrushSize,
                                                  modify.ModifyBlock.Action,
                                                  modify.ParcelData [i].North,
                                                  modify.ParcelData [i].West,
                                                  modify.ParcelData [i].South,
                                                  modify.ParcelData [i].East,
                                                  AgentId,
                                                  modify.ModifyBlockExtended [i].BrushSize);
                        }
                    }
                }
            }

            return true;
        }

        bool HandlerRegionHandshakeReply (IClientAPI sender, Packet pack)
        {
            Action<IClientAPI> handlerRegionHandShakeReply = OnRegionHandShakeReply;
            if (handlerRegionHandShakeReply != null) {
                handlerRegionHandShakeReply (this);
            }

            return true;
        }

        bool HandlerAgentWearablesRequest (IClientAPI sender, Packet pack)
        {
            GenericCall1 handlerRequestWearables = OnRequestWearables;

            if (handlerRequestWearables != null) {
                handlerRequestWearables (this);
            }

            Action<IClientAPI> handlerRequestAvatarsData = OnRequestAvatarsData;

            if (handlerRequestAvatarsData != null) {
                handlerRequestAvatarsData (this);
            }

            return true;
        }

        bool HandlerAgentSetAppearance (IClientAPI sender, Packet pack)
        {
            var appear = (AgentSetAppearancePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (appear.AgentData.SessionID != SessionId || appear.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SetAppearance handlerSetAppearance = OnSetAppearance;
            if (handlerSetAppearance != null) {
                // Temporarily protect ourselves from the mantis #951 failure.
                // However, we could do this for several other handlers where a failure isn't terminal
                // for the client session anyway, in order to protect ourselves against bad code in plugins
                try {
                    byte [] visualparams = new byte [appear.VisualParam.Length];
                    for (int i = 0; i < appear.VisualParam.Length; i++)
                        visualparams [i] = appear.VisualParam [i].ParamValue;

                    Primitive.TextureEntry te = null;
                    if (appear.ObjectData.TextureEntry.Length > 1)
                        te = new Primitive.TextureEntry (appear.ObjectData.TextureEntry, 0,
                                                        appear.ObjectData.TextureEntry.Length);

                    WearableCache [] items = new WearableCache [appear.WearableData.Length];
                    for (int i = 0; i < appear.WearableData.Length; i++) {
                        var cache = new WearableCache {
                            CacheID = appear.WearableData [i].CacheID,
                            TextureIndex = appear.WearableData [i].TextureIndex
                        };
                        items [i] = cache;
                    }
                    handlerSetAppearance (this, te, visualparams, items, appear.AgentData.SerialNum);
                } catch (Exception e) {
                    MainConsole.Instance.ErrorFormat (
                        "[Client]: AgentSetApperance packet handler threw an exception, {0}",
                        e);
                }
            }

            return true;
        }

        /// <summary>
        ///     Send a response back to a client when it asks the asset server (via the region server) if it has
        ///     its appearance texture cached.
        /// </summary>
        /// <param name="simclient"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        bool HandleAgentTextureCached (IClientAPI simclient, Packet packet)
        {
            var cachedtex = (AgentCachedTexturePacket)packet;

            if (cachedtex.AgentData.SessionID != SessionId) return false;

            List<CachedAgentArgs> args =
                cachedtex.WearableData.Select (t => new CachedAgentArgs { ID = t.ID, TextureIndex = t.TextureIndex }).
                          ToList ();

            AgentCachedTextureRequest actr = OnAgentCachedTextureRequest;
            if (actr != null)
                actr (this, args);

            return true;
        }

        public void SendAgentCachedTexture (List<CachedAgentArgs> args)
        {
            var cachedresp =
                (AgentCachedTextureResponsePacket)PacketPool.Instance.GetPacket (PacketType.AgentCachedTextureResponse);
            cachedresp.AgentData.AgentID = AgentId;
            cachedresp.AgentData.SessionID = m_sessionId;
            cachedresp.AgentData.SerialNum = m_cachedTextureSerial;
            m_cachedTextureSerial++;
            cachedresp.WearableData = new AgentCachedTextureResponsePacket.WearableDataBlock [args.Count];

            for (int i = 0; i < args.Count; i++) {
                cachedresp.WearableData [i] = new AgentCachedTextureResponsePacket.WearableDataBlock {
                    TextureIndex = args [i].TextureIndex,
                    TextureID = args [i].ID,
                    HostName = new byte [0]
                };
            }

            cachedresp.Header.Zerocoded = true;
            OutPacket (cachedresp, ThrottleOutPacketType.Texture);
        }

        bool HandlerAgentIsNowWearing (IClientAPI sender, Packet pack)
        {
            if (OnAvatarNowWearing != null) {
                var nowWearing = (AgentIsNowWearingPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (nowWearing.AgentData.SessionID != SessionId || nowWearing.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                var wearingArgs = new AvatarWearingArgs ();

                foreach (
                    AvatarWearingArgs.Wearable wearable in
                        nowWearing.WearableData.Select (t => new AvatarWearingArgs.Wearable (t.ItemID,
                                                                                             t.WearableType))) {
                    wearingArgs.NowWearing.Add (wearable);
                }

                AvatarNowWearing handlerAvatarNowWearing = OnAvatarNowWearing;
                if (handlerAvatarNowWearing != null) {
                    handlerAvatarNowWearing (this, wearingArgs);
                }
            }
            return true;
        }

        bool HandlerRezSingleAttachmentFromInv (IClientAPI sender, Packet pack)
        {
            RezSingleAttachmentFromInv handlerRezSingleAttachment = OnRezSingleAttachmentFromInv;
            if (handlerRezSingleAttachment != null) {
                RezSingleAttachmentFromInvPacket rez = (RezSingleAttachmentFromInvPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (rez.AgentData.SessionID != SessionId || rez.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                handlerRezSingleAttachment (this, rez.ObjectData.ItemID, rez.ObjectData.AttachmentPt);
            }

            return true;
        }

        /* original - assumed all objects were attachments
         bool HandlerRezRestoreToWorld(IClientAPI sender, Packet Pack)
           {
        	   RezSingleAttachmentFromInv handlerRezSingleAttachment = OnRezSingleAttachmentFromInv;
        	   if (handlerRezSingleAttachment != null)
        	   {
        		   RezRestoreToWorldPacket rez = (RezRestoreToWorldPacket) Pack;

        		   #region Packet Session and User Check

        		   if (m_checkPackets)
        		   {
        			   if (rez.AgentData.SessionID != SessionId ||
        				   rez.AgentData.AgentID != AgentId)
        				   return true;
        		   }

        		   #endregion

        		   handlerRezSingleAttachment(this, rez.InventoryData.ItemID, 0);
        	   }

        	   return true;
           }
          */

        // update 20160129 - greythane-
        bool HandlerRezRestoreToWorld (IClientAPI sender, Packet pack)
        {
            RezRestoreToWorld handlerRezRestoreToWorld = OnRezRestoreToWorld;
            if (handlerRezRestoreToWorld != null) {
                var rezPacket = (RezRestoreToWorldPacket)pack;

                #region Packet Session and User Check
                if (m_checkPackets) {
                    if (rezPacket.AgentData.SessionID != SessionId || rezPacket.AgentData.AgentID != AgentId)
                        return true;
                }
                #endregion

                handlerRezRestoreToWorld (this, rezPacket.InventoryData.ItemID, rezPacket.InventoryData.GroupID);

            }
            return true;
        }



        bool HandleRezMultipleAttachmentsFromInv (IClientAPI sender, Packet pack)
        {
            RezSingleAttachmentFromInv handlerRezMultipleAttachments = OnRezSingleAttachmentFromInv;

            if (handlerRezMultipleAttachments != null) {
                RezMultipleAttachmentsFromInvPacket rez = (RezMultipleAttachmentsFromInvPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (rez.AgentData.SessionID != SessionId || rez.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                foreach (RezMultipleAttachmentsFromInvPacket.ObjectDataBlock obj in rez.ObjectData) {
                    handlerRezMultipleAttachments (this, obj.ItemID, obj.AttachmentPt);
                }
            }

            return true;
        }

        bool HandleDetachAttachmentIntoInv (IClientAPI sender, Packet pack)
        {
            UUIDNameRequest handlerDetachAttachmentIntoInv = OnDetachAttachmentIntoInv;
            if (handlerDetachAttachmentIntoInv != null) {
                var detachtoInv = (DetachAttachmentIntoInvPacket)pack;

                #region Packet Session and User Check

                //TODO!
                // UNSUPPORTED ON THIS PACKET

                #endregion

                UUID itemID = detachtoInv.ObjectData.ItemID;
                // UUID ATTACH_agentID = detachtoInv.ObjectData.AgentID;

                handlerDetachAttachmentIntoInv (itemID, this);
            }
            return true;
        }

        bool HandleObjectAttach (IClientAPI sender, Packet pack)
        {
            if (OnObjectAttach != null) {
                var att = (ObjectAttachPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (att.AgentData.SessionID != SessionId || att.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                ObjectAttach handlerObjectAttach = OnObjectAttach;

                if (handlerObjectAttach != null) {
                    if (att.ObjectData.Length > 0) {
                        handlerObjectAttach (this, att.ObjectData [0].ObjectLocalID, att.AgentData.AttachmentPoint, false);
                    }
                }
            }
            return true;
        }

        bool HandleObjectDetach (IClientAPI sender, Packet pack)
        {
            var dett = (ObjectDetachPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dett.AgentData.SessionID != SessionId || dett.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectDetachPacket.ObjectDataBlock t in dett.ObjectData) {
                uint obj = t.ObjectLocalID;
                ObjectDeselect handlerObjectDetach = OnObjectDetach;
                if (handlerObjectDetach != null) {
                    handlerObjectDetach (obj, this);
                }
            }
            return true;
        }

        bool HandleObjectDrop (IClientAPI sender, Packet Pack)
        {
            var dropp = (ObjectDropPacket)Pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (dropp.AgentData.SessionID != SessionId || dropp.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (ObjectDropPacket.ObjectDataBlock t in dropp.ObjectData) {
                uint obj = t.ObjectLocalID;
                ObjectDrop handlerObjectDrop = OnObjectDrop;
                if (handlerObjectDrop != null) {
                    handlerObjectDrop (obj, this);
                }
            }
            return true;
        }

        bool HandleSetAlwaysRun (IClientAPI sender, Packet pack)
        {
            var run = (SetAlwaysRunPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (run.AgentData.SessionID != SessionId || run.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            SetAlwaysRun handlerSetAlwaysRun = OnSetAlwaysRun;
            if (handlerSetAlwaysRun != null)
                handlerSetAlwaysRun (this, run.AgentData.AlwaysRun);

            return true;
        }

        bool HandleCompleteAgentMovement (IClientAPI sender, Packet pack)
        {
            GenericCall1 handlerCompleteMovementToRegion = OnCompleteMovementToRegion;
            if (handlerCompleteMovementToRegion != null) {
                handlerCompleteMovementToRegion (sender);
            }
            handlerCompleteMovementToRegion = null;

            return true;
        }

        bool HandleAgentAnimation (IClientAPI sender, Packet pack)
        {
            var AgentAni = (AgentAnimationPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (AgentAni.AgentData.SessionID != SessionId || AgentAni.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            foreach (AgentAnimationPacket.AnimationListBlock t in AgentAni.AnimationList) {
                if (t.StartAnim) {
                    StartAnim handlerStartAnim = OnStartAnim;
                    if (handlerStartAnim != null) {
                        handlerStartAnim (this, t.AnimID);
                    }
                } else {
                    StopAnim handlerStopAnim = OnStopAnim;
                    if (handlerStopAnim != null) {
                        handlerStopAnim (this, t.AnimID);
                    }
                }
            }
            return true;
        }

        bool HandleAgentRequestSit (IClientAPI sender, Packet pack)
        {
            if (OnAgentRequestSit != null) {
                var agentRequestSit = (AgentRequestSitPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (agentRequestSit.AgentData.SessionID != SessionId || agentRequestSit.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                AgentRequestSit handlerAgentRequestSit = OnAgentRequestSit;
                if (handlerAgentRequestSit != null)
                    handlerAgentRequestSit (this,
                                            agentRequestSit.TargetObject.TargetID,
                                            agentRequestSit.TargetObject.Offset);
            }
            return true;
        }

        bool HandleAgentSit (IClientAPI sender, Packet pack)
        {
            if (OnAgentSit != null) {
                var agentSit = (AgentSitPacket)pack;

                #region Packet Session and User Check

                if (m_checkPackets) {
                    if (agentSit.AgentData.SessionID != SessionId || agentSit.AgentData.AgentID != AgentId)
                        return true;
                }

                #endregion

                AgentSit handlerAgentSit = OnAgentSit;
                if (handlerAgentSit != null) {
                    OnAgentSit (this, agentSit.AgentData.AgentID);
                }
            }
            return true;
        }

        bool HandleSoundTrigger (IClientAPI sender, Packet pack)
        {
            var soundTriggerPacket = (SoundTriggerPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                //TODO!
                // UNSUPPORTED ON THIS PACKET
            }

            #endregion

            SoundTrigger handlerSoundTrigger = OnSoundTrigger;
            if (handlerSoundTrigger != null) {
                // UUIDS are sent as zeroes by the client, substitute agent's id
                handlerSoundTrigger (soundTriggerPacket.SoundData.SoundID,
                                     AgentId,
                                     AgentId, 
                                     AgentId,
                                     soundTriggerPacket.SoundData.Gain,
                                     soundTriggerPacket.SoundData.Position,
                                     soundTriggerPacket.SoundData.Handle,
                                     0);
            }
            return true;
        }

        bool HandleAvatarPickerRequest (IClientAPI sender, Packet pack)
        {
            var avRequestQuery = (AvatarPickerRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (avRequestQuery.AgentData.SessionID != SessionId || avRequestQuery.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            AvatarPickerRequestPacket.AgentDataBlock Requestdata = avRequestQuery.AgentData;
            AvatarPickerRequestPacket.DataBlock querydata = avRequestQuery.Data;
            //MainConsole.Instance.Debug("Agent Sends:" + Utils.BytesToString(querydata.Name));

            AvatarPickerRequest handlerAvatarPickerRequest = OnAvatarPickerRequest;
            if (handlerAvatarPickerRequest != null) {
                handlerAvatarPickerRequest (this,
                                            Requestdata.AgentID,
                                            Requestdata.QueryID,
                                            Utils.BytesToString (querydata.Name));
            }
            return true;
        }

        bool HandleAgentDataUpdateRequest (IClientAPI sender, Packet pack)
        {
            var avRequestDataUpdatePacket = (AgentDataUpdateRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (avRequestDataUpdatePacket.AgentData.SessionID != SessionId ||
                    avRequestDataUpdatePacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            FetchInventory handlerAgentDataUpdateRequest = OnAgentDataUpdateRequest;

            if (handlerAgentDataUpdateRequest != null) {
                handlerAgentDataUpdateRequest (this,
                                               avRequestDataUpdatePacket.AgentData.AgentID,
                                               avRequestDataUpdatePacket.AgentData.SessionID);
            }

            return true;
        }

        bool HandleUserInfoRequest (IClientAPI sender, Packet pack)
        {
            UserInfoRequest handlerUserInfoRequest = OnUserInfoRequest;
            if (handlerUserInfoRequest != null) {
                handlerUserInfoRequest (this);
            } else {
                SendUserInfoReply (false, true, "");
            }
            return true;
        }

        bool HandleUpdateUserInfo (IClientAPI sender, Packet pack)
        {
            var updateUserInfo = (UpdateUserInfoPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (updateUserInfo.AgentData.SessionID != SessionId || updateUserInfo.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            UpdateUserInfo handlerUpdateUserInfo = OnUpdateUserInfo;
            if (handlerUpdateUserInfo != null) {
                bool visible = true;
                string DirectoryVisibility = Utils.BytesToString (updateUserInfo.UserData.DirectoryVisibility);
                if (DirectoryVisibility == "hidden")
                    visible = false;

                handlerUpdateUserInfo (updateUserInfo.UserData.IMViaEMail, visible, this);
            }
            return true;
        }

        bool HandleSetStartLocationRequest (IClientAPI sender, Packet pack)
        {
            var avSetStartLocationRequestPacket = (SetStartLocationRequestPacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (avSetStartLocationRequestPacket.AgentData.SessionID != SessionId ||
                    avSetStartLocationRequestPacket.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            if (avSetStartLocationRequestPacket.AgentData.AgentID == AgentId &&
                avSetStartLocationRequestPacket.AgentData.SessionID == SessionId) {
                //TODO: Should this limitation apply??
                // Linden Client limitation..
                if (Util.ApproxEqual (avSetStartLocationRequestPacket.StartLocationData.LocationPos.X, 255.5f) ||
                    Util.ApproxEqual (avSetStartLocationRequestPacket.StartLocationData.LocationPos.Y, 255.5f)) {

                    IScenePresence avatar = null;
                    if (m_scene.TryGetScenePresence (AgentId, out avatar)) {
                        if (Util.ApproxEqual (avSetStartLocationRequestPacket.StartLocationData.LocationPos.X, 255.5f)) {
                            avSetStartLocationRequestPacket.StartLocationData.LocationPos.X = avatar.AbsolutePosition.X;
                        }

                        if (Util.ApproxEqual (avSetStartLocationRequestPacket.StartLocationData.LocationPos.Y, 255.5f)) {
                            avSetStartLocationRequestPacket.StartLocationData.LocationPos.Y = avatar.AbsolutePosition.Y;
                        }
                    }
                }
                TeleportLocationRequest handlerSetStartLocationRequest = OnSetStartLocationRequest;
                if (handlerSetStartLocationRequest != null) {
                    handlerSetStartLocationRequest (this, 0,
                                                   avSetStartLocationRequestPacket.StartLocationData.LocationPos,
                                                   avSetStartLocationRequestPacket.StartLocationData.LocationLookAt,
                                                   avSetStartLocationRequestPacket.StartLocationData.LocationID);
                }
            }
            return true;
        }

        bool HandleAgentThrottle (IClientAPI sender, Packet pack)
        {
            var atpack = (AgentThrottlePacket)pack;

            #region Packet Session and User Check

            if (m_checkPackets) {
                if (atpack.AgentData.SessionID != SessionId || atpack.AgentData.AgentID != AgentId)
                    return true;
            }

            #endregion

            m_udpClient.SetThrottles (atpack.Throttle.Throttles);
            return true;
        }

        bool HandleAgentPause (IClientAPI sender, Packet pack)
        {
            #region Packet Session and User Check

            if (m_checkPackets) {
                var agentPausePacket = (AgentPausePacket)pack;
                if (agentPausePacket != null && 
                    (agentPausePacket.AgentData.SessionID != SessionId || agentPausePacket.AgentData.AgentID != AgentId))
                    return true;
            }

            #endregion

            m_udpClient.IsPaused = true;
            return true;
        }

        bool HandleAgentResume (IClientAPI sender, Packet pack)
        {
            #region Packet Session and User Check

            if (m_checkPackets) {
                var agentResumePacket = (AgentResumePacket)pack;
                if (agentResumePacket != null && 
                    (agentResumePacket.AgentData.SessionID != SessionId || agentResumePacket.AgentData.AgentID != AgentId))
                    return true;
            }

            #endregion

            m_udpClient.IsPaused = false;
            SendStartPingCheck (m_udpClient.CurrentPingSequence++);
            return true;
        }

        bool HandleForceScriptControlRelease (IClientAPI sender, Packet pack)
        {
            ForceReleaseControls handlerForceReleaseControls = OnForceReleaseControls;
            if (handlerForceReleaseControls != null) {
                handlerForceReleaseControls (this, AgentId);
            }
            return true;
        }

        #endregion Scene/Avatar

        public void StopFlying (IEntity p)
        {
            if (p is IScenePresence) {
                IScenePresence presence = p as IScenePresence;
                // It turns out to get the agent to stop flying, you have to feed it stop flying velocities
                // There's no explicit message to send the client to tell it to stop flying..   it relies on the
                // velocity, collision plane and avatar height

                // Add 1/6 the avatar's height to it's position so it doesn't shoot into the air
                // when the avatar stands up

                Vector3 pos = presence.AbsolutePosition;

                IAvatarAppearanceModule appearance = presence.RequestModuleInterface<IAvatarAppearanceModule> ();
                if (appearance != null)
                    pos += new Vector3 (0f, 0f, (appearance.Appearance.AvatarHeight / 6f));

                presence.AbsolutePosition = pos;

                // attach a suitable collision plane regardless of the actual situation to force the LLClient to land.
                // Collision plane below the avatar's position a 6th of the avatar's height is suitable.
                // Mind you, that this method doesn't get called if the avatar's velocity magnitude is greater then a
                // certain amount..   because the LLClient wouldn't land in that situation anyway.

                if (appearance != null)
                    presence.CollisionPlane = new Vector4 (0, 0, 0, pos.Z - appearance.Appearance.AvatarHeight / 6f);


                ImprovedTerseObjectUpdatePacket.ObjectDataBlock block =
                    CreateImprovedTerseBlock (p, false);

                float TIME_DILATION = m_scene.TimeDilation;
                ushort timeDilation = Utils.FloatToUInt16 (TIME_DILATION, 0.0f, 1.0f);


                var packet = new ImprovedTerseObjectUpdatePacket {
                    RegionData = {RegionHandle = m_scene.RegionInfo.RegionHandle,
                                  TimeDilation = timeDilation},
                    ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock [1]
                };

                packet.ObjectData [0] = block;

                OutPacket (packet, ThrottleOutPacketType.Task, true);
            }

            //ControllingClient.SendAvatarTerseUpdate(new SendAvatarTerseData(m_rootRegionHandle, (ushort)(m_scene.TimeDilation * ushort.MaxValue), LocalId,
            //        AbsolutePosition, Velocity, Vector3.Zero, m_bodyRot, new Vector4(0,0,1,AbsolutePosition.Z - 0.5f), m_uuid, null, GetUpdatePriority(ControllingClient)));
        }

        public void ForceSendOnAgentUpdate (IClientAPI client, AgentUpdateArgs args)
        {
            OnAgentUpdate (client, args);
        }

        public void SendRebakeAvatarTextures (UUID textureID)
        {
            RebakeAvatarTexturesPacket pack =
                (RebakeAvatarTexturesPacket)PacketPool.Instance.GetPacket (PacketType.RebakeAvatarTextures);

            pack.TextureData = new RebakeAvatarTexturesPacket.TextureDataBlock { TextureID = textureID };
            //            OutPacket(pack, ThrottleOutPacketType.Texture);
            OutPacket (pack, ThrottleOutPacketType.AvatarInfo);
        }

    }

}
