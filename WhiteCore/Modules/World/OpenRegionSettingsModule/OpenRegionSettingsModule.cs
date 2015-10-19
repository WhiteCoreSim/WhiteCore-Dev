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


using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.IO;

namespace WhiteCore.Modules.OpenRegionSettingsModule
{
    public class OpenRegionSettingsModule : INonSharedRegionModule, IOpenRegionSettingsModule
    {
        #region IOpenRegionSettingsModule

        public float TerrainDetailScale
        {
            get { return m_settings.TerrainDetailScale; }
            set { m_settings.TerrainDetailScale = value; }
        }

        public bool OffsetOfUTCDST
        {
            get { return m_settings.OffsetOfUTCDST; }
            set { m_settings.OffsetOfUTCDST = value; }
        }

        public bool SetTeenMode
        {
            get { return m_settings.SetTeenMode; }
            set { m_settings.SetTeenMode = value; }
        }

        public float MaxDragDistance
        {
            get { return m_settings.MaxDragDistance; }
            set { m_settings.MaxDragDistance = value; }
        }

        public float DefaultDrawDistance
        {
            get { return m_settings.DefaultDrawDistance; }
            set { m_settings.DefaultDrawDistance = value; }
        }

        public float MaximumPrimScale
        {
            get { return m_settings.MaximumPrimScale; }
            set { m_settings.MaximumPrimScale = value; }
        }

        public float MinimumPrimScale
        {
            get { return m_settings.MinimumPrimScale; }
            set { m_settings.MinimumPrimScale = value; }
        }

        public float MaximumPhysPrimScale
        {
            get { return m_settings.MaximumPhysPrimScale; }
            set { m_settings.MaximumPhysPrimScale = value; }
        }

        public float MaximumHollowSize
        {
            get { return m_settings.MaximumHollowSize; }
            set { m_settings.MaximumHollowSize = value; }
        }

        public float MinimumHoleSize
        {
            get { return m_settings.MinimumHoleSize; }
            set { m_settings.MinimumHoleSize = value; }
        }

        public int MaximumLinkCount
        {
            get { return m_settings.MaximumLinkCount; }
            set { m_settings.MaximumLinkCount = value; }
        }

        public int MaximumLinkCountPhys
        {
            get { return m_settings.MaximumLinkCountPhys; }
            set { m_settings.MaximumLinkCountPhys = value; }
        }

        public OSDArray LSLCommands
        {
            get { return m_settings.LSLCommands; }
            set { m_settings.LSLCommands = value; }
        }

        public float WhisperDistance
        {
            get { return m_settings.WhisperDistance; }
            set { m_settings.WhisperDistance = value; }
        }

        public float SayDistance
        {
            get { return m_settings.SayDistance; }
            set { m_settings.SayDistance = value; }
        }

        public float ShoutDistance
        {
            get { return m_settings.ShoutDistance; }
            set { m_settings.ShoutDistance = value; }
        }

        public bool RenderWater
        {
            get { return m_settings.RenderWater; }
            set { m_settings.RenderWater = value; }
        }

        public int MaximumInventoryItemsTransfer
        {
            get { return m_settings.MaximumInventoryItemsTransfer; }
            set { m_settings.MaximumInventoryItemsTransfer = value; }
        }

        public bool DisplayMinimap
        {
            get { return m_settings.DisplayMinimap; }
            set { m_settings.DisplayMinimap = value; }
        }

        public bool AllowPhysicalPrims
        {
            get { return m_settings.AllowPhysicalPrims; }
            set { m_settings.AllowPhysicalPrims = value; }
        }

        public int OffsetOfUTC
        {
            get { return m_settings.OffsetOfUTC; }
            set { m_settings.OffsetOfUTC = value; }
        }

        public bool EnableTeenMode
        {
            get { return m_settings.EnableTeenMode; }
            set { m_settings.EnableTeenMode = value; }
        }

