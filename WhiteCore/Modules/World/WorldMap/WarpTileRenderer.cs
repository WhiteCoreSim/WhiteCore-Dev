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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Rendering;
using OpenMetaverse.StructuredData;
using Warp3Dw;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Utilities;
using WhiteCore.Modules.WorldMap.Warp3DMap;
using RegionSettings = WhiteCore.Framework.SceneInfo.RegionSettings;
using WarpRenderer = Warp3Dw.Warp3D;

namespace WhiteCore.Modules.WorldMap
{
    public class WarpTileRenderer : IMapTileTerrainRenderer
    {
        const float MIN_PRIM_SIZE = 2f;     // minimum size of a prim before it is rendered

        static readonly Color4 WATER_COLOR = new Color4 (29, 72, 96, 216);
        static readonly Color4 OPAQUE_WATER_COLOR = new Color4 (34, 92, 114, 255);
        //static readonly Color4 SKY_COLOR = new Color4(106, 178, 236, 216);
        static readonly int SKYCOLOR = 0x8BC4EC;

        readonly Dictionary<UUID, Color4> m_colors = new Dictionary<UUID, Color4> ();
        IConfigSource m_config;
        string m_assetCacheDir = "";
        IRendering m_primMesher;
        IScene m_scene;
        IJ2KDecoder m_imgDecoder;

        bool m_drawPrimVolume = true;   // true if should render the prims on the tile
        bool m_textureTerrain = true;   // true if to create terrain splatting texture
        bool m_texturePrims = true;     // true if should texture the rendered prims
        float m_texturePrimSize = 4f;   // size of prim before we consider texturing it
        bool m_renderMeshes = true;     // true if to render meshes rather than just bounding boxes

        #region IMapTileTerrainRenderer Members

        public void Initialise (IScene scene, IConfigSource config)
        {
            m_scene = scene;
            m_imgDecoder = m_scene.RequestModuleInterface<IJ2KDecoder> ();
            m_config = config;
            m_assetCacheDir = m_config.Configs ["AssetCache"].GetString ("CacheDirectory", m_assetCacheDir);
            if (m_assetCacheDir == "") {
                var defpath = scene.RequestModuleInterface<ISimulationBase> ().DefaultDataPath;
                m_assetCacheDir = System.IO.Path.Combine (defpath, Constants.DEFAULT_ASSETCACHE_DIR);
            }

            List<string> renderers = RenderingLoader.ListRenderers (Util.ExecutingDirectory ());
            if (renderers.Count > 0) {
                m_primMesher = RenderingLoader.LoadRenderer (renderers [0]);
                MainConsole.Instance.Debug ("[WarpTile generator]: Loaded prim mesher " + m_primMesher);
            } else {
                MainConsole.Instance.Info ("[WarpTile generator]: No prim mesher loaded, prim rendering will be disabled");
            }

            var mapConfig = m_config.Configs ["MapModule"];
            if (mapConfig != null) {
                m_texturePrimSize = mapConfig.GetFloat ("TexturePrimSize", m_texturePrimSize);
                m_renderMeshes = mapConfig.GetBoolean ("RenderMeshes", m_renderMeshes);
            }


            ReadCacheMap ();
        }

        // Standard maptile rendering
        public Bitmap TerrainToBitmap (Bitmap mapBmp)
        {
            int scaledRemovalFactor = m_scene.RegionInfo.RegionSizeX / (Constants.RegionSize / 2);
            Vector3 camPos = new Vector3 (
                (m_scene.RegionInfo.RegionSizeX / 2f) - 0.5f,
                (m_scene.RegionInfo.RegionSizeY / 2f) - 0.5f,
                221f); //.7025033688163f);

            Viewport viewport = new Viewport (camPos, -Vector3.UnitZ, 256f, 0.1f,
                                             m_scene.RegionInfo.RegionSizeX - scaledRemovalFactor,
                                             m_scene.RegionInfo.RegionSizeY - scaledRemovalFactor,
                                             m_scene.RegionInfo.RegionSizeX - scaledRemovalFactor,
                                             m_scene.RegionInfo.RegionSizeY - scaledRemovalFactor);

            viewport.FieldOfView = 150;
            viewport.Width = m_scene.RegionInfo.RegionSizeX;
            viewport.Height = m_scene.RegionInfo.RegionSizeY;

            mapBmp = TerrainBitmap (viewport, false);
            return mapBmp;
        }

