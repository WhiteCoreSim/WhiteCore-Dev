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

//#define SPAM

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.SceneInfo;
using ZLibNet;

#if DEBUGING
using PrimMesher;
#else
using WhiteCore.Physics.PrimMesher;
#endif

namespace WhiteCore.Physics.Meshing
{
    public class MeshmerizerPlugin : IMeshingPlugin
    {
        #region IMeshingPlugin Members

        public string GetName()
        {
            return "Meshmerizer";
        }

        public IMesher GetMesher(IConfigSource config, IRegistryCore registry)
        {
            return new Meshmerizer(config, registry);
        }

        #endregion
    }

    public class Meshmerizer : IMesher
    {
        // Setting baseDir to a path will enable the dumping of raw files
        // raw files can be imported by blender so a visual inspection of the results can be done
        #if SPAM
            const string baseDir = "rawFiles";
        #endif

        // detailed debugging info
        bool debugDetail = false;
        readonly bool cacheSculptMaps = true;
        bool cacheSculptAlphaMaps = true;

        readonly string decodedSculptMapPath;
        readonly bool UseMeshesPhysicsMesh;

        // prims with all dimensions smaller than this will have a bounding box mesh
        float minSizeForComplexMesh = 0.2f;

        IJ2KDecoder m_j2kDecoder;
        List<List<Vector3>> mConvexHulls;
        List<Vector3> mBoundingHull;

        // Mesh cache. Static so it can be shared across instances of this class
        static Dictionary<ulong, Mesh> m_uniqueMeshes = new Dictionary<ulong, Mesh>();

        public Meshmerizer(IConfigSource config, IRegistryCore registry)
        {
            IConfig start_config = config.Configs["Meshing"];

            decodedSculptMapPath = start_config.GetString("DecodedSculptMapPath", "j2kDecodeCache");
            cacheSculptMaps = start_config.GetBoolean("CacheSculptMaps", cacheSculptMaps);
            UseMeshesPhysicsMesh = start_config.GetBoolean("UseMeshesPhysicsMesh", UseMeshesPhysicsMesh);

            cacheSculptAlphaMaps = Environment.OSVersion.Platform != PlatformID.Unix && cacheSculptMaps;
            m_j2kDecoder = registry.RequestModuleInterface<IJ2KDecoder>();
            try
            {
                if (!Directory.Exists(decodedSculptMapPath))
                    Directory.CreateDirectory(decodedSculptMapPath);
            }
            catch (Exception e)
            {
                MainConsole.Instance.WarnFormat("[SCULPT]: Unable to create {0} directory: ", decodedSculptMapPath,
                                                e.ToString());
            }
        }

        /// <summary>
        ///     creates a simple box mesh of the specified size. This mesh is of very low vertex count and may
        ///     be useful as a backup proxy when level of detail is not needed or when more complex meshes fail
        ///     for some reason
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <param name="minZ"></param>
        /// <param name="maxZ"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        static Mesh CreateSimpleBoxMesh(float minX, float maxX, float minY, float maxY, float minZ, float maxZ,
                                                ulong key)
        {
            Mesh box = new Mesh(key);
            List<Coord> vertices = new List<Coord>
            {
                new Coord(minX, maxY, minZ),
                new Coord(maxX, maxY, minZ),
                new Coord(maxX, minY, minZ),
                new Coord(minX, minY, minZ),
                new Coord(maxX, maxY, maxZ),
                new Coord(minX, maxY, maxZ),
                new Coord(minX, minY, maxZ),
                new Coord(maxX, minY, maxZ)
            };

            List<Face> faces = new List<Face>();

            // bottom
            faces.Add(new Face(0, 1, 2));
            faces.Add(new Face(0, 2, 3));

            // top
            faces.Add(new Face(4, 5, 6));
            faces.Add(new Face(4, 6, 7));

            // sides

            faces.Add(new Face(5, 0, 3));
            faces.Add(new Face(5, 3, 6));

            faces.Add(new Face(1, 0, 5));
            faces.Add(new Face(1, 5, 4));

            faces.Add(new Face(7, 1, 4));
            faces.Add(new Face(7, 2, 1));

            faces.Add(new Face(3, 2, 7));
            faces.Add(new Face(3, 7, 6));

            box.Set(vertices, faces);

            vertices.Clear();
            faces.Clear();

            return box;
        }


