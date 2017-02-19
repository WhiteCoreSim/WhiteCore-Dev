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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;

namespace WhiteCore.Modules.Land
{
    public class ParcelCounts
    {
        public int Group;
        public Dictionary<UUID, ISceneEntity> Objects = new Dictionary<UUID, ISceneEntity>();
        public int Others;
        public int Owner;
        public int Selected;
        public int Temporary;

        public Dictionary<UUID, int> Users =
            new Dictionary<UUID, int>();
    }

    public class PrimCountModule : IPrimCountModule, INonSharedRegionModule
    {
        readonly Dictionary<UUID, UUID> m_OwnerMap = new Dictionary<UUID, UUID>();
        readonly Dictionary<UUID, ParcelCounts> m_ParcelCounts = new Dictionary<UUID, ParcelCounts>();
        readonly Dictionary<UUID, PrimCounts> m_PrimCounts = new Dictionary<UUID, PrimCounts>();
        readonly Dictionary<UUID, int> m_SimwideCounts = new Dictionary<UUID, int>();

        // For now, a simple sim-wide taint to get this up. Later parcel based
        // taint to allow recounting a parcel if only ownership has changed
        // without recounting the whole sim.
        readonly object m_TaintLock = new object();
        IScene m_Scene;
        bool m_Tainted = true;

        #region INonSharedRegionModule Members

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(IScene scene)
        {
            m_Scene = scene;

            scene.RegisterModuleInterface<IPrimCountModule>(this);

            m_Scene.EventManager.OnObjectBeingAddedToScene += OnPrimCountAdd;
            m_Scene.EventManager.OnObjectBeingRemovedFromScene += OnObjectBeingRemovedFromScene;
            m_Scene.EventManager.OnLandObjectAdded += OnLandObjectAdded;
            m_Scene.EventManager.OnLandObjectRemoved += OnLandObjectRemoved;

            m_Scene.WhiteCoreEventManager.RegisterEventHandler("ObjectChangedOwner", OnGenericEvent);
            m_Scene.WhiteCoreEventManager.RegisterEventHandler("ObjectEnteringNewParcel", OnGenericEvent);
            m_Scene.EventManager.OnSceneGroupMove += EventManager_OnSceneGroupMove;
        }

        bool EventManager_OnSceneGroupMove(UUID groupID, Vector3 pos)
        {
            var parcelManagment = m_Scene.RequestModuleInterface<IParcelManagementModule>();
            ILandObject landObject = parcelManagment.GetLandObject(pos.X, pos.Y) ??
                                     parcelManagment.GetNearestAllowedParcel(UUID.Zero, pos.X, pos.Y);
            if (landObject == null) return true;

            ParcelCounts parcelCounts;
            if ((m_ParcelCounts.TryGetValue(landObject.LandData.GlobalID, out parcelCounts)) &&
                (!parcelCounts.Objects.ContainsKey(groupID)))

                lock (m_TaintLock)
                    m_Tainted = true;
            
            return true;
        }

        public void RegionLoaded(IScene scene)
        {
        }

        public void RemoveRegion(IScene scene)
        {
            m_Scene.UnregisterModuleInterface<IPrimCountModule>(this);

            m_Scene.EventManager.OnObjectBeingAddedToScene -= OnPrimCountAdd;
            m_Scene.EventManager.OnObjectBeingRemovedFromScene -= OnObjectBeingRemovedFromScene;
            m_Scene.EventManager.OnLandObjectAdded -= OnLandObjectAdded;
            m_Scene.EventManager.OnLandObjectRemoved -= OnLandObjectRemoved;

            m_Scene.WhiteCoreEventManager.UnregisterEventHandler("ObjectChangedOwner", OnGenericEvent);
            m_Scene.WhiteCoreEventManager.UnregisterEventHandler("ObjectEnteringNewParcel", OnGenericEvent);
            m_Scene.EventManager.OnSceneGroupMove -= EventManager_OnSceneGroupMove;
            m_Scene = null;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "PrimCountModule"; }
        }

        #endregion

        #region IPrimCountModule Members

        public void TaintPrimCount(ILandObject land)
        {
            lock (m_TaintLock)
                m_Tainted = true;
        }

        public void TaintPrimCount(int x, int y)
        {
            lock (m_TaintLock)
                m_Tainted = true;
        }

