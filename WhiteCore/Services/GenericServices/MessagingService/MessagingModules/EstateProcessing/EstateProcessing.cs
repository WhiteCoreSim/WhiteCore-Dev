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


using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Other;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Services
{
    public class EstateProcessing : IService
    {
        #region Declares

        protected IRegistryCore m_registry;

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            registry.RequestModuleInterface<ISimulationBase> ().EventManager.RegisterEventHandler ("EstateUpdated",
                                                                                                 OnGenericEvent);
        }

        public void FinishedStartup ()
        {
            //Also look for incoming messages to display
            m_registry.RequestModuleInterface<ISyncMessageRecievedService> ().OnMessageReceived += OnMessageReceived;
        }

        #endregion

        /// <summary>
        ///     Server side
        /// </summary>
        /// <param name="FunctionName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        protected object OnGenericEvent (string FunctionName, object parameters)
        {
            if (FunctionName == "EstateUpdated") {
                EstateSettings es = (EstateSettings)parameters;
                IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();
                ISyncMessagePosterService asyncPoster = m_registry.RequestModuleInterface<ISyncMessagePosterService> ();
                IGridService gridService = m_registry.RequestModuleInterface<IGridService> ();

                if (estateConnector != null && gridService != null && asyncPoster != null) {
                    List<UUID> regions = estateConnector.GetRegions ((int)es.EstateID);
                    if (regions != null) {
                        foreach (UUID region in regions) {
                            //Send the message to update all regions that are in this estate, as a setting changed
                            GridRegion r = gridService.GetRegionByUUID (null, region);
                            if (r != null)
                                asyncPoster.Post (r.ServerURI, SyncMessageHelper.UpdateEstateInfo (es.EstateID, region));
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        ///     Region side
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected OSDMap OnMessageReceived (OSDMap message)
        {
            //We need to check and see if this is an AgentStatusChange
            if (message.ContainsKey ("Method") && message ["Method"] == "EstateUpdated") {
                OSDMap innerMessage = (OSDMap)message ["Message"];
                //We got a message, deal with it
                uint estateID = innerMessage ["EstateID"].AsUInteger ();
                UUID regionID = innerMessage ["RegionID"].AsUUID ();
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager> ();
                IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();

                if (manager != null && estateConnector != null) {
                    foreach (IScene scene in manager.Scenes) {
                        if (scene.RegionInfo.EstateSettings.EstateID == estateID) {
                            EstateSettings es = null;
                            if ((es = estateConnector.GetEstateSettings (regionID)) != null && es.EstateID != 0) {
                                scene.RegionInfo.EstateSettings = es;
                                MainConsole.Instance.Debug ("[EstateProcessor]: Updated estate information.");
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}