        /// <summary>
        ///     Creates a simple bounding box mesh for a complex input mesh
        /// </summary>
        /// <param name="meshIn"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        static Mesh CreateBoundingBoxMesh(Vector3 size, ulong key)
        {
            return CreateSimpleBoxMesh(0, size.X, 0, size.Y, 0, size.Z, key);
        }

        void ReportPrimError(string message, string primName, PrimMesh primMesh)
        {
            MainConsole.Instance.Error(message);
            MainConsole.Instance.Error("\nPrim Name: " + primName);
            MainConsole.Instance.Error("****** PrimMesh Parameters ******\n" + primMesh.ParamsToDisplayString());
        }

        /// <summary>
        /// decompresses a gzipped OSD object
        /// </summary>
        /// <param name="decodedOsd"></param> the OSD object
        /// <param name="meshBytes"></param>
        /// <returns></returns>
        static OSD DecompressOsd(byte[] meshBytes)
        {
            OSD decodedOsd = null;

            using (MemoryStream outMs = new MemoryStream())
            {
                using (ZOutputStream zOut = new ZOutputStream(outMs))
                {
                    using (Stream inMs = new MemoryStream(meshBytes))
                     {
                        byte[] readBuffer = new byte[2000];
                        int readLen;

                        while ((readLen = inMs.Read(readBuffer, 0, readBuffer.Length)) > 0)
                        {
                            zOut.Write(readBuffer, 0, readLen);
                        }
                        zOut.Flush();
                        zOut.finish();

                        byte[] decompressedBuf = outMs.ToArray();

                        decodedOsd = OSDParser.DeserializeLLSDBinary(decompressedBuf);
                    }
                }
            }

            return decodedOsd;
        }



        /// <summary>
        /// Create a physics mesh from data that comes with the prim.  The actual data used depends on the prim type.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="lod"></param>
        /// <returns></returns>
        Mesh CreateMeshFromPrimMesher (string primName, PrimitiveBaseShape primShape, Vector3 size,
                                      float lod, ulong key)
        {
            Mesh mesh = null;

            if (primShape.SculptEntry)
            {
                if (((SculptType)primShape.SculptType & SculptType.Mesh) == SculptType.Mesh)
                {
                    if (UseMeshesPhysicsMesh)
                        mesh = GenerateFromPrimMeshData (primName, primShape, size, key);
                } else
                {
                    mesh = GenerateFromPrimSculptData (primName, primShape, size, lod, key);
                }
            } else
            {
                mesh = GenerateFromPrimShapeData (primName, primShape, size, lod, key);
            }

            return mesh;
        }


