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
using System.IO;
using System.Linq;
using System.Net;
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;
using WhiteCore.Modules.Terrain.FileLoaders;
using WhiteCore.Modules.Terrain.FloodBrushes;
using WhiteCore.Modules.Terrain.PaintBrushes;

namespace WhiteCore.Modules.Terrain
{
    public class TerrainModule : INonSharedRegionModule, ITerrainModule
    {
        #region StandardTerrainEffects enum

        /// <summary>
        ///     A standard set of terrain brushes and effects recognized by viewers
        /// </summary>
        public enum StandardTerrainEffects : byte
        {
            Flatten = 0,
            Raise = 1,
            Lower = 2,
            Smooth = 3,
            Noise = 4,
            Revert = 5,

            // Extended brushes for WhiteCore
            Erode = 255,
            Weather = 254,
            Olsen = 253,
            Paint = 252
        }

        #endregion

        const int MAX_HEIGHT = 250;
        const int MIN_HEIGHT = 0;

        static readonly List<TerrainModule> m_terrainModules = new List<TerrainModule> ();

        readonly Dictionary<StandardTerrainEffects, ITerrainFloodEffect> m_floodeffects =
            new Dictionary<StandardTerrainEffects, ITerrainFloodEffect> ();

        readonly Dictionary<string, ITerrainLoader> m_loaders = new Dictionary<string, ITerrainLoader> ();

        readonly Dictionary<StandardTerrainEffects, ITerrainPaintableEffect> m_painteffects =
            new Dictionary<StandardTerrainEffects, ITerrainPaintableEffect> ();

        readonly Timer m_queueTimer = new Timer ();
        readonly UndoStack<LandUndoState> m_undo = new UndoStack<LandUndoState> (5);

        ITerrainChannel m_channel;
        protected bool m_noTerrain;
        Vector3 m_previousCheckedPosition = Vector3.Zero;
        long m_queueNextSave;
        ITerrainChannel m_revert;
        int m_savetime = 2; // seconds to wait before saving terrain
        IScene m_scene;
        bool m_sendTerrainUpdatesByViewDistance;
        protected Dictionary<UUID, bool [,]> m_terrainPatchesSent = new Dictionary<UUID, bool [,]> ();
        protected bool m_use3DWater;
        ITerrainChannel m_waterChannel;
        ITerrainChannel m_waterRevert;

        #region INonSharedRegionModule Members

        /// <summary>
        ///     Creates and initializes a terrain module for a region
        /// </summary>
        /// <param name="config">Config for the region</param>
        public void Initialise (IConfigSource config)
        {
            if (config.Configs ["TerrainModule"] != null) {
                m_sendTerrainUpdatesByViewDistance =
                    config.Configs ["TerrainModule"].GetBoolean ("SendTerrainByViewDistance", m_sendTerrainUpdatesByViewDistance);
                m_use3DWater = config.Configs ["TerrainModule"].GetBoolean ("Use3DWater", m_use3DWater);
                m_noTerrain = config.Configs ["TerrainModule"].GetBoolean ("NoTerrain", m_noTerrain);
            }
        }

        public void AddRegion (IScene scene)
        {
            m_scene = scene;
            m_terrainModules.Add (this);

            m_scene.RegisterModuleInterface<ITerrainModule> (this);

            AddConsoleCommands ();

            InstallDefaultEffects ();
            LoadPlugins ();

            if (!m_noTerrain) {
                LoadWorldHeightmap ();
                LoadWorldWaterMap ();
                scene.PhysicsScene.SetTerrain (m_channel, m_channel.GetSerialised ());
                UpdateWaterHeight (scene.RegionInfo.RegionSettings.WaterHeight);
            }

            m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
            m_scene.EventManager.OnClosingClient += OnClosingClient;
            m_scene.EventManager.OnSignificantClientMovement += EventManager_OnSignificantClientMovement;
            m_scene.WhiteCoreEventManager.RegisterEventHandler ("DrawDistanceChanged", WhiteCoreEventManager_OnGenericEvent);
            m_scene.WhiteCoreEventManager.RegisterEventHandler ("SignficantCameraMovement", WhiteCoreEventManager_OnGenericEvent);
            m_scene.EventManager.OnNewPresence += OnNewPresence;

            m_queueTimer.Enabled = false;
            m_queueTimer.AutoReset = true;
            m_queueTimer.Interval = m_savetime * 1000;
            m_queueTimer.Elapsed += TerrainUpdateTimer;
        }

        public void RegionLoaded (IScene scene)
        {
        }

        public void RemoveRegion (IScene scene)
        {
            // remove the event-handlers
            m_scene.EventManager.OnNewClient -= EventManager_OnNewClient;
            m_scene.EventManager.OnClosingClient -= OnClosingClient;
            m_scene.EventManager.OnSignificantClientMovement -= EventManager_OnSignificantClientMovement;
            m_scene.WhiteCoreEventManager.UnregisterEventHandler ("DrawDistanceChanged", WhiteCoreEventManager_OnGenericEvent);
            m_scene.WhiteCoreEventManager.UnregisterEventHandler ("SignficantCameraMovement", WhiteCoreEventManager_OnGenericEvent);
            m_scene.EventManager.OnNewPresence -= OnNewPresence;

            // remove the interface
            m_scene.UnregisterModuleInterface<ITerrainModule> (this);
        }

        public void Close ()
        {
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public string Name {
            get { return "TerrainModule"; }
        }

        #endregion

        #region ITerrainModule Members

        public ITerrainChannel TerrainMap {
            get { return m_channel; }
            set { m_channel = value; }
        }

        public ITerrainChannel TerrainRevertMap {
            get { return m_revert; }
            set { m_revert = value; }
        }

        public ITerrainChannel TerrainWaterMap {
            get { return m_waterChannel; }
            set { m_waterChannel = value; }
        }

        public ITerrainChannel TerrainWaterRevertMap {
            get { return m_waterRevert; }
            set { m_waterRevert = value; }
        }

        public void UpdateWaterHeight (double height)
        {
            short [] waterMap = null;
            if (m_waterChannel != null)
                waterMap = m_waterChannel.GetSerialised ();
            m_scene.PhysicsScene.SetWaterLevel (height, waterMap);
        }

        /// <summary>
        ///     Reset the terrain of this region to the default
        /// </summary>
        public void ResetTerrain ()
        {
            if (!m_noTerrain) {
                TerrainChannel channel = new TerrainChannel (m_scene);
                m_channel = channel;
                m_scene.SimulationDataService.Tainted ();
                m_scene.RegisterModuleInterface (m_channel);
                CheckForTerrainUpdates (false, true, false);
            }
        }

        /// <summary>
        ///     Loads the World Revert heightmap
        /// </summary>
        public void LoadRevertMap ()
        {
            try {
                m_scene.SimulationDataService.LoadTerrain (true, m_scene.RegionInfo.RegionSizeX, m_scene.RegionInfo.RegionSizeY);
                if (m_revert == null) {
                    m_revert = m_channel.MakeCopy ();
                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IOException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldMap() - Failed with exception " + e + " Regenerating");
                // Non standard region size.    If there's an old terrain in the database, it might read past the buffer
                if (m_scene.RegionInfo.RegionSizeX != Constants.RegionSize ||
                    m_scene.RegionInfo.RegionSizeY != Constants.RegionSize) {
                    m_revert = m_channel.MakeCopy ();

                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IndexOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldMap() - Failed with exception " + e + " Regenerating");
                m_revert = m_channel.MakeCopy ();

                m_scene.SimulationDataService.Tainted ();
            } catch (ArgumentOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldMap() - Failed with exception " + e + " Regenerating");
                m_revert = m_channel.MakeCopy ();

                m_scene.SimulationDataService.Tainted ();
            } catch (Exception e) {
                MainConsole.Instance.Warn ("[Terrain]: Scene.cs: LoadWorldMap() - Failed with exception " + e);
            }
        }

        /// <summary>
        ///     Loads the World heightmap
        /// </summary>
        public void LoadWorldHeightmap ()
        {
            try {
                m_scene.SimulationDataService.LoadTerrain (false, m_scene.RegionInfo.RegionSizeX,
                                                          m_scene.RegionInfo.RegionSizeY);
                if (m_channel == null) {
                    MainConsole.Instance.Info ("[Terrain]: No default terrain. Generating a new terrain.");
                    m_channel = new TerrainChannel (m_scene);

                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IOException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldMap() - Failed with exception " + e + " Regenerating");
                // Non standard region size.    If there's an old terrain in the database, it might read past the buffer
                if (m_scene.RegionInfo.RegionSizeX != Constants.RegionSize ||
                    m_scene.RegionInfo.RegionSizeY != Constants.RegionSize) {
                    m_channel = new TerrainChannel (m_scene);

                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IndexOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldMap() - Failed with exception " + e + " Regenerating");
                m_channel = new TerrainChannel (m_scene);

                m_scene.SimulationDataService.Tainted ();
            } catch (ArgumentOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldMap() - Failed with exception " + e + " Regenerating");
                m_channel = new TerrainChannel (m_scene);

                m_scene.SimulationDataService.Tainted ();
            } catch (Exception e) {
                MainConsole.Instance.Warn ("[Terrain]: Scene.cs: LoadWorldMap() - Failed with exception " + e);
                m_channel = new TerrainChannel (m_scene);

                m_scene.SimulationDataService.Tainted ();
            }
            LoadRevertMap ();
            m_scene.RegisterModuleInterface (m_channel);
        }

        public void UndoTerrain (ITerrainChannel channel)
        {
            m_channel = channel;
        }

        /// <summary>
        /// Gets the terrain loader.
        /// </summary>
        /// <returns>The terrain loader.</returns>
        /// <param name="fileName">File name of the terrain heightmap.</param>
        ITerrainLoader GetTerrainLoader (string fileName)
        {
            ITerrainLoader loader = null;

            // find the loader to use..
            var fileExt = Path.GetExtension (fileName.ToLower ());
            foreach (KeyValuePair<string, ITerrainLoader> floader in m_loaders) {
                if (fileExt != floader.Key)
                    continue;

                loader = floader.Value;
            }
            return loader;
        }


        /// <summary>
        ///     Loads a terrain file from disk and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public void LoadFromFile (string filename, int offsetX, int offsetY)
        {

            var loader = GetTerrainLoader (filename);
            if (loader != null) {
                lock (m_scene) {
                    try {
                        MainConsole.Instance.Info ("[Terrain]: Loading " + filename + " to " + m_scene.RegionInfo.RegionName);
                        ITerrainChannel channel = loader.LoadFile (filename, m_scene);
                        channel.Scene = m_scene;

                        if (m_scene.RegionInfo.RegionSizeY == channel.Height &&
                            m_scene.RegionInfo.RegionSizeX == channel.Width) {
                            if (offsetX > 0 || offsetY > 0)
                                MainConsole.Instance.Warn ("[Terrain]: The terrain file is the same size as the region! Offsets will be ignored");

                            m_channel = channel;
                            m_scene.RegisterModuleInterface (m_channel);
                            MainConsole.Instance.DebugFormat ("[Terrain]: Loaded terrain, wd/ht: {0}/{1}", channel.Width,
                                channel.Height);
                        } else {
                            //Make sure it is in bounds
                            if ((offsetX + channel.Width) > m_channel.Width ||
                                (offsetY + channel.Height) > m_channel.Height) {
                                MainConsole.Instance.Error (
                                    "[Terrain]: Unable to load heightmap, the terrain details you have specified are not able to fit in the current region." +
                                    "\n      Maybe the 'terrain load-tile' command is what you need?");
                                return;
                            }

                            // Merge the new terrain at the specified offset with the existing terrain
                            for (int x = offsetX; x < offsetX + channel.Width; x++) {
                                for (int y = offsetY; y < offsetY + channel.Height; y++) {
                                    m_channel [x, y] = channel [x - offsetX, y - offsetY];
                                }
                            }
                            MainConsole.Instance.DebugFormat ("[Terrain]: Loaded terrain, wd/ht: {0}/{1}",
                                channel.Width,
                                channel.Height);

                        }
                        UpdateRevertMap ();
                    } catch (NotImplementedException) {
                        MainConsole.Instance.Error ("[Terrain]: Unable to load heightmap, the " + loader +
                        " parser does not support file loading. (May be save only)");
                    } catch (FileNotFoundException) {
                        MainConsole.Instance.ErrorFormat (
                            "[Terrain]: Unable to load heightmap, file {0} not found. (Directory permissions errors may also cause this)", filename);
                    } catch (ArgumentException e) {
                        MainConsole.Instance.ErrorFormat ("[Terrain]: Unable to load heightmap: {0}", e.Message);
                    } catch (Exception e) {
                        MainConsole.Instance.ErrorFormat ("[Terrain]: Something crashed during load. {0} Exception.", e);
                    }
                }

                CheckForTerrainUpdates ();
                MainConsole.Instance.Info ("[Terrain]: File (" + filename + ") loaded successfully");
                return;
            }

            MainConsole.Instance.Error ("[Terrain]: Unable to locate a file loader for " + filename);
        }


        /// <summary>
        ///     Saves the current heightmap to a specified file.
        /// </summary>
        /// <param name="filename">The destination filename</param>
        public void SaveToFile (string filename)
        {
            if (File.Exists (filename)) {
                if (MainConsole.Instance.Prompt ("File '" + filename + "' exists. Overwrite?", "yes") != "yes")
                    return;

                File.Delete (filename);
            }

            try {
                var loader = GetTerrainLoader (filename);
                if (loader != null) {

                    loader.SaveFile (filename, m_channel);
                    return;
                }
            } catch (NotImplementedException) {
                MainConsole.Instance.Error ("[Terrain]: Unable to save to " + filename +
                                           ", saving of this file format has not been implemented.");
            } catch (IOException ioe) {
                MainConsole.Instance.ErrorFormat ("[Terrain]: Unable to save to {0}, {1}", filename, ioe.Message);
            } catch (Exception e) {
                MainConsole.Instance.Error (string.Format ("[Terrain]: Something crashed during save. {0} Exception.", e));
            }
        }

        /// <summary>
        ///     Loads a terrain file from the specified URI
        /// </summary>
        /// <param name="filename">The name of the terrain to load</param>
        /// <param name="pathToTerrainHeightmap">The URI to the terrain height map</param>
        public void LoadFromStream (string filename, Uri pathToTerrainHeightmap)
        {
            LoadFromStream (filename, URIFetch (pathToTerrainHeightmap));
        }

        /// <summary>
        ///     Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        public void LoadFromStream (string filename, Stream stream)
        {
            var loader = GetTerrainLoader (filename);
            if (loader != null) {
                lock (m_scene) {
                    try {
                        ITerrainChannel channel = loader.LoadStream (stream, m_scene);
                        if (channel != null) {
                            channel.Scene = m_scene;
                            if (m_channel.Height == channel.Height &&
                                m_channel.Width == channel.Width) {

                                m_channel = channel;
                                m_scene.RegisterModuleInterface (m_channel);
                            } else {
                                //Make sure it is in bounds
                                if ((channel.Width) > m_channel.Width ||
                                    (channel.Height) > m_channel.Height) {

                                    for (int x = 0; x < m_channel.Width; x++) {
                                        for (int y = 0; y < m_channel.Height; y++) {
                                            m_channel [x, y] = channel [x, y];
                                        }
                                    }
                                    //MainConsole.Instance.Error("[Terrain]: Unable to load heightmap, the terrain you have given is larger than the current region.");
                                    //return;
                                } else {
                                    //Merge the terrains together at the specified offset
                                    for (int x = 0; x < channel.Width; x++) {
                                        for (int y = 0; y < channel.Height; y++) {
                                            m_channel [x, y] = channel [x, y];
                                        }
                                    }
                                    MainConsole.Instance.DebugFormat ("[Terrain]: Loaded terrain, wd/ht: {0}/{1}",
                                                                     channel.Width,
                                                                     channel.Height);
                                }
                            }
                            UpdateRevertMap ();
                        }
                    } catch (NotImplementedException) {
                        MainConsole.Instance.Error ("[Terrain]: Unable to load heightmap, the " + loader +
                                                   " parser does not support file loading. (May be save only)");
                    }
                }

                CheckForTerrainUpdates ();
                MainConsole.Instance.Info ("[Terrain]: File (" + filename + ") loaded successfully");
                return;
            }

            MainConsole.Instance.ErrorFormat ("[Terrain]: Unable to load heightmap from {0}, no file loader available for that format.", filename);
        }

        /// <summary>
        ///     Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public void LoadFromStream (string filename, Stream stream, int offsetX, int offsetY)
        {
            m_channel = InternalLoadFromStream (filename, stream, offsetX, offsetY, m_channel);
            if (m_channel != null) {
                CheckForTerrainUpdates ();
                m_scene.RegisterModuleInterface (m_channel);
            }
        }

        /// <summary>
        ///     Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public void LoadWaterFromStream (string filename, Stream stream, int offsetX, int offsetY)
        {
            m_waterChannel = InternalLoadFromStream (filename, stream, offsetX, offsetY, m_waterChannel);
            if (m_waterChannel != null) {
                CheckForTerrainUpdates (false, false, true);
            }
        }

        /// <summary>
        ///     Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public void LoadRevertMapFromStream (string filename, Stream stream, int offsetX, int offsetY)
        {
            m_revert = InternalLoadFromStream (filename, stream, offsetX, offsetY, m_revert);
        }

        /// <summary>
        ///     Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        public void LoadWaterRevertMapFromStream (string filename, Stream stream, int offsetX, int offsetY)
        {
            m_waterRevert = InternalLoadFromStream (filename, stream, offsetX, offsetY, m_waterRevert);
        }

        /// <summary>
        ///     Modify Land
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pos">Land-position (X,Y,0)</param>
        /// <param name="size">The size of the brush (0=small, 1=medium, 2=large)</param>
        /// <param name="action">0=LAND_LEVEL, 1=LAND_RAISE, 2=LAND_LOWER, 3=LAND_SMOOTH, 4=LAND_NOISE, 5=LAND_REVERT</param>
        /// <param name="agentId">UUID of script-owner</param>
        public void ModifyTerrain (UUID user, Vector3 pos, byte size, byte action, UUID agentId)
        {
            float duration = 0.25f;
            if (action == 0)
                duration = 4.0f;
            client_OnModifyTerrain (user, pos.Z, duration, size, action, pos.Y, pos.X, pos.Y, pos.X, agentId, size);
        }

        /// <summary>
        ///     Saves the current heightmap to a specified stream.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="filename">The destination filename.  Used here only to identify the image type</param>
        /// <param name="stream"></param>
        public void SaveToStream (ITerrainChannel channel, string filename, Stream stream)
        {
            try {
                //foreach (
                //    KeyValuePair<string, ITerrainLoader> loader in
                //        m_loaders.Where(loader => Path.GetExtension(filename.ToLower()) == loader.Key))
                //{
                var loader = GetTerrainLoader (filename);
                if (loader != null) {

                    loader.SaveStream (stream, channel);
                    return;
                }
            } catch (NotImplementedException) {
                MainConsole.Instance.Error ("[Terrain]: Unable to save to " + filename +
                                           ", saving of this file format has not been implemented.");
            }
        }

        public void TaintTerrain ()
        {
            CheckForTerrainUpdates ();
        }

        #endregion

        #region Plugin Loading Methods

        void LoadPlugins ()
        {
            string plugineffectsPath = "Terrain";

            // Load the files in the Terrain/ dir
            if (!Directory.Exists (plugineffectsPath))
                return;

            ITerrainLoader [] loaders = WhiteCoreModuleLoader.PickupModules<ITerrainLoader> ().ToArray ();
            foreach (ITerrainLoader terLoader in loaders) {
                m_loaders [terLoader.FileExtension] = terLoader;
            }
        }

        #endregion

        public void TerrainUpdateTimer (object sender, EventArgs ea)
        {
            long now = DateTime.Now.Ticks;

            if (m_queueNextSave > 0 && m_queueNextSave < now) {
                m_queueNextSave = 0;
                m_scene.PhysicsScene.SetTerrain (m_channel, m_channel.GetSerialised ());

                if (m_queueNextSave == 0)
                    m_queueTimer.Stop ();
            }
        }

        public void QueueTerrainUpdate ()
        {
            m_queueNextSave = DateTime.Now.Ticks + Convert.ToInt64 (m_savetime * 1000 * 10000);
            m_queueTimer.Start ();
        }

        /// <summary>
        ///     Installs terrain brush hook to IClientAPI
        /// </summary>
        /// <param name="client"></param>
        void EventManager_OnNewClient (IClientAPI client)
        {
            client.OnModifyTerrain += client_OnModifyTerrain;
            client.OnBakeTerrain += client_OnBakeTerrain;
            client.OnLandUndo += client_OnLandUndo;
            client.OnGodlikeMessage += client_onGodlikeMessage;
            client.OnRegionHandShakeReply += SendLayerData;

            //Add them to the cache
            lock (m_terrainPatchesSent) {
                if (!m_terrainPatchesSent.ContainsKey (client.AgentId)) {
                    IScenePresence agent = m_scene.GetScenePresence (client.AgentId);
                    if (agent != null && agent.IsChildAgent) {
                        //If the avatar is a child agent, we need to send the terrain data initially
                        EventManager_OnSignificantClientMovement (agent);
                    }
                }
            }
        }

        void OnClosingClient (IClientAPI client)
        {
            client.OnModifyTerrain -= client_OnModifyTerrain;
            client.OnBakeTerrain -= client_OnBakeTerrain;
            client.OnLandUndo -= client_OnLandUndo;
            client.OnGodlikeMessage -= client_onGodlikeMessage;
            client.OnRegionHandShakeReply -= SendLayerData;

            //Remove them from the cache
            lock (m_terrainPatchesSent) {
                m_terrainPatchesSent.Remove (client.AgentId);
            }
        }

        /// <summary>
        ///     Send the region heightmap to the client
        /// </summary>
        /// <param name="RemoteClient">Client to send to</param>
        public void SendLayerData (IClientAPI RemoteClient)
        {
            if (!m_sendTerrainUpdatesByViewDistance && !m_noTerrain) {
                //Default way, send the full terrain at once
                RemoteClient.SendLayerData (m_channel.GetSerialised ());
            } else {
                //Send only what the client can see,
                //  but the client isn't loaded yet, wait until they get set up
                //  The first agent update they send will trigger the DrawDistanceChanged event and send the land
            }
        }

        object WhiteCoreEventManager_OnGenericEvent (string FunctionName, object parameters)
        {
            if (FunctionName == "DrawDistanceChanged" || FunctionName == "SignficantCameraMovement") {
                SendTerrainUpdatesForClient ((IScenePresence)parameters);
            }
            return null;
        }

        void EventManager_OnSignificantClientMovement (IScenePresence presence)
        {
            if (Vector3.DistanceSquared (presence.AbsolutePosition, m_previousCheckedPosition) > 16 * 16) {
                m_previousCheckedPosition = presence.AbsolutePosition;
                SendTerrainUpdatesForClient (presence);
            }
        }

        void OnNewPresence (IScenePresence presence)
        {
            SendTerrainUpdatesForClient (presence);
        }

        protected void SendTerrainUpdatesForClient (IScenePresence presence)
        {
            if (presence == null)
                return;

            if (!m_sendTerrainUpdatesByViewDistance || m_noTerrain || presence.DrawDistance < 0.1f)
                return;


            bool [,] terrainarray;
            lock (m_terrainPatchesSent) {
                m_terrainPatchesSent.TryGetValue (presence.UUID, out terrainarray);
            }
            bool fillLater = false;
            if (terrainarray == null) {
                int xSize = m_scene.RegionInfo.RegionSizeX != int.MaxValue
                                ? m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize
                                : Constants.RegionSize / Constants.TerrainPatchSize;
                int ySize = m_scene.RegionInfo.RegionSizeX != int.MaxValue
                                ? m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize
                                : Constants.RegionSize / Constants.TerrainPatchSize;
                terrainarray = new bool [xSize, ySize];
                fillLater = true;
            }

            List<int> xs = new List<int> ();
            List<int> ys = new List<int> ();
            int startX = (((int)(presence.AbsolutePosition.X - presence.DrawDistance)) / Constants.TerrainPatchSize) - 2;
            startX = Math.Max (startX, 0);
            startX = Math.Min (startX, m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize);
            int startY = (((int)(presence.AbsolutePosition.Y - presence.DrawDistance)) / Constants.TerrainPatchSize) - 2;
            startY = Math.Max (startY, 0);
            startY = Math.Min (startY, m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize);
            int endX = (((int)(presence.AbsolutePosition.X + presence.DrawDistance)) / Constants.TerrainPatchSize) + 2;
            endX = Math.Max (endX, 0);
            endX = Math.Min (endX, m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize);
            int endY = (((int)(presence.AbsolutePosition.Y + presence.DrawDistance)) / Constants.TerrainPatchSize) + 2;
            endY = Math.Max (endY, 0);
            endY = Math.Min (endY, m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize);
            for (int x = startX; x < endX; x++) {
                for (int y = startY; y < endY; y++) {
                    if (x < 0 || y < 0 || x >= m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize ||
                        y >= m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize)
                        continue;
                    //Need to make sure we don't send the same ones over and over
                    if (!terrainarray [x, y]) {
                        Vector3 posToCheckFrom = new Vector3 (presence.AbsolutePosition.X % m_scene.RegionInfo.RegionSizeX,
                                                             presence.AbsolutePosition.Y % m_scene.RegionInfo.RegionSizeY,
                                                             presence.AbsolutePosition.Z);
                        //Check which has less distance, camera or avatar position, both have to be done
                        if (Util.DistanceLessThan (posToCheckFrom,
                                                  new Vector3 (
                                                      x * Constants.TerrainPatchSize,
                                                      y * Constants.TerrainPatchSize,
                                                      0), presence.DrawDistance + 50) ||
                            Util.DistanceLessThan (presence.CameraPosition,
                                                  new Vector3 (x * Constants.TerrainPatchSize, y * Constants.TerrainPatchSize,
                                                              0), presence.DrawDistance + 50))
                        //Its not a radius, its a diameter and we add 35 so that it doesn't look like it cuts off
                        {
                            //They can see it, send it to them
                            terrainarray [x, y] = true;
                            xs.Add (x);
                            ys.Add (y);
                            //Wait and send them all at once
                            //presence.ControllingClient.SendLayerData(x, y, serializedMap);
                        }
                    }
                }
            }
            if (xs.Count != 0) {
                //Send all the terrain patches at once
                presence.ControllingClient.SendLayerData (xs.ToArray (), ys.ToArray (), m_channel.GetSerialised (),
                                                         TerrainPatch.LayerType.Land);
                if (m_use3DWater) {
                    //Send all the water patches at once
                    presence.ControllingClient.SendLayerData (xs.ToArray (), ys.ToArray (),
                                                             m_waterChannel.GetSerialised (),
                                                             TerrainPatch.LayerType.Water);
                }
            }
            if ((xs.Count != 0) || (fillLater)) {
                if (m_terrainPatchesSent.ContainsKey (presence.UUID)) {
                    lock (m_terrainPatchesSent)
                        m_terrainPatchesSent [presence.UUID] = terrainarray;
                } else {
                    lock (m_terrainPatchesSent)
                        m_terrainPatchesSent.Add (presence.UUID, terrainarray);
                }
            }
        }

        /// <summary>
        ///     Reset the terrain of this region to the default
        /// </summary>
        public void ResetWater ()
        {
            if (!m_noTerrain) {
                TerrainChannel channel = new TerrainChannel (m_scene);
                m_waterChannel = channel;
                m_scene.SimulationDataService.Tainted ();
                CheckForTerrainUpdates (false, true, true);
            }
        }

        /// <summary>
        ///     Loads the World Revert heightmap
        /// </summary>
        public void LoadRevertWaterMap ()
        {
            try {
                m_scene.SimulationDataService.LoadWater (true, m_scene.RegionInfo.RegionSizeX,
                                                        m_scene.RegionInfo.RegionSizeY);
                if (m_waterRevert == null) {
                    m_waterRevert = m_waterChannel.MakeCopy ();
                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IOException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadRevertWaterMap() - Failed with exception " + e +
                                          " Regenerating");
                // Non standard region size.    If there's an old terrain in the database, it might read past the buffer
                if (m_scene.RegionInfo.RegionSizeX != Constants.RegionSize ||
                    m_scene.RegionInfo.RegionSizeY != Constants.RegionSize) {
                    m_waterRevert = m_waterChannel.MakeCopy ();

                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IndexOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadRevertWaterMap() - Failed with exception " + e +
                                          " Regenerating");
                m_waterRevert = m_waterChannel.MakeCopy ();

                m_scene.SimulationDataService.Tainted ();
            } catch (ArgumentOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadRevertWaterMap() - Failed with exception " + e +
                                          " Regenerating");
                m_waterRevert = m_waterChannel.MakeCopy ();

                m_scene.SimulationDataService.Tainted ();
            } catch (Exception e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadRevertWaterMap() - Failed with exception " + e);
                m_waterRevert = m_waterChannel.MakeCopy ();

                m_scene.SimulationDataService.Tainted ();
            }
        }

        /// <summary>
        ///     Loads the World heightmap
        /// </summary>
        public void LoadWorldWaterMap ()
        {
            if (!m_use3DWater)
                return;
            try {
                m_scene.SimulationDataService.LoadWater (false, m_scene.RegionInfo.RegionSizeX,
                                                        m_scene.RegionInfo.RegionSizeY);
                if (m_waterChannel == null) {
                    MainConsole.Instance.Info ("[Terrain]: No default water. Generating a new water.");
                    m_waterChannel = new TerrainChannel (m_scene);
                    for (int x = 0; x < m_waterChannel.Height; x++) {
                        for (int y = 0; y < m_waterChannel.Height; y++) {
                            m_waterChannel [x, y] = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
                        }
                    }

                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IOException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldWaterMap() - Failed with exception " + e +
                                          " Regenerating");
                // Non standard region size.    If there's an old terrain in the database, it might read past the buffer
                if (m_scene.RegionInfo.RegionSizeX != Constants.RegionSize ||
                    m_scene.RegionInfo.RegionSizeY != Constants.RegionSize) {
                    m_waterChannel = new TerrainChannel (m_scene);
                    for (int x = 0; x < m_waterChannel.Height; x++) {
                        for (int y = 0; y < m_waterChannel.Height; y++) {
                            m_waterChannel [x, y] = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
                        }
                    }

                    m_scene.SimulationDataService.Tainted ();
                }
            } catch (IndexOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldWaterMap() - Failed with exception " + e +
                                          " Regenerating");
                m_waterChannel = new TerrainChannel (m_scene);
                for (int x = 0; x < m_waterChannel.Height; x++) {
                    for (int y = 0; y < m_waterChannel.Height; y++) {
                        m_waterChannel [x, y] = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
                    }
                }

                m_scene.SimulationDataService.Tainted ();
            } catch (ArgumentOutOfRangeException e) {
                MainConsole.Instance.Warn ("[Terrain]: LoadWorldWaterMap() - Failed with exception " + e +
                                          " Regenerating");
                m_waterChannel = new TerrainChannel (m_scene);
                for (int x = 0; x < m_waterChannel.Height; x++) {
                    for (int y = 0; y < m_waterChannel.Height; y++) {
                        m_waterChannel [x, y] = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
                    }
                }

                m_scene.SimulationDataService.Tainted ();
            } catch (Exception e) {
                MainConsole.Instance.Warn ("[Terrain]: Scene.cs: LoadWorldMap() - Failed with exception " + e);
                m_waterChannel = new TerrainChannel (m_scene);
                for (int x = 0; x < m_waterChannel.Height; x++) {
                    for (int y = 0; y < m_waterChannel.Height; y++) {
                        m_waterChannel [x, y] = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
                    }
                }

                m_scene.SimulationDataService.Tainted ();
            }
            LoadRevertWaterMap ();
        }

        /// <summary>
        ///     Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="update"></param>
        public ITerrainChannel InternalLoadFromStream (string filename, Stream stream, int offsetX, int offsetY,
                                                      ITerrainChannel update)
        {
            ITerrainChannel channel = null;

            // find the loader to use..
            //var fileExt = Path.GetExtension(filename.ToLower());
            //foreach (KeyValuePair<string, ITerrainLoader> floader in m_loaders)
            // {
            //	if (fileExt != floader.Key)
            //		continue;
            //
            //	ITerrainLoader loader = floader.Value;
            //{
            var loader = GetTerrainLoader (filename);
            if (loader != null) {
                lock (m_scene) {
                    try {
                        channel = loader.LoadStream (stream, m_scene);
                        if (channel != null) {
                            channel.Scene = m_scene;
                            if (update == null || 
                                (update.Height == channel.Height && update.Width == channel.Width)) {

                                if (m_scene.RegionInfo.RegionSizeX != channel.Width ||
                                    m_scene.RegionInfo.RegionSizeY != channel.Height) {

                                    if ((channel.Width) > m_scene.RegionInfo.RegionSizeX ||
                                        (channel.Height) > m_scene.RegionInfo.RegionSizeY) {

                                        TerrainChannel c = new TerrainChannel (true, m_scene);
                                        for (int x = 0; x < m_scene.RegionInfo.RegionSizeX; x++) {
                                            for (int y = 0; y < m_scene.RegionInfo.RegionSizeY; y++) {
                                                c [x, y] = channel [x, y];
                                            }
                                        }
                                        return c;
                                    }
                                    return null;
                                }
                            } else {
                                //Make sure it is in bounds
                                if ((offsetX + channel.Width) > update.Width ||
                                    (offsetY + channel.Height) > update.Height) {
                                    MainConsole.Instance.Error (
                                        "[Terrain]: Unable to load heightmap, the terrain you have given is larger than the current region.");
                                    return null;
                                }        

                                //Merge the terrains together at the specified offset
                                for (int x = offsetX; x < offsetX + channel.Width; x++) {
                                    for (int y = offsetY; y < offsetY + channel.Height; y++) {
                                        update [x, y] = channel [x - offsetX, y - offsetY];
                                    }
                                }
                                return update;
                            }
                        }
                    } catch (NotImplementedException) {
                        MainConsole.Instance.Error ("[Terrain]: Unable to load heightmap, the " + loader +
                                                   " parser does not support file loading. (May be save only)");
                    }
                }

                MainConsole.Instance.Info ("[Terrain]: File (" + filename + ") loaded successfully");
                return channel;
            }

            MainConsole.Instance.ErrorFormat ("[Terrain]: Unable to load heightmap from {0}, no file loader available for that format.", filename);
            return channel;
        }

        static Stream URIFetch (Uri uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create (uri);

            // request.Credentials = credentials;

            request.ContentLength = 0;
            request.KeepAlive = false;

            WebResponse response = request.GetResponse ();
            Stream file = response.GetResponseStream ();
            var contentLen = response.ContentLength;
            response.Dispose ();

            if (contentLen == 0) {
                MainConsole.Instance.ErrorFormat ("{0} returned an empty file", uri);
                return new BufferedStream (file, 0);
            }

            // return new BufferedStream(file, (int) response.ContentLength);
            return new BufferedStream (file, 1000000);
        }

        /// <summary>
        ///     Installs into terrain module the standard suite of brushes
        /// </summary>
        void InstallDefaultEffects ()
        {
            // Draggable Paint Brush Effects
            m_painteffects [StandardTerrainEffects.Raise] = new RaiseSphere ();
            m_painteffects [StandardTerrainEffects.Lower] = new LowerSphere ();
            m_painteffects [StandardTerrainEffects.Smooth] = new SmoothSphere ();
            m_painteffects [StandardTerrainEffects.Noise] = new NoiseSphere ();
            m_painteffects [StandardTerrainEffects.Flatten] = new FlattenSphere ();
            m_painteffects [StandardTerrainEffects.Revert] = new RevertSphere (this);
            m_painteffects [StandardTerrainEffects.Erode] = new ErodeSphere ();
            m_painteffects [StandardTerrainEffects.Weather] = new WeatherSphere ();

            // Area of effect selection effects
            m_floodeffects [StandardTerrainEffects.Raise] = new RaiseArea ();
            m_floodeffects [StandardTerrainEffects.Lower] = new LowerArea ();
            m_floodeffects [StandardTerrainEffects.Smooth] = new SmoothArea ();
            m_floodeffects [StandardTerrainEffects.Noise] = new NoiseArea ();
            m_floodeffects [StandardTerrainEffects.Flatten] = new FlattenArea ();
            m_floodeffects [StandardTerrainEffects.Revert] = new RevertArea (this);

            // Filesystem load/save loaders
            m_loaders [".r32"] = new RAW32 ();
            m_loaders [".f32"] = m_loaders [".r32"];
            m_loaders [".ter"] = new Terragen ();
            m_loaders [".raw"] = new LLRAW ();
            m_loaders [".jpg"] = new JPEG ();
            m_loaders [".jpeg"] = m_loaders [".jpg"];
            m_loaders [".bmp"] = new BMP ();
            m_loaders [".png"] = new PNG ();
            m_loaders [".gif"] = new GIF ();
            m_loaders [".tif"] = new TIFF ();
            m_loaders [".tiff"] = m_loaders [".tif"];
        }

        /// <summary>
        ///     Saves the current state of the region into the revert map buffer.
        /// </summary>
        public void UpdateRevertWaterMap ()
        {
            m_waterRevert = m_waterChannel.MakeCopy ();
            m_scene.SimulationDataService.Tainted ();
        }

        /// <summary>
        ///     Saves the current state of the region into the revert map buffer.
        /// </summary>
        public void UpdateRevertMap ()
        {
            m_revert = null;
            m_revert = m_channel.MakeCopy ();
            m_scene.SimulationDataService.Tainted ();
        }

        /// <summary>
        ///     Loads a 256x256 tile from a larger terrain file and installs it into the region.
        /// </summary>
        /// <param name="filename">The terrain file to load</param>
        /// <param name="fileTileWidth">The width of the file</param>
        /// <param name="fileTileHeight">The height of the file</param>
        /// <param name="tileLocX">Where to begin our slice</param>
        /// <param name="tileLocY">Where to begin our slice</param>
        public void LoadTileFromFile (string filename, int fileTileWidth, int fileTileHeight, int tileLocX, int tileLocY)
        {
            int offsetX = (m_scene.RegionInfo.RegionLocX / Constants.RegionSize) - tileLocX;
            int offsetY = (m_scene.RegionInfo.RegionLocY / Constants.RegionSize) - tileLocY;

            if (offsetX >= 0 && 
                offsetX < fileTileWidth &&
                offsetY >= 0 && 
                offsetY < fileTileHeight) {

                var loader = GetTerrainLoader (filename);
                if (loader != null) {
                    lock (m_scene) {
                        MainConsole.Instance.Info ("[Terrain]: Loading " + filename + " to " + m_scene.RegionInfo.RegionName);
                        ITerrainChannel channel = loader.LoadFile (
                            filename, m_scene,
                            offsetX, offsetY,
                            fileTileWidth, fileTileHeight,
                            m_scene.RegionInfo.RegionSizeX,
                            m_scene.RegionInfo.RegionSizeY);

                        channel.Scene = m_scene;
                        m_channel = channel;
                        m_scene.RegisterModuleInterface (m_channel);
                        UpdateRevertMap ();
                    }
                    CheckForTerrainUpdates ();
                    MainConsole.Instance.Info ("[Terrain]: File (" + filename + ") loaded successfully");
                    return;
                }
                MainConsole.Instance.Error ("[Terrain]: Unable to locate a file loader for " + filename);
                return;
            }
            MainConsole.Instance.Error ("[Terrain]: Tile location is outside of image");

        }

        void client_onGodlikeMessage (IClientAPI client, UUID requester, string Method, List<string> Parameters)
        {
            if (!m_scene.Permissions.IsGod (client.AgentId))
                return;

            if (client.Scene.RegionInfo.RegionID != m_scene.RegionInfo.RegionID)
                return;
            
            string parameter1 = Parameters [0];
            if (Method == "terrain") {
                if (parameter1 == "bake") {
                    UpdateRevertMap ();
                }
                if (parameter1 == "revert") {
                    InterfaceRevertTerrain (client.Scene, null);
                }
                if (parameter1 == "swap") {
                    //This is so you can change terrain with other regions... not implemented yet
                }
            }
        }

        /// <summary>
        ///     Checks to see if the terrain has been modified since last check
        ///     but won't attempt to limit those changes to the limits specified in the estate settings
        ///     currently invoked by the command line operations in the region server only
        /// </summary>
        void CheckForTerrainUpdates ()
        {
            CheckForTerrainUpdates (false, false, false);
        }

        /// <summary>
        ///     Checks to see if the terrain has been modified since last check.
        ///     If it has been modified, every all the terrain patches are sent to the client.
        ///     If the call is asked to respect the estate settings for terrain_raise_limit and
        ///     terrain_lower_limit, it will clamp terrain updates between these values
        ///     currently invoked by client_OnModifyTerrain only and not the Commander interfaces
        ///     <param name="respectEstateSettings">should height map deltas be limited to the estate settings limits</param>
        ///     <param name="forceSendOfTerrainInfo">force send terrain</param>
        ///     <param name="isWater">Check water or terrain</param>
        /// </summary>
        void CheckForTerrainUpdates (bool respectEstateSettings, bool forceSendOfTerrainInfo, bool isWater)
        {
            ITerrainChannel channel = isWater ? m_waterChannel : m_channel;
            bool shouldTaint = false;

            // if we should respect the estate settings then
            // fixup and height deltas that don't respect them
            if (respectEstateSettings)
                LimitChannelChanges (channel, isWater ? m_waterRevert : m_revert);
            else if (!forceSendOfTerrainInfo)
                LimitMaxTerrain (channel);

            List<int> xs = new List<int> ();
            List<int> ys = new List<int> ();
            for (int x = 0; x < channel.Width; x += Constants.TerrainPatchSize) {
                for (int y = 0; y < channel.Height; y += Constants.TerrainPatchSize) {
                    if (channel.Tainted (x, y) || forceSendOfTerrainInfo) {
                        xs.Add (x / Constants.TerrainPatchSize);
                        ys.Add (y / Constants.TerrainPatchSize);
                        shouldTaint = true;
                    }
                }
            }
            if (shouldTaint || forceSendOfTerrainInfo) {
                QueueTerrainUpdate ();
                m_scene.SimulationDataService.Tainted ();
            }

            foreach (IScenePresence presence in m_scene.GetScenePresences ()) {
                if (!m_sendTerrainUpdatesByViewDistance) {
                    presence.ControllingClient.SendLayerData (xs.ToArray (),
                                                              ys.ToArray (),
                                                              channel.GetSerialised (),
                                                              isWater
                                                                 ? TerrainPatch.LayerType.Land
                                                                 : TerrainPatch.LayerType.Water);
                } else {
                    for (int i = 0; i < xs.Count; i++) {
                        m_terrainPatchesSent [presence.UUID] [xs [i], ys [i]] = false;
                    }
                    SendTerrainUpdatesForClient (presence);
                }
            }
        }

        bool LimitMaxTerrain (ITerrainChannel channel)
        {
            bool changesLimited = false;

            // loop through the height map for this patch and compare it against
            // the revert map
            for (int x = 0; x < m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize; x++) {
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY / +Constants.TerrainPatchSize; y++) {
                    float requestedHeight = channel [x, y];

                    if (requestedHeight > MAX_HEIGHT) {
                        channel [x, y] = MAX_HEIGHT;
                        changesLimited = true;
                    } else if (requestedHeight < MIN_HEIGHT) {
                        channel [x, y] = MIN_HEIGHT; //as lower is a -ve delta
                        changesLimited = true;
                    }
                }
            }

            return changesLimited;
        }

        /// <summary>
        ///     Checks to see height deltas in the tainted terrain patch at xStart ,yStart
        ///     are all within the current estate limits
        ///     <returns>true if changes were limited, false otherwise</returns>
        /// </summary>
        bool LimitChannelChanges (ITerrainChannel channel, ITerrainChannel revert)
        {
            bool changesLimited = false;
            float minDelta = (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit;
            float maxDelta = (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit;

            // loop through the height map for this patch and compare it against
            // the revert map
            for (int x = 0; x < m_scene.RegionInfo.RegionSizeX / Constants.TerrainPatchSize; x++) {
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY / Constants.TerrainPatchSize; y++) {
                    float requestedHeight = channel [x, y];
                    float bakedHeight = revert [x, y];
                    float requestedDelta = requestedHeight - bakedHeight;

                    if (requestedDelta > maxDelta) {
                        channel [x, y] = bakedHeight + maxDelta;
                        changesLimited = true;
                    } else if (requestedDelta < minDelta) {
                        channel [x, y] = bakedHeight + minDelta; //as lower is a -ve delta
                        changesLimited = true;
                    }
                }
            }

            return changesLimited;
        }

        void client_OnLandUndo (IClientAPI client)
        {
            lock (m_undo) {
                if (m_undo.Count > 0) {
                    LandUndoState goback = m_undo.Pop ();
                    if (goback != null)
                        goback.PlaybackState ();
                }
            }
        }

        void client_OnModifyTerrain (UUID user, float height, float seconds, byte size, byte action,
                                    float north, float west, float south, float east, UUID agentId,
                                    float BrushSize)
        {
            bool god = m_scene.Permissions.IsGod (user);
            const byte WATER_CONST = 128;
            if (north == south && east == west) {
                if (m_painteffects.ContainsKey ((StandardTerrainEffects)action)) {
                    StoreUndoState ();
                    m_painteffects [(StandardTerrainEffects)action].PaintEffect (
                        m_channel, user, west, south, height, size, seconds, BrushSize);

                    //revert changes outside estate limits
                    CheckForTerrainUpdates (!god, false, false);
                } else {
                    if (m_painteffects.ContainsKey ((StandardTerrainEffects)(action - WATER_CONST))) {
                        StoreUndoState ();
                        m_painteffects [(StandardTerrainEffects)action - WATER_CONST].PaintEffect (
                            m_waterChannel, user, west, south, height, size, seconds, BrushSize);

                        //revert changes outside estate limits
                        CheckForTerrainUpdates (!god, false, true);
                    } else
                        MainConsole.Instance.Warn ("Unknown terrain brush type " + action);
                }
            } else {
                if (m_floodeffects.ContainsKey ((StandardTerrainEffects)action)) {
                    StoreUndoState ();
                    m_floodeffects [(StandardTerrainEffects)action].FloodEffect (
                        m_channel, user, north, west, south, east, size);

                    //revert changes outside estate limits
                    CheckForTerrainUpdates (!god, false, false);
                } else {
                    if (m_floodeffects.ContainsKey ((StandardTerrainEffects)(action - WATER_CONST))) {
                        StoreUndoState ();
                        m_floodeffects [(StandardTerrainEffects)action - WATER_CONST].FloodEffect (
                            m_waterChannel, user, north, west, south, east, size);

                        //revert changes outside estate limits
                        CheckForTerrainUpdates (!god, false, true);
                    } else
                        MainConsole.Instance.Warn ("Unknown terrain flood type " + action);
                }
            }
        }

        void client_OnBakeTerrain (IClientAPI remoteClient)
        {
            // Not a good permissions check (see client_OnModifyTerrain above), need to check the entire area.
            // for now check a point in the centre of the region

            if (m_scene.Permissions.CanIssueEstateCommand (remoteClient.AgentId, true)) {
                UpdateRevertMap ();
            }
        }

        void StoreUndoState ()
        {
            lock (m_undo) {
                if (m_undo.Count > 0) {
                    LandUndoState last = m_undo.Peek ();
                    if (last != null) {
                        if (last.Compare (m_channel))
                            return;
                    }
                }

                LandUndoState nUndo = new LandUndoState (this, m_channel);
                m_undo.Push (nUndo);
            }
        }

        #region Console Commands

        List<TerrainModule> FindModuleForScene (IScene scene)
        {
            List<TerrainModule> modules = new List<TerrainModule> ();
            if (scene == null) {
                string line =
                    MainConsole.Instance.Prompt ("Are you sure that you want to do this command on all regions?", "yes");
                if (!line.Equals ("yes", StringComparison.CurrentCultureIgnoreCase))
                    return modules;
                
                //Return them all
                return m_terrainModules;
            }

            modules.AddRange (m_terrainModules.Where (module => module.m_scene == scene));

            return modules;
        }

        void InterfaceLoadFile (IScene scene, string [] cmd)
        {

            if (scene == null) {
                MainConsole.Instance.Warn ("[Terrain]: Please change to your region before loading terrain");
                return;
            }

            bool prompt = false;
            string loadFile;
            if (cmd.Length > 2) {
                loadFile = cmd [2];
                if (!File.Exists (loadFile)) {
                    MainConsole.Instance.Info ("Terrain file '" + loadFile + "' not found.");
                    return;
                }

            } else {
                var defpath = scene.RequestModuleInterface<ISimulationBase> ().DefaultDataPath;

                loadFile = PathHelpers.GetReadFilename ("Terrain file to load into " + scene.RegionInfo.RegionName,
                    defpath,
                    new List<string> { "png", "jpg", "bmp", "gif", "raw", "tiff" },
                    true);
                if (loadFile == "")
                    return;
                prompt = true;
            }

            int offsetX = 0;
            int offsetY = 0;

            // check for offsets
            int i = 0;
            foreach (string param in cmd) {
                if (param.ToLower ().StartsWith ("offsetx", StringComparison.Ordinal)) {
                    string retVal = param.Remove (0, 8);
                    int.TryParse (retVal, out offsetX);
                } else if (param.ToLower ().StartsWith ("offsety", StringComparison.Ordinal)) {
                    string retVal = param.Remove (0, 8);
                    int.TryParse (retVal, out offsetY);
                }
                i++;
            }

            // check if interactive
            if (prompt) {
                var enterOffsets = "no";
                enterOffsets = MainConsole.Instance.Prompt ("Do you wish to offset this terrain in the region? (y/n)", enterOffsets);
                if (enterOffsets.ToLower ().StartsWith ("y", StringComparison.Ordinal)) {
                    MainConsole.Instance.Info ("Note: The region should fit into the terrain image (including any offsets)");
                    int.TryParse (MainConsole.Instance.Prompt ("X offset?", offsetX.ToString ()), out offsetX);
                    int.TryParse (MainConsole.Instance.Prompt ("Y offset?", offsetY.ToString ()), out offsetY);
                }
            }


            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                tmodule.LoadFromFile (loadFile, offsetX, offsetY);
            }
        }

        void InterfaceLoadTileFile (IScene scene, string [] cmd)
        {
            if (scene == null) {
                MainConsole.Instance.Warn ("[Terrain]: Please change to your region before loading terrain");
                return;
            }

            bool prompt = false;
            string loadFile;
            if (cmd.Length > 2) {
                loadFile = cmd [2];
                if (!File.Exists (loadFile)) {
                    MainConsole.Instance.Info ("Terrain file '" + loadFile + "' not found.");
                    return;
                }

            } else {
                var defpath = scene.RequestModuleInterface<ISimulationBase> ().DefaultDataPath;

                loadFile = PathHelpers.GetReadFilename ("Terrain file to load into " + scene.RegionInfo.RegionName,
                    defpath,
                    new List<string> { "png", "jpg", "bmp", "gif", "raw", "tiff" },
                    true);
                if (loadFile == "")
                    return;
                prompt = true;
            }

            int fileTileWidth = 0;
            int fileTileHeight = 0;
            int xOffset = 0;
            int yOffset = 0;
            if (prompt) {
                MainConsole.Instance.Info ("Please enter the height and width (in 256x256 tiles) of the terrain file");
                int.TryParse (MainConsole.Instance.Prompt ("Terrain image width (tiles)?", fileTileWidth.ToString ()), out fileTileWidth);
                if (fileTileWidth == 0)
                    return;
                
                fileTileHeight = fileTileWidth;
                int.TryParse (MainConsole.Instance.Prompt ("Terrain image hetight (tiles)?", fileTileHeight.ToString ()), out fileTileHeight);
                if (fileTileHeight == 0)
                    return;

                string regSize = Constants.RegionSize.ToString ();
                string tileSize = regSize + "x" + regSize;
                MainConsole.Instance.Info ("   The tile location is the X/Y position of the region tile (" + tileSize + ") within the image");
                int.TryParse (MainConsole.Instance.Prompt ("Region X tile location?", xOffset.ToString ()), out xOffset);
                int.TryParse (MainConsole.Instance.Prompt ("Region Y tile location?", yOffset.ToString ()), out yOffset);

            } else {
                fileTileWidth = int.Parse (cmd [3]);
                fileTileHeight = int.Parse (cmd [4]);
                xOffset = int.Parse (cmd [5]);
                yOffset = int.Parse (cmd [6]);

                if (fileTileHeight == 0 || fileTileWidth == 0) {
                    MainConsole.Instance.Error ("[Terrain]: The file tile height and/or width must be specified");
                    return;
                }
            }

            if (((fileTileWidth * Constants.RegionSize) < scene.RegionInfo.RegionSizeX) ||
                ((fileTileHeight * Constants.RegionSize) < scene.RegionInfo.RegionSizeY)) {
                MainConsole.Instance.Info ("The region is larger than the image size (Use the 'terrain load' command instead)!");
                return;
            }

            if ((((fileTileWidth + xOffset) * Constants.RegionSize) < scene.RegionInfo.RegionSizeX) ||
                 (((fileTileHeight + yOffset) * Constants.RegionSize) < scene.RegionInfo.RegionSizeY)) {
                MainConsole.Instance.Info ("The region will not fit into the image given the offsets provided!");
                return;
            }

            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                tmodule.LoadTileFromFile (loadFile, fileTileWidth, fileTileHeight, xOffset, yOffset);
            }
        }

        void InterfaceSaveFile (IScene scene, string [] cmd)
        {
            if (cmd.Length < 3) {
                MainConsole.Instance.Info ("[Terrain]: You need to specify a filename.");
                return;
            }
            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                MainConsole.Instance.Info ("[Terrain]: Saving scene " + tmodule.m_scene.RegionInfo.RegionName + " to " + cmd [2]);
                tmodule.SaveToFile (cmd [2]);
            }
        }

