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
using ZLib.Net;

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
#else
        const string baseDir = null; //"rawFiles";
#endif

        readonly bool cacheSculptMaps = true;
        bool cacheSculptAlphaMaps = true;

        readonly string decodedSculptMapPath;
        readonly bool UseMeshesPhysicsMesh;

        // prims with all dimensions smaller than this will have a bounding box mesh
        float minSizeForComplexMesh = 0.15f;

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
                        //outMs.Seek(0, SeekOrigin.Begin);

                        //byte[] decompressedBuf = outMs.GetBuffer();
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
            List<Coord> coords;
            List<Face> faces;

            if (primShape.SculptEntry)
            {
                if (((SculptType)primShape.SculptType) == SculptType.Mesh)
                {
                    if (!UseMeshesPhysicsMesh)
                        return null;

                    if (!GenerateFromPrimMeshData (primName, primShape, size, out coords, out faces))
                        return null;
                } else
                {
                    if (!GenerateFromPrimSculptData (primName, primShape, size, lod, out coords, out faces))
                        return null;
                }
            } else
            {
                if (!GenerateFromPrimShapeData (primName, primShape, size, lod, out coords, out faces))
                    return null;
            }

            Mesh mesh = new Mesh (key);
            mesh.Set (coords, faces);
            coords.Clear ();
            faces.Clear ();

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
            if (subMeshMap.ContainsKey("PositionDomain"))
                //Optional, so leave the max and min values otherwise
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
        /// Generate the co-ords and faces necessary to construct a mesh from the mesh data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="coords">Coords are added to this list by the method.</param>
        /// <param name="faces">Faces are added to this list by the method.</param>
        /// <returns>true if coords and faces were successfully generated, false if not</returns>
        bool GenerateFromPrimMeshData(string primName, PrimitiveBaseShape primShape, Vector3 size, 
            out List<Coord> coords, out List<Face> faces)
        {
            coords = new List<Coord> ();
            faces = new List<Face> ();
                     
            OSD meshOsd;
            mConvexHulls = null;
            mBoundingHull = null;

            if (primShape.SculptData == null || primShape.SculptData.Length <= 0)
            {
                //MainConsole.Instance.Error("[MESH]: asset data is zero length");
                return false;
            }

            long start = 0;
            using (MemoryStream data = new MemoryStream(primShape.SculptData))
            {
                try
                {
                    meshOsd = OSDParser.DeserializeLLSDBinary(data);
                }
                catch (Exception e)
                {
                    MainConsole.Instance.Error("[MESH]: Exception deserializing mesh asset header:" + e);
                    return false;
                }
                start = data.Position;
            }

            if (meshOsd is OSDMap)
            {
                OSDMap map = (OSDMap)meshOsd;
                OSDMap physicsParms = new OSDMap ();

//               if (map.ContainsKey ("physics_cached"))
//                {
                    //                  OSD cachedMeshMap = map["physics_cached"]; // cached data from WhiteCore
                    //                  Mesh cachedMesh = new Mesh(key);
                    //                  cachedMesh.Deserialize(cachedMeshMap);
                    //                  cachedMesh.WasCached = true;
                    //                  return cachedMesh; //Return here, we found all of the info right here
//                    return true;
//                }
                if (map.ContainsKey ("physics_shape"))
                    physicsParms = (OSDMap)map ["physics_shape"];   // old asset format
                else if (map.ContainsKey ("physics_mesh"))
                    physicsParms = (OSDMap)map ["physics_mesh"];    // new asset format
                else if (map.ContainsKey ("medium_lod"))
                    physicsParms = (OSDMap)map ["medium_lod"];     // fallback to medium LOD mesh
                else if (map.ContainsKey ("high_lod"))
                    physicsParms = (OSDMap)map ["high_lod"];        // if all esle fails...

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
                                MainConsole.Instance.DebugFormat ("[MESH]: prim '{0}': parsed bounding hull. nVerts={1}", primName, mBoundingHull.Count);
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
                                MainConsole.Instance.DebugFormat ("[MESH]: prim '{0}': parsed hulls. nHulls '{1}'", primName, mConvexHulls.Count);
                            } else
                            {
                                MainConsole.Instance.DebugFormat ("[MESH]: prim '{0}' has physics_convex but no HullList", primName);
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
                    return  false;
                }

                int physOffset = physicsParms ["offset"].AsInteger () + (int)start;
                int physSize = physicsParms ["size"].AsInteger ();

                if (physOffset < 0 || physSize == 0)
                    return false; // no mesh data in asset

                var decodedMeshOsd = new OSD ();
                var meshBytes = new byte[physSize];
                Buffer.BlockCopy (primShape.SculptData, physOffset, meshBytes, 0, physSize);

                try
                {
                    decodedMeshOsd = DecompressOsd (meshBytes);
                } catch (Exception e)
                {
                    MainConsole.Instance.ErrorFormat ("[MESH]: prim: '{0}', exception decoding physical mesh: {1}", primName, e);
                    return false;
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
                    MainConsole.Instance.DebugFormat ("[MESH]: {0}: mesh decoded. offset={1}, size={2}, nCoords={3}, nFaces={4}",
                        primName, physOffset, physSize, coords.Count, faces.Count);
                }
            }
        
            return true;

        }


        /// <summary>
        /// Generate the co-ords and faces necessary to construct a mesh from the sculpt data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="lod"></param>
        /// <param name="coords">Coords are added to this list by the method.</param>
        /// <param name="faces">Faces are added to this list by the method.</param>
        /// <returns>true if coords and faces were successfully generated, false if not</returns>
        bool GenerateFromPrimSculptData(string primName, PrimitiveBaseShape primShape, Vector3 size, float lod,
            out List<Coord> coords, out List<Face> faces)
        {
            coords = new List<Coord> ();
            faces = new List<Face> ();
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
                    return false;

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
                    return false;
                } catch (IndexOutOfRangeException)
                {
                    MainConsole.Instance.Error (
                        "[PHYSICS]: OpenJpeg was unable to decode this. Physics Proxy generation failed");
                    return false;
                } catch (Exception ex)
                {
                    MainConsole.Instance.Error (
                        "[PHYSICS]: Unable to generate a Sculpty physics proxy. Sculpty texture decode failed: " +
                        ex);
                    return false;
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
                return false;

            sculptMesh = new SculptMesh ((Bitmap)idata, sculptType, (int)lod, false, mirror, invert);

            idata.Dispose ();

            sculptMesh.DumpRaw (baseDir, primName, "primMesh");

            sculptMesh.Scale (size.X, size.Y, size.Z);

            coords = sculptMesh.coords;
            faces = sculptMesh.faces;

            return true;
        }



        /// <summary>
        /// Generate the co-ords and faces necessary to construct a mesh from the shape data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="coords">Coords are added to this list by the method.</param>
        /// <param name="faces">Faces are added to this list by the method.</param>
        /// <returns>true if coords and faces were successfully generated, false if not</returns>
        bool GenerateFromPrimShapeData(string primName, PrimitiveBaseShape primShape, Vector3 size,
            float lod, out List<Coord> coords, out List<Face> faces)
        {
            PrimMesh primMesh;
            coords = new List<Coord>();
            faces = new List<Face>();            
            float pathShearX = primShape.PathShearX < 128
                ? primShape.PathShearX * 0.01f
                : (primShape.PathShearX - 256) * 0.01f;
            float pathShearY = primShape.PathShearY < 128
                ? primShape.PathShearY * 0.01f
                : (primShape.PathShearY - 256) * 0.01f;
            float pathBegin = primShape.PathBegin * 2.0e-5f;
            float pathEnd = 1.0f - primShape.PathEnd * 2.0e-5f;
            float pathScaleX = (primShape.PathScaleX - 100) * 0.01f;
            float pathScaleY = (primShape.PathScaleY - 100) * 0.01f;

            float profileBegin = primShape.ProfileBegin * 2.0e-5f;
            float profileEnd = 1.0f - primShape.ProfileEnd * 2.0e-5f;
            float profileHollow = primShape.ProfileHollow * 2.0e-5f;
            if (profileHollow > 0.95f)
            {
                if (profileHollow > 0.99f)
                    profileHollow = 0.99f;
                float sizeX = primShape.Scale.X - (primShape.Scale.X*profileHollow);
                if (sizeX < 0.1f)                           //If its > 0.1, its fine to mesh at the small hollow
                    profileHollow = 0.95f + (sizeX/2);      //Scale the rest by how large the size of the prim is
            }

            int sides = 4;

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

            switch ((primShape.ProfileCurve & 0x07))
            {
            case (byte) ProfileShape.EquilateralTriangle:
                sides = 3;
                break;
            case (byte) ProfileShape.Circle:
                break;      // use lod set above
            case (byte) ProfileShape.HalfCircle:
                profileBegin = 0.5f * profileBegin + 0.5f;
                profileEnd = 0.5f * profileEnd + 0.5f;
                break;      // use lod already set and...
            }

            int hollowSides = sides;
            // default lod for hollows
            switch (iLOD)
            {    
            case LevelOfDetail.High:
                hollowSides = 24;
                break;
            case LevelOfDetail.Medium:
                hollowSides = 12;
                break;
            case LevelOfDetail.Low:
                hollowSides = 6;
                break;
            case LevelOfDetail.VeryLow:
                hollowSides = 3;
                break;
            default:
                hollowSides = 24;
                break;
            }

            switch (primShape.HollowShape)
            {
            case HollowShape.Circle:
                //hollowSides = 24;
                break; // use lod preset
            case HollowShape.Square:
                hollowSides = 4;
                break;
            case HollowShape.Triangle:
                hollowSides = 3;
                break;
            }

            primMesh = new PrimMesh(sides, profileBegin, profileEnd, profileHollow, hollowSides);

            if (primMesh.errorMessage != null)
            if (primMesh.errorMessage.Length > 0)
                MainConsole.Instance.Error("[ERROR] " + primMesh.errorMessage);

            primMesh.topShearX = pathShearX;
            primMesh.topShearY = pathShearY;
            primMesh.pathCutBegin = pathBegin;
            primMesh.pathCutEnd = pathEnd;

            if (primShape.PathCurve == (byte)Extrusion.Straight || primShape.PathCurve == (byte)Extrusion.Flexible)
            {
                primMesh.twistBegin = primShape.PathTwistBegin * 18 / 10;
                primMesh.twistEnd = primShape.PathTwist * 18 / 10;
                primMesh.taperX = pathScaleX;
                primMesh.taperY = pathScaleY;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    ReportPrimError ("*** CORRUPT PRIM!! ***", primName, primMesh);
                    if (profileBegin < 0.0f)
                        profileBegin = 0.0f;
                    if (profileEnd > 1.0f)
                        profileEnd = 1.0f;
                }
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
                    return false;
                }
            } else
            {
                primMesh.holeSizeX = (200 - primShape.PathScaleX) * 0.01f;
                primMesh.holeSizeY = (200 - primShape.PathScaleY) * 0.01f;
                primMesh.radius = 0.01f * primShape.PathRadiusOffset;
                primMesh.revolutions = 1.0f + 0.015f * primShape.PathRevolutions;
                primMesh.skew = 0.01f * primShape.PathSkew;
                primMesh.twistBegin = primShape.PathTwistBegin * 36 / 10;
                primMesh.twistEnd = primShape.PathTwist * 36 / 10;
                primMesh.taperX = primShape.PathTaperX * 0.01f;
                primMesh.taperY = primShape.PathTaperY * 0.01f;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    ReportPrimError ("*** CORRUPT PRIM!! ***", primName, primMesh);
                    if (profileBegin < 0.0f)
                        profileBegin = 0.0f;
                    if (profileEnd > 1.0f)
                        profileEnd = 1.0f;
                }