        /// <summary>
        /// Add a submesh to an existing list of coords and faces.
        /// </summary>
        /// <param name="subMeshData"></param>
        /// <param name="size">Size of entire object</param>
        /// <param name="coords"></param>
        /// <param name="faces"></param>
        void AddSubMesh(OSDMap subMeshMap, Vector3 size, List<Coord> coords, List<Face> faces)
        {
 
            // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
            // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
            // geometry for this submesh.
            if (subMeshMap.ContainsKey("NoGeometry") && (subMeshMap["NoGeometry"]))
                return;

            Vector3 posMax = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 posMin = new Vector3(-0.5f, -0.5f, -0.5f);
            if (subMeshMap.ContainsKey("PositionDomain"))                //Optional, so leave the max and min values otherwise
            {
                posMax = ((OSDMap) subMeshMap["PositionDomain"])["Max"].AsVector3();
                posMin = ((OSDMap) subMeshMap["PositionDomain"])["Min"].AsVector3();
            }
            ushort faceIndexOffset = (ushort) coords.Count;

            byte[] posBytes = subMeshMap["Position"].AsBinary();
            for (int i = 0; i < posBytes.Length; i += 6)
            {
                ushort uX = Utils.BytesToUInt16(posBytes, i);
                ushort uY = Utils.BytesToUInt16(posBytes, i + 2);
                ushort uZ = Utils.BytesToUInt16(posBytes, i + 4);

                Coord c = new Coord(
                    Utils.UInt16ToFloat(uX, posMin.X, posMax.X)*size.X,
                    Utils.UInt16ToFloat(uY, posMin.Y, posMax.Y)*size.Y,
                    Utils.UInt16ToFloat(uZ, posMin.Z, posMax.Z)*size.Z);

                coords.Add(c);
            }

            byte[] triangleBytes = subMeshMap["TriangleList"].AsBinary();
            for (int i = 0; i < triangleBytes.Length; i += 6)
            {
                ushort v1 = (ushort) (Utils.BytesToUInt16(triangleBytes, i) + faceIndexOffset);
                ushort v2 = (ushort) (Utils.BytesToUInt16(triangleBytes, i + 2) + faceIndexOffset);
                ushort v3 = (ushort) (Utils.BytesToUInt16(triangleBytes, i + 4) + faceIndexOffset);
                Face f = new Face(v1, v2, v3);
                faces.Add(f);
            }
        }

