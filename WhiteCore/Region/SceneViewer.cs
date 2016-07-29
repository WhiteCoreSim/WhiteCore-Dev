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
//#define UseRemovingEntityUpdates

#define UseDictionaryForEntityUpdates

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Timers;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Utilities;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Region
{
    public class SceneViewer : ISceneViewer
    {
        #region Declares

        const double MINVIEWDSTEP = 16;
        const double MINVIEWDSTEPSQ = MINVIEWDSTEP*MINVIEWDSTEP;

        protected IScenePresence m_presence;
        protected IScene m_scene;

        /// <summary>
        ///     Have we sent all of the objects in the sim that the client can see for the first time?
        /// </summary>
        protected bool m_SentInitialObjects;

        protected volatile bool m_queueing;
        protected volatile bool m_inUse;
        protected Prioritizer m_prioritizer;
        protected Culler m_culler;
        protected bool m_forceCullCheck;
        readonly object m_presenceUpdatesToSendLock = new object();
        readonly object m_presenceAnimationsToSendLock = new object();
        readonly object m_objectPropertiesToSendLock = new object();
        readonly object m_objectUpdatesToSendLock = new object();
#if UseRemovingEntityUpdates
        OrderedDictionary/*<UUID, EntityUpdate>*/ m_presenceUpdatesToSend = new OrderedDictionary/*<UUID, EntityUpdate>*/ ();
#elif UseDictionaryForEntityUpdates
        readonly Dictionary<uint, EntityUpdate> m_presenceUpdatesToSend = new Dictionary<uint, EntityUpdate>();
#else
        Queue<EntityUpdate> m_presenceUpdatesToSend = new Queue<EntityUpdate>();
#endif

        readonly Queue<AnimationGroup> m_presenceAnimationsToSend =
            new Queue<AnimationGroup> /*<UUID, AnimationGroup>*/();

        readonly OrderedDictionary /*<UUID, EntityUpdate>*/
            m_objectUpdatesToSend = new OrderedDictionary /*<UUID, EntityUpdate>*/();

        readonly OrderedDictionary /*<UUID, ISceneChildEntity>*/
            m_objectPropertiesToSend = new OrderedDictionary /*<UUID, ISceneChildEntity>*/();

        HashSet<ISceneEntity> lastGrpsInView = new HashSet<ISceneEntity>();
        readonly Dictionary<UUID, IScenePresence> lastPresencesDInView = new Dictionary<UUID, IScenePresence>();
        readonly object m_lastPresencesInViewLock = new object();
        Vector3 m_lastUpdatePos;
        int m_numberOfLoops;
        Timer m_drawDistanceChangedTimer;
        readonly object m_drawDistanceTimerLock = new object();
        const int NUMBER_OF_LOOPS_TO_WAIT = 30;

        const float PresenceSendPercentage = 0.60f;
        const float PrimSendPercentage = 0.40f;

        public IPrioritizer Prioritizer
        {
            get { return m_prioritizer; }
        }

        public ICuller Culler
        {
            get { return m_culler; }
        }

        #endregion

        #region Constructor

        public SceneViewer(IScenePresence presence)
        {
            m_presence = presence;
            m_scene = presence.Scene;
            m_presence.OnSignificantClientMovement += SignificantClientMovement;
            m_presence.Scene.EventManager.OnMakeChildAgent += EventManager_OnMakeChildAgent;
            m_scene.EventManager.OnClosingClient += EventManager_OnClosingClient;
            m_presence.Scene.WhiteCoreEventManager.RegisterEventHandler("DrawDistanceChanged",
                                                                     WhiteCoreEventManager_OnGenericEvent);
            m_presence.Scene.WhiteCoreEventManager.RegisterEventHandler("SignficantCameraMovement",
                                                                     WhiteCoreEventManager_OnGenericEvent);
            m_prioritizer = new Prioritizer(presence.Scene);
            m_culler = new Culler(presence.Scene);
        }

        void EventManager_OnClosingClient(IClientAPI client)
        {
            lock (m_lastPresencesInViewLock)
                if (lastPresencesDInView.ContainsKey(client.AgentId))
                    lastPresencesDInView.Remove(client.AgentId);
        }

        void EventManager_OnMakeChildAgent(IScenePresence presence, GridRegion destination)
        {
            RemoveAvatarFromView(presence);
        }

        object WhiteCoreEventManager_OnGenericEvent(string FunctionName, object parameters)
        {
            if (m_culler != null && m_culler.UseCulling && FunctionName == "DrawDistanceChanged")
            {
                IScenePresence sp = (IScenePresence) parameters;
                if (sp.UUID != m_presence.UUID)
                    return null; //Only want our av

                //Draw Distance changed, force a cull check
                m_forceCullCheck = true;
                //Don't do this immediately as the viewer may keep changing the draw distance
                lock (m_drawDistanceTimerLock)
                {
                    if (m_drawDistanceChangedTimer != null)
                        m_drawDistanceChangedTimer.Stop(); //Stop any old timers
                    m_drawDistanceChangedTimer = new Timer {Interval = 3000};
                    //Fire this again in 3 seconds so that we do send prims to children agents
                    m_drawDistanceChangedTimer.Elapsed += m_drawDistanceChangedTimer_Elapsed;
                    m_drawDistanceChangedTimer.Start();
                }
                //SignificantClientMovement (m_presence.ControllingClient);
            }
            else if (FunctionName == "SignficantCameraMovement")
            {
                //Camera changed, do a cull check
                m_forceCullCheck = true;
                //Don't do this immediately as the viewer may keep changing the camera quickly
                lock (m_drawDistanceTimerLock)
                {
                    if (m_drawDistanceChangedTimer != null)
                        m_drawDistanceChangedTimer.Stop(); //Stop any old timers
                    m_drawDistanceChangedTimer = new Timer {Interval = 3000};
                    //Fire this again in 3 seconds so that we do send prims to children agents
                    m_drawDistanceChangedTimer.Elapsed += m_drawDistanceChangedTimer_Elapsed;
                    m_drawDistanceChangedTimer.Start();
                }
            }
            return null;
        }

        void m_drawDistanceChangedTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (m_drawDistanceTimerLock)
                m_drawDistanceChangedTimer.Stop();
            if (m_presence != null)
                SignificantClientMovement();
        }

        #endregion

        #region Enqueue/Remove updates for entities

        public void QueuePresenceForUpdate(IScenePresence presence, PrimUpdateFlags flags)
        {
            if (m_culler != null && !m_culler.ShowEntityToClient(m_presence, presence, m_scene))
            {
                //They are out of view and they changed, we need to update them when they do come in view
                lock (m_lastPresencesInViewLock)
                    lastPresencesDInView.Remove(presence.UUID);
                return; // if 2 far ignore
            }
            //Is this really necessary? -7/21
            //Very much so... the client cannot get a terse update before a full update -7/25
            lock (m_lastPresencesInViewLock)
                if (!lastPresencesDInView.ContainsKey(presence.UUID))
                    return; //Only send updates if they are in view
            AddPresenceUpdate(presence, flags);
        }

        public void QueuePresenceForFullUpdate(IScenePresence presence, bool forced)
        {
            if (!forced && m_culler != null && !m_culler.ShowEntityToClient(m_presence, presence, m_scene))
            {
                //They are out of view and they changed, we need to update them when they do come in view
                lock (m_lastPresencesInViewLock)
                    lastPresencesDInView.Remove(presence.UUID);
                return; // if 2 far ignore
            }
            lock (m_lastPresencesInViewLock)
            {
                if (!lastPresencesDInView.ContainsKey(presence.UUID))
                    AddPresenceToCurrentlyInView(presence);
                else if (!forced) //Only send one full update please!
                    return;
            }

            SendFullUpdateForPresence(presence);
            AddPresenceUpdate(presence, PrimUpdateFlags.ForcedFullUpdate);
        }

        void AddPresenceUpdate(IScenePresence presence, PrimUpdateFlags flags)
        {
            lock (m_presenceUpdatesToSendLock)
            {
#if UseRemovingEntityUpdates
                EntityUpdate o = (EntityUpdate)m_presenceUpdatesToSend[presence.UUID];
                if (o == null)
                    o = new EntityUpdate (presence, flags);
                else
                {
                    if ((o.Flags & flags) == o.Flags)
                        return; //Same, leave it alone!
                    o.Flags |= flags;
                    return;//All done, its updated
                }

                if (m_presence.UUID == presence.UUID) //Its us, set us first!
                    m_presenceUpdatesToSend.Insert (0, presence.UUID, o);
                else //Not us, set at the end
                    m_presenceUpdatesToSend.Insert (m_presenceUpdatesToSend.Count, presence.UUID, o);
#elif UseDictionaryForEntityUpdates
                EntityUpdate o = null;
                if (!m_presenceUpdatesToSend.TryGetValue(presence.LocalId, out o))
                    o = new EntityUpdate(presence, flags);
                else
                {
                    if ((o.Flags & flags) == o.Flags)
                        return; //Same, leave it alone!
                    o.Flags |= flags;
                    return; //All done, its updated, no need to re-add
                }

                m_presenceUpdatesToSend[presence.LocalId] = o;
#else
                m_presenceUpdatesToSend.Enqueue (new EntityUpdate (presence, flags));
#endif
            }
        }

        public void QueuePresenceForAnimationUpdate(IScenePresence presence, AnimationGroup animation)
        {
            if (m_culler != null && !m_culler.ShowEntityToClient(m_presence, presence, m_scene))
            {
                //They are out of view and they changed, we need to update them when they do come in view
                lock (m_lastPresencesInViewLock)
                    lastPresencesDInView.Remove(presence.UUID);
                return; // if 2 far ignore
            }

            //Send a terse as well, since we are sending an animation
            if (m_presence.LocalId == presence.LocalId &&
                presence.SittingOnUUID == UUID.Zero) //As long as we aren't sitting, in which we don't get terse updates
            {
                //Is this really necessary? -7/21
                //Very much so... the client cannot get a terse update before a full update -7/25
                bool lpinview;
                lock (m_lastPresencesInViewLock)
                    lpinview = lastPresencesDInView.ContainsKey (presence.UUID);

                if (lpinview)
                    AddPresenceUpdate(presence, PrimUpdateFlags.TerseUpdate);
                //Only send updates if they are in view
            }
            else
                AddPresenceUpdate(presence, PrimUpdateFlags.TerseUpdate);

            lock (m_presenceAnimationsToSendLock)
                m_presenceAnimationsToSend.Enqueue(animation);
        }

        public void ClearPresenceUpdates(IScenePresence presence)
        {
            lock (m_presenceUpdatesToSendLock)
            {
                m_presenceUpdatesToSend.Remove(presence.LocalId);
            }
        }

        /// <summary>
        ///     Add the objects to the queue for which we need to send an update to the client
        /// </summary>
        /// <param name="part"></param>
        /// <param name="flags"></param>
        public void QueuePartForUpdate(ISceneChildEntity part, PrimUpdateFlags flags)
        {
            if (m_presence == null)
                return;
            if (m_culler != null && !m_culler.ShowEntityToClient(m_presence, part.ParentEntity, m_scene))
            {
                //They are out of view and they changed, we need to update them when they do come in view
                lastGrpsInView.Remove(part.ParentEntity);
                return; // if 2 far ignore
            }
            if ((!(m_presence.DrawDistance > m_presence.Scene.RegionInfo.RegionSizeX &&
                   m_presence.DrawDistance > m_presence.Scene.RegionInfo.RegionSizeY)) &&
                !lastGrpsInView.Contains(part.ParentEntity))
            {
                //This object entered our draw distance on its own, and we haven't seen it before
                flags = PrimUpdateFlags.ForcedFullUpdate;
                lock (m_objectUpdatesToSendLock)
                {
                    foreach (
                        EntityUpdate update in
                            part.ParentEntity.ChildrenEntities().Select(child => new EntityUpdate(child, flags)))
                    {
                        QueueEntityUpdate(update);
                    }
                }
                lastGrpsInView.Add(part.ParentEntity);
                return;
            }

            EntityUpdate o = new EntityUpdate(part, flags);
            lock (m_objectUpdatesToSendLock)
                QueueEntityUpdate(o);
        }

        /// <summary>
        ///     NOTE: DO THE LOCKING ON YOUR OWN
        /// </summary>
        /// <param name="update"></param>
        void QueueEntityUpdate(EntityUpdate update)
        {
            EntityUpdate o = (EntityUpdate) m_objectUpdatesToSend[update.Entity.UUID];
            if (o == null)
                o = update;
            else
            {
                if (o.Flags == update.Flags)
                    return; //Same, leave it alone!
                m_objectUpdatesToSend.Remove(update.Entity.UUID);
                o.Flags = o.Flags | update.Flags;
            }
            m_objectUpdatesToSend.Insert(m_objectUpdatesToSend.Count, o.Entity.UUID, o);
        }

        public void QueuePartsForPropertiesUpdate(ISceneChildEntity[] entities)
        {
            lock (m_objectPropertiesToSendLock)
            {
                foreach (
                    ISceneChildEntity entity in
                        entities.Where(
                            entity =>
                            m_culler == null || m_culler.ShowEntityToClient(m_presence, entity.ParentEntity, m_scene)))
                {
                    m_objectPropertiesToSend.Remove(entity.UUID);
                    //Insert at the end
                    m_objectPropertiesToSend.Insert(m_objectPropertiesToSend.Count, entity.UUID, entity);
                }
            }
        }

        public void RemoveAvatarFromView(IScenePresence sp)
        {
            lock (m_lastPresencesInViewLock)
                lastPresencesDInView.Remove(sp.UUID);
        }

        public void SendPresenceFullUpdate(IScenePresence presence)
        {
            if (m_culler != null && !m_culler.ShowEntityToClient(m_presence, presence, m_scene))
                m_presence.ControllingClient.SendAvatarDataImmediate(presence);
            lock (m_lastPresencesInViewLock)
                if (!lastPresencesDInView.ContainsKey(presence.UUID))
                    AddPresenceToCurrentlyInView(presence);
        }

        protected void SendFullUpdateForPresence(IScenePresence presence)
        {
            m_presence.ControllingClient.SendAvatarDataImmediate(presence);
            //Send the animations too
            presence.Animator.SendAnimPackToClient(m_presence.ControllingClient);
            //Send the presence of this agent to us
            IAvatarAppearanceModule module =
                presence.RequestModuleInterface<IAvatarAppearanceModule>();
            if (module != null)
                module.SendAppearanceToAgent(m_presence);
        }

        void AddPresenceToCurrentlyInView(IScenePresence presence)
        {
            lastPresencesDInView.Add(presence.UUID, presence);
            //We need to send all attachments of this avatar as well
            IAttachmentsModule attmodule =
                presence.Scene.RequestModuleInterface<IAttachmentsModule>();
            if (attmodule != null)
                attmodule.SendAttachmentsToPresence(m_presence, presence);
        }

        #endregion

        #region Object Culling by draw distance

        /// <summary>
        ///     When the client moves enough to trigger this, make sure that we have sent
        ///     the client all of the objects that have just entered their FOV in their draw distance.
        /// </summary>
        void SignificantClientMovement()
        {
            if (m_culler == null)
                return;

            if (!m_culler.UseCulling)
                return;

            if (!m_forceCullCheck && m_presence.DrawDistance > m_presence.Scene.RegionInfo.RegionSizeX &&
                m_presence.DrawDistance > m_presence.Scene.RegionInfo.RegionSizeY && !m_presence.IsChildAgent)
            {
                m_forceCullCheck = false; //Make sure to reset it
                return;
            }

            if (m_presence.DrawDistance == 0)
                return;

            if (m_presence.DrawDistance < 32)
            {
                //If the draw distance is small, the client has gotten messed up or something and we can't do this...
                m_presence.DrawDistance = 32; //Force give them a draw distance
            }

            if (!m_presence.IsChildAgent || m_presence.Scene.RegionInfo.SeeIntoThisSimFromNeighbor)
            {
                Vector3 pos = m_presence.CameraPosition;
                float distsq = Vector3.DistanceSquared(pos, m_lastUpdatePos);
                distsq += 0.2f*m_presence.Velocity.LengthSquared();
                if (distsq < MINVIEWDSTEPSQ && !m_forceCullCheck)
                    //They havn't moved enough to trigger another update, so just quit
                    return;
                m_forceCullCheck = false;
                Util.FireAndForget(DoSignificantClientMovement);
            }
        }

        class DoubleComparer : IComparer
        {
            #region IComparer Members

            public int Compare(object x, object y)
            {
                return Compare((PriorityQueueItem<EntityUpdate, int>) x, (PriorityQueueItem<EntityUpdate, int>) y);
            }

            #endregion

            public static int Compare(PriorityQueueItem<EntityUpdate, int> x, PriorityQueueItem<EntityUpdate, int> y)
            {
                return x._priority.CompareTo(y._priority);
            }
        }

        void DoSignificantClientMovement(object o)
        {
            //Just return all the entities, its quicker to do the culling check rather than the position check
            ISceneEntity[] entities = m_presence.Scene.Entities.GetEntities();

            // build a prioritized list of things we need to send

            HashSet<ISceneEntity> NewGrpsInView = new HashSet<ISceneEntity>();

            int time = Util.EnvironmentTickCount();
            foreach (ISceneEntity e in from e in entities
                                       where e != null
                                       where !e.IsAttachment
                                       where !e.IsDeleted
                                       where !lastGrpsInView.Contains(e)
                                       where m_culler != null
                                       where m_culler.ShowEntityToClient(m_presence, e, m_scene, time)
                                       select e)
            {
                NewGrpsInView.Add(e);
            }
            entities = null;
            if (lastGrpsInView.Count == 0)
                lastGrpsInView = new HashSet<ISceneEntity>(NewGrpsInView);
            else
                lastGrpsInView.UnionWith(NewGrpsInView);
            // send them 
            if (NewGrpsInView.Count != 0)
                SendQueued(NewGrpsInView);
            NewGrpsInView.Clear();


            //Check for scenepresences as well
            List<IScenePresence> presences = new List<IScenePresence>(m_presence.Scene.Entities.GetPresences());

            foreach (
                IScenePresence presence in
                    presences.Where(presence => presence != null && presence.UUID != m_presence.UUID))
            {
                lock (m_lastPresencesInViewLock)
                    if (lastPresencesDInView.ContainsKey(presence.UUID))
                        continue; //Don't resend the update

                //Check for culling here!
                if (!m_culler.ShowEntityToClient(m_presence, presence, m_scene, time))
                    continue; // if 2 far ignore

                lock (m_lastPresencesInViewLock)
                    lastPresencesDInView.Add(presence.UUID, presence);

                SendFullUpdateForPresence(presence);
            }

            presences = null;
        }

        #endregion

        #region SendPrimUpdates

        /// <summary>
        ///     This method is called by the LLUDPServer and should never be called by anyone else
        ///     It loops through the available updates and sends them out (no waiting)
        /// </summary>
        /// <param name="numPrimUpdates">The number of prim updates to send</param>
        /// <param name="numAvaUpdates">The number of avatar updates to send</param>
        public void SendPrimUpdates(int numPrimUpdates, int numAvaUpdates)
        {
            if (m_numberOfLoops < NUMBER_OF_LOOPS_TO_WAIT)
                //Wait for the client to finish connecting fully before sending out bunches of updates
            {
                m_numberOfLoops++;
                return;
            }

            if (m_inUse || m_presence.IsInTransit)
                return;

            m_inUse = true;
            //This is for stats
            int AgentMS = Util.EnvironmentTickCount();

            #region New client entering the Scene, requires all objects in the Scene

            //If we havn't started processing this client yet, we need to send them ALL the prims that we have in this Scene (and deal with culling as well...)
            if (!m_SentInitialObjects && m_presence.DrawDistance != 0.0f)
                SendInitialObjects();

            int presenceNumToSend = numAvaUpdates;
            List<EntityUpdate> updates = new List<EntityUpdate>();
            lock (m_presenceUpdatesToSendLock)
            {
                //Send the numUpdates of them if that many
                // if we don't have that many, we send as many as possible, then switch to objects
                if (m_presenceUpdatesToSend.Count != 0)
                {
                    try
                    {
#if UseDictionaryForEntityUpdates
                        Dictionary<uint, EntityUpdate>.Enumerator e = m_presenceUpdatesToSend.GetEnumerator();
                        e.MoveNext();
                        List<uint> entitiesToRemove = new List<uint>();
#endif
                        int count = m_presenceUpdatesToSend.Count > presenceNumToSend
                                        ? presenceNumToSend
                                        : m_presenceUpdatesToSend.Count;
                        for (int i = 0; i < count; i++)
                        {
#if UseRemovingEntityUpdates
                            EntityUpdate update = ((EntityUpdate)m_presenceUpdatesToSend[0]);
                            /*if (m_EntitiesInPacketQueue.Contains (update.Entity.UUID))
                            {
                                m_presenceUpdatesToSend.RemoveAt (0);
                                m_presenceUpdatesToSend.Insert (m_presenceUpdatesToSend.Count, update.Entity.UUID, update);
                                continue;
                            }
                            m_EntitiesInPacketQueue.Add (update.Entity.UUID);*/
                            m_presenceUpdatesToSend.RemoveAt (0);
                            if (update.Flags == PrimUpdateFlags.ForcedFullUpdate)
                                SendFullUpdateForPresence ((IScenePresence)update.Entity);
                            else
                                updates.Add (update);
#elif UseDictionaryForEntityUpdates
                            EntityUpdate update = e.Current.Value;
                            entitiesToRemove.Add(update.Entity.LocalId); //Remove it later
                            if (update.Flags == PrimUpdateFlags.ForcedFullUpdate)
                                SendFullUpdateForPresence((IScenePresence) update.Entity);
                            else if (!((IScenePresence) update.Entity).IsChildAgent)
                                updates.Add(update);
                            e.MoveNext();
#else
                            EntityUpdate update = m_presenceUpdatesToSend.Dequeue ();
                            if (update.Flags == PrimUpdateFlags.ForcedFullUpdate)
                                SendFullUpdateForPresence ((IScenePresence)update.Entity);
                            else
                                updates.Add (update);
#endif
                        }
#if UseDictionaryForEntityUpdates
                        foreach (uint id in entitiesToRemove)
                        {
                            m_presenceUpdatesToSend.Remove(id);
                        }
#endif
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[SceneViewer]: Exception while running presence loop: {0}", ex);
                    }
                }
            }
            if (updates.Count != 0)
            {
                presenceNumToSend -= updates.Count;
                m_presence.ControllingClient.SendAvatarUpdate(updates);
            }
            updates.Clear();

            List<AnimationGroup> animationsToSend = new List<AnimationGroup>();
            lock (m_presenceAnimationsToSendLock)
            {
                //Send the numUpdates of them if that many
                // if we don't have that many, we send as many as possible, then switch to objects
                if (m_presenceAnimationsToSend.Count != 0 && presenceNumToSend > 0)
                {
                    try
                    {
                        int count = m_presenceAnimationsToSend.Count > presenceNumToSend
                                        ? presenceNumToSend
                                        : m_presenceAnimationsToSend.Count;
                        for (int i = 0; i < count; i++)
                        {
                            AnimationGroup update = m_presenceAnimationsToSend.Dequeue();
                            /*if (m_AnimationsInPacketQueue.Contains (update.AvatarID))
                            {
                                m_presenceAnimationsToSend.RemoveAt (0);
                                m_presenceAnimationsToSend.Insert (m_presenceAnimationsToSend.Count, update.AvatarID, update);
                                continue;
                            }
                            m_AnimationsInPacketQueue.Add (update.AvatarID);*/
                            animationsToSend.Add(update);
                        }
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[SceneViewer]: Exception while running presence loop: {0}", ex);
                    }
                }
            }
            foreach (AnimationGroup update in animationsToSend)
            {
                m_presence.ControllingClient.SendAnimations(update);
            }
            animationsToSend.Clear();

            int primsNumToSend = numPrimUpdates;

            List<IEntity> entities = new List<IEntity>();
            lock (m_objectPropertiesToSendLock)
            {
                //Send the numUpdates of them if that many
                // if we don't have that many, we send as many as possible, then switch to objects
                if (m_objectPropertiesToSend.Count != 0)
                {
                    try
                    {
                        int count = m_objectPropertiesToSend.Count > primsNumToSend
                                        ? primsNumToSend
                                        : m_objectPropertiesToSend.Count;
                        for (int i = 0; i < count; i++)
                        {
                            ISceneChildEntity entity = ((ISceneChildEntity) m_objectPropertiesToSend[0]);
                            /*if (m_PropertiesInPacketQueue.Contains (entity.UUID))
                            {
                                m_objectPropertiesToSend.RemoveAt (0);
                                m_objectPropertiesToSend.Insert (m_objectPropertiesToSend.Count, entity.UUID, entity);
                                continue;
                            }
                            m_PropertiesInPacketQueue.Add (entity.UUID);*/
                            m_objectPropertiesToSend.RemoveAt(0);
                            entities.Add(entity);
                        }
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[SceneViewer]: Exception while running presence loop: {0}", ex);
                    }
                }
            }
            if (entities.Count > 0)
            {
                primsNumToSend -= entities.Count;
                m_presence.ControllingClient.SendObjectPropertiesReply(entities);
            }

            updates = new List<EntityUpdate>();
            lock (m_objectUpdatesToSendLock)
            {
                if (m_objectUpdatesToSend.Count != 0)
                {
                    try
                    {
                        int count = m_objectUpdatesToSend.Count > primsNumToSend
                                        ? primsNumToSend
                                        : m_objectUpdatesToSend.Count;
                        for (int i = 0; i < count; i++)
                        {
                            EntityUpdate update = ((EntityUpdate) m_objectUpdatesToSend[0]);
                            //Fix the CRC for this update
                            //Increment the CRC code so that the client won't be sent a cached update for this
                            if (update.Flags != PrimUpdateFlags.PrimFlags)
                                ((ISceneChildEntity) update.Entity).CRC++;

                            updates.Add(update);
                            m_objectUpdatesToSend.RemoveAt(0);
                        }
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[SceneViewer]: Exception while running object loop: {0}", ex);
                    }
                    m_presence.ControllingClient.SendPrimUpdate(updates);
                }
            }


            //Add the time to the stats tracker
            IAgentUpdateMonitor reporter =
                m_presence.Scene.RequestModuleInterface<IMonitorModule>().GetMonitor<IAgentUpdateMonitor>(m_presence.Scene);
            if (reporter != null)
                reporter.AddAgentTime(Util.EnvironmentTickCountSubtract(AgentMS));

            m_inUse = false;
        }

        void SendInitialObjects()
        {
            //If they are not in this region, we check to make sure that we allow seeing into neighbors
            if (!m_presence.IsChildAgent ||
                (m_presence.Scene.RegionInfo.SeeIntoThisSimFromNeighbor) && m_prioritizer != null)
            {
                try
                {
                    m_SentInitialObjects = true;
                    ISceneEntity[] allEntities = m_presence.Scene.Entities.GetEntities();
                    HashSet<ISceneEntity> NewGrpsInView = new HashSet<ISceneEntity>();
                    // build a prioritized list of things we need to send
                    int time = Util.EnvironmentTickCount();
                    foreach (ISceneEntity e in from e in allEntities
                                               where e != null && e is SceneObjectGroup
                                               where !e.IsDeleted
                                               where !lastGrpsInView.Contains(e)
                                               where m_culler != null
                                               where m_culler.ShowEntityToClient(m_presence, e, m_scene, time)
                                               select e)
                    {
                        NewGrpsInView.Add(e);
                    }
                    //Merge the last seen lists
                    lastGrpsInView.UnionWith(NewGrpsInView);
                    allEntities = null;
                    // send them 
                    if (NewGrpsInView.Count != 0)
                        SendQueued(NewGrpsInView);
                    NewGrpsInView.Clear();
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.Warn("[SceneViewer]: Exception occurred in sending initial prims, " + ex);
                    //An exception occurred, don't fail to send all the prims to the client
                    m_SentInitialObjects = false;
                }
            }
        }

        /// <summary>
        ///     Once the packet has been sent, allow newer updates to be sent for the given entity
        /// </summary>
        /// <param name="updates"></param>
        public void FinishedEntityPacketSend(IEnumerable<EntityUpdate> updates)
        {
            /*foreach (EntityUpdate update in updates)
            {
                m_EntitiesInPacketQueue.Remove(update.Entity.UUID);
            }*/
        }

        /// <summary>
        ///     Once the packet has been sent, allow newer updates to be sent for the given entity
        /// </summary>
        /// <param name="updates"></param>
        public void FinishedPropertyPacketSend(IEnumerable<IEntity> updates)
        {
            /*foreach (IEntity update in updates)
            {
                m_PropertiesInPacketQueue.Remove(update.UUID);
            }*/
        }

        /// <summary>
        ///     Once the packet has been sent, allow newer animations to be sent for the given entity
        /// </summary>
        /// <param name="update"></param>
        public void FinishedAnimationPacketSend(AnimationGroup update)
        {
            //m_AnimationsInPacketQueue.Remove(update.AvatarID);
        }

        void SendQueued(HashSet<ISceneEntity> entsqueue)
        {
            //NO LOCKING REQUIRED HERE, THE PRIORITYQUEUE IS LOCAL
            //Enqueue them all
            List<KeyValuePair<double, ISceneEntity>> sortableList = new List<KeyValuePair<double, ISceneEntity>>();
            foreach (ISceneEntity ent in entsqueue)
                sortableList.Add(new KeyValuePair<double, ISceneEntity>(
                                     m_prioritizer.GetUpdatePriority(m_presence, ent), ent));
            sortableList.Sort(sortPriority);
            lock (m_objectUpdatesToSendLock)
            {
                foreach (KeyValuePair<double, ISceneEntity> t in sortableList)
                {
                    //Always send the root child first!
                    EntityUpdate update = new EntityUpdate(t.Value.RootChild, PrimUpdateFlags.ForcedFullUpdate);
                    QueueEntityUpdate(update);
                    foreach (ISceneChildEntity child in t.Value.ChildrenEntities().Where(child => !child.IsRoot))
                    {
                        update = new EntityUpdate(child, PrimUpdateFlags.ForcedFullUpdate);
                        QueueEntityUpdate(update);
                    }
                }
            }

            m_lastUpdatePos = (m_presence.IsChildAgent)
                                  ? m_presence.AbsolutePosition
                                  : m_presence.CameraPosition;
        }

        int sortPriority(KeyValuePair<double, ISceneEntity> a, KeyValuePair<double, ISceneEntity> b)
        {
            return a.Key.CompareTo(b.Key);
        }

        #endregion

        #endregion

        #region Reset and Close

        /// <summary>
        ///     The client has left this region and went into a child region
        /// </summary>
        public void Reset()
        {
            if (m_culler == null)
                return;
            //Gotta remove this so that if the client comes back, we don't have any issues with sending them another update
            lock (m_lastPresencesInViewLock)
                lastPresencesDInView.Remove(m_presence.UUID);
        }

        /// <summary>
        ///     Reset all lists that have to deal with what updates the viewer has
        /// </summary>
        public void Close()
        {
            if (m_presence == null)
                return;
            m_SentInitialObjects = false;
            m_prioritizer = null;
            m_culler = null;
            m_inUse = false;
            m_queueing = false;
            lock (m_objectUpdatesToSendLock)
                m_objectUpdatesToSend.Clear();
            lock (m_presenceUpdatesToSendLock)
                m_presenceUpdatesToSend.Clear();
            lock (m_presenceAnimationsToSendLock)
                m_presenceAnimationsToSend.Clear();
            lock (m_lastPresencesInViewLock)
                lastPresencesDInView.Clear();
            lock (m_objectPropertiesToSendLock)
                m_objectPropertiesToSend.Clear();
            lastGrpsInView.Clear();
            m_presence.OnSignificantClientMovement -= SignificantClientMovement;
            m_presence.Scene.EventManager.OnMakeChildAgent -= EventManager_OnMakeChildAgent;
            m_scene.EventManager.OnClosingClient -= EventManager_OnClosingClient;
            m_presence.Scene.WhiteCoreEventManager.UnregisterEventHandler("DrawDistanceChanged",
                                                                       WhiteCoreEventManager_OnGenericEvent);
            m_presence.Scene.WhiteCoreEventManager.UnregisterEventHandler("SignficantCameraMovement",
                                                                       WhiteCoreEventManager_OnGenericEvent);
            m_presence = null;
        }

        #endregion
    }
}
