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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Timers;

namespace WhiteCore.Modules.WorldMap
{
    public enum DrawRoutine
    {
        Rectangle,
        Polygon,
        Ellipse
    }

    public struct face
    {
        public Point[] pts;
    }

    public struct DrawStruct
    {
        public SolidBrush brush;
        public DrawRoutine dr;
        public Rectangle rect;
        public face[] trns;
    }

    public class MapImageModule : IMapImageGenerator, INonSharedRegionModule, IDisposable
    {
        IConfigSource m_config;
        Dictionary<UUID, Color> m_mapping;
        IScene m_scene;
        IMapTileTerrainRenderer terrainRenderer;
        double minutes = 60*24;
        const double oneminute = 60000;
        Timer UpdateMapImage;
        Timer UpdateOnlineStatus;
        bool m_generateMapTiles = true;
        UUID staticMapTileUUID = UUID.Zero;
        UUID regionMapTileUUID = UUID.Zero;
        bool m_asyncMapTileCreation = false;
        string m_assetCacheDir = Constants.DEFAULT_ASSETCACHE_DIR;
        string m_assetMapCacheDir = Constants.DEFAULT_ASSETCACHE_DIR + "/mapzoomlevels";
        string m_assetWorldviewCacheDir = Constants.DEFAULT_ASSETCACHE_DIR + "/Worldview";

        #region IMapImageGenerator Members

        public void CreateMapTile(out Bitmap terrainBMP, out Bitmap mapBMP)
        {

            int start = Environment.TickCount;
            bool drawPrimVolume = true;
            string tileRenderer = "WarpTileRenderer";

            if (m_config.Configs["MapModule"] != null)
            {
                drawPrimVolume = m_config.Configs["MapModule"].GetBoolean("DrawPrimOnMapTile", drawPrimVolume);
                tileRenderer = m_config.Configs["MapModule"].GetString("TerrainTileRenderer", tileRenderer);
            }

            if (tileRenderer == "TexturedMapTileRenderer")
            {
                terrainRenderer = new TexturedMapTileRenderer();
            }
            else if (tileRenderer == "ShadedMapTileRenderer")
            {
                terrainRenderer = new ShadedMapTileRenderer();
            }
            else
            {
                tileRenderer = "WarpTileRenderer";
                terrainRenderer = new WarpTileRenderer();
                drawPrimVolume = false;
            }

            terrainRenderer.Initialise(m_scene, m_config);

            mapBMP = null;
            terrainBMP = new Bitmap(Constants.RegionSize, Constants.RegionSize, PixelFormat.Format24bppRgb);
            terrainBMP = terrainRenderer.TerrainToBitmap(terrainBMP);

            if (drawPrimVolume && terrainBMP != null)
            {
                mapBMP = new Bitmap(terrainBMP);
                mapBMP = DrawObjectVolume(m_scene, mapBMP);
            }
            else
            {
                if (terrainBMP != null) mapBMP = new Bitmap(terrainBMP);
            }

            if (m_mapping != null)
            {
                SaveCache();
                m_mapping.Clear();
            }

            terrainRenderer = null;

            MainConsole.Instance.InfoFormat("[MapTileGenerator]: Generating Maptile for {0}, using {1}, took {2} ms",
                m_scene.RegionInfo.RegionName, tileRenderer, (Environment.TickCount - start));

        }


        public void CreateMapTile(out byte[] terrain, out byte[] map)
        {
            terrain = null;
            map = null;
            Bitmap terrainBMP, mapBMP;
            CreateMapTile(out terrainBMP, out mapBMP);

            if (terrainBMP != null)
            {
                terrain = OpenJPEG.EncodeFromImage(terrainBMP, true);
                terrainBMP.Dispose();
            }
            if (mapBMP != null)
            {
                map = OpenJPEG.EncodeFromImage(mapBMP, true);
                mapBMP.Dispose();
            }
                
        }

        public Bitmap CreateViewImage(Vector3 camPos, Vector3 camDir, float fov, int width, int height, bool useTextures)
        {
            int start = Environment.TickCount;
            var renderer = new WarpTileRenderer();

            renderer.Initialise(m_scene, m_config);
            Bitmap worldView = renderer.CreateViewImage (camPos, camDir, fov, width, height, useTextures);

            MainConsole.Instance.InfoFormat("[MapTileGenerator]: Generating worldview for {0} took {1} ms",
                m_scene.RegionInfo.RegionName, (Environment.TickCount - start));

            return worldView;
        }

        public Bitmap CreateViewTileImage(int size)
        {
            int start = Environment.TickCount;
            var renderer = new WarpTileRenderer();

            renderer.Initialise(m_scene, m_config);
            Bitmap worldMap = renderer.TerrainToBitmap (null, size);

            MainConsole.Instance.InfoFormat("[MapTileGenerator]: Generating world maptile for {0} took {1} ms",
                m_scene.RegionInfo.RegionName, (Environment.TickCount - start));

            return worldMap;
        }

        public void UpdateWorldMaps()
        {
            if (m_scene != null)
            {
                CreateTerrainTexture (true);

                ClearWebCachedImages(
                    m_scene.RegionInfo.RegionLocX,
                    m_scene.RegionInfo.RegionLocY,
                    m_scene.RegionInfo.RegionSizeX,
                    m_scene.RegionInfo.RegionSizeY
                );
                ClearWorldviewCachedImages (m_scene.RegionInfo.RegionID);
            }
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource source)
        {
            m_config = source;
        }

