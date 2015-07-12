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

        float minSizeForComplexMesh = 0.2f;
        IJ2KDecoder m_j2kDecoder = null;
        private List<List<Vector3>> mConvexHulls = null;
        private List<Vector3> mBoundingHull = null;
        // prims with all dimensions smaller than this will have a bounding box mesh

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
            vertices = null;
            faces = null;

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
            return CreateSimpleBoxMesh(0, size.X,0, size.Y, 0, size.Z, key);
        }

        void ReportPrimError(string message, string primName, PrimMesh primMesh)
        {
            MainConsole.Instance.Error(message);
            MainConsole.Instance.Error("\nPrim Name: " + primName);
            MainConsole.Instance.Error("****** PrimMesh Parameters ******\n" + primMesh.ParamsToDisplayString());
        }


        Mesh CreateMeshFromPrimMesher(string primName, PrimitiveBaseShape primShape, Vector3 size, float lod,
                                              ulong key)
        {
            PrimMesh primMesh;
            SculptMesh sculptMesh;

            List<Coord> coords = new List<Coord>();
            List<Face> faces = new List<Face>();

            Image idata = null;
            string decodedSculptFileName = "";

            if (primShape.SculptEntry)
            {
                if (((SculptType) primShape.SculptType & SculptType.Mesh) == SculptType.Mesh)
                {
                    if (!UseMeshesPhysicsMesh)
                        return null;

                    MainConsole.Instance.Debug("[MESH]: experimental mesh proxy generation");

                    OSD meshOsd = null;

                    if (primShape.SculptData == null || primShape.SculptData.Length <= 0)
                    {
                        //MainConsole.Instance.Error("[MESH]: asset data is zero length");
                        return null;
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
                        }
                        start = data.Position;
                    }

                    if (meshOsd is OSDMap)
                    {
                        OSDMap map = (OSDMap) meshOsd;
                        OSDMap physicsParms = new OSDMap();

                        if (map.ContainsKey("physics_cached"))
                        {
                            OSD cachedMeshMap = map["physics_cached"]; // cached data from WhiteCore
                            Mesh cachedMesh = new Mesh(key);
                            cachedMesh.Deserialize(cachedMeshMap);
                            cachedMesh.WasCached = true;
                            return cachedMesh; //Return here, we found all of the info right here
                        }
                        if (map.ContainsKey("physics_shape"))
                            physicsParms = (OSDMap) map["physics_shape"]; // old asset format
                        if (physicsParms.Count == 0 && map.ContainsKey("physics_mesh"))
                            physicsParms = (OSDMap) map["physics_mesh"]; // new asset format
                        if (physicsParms.Count == 0 && map.ContainsKey("physics_convex"))
                            // convex hull format, which we can't read, so instead
                            // read the highest lod that exists, and use it instead
                            physicsParms = (OSDMap) map["high_lod"];

                        int physOffset = physicsParms["offset"].AsInteger() + (int) start;
                        int physSize = physicsParms["size"].AsInteger();

                        if (physOffset < 0 || physSize == 0)
                            return null; // no mesh data in asset

                        OSD decodedMeshOsd = new OSD();
                        byte[] meshBytes = new byte[physSize];
                        Buffer.BlockCopy(primShape.SculptData, physOffset, meshBytes, 0, physSize);
                        try
                        {
                            using (MemoryStream inMs = new MemoryStream(meshBytes))
                            {
                                using (MemoryStream outMs = new MemoryStream())
                                {
                                    using (ZOutputStream zOut = new ZOutputStream(outMs))
                                    {
                                        byte[] readBuffer = new byte[2048];
                                        int readLen = 0;
                                        while ((readLen = inMs.Read(readBuffer, 0, readBuffer.Length)) > 0)
                                        {
                                            zOut.Write(readBuffer, 0, readLen);
                                        }
                                        zOut.Flush();
                                        outMs.Seek(0, SeekOrigin.Begin);

                                        byte[] decompressedBuf = outMs.GetBuffer();

                                        decodedMeshOsd = OSDParser.DeserializeLLSDBinary(decompressedBuf);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            MainConsole.Instance.Error("[MESH]: exception decoding physical mesh: " + e);
                            return null;
                        }

                        OSDArray decodedMeshOsdArray = null;

                        // physics_shape is an array of OSDMaps, one for each submesh
                        if (decodedMeshOsd is OSDArray)
                        {
                            decodedMeshOsdArray = (OSDArray) decodedMeshOsd;
                            foreach (OSD subMeshOsd in decodedMeshOsdArray)
                            {
                                if (subMeshOsd is OSDMap)
                                {
                                    OSDMap subMeshMap = (OSDMap) subMeshOsd;

                                    // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
                                    // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
                                    // geometry for this submesh.
                                    if (subMeshMap.ContainsKey("NoGeometry") && (subMeshMap["NoGeometry"]))
                                        continue;

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
                                        ushort v2 =
                                            (ushort) (Utils.BytesToUInt16(triangleBytes, i + 2) + faceIndexOffset);
                                        ushort v3 =
                                            (ushort) (Utils.BytesToUInt16(triangleBytes, i + 4) + faceIndexOffset);
                                        Face f = new Face(v1, v2, v3);
                                        faces.Add(f);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (cacheSculptMaps && primShape.SculptTexture != UUID.Zero)
                    {
                        decodedSculptFileName = System.IO.Path.Combine(decodedSculptMapPath,
                                                             "smap_" + primShape.SculptTexture.ToString());
                        try
                        {
                            if (File.Exists(decodedSculptFileName))
                            {
                                idata = Image.FromFile(decodedSculptFileName);
                            }
                        }
                        catch (Exception e)
                        {
                            MainConsole.Instance.Error("[SCULPT]: unable to load cached sculpt map " +
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
                            idata = m_j2kDecoder.DecodeToImage(primShape.SculptData);

                            if (idata != null && cacheSculptMaps &&
                                (cacheSculptAlphaMaps || (((ImageFlags) (idata.Flags) & ImageFlags.HasAlpha) == 0)))
                            {
                                try
                                {
                                    idata.Save(decodedSculptFileName, ImageFormat.MemoryBmp);
                                }
                                catch (Exception e)
                                {
                                    MainConsole.Instance.Error("[SCULPT]: unable to cache sculpt map " +
                                                               decodedSculptFileName + " " +
                                                               e);
                                }
                            }
                        }
                        catch (DllNotFoundException)
                        {
                            MainConsole.Instance.Error(
                                "[PHYSICS]: OpenJpeg is not installed correctly on this system. Physics Proxy generation failed.  Often times this is because of an old version of GLIBC.  You must have version 2.4 or above!");
                            return null;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            MainConsole.Instance.Error(
                                "[PHYSICS]: OpenJpeg was unable to decode this. Physics Proxy generation failed");
                            return null;
                        }
                        catch (Exception ex)
                        {
                            MainConsole.Instance.Error(
                                "[PHYSICS]: Unable to generate a Sculpty physics proxy. Sculpty texture decode failed: " +
                                ex);
                            return null;
                        }
                    }

                    SculptMesh.SculptType sculptType;
                    switch ((SculptType) primShape.SculptType)
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

                    sculptMesh = new SculptMesh((Bitmap) idata, sculptType, (int) lod, false, mirror, invert);

                    idata.Dispose();
                    idata = null;

                    sculptMesh.DumpRaw(baseDir, primName, "primMesh");

                    sculptMesh.Scale(size.X, size.Y, size.Z);

                    coords = sculptMesh.coords;
                    faces = sculptMesh.faces;
                }
            }
            else
            {
                float pathShearX = primShape.PathShearX < 128
                                       ? primShape.PathShearX*0.01f
                                       : (primShape.PathShearX - 256)*0.01f;
                float pathShearY = primShape.PathShearY < 128
                                       ? primShape.PathShearY*0.01f
                                       : (primShape.PathShearY - 256)*0.01f;
                float pathBegin = primShape.PathBegin*2.0e-5f;
                float pathEnd = 1.0f - primShape.PathEnd*2.0e-5f;
                float pathScaleX = (primShape.PathScaleX - 100)*0.01f;
                float pathScaleY = (primShape.PathScaleY - 100)*0.01f;

                float profileBegin = primShape.ProfileBegin*2.0e-5f;
                float profileEnd = 1.0f - primShape.ProfileEnd*2.0e-5f;
                float profileHollow = primShape.ProfileHollow*2.0e-5f;
                if (profileHollow > 0.95f)
                {
                    if (profileHollow > 0.99f)
                        profileHollow = 0.99f;
                    float sizeX = primShape.Scale.X - (primShape.Scale.X*profileHollow);
                    if (sizeX < 0.1f) //If its > 0.1, its fine to mesh at the small hollow
                        profileHollow = 0.95f + (sizeX/2); //Scale the rest by how large the size of the prim is
                }

                int sides = 4;
                switch ((primShape.ProfileCurve & 0x07))
                {
                    case (byte) ProfileShape.EquilateralTriangle:
                        sides = 3;
                        break;
                    case (byte) ProfileShape.Circle:
                        sides = 24;
                        break;
                    case (byte) ProfileShape.HalfCircle:
                        sides = 24;
                        profileBegin = 0.5f*profileBegin + 0.5f;
                        profileEnd = 0.5f*profileEnd + 0.5f;
                        break;
                }

                int hollowSides = sides;
                switch (primShape.HollowShape)
                {
                    case HollowShape.Circle:
                        hollowSides = 24;
                        break;
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

                if (primShape.PathCurve == (byte) Extrusion.Straight || primShape.PathCurve == (byte) Extrusion.Flexible)
                {
                    primMesh.twistBegin = primShape.PathTwistBegin*18/10;
                    primMesh.twistEnd = primShape.PathTwist*18/10;
                    primMesh.taperX = pathScaleX;
                    primMesh.taperY = pathScaleY;

                    if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                    {
                        ReportPrimError("*** CORRUPT PRIM!! ***", primName, primMesh);
                        if (profileBegin < 0.0f) profileBegin = 0.0f;
                        if (profileEnd > 1.0f) profileEnd = 1.0f;
                    }
#if SPAM
                MainConsole.Instance.Debug("****** PrimMesh Parameters (Linear) ******\n" + primMesh.ParamsToDisplayString());
#endif
                    try
                    {
                        primMesh.Extrude(primShape.PathCurve == (byte) Extrusion.Straight
                                             ? PathType.Linear
                                             : PathType.Flexible);
                    }
                    catch (Exception ex)
                    {
                        ReportPrimError("Extrusion failure: exception: " + ex, primName, primMesh);
                        return null;
                    }
                }
                else
                {
                    primMesh.holeSizeX = (200 - primShape.PathScaleX)*0.01f;
                    primMesh.holeSizeY = (200 - primShape.PathScaleY)*0.01f;
                    primMesh.radius = 0.01f*primShape.PathRadiusOffset;
                    primMesh.revolutions = 1.0f + 0.015f*primShape.PathRevolutions;
                    primMesh.skew = 0.01f*primShape.PathSkew;
                    primMesh.twistBegin = primShape.PathTwistBegin*36/10;
                    primMesh.twistEnd = primShape.PathTwist*36/10;
                    primMesh.taperX = primShape.PathTaperX*0.01f;
                    primMesh.taperY = primShape.PathTaperY*0.01f;

                    if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                    {
                        ReportPrimError("*** CORRUPT PRIM!! ***", primName, primMesh);
                        if (profileBegin < 0.0f) profileBegin = 0.0f;
                        if (profileEnd > 1.0f) profileEnd = 1.0f;
                    }
#if SPAM
                MainConsole.Instance.Debug("****** PrimMesh Parameters (Circular) ******\n" + primMesh.ParamsToDisplayString());
#endif
                    try
                    {
                        primMesh.Extrude(PathType.Circular);
                    }
                    catch (Exception ex)
                    {
                        ReportPrimError("Extrusion failure: exception: " + ex, primName, primMesh);
                        return null;
                    }
                }

                primMesh.DumpRaw(baseDir, primName, "primMesh");

                primMesh.Scale(size.X, size.Y, size.Z);

                coords = primMesh.coords;
                faces = primMesh.faces;
                primMesh = null;
            }

            Mesh mesh = new Mesh(key);
            //mesh.m_triangles = faces;
            //mesh.m_vertices = coords;
            // Add the corresponding triangles to the mesh
            mesh.Set(coords, faces);
            coords.Clear();
            faces.Clear();
            coords = null;
            faces = null;
            return mesh;
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

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical)
        {
            Mesh mesh = null;
            ulong key = primShape.GetMeshKey(size, lod);

            if (size.X < 0.01f) size.X = 0.01f;
            if (size.Y < 0.01f) size.Y = 0.01f;
            if (size.Z < 0.01f) size.Z = 0.01f;

            if ((!isPhysical) && size.X < minSizeForComplexMesh && size.Y < minSizeForComplexMesh &&
                size.Z < minSizeForComplexMesh)
                mesh = CreateBoundingBoxMesh(size, key);
            else
                mesh = CreateMeshFromPrimMesher(primName, primShape, size, lod, key);

            return mesh;
        }
    }
}