        public Bitmap TerrainToBitmap (Bitmap mapBmp, int size)
        {
            int scaledRemovalFactor = m_scene.RegionInfo.RegionSizeX / (Constants.RegionSize / 2);

            Vector3 camPos = new Vector3 (
                (m_scene.RegionInfo.RegionSizeX / 2f) - 0.5f,
                (m_scene.RegionInfo.RegionSizeY / 2f) - 0.5f,
                221f);  //.7025033688163f);


            Viewport viewport = new Viewport (camPos, -Vector3.UnitZ, 256f, 0.1f,
                m_scene.RegionInfo.RegionSizeX - scaledRemovalFactor,
                m_scene.RegionInfo.RegionSizeY - scaledRemovalFactor,
                m_scene.RegionInfo.RegionSizeX - scaledRemovalFactor,
                m_scene.RegionInfo.RegionSizeY - scaledRemovalFactor);

            viewport.FieldOfView = 150;

            //testing
            //viewport.Height = size;
            //viewport.Width = size;

            mapBmp = TerrainBitmap (viewport, false);
            return mapBmp;

        }

        public Bitmap TerrainBitmap (Viewport viewport, bool threeD)
        {
            // AntiAliasing
            int width = viewport.Width * 2;
            int height = viewport.Height * 2;

            WarpRenderer renderer = new WarpRenderer ();
            if (!renderer.CreateScene (width, height)) {
                MainConsole.Instance.Error ("[Warp3D]: Unable to create the required scene! Maybe lack of RAM?");
                return new Bitmap (Constants.RegionSize, Constants.RegionSize, PixelFormat.Format24bppRgb);
            }
            renderer.Scene.autoCalcNormals = false;
            if (threeD)
                renderer.SetBackgroundColor (SKYCOLOR);

            #region Camera

            warp_Vector pos = ConvertVector (viewport.Position);
            pos.z -= 0.001f; // Works around an issue with the Warp3D camera
            warp_Vector lookat = warp_Vector.add (ConvertVector (viewport.Position), ConvertVector (viewport.LookDirection));

            renderer.Scene.defaultCamera.setPos (pos);
            renderer.Scene.defaultCamera.lookAt (lookat);

            if (viewport.Orthographic) {
                renderer.Scene.defaultCamera.isOrthographic = true;
                if (viewport.OrthoWindowWidth <= viewport.OrthoWindowHeight) {
                    renderer.Scene.defaultCamera.orthoViewWidth = viewport.OrthoWindowWidth;
                    renderer.Scene.defaultCamera.orthoViewHeight = viewport.OrthoWindowWidth;
                } else {
                    renderer.Scene.defaultCamera.orthoViewWidth = viewport.OrthoWindowHeight;
                    renderer.Scene.defaultCamera.orthoViewHeight = viewport.OrthoWindowHeight;
                }
            } else {
                viewport.Orthographic = false;
                float fov = viewport.FieldOfView;
                renderer.Scene.defaultCamera.setFov (fov);
            }

            #endregion Camera

            renderer.Scene.addLight ("Light1", new warp_Light (new warp_Vector (1.0f, 0.5f, 1f), 0xffffff, 0, 320, 40));
            renderer.Scene.addLight ("Light2", new warp_Light (new warp_Vector (-1f, -1f, 1f), 0xffffff, 0, 100, 40));


            try {
                CreateWater (renderer, threeD);
                CreateTerrain (renderer, m_textureTerrain);

                if (m_drawPrimVolume && m_primMesher != null) {
                    foreach (ISceneChildEntity part in m_scene.Entities.GetEntities ().SelectMany (ent => ent.ChildrenEntities ()))
                        CreatePrim (renderer, part);
                }

            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[Warp3D]: Exception in the map generation, " + ex);
            }

            renderer.Render ();
            Bitmap bitmap = renderer.Scene.getImage ();

            // AntiAliasing
            using (Bitmap origBitmap = bitmap)
                bitmap = ImageUtils.ResizeImage (origBitmap, viewport.Width, viewport.Height);


            // Clean up
            SaveCache ();
            foreach (var o in renderer.Scene.objectData.Values) {
                warp_Object obj = (warp_Object)o;
                obj.vertexData = null;
                obj.triangleData = null;
            }

            renderer.Scene.removeAllObjects ();
            renderer.Reset ();
            m_colors.Clear ();

            //Force GC to try to clean this mess up
            GC.Collect ();

            return bitmap;
        }


