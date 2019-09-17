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
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.FileBasedServices.AssetService
{
    public class AssetService : ConnectorBase, IAssetService, IService
    {
        #region Declares

        protected bool doDatabaseCaching;
        protected string m_connectionPassword;
        protected string m_assetsDirectory = "";
        protected bool m_enabled;

        protected bool m_migrateSQL;
        protected IAssetDataPlugin m_assetService;

        #endregion

        #region IService Members

        public virtual string Name
        {
            get { return GetType ().Name; }
        }

        public virtual void Initialize (IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs ["Handlers"];
            if (handlerConfig.GetString ("AssetHandler", "") != "FileBased" + Name)
                return;
            m_enabled = true;
            Configure (config, registry);
            Init (registry, Name, serverPath: "/asset/", serverHandlerName: "AssetServerURI");

            // set defaults
            var simbase = registry.RequestModuleInterface<ISimulationBase> ();
            var defpath = simbase.DefaultDataPath;
            m_assetsDirectory = Path.Combine (defpath, Constants.DEFAULT_FILEASSETS_DIR);
            m_migrateSQL = true;

            IConfig fileConfig = config.Configs ["FileBasedAssetService"];
            if (fileConfig != null)
            {
                var assetsPath = fileConfig.GetString ("AssetFolderPath", m_assetsDirectory);
                if (assetsPath != "")
                    m_assetsDirectory = assetsPath;

                // try and migrate sql assets if they are missing?
                m_migrateSQL = fileConfig.GetBoolean ("MigrateSQLAssets", true);
            }
            SetUpFileBase (m_assetsDirectory);

        }

        public virtual void Configure (IConfigSource config, IRegistryCore registry)
        {
            if (!m_enabled)
                return;
            m_registry = registry;

            registry.RegisterModuleInterface<IAssetService> (this);

            IConfig handlers = config.Configs ["Handlers"];
            if (handlers != null)
                doDatabaseCaching = handlers.GetBoolean ("AssetHandlerUseCache", false);

            if (IsLocalConnector  && (MainConsole.Instance != null))
            {
                MainConsole.Instance.Commands.AddCommand (
                    "show digest",
                    "show digest <ID>",
                    "Show asset digest", 
                    HandleShowDigest, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "delete asset",
                    "delete asset <ID>",
                    "Delete asset from database", 
                    HandleDeleteAsset, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "get asset",
                    "get asset <ID>",
                    "Gets info about asset from database", 
                    HandleGetAsset, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "migrate sql assets",
                    "migrate sql assets <enable|disable>",
                    "Enable or disable migration of SQL assets",
                    HandleMigrateSQLAssets, false, true);
            }
            MainConsole.Instance.Info ("[Filebased asset service]: File based asset service enabled");

        }

        public virtual void Start (IConfigSource config, IRegistryCore registry)
        {
            if (m_migrateSQL)
                m_assetService = Framework.Utilities.DataManager.RequestPlugin<IAssetDataPlugin> ();
        }

        public virtual void FinishedStartup ()
        {
        }

        #endregion

        #region IAssetService Members

        public IAssetService InnerService
        {
            get { return this; }
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase Get (string id)
        {
            return Get (id, true);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual AssetBase Get (string id, bool showWarnings)
        {
            if (id == UUID.Zero.ToString ())
                return null;

            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
            if (doDatabaseCaching && cache != null)
            {
                bool found;
                AssetBase cachedAsset = cache.Get (id, out found);
                if (found) {
                    if (cachedAsset != null && cachedAsset.Data != null)
                        return cachedAsset;
                }
            }

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id, showWarnings);
                if (remoteValue != null) {
                    if (doDatabaseCaching && cache != null)
                        cache.Cache (id, (AssetBase)remoteValue);
                    return (AssetBase)remoteValue;
                }
                return null;
            }

            AssetBase asset = FileGetAsset (id, showWarnings);
            if (doDatabaseCaching && cache != null)
                cache.Cache (id, asset);
            return asset;
        }

        public virtual AssetBase GetMesh (string id)
        {
            return Get (id);
        }

        public virtual AssetBase GetCached (string id)
        {
            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
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
            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
            if (doDatabaseCaching && cache != null)
            {
                bool found;
                byte[] cachedAsset = cache.GetData (id, out found);
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

            AssetBase asset = FileGetAsset (id);
            if (doDatabaseCaching && cache != null)
                cache.Cache (id, asset);
            if (asset == null)
                return null;

            // see assetservice.GetData  byte[0] != null            return new byte[0];
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

            return FileExistsAsset (id);
        }

        public virtual void Get (string id, object sender, AssetRetrieved handler)
        {
            var asset = Get (id);
            if (asset != null) {
                Util.FireAndForget ((o) => {handler (id, sender, asset);});
                //asset.Dispose ();
            }
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual UUID Store (AssetBase asset)
        {
            if (asset == null)
                return UUID.Zero;

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", asset);
                if (remoteValue == null)
                    return UUID.Zero;
                asset.ID = (UUID)remoteValue;
            } else
                FileSetAsset (asset);

            IImprovedAssetCache cache = m_registry.RequestModuleInterface<IImprovedAssetCache> ();
            if (doDatabaseCaching && cache != null && asset != null && asset.Data != null && asset.Data.Length != 0)
            {
                cache.Expire (asset.ID.ToString ());
                cache.Cache (asset.ID.ToString (), asset);
            }

            return asset.ID;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual UUID UpdateContent (UUID id, byte[] data)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id, data);
                return remoteValue != null ? (UUID)remoteValue : UUID.Zero;
            }

            AssetBase asset = FileGetAsset (id.ToString ());
            if (asset == null)
                return UUID.Zero;
            UUID newID = asset.ID = UUID.Random ();
            asset.Data = data;
            bool success = FileSetAsset (asset);
            asset.Dispose ();

            if (!success)
                return UUID.Zero; //We weren't able to update the asset
            return newID;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public virtual bool Delete (UUID id)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("AssetServerURI", id);
                return remoteValue != null ? (bool)remoteValue : false;
            }

            FileDeleteAsset (id.ToString ());
            return true;
        }

        #endregion

        void SetUpFileBase (string path)
        {
            m_assetsDirectory = path;

            if (!Directory.Exists (m_assetsDirectory))
                Directory.CreateDirectory (m_assetsDirectory);

            if (!Directory.Exists (Path.Combine (m_assetsDirectory, "data")))
                Directory.CreateDirectory (Path.Combine (m_assetsDirectory, "data"));

            MainConsole.Instance.InfoFormat ("[Filebased asset service]: Set up Filebased Assets in {0}.",
                m_assetsDirectory);
        }

        string GetPathForID (string id)
        {
            string fileName = MakeValidFileName (id);
            string baseStr = m_assetsDirectory;
            for (int i = 0; i < 4; i++)
            {
                baseStr = Path.Combine (baseStr, fileName.Substring (i * 2, 2));
                if (!Directory.Exists (baseStr))
                    Directory.CreateDirectory (baseStr);
            }
            return Path.Combine (baseStr, fileName + ".asset");
        }

        string GetDataPathForID (string hashCode)
        {
            string fileName = MakeValidFileName (hashCode);
            string baseStr = Path.Combine (m_assetsDirectory, "data");
            for (int i = 0; i < 4; i++)
            {
                baseStr = Path.Combine (baseStr, fileName.Substring (i * 2, 2));
                if (!Directory.Exists (baseStr))
                    Directory.CreateDirectory (baseStr);
            }
            return Path.Combine (baseStr, fileName + ".data");
        }

        static string MakeValidFileName (string name)
        {
            string invalidChars =
                System.Text.RegularExpressions.Regex.Escape (new string (Path.GetInvalidFileNameChars ())) + "+=";
            string invalidReStr = string.Format (@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return System.Text.RegularExpressions.Regex.Replace (name, invalidReStr, "");
        }

        object _lock = new object ();

        public AssetBase FileGetAsset (string id)
        {
            return FileGetAsset (id, true);
        }

        public AssetBase FileGetAsset (string id, bool showWarnings)
        {
            AssetBase asset;
#if ASSET_DEBUG
            long startTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            try
            {
                if (!File.Exists (GetPathForID (id)))
                    return CheckForConversion (id);

                lock (_lock)
                {
                    FileStream openStream = File.OpenRead (GetPathForID (id));
                    asset = ProtoBuf.Serializer.Deserialize<AssetBase> (openStream);
                    openStream.Close ();
                    if (asset.Type == -1)
                        asset.Type = 0;
                    asset.Data = File.ReadAllBytes (GetDataPathForID (asset.HashCode));
                }
            } catch (Exception ex)
            {
                if (showWarnings)
                    MainConsole.Instance.WarnFormat ("[Filebased asset service]: Failed to retrieve asset {0}: {1} ", id, ex);
                return null;
            }
#if ASSET_DEBUG
            finally
            {
                long endTime = System.Diagnostics.Stopwatch.GetTimestamp();
                if (MainConsole.Instance != null && asset != null)
                    MainConsole.Instance.Warn("[FILE BASED ASSET SERVICE]: Took " + (endTime - startTime)/10000 +
                                              " to get asset " + id + " sized " + asset.Data.Length/(1024) + "kbs");

            }
#endif
            return asset;
        }

        AssetBase CheckForConversion (string id)
        {
            if (!m_migrateSQL)
                return null;

            AssetBase asset;
            asset = m_assetService.GetAsset (UUID.Parse (id), false);       // don't show wornings for missing assets

            if (asset == null)
                return null;

            //Delete first, then restore it with the new local flag attached, so that we know we've converted it
            //m_assetService.Delete(asset.ID, true);
            //asset.Flags = AssetFlags.Local;
            //m_assetService.StoreAsset(asset);

            //Now store in Redis
            FileSetAsset (asset);

            return asset;
        }

        public bool FileExistsAsset (string id)
        {
            bool success = File.Exists (GetPathForID (id));
            if (!success)
                success = m_assetService.ExistsAsset (UUID.Parse (id));
            return success;
        }

        public bool FileSetAsset (AssetBase asset)
        {
            FileStream assetStream;
            try
            {
                string hash = asset.HashCode;
                bool duplicate = File.Exists (GetDataPathForID (hash));

                byte[] data = asset.Data;
                asset.Data = new byte[0];
                lock (_lock)
                {
                    assetStream = File.OpenWrite (GetPathForID (asset.IDString));
                    asset.HashCode = hash;
                    ProtoBuf.Serializer.Serialize (assetStream, asset);
                    assetStream.SetLength (assetStream.Position);
                    assetStream.Close ();
                    asset.Data = data;

                    //Deduplication...
                    if (duplicate)
                    {
                        //Only set id --> asset, and not the hashcode --> data to de-duplicate
                        return true;
                    }

                    File.WriteAllBytes (GetDataPathForID (hash), data);
                }
                return true;
            } catch
            {
                return false;
            }
        }

        public void FileDeleteAsset (string id)
        {
            AssetBase asset = FileGetAsset (id);
            if (asset == null)
                return;
            File.Delete (GetPathForID (id));
            //DON'T DO THIS, there might be other references to this hash
            //File.Delete(GetDataPathForID(asset.HashCode));
        }

        #region Console Commands

        /// <summary>
        /// Handles the show digest command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleShowDigest (IScene scene, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Info ("Syntax: show digest <ID>");
                return;
            }

            AssetBase asset = Get (args [2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Info ("Asset not found");
                return;
            }

            int i;

            MainConsole.Instance.InfoFormat ("Name: {0}", asset.Name);
            MainConsole.Instance.InfoFormat ("Description: {0}", asset.Description);
            MainConsole.Instance.InfoFormat ("Type: {0}", asset.TypeAsset);
            MainConsole.Instance.InfoFormat ("Content-type: {0}", asset.TypeAsset);
            MainConsole.Instance.InfoFormat ("Flags: {0}", asset.Flags);

            for (i = 0; i < 5; i++)
            {
                int off = i * 16;
                if (asset.Data.Length <= off)
                    break;
                int len = 16;
                if (asset.Data.Length < off + len)
                    len = asset.Data.Length - off;

                byte[] line = new byte[len];
                Array.Copy (asset.Data, off, line, 0, len);

                string text = BitConverter.ToString (line);
                MainConsole.Instance.Info (string.Format ("{0:x4}: {1}", off, text));
            }
        }

        /// <summary>
        /// Handles the delete asset command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleDeleteAsset (IScene scene, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Info ("Syntax: delete asset <ID>");
                return;
            }

            AssetBase asset = Get (args [2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Info ("Asset not found");
                return;
            }

            Delete (UUID.Parse (args [2]));

            MainConsole.Instance.Info ("Asset deleted");
        }

        /// <summary>
        /// Handles the get asset command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="args">Arguments.</param>
        void HandleGetAsset (IScene scene, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Info ("Syntax: get asset <ID>");
                return;
            }

            AssetBase asset = FileGetAsset (args [2]);
            if (asset == null)
                asset = FileGetAsset (args [2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Info ("Asset not found");
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
                    var userAcct = accountService.GetUserAccount (null, asset.CreatorID);
                    creatorName = userAcct.Name;
                }
            }

            MainConsole.Instance.InfoFormat ("{0} - {1}",
                asset.Name == "" ? "(No name)" : asset.Name,
                asset.Description == "" ? "(No description)" : asset.Description
            );

            MainConsole.Instance.CleanInfoFormat (
                "                  {0} created by {1} on {2}",
                asset.AssetTypeInfo (),
                creatorName,
                asset.CreationDate.ToShortDateString ()
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