        void InterfaceSavePhysics (IScene scene, string [] cmd)
        {
            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);

            foreach (TerrainModule tmodule in m) {
                MainConsole.Instance.Info ("[Terrain]: Saving scene " + tmodule.m_scene.RegionInfo.RegionName + " physics");
                tmodule.m_scene.PhysicsScene.SetTerrain (tmodule.m_channel, tmodule.m_channel.GetSerialised ());
            }
        }

        void InterfaceBakeTerrain (IScene scene, string [] cmd)
        {
            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                MainConsole.Instance.Info ("[Terrain]: Saving scene " + tmodule.m_scene.RegionInfo.RegionName + " physics");
                tmodule.UpdateRevertMap ();
            }
        }

        void InterfaceRevertTerrain (IScene scene, string [] cmd)
        {
            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                int x, y;
                for (x = 0; x < tmodule.m_channel.Width; x++)
                    for (y = 0; y < tmodule.m_channel.Height; y++)
                        tmodule.m_channel [x, y] = m_revert [x, y];

                tmodule.CheckForTerrainUpdates ();
            }
        }

        void InterfaceFlipTerrain (IScene scene, string [] cmd)
        {
            if (cmd.Length < 3) {
                MainConsole.Instance.Info ("[Terrain]: You need to specify a direction x or y.");
                return;
            }

            string direction = cmd [2];

            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                if (direction.ToLower ().StartsWith ("y", StringComparison.Ordinal)) {
                    for (int x = 0; x < tmodule.m_scene.RegionInfo.RegionSizeX; x++) {
                        for (int y = 0; y < tmodule.m_scene.RegionInfo.RegionSizeY / 2; y++) {
                            float height = tmodule.m_channel [x, y];
                            float flippedHeight = tmodule.m_channel [x, tmodule.m_scene.RegionInfo.RegionSizeY - 1 - y];

                            tmodule.m_channel [x, y] = flippedHeight;
                            tmodule.m_channel [x, m_scene.RegionInfo.RegionSizeY - 1 - y] = height;
                        }
                    }
                } else if (direction.ToLower ().StartsWith ("x", StringComparison.Ordinal)) {
                    for (int y = 0; y < tmodule.m_scene.RegionInfo.RegionSizeY; y++) {
                        for (int x = 0; x < tmodule.m_scene.RegionInfo.RegionSizeX / 2; x++) {
                            float height = tmodule.m_channel [x, y];
                            float flippedHeight = tmodule.m_channel [tmodule.m_scene.RegionInfo.RegionSizeX - 1 - x, y];

                            tmodule.m_channel [x, y] = flippedHeight;
                            tmodule.m_channel [tmodule.m_scene.RegionInfo.RegionSizeX - 1 - x, y] = height;
                        }
                    }

                } else {
                    MainConsole.Instance.Error ("[Terrain]: Unrecognised direction - need x or y");
                }

                tmodule.CheckForTerrainUpdates ();
            }

        }

        void InterfaceRescaleTerrain (IScene scene, string [] cmd)
        {
            if (cmd.Length < 4) {
                MainConsole.Instance.Info ("[Terrain]: You need to specify both <min> and <max> height.");
                return;
            }


            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                float desiredMin = float.Parse (cmd [2]);
                float desiredMax = float.Parse (cmd [3]);

                // determine desired scaling factor
                float desiredRange = desiredMax - desiredMin;
                //MainConsole.Instance.InfoFormat("Desired {0}, {1} = {2}", new Object[] { desiredMin, desiredMax, desiredRange });

                // a bit ambiguous here... // if (desiredRange == 0d) {
                if (desiredRange >= 0) {
                    // delta is zero so flatten at requested height
                    tmodule.InterfaceFillTerrain (scene, cmd);
                } else {
                    //work out current heightmap range
                    float currMin = float.MaxValue;
                    float currMax = float.MinValue;

                    int width = tmodule.m_channel.Width;
                    int height = tmodule.m_channel.Height;

                    for (int x = 0; x < width; x++) {
                        for (int y = 0; y < height; y++) {
                            float currHeight = tmodule.m_channel [x, y];
                            if (currHeight < currMin) {
                                currMin = currHeight;
                            } else if (currHeight > currMax) {
                                currMax = currHeight;
                            }
                        }
                    }

                    float currRange = currMax - currMin;
                    float scale = desiredRange / currRange;

                    //MainConsole.Instance.InfoFormat("Current {0}, {1} = {2}", new Object[] { currMin, currMax, currRange });
                    //MainConsole.Instance.InfoFormat("Scale = {0}", scale);

                    // scale the heightmap accordingly
                    for (int x = 0; x < width; x++) {
                        for (int y = 0; y < height; y++) {
                            float currHeight = tmodule.m_channel [x, y] - currMin;
                            tmodule.m_channel [x, y] = desiredMin + (currHeight * scale);
                        }
                    }

                    tmodule.CheckForTerrainUpdates ();
                }
            }
        }

        void InterfaceElevateTerrain (IScene scene, string [] cmd)
        {
            if (cmd.Length < 3) {
                MainConsole.Instance.Info ("[Terrain]: You need to specify how much height to add.");
                return;
            }

            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                int x, y;
                for (x = 0; x < tmodule.m_channel.Width; x++)
                    for (y = 0; y < tmodule.m_channel.Height; y++)
                        tmodule.m_channel [x, y] += float.Parse (cmd [2]);
                tmodule.CheckForTerrainUpdates ();
            }
        }

        void InterfaceMultiplyTerrain (IScene scene, string [] cmd)
        {
            if (cmd.Length < 3) {
                MainConsole.Instance.Info (
                    "[Terrain]: You need to specify how much to multiply existing terrain by.");
                return;
            }

            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                int x, y;
                for (x = 0; x < tmodule.m_channel.Width; x++)
                    for (y = 0; y < tmodule.m_channel.Height; y++)
                        tmodule.m_channel [x, y] *= float.Parse (cmd [2]);
                tmodule.CheckForTerrainUpdates ();
            }
        }

        void InterfaceLowerTerrain (IScene scene, string [] cmd)
        {
            if (cmd.Length < 3) {
                MainConsole.Instance.Info (
                    "[Terrain]: You need to specify how much height to subtract.");
                return;
            }

            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                int x, y;
                for (x = 0; x < tmodule.m_channel.Width; x++)
                    for (y = 0; y < tmodule.m_channel.Height; y++)
                        tmodule.m_channel [x, y] -= float.Parse (cmd [2]);
                tmodule.CheckForTerrainUpdates ();
            }
        }

        void InterfaceFillTerrain (IScene scene, string [] cmd)
        {
            if (cmd.Length < 3) {
                MainConsole.Instance.Info (
                    "[Terrain]: You need to specify the height of the terrain.");
                return;
            }

            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                int x, y;

                for (x = 0; x < tmodule.m_channel.Width; x++)
                    for (y = 0; y < tmodule.m_channel.Height; y++)
                        tmodule.m_channel [x, y] = float.Parse (cmd [2]);
                tmodule.CheckForTerrainUpdates ();
            }
        }

        /// <summary>
        /// User interface for user generation of terrain in the selected region.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void InterfaceGenerateTerrain (IScene scene, string [] cmd)
        {
            string terrainType;
            //assume grassland paramters
            float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
            float minHeight = waterHeight - 5f;
            float maxHeight = minHeight + 10f;
            int smoothing = 1;

            if (cmd.Length < 3) {
                MainConsole.Instance.Info ("Available terrains: Flatland, Grassland, Hills, Mountainous, Island, Swamp or Aquatic");
                terrainType = MainConsole.Instance.Prompt ("What terrain type to use?", "Flatland");
            } else
                terrainType = cmd [2];
            terrainType = terrainType.ToLower ();

            // have heights?
            bool presets = (terrainType.StartsWith ("f", StringComparison.Ordinal) ||
                            terrainType.StartsWith ("a", StringComparison.Ordinal));
            if (!presets && (cmd.Length < 5)) {
                if (terrainType.StartsWith ("i", StringComparison.Ordinal)) {
                    minHeight = waterHeight - 5f;
                    maxHeight = 30;
                    smoothing = 3;
                } else if (terrainType.StartsWith ("h", StringComparison.Ordinal)) {
                    minHeight = waterHeight;
                    maxHeight = 40;
                    smoothing = 3;
                } else if (terrainType.StartsWith ("m", StringComparison.Ordinal)) {
                    minHeight = waterHeight;
                    maxHeight = 60;
                    smoothing = 2;
                } else if (terrainType.StartsWith ("g", StringComparison.Ordinal)) {
                    minHeight = waterHeight - 0.25f;
                    maxHeight = waterHeight + 2f;
                    smoothing = 5;
                } else if (terrainType.StartsWith ("s", StringComparison.Ordinal)) {
                    minHeight = waterHeight - 0.25f;
                    maxHeight = waterHeight + 0.75f;
                    smoothing = 3;
                }

                // prompt for custom heights...
                minHeight = float.Parse (MainConsole.Instance.Prompt ("Minimum land height", minHeight.ToString ()));
                maxHeight = float.Parse (MainConsole.Instance.Prompt ("Maximum land height", maxHeight.ToString ()));
                smoothing = int.Parse (MainConsole.Instance.Prompt ("Smoothing passes", smoothing.ToString ()));

            } else if (terrainType.StartsWith ("a", StringComparison.Ordinal)) {
                minHeight = 0;                                      // Aquatic
                maxHeight = waterHeight - 5f;
                smoothing = 4;
            } else if (terrainType.StartsWith ("f", StringComparison.Ordinal)) {
                minHeight = 0.01f;                                  // Flatland
                maxHeight = 0.012f;
            } else {
                minHeight = float.Parse (cmd [3]);
                maxHeight = float.Parse (cmd [4]);
            }

            //have smoothing?
            if (cmd.Length == 6)
                smoothing = int.Parse (cmd [5]);

            List<TerrainModule> m = FindModuleForScene (scene);
            foreach (TerrainModule tmodule in m) {
                // try for the land type
                tmodule.m_channel.GenerateTerrain (terrainType, minHeight, maxHeight, smoothing, scene);
                tmodule.CheckForTerrainUpdates ();

                uint regionArea = scene.RegionInfo.RegionArea;
                string rArea = regionArea < 1000000 ? regionArea + " m2" : (regionArea / 1000000) + " km2";
                MainConsole.Instance.InfoFormat ("[Terrain]: New terrain  of {0} generated.", rArea);

            }
        }

        void InterfaceCalcArea (IScene scene, string [] cmd)
        {

            uint regionArea = CalcLandArea (scene);

            string rArea = regionArea < 1000000 ? regionArea + " m2" : (regionArea / 1000000) + " km2";
            MainConsole.Instance.InfoFormat ("[Terrain]: Land area for {0} is {1}", scene.RegionInfo.RegionName, rArea);
        }

        /// <summary>
        /// Calculates the land area.
        /// </summary>
        /// <param name="scene">Scene.</param>
        public uint CalcLandArea (IScene scene)
        {

            //We need to update the grid server as well
            IGridRegisterModule gridRegisterModule = scene.RequestModuleInterface<IGridRegisterModule> ();
            uint regionArea = 0;

            List<TerrainModule> m = FindModuleForScene (scene);
            foreach (TerrainModule tmodule in m) {
                TerrainMap.ReCalcLandArea ();
            }

            if (gridRegisterModule != null)
                gridRegisterModule.UpdateGridRegion (scene);

            return regionArea;
        }

        void InterfaceShowDebugStats (IScene scene, string [] cmd)
        {
            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                float max = float.MinValue;
                float min = float.MaxValue;
                float sum = 0;

                int x;
                for (x = 0; x < tmodule.m_channel.Width; x++) {
                    int y;
                    for (y = 0; y < tmodule.m_channel.Height; y++) {
                        sum += tmodule.m_channel [x, y];
                        if (max < tmodule.m_channel [x, y])
                            max = tmodule.m_channel [x, y];
                        if (min > tmodule.m_channel [x, y])
                            min = tmodule.m_channel [x, y];
                    }
                }

                double avg = sum / (tmodule.m_channel.Height * tmodule.m_channel.Width);

                MainConsole.Instance.Info ("Channel " + tmodule.m_channel.Width + "x" + tmodule.m_channel.Height);
                MainConsole.Instance.Info ("max/min/avg/sum: " + max + "/" + min + "/" + avg + "/" + sum);
            }
        }

        void InterfaceEnableExperimentalBrushes (IScene scene, string [] cmd)
        {
            List<TerrainModule> m = FindModuleForScene (MainConsole.Instance.ConsoleScene);
            foreach (TerrainModule tmodule in m) {
                if (bool.Parse (cmd [2])) {
                    tmodule.m_painteffects [StandardTerrainEffects.Revert] = new WeatherSphere ();
                    tmodule.m_painteffects [StandardTerrainEffects.Flatten] = new OlsenSphere ();
                    tmodule.m_painteffects [StandardTerrainEffects.Smooth] = new ErodeSphere ();
                } else {
                    tmodule.InstallDefaultEffects ();
                }
            }
        }

        /// <summary>
        /// Command line interface help.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void InterfaceHelp (IScene scene, string [] cmd)
        {
            if (!MainConsole.Instance.HasProcessedCurrentCommand)
                MainConsole.Instance.HasProcessedCurrentCommand = true;

            string supportedFileExtensions = 
                m_loaders.Aggregate ("", (current, loader) => current + (" " + loader.Key + " (" + loader.Value + ")"));

            MainConsole.Instance.Info (
                "terrain load <FileName> - Loads a terrain from a specified file. " +
                "\n FileName: The file you wish to load from, the file extension determines the loader to be used." +
                "\n Supported extensions include: " +
                supportedFileExtensions);

            MainConsole.Instance.Info (
                "terrain save <FileName> - Saves the current heightmap to a specified file. " +
                "\n FileName: The destination filename for your heightmap, the file extension determines the format to save in. " +
                "\n Supported extensions include: " +
                supportedFileExtensions);

            MainConsole.Instance.Info (
                "terrain load-tile <FileName> <file width> <file height> <minimum X tile> <minimum Y tile>\n" +
                "     - Loads a terrain from a section of a larger file.\n" +
                "\n FileName: The image file to use for tiling" +
                "\n file width: The width of the file image" +
                "\n file height: The height of the file image" +
                "\n minimum X tile: The X region coordinate of the first section on the file" +
                "\n minimum Y tile: The Y region coordinate of the first section on the file");

            MainConsole.Instance.Info (
                "terrain fill <value> - Fills the current heightmap with a specified value." +
                "\n value: The numeric value of the height you wish to set your region to.");

            MainConsole.Instance.Info (
                "terrain elevate <value> - Raises the current heightmap by the specified amount." +
                "\n amount: The amount of height to remove from the terrain in meters.");

            MainConsole.Instance.Info (
                "terrain lower <value> - Lowers the current heightmap by the specified amount." +
                "\n amount: The amount of height to remove from the terrain in meters.");

            MainConsole.Instance.Info (
                "terrain multiply <value> - Multiplies the heightmap by the value specified." +
                "\n value: The value to multiply the heightmap by.");

            MainConsole.Instance.Info (
                "terrain bake - Saves the current terrain into the regions revert map.");

            MainConsole.Instance.Info (
                "terrain revert - Loads the revert map terrain into the regions heightmap.");

            MainConsole.Instance.Info (
                "terrain stats - Shows some information about the regions heightmap for debugging purposes.");

            MainConsole.Instance.Info (
                "terrain newbrushes <enabled> - Enables experimental brushes which replace the standard terrain brushes." +
                "\n enabled: true / false - Enable new brushes");

            MainConsole.Instance.Info (
                "terrain flip <direction> - Flips the current terrain about the X or Y axis" +
                "\n direction: [x|y] the direction to flip the terrain in");

            MainConsole.Instance.Info (
                "terrain rescale <min> <max> - Rescales the current terrain to fit between the given min and max heights" +
                "\n Min: min terrain height after rescaling" +
                "\n Max: max terrain height after rescaling");

            MainConsole.Instance.Info (
                "terrain generate <type> <Min> <Max> [smoothing]- Genrate new terrain to fit between the given min and max heights" +
                "\n Type: Flatland, Mainland, Island, Aquatic" +
                "\n Min: min terrain height after rescaling" +
                "\n Max: max terrain height after rescaling" +
                "\n Smoothing: [Optional] number of smoothing passes");

            MainConsole.Instance.Info (
                "terrain calc area - Calulates the region land area. ");

        }

        /// <summary>
        /// Adds the console commands.
        /// </summary>
        void AddConsoleCommands ()
        {
            // Load / Save
            string supportedFileExtensions = 
                m_loaders.Aggregate ("", (current, loader) => current + (" " + loader.Key + " (" + loader.Value + ")"));

            if (MainConsole.Instance != null) {
                MainConsole.Instance.Commands.AddCommand (
                    "terrain save",
                    "terrain save <FileName>",
                    "Saves the current heightmap to a specified file. FileName: The destination filename for your heightmap,\n" +
                    "the file extension determines the format to save in. Supported extensions include: " +
                    supportedFileExtensions,
                    InterfaceSaveFile, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain physics update",
                    "terrain physics update", "Update the physics map",
                    InterfaceSavePhysics, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain load",
                    "terrain load <FileName> <OffsetX=> <OffsetY=>",
                    "Loads a terrain from a specified file. FileName: The file you wish to load from. Supported extensions include: " +
                    supportedFileExtensions,
                    InterfaceLoadFile, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain load-tile",
                    "terrain load-tile <FileName> <file width> <file height> <minimum X tile> <minimum Y tile>",
                    "Loads a terrain from a section of a larger file." +
                    "\n FileName: The image file to use for tiling" +
                    "\n file width: The width of the file image" +
                    "\n file height: The height of the file image" +
                    "\n minimum X tile: The X region coordinate of the first section on the file" +
                    "\n minimum Y tile: The Y region coordinate of the first section on the file",
                    InterfaceLoadTileFile, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain fill",
                    "terrain fill <value> ",
                    "Fills the current heightmap with a specified value." +
                    "\n value: The numeric value of the height you wish to set your region to.",
                    InterfaceFillTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain elevate",
                    "terrain elevate <amount> ",
                    "Raises the current heightmap by the specified amount." +
                    "\n amount: The amount of height to remove from the terrain in meters.",
                    InterfaceElevateTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain lower",
                    "terrain lower <amount> ",
                    "Lowers the current heightmap by the specified amount." +
                    "\n amount: The amount of height to remove from the terrain in meters.",
                    InterfaceLowerTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain multiply",
                    "terrain multiply <value> ",
                    "Multiplies the heightmap by the value specified." +
                    "\n value: The value to multiply the heightmap by.",
                    InterfaceMultiplyTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain bake",
                    "terrain bake",
                    "Saves the current terrain into the regions revert map.",
                    InterfaceBakeTerrain, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain revert",
                    "terrain revert",
                    "Loads the revert map terrain into the regions heightmap.",
                    InterfaceRevertTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain stats",
                    "terrain stats",
                    "Shows some information about the regions heightmap for debugging purposes.",
                    InterfaceShowDebugStats, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain newbrushes",
                    "terrain newbrushes <enabled> ",
                    "Enables experimental brushes which replace the standard terrain brushes." +
                    "\n enabled: true / false - Enable new brushes",
                    InterfaceEnableExperimentalBrushes, true, false);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain flip",
                    "terrain flip <direction> ",
                    "Flips the current terrain about the X or Y axis" +
                    "\n direction: [x|y] the direction to flip the terrain in",
                    InterfaceFlipTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain rescale",
                    "terrain rescale <min> <max>",
                    "Rescales the current terrain to fit between the given min and max heights" +
                    "\n Min: min terrain height after rescaling" +
                    "\n Max: max terrain height after rescaling",
                    InterfaceRescaleTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain generate",
                    "terrain generate <type> <min> <max> [smoothing]",
                    "Generate new terrain to fit between the given min and max heights" +
                    "\n Type: Flatland, Mainland, Island, Aquatic" +
                    "\n Min: min terrain height after rescaling" +
                    "\n Max: max terrain height after rescaling" +
                    "\n Smoothing: [Optional - default 2] number of smoothing passes to perform",
                    InterfaceGenerateTerrain, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain help",
                    "terrain help", "Gives help about the terrain module.",
                    InterfaceHelp, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "terrain calc area",
                    "terrain calc area",
                    "Calculates the region land area above the water line",
                    InterfaceCalcArea, true, false);

            }
        }

        #endregion
    }
}