        public void TaintPrimCount()
        {
            lock (m_TaintLock)
                m_Tainted = true;
        }

        public int GetParcelMaxPrimCount(ILandObject thisObject)
        {
            // Normal Calculations
            // max = (this Land area) / (calculated region area) * region capacity * bonus  [ bonus is normally = 1 ]
            return (int) Math.Round(
                ((float) thisObject.LandData.Area / (m_Scene.RegionInfo.RegionSizeX * m_Scene.RegionInfo.RegionSizeY)) *   
                m_Scene.RegionInfo.ObjectCapacity * (float) m_Scene.RegionInfo.RegionSettings.ObjectBonus
            );
        }

        public IPrimCounts GetPrimCounts(UUID parcelID)
        {
            PrimCounts primCounts;

            lock (m_PrimCounts)
            {
                if (m_PrimCounts.TryGetValue(parcelID, out primCounts))
                    return primCounts;

                primCounts = new PrimCounts(parcelID, this);
                m_PrimCounts[parcelID] = primCounts;
            }
            return primCounts;
        }

        #endregion

        void OnPrimCountAdd(ISceneEntity obj)
        {
            // If we're tainted already, don't bother to add. The next
            // access will cause a recount anyway
            lock (m_TaintLock)
            {
                if (!m_Tainted)
                    AddObject(obj);
            }
        }

        void OnObjectBeingRemovedFromScene(ISceneEntity obj)
        {
            // Don't bother to update tainted counts
            lock (m_TaintLock)
            {
                if (!m_Tainted)
                    RemoveObject(obj);
            }
        }

        void OnParcelPrimCountTainted()
        {
            lock (m_TaintLock)
                m_Tainted = true;
        }

        // NOTE: Call under Taint Lock
        void AddObject(ISceneEntity obj)
        {
            ParcelCounts parcelCounts;

            if (obj.IsAttachment)
                return;


            Vector3 pos = obj.AbsolutePosition;
            var landObject = m_Scene.RequestModuleInterface<IParcelManagementModule>().GetLandObject(pos.X, pos.Y);
            if (landObject == null)
                return;
            LandData landData = landObject.LandData;

            if (m_ParcelCounts.TryGetValue(landData.GlobalID, out parcelCounts))
            {
                UUID landOwner = landData.OwnerID;

                parcelCounts.Objects[obj.UUID] = obj;

                // check for temporary objects first
                if (((obj.RootChild.Flags & PrimFlags.TemporaryOnRez) != 0) ||
                    ((obj.RootChild.Flags & PrimFlags.Temporary) != 0)) {
                    parcelCounts.Temporary += obj.PrimCount;
                 
                    // do not add temporary counts further
                    return;
                }

                if (!m_SimwideCounts.ContainsKey(landOwner))
                    m_SimwideCounts.Add(landOwner, 0);
                m_SimwideCounts[landOwner] += obj.PrimCount;

                if (parcelCounts.Users.ContainsKey(obj.OwnerID))
                    parcelCounts.Users[obj.OwnerID] += obj.PrimCount;
                else
                    parcelCounts.Users[obj.OwnerID] = obj.PrimCount;

                if (landData.IsGroupOwned)
                {
                    if (obj.OwnerID == landData.GroupID)
                        parcelCounts.Owner += obj.PrimCount;
                    else if (obj.GroupID == landData.GroupID)
                        parcelCounts.Group += obj.PrimCount;
                    else
                        parcelCounts.Others += obj.PrimCount;
                }
                else
                {
                    if (obj.OwnerID == landData.OwnerID)
                        parcelCounts.Owner += obj.PrimCount;
                    else if (obj.GroupID == landData.GroupID)
                        parcelCounts.Group += obj.PrimCount;
                    else
                        parcelCounts.Others += obj.PrimCount;
                }
            }
        }