        /// <summary>
        /// Generate a mesh from the mesh data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="key"></param> 
        /// <returns>created mesh or null if invalid</returns>
        Mesh GenerateFromPrimMeshData(string primName, PrimitiveBaseShape primShape, Vector3 size, ulong key)
        {
            var coords = new List<Coord> ();
            var faces = new List<Face> ();
                     
            OSD meshOsd;
            mConvexHulls = null;
            mBoundingHull = null;

            if (primShape.SculptData == null || primShape.SculptData.Length <= 0)
            {
                // At the moment we can not log here since ODEPrim, for instance, ends up triggering this
                // method twice - once before it has loaded sculpt data from the asset service and once afterwards.
                // The first time will always call with unloaded SculptData if this needs to be uploaded.
                //MainConsole.Instance.Error("[MESH]: asset data is zero length");
                return null;
            }

            long start;
            using (MemoryStream data = new MemoryStream(primShape.SculptData))
            {
                try
                {
                    meshOsd = OSDParser.DeserializeLLSDBinary(data);
                }
                catch (Exception e)
                {
                    MainConsole.Instance.Error("[MESH]: Exception deserializing mesh asset header:" + e);
                    return null;
                }
                start = data.Position;
            }

            if (meshOsd is OSDMap)
            {
                OSDMap physicsParms = null;
                OSDMap map = (OSDMap)meshOsd;
                if (map.ContainsKey("physics_shape"))
                {
                    physicsParms = (OSDMap)map["physics_shape"]; // old asset format
                    if (debugDetail) MainConsole.Instance.DebugFormat("[MESH]: prim='{0}': using 'physics_shape' mesh data", primName);
                }
                else if (map.ContainsKey("physics_mesh"))
                {
                    physicsParms = (OSDMap)map["physics_mesh"]; // new asset format
                    if (debugDetail) MainConsole.Instance.DebugFormat("[MESH]: prim='{0}':using 'physics_mesh' mesh data", primName);
                }
                else if (map.ContainsKey("medium_lod"))
                {
                    physicsParms = (OSDMap)map["medium_lod"]; // if no physics mesh, try to fall back to medium LOD display mesh
                    if (debugDetail) MainConsole.Instance.DebugFormat("[MESH]: prim='{0}':using 'medium_lod' mesh data", primName);
                }
                else if (map.ContainsKey("high_lod"))
                {
                    physicsParms = (OSDMap)map["high_lod"]; // if all else fails, use highest LOD display mesh and hope it works :)
                    if (debugDetail) MainConsole.Instance.DebugFormat("[MESH]: prim='{0}':using 'high_lod' mesh data", primName);
                }

                if (map.ContainsKey ("physics_convex"))
                { 
                    // pull this out also in case physics engine can use it
                    OSD convexBlockOsd = null;
                    try
                    {
                        OSDMap convexBlock = (OSDMap)map ["physics_convex"];
                        {
                            int convexOffset = convexBlock ["offset"].AsInteger () + (int)start;
                            int convexSize = convexBlock ["size"].AsInteger ();

                            byte[] convexBytes = new byte[convexSize];

                            Buffer.BlockCopy (primShape.SculptData, convexOffset, convexBytes, 0, convexSize);

                            try
                            {
                                convexBlockOsd = DecompressOsd (convexBytes);
                            } catch (Exception e)
                            {
                                MainConsole.Instance.ErrorFormat ("[MESH]: prim '{0}': exception decoding convex block: {1}", primName, e);
                                //return false;
                            }
                        }

                        if (convexBlockOsd is OSDMap)
                        {
                            convexBlock = convexBlockOsd as OSDMap;

                            if (debugDetail)
                            {
                                string keys = "[Mesh]: keys found in convexBlock: ";
                                foreach (KeyValuePair<string, OSD> kvp in convexBlock)
                                    keys += "'" + kvp.Key + "' ";
                                MainConsole.Instance.Debug(keys);
                            }
                            Vector3 min = new Vector3 (-0.5f, -0.5f, -0.5f);
                            if (convexBlock.ContainsKey ("Min"))
                                min = convexBlock ["Min"].AsVector3 ();

                            Vector3 max = new Vector3 (0.5f, 0.5f, 0.5f);
                            if (convexBlock.ContainsKey ("Max"))
                                max = convexBlock ["Max"].AsVector3 ();

                            List<Vector3> boundingHull = null;

                            if (convexBlock.ContainsKey ("BoundingVerts"))
                            {
                                byte[] boundingVertsBytes = convexBlock ["BoundingVerts"].AsBinary ();
                                boundingHull = new List<Vector3> ();
                                for (int i = 0; i < boundingVertsBytes.Length;)
                                {
                                    ushort uX = Utils.BytesToUInt16 (boundingVertsBytes, i);
                                    i += 2;
                                    ushort uY = Utils.BytesToUInt16 (boundingVertsBytes, i);
                                    i += 2;
                                    ushort uZ = Utils.BytesToUInt16 (boundingVertsBytes, i);
                                    i += 2;

                                    Vector3 pos = new Vector3 (
                                                      Utils.UInt16ToFloat (uX, min.X, max.X),
                                                      Utils.UInt16ToFloat (uY, min.Y, max.Y),
                                                      Utils.UInt16ToFloat (uZ, min.Z, max.Z)
                                                  );

                                    boundingHull.Add (pos);
                                }

                                mBoundingHull = boundingHull;
                                if (debugDetail) MainConsole.Instance.DebugFormat ("[MESH]: prim '{0}': parsed bounding hull. nVerts={1}", primName, mBoundingHull.Count);
                            }

                            if (convexBlock.ContainsKey ("HullList"))
                            {
                                byte[] hullList = convexBlock ["HullList"].AsBinary ();
                                byte[] posBytes = convexBlock ["Positions"].AsBinary ();

                                var hulls = new List<List<Vector3>> ();
                                int posNdx = 0;

                                foreach (byte cnt in hullList)
                                {
                                    int count = cnt == 0 ? 256 : cnt;
                                    var hull = new List<Vector3> ();

                                    for (int i = 0; i < count; i++)
                                    {
                                        ushort uX = Utils.BytesToUInt16 (posBytes, posNdx);
                                        posNdx += 2;
                                        ushort uY = Utils.BytesToUInt16 (posBytes, posNdx);
                                        posNdx += 2;
                                        ushort uZ = Utils.BytesToUInt16 (posBytes, posNdx);
                                        posNdx += 2;

                                        var pos = new Vector3 (
                                                      Utils.UInt16ToFloat (uX, min.X, max.X),
                                                      Utils.UInt16ToFloat (uY, min.Y, max.Y),
                                                      Utils.UInt16ToFloat (uZ, min.Z, max.Z)
                                                  );

                                        hull.Add (pos);
                                    }

                                    hulls.Add (hull);
                                }

                                mConvexHulls = hulls;
                                if (debugDetail) MainConsole.Instance.DebugFormat ("[MESH]: prim '{0}': parsed hulls. nHulls '{1}'", primName, mConvexHulls.Count);
                            } else
                            {
                                if (debugDetail) MainConsole.Instance.DebugFormat ("[MESH]: prim '{0}' has physics_convex but no HullList", primName);
                            }
                        }
                    } catch (Exception e)
                    {
                        MainConsole.Instance.WarnFormat ("[MESH]: Exception decoding convex block: {0}", e);
                    }
                }

                if (physicsParms == null)
                {
                    MainConsole.Instance.WarnFormat ("[MESH]: No recognised physics mesh found in mesh asset for {0}", primName);
                    return  null;
                }

                int physOffset = physicsParms ["offset"].AsInteger () + (int)start;
                int physSize = physicsParms ["size"].AsInteger ();

                if (physOffset < 0 || physSize == 0)
                    return null; // no mesh data in asset

                var decodedMeshOsd = new OSD ();
                var meshBytes = new byte[physSize];
                Buffer.BlockCopy (primShape.SculptData, physOffset, meshBytes, 0, physSize);

                try
                {
                    decodedMeshOsd = DecompressOsd (meshBytes);
                } catch (Exception e)
                {
                    MainConsole.Instance.ErrorFormat ("[MESH]: prim: '{0}', exception decoding physical mesh: {1}", primName, e);
                    return null;
                }

                OSDArray decodedMeshOsdArray = null;

                // physics_shape is an array of OSDMaps, one for each submesh
                if (decodedMeshOsd is OSDArray)
                {
                    decodedMeshOsdArray = (OSDArray)decodedMeshOsd;
                    foreach (OSD subMeshOsd in decodedMeshOsdArray)
                    {
                        if (subMeshOsd is OSDMap)
                            AddSubMesh (subMeshOsd as OSDMap, size, coords, faces);
                    }
										if (debugDetail) 
                        MainConsole.Instance.DebugFormat ("[MESH]: {0}: mesh decoded. offset={1}, size={2}, nCoords={3}, nFaces={4}",
                            primName, physOffset, physSize, coords.Count, faces.Count);
                }
            }
        
            Mesh mesh = new Mesh(key);
            mesh.Set(coords, faces);
            // This is probably wher we should process convexhulls etc. - greythane - Sep 2015
            coords.Clear();
            faces.Clear();

            // debug info only
            //Console.Write ("M");
            return mesh;

        }


