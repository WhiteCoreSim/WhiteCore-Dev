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
using System.IO;
using Nini.Config;
using OpenMetaverse;
using Sider;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;
using WhiteCore.RedisServices.ConnectionHelpers;

namespace WhiteCore.RedisServices.AssetService
{
    public class AssetService : ConnectorBase, IAssetService, IService
    {
        #region Declares

        protected const string DATA_PREFIX = "DATA";
        protected bool doDatabaseCaching;
        protected string m_connectionDNS = "localhost";
        protected string m_connectionPassword;
        protected Pool<RedisClient<byte[]>> m_connectionPool;
        protected int m_connectionPort = 6379;
        protected bool m_enabled;

        protected bool m_migrateSQL;
        protected IAssetDataPlugin m_assetService;

        #endregion

        #region IService Members

        public virtual string Name
        {
            get { return GetType().Name; }
        }

        public virtual void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("AssetHandler", "") != "Redis" + Name)
                return;
            
            m_enabled = true;
            Configure(config, registry);
            Init(registry, Name, serverPath: "/asset/", serverHandlerName: "AssetServerURI");
        }

        public virtual void Configure(IConfigSource config, IRegistryCore registry)
        {
            if (!m_enabled)
                return;
            
            m_registry = registry;

            registry.RegisterModuleInterface<IAssetService>(this);

            IConfig handlers = config.Configs["Handlers"];
            if (handlers != null)
                doDatabaseCaching = handlers.GetBoolean("AssetHandlerUseCache", false);

            IConfig redisConnection = config.Configs["RedisConnection"];
            if (redisConnection != null)
            {
                string connString = redisConnection.Get("ConnectionString", "localhost:6379");
                m_connectionDNS = connString.Split(':')[0];
                m_connectionPort = int.Parse(connString.Split(':')[1]);
                m_connectionPassword = redisConnection.Get("ConnectionPassword", null);

                // try and migrate sql assets if they are missing?
                m_migrateSQL = redisConnection.GetBoolean("MigrateSQLAssets", true);

            }

            m_connectionPool =
                new Pool<RedisClient<byte[]>>(() => new RedisClient<byte[]>(m_connectionDNS, m_connectionPort));

            if (IsLocalConnector && (MainConsole.Instance != null))
            {
                MainConsole.Instance.Commands.AddCommand(
                    "show digest",
                    "show digest <ID>",
                    "Show asset digest", 
                    HandleShowDigest, false, true);

                MainConsole.Instance.Commands.AddCommand("delete asset",
                    "delete asset <ID>",
                    "Delete asset from database", 
                    HandleDeleteAsset, false, true);

                MainConsole.Instance.Commands.AddCommand("get asset",
                    "get asset <ID>",
                    "Gets info about asset from database", 
                    HandleGetAsset, false, true);
                
                MainConsole.Instance.Commands.AddCommand (
                   "migrate sql assets",
                   "migrate sql assets <enable|disable>",
                   "Enable or disable migration of SQL assets",
                   HandleMigrateSQLAssets, false, true);
            }
            MainConsole.Instance.Info("[Redis asset service]: Redis asset service enabled");
        }

        public virtual void Start(IConfigSource config, IRegistryCore registry)
        {
            if (!m_enabled)
                return;
            if (m_migrateSQL)
                m_assetService = Framework.Utilities.DataManager.RequestPlugin<IAssetDataPlugin>();
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
                if (found) {
                    if (cachedAsset != null && cachedAsset.Data != null)
                        return cachedAsset;
                }
            }

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL("AssetServerURI", id, showWarnings);
                if (remoteValue != null) {
                    if (doDatabaseCaching && cache != null)
                        cache.Cache (id, (AssetBase)remoteValue);
                    return (AssetBase)remoteValue;
                }
                return null;
            }

            AssetBase asset = RedisGetAsset(id);
            if (doDatabaseCaching && cache != null)
                cache.Cache(id, asset);
            return asset;
        }

        public virtual AssetBase GetMesh(string id)
        {
            return Get(id);
        }

        public virtual AssetBase GetCached(string id)
        {
            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache>();
            if (doDatabaseCaching && cache != null)
                return cache.Get(id);
            return null;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual byte [] GetData (string id)
        {
            return GetData (id, true);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual byte[] GetData(string id, bool showWarnings)
        {
            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache>();
            if (doDatabaseCaching && cache != null)
            {
                bool found;
                byte[] cachedAsset = cache.GetData(id, out found);
                if (found)
                    return cachedAsset;
            }

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id, showWarnings);
                if (remoteValue != null) {
                    byte [] data = (byte [])remoteValue;
                    if (doDatabaseCaching && cache != null && data != null)
                        cache.CacheData (id, data);
                    return data;
                }
                return null;
            }

            AssetBase asset = RedisGetAsset(id, showWarnings);
            if (doDatabaseCaching && cache != null)
                cache.Cache(id, asset);

            if (asset == null)
                return null;

            var assetData = new byte [asset.Data.Length];
            asset.Data.CopyTo (assetData, 0);
            asset.Dispose ();
            return assetData;

        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool GetExists(string id)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            return RedisExistsAsset(id);
        }

        public virtual void Get(string id, object sender, AssetRetrieved handler)
        {
            var asset = Get (id);
            if (asset != null) {
                Util.FireAndForget ((o) => { handler (id, sender, asset); });
                // asset.Dispose ();
            }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID Store(AssetBase asset)
        {
            if (asset == null)
                return UUID.Zero;

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL("AssetServerURI", asset);
                if (remoteValue == null)
                    return UUID.Zero;
                asset.ID = (UUID) remoteValue;
            }
            else
                RedisSetAsset(asset);

            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache>();
            if (doDatabaseCaching && cache != null && asset != null && asset.Data != null && asset.Data.Length != 0)
            {
                cache.Expire(asset.ID.ToString());
                cache.Cache(asset.ID.ToString(), asset);
            }

            return asset.ID;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID UpdateContent(UUID id, byte[] data)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id, data);
                return remoteValue != null ? (UUID)remoteValue : UUID.Zero;
            }

            AssetBase asset = RedisGetAsset(id.ToString());
            if (asset == null)
                return UUID.Zero;
            UUID newID = asset.ID = UUID.Random();
            asset.Data = data;

            bool success = RedisSetAsset(asset);
            asset.Dispose ();

            if (!success)
                return UUID.Zero; //We weren't able to update the asset
            return newID;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool Delete(UUID id)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            RedisDeleteAsset(id.ToString());
            return true;
        }

        #endregion

        byte[] RedisEnsureConnection(Func<RedisClient<byte[]>, byte[]> func)
        {
            RedisClient<byte[]> client = null;
            try
            {
                client = m_connectionPool.GetFreeItem();
                if (func == null)
                    return null; //Checking whether the connection is alive
                return func(client);
            }
            catch (Exception)
            {
                try
                {
                    client.Dispose();
                }
                catch
                {
                }
                m_connectionPool.DestroyItem(client);
                client = null;
            }
            finally
            {
                if (client != null)
                    m_connectionPool.FlagFreeItem(client);
            }
            return null;
        }

        bool RedisEnsureConnection(Func<RedisClient<byte[]>, bool> func)
        {
            RedisClient<byte[]> client = null;
            try
            {
                client = m_connectionPool.GetFreeItem();
                if (func == null)
                    return false; //Checking whether the connection is alive
                return func(client);
            }
            catch (Exception)
            {
                try
                {
                    client.Dispose();
                }
                catch
                {
                }
                m_connectionPool.DestroyItem(client);
                client = null;
            }
            finally
            {
                if (client != null)
                    m_connectionPool.FlagFreeItem(client);
            }
            return false;
        }

        public AssetBase RedisGetAsset (string id)
        {
            return RedisGetAsset(id, true);
        }

        public AssetBase RedisGetAsset(string id, bool showWarnings)
        {
            AssetBase asset = null;

#if DEBUG
            long startTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            try
            {
                RedisEnsureConnection((conn) => {
                    byte[] data = conn.Get(id);
                    if (data == null) {
                        return null;
                    }

                    MemoryStream memStream = new MemoryStream(data);
                    asset = ProtoBuf.Serializer.Deserialize<AssetBase>(memStream);
                    if (asset.Type == -1)
                        asset.Type = 0;
                    memStream.Close();
                    byte[] assetdata = conn.Get(DATA_PREFIX + asset.HashCode);
                    if (assetdata == null || asset.HashCode == "")
                        return null;
                    asset.Data = assetdata;
                    return null;
                });

                if (asset == null) {
                    if (showWarnings)
                        MainConsole.Instance.Warn ("Redis asset service]: Failed to retrieve asset " + id);

                    return CheckForConversion (id);
                }
            }
            finally
            {
#if DEBUG
                long endTime = System.Diagnostics.Stopwatch.GetTimestamp();
                if (MainConsole.Instance != null && asset != null)
                    MainConsole.Instance.Warn("[Redis asset service]: Took " + (endTime - startTime)/10000 +
                                              " to get asset " + id + " sized " + asset.Data.Length/(1024) + "kbs");
#endif
            }
            return asset;
        }

        AssetBase CheckForConversion(string id)
        {
            if (!m_migrateSQL)
                return null;

            AssetBase asset;
            asset = m_assetService.GetAsset(UUID.Parse(id));

            if (asset == null)
                return null;

            //Delete first, then restore it with the new local flag attached, so that we know we've converted it
            //m_assetService.Delete(asset.ID, true);
            //asset.Flags = AssetFlags.Local;
            //m_assetService.StoreAsset(asset);

            //Now store in Redis
            RedisSetAsset(asset);

            return asset;
        }

        public bool RedisExistsAsset(string id)
        {
            bool success = RedisEnsureConnection((conn) => conn.Exists(id));
            if (!success)
                success = m_assetService.ExistsAsset(UUID.Parse(id));
            return success;
        }

        public bool RedisSetAsset(AssetBase asset)
        {
            bool duplicate = RedisEnsureConnection((conn) => conn.Exists(DATA_PREFIX + asset.HashCode));

            MemoryStream memStream = new MemoryStream();
            byte[] data = asset.Data;
            string hash = asset.HashCode;
            asset.Data = new byte[0];
            ProtoBuf.Serializer.Serialize (memStream, asset);
            asset.Data = data;

            try
            {
                //Deduplication...
                if (duplicate)
                {
                    if (MainConsole.Instance != null)
                        MainConsole.Instance.Debug("[Redis asset service]: Found duplicate asset " + asset.IDString +
                                                   " for " + asset.IDString);

                    //Only set id --> asset, and not the hashcode --> data to de-duplicate
                    RedisEnsureConnection((conn) => conn.Set(asset.IDString, memStream.ToArray()));
                    return true;
                }

                RedisEnsureConnection(
                    (conn) =>
                    {
                        conn.Pipeline((c) =>
                            {
                                c.Set(asset.IDString, memStream.ToArray());
                                c.Set(DATA_PREFIX + hash, data);
                            });
                        return true;
                    });
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                memStream.Close();
            }
        }

        public void RedisDeleteAsset(string id)
        {
            AssetBase asset = RedisGetAsset(id);
            if (asset == null)
                return;
            
            RedisEnsureConnection((conn) => conn.Del(id) == 1);
            //DON'T DO THIS, there might be other references to this hash
            //RedisEnsureConnection((conn) => conn.Del(DATA_PREFIX + asset.HashCode) == 1);
        }

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

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Info("Asset not found");
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
                MainConsole.Instance.Info(string.Format("{0:x4}: {1}", off, text));
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

            AssetBase asset = RedisGetAsset(args[2]);
            if (asset == null)
                asset = RedisGetAsset(args[2]);

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
                    var creatorAcct = accountService.GetUserAccount (null, asset.CreatorID);
                    if (creatorAcct.Valid)
                        creatorName = creatorAcct.Name;
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

        /// <summary>
        /// Handles enable/disable of the migrate SQL setting.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleMigrateSQLAssets (IScene scene, string [] args)
        {

            bool migrate = m_migrateSQL;

            if (args.Length < 4) {
                MainConsole.Instance.InfoFormat ("Migration pf SQL assets is currently {0}",
                                                 m_migrateSQL ? "enabled" : "disabled");
                var prompt = MainConsole.Instance.Prompt (
                    "Do you wish to " + (m_migrateSQL ? "disable" : "enable") + " migration of SQL assets? (yes/no)", "no");
                if (prompt.ToLower ().StartsWith ("y", StringComparison.Ordinal))
                    migrate = !migrate;
                else
                    return;
            } else {
                var setting = args [3];
                if (setting.ToLower ().StartsWith ("e", StringComparison.Ordinal))
                    migrate = true;
                if (setting.ToLower ().StartsWith ("d", StringComparison.Ordinal))
                    migrate = false;
            }

            if (migrate == m_migrateSQL)
                return;

            m_migrateSQL = migrate;
            MainConsole.Instance.InfoFormat ("Migration of SQL assets has been {0}",
                                                 m_migrateSQL ? "enabled" : "disabled");
        }

        #endregion
    }
}
