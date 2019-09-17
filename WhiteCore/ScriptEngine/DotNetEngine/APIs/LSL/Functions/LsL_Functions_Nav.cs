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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using WhiteCore.ScriptEngine.DotNetEngine.Plugins;
using WhiteCore.ScriptEngine.DotNetEngine.Runtime;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using LSL_Float = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger;
using LSL_Key = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.List;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {

        public void llEvade(LSL_String target, LSL_List options) {
            NotImplemented("llEvade");
        }

        public void llFleeFrom(LSL_Vector source, LSL_Float distance, LSL_List options) {
            NotImplemented("llFleeFrom");
        }

        public void llStopPointAt() {
        }

        public void llMoveToTarget(LSL_Vector target, double tau) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.MoveToTarget(new Vector3((float)target.x, (float)target.y, (float)target.z), (float)tau);
        }

        public void llStopMoveToTarget() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.StopMoveToTarget();
        }

        public void llPatrolPoints(LSL_List patrolPoints, LSL_List options) {
            List<Vector3> positions = new List<Vector3>();
            List<TravelMode> travelMode = new List<TravelMode>();
            foreach (object pos in patrolPoints.Data) {
                if (!(pos is LSL_Vector))
                    continue;
                LSL_Vector p = (LSL_Vector)pos;
                positions.Add(p.ToVector3());
                travelMode.Add(TravelMode.Walk);
            }
            IBotManager botManager = World.RequestModuleInterface<IBotManager>();
            if (botManager != null)
                botManager.SetBotMap(m_host.ParentEntity.UUID, positions, travelMode, 1, m_host.ParentEntity.OwnerID);
        }

        public void llNavigateTo(LSL_Vector point, LSL_List options) {
            List<Vector3> positions = new List<Vector3>() { point.ToVector3() };
            List<TravelMode> travelMode = new List<TravelMode>() { TravelMode.Walk };
            IBotManager botManager = World.RequestModuleInterface<IBotManager>();
            int flags = 0;
            if (options.Length > 0)
                flags |= options.GetLSLIntegerItem(0);
            if (botManager != null)
                botManager.SetBotMap(m_host.ParentEntity.UUID, positions, travelMode, flags, m_host.ParentEntity.OwnerID);
        }

        public void llWanderWithin(LSL_Vector origin, LSL_Float distance, LSL_List options) {
            NotImplemented("llWanderWithin");
        }

        public LSL_List llGetClosestNavPoint(LSL_Vector point, LSL_List options) {
            Vector3 diff = new Vector3(0, 0, 0.1f) *
                           (Vector3.RotationBetween(m_host.ParentEntity.AbsolutePosition, point.ToVector3()));
            return new LSL_List(new LSL_Vector((m_host.ParentEntity.AbsolutePosition + diff)));
        }

    }
}