        public void AddRegion(IScene scene)
        {
            m_scene = scene;

            IConfig startupConfig = m_config.Configs["Startup"];
            if (startupConfig.GetString("MapImageModule", "MapImageModule") !=
                "MapImageModule")
                return;

            if (m_config.Configs["MapModule"] != null)
            {
                string name = scene.RegionInfo.RegionName;
                name = name.Replace(' ', '_');
                string regionMapTile = m_config.Configs["MapModule"].GetString(name + "MaptileStaticUUID", "");
                if (regionMapTile != "")
                {
                    //It exists, override the default
                    UUID.TryParse(regionMapTile, out regionMapTileUUID);
                }
                m_asyncMapTileCreation = m_config.Configs["MapModule"].GetBoolean("UseAsyncMapTileCreation",
                                                                                  m_asyncMapTileCreation);
                minutes = m_config.Configs["MapModule"].GetDouble("TimeBeforeMapTileRegeneration", minutes);
                m_generateMapTiles = m_config.Configs["MapModule"].GetBoolean("GenerateMaptiles", true);
                UUID.TryParse(m_config.Configs["MapModule"].GetString("MaptileStaticUUID", UUID.Zero.ToString()),
                              out staticMapTileUUID);
            }

            // get cache dir
            m_assetCacheDir = m_config.Configs ["AssetCache"].GetString ("CacheDirectory",m_assetCacheDir);
            m_assetMapCacheDir = m_assetCacheDir + "/mapzoomlevels";
            m_assetWorldviewCacheDir = m_assetCacheDir + "/Worldview";


            m_scene.RegisterModuleInterface<IMapImageGenerator>(this);

            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand(
                    "update map",
                    "update map",
                    "Updates the maptile image of the world map",
                    HandleUpdateWorldMapConsoleCommand, true, false);
            }

            scene.EventManager.OnStartupComplete += StartupComplete;
        }

        public void RemoveRegion(IScene scene)
        {
            if (UpdateMapImage != null)
            {
                UpdateMapImage.Stop();
                UpdateMapImage.Elapsed -= OnTimedCreateNewMapImage;
                UpdateMapImage.Enabled = false;
                UpdateMapImage.Close();
            }

            if (UpdateOnlineStatus != null)
            {
                UpdateOnlineStatus.Stop();
                UpdateOnlineStatus.Elapsed -= OnUpdateRegion;
                UpdateOnlineStatus.Enabled = false;
                UpdateOnlineStatus.Close();
            }
        }