        /// <summary>
        /// Generate a mesh from the sculpt data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="lod"></param>
        /// <param name="key"></param> 
        /// <returns>created mesh or null if invalid</returns>
        Mesh GenerateFromPrimSculptData(string primName, PrimitiveBaseShape primShape, Vector3 size, float lod, ulong key)
        {
            SculptMesh sculptMesh;
            Image idata = null;
            string decodedSculptFileName = "";


            if (cacheSculptMaps && primShape.SculptTexture != UUID.Zero)
            {
                decodedSculptFileName = System.IO.Path.Combine (decodedSculptMapPath,
                    "smap_" + primShape.SculptTexture);
                try
                {
                    if (File.Exists (decodedSculptFileName))
                    {
                        idata = Image.FromFile (decodedSculptFileName);
                    }
                } catch (Exception e)
                {
                    MainConsole.Instance.Error ("[SCULPT]: unable to load cached sculpt map " +
                    decodedSculptFileName + " " + e);
                }
                //if (idata != null)
                //    MainConsole.Instance.Debug("[SCULPT]: loaded cached map asset for map ID: " + primShape.SculptTexture.ToString());
            }

            if (idata == null)
            {
                if (primShape.SculptData == null || primShape.SculptData.Length == 0)
                    return null;

                try
                {
                    idata = m_j2kDecoder.DecodeToImage (primShape.SculptData);

                    if (idata != null && cacheSculptMaps &&
                        (cacheSculptAlphaMaps || (((ImageFlags)(idata.Flags) & ImageFlags.HasAlpha) == 0)))
                    {
                        try
                        {
                            idata.Save (decodedSculptFileName, ImageFormat.MemoryBmp);
                        } catch (Exception e)
                        {
                            MainConsole.Instance.Error ("[SCULPT]: unable to cache sculpt map " +
                            decodedSculptFileName + " " +
                            e);
                        }
                    }
                } catch (DllNotFoundException)
                {
                    MainConsole.Instance.Error (
                        "[PHYSICS]: OpenJpeg is not installed correctly on this system. Physics Proxy generation failed.\n" +
                        "Often times this is because of an old version of GLIBC.  You must have version 2.4 or above!");
                    return null;
                } catch (IndexOutOfRangeException)
                {
                    MainConsole.Instance.Error (
                        "[PHYSICS]: OpenJpeg was unable to decode this. Physics Proxy generation failed");
                    return null;
                } catch (Exception ex)
                {
                    MainConsole.Instance.Error (
                        "[PHYSICS]: Unable to generate a Sculpty physics proxy. Sculpty texture decode failed: " +
                        ex);
                    return null;
                }
            }

            SculptMesh.SculptType sculptType;
            switch ((SculptType)primShape.SculptType)
            {
            case SculptType.Cylinder:
                sculptType = SculptMesh.SculptType.cylinder;
                break;
            case SculptType.Plane:
                sculptType = SculptMesh.SculptType.plane;
                break;
            case SculptType.Torus:
                sculptType = SculptMesh.SculptType.torus;
                break;
            case SculptType.Sphere:
                sculptType = SculptMesh.SculptType.sphere;
                break;
            default:
                sculptType = SculptMesh.SculptType.plane;
                break;
            }

            bool mirror = ((primShape.SculptType & 128) != 0);
            bool invert = ((primShape.SculptType & 64) != 0);

            if (idata == null)
                return null;

            sculptMesh = new SculptMesh ((Bitmap)idata, sculptType, (int)lod, false, mirror, invert);

            idata.Dispose ();
            #if SPAM
                sculptMesh.DumpRaw (baseDir, primName, "primMesh");
            #endif

            sculptMesh.Scale (size.X, size.Y, size.Z);

            var coords = sculptMesh.coords;
            var faces = sculptMesh.faces;

            Mesh mesh = new Mesh(key);
            mesh.Set(coords, faces);
            coords.Clear();
            faces.Clear();

            // debug info only
            //Console.Write ("S");

            return mesh;
        }