        public Bitmap CreateViewImage (Vector3 camPos, Vector3 camDir, float fov, int width, int height, bool useTextures)
        {
            Viewport viewport = new Viewport (camPos, camDir, fov, 1024f, 0.1f, width, height);
            //             Viewport viewport = new Viewport(camPos, camDir, fov, Constants.RegionSize,  0.1f, width, height);
            return TerrainBitmap (viewport, true);
        }


        #endregion

        #region Rendering Methods

        void CreateWater (WarpRenderer renderer, bool threeD)
        {
            float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
            int maxSize = m_scene.RegionInfo.RegionSizeX;
            if (m_scene.RegionInfo.RegionSizeY >= maxSize)
                maxSize = m_scene.RegionInfo.RegionSizeY;

            warp_Material waterColormaterial;
            if (!threeD) {
                // 20160210 -greythane-
                // it appears that the default plan object is rotated 45 degrees
                // work-a-round until verified or otherwise in the Warp3D library
                renderer.AddPlane ("Water", maxSize);
                renderer.Scene.sceneobject ("Water").setPos (0, waterHeight, 0);

                /* reference    if(m_scene.RegionInfo.RegionSizeX >= m_scene.RegionInfo.RegionSizeY)
                                    renderer.AddPlane ("Water", m_scene.RegionInfo.RegionSizeX/2);
                                else
                                    renderer.AddPlane ("Water", m_scene.RegionInfo.RegionSizeY/2);

                                renderer.Scene.sceneobject ("Water").setPos (
                                    (m_scene.RegionInfo.RegionSizeX / 2) - 0.5f,
                                    waterHeight,
                                    (m_scene.RegionInfo.RegionSizeY / 2) - 0.5f);


                */
                waterColormaterial = new warp_Material (ConvertColor (WATER_COLOR));
                //  waterColormaterial.setTransparency ((byte)((1f - WATER_COLOR.A) * 255f) * 2);
                waterColormaterial.setTransparency ((byte)((1f - WATER_COLOR.A) * 255f));
            } else {
                renderer.AddPlane ("Water", maxSize / 2);

                /* for reference
                                if(m_scene.RegionInfo.RegionSizeX >= m_scene.RegionInfo.RegionSizeY)
                                    renderer.AddPlane ("Water", m_scene.RegionInfo.RegionSizeX/2);
                                else
                                    renderer.AddPlane ("Water", m_scene.RegionInfo.RegionSizeY/2);
                */
                renderer.Scene.sceneobject ("Water").setPos (
                    (m_scene.RegionInfo.RegionSizeX / 2f) - 0.5f,
                    -0.5f,
                    waterHeight + 5.1f
                    );

                waterColormaterial = new warp_Material (ConvertColor (OPAQUE_WATER_COLOR));
                waterColormaterial.setTransparency (48);
                //waterColormaterial.opaque = true;
            }

            waterColormaterial.setReflectivity (0);
            renderer.Scene.addMaterial ("WaterColor", waterColormaterial);
            renderer.SetObjectMaterial ("Water", "WaterColor");
        }

        warp_Object CreateTerrain (WarpRenderer renderer, bool textureTerrain)
        {
            ITerrainChannel terrain = m_scene.RequestModuleInterface<ITerrainChannel> ();

            float diffX = 1.0f; //(float) m_scene.RegionInfo.RegionSizeX/(float) Constants.RegionSize;
            float diffY = 1.0f; //(float) m_scene.RegionInfo.RegionSizeY/(float) Constants.RegionSize;
            int newRsX = m_scene.RegionInfo.RegionSizeX / (int)diffX;
            int newRsY = m_scene.RegionInfo.RegionSizeY / (int)diffY;

            warp_Object obj = new warp_Object (newRsX * newRsY, ((newRsX - 1) * (newRsY - 1) * 2));

            for (float y = 0; y < m_scene.RegionInfo.RegionSizeY; y += diffY) {
                for (float x = 0; x < m_scene.RegionInfo.RegionSizeX; x += diffX) {
                    float t_height = terrain [(int)x, (int)y];
                    float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;

                    //clamp to eliminate artifacts
                    t_height = Utils.Clamp (t_height, waterHeight - 0.5f, waterHeight + 0.5f);
                    if (t_height < 0.0f) t_height = 0.0f;

                    warp_Vector pos = ConvertVector (x / diffX, y / diffY, t_height);
                    obj.addVertex (
                        new warp_Vertex (pos,
                                        x / m_scene.RegionInfo.RegionSizeX,
                                        (m_scene.RegionInfo.RegionSizeY - y) / (m_scene.RegionInfo.RegionSizeY)));
                }
            }

            const float normal_map_reduction = 2.0f; //2.0f-2.5f is the sweet spot

            for (float y = 0; y < m_scene.RegionInfo.RegionSizeY; y += diffY) {
                for (float x = 0; x < m_scene.RegionInfo.RegionSizeX; x += diffX) {
                    float newX = x / diffX;
                    float newY = y / diffY;

                    if (newX < newRsX - 1 && newY < newRsY - 1) {
                        int v = (int)(newY * newRsX + newX);

                        // Normal
                        Vector3 v1 = new Vector3 (newX, newY, (terrain [(int)x, (int)y]) / normal_map_reduction);
                        Vector3 v2 = new Vector3 (newX + 1, newY,
                                                 (terrain [(int)x + 1, (int)y]) / normal_map_reduction);
                        Vector3 v3 = new Vector3 (newX, newY + 1,
                                                 (terrain [(int)x, (int)(y + 1)]) / normal_map_reduction);
                        warp_Vector norm = ConvertVector (SurfaceNormal (v1, v2, v3));
                        norm = norm.reverse ();
                        obj.vertex (v).n = norm;

                        // Triangle 1
                        obj.addTriangle (
                            v,
                            v + 1,
                            v + newRsX);

                        // Triangle 2
                        obj.addTriangle (
                            v + newRsX + 1,
                            v + newRsX,
                            v + 1);
                    }
                }
            }

            renderer.Scene.addObject ("Terrain", obj);
            renderer.Scene.sceneobject ("Terrain").setPos (0.0f, 0.0f, 0.0f);

            UUID [] textureIDs = new UUID [4];
            float [] startHeights = new float [4];
            float [] heightRanges = new float [4];

            RegionSettings regionInfo = m_scene.RegionInfo.RegionSettings;

            textureIDs [0] = regionInfo.TerrainTexture1;
            textureIDs [1] = regionInfo.TerrainTexture2;
            textureIDs [2] = regionInfo.TerrainTexture3;
            textureIDs [3] = regionInfo.TerrainTexture4;

            startHeights [0] = (float)regionInfo.Elevation1SW;
            startHeights [1] = (float)regionInfo.Elevation1NW;
            startHeights [2] = (float)regionInfo.Elevation1SE;
            startHeights [3] = (float)regionInfo.Elevation1NE;

            heightRanges [0] = (float)regionInfo.Elevation2SW;
            heightRanges [1] = (float)regionInfo.Elevation2NW;
            heightRanges [2] = (float)regionInfo.Elevation2SE;
            heightRanges [3] = (float)regionInfo.Elevation2NE;

            uint globalX, globalY;
            Utils.LongToUInts (m_scene.RegionInfo.RegionHandle, out globalX, out globalY);

            Bitmap image = TerrainSplat.Splat (terrain, textureIDs, startHeights, heightRanges,
                                              new Vector3d (globalX, globalY, 0.0), m_scene.AssetService, textureTerrain);
            warp_Texture texture = new warp_Texture (image);
            warp_Material material = new warp_Material (texture);
            material.setReflectivity (0); // reduces tile seams a bit thanks lkalif
            renderer.Scene.addMaterial ("TerrainColor", material);
            renderer.SetObjectMaterial ("Terrain", "TerrainColor");

            image.Dispose ();

            return obj;
        }

        static Vector3 SurfaceNormal (Vector3 c1, Vector3 c2, Vector3 c3)
        {
            Vector3 edge1 = new Vector3 (c2.X - c1.X, c2.Y - c1.Y, c2.Z - c1.Z);
            Vector3 edge2 = new Vector3 (c3.X - c1.X, c3.Y - c1.Y, c3.Z - c1.Z);

            Vector3 normal = Vector3.Cross (edge1, edge2);
            normal.Normalize ();

            return normal;
        }

        void CreatePrim (WarpRenderer renderer, ISceneChildEntity prim)
        {
            try {

                if ((PCode)prim.Shape.PCode != PCode.Prim)
                    return;
                if (prim.Scale.LengthSquared () < MIN_PRIM_SIZE * MIN_PRIM_SIZE)
                    return;

                Primitive omvPrim = prim.Shape.ToOmvPrimitive (prim.OffsetPosition, prim.GetRotationOffset ());
                FacetedMesh renderMesh = null;

                // Are we dealing with a sculptie or mesh?
                if (omvPrim.Sculpt != null && omvPrim.Sculpt.SculptTexture != UUID.Zero) {
                    // Try fetching the asset
                    byte [] sculptAsset = m_scene.AssetService.GetData (omvPrim.Sculpt.SculptTexture.ToString ());
                    if (sculptAsset != null) {
                        // Is it a mesh?
                        if (omvPrim.Sculpt.Type == SculptType.Mesh) {
                            AssetMesh meshAsset = new AssetMesh (omvPrim.Sculpt.SculptTexture, sculptAsset);
                            FacetedMesh.TryDecodeFromAsset (omvPrim, meshAsset, DetailLevel.Highest, out renderMesh);
                            meshAsset = null;
                        } else // It's sculptie
                          {
                            Image sculpt = m_imgDecoder.DecodeToImage (sculptAsset);
                            if (sculpt != null) {
                                renderMesh = m_primMesher.GenerateFacetedSculptMesh (omvPrim, (Bitmap)sculpt,
                                                                                    DetailLevel.Medium);
                                sculpt.Dispose ();
                            }
                        }
                        sculptAsset = null;
                    } else {
                        // missing sculpt data... replace with something
                        renderMesh = m_primMesher.GenerateFacetedMesh (omvPrim, DetailLevel.Medium);
                    }

                } else // Prim
                  {
                    renderMesh = m_primMesher.GenerateFacetedMesh (omvPrim, DetailLevel.Medium);
                }

                if (renderMesh == null)
                    return;

                warp_Vector primPos = ConvertVector (prim.GetWorldPosition ());
                warp_Quaternion primRot = ConvertQuaternion (prim.GetRotationOffset ());

                warp_Matrix m = warp_Matrix.quaternionMatrix (primRot);

                if (prim.ParentID != 0) {
                    ISceneEntity group = m_scene.GetGroupByPrim (prim.LocalId);
                    if (group != null)
                        m.transform (warp_Matrix.quaternionMatrix (ConvertQuaternion (group.RootChild.GetRotationOffset ())));
                }

                warp_Vector primScale = ConvertVector (prim.Scale);

                string primID = prim.UUID.ToString ();

                // Create the prim faces
                for (int i = 0; i < renderMesh.Faces.Count; i++) {
                    Face renderFace = renderMesh.Faces [i];
                    string meshName = primID + "-Face-" + i;

                    warp_Object faceObj = new warp_Object (renderFace.Vertices.Count, renderFace.Indices.Count / 3);

                    foreach (Vertex v in renderFace.Vertices) {
                        warp_Vector pos = ConvertVector (v.Position);
                        warp_Vector norm = ConvertVector (v.Normal);

                        if (prim.Shape.SculptTexture == UUID.Zero)
                            norm = norm.reverse ();
                        warp_Vertex vert = new warp_Vertex (pos, norm, v.TexCoord.X, v.TexCoord.Y);

                        faceObj.addVertex (vert);
                    }

                    for (int j = 0; j < renderFace.Indices.Count;) {
                        faceObj.addTriangle (
                            renderFace.Indices [j++],
                            renderFace.Indices [j++],
                            renderFace.Indices [j++]);
                    }

                    Primitive.TextureEntryFace teFace = prim.Shape.Textures.GetFace ((uint)i);
                    string materialName;
                    Color4 faceColor = GetFaceColor (teFace);

                    if (m_texturePrims && (prim.Scale.LengthSquared () > m_texturePrimSize)) {
                        materialName = GetOrCreateMaterial (renderer, faceColor, teFace.TextureID);
                    } else {
                        materialName = GetOrCreateMaterial (renderer, faceColor);
                    }

                    faceObj.transform (m);
                    faceObj.setPos (primPos);
                    faceObj.scaleSelf (primScale.x, primScale.y, primScale.z);

                    renderer.Scene.addObject (meshName, faceObj);

                    renderer.SetObjectMaterial (meshName, materialName);

                    faceObj = null;
                }
                renderMesh.Faces.Clear ();
                renderMesh = null;
            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[Warp3D]: Exception creating prim, " + ex);
            }
        }

        Color4 GetFaceColor (Primitive.TextureEntryFace textureFace)
        {
            Color4 color;

            if (textureFace.TextureID == UUID.Zero)
                return textureFace.RGBA;

            if (!m_colors.TryGetValue (textureFace.TextureID, out color)) {
                // Fetch the texture, decode and get the average color,
                // then save it to a temporary metadata asset
                byte [] textureAsset = m_scene.AssetService.GetData (textureFace.TextureID.ToString ());
                if (textureAsset == null || textureAsset.Length == 0) {
                    textureAsset = m_scene.AssetService.GetData (Constants.MISSING_TEXTURE_ID);      // not found, replace with something identifable
                    if (textureAsset == null || textureAsset.Length == 0)                           // no data.
                        color = new Color4 (1.0f, 0.0f, 0.5f, 1.0f);
                } else {
                    color = GetAverageColor (textureFace.TextureID, textureAsset, m_scene);
                }

                m_colors [textureFace.TextureID] = color;
            }

            return color * textureFace.RGBA;
        }

        string GetOrCreateMaterial (WarpRenderer renderer, Color4 color)
        {
            string name = color.ToString ();

            warp_Material material = renderer.Scene.material (name);
            if (material != null)
                return name;

            renderer.AddMaterial (name, ConvertColor (color));
            if (color.A < 1f)
                renderer.Scene.material (name).setTransparency ((byte)((1f - color.A) * 255f));
            return name;
        }

        public string GetOrCreateMaterial (WarpRenderer renderer, Color4 faceColor, UUID textureID)
        {
            string materialName = "Color-" + faceColor + "-Texture-" + textureID;

            if (renderer.Scene.material (materialName) == null) {
                MainConsole.Instance.DebugFormat ("Creating material {0}", materialName);
                renderer.AddMaterial (materialName, ConvertColor (faceColor));
                if (faceColor.A < 1f) {
                    renderer.Scene.material (materialName).setTransparency ((byte)((1f - faceColor.A) * 255f));
                }
                warp_Texture texture = GetTexture (textureID);
                if (texture != null)
                    renderer.Scene.material (materialName).setTexture (texture);
            }

            return materialName;
        }

        warp_Texture GetTexture (UUID id)
        {
            warp_Texture ret = null;

            if (id == UUID.Zero)
                id = (UUID)Constants.MISSING_TEXTURE_ID;

            byte [] assetData = m_scene.AssetService.GetData (id.ToString ());
            if (assetData == null || assetData.Length == 0)
                assetData = m_scene.AssetService.GetData (Constants.MISSING_TEXTURE_ID);              // not found, replace with something identifable
            if (assetData != null && assetData.Length > 0) {
                IJ2KDecoder imgDecoder = m_scene.RequestModuleInterface<IJ2KDecoder> ();
                Bitmap img = (Bitmap)imgDecoder.DecodeToImage (assetData);

                if (img != null) {
                    ret = new warp_Texture (img);
                    img.Dispose ();
                    return ret;
                }
            }
            MainConsole.Instance.Debug ("Gettexture returning null, asset id: " + id);
            return ret;
        }

        #endregion Rendering Methods

        #region Cache methods

        void ReadCacheMap ()
        {
            if (!Directory.Exists (m_assetCacheDir))
                Directory.CreateDirectory (m_assetCacheDir);
            if (!Directory.Exists (System.IO.Path.Combine (m_assetCacheDir, "mapTileTextureCache")))
                Directory.CreateDirectory (System.IO.Path.Combine (m_assetCacheDir, "mapTileTextureCache"));

            FileStream stream =
                new FileStream (
                    System.IO.Path.Combine (System.IO.Path.Combine (m_assetCacheDir, "mapTileTextureCache"),
                                           m_scene.RegionInfo.RegionName + ".tc"), FileMode.OpenOrCreate);
            StreamReader m_streamReader = new StreamReader (stream);
            string file = "";
            try {
                file = m_streamReader.ReadToEnd ();
            } catch {
            }
            m_streamReader.Close ();
            stream.Close ();

            //Read file here
            if (file != "") //New file
            {
                bool loaded = DeserializeCache (file);
                if (!loaded) {
                    //Something went wrong, delete the file
                    try {
                        File.Delete (System.IO.Path.Combine (System.IO.Path.Combine (m_assetCacheDir, "mapTileTextureCache"),
                                                           m_scene.RegionInfo.RegionName + ".tc"));
                    } catch {
                    }
                }
            }
        }

        bool DeserializeCache (string file)
        {
            OSDMap map = OSDParser.DeserializeJson (file) as OSDMap;
            if (map == null)
                return false;

            foreach (KeyValuePair<string, OSD> kvp in map) {
                Color4 c = kvp.Value.AsColor4 ();
                UUID key = UUID.Parse (kvp.Key);
                if (!m_colors.ContainsKey (key))
                    m_colors.Add (key, c);
            }

            return true;
        }

        void SaveCache ()
        {
            OSDMap map = SerializeCache ();
            FileStream stream =
                new FileStream (
                    System.IO.Path.Combine (System.IO.Path.Combine (m_assetCacheDir, "mapTileTextureCache"),
                                           m_scene.RegionInfo.RegionName + ".tc"), FileMode.Create);
            StreamWriter writer = new StreamWriter (stream);
            try {
                writer.WriteLine (OSDParser.SerializeJsonString (map));
            } catch {
            }
            writer.Close ();
        }

        OSDMap SerializeCache ()
        {
            OSDMap map = new OSDMap ();
            foreach (KeyValuePair<UUID, Color4> kvp in m_colors) {
                map.Add (kvp.Key.ToString (), kvp.Value);
            }
            return map;
        }

        #endregion

        #region Static Helpers

        static warp_Vector ConvertVector (float x, float y, float z)
        {
            return new warp_Vector (x, z, y);
        }

        static warp_Vector ConvertVector (Vector3 vector)
        {
            return new warp_Vector (vector.X, vector.Z, vector.Y);
        }

        static warp_Quaternion ConvertQuaternion (Quaternion quat)
        {
            return new warp_Quaternion (quat.X, quat.Z, quat.Y, -quat.W);
        }