#if SPAM
                MainConsole.Instance.Debug("****** PrimMesh Parameters (Circular) ******\n" + primMesh.ParamsToDisplayString());
#endif
                try
                {
                    primMesh.Extrude (PathType.Circular);
                } catch (Exception ex)
                {
                    ReportPrimError ("Extrusion failure: exception: " + ex, primName, primMesh);
                    return false;
                }
            }

            primMesh.DumpRaw(baseDir, primName, "primMesh");

            primMesh.Scale(size.X, size.Y, size.Z);

            coords = primMesh.coords;
            faces = primMesh.faces;

            return true;
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

        public void RemoveMesh(ulong key)
        {
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod)
        {
            return CreateMesh(primName, primShape, size, lod, false);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical)
        {
            Mesh mesh;
            ulong key = primShape.GetMeshKey(size, lod);

            // If this mesh has been created already, return it instead of creating another copy
            lock (m_uniqueMeshes)
            {
                if (m_uniqueMeshes.TryGetValue(key, out mesh))
                    return mesh;
            }

            // set miniumm sizes
            if (size.X < 0.01f) size.X = 0.01f;
            if (size.Y < 0.01f) size.Y = 0.01f;
            if (size.Z < 0.01f) size.Z = 0.01f;

            if ((!isPhysical) && size.X < minSizeForComplexMesh && size.Y < minSizeForComplexMesh && size.Z < minSizeForComplexMesh)
                mesh = CreateBoundingBoxMesh(size, key);
            else
                mesh = CreateMeshFromPrimMesher(primName, primShape, size, lod, key);

            // cache newly created mesh's
            lock (m_uniqueMeshes)
            {
                m_uniqueMeshes.Add(key, mesh);
            }

            return mesh;
        }
    }
}
