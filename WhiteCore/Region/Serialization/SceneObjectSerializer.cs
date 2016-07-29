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
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;

namespace WhiteCore.Region.Serialization
{
    /// <summary>
    ///     Serialize and deserialize scene objects.
    /// </summary>
    /// This should really be in WhiteCore.Framework.Serialization but this would mean circular dependency problems
    /// right now - hopefully this isn't forever.
    public class SceneObjectSerializer : ISceneObjectSerializer
    {
        /// <summary>
        ///     Restore this part from the serialized xml representation.
        /// </summary>
        /// <param name="xmlReader"></param>
        /// <returns></returns>
        protected SceneObjectPart FromXml(XmlTextReader xmlReader)
        {
            SceneObjectPart part = Xml2ToSOP(xmlReader);

            // for tempOnRez objects, we have to fix the Expire date.
            if ((part.Flags & PrimFlags.TemporaryOnRez) != 0) part.ResetExpire();
            part.GenerateRotationalVelocityFromOmega(); //Fix the rotational velocity
            return part;
        }

        /// <summary>
        ///     Deserialize a scene object from the original xml format
        /// </summary>
        /// <param name="serialization"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        public ISceneEntity FromOriginalXmlFormat(string serialization, IRegistryCore scene)
        {
            return FromOriginalXmlFormat(UUID.Zero, serialization, scene);
        }

        /// <summary>
        ///     Deserialize a scene object from the original xml format
        /// </summary>
        /// <param name="fromUserInventoryItemID"></param>
        /// <param name="xmlData"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        public ISceneEntity FromOriginalXmlFormat(UUID fromUserInventoryItemID, string xmlData,
                                                  IRegistryCore scene)
        {
            //MainConsole.Instance.DebugFormat("[SOG]: Starting deserialization of SOG");
            //int time = Util.EnvironmentTickCount();

            try
            {
                StringReader sr;
                XmlTextReader reader;
                XmlNodeList parts;
                XmlDocument doc;

                doc = new XmlDocument();
                doc.LoadXml(xmlData);
                parts = doc.GetElementsByTagName("RootPart");

                IScene m_sceneForGroup = scene is IScene ? (IScene) scene : null;
                ISceneEntity sceneObject;
                if (parts.Count == 0)
                {
                    sceneObject = FromXml2Format(xmlData, m_sceneForGroup);
                    if (sceneObject == null)
                        return null;
                    
                    return sceneObject;
                }

                sr = new StringReader(parts[0].InnerXml);
                reader = new XmlTextReader(sr);


                sceneObject = new SceneObjectGroup(FromXml(reader), m_sceneForGroup, false);
                sceneObject.RootChild.FromUserInventoryItemID = fromUserInventoryItemID;
                reader.Close();

                parts = doc.GetElementsByTagName("Part");

                for (int i = 0; i < parts.Count; i++)
                {
                    sr = new StringReader(parts[i].InnerXml);
                    reader = new XmlTextReader(sr);
                    SceneObjectPart part = FromXml(reader);
                    sceneObject.AddChild(part, part.LinkNum);
                    part.TrimPermissions();
                    part.StoreUndoState();
                    reader.Close();
                }

                return sceneObject;
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat(
                    "[Serializer]: Deserialization of xml failed with {0}.  xml was {1}", e, xmlData);
                return null;
            }
        }

        /// <summary>
        ///     Serialize a scene object to the original xml format
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        public string ToOriginalXmlFormat(ISceneEntity sceneObject)
        {
            if (!(sceneObject is SceneObjectGroup))
                return "";
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    ToOriginalXmlFormat(sceneObject as SceneObjectGroup, writer);
                }

                return sw.ToString();
            }
        }

        /// <summary>
        ///     Serialize a scene object to the original xml format
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="writer"></param>
        /// <returns></returns>
        protected void ToOriginalXmlFormat(SceneObjectGroup sceneObject, XmlTextWriter writer)
        {
            //MainConsole.Instance.DebugFormat("[SERIALIZER]: Starting serialization of {0}", Name);
            //int time = Util.EnvironmentTickCount();

            writer.WriteStartElement(string.Empty, "SceneObjectGroup", string.Empty);
            writer.WriteStartElement(string.Empty, "RootPart", string.Empty);
            ToXmlFormat(sceneObject.RootPart, writer);
            writer.WriteEndElement();
            writer.WriteStartElement(string.Empty, "OtherParts", string.Empty);

            SceneObjectPart[] parts = sceneObject.Parts;
            foreach (SceneObjectPart part in parts.Where(part => part.UUID != sceneObject.RootPart.UUID))
            {
                writer.WriteStartElement(string.Empty, "Part", string.Empty);
                ToXmlFormat(part, writer);
                writer.WriteEndElement();
            }

            writer.WriteEndElement(); // OtherParts
            writer.WriteEndElement(); // SceneObjectGroup

            //MainConsole.Instance.DebugFormat("[SERIALIZER]: Finished serialization of SOG {0}, {1}ms", Name, Util.EnvironmentTickCount() - time);
        }

        public void ToXmlFormat(ISceneChildEntity part, XmlTextWriter writer)
        {
            if (!(part is SceneObjectPart))
                return;
            SOPToXml2(writer, part as SceneObjectPart, new Dictionary<string, object>());
        }

        public ISceneEntity FromXml2Format(string xmlData, IScene scene)
        {
            //MainConsole.Instance.DebugFormat("[SOG]: Starting deserialization of SOG");
            //int time = Util.EnvironmentTickCount();

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.LoadXml(xmlData);
                SceneObjectGroup grp = InternalFromXml2Format(doc, scene);
                xmlData = null;
                return grp;
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[Serializer]: Deserialization of xml failed with {0}.  xml was {1}", e,
                                                 xmlData);
                return null;
            }
            finally
            {
                doc.RemoveAll();
                doc = null;
            }
        }

        public ISceneEntity FromXml2Format(ref MemoryStream ms, IScene scene)
        {
            //MainConsole.Instance.DebugFormat("[SOG]: Starting deserialization of SOG");
            //int time = Util.EnvironmentTickCount();

            XmlDocument doc = new XmlDocument();
            SceneObjectGroup grp = null;
            try
            {
                doc.Load(ms);

                grp = InternalFromXml2Format(doc, scene);
                if (grp != null)
                    foreach (var c in grp.ChildrenList)
                        c.FinishedSerializingGenericProperties();
                return grp;
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[Serializer]: Deserialization of xml failed with {0}", e);
                return grp;
            }
            finally
            {
                doc = null;
            }
        }

        SceneObjectGroup InternalFromXml2Format(XmlDocument doc, IScene scene)
        {
            //MainConsole.Instance.DebugFormat("[SOG]: Starting deserialization of SOG");
            //int time = Util.EnvironmentTickCount();

            try
            {
                XmlNodeList parts = doc.GetElementsByTagName("SceneObjectPart");
                if (parts.Count == 0)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[SERIALIZER]: Deserialization of xml failed: No SceneObjectPart nodes. xml was " + doc.Value);
                    return null;
                }

                StringReader sr = new StringReader(parts[0].OuterXml);
                XmlTextReader reader = new XmlTextReader(sr);
                SceneObjectGroup sceneObject = new SceneObjectGroup(FromXml(reader), scene);
                reader.Close();

                // Then deal with the rest
                for (int i = 1; i < parts.Count; i++)
                {
                    sr = new StringReader(parts[i].OuterXml);
                    reader = new XmlTextReader(sr);
                    SceneObjectPart part = FromXml(reader);
                    sceneObject.AddChild(part, part.LinkNum);

                    part.StoreUndoState();
                    reader.Close();
                }
                parts = null;
                doc = null;
                return sceneObject;
            }
            catch (Exception e)
            {
                MainConsole.Instance.ErrorFormat("[Serializer]: Deserialization of xml failed with {0}.  xml was {1}", e,
                                                 doc.Value);
                return null;
            }
        }

        /// <summary>
        ///     Serialize a scene object to the 'xml2' format.
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        public string ToXml2Format(ISceneEntity sceneObject)
        {
            if (!(sceneObject is SceneObjectGroup))
                return null;
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    SOGToXml2(writer, sceneObject as SceneObjectGroup, new Dictionary<string, object>());
                }

                return sw.ToString();
            }
        }

        /// <summary>
        ///     Serialize a scene object to the 'xml2' format.
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        public byte[] ToBinaryXml2Format(ISceneEntity sceneObject)
        {
            if (!(sceneObject is SceneObjectGroup))
                return null;
            using (MemoryStream sw = new MemoryStream())
            {
                using (StreamWriter wr = new StreamWriter(sw, Encoding.UTF8))
                {
                    using (XmlTextWriter writer = new XmlTextWriter(wr))
                    {
                        SOGToXml2(writer, sceneObject as SceneObjectGroup, new Dictionary<string, object>());
                    }
                    return sw.ToArray();
                }
            }
        }

        #region manual serialization

        #region Delegates

        public delegate void SOPXmlProcessor(SceneObjectPart sop, XmlTextReader reader);

        #endregion

        readonly Dictionary<string, SOPXmlProcessor> m_SOPXmlProcessors =
            new Dictionary<string, SOPXmlProcessor>();

        readonly Dictionary<string, TaskInventoryXmlProcessor> m_TaskInventoryXmlProcessors =
            new Dictionary<string, TaskInventoryXmlProcessor>();

        readonly Dictionary<string, ShapeXmlProcessor> m_ShapeXmlProcessors =
            new Dictionary<string, ShapeXmlProcessor>();

        readonly Dictionary<string, Serialization> m_genericSerializers =
            new Dictionary<string, Serialization>();

        public SceneObjectSerializer()
        {
            #region SOPXmlProcessors initialization

            m_SOPXmlProcessors.Add("AllowedDrop", ProcessAllowedDrop);
            m_SOPXmlProcessors.Add("CreatorID", ProcessCreatorID);
            m_SOPXmlProcessors.Add("CreatorData", ProcessCreatorData);
            m_SOPXmlProcessors.Add("InventorySerial", ProcessInventorySerial);
            m_SOPXmlProcessors.Add("TaskInventory", ProcessTaskInventory);
            m_SOPXmlProcessors.Add("UUID", ProcessUUID);
            m_SOPXmlProcessors.Add("LocalId", ProcessLocalId);
            m_SOPXmlProcessors.Add("Name", ProcessName);
            m_SOPXmlProcessors.Add("Material", ProcessMaterial);
            m_SOPXmlProcessors.Add("PassTouch", ProcessPassTouch);
            m_SOPXmlProcessors.Add("PassCollisions", ProcessPassCollisions);
            m_SOPXmlProcessors.Add("ScriptAccessPin", ProcessScriptAccessPin);
            m_SOPXmlProcessors.Add("GroupPosition", ProcessGroupPosition);
            m_SOPXmlProcessors.Add("OffsetPosition", ProcessOffsetPosition);
            m_SOPXmlProcessors.Add("RotationOffset", ProcessRotationOffset);
            m_SOPXmlProcessors.Add("Velocity", ProcessVelocity);
            //m_SOPXmlProcessors.Add("AngularVelocity", ProcessAngularVelocity);
            m_SOPXmlProcessors.Add("Acceleration", ProcessAcceleration);
            m_SOPXmlProcessors.Add("Description", ProcessDescription);
            m_SOPXmlProcessors.Add("Color", ProcessColor);
            m_SOPXmlProcessors.Add("Text", ProcessText);
            m_SOPXmlProcessors.Add("SitName", ProcessSitName);
            m_SOPXmlProcessors.Add("TouchName", ProcessTouchName);
            m_SOPXmlProcessors.Add("LinkNum", ProcessLinkNum);
            m_SOPXmlProcessors.Add("ClickAction", ProcessClickAction);
            m_SOPXmlProcessors.Add("Shape", ProcessShape);
            m_SOPXmlProcessors.Add("Scale", ProcessScale);
            m_SOPXmlProcessors.Add("UpdateFlag", ProcessUpdateFlag);
            m_SOPXmlProcessors.Add("SitTargetOrientation", ProcessSitTargetOrientation);
            m_SOPXmlProcessors.Add("SitTargetPosition", ProcessSitTargetPosition);
            m_SOPXmlProcessors.Add("ParentID", ProcessParentID);
            m_SOPXmlProcessors.Add("CreationDate", ProcessCreationDate);
            m_SOPXmlProcessors.Add("Category", ProcessCategory);
            m_SOPXmlProcessors.Add("SalePrice", ProcessSalePrice);
            m_SOPXmlProcessors.Add("ObjectSaleType", ProcessObjectSaleType);
            m_SOPXmlProcessors.Add("OwnershipCost", ProcessOwnershipCost);
            m_SOPXmlProcessors.Add("GroupID", ProcessGroupID);
            m_SOPXmlProcessors.Add("OwnerID", ProcessOwnerID);
            m_SOPXmlProcessors.Add("LastOwnerID", ProcessLastOwnerID);
            m_SOPXmlProcessors.Add("BaseMask", ProcessBaseMask);
            m_SOPXmlProcessors.Add("OwnerMask", ProcessOwnerMask);
            m_SOPXmlProcessors.Add("GroupMask", ProcessGroupMask);
            m_SOPXmlProcessors.Add("EveryoneMask", ProcessEveryoneMask);
            m_SOPXmlProcessors.Add("NextOwnerMask", ProcessNextOwnerMask);
            m_SOPXmlProcessors.Add("Flags", ProcessFlags);
            m_SOPXmlProcessors.Add("CollisionSound", ProcessCollisionSound);
            m_SOPXmlProcessors.Add("CollisionSoundVolume", ProcessCollisionSoundVolume);
            m_SOPXmlProcessors.Add("MediaUrl", ProcessMediaUrl);
            m_SOPXmlProcessors.Add("TextureAnimation", ProcessTextureAnimation);
            m_SOPXmlProcessors.Add("ParticleSystem", ProcessParticleSystem);
            m_SOPXmlProcessors.Add("PayPrice0", ProcessPayPrice0);
            m_SOPXmlProcessors.Add("PayPrice1", ProcessPayPrice1);
            m_SOPXmlProcessors.Add("PayPrice2", ProcessPayPrice2);
            m_SOPXmlProcessors.Add("PayPrice3", ProcessPayPrice3);
            m_SOPXmlProcessors.Add("PayPrice4", ProcessPayPrice4);
            m_SOPXmlProcessors.Add("FromUserInventoryAssetID", ProcessFromUserInventoryAssetID);
            m_SOPXmlProcessors.Add("FromUserInventoryItemID", ProcessFromUserInventoryItemID);
            Type sopType = typeof (SceneObjectPart);
            m_SOPXmlProcessors.Add("RETURN_AT_EDGE", ((sop, xml) => GenericBool(sop, xml, "RETURN_AT_EDGE", sopType)));
            m_SOPXmlProcessors.Add("BlockGrab", ((sop, xml) => GenericBool(sop, xml, "BlockGrab", sopType)));
            m_SOPXmlProcessors.Add("BlockGrabObject", ((sop, xml) => GenericBool(sop, xml, "BlockGrabObject", sopType)));
            m_SOPXmlProcessors.Add("StatusSandbox", ((sop, xml) => GenericBool(sop, xml, "StatusSandbox", sopType)));
            m_SOPXmlProcessors.Add("StatusSandboxPos",
                                   ((sop, xml) => GenericVector3(sop, xml, "StatusSandboxPos", sopType)));
            m_SOPXmlProcessors.Add("STATUS_ROTATE_X", ((sop, xml) => GenericInt(sop, xml, "STATUS_ROTATE_X", sopType)));
            m_SOPXmlProcessors.Add("STATUS_ROTATE_Y", ((sop, xml) => GenericInt(sop, xml, "STATUS_ROTATE_Y", sopType)));
            m_SOPXmlProcessors.Add("STATUS_ROTATE_Z", ((sop, xml) => GenericInt(sop, xml, "STATUS_ROTATE_Z", sopType)));
            m_SOPXmlProcessors.Add("OmegaAxis", ((sop, xml) => GenericVector3(sop, xml, "OmegaAxis", sopType)));
            m_SOPXmlProcessors.Add("OmegaSpinRate", ((sop, xml) => GenericDouble(sop, xml, "OmegaSpinRate", sopType)));
            m_SOPXmlProcessors.Add("OmegaGain", ((sop, xml) => GenericDouble(sop, xml, "OmegaGain", sopType)));
            m_SOPXmlProcessors.Add("PhysicsType", ((sop, xml) => GenericByte(sop, xml, "PhysicsType", sopType)));
            m_SOPXmlProcessors.Add("Density", ((sop, xml) => GenericFloat(sop, xml, "Density", sopType)));
            m_SOPXmlProcessors.Add("Friction", ((sop, xml) => GenericFloat(sop, xml, "Friction", sopType)));
            m_SOPXmlProcessors.Add("Restitution", ((sop, xml) => GenericFloat(sop, xml, "Restitution", sopType)));
            m_SOPXmlProcessors.Add("GravityMultiplier",
                                   ((sop, xml) => GenericFloat(sop, xml, "GravityMultiplier", sopType)));
            m_SOPXmlProcessors.Add("DIE_AT_EDGE", ((sop, xml) => GenericBool(sop, xml, "DIE_AT_EDGE", sopType)));
            m_SOPXmlProcessors.Add("UseSoundQueue", ((sop, xml) => GenericInt(sop, xml, "UseSoundQueue", sopType)));
            m_SOPXmlProcessors.Add("Sound", ((sop, xml) => GenericUUID(sop, xml, "Sound", sopType)));
            m_SOPXmlProcessors.Add("SoundFlags", ((sop, xml) => GenericByte(sop, xml, "SoundFlags", sopType)));
            m_SOPXmlProcessors.Add("SoundGain", ((sop, xml) => GenericDouble(sop, xml, "SoundGain", sopType)));
            m_SOPXmlProcessors.Add("SoundRadius", ((sop, xml) => GenericDouble(sop, xml, "SoundRadius", sopType)));
            m_SOPXmlProcessors.Add("PIDTarget", ((sop, xml) => GenericVector3(sop, xml, "PIDTarget", sopType)));
            m_SOPXmlProcessors.Add("PIDActive", ((sop, xml) => GenericBool(sop, xml, "PIDActive", sopType)));
            m_SOPXmlProcessors.Add("PIDTau", ((sop, xml) => GenericFloat(sop, xml, "PIDTau", sopType)));
            m_SOPXmlProcessors.Add("PIDHoverHeight", ((sop, xml) => GenericFloat(sop, xml, "PIDHoverHeight", sopType)));
            m_SOPXmlProcessors.Add("PIDHoverTau", ((sop, xml) => GenericFloat(sop, xml, "PIDHoverTau", sopType)));

            m_SOPXmlProcessors.Add("Vehicle", ((sop, xml) => ReadVehicleSettings(sop, xml)));

            m_SOPXmlProcessors.Add("SavedAttachedPos",
                                   ((sop, xml) => GenericVector3(sop, xml, "SavedAttachedPos", sopType)));
            m_SOPXmlProcessors.Add("SavedAttachmentPoint",
                                   ((sop, xml) => GenericInt(sop, xml, "SavedAttachmentPoint", sopType)));
            m_SOPXmlProcessors.Add("VolumeDetectActive",
                                   ((sop, xml) => GenericBool(sop, xml, "VolumeDetectActive", sopType)));
            m_SOPXmlProcessors.Add("CameraEyeOffset",
                                   ((sop, xml) => GenericVector3(sop, xml, "CameraEyeOffset", sopType)));
            m_SOPXmlProcessors.Add("CameraAtOffset", ((sop, xml) => GenericVector3(sop, xml, "CameraAtOffset", sopType)));
            m_SOPXmlProcessors.Add("ForceMouselook", ((sop, xml) => GenericBool(sop, xml, "ForceMouselook", sopType)));
            m_SOPXmlProcessors.Add("APIDTarget", ((sop, xml) => GenericQuaternion(sop, xml, "APIDTarget", sopType)));
            m_SOPXmlProcessors.Add("APIDDamp", ((sop, xml) => GenericFloat(sop, xml, "APIDDamp", sopType)));
            m_SOPXmlProcessors.Add("APIDStrength", ((sop, xml) => GenericFloat(sop, xml, "APIDStrength", sopType)));
            m_SOPXmlProcessors.Add("APIDIterations", ((sop, xml) => GenericInt(sop, xml, "APIDIterations", sopType)));
            m_SOPXmlProcessors.Add("APIDEnabled", ((sop, xml) => GenericBool(sop, xml, "APIDEnabled", sopType)));
            m_SOPXmlProcessors.Add("Damage", ((sop, xml) => GenericFloat(sop, xml, "Damage", sopType)));
            m_SOPXmlProcessors.Add("StateSaveXML", ((sop, xml) => ReadStateSave(sop, xml, sopType)));
            m_SOPXmlProcessors.Add("KeyframeAnimation", ((sop, xml) => ReadProtobuf<KeyframeAnimation>(sop, xml, "KeyframeAnimation", sopType)));

            #endregion

            #region TaskInventoryXmlProcessors initialization

            m_TaskInventoryXmlProcessors.Add("AssetID", ProcessTIAssetID);
            m_TaskInventoryXmlProcessors.Add("BasePermissions", ProcessTIBasePermissions);
            m_TaskInventoryXmlProcessors.Add("CreationDate", ProcessTICreationDate);
            m_TaskInventoryXmlProcessors.Add("CreatorID", ProcessTICreatorID);
            m_TaskInventoryXmlProcessors.Add("CreatorData", ProcessTICreatorData);
            m_TaskInventoryXmlProcessors.Add("Description", ProcessTIDescription);
            m_TaskInventoryXmlProcessors.Add("EveryonePermissions", ProcessTIEveryonePermissions);
            m_TaskInventoryXmlProcessors.Add("Flags", ProcessTIFlags);
            m_TaskInventoryXmlProcessors.Add("GroupID", ProcessTIGroupID);
            m_TaskInventoryXmlProcessors.Add("GroupPermissions", ProcessTIGroupPermissions);
            m_TaskInventoryXmlProcessors.Add("InvType", ProcessTIInvType);
            m_TaskInventoryXmlProcessors.Add("ItemID", ProcessTIItemID);
            m_TaskInventoryXmlProcessors.Add("OldItemID", ProcessTIOldItemID);
            m_TaskInventoryXmlProcessors.Add("LastOwnerID", ProcessTILastOwnerID);
            m_TaskInventoryXmlProcessors.Add("Name", ProcessTIName);
            m_TaskInventoryXmlProcessors.Add("NextPermissions", ProcessTINextPermissions);
            m_TaskInventoryXmlProcessors.Add("OwnerID", ProcessTIOwnerID);
            m_TaskInventoryXmlProcessors.Add("CurrentPermissions", ProcessTICurrentPermissions);
            m_TaskInventoryXmlProcessors.Add("ParentID", ProcessTIParentID);
            m_TaskInventoryXmlProcessors.Add("ParentPartID", ProcessTIParentPartID);
            m_TaskInventoryXmlProcessors.Add("PermsGranter", ProcessTIPermsGranter);
            m_TaskInventoryXmlProcessors.Add("PermsMask", ProcessTIPermsMask);
            m_TaskInventoryXmlProcessors.Add("Type", ProcessTIType);
            m_TaskInventoryXmlProcessors.Add("OwnerChanged", ProcessTIOwnerChanged);

            #endregion

            #region ShapeXmlProcessors initialization

            m_ShapeXmlProcessors.Add("ProfileCurve", ProcessShpProfileCurve);
            m_ShapeXmlProcessors.Add("TextureEntry", ProcessShpTextureEntry);
            m_ShapeXmlProcessors.Add("ExtraParams", ProcessShpExtraParams);
            m_ShapeXmlProcessors.Add("PathBegin", ProcessShpPathBegin);
            m_ShapeXmlProcessors.Add("PathCurve", ProcessShpPathCurve);
            m_ShapeXmlProcessors.Add("PathEnd", ProcessShpPathEnd);
            m_ShapeXmlProcessors.Add("PathRadiusOffset", ProcessShpPathRadiusOffset);
            m_ShapeXmlProcessors.Add("PathRevolutions", ProcessShpPathRevolutions);
            m_ShapeXmlProcessors.Add("PathScaleX", ProcessShpPathScaleX);
            m_ShapeXmlProcessors.Add("PathScaleY", ProcessShpPathScaleY);
            m_ShapeXmlProcessors.Add("PathShearX", ProcessShpPathShearX);
            m_ShapeXmlProcessors.Add("PathShearY", ProcessShpPathShearY);
            m_ShapeXmlProcessors.Add("PathSkew", ProcessShpPathSkew);
            m_ShapeXmlProcessors.Add("PathTaperX", ProcessShpPathTaperX);
            m_ShapeXmlProcessors.Add("PathTaperY", ProcessShpPathTaperY);
            m_ShapeXmlProcessors.Add("PathTwist", ProcessShpPathTwist);
            m_ShapeXmlProcessors.Add("PathTwistBegin", ProcessShpPathTwistBegin);
            m_ShapeXmlProcessors.Add("PCode", ProcessShpPCode);
            m_ShapeXmlProcessors.Add("ProfileBegin", ProcessShpProfileBegin);
            m_ShapeXmlProcessors.Add("ProfileEnd", ProcessShpProfileEnd);
            m_ShapeXmlProcessors.Add("ProfileHollow", ProcessShpProfileHollow);
            m_ShapeXmlProcessors.Add("Scale", ProcessShpScale);
            m_ShapeXmlProcessors.Add("State", ProcessShpState);
            m_ShapeXmlProcessors.Add("ProfileShape", ProcessShpProfileShape);
            m_ShapeXmlProcessors.Add("HollowShape", ProcessShpHollowShape);
            m_ShapeXmlProcessors.Add("SculptTexture", ProcessShpSculptTexture);
            m_ShapeXmlProcessors.Add("SculptType", ProcessShpSculptType);
            m_ShapeXmlProcessors.Add("SculptData", ProcessShpSculptData);
            m_ShapeXmlProcessors.Add("FlexiSoftness", ProcessShpFlexiSoftness);
            m_ShapeXmlProcessors.Add("FlexiTension", ProcessShpFlexiTension);
            m_ShapeXmlProcessors.Add("FlexiDrag", ProcessShpFlexiDrag);
            m_ShapeXmlProcessors.Add("FlexiGravity", ProcessShpFlexiGravity);
            m_ShapeXmlProcessors.Add("FlexiWind", ProcessShpFlexiWind);
            m_ShapeXmlProcessors.Add("FlexiForceX", ProcessShpFlexiForceX);
            m_ShapeXmlProcessors.Add("FlexiForceY", ProcessShpFlexiForceY);
            m_ShapeXmlProcessors.Add("FlexiForceZ", ProcessShpFlexiForceZ);
            m_ShapeXmlProcessors.Add("LightColorR", ProcessShpLightColorR);
            m_ShapeXmlProcessors.Add("LightColorG", ProcessShpLightColorG);
            m_ShapeXmlProcessors.Add("LightColorB", ProcessShpLightColorB);
            m_ShapeXmlProcessors.Add("LightColorA", ProcessShpLightColorA);
            m_ShapeXmlProcessors.Add("LightRadius", ProcessShpLightRadius);
            m_ShapeXmlProcessors.Add("LightCutoff", ProcessShpLightCutoff);
            m_ShapeXmlProcessors.Add("LightFalloff", ProcessShpLightFalloff);
            m_ShapeXmlProcessors.Add("LightIntensity", ProcessShpLightIntensity);
            m_ShapeXmlProcessors.Add("FlexiEntry", ProcessShpFlexiEntry);
            m_ShapeXmlProcessors.Add("LightEntry", ProcessShpLightEntry);
            m_ShapeXmlProcessors.Add("SculptEntry", ProcessShpSculptEntry);
            m_ShapeXmlProcessors.Add("Media", ProcessShpMedia);

            #endregion
        }

        public void AddSerializer(string Name, ISOPSerializerModule processor)
        {
            if (!m_SOPXmlProcessors.ContainsKey(Name))
            {
                m_SOPXmlProcessors.Add(Name, processor.Deserialization);
                m_genericSerializers.Add(Name, processor.Serialization);
            }
            else
                MainConsole.Instance.Warn("[Serializer]: Tried to add an additional SOP processor for " +
                                          Name);
        }

        public void RemoveSerializer(string Name)
        {
            if (m_SOPXmlProcessors.ContainsKey(Name))
            {
                m_SOPXmlProcessors.Remove(Name);
                m_genericSerializers.Remove(Name);
            }
            else
                MainConsole.Instance.Warn("[Serializer]: Tried to remove a SOP processor for " + Name +
                                          " that did not exist");
        }

        ////////// Write /////////

        public void SOGToXml2(XmlTextWriter writer, SceneObjectGroup sog, Dictionary<string, object> options)
        {
            writer.WriteStartElement(string.Empty, "SceneObjectGroup", string.Empty);
            SOPToXml2(writer, sog.RootPart, options);
            writer.WriteStartElement(string.Empty, "OtherParts", string.Empty);

            sog.ForEachPart(delegate(SceneObjectPart sop)
                                {
                                    if (sop.UUID != sog.RootPart.UUID)
                                        SOPToXml2(writer, sop, options);
                                });

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        public void SOPToXml2(XmlTextWriter writer, SceneObjectPart sop, Dictionary<string, object> options)
        {
            writer.WriteStartElement("SceneObjectPart");
            writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            writer.WriteAttributeString("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");

            writer.WriteElementString("AllowedDrop", sop.AllowedDrop.ToString().ToLower());
            WriteUUID(writer, "CreatorID", sop.CreatorID, options);

            if (!string.IsNullOrEmpty(sop.CreatorData))
                writer.WriteElementString("CreatorData", sop.CreatorData);

            writer.WriteElementString("InventorySerial", sop.InventorySerial.ToString());

            WriteTaskInventory(writer, sop.TaskInventory, options, sop.ParentEntity.Scene);

            WriteUUID(writer, "UUID", sop.UUID, options);
            writer.WriteElementString("LocalId", sop.LocalId.ToString());
            writer.WriteElementString("Name", sop.Name);
            writer.WriteElementString("Material", sop.Material.ToString());
            writer.WriteElementString("PassTouch", sop.PassTouch.ToString());
            writer.WriteElementString("PassCollisions", sop.PassCollisions.ToString());
            writer.WriteElementString("ScriptAccessPin", sop.ScriptAccessPin.ToString());

            WriteVector(writer, "GroupPosition", sop.GroupPosition);
            WriteVector(writer, "OffsetPosition", sop.OffsetPosition);

            WriteQuaternion(writer, "RotationOffset", sop.RotationOffset);
            WriteVector(writer, "Velocity", sop.Velocity);
            WriteVector(writer, "AngularVelocity", sop.AngularVelocity);
            WriteVector(writer, "Acceleration", sop.Acceleration);
            writer.WriteElementString("Description", sop.Description);
            writer.WriteStartElement("Color");
            writer.WriteElementString("R", sop.Color.R.ToString(Utils.EnUsCulture));
            writer.WriteElementString("G", sop.Color.G.ToString(Utils.EnUsCulture));
            writer.WriteElementString("B", sop.Color.B.ToString(Utils.EnUsCulture));
            writer.WriteElementString("A", sop.Color.A.ToString(Utils.EnUsCulture));
            writer.WriteEndElement();

            writer.WriteElementString("Text", sop.Text);
            writer.WriteElementString("SitName", sop.SitName);
            writer.WriteElementString("TouchName", sop.TouchName);

            writer.WriteElementString("LinkNum", sop.LinkNum.ToString());
            writer.WriteElementString("ClickAction", sop.ClickAction.ToString());

            WriteShape(writer, sop.Shape, options);

            WriteVector(writer, "Scale", sop.Scale);
            writer.WriteElementString("UpdateFlag", ((byte) 0).ToString());
            WriteQuaternion(writer, "SitTargetOrientation", sop.SitTargetOrientation);
            WriteVector(writer, "SitTargetPosition", sop.SitTargetPosition);
            writer.WriteElementString("ParentID", sop.ParentID.ToString());
            writer.WriteElementString("CreationDate", sop.CreationDate.ToString());
            writer.WriteElementString("Category", sop.Category.ToString());
            writer.WriteElementString("SalePrice", sop.SalePrice.ToString());
            writer.WriteElementString("ObjectSaleType", sop.ObjectSaleType.ToString());
            writer.WriteElementString("OwnershipCost", sop.OwnershipCost.ToString());
            WriteUUID(writer, "GroupID", sop.GroupID, options);
            WriteUUID(writer, "OwnerID", sop.OwnerID, options);
            WriteUUID(writer, "LastOwnerID", sop.LastOwnerID, options);
            writer.WriteElementString("BaseMask", sop.BaseMask.ToString());
            writer.WriteElementString("OwnerMask", sop.OwnerMask.ToString());
            writer.WriteElementString("GroupMask", sop.GroupMask.ToString());
            writer.WriteElementString("EveryoneMask", sop.EveryoneMask.ToString());
            writer.WriteElementString("NextOwnerMask", sop.NextOwnerMask.ToString());
            writer.WriteElementString("Flags", sop.Flags.ToString());
            WriteUUID(writer, "CollisionSound", sop.CollisionSound, options);
            writer.WriteElementString("CollisionSoundVolume", sop.CollisionSoundVolume.ToString());
            if (sop.MediaUrl != null)
                writer.WriteElementString("MediaUrl", sop.MediaUrl);
            WriteBytes(writer, "TextureAnimation", sop.TextureAnimation);
            WriteBytes(writer, "ParticleSystem", sop.ParticleSystem);
            writer.WriteElementString("PayPrice0", sop.PayPrice[0].ToString());
            writer.WriteElementString("PayPrice1", sop.PayPrice[1].ToString());
            writer.WriteElementString("PayPrice2", sop.PayPrice[2].ToString());
            writer.WriteElementString("PayPrice3", sop.PayPrice[3].ToString());
            writer.WriteElementString("PayPrice4", sop.PayPrice[4].ToString());
            writer.WriteElementString("FromUserInventoryItemID", sop.FromUserInventoryItemID.ToString());
            writer.WriteElementString("FromUserInventoryAssetID", sop.FromUserInventoryAssetID.ToString());

            writer.WriteElementString("RETURN_AT_EDGE", sop.RETURN_AT_EDGE.ToString().ToLower());
            writer.WriteElementString("BlockGrab", sop.BlockGrab.ToString().ToLower());
            writer.WriteElementString("BlockGrabObject", sop.BlockGrabObject.ToString().ToLower());
            writer.WriteElementString("StatusSandbox", sop.StatusSandbox.ToString().ToLower());
            WriteVector(writer, "StatusSandboxPos", sop.StatusSandboxPos);

            writer.WriteElementString("STATUS_ROTATE_X", sop.STATUS_ROTATE_X.ToString());
            writer.WriteElementString("STATUS_ROTATE_Y", sop.STATUS_ROTATE_Y.ToString());
            writer.WriteElementString("STATUS_ROTATE_Z", sop.STATUS_ROTATE_Z.ToString());

            WriteVector(writer, "SitTargetPosition", sop.SitTargetPosition);
            WriteQuaternion(writer, "SitTargetOrientation", sop.SitTargetOrientation);
            WriteVector(writer, "OmegaAxis", sop.OmegaAxis);

            writer.WriteElementString("OmegaSpinRate", sop.OmegaSpinRate.ToString());
            writer.WriteElementString("OmegaGain", sop.OmegaGain.ToString());
            writer.WriteElementString("PhysicsType", sop.PhysicsType.ToString());
            writer.WriteElementString("Density", sop.Density.ToString());
            writer.WriteElementString("Friction", sop.Friction.ToString());
            writer.WriteElementString("Restitution", sop.Restitution.ToString());
            writer.WriteElementString("GravityMultiplier", sop.GravityMultiplier.ToString());
            writer.WriteElementString("DIE_AT_EDGE", sop.DIE_AT_EDGE.ToString().ToLower());
            writer.WriteElementString("UseSoundQueue", sop.UseSoundQueue.ToString().ToLower());

            WriteUUID(writer, "Sound", sop.Sound, options);

            writer.WriteElementString("SoundFlags", sop.SoundFlags.ToString());
            writer.WriteElementString("SoundGain", sop.SoundGain.ToString());
            writer.WriteElementString("SoundRadius", sop.SoundRadius.ToString());

            WriteVector(writer, "PIDTarget", sop.PIDTarget);

            writer.WriteElementString("PIDActive", sop.PIDActive.ToString().ToLower());
            writer.WriteElementString("PIDTau", sop.PIDTau.ToString()); //fl
            writer.WriteElementString("PIDHoverHeight", sop.PIDHoverHeight.ToString()); //fl
            writer.WriteElementString("PIDHoverTau", sop.PIDHoverTau.ToString()); //fl

            WriteVehicle(writer, sop);

            WriteVector(writer, "SavedAttachedPos", sop.SavedAttachedPos);
            writer.WriteElementString("SavedAttachmentPoint", sop.SavedAttachmentPoint.ToString());
            writer.WriteElementString("VolumeDetectActive", sop.VolumeDetectActive.ToString().ToLower());

            WriteVector(writer, "CameraEyeOffset", sop.CameraEyeOffset);
            WriteVector(writer, "CameraAtOffset", sop.CameraAtOffset);

            writer.WriteElementString("ForceMouselook", sop.ForceMouselook.ToString().ToLower());

            WriteQuaternion(writer, "APIDTarget", sop.APIDTarget);
            writer.WriteElementString("APIDDamp", sop.APIDDamp.ToString());
            writer.WriteElementString("APIDStrength", sop.APIDStrength.ToString());
            writer.WriteElementString("APIDIterations", sop.APIDIterations.ToString());
            writer.WriteElementString("APIDEnabled", sop.APIDEnabled.ToString().ToLower());
            writer.WriteElementString("Damage", sop.Damage.ToString());

            writer.WriteStartElement("StateSaveXML");
            foreach (KeyValuePair<UUID, StateSave> kvp in sop.StateSaves)
            {
                writer.WriteStartElement("StateSave");

                writer.WriteElementString("AssemblyName", kvp.Value.AssemblyName);
                writer.WriteElementString("Compiled", kvp.Value.Compiled.ToString().ToLower());
                writer.WriteElementString("Disabled", kvp.Value.Disabled.ToString().ToLower());
                writer.WriteElementString("ItemID", kvp.Value.ItemID.ToString());
                writer.WriteElementString("MinEventDelay", kvp.Value.MinEventDelay.ToString());
                writer.WriteElementString("PermsGranter", kvp.Value.PermsGranter.ToString());
                writer.WriteElementString("PermsMask", kvp.Value.PermsMask.ToString());
                writer.WriteElementString("Plugins", kvp.Value.Plugins);
                writer.WriteElementString("Running", kvp.Value.Running.ToString().ToLower());
                writer.WriteElementString("Source", kvp.Value.Source);
                writer.WriteElementString("State", kvp.Value.State);
                writer.WriteElementString("TargetOmegaWasSet", kvp.Value.TargetOmegaWasSet.ToString().ToLower());
                writer.WriteElementString("UserInventoryID", kvp.Value.UserInventoryID.ToString());
                writer.WriteElementString("Variables", kvp.Value.Variables);

                writer.WriteEndElement();// StateSave
            }
            writer.WriteEndElement();// StateSaveXML

            using(MemoryStream stream = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<KeyframeAnimation>(stream, sop.KeyframeAnimation);
                writer.WriteElementString("KeyframeAnimation", Convert.ToBase64String(stream.ToArray()));
            }
             
            //Write the generic elements last
            foreach (KeyValuePair<string, Serialization> kvp in m_genericSerializers)
            {
                string val = kvp.Value(sop);
                if (val != null)
                    writer.WriteElementString(kvp.Key, val);
            }

            writer.WriteEndElement();
        }

        void WriteUUID(XmlTextWriter writer, string name, UUID id, Dictionary<string, object> options)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString(options.ContainsKey("old-guids") ? "Guid" : "UUID", id.ToString());
            writer.WriteEndElement();
        }

        void WriteVector(XmlTextWriter writer, string name, Vector3 vec)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", vec.X.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Y", vec.Y.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Z", vec.Z.ToString(Utils.EnUsCulture));
            writer.WriteEndElement();
        }

        void WriteQuaternion(XmlTextWriter writer, string name, Quaternion quat)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", quat.X.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Y", quat.Y.ToString(Utils.EnUsCulture));
            writer.WriteElementString("Z", quat.Z.ToString(Utils.EnUsCulture));
            writer.WriteElementString("W", quat.W.ToString(Utils.EnUsCulture));
            writer.WriteEndElement();
        }

        void WriteBytes(XmlTextWriter writer, string name, byte[] data)
        {
            writer.WriteStartElement(name);
            byte[] d;
            if (data != null)
                d = data;
            else
                d = Utils.EmptyBytes;
            writer.WriteBase64(d, 0, d.Length);
            writer.WriteEndElement(); // name
        }

        void WriteTaskInventory(XmlTextWriter writer, TaskInventoryDictionary tinv,
                                        Dictionary<string, object> options, IScene scene)
        {
            if (tinv.Count > 0) // otherwise skip this
            {
                writer.WriteStartElement("TaskInventory");

                foreach (TaskInventoryItem item in tinv.Values)
                {
                    writer.WriteStartElement("TaskInventoryItem");

                    WriteUUID(writer, "AssetID", item.AssetID, options);
                    writer.WriteElementString("BasePermissions", item.BasePermissions.ToString());
                    writer.WriteElementString("CreationDate", item.CreationDate.ToString());
                    WriteUUID(writer, "CreatorID", item.CreatorID, options);
                    if (!string.IsNullOrEmpty(item.CreatorData))
                        writer.WriteElementString("CreatorData", item.CreatorData);
                    writer.WriteElementString("Description", item.Description);
                    writer.WriteElementString("EveryonePermissions", item.EveryonePermissions.ToString());
                    writer.WriteElementString("Flags", item.Flags.ToString());
                    WriteUUID(writer, "GroupID", item.GroupID, options);
                    writer.WriteElementString("GroupPermissions", item.GroupPermissions.ToString());
                    writer.WriteElementString("InvType", item.InvType.ToString());
                    WriteUUID(writer, "ItemID", item.ItemID, options);
                    WriteUUID(writer, "OldItemID", item.OldItemID, options);
                    WriteUUID(writer, "LastOwnerID", item.LastOwnerID, options);
                    writer.WriteElementString("Name", item.Name);
                    writer.WriteElementString("NextPermissions", item.NextPermissions.ToString());
                    WriteUUID(writer, "OwnerID", item.OwnerID, options);
                    writer.WriteElementString("CurrentPermissions", item.CurrentPermissions.ToString());
                    WriteUUID(writer, "ParentID", item.ParentID, options);
                    WriteUUID(writer, "ParentPartID", item.ParentPartID, options);
                    WriteUUID(writer, "PermsGranter", item.PermsGranter, options);
                    writer.WriteElementString("PermsMask", item.PermsMask.ToString());
                    writer.WriteElementString("Type", item.Type.ToString());
                    writer.WriteElementString("OwnerChanged", item.OwnerChanged.ToString().ToLower());

                    writer.WriteEndElement(); // TaskInventoryItem
                }

                writer.WriteEndElement(); // TaskInventory
            }
        }

        void WriteShape(XmlTextWriter writer, PrimitiveBaseShape shp, Dictionary<string, object> options)
        {
            if (shp != null)
            {
                writer.WriteStartElement("Shape");

                writer.WriteElementString("ProfileCurve", shp.ProfileCurve.ToString());

                writer.WriteStartElement("TextureEntry");
                byte[] te;
                if (shp.TextureEntry != null)
                    te = shp.TextureEntry;
                else
                    te = Utils.EmptyBytes;
                writer.WriteBase64(te, 0, te.Length);
                writer.WriteEndElement(); // TextureEntry

                writer.WriteStartElement("ExtraParams");
                byte[] ep;
                if (shp.ExtraParams != null)
                    ep = shp.ExtraParams;
                else
                    ep = Utils.EmptyBytes;
                writer.WriteBase64(ep, 0, ep.Length);
                writer.WriteEndElement(); // ExtraParams

                writer.WriteElementString("PathBegin", shp.PathBegin.ToString());
                writer.WriteElementString("PathCurve", shp.PathCurve.ToString());
                writer.WriteElementString("PathEnd", shp.PathEnd.ToString());
                writer.WriteElementString("PathRadiusOffset", shp.PathRadiusOffset.ToString());
                writer.WriteElementString("PathRevolutions", shp.PathRevolutions.ToString());
                writer.WriteElementString("PathScaleX", shp.PathScaleX.ToString());
                writer.WriteElementString("PathScaleY", shp.PathScaleY.ToString());
                writer.WriteElementString("PathShearX", shp.PathShearX.ToString());
                writer.WriteElementString("PathShearY", shp.PathShearY.ToString());
                writer.WriteElementString("PathSkew", shp.PathSkew.ToString());
                writer.WriteElementString("PathTaperX", shp.PathTaperX.ToString());
                writer.WriteElementString("PathTaperY", shp.PathTaperY.ToString());
                writer.WriteElementString("PathTwist", shp.PathTwist.ToString());
                writer.WriteElementString("PathTwistBegin", shp.PathTwistBegin.ToString());
                writer.WriteElementString("PCode", shp.PCode.ToString());
                writer.WriteElementString("ProfileBegin", shp.ProfileBegin.ToString());
                writer.WriteElementString("ProfileEnd", shp.ProfileEnd.ToString());
                writer.WriteElementString("ProfileHollow", shp.ProfileHollow.ToString());
                writer.WriteElementString("State", shp.State.ToString());

                writer.WriteElementString("ProfileShape", shp.ProfileShape.ToString());
                writer.WriteElementString("HollowShape", shp.HollowShape.ToString());

                WriteUUID(writer, "SculptTexture", shp.SculptTexture, options);
                writer.WriteElementString("SculptType", shp.SculptType.ToString());
                writer.WriteStartElement("SculptData");
                byte[] sd;
                //if (shp.SculptData != null)
                sd = shp.ExtraParams;
                //else
                //    sd = Utils.EmptyBytes;
                if (sd != null) writer.WriteBase64(sd, 0, sd.Length);
                writer.WriteEndElement(); // SculptData

                writer.WriteElementString("FlexiSoftness", shp.FlexiSoftness.ToString());
                writer.WriteElementString("FlexiTension", shp.FlexiTension.ToString());
                writer.WriteElementString("FlexiDrag", shp.FlexiDrag.ToString());
                writer.WriteElementString("FlexiGravity", shp.FlexiGravity.ToString());
                writer.WriteElementString("FlexiWind", shp.FlexiWind.ToString());
                writer.WriteElementString("FlexiForceX", shp.FlexiForceX.ToString());
                writer.WriteElementString("FlexiForceY", shp.FlexiForceY.ToString());
                writer.WriteElementString("FlexiForceZ", shp.FlexiForceZ.ToString());

                writer.WriteElementString("LightColorR", shp.LightColorR.ToString());
                writer.WriteElementString("LightColorG", shp.LightColorG.ToString());
                writer.WriteElementString("LightColorB", shp.LightColorB.ToString());
                writer.WriteElementString("LightColorA", shp.LightColorA.ToString());
                writer.WriteElementString("LightRadius", shp.LightRadius.ToString());
                writer.WriteElementString("LightCutoff", shp.LightCutoff.ToString());
                writer.WriteElementString("LightFalloff", shp.LightFalloff.ToString());
                writer.WriteElementString("LightIntensity", shp.LightIntensity.ToString());

                writer.WriteElementString("FlexiEntry", shp.FlexiEntry.ToString().ToLower());
                writer.WriteElementString("LightEntry", shp.LightEntry.ToString().ToLower());
                writer.WriteElementString("SculptEntry", shp.SculptEntry.ToString().ToLower());

                if (shp.Media != null)
                    writer.WriteElementString("Media", shp.Media.ToXml());

                writer.WriteEndElement(); // Shape
            }
        }

        void WriteVehicle(XmlTextWriter writer, SceneObjectPart sop)
        {
            // flags & parameters need a character prefix to the element number to conform to xml specs
            writer.WriteStartElement("Vehicle");
            writer.WriteElementString("VehicleType", sop.VehicleType.ToString ());

            if(sop.VehicleType > 0)
            {
                writer.WriteStartElement("VehicleSettings");
                writer.WriteStartElement("VehicleFlags");

                int elem = 0;
                foreach (int flag in sop.VehicleFlags)
                {
                    writer.WriteElementString("F"+elem, flag.ToString());
                    elem++;
                }
                writer.WriteEndElement ();

                writer.WriteStartElement("VehicleParameters");
                foreach (KeyValuePair<string, OSD> param in sop.VehicleParameters) {
                    if(param.Value.Type == OSDType.Array) {
                        OSDArray a = (OSDArray)param.Value;
                        if(a.Count == 3)
                            WriteVector(writer, "P"+param.Key, param.Value.AsVector3());
                        else
                            WriteQuaternion(writer, "P"+param.Key, param.Value.AsQuaternion());
                    }
                    else
                        writer.WriteElementString("P"+param.Key, param.Value.ToString ());
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }


        //////// Read /////////
        public bool Xml2ToSOG(XmlTextReader reader, SceneObjectGroup sog)
        {
            reader.Read();
            reader.ReadStartElement("SceneObjectGroup");
            SceneObjectPart root = Xml2ToSOP(reader);
            if (root != null)
                sog.SetRootPart(root);
            else
            {
                return false;
            }

            if (sog.UUID == UUID.Zero)
                sog.UUID = sog.RootPart.UUID;

            reader.Read(); // OtherParts

            while (!reader.EOF)
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "SceneObjectPart")
                        {
                            SceneObjectPart child = Xml2ToSOP(reader);
                            if (child != null)
                                sog.AddChild(child, child.LinkNum);
                        }
                        else
                        {
                            //Logger.Log("Found unexpected prim XML element " + reader.Name, Helpers.LogLevel.Debug);
                            reader.Read();
                        }
                        break;
                    case XmlNodeType.EndElement:
                    default:
                        reader.Read();
                        break;
                }
            }
            return true;
        }

        public SceneObjectPart Xml2ToSOP(XmlTextReader reader)
        {
            SceneObjectPart obj = new SceneObjectPart();

            reader.ReadStartElement("SceneObjectPart");

            string nodeName = string.Empty;
            while (reader.Name != "SceneObjectPart")
            {
                nodeName = reader.Name;
                SOPXmlProcessor p = null;
                if (m_SOPXmlProcessors.TryGetValue(reader.Name, out p))
                {
                    //MainConsole.Instance.DebugFormat("[XXX] Processing: {0}", reader.Name);
                    try
                    {
                        p(obj, reader);
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.DebugFormat("[Serializer]: exception while parsing {0}: {1}",
                                                         nodeName, e);
                        if (reader.NodeType == XmlNodeType.EndElement)
                            reader.Read();
                    }
                }
                else
                {
                    //                    MainConsole.Instance.DebugFormat("[SceneObjectSerializer]: caught unknown element {0}", nodeName);
                    reader.ReadOuterXml(); // ignore
                }
            }

            reader.ReadEndElement(); // SceneObjectPart

            //SceneObjectPart copy = ProtoBuf.Serializer.DeepClone<SceneObjectPart>(obj);
            //MainConsole.Instance.DebugFormat("[XXX]: parsed SOP {0} - {1}", obj.Name, obj.UUID);
            //bool success = AreMatch(obj, copy);
            return obj;
        }

        bool AreMatch(object initial, object result)
        {
            if (initial.Equals(result))
                return true;

            foreach (var property in initial.GetType().GetProperties())
            {
                try
                {
                    if (!property.CanRead || !property.CanWrite)
                        continue;
                    var data = property.GetCustomAttributes(typeof (ProtoBuf.ProtoMemberAttribute), false);
                    if (data.Length == 0)
                        continue;
                    var initialPropValue = property.GetValue(initial, null);
                    var resultPropValue = result.GetType().GetProperty(property.Name).GetValue(result, null);

                    if (property.PropertyType.IsArray)
                    {
                        if (initialPropValue != null && resultPropValue != null)
                        {
                            Array initialArray = (Array) initialPropValue;
                            Array resultArray = (Array) resultPropValue;
                            for (int i = 0; i < initialArray.Length; i++)
                            {
                                if (!object.Equals(initialArray.GetValue(i),
                                                   resultArray.GetValue(i)))
                                {
                                    MainConsole.Instance.WarnFormat("Failed to verify {0}, {1} != {2}", property.Name,
                                                                    initialPropValue, resultPropValue);
                                }
                            }
                        }
                    }
                    else if (initialPropValue != null && property.PropertyType.IsClass)
                    {
                        if (!AreMatch(initialPropValue, resultPropValue))
                            MainConsole.Instance.WarnFormat("Failed to verify {0}, {1} != {2}", property.Name,
                                                            initialPropValue, resultPropValue);
                    }
                    else if (!object.Equals(initialPropValue, resultPropValue))
                    {
                        //if(property.Name != "Color")
                        MainConsole.Instance.WarnFormat("Failed to verify {0}, {1} != {2}", property.Name,
                                                        initialPropValue, resultPropValue);
                    }
                }
                catch (Exception)
                {
                }
            }
            return true;
        }

        UUID ReadUUID(XmlTextReader reader, string name)
        {
            UUID id;
            string idStr;

            reader.ReadStartElement(name);
            if (reader.Name == "")
                idStr = reader.ReadString();
            else
                idStr = reader.ReadElementString(reader.Name == "Guid" ? "Guid" : "UUID");
            reader.ReadEndElement();

            UUID.TryParse(idStr, out id);

            return id;
        }

        Vector3 ReadVector(XmlTextReader reader, string name)
        {
            Vector3 vec;

            reader.ReadStartElement(name);
            vec.X = reader.ReadElementContentAsFloat(reader.Name, string.Empty); // X or x
            vec.Y = reader.ReadElementContentAsFloat(reader.Name, string.Empty); // Y or y
            vec.Z = reader.ReadElementContentAsFloat(reader.Name, string.Empty); // Z or z
            reader.ReadEndElement();

            return vec;
        }

        Quaternion ReadQuaternion(XmlTextReader reader, string name)
        {
            Quaternion quat = new Quaternion();

            reader.ReadStartElement(name);
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                switch (reader.Name.ToLower())
                {
                    case "x":
                        quat.X = reader.ReadElementContentAsFloat(reader.Name, string.Empty);
                        break;
                    case "y":
                        quat.Y = reader.ReadElementContentAsFloat(reader.Name, string.Empty);
                        break;
                    case "z":
                        quat.Z = reader.ReadElementContentAsFloat(reader.Name, string.Empty);
                        break;
                    case "w":
                        quat.W = reader.ReadElementContentAsFloat(reader.Name, string.Empty);
                        break;
                }
            }

            reader.ReadEndElement();

            return quat;
        }

        TaskInventoryDictionary ReadTaskInventory(XmlTextReader reader, string name)
        {
            TaskInventoryDictionary tinv = new TaskInventoryDictionary();

            reader.ReadStartElement(name, string.Empty);

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return tinv;
            }

            while (reader.Name == "TaskInventoryItem")
            {
                reader.ReadStartElement("TaskInventoryItem", string.Empty); // TaskInventory

                TaskInventoryItem item = new TaskInventoryItem();
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    TaskInventoryXmlProcessor p = null;
                    try
                    {
                        if (m_TaskInventoryXmlProcessors.TryGetValue(reader.Name, out p))
                            p(item, reader);
                        else
                        {
                            //MainConsole.Instance.DebugFormat("[SceneObjectSerializer]: caught unknown element in TaskInventory {0}, {1}", reader.Name, reader.Value);
                            reader.ReadOuterXml();
                        }
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.DebugFormat(
                            "[Serializer]: exception while parsing Inventory Items {0}: {1}",
                            reader.Name, e);
                    }
                }
                reader.ReadEndElement(); // TaskInventoryItem

                tinv.Add(item.ItemID, item);
            }

            if (reader.NodeType == XmlNodeType.EndElement)
                reader.ReadEndElement(); // TaskInventory

            return tinv;
        }

        PrimitiveBaseShape ReadShape(XmlTextReader reader, string name)
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape();

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return shape;
            }

            reader.ReadStartElement(name, string.Empty); // Shape

            string nodeName = string.Empty;
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                nodeName = reader.Name;
                //MainConsole.Instance.DebugFormat("[XXX] Processing: {0}", reader.Name); 
                ShapeXmlProcessor p = null;
                if (m_ShapeXmlProcessors.TryGetValue(reader.Name, out p))
                {
                    try
                    {
                        p(shape, reader);
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.DebugFormat(
                            "[Serializer]: exception while parsing Shape {0}: {1}", nodeName, e);
                        if (reader.NodeType == XmlNodeType.EndElement)
                            reader.Read();
                    }
                }
                else
                {
                    // MainConsole.Instance.DebugFormat("[SceneObjectSerializer]: caught unknown element in Shape {0}", reader.Name);
                    reader.ReadOuterXml();
                }
            }

            reader.ReadEndElement(); // Shape

            return shape;
        }

        void ReadVehicleSettings(SceneObjectPart obj, XmlTextReader reader)
        {
            reader.ReadStartElement("Vehicle");
            obj.VehicleType = reader.ReadElementContentAsInt ("VehicleType", string.Empty);

            if (obj.VehicleType > 0)
            {
                reader.ReadStartElement("VehicleSettings", string.Empty);
                reader.ReadStartElement("VehicleFlags", string.Empty);

                int nodeName = 0;
                OSDArray partFlags = new OSDArray ();
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    partFlags.Add(reader.ReadElementContentAsInt("F"+nodeName, string.Empty));
                    nodeName++;
                }
                obj.VehicleFlags = partFlags;
                reader.ReadEndElement(); // VehicleFlags

                reader.ReadStartElement("VehicleParameters", string.Empty);
                while (reader.NodeType != XmlNodeType.EndElement)
                {
                    int key = int.Parse(reader.Name.Substring(1));
                    switch (key)
                    {
                    case 44:            // REFERENCE_FRAME
                    case 46:            // ROLL_FRAME
                        obj.SetVehicleRotationParam(key, ReadQuaternion(reader,reader.Name));
                        break;
                    case 16:    // LINEAR_FRICTION_TIMESCALE:
                    case 17:    // ANGULAR_FRICTION_TIMESCALE:
                    case 18:    // LINEAR_MOTOR_DIRECTION:
                    case 19:    // ANGULAR_MOTOR_DIRECTION:
                    case 20:    // LINEAR_MOTOR_OFFSET:
                    case 45:    // BLOCK_EXIT:
                        obj.SetVehicleVectorParam(key, ReadVector(reader,reader.Name));
                        break;
                    default:
                        obj.SetVehicleFloatParam(key, reader.ReadElementContentAsFloat(reader.Name, string.Empty));
                        break;
                    }
                }
                reader.ReadEndElement(); //VehicleParameters
                reader.ReadEndElement(); //VehicleSettings
            }
            reader.ReadEndElement(); //Vehicle
        }



        #region SOPXmlProcessors

        void ProcessAllowedDrop(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.AllowedDrop = reader.ReadElementContentAsBoolean("AllowedDrop", string.Empty);
        }

        void ProcessCreatorID(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.CreatorID = ReadUUID(reader, "CreatorID");
        }

        void ProcessCreatorData(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.CreatorData = reader.ReadElementContentAsString("CreatorData", string.Empty);
        }

        void ProcessInventorySerial(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.InventorySerial = uint.Parse(reader.ReadElementContentAsString("InventorySerial", string.Empty));
        }

        void ProcessTaskInventory(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.TaskInventory = ReadTaskInventory(reader, "TaskInventory");
        }

        void ProcessUUID(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.UUID = ReadUUID(reader, "UUID");
        }

        void ProcessLocalId(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.LocalId = (uint) reader.ReadElementContentAsLong("LocalId", string.Empty);
        }

        void ProcessName(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Name = reader.ReadElementString("Name");
        }

        void ProcessMaterial(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Material = (byte) reader.ReadElementContentAsInt("Material", string.Empty);
        }

        void ProcessPassCollisions(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.PassCollisions = reader.ReadElementContentAsInt("PassCollisions", string.Empty);
        }

        void ProcessPassTouch(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.PassTouch = reader.ReadElementContentAsInt("PassTouch", string.Empty);
        }

        void ProcessScriptAccessPin(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.ScriptAccessPin = reader.ReadElementContentAsInt("ScriptAccessPin", string.Empty);
        }

        void ProcessGroupPosition(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.FixGroupPosition(ReadVector(reader, "GroupPosition"), false);
        }

        void ProcessOffsetPosition(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.FixOffsetPosition(ReadVector(reader, "OffsetPosition"), true);
        }

        void ProcessRotationOffset(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.RotationOffset = ReadQuaternion(reader, "RotationOffset");
        }

        void ProcessVelocity(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Velocity = ReadVector(reader, "Velocity");
        }

        void ProcessAngularVelocity(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.AngularVelocity = ReadVector(reader, "AngularVelocity");
        }

        void ProcessAcceleration(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Acceleration = ReadVector(reader, "Acceleration");
        }

        void ProcessDescription(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Description = reader.ReadElementString("Description");
        }

        void ProcessColor(SceneObjectPart obj, XmlTextReader reader)
        {
            reader.ReadStartElement("Color");
            if (reader.Name == "R")
            {
                float r = reader.ReadElementContentAsFloat("R", string.Empty);
                float g = reader.ReadElementContentAsFloat("G", string.Empty);
                float b = reader.ReadElementContentAsFloat("B", string.Empty);
                float a = reader.ReadElementContentAsFloat("A", string.Empty);
                obj.Color = Color.FromArgb((int) a, (int) r, (int) g, (int) b);
                reader.ReadEndElement();
            }
        }

        void ProcessText(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Text = reader.ReadElementString("Text", string.Empty);
        }

        void ProcessSitName(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.SitName = reader.ReadElementString("SitName", string.Empty);
        }

        void ProcessTouchName(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.TouchName = reader.ReadElementString("TouchName", string.Empty);
        }

        void ProcessLinkNum(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.LinkNum = reader.ReadElementContentAsInt("LinkNum", string.Empty);
        }

        void ProcessClickAction(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.ClickAction = (byte) reader.ReadElementContentAsInt("ClickAction", string.Empty);
        }

        void ProcessShape(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Shape = ReadShape(reader, "Shape");
        }

        void ProcessScale(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Scale = ReadVector(reader, "Scale");
        }

        void ProcessUpdateFlag(SceneObjectPart obj, XmlTextReader reader)
        {
            reader.Read();
            //InternalUpdateFlags flags = (InternalUpdateFlags)(byte)reader.ReadElementContentAsInt("UpdateFlag", String.Empty);
        }

        void ProcessSitTargetOrientation(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.SitTargetOrientation = ReadQuaternion(reader, "SitTargetOrientation");
        }

        void ProcessSitTargetPosition(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.SitTargetPosition = ReadVector(reader, "SitTargetPosition");
        }

        void ProcessParentID(SceneObjectPart obj, XmlTextReader reader)
        {
            string str = reader.ReadElementContentAsString("ParentID", string.Empty);
            obj.ParentID = Convert.ToUInt32(str);
        }

        void ProcessCreationDate(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.CreationDate = reader.ReadElementContentAsInt("CreationDate", string.Empty);
        }

        void ProcessCategory(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.Category = uint.Parse(reader.ReadElementContentAsString("Category", string.Empty));
        }

        void ProcessSalePrice(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.SalePrice = reader.ReadElementContentAsInt("SalePrice", string.Empty);
        }

        void ProcessObjectSaleType(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.ObjectSaleType = (byte) reader.ReadElementContentAsInt("ObjectSaleType", string.Empty);
        }

        void ProcessOwnershipCost(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.OwnershipCost = reader.ReadElementContentAsInt("OwnershipCost", string.Empty);
        }

        void ProcessGroupID(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.GroupID = ReadUUID(reader, "GroupID");
        }

        void ProcessOwnerID(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.OwnerID = ReadUUID(reader, "OwnerID");
        }

        void ProcessLastOwnerID(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.LastOwnerID = ReadUUID(reader, "LastOwnerID");
        }

        void ProcessBaseMask(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.BaseMask = uint.Parse(reader.ReadElementContentAsString("BaseMask", string.Empty));
        }

        void ProcessOwnerMask(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.OwnerMask = uint.Parse(reader.ReadElementContentAsString("OwnerMask", string.Empty));
        }

        void ProcessGroupMask(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.GroupMask = uint.Parse(reader.ReadElementContentAsString("GroupMask", string.Empty));
        }

        void ProcessEveryoneMask(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.EveryoneMask = uint.Parse(reader.ReadElementContentAsString("EveryoneMask", string.Empty));
        }

        void ProcessNextOwnerMask(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.NextOwnerMask = uint.Parse(reader.ReadElementContentAsString("NextOwnerMask", string.Empty));
        }

        void ProcessFlags(SceneObjectPart obj, XmlTextReader reader)
        {
            string value = reader.ReadElementContentAsString("Flags", string.Empty);
            // !!!!! to deal with flags without commas
            if (value.Contains(" ") && !value.Contains(","))
                value = value.Replace(" ", ", ");
            obj.Flags = (PrimFlags) Enum.Parse(typeof (PrimFlags), value);
        }

        void ProcessCollisionSound(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.CollisionSound = ReadUUID(reader, "CollisionSound");
        }

        void ProcessCollisionSoundVolume(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.CollisionSoundVolume =
                float.Parse(reader.ReadElementContentAsString("CollisionSoundVolume", string.Empty));
        }

        void ProcessMediaUrl(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.MediaUrl = reader.ReadElementContentAsString("MediaUrl", string.Empty);
        }

        void ProcessTextureAnimation(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.TextureAnimation =
                Convert.FromBase64String(reader.ReadElementContentAsString("TextureAnimation", string.Empty));
        }

        void ProcessParticleSystem(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.ParticleSystem =
                Convert.FromBase64String(reader.ReadElementContentAsString("ParticleSystem", string.Empty));
        }

        void ProcessPayPrice0(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.PayPrice[0] = int.Parse(reader.ReadElementContentAsString("PayPrice0", string.Empty));
        }

        void ProcessPayPrice1(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.PayPrice[1] = int.Parse(reader.ReadElementContentAsString("PayPrice1", string.Empty));
        }

        void ProcessPayPrice2(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.PayPrice[2] = int.Parse(reader.ReadElementContentAsString("PayPrice2", string.Empty));
        }

        void ProcessPayPrice3(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.PayPrice[3] = int.Parse(reader.ReadElementContentAsString("PayPrice3", string.Empty));
        }

        void ProcessPayPrice4(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.PayPrice[4] = int.Parse(reader.ReadElementContentAsString("PayPrice4", string.Empty));
        }

        void ProcessFromUserInventoryAssetID(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.FromUserInventoryAssetID = ReadUUID(reader, "FromUserInventoryAssetID");
        }

        void ProcessFromUserInventoryItemID(SceneObjectPart obj, XmlTextReader reader)
        {
            obj.FromUserInventoryItemID = ReadUUID(reader, "FromUserInventoryItemID");
        }
        void GenericBool(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            bool val = reader.ReadElementContentAsBoolean(name, "");
            SOPType.GetProperty(name).SetValue(obj, val, null);
        }

        void GenericVector3(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            SOPType.GetProperty(name).SetValue(obj, ReadVector(reader, name), null);
        }

        void GenericInt(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            SOPType.GetProperty(name)
                   .SetValue(obj, int.Parse(reader.ReadElementContentAsString(name, string.Empty)), null);
        }

        void GenericDouble(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            SOPType.GetProperty(name)
                   .SetValue(obj, double.Parse(reader.ReadElementContentAsString(name, string.Empty)), null);
        }

        void GenericFloat(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            SOPType.GetProperty(name)
                   .SetValue(obj, float.Parse(reader.ReadElementContentAsString(name, string.Empty)), null);
        }

        void ReadProtobuf<T>(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(reader.ReadElementString());
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    T list = ProtoBuf.Serializer.Deserialize<T>(stream);
                    if (list != null)
                        SOPType.GetProperty(name).SetValue(obj, list, null);
                }
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Debug("[Serializer]: Failed to parse " + name + ": " + ex);
            }
        }

        void ReadStateSave(SceneObjectPart obj, XmlTextReader reader, Type sopType)
        {
            reader.ReadStartElement();

            Dictionary<UUID, StateSave> states = new Dictionary<UUID, StateSave>();

            while (reader.IsStartElement("StateSave"))
            {
                reader.ReadStartElement("StateSave");

                StateSave state = new StateSave();

                while (!reader.HasValue)
                {
                    string name = reader.Name;
                    if (name == "StateSave")
                        break;
                    string val = reader.ReadElementContentAsString();

                    switch (name)
                    {
                        case "AssemblyName":
                            state.AssemblyName = val;
                            break;
                        case "Compiled":
                            state.Compiled = bool.Parse(val);
                            break;
                        case "Disabled":
                            state.Disabled = bool.Parse(val);
                            break;
                        case "ItemID":
                            state.ItemID = UUID.Parse(val);
                            break;
                        case "MinEventDelay":
                            state.MinEventDelay = double.Parse(val);
                            break;
                        case "PermsGranter":
                            state.PermsGranter = UUID.Parse(val);
                            break;
                        case "PermsMask":
                            state.PermsMask = int.Parse(val);
                            break;
                        case "Plugins":
                            state.Plugins = val;
                            break;
                        case "Running":
                            state.Running = bool.Parse(val);
                            break;
                        case "Source":
                            state.Source = val;
                            break;
                        case "State":
                            state.State = val;
                            break;
                        case "TargetOmegaWasSet":
                            state.TargetOmegaWasSet = bool.Parse(val);
                            break;
                        case "UserInventoryID":
                            state.UserInventoryID = UUID.Parse(val);
                            break;
                        case "Variables":
                            state.Variables = val;
                            break;
                    }
                }

                states.Add(state.ItemID, state);

                reader.ReadEndElement();
            }
            reader.ReadEndElement();

            obj.StateSaves = states;
        }

        void GenericByte(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            SOPType.GetProperty(name)
                   .SetValue(obj, byte.Parse(reader.ReadElementContentAsString(name, string.Empty)), null);
        }

        void GenericUUID(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            SOPType.GetProperty(name).SetValue(obj, ReadUUID(reader, name), null);
        }

        void GenericQuaternion(SceneObjectPart obj, XmlTextReader reader, string name, Type SOPType)
        {
            SOPType.GetProperty(name).SetValue(obj, ReadQuaternion(reader, name), null);
        }

        #endregion

        #region TaskInventoryXmlProcessors

        void ProcessTIAssetID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.AssetID = ReadUUID(reader, "AssetID");
        }

        void ProcessTIBasePermissions(TaskInventoryItem item, XmlTextReader reader)
        {
            item.BasePermissions = uint.Parse(reader.ReadElementContentAsString("BasePermissions", string.Empty));
        }

        void ProcessTICreationDate(TaskInventoryItem item, XmlTextReader reader)
        {
            item.CreationDate = uint.Parse(reader.ReadElementContentAsString("CreationDate", string.Empty));
        }

        void ProcessTICreatorID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.CreatorID = ReadUUID(reader, "CreatorID");
        }

        void ProcessTICreatorData(TaskInventoryItem item, XmlTextReader reader)
        {
            item.CreatorData = reader.ReadElementContentAsString("CreatorData", string.Empty);
        }

        void ProcessTIDescription(TaskInventoryItem item, XmlTextReader reader)
        {
            item.Description = reader.ReadElementContentAsString("Description", string.Empty);
        }

        void ProcessTIEveryonePermissions(TaskInventoryItem item, XmlTextReader reader)
        {
            item.EveryonePermissions = uint.Parse(reader.ReadElementContentAsString("EveryonePermissions", string.Empty));
        }

        void ProcessTIFlags(TaskInventoryItem item, XmlTextReader reader)
        {
            item.Flags = uint.Parse(reader.ReadElementContentAsString("Flags", string.Empty));
        }

        void ProcessTIGroupID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.GroupID = ReadUUID(reader, "GroupID");
        }

        void ProcessTIGroupPermissions(TaskInventoryItem item, XmlTextReader reader)
        {
            item.GroupPermissions = uint.Parse(reader.ReadElementContentAsString("GroupPermissions", string.Empty));
        }

        void ProcessTIInvType(TaskInventoryItem item, XmlTextReader reader)
        {
            item.InvType = reader.ReadElementContentAsInt("InvType", string.Empty);
        }

        void ProcessTIItemID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.ItemID = ReadUUID(reader, "ItemID");
        }

        void ProcessTIOldItemID(TaskInventoryItem item, XmlTextReader reader)
        {
            //Disable this, if we are rezzing from inventory, we want to get a new ItemID for next time
            //item.OldItemID = ReadUUID (reader, "OldItemID");
            ReadUUID(reader, "OldItemID");
        }

        void ProcessTILastOwnerID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.LastOwnerID = ReadUUID(reader, "LastOwnerID");
        }

        void ProcessTIName(TaskInventoryItem item, XmlTextReader reader)
        {
            item.Name = reader.ReadElementContentAsString("Name", string.Empty);
        }

        void ProcessTINextPermissions(TaskInventoryItem item, XmlTextReader reader)
        {
            item.NextPermissions = uint.Parse(reader.ReadElementContentAsString("NextPermissions", string.Empty));
        }

        void ProcessTIOwnerID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.OwnerID = ReadUUID(reader, "OwnerID");
        }

        void ProcessTICurrentPermissions(TaskInventoryItem item, XmlTextReader reader)
        {
            item.CurrentPermissions = uint.Parse(reader.ReadElementContentAsString("CurrentPermissions", string.Empty));
        }

        void ProcessTIParentID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.ParentID = ReadUUID(reader, "ParentID");
        }

        void ProcessTIParentPartID(TaskInventoryItem item, XmlTextReader reader)
        {
            item.ParentPartID = ReadUUID(reader, "ParentPartID");
        }

        void ProcessTIPermsGranter(TaskInventoryItem item, XmlTextReader reader)
        {
            item.PermsGranter = ReadUUID(reader, "PermsGranter");
        }

        void ProcessTIPermsMask(TaskInventoryItem item, XmlTextReader reader)
        {
            item.PermsMask = reader.ReadElementContentAsInt("PermsMask", string.Empty);
        }

        void ProcessTIType(TaskInventoryItem item, XmlTextReader reader)
        {
            item.Type = reader.ReadElementContentAsInt("Type", string.Empty);
        }

        void ProcessTIOwnerChanged(TaskInventoryItem item, XmlTextReader reader)
        {
            item.OwnerChanged = reader.ReadElementContentAsBoolean("OwnerChanged", string.Empty);
        }

        #endregion

        #region ShapeXmlProcessors

        void ProcessShpProfileCurve(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.ProfileCurve = (byte) reader.ReadElementContentAsInt("ProfileCurve", string.Empty);
        }

        void ProcessShpTextureEntry(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            byte[] teData = Convert.FromBase64String(reader.ReadElementString("TextureEntry"));
            shp.Textures = new Primitive.TextureEntry(teData, 0, teData.Length);
        }

        void ProcessShpExtraParams(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.ExtraParams = Convert.FromBase64String(reader.ReadElementString("ExtraParams"));
        }

        void ProcessShpPathBegin(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathBegin = (ushort) reader.ReadElementContentAsInt("PathBegin", string.Empty);
        }

        void ProcessShpPathCurve(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathCurve = (byte) reader.ReadElementContentAsInt("PathCurve", string.Empty);
        }

        void ProcessShpPathEnd(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathEnd = (ushort) reader.ReadElementContentAsInt("PathEnd", string.Empty);
        }

        void ProcessShpPathRadiusOffset(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathRadiusOffset = (sbyte) reader.ReadElementContentAsInt("PathRadiusOffset", string.Empty);
        }

        void ProcessShpPathRevolutions(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathRevolutions = (byte) reader.ReadElementContentAsInt("PathRevolutions", string.Empty);
        }

        void ProcessShpPathScaleX(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathScaleX = (byte) reader.ReadElementContentAsInt("PathScaleX", string.Empty);
        }

        void ProcessShpPathScaleY(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathScaleY = (byte) reader.ReadElementContentAsInt("PathScaleY", string.Empty);
        }

        void ProcessShpPathShearX(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathShearX = (byte) reader.ReadElementContentAsInt("PathShearX", string.Empty);
        }

        void ProcessShpPathShearY(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathShearY = (byte) reader.ReadElementContentAsInt("PathShearY", string.Empty);
        }

        void ProcessShpPathSkew(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathSkew = (sbyte) reader.ReadElementContentAsInt("PathSkew", string.Empty);
        }

        void ProcessShpPathTaperX(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathTaperX = (sbyte) reader.ReadElementContentAsInt("PathTaperX", string.Empty);
        }

        void ProcessShpPathTaperY(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathTaperY = (sbyte) reader.ReadElementContentAsInt("PathTaperY", string.Empty);
        }

        void ProcessShpPathTwist(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathTwist = (sbyte) reader.ReadElementContentAsInt("PathTwist", string.Empty);
        }

        void ProcessShpPathTwistBegin(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PathTwistBegin = (sbyte) reader.ReadElementContentAsInt("PathTwistBegin", string.Empty);
        }

        void ProcessShpPCode(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.PCode = (byte) reader.ReadElementContentAsInt("PCode", string.Empty);
        }

        void ProcessShpProfileBegin(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.ProfileBegin = (ushort) reader.ReadElementContentAsInt("ProfileBegin", string.Empty);
        }

        void ProcessShpProfileEnd(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.ProfileEnd = (ushort) reader.ReadElementContentAsInt("ProfileEnd", string.Empty);
        }

        void ProcessShpProfileHollow(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.ProfileHollow = (ushort) reader.ReadElementContentAsInt("ProfileHollow", string.Empty);
        }

        void ProcessShpScale(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.Scale = ReadVector(reader, "Scale");
        }

        void ProcessShpState(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.State = (byte) reader.ReadElementContentAsInt("State", string.Empty);
        }

        void ProcessShpProfileShape(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            string value = reader.ReadElementContentAsString("ProfileShape", string.Empty);
            // !!!!! to deal with flags without commas
            if (value.Contains(" ") && !value.Contains(","))
                value = value.Replace(" ", ", ");
            shp.ProfileShape = (ProfileShape) Enum.Parse(typeof (ProfileShape), value);
        }

        void ProcessShpHollowShape(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            string value = reader.ReadElementContentAsString("HollowShape", string.Empty);
            // !!!!! to deal with flags without commas
            if (value.Contains(" ") && !value.Contains(","))
                value = value.Replace(" ", ", ");
            shp.HollowShape = (HollowShape) Enum.Parse(typeof (HollowShape), value);
        }

        void ProcessShpSculptTexture(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.SculptTexture = ReadUUID(reader, "SculptTexture");
        }

        void ProcessShpSculptType(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.SculptType = (byte) reader.ReadElementContentAsInt("SculptType", string.Empty);
        }

        void ProcessShpSculptData(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.SculptData = Convert.FromBase64String(reader.ReadElementString("SculptData"));
        }

        void ProcessShpFlexiSoftness(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiSoftness = reader.ReadElementContentAsInt("FlexiSoftness", string.Empty);
        }

        void ProcessShpFlexiTension(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiTension = reader.ReadElementContentAsFloat("FlexiTension", string.Empty);
        }

        void ProcessShpFlexiDrag(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiDrag = reader.ReadElementContentAsFloat("FlexiDrag", string.Empty);
        }

        void ProcessShpFlexiGravity(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiGravity = reader.ReadElementContentAsFloat("FlexiGravity", string.Empty);
        }

        void ProcessShpFlexiWind(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiWind = reader.ReadElementContentAsFloat("FlexiWind", string.Empty);
        }

        void ProcessShpFlexiForceX(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiForceX = reader.ReadElementContentAsFloat("FlexiForceX", string.Empty);
        }

        void ProcessShpFlexiForceY(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiForceY = reader.ReadElementContentAsFloat("FlexiForceY", string.Empty);
        }

        void ProcessShpFlexiForceZ(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiForceZ = reader.ReadElementContentAsFloat("FlexiForceZ", string.Empty);
        }

        void ProcessShpLightColorR(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightColorR = reader.ReadElementContentAsFloat("LightColorR", string.Empty);
        }

        void ProcessShpLightColorG(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightColorG = reader.ReadElementContentAsFloat("LightColorG", string.Empty);
        }

        void ProcessShpLightColorB(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightColorB = reader.ReadElementContentAsFloat("LightColorB", string.Empty);
        }

        void ProcessShpLightColorA(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightColorA = reader.ReadElementContentAsFloat("LightColorA", string.Empty);
        }

        void ProcessShpLightRadius(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightRadius = reader.ReadElementContentAsFloat("LightRadius", string.Empty);
        }

        void ProcessShpLightCutoff(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightCutoff = reader.ReadElementContentAsFloat("LightCutoff", string.Empty);
        }

        void ProcessShpLightFalloff(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightFalloff = reader.ReadElementContentAsFloat("LightFalloff", string.Empty);
        }

        void ProcessShpLightIntensity(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightIntensity = reader.ReadElementContentAsFloat("LightIntensity", string.Empty);
        }

        void ProcessShpFlexiEntry(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.FlexiEntry = reader.ReadElementContentAsBoolean("FlexiEntry", string.Empty);
        }

        void ProcessShpLightEntry(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.LightEntry = reader.ReadElementContentAsBoolean("LightEntry", string.Empty);
        }

        void ProcessShpSculptEntry(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            shp.SculptEntry = reader.ReadElementContentAsBoolean("SculptEntry", string.Empty);
        }

        void ProcessShpMedia(PrimitiveBaseShape shp, XmlTextReader reader)
        {
            string value = reader.ReadElementContentAsString("Media", string.Empty);
            shp.Media = PrimitiveBaseShape.MediaList.FromXml(value);
        }


        #endregion

        #region Nested type: Serialization

        delegate string Serialization(SceneObjectPart part);

        #endregion

        #region Nested type: ShapeXmlProcessor

        delegate void ShapeXmlProcessor(PrimitiveBaseShape shape, XmlTextReader reader);

        #endregion

        #region Nested type: TaskInventoryXmlProcessor

        delegate void TaskInventoryXmlProcessor(TaskInventoryItem item, XmlTextReader reader);

        #endregion

        #endregion
    }
}