        static int ConvertColor (Color4 color)
        {
            int c = warp_Color.getColor ((byte)(color.R * 255f), (byte)(color.G * 255f), (byte)(color.B * 255f));
            if (color.A < 1f)
                c |= (byte)(color.A * 255f) << 24;

            return c;
        }

        public static Color4 GetAverageColor (UUID textureID, byte [] j2kData, IScene scene)
        {
            ulong r = 0;
            ulong g = 0;
            ulong b = 0;
            ulong a = 0;
            Bitmap bitmap = null;
            try {

                if (j2kData.Length == 0)
                    return new Color4 (1.0f, 0.0f, 1.0f, 1.0f);

                IJ2KDecoder decoder = scene.RequestModuleInterface<IJ2KDecoder> ();
                bitmap = (Bitmap)decoder.DecodeToImage (j2kData);
                if (bitmap == null)
                    return new Color4 (1.0f, 0.0f, 0.5f, 1.0f);

                j2kData = null;
                int width = bitmap.Width;
                int height = bitmap.Height;

                BitmapData bitmapData = bitmap.LockBits (new Rectangle (0, 0, width, height), ImageLockMode.ReadOnly,
                                                        bitmap.PixelFormat);
                int pixelBytes = (bitmap.PixelFormat == PixelFormat.Format24bppRgb) ? 3 : 4;
                bool hasAlpha = (pixelBytes == 4);

                // Sum up the individual channels
                unsafe
                {
                    for (int y = 0; y < height; y++) {
                        byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                        for (int x = 0; x < width; x++) {
                            b += row [x * pixelBytes + 0];
                            g += row [x * pixelBytes + 1];
                            r += row [x * pixelBytes + 2];
                            if (hasAlpha)
                                a += row [x * pixelBytes + 3];
                        }
                    }
                }

                // Get the averages for each channel
                const decimal OO_255 = 1m / 255m;
                decimal totalPixels = (width * height);

                decimal rm = (r / totalPixels) * OO_255;
                decimal gm = (g / totalPixels) * OO_255;
                decimal bm = (b / totalPixels) * OO_255;
                decimal am;
                if (hasAlpha)
                    am = (a / totalPixels) * OO_255;
                else
                    am = 1m;

                return new Color4 ((float)rm, (float)gm, (float)bm, (float)am);
            } catch (Exception ex) {
                MainConsole.Instance.WarnFormat ("[WarpTile generator]: Error decoding JPEG2000 texture {0} ({1} bytes): {2}",
                                                textureID,
                                                j2kData.Length, ex.Message);
                return new Color4 (0.5f, 0.5f, 0.5f, 1.0f);
            } finally {
                if (bitmap != null)
                    bitmap.Dispose ();
            }
        }

        #endregion Static Helpers
    }

    public static class ImageUtils
    {
        /// <summary>
        ///     Performs bilinear interpolation between four values
        /// </summary>
        /// <param name="v00">First, or top left value</param>
        /// <param name="v01">Second, or top right value</param>
        /// <param name="v10">Third, or bottom left value</param>
        /// <param name="v11">Fourth, or bottom right value</param>
        /// <param name="xPercent">Interpolation value on the X axis, between 0.0 and 1.0</param>
        /// <param name="yPercent">Interpolation value on the Y axis, between 0.0 and 1.0</param>
        /// <returns>The bilinear interpolated result</returns>
        public static float Bilinear (float v00, float v01, float v10, float v11, float xPercent, float yPercent)
        {
            return Utils.Lerp (Utils.Lerp (v00, v01, xPercent), Utils.Lerp (v10, v11, xPercent), yPercent);
        }

        /// <summary>
        ///     Performs a high quality image resize
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        /// <returns>Resized image</returns>
        public static Bitmap ResizeImage (Image image, int width, int height)
        {
            Bitmap result = new Bitmap (width, height);

            using (Graphics graphics = Graphics.FromImage (result)) {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage (image, 0, 0, result.Width, result.Height);
            }
            image.Dispose ();

            return result;
        }
    }
}
