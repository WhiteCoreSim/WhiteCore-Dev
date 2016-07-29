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
using System.IO;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Gods
{
    public class GodModifiers : INonSharedRegionModule
    {
        #region Declares

        bool m_Enabled = true;
        string m_savestate_oar_directory = "";

        #endregion

        #region INonSharedRegionModule

        public void Initialise (IConfigSource source)
        {
            if (source.Configs ["GodModule"] != null) {
                if (source.Configs ["GodModule"].GetString ("GodModule", Name) != Name) {
                    m_Enabled = false;
                    return;
                }

                m_savestate_oar_directory = source.Configs ["GodModule"].GetString ("DirectoryForSaveStateOARs", m_savestate_oar_directory);
            }
        }

        public void AddRegion (IScene scene)
        {
            if (!m_Enabled)
                return;

            // set the savestate location if not configured
            if (m_savestate_oar_directory == "") {
                var simBase = scene.RequestModuleInterface<ISimulationBase> ();
                m_savestate_oar_directory = Path.Combine (simBase.DefaultDataPath, "Region/SaveStates/");
            }

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
        }

        public void RemoveRegion (IScene scene)
        {
            if (!m_Enabled)
                return;

            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
        }

        public void RegionLoaded (IScene scene)
        {
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public string Name {
            get { return "GodModeModule"; }
        }

        public void Close ()
        {
        }

        #endregion

        #region Client

        void OnNewClient (IClientAPI client)
        {
            client.OnGodUpdateRegionInfoUpdate += GodUpdateRegionInfoUpdate;
            client.OnGodlikeMessage += onGodlikeMessage;
            client.OnSaveState += GodSaveState;
        }

        void OnClosingClient (IClientAPI client)
        {
            client.OnGodUpdateRegionInfoUpdate -= GodUpdateRegionInfoUpdate;
            client.OnGodlikeMessage -= onGodlikeMessage;
            client.OnSaveState -= GodSaveState;
        }

        /// <summary>
        ///     The user requested something from god tools
        /// </summary>
        /// <param name="client"></param>
        /// <param name="requester"></param>
        /// <param name="Method"></param>
        /// <param name="Parameter"></param>
        void onGodlikeMessage (IClientAPI client, UUID requester, string Method, List<string> Parameter)
        {
            //Just rebuild the map
            if (Method == "refreshmapvisibility") {
                if (client.Scene.Permissions.IsGod (client.AgentId)) {
                    //Rebuild the map tile
                    IMapImageGenerator mapModule = client.Scene.RequestModuleInterface<IMapImageGenerator> ();
                    if (mapModule != null)
                        mapModule.CreateTerrainTexture ();
                }
            }
        }

        /// <summary>
        ///     Save the state of the region
        /// </summary>
        /// <param name="client"></param>
        /// <param name="agentID"></param>
        public void GodSaveState (IClientAPI client, UUID agentID)
        {
            //Check for god perms
            if (client.Scene.Permissions.IsGod (client.AgentId)) {
                IScene scene = MainConsole.Instance.ConsoleScene; //Switch back later
                MainConsole.Instance.RunCommand ("change region " + client.Scene.RegionInfo.RegionName);
                MainConsole.Instance.RunCommand (
                    "save oar "
                    + m_savestate_oar_directory
                    + client.Scene.RegionInfo.RegionName.Replace (" ", "%20")// Check if the region name has spaces in them
                    + ".statesave.oar");
                if (scene == null)
                    MainConsole.Instance.RunCommand ("change region root");
                else
                    MainConsole.Instance.RunCommand ("change region " + scene.RegionInfo.RegionName);
            }
        }

        /// <summary>
        ///     The god has requested that we update something in the region configuration
        /// </summary>
        /// <param name="client"></param>
        /// <param name="BillableFactor"></param>
        /// <param name="PricePerMeter"></param>
        /// <param name="EstateID"></param>
        /// <param name="RegionFlags"></param>
        /// <param name="SimName"></param>
        /// <param name="RedirectX"></param>
        /// <param name="RedirectY"></param>
        public void GodUpdateRegionInfoUpdate (IClientAPI client, float BillableFactor, int PricePerMeter, ulong EstateID,
                                              ulong RegionFlags, byte [] SimName, int RedirectX, int RedirectY)
        {
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();

            //Check god perms
            if (!client.Scene.Permissions.IsGod (client.AgentId))
                return;

            string oldRegionName = client.Scene.RegionInfo.RegionName;
            //Update their current region with new information
            if (Utils.BytesToString (SimName) != oldRegionName) {
                client.Scene.RegionInfo.RegionName = Utils.BytesToString (SimName);
                MainConsole.Instance.InfoFormat ("[REGION GOD] Region {0} has been renamed to {1}", oldRegionName, Utils.BytesToString (SimName));
                client.SendAgentAlertMessage ("Region has been renamed to " + Utils.BytesToString (SimName), true);
            }

            // Save the old region locations
            int oldRegionLocX = client.Scene.RegionInfo.RegionLocX;
            int oldRegionLocY = client.Scene.RegionInfo.RegionLocY;
            int newRegionLocX = client.Scene.RegionInfo.RegionLocX;
            int newRegionLocY = client.Scene.RegionInfo.RegionLocY;

            //Set the region loc X and Y
            if (RedirectX != 0) {
                client.Scene.RegionInfo.RegionLocX = RedirectX * Constants.RegionSize;
                newRegionLocX = RedirectX;
            }
            if (RedirectY != 0) {
                client.Scene.RegionInfo.RegionLocY = RedirectY * Constants.RegionSize;
                newRegionLocY = RedirectY;
            }

            // Check if there's changes to display the new coords on the console and inworld
            if (newRegionLocX != oldRegionLocX || newRegionLocY != oldRegionLocY) {
                MainConsole.Instance.InfoFormat ("[REGION GOD] Region {0} has been moved from {1},{2} to {3},{4}",
                                            client.Scene.RegionInfo.RegionName, (oldRegionLocX / Constants.RegionSize), (oldRegionLocY / Constants.RegionSize),
                                                 (client.Scene.RegionInfo.RegionLocX / Constants.RegionSize), (client.Scene.RegionInfo.RegionLocY / Constants.RegionSize));
                client.SendAgentAlertMessage ("Region has been moved from " + (oldRegionLocX / Constants.RegionSize) + "," + (oldRegionLocY / Constants.RegionSize)
                                             + " to " + newRegionLocX + "," + newRegionLocY, true);
            }

            //Update the estate ID
            if (client.Scene.RegionInfo.EstateSettings.EstateID != EstateID) {
                bool changed = estateConnector.LinkRegion (client.Scene.RegionInfo.RegionID, (int)EstateID);
                if (!changed)
                    client.SendAgentAlertMessage ("Unable to connect to the given estate.", false);
                else {
                    client.Scene.RegionInfo.EstateSettings.EstateID = (uint)EstateID;
                    estateConnector.SaveEstateSettings (client.Scene.RegionInfo.EstateSettings);
                }
            }

            //Set the other settings
            client.Scene.RegionInfo.EstateSettings.BillableFactor = BillableFactor;
            client.Scene.RegionInfo.EstateSettings.PricePerMeter = PricePerMeter;
            client.Scene.RegionInfo.EstateSettings.SetFromFlags (RegionFlags);

            client.Scene.RegionInfo.RegionSettings.AllowDamage =
                ((RegionFlags & (ulong)OpenMetaverse.RegionFlags.AllowDamage) == (ulong)OpenMetaverse.RegionFlags.AllowDamage);
            client.Scene.RegionInfo.RegionSettings.FixedSun = ((RegionFlags & (ulong)OpenMetaverse.RegionFlags.SunFixed) ==
            (ulong)OpenMetaverse.RegionFlags.SunFixed);
            client.Scene.RegionInfo.RegionSettings.BlockTerraform =
                ((RegionFlags & (ulong)OpenMetaverse.RegionFlags.BlockTerraform) == (ulong)OpenMetaverse.RegionFlags.BlockTerraform);
            client.Scene.RegionInfo.RegionSettings.Sandbox =
                ((RegionFlags & (ulong)OpenMetaverse.RegionFlags.Sandbox) == (ulong)OpenMetaverse.RegionFlags.Sandbox);

            //Update skipping scripts/physics/collisions
            IEstateModule mod = client.Scene.RequestModuleInterface<IEstateModule> ();
            if (mod != null)
                mod.SetSceneCoreDebug (
                    ((RegionFlags & (ulong)OpenMetaverse.RegionFlags.SkipScripts) == (ulong)OpenMetaverse.RegionFlags.SkipScripts),
                    ((RegionFlags & (ulong)OpenMetaverse.RegionFlags.SkipCollisions) == (ulong)OpenMetaverse.RegionFlags.SkipCollisions),
                    ((RegionFlags & (ulong)OpenMetaverse.RegionFlags.SkipPhysics) == (ulong)OpenMetaverse.RegionFlags.SkipPhysics));

            //Save the changes
            estateConnector.SaveEstateSettings (client.Scene.RegionInfo.EstateSettings);

            //Tell the clients to update all references to the new settings
            foreach (IScenePresence sp in client.Scene.GetScenePresences ()) {
                HandleRegionInfoRequest (sp.ControllingClient, client.Scene);
            }

            //Update the grid server as well
            IGridRegisterModule gridRegisterModule = client.Scene.RequestModuleInterface<IGridRegisterModule> ();
            if (gridRegisterModule != null)
                gridRegisterModule.UpdateGridRegion (client.Scene);
        }

        #endregion

        #region Helpers

        /// <summary>
        ///     Tell the client about the changes
        /// </summary>
        /// <param name="remote_client"></param>
        /// <param name="m_scene"></param>
        void HandleRegionInfoRequest (IClientAPI remote_client, IScene m_scene)
        {
            RegionInfoForEstateMenuArgs args = new RegionInfoForEstateMenuArgs {
                billableFactor = m_scene.RegionInfo.EstateSettings.BillableFactor,
                estateID = m_scene.RegionInfo.EstateSettings.EstateID,
                maxAgents = (byte)m_scene.RegionInfo.RegionSettings.AgentLimit,
                objectBonusFactor = (float)m_scene.RegionInfo.RegionSettings.ObjectBonus,
                parentEstateID = m_scene.RegionInfo.EstateSettings.ParentEstateID,
                pricePerMeter = m_scene.RegionInfo.EstateSettings.PricePerMeter,
                redirectGridX = 0,
                redirectGridY = 0
            };

            IEstateModule estate = m_scene.RequestModuleInterface<IEstateModule> ();
            args.regionFlags = estate == null ? 0 : estate.GetRegionFlags ();

            args.simAccess = m_scene.RegionInfo.AccessLevel;
            args.sunHour = (float)m_scene.RegionInfo.RegionSettings.SunPosition;
            args.terrainLowerLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit;
            args.terrainRaiseLimit = (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit;
            args.useEstateSun = m_scene.RegionInfo.RegionSettings.UseEstateSun;
            args.waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
            args.simName = m_scene.RegionInfo.RegionName;
            args.regionType = m_scene.RegionInfo.RegionType;
            //args.regionTerrain = m_scene.RegionInfo.RegionTerrain;

            remote_client.SendRegionInfoToEstateMenu (args);
        }

        #endregion
    }
}