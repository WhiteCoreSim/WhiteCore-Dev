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

// Uncomment to make asset Get requests for existing 
// #define WAIT_ON_INPROGRESS_REQUESTS


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services
{
    public class FlotsamAssetCache : IService, IImprovedAssetCache
    {
        #region Declares

        const string m_ModuleName = "FlotsamAssetCache";
        string m_CacheDirectory = "";

        readonly List<char> m_InvalidChars = new List<char>();

        int m_logLevel;
        ulong m_HitRateDisplay = 1; // How often to display hit statistics, given in requests

        static ulong m_Requests;

        PreAddedDictionary<string, AssetRequest> m_assetRequests =
            new PreAddedDictionary<string, AssetRequest>(() => new AssetRequest());

        class AssetRequest
        {
            int _amt = 1;

            public int Amt
            {
                get { return _amt; }
                set
                {
                    _amt = value;
                    LastAccessedTimeSpan = DateTime.Now - LastAccessed;
                    if (LastAccessedTimeSpan.Seconds > 10)
                        _amt = 0;
                    LastAccessed = DateTime.Now;
                }
            }

            int _savedamt = 1;

            public int SavedAmt
            {
                get { return _savedamt; }
                set
                {
                    _savedamt = value;
                    LastAccessedTimeSpan = DateTime.Now - LastAccessed;
                    if (LastAccessedTimeSpan.Seconds > 10)
                        _savedamt = 0;
                    LastAccessed = DateTime.Now;
                }
            }

            public TimeSpan LastAccessedTimeSpan = TimeSpan.FromDays(1000);
            public DateTime LastAccessed = DateTime.Now;
            public object Lock = new object();
        }

        static ulong m_RequestsForInprogress;
        static ulong m_DiskHits;
        static ulong m_MemoryHits;
        static double m_HitRateMemory;
        static double m_HitRateFile;

#if WAIT_ON_INPROGRESS_REQUESTS
        Dictionary<string, ManualResetEvent> m_CurrentlyWriting = new Dictionary<string, ManualResetEvent>();
        int m_WaitOnInprogressTimeout = 3000;
#else
        HashSet<string> m_CurrentlyWriting = new HashSet<string>();
#endif

        ExpiringCache<string, AssetBase> m_MemoryCache;
        bool m_MemoryCacheEnabled = true;

        // Expiration is expressed in hours.
        const double m_DefaultMemoryExpiration = 1.0;
        const double m_DefaultFileExpiration = 48;
        TimeSpan m_MemoryExpiration = TimeSpan.Zero;
        TimeSpan m_FileExpiration = TimeSpan.Zero;
        TimeSpan m_FileExpirationCleanupTimer = TimeSpan.Zero;
        readonly object m_fileCacheLock = new object();

        static int m_CacheDirectoryTiers = 1;
        static int m_CacheDirectoryTierLen = 3;
        static int m_CacheWarnAt = 30000;

        Timer m_CacheCleanTimer;

        IAssetService m_AssetService;
        ISimulationBase m_simulationBase;

        bool m_DeepScanBeforePurge;

        static int _forceMemoryCacheAmount = 2;
        IAssetMonitor _assetMonitor;

        public FlotsamAssetCache()
        {
            m_InvalidChars.AddRange(Path.GetInvalidPathChars());
            m_InvalidChars.AddRange(Path.GetInvalidFileNameChars());
        }

        public string Name
        {
            get { return m_ModuleName; }
        }

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig moduleConfig = config.Configs["Modules"];

            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetCaching", string.Empty);

                if (name == Name)
                {
                    m_MemoryCache = new ExpiringCache<string, AssetBase>();

                    IConfig assetConfig = config.Configs["AssetCache"];
                    if (assetConfig == null)
                    {
                        //MainConsole.Instance.Warn("[Flotsam asset cache]: AssetCache missing from WhiteCore.ini, using defaults.");
                        //MainConsole.Instance.InfoFormat("[Flotsam asset cache]: Cache Directory", m_CacheDirectory);
                        return;
                    }

                    m_CacheDirectory = assetConfig.GetString("CacheDirectory", m_CacheDirectory);
                    if (m_CacheDirectory == "")
                    {
                        var defpath = registry.RequestModuleInterface<ISimulationBase> ().DefaultDataPath;
                        m_CacheDirectory = Path.Combine (defpath, Constants.DEFAULT_ASSETCACHE_DIR);
                        m_CacheDirectory = Path.Combine (m_CacheDirectory, "flotsam");
                    }
                    MainConsole.Instance.InfoFormat("[Flotsam asset cache]: Cache Directory '{0}'", m_CacheDirectory);

                    m_MemoryCacheEnabled = assetConfig.GetBoolean("MemoryCacheEnabled", false);
                    m_MemoryExpiration =
                        TimeSpan.FromHours(assetConfig.GetDouble("MemoryCacheTimeout", m_DefaultMemoryExpiration));

#if WAIT_ON_INPROGRESS_REQUESTS
                    m_WaitOnInprogressTimeout = assetConfig.GetInt("WaitOnInprogressTimeout", 3000);
#endif

                    m_logLevel = assetConfig.GetInt("LogLevel", 0);
                    m_HitRateDisplay = (ulong) assetConfig.GetInt("HitRateDisplay", 1000);

                    m_FileExpiration =
                        TimeSpan.FromHours(assetConfig.GetDouble("FileCacheTimeout", m_DefaultFileExpiration));
                    m_FileExpirationCleanupTimer =
                        TimeSpan.FromHours(assetConfig.GetDouble("FileCleanupTimer", m_DefaultFileExpiration));
                    if ((m_FileExpiration > TimeSpan.Zero) && (m_FileExpirationCleanupTimer > TimeSpan.Zero))
                    {
                        m_CacheCleanTimer = new Timer(m_FileExpirationCleanupTimer.TotalMilliseconds) {AutoReset = true};
                        m_CacheCleanTimer.Elapsed += CleanupExpiredFiles;
                        lock (m_CacheCleanTimer)
                            m_CacheCleanTimer.Start();
                    }

                    m_CacheDirectoryTiers = assetConfig.GetInt("CacheDirectoryTiers", 1);
                    if (m_CacheDirectoryTiers < 1)
                    {
                        m_CacheDirectoryTiers = 1;
                    }
                    else if (m_CacheDirectoryTiers > 3)
                    {
                        m_CacheDirectoryTiers = 3;
                    }

                    m_CacheDirectoryTierLen = assetConfig.GetInt("CacheDirectoryTierLength", 3);
                    if (m_CacheDirectoryTierLen < 1)
                    {
                        m_CacheDirectoryTierLen = 1;
                    }
                    else if (m_CacheDirectoryTierLen > 4)
                    {
                        m_CacheDirectoryTierLen = 4;
                    }

                    m_CacheWarnAt = assetConfig.GetInt("CacheWarnAt", 30000);

                    m_DeepScanBeforePurge = assetConfig.GetBoolean("DeepScanBeforePurge", false);

                    if (MainConsole.Instance != null)
                    {
                        MainConsole.Instance.Commands.AddCommand(
                            "fcache status", 
                            "fcache status",
                            "Display cache status", 
                            HandleConsoleCommand, false, true);
                        
                    	MainConsole.Instance.Commands.AddCommand(
                            "fcache clear",                    	                                         
                            "fcache clear [file] [memory]",
                            "Remove all assets in the file and/or memory cache",
                            HandleConsoleCommand, false, true);
                        
                    	MainConsole.Instance.Commands.AddCommand(
                            "fcache assets",
                            "fcache assets",
                            "Attempt a deep scan and cache of all assets in all scenes",
                            HandleConsoleCommand, false, true);
                        
                    	MainConsole.Instance.Commands.AddCommand(
                            "fcache expire",
                            "fcache expire <datetime>",
                            "Purge cached assets older then the specified date/time",
                            HandleConsoleCommand, false, true);
                    }
                    registry.RegisterModuleInterface<IImprovedAssetCache>(this);
                }
            }
        }

        public void Configure(IConfigSource config, IRegistryCore registry)
        {
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_AssetService = registry.RequestModuleInterface<IAssetService>();
            m_simulationBase = registry.RequestModuleInterface<ISimulationBase>();
        }

        public void FinishedStartup()
        {
            IMonitorModule monitor = m_simulationBase.ApplicationRegistry.RequestModuleInterface<IMonitorModule>();
            if (monitor != null)
                _assetMonitor = monitor.GetMonitor<IAssetMonitor>(null);
        }

        #endregion

        #region IImprovedAssetCache

        ////////////////////////////////////////////////////////////
        // IImprovedAssetCache
        //

        void UpdateMemoryCache(string key, AssetBase asset)
        {
            UpdateMemoryCache(key, asset, false);
        }

        void UpdateMemoryCache(string key, AssetBase asset, bool forceMemCache)
        {
            if (m_MemoryCacheEnabled || forceMemCache)
            {
                if (m_MemoryExpiration > TimeSpan.Zero)
                {
                    m_MemoryCache.AddOrUpdate(key, asset, m_MemoryExpiration);
                }
                else
                {
                    m_MemoryCache.AddOrUpdate(key, asset, m_DefaultMemoryExpiration);
                }
            }
        }

        public void Cache(string assetID, AssetBase asset)
        {
            if (asset != null)
            {
                UpdateMemoryCache(asset.IDString, asset);

                string filename = GetFileName(asset.IDString);

                try
                {
                    // If the file is already cached just update access time
                    if (File.Exists(filename))
                    {
                        lock (m_CurrentlyWriting)
                        {
                            if (!m_CurrentlyWriting.Contains(filename))
                                File.SetLastAccessTime(filename, DateTime.Now);
                        }
                    }
                    else
                    {
                        // Once we start writing, make sure we flag that we're writing
                        // that object to the cache so that we don't try to write the 
                        // same file multiple times.
                        lock (m_CurrentlyWriting)
                        {
#if WAIT_ON_INPROGRESS_REQUESTS
                            if (m_CurrentlyWriting.ContainsKey(filename))
                            {
                                return;
                            }
                            else
                            {
                                m_CurrentlyWriting.Add(filename, new ManualResetEvent(false));
                            }

#else
                            if (m_CurrentlyWriting.Contains(filename))
                            {
                                return;
                            }
                            else
                            {
                                m_CurrentlyWriting.Add(filename);
                            }
#endif
                        }

                        Util.FireAndForget(
                            delegate { WriteFileCache(filename, asset); });
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
            else
            {
                m_assetRequests[assetID].SavedAmt++;

                if (m_assetRequests[assetID].SavedAmt > _forceMemoryCacheAmount)
                    UpdateMemoryCache(assetID, asset, true);
            }
        }

        public void CacheData(string assetID, byte[] data)
        {
            string filename = GetFileName("DataOnly" + assetID);

            try
            {
                // If the file is already cached just update access time
                if (File.Exists(filename))
                {
                    lock (m_CurrentlyWriting)
                    {
                        if (!m_CurrentlyWriting.Contains(filename))
                            File.SetLastAccessTime(filename, DateTime.Now);
                    }
                }
                else
                {
                    // Once we start writing, make sure we flag that we're writing
                    // that object to the cache so that we don't try to write the 
                    // same file multiple times.
                    lock (m_CurrentlyWriting)
                    {
#if WAIT_ON_INPROGRESS_REQUESTS
                        if (m_CurrentlyWriting.ContainsKey(filename))
                        {
                            return;
                        }
                        else
                        {
                            m_CurrentlyWriting.Add(filename, new ManualResetEvent(false));
                        }

#else
                        if (m_CurrentlyWriting.Contains(filename))
                        {
                            return;
                        }
                        else
                        {
                            m_CurrentlyWriting.Add(filename);
                        }
#endif
                    }

                    Util.FireAndForget(
                        delegate { WriteFileCache(filename, data); });
                }
            }
            catch (Exception e)
            {
                LogException(e);
            }
        }

        public AssetBase Get(string id)
        {
            bool found;
            return Get(id, out found);
        }

        public AssetBase Get(string id, out bool found)
        {
            m_assetRequests[id].Amt++;
            m_Requests++;

            AssetBase asset = null;
            found = false;

            bool forceMemCache = m_assetRequests[id].Amt > _forceMemoryCacheAmount;
            bool gotValue = false;
            try {
                gotValue = m_MemoryCache.TryGetValue (id, out asset);
            } catch {
            }
            if (gotValue && (m_MemoryCacheEnabled || forceMemCache))
            {
                found = true;
                m_MemoryHits++;
            }
            else
            {
                string filename = GetFileName(id);
                if (File.Exists(filename))
                {
                    try
                    {
                        asset = ExtractAsset(id, asset, filename, forceMemCache);
                        found = true;
                        if (asset == null)
                            m_assetRequests[id].Amt = _forceMemoryCacheAmount;
                    }
                    catch (Exception e)
                    {
                        LogException(e);

                        // If there was a problem de-serializing the asset, the asset may 
                        // either be corrupted OR was serialized under an old format 
                        // {different version of AssetBase} -- we should attempt to
                        // delete it and re-cache
                        File.Delete(filename);
                    }
                }


#if WAIT_ON_INPROGRESS_REQUESTS
    // Check if we're already downloading this asset.  If so, try to wait for it to 
    // download.
                if (m_WaitOnInprogressTimeout > 0)
                {
                    m_RequestsForInprogress++;

                    ManualResetEvent waitEvent;
                    if (m_CurrentlyWriting.TryGetValue(filename, out waitEvent))
                    {
                        waitEvent.WaitOne(m_WaitOnInprogressTimeout);
                        return Get(id);
                    }
                }
#else
                // Track how often we have the problem that an asset is requested while
                // it is still being downloaded by a previous request.
                bool inProgress;
                lock(m_CurrentlyWriting) 
                    inProgress = m_CurrentlyWriting.Contains (filename);
                if (inProgress)
                    m_RequestsForInprogress++;

#endif
            }

            if (((m_logLevel >= 1)) && (m_HitRateDisplay != 0) && (m_Requests%m_HitRateDisplay == 0))
            {
                m_HitRateFile = (double) m_DiskHits/m_Requests*100.0;

                MainConsole.Instance.InfoFormat("[Flotsam asset cache]: Cache Get :: {0} :: {1}", id,
                                                asset == null ? "Miss" : "Hit");
                MainConsole.Instance.InfoFormat("[Flotsam asset cache]: File Hit Rate {0}% for {1} requests",
                                                m_HitRateFile.ToString("0.00"), m_Requests);

                if (m_MemoryCacheEnabled)
                {
                    m_HitRateMemory = (double) m_MemoryHits/m_Requests*100.0;
                    MainConsole.Instance.InfoFormat("[Flotsam asset cache]: Memory Hit Rate {0}% for {1} requests",
                                                    m_HitRateMemory.ToString("0.00"), m_Requests);
                }

                MainConsole.Instance.InfoFormat(
                    "[Flotsam asset cache]: {0} unnecessary requests due to requests for assets that are currently downloading.",
                    m_RequestsForInprogress);
            }
            if (_assetMonitor != null)
                _assetMonitor.AddAsset(asset);

            return asset;
        }

        public byte[] GetData(string id, out bool found)
        {
            m_assetRequests[id].Amt++;
            m_Requests++;

            AssetBase asset = Get(id, out found);
            if (found)
                return asset == null ? null : asset.Data;

            byte[] data = null;

            string filename = GetFileName("DataOnly" + id);
            if (File.Exists(filename))
            {
                try
                {
                    data = ExtractData(filename);
                    found = true;
                }
                catch (Exception e)
                {
                    LogException(e);

                    // If there was a problem de-serializing the asset, the asset may 
                    // either be corrupted OR was serialized under an old format 
                    // {different version of AssetBase} -- we should attempt to
                    // delete it and re-cache
                    File.Delete(filename);
                }
            }

            return data;
        }

        byte[] ExtractData(string filename)
        {
            return File.ReadAllBytes(filename);
        }

        AssetBase ExtractAsset(string id, AssetBase asset, string filename, bool forceMemCache)
        {
            try
            {
                lock (m_assetRequests[id].Lock)
                {
                    Stream s = File.Open(filename, FileMode.Open);
                    asset = ProtoBuf.Serializer.Deserialize<AssetBase>(s);
                    if (asset.Type == -1)
                        //This is a bug... it's because Texture is 0, and because it is the default value, it doesn't set it, even though we set it to -1 in the initialization of AssetBase
                        asset.Type = 0;
                    s.Close();
                }
            }
            catch
            {
            }
            UpdateMemoryCache(id, asset, asset == null || forceMemCache);

            m_DiskHits++;
            return asset;
        }

        static void InsertAsset(string filename, AssetBase asset, string directory, string tempname)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Stream s = File.Open(tempname, FileMode.OpenOrCreate);
            ProtoBuf.Serializer.Serialize (s, asset);
            s.Close();
            //File.WriteAllText(tempname, OpenMetaverse.StructuredData.OSDParser.SerializeJsonString(asset.ToOSD()));

            // Now that it's written, rename it so that it can be found.
            if (File.Exists(filename))
                File.Delete(filename);
            try
            {
                File.Move(tempname, filename);
            }
            catch
            {
                File.Delete(tempname);
            }
        }

        public void Expire(string id)
        {
            if (m_logLevel >= 2)
                MainConsole.Instance.DebugFormat("[Flotsam asset cache]: Expiring Asset {0}.", id);

            try
            {
                string filename = GetFileName(id);
                lock (m_fileCacheLock)
                {
                    if (File.Exists(filename))
                        File.Delete(filename);
                }

                if (m_MemoryCacheEnabled)
                    m_MemoryCache.Remove(id);
            }
            catch (Exception e)
            {
                LogException(e);
            }
        }

        public void Clear()
        {
            if (m_logLevel >= 2)
                MainConsole.Instance.Debug("[Flotsam asset cache]: Clearing Cache.");

            lock (m_fileCacheLock)
            {
                foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
                {
                    Directory.Delete(dir, true);
                }
            }

            if (m_MemoryCacheEnabled)
                m_MemoryCache.Clear();

            if (_assetMonitor != null)
                _assetMonitor.ClearAssetCacheStatistics();
        }

        public bool Contains(string id)
        {
            return (m_MemoryCacheEnabled && m_MemoryCache.Contains(id)) || (File.Exists(GetFileName(id)));
        }

        void CleanupExpiredFiles(object source, ElapsedEventArgs e)
        {
            if (m_logLevel >= 2)
                MainConsole.Instance.DebugFormat("[Flotsam asset cache]: Checking for expired files older then {0}.",
                                                 m_FileExpiration.ToString());

            // Purge all files last accessed prior to this point
            DateTime purgeLine = DateTime.Now - m_FileExpiration;

            // An optional deep scan at this point will ensure assets present in scenes,
            // or referenced by objects in the scene, but not recently accessed 
            // are not purged.
            if (m_DeepScanBeforePurge)
            {
                CacheScenes();
            }

            lock (m_fileCacheLock)
            {
                foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
                {
                    CleanExpiredFiles(dir, purgeLine);
                }
            }
        }

        /// <summary>
        ///     Recursively through specified directory checking for asset files last
        ///     accessed prior to the specified purge line and deletes them.  Also
        ///     removes empty tier directories.
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="purgeLine"></param>
        private void CleanExpiredFiles(string dir, DateTime purgeLine)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                if (File.GetLastAccessTime(file) < purgeLine)
                    File.Delete(file);
            }

            // Recursive into lower tiers
            foreach (string subdir in Directory.GetDirectories(dir))
            {
                CleanExpiredFiles(subdir, purgeLine);
            }

            // Check if a tier directory is empty, if so, delete it
            int dirSize = Directory.GetFiles(dir).Length + Directory.GetDirectories(dir).Length;
            if (dirSize == 0)
                Directory.Delete(dir);
            else if (dirSize >= m_CacheWarnAt)
            {
                MainConsole.Instance.WarnFormat(
                    "[Flotsam asset cache]: Cache folder exceeded CacheWarnAt limit {0} {1}.  Suggest increasing tiers, tier length, or reducing cache expiration",
                    dir, dirSize);
            }
        }

        /// <summary>
        ///     Determines the filename for an AssetID stored in the file cache
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        string GetFileName(string id)
        {
            // Would it be faster to just hash the darn thing?
            id = m_InvalidChars.Aggregate(id, (current, c) => current.Replace(c, '_'));

			string path = m_CacheDirectory;
            for (int p = 1; p <= m_CacheDirectoryTiers; p++)
            {
                string pathPart = id.Substring((p - 1)*m_CacheDirectoryTierLen, m_CacheDirectoryTierLen);
                path = Path.Combine(path, pathPart);
            }

            return Path.Combine(path, id);
        }

        /// <summary>
        ///     Writes a file to the file cache, creating any necessary
        ///     tier directories along the way
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="asset"></param>
        void WriteFileCache(string filename, AssetBase asset)
        {
            Stream stream = null;

            // Make sure the target cache directory exists
            string directory = Path.GetDirectoryName(filename);

            // Write file first to a temp name, so that it doesn't look 
            // like it's already cached while it's still writing.
            if (directory != null)
            {
                string tempname = Path.Combine(directory, Path.GetRandomFileName());

                try
                {
                    lock (m_fileCacheLock)
                    {
                        InsertAsset(filename, asset, directory, tempname);
                    }

                    if (m_logLevel >= 2)
                        MainConsole.Instance.DebugFormat("[Flotsam asset cache]: Cache Stored :: {0}", asset.ID);
                }
                catch (Exception e)
                {
                    LogException(e);
                }
                finally
                {
                    if (stream != null)
                        stream.Close();

                    // Even if the write fails with an exception, we need to make sure
                    // that we release the lock on that file, otherwise it'll never get
                    // cached
                    lock (m_CurrentlyWriting)
                    {
#if WAIT_ON_INPROGRESS_REQUESTS
                    ManualResetEvent waitEvent;
                    if (m_CurrentlyWriting.TryGetValue(filename, out waitEvent))
                    {
                        m_CurrentlyWriting.Remove(filename);
                        waitEvent.Set();
                    }
#else
                        if (m_CurrentlyWriting.Contains(filename))
                            m_CurrentlyWriting.Remove(filename);
#endif
                    }
                }
            }
        }

        /// <summary>
        ///     Writes a file to the file cache, creating any necessary
        ///     tier directories along the way
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="data"></param>
        void WriteFileCache(string filename, byte[] data)
        {
            // Make sure the target cache directory exists
            string directory = Path.GetDirectoryName(filename);

            // Write file first to a temp name, so that it doesn't look 
            // like it's already cached while it's still writing.
            if (directory != null)
            {
                string tempname = Path.Combine(directory, Path.GetRandomFileName());

                try
                {
                    lock (m_fileCacheLock)
                    {
                        if (!Directory.Exists(directory))
                            Directory.CreateDirectory(directory);

                        File.WriteAllBytes(tempname, data);
                        // Now that it's written, rename it so that it can be found.
                        if (File.Exists(filename))
                            File.Delete(filename);
                        try
                        {
                            File.Move(tempname, filename);
                        }
                        catch
                        {
                            File.Delete(tempname);
                        }
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                }
                finally
                {
                    // Even if the write fails with an exception, we need to make sure
                    // that we release the lock on that file, otherwise it'll never get
                    // cached
                    lock (m_CurrentlyWriting)
                    {
#if WAIT_ON_INPROGRESS_REQUESTS
                    ManualResetEvent waitEvent;
                    if (m_CurrentlyWriting.TryGetValue(filename, out waitEvent))
                    {
                        m_CurrentlyWriting.Remove(filename);
                        waitEvent.Set();
                    }
#else
                        if (m_CurrentlyWriting.Contains(filename))
                            m_CurrentlyWriting.Remove(filename);
#endif
                    }
                }
            }
        }

        static void LogException(Exception e)
        {
            string[] text = e.ToString().Split(new[] {'\n'});
            foreach (string t in text)
            {
                if (t.Trim() != "")
                    MainConsole.Instance.ErrorFormat("[Flotsam asset cache]: {0} ", t);
            }
        }

        /// <summary>
        ///     Scan through the file cache, and return number of assets currently cached.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        int GetFileCacheCount(string dir)
        {
            return Directory.GetFiles(dir).Length +
                   Directory.GetDirectories(dir).Sum(subdir => GetFileCacheCount(subdir));
        }

        /// <summary>
        ///     This notes the last time the Region had a deep asset scan performed on it.
        /// </summary>
        /// <param name="RegionID"></param>
        void StampRegionStatusFile(UUID RegionID)
        {
            string RegionCacheStatusFile = Path.Combine(m_CacheDirectory, "RegionStatus_" + RegionID + ".fac");
            lock (m_fileCacheLock)
            {
                if (!Directory.Exists(m_CacheDirectory))
                    Directory.CreateDirectory(m_CacheDirectory);
                if (File.Exists(RegionCacheStatusFile))
                {
                    File.SetLastWriteTime(RegionCacheStatusFile, DateTime.Now);
                }
                else
                {
                    File.WriteAllText(RegionCacheStatusFile,
                                      "Please do not delete this file unless you are manually clearing your Flotsam Asset Cache.");
                }
            }
        }

        /// <summary>
        ///     Iterates through all Scenes, doing a deep scan through assets
        ///     to cache all assets present in the scene or referenced by assets
        ///     in the scene
        /// </summary>
        /// <returns></returns>
        int CacheScenes()
        {
            //Make sure this is not null
            if (m_AssetService == null)
                return 0;
            
            HashSet<UUID> uniqueUuids = new HashSet<UUID>();
            Dictionary<UUID, AssetType> assets = new Dictionary<UUID, AssetType>();
            ISceneManager manager = m_simulationBase.ApplicationRegistry.RequestModuleInterface<ISceneManager>();
            if (manager != null)
            {
                UuidGatherer gatherer = new UuidGatherer(m_AssetService);

                foreach (IScene scene in manager.Scenes)
                {
                    StampRegionStatusFile(scene.RegionInfo.RegionID);
                    scene.ForEachSceneEntity(e => gatherer.GatherAssetUuids(e, assets));
                }

                foreach (UUID assetID in assets.Keys)
                {
                    string filename = GetFileName(assetID.ToString());

                    if (File.Exists(filename))
                    {
                    	if (!uniqueUuids.Contains(assetID))
                    	{
                    		File.SetLastAccessTime(filename, DateTime.Now);
                    	}
                    }
                    else
                    {
                    	AssetBase cachedAsset = null;
                        if (!uniqueUuids.Contains (assetID))
                        {
                            // getting the asset will save it in cache if reqy=uired
                            cachedAsset = m_AssetService.Get (assetID.ToString ());
                    	
                            if (cachedAsset == null && assets [assetID] != AssetType.Unknown)
                            {
                                MainConsole.Instance.DebugFormat ("[Flotsam asset cache]: Could not find asset {0}, type {1} when pre-caching all scene assets",
                                    assetID, assets [assetID]);
                            }
                            // we don't actually need what we retrieved
                            if (cachedAsset != null)
                                cachedAsset.Dispose ();
                        }
                    }
                    uniqueUuids.Add(assetID);
                }
                assets.Clear();
            }
            return assets.Keys.Count;
        }

        /// <summary>
        ///     Deletes all cache contents
        /// </summary>
        void ClearFileCache()
        {
            lock (m_fileCacheLock)
            {
                foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (Exception e)
                    {
                        LogException(e);
                    }
                }

                foreach (string file in Directory.GetFiles(m_CacheDirectory))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        LogException(e);
                    }
                }
            }
        }

        #region Console Commands

        void HandleConsoleCommand(IScene scene, string[] cmdparams)
        {
            if (cmdparams.Length >= 2)
            {
                string cmd = cmdparams[1];
                switch (cmd)
                {
                    case "status":
                        MainConsole.Instance.InfoFormat("[Flotsam asset cache] Memory Cache : {0} assets",
                                                        m_MemoryCache.Count);

                        int fileCount = GetFileCacheCount(m_CacheDirectory);
                        MainConsole.Instance.InfoFormat("[Flotsam asset cache] File Cache : {0} assets", fileCount);

                        foreach (string s in Directory.GetFiles(m_CacheDirectory, "*.fac"))
                        {
                            MainConsole.Instance.Info(
                                "[Flotsam asset cache] Deep Scans were performed on the following regions:");

                            string RegionID = s.Remove(0, s.IndexOf ("_", StringComparison.Ordinal)).Replace(".fac", "");
                            DateTime RegionDeepScanTMStamp = File.GetLastWriteTime(s);
                            MainConsole.Instance.InfoFormat("[Flotsam asset cache] Region: {0}, {1}", RegionID,
                                                            RegionDeepScanTMStamp.ToString("MM/dd/yyyy hh:mm:ss"));
                        }

                        break;

                    case "clear":
                        if (cmdparams.Length < 3)
                        {
                            MainConsole.Instance.Warn("[Flotsam asset cache] Please specify memory and/or file cache.");
                            break;
                        }
                        foreach (string s in cmdparams)
                        {
                            if (s.ToLower() == "memory")
                            {
                                m_MemoryCache.Clear();
                                MainConsole.Instance.Info("[Flotsam asset cache] Memory cache cleared.");
                            }
                            else if (s.ToLower() == "file")
                            {
                                ClearFileCache();
                                MainConsole.Instance.Info("[Flotsam asset cache] File cache cleared.");
                            }
                        }
                        break;


                    case "assets":
                        MainConsole.Instance.Info("[Flotsam asset cache] Caching all assets, in all scenes.");

                        Util.FireAndForget(delegate
                                               {
                                                   int assetsCached = CacheScenes();
                                                   MainConsole.Instance.InfoFormat(
                                                       "[Flotsam asset cache] Completed Scene Caching, {0} assets found.",
                                                       assetsCached);
                                               });

                        break;

                    case "expire":


                        if (cmdparams.Length < 3)
                        {
                            MainConsole.Instance.InfoFormat(
                                "[Flotsam asset cache] Invalid parameters for Expire, please specify a valid date & time",
                                cmd);
                            break;
                        }

                        string s_expirationDate = "";
                        DateTime expirationDate;

                        s_expirationDate = cmdparams.Length > 3
                                               ? string.Join(" ", cmdparams, 2, cmdparams.Length - 2)
                                               : cmdparams[2];

                        if (!DateTime.TryParse(s_expirationDate, out expirationDate))
                        {
                            MainConsole.Instance.InfoFormat("[Flotsam asset cache] {0} is not a valid date & time", cmd);
                            break;
                        }

                        CleanExpiredFiles(m_CacheDirectory, expirationDate);

                        break;
                    default:
                        MainConsole.Instance.InfoFormat("[Flotsam asset cache] Unknown command {0}", cmd);
                        break;
                }
            }
            else if (cmdparams.Length == 1)
            {
                MainConsole.Instance.InfoFormat("[Flotsam asset cache] flotsamcache status - Display cache status");
                MainConsole.Instance.InfoFormat(
                    "[Flotsam asset cache] flotsamcache clearmem - Remove all assets cached in memory");
                MainConsole.Instance.InfoFormat(
                    "[Flotsam asset cache] flotsamcache clearfile - Remove all assets cached on disk");
                MainConsole.Instance.InfoFormat(
                    "[Flotsam asset cache] flotsamcache cachescenes - Attempt a deep cache of all assets in all scenes");
                MainConsole.Instance.InfoFormat(
                    "[Flotsam asset cache] flotsamcache <datetime> - Purge assets older then the specified date & time");
            }
        }

        #endregion

        #endregion
    }
}