        /// <summary>
        /// Generate a mesh from the shape data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="key"></param> 
        /// <returns>created mesh or null if invalid</returns>
        Mesh GenerateFromPrimShapeData(string primName, PrimitiveBaseShape primShape, Vector3 size, float lod, ulong key)
        {
            PrimMesh primMesh;

            float pathShearX = primShape.PathShearX < 128
                ? primShape.PathShearX * 0.01f
                : (primShape.PathShearX - 256) * 0.01f;
            float pathShearY = primShape.PathShearY < 128
                ? primShape.PathShearY * 0.01f
                : (primShape.PathShearY - 256) * 0.01f;
            float pathBegin = primShape.PathBegin * 2.0e-5f;
            float pathEnd = 1.0f - (primShape.PathEnd * 2.0e-5f);
            float pathScaleX = (primShape.PathScaleX - 100) * 0.01f;
            float pathScaleY = (primShape.PathScaleY - 100) * 0.01f;

            float profileBegin = primShape.ProfileBegin * 2.0e-5f;
            float profileEnd = 1.0f - (primShape.ProfileEnd * 2.0e-5f);
            float profileHollow = primShape.ProfileHollow * 2.0e-5f;
            if (profileHollow > 0.95f)
            {
                if (profileHollow > 0.99f)
                    profileHollow = 0.99f;
                float sizeX = primShape.Scale.X - (primShape.Scale.X * profileHollow);
                if (sizeX < 0.1f)                             // If its > 0.1, its fine to mesh at the small hollow
                    profileHollow = 0.95f + (sizeX / 2);      // Scale the rest by how large the size of the prim is
            }

            int sides = 4;          // assume the prim is square
            switch ((primShape.ProfileCurve & 0x07))
            {
            case (byte) ProfileShape.EquilateralTriangle:
                sides = 3;
                break;
            case (byte) ProfileShape.Circle:
                sides = GetLevelOfDetail(lod);
                break;
            case (byte) ProfileShape.HalfCircle:
                sides = GetLevelOfDetail(lod);
                profileBegin = (0.5f * profileBegin) + 0.5f;
                profileEnd = (0.5f * profileEnd) + 0.5f;
                break;  
            }

            int hollowSides = sides;
            switch (primShape.HollowShape)
            {
            case HollowShape.Circle:
                hollowSides = GetLevelOfDetail(lod);
                break; 
            case HollowShape.Square:
                hollowSides = 4;
                break;
            case HollowShape.Triangle:
                hollowSides = 3;
                break;
            }
                
            primMesh = new PrimMesh(sides, profileBegin, profileEnd, profileHollow, hollowSides);

            if ( (primMesh.errorMessage != null) && (primMesh.errorMessage.Length > 0) )
                MainConsole.Instance.Error("[ERROR] " + primMesh.errorMessage);

            primMesh.topShearX = pathShearX;
            primMesh.topShearY = pathShearY;
            primMesh.pathCutBegin = pathBegin;
            primMesh.pathCutEnd = pathEnd;

            if (primShape.PathCurve == (byte)Extrusion.Straight || primShape.PathCurve == (byte)Extrusion.Flexible)
            {
                primMesh.twistBegin = (primShape.PathTwistBegin * 18) / 10;
                primMesh.twistEnd = (primShape.PathTwist * 18) / 10;
                primMesh.taperX = pathScaleX;
                primMesh.taperY = pathScaleY;

                #if SPAM
                    MainConsole.Instance.Debug("****** PrimMesh Parameters (Linear) ******\n" + primMesh.ParamsToDisplayString());
                #endif
                try
                {
                    primMesh.Extrude (primShape.PathCurve == (byte)Extrusion.Straight
                        ? PathType.Linear
                        : PathType.Flexible);
                } catch (Exception ex)
                {
                    ReportPrimError ("Extrusion failure: exception: " + ex, primName, primMesh);
                    return null;
                }
            } else
            {
                primMesh.holeSizeX = (200 - primShape.PathScaleX) * 0.01f;
                primMesh.holeSizeY = (200 - primShape.PathScaleY) * 0.01f;
                primMesh.radius = 0.01f * primShape.PathRadiusOffset;
                primMesh.revolutions = 1.0f + (primShape.PathRevolutions * 0.015f);
                primMesh.skew = 0.01f * primShape.PathSkew;
                primMesh.twistBegin = (primShape.PathTwistBegin * 36) / 10;
                primMesh.twistEnd = (primShape.PathTwist * 36) / 10;
                primMesh.taperX = primShape.PathTaperX * 0.01f;
                primMesh.taperY = primShape.PathTaperY * 0.01f;

               #if SPAM
                    MainConsole.Instance.Debug("****** PrimMesh Parameters (Circular) ******\n" + primMesh.ParamsToDisplayString());
                #endif
                try
                {
                    primMesh.Extrude (PathType.Circular);
                } catch (Exception ex)
                {
                    ReportPrimError ("Extrusion failure: exception: " + ex, primName, primMesh);
                    return null;
                }
            }

            #if SPAM
                debugprimMesh.DumpRaw(baseDir, primName, "primMesh");
            #endif

            primMesh.Scale(size.X, size.Y, size.Z);

            var coords = primMesh.coords;
            var faces = primMesh.faces;

            Mesh mesh = new Mesh(key);
            mesh.Set(coords, faces);
            coords.Clear();
            faces.Clear();

            // debug info only
            //Console.Write ("P");

            return mesh;
        }


