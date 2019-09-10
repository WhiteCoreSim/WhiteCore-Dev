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
        public LSL_Integer llTarget(LSL_Vector position, LSL_Float range) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return 0;

            return m_host.registerTargetWaypoint(
                new Vector3((float)position.x, (float)position.y, (float)position.z), (float)range);
        }

        public void llTargetRemove(int number) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.unregisterTargetWaypoint(number);
        }



        public LSL_String llToUpper(string src) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();

            return src.ToUpper();
        }

        public LSL_String llToLower(string src) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();

            return src.ToLower();
        }

        public void llTakeControls(int controls, int accept, int pass_on) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            TaskInventoryItem item;

            lock (m_host.TaskInventory) {
                if (!m_host.TaskInventory.ContainsKey(InventorySelf()))
                    return;
                item = m_host.TaskInventory[InventorySelf()];
            }

            if (item.PermsGranter != UUID.Zero) {
                IScenePresence presence = World.GetScenePresence(item.PermsGranter);

                if (presence != null) {
                    if ((item.PermsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0) {
                        IScriptControllerModule m = presence.RequestModuleInterface<IScriptControllerModule>();
                        if (m != null)
                            m.RegisterControlEventsToScript(controls, accept, pass_on, m_host, m_itemID);
                    }
                }
            }
        }

        public void llTakeCamera(string avatar) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            Deprecated("llTakeCamera", "Use llSetCameraParams instead");
        }

        public void llTargetOmega(LSL_Vector axis, LSL_Float spinrate, LSL_Float gain) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            m_host.OmegaAxis = new Vector3((float)axis.x, (float)axis.y, (float)axis.z);
            m_host.OmegaGain = gain;
            m_host.OmegaSpinRate = spinrate;

            m_host.GenerateRotationalVelocityFromOmega();
            ScriptData script = ScriptProtection.GetScript(m_itemID);
            if (script != null)
                script.TargetOmegaWasSet = true;
            m_host.ScheduleTerseUpdate();
            //m_host.SendTerseUpdateTts();
        }

        public DateTime llTeleportAgentHome(LSL_Key _agent) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            string agent = _agent.ToString();

            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId)) {
                IScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null) {
                    // agent must be over the owners land
                    IParcelManagementModule parcelManagement = World.RequestModuleInterface<IParcelManagementModule>();
                    if (parcelManagement != null) {
                        if (m_host.OwnerID != parcelManagement.GetLandObject(
                            presence.AbsolutePosition.X, presence.AbsolutePosition.Y).LandData.OwnerID &&
                            !World.Permissions.CanIssueEstateCommand(m_host.OwnerID, false)) {
                            return PScriptSleep(m_sleepMsOnTeleportAgentHome);
                        }
                    }

                    //Send disable cancel so that the agent cannot attempt to stay in the region
                    presence.ControllingClient.SendTeleportStart((uint)TeleportFlags.DisableCancel);
                    IEntityTransferModule transferModule = World.RequestModuleInterface<IEntityTransferModule>();
                    if (transferModule != null)
                        transferModule.TeleportHome(agentId, presence.ControllingClient);
                    else
                        presence.ControllingClient.SendTeleportFailed("Unable to perform teleports on this simulator.");
                }
            }
            return PScriptSleep(m_sleepMsOnTeleportAgentHome);
        }

        public DateTime llTextBox(string agent, string message, int chatChannel) {
            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();

            if (dm == null)
                return DateTime.Now;

            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;

            UUID av = new UUID();
            if (!UUID.TryParse(agent, out av)) {
                Error("llDialog", "First parameter must be a key");
                return DateTime.Now;
            }

            if (message != null && message.Length > 1024)
                message = message.Substring(0, 1024);

            dm.SendTextBoxToUser(av, message, chatChannel, m_host.Name, m_host.UUID, m_host.OwnerID);
            return PScriptSleep(m_sleepMsOnTextBox);
        }

        public void llTeleportAgent(LSL_Key avatar, LSL_String landmark, LSL_Vector position, LSL_Vector look_at) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            UUID invItemID = InventorySelf();

            if (invItemID == UUID.Zero)
                return;

            lock (m_host.TaskInventory) {
                if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero) {
                    Error("llTeleportAgent", "No permissions to teleport the agent");
                    return;
                }

                if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) == 0) {
                    Error("llTeleportAgent", "No permissions to teleport the agent");
                    return;
                }
            }

            TaskInventoryItem item = null;
            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == landmark)
                        item = inv.Value;
                }
            }
            if (item == null)
                return;

            IScenePresence presence = World.GetScenePresence(m_host.OwnerID);
            if (presence != null) {
                IEntityTransferModule module = World.RequestModuleInterface<IEntityTransferModule>();
                if (module != null) {
                    if (landmark != "") {
                        var worldAsset = World.AssetService.Get(item.AssetID.ToString());
                        if (worldAsset != null) {
                            var lm = new AssetLandmark(worldAsset);
                            worldAsset.Dispose();

                            module.Teleport(presence, lm.RegionHandle, lm.Position,
                                             look_at.ToVector3(), (uint)TeleportFlags.ViaLocation);
                            lm.Dispose();
                            return;

                        }
                    }
                    // no landmark details
                    module.Teleport(presence, World.RegionInfo.RegionHandle,
                                     position.ToVector3(), look_at.ToVector3(), (uint)TeleportFlags.ViaLocation);

                }
            }
        }

        public void llTeleportAgentGlobalCoords(LSL_Key agent, LSL_Vector global_coordinates,
                                                LSL_Vector region_coordinates, LSL_Vector look_at) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            UUID invItemID = InventorySelf();

            if (invItemID == UUID.Zero)
                return;

            lock (m_host.TaskInventory) {
                if (m_host.TaskInventory[invItemID].PermsGranter == UUID.Zero) {
                    Error("llTeleportAgentGlobalCoords", "No permissions to teleport the agent");
                    return;
                }

                if ((m_host.TaskInventory[invItemID].PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) == 0) {
                    Error("llTeleportAgentGlobalCoords", "No permissions to teleport the agent");
                    return;
                }
            }

            IScenePresence presence = World.GetScenePresence(m_host.OwnerID);
            if (presence != null) {
                IEntityTransferModule module = World.RequestModuleInterface<IEntityTransferModule>();
                if (module != null) {
                    module.Teleport(presence,
                                    Utils.UIntsToLong((uint)global_coordinates.x, (uint)global_coordinates.y),
                                    region_coordinates.ToVector3(), look_at.ToVector3(),
                                    (uint)TeleportFlags.ViaLocation);
                }
            }
        }

        public LSL_String llTransferLindenDollars(LSL_String destination, LSL_Integer amt) {
            LSL_String transferID = UUID.Random().ToString();
            IMoneyModule moneyMod = World.RequestModuleInterface<IMoneyModule>();
            LSL_String data = "";
            LSL_Integer success = LSL_Integer.FALSE;
            TaskInventoryItem item = m_host.TaskInventory[m_itemID];
            UUID destID;
            if (item.PermsGranter == UUID.Zero || (item.PermsMask & ScriptBaseClass.PERMISSION_DEBIT) == 0)
                data = llList2CSV(new LSL_List("MISSING_PERMISSION_DEBIT"));
            else if (!UUID.TryParse(destination, out destID))
                data = llList2CSV(new LSL_List("INVALID_AGENT"));
            else if (amt <= 0)
                data = llList2CSV(new LSL_List("INVALID_AMOUNT"));
            else if (!World.UserAccountService.GetUserAccount(World.RegionInfo.AllScopeIDs, destID).Valid)
                data = llList2CSV(new LSL_List("LINDENDOLLAR_ENTITYDOESNOTEXIST"));
            else if (m_host.ParentEntity.OwnerID == m_host.ParentEntity.GroupID)
                data = llList2CSV(new LSL_List("GROUP_OWNED"));
            else if (moneyMod != null) {
                success = moneyMod.Transfer(UUID.Parse(destination), m_host.OwnerID, amt, "", TransactionType.ObjectPays);
                data =
                    llList2CSV(success
                                   ? new LSL_List(destination, amt)
                                   : new LSL_List("LINDENDOLLAR_INSUFFICIENTFUNDS"));
            } else
                data = llList2CSV(new LSL_List("SERVICE_ERROR"));

            m_ScriptEngine.PostScriptEvent(
                m_itemID,
                m_host.UUID,
                new EventParams("transaction_result",
                                new object[] { transferID, success, data },
                                new DetectParams[0]),
                EventPriority.FirstStart
            );

            return transferID;
        }

    }
}
