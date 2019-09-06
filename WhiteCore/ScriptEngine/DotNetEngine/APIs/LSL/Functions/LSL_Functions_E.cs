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
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.list;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {
        public DateTime llEmail (string address, string subject, string message)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.Low, "llEmail", m_host, "LSL", m_itemID))
                return DateTime.Now;
            IEmailModule emailModule = World.RequestModuleInterface<IEmailModule> ();
            if (emailModule == null) {
                Error ("llEmail", "Email module not configured");
                return DateTime.Now;
            }

            emailModule.SendEmail (m_host.UUID, address, subject, message, World);
            return PScriptSleep (m_sleepMsOnEmail);
        }

        public LSL_Integer llEdgeOfWorld (LSL_Vector pos, LSL_Vector dir)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            // edge will be used to pass the Region Coordinates offset
            // we want to check for a neighboring sim
            LSL_Vector edge = new LSL_Vector (0, 0, 0);

            if (dir.x == 0) {
                if (dir.y == 0) {
                    // Direction vector is 0,0 so return
                    // false since we're staying in the sim
                    return 0;
                }
                // Y is the only valid direction
                edge.y = dir.y / Math.Abs (dir.y);
            } else {
                LSL_Float mag;
                if (dir.x > 0) {
                    mag = (World.RegionInfo.RegionSizeX - pos.x) / dir.x;
                } else {
                    mag = (pos.x / dir.x);
                }

                mag = Math.Abs (mag);

                edge.y = pos.y + (dir.y * mag);

                if (edge.y > World.RegionInfo.RegionSizeY || edge.y < 0) {
                    // Y goes out of bounds first
                    edge.y = dir.y / Math.Abs (dir.y);
                } else {
                    // X goes out of bounds first or its a corner exit
                    edge.y = 0;
                    edge.x = dir.x / Math.Abs (dir.x);
                }
            }
            IGridRegisterModule service = World.RequestModuleInterface<IGridRegisterModule> ();
            List<GridRegion> neighbors = new List<GridRegion> ();
            if (service != null)
                neighbors = service.GetNeighbors (World);

            int neighborX = World.RegionInfo.RegionLocX + (int)dir.x;
            int neighborY = World.RegionInfo.RegionLocY + (int)dir.y;

            if (neighbors.Any (neighbor => neighbor.RegionLocX == neighborX && neighbor.RegionLocY == neighborY)) {
                return LSL_Integer.TRUE;
            }

            return LSL_Integer.FALSE;
        }

        public DateTime llEjectFromLand (string pest)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            UUID agentId = new UUID ();
            if (UUID.TryParse (pest, out agentId)) {
                IScenePresence presence = World.GetScenePresence (agentId);
                if (presence != null) {
                    // agent must be over the owners land
                    IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule> ();
                    if (parcelManagement != null) {
                        if (m_host.OwnerID != parcelManagement.GetLandObject (
                            presence.AbsolutePosition.X, presence.AbsolutePosition.Y).LandData.OwnerID &&
                            !World.Permissions.CanIssueEstateCommand (m_host.OwnerID, false)) {
                            return PScriptSleep (m_sleepMsOnEjectFromLand);
                        }
                    }
                    IEntityTransferModule transferModule = World.RequestModuleInterface<IEntityTransferModule> ();
                    if (transferModule != null)
                        transferModule.TeleportHome (agentId, presence.ControllingClient);
                    else
                        presence.ControllingClient.SendTeleportFailed ("Unable to perform teleports on this simulator.");
                }
            }
            return PScriptSleep (m_sleepMsOnEjectFromLand);
        }


        public void llExecCharacterCmd (LSL_Integer command, LSL_List options)
        {
            IBotManager botManager = World.RequestModuleInterface<IBotManager> ();
            if (botManager != null) {
                IBotController controller = botManager.GetCharacterManager (m_host.ParentEntity.UUID);
                if (controller != null) {
                    if (command == ScriptBaseClass.CHARACTER_CMD_JUMP)
                        controller.Jump ();
                    if (command == ScriptBaseClass.CHARACTER_CMD_STOP)
                        controller.StopMoving (false, true);
                }
            }
        }


    }
}