        /// <summary>
        /// Gets the level of detail for circles.
        /// </summary>
        /// <returns>The level of detail.</returns>
        /// <param name="lod">Lod.</param>
        int GetLevelOfDetail(float lod)
        {
            int sides;

            // defaults for LOD
            LevelOfDetail iLOD = (LevelOfDetail)lod;
            switch (iLOD)
            {    
            case LevelOfDetail.High:
                sides = 24;
                break;
            case LevelOfDetail.Medium:
                sides = 12;
                break;
            case LevelOfDetail.Low:
                sides = 6;
                break;
            case LevelOfDetail.VeryLow:
                sides = 3;
                break;
            default:
                sides = 24;
                break;
            }

            return sides;
        }

        /// <summary>
        /// temporary prototype code - please do not use until the interface has been finalized!
        /// </summary>
        /// <param name="size">value to scale the hull points by</param>
        /// <returns>a list of vertices in the bounding hull if it exists and has been successfully decoded, otherwise null</returns>
        public List<Vector3> GetBoundingHull(Vector3 size)
        {
            if (mBoundingHull == null)
                return null;

            List<Vector3> verts = new List<Vector3>();
            foreach (var vert in mBoundingHull)
                verts.Add(vert * size);

            return verts;
        }

        /// <summary>
        /// temporary prototype code - please do not use until the interface has been finalized!
        /// </summary>
        /// <param name="size">value to scale the hull points by</param>
        /// <returns>a list of hulls if they exist and have been successfully decoded, otherwise null</returns>
        public List<List<Vector3>> GetConvexHulls(Vector3 size)
        {
            if (mConvexHulls == null)
                return null;

            List<List<Vector3>> hulls = new List<List<Vector3>>();
            foreach (var hull in mConvexHulls)
            {
                List<Vector3> verts = new List<Vector3>();
                foreach (var vert in hull)
                    verts.Add(vert * size);
                hulls.Add(verts);
            }

            return hulls;
        }