        // NOTE: Call under Taint Lock
        void RemoveObject(ISceneEntity obj)
        {
            ParcelCounts parcelCounts;
            if (obj.IsAttachment)
                return;

            var parcelManagment = m_Scene.RequestModuleInterface<IParcelManagementModule>();
            Vector3 pos = obj.AbsolutePosition;
            ILandObject landObject = parcelManagment.GetLandObject(pos.X, pos.Y) ??
                                     parcelManagment.GetNearestAllowedParcel(UUID.Zero, pos.X, pos.Y);
            if (landObject == null) return;

            LandData landData = landObject.LandData;

            if (m_ParcelCounts.TryGetValue(landData.GlobalID, out parcelCounts))
            {
                UUID landOwner = landData.OwnerID;

                // check for temporary objects first
                if (((obj.RootChild.Flags & PrimFlags.TemporaryOnRez) != 0) ||
                    ((obj.RootChild.Flags & PrimFlags.Temporary) != 0)) {
                    parcelCounts.Temporary -= obj.PrimCount;

                    // do not process temporary objects further
                    return;
                }

                foreach (ISceneChildEntity child in obj.ChildrenEntities())
                {
                    bool foundit = false;
                    if (!parcelCounts.Objects.ContainsKey(child.UUID))
                    {
                        //was not found, lets look through all the parcels
                        foreach (ILandObject parcel in parcelManagment.AllParcels())
                        {
                            landData = parcel.LandData;
                            if (!m_ParcelCounts.TryGetValue(landData.GlobalID, out parcelCounts)) continue;
                            landOwner = landData.OwnerID;
                            if (!parcelCounts.Objects.ContainsKey(child.UUID)) continue;
                            foundit = true;
                            break;
                        }
                    }
                    else
                        foundit = true;
                    if (!foundit) continue;
                    parcelCounts.Objects.Remove(child.UUID);
                    if (m_SimwideCounts.ContainsKey(landOwner))
                        m_SimwideCounts[landOwner] -= 1;
                    if (parcelCounts.Users.ContainsKey(obj.OwnerID))
                        parcelCounts.Users[obj.OwnerID] -= 1;

                    if (landData.IsGroupOwned)
                    {
                        if (obj.OwnerID == landData.GroupID)
                            parcelCounts.Owner -= 1;
                        else if (obj.GroupID == landData.GroupID)
                            parcelCounts.Group -= 1;
                        else
                            parcelCounts.Others -= 1;
                    }
                    else
                    {
                        if (obj.OwnerID == landData.OwnerID)
                            parcelCounts.Owner -= 1;
                        else if (obj.GroupID == landData.GroupID)
                            parcelCounts.Group -= 1;
                        else
                            parcelCounts.Others -= 1;
                    }
                }
            }
        }

        public Dictionary<UUID, int> GetAllUserCounts(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                {
                    return new Dictionary<UUID, int>(counts.Users);
                }
            }
            return new Dictionary<UUID, int>();
        }

        public List<ISceneEntity> GetParcelObjects(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                {
                    return new List<ISceneEntity>(counts.Objects.Values);
                }
            }
            return new List<ISceneEntity>();
        }

        public int GetOwnerCount(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                    return counts.Owner;
            }
            return 0;
        }

        public int GetGroupCount(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                    return counts.Group;
            }
            return 0;
        }

        public int GetOthersCount(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                    return counts.Others;
            }
            return 0;
        }

        public int GetSelectedCount(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                    return counts.Selected;
            }
            return 0;
        }

        public int GetSimulatorCount(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                UUID owner;
                if (m_OwnerMap.TryGetValue(parcelID, out owner))
                {
                    int val;
                    if (m_SimwideCounts.TryGetValue(owner, out val))
                        return val;
                }
            }
            return 0;
        }

        public int GetTemporaryCount(UUID parcelID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                    return counts.Temporary;
            }
            return 0;
        }

        public int GetUserCount(UUID parcelID, UUID userID)
        {
            lock (m_TaintLock)
            {
                if (m_Tainted)
                    Recount();

                ParcelCounts counts;
                if (m_ParcelCounts.TryGetValue(parcelID, out counts))
                {
                    int val;
                    if (counts.Users.TryGetValue(userID, out val))
                        return val;
                }
            }
            return 0;
        }

        // NOTE: This method MUST be called while holding the taint lock!
        void Recount()
        {
            m_OwnerMap.Clear();
            m_SimwideCounts.Clear();
            m_ParcelCounts.Clear();

            List<ILandObject> land = m_Scene.RequestModuleInterface<IParcelManagementModule>().AllParcels();

            foreach (LandData landData in land.Select(l => l.LandData))
            {
                m_OwnerMap[landData.GlobalID] = landData.OwnerID;
                m_SimwideCounts[landData.OwnerID] = 0;
                m_ParcelCounts[landData.GlobalID] = new ParcelCounts();
            }

            ISceneEntity[] objlist = m_Scene.Entities.GetEntities();
            foreach (ISceneEntity obj in objlist)
            {
                try
                {
                    AddObject(obj);
                }
                catch (Exception e)
                {
                    // Catch it and move on. This includes situations where splist has inconsistent info
                    MainConsole.Instance.WarnFormat(
                        "[ParcelPrimCountModule]: Problem processing action in Recount: {0}", e);
                }
            }

            List<UUID> primcountKeys = new List<UUID>(m_PrimCounts.Keys);

            foreach (UUID k in primcountKeys.Where(k => !m_OwnerMap.ContainsKey(k)))
            {
                m_PrimCounts.Remove(k);
            }

            m_Tainted = false;
        }

        void OnLandObjectRemoved(UUID RegionID, UUID globalID)
        {
            //Taint everything... we don't know what might have hapened
            TaintPrimCount();
        }

        void OnLandObjectAdded(LandData newParcel)
        {
            //Taint it!
            TaintPrimCount(m_Scene.RequestModuleInterface<IParcelManagementModule>().GetLandObject(newParcel.GlobalID));
        }

        object OnGenericEvent(string FunctionName, object parameters)
        {
            if (FunctionName == "ObjectChangedOwner")
            {
                TaintPrimCount((int) ((ISceneEntity) parameters).AbsolutePosition.X,
                               (int) ((ISceneEntity) parameters).AbsolutePosition.Y);
            }
            else if (FunctionName == "ObjectEnteringNewParcel")
            {
                //Taint the parcels
                //SceneObjectGroup grp = (((Object[])parameters)[0]) as SceneObjectGroup;
                var newParcel = (UUID) (((object[]) parameters)[1]);
                var oldParcel = (UUID) (((object[]) parameters)[2]);
                var oldlandObject = m_Scene.RequestModuleInterface<IParcelManagementModule>().GetLandObject(oldParcel);
                var newlandObject = m_Scene.RequestModuleInterface<IParcelManagementModule>().GetLandObject(newParcel);

                TaintPrimCount(oldlandObject);
                TaintPrimCount(newlandObject);
            }
            return null;
        }
    }

    public class PrimCounts : IPrimCounts
    {
        readonly UUID m_ParcelID;
        readonly PrimCountModule m_Parent;
        readonly UserPrimCounts m_UserPrimCounts;

        public PrimCounts(UUID parcelID, PrimCountModule parent)
        {
            m_ParcelID = parcelID;
            m_Parent = parent;

            m_UserPrimCounts = new UserPrimCounts(this);
        }

        #region IPrimCounts Members

        public int Owner
        {
            get { return m_Parent.GetOwnerCount(m_ParcelID); }
        }

        public int Group
        {
            get { return m_Parent.GetGroupCount(m_ParcelID); }
        }

        public List<ISceneEntity> Objects
        {
            get { return m_Parent.GetParcelObjects(m_ParcelID); }
        }

        public int Others
        {
            get { return m_Parent.GetOthersCount(m_ParcelID); }
        }

        public int Selected
        {
            get { return m_Parent.GetSelectedCount(m_ParcelID); }
        }

        public int Simulator
        {
            get { return m_Parent.GetSimulatorCount(m_ParcelID); }
        }

        public int Temporary
        {
            get { return m_Parent.GetTemporaryCount(m_ParcelID); }
        }

        public int Total
        {
            get { return Group + Owner + Others; }
        }

        public IUserPrimCounts Users
        {
            get { return m_UserPrimCounts; }
        }

        public Dictionary<UUID, int> GetAllUserCounts()
        {
            return m_Parent.GetAllUserCounts(m_ParcelID);
        }

        #endregion

        public int GetUserCount(UUID userID)
        {
            return m_Parent.GetUserCount(m_ParcelID, userID);
        }
    }

    public class UserPrimCounts : IUserPrimCounts
    {
        readonly PrimCounts m_Parent;

        public UserPrimCounts(PrimCounts parent)
        {
            m_Parent = parent;
        }

        #region IUserPrimCounts Members

        public int this[UUID userID]
        {
            get { return m_Parent.GetUserCount(userID); }
        }

        #endregion
    }
}
