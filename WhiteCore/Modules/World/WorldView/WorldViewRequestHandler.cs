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
using System.IO;
using OpenMetaverse;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.ConsoleFramework;

namespace WhiteCore.Modules.WorldView
{
    public class WorldViewRequestHandler : BaseRequestHandler
    {

        protected WorldViewModule m_WorldViewModule;
        protected object m_RequestLock = new object();

        public WorldViewRequestHandler(WorldViewModule fmodule, string rid)
                : base("GET", "/worldview/" + rid)
        {
            m_WorldViewModule = fmodule;
        }

        public override byte[] Handle(string path, Stream request,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            httpResponse.ContentType = "image/jpeg";

            //Remove the /worldview/
            string uri = httpRequest.RawUrl.Remove(0, 11);
            byte[] jpeg = FindCachedWorldViewImage(uri);
            if (jpeg.Length != 0)
            {
                return jpeg;
            }

            // image not in the cache...
            try
            {
                lock (m_RequestLock)
                {
                    var requestData = new Dictionary<string, object>();
                    foreach (string name in httpRequest.QueryString)
                        requestData[name] = httpRequest.QueryString[name];

                    if (requestData.Count > 1)
                    {
                        // we have specific parameters
                        var worldviewImage = SendWorldView(requestData);
                        if (worldviewImage != null)
                            SaveCachedImage(uri,worldviewImage);

                        return worldviewImage;
                    }

                    // create and return a default image
                    var stdWworldview = GetWorldView();
                    if (stdWworldview != null)
                        SaveCachedImage(uri,stdWworldview);

                    return stdWworldview;

                }
            }
            catch (Exception e)
            {
                MainConsole.Instance.Debug("[World view]: Exception: " + e);
            }

            return new byte [0];
        }

        /// <summary>
        /// Generates a world view from the supplied parameters.
        /// </summary>
        /// <returns>The world view.</returns>
        /// <param name="request">parameters.</param>
        public byte [] SendWorldView(Dictionary<string, object> request)
        {
            float posX;
            float posY;
            float posZ;
            float rotX;
            float rotY;
            float rotZ;
            float fov;
            int width;
            int height;
            bool usetex;

            if (!request.ContainsKey("posX"))
                return new byte [0];
            if (!request.ContainsKey("posY"))
                return new byte [0];
            if (!request.ContainsKey("posZ"))
                return new byte [0];
            if (!request.ContainsKey("rotX"))
                return new byte [0];
            if (!request.ContainsKey("rotY"))
                return new byte [0];
            if (!request.ContainsKey("rotZ"))
                return new byte [0];
            if (!request.ContainsKey("fov"))
                return new byte [0];
            if (!request.ContainsKey("width"))
                return new byte [0];
            if (!request.ContainsKey("height"))
                return new byte [0];
            if (!request.ContainsKey("usetex"))
                return new byte [0];

            try
            {
                posX = Convert.ToSingle(request["posX"]);
                posY = Convert.ToSingle(request["posY"]);
                posZ = Convert.ToSingle(request["posZ"]);
                rotX = Convert.ToSingle(request["rotX"]);
                rotY = Convert.ToSingle(request["rotY"]);
                rotZ = Convert.ToSingle(request["rotZ"]);
                fov = Convert.ToSingle(request["fov"]);
                width = Convert.ToInt32(request["width"]);
                height = Convert.ToInt32(request["height"]);
                usetex = Convert.ToBoolean(request["usetex"]);
            }
            catch
            {
                return new byte [0];
            }

            var pos = new Vector3(posX, posY, posZ);
            var rot = new Vector3(rotX, rotY, rotZ);

            return m_WorldViewModule.GenerateWorldView(pos, rot, fov, width, height, usetex);
        }


        /// <summary>
        /// Generates a standard world view.
        /// </summary>
        /// <returns>The world view.</returns>
        public byte [] GetWorldView()
        {

            // set some basic defaults
            Vector3 camPos = new Vector3 ();

            // this is the basic topdown view used for a map tile
            //camPos.Y = scene.RegionInfo.RegionSizeY / 2 - 0.5f;
            //camPos.X = scene.RegionInfo.RegionSizeX / 2 - 0.5f;
            //camPos.Z = 221.7025033688163f);

            camPos.X = 80.25f;
            camPos.Y = 75.25f;
            camPos.Z = 61.0f;

            Vector3 camDir = new Vector3 ();
            camDir.X = .687462f;                        // -1  -< y/x > 1
            camDir.Y = .687462f;
            camDir.Z = -0.23536f;                       // -1 (up) < Z > (down) 1

            float fov = 89f;                            // degrees

            int width = 256;           
            int height = 256;  

            return m_WorldViewModule.GenerateWorldView(camPos, camDir, fov, width, height, true);
        }


        /// <summary>
        /// Finds the cached world view image.
        /// </summary>
        /// <returns>The cached world view image.</returns>
        /// <param name="name">Name.</param>
        byte[] FindCachedWorldViewImage(string name)
        {
            if (!m_WorldViewModule.CacheEnabled)
                return new byte[0];
            string cacheDir = m_WorldViewModule.CacheDir;
            string cacheFile = cacheDir + "/wv-" + name + ".jpg";
            if (File.Exists(cacheFile))
            {
                //Make sure the time is ok
                if (DateTime.Now < File.GetLastWriteTime(cacheFile).AddHours(m_WorldViewModule.CacheExpires))
                    return File.ReadAllBytes(cacheFile);
            }
            return new byte[0];
        }

        /// <summary>
        /// Saves the cached image.
        /// </summary>
        /// <param name="regionUri">Region URI.</param>
        /// <param name="data">Data.</param>
        void SaveCachedImage(string regionUri, byte[] data)
        {
            if (!m_WorldViewModule.CacheEnabled)
                return;

            string name = string.Format("wv-{0}.jpg", regionUri);
            string cacheDir = m_WorldViewModule.CacheDir;
            string cacheFile = cacheDir + "/" + name;
            File.WriteAllBytes(cacheFile, data);
        }


    }
}
