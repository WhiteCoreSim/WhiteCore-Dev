/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Aurora-Sim Project nor the
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
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.WorldView
{
    public class WorldViewModule : INonSharedRegionModule
    {

        bool m_Enabled = true;
        IMapImageGenerator m_Generator;
        string m_assetCacheDir = "";
        string m_worldviewCacheDir;
        bool m_cacheEnabled = true;
        float m_cacheExpires = 24;
        ISimulationBase simulationBase;

        public void Initialise (IConfigSource config)
        {

            IConfig moduleConfig = config.Configs ["WorldViewModule"];
            if (moduleConfig != null) {
                // enabled by default but allow disabling
                m_Enabled = moduleConfig.GetBoolean ("Enabled", m_Enabled);
                m_cacheEnabled = moduleConfig.GetBoolean ("EnableCache", true);
                m_cacheExpires = moduleConfig.GetFloat ("CacheExpires", m_cacheExpires);
            }

            if (m_Enabled) {
                MainConsole.Instance.Commands.AddCommand (
                    "save worldview",
                    "save worldview [filename]< --fov degrees >",
                    "Save a view of the region to a file",
                    HandleSaveWorldview, true, true);

                MainConsole.Instance.Commands.AddCommand (
                    "save worldmaptile",
                    "save worldmaptile [filename] < --size pixels >",
                    "Save a maptile view of the region to a file",
                    HandleSaveWorldTile, true, true);


                if (m_cacheEnabled) {
                    m_assetCacheDir = config.Configs ["AssetCache"].GetString ("CacheDirectory", m_assetCacheDir);
                }

            }
        }

        public void AddRegion (IScene scene)
        {
        }

        public void RegionLoaded (IScene scene)
        {
            if (!m_Enabled)
                return;

            m_Generator = scene.RequestModuleInterface<IMapImageGenerator> ();
            if (m_Generator == null) {
                m_Enabled = false;
                return;
            }

            simulationBase = scene.RequestModuleInterface<ISimulationBase> ();
            if (simulationBase != null) {
                // verify cache path
                if (m_cacheEnabled) {
                    if (m_assetCacheDir == "") {
                        var defpath = simulationBase.DefaultDataPath;
                        m_assetCacheDir = Path.Combine (defpath, Constants.DEFAULT_ASSETCACHE_DIR);
                    }
                    CreateCacheDirectories (m_assetCacheDir);
                }

                IHttpServer server = simulationBase.GetHttpServer (0);
                server.AddStreamHandler (new WorldViewRequestHandler (this,
                        scene.RegionInfo.RegionID.ToString ()));
                MainConsole.Instance.Info ("[World view]: Configured and enabled for " + scene.RegionInfo.RegionName);
                MainConsole.Instance.Info ("[World view]: RegionID " + scene.RegionInfo.RegionID);
            }
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public string Name {
            get { return "WorldViewModule"; }
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public void Close ()
        {
        }

        public bool CacheEnabled {
            get { return m_cacheEnabled; }
        }

        public float CacheExpires {
            get { return m_cacheExpires; }
        }

        public string CacheDir {
            get { return m_worldviewCacheDir; }
        }

        void CreateCacheDirectories (string cacheDir)
        {
            if (!Directory.Exists (cacheDir))
                Directory.CreateDirectory (cacheDir);

            m_worldviewCacheDir = cacheDir + "/Worldview";
            if (!Directory.Exists (m_worldviewCacheDir))
                Directory.CreateDirectory (m_worldviewCacheDir);
        }

        public byte [] GenerateWorldView (Vector3 pos, Vector3 rot, float fov,
                int width, int height, bool usetex)
        {
            if (!m_Enabled)
                return new byte [0];

            Bitmap bmp = m_Generator.CreateViewImage (pos, rot, fov, width, height, usetex);

            MemoryStream str = new MemoryStream ();

            bmp.Save (str, ImageFormat.Jpeg);

            return str.ToArray ();
        }

        public void SaveRegionWorldView (IScene scene, string fileName, float fieldOfView)
        {
            m_Generator = scene.RequestModuleInterface<IMapImageGenerator> ();
            if (m_Generator == null)
                return;

            // set some basic defaults
            Vector3 camPos = new Vector3 ();
            //camPos.Y = scene.RegionInfo.RegionSizeY / 2 - 0.5f;
            //camPos.X = scene.RegionInfo.RegionSizeX / 2 - 0.5f;
            //camPos.Z = 221.7025033688163f);

            camPos.X = 1.25f;
            camPos.Y = 1.25f;
            camPos.Z = 61.0f;

            Vector3 camDir = new Vector3 ();
            camDir.X = .687462f;                        // -1  -< y/x > 1
            camDir.Y = .687462f;
            camDir.Z = -0.23536f;                       // -1 (up) < Z > (down) 1

            float fov = 89f;                            // degrees
            if (fieldOfView > 0)
                fov = fieldOfView;

            int width = 1280;
            int height = 720;

            //byte[] jpeg = ExportWorldView(camPos, camDir, fov, width, height, true); 
            Bitmap bmp = m_Generator.CreateViewImage (camPos, camDir, fov, width, height, true);
            if (bmp == null)
                return;

            MemoryStream str = new MemoryStream ();
            bmp.Save (str, ImageFormat.Jpeg);
            byte [] jpeg = str.ToArray ();

            // save image
            var savePath = fileName;
            if (string.IsNullOrEmpty (fileName)) {
                fileName = scene.RegionInfo.RegionName + ".jpg";
                savePath = PathHelpers.VerifyWriteFile (fileName, ".jpg", simulationBase.DefaultDataPath + "/Worldview", true);
            }
            File.WriteAllBytes (savePath, jpeg);

            bmp.Dispose ();
        }

        public void SaveRegionWorldMapTile (IScene scene, string fileName, int size)
        {
            // if different formats etc are needed
            //var imgEncoder = GetEncoderInfo ("image/jpeg");
            //var encQuality = Encoder.Quality;
            //var encParms = new EncoderParameters (1);
            //encParms.Param[0] = new EncoderParameter (encQuality, 50L);

            m_Generator = scene.RequestModuleInterface<IMapImageGenerator> ();
            if (m_Generator == null)
                return;

            Bitmap bmp = m_Generator.CreateViewTileImage (size);
            if (bmp == null)
                return;

            var regionName = scene.RegionInfo.RegionName;
            Bitmap outbmp = ResizeBitmap (bmp, size, size, regionName);
            MemoryStream str = new MemoryStream ();
            outbmp.Save (str, ImageFormat.Jpeg);            // default quality is about 75
            //outbmp.Save(str, imgEncoder, encParms);       // if encoder parms is used
            byte [] jpeg = str.ToArray ();

            // save image
            var savePath = fileName;
            if (string.IsNullOrEmpty (fileName)) {
                fileName = regionName + "_maptile.jpg";
                savePath = PathHelpers.VerifyWriteFile (fileName, ".jpg", simulationBase.DefaultDataPath + "/Worldview", true);
            }
            File.WriteAllBytes (savePath, jpeg);

            bmp.Dispose ();
            outbmp.Dispose ();
        }


        protected void HandleSaveWorldview (IScene scene, string [] cmdparams)
        {
            string fileName;
            float fieldOfView = 0f;

            // check for switch options
            var cmds = new List<string> ();
            for (int i = 2; i < cmdparams.Length;) {
                if (cmdparams [i].StartsWith ("--fov", StringComparison.Ordinal)) {
                    fieldOfView = float.Parse (cmdparams [i + 1]);
                    i += 2;
                } else {
                    cmds.Add (cmdparams [i]);
                    i++;
                }
            }

            if (cmds.Count > 0)
                fileName = cmds [0];
            else {
                fileName = scene.RegionInfo.RegionName;
                fileName = MainConsole.Instance.Prompt (" Worldview filename", fileName);
                if (fileName == "")
                    return;
            }

            //some file sanity checks
            var savePath = PathHelpers.VerifyWriteFile (fileName, ".jpg", simulationBase.DefaultDataPath + "/Worldview", true);

            MainConsole.Instance.InfoFormat (
                "[World view]: Saving worldview for {0} to {1}", scene.RegionInfo.RegionName, savePath);

            SaveRegionWorldView (scene, savePath, fieldOfView);

        }

        protected void HandleSaveWorldTile (IScene scene, string [] cmdparams)
        {
            string fileName = "";
            int size = scene.RegionInfo.RegionSizeX;
            if (scene.RegionInfo.RegionSizeY > size)
                size = scene.RegionInfo.RegionSizeY;

            // check for switch options
            var cmds = new List<string> ();
            for (int i = 2; i < cmdparams.Length;) {
                if (cmdparams [i].StartsWith ("--size", StringComparison.Ordinal)) {
                    size = int.Parse (cmdparams [i + 1]);
                    if (size > 4096) {
                        MainConsole.Instance.Warn ("[World view]: You may experience problems generating large images.");
                        size = int.Parse (MainConsole.Instance.Prompt (" World maptile size", "4096"));
                    }
                    i += 2;
                } else {
                    cmds.Add (cmdparams [i]);
                    i++;
                }
            }

            if (cmds.Count > 0)
                fileName = cmds [0];
            else {
                fileName = scene.RegionInfo.RegionName;
                fileName = MainConsole.Instance.Prompt (" World maptile filename", fileName);
                if (fileName == "")
                    return;
            }

            //some file sanity checks
            var savePath = PathHelpers.VerifyWriteFile (fileName + "_maptile", ".jpg", simulationBase.DefaultDataPath + "/Worldview", true);

            MainConsole.Instance.InfoFormat (
                "[World view]: Saving world maptile for {0} to {1}", scene.RegionInfo.RegionName, savePath);

            SaveRegionWorldMapTile (scene, savePath, size);

        }

        /*
         private void ExportArchiveImage(UUID imageUUID, string archiveName, string filePath)
        {
            byte[] jpeg = new byte[0];

            using (MemoryStream imgstream = new MemoryStream())
            {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                // non-async because we know we have the asset immediately.
                byte[] imageAsset = AssetService.GetData(imageUUID.ToString());

                if (imageAsset != null)
                {
                    // Decode image to System.Drawing.Image
                    Image image = null;
                    ManagedImage managedImage;
                    if (OpenJPEG.DecodeToImage(imageAsset, out managedImage, out image))
                    {
                        // Save to bitmap
                        using (Bitmap texture = ResizeBitmap(image, 256, 256, archiveName))
                        {
                            EncoderParameters myEncoderParameters = new EncoderParameters();
                            myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,
                                75L);

                            // Save bitmap to stream
                            texture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);

                            // Write the stream to a byte array for output
                            jpeg = imgstream.ToArray();

                            // save image
                            string fileName = archiveName + ".jpg";
                            string fullPath = Path.Combine(filePath, fileName);
                            File.WriteAllBytes(fullPath, jpeg);

                        }
                        image.Dispose();
                    }
                }
            }
        }

        // From MSDN
         static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
           ImageCodecInfo[] encoders;
            try {
                encoders = ImageCodecInfo.GetImageEncoders ();
            } catch {
                return null;
            }

            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
        */

        Bitmap ResizeBitmap (Image b, int nWidth, int nHeight, string name)
        {
            Bitmap newsize = new Bitmap (nWidth, nHeight);
            Graphics temp = Graphics.FromImage (newsize);

            // resize...
            temp.DrawImage (b, 0, 0, nWidth, nHeight);
            temp.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // overlay if needed
            if (name != "") {
                int fontScale = (nHeight / Constants.RegionSize);
                temp.DrawString (name, new Font ("Arial", 8 * fontScale, FontStyle.Regular),
                    new SolidBrush (Color.FromArgb (200, 255, 255, 90)), new Point (5, nHeight - (15 * fontScale)));     // bottom left
            }

            temp.Dispose ();

            return newsize;
        }
    }
}
