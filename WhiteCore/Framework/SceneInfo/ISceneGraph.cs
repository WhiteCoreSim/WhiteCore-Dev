/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org
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
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using OpenMetaverse;

namespace WhiteCore.Framework.SceneInfo
{
    public interface ISceneGraph
    {
        ISceneEntity AddNewPrim(
            UUID ownerID, UUID groupID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape);

        Vector3 GetNewRezLocation(Vector3 RayStart, Vector3 RayEnd, UUID RayTargetID, Quaternion rot, byte bypassRayCast,
                                  byte RayEndIsIntersection, bool frontFacesOnly, Vector3 scale, bool FaceCenter);

        bool GetCoarseLocations(out List<Vector3> coarseLocations, out List<UUID> avatarUUIDs, uint maxLocations);
        IScenePresence GetScenePresence(string Name);
        IScenePresence GetScenePresence(uint localID);
        void ForEachScenePresence(Action<IScenePresence> action);
        bool LinkPartToSOG(ISceneEntity grp, ISceneChildEntity part, int linkNum);
        ISceneEntity DuplicateEntity(ISceneEntity entity);
        bool LinkPartToEntity(ISceneEntity entity, ISceneChildEntity part);
        bool DeLinkPartFromEntity(ISceneEntity entity, ISceneChildEntity part);
        void UpdateEntity(ISceneEntity entity, UUID newID);
        bool TryGetEntity(UUID ID, out IEntity entity);
        bool TryGetPart(uint LocalID, out ISceneChildEntity entity);
        bool TryGetEntity(uint LocalID, out IEntity entity);
        bool TryGetPart(UUID ID, out ISceneChildEntity entity);
        void PrepPrimForAdditionToScene(ISceneEntity entity);
        bool AddPrimToScene(ISceneEntity entity);
        bool RestorePrimToScene(ISceneEntity entity, bool force);
        void DelinkPartToScene(ISceneEntity entity);
        bool DeleteEntity(IEntity entity);
        void CheckAllocationOfLocalIds(ISceneEntity group);
        uint AllocateLocalId();
        int LinkSetSorter(ISceneChildEntity a, ISceneChildEntity b);

        List<EntityIntersection> GetIntersectingPrims(Ray hray, float length, int count, bool frontFacesOnly,
                                                      bool faceCenters, bool getAvatars, bool getLand, bool getPrims);

        void RegisterEntityCreatorModule(IEntityCreator entityCreator);

        void TaintPresenceForUpdate(IScenePresence sp, PresenceTaint taint);
    }
}