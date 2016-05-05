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
using System.IO;
using System.Linq;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Utilities;
using WhiteCore.Region;

namespace WhiteCore.Services
{
    public class InventoryCAPS : IExternalCapsRequestHandler
    {
        #region Static Constructor

        static InventoryCAPS ()
        {
            Framework.Serialization.SceneEntitySerializer.SceneObjectSerializer =
                new Region.Serialization.SceneObjectSerializer ();
        }

        #endregion

        IAssetService m_assetService;
        IInventoryService m_inventoryService;
        ILibraryService m_libraryService;
        IMoneyModule m_moneyModule;
        UUID m_agentID;
        IInventoryData m_inventoryData;
        List<string> m_uris = new List<string> ();

        #region ICapsServiceConnector Members

        public string Name { get { return GetType ().Name; } }

        public void IncomingCapsRequest (UUID agentID, Framework.Services.GridRegion region, ISimulationBase simbase, ref OSDMap capURLs)
        {
            m_agentID = agentID;
            m_moneyModule = simbase.ApplicationRegistry.RequestModuleInterface<IMoneyModule> ();
            m_assetService = simbase.ApplicationRegistry.RequestModuleInterface<IAssetService> ();
            m_inventoryService = simbase.ApplicationRegistry.RequestModuleInterface<IInventoryService> ();
            m_libraryService = simbase.ApplicationRegistry.RequestModuleInterface<ILibraryService> ();
            m_inventoryData = Framework.Utilities.DataManager.RequestPlugin<IInventoryData> ();

            HttpServerHandle method;
            string uri;

            method = (path, request, httpRequest, httpResponse) => HandleFetchInventoryDescendents (request, m_agentID);
            uri = "/CAPS/FetchInventoryDescendents/" + UUID.Random () + "/";
            capURLs ["WebFetchInventoryDescendents"] = MainServer.Instance.ServerURI + uri;
            capURLs ["FetchInventoryDescendents"] = MainServer.Instance.ServerURI + uri;
            capURLs ["FetchInventoryDescendents2"] = MainServer.Instance.ServerURI + uri;
            m_uris.Add (uri);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uri, method));

            method = (path, request, httpRequest, httpResponse) => HandleFetchLibDescendents (request, m_agentID);
            uri = "/CAPS/FetchLibDescendents/" + UUID.Random () + "/";
            capURLs ["FetchLibDescendents"] = MainServer.Instance.ServerURI + uri;
            capURLs ["FetchLibDescendents2"] = MainServer.Instance.ServerURI + uri;
            m_uris.Add (uri);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uri, method));

            method = (path, request, httpRequest, httpResponse) => HandleFetchInventory (request, m_agentID);
            uri = "/CAPS/FetchInventory/" + UUID.Random () + "/";
            capURLs ["FetchInventory"] = MainServer.Instance.ServerURI + uri;
            capURLs ["FetchInventory2"] = MainServer.Instance.ServerURI + uri;
            m_uris.Add (uri);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uri, method));

            method = (path, request, httpRequest, httpResponse) => HandleFetchLib (request, m_agentID);
            uri = "/CAPS/FetchLib/" + UUID.Random () + "/";
            capURLs ["FetchLib"] = MainServer.Instance.ServerURI + uri;
            capURLs ["FetchLib2"] = MainServer.Instance.ServerURI + uri;
            m_uris.Add (uri);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uri, method));


            uri = "/CAPS/NewFileAgentInventory/" + UUID.Random () + "/";
            capURLs ["NewFileAgentInventory"] = MainServer.Instance.ServerURI + uri;
            m_uris.Add (uri);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uri, NewAgentInventoryRequest));

            uri = "/CAPS/NewFileAgentInventoryVariablePrice/" + UUID.Random () + "/";
            capURLs ["NewFileAgentInventoryVariablePrice"] = MainServer.Instance.ServerURI + uri;
            m_uris.Add (uri);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uri, NewAgentInventoryRequestVariablePrice));

            uri = "/CAPS/CreateInventoryCategory/" + UUID.Random () + "/";
            capURLs ["CreateInventoryCategory"] = MainServer.Instance.ServerURI + uri;
            m_uris.Add (uri);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uri, CreateInventoryCategory));
        }

        public void IncomingCapsDestruction ()
        {
            foreach (string uri in m_uris)
                MainServer.Instance.RemoveStreamHandler ("POST", uri);
        }

        #endregion

        #region Inventory

        public byte [] HandleFetchInventoryDescendents (Stream request, UUID agentID)
        {
            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
            OSDArray foldersrequested = (OSDArray)map ["folders"];
            try {
                //MainConsole.Instance.DebugFormat("[InventoryCAPS]: Received WebFetchInventoryDescendents request for {0}", AgentID);
                return m_inventoryData.FetchInventoryReply (foldersrequested, agentID,
                    UUID.Zero, m_libraryService.LibraryOwner);

            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[InventoryCAPS]: SERIOUS ISSUE! " + ex);
            } finally {
                map = null;
                foldersrequested = null;
            }

            OSDMap rmap = new OSDMap ();
            rmap ["folders"] = new OSDArray ();
            return OSDParser.SerializeLLSDXmlBytes (rmap);
        }

        public byte [] HandleFetchLibDescendents (Stream request, UUID agentID)
        {
            try {
                //MainConsole.Instance.DebugFormat("[InventoryCAPS]: Received FetchLibDescendents request for {0}", agentID);
                OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
                OSDArray foldersrequested = (OSDArray)map ["folders"];

                return m_inventoryData.FetchInventoryReply (foldersrequested,
                    m_libraryService.LibraryOwner,
                    agentID, m_libraryService.LibraryOwner);

            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[InventoryCAPS]: SERIOUS ISSUE! " + ex);
            }

            OSDMap rmap = new OSDMap ();
            rmap ["folders"] = new OSDArray ();
            return OSDParser.SerializeLLSDXmlBytes (rmap);
        }

        public byte [] HandleFetchInventory (Stream request, UUID agentID)
        {
            try {
                //MainConsole.Instance.DebugFormat("[InventoryCAPS]: Received FetchInventory request for {0}", agentID);
                OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
                if (requestmap ["items"].Type == OSDType.Unknown)
                    return MainServer.BadRequest;

                OSDArray foldersrequested = (OSDArray)requestmap ["items"];
                OSDMap map = new OSDMap { { "agent_id", OSD.FromUUID (agentID) } };
                //We have to send the agent_id in the main map as well as all the items

                OSDArray items = new OSDArray ();
                foreach (
                    OSDArray item in
                        foldersrequested.Cast<OSDMap> ()
                                        .Select (requestedFolders => requestedFolders ["item_id"].AsUUID ())
                                        .Select (item_id => m_inventoryService.GetOSDItem (m_agentID, item_id))
                                        .Where (item => item != null && item.Count > 0)) {
                    items.Add (item [0]);
                }
                map.Add ("items", items);

                byte [] response = OSDParser.SerializeLLSDXmlBytes (map);
                map.Clear ();
                return response;

            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[InventoryCAPS]: SERIOUS ISSUE! " + ex);
            }

            OSDMap rmap = new OSDMap ();
            rmap ["items"] = new OSDArray ();
            return OSDParser.SerializeLLSDXmlBytes (rmap);
        }

        public byte [] HandleFetchLib (Stream request, UUID agentID)
        {
            try {
                //MainConsole.Instance.DebugFormat("[InventoryCAPS]: Received FetchLib request for {0}", agentID);
                OSDMap requestmap = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
                OSDArray foldersrequested = (OSDArray)requestmap ["items"];
                OSDMap map = new OSDMap { { "agent_id", OSD.FromUUID (agentID) } };
                OSDArray items = new OSDArray ();

                foreach (
                    OSDArray item in
                        foldersrequested.Cast<OSDMap> ()
                                        .Select (requestedFolders => requestedFolders ["item_id"].AsUUID ())
                                        .Select (item_id => m_inventoryService.GetOSDItem (UUID.Zero, item_id))
                                        .Where (item => item != null && item.Count > 0)) {
                    items.Add (item [0]);
                }
                map.Add ("items", items);

                byte [] response = OSDParser.SerializeLLSDXmlBytes (map);
                map.Clear ();
                return response;

            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[InventoryCAPS]: SERIOUS ISSUE! " + ex);
            }

            OSDMap rmap = new OSDMap ();
            rmap ["items"] = new OSDArray ();
            return OSDParser.SerializeLLSDXmlBytes (rmap);
        }

        #endregion

        #region Inventory upload

        /// <summary>
        ///     This handles the uploading of some inventory types
        /// </summary>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <param name="httpRequest"></param>
        /// <param name="httpResponse"></param>
        /// <returns></returns>
        public byte [] NewAgentInventoryRequest (string path, Stream request, OSHttpRequest httpRequest,
                                                OSHttpResponse httpResponse)
        {
            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
            string asset_type = map ["asset_type"].AsString ();
            if (!ChargeUser (asset_type, map)) {
                map = new OSDMap ();
                map ["uploader"] = "";
                map ["state"] = "error";
                return OSDParser.SerializeLLSDXmlBytes (map);
            }
            return OSDParser.SerializeLLSDXmlBytes (InternalNewAgentInventoryRequest (map, httpRequest, httpResponse));
        }

        public byte [] NewAgentInventoryRequestVariablePrice (string path, Stream request, OSHttpRequest httpRequest,
                                                             OSHttpResponse httpResponse)
        {
            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
            string asset_type = map ["asset_type"].AsString ();
            int charge = 0;
            int resourceCost;
            if (!ChargeUser (asset_type, map, out charge, out resourceCost)) {
                map = new OSDMap ();
                map ["uploader"] = "";
                map ["state"] = "error";
                return OSDParser.SerializeLLSDXmlBytes (map);
            }
            OSDMap resp = InternalNewAgentInventoryRequest (map, httpRequest, httpResponse);

            resp ["resource_cost"] = resourceCost;
            resp ["upload_price"] = charge; //Set me if you want to use variable cost stuff

            return OSDParser.SerializeLLSDXmlBytes (map);
        }

        bool ChargeUser (string assetType, OSDMap map)
        {
            int charge, cost;
            return ChargeUser (assetType, map, out charge, out cost);
        }

        bool ChargeUser (string assetType, OSDMap map, out int charge, out int resourceCost)
        {
            charge = 0;
            resourceCost = 0;

            if (m_moneyModule != null) {
                if (assetType == "texture" ||
                    assetType == "animation" ||
                    assetType == "snapshot" ||
                    assetType == "sound") {
                    charge = m_moneyModule.UploadCharge;
                } else if (assetType == "mesh" ||
                         assetType == "object") {
                    OSDMap meshMap = (OSDMap)map ["asset_resources"];
                    //OSDArray instance_list = (OSDArray)meshMap["instance_list"];
                    int mesh_list = meshMap.ContainsKey ("mesh_list") ? ((OSDArray)meshMap ["mesh_list"]).Count : 1;
                    int texture_list = meshMap.ContainsKey ("texture_list")
                                           ? ((OSDArray)meshMap ["texture_list"]).Count
                                           : 1;
                    if (texture_list == 0)
                        texture_list = 1;
                    if (mesh_list == 0)
                        mesh_list = 1;
                    charge = texture_list * m_moneyModule.UploadCharge +
                    mesh_list * m_moneyModule.UploadCharge;
                    resourceCost = mesh_list * m_moneyModule.UploadCharge;
                }
                if (charge > 0 &&
                    !m_moneyModule.Charge (m_agentID, m_moneyModule.UploadCharge, "Upload Charge", TransactionType.UploadCharge))
                    return false;
            }
            return true;
        }

        OSDMap InternalNewAgentInventoryRequest (OSDMap map, OSHttpRequest httpRequest,
                                                 OSHttpResponse httpResponse)
        {
            string asset_type = map ["asset_type"].AsString ();
            //MainConsole.Instance.Info("[CAPS]: NewAgentInventoryRequest Request is: " + map.ToString());
            //MainConsole.Instance.Debug("asset upload request via CAPS" + llsdRequest.inventory_type + " , " + llsdRequest.asset_type);

            string assetName = map ["name"].AsString ();
            string assetDes = map ["description"].AsString ();
            UUID parentFolder = map ["folder_id"].AsUUID ();
            string inventory_type = map ["inventory_type"].AsString ();
            uint everyone_mask = map ["everyone_mask"].AsUInteger ();
            uint group_mask = map ["group_mask"].AsUInteger ();
            uint next_owner_mask = map ["next_owner_mask"].AsUInteger ();

            UUID newAsset = UUID.Random ();
            UUID newInvItem = UUID.Random ();
            string uploadpath = "/CAPS/Upload/" + UUID.Random () + "/";

            AssetUploader uploader =
                new AssetUploader (assetName, assetDes, newAsset, newInvItem, parentFolder, inventory_type,
                    asset_type, uploadpath, everyone_mask,
                    group_mask, next_owner_mask, UploadCompleteHandler);
            MainServer.Instance.AddStreamHandler (new GenericStreamHandler ("POST", uploadpath, uploader.UploaderCaps));

            string uploaderURL = MainServer.Instance.ServerURI + uploadpath;
            map = new OSDMap ();
            map ["uploader"] = uploaderURL;
            map ["state"] = "upload";
            return map;
        }

        public byte [] CreateInventoryCategory (string path, Stream request, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse)
        {
            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml (HttpServerHandlerHelpers.ReadFully (request));
            UUID folder_id = map ["folder_id"].AsUUID ();
            UUID parent_id = map ["parent_id"].AsUUID ();
            int type = map ["type"].AsInteger ();
            string name = map ["name"].AsString ();

            InventoryFolderBase newFolder
                = new InventoryFolderBase (
                      folder_id, name, m_agentID, (short)type, parent_id, 1);
            m_inventoryService.AddFolder (newFolder);
            OSDMap resp = new OSDMap ();
            resp ["folder_id"] = folder_id;
            resp ["parent_id"] = parent_id;
            resp ["type"] = type;
            resp ["name"] = name;

            return OSDParser.SerializeLLSDXmlBytes (map);
        }

        /// <summary>
        /// </summary>
        /// <param name="assetName"></param>
        /// <param name="assetDescription"></param>
        /// <param name="assetID"></param>
        /// <param name="inventoryItem"></param>
        /// <param name="parentFolder"></param>
        /// <param name="data"></param>
        /// <param name="inventoryType"></param>
        /// <param name="assetType"></param>
        /// <param name="everyoneMask"></param>
        /// <param name="groupMask"></param>
        /// <param name="nextOwnerMask"></param>
        public UUID UploadCompleteHandler (string assetName, string assetDescription, UUID assetID,
                                           UUID inventoryItem, UUID parentFolder, byte [] data, string inventoryType,
                                           string assetType, uint everyoneMask, uint groupMask, uint nextOwnerMask)
        {
            sbyte assType = 0;
            sbyte inType = 0;

            switch (inventoryType) {
            case "sound":
                inType = 1;
                assType = 1;
                break;
            case "animation":
                inType = 19;
                assType = 20;
                break;
            case "snapshot":
                inType = 15;
                assType = 0;
                break;
            case "wearable":
                inType = 18;
                switch (assetType) {
                case "bodypart":
                    assType = 13;
                    break;
                case "clothing":
                    assType = 5;
                    break;
                }
                break;
            case "object": {
                    inType = (sbyte)InventoryType.Object;
                    assType = (sbyte)AssetType.Object;

                    List<Vector3> positions = new List<Vector3> ();
                    List<Quaternion> rotations = new List<Quaternion> ();
                    OSDMap request = (OSDMap)OSDParser.DeserializeLLSDXml (data);
                    OSDArray instance_list = (OSDArray)request ["instance_list"];
                    OSDArray mesh_list = (OSDArray)request ["mesh_list"];
                    OSDArray texture_list = (OSDArray)request ["texture_list"];
                    SceneObjectGroup grp = null;

                    List<UUID> textures = new List<UUID> ();
                    foreach (
                            AssetBase textureAsset in
                                texture_list.Select (t => new AssetBase (UUID.Random (), assetName, AssetType.Texture,
                                                                        m_agentID) { Data = t.AsBinary () })) {
                        textureAsset.ID = m_assetService.Store (textureAsset);
                        textures.Add (textureAsset.ID);
                    }

                    InventoryFolderBase meshFolder = m_inventoryService.GetFolderForType (m_agentID, InventoryType.Mesh, FolderType.Mesh);
                    for (int i = 0; i < mesh_list.Count; i++) {
                        PrimitiveBaseShape pbs = PrimitiveBaseShape.CreateBox ();

                        Primitive.TextureEntry textureEntry =
                            new Primitive.TextureEntry (Primitive.TextureEntry.WHITE_TEXTURE);
                        OSDMap inner_instance_list = (OSDMap)instance_list [i];

                        OSDArray face_list = (OSDArray)inner_instance_list ["face_list"];
                        for (uint face = 0; face < face_list.Count; face++) {
                            OSDMap faceMap = (OSDMap)face_list [(int)face];
                            Primitive.TextureEntryFace f = pbs.Textures.CreateFace (face);
                            if (faceMap.ContainsKey ("fullbright"))
                                f.Fullbright = faceMap ["fullbright"].AsBoolean ();
                            if (faceMap.ContainsKey ("diffuse_color"))
                                f.RGBA = faceMap ["diffuse_color"].AsColor4 ();

                            int textureNum = faceMap ["image"].AsInteger ();
                            float imagerot = faceMap ["imagerot"].AsInteger ();
                            float offsets = (float)faceMap ["offsets"].AsReal ();
                            float offsett = (float)faceMap ["offsett"].AsReal ();
                            float scales = (float)faceMap ["scales"].AsReal ();
                            float scalet = (float)faceMap ["scalet"].AsReal ();

                            if (imagerot != 0)
                                f.Rotation = imagerot;
                            if (offsets != 0)
                                f.OffsetU = offsets;
                            if (offsett != 0)
                                f.OffsetV = offsett;
                            if (scales != 0)
                                f.RepeatU = scales;
                            if (scalet != 0)
                                f.RepeatV = scalet;
                            f.TextureID = textures.Count > textureNum
                                                  ? textures [textureNum]
                                                  : Primitive.TextureEntry.WHITE_TEXTURE;
                            textureEntry.FaceTextures [face] = f;
                        }
                        pbs.TextureEntry = textureEntry.GetBytes ();

                        AssetBase meshAsset = new AssetBase (UUID.Random (), assetName, AssetType.Mesh, m_agentID) { Data = mesh_list [i].AsBinary () };
                        meshAsset.ID = m_assetService.Store (meshAsset);

                        if (meshFolder == null) {
                            m_inventoryService.CreateUserInventory (m_agentID, false);
                            meshFolder = m_inventoryService.GetFolderForType (m_agentID, InventoryType.Mesh, FolderType.Mesh);
                        }

                        InventoryItemBase itemBase = new InventoryItemBase (UUID.Random (), m_agentID) {
                            AssetType = (sbyte)AssetType.Mesh,
                            AssetID = meshAsset.ID,
                            CreatorId = m_agentID.ToString (),
                            Folder = meshFolder.ID,
                            InvType = (int)InventoryType.Texture,
                            Name = "(Mesh) - " + assetName,
                            CurrentPermissions = (uint)PermissionMask.All,
                            BasePermissions = (uint)PermissionMask.All,
                            EveryOnePermissions = everyoneMask,
                            GroupPermissions = groupMask,
                            NextPermissions = nextOwnerMask
                        };
                        //Bad... but whatever
                        m_inventoryService.AddItem (itemBase);

                        pbs.SculptEntry = true;
                        pbs.SculptTexture = meshAsset.ID;
                        pbs.SculptType = (byte)SculptType.Mesh;
                        pbs.SculptData = meshAsset.Data;

                        Vector3 position = inner_instance_list ["position"].AsVector3 ();
                        Vector3 scale = inner_instance_list ["scale"].AsVector3 ();
                        Quaternion rotation = inner_instance_list ["rotation"].AsQuaternion ();

                        int physicsShapeType = inner_instance_list ["physics_shape_type"].AsInteger ();
                        // not currently used                        int material = inner_instance_list["material"].AsInteger();
                        // not currently used                        int mesh = inner_instance_list["mesh"].AsInteger();

                        SceneObjectPart prim = new SceneObjectPart (m_agentID, pbs, position, Quaternion.Identity,
                                                   Vector3.Zero, assetName) { Scale = scale, AbsolutePosition = position };

                        rotations.Add (rotation);
                        positions.Add (position);
                        prim.UUID = UUID.Random ();
                        prim.CreatorID = m_agentID;
                        prim.OwnerID = m_agentID;
                        prim.GroupID = UUID.Zero;
                        prim.LastOwnerID = m_agentID;
                        prim.CreationDate = Util.UnixTimeSinceEpoch ();
                        prim.Name = assetName;
                        prim.Description = "";
                        prim.PhysicsType = (byte)physicsShapeType;

                        prim.BaseMask = (uint)PermissionMask.All;
                        prim.EveryoneMask = everyoneMask;
                        prim.NextOwnerMask = nextOwnerMask;
                        prim.GroupMask = groupMask;
                        prim.OwnerMask = (uint)PermissionMask.All;

                        if (grp == null)
                            grp = new SceneObjectGroup (prim, null);
                        else
                            grp.AddChild (prim, i + 1);
                        grp.RootPart.IsAttachment = false;
                    }
                    if (grp != null) {              // unlikely not to have anything but itis possible
                        if (grp.ChildrenList.Count > 1) //Fix first link #
                            grp.RootPart.LinkNum++;

                        Vector3 rootPos = positions [0];
                        grp.SetAbsolutePosition (false, rootPos);
                        for (int i = 0; i < positions.Count; i++) {
                            Vector3 offset = positions [i] - rootPos;
                            grp.ChildrenList [i].SetOffsetPosition (offset);
                        }
                        //grp.Rotation = rotations[0];
                        for (int i = 0; i < rotations.Count; i++) {
                            if (i != 0)
                                grp.ChildrenList [i].SetRotationOffset (false, rotations [i], false);
                        }
                        grp.UpdateGroupRotationR (rotations [0]);
                        data = Encoding.ASCII.GetBytes (grp.ToXml2 ());
                    }
                }
                break;
            }
            AssetBase asset = new AssetBase (assetID, assetName, (AssetType)assType, m_agentID) { Data = data };
            asset.ID = m_assetService.Store (asset);
            assetID = asset.ID;

            InventoryItemBase item = new InventoryItemBase {
                Owner = m_agentID,
                CreatorId = m_agentID.ToString (),
                ID = inventoryItem,
                AssetID = asset.ID,
                Description = assetDescription,
                Name = assetName,
                AssetType = assType,
                InvType = inType,
                Folder = parentFolder,
                CurrentPermissions = (uint)PermissionMask.All,
                BasePermissions = (uint)PermissionMask.All,
                EveryOnePermissions = everyoneMask,
                NextPermissions = nextOwnerMask,
                GroupPermissions = groupMask,
                CreationDate = Util.UnixTimeSinceEpoch ()
            };

            m_inventoryService.AddItem (item);

            return assetID;
        }

        public class AssetUploader
        {
            readonly UUID inventoryItemID;
            readonly string m_assetDes = string.Empty;
            readonly string m_assetName = string.Empty;
            readonly string m_assetType = string.Empty;
            readonly uint m_everyone_mask;
            readonly uint m_group_mask;
            readonly string m_invType = string.Empty;
            readonly uint m_next_owner_mask;
            readonly UUID parentFolder;
            readonly string uploaderPath = string.Empty;
            UUID newAssetID;
            readonly UploadHandler m_uploadCompleteHandler;

            public delegate UUID UploadHandler (string assetName, string description, UUID assetID, UUID inventoryItem,
                                 UUID parentFolderID, byte [] data, string invType, string assetType,
                                 uint everyoneMask, uint groupMask, uint nextOwnerMask);

            public AssetUploader (string assetName, string description, UUID assetID, UUID inventoryItem,
                                  UUID parentFolderID, string invType, string assetType, string path,
                                  uint everyoneMask, uint groupMask, uint nextOwnerMask, UploadHandler action)
            {
                m_assetName = assetName;
                m_assetDes = description;
                newAssetID = assetID;
                inventoryItemID = inventoryItem;
                uploaderPath = path;
                parentFolder = parentFolderID;
                m_assetType = assetType;
                m_invType = invType;
                m_everyone_mask = everyoneMask;
                m_group_mask = groupMask;
                m_next_owner_mask = nextOwnerMask;
                m_uploadCompleteHandler = action;
            }

            /// <summary>
            /// </summary>
            /// <param name="path"></param>
            /// <param name="request"></param>
            /// <param name="httpRequest"></param>
            /// <param name="httpResponse"></param>
            /// <returns></returns>
            public byte [] UploaderCaps (string path, Stream request,
                                        OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                UUID inv = inventoryItemID;
                byte [] data = HttpServerHandlerHelpers.ReadFully (request);
                MainServer.Instance.RemoveStreamHandler ("POST", uploaderPath);

                newAssetID = m_uploadCompleteHandler (m_assetName, m_assetDes, newAssetID, inv, parentFolder,
                    data, m_invType, m_assetType, m_everyone_mask, m_group_mask,
                    m_next_owner_mask);

                OSDMap map = new OSDMap ();
                map ["new_asset"] = newAssetID.ToString ();
                map ["new_inventory_item"] = inv;
                map ["state"] = "complete";

                return OSDParser.SerializeLLSDXmlBytes (map);
            }
        }

        #endregion
    }
}
