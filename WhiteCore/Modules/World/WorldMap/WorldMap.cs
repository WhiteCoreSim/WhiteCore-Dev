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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Utilities;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.Modules.WorldMap
{
    public class WorldMapModule : INonSharedRegionModule, IWorldMapModule
    {
        const string DEFAULT_WORLD_MAP_EXPORT_PATH = "exportmap.jpg";

        protected IScene m_scene;
        protected bool m_Enabled;

        readonly ExpiringCache<ulong, List<mapItemReply>> m_mapItemCache =
            new ExpiringCache<ulong, List<mapItemReply>> ();

        readonly ConcurrentQueue<MapItemRequester> m_itemsToRequest = 
            new ConcurrentQueue<MapItemRequester> ();
        bool itemRequesterIsRunning;
        static WhiteCoreThreadPool threadpool;
        static WhiteCoreThreadPool blockthreadpool;
        int MapViewLength = 8;

        #region INonSharedRegionModule Members

        public virtual void Initialise (IConfigSource source)
        {
            if (source.Configs ["MapModule"] != null) {
                if (source.Configs ["MapModule"].GetString ("WorldMapModule", "WorldMapModule") != Name)
                    return;
                m_Enabled = true;
                MapViewLength = source.Configs ["MapModule"].GetInt ("MapViewLength", MapViewLength);
            }
        }

        public virtual void AddRegion (IScene scene)
        {
            if (!m_Enabled)
                return;

            m_scene = scene;
            m_scene.RegisterModuleInterface<IWorldMapModule> (this);
            AddHandlers ();
        }

        public virtual void RemoveRegion (IScene scene)
        {
            if (!m_Enabled)
                return;

            m_Enabled = false;
            RemoveHandlers ();
            m_scene = null;
        }

        public virtual void RegionLoaded (IScene scene)
        {
            if (!m_Enabled)
                return;

            WhiteCoreThreadPoolStartInfo info = new WhiteCoreThreadPoolStartInfo { priority = ThreadPriority.Lowest, Threads = 1 };
            threadpool = new WhiteCoreThreadPool (info);
            blockthreadpool = new WhiteCoreThreadPool (info);
        }

        public virtual void Close ()
        {
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public virtual string Name {
            get { return "WorldMapModule"; }
        }

        #endregion

        // this has to be called with a lock on m_scene
        protected virtual void AddHandlers ()
        {
            string regionimage = "/index.php?method=regionImage" + m_scene.RegionInfo.RegionID;
            regionimage = regionimage.Replace ("-", "");
            MainConsole.Instance.Debug ("[World map]: JPEG Map location: " + MainServer.Instance.ServerURI + regionimage);

            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("GET", regionimage, OnHTTPGetMapImage));

            m_scene.EventManager.OnNewClient += OnNewClient;
            m_scene.EventManager.OnClosingClient += OnClosingClient;
        }

        // this has to be called with a lock on m_scene
        protected virtual void RemoveHandlers ()
        {
            m_scene.EventManager.OnNewClient -= OnNewClient;
            m_scene.EventManager.OnClosingClient -= OnClosingClient;

            string regionimage = "/index.php?method=regionImage" + m_scene.RegionInfo.RegionID;
            regionimage = regionimage.Replace ("-", "");
            MainServer.Instance.RemoveStreamHandler ("GET", regionimage);
        }

        #region EventHandlers

        /// <summary>
        ///     Registered for event
        /// </summary>
        /// <param name="client"></param>
        void OnNewClient (IClientAPI client)
        {
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnMapItemRequest += HandleMapItemRequest;
            client.OnMapNameRequest += OnMapNameRequest;
        }

        void OnClosingClient (IClientAPI client)
        {
            client.OnRequestMapBlocks -= RequestMapBlocks;
            client.OnMapItemRequest -= HandleMapItemRequest;
            client.OnMapNameRequest -= OnMapNameRequest;
        }

        #endregion

        public virtual void HandleMapItemRequest (IClientAPI remoteClient, uint flags,
                                                 uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            IScenePresence presence = remoteClient.Scene.GetScenePresence (remoteClient.AgentId);
            if (presence == null || presence.IsChildAgent)
                return; //No child agent requests

            uint xstart;
            uint ystart;
            Utils.LongToUInts (m_scene.RegionInfo.RegionHandle, out xstart, out ystart);

            List<mapItemReply> mapitems = new List<mapItemReply> ();
            int tc = Environment.TickCount;
            if (itemtype == (int)GridItemType.AgentLocations) {
                //If its local, just let it do it on its own.
                if (regionhandle == 0 || regionhandle == m_scene.RegionInfo.RegionHandle) {
                    //Only one person here, send a zero person response
                    mapItemReply mapitem;
                    IEntityCountModule entityCountModule = m_scene.RequestModuleInterface<IEntityCountModule> ();
                    if (entityCountModule != null && entityCountModule.RootAgents <= 1) {
                        mapitem = new mapItemReply {
                            x = xstart + 1,
                            y = ystart + 1,
                            id = UUID.Zero,
                            name = Util.Md5Hash (m_scene.RegionInfo.RegionName + tc),
                            Extra = 0,
                            Extra2 = 0
                        };
                        mapitems.Add (mapitem);
                        remoteClient.SendMapItemReply (mapitems.ToArray (), itemtype, flags);
                        return;
                    }
                    m_scene.ForEachScenePresence (delegate (IScenePresence sp) {
                        // Don't send a green dot for yourself
                        if (!sp.IsChildAgent && sp.UUID != remoteClient.AgentId) {
                            mapitem = new mapItemReply {
                                x = (uint)(xstart + sp.AbsolutePosition.X),
                                y = (uint)(ystart + sp.AbsolutePosition.Y),
                                id = UUID.Zero,
                                name = Util.Md5Hash (m_scene.RegionInfo.RegionName + tc),
                                Extra = 1,
                                Extra2 = 0
                            };
                            mapitems.Add (mapitem);
                        }
                    });
                    remoteClient.SendMapItemReply (mapitems.ToArray (), itemtype, flags);
                } else {
                    List<mapItemReply> reply;
                    if (!m_mapItemCache.TryGetValue (regionhandle, out reply)) {
                        m_itemsToRequest.Enqueue (new MapItemRequester {
                            flags = flags,
                            itemtype = itemtype,
                            regionhandle = regionhandle,
                            remoteClient = remoteClient
                        });

                        if (!itemRequesterIsRunning)
                            threadpool.QueueEvent (GetMapItems, 3);
                    } else {
                        remoteClient.SendMapItemReply (mapitems.ToArray (), itemtype, flags);
                    }
                }
            }
        }

        void GetMapItems ()
        {
            itemRequesterIsRunning = true;
            while (true) {
                MapItemRequester item;
                if (!m_itemsToRequest.TryDequeue (out item))
                    break; //Nothing in the queue

                List<mapItemReply> mapitems;
                if (!m_mapItemCache.TryGetValue (item.regionhandle, out mapitems))
                //try again, might have gotten picked up by this already
                {
                    multipleMapItemReply allmapitems = m_scene.GridService.GetMapItems (item.remoteClient.AllScopeIDs,
                                                                                       item.regionhandle,
                                                                                       (GridItemType)item.itemtype);

                    if (allmapitems == null)
                        continue;
                    
                    //Send out the update
                    if (allmapitems.items.ContainsKey (item.regionhandle)) {
                        mapitems = allmapitems.items [item.regionhandle];

                        //Update the cache
                        foreach (KeyValuePair<ulong, List<mapItemReply>> kvp in allmapitems.items) {
                            m_mapItemCache.AddOrUpdate (kvp.Key, kvp.Value, 3 * 60); //3 mins
                        }
                    }
                }

                if (mapitems != null)
                    item.remoteClient.SendMapItemReply (mapitems.ToArray (), item.itemtype, item.flags);
                Thread.Sleep (5);
            }
            itemRequesterIsRunning = false;
        }

        /// <summary>
        ///     Requests map blocks in area of minX, maxX, minY, MaxY in world coordinates
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <param name="flag"></param>
        public virtual void RequestMapBlocks (IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            if ((flag & 0x10000) != 0) // user clicked on the map a tile that isn't visible
            {
                ClickedOnTile (remoteClient, minX, minY, maxX, maxY, flag);
            } else if (flag == 0) //Terrain and objects
            {
                // normal mapblock request. Use the provided values
                GetAndSendMapBlocks (remoteClient, minX, minY, maxX, maxY, flag);
            } else if ((flag & 1) == 1) //Terrain only
            {
                // normal terrain only request. Use the provided values
                GetAndSendTerrainBlocks (remoteClient, minX, minY, maxX, maxY, flag);
            } else {
                if (flag != 2) //Land sales
                    MainConsole.Instance.Warn ("[World Map] : Got new flag, " + flag + " RequestMapBlocks()");
            }
        }

        protected virtual void ClickedOnTile (IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            m_blockitemsToRequest.Enqueue (new MapBlockRequester {
                maxX = maxX,
                maxY = maxY,
                minX = minX,
                minY = minY,
                mapBlocks = (uint)(flag & ~0x10000),
                remoteClient = remoteClient
            });
            if (!blockRequesterIsRunning)
                blockthreadpool.QueueEvent (GetMapBlocks, 3);
        }

        protected virtual void GetAndSendMapBlocks (IClientAPI remoteClient, int minX, int minY, int maxX, int maxY,
                                                   uint flag)
        {
            m_blockitemsToRequest.Enqueue (new MapBlockRequester {
                maxX = maxX,
                maxY = maxY,
                minX = minX,
                minY = minY,
                mapBlocks = 0, //Map
                remoteClient = remoteClient
            });
            if (!blockRequesterIsRunning)
                blockthreadpool.QueueEvent (GetMapBlocks, 3);
        }

        protected virtual void GetAndSendTerrainBlocks (IClientAPI remoteClient, int minX, int minY, int maxX, int maxY,
                                                       uint flag)
        {
            m_blockitemsToRequest.Enqueue (new MapBlockRequester {
                maxX = maxX,
                maxY = maxY,
                minX = minX,
                minY = minY,
                mapBlocks = 1, //Terrain
                remoteClient = remoteClient
            });
            if (!blockRequesterIsRunning)
                blockthreadpool.QueueEvent (GetMapBlocks, 3);
        }

        bool blockRequesterIsRunning;

        readonly ConcurrentQueue<MapBlockRequester> m_blockitemsToRequest =
            new ConcurrentQueue<MapBlockRequester> ();

        class MapBlockRequester
        {
            public int minX;
            public int minY;
            public int maxX;
            public int maxY;
            public uint mapBlocks;
            public IClientAPI remoteClient;
        }

        void GetMapBlocks ()
        {
            try {
                blockRequesterIsRunning = true;
                while (true) {
                    MapBlockRequester item = null;
                    if (!m_blockitemsToRequest.TryDequeue (out item))
                        break;
                    List<MapBlockData> mapBlocks = new List<MapBlockData> ();

                    if (item.minX == item.maxX && item.minY == item.maxY) {
                        List<GridRegion> regions = m_scene.GridService.GetRegionRange (
                            item.remoteClient.AllScopeIDs,
                            (item.minX) * Constants.RegionSize,
                            (item.maxX) * Constants.RegionSize,
                            (item.minY) * Constants.RegionSize,
                            (item.maxY) * Constants.RegionSize
                        );

                        foreach (GridRegion region in regions) {
                            if ((item.mapBlocks & 1) == 1)
                                mapBlocks.Add (TerrainBlockFromGridRegion (region));
                            else if ((item.mapBlocks & 2) == 2) //V2 viewer, we need to deal with it a bit
                                mapBlocks.AddRange (Map2BlockFromGridRegion (region));
                            else
                                mapBlocks.Add (MapBlockFromGridRegion (region, region.RegionLocX, region.RegionLocY));
                        }
                        if (regions.Count == 0) {
                            mapBlocks.Add (MapBlockFromGridRegion (null, item.minX, item.minY));
                        }

                        item.remoteClient.SendMapBlock (mapBlocks, item.mapBlocks);
                        Thread.Sleep (5);
                    } else {
                        List<GridRegion> regions = m_scene.GridService.GetRegionRange (
                            item.remoteClient.AllScopeIDs,
                            (item.minX - 4) * Constants.RegionSize,
                            (item.maxX + 4) * Constants.RegionSize,
                            (item.minY - 4) * Constants.RegionSize,
                            (item.maxY + 4) * Constants.RegionSize
                        );

                        foreach (GridRegion region in regions) {
                            if ((item.mapBlocks & 1) == 1)
                                mapBlocks.Add (TerrainBlockFromGridRegion (region));
                            else if ((item.mapBlocks & 2) == 2) //V2 viewer, we need to deal with it a bit
                                mapBlocks.AddRange (Map2BlockFromGridRegion (region));
                            else
                                mapBlocks.Add (MapBlockFromGridRegion (region, region.RegionLocX, region.RegionLocY));
                        }

                        item.remoteClient.SendMapBlock (mapBlocks, item.mapBlocks);
                        Thread.Sleep (5);
                    }
                }
            } catch (Exception) {
            }
            blockRequesterIsRunning = false;
        }

        protected MapBlockData MapBlockFromGridRegion (GridRegion r, int x, int y)
        {
            MapBlockData block = new MapBlockData ();
            if (r == null) {
                block.Access = (byte)SimAccess.NonExistent;
                block.X = (ushort)x;
                block.Y = (ushort)y;
                block.MapImageID = UUID.Zero;
                return block;
            }
            if ((r.Flags & (int)RegionFlags.RegionOnline) ==
                (int)RegionFlags.RegionOnline)
                block.Access = r.Access;
            else
                block.Access = (byte)SimAccess.Down;
            block.MapImageID = r.TerrainImage;
            block.Name = r.RegionName;
            block.X = (ushort)(r.RegionLocX / Constants.RegionSize);
            block.Y = (ushort)(r.RegionLocY / Constants.RegionSize);
            block.SizeX = (ushort)r.RegionSizeX;
            block.SizeY = (ushort)r.RegionSizeY;

            return block;
        }

        protected List<MapBlockData> Map2BlockFromGridRegion (GridRegion r)
        {
            List<MapBlockData> blocks = new List<MapBlockData> ();
            MapBlockData block = new MapBlockData ();
            if (r == null) {
                block.Access = (byte)SimAccess.Down;
                block.MapImageID = UUID.Zero;
                blocks.Add (block);
                return blocks;
            }
            if ((r.Flags & (int)RegionFlags.RegionOnline) ==
                (int)RegionFlags.RegionOnline)
                block.Access = r.Access;
            else
                block.Access = (byte)SimAccess.Down;
            block.MapImageID = r.TerrainImage;
            block.Name = r.RegionName;
            block.X = (ushort)(r.RegionLocX / Constants.RegionSize);
            block.Y = (ushort)(r.RegionLocY / Constants.RegionSize);
            block.SizeX = (ushort)r.RegionSizeX;
            block.SizeY = (ushort)r.RegionSizeY;
            blocks.Add (block);
            if (r.RegionSizeX > Constants.RegionSize || r.RegionSizeY > Constants.RegionSize) {
                for (int x = 0; x < r.RegionSizeX / Constants.RegionSize; x++) {
                    for (int y = 0; y < r.RegionSizeY / Constants.RegionSize; y++) {
                        if (x == 0 && y == 0)
                            continue;
                        block = new MapBlockData {
                            Access = r.Access,
                            MapImageID = r.TerrainImage,
                            Name = r.RegionName,
                            X = (ushort)((r.RegionLocX / Constants.RegionSize) + x),
                            Y = (ushort)((r.RegionLocY / Constants.RegionSize) + y),
                            SizeX = (ushort)r.RegionSizeX,
                            SizeY = (ushort)r.RegionSizeY
                        };
                        //Child piece, so ignore it
                        blocks.Add (block);
                    }
                }
            }
            return blocks;
        }

        void OnMapNameRequest (IClientAPI remoteClient, string mapName, uint flags)
        {
            if (mapName.Length < 1) {
                remoteClient.SendAlertMessage ("Use a search string with at least 1 character");
                return;
            }

            bool TryCoordsSearch = false;
            int XCoord = 0;
            int YCoord = 0;

            string [] splitSearch = mapName.Split (',');
            if (splitSearch.Length != 1) {
                if (splitSearch [1].StartsWith (" ", StringComparison.Ordinal))
                    splitSearch [1] = splitSearch [1].Remove (0, 1);
                if (int.TryParse (splitSearch [0], out XCoord) && int.TryParse (splitSearch [1], out YCoord))
                    TryCoordsSearch = true;
            }

            List<MapBlockData> blocks = new List<MapBlockData> ();

            List<GridRegion> regionInfos = m_scene.GridService.GetRegionsByName (remoteClient.AllScopeIDs, mapName, 0, 20);
            if (TryCoordsSearch) {
                GridRegion region = m_scene.GridService.GetRegionByPosition (
                    remoteClient.AllScopeIDs,
                    XCoord * Constants.RegionSize,
                    YCoord * Constants.RegionSize
                );

                if (region != null) {
                    region.RegionName = mapName + " - " + region.RegionName;
                    regionInfos.Add (region);
                }
            }

            List<GridRegion> allRegions = new List<GridRegion> ();
            if (regionInfos != null) {
                foreach (GridRegion region in regionInfos) {
                    //Add the found in search region first
                    if (!allRegions.Contains (region)) {
                        allRegions.Add (region);
                        blocks.Add (SearchMapBlockFromGridRegion (region));
                    }
                    //Then send surrounding regions
                    List<GridRegion> nearRegions = m_scene.GridService.GetRegionRange (
                        remoteClient.AllScopeIDs,
                        (region.RegionLocX - (4 * Constants.RegionSize)),
                        (region.RegionLocX + (4 * Constants.RegionSize)),
                        (region.RegionLocY - (4 * Constants.RegionSize)),
                        (region.RegionLocY + (4 * Constants.RegionSize))
                    );

                    if (nearRegions != null) {
                        foreach (GridRegion nRegion in nearRegions) {
                            if (!allRegions.Contains (nRegion)) {
                                allRegions.Add (nRegion);
                                blocks.Add (SearchMapBlockFromGridRegion (nRegion));
                            }
                        }
                    }
                }
            }

            // final block, closing the search result
            MapBlockData data = new MapBlockData {
                Agents = 0,
                Access = 255,
                MapImageID = UUID.Zero,
                Name = mapName,
                RegionFlags = 0,
                WaterHeight = 0,
                X = 0,
                Y = 0,
                SizeX = 256,
                SizeY = 256
            };
            // not used
            blocks.Add (data);

            remoteClient.SendMapBlock (blocks, flags);
        }

        protected MapBlockData SearchMapBlockFromGridRegion (GridRegion region)
        {
            MapBlockData block = new MapBlockData ();
            if (region == null) {
                block.Access = (byte)SimAccess.Down;
                block.MapImageID = UUID.Zero;
                return block;
            }
            block.Access = region.Access;
            if ((region.Flags & (int)RegionFlags.RegionOnline) !=
                (int)RegionFlags.RegionOnline)
                block.Name = region.RegionName + " (offline)";
            else
                block.Name = region.RegionName;
            block.MapImageID = region.TerrainImage;
            block.Name = region.RegionName;
            block.X = (ushort)(region.RegionLocX / Constants.RegionSize);
            block.Y = (ushort)(region.RegionLocY / Constants.RegionSize);
            block.SizeX = (ushort)region.RegionSizeX;
            block.SizeY = (ushort)region.RegionSizeY;
            return block;
        }

        protected MapBlockData TerrainBlockFromGridRegion (GridRegion region)
        {
            MapBlockData block = new MapBlockData ();
            if (region == null) {
                block.Access = (byte)SimAccess.Down;
                block.MapImageID = UUID.Zero;
                return block;
            }
            block.Access = region.Access;
            block.MapImageID = region.TerrainMapImage;
            if ((region.Flags & (int)RegionFlags.RegionOnline) !=
                (int)RegionFlags.RegionOnline)
                block.Name = region.RegionName + " (offline)";
            else
                block.Name = region.RegionName;
            block.X = (ushort)(region.RegionLocX / Constants.RegionSize);
            block.Y = (ushort)(region.RegionLocY / Constants.RegionSize);
            block.SizeX = (ushort)region.RegionSizeX;
            block.SizeY = (ushort)region.RegionSizeY;
            return block;
        }

        public byte [] OnHTTPGetMapImage (string path, Stream request, OSHttpRequest httpRequest,
                                        OSHttpResponse httpResponse)
        {
            MainConsole.Instance.Debug ("[World map]: Sending map image jpeg");
            byte [] jpeg = new byte [0];

            MemoryStream imgstream = new MemoryStream ();
            Bitmap mapTexture;
            Image image = null;

            try {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                imgstream = new MemoryStream ();

                // non-async because we know we have the asset immediately.
                byte [] mapasset =
                    m_scene.AssetService.GetData (m_scene.RegionInfo.RegionSettings.TerrainImageID.ToString ());
                if (mapasset != null) {
                    image = m_scene.RequestModuleInterface<IJ2KDecoder> ().DecodeToImage (mapasset);
                    // Decode image to System.Drawing.Image
                    if (image != null) {
                        // Save to bitmap
                        mapTexture = (Bitmap)image;

                        EncoderParameters myEncoderParameters = new EncoderParameters ();
                        myEncoderParameters.Param [0] = new EncoderParameter (Encoder.Quality, 95L);
                        var encInfo = GetEncoderInfo ("image/jpeg");

                        if (encInfo != null) {
                            // Save bitmap to stream
                            mapTexture.Save (imgstream, encInfo, myEncoderParameters);
                        }
                        myEncoderParameters.Dispose ();

                        // Write the stream to a byte array for output
                        jpeg = imgstream.ToArray ();
                    }
                }
            } catch (Exception) {
                // Dummy!
                MainConsole.Instance.Warn ("[World map]: Unable to generate Map image");
            } finally {
                // Reclaim memory, these are unmanaged resources
                // If we encountered an exception, one or more of these will be null
                if (image != null)
                    image.Dispose ();

                imgstream.Close ();
            }

            httpResponse.ContentType = "image/jpeg";
            return jpeg;
        }

        // From MSDN
        static ImageCodecInfo GetEncoderInfo (string mimeType)
        {
            ImageCodecInfo [] encoders = null;
            try {
                encoders = ImageCodecInfo.GetImageEncoders ();
            } catch {
                return null;
            }

            return encoders.FirstOrDefault (t => t.MimeType == mimeType);
        }

        class MapItemRequester
        {
            public ulong regionhandle;
            public uint itemtype;
            public IClientAPI remoteClient;
            public uint flags;
        }
    }
}