        public UUID DefaultUnderpants
        {
            get { return m_settings.DefaultUnderpants; }
            set { m_settings.DefaultUnderpants = value; }
        }

        public UUID DefaultUndershirt
        {
            get { return m_settings.DefaultUndershirt; }
            set { m_settings.DefaultUndershirt = value; }
        }

        public bool ForceDrawDistance
        {
            get { return m_settings.ForceDrawDistance; }
            set { m_settings.ForceDrawDistance = value; }
        }

        public int ShowTags
        {
            get { return m_settings.ShowTags; }
            set { m_settings.ShowTags = value; }
        }

        public int MaxGroups
        {
            get { return m_settings.MaxGroups; }
            set { m_settings.MaxGroups = value; }
        }

        public void RegisterGenericValue(string key, string value)
        {
            additionalKVPs.Add(key, value);
        }

        #endregion

        #region Declares

        private readonly Dictionary<string, string> additionalKVPs = new Dictionary<string, string>();
        private IScene m_scene;
        private OpenRegionSettings m_settings;

        #endregion

        #region INonSharedRegionModule

        public void Initialise(IConfigSource source)
        {
        }

        public void Close()
        {
        }

        public void AddRegion(IScene scene)
        {
            m_scene = scene;
            scene.EventManager.OnMakeRootAgent += OnNewClient;
            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_settings = scene.RegionInfo.OpenRegionSettings;
            scene.RegisterModuleInterface<IOpenRegionSettingsModule>(this);
            RegionInfo reg = m_scene.RegionInfo;
            ReadOpenRegionSettings(m_scene.Config.Configs["OpenRegionSettings"], ref reg);
            m_scene.RegionInfo = reg;
        }

        public void RemoveRegion(IScene scene)
        {
        }

        public void RegionLoaded(IScene scene)
        {
            IChatModule chatmodule = scene.RequestModuleInterface<IChatModule>();
            if (chatmodule != null && m_settings != null)
            {
                //Set default chat ranges
                m_settings.WhisperDistance = chatmodule.WhisperDistance;
                m_settings.SayDistance = chatmodule.SayDistance;
                m_settings.ShoutDistance = chatmodule.ShoutDistance;
            }
            /*IScriptModule scriptmodule = scene.RequestModuleInterface<IScriptModule>();
            if (scriptmodule != null)
            {
                List<string> FunctionNames = scriptmodule.GetAllFunctionNames();
                foreach (string FunctionName in FunctionNames)
                {
                    m_settings.LSLCommands.Add(OSD.FromString(FunctionName));
                }
            }*/
        }

        public string Name
        {
            get { return "WorldSettingsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region CAPS

        public OSDMap OnRegisterCaps(UUID agentID, IHttpServer server)
        {
            OSDMap retVal = new OSDMap();
            retVal["DispatchOpenRegionSettings"] = CapsUtil.CreateCAPS("DispatchOpenRegionSettings", "");

            //Sets the OpenRegionSettings
            server.AddStreamHandler(new GenericStreamHandler("POST", retVal["DispatchOpenRegionSettings"],
                                                             delegate(string path, Stream request,
                                                                      OSHttpRequest httpRequest,
                                                                      OSHttpResponse httpResponse)
                                                                 { return DispatchOpenRegionSettings(request, agentID);
                                                                 }));
            return retVal;
        }

        private byte[] DispatchOpenRegionSettings(Stream request, UUID agentID)
        {
            IScenePresence SP = m_scene.GetScenePresence(agentID);
            if (SP == null || !SP.Scene.Permissions.CanIssueEstateCommand(SP.UUID, false))
                return new byte[0];

            OSDMap rm = (OSDMap) OSDParser.DeserializeLLSDXml(HttpServerHandlerHelpers.ReadFully(request));

            m_settings.DefaultDrawDistance = rm["draw_distance"].AsInteger();
            m_settings.ForceDrawDistance = rm["force_draw_distance"].AsBoolean();
            m_settings.DisplayMinimap = rm["allow_minimap"].AsBoolean();
            m_settings.AllowPhysicalPrims = rm["allow_physical_prims"].AsBoolean();
            m_settings.MaxDragDistance = (float) rm["max_drag_distance"].AsReal();
            m_settings.MinimumHoleSize = (float) rm["min_hole_size"].AsReal();
            m_settings.MaximumHollowSize = (float) rm["max_hollow_size"].AsReal();
            m_settings.MaximumInventoryItemsTransfer = rm["max_inventory_items_transfer"].AsInteger();
            m_settings.MaximumLinkCount = (int) rm["max_link_count"].AsReal();
            m_settings.MaximumLinkCountPhys = (int) rm["max_link_count_phys"].AsReal();
            m_settings.MaximumPhysPrimScale = (float) rm["max_phys_prim_scale"].AsReal();
            m_settings.MaximumPrimScale = (float) rm["max_prim_scale"].AsReal();
            m_settings.MinimumPrimScale = (float) rm["min_prim_scale"].AsReal();
            m_settings.RenderWater = rm["render_water"].AsBoolean();
            m_settings.TerrainDetailScale = (float) rm["terrain_detail_scale"].AsReal();
            m_settings.ShowTags = (int) rm["show_tags"].AsReal();
            m_settings.MaxGroups = (int) rm["max_groups"].AsReal();
            m_settings.EnableTeenMode = rm["enable_teen_mode"].AsBoolean();

            m_scene.RegionInfo.OpenRegionSettings = m_settings;

            //Update all clients about changes
            SendToAllClients();

            return new byte[0];
        }

        #endregion

        #region Setup

        private void ReadOpenRegionSettings(IConfig instanceSettings, ref RegionInfo region)
        {
            if (instanceSettings == null)
                return;
            region.OpenRegionSettings.MaxDragDistance = instanceSettings.GetFloat("MaxDragDistance",
                                                                                  region.OpenRegionSettings
                                                                                        .MaxDragDistance);
            region.OpenRegionSettings.DefaultDrawDistance = instanceSettings.GetFloat("DefaultDrawDistance",
                                                                                      region.OpenRegionSettings
                                                                                            .DefaultDrawDistance);


            region.OpenRegionSettings.MaximumPrimScale = instanceSettings.GetFloat("MaximumPrimScale",
                                                                                   region.OpenRegionSettings
                                                                                         .MaximumPrimScale);
            region.OpenRegionSettings.MinimumPrimScale = instanceSettings.GetFloat("MinimumPrimScale",
                                                                                   region.OpenRegionSettings
                                                                                         .MinimumPrimScale);
            region.OpenRegionSettings.MaximumPhysPrimScale = instanceSettings.GetFloat("MaximumPhysPrimScale",
                                                                                       region.OpenRegionSettings
                                                                                             .MaximumPhysPrimScale);


            region.OpenRegionSettings.MaximumHollowSize = instanceSettings.GetFloat("MaximumHollowSize",
                                                                                    region.OpenRegionSettings
                                                                                          .MaximumHollowSize);
            region.OpenRegionSettings.MinimumHoleSize = instanceSettings.GetFloat("MinimumHoleSize",
                                                                                  region.OpenRegionSettings
                                                                                        .MinimumHoleSize);


            region.OpenRegionSettings.MaximumLinkCount = instanceSettings.GetInt("MaximumLinkCount",
                                                                                 region.OpenRegionSettings
                                                                                       .MaximumLinkCount);
            region.OpenRegionSettings.MaximumLinkCountPhys = instanceSettings.GetInt("MaximumLinkCountPhys",
                                                                                     region.OpenRegionSettings
                                                                                           .MaximumLinkCountPhys);


            region.OpenRegionSettings.RenderWater = instanceSettings.GetBoolean("RenderWater",
                                                                                region.OpenRegionSettings.RenderWater);
            region.OpenRegionSettings.MaximumInventoryItemsTransfer =
                instanceSettings.GetInt("MaximumInventoryItemsTransfer",
                                        region.OpenRegionSettings.MaximumInventoryItemsTransfer);
            region.OpenRegionSettings.DisplayMinimap = instanceSettings.GetBoolean("DisplayMinimap",
                                                                                   region.OpenRegionSettings
                                                                                         .DisplayMinimap);
            region.OpenRegionSettings.AllowPhysicalPrims = instanceSettings.GetBoolean("AllowPhysicalPrims",
                                                                                       region.OpenRegionSettings
                                                                                             .AllowPhysicalPrims);
            region.OpenRegionSettings.ForceDrawDistance = instanceSettings.GetBoolean("ForceDrawDistance",
                                                                                      region.OpenRegionSettings
                                                                                            .ForceDrawDistance);

            string offset = instanceSettings.GetString("OffsetOfUTC", region.OpenRegionSettings.OffsetOfUTC.ToString());
            int off;
            if (!int.TryParse(offset, out off))
            {
                if (offset == "SLT" || offset == "PST" || offset == "PDT")
                    off = -8;
                else if (offset == "UTC" || offset == "GMT")
                    off = 0;
            }
            region.OpenRegionSettings.OffsetOfUTC = off;
            region.OpenRegionSettings.OffsetOfUTCDST = instanceSettings.GetBoolean("OffsetOfUTCDST",
                                                                                   region.OpenRegionSettings
                                                                                         .OffsetOfUTCDST);
            region.OpenRegionSettings.EnableTeenMode = instanceSettings.GetBoolean("EnableTeenMode",
                                                                                   region.OpenRegionSettings
                                                                                         .EnableTeenMode);
            region.OpenRegionSettings.ShowTags = instanceSettings.GetInt("ShowTags", region.OpenRegionSettings.ShowTags);
            region.OpenRegionSettings.MaxGroups = instanceSettings.GetInt("MaxGroups",
                                                                          region.OpenRegionSettings.MaxGroups);

            string defaultunderpants = instanceSettings.GetString("DefaultUnderpants",
                                                                  region.OpenRegionSettings.DefaultUnderpants.ToString());
            UUID.TryParse(defaultunderpants, out region.OpenRegionSettings.m_DefaultUnderpants);
            string defaultundershirt = instanceSettings.GetString("DefaultUndershirt",
                                                                  region.OpenRegionSettings.DefaultUndershirt.ToString());
            UUID.TryParse(defaultundershirt, out region.OpenRegionSettings.m_DefaultUndershirt);
        }

        #endregion

        #region Client and Event Queue

        public void OnNewClient(IScenePresence presence)
        {
            OpenRegionInfo(presence);
        }

        public void SendToAllClients()
        {
            m_scene.ForEachScenePresence(OpenRegionInfo);
        }

        public void OpenRegionInfo(IScenePresence presence)
        {
            OSD item = BuildOpenRegionInfo(presence);
            IEventQueueService eq = presence.Scene.RequestModuleInterface<IEventQueueService>();
            if (eq != null)
                eq.Enqueue(item, presence.UUID, presence.Scene.RegionInfo.RegionID);
        }

        public OSD BuildOpenRegionInfo(IScenePresence sp)
        {
            OSDMap map = new OSDMap();

            OSDMap body = new OSDMap();

            if (sp.Scene.Permissions.CanIssueEstateCommand(sp.UUID, false))
                body.Add("EditURL", OSD.FromString(AddOpenRegionSettingsHTMLPage(sp.Scene)));

            if (m_settings.MaxDragDistance != -1)
                body.Add("MaxDragDistance", OSD.FromReal(m_settings.MaxDragDistance));

            if (m_settings.DefaultDrawDistance != -1)
            {
                body.Add("DrawDistance", OSD.FromReal(m_settings.DefaultDrawDistance));
                body.Add("ForceDrawDistance", OSD.FromInteger(m_settings.ForceDrawDistance ? 1 : 0));
            }

            if (m_settings.MaximumPrimScale != -1)
                body.Add("MaxPrimScale", OSD.FromReal(m_settings.MaximumPrimScale));
            if (m_settings.MinimumPrimScale != -1)
                body.Add("MinPrimScale", OSD.FromReal(m_settings.MinimumPrimScale));
            if (m_settings.MaximumPhysPrimScale != -1)
                body.Add("MaxPhysPrimScale", OSD.FromReal(m_settings.MaximumPhysPrimScale));

            if (m_settings.MaximumHollowSize != -1)
                body.Add("MaxHollowSize", OSD.FromReal(m_settings.MaximumHollowSize));
            if (m_settings.MinimumHoleSize != -1)
                body.Add("MinHoleSize", OSD.FromReal(m_settings.MinimumHoleSize));
            body.Add("EnforceMaxBuild", OSD.FromInteger(1));

            if (m_settings.MaximumLinkCount != -1)
                body.Add("MaxLinkCount", OSD.FromInteger(m_settings.MaximumLinkCount));
            if (m_settings.MaximumLinkCountPhys != -1)
                body.Add("MaxLinkCountPhys", OSD.FromInteger(m_settings.MaximumLinkCountPhys));

            body.Add("LSLFunctions", m_settings.LSLCommands);

            body.Add("WhisperDistance", OSD.FromReal(m_settings.WhisperDistance));
            body.Add("SayDistance", OSD.FromReal(m_settings.SayDistance));
            body.Add("ShoutDistance", OSD.FromReal(m_settings.ShoutDistance));

            body.Add("RenderWater", OSD.FromInteger(m_settings.RenderWater ? 1 : 0));

            body.Add("TerrainDetailScale", OSD.FromReal(m_settings.TerrainDetailScale));

            if (m_settings.MaximumInventoryItemsTransfer != -1)
                body.Add("MaxInventoryItemsTransfer", OSD.FromInteger(m_settings.MaximumInventoryItemsTransfer));

            body.Add("AllowMinimap", OSD.FromInteger(m_settings.DisplayMinimap ? 1 : 0));
            body.Add("AllowPhysicalPrims", OSD.FromInteger(m_settings.AllowPhysicalPrims ? 1 : 0));
            body.Add("OffsetOfUTC", OSD.FromInteger(m_settings.OffsetOfUTC));
            body.Add("OffsetOfUTCDST", OSD.FromInteger(m_settings.OffsetOfUTCDST ? 1 : 0));
            body.Add("ToggleTeenMode", OSD.FromInteger(m_settings.EnableTeenMode ? 1 : 0));
            body.Add("SetTeenMode", OSD.FromInteger(m_settings.SetTeenMode ? 1 : 0));

            body.Add("ShowTags", OSD.FromInteger(m_settings.ShowTags));
            if (m_settings.MaxGroups != -1)
                body.Add("MaxGroups", OSD.FromInteger(m_settings.MaxGroups));
            body.Add("AllowParcelWindLight", 1);

            //Add all the generic ones
            foreach (KeyValuePair<string, string> KVP in additionalKVPs)
            {
                body.Add(KVP.Key, OSD.FromString(KVP.Value));
            }

            map.Add("body", body);
            map.Add("message", OSD.FromString("OpenRegionInfo"));
            return map;
        }

        #endregion

        #region ORS HTML

        public string AddOpenRegionSettingsHTMLPage(IScene scene)
        {
            Dictionary<string, object> vars = new Dictionary<string, object>();
            OpenRegionSettings settings = scene.RegionInfo.OpenRegionSettings;
            vars.Add("Default Draw Distance", settings.DefaultDrawDistance.ToString());
            vars.Add("Force Draw Distance", settings.ForceDrawDistance ? "checked" : "");
            vars.Add("Max Drag Distance", settings.MaxDragDistance.ToString());
            vars.Add("Max Prim Scale", settings.MaximumPrimScale.ToString());
            vars.Add("Min Prim Scale", settings.MinimumPrimScale.ToString());
            vars.Add("Max Physical Prim Scale", settings.MaximumPhysPrimScale.ToString());
            vars.Add("Max Hollow Size", settings.MaximumHollowSize.ToString());
            vars.Add("Min Hole Size", settings.MinimumHoleSize.ToString());
            vars.Add("Max Link Count", settings.MaximumLinkCount.ToString());
            vars.Add("Max Link Count Phys", settings.MaximumLinkCountPhys.ToString());
            vars.Add("Max Inventory Items To Transfer", settings.MaximumInventoryItemsTransfer.ToString());
            vars.Add("Terrain Scale", settings.TerrainDetailScale.ToString());
            vars.Add("Show Tags", settings.ShowTags.ToString());
            vars.Add("Render Water", settings.RenderWater ? "checked" : "");
            vars.Add("Allow Minimap", settings.DisplayMinimap ? "checked" : "");
            vars.Add("Allow Physical Prims", settings.AllowPhysicalPrims ? "checked" : "");
            vars.Add("Enable Teen Mode", settings.EnableTeenMode ? "checked" : "");
            vars.Add("Enforce Max Build Constraints", "checked");
            string HTMLPage = "";
            string path = Util.BasePathCombine(System.IO.Path.Combine("data", "OpenRegionSettingsPage.html"));
            if (System.IO.File.Exists(path))
                HTMLPage = System.IO.File.ReadAllText(path);
            return CSHTMLCreator.AddHTMLPage(HTMLPage, "", "OpenRegionSettings", vars, (newVars) =>
                                                                                           {
                                                                                               ParseUpdatedList(scene,
                                                                                                                newVars);
                                                                                               return
                                                                                                   AddOpenRegionSettingsHTMLPage
                                                                                                       (scene);
                                                                                           });
        }

        private void ParseUpdatedList(IScene scene, Dictionary<string, string> vars)
        {
            OpenRegionSettings settings = scene.RegionInfo.OpenRegionSettings;
            settings.DefaultDrawDistance = floatParse(vars["Default Draw Distance"]);
            settings.ForceDrawDistance = vars["Force Draw Distance"] != null;
            settings.MaxDragDistance = floatParse(vars["Max Drag Distance"]);
            settings.MaximumPrimScale = floatParse(vars["Max Prim Scale"]);
            settings.MinimumPrimScale = floatParse(vars["Min Prim Scale"]);
            settings.MaximumPhysPrimScale = floatParse(vars["Max Physical Prim Scale"]);
            settings.MaximumHollowSize = floatParse(vars["Max Hollow Size"]);
            settings.MinimumHoleSize = floatParse(vars["Min Hole Size"]);
            settings.MaximumLinkCount = (int) floatParse(vars["Max Link Count"]);
            settings.MaximumLinkCountPhys = (int) floatParse(vars["Max Link Count Phys"]);
            settings.MaximumInventoryItemsTransfer = (int) floatParse(vars["Max Inventory Items To Transfer"]);
            settings.TerrainDetailScale = floatParse(vars["Terrain Scale"]);
            settings.ShowTags = (int) floatParse(vars["Show Tags"]);
            settings.RenderWater = vars["Render Water"] != null;
            settings.DisplayMinimap = vars["Allow Minimap"] != null;
            settings.AllowPhysicalPrims = vars["Allow Physical Prims"] != null;
            settings.EnableTeenMode = vars["Enable Teen Mode"] != null;
            scene.RegionInfo.OpenRegionSettings = settings;
        }

        private float floatParse(string p)
        {
            float d = 0;
            if (!float.TryParse(p, out d))
                d = 0;
            return d;
        }

        #endregion
    }
}