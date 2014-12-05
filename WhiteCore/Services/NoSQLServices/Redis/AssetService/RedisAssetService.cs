﻿/*
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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;
using WhiteCore.RedisServices.ConnectionHelpers;
using Nini.Config;
using OpenMetaverse;
using Sider;
using System;
using System.IO;

namespace WhiteCore.RedisServices.AssetService
{
    public class AssetService : ConnectorBase, IAssetService, IService
    {
        #region Declares

        protected const string DATA_PREFIX = "DATA";
        protected bool doDatabaseCaching = false;
        protected string m_connectionDNS = "localhost", m_connectionPassword = null;
        protected Pool<RedisClient<byte[]>> m_connectionPool;
        protected int m_connectionPort = 6379;
        protected bool m_enabled = false;

        protected bool m_doConversion = false;
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
            }

            IConfig assetConfig = config.Configs["AssetService"];
            if (assetConfig != null)
                m_doConversion = assetConfig.GetBoolean("DoConversion", true);

            m_connectionPool =
                new Pool<RedisClient<byte[]>>(() => new RedisClient<byte[]>(m_connectionDNS, m_connectionPort));

            if (MainConsole.Instance != null && !DoRemoteCalls)
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

                MainConsole.Instance.Info("[REDIS ASSET SERVICE]: Redis asset service enabled");
            }
        }

        public virtual void Start(IConfigSource config, IRegistryCore registry)
        {
            if (!m_enabled)
                return;
            if (m_doConversion)
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

            AssetBase asset = RedisGetAsset(id);
            if (doDatabaseCaching && cache != null)
                cache.Cache(id, asset);
            if (asset != null) return asset.Data;
            return new byte[0];
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool GetExists(string id)
        {
            object remoteValue = DoRemoteByURL("AssetServerURI", id);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            return RedisExistsAsset(id);
        }

        public virtual void Get(String id, Object sender, AssetRetrieved handler)
        {
            Util.FireAndForget((o) => { handler(id, sender, Get(id)); });
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID Store(AssetBase asset)
        {
            if (asset == null)
                return UUID.Zero;

            object remoteValue = DoRemoteByURL("AssetServerURI", asset);
            if (remoteValue != null || m_doRemoteOnly)
            {
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

            return asset != null ? asset.ID : UUID.Zero;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual UUID UpdateContent(UUID id, byte[] data)
        {
            object remoteValue = DoRemoteByURL("AssetServerURI", id, data);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? UUID.Zero : (UUID) remoteValue;

            AssetBase asset = RedisGetAsset(id.ToString());
            if (asset == null)
                return UUID.Zero;
            UUID newID = asset.ID = UUID.Random();
            asset.Data = data;
            bool success = RedisSetAsset(asset);
            if (!success)
                return UUID.Zero; //We weren't able to update the asset
            return newID;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public virtual bool Delete(UUID id)
        {
            object remoteValue = DoRemoteByURL("AssetServerURI", id);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool) remoteValue;

            RedisDeleteAsset(id.ToString());
            return true;
        }

        #endregion

        private byte[] RedisEnsureConnection(Func<RedisClient<byte[]>, byte[]> func)
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

        private bool RedisEnsureConnection(Func<RedisClient<byte[]>, bool> func)
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

        public AssetBase RedisGetAsset(string id)
        {
            AssetBase asset = null;

#if DEBUG
            long startTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            try
            {
                RedisEnsureConnection((conn) =>
                                          {
                                              byte[] data = conn.Get(id);
                                              if (data == null)
                                                  return null;

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

                if (asset == null)
                    return CheckForConversion(id);
            }
            finally
            {
#if DEBUG
                long endTime = System.Diagnostics.Stopwatch.GetTimestamp();
                if (MainConsole.Instance != null && asset != null)
                    MainConsole.Instance.Warn("[REDIS ASSET SERVICE]: Took " + (endTime - startTime)/10000 +
                                              " to get asset " + id + " sized " + asset.Data.Length/(1024) + "kbs");
#endif
            }
            return asset;
        }

        private AssetBase CheckForConversion(string id)
        {
            if (!m_doConversion)
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
            ProtoBuf.Serializer.Serialize<AssetBase>(memStream, asset);
            asset.Data = data;

            try
            {
                //Deduplication...
                if (duplicate)
                {
                    if (MainConsole.Instance != null)
                        MainConsole.Instance.Debug("[REDIS ASSET SERVICE]: Found duplicate asset " + asset.IDString +
                                                   " for " + asset.IDString);

                    //Only set id --> asset, and not the hashcode --> data to deduplicate
                    RedisEnsureConnection((conn) => conn.Set(asset.IDString, memStream.ToArray()));
                    return true;
                }

                RedisEnsureConnection((conn) =>
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
            MainConsole.Instance.InfoFormat("Content-type: {0}", asset.TypeAsset.ToString());
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