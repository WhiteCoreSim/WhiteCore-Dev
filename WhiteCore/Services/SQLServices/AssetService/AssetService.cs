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

        public virtual string Name
        {
            get { return GetType().Name; }
        }

        public virtual void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("AssetHandler", "") != Name)
                return;
            Configure(config, registry);
            Init(registry, Name, serverPath: "/asset/", serverHandlerName: "AssetServerURI");
        }

        public virtual void Configure(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;

            m_database = Framework.Utilities.DataManager.RequestPlugin<IAssetDataPlugin>();

            registry.RegisterModuleInterface<IAssetService>(this);

            IConfig handlers = config.Configs["Handlers"];
            if (handlers != null)
                doDatabaseCaching = handlers.GetBoolean("AssetHandlerUseCache", false);

            if (IsLocalConnector && (MainConsole.Instance != null))
            {
                MainConsole.Instance.Commands.AddCommand(
                    "show digest",
                    "show digest <ID>",
                    "Show asset digest", 
                    HandleShowDigest, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "delete asset",
                    "delete asset <ID>",
                    "Delete asset from database", 
                    HandleDeleteAsset, false, true);

                MainConsole.Instance.Commands.AddCommand("get asset",
                    "get asset <ID>",
                    "Gets info about asset from database", 
                    HandleGetAsset, false, true);

            }

            MainConsole.Instance.Debug("[Asset service]: Local asset service enabled");
        }

        public virtual void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public virtual void FinishedStartup()
        {
        }

        #endregion

        #region IAssetService Members

        public IAssetService InnerService
        {
            get { return this; }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase GetMesh(string id)
        {
            return Get(id);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase Get(string id)
        {
            return Get (id, true);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase Get(string id, bool showWarnings)
            {
                if (id == UUID.Zero.ToString()) return null;

            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache>();
            if (doDatabaseCaching && cache != null)
            {
                bool found;
                AssetBase cachedAsset = cache.Get(id, out found);
                if (found && (cachedAsset == null || cachedAsset.Data.Length != 0))
                    return cachedAsset;
            }
            object remoteValue = DoRemoteByURL("AssetServerURI", id, showWarnings);
            if (remoteValue != null || m_doRemoteOnly)
            {
                if (doDatabaseCaching && cache != null)
                    cache.Cache(id, (AssetBase) remoteValue);
                return (AssetBase) remoteValue;
            }

            AssetBase asset = m_database.GetAsset(UUID.Parse(id),showWarnings);
            if (doDatabaseCaching && cache != null)
                cache.Cache(id, asset);
            return asset;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase GetCached(string id)
        {
            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache>();
            if (doDatabaseCaching && cache != null)
                return cache.Get(id);
            return null;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual byte[] GetData(string id)
        {
            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache>();
            if (doDatabaseCaching && cache != null)
            {
                bool found;
                byte[] cachedAsset = cache.GetData(id, out found);
                if (found)
                    return cachedAsset;
            }

            object remoteValue = DoRemoteByURL("AssetServerURI", id);
            if (remoteValue != null || m_doRemoteOnly)
            {
                byte[] data = (byte[]) remoteValue;
                if (doDatabaseCaching && cache != null && data != null)
                    cache.CacheData(id, data);
                return data;
            }

            AssetBase asset = m_database.GetAsset(UUID.Parse(id));
            if (doDatabaseCaching && cache != null)
                cache.Cache(id, asset);
            
            // An empty array byte [] is NOT null and a lot of tests depend on the null test still - greythane -
            return asset == null ? null : asset.Data;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool GetExists(string id)
        {
            object remoteValue = DoRemoteByURL("AssetServerURI", id);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            return m_database.ExistsAsset(UUID.Parse(id));
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual void Get(String id, Object sender, AssetRetrieved handler)
        {
            Util.FireAndForget((o) => { handler(id, sender, Get(id)); });
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID Store(AssetBase asset)
        {
            // this should never happen but...
            if (asset == null)
            {
                MainConsole.Instance.Error ("[Asset service]: Trying to store a null asset!");
                return UUID.Zero;
            }

            object remoteValue = DoRemoteByURL("AssetServerURI", asset);
            if (remoteValue != null || m_doRemoteOnly)
            {
                if (remoteValue == null)
                    return UUID.Zero;
                asset.ID = (UUID) remoteValue;
            }
            else
                asset.ID = m_database.Store(asset);
            
            if (doDatabaseCaching)
            {
                IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
                if (cache != null && asset.Data.Length != 0)
                {
                    cache.Expire (asset.ID.ToString ());
                    cache.Cache (asset.ID.ToString (), asset);
                }
            }

            return asset.ID;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID UpdateContent(UUID id, byte[] data)
        {
            object remoteValue = DoRemoteByURL("AssetServerURI", id, data);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? UUID.Zero : (UUID) remoteValue;

            UUID newID;
            m_database.UpdateContent(id, data, out newID);
            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache>();
            if (doDatabaseCaching && cache != null)
                cache.Expire(id.ToString());
            return newID;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool Delete(UUID id)
        {
            object remoteValue = DoRemoteByURL("AssetServerURI", id);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            return m_database.Delete(id);
        }

        #endregion

        #region Console Commands

        /// <summary>
        /// Handles the show digest command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleShowDigest(IScene scene, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Info("Syntax: show digest <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null)
            {
                MainConsole.Instance.Warn ("Asset not found");
                return;
            }
            if (asset.Data.Length == 0)
            {
                MainConsole.Instance.Warn ("Asset has no data");
                return;
            }

            int i;

            MainConsole.Instance.InfoFormat("Name: {0}", asset.Name);
            MainConsole.Instance.InfoFormat("Description: {0}", asset.Description);
            MainConsole.Instance.InfoFormat("Type: {0}", asset.TypeAsset);
            MainConsole.Instance.InfoFormat("Content-type: {0}", asset.TypeAsset);
            MainConsole.Instance.InfoFormat("Flags: {0}", asset.Flags);

            for (i = 0; i < 5; i++)
            {
                int off = i*16;
                if (asset.Data.Length <= off)
                    break;
                int len = 16;
                if (asset.Data.Length < off + len)
                    len = asset.Data.Length - off;

                byte[] line = new byte[len];
                Array.Copy(asset.Data, off, line, 0, len);

                string text = BitConverter.ToString(line);
                MainConsole.Instance.Info(String.Format("{0:x4}: {1}", off, text));
            }
        }

        /// <summary>
        /// Handles the delete asset command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleDeleteAsset(IScene scene, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Info("Syntax: delete asset <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Info("Asset not found");
                return;
            }

            Delete(UUID.Parse(args[2]));

            MainConsole.Instance.Info("Asset deleted");
        }

        /// <summary>
        /// Handles the get asset command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleGetAsset(IScene scene, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Info("Syntax: get asset <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Info("Asset not found");
                return;
            }

            string creatorName = "Unknown";
            if (asset.CreatorID == UUID.Zero)
                creatorName = "System";
            else
            {
                var accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
                if (accountService != null)
                {
                    var account = accountService.GetUserAccount (null, asset.CreatorID);
                    if (account != null)
                        creatorName = account.Name;
                }
            }

            MainConsole.Instance.InfoFormat ("{0} - {1}",
                asset.Name == "" ? "(No name)" : asset.Name,
                asset.Description == "" ? "(No description)" : asset.Description
            );

            MainConsole.Instance.CleanInfoFormat (
                "                  {0} created by {1} on {2}",
                asset.AssetTypeInfo(),
                creatorName,
                asset.CreationDate.ToShortDateString()
            );      
        }


        #endregion
    }
}