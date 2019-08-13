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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Nini.Config;
using ProtoBuf;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Services
{
    public class MapService : IService, IMapService
    {
        uint m_port = 8012;
        IHttpServer m_server;
        IRegistryCore m_registry;
        bool m_enabled = false;
        bool m_cacheEnabled = true;
        float m_cacheExpires = 24;
        IAssetService m_assetService;
        string m_assetCacheDir = "";
        string m_assetMapCacheDir;
        IGridService m_gridService;
        IJ2KDecoder m_j2kDecoder;
        static Bitmap m_blankRegionTile = null;
        MapTileIndex m_blankTiles = new MapTileIndex ();
        byte [] m_blankRegionTileData;
        int m_mapcenter_x = Constants.DEFAULT_REGIONSTART_X;
        int m_mapcenter_y = Constants.DEFAULT_REGIONSTART_Y;

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;

            var simbase = registry.RequestModuleInterface<ISimulationBase> ();
            m_mapcenter_x = simbase.MapCenterX;
            m_mapcenter_y = simbase.MapCenterY;

            var mapConfig = config.Configs ["MapService"];
            if (mapConfig != null) {
                m_enabled = mapConfig.GetBoolean ("Enabled", m_enabled);
                m_port = mapConfig.GetUInt ("Port", m_port);
                m_cacheEnabled = mapConfig.GetBoolean ("CacheEnabled", m_cacheEnabled);
                m_cacheExpires = mapConfig.GetFloat ("CacheExpires", m_cacheExpires);
            }
            if (!m_enabled)
                return;

            if (m_cacheEnabled) {
                m_assetCacheDir = config.Configs ["AssetCache"].GetString ("CacheDirectory", m_assetCacheDir);
                if (m_assetCacheDir == "") {
                    var defpath = registry.RequestModuleInterface<ISimulationBase> ().DefaultDataPath;
                    m_assetCacheDir = Path.Combine (defpath, Constants.DEFAULT_ASSETCACHE_DIR);
                }
                CreateCacheDirectories (m_assetCacheDir);
            }

            m_server = registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (m_port);
            m_server.AddStreamHandler (new GenericStreamHandler ("GET", "/MapService/", MapRequest));
            m_server.AddStreamHandler (new GenericStreamHandler ("GET", "/MapAPI/", MapAPIRequest));

            registry.RegisterModuleInterface<IMapService> (this);

            m_blankRegionTile = new Bitmap (256, 256);
            m_blankRegionTile.Tag = "StaticBlank";
            using (Graphics g = Graphics.FromImage (m_blankRegionTile)) {
                SolidBrush sea = new SolidBrush (Color.FromArgb (29, 71, 95));
                g.FillRectangle (sea, 0, 0, 256, 256);
            }
            m_blankRegionTileData = CacheMapTexture (1, 0, 0, m_blankRegionTile, true);
            /*string path = Path.Combine(m_assetCacheDir, Path.Combine("mapzoomlevels", "blankMap.index"));
            if(File.Exists(path))
            {
                FileStream stream = File.OpenRead(path);
                m_blankTiles = ProtoBuf.Serializer.Deserialize<MapTileIndex>(stream);
                stream.Close();
            }*/
        }

        void CreateCacheDirectories (string cacheDir)
        {
            if (!Directory.Exists (cacheDir))
                Directory.CreateDirectory (cacheDir);

            m_assetMapCacheDir = cacheDir + "/mapzoomlevels";
            if (!Directory.Exists (m_assetMapCacheDir))
                Directory.CreateDirectory (m_assetMapCacheDir);
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            if (!m_enabled) return;
            m_assetService = m_registry.RequestModuleInterface<IAssetService> ();
            m_gridService = m_registry.RequestModuleInterface<IGridService> ();
            m_j2kDecoder = m_registry.RequestModuleInterface<IJ2KDecoder> ();
        }

        public void FinishedStartup ()
        {
            if (!m_enabled) return;
            IGridServerInfoService serverInfo = m_registry.RequestModuleInterface<IGridServerInfoService> ();
            if (serverInfo != null)
                serverInfo.AddURI ("MapAPIService", MapServiceAPIURL);
            IGridInfo gridInfo = m_registry.RequestModuleInterface<IGridInfo> ();
            if (gridInfo != null)
                gridInfo.GridMapTileURI = MapServiceURL;
        }

        public string MapServiceURL {
            get { return m_server.ServerURI + "/MapService/"; }
        }

        public string MapServiceAPIURL {
            get { return m_server.ServerURI + "/MapAPI/"; }
        }

        public int MapCenterX {
            get { return m_mapcenter_x; }
        }

        public int MapCenterY {
            get { return m_mapcenter_y; }
        }

        public byte [] MapAPIRequest (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            byte [] response = MainServer.BlankResponse;

            var resp = "var {0} = {{regionName:\"{1}\",xloc:\"{2}\",yloc:\"{3}\",xsize:\"{4}\",ysize:\"{5}\"}};";
            var varName = httpRequest.Query ["var"].ToString ();
            var requestType = path.Substring (0, path.IndexOf ("?", StringComparison.Ordinal));

            if (requestType == "/MapAPI/get-region-coords-by-name") {
                string sim_name = httpRequest.Query ["sim_name"].ToString ();
                var region = m_gridService.GetRegionByName (null, sim_name);
                if (region == null)
                    resp = "var " + varName + "={error: true};";
                else {
                    resp = string.Format (resp, varName, region.RegionName,
                                          region.RegionLocX / Constants.RegionSize , region.RegionLocY / Constants.RegionSize,
                                          region.RegionSizeX, region.RegionSizeY);
                }
                response = System.Text.Encoding.UTF8.GetBytes (resp);
                httpResponse.ContentType = "text/javascript";
            } else if (requestType == "/MapAPI/get-region-name-by-coords") {
                int grid_x = int.Parse (httpRequest.Query ["grid_x"].ToString ());
                int grid_y = int.Parse (httpRequest.Query ["grid_y"].ToString ());
                var region = m_gridService.GetRegionByPosition (null,
                                                               grid_x * Constants.RegionSize,
                                                               grid_y * Constants.RegionSize);
                if (region == null) {
                    var maxRegionSize = m_gridService.GetMaxRegionSize ();
                    List<GridRegion> regions = m_gridService.GetRegionRange (null,
                                                                             (grid_x * Constants.RegionSize) - maxRegionSize,
                                                                             (grid_x * Constants.RegionSize) + maxRegionSize,
                                                                             (grid_y * Constants.RegionSize) - maxRegionSize,
                                                                             (grid_y * Constants.RegionSize) + maxRegionSize);
                    bool found = false;
                    foreach (var r in regions) {
                        if (r.PointIsInRegion (grid_x * Constants.RegionSize, grid_y * Constants.RegionSize)) {
                            resp = string.Format (resp, varName, r.RegionName,
							        			  r.RegionLocX / Constants.RegionSize, r.RegionLocY / Constants.RegionSize,
									        	  r.RegionSizeX, r.RegionSizeY);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        resp = "var " + varName + "={error: true};";
                } else {
                    resp = string.Format (resp, varName, region.RegionName,
										  region.RegionLocX / Constants.RegionSize, region.RegionLocY / Constants.RegionSize,
										  region.RegionSizeX, region.RegionSizeY);
                }
                response = System.Text.Encoding.UTF8.GetBytes (resp);
                httpResponse.ContentType = "text/javascript";
            }

            return response;
        }

        public byte [] MapRequest (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            //Remove the /MapService/
            string uri = httpRequest.RawUrl.Remove (0, 12);
            if (!uri.StartsWith ("map", StringComparison.Ordinal)) {
                if (uri == "") {
                    string resp = "<ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">" +
                                  "<Name>map.secondlife.com</Name>" +
                                  "<Prefix/>" +
                                  "<Marker/>" +
                                  "<MaxKeys>1000</MaxKeys>" +
                                  "<IsTruncated>true</IsTruncated>";

                    var txSize = m_mapcenter_x * Constants.RegionSize;
                    var tySize = m_mapcenter_x * Constants.RegionSize;
                    var etSize = 8 * Constants.RegionSize;
                    List<GridRegion> regions = m_gridService.GetRegionRange (
                                                   null, (txSize - etSize), (txSize + etSize), (tySize - etSize), (tySize + etSize));
                    foreach (var region in regions) {
                        resp += "<Contents><Key>map-1-" + region.RegionLocX / Constants.RegionSize +
                                "-" + region.RegionLocY / Constants.RegionSize +
                                "-objects.jpg</Key>" +
                                "<LastModified>2012-07-09T21:26:32.000Z</LastModified></Contents>";
                    }
                    resp += "</ListBucketResult>";
                    httpResponse.ContentType = "application/xml";
                    return System.Text.Encoding.UTF8.GetBytes (resp);
                }
                using (MemoryStream imgstream = new MemoryStream ()) {
                    GridRegion region = m_gridService.GetRegionByName (null, uri.Remove (4));
                    if (region == null) {
                        region = m_gridService.GetRegionByUUID (null, OpenMetaverse.UUID.Parse (uri.Remove (uri.Length - 4)));
                        if (region == null)         // unable to resoleve region details
                            return new byte [0];
                    }

                    // non-async because we know we have the asset immediately.
                    byte [] mapasset = null;
                    if (m_assetService.GetExists (region.TerrainMapImage.ToString ()))
                        mapasset = m_assetService.GetData (region.TerrainMapImage.ToString ());
                    if (mapasset != null) {
                        try {
                            Image image = m_j2kDecoder.DecodeToImage (mapasset);
                            if (image == null)
                                return new byte [0];
                            // Decode image to System.Drawing.Image

                            EncoderParameters myEncoderParameters = new EncoderParameters ();
                            myEncoderParameters.Param [0] = new EncoderParameter (Encoder.Quality, 95L);
                            var encInfo = GetEncoderInfo ("image/jpeg");
                            if (encInfo != null) {
                                // Save bitmap to stream
                                image.Save (imgstream, encInfo, myEncoderParameters);
                            }
                            image.Dispose ();
                            myEncoderParameters.Dispose ();

                            // Write the stream to a byte array for output
                            return imgstream.ToArray ();
                        } catch (Exception e) {
                            MainConsole.Instance.Debug ("Exception in Mapservice: " + e); 
                        }

                    }
                }
                return new byte [0];
            }
            string [] splitUri = uri.Split ('-');
            byte [] jpeg = FindCachedImage (uri);
            if (jpeg.Length != 0) {
                httpResponse.ContentType = "image/jpeg";
                return jpeg;
            }
            try {
                int mapLayer = int.Parse (uri.Substring (4, 1));
                int regionX = int.Parse (splitUri [2]);
                int regionY = int.Parse (splitUri [3]);
                int distance = (int)Math.Pow (2, mapLayer);
                int maxRegionSize = m_gridService.GetMaxRegionSize ();
                if (maxRegionSize == 0) maxRegionSize = Constants.MaxRegionSize;
                List<GridRegion> regions = m_gridService.GetRegionRange (
                    null,
                    ((regionX) * Constants.RegionSize) - maxRegionSize,
                    ((regionX + distance) * Constants.RegionSize) + maxRegionSize,
                    ((regionY) * Constants.RegionSize) - maxRegionSize,
                    ((regionY + distance) * Constants.RegionSize) + maxRegionSize);

                Bitmap mapTexture = BuildMapTile (mapLayer, regionX, regionY, regions);
                jpeg = CacheMapTexture (mapLayer, regionX, regionY, mapTexture);
                DisposeTexture (mapTexture);
            } catch (Exception e) {
                MainConsole.Instance.Debug ("Exception in Mapservice: " + e); 
            }
            httpResponse.ContentType = "image/jpeg";
            return jpeg;
        }

        Bitmap BuildMapTile (int mapView, int regionX, int regionY, List<GridRegion> regions)
        {
            Bitmap mapTexture = FindCachedImage (mapView, regionX, regionY);
            if (mapTexture != null)
                return mapTexture;
            if (mapView == 1)
                return BuildMapTile (regionX, regionY, regions.ToList ());

            const int SizeOfImage = 256;

            List<Bitmap> generatedMapTiles = new List<Bitmap> ();
            int offset = (int)(Math.Pow (2, mapView - 1) / 2f);
            generatedMapTiles.Add (BuildMapTile (mapView - 1, regionX, regionY, regions));
            generatedMapTiles.Add (BuildMapTile (mapView - 1, regionX + offset, regionY, regions));
            generatedMapTiles.Add (BuildMapTile (mapView - 1, regionX, regionY + offset, regions));
            generatedMapTiles.Add (BuildMapTile (mapView - 1, regionX + offset, regionY + offset, regions));
            bool isStatic = true;
            for (int i = 0; i < 4; i++)
                if (!IsStaticBlank (generatedMapTiles [i]))
                    isStatic = false;
                else
                    generatedMapTiles [i] = null;
            if (isStatic) {
                lock (m_blankTiles.BlankTilesLayers)
                    m_blankTiles.BlankTilesLayers.Add (((long)Util.IntsToUlong (regionX, regionY) << 8) + mapView);
                return m_blankRegionTile;
            }

            mapTexture = new Bitmap (SizeOfImage, SizeOfImage);
            using (Graphics g = Graphics.FromImage (mapTexture)) {
                SolidBrush sea = new SolidBrush (Color.FromArgb (29, 71, 95));
                g.FillRectangle (sea, 0, 0, SizeOfImage, SizeOfImage);

                if (generatedMapTiles [0] != null) {
                    Bitmap texture = ResizeBitmap (generatedMapTiles [0], 128, 128);
                    g.DrawImage (texture, new Point (0, 128));
                    DisposeTexture (texture);
                }

                if (generatedMapTiles [1] != null) {
                    Bitmap texture = ResizeBitmap (generatedMapTiles [1], 128, 128);
                    g.DrawImage (texture, new Point (128, 128));
                    DisposeTexture (texture);
                }

                if (generatedMapTiles [2] != null) {
                    Bitmap texture = ResizeBitmap (generatedMapTiles [2], 128, 128);
                    g.DrawImage (texture, new Point (0, 0));
                    DisposeTexture (texture);
                }

                if (generatedMapTiles [3] != null) {
                    Bitmap texture = ResizeBitmap (generatedMapTiles [3], 128, 128);
                    g.DrawImage (texture, new Point (128, 0));
                    DisposeTexture (texture);
                }
            }

            CacheMapTexture (mapView, regionX, regionY, mapTexture);
            return mapTexture;
        }

        void DisposeTexture (Bitmap bitmap)
        {
            if (!IsStaticBlank (bitmap))
                bitmap.Dispose ();
        }

        bool IsStaticBlank (Bitmap bitmap)
        {
            bool isStatic = false;
            if ((bitmap != null) && (bitmap.Tag is string))
                isStatic = ((string)bitmap.Tag == "StaticBlank");
            return isStatic;
        }

        Bitmap ResizeBitmap (Bitmap b, int nWidth, int nHeight)
        {
            Bitmap newsize = new Bitmap (nWidth, nHeight);
            using (Graphics temp = Graphics.FromImage (newsize)) {
                temp.DrawImage (b, 0, 0, nWidth, nHeight);
                temp.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            }
            DisposeTexture (b);
            return newsize;
        }

        Bitmap BuildMapTile (int regionX, int regionY, List<GridRegion> regions)
        {
            if (regions == null) {
                int maxRegionSize = m_gridService.GetMaxRegionSize ();
                if (maxRegionSize == 0) maxRegionSize = Constants.MaxRegionSize;
                regions = m_gridService.GetRegionRange (
                    null,
                    (regionX * Constants.RegionSize) - maxRegionSize,
                    (regionX * Constants.RegionSize) + maxRegionSize,
                    (regionY * Constants.RegionSize) - maxRegionSize,
                    (regionY * Constants.RegionSize) + maxRegionSize);
            }

            List<Image> bitImages = new List<Image> ();
            List<GridRegion> badRegions = new List<GridRegion> ();
            Rectangle mapRect = new Rectangle (regionX * Constants.RegionSize, regionY * Constants.RegionSize, Constants.RegionSize, Constants.RegionSize);
            foreach (GridRegion r in regions) {
                Rectangle regionRect = new Rectangle (r.RegionLocX, r.RegionLocY, r.RegionSizeX, r.RegionSizeY);
                if (!mapRect.IntersectsWith (regionRect))
                    badRegions.Add (r);
            }
            foreach (GridRegion r in badRegions)
                regions.Remove (r);
            badRegions.Clear ();
            IJ2KDecoder decoder = m_registry.RequestModuleInterface<IJ2KDecoder> ();
            foreach (GridRegion r in regions) {
                byte [] texAsset = null;
                if (m_assetService.GetExists (r.TerrainMapImage.ToString ()))
                    texAsset = m_assetService.GetData (r.TerrainMapImage.ToString ());

                if (texAsset != null) {
                    Image image = decoder.DecodeToImage (texAsset);
                    if (image != null)
                        bitImages.Add (image);
                    else
                        badRegions.Add (r);
                } else
                    badRegions.Add (r);
            }
            foreach (GridRegion r in badRegions)
                regions.Remove (r);

            if (regions.Count == 0) {
                lock (m_blankTiles.BlankTiles)
                    m_blankTiles.BlankTiles.Add (Util.IntsToUlong (regionX, regionY));
                return m_blankRegionTile;
            }

            const int SizeOfImage = Constants.RegionSize;           // 256

            Bitmap mapTexture = new Bitmap (SizeOfImage, SizeOfImage);
            using (Graphics g = Graphics.FromImage (mapTexture)) {
                SolidBrush sea = new SolidBrush (Color.FromArgb (29, 71, 95));
                g.FillRectangle (sea, 0, 0, SizeOfImage, SizeOfImage);

                for (int i = 0; i < regions.Count; i++) {
                    //Find the offsets first
                    float x = (regions [i].RegionLocX - (regionX * (float)Constants.RegionSize)) /
                                Constants.RegionSize;
                    float y = (regions [i].RegionLocY - (regionY * (float)Constants.RegionSize)) /
                                Constants.RegionSize;
                    y += (regions [i].RegionSizeY - Constants.RegionSize) / Constants.RegionSize;
                    float xx = (x * (SizeOfImage));
                    float yy = SizeOfImage - (y * (SizeOfImage) + (SizeOfImage));
                    g.DrawImage (bitImages [i], xx, yy,
                        (int)(SizeOfImage * ((float)regions [i].RegionSizeX / Constants.RegionSize)),
                        (int)(SizeOfImage * (regions [i].RegionSizeY / (float)Constants.RegionSize))); // y origin is top
                }
            }

            foreach (var bmp in bitImages)
                bmp.Dispose ();

            CacheMapTexture (1, regionX, regionY, mapTexture);
            //mapTexture = ResizeBitmap(mapTexture, 128, 128);
            return mapTexture;
        }

        // From MSDN
        static ImageCodecInfo GetEncoderInfo (string mimeType)
        {
            ImageCodecInfo [] encoders;
            try {
                encoders = ImageCodecInfo.GetImageEncoders ();
            } catch {
                return null;
            }

            return encoders.FirstOrDefault (t => t.MimeType == mimeType);
        }

        byte [] FindCachedImage (string name)
        {
            if (!m_cacheEnabled)
                return new byte [0];

            string fullPath = Path.Combine (m_assetMapCacheDir, name);
            if (File.Exists (fullPath)) {
                //Make sure the time is ok
                if (DateTime.Now < File.GetLastWriteTime (fullPath).AddHours (m_cacheExpires))
                    return File.ReadAllBytes (fullPath);
            }
            return new byte [0];
        }

        Bitmap FindCachedImage (int maplayer, int regionX, int regionY)
        {
            if (!m_cacheEnabled)
                return null;

            if (maplayer == 1) {
                lock (m_blankTiles.BlankTiles)
                    if (m_blankTiles.BlankTiles.Contains (Util.IntsToUlong (regionX, regionY)))
                        return m_blankRegionTile;
            } else {
                lock (m_blankTiles.BlankTilesLayers)
                    if (m_blankTiles.BlankTilesLayers.Contains (((long)Util.IntsToUlong (regionX, regionY) << 8) + maplayer))
                        return m_blankRegionTile;
            }

            string name = string.Format ("map-{0}-{1}-{2}-objects.jpg", maplayer, regionX, regionY);
            string fullPath = Path.Combine (m_assetMapCacheDir, name);
            if (File.Exists (fullPath)) {
                //Make sure the time is ok
                if (DateTime.Now < File.GetLastWriteTime (fullPath).AddHours (m_cacheExpires)) {
                    using (MemoryStream imgstream = new MemoryStream (File.ReadAllBytes (fullPath))) {
                        return new Bitmap (imgstream);
                    }
                }
            }
            return null;
        }

        byte [] CacheMapTexture (int maplayer, int regionX, int regionY, Bitmap mapTexture, bool forced = false)
        {
            if (!forced && IsStaticBlank (mapTexture))
                return m_blankRegionTileData;

            byte [] jpeg;
            EncoderParameters myEncoderParameters = new EncoderParameters ();
            myEncoderParameters.Param [0] = new EncoderParameter (Encoder.Quality, 95L);

            using (MemoryStream imgstream = new MemoryStream ()) {
                var encInfo = GetEncoderInfo ("image/jpeg");
                if (encInfo != null) {
                    // Save bitmap to stream
                    lock (mapTexture)
                        mapTexture.Save (imgstream, encInfo, myEncoderParameters);
                }
                // Write the stream to a byte array for output
                jpeg = imgstream.ToArray ();

            }

            myEncoderParameters.Dispose ();
            SaveCachedImage (maplayer, regionX, regionY, jpeg);
            return jpeg;
        }

        void SaveCachedImage (int maplayer, int regionX, int regionY, byte [] data)
        {
            if (!m_cacheEnabled)
                return;

            string name = string.Format ("map-{0}-{1}-{2}-objects.jpg", maplayer, regionX, regionY);
            //string fullPath = Path.Combine(m_assetCacheDir, Path.Combine("mapzoomlevels", name));
            string fullPath = Path.Combine (m_assetMapCacheDir, name);
            File.WriteAllBytes (fullPath, data);
        }
    }

    [ProtoContract ()]
    class MapTileIndex
    {
        [ProtoMember (1)]
        public HashSet<ulong> BlankTiles = new HashSet<ulong> ();
        [ProtoMember (2)]
        public HashSet<long> BlankTilesLayers = new HashSet<long> ();
    }
}
