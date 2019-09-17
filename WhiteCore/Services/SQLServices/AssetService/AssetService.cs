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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.SQLServices.AssetService
{
    public class AssetService : ConnectorBase, IAssetService, IService
    {
        #region Declares

        protected IAssetDataPlugin m_database;
        protected bool doDatabaseCaching = false;

        #endregion

        #region IService Members

        public virtual string Name {
            get { return GetType ().Name; }
        }

        public virtual void Initialize (IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs ["Handlers"];
            if (handlerConfig.GetString ("AssetHandler", "") != Name)
                return;
            Configure (config, registry);
            Init (registry, Name, serverPath: "/asset/", serverHandlerName: "AssetServerURI");
        }

        public virtual void Configure (IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;

            m_database = Framework.Utilities.DataManager.RequestPlugin<IAssetDataPlugin> ();

            registry.RegisterModuleInterface<IAssetService> (this);

            IConfig handlers = config.Configs ["Handlers"];
            if (handlers != null)
                doDatabaseCaching = handlers.GetBoolean ("AssetHandlerUseCache", false);

            if (IsLocalConnector && (MainConsole.Instance != null)) {
                MainConsole.Instance.Commands.AddCommand (
                    "show digest",
                    "show digest <ID>",
                    "Show asset digest",
                    CmdShowDigest, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "delete asset",
                    "delete asset <ID>",
                    "Delete asset from database",
                    CmdDeleteAsset, false, true);

                MainConsole.Instance.Commands.AddCommand ("get asset",
                    "get asset <ID>",
                    "Gets info about asset from database",
                    CmdGetAsset, false, true);

            }

            MainConsole.Instance.Debug ("[Asset service]: Local asset service enabled");
        }

        public virtual void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public virtual void FinishedStartup ()
        {
        }

        #endregion

        #region IAssetService Members

        public IAssetService InnerService {
            get { return this; }
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase GetMesh (string id)
        {
            return Get (id);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase Get (string id)
        {
            return Get (id, true);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase Get (string id, bool showWarnings)
        {
            if (id == UUID.Zero.ToString ()) return null;

            var cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
            if (doDatabaseCaching && cache != null) {
                bool found;
                AssetBase cachedAsset = cache.Get (id, out found);
                if (found) {
                    if (cachedAsset != null && cachedAsset.Data != null)
                        return cachedAsset;
                }
            }

            if (m_doRemoteOnly) {
                var remoteValue = DoRemoteByURL ("AssetServerURI", id, showWarnings);
                if (remoteValue != null) {
                    if (doDatabaseCaching && cache != null)
                        cache.Cache (id, (AssetBase)remoteValue);
                    return (AssetBase)remoteValue;
                }
                return null;
            }

            var asset = m_database.GetAsset (UUID.Parse (id), showWarnings);
            if (doDatabaseCaching && cache != null)
                cache.Cache (id, asset);
            return asset;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase GetCached (string id)
        {
            var cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
            if (doDatabaseCaching && cache != null)
                return cache.Get (id);
            return null;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual byte [] GetData (string id)
        {
            return GetData (id, true);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual byte [] GetData (string id, bool showWarnings)
        {
            var cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
            if (doDatabaseCaching && cache != null) {
                bool found;
                byte [] cachedAsset = cache.GetData (id, out found);
                if (found)
                    return cachedAsset;
            }

            if (m_doRemoteOnly) {
                var remoteValue = DoRemoteByURL ("AssetServerURI", id, showWarnings);
                if (remoteValue != null) {
                    byte [] data = (byte [])remoteValue;
                    if (doDatabaseCaching && cache != null && data != null)
                        cache.CacheData (id, data);
                    return data;
                }
                return null;
            }

            AssetBase asset = m_database.GetAsset (UUID.Parse (id), showWarnings);
            if (doDatabaseCaching && cache != null)
                cache.Cache (id, asset);

            // An empty array byte [] is NOT null and a lot of tests depend on the null test still - greythane -
            if (asset == null)
                return null;

            var assetData = new byte [asset.Data.Length];
            asset.Data.CopyTo (assetData, 0);
            asset.Dispose ();
            return assetData;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual bool GetExists (string id)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            return m_database.ExistsAsset (UUID.Parse (id));
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual void Get (string id, object sender, AssetRetrieved handler)
        {
            var asset = Get (id);
            if (asset != null) {
                Util.FireAndForget ((o) => { handler (id, sender, asset); });
                // asset.Dispose ();
            }
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual UUID Store (AssetBase asset)
        {
            // this should never happen but...
            if (asset != null) {

                if (m_doRemoteOnly) {
                    var remoteValue = DoRemoteByURL ("AssetServerURI", asset);
                    if (remoteValue != null)
                        asset.ID = (UUID)remoteValue;
                    else
                        return UUID.Zero;
                } else
                    asset.ID = m_database.Store (asset);

                if (doDatabaseCaching) {
                    var cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
                    if (cache != null && asset.Data.Length != 0) {
                        cache.Expire (asset.ID.ToString ());
                        cache.Cache (asset.ID.ToString (), asset);
                    }
                }

                return asset.ID;
            }

            MainConsole.Instance.Error ("[Asset service]: Trying to store a null asset!");
            return UUID.Zero;
        }


        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID UpdateContent(UUID id, byte[] data)
        {
            if (m_doRemoteOnly) {
                var remoteValue = DoRemoteByURL ("AssetServerURI", id, data);
                return remoteValue != null ? (UUID)remoteValue : UUID.Zero;
            }

            UUID newID;
            m_database.UpdateContent (id, data, out newID);
            var cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
            if (doDatabaseCaching && cache != null)
                cache.Expire(id.ToString());
            return newID;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool Delete(UUID id)
        {
            if (m_doRemoteOnly) {
                var remoteValue = DoRemoteByURL ("AssetServerURI", id);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            return m_database.Delete(id);
        }

        #endregion

        #region Console Commands

        /// <summary>
        /// Handles the show digest command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void CmdShowDigest (IScene scene, string [] args)
        {
            if (args.Length < 3) {
                MainConsole.Instance.Info ("Asset ID required - Syntax: show digest <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null) {
                MainConsole.Instance.Warn ("Asset not found");
                return;
            }
            if (asset.Data.Length == 0) {
                MainConsole.Instance.Warn ("Asset has no data");
                asset.Dispose ();
                return;
            }

            int i;

            MainConsole.Instance.InfoFormat("Name: {0}", asset.Name);
            MainConsole.Instance.InfoFormat("Description: {0}", asset.Description);
            MainConsole.Instance.InfoFormat("Type: {0}", asset.TypeAsset);
            MainConsole.Instance.InfoFormat("Content-type: {0}", asset.TypeAsset);
            MainConsole.Instance.InfoFormat("Flags: {0}", asset.Flags);

            for (i = 0; i < 5; i++) {
                int off = i * 16;
                if (asset.Data.Length <= off)
                    break;
                int len = 16;
                if (asset.Data.Length < off + len)
                    len = asset.Data.Length - off;

                byte[] line = new byte[len];
                Array.Copy(asset.Data, off, line, 0, len);

                var text = BitConverter.ToString (line);
                MainConsole.Instance.Info (string.Format ("{0:x4}: {1}", off, text));
            }
            asset.Dispose ();
        }

        /// <summary>
        /// Handles the delete asset command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void CmdDeleteAsset (IScene scene, string [] args)
        {
            if (args.Length < 3) {
                MainConsole.Instance.Info ("Asset ID required - Syntax: delete asset <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null) {
                MainConsole.Instance.Info ("Asset not found");
                return;
            }

            asset.Dispose ();
            Delete(UUID.Parse(args[2]));

            MainConsole.Instance.Info("Asset deleted");
        }

        /// <summary>
        /// Handles the get asset command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void CmdGetAsset (IScene scene, string [] args)
        {
            if (args.Length < 3) {
                MainConsole.Instance.Info ("Asset ID required - Syntax: get asset <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null) {
                MainConsole.Instance.Info ("Asset not found");
                return;
            }

            string creatorName = "Unknown";
            if (asset.CreatorID == UUID.Zero)
                creatorName = "System";
            else {
                var accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
                if (accountService != null) {
                    try {
                        UserAccount userAcct = accountService.GetUserAccount (null, asset.CreatorID);
                        if (userAcct.Valid)
                            creatorName = userAcct.Name;
                    } catch (Exception e) {
                        MainConsole.Instance.Info ("Exception during retrieval of asset creator account\n" + e);
                    }
                }
            }

            MainConsole.Instance.InfoFormat ("{0} - {1}",
                asset.Name == "" ? "(No name)" : asset.Name,
                asset.Description == "" ? "(No description)" : asset.Description
            );

            MainConsole.Instance.CleanInfoFormat ("{0} created by {1} on {2}",
                asset.AssetTypeInfo(),
                creatorName,
                asset.CreationDate.ToShortDateString()
            );

            asset.Dispose ();
        }


        #endregion
    }
}