        // Main mesh creation entry point
        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool shouldCache)
        {
#if SPAM
            MainConsole.Instance.DebugFormat("[MESH]: Creating mesh for {0}", primName);
#endif
            Mesh mesh;
            ulong key = primShape.GetMeshKey(size, lod);

            // If caching and this mesh has been created already, return it instead of creating another copy
            if (shouldCache)
            {
                lock (m_uniqueMeshes)
                {
                    if (m_uniqueMeshes.TryGetValue(key, out mesh))
                        return mesh;
                }
            }
            // set miniumm sizes
            if (size.X < 0.01f) size.X = 0.01f;
            if (size.Y < 0.01f) size.Y = 0.01f;
            if (size.Z < 0.01f) size.Z = 0.01f;

            if ((!isPhysical) && size.X < minSizeForComplexMesh && size.Y < minSizeForComplexMesh && size.Z < minSizeForComplexMesh)
                mesh = CreateBoundingBoxMesh(size, key);
            else
                mesh = CreateMeshFromPrimMesher(primName, primShape, size, lod, key);

            // cache newly created mesh?
            if (shouldCache)
            {
                lock (m_uniqueMeshes)
                {
                    m_uniqueMeshes.Add(key, mesh);
                }
            }
            return mesh;
        }
    }
}