        public void RegionLoaded(IScene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "MapImageModule"; }
        }

        #endregion

        public void StartupComplete(IScene scene, List<string> data)
        {
            //Startup complete, we can generate a tile now
            if ((DateTime.Now - m_scene.RegionInfo.RegionSettings.TerrainMapLastRegenerated).TotalMinutes > minutes)
                CreateTerrainTexture();
            //and set up timers.
            SetUpTimers();
        }

        public void SetUpTimers()
        {
            if (m_generateMapTiles)
            {
                UpdateMapImage = new Timer(oneminute*minutes);
                UpdateMapImage.Elapsed += OnTimedCreateNewMapImage;
                UpdateMapImage.Enabled = true;
            }
            UpdateOnlineStatus = new Timer(oneminute*60);
            UpdateOnlineStatus.Elapsed += OnUpdateRegion;
            UpdateOnlineStatus.Enabled = true;
        }

        /// <summary>
        ///     Update the world map
        /// </summary>
        public void HandleUpdateWorldMapConsoleCommand(IScene scene, string[] cmdparams)
        {
            if (scene != null)
            {
                m_scene = scene;
                CreateTerrainTexture (true);

                ClearWebCachedImages(
                    m_scene.RegionInfo.RegionLocX,
                    m_scene.RegionInfo.RegionLocY,
                    m_scene.RegionInfo.RegionSizeX,
                    m_scene.RegionInfo.RegionSizeY
                );
                ClearWorldviewCachedImages (m_scene.RegionInfo.RegionID);
            }
        }

        private void OnUpdateRegion(object source, ElapsedEventArgs e)
        {
            if (m_scene != null)
            {
                IGridRegisterModule gridRegModule = m_scene.RequestModuleInterface<IGridRegisterModule>();
                if (gridRegModule != null)
                    gridRegModule.UpdateGridRegion(m_scene);
            }
        }

        private void OnTimedCreateNewMapImage(object source, ElapsedEventArgs e)
        {
            if (m_scene.SimulationDataService.MapTileNeedsGenerated)
            {
                CreateTerrainTexture ();
                m_scene.SimulationDataService.MapTileNeedsGenerated = false;
                ClearWebCachedImages(
                    m_scene.RegionInfo.RegionLocX,
                    m_scene.RegionInfo.RegionLocY,
                    m_scene.RegionInfo.RegionSizeX,
                    m_scene.RegionInfo.RegionSizeY
                );
                ClearWorldviewCachedImages (m_scene.RegionInfo.RegionID);
            }

        }

        /// <summary>
        ///     Create a terrain texture for this scene
        /// </summary>
        public void CreateTerrainTexture()
        {
            CreateTerrainTexture(false);
        }

        /// <summary>
        ///     Create a terrain texture for this scene
        /// </summary>
        public void CreateTerrainTexture(bool forced)
        {
            if (!m_generateMapTiles && !forced)
            {
                //They want a static texture, lock it in.
                if (regionMapTileUUID != UUID.Zero)
                {
                    m_scene.RegionInfo.RegionSettings.TerrainMapImageID = regionMapTileUUID;
                    m_scene.RegionInfo.RegionSettings.TerrainImageID = regionMapTileUUID;
                }
                else if (staticMapTileUUID != UUID.Zero)
                {
                    m_scene.RegionInfo.RegionSettings.TerrainMapImageID = staticMapTileUUID;
                    m_scene.RegionInfo.RegionSettings.TerrainImageID = staticMapTileUUID;
                }
                m_scene.RegionInfo.RegionSettings.TerrainMapLastRegenerated = DateTime.Now;

                return;
            }

            // Cannot create a map for a nonexistant heightmap.
            ITerrainChannel heightmap = m_scene.RequestModuleInterface<ITerrainChannel>();
            if (heightmap == null)
                 return;
 
            if (!m_asyncMapTileCreation)
            {
                CreateMapTileAsync(null);
            }
            else
            {
                Util.FireAndForget(CreateMapTileAsync);
            }
                
        }

        #region Async map tile

        protected void CreateMapTileAsyncCompleted(IAsyncResult iar)
        {
            CreateMapTileAsyncCall icon = (CreateMapTileAsyncCall) iar.AsyncState;
            icon.EndInvoke(iar);
        }

        public delegate void CreateMapTileAsyncCall(AssetBase Mapasset, AssetBase Terrainasset);

        #endregion

        #region Generate map tile

        public void CreateMapTileAsync(object worthless)
        {
            IMapImageGenerator terrain = m_scene.RequestModuleInterface<IMapImageGenerator>();

            if (terrain == null)
                return;
                
            byte[] terraindata, mapdata;
            terrain.CreateMapTile(out terraindata, out mapdata);

            if (terraindata != null)
            {
                if (m_scene.RegionInfo.RegionSettings.TerrainMapImageID != UUID.Zero)
                    m_scene.RegionInfo.RegionSettings.TerrainMapImageID =
                        m_scene.AssetService.UpdateContent(m_scene.RegionInfo.RegionSettings.TerrainMapImageID,
                                                           terraindata);
                if (m_scene.RegionInfo.RegionSettings.TerrainMapImageID == UUID.Zero)
                    //Do not optimize away! UpdateContent can fail sometimes!
                {
                    AssetBase Terrainasset = new AssetBase(
                        UUID.Random(),
                        "terrainMapImage_" + m_scene.RegionInfo.RegionID.ToString(),
                        AssetType.Simstate,
                        m_scene.RegionInfo.RegionID)
                                                 {
                                                     Data = terraindata,
                                                     Description = m_scene.RegionInfo.RegionName,
                                                     Flags =
                                                         AssetFlags.Deletable | AssetFlags.Rewritable |
                                                         AssetFlags.Maptile
                                                 };
                    m_scene.RegionInfo.RegionSettings.TerrainMapImageID = m_scene.AssetService.Store(Terrainasset);
                }
            }

            if (mapdata != null)
            {
                if (m_scene.RegionInfo.RegionSettings.TerrainImageID != UUID.Zero)
                    m_scene.RegionInfo.RegionSettings.TerrainImageID =
                        m_scene.AssetService.UpdateContent(m_scene.RegionInfo.RegionSettings.TerrainImageID, mapdata);
                if (m_scene.RegionInfo.RegionSettings.TerrainImageID == UUID.Zero)
                    //Do not optimize away! UpdateContent can fail sometimes!
                {
                    AssetBase Mapasset = new AssetBase(
                        UUID.Random(),
                        "terrainImage_" + m_scene.RegionInfo.RegionID.ToString(),
                        AssetType.Simstate,
                        m_scene.RegionInfo.RegionID)
                                             {
                                                 Data = mapdata,
                                                 Description = m_scene.RegionInfo.RegionName,
                                                 Flags =
                                                     AssetFlags.Deletable | AssetFlags.Rewritable | AssetFlags.Maptile
                                             };
                    m_scene.RegionInfo.RegionSettings.TerrainImageID = m_scene.AssetService.Store(Mapasset);
                }
            }

            byte[] overlay = GenerateOverlay();

            if (overlay != null)
            {
                if (m_scene.RegionInfo.RegionSettings.ParcelMapImageID != UUID.Zero)
                    m_scene.RegionInfo.RegionSettings.ParcelMapImageID =
                        m_scene.AssetService.UpdateContent(m_scene.RegionInfo.RegionSettings.ParcelMapImageID, overlay);
                if (m_scene.RegionInfo.RegionSettings.ParcelMapImageID == UUID.Zero)
                    //Do not optimize away! UpdateContent can fail sometimes!
                {
                    AssetBase Parcelasset = new AssetBase(
                        UUID.Random(),
                        "terrainMapImage_" + m_scene.RegionInfo.RegionID,
                        AssetType.Simstate,
                        m_scene.RegionInfo.RegionID)
                                                {
                                                    Data = overlay,
                                                    Description = m_scene.RegionInfo.RegionName,
                                                    Flags =
                                                        AssetFlags.Deletable | AssetFlags.Rewritable |
                                                        AssetFlags.Maptile
                                                };
                    m_scene.RegionInfo.RegionSettings.ParcelMapImageID = m_scene.AssetService.Store(Parcelasset);
                }
            }
            else
                m_scene.RegionInfo.RegionSettings.ParcelMapImageID = UUID.Zero;

            m_scene.RegionInfo.RegionSettings.TerrainMapLastRegenerated = DateTime.Now;

            //Update the grid map
            IGridRegisterModule gridRegModule = m_scene.RequestModuleInterface<IGridRegisterModule>();
            if (gridRegModule != null)
                gridRegModule.UpdateGridRegion(m_scene);

            // clear out... these are all redundant?
            //terraindata = null;
            //mapdata = null;
            //overlay = null;
            //terrain = null;

         }

        /// <summary>
        /// Generates the overlay.
        /// </summary>
        /// <returns>The overlay.</returns>
        private Byte[] GenerateOverlay()
        {
            Bitmap overlay = new Bitmap(m_scene.RegionInfo.RegionSizeX, m_scene.RegionInfo.RegionSizeY);

            bool[,] saleBitmap = new bool[m_scene.RegionInfo.RegionSizeX/4,m_scene.RegionInfo.RegionSizeX/4];
            bool[,] auctionBitmap = new bool[m_scene.RegionInfo.RegionSizeX/4,m_scene.RegionInfo.RegionSizeX/4];
            for (int x = 0; x < m_scene.RegionInfo.RegionSizeX/4; x++)
            {
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY/4; y++)
                {
                    saleBitmap[x, y] = false;
                    auctionBitmap[x, y] = false;
                }
            }

            bool landForSale = false;

            List<ILandObject> parcels = m_scene.RequestModuleInterface<IParcelManagementModule>().AllParcels();

            Color background = Color.FromArgb(0, 0, 0, 0);
            SolidBrush transparent = new SolidBrush(background);
            Graphics g = Graphics.FromImage(overlay);
            g.FillRectangle(transparent, 0, 0, m_scene.RegionInfo.RegionSizeX, m_scene.RegionInfo.RegionSizeY);

            SolidBrush yellow = new SolidBrush(Color.FromArgb(255, 249, 223, 9));
            SolidBrush purple = new SolidBrush(Color.Purple);

            foreach (ILandObject land in parcels)
            {
                // MainConsole.Instance.DebugFormat("[WORLD MAP]: Parcel {0} flags {1}", land.LandData.Name, land.LandData.Flags);
                if ((land.LandData.Flags & (uint) ParcelFlags.ForSale) != 0)
                {
                    landForSale = true;

                    for (int x = 0; x < m_scene.RegionInfo.RegionSizeX/4; x++)
                    {
                        for (int y = 0; y < m_scene.RegionInfo.RegionSizeY/4; y++)
                            if (land.ContainsPoint(x, y))
                                if (land.LandData.AuctionID > 0)
                                    auctionBitmap[x, y] = true;
                                else
                                    saleBitmap[x, y] = true;
                    }
                }
            }

            if (!landForSale)
                return null;

            for (int x = 0; x < m_scene.RegionInfo.RegionSizeX/4; x++)
            {
                for (int y = 0; y < m_scene.RegionInfo.RegionSizeY/4; y++)
                {
                    if (saleBitmap[x, y])
                        g.FillRectangle(yellow, x*4, m_scene.RegionInfo.RegionSizeY - 4 - (y*4), 4, 4);
                    if (auctionBitmap[x, y])
                        g.FillRectangle(purple, x*4, m_scene.RegionInfo.RegionSizeY - 4 - (y*4), 4, 4);
                }
            }

            try
            {
                return OpenJPEG.EncodeFromImage(overlay, true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void RegenerateMaptile(string ID, byte[] data)
        {
            MemoryStream imgstream = new MemoryStream();
            Bitmap mapTexture = new Bitmap(1, 1);
            Image image = null;

            try
            {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                imgstream = new MemoryStream();

                image = m_scene.RequestModuleInterface<IJ2KDecoder>().DecodeToImage(data);
                // Decode image to System.Drawing.Image
                if (image != null)
                {
                    // Save to bitmap
                    mapTexture = new Bitmap(image);
                    mapTexture = OverlayBitmapText(mapTexture,"Region");

                    EncoderParameters myEncoderParameters = new EncoderParameters();
                    myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

                    // Save bitmap to stream
                    mapTexture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);
                }
            }
            catch (Exception)
            {
                // Dummy!
                MainConsole.Instance.Warn("[WORLD MAP]: Unable to generate Map image");
            }
            finally
            {
                // Reclaim memory, these are unmanaged resources
                // If we encountered an exception, one or more of these will be null
                mapTexture.Dispose();

                if (image != null)
                    image.Dispose();

                imgstream.Close();
            }
        }

        // From msdn
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo t in encoders)
            {
                if (t.MimeType == mimeType) return t;
            }
            return null;
        }

        private Bitmap OverlayBitmapText(Image bMap, string overlayText)
        {
            Bitmap newMap = new Bitmap(bMap);
            Graphics temp = Graphics.FromImage(newMap);
            temp.DrawString(overlayText, new Font("Arial", 8, FontStyle.Regular),
                new SolidBrush(Color.FromArgb(90, 255, 255, 255)), new Point(2, 2));     // top left corner

            return newMap;
        }

        #endregion

        private Bitmap DrawObjectVolume(IScene whichScene, Bitmap mapbmp)
        {
            ITerrainChannel heightmap = whichScene.RequestModuleInterface<ITerrainChannel>();
            //MainConsole.Instance.Info("[MAPTILE]: Generating Maptile Step 2: Object Volume Profile");
            ISceneEntity[] objs = whichScene.Entities.GetEntities();
            Dictionary<uint, DrawStruct> z_sort = new Dictionary<uint, DrawStruct>();
            //SortedList<float, RectangleDrawStruct> z_sort = new SortedList<float, RectangleDrawStruct>();
            List<float> z_sortheights = new List<float>();
            List<uint> z_localIDs = new List<uint>();

            lock (objs)
            {
                foreach (ISceneEntity mapdot in objs)
                {
                    // Only draw the contents of SceneObjectGroup
                    Color mapdotspot = Color.Gray; // Default color when prim color is white

                    // Loop over prim in group
                    foreach (ISceneChildEntity part in mapdot.ChildrenEntities())
                    {
                        if (part == null || part.Shape == null)
                            continue;

                        // Draw if the object is at least .5 meter wide in any direction
                        if (part.Scale.X > .5f || part.Scale.Y > .5f || part.Scale.Z > .5f)
                        {
                            Vector3 pos = part.GetWorldPosition();

                            // skip prim outside of retion
                            if (pos.X < 0f || pos.X > 256f || pos.Y < 0f || pos.Y > 256f)
                                continue;

                            // skip prim in non-finite position
                            if (Single.IsNaN(pos.X) || Single.IsNaN(pos.Y) ||
                                Single.IsInfinity(pos.X) || Single.IsInfinity(pos.Y))
                                continue;

                            // Figure out if object is under 256m above the height of the terrain
                            bool isBelow256AboveTerrain = false;

                            try
                            {
                                if ((int) pos.X == m_scene.RegionInfo.RegionSizeX)
                                    pos.X = m_scene.RegionInfo.RegionSizeX - 1;
                                if ((int) pos.Y == m_scene.RegionInfo.RegionSizeY)
                                    pos.Y = m_scene.RegionInfo.RegionSizeY - 1;
                                isBelow256AboveTerrain = (pos.Z < (heightmap[(int) pos.X, (int) pos.Y] + 256f));
                            }
                            catch (Exception)
                            {
                            }

                            if (isBelow256AboveTerrain)
                            {
                                // Try to get the RGBA of the default texture entry..
                                //
                                try
                                {
                                    // get the null checks out of the way
                                    // skip the ones that break
                                    if (part == null)
                                        continue;

                                    if (part.Shape == null)
                                        continue;

                                    if (part.Shape.PCode == (byte) PCode.Tree ||
                                        part.Shape.PCode == (byte) PCode.NewTree ||
                                        part.Shape.PCode == (byte) PCode.Grass)
                                        continue;
                                    // eliminates trees from this since we don't really have a good tree representation
                                    // if you want tree blocks on the map comment the above line and uncomment the below line
                                    //mapdotspot = Color.PaleGreen;

                                    Primitive.TextureEntry textureEntry = part.Shape.Textures;

                                    if (textureEntry == null || textureEntry.DefaultTexture == null)
                                        continue;
                                    Color texcolor = Color.Black;
                                    try
                                    {
                                        Primitive.TextureEntryFace tx = part.Shape.Textures.CreateFace(6);
                                        texcolor = computeAverageColor(tx.TextureID, Color.Black);
                                    }
                                    catch (Exception)
                                    {
                                        texcolor = Color.FromArgb((int) textureEntry.DefaultTexture.RGBA.A,
                                                                  (int) textureEntry.DefaultTexture.RGBA.R,
                                                                  (int) textureEntry.DefaultTexture.RGBA.G,
                                                                  (int) textureEntry.DefaultTexture.RGBA.B);
                                    }

                                    if (!(texcolor.R == 255 && texcolor.G == 255 && texcolor.B == 255))
                                    {
                                        // Try to set the map spot color
                                        // If the color gets goofy somehow, skip it *shakes fist at Color4
                                        mapdotspot = texcolor;
                                    }
                                }
                                catch (IndexOutOfRangeException)
                                {
                                    // Windows Array
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    // Mono Array
                                }
                                // Translate scale by rotation so scale is represented properly when object is rotated
                                Vector3 lscale = new Vector3(part.Shape.Scale.X, part.Shape.Scale.Y,
                                                             part.Shape.Scale.Z);
                                Vector3 scale = new Vector3();
                                Vector3 tScale = new Vector3();
                                Vector3 axPos = new Vector3(pos.X, pos.Y, pos.Z);

                                scale = lscale*part.GetWorldRotation();

                                // negative scales don't work in this situation
                                scale.X = Math.Abs(scale.X);
                                scale.Y = Math.Abs(scale.Y);
                                scale.Z = Math.Abs(scale.Z);

                                // This scaling isn't very accurate and doesn't take into account the face rotation :P
                                int mapdrawstartX = (int) (pos.X - scale.X);
                                int mapdrawstartY = (int) (pos.Y - scale.Y);
                                int mapdrawendX = (int) (pos.X + scale.X);
                                int mapdrawendY = (int) (pos.Y + scale.Y);

                                // If object is beyond the edge of the map, don't draw it to avoid errors
                                if (mapdrawstartX < 0 || mapdrawstartX > (m_scene.RegionInfo.RegionSizeX - 1) ||
                                    mapdrawendX < 0 || mapdrawendX > (m_scene.RegionInfo.RegionSizeX - 1)
                                    || mapdrawstartY < 0 || mapdrawstartY > (m_scene.RegionInfo.RegionSizeY - 1) ||
                                    mapdrawendY < 0
                                    || mapdrawendY > (m_scene.RegionInfo.RegionSizeY - 1))
                                    continue;

                                #region obb face reconstruction part duex

                                Vector3[] vertexes = new Vector3[8];

                                // float[] distance = new float[6];
                                Vector3[] FaceA = new Vector3[6]; // vertex A for Facei
                                Vector3[] FaceB = new Vector3[6]; // vertex B for Facei
                                Vector3[] FaceC = new Vector3[6]; // vertex C for Facei
                                Vector3[] FaceD = new Vector3[6]; // vertex D for Facei

                                tScale = new Vector3(lscale.X, -lscale.Y, lscale.Z);
                                scale = ((tScale*part.GetWorldRotation()));
                                vertexes[0] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                                // vertexes[0].x = pos.X + vertexes[0].x;
                                //vertexes[0].y = pos.Y + vertexes[0].y;
                                //vertexes[0].z = pos.Z + vertexes[0].z;

                                FaceA[0] = vertexes[0];
                                FaceB[3] = vertexes[0];
                                FaceA[4] = vertexes[0];

                                tScale = lscale;
                                scale = ((tScale*part.GetWorldRotation()));
                                vertexes[1] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                // vertexes[1].x = pos.X + vertexes[1].x;
                                // vertexes[1].y = pos.Y + vertexes[1].y;
                                //vertexes[1].z = pos.Z + vertexes[1].z;

                                FaceB[0] = vertexes[1];
                                FaceA[1] = vertexes[1];
                                FaceC[4] = vertexes[1];

                                tScale = new Vector3(lscale.X, -lscale.Y, -lscale.Z);
                                scale = ((tScale*part.GetWorldRotation()));

                                vertexes[2] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                //vertexes[2].x = pos.X + vertexes[2].x;
                                //vertexes[2].y = pos.Y + vertexes[2].y;
                                //vertexes[2].z = pos.Z + vertexes[2].z;

                                FaceC[0] = vertexes[2];
                                FaceD[3] = vertexes[2];
                                FaceC[5] = vertexes[2];

                                tScale = new Vector3(lscale.X, lscale.Y, -lscale.Z);
                                scale = ((tScale*part.GetWorldRotation()));
                                vertexes[3] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                //vertexes[3].x = pos.X + vertexes[3].x;
                                // vertexes[3].y = pos.Y + vertexes[3].y;
                                // vertexes[3].z = pos.Z + vertexes[3].z;

                                FaceD[0] = vertexes[3];
                                FaceC[1] = vertexes[3];
                                FaceA[5] = vertexes[3];

                                tScale = new Vector3(-lscale.X, lscale.Y, lscale.Z);
                                scale = ((tScale*part.GetWorldRotation()));
                                vertexes[4] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                // vertexes[4].x = pos.X + vertexes[4].x;
                                // vertexes[4].y = pos.Y + vertexes[4].y;
                                // vertexes[4].z = pos.Z + vertexes[4].z;

                                FaceB[1] = vertexes[4];
                                FaceA[2] = vertexes[4];
                                FaceD[4] = vertexes[4];

                                tScale = new Vector3(-lscale.X, lscale.Y, -lscale.Z);
                                scale = ((tScale*part.GetWorldRotation()));
                                vertexes[5] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                // vertexes[5].x = pos.X + vertexes[5].x;
                                // vertexes[5].y = pos.Y + vertexes[5].y;
                                // vertexes[5].z = pos.Z + vertexes[5].z;

                                FaceD[1] = vertexes[5];
                                FaceC[2] = vertexes[5];
                                FaceB[5] = vertexes[5];

                                tScale = new Vector3(-lscale.X, -lscale.Y, lscale.Z);
                                scale = ((tScale*part.GetWorldRotation()));
                                vertexes[6] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                // vertexes[6].x = pos.X + vertexes[6].x;
                                // vertexes[6].y = pos.Y + vertexes[6].y;
                                // vertexes[6].z = pos.Z + vertexes[6].z;

                                FaceB[2] = vertexes[6];
                                FaceA[3] = vertexes[6];
                                FaceB[4] = vertexes[6];

                                tScale = new Vector3(-lscale.X, -lscale.Y, -lscale.Z);
                                scale = ((tScale*part.GetWorldRotation()));
                                vertexes[7] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                // vertexes[7].x = pos.X + vertexes[7].x;
                                // vertexes[7].y = pos.Y + vertexes[7].y;
                                // vertexes[7].z = pos.Z + vertexes[7].z;

                                FaceD[2] = vertexes[7];
                                FaceC[3] = vertexes[7];
                                FaceD[5] = vertexes[7];

                                #endregion

                                //int wy = 0;

                                //bool breakYN = false; // If we run into an error drawing, break out of the
                                // loop so we don't lag to death on error handling
                                DrawStruct ds = new DrawStruct {brush = new SolidBrush(mapdotspot)};
                                if (mapdot.RootChild.Shape.ProfileShape == ProfileShape.Circle)
                                {
                                    ds.dr = DrawRoutine.Ellipse;
                                    Vector3 Location = new Vector3(part.AbsolutePosition.X - (part.Scale.X/2),
                                                                   (256 -
                                                                    (part.AbsolutePosition.Y + (part.Scale.Y/2))),
                                                                   0);
                                    Location.X /= m_scene.RegionInfo.RegionSizeX/Constants.RegionSize;
                                    Location.Y /= m_scene.RegionInfo.RegionSizeY/Constants.RegionSize;
                                    Location = Location*part.GetWorldRotation();
                                    ds.rect = new Rectangle((int) Location.X, (int) Location.Y,
                                                            (int) Math.Abs(part.Shape.Scale.X),
                                                            (int) Math.Abs(part.Shape.Scale.Y));
                                }
                                else //if (mapdot.RootPart.Shape.ProfileShape == ProfileShape.Square)
                                {
                                    ds.dr = DrawRoutine.Rectangle;
                                    //ds.rect = new Rectangle(mapdrawstartX, (255 - mapdrawstartY), mapdrawendX - mapdrawstartX, mapdrawendY - mapdrawstartY);

                                    ds.trns = new face[FaceA.Length];

                                    for (int i = 0; i < FaceA.Length; i++)
                                    {
                                        Point[] working = new Point[5];
                                        working[0] = project(FaceA[i], axPos);
                                        working[1] = project(FaceB[i], axPos);
                                        working[2] = project(FaceD[i], axPos);
                                        working[3] = project(FaceC[i], axPos);
                                        working[4] = project(FaceA[i], axPos);

                                        face workingface = new face {pts = working};

                                        ds.trns[i] = workingface;
                                    }
                                }

                                if (!z_localIDs.Contains(part.LocalId))
                                {
                                    z_sort[part.LocalId] = ds;
                                    z_localIDs.Add(part.LocalId);
                                    z_sortheights.Add(pos.Z);
                                }
                            } // Object is within 256m Z of terrain
                        } // object is at least a meter wide
                    } // loop over group children
                } // foreach loop over entities

                float[] sortedZHeights = z_sortheights.ToArray();
                uint[] sortedlocalIds = z_localIDs.ToArray();

                // Sort prim by Z position
                Array.Sort(sortedZHeights, sortedlocalIds);

                Graphics g = Graphics.FromImage(mapbmp);

                for (int s = 0; s < sortedZHeights.Length; s++)
                {
                    if (z_sort.ContainsKey(sortedlocalIds[s]))
                    {
                        DrawStruct rectDrawStruct = z_sort[sortedlocalIds[s]];
                        if (rectDrawStruct.dr == DrawRoutine.Rectangle)
                        {
                            for (int r = 0; r < rectDrawStruct.trns.Length; r++)
                            {
                                g.FillPolygon(rectDrawStruct.brush, rectDrawStruct.trns[r].pts);
                            }
                        }
                        else if (rectDrawStruct.dr == DrawRoutine.Ellipse)
                        {
                            g.FillEllipse(rectDrawStruct.brush, rectDrawStruct.rect);
                        }
                        //g.FillRectangle(rectDrawStruct.brush , rectDrawStruct.rect);
                    }
                }
            } // lock entities objs

            //MainConsole.Instance.Info("[MAPTILE]: Generating Maptile Step 2: Done in " + (Environment.TickCount - tc) + " ms");
            return mapbmp;
        }

        private void ReadCacheMap()
        {
            if (!Directory.Exists(m_assetCacheDir))
                Directory.CreateDirectory(m_assetCacheDir);
            if (!Directory.Exists(Path.Combine(m_assetCacheDir, "mapTileTextureCache")))
                Directory.CreateDirectory(Path.Combine(m_assetCacheDir, "mapTileTextureCache"));

            FileStream stream =
                new FileStream(
                    Path.Combine(Path.Combine(m_assetCacheDir, "mapTileTextureCache"),
                                 m_scene.RegionInfo.RegionName + ".tc"), FileMode.OpenOrCreate);
            StreamReader m_streamReader = new StreamReader(stream);
            string file = m_streamReader.ReadToEnd();
            m_streamReader.Close();
            //Read file here
            if (file != "") //New file
            {
                bool loaded = DeserializeCache(file);
                if (!loaded)
                {
                    //Something went wrong, delete the file
                    try
                    {
                        File.Delete(Path.Combine(Path.Combine(m_assetCacheDir, "mapTileTextureCache"),
                                                 m_scene.RegionInfo.RegionName + ".tc"));
                    }
                    catch
                    {
                    }
                }
            }
        }

        private bool DeserializeCache(string file)
        {
            OSDMap map = OSDParser.DeserializeJson(file) as OSDMap;
            if (map == null)
                return false;

            foreach (KeyValuePair<string, OSD> kvp in map)
            {
                Color4 c = kvp.Value.AsColor4();
                UUID key = UUID.Parse(kvp.Key);
                if (!m_mapping.ContainsKey(key))
                    m_mapping.Add(key,
                                  Color.FromArgb((int) (c.A*255), (int) (c.R*255), (int) (c.G*255), (int) (c.B*255)));
            }

            return true;
        }

        private void SaveCache()
        {
            OSDMap map = SerializeCache();
            FileStream stream =
                new FileStream(
                    Path.Combine(Path.Combine(m_assetCacheDir, "mapTileTextureCache"),
                                 m_scene.RegionInfo.RegionName + ".tc"), FileMode.Create);
            StreamWriter writer = new StreamWriter(stream);
            writer.WriteLine(OSDParser.SerializeJsonString(map));
            writer.Close();
        }

        private OSDMap SerializeCache()
        {
            OSDMap map = new OSDMap();
            foreach (KeyValuePair<UUID, Color> kvp in m_mapping)
            {
                map.Add(kvp.Key.ToString(), new Color4(kvp.Value.R, kvp.Value.G, kvp.Value.B, kvp.Value.A));
            }
            return map;
        }

        private Color computeAverageColor(UUID textureID, Color defaultColor)
        {
            if (m_mapping == null)
            {
                m_mapping = new Dictionary<UUID, Color>();
                this.ReadCacheMap();
            }
            if (textureID == UUID.Zero) return defaultColor; // not set
            if (m_mapping.ContainsKey(textureID)) return m_mapping[textureID]; // one of the predefined textures

            Bitmap bmp = fetchTexture(textureID);
            Color color = bmp == null ? defaultColor : computeAverageColor(bmp);
            // store it for future reference
            m_mapping[textureID] = color;

            return color;
        }

        private Bitmap fetchTexture(UUID id)
        {
            byte[] asset = m_scene.AssetService.GetData(id.ToString());
            //MainConsole.Instance.DebugFormat("Fetched texture {0}, found: {1}", id, asset != null);
            if (asset == null) return null;

            try
            {
                Image i = m_scene.RequestModuleInterface<IJ2KDecoder>().DecodeToImage(asset);
                if (i != null)
                    return new Bitmap(i);
            }
            catch (DllNotFoundException)
            {
                MainConsole.Instance.ErrorFormat(
                    "[TexturedMapTileRenderer]: OpenJpeg is not installed correctly on this system.   Asset Data is emtpy for {0}",
                    id);
            }
            catch (IndexOutOfRangeException)
            {
                MainConsole.Instance.ErrorFormat(
                    "[TexturedMapTileRenderer]: OpenJpeg was unable to encode this.   Asset Data is emtpy for {0}", id);
            }
            catch (Exception)
            {
                MainConsole.Instance.ErrorFormat(
                    "[TexturedMapTileRenderer]: OpenJpeg was unable to encode this.   Asset Data is emtpy for {0}", id);
            }
            return null;
        }

        // Compute the average color of a texture.
        private Color computeAverageColor(Bitmap bmp)
        {
            FastBitmap unsafeBMP = new FastBitmap(bmp);
            // we have 256 x 256 pixel, each with 256 possible color-values per
            // color-channel, so 2^24 is the maximum value we can get, adding everything.
            unsafeBMP.LockBitmap();
            int r = 0;
            int g = 0;
            int b = 0;
            int pixels = 0;
            for (int y = 0; y < bmp.Height; y += 10)
            {
                for (int x = 0; x < bmp.Width; x += 10)
                {
                    Color pixel = unsafeBMP.GetPixel(x, y);
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    pixels++;
                }
            }

            unsafeBMP.UnlockBitmap();

            return Color.FromArgb(r/pixels, g/pixels, b/pixels);
        }

        private Point project(Vector3 point3d, Vector3 originpos)
        {
            Point returnpt = new Point
                                 {X = (int) point3d.X, Y = (int) ((m_scene.RegionInfo.RegionSizeY - 1) - point3d.Y)};
            //originpos = point3d;
            //int d = (int)(256f / 1.5f);

            //Vector3 topos = new Vector3(0, 0, 0);
            // float z = -point3d.z - topos.z;

            //(int)((topos.x - point3d.x) / z * d);
            //(int)(255 - (((topos.y - point3d.y) / z * d)));
            returnpt.X /= m_scene.RegionInfo.RegionSizeX/Constants.RegionSize;
            returnpt.Y /= m_scene.RegionInfo.RegionSizeY/Constants.RegionSize;
            return returnpt;
        }

        /// <summary>
        /// Clears the web cached images.
        /// </summary>
        /// <param name="regionX">Region x.</param>
        /// <param name="regionY">Region y.</param>
        public void ClearWebCachedImages(int regionX, int regionY, int sizeX, int sizeY)
        {
            if (m_assetMapCacheDir == "")
                return;

            // easier to use vars...
            var regionXPos = regionX / Constants.RegionSize;
            var regionYPos = regionY / Constants.RegionSize;
            var xLimit = (regionX + sizeX) / Constants.RegionSize;
            var yLimit = (regionY + sizeY) / Constants.RegionSize;

            for (int maplayer = 1; maplayer < 9; maplayer++)
            {
                for (int xPos = regionXPos; xPos < xLimit; xPos++)
                {
                    for (int yPos = regionYPos; yPos < yLimit; yPos++)
                    {
                        string name = string.Format ("map-{0}-{1}-{2}-objects.jpg", maplayer, xPos, yPos);
                        string fullPath = Path.Combine (m_assetMapCacheDir, name);

                        if (File.Exists (fullPath))
                            File.Delete (fullPath);
                    }
                }
            }
        }

        public void ClearWorldviewCachedImages(UUID regionID)
        {
            if (m_assetWorldviewCacheDir == "")
                return;

            string name = string.Format ("wv-{0}.jpg", regionID);
            string fullPath = Path.Combine (m_assetWorldviewCacheDir, name);

            if (File.Exists (fullPath))
                File.Delete (fullPath);

        }

        public void Dispose()
        {
            UpdateMapImage.Close();
            UpdateOnlineStatus.Close();
        }
    }
}