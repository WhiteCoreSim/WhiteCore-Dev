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
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;
using ZLib.Net;

namespace WhiteCore.Modules.Caps
{
    public class RenderMaterials : INonSharedRegionModule
    {
        IScene m_scene;
        bool m_enabled;
        public Dictionary<UUID, OSDMap> m_knownMaterials = new Dictionary<UUID, OSDMap> ();

        #region INonSharedRegionModule Members

        public void Initialise (IConfigSource source)
        {
            var cfg = source.Configs ["MaterialsModule"];
            if (cfg != null)
                m_enabled = cfg.GetBoolean ("Enabled", false);
            if (!m_enabled)
                return;

            MainConsole.Instance.Info ("[Materials]: Initializing module");
        }

        public void AddRegion (IScene scene)
        {
            if (!m_enabled)
                return;

            m_scene = scene;
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void RemoveRegion (IScene scene)
        {
            if (!m_enabled)
                return;

            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene = null;
        }

        public void RegionLoaded (IScene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close ()
        {
        }

        public string Name
        {
            get { return "RenderMaterials"; }
        }

        #endregion

        public OSDMap RegisterCaps (UUID agentID, IHttpServer server)
        {
            OSDMap retVal = new OSDMap ();
            retVal ["RenderMaterials"] = CapsUtil.CreateCAPS ("RenderMaterials", "");
            server.AddStreamHandler (new GenericStreamHandler ("POST", retVal ["RenderMaterials"],
                RenderMaterialsPostCap));
            server.AddStreamHandler (new GenericStreamHandler ("GET", retVal ["RenderMaterials"],
                RenderMaterialsGetCap));
            server.AddStreamHandler (new GenericStreamHandler ("PUT", retVal ["RenderMaterials"],
                RenderMaterialsPostCap));
            return retVal;
        }

        public byte[] RenderMaterialsPostCap (string path, Stream request,
                                             OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            MainConsole.Instance.Debug ("[Materials]: POST cap handler");

            OSDMap req = (OSDMap)OSDParser.DeserializeLLSDXml (request);
            OSDMap resp = new OSDMap ();

            OSDMap materialsFromViewer = null;

            OSDArray respArr = new OSDArray ();

            if (req.ContainsKey ("Zipped"))
            {
                OSD osd;

                byte[] inBytes = req ["Zipped"].AsBinary ();

                try
                {
                    osd = ZDecompressBytesToOsd (inBytes);

                    if (osd != null)
                    {
                        if (osd is OSDArray) // assume array of MaterialIDs designating requested material entries
                        {
                            foreach (OSD elem in (OSDArray)osd)
                            {

                                try
                                {
                                    UUID id = new UUID (elem.AsBinary (), 0);
                                    AssetBase materialAsset = null;
                                    if (m_knownMaterials.ContainsKey (id))
                                    {
                                        MainConsole.Instance.Info ("[Materials]: request for known material ID: " + id);
                                        OSDMap matMap = new OSDMap ();
                                        matMap ["ID"] = elem.AsBinary ();

                                        matMap ["Material"] = m_knownMaterials [id];
                                        respArr.Add (matMap);
                                    } else if ((materialAsset = m_scene.AssetService.Get (id.ToString ())) != null)
                                    {
                                        MainConsole.Instance.Info ("[Materials]: request for stored material ID: " + id);
                                        OSDMap matMap = new OSDMap ();
                                        matMap ["ID"] = elem.AsBinary ();

                                        matMap ["Material"] = OSDParser.DeserializeJson (Encoding.UTF8.GetString (materialAsset.Data));
                                        respArr.Add (matMap);
                                    } else
                                        MainConsole.Instance.Info ("[Materials]: request for UNKNOWN material ID: " + id);
                                } catch (Exception)
                                {
                                    // report something here?
                                    continue;
                                }
                            }
                        } else if (osd is OSDMap) // request to assign a material
                        {
                            materialsFromViewer = osd as OSDMap;

                            if (materialsFromViewer.ContainsKey ("FullMaterialsPerFace"))
                            {
                                OSD matsOsd = materialsFromViewer ["FullMaterialsPerFace"];
                                if (matsOsd is OSDArray)
                                {
                                    OSDArray matsArr = matsOsd as OSDArray;

                                    try
                                    {
                                        foreach (OSDMap matsMap in matsArr)
                                        {
                                            // MainConsole.Instance.Debug("[Materials]: processing matsMap: " + OSDParser.SerializeJsonString(matsMap));

                                            uint matLocalID = 0;
                                            try
                                            {
                                                matLocalID = matsMap ["ID"].AsUInteger ();
                                            } catch (Exception e)
                                            {
                                                MainConsole.Instance.Warn ("[Materials]: cannot decode \"ID\" from matsMap: " + e.Message);
                                            }
                                            // MainConsole.Instance.Debug("[Materials]: matLocalId: " + matLocalID);


                                            OSDMap mat = null;
                                            if (matsMap.ContainsKey ("Material"))
                                            {
                                                try
                                                {
                                                    mat = matsMap ["Material"] as OSDMap; 

                                                } catch (Exception e)
                                                { 
                                                    MainConsole.Instance.Warn ("[MaterialsDemoModule]: cannot decode \"Material\" from matsMap: " + e.Message); 
                                                    continue;
                                                }
                                            }

                                            if (mat == null)
                                                continue;

                                            // MainConsole.Instance.Debug("[Materials]: mat: " + OSDParser.SerializeJsonString(mat));
                                            UUID id = HashOsd (mat);
                                            m_knownMaterials [id] = mat;

                                            var sop = m_scene.GetSceneObjectPart (matLocalID);
                                            if (sop == null)
                                                MainConsole.Instance.Debug ("[Materials]: null SOP for localId: " + matLocalID);
                                            else
                                            {
                                                //var te = sop.Shape.Textures;
                                                var te = new Primitive.TextureEntry (sop.Shape.TextureEntry, 0, sop.Shape.TextureEntry.Length);

                                                if (te == null)
                                                {
                                                    MainConsole.Instance.Debug ("[Materials]: null TextureEntry for localId: " + matLocalID);
                                                } else
                                                {
                                                    int face = -1;

                                                    if (matsMap.ContainsKey ("Face"))
                                                    {
                                                        face = matsMap ["Face"].AsInteger ();
                                                        if (te.FaceTextures == null) // && face == 0)
                                                        {
                                                            if (te.DefaultTexture == null)
                                                                MainConsole.Instance.Debug ("[Materials]: te.DefaultTexture is null");
                                                            else
                                                            {
//## FixMe ##
// comparison always results in 'False'                                   if (te.DefaultTexture.MaterialID == null)
//                                                                    MainConsole.Instance.Debug("[MaterialsDemoModule]: te.DefaultTexture.MaterialID is null");
//                                                                else
//                                                                {
                                                                te.DefaultTexture.MaterialID = id;
//                                                                }
                                                            }
                                                        } else
                                                        {
                                                            if (te.FaceTextures.Length >= face - 1)
                                                            {
                                                                if (te.FaceTextures [face] == null)
                                                                    te.DefaultTexture.MaterialID = id;
                                                                else
                                                                    te.FaceTextures [face].MaterialID = id;
                                                            }
                                                        }
                                                    } else
                                                    {
                                                        if (te.DefaultTexture != null)
                                                            te.DefaultTexture.MaterialID = id;
                                                    }

                                                    MainConsole.Instance.Debug ("[Materials]: setting material ID for face " + face + " to " + id);

                                                    //we cant use sop.UpdateTextureEntry(te); because it filters so do it manually

                                                    if (sop.ParentEntity != null)
                                                    {
                                                        sop.Shape.TextureEntry = te.GetBytes ();
                                                        sop.TriggerScriptChangedEvent (Changed.TEXTURE);
                                                        sop.ParentEntity.HasGroupChanged = true;

                                                        sop.ScheduleUpdate (PrimUpdateFlags.FullUpdate);

                                                        AssetBase asset = new AssetBase (id, "RenderMaterial",
                                                                              AssetType.Texture, sop.OwnerID) {
                                                            Data = Encoding.UTF8.GetBytes (
                                                                OSDParser.SerializeJsonString (mat))
                                                        };
                                                        m_scene.AssetService.Store (asset);

                                                        StoreMaterialsForPart (sop);
                                                    }
                                                }
                                            }
                                        }
                                    } catch (Exception e)
                                    {
                                        MainConsole.Instance.Warn ("[Materials]: exception processing received material: " + e);
                                    }
                                }
                            }
                        }
                    }

                } catch (Exception e)
                {
                    MainConsole.Instance.Warn ("[Materials]: exception decoding zipped CAP payload: " + e);
                    //return "";
                }
                MainConsole.Instance.Debug ("[Materials]: knownMaterials.Count: " + m_knownMaterials.Count);
            }


            resp ["Zipped"] = ZCompressOSD (respArr, false);
            string response = OSDParser.SerializeLLSDXmlString (resp);

            //MainConsole.Instance.Debug("[Materials]: cap request: " + request);
            MainConsole.Instance.Debug ("[Materials]: cap request (zipped portion): " + ZippedOsdBytesToString (req ["Zipped"].AsBinary ()));
            MainConsole.Instance.Debug ("[Materials]: cap response: " + response);
            return OSDParser.SerializeLLSDBinary (resp);
        }

        void StoreMaterialsForPart (ISceneChildEntity part)
        {
            try
            {
                if (part == null || part.Shape == null)
                    return;

                Dictionary<UUID, OSDMap> mats = new Dictionary<UUID, OSDMap> ();

                Primitive.TextureEntry te = part.Shape.Textures;

                if (te.DefaultTexture != null)
                {
                    if (m_knownMaterials.ContainsKey (te.DefaultTexture.MaterialID))
                        mats [te.DefaultTexture.MaterialID] = m_knownMaterials [te.DefaultTexture.MaterialID];
                }

                if (te.FaceTextures != null)
                {
                    foreach (var face in te.FaceTextures)
                    {
                        if (face != null)
                        {
                            if (m_knownMaterials.ContainsKey (face.MaterialID))
                                mats [face.MaterialID] = m_knownMaterials [face.MaterialID];
                        }
                    }
                }
                if (mats.Count == 0)
                    return;

                OSDArray matsArr = new OSDArray ();
                foreach (KeyValuePair<UUID, OSDMap> kvp in mats)
                {
                    OSDMap matOsd = new OSDMap ();
                    matOsd ["ID"] = OSD.FromUUID (kvp.Key);
                    matOsd ["Material"] = kvp.Value;
                    matsArr.Add (matOsd);
                }

                lock (part.RenderMaterials)
                    part.RenderMaterials = matsArr;
            } catch (Exception e)
            {
                MainConsole.Instance.Warn ("[Materials]: exception in StoreMaterialsForPart(): " + e);
            }
        }


        public byte[] RenderMaterialsGetCap (string path, Stream request,
                                            OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            MainConsole.Instance.Debug ("[Materials]: GET cap handler");

            OSDMap resp = new OSDMap ();


            int matsCount = 0;

            OSDArray allOsd = new OSDArray ();

            foreach (KeyValuePair<UUID, OSDMap> kvp in m_knownMaterials)
            {
                OSDMap matMap = new OSDMap ();

                matMap ["ID"] = OSD.FromBinary (kvp.Key.GetBytes ());

                matMap ["Material"] = kvp.Value;
                allOsd.Add (matMap);
                matsCount++;
            }


            resp ["Zipped"] = ZCompressOSD (allOsd, false);
            MainConsole.Instance.Debug ("[Materials]: matsCount: " + matsCount);

            return OSDParser.SerializeLLSDBinary (resp);
        }

        static string ZippedOsdBytesToString (byte[] bytes)
        {
            try
            {
                return OSDParser.SerializeJsonString (ZDecompressBytesToOsd (bytes));
            } catch (Exception e)
            {
                return "ZippedOsdBytesToString caught an exception: " + e;
            }
        }

        /// <summary>
        /// computes a UUID by hashing a OSD object
        /// </summary>
        /// <param name="osd"></param>
        /// <returns></returns>
        static UUID HashOsd (OSD osd)
        {
            using (var md5 = MD5.Create ())
            using (MemoryStream ms = new MemoryStream (OSDParser.SerializeLLSDBinary (osd, false)))
                return new UUID (md5.ComputeHash (ms), 0);
        }

        /// <summary>
        /// Compress an OSD.
        /// </summary>
        /// <returns>The compressed OSD</returns>
        /// <param name="inOsd">In osd.</param>
        /// <param name="useHeader">If set to <c>true</c> use header.</param>
        public static OSD ZCompressOSD (OSD inOsd, bool useHeader)
        {
            OSD osd;

            using (MemoryStream msSinkCompressed = new MemoryStream ())
            {
                using (ZOutputStream zOut = new ZOutputStream (msSinkCompressed, zlibConst.Z_DEFAULT_COMPRESSION))
                {
                    CopyStream (new MemoryStream (OSDParser.SerializeLLSDBinary (inOsd, useHeader)), zOut);
                    zOut.finish ();

                    osd = OSD.FromBinary (msSinkCompressed.ToArray ());
                }
            }

            return osd;
        }

        /// <summary>
        /// Decompress bytes to osd.
        /// </summary>
        /// <returns>The decompressed osd.</returns>
        /// <param name="input">Input.</param>
        public static OSD ZDecompressBytesToOsd (byte[] input)
        {
            OSD osd;

            using (MemoryStream msSinkUnCompressed = new MemoryStream ())
            {
                using (ZOutputStream zOut = new ZOutputStream (msSinkUnCompressed))
                {
                    using (Stream inMs = new MemoryStream (input))
                    {
                        CopyStream (inMs, zOut);
                        zOut.finish ();

                        osd = OSDParser.DeserializeLLSDBinary (msSinkUnCompressed.ToArray ());
                    }
                }
            }

            return osd;
        }
 
        static void CopyStream (Stream input, Stream output)
        {
            byte[] buffer = new byte[2000];
            int length;
            while ((length = input.Read (buffer, 0, 2000)) > 0)
                output.Write (buffer, 0, length);

            output.Flush ();
        }
    }
}
