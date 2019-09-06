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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using WhiteCore.ScriptEngine.DotNetEngine.Plugins;
using WhiteCore.ScriptEngine.DotNetEngine.Runtime;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using LSL_Float = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger;
using LSL_Key = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.list;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    /// <summary>
    ///     Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {

        const double DoubleDifference = .0000005;
        static bool FloatAlmostEqual (LSL_Float valA, LSL_Float valB)
        {
            return Math.Abs (valA - valB) <= DoubleDifference;
        }

        protected IScriptModulePlugin m_ScriptEngine;
        protected ISceneChildEntity m_host;
        protected uint m_localID;
        protected UUID m_itemID;
        protected bool throwErrorOnNotImplemented = true;
        protected float m_ScriptDelayFactor = 1.0f;
        protected float m_ScriptDistanceFactor = 1.0f;
        protected float m_MinTimerInterval = 0.1f;

        protected double m_timer = Util.GetTimeStampMS ();
        protected bool m_waitingForScriptAnswer = false;
        protected bool m_automaticLinkPermission = false;
        protected IMessageTransferModule m_TransferModule = null;
        protected int m_notecardLineReadCharsMax = 255;
        protected int m_scriptConsoleChannel = 0;
        protected bool m_scriptConsoleChannelEnabled = false;
        protected IUrlModule m_UrlModule = null;
        internal ScriptProtectionModule ScriptProtection;
        protected IWorldComm m_comms = null;

        // Sleep parameters
        protected int EMAIL_PAUSE_TIME = 20;  // documented delay value for smtp.
        protected int m_sleepMsOnSetTexture = 200;
        protected int m_sleepMsOnSetLinkTexture = 200;
        protected int m_sleepMsOnScaleTexture = 200;
        protected int m_sleepMsOnOffsetTexture = 200;
        protected int m_sleepMsOnRotateTexture = 200;
        protected int m_sleepMsOnSetPos = 200;
        protected int m_sleepMsOnSetRot = 200;
        protected int m_sleepMsOnSetLocalRot = 200;
        protected int m_sleepMsOnPreloadSound = 1000;
        protected int m_sleepMsOnMakeExplosion = 100;
        protected int m_sleepMsOnMakeFountain = 100;
        protected int m_sleepMsOnMakeSmoke = 100;
        protected int m_sleepMsOnMakeFire = 100;
        protected int m_sleepMsOnRezAtRoot = 100;
        protected int m_sleepMsOnInstantMessage = 2000;
        protected int m_sleepMsOnEmail = 20000;
        protected int m_sleepMsOnCreateLink = 1000;
        protected int m_sleepMsOnGiveInventory = 3000;
        protected int m_sleepMsOnRequestAgentData = 100;
        protected int m_sleepMsOnRequestInventoryData = 1000;
        protected int m_sleepMsOnTeleportAgentHome = 5000;
        protected int m_sleepMsOnTextBox = 1000;
        protected int m_sleepMsOnAdjustSoundVolume = 100;
        protected int m_sleepMsOnEjectFromLand = 5000;
        protected int m_sleepMsOnAddToLandPassList = 100;
        protected int m_sleepMsOnDialog = 1000;
        protected int m_sleepMsOnRemoteLoadScript = 3000;
        protected int m_sleepMsOnRemoteLoadScriptPin = 3000;
        protected int m_sleepMsOnOpenRemoteDataChannel = 1000;
        protected int m_sleepMsOnSendRemoteData = 3000;                 // was 100
        protected int m_sleepMsOnRemoteDataReply = 3000;                // was 100
        protected int m_sleepMsOnCloseRemoteDataChannel = 1000;
        protected int m_sleepMsOnSetPrimitiveParams = 200;
        protected int m_sleepMsOnSetLinkPrimitiveParams = 200;
        protected int m_sleepMsOnXorBase64Strings = 300;
        protected int m_sleepMsOnSetParcelMusicURL = 2000;
        protected int m_sleepMsOnGetPrimMediaParams = 1000;
        //protected int m_sleepMsOnGetLinkMedia = 1000;
        protected int m_sleepMsOnSetPrimMediaParams = 1000;
        //protected int m_sleepMsOnSetLinkMedia = 1000;
        protected int m_sleepMsOnClearPrimMedia = 1000;
        //protected int m_sleepMsOnClearLinkMedia = 1000;
        protected int m_sleepMsOnRequestSimulatorData = 1000;
        protected int m_sleepMsOnLoadURL = 10000;
        protected int m_sleepMsOnParcelMediaCommandList = 2000;
        protected int m_sleepMsOnParcelMediaQuery = 2000;
        protected int m_sleepMsOnModPow = 1000;
        protected int m_sleepMsOnSetPrimURL = 2000;
        protected int m_sleepMsOnRefreshPrimURL = 20000;
        protected int m_sleepMsOnMapDestination = 1000;
        protected int m_sleepMsOnAddToLandBanList = 100;
        protected int m_sleepMsOnRemoveFromLandPassList = 100;
        protected int m_sleepMsOnRemoveFromLandBanList = 100;
        protected int m_sleepMsOnResetLandBanList = 100;
        protected int m_sleepMsOnResetLandPassList = 100;
        protected int m_sleepMsOnGetParcelPrimOwners = 2000;
        protected int m_sleepMsOnGetNumberOfNotecardLines = 100;
        protected int m_sleepMsOnGetNotecardLine = 100;
        protected int m_sleepMsOnRequestUserName = 100;

        // MUST be a ref type
        public class UserInfoCacheEntry
        {
            public int time;
            public UserAccount account;
            public UserInfo pinfo;
        }

        protected Dictionary<UUID, UserInfoCacheEntry> m_userInfoCache =
            new Dictionary<UUID, UserInfoCacheEntry> ();

        /// <summary>
        /// Determines whether OpenSim params can be used with
        /// llSetPrimitiveParams etc.
        /// </summary>
        protected bool m_allowOpenSimParams = false;

        public void Initialize (IScriptModulePlugin ScriptEngine, ISceneChildEntity host, uint localID, UUID itemID,
                               ScriptProtectionModule module)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;
            ScriptProtection = module;

            m_ScriptDelayFactor = m_ScriptEngine.Config.GetFloat ("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor = m_ScriptEngine.Config.GetFloat ("ScriptDistanceLimitFactor", 1.0f);
            m_MinTimerInterval = m_ScriptEngine.Config.GetFloat ("MinTimerInterval", 0.5f);
            m_automaticLinkPermission = m_ScriptEngine.Config.GetBoolean ("AutomaticLinkPermission", false);
            m_allowOpenSimParams = m_ScriptEngine.Config.GetBoolean ("AllowOpenSimParamsInLLFunctions", false);
            m_notecardLineReadCharsMax = m_ScriptEngine.Config.GetInt ("NotecardLineReadCharsMax", 255);
            if (m_notecardLineReadCharsMax > 65535)
                m_notecardLineReadCharsMax = 65535;

            m_TransferModule = World.RequestModuleInterface<IMessageTransferModule> ();
            m_UrlModule = World.RequestModuleInterface<IUrlModule> ();
            m_comms = World.RequestModuleInterface<IWorldComm> ();

            m_sleepMsOnEmail = EMAIL_PAUSE_TIME * 1000;

        }

        public IScriptApi Copy ()
        {
            return new LSL_Api ();
        }

        public string Name {
            get { return "ll"; }
        }

        public string InterfaceName {
            get { return "ILSL_Api"; }
        }

        /// <summary>
        ///     We don't have to add any assemblies here
        /// </summary>
        public string [] ReferencedAssemblies {
            get { return new string [0]; }
        }

        /// <summary>
        ///     We use the default namespace, so we don't have any to add
        /// </summary>
        public string [] NamespaceAdditions {
            get { return new string [0]; }
        }

        public void Dispose ()
        {
        }

        public override object InitializeLifetimeService ()
        {
            ILease lease = (ILease)base.InitializeLifetimeService ();

            if (lease != null && lease.CurrentState == LeaseState.Initial) {
                lease.InitialLeaseTime = TimeSpan.FromMinutes (0);
                //                lease.RenewOnCallTime = TimeSpan.FromSeconds(10.0);
                //                lease.SponsorshipTimeout = TimeSpan.FromMinutes(1.0);
            }
            return lease;
        }

        protected virtual void ScriptSleep (int delay)
        {
            delay = (int)(delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;

            Thread.Sleep (delay);
        }

        /// <summary>
        ///     This is the new sleep implementation that allows for us to not freeze the script thread while we run
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        protected DateTime PScriptSleep (int delay)
        {
            double dly = (delay * m_ScriptDelayFactor);
            if (dly <= DoubleDifference)
                return DateTime.Now;

            DateTime timeToStopSleeping = DateTime.Now.AddMilliseconds (dly);
            return timeToStopSleeping;
        }

        public IScene World {
            get { return m_host.ParentEntity.Scene; }
        }

        public void state (string newState)
        {
            m_ScriptEngine.SetState (m_itemID, newState);
            throw new EventAbortException ();
        }


        public List<ISceneChildEntity> GetLinkParts (int linkType)
        {
            List<ISceneChildEntity> ret = new List<ISceneChildEntity> { m_host };

            if (linkType == ScriptBaseClass.LINK_SET) {
                if (m_host.ParentEntity != null)
                    return new List<ISceneChildEntity> (m_host.ParentEntity.ChildrenEntities ());
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_ROOT) {
                if (m_host.ParentEntity != null) {
                    ret = new List<ISceneChildEntity> { m_host.ParentEntity.RootChild };
                    return ret;
                }
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_ALL_OTHERS) {
                if (m_host.ParentEntity == null)
                    return new List<ISceneChildEntity> ();
                ret = new List<ISceneChildEntity> (m_host.ParentEntity.ChildrenEntities ());
                if (ret.Contains (m_host))
                    ret.Remove (m_host);
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_ALL_CHILDREN) {
                if (m_host.ParentEntity == null)
                    return new List<ISceneChildEntity> ();
                ret = new List<ISceneChildEntity> (m_host.ParentEntity.ChildrenEntities ());
                if (ret.Contains (m_host.ParentEntity.RootChild))
                    ret.Remove (m_host.ParentEntity.RootChild);
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_THIS) {
                return ret;
            }

            if (linkType < 0 || m_host.ParentEntity == null)
                return new List<ISceneChildEntity> ();
            IEntity target = m_host.ParentEntity.GetLinkNumPart (linkType);
            if (target is ISceneChildEntity) {
                ret = new List<ISceneChildEntity> { target as ISceneChildEntity };
            }
            //No allowing scene presences to be found here
            return ret;
        }

        public List<IEntity> GetLinkPartsAndEntities (int linkType)
        {
            List<IEntity> ret = new List<IEntity> { m_host };

            if (linkType == ScriptBaseClass.LINK_SET) {
                if (m_host.ParentEntity != null) {
                    List<ISceneChildEntity> parts = new List<ISceneChildEntity> (m_host.ParentEntity.ChildrenEntities ());
                    return parts.ConvertAll (part => (IEntity)part);
                }
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_ROOT) {
                if (m_host.ParentEntity != null) {
                    ret = new List<IEntity> { m_host.ParentEntity.RootChild };
                    return ret;
                }
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_ALL_OTHERS) {
                if (m_host.ParentEntity == null)
                    return new List<IEntity> ();
                List<ISceneChildEntity> sceneobjectparts = new List<ISceneChildEntity> (m_host.ParentEntity.ChildrenEntities ());

                ret = sceneobjectparts.ConvertAll (part => (IEntity)part);

                if (ret.Contains (m_host))
                    ret.Remove (m_host);
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_ALL_CHILDREN) {
                if (m_host.ParentEntity == null)
                    return new List<IEntity> ();
                List<ISceneChildEntity> children = new List<ISceneChildEntity> (m_host.ParentEntity.ChildrenEntities ());

                ret = children.ConvertAll (part => (IEntity)part);

                if (ret.Contains (m_host.ParentEntity.RootChild))
                    ret.Remove (m_host.ParentEntity.RootChild);
                return ret;
            }

            if (linkType == ScriptBaseClass.LINK_THIS) {
                return ret;
            }

            if (linkType < 0 || m_host.ParentEntity == null)
                return new List<IEntity> ();
            IEntity target = m_host.ParentEntity.GetLinkNumPart (linkType);
            if (target == null)
                return new List<IEntity> ();

            ret = new List<IEntity> { target };

            return ret;
        }

        protected UUID InventorySelf ()
        {
            UUID invItemID = new UUID ();

            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Type == 10 && inv.Value.ItemID == m_itemID) {
                        invItemID = inv.Key;
                        break;
                    }
                }
            }

            return invItemID;
        }

        protected UUID InventoryKey (string name, AssetType type, bool throwExceptionIfDoesNotExist)
        {
            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == name && inv.Value.Type == (int)type) {
                        return inv.Value.AssetID;
                    }
                }
            }

            if (throwExceptionIfDoesNotExist) {
                IChatModule chatModule = World.RequestModuleInterface<IChatModule> ();
                if (chatModule != null)
                    chatModule.SimChat ("Could not find item '" + name + "'.",
                                       ChatTypeEnum.DebugChannel, 2147483647, m_host.AbsolutePosition,
                                       m_host.Name, m_host.UUID, false, World);
            }

            return UUID.Zero;
        }


        /// <summary>
        ///     accepts a valid UUID, -or- a name of an inventory item.
        ///     Returns a valid UUID or UUID.Zero if key invalid and item not found
        ///     in prim inventory.
        /// </summary>
        /// <param name="k"></param>
        /// <param name="throwExceptionIfDoesNotExist"></param>
        /// <returns></returns>
        protected UUID KeyOrName (string k, AssetType type, bool throwExceptionIfDoesNotExist)
        {
            UUID key = UUID.Zero;

            // if we can parse the string as a key, use it.
            if (UUID.TryParse (k, out key)) {
                TaskInventoryItem itm;
                lock (m_host.TaskInventory)
                    m_host.TaskInventory.TryGetValue (key, out itm);
                if (itm == null || itm.Type == (int)type)
                    return key;
                //The item was not of the right type
            }
            // else try to locate the name in inventory of object. found returns key,
            // not found returns UUID.Zero which will translate to the default particle texture
            return InventoryKey (k, type, throwExceptionIfDoesNotExist);
        }


        public string resolveName (UUID objecUUID)
        {
            // try for avatar name
            UserAccount userAcct = World.UserAccountService.GetUserAccount (World.RegionInfo.AllScopeIDs, objecUUID);
            if (userAcct.Valid)
                return userAcct.Name;

            // try for a scene object
            ISceneChildEntity SOP = World.GetSceneObjectPart (objecUUID);
            if (SOP != null)
                return SOP.Name;

            IEntity SensedObject;
            if (!World.Entities.TryGetValue (objecUUID, out SensedObject)) {
                IGroupsModule groups = World.RequestModuleInterface<IGroupsModule> ();
                if (groups != null) {
                    GroupRecord gr = groups.GetGroupRecord (objecUUID);
                    if (gr != null)
                        return gr.GroupName;
                }
                return string.Empty;
            }

            return SensedObject.Name;
        }


        protected void SetScale (ISceneChildEntity part, LSL_Vector scale)
        {
            if (part == null || part.ParentEntity == null || part.ParentEntity.IsDeleted)
                return;

            IOpenRegionSettingsModule WSModule =
                m_host.ParentEntity.Scene.RequestModuleInterface<IOpenRegionSettingsModule> ();
            if (WSModule != null) {
                float minSize = 0.01f;
                if (!FloatAlmostEqual (WSModule.MinimumPrimScale, -1))
                    minSize = WSModule.MinimumPrimScale;
                if (scale.x < minSize)
                    scale.x = minSize;
                if (scale.y < minSize)
                    scale.y = minSize;
                if (scale.z < minSize)
                    scale.z = minSize;

                if (part.ParentEntity.RootChild.PhysActor != null &&
                    part.ParentEntity.RootChild.PhysActor.IsPhysical &&
                    !FloatAlmostEqual (WSModule.MaximumPhysPrimScale, -1)) {
                    if (scale.x > WSModule.MaximumPhysPrimScale)
                        scale.x = WSModule.MaximumPhysPrimScale;
                    if (scale.y > WSModule.MaximumPhysPrimScale)
                        scale.y = WSModule.MaximumPhysPrimScale;
                    if (scale.z > WSModule.MaximumPhysPrimScale)
                        scale.z = WSModule.MaximumPhysPrimScale;
                }

                if (!FloatAlmostEqual (WSModule.MaximumPrimScale, -1)) {
                    if (scale.x > WSModule.MaximumPrimScale)
                        scale.x = WSModule.MaximumPrimScale;
                    if (scale.y > WSModule.MaximumPrimScale)
                        scale.y = WSModule.MaximumPrimScale;
                    if (scale.z > WSModule.MaximumPrimScale)
                        scale.z = WSModule.MaximumPrimScale;
                }
            }

            Vector3 tmp = part.Scale;
            tmp.X = (float)scale.x;
            tmp.Y = (float)scale.y;
            tmp.Z = (float)scale.z;
            part.Scale = tmp;
            part.ScheduleUpdate (PrimUpdateFlags.FindBest);
        }


        public void SetTexGen (ISceneChildEntity part, int face, int style)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            MappingType textype = MappingType.Default;
            if (style == (int)ScriptBaseClass.PRIM_TEXGEN_PLANAR)
                textype = MappingType.Planar;

            if (face >= 0 && face < GetNumberOfSides (part)) {
                tex.CreateFace ((uint)face);
                tex.FaceTextures [face].TexMapType = textype;
                part.UpdateTexture (tex, false);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (uint i = 0; i < GetNumberOfSides (part); i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].TexMapType = textype;
                    }
                    tex.DefaultTexture.TexMapType = textype;
                }
                part.UpdateTexture (tex, false);
            }
        }

        public void SetGlow (ISceneChildEntity part, int face, float glow)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides (part)) {
                tex.CreateFace ((uint)face);
                tex.FaceTextures [face].Glow = glow;
                part.UpdateTexture (tex, false);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (uint i = 0; i < GetNumberOfSides (part); i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].Glow = glow;
                    }
                    tex.DefaultTexture.Glow = glow;
                }
                part.UpdateTexture (tex, false);
            }
        }

        public void SetShiny (ISceneChildEntity part, int face, int shiny, Bumpiness bump)
        {
            Shininess sval = new Shininess ();

            switch (shiny) {
            case 0:
                sval = Shininess.None;
                break;
            case 1:
                sval = Shininess.Low;
                break;
            case 2:
                sval = Shininess.Medium;
                break;
            case 3:
                sval = Shininess.High;
                break;
            default:
                sval = Shininess.None;
                break;
            }

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides (part)) {
                tex.CreateFace ((uint)face);
                tex.FaceTextures [face].Shiny = sval;
                tex.FaceTextures [face].Bump = bump;
                part.UpdateTexture (tex, false);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (uint i = 0; i < GetNumberOfSides (part); i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].Shiny = sval;
                        tex.FaceTextures [i].Bump = bump;
                    }
                    tex.DefaultTexture.Shiny = sval;
                    tex.DefaultTexture.Bump = bump;
                }
                part.UpdateTexture (tex, false);
            }
        }

        public void SetFullBright (ISceneChildEntity part, int face, bool bright)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides (part)) {
                tex.CreateFace ((uint)face);
                tex.FaceTextures [face].Fullbright = bright;
                part.UpdateTexture (tex, false);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (uint i = 0; i < GetNumberOfSides (part); i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].Fullbright = bright;
                    }
                }
                tex.DefaultTexture.Fullbright = bright;
                part.UpdateTexture (tex, false);
            }
        }


        protected LSL_Float GetAlpha (ISceneChildEntity part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face == ScriptBaseClass.ALL_SIDES) {
                int i;
                double sum = 0.0;
                for (i = 0; i < GetNumberOfSides (part); i++)
                    sum += tex.GetFace ((uint)i).RGBA.A;
                return sum;
            }
            if (face >= 0 && face < GetNumberOfSides (part)) {
                return tex.GetFace ((uint)face).RGBA.A;
            }
            return 0.0;
        }


        protected void SetAlpha (ISceneChildEntity part, double alpha, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            bool changed = false;
            if (face >= 0 && face < GetNumberOfSides (part)) {
                texcolor = tex.CreateFace ((uint)face).RGBA;
                if (!FloatAlmostEqual (texcolor.A, alpha))
                    changed = true;
                texcolor.A = Util.Clip ((float)alpha, 0.0f, 1.0f);
                tex.FaceTextures [face].RGBA = texcolor;
                if (changed)
                    part.UpdateTexture (tex, false);
            } else if (face == ScriptBaseClass.ALL_SIDES) {
                for (int i = 0; i < GetNumberOfSides (part); i++) {
                    if (tex.FaceTextures [i] != null) {
                        texcolor = tex.FaceTextures [i].RGBA;
                        if (!FloatAlmostEqual (texcolor.A, alpha))
                            changed = true;
                        texcolor.A = Util.Clip ((float)alpha, 0.0f, 1.0f);
                        tex.FaceTextures [i].RGBA = texcolor;
                    }
                }
                texcolor = tex.DefaultTexture.RGBA;
                if (!FloatAlmostEqual (texcolor.A, alpha))
                    changed = true;
                texcolor.A = Util.Clip ((float)alpha, 0.0f, 1.0f);
                tex.DefaultTexture.RGBA = texcolor;
                if (changed)
                    part.UpdateTexture (tex, false);
            }
            part.ScheduleUpdate (PrimUpdateFlags.FullUpdate);
        }

        /// <summary>
        ///     Set flexi parameters of a part.
        ///     FIXME: Much of this code should probably be within the part itself.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="flexi"></param>
        /// <param name="softness"></param>
        /// <param name="gravity"></param>
        /// <param name="friction"></param>
        /// <param name="wind"></param>
        /// <param name="tension"></param>
        /// <param name="Force"></param>
        protected void SetFlexi (ISceneChildEntity part, bool flexi, int softness, float gravity, float friction,
                                float wind, float tension, LSL_Vector Force)
        {
            if (part == null)
                return;

            if (flexi) {
                part.Shape.PathCurve |= (byte)Extrusion.Flexible;
                part.Shape.FlexiEntry = true; // this setting flexi true isn't working, but the below parameters do
                // work once the prim is already flexi
                part.Shape.FlexiSoftness = softness;
                part.Shape.FlexiGravity = gravity;
                part.Shape.FlexiDrag = friction;
                part.Shape.FlexiWind = wind;
                part.Shape.FlexiTension = tension;
                part.Shape.FlexiForceX = (float)Force.x;
                part.Shape.FlexiForceY = (float)Force.y;
                part.Shape.FlexiForceZ = (float)Force.z;
                part.Shape.PathCurve = 0x80;
            } else {
                int curve = part.Shape.PathCurve;
                curve &= (int)(~(Extrusion.Flexible));
                part.Shape.PathCurve = (byte)curve;
                part.Shape.FlexiEntry = false;
            }


            part.ParentEntity.HasGroupChanged = true;
            part.ScheduleUpdate (PrimUpdateFlags.FullUpdate);
        }

        /// <summary>
        ///     Set a light point on a part
        /// </summary>
        /// FIXME: Much of this code should probably be in SceneObjectGroup
        /// <param name="part"></param>
        /// <param name="light"></param>
        /// <param name="color"></param>
        /// <param name="intensity"></param>
        /// <param name="radius"></param>
        /// <param name="falloff"></param>
        protected void SetPointLight (ISceneChildEntity part, bool light, LSL_Vector color, float intensity, float radius,
                                     float falloff)
        {
            if (part == null)
                return;

            bool same = true;
            if (light) {
                if (part.Shape.LightEntry != true)
                    same = false;
                part.Shape.LightEntry = true;
                if (!FloatAlmostEqual (part.Shape.LightColorR, Util.Clip ((float)color.x, 0.0f, 1.0f)))
                    same = false;
                part.Shape.LightColorR = Util.Clip ((float)color.x, 0.0f, 1.0f);
                if (!FloatAlmostEqual (part.Shape.LightColorG, Util.Clip ((float)color.y, 0.0f, 1.0f)))
                    same = false;
                part.Shape.LightColorG = Util.Clip ((float)color.y, 0.0f, 1.0f);
                if (!FloatAlmostEqual (part.Shape.LightColorB, Util.Clip ((float)color.z, 0.0f, 1.0f)))
                    same = false;
                part.Shape.LightColorB = Util.Clip ((float)color.z, 0.0f, 1.0f);
                if (!FloatAlmostEqual (part.Shape.LightIntensity, intensity))
                    same = false;
                part.Shape.LightIntensity = intensity;
                if (!FloatAlmostEqual (part.Shape.LightRadius, radius))
                    same = false;
                part.Shape.LightRadius = radius;
                if (!FloatAlmostEqual (part.Shape.LightFalloff, falloff))
                    same = false;
                part.Shape.LightFalloff = falloff;
            } else {
                if (part.Shape.LightEntry)
                    same = false;
                part.Shape.LightEntry = false;
            }

            if (!same) {
                part.ParentEntity.HasGroupChanged = true;
                part.ScheduleUpdate (PrimUpdateFlags.FindBest);
            }
        }


        protected LSL_Vector GetColor (ISceneChildEntity part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            LSL_Vector rgb = new LSL_Vector ();
            int ns = GetNumberOfSides (part);
            if (face == ScriptBaseClass.ALL_SIDES) {
                int i;

                for (i = 0; i < ns; i++) {
                    texcolor = tex.GetFace ((uint)i).RGBA;
                    rgb.x += texcolor.R;
                    rgb.y += texcolor.G;
                    rgb.z += texcolor.B;
                }

                float tmp = 1f / ns;
                rgb.x *= tmp;
                rgb.y *= tmp;
                rgb.z *= tmp;

                return rgb;
            }
            if (face >= 0 && face < ns) {
                texcolor = tex.GetFace ((uint)face).RGBA;
                rgb.x = texcolor.R;
                rgb.y = texcolor.G;
                rgb.z = texcolor.B;
                return rgb;
            }
            return new LSL_Vector ();
        }


        protected bool SetTexture (ISceneChildEntity part, string texture, int face)
        {
            UUID textureID = new UUID ();
            int ns = GetNumberOfSides (part);

            textureID = KeyOrName (texture, (int)AssetType.Texture, true);
            if (textureID == UUID.Zero)
                return false;

            Primitive.TextureEntry tex = part.Shape.Textures;

            if (face >= 0 && face < ns) {
                Primitive.TextureEntryFace texface = tex.CreateFace ((uint)face);
                texface.TextureID = textureID;
                tex.FaceTextures [face] = texface;
                part.UpdateTexture (tex, false);
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (uint i = 0; i < ns; i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].TextureID = textureID;
                    }
                }
                tex.DefaultTexture.TextureID = textureID;
                part.UpdateTexture (tex, false);
            }
            return true;
        }


        protected void ScaleTexture (ISceneChildEntity part, double u, double v, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            int ns = GetNumberOfSides (part);
            if (face >= 0 && face < ns) {
                Primitive.TextureEntryFace texface = tex.CreateFace ((uint)face);
                texface.RepeatU = (float)u;
                texface.RepeatV = (float)v;
                tex.FaceTextures [face] = texface;
                part.UpdateTexture (tex, false);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (int i = 0; i < ns; i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].RepeatU = (float)u;
                        tex.FaceTextures [i].RepeatV = (float)v;
                    }
                }
                tex.DefaultTexture.RepeatU = (float)u;
                tex.DefaultTexture.RepeatV = (float)v;
                part.UpdateTexture (tex, false);
            }
        }


        protected void OffsetTexture (ISceneChildEntity part, double u, double v, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            int ns = GetNumberOfSides (part);
            if (face >= 0 && face < ns) {
                Primitive.TextureEntryFace texface = tex.CreateFace ((uint)face);
                texface.OffsetU = (float)u;
                texface.OffsetV = (float)v;
                tex.FaceTextures [face] = texface;
                part.UpdateTexture (tex, false);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (int i = 0; i < ns; i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].OffsetU = (float)u;
                        tex.FaceTextures [i].OffsetV = (float)v;
                    }
                }
                tex.DefaultTexture.OffsetU = (float)u;
                tex.DefaultTexture.OffsetV = (float)v;
                part.UpdateTexture (tex, false);
            }
        }



        protected void RotateTexture (ISceneChildEntity part, double rotation, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            int ns = GetNumberOfSides (part);
            if (face >= 0 && face < ns) {
                Primitive.TextureEntryFace texface = tex.CreateFace ((uint)face);
                texface.Rotation = (float)rotation;
                tex.FaceTextures [face] = texface;
                part.UpdateTexture (tex, false);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES) {
                for (int i = 0; i < ns; i++) {
                    if (tex.FaceTextures [i] != null) {
                        tex.FaceTextures [i].Rotation = (float)rotation;
                    }
                }
                tex.DefaultTexture.Rotation = (float)rotation;
                part.UpdateTexture (tex, false);
                return;
            }
        }


        protected LSL_String GetTexture (ISceneChildEntity part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;

            if (face == ScriptBaseClass.ALL_SIDES) {
                face = 0;
            }
            if (face >= 0 && face < GetNumberOfSides (part)) {
                Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);
                TaskInventoryItem item = null;
                m_host.TaskInventory.TryGetValue (texface.TextureID, out item);
                if (item != null)
                    return item.Name.ToString ();
                return texface.TextureID.ToString ();
            }
            return string.Empty;
        }



        // Capped movemment if distance > 10m (http://wiki.secondlife.com/wiki/LlSetPos)
        // note linked setpos is capped "differently"
        LSL_Vector SetPosAdjust (LSL_Vector start, LSL_Vector end)
        {
            if (llVecDist (start, end) > 10.0f * m_ScriptDistanceFactor)
                return start + m_ScriptDistanceFactor * 10.0f * llVecNorm (end - start);
            return end;
        }

        protected void SetPos (ISceneChildEntity part, LSL_Vector targetPos, bool checkPos)
        {
            // Capped movemment if distance > 10m (http://wiki.secondlife.com/wiki/LlSetPos)
            LSL_Vector currentPos = GetPartLocalPos (part);
            float ground = 0;
            bool disable_underground_movement = m_ScriptEngine.Config.GetBoolean ("DisableUndergroundMovement", true);

            ITerrainChannel heightmap = World.RequestModuleInterface<ITerrainChannel> ();
            if (heightmap != null)
                ground = heightmap.GetNormalizedGroundHeight ((int)(float)targetPos.x, (int)(float)targetPos.y);
            if (part.ParentEntity == null)
                return;
            if (part.ParentEntity.RootChild == part) {
                ISceneEntity parent = part.ParentEntity;
                if (!part.IsAttachment) {
                    if (!FloatAlmostEqual (ground, 0) && (targetPos.z < ground) && disable_underground_movement)
                        targetPos.z = ground;
                }
                LSL_Vector real_vec = checkPos ? SetPosAdjust (currentPos, targetPos) : targetPos;
                parent.UpdateGroupPosition (new Vector3 ((float)real_vec.x, (float)real_vec.y, (float)real_vec.z), true);
            } else {
                LSL_Vector rel_vec = checkPos ? SetPosAdjust (currentPos, targetPos) : targetPos;
                part.FixOffsetPosition ((new Vector3 ((float)rel_vec.x, (float)rel_vec.y, (float)rel_vec.z)), true);
            }
        }

        protected LSL_Vector GetPartLocalPos (ISceneChildEntity part)
        {
            Vector3 tmp;
            if (part.ParentID == 0) {
                tmp = part.AbsolutePosition;
                return new LSL_Vector (tmp.X,
                                      tmp.Y,
                                      tmp.Z);
            }
            if (m_host.IsRoot) {
                tmp = m_host.AttachedPos;
                return new LSL_Vector (tmp.X,
                                      tmp.Y,
                                      tmp.Z);
            }
            tmp = part.OffsetPosition;
            return new LSL_Vector (tmp.X,
                                  tmp.Y,
                                  tmp.Z);
        }

        LSL_Vector GetLocalPos (ISceneChildEntity entity)
        {
            Vector3 tmp;
            if (entity.ParentID != 0) {
                tmp = entity.OffsetPosition;
                return new LSL_Vector (tmp.X,
                                      tmp.Y,
                                      tmp.Z);
            }
            tmp = entity.AbsolutePosition;
            return new LSL_Vector (tmp.X,
                                  tmp.Y,
                                  tmp.Z);
        }


        void SetLinkRot (ISceneChildEntity obj, LSL_Rotation rot)
        {
            if (obj.ParentID == 0) {
                // special case: If we are root, rotate complete SOG to new rotation
                SetRot (obj, Rot2Quaternion (rot));
            } else {
                // we are a child. The rotation values will be set to the one of root modified by rot, as in SL. Don't ask.
                ISceneEntity group = obj.ParentEntity;
                if (group != null) // a bit paranoid, maybe
                {
                    ISceneChildEntity rootPart = group.RootChild;
                    if (rootPart != null) // again, better safe than sorry
                    {
                        SetRot (obj, rootPart.GetRotationOffset () * Rot2Quaternion (rot));
                    }
                }
            }
        }


        protected void SetRot (ISceneChildEntity part, Quaternion rot)
        {
            part.UpdateRotation (rot);
            // Update rotation does not move the object in the physics scene if it's a linkset.

            //KF:  Do NOT use this next line if using ODE physics engine.
            //   This need a switch based on .ini Phys Engine type
            //part.ParentGroup.ResetChildPrimPhysicsPositions()

            // So, after thinking about this for a bit, the issue with the part.ParentGroup.AbsolutePosition = part.ParentGroup.AbsolutePosition line
            // is it isn't compatible with vehicles because it causes the vehicle body to have to be broken down and rebuilt
            // It's perfectly okay when the object is not an active physical body though.
            // So, part.ParentGroup.ResetChildPrimPhysicsPositions(); does the thing that Kitto is warning against
            // but only if the object is not physial and active.   This is important for rotating doors.
            // without the absoluteposition = absoluteposition happening, the doors do not move in the physics
            // scene
            if (part.PhysActor != null && !part.PhysActor.IsPhysical) {
                part.ParentEntity.ResetChildPrimPhysicsPositions ();
            }
        }


        LSL_Rotation GetPartRot (ISceneChildEntity part)
        {
            Quaternion q;
            if (part.LinkNum == 0 || part.LinkNum == 1) // unlinked or root prim
            {
                if (part.ParentEntity.RootChild.AttachmentPoint != 0) {
                    IScenePresence avatar = World.GetScenePresence (part.AttachedAvatar);
                    if (avatar != null) {
                        q = (avatar.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0
                                ? avatar.CameraRotation
                                : avatar.Rotation;
                    } else
                        q = part.ParentEntity.GroupRotation; // Likely never get here but just in case
                } else
                    q = part.ParentEntity.GroupRotation; // just the group rotation
                return new LSL_Rotation (q.X, q.Y, q.Z, q.W);
            }
            q = part.GetWorldRotation ();
            return new LSL_Rotation (q.X, q.Y, q.Z, q.W);
        }


        /// <summary>
        ///     Rez an object into the scene from a prim's inventory.
        /// </summary>
        /// <param name="sourcePart"></param>
        /// <param name="item"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="vel"></param>
        /// <param name="param"></param>
        /// <param name="RezzedFrom"></param>
        /// <param name="RezObjectAtRoot"></param>
        /// <returns>The SceneObjectGroup rezzed or null if rez was unsuccessful</returns>
        public ISceneEntity RezObject (
            ISceneChildEntity sourcePart, TaskInventoryItem item,
            Vector3 pos, Quaternion rot, Vector3 vel, int param, UUID RezzedFrom, bool RezObjectAtRoot)
        {
            if (item != null) {
                UUID ownerID = item.OwnerID;

                byte [] rezAsset = World.AssetService.GetData (item.AssetID.ToString ());

                if (rezAsset != null) {
                    string xmlData = Utils.BytesToString (rezAsset);
                    ISceneEntity group = SceneEntitySerializer.SceneObjectSerializer.FromOriginalXmlFormat (xmlData,
                                                                                                           World);
                    if (group == null)
                        return null;

                    string reason;
                    if (!World.Permissions.CanRezObject (group.ChildrenEntities ().Count, ownerID, pos, out reason)) {
                        World.GetScenePresence (ownerID)
                             .ControllingClient.SendAlertMessage ("You do not have permission to rez objects here: " +
                                                                 reason);
                        return null;
                    }

                    List<ISceneChildEntity> partList = group.ChildrenEntities ();
                    // we set it's position in world.
                    // llRezObject sets the whole group at the position, while llRezAtRoot rezzes the group based on the root prim's position
                    // See: http://lslwiki.net/lslwiki/wakka.php?wakka=llRezAtRoot
                    // Shorthand: llRezAtRoot rezzes the root prim of the group at the position
                    //            llRezObject rezzes the center of group at the position
                    if (RezObjectAtRoot)
                        //This sets it right...
                        group.AbsolutePosition = pos;
                    else {
                        // center is on average of all positions
                        // less root prim position
                        Vector3 offset = partList.Aggregate (Vector3.Zero,
                                                            (current, child) => current + child.AbsolutePosition);

                        offset /= partList.Count;
                        offset -= group.AbsolutePosition;
                        offset += pos;
                        group.AbsolutePosition = offset;
                    }

                    ISceneChildEntity rootPart = group.GetChildPart (group.UUID);

                    // Since renaming the item in the inventory does not affect the name stored
                    // in the serialization, transfer the correct name from the inventory to the
                    // object itself before we rez.
                    rootPart.Name = item.Name;
                    rootPart.Description = item.Description;

                    group.SetGroup (sourcePart.GroupID, group.OwnerID, false);

                    if (rootPart.OwnerID != item.OwnerID) {
                        if (World.Permissions.PropagatePermissions ()) {
                            if ((item.CurrentPermissions & 8) != 0) {
                                foreach (ISceneChildEntity part in partList) {
                                    part.EveryoneMask = item.EveryonePermissions;
                                    part.NextOwnerMask = item.NextPermissions;
                                }
                            }
                            group.ApplyNextOwnerPermissions ();
                        }
                    }

                    foreach (ISceneChildEntity part in partList) {
                        if (part.OwnerID != item.OwnerID) {
                            part.LastOwnerID = part.OwnerID;
                            part.OwnerID = item.OwnerID;
                            part.Inventory.ChangeInventoryOwner (item.OwnerID);
                        } else if ((item.CurrentPermissions & 8) != 0) // Slam!
                          {
                            part.EveryoneMask = item.EveryonePermissions;
                            part.NextOwnerMask = item.NextPermissions;
                        }
                    }

                    rootPart.TrimPermissions ();

                    if (group.RootChild.Shape.PCode == (byte)PCode.Prim) {
                        group.ClearPartAttachmentData ();
                    }

                    group.UpdateGroupRotationR (rot);

                    //group.ApplyPhysics(m_physicalPrim);
                    World.SceneGraph.AddPrimToScene (group);
                    if ((group.RootChild.Flags & PrimFlags.Physics) == PrimFlags.Physics) {
                        group.RootChild.PhysActor.OnPhysicalRepresentationChanged +=
                            delegate {
                                float groupmass = group.GetMass ();
                                //Apply the velocity to the object
                                //llApplyImpulse(new LSL_Vector(llvel.X * groupmass, llvel.Y * groupmass, llvel.Z * groupmass), 0);
                                // @Above: Err.... no. Read http://lslwiki.net/lslwiki/wakka.php?wakka=llRezObject
                                //    Notice the "Creates ("rezzes") object's inventory object centered at position pos (in region coordinates) with velocity vel"
                                //    This means SET the velocity to X, not just temperarily add it!
                                //   -- Revolution Smythe
                                llSetForce (new LSL_Vector (vel * groupmass), 0);
                                group.RootChild.PhysActor.ForceSetVelocity (vel * groupmass);
                                group.RootChild.PhysActor.Velocity = vel * groupmass;
                            };
                    }

                    group.CreateScriptInstances (param, true, StateSource.ScriptedRez, RezzedFrom, false);

                    if (!World.Permissions.BypassPermissions ()) {
                        if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                            sourcePart.Inventory.RemoveInventoryItem (item.ItemID);
                    }

                    group.ScheduleGroupUpdate (PrimUpdateFlags.FullUpdate);

                    return rootPart.ParentEntity;
                }
            }

            return null;
        }


        void LookAt (LSL_Vector target, double strength, double damping, ISceneChildEntity obj)
        {
            // Determine where we are looking from
            LSL_Vector from = new LSL_Vector (obj.GetWorldPosition ());

            // The following code bit was written by Dahlia 
            // from the Opensimulator Core Team. Thank you for fixing this issue

            // normalized direction to target
            LSL_Vector dir = llVecNorm (target - from);

            // use vertical to help compute left axis
            LSL_Vector up = new LSL_Vector (0.0, 0.0, 1.0);

            // find normalized left axis parallel to horizon
            LSL_Vector left = llVecNorm (LSL_Vector.Cross (up, dir));

            // make up orthogonal to left and direction
            up = LSL_Vector.Cross (dir, left);

            // compute rotation based on orthogonal axes
            LSL_Rotation rot = new LSL_Rotation (0.0, 0.707107, 0.0, 0.707107) * llAxes2Rot (dir, left, up);

            // End codebit

            //If the strength is 0, or we are non-physical, set the rotation
            if (FloatAlmostEqual (strength, 0) || obj.PhysActor == null || !obj.PhysActor.IsPhysical)
                SetLinkRot (obj, rot);
            else
                obj.startLookAt (Rot2Quaternion (rot), (float)strength, (float)damping);
        }


        /// <summary>
        ///     Attach the object containing this script to the avatar that owns it.
        /// </summary>
        /// <returns>true if the attach succeeded, false if it did not</returns>
        public bool AttachToAvatar (int attachmentPoint, bool temp)
        {
            IScenePresence presence = World.GetScenePresence (m_host.OwnerID);
            IAttachmentsModule attachmentsModule = World.RequestModuleInterface<IAttachmentsModule> ();
            if (attachmentsModule != null)
                return attachmentsModule.AttachObjectFromInworldObject (m_localID, presence.ControllingClient,
                                                                       m_host.ParentEntity, attachmentPoint, temp);

            return false;
        }

        /// <summary>
        ///     Detach the object containing this script from the avatar it is attached to.
        /// </summary>
        /// <remarks>
        ///     Nothing happens if the object is not attached.
        /// </remarks>
        public void DetachFromAvatar ()
        {
            Util.FireAndForget (DetachWrapper, m_host);
        }

        void DetachWrapper (object o)
        {
            ISceneEntity grp = ((ISceneChildEntity)o).ParentEntity;
            IScenePresence presence = World.GetScenePresence (grp.OwnerID);
            IAttachmentsModule attachmentsModule = World.RequestModuleInterface<IAttachmentsModule> ();
            if (attachmentsModule != null)
                attachmentsModule.DetachSingleAttachmentToInventory (grp.RootChild.FromUserInventoryItemID, presence.ControllingClient);
        }


        void handleScriptAnswer (IClientAPI client, UUID taskID, UUID itemID, int answer)
        {
            if (taskID != m_host.UUID)
                return;

            UUID invItemID = InventorySelf ();

            if (invItemID == UUID.Zero)
                return;

            client.OnScriptAnswer -= handleScriptAnswer;
            m_waitingForScriptAnswer = false;

            if ((answer & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) == 0)
                llReleaseControls ();

            lock (m_host.TaskInventory) {
                m_host.TaskInventory [invItemID].PermsMask = answer;
            }

            m_ScriptEngine.PostScriptEvent (
                m_itemID
                , m_host.UUID,
                new EventParams ("run_time_permissions",
                                new object [] { new LSL_Integer (answer) },
                                new DetectParams [0]),
                EventPriority.FirstStart
            );
        }


        protected int GetNumberOfSides (ISceneChildEntity part)
        {
            int sides = part.GetNumberOfSides ();

            if (part.GetPrimType () == PrimType.SPHERE && part.Shape.ProfileHollow > 0) {
                // Make up for a bug where LSL shows 4 sides rather than 2
                sides += 2;
            }

            return sides;
        }

        protected LSL_Vector GetTextureOffset (ISceneChildEntity part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            LSL_Vector offset = new LSL_Vector ();
            if (face == ScriptBaseClass.ALL_SIDES) {
                face = 0;
            }
            if (face >= 0 && face < GetNumberOfSides (part)) {
                offset.x = tex.GetFace ((uint)face).OffsetU;
                offset.y = tex.GetFace ((uint)face).OffsetV;
                offset.z = 0.0;
                return offset;
            }
            return offset;
        }



        protected LSL_Float GetTextureRot (ISceneChildEntity part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face == -1) {
                face = 0;
            }
            if (face >= 0 && face < GetNumberOfSides (part)) {
                return tex.GetFace ((uint)face).Rotation;
            }
            return 0.0;
        }


        void SetTextureAnim (ISceneChildEntity part, int mode, int face, int sizex, int sizey, double start,
                                    double length, double rate)
        {
            Primitive.TextureAnimation pTexAnim =
                new Primitive.TextureAnimation { Flags = (Primitive.TextureAnimMode)mode };

            //ALL_SIDES
            if (face == ScriptBaseClass.ALL_SIDES)
                face = 255;

            pTexAnim.Face = (uint)face;
            pTexAnim.Length = (float)length;
            pTexAnim.Rate = (float)rate;
            pTexAnim.SizeX = (uint)sizex;
            pTexAnim.SizeY = (uint)sizey;
            pTexAnim.Start = (float)start;

            part.AddTextureAnimation (pTexAnim);
            part.ScheduleUpdate (PrimUpdateFlags.FindBest);
        }


        /* particle system rules should be coming into this routine as doubles, that is
        rule[0] should be an integer from this list and rule[1] should be the arg
        for the same integer. wiki.secondlife.com has most of this mapping, but some
        came from http://www.caligari-designs.com/p4u2

        We iterate through the list for 'Count' elements, incrementing by two for each
        iteration and set the members of Primitive.ParticleSystem, one at a time.
        */

        public enum PrimitiveRule
        {
            PSYS_PART_FLAGS = 0,
            PSYS_PART_START_COLOR = 1,
            PSYS_PART_START_ALPHA = 2,
            PSYS_PART_END_COLOR = 3,
            PSYS_PART_END_ALPHA = 4,
            PSYS_PART_START_SCALE = 5,
            PSYS_PART_END_SCALE = 6,
            PSYS_PART_MAX_AGE = 7,
            PSYS_SRC_ACCEL = 8,
            PSYS_SRC_PATTERN = 9,
            PSYS_SRC_INNERANGLE = 10,
            PSYS_SRC_OUTERANGLE = 11,
            PSYS_SRC_TEXTURE = 12,
            PSYS_SRC_BURST_RATE = 13,
            PSYS_SRC_BURST_PART_COUNT = 15,
            PSYS_SRC_BURST_RADIUS = 16,
            PSYS_SRC_BURST_SPEED_MIN = 17,
            PSYS_SRC_BURST_SPEED_MAX = 18,
            PSYS_SRC_MAX_AGE = 19,
            PSYS_SRC_TARGET_KEY = 20,
            PSYS_SRC_OMEGA = 21,
            PSYS_SRC_ANGLE_BEGIN = 22,
            PSYS_SRC_ANGLE_END = 23,
            PSYS_PART_BLEND_FUNC_SOURCE = 24,
            PSYS_PART_BLEND_FUNC_DEST = 25,
            PSYS_PART_START_GLOW = 25,
            PSYS_PART_END_GLOW = 27
        }

        internal Primitive.ParticleSystem.ParticleDataFlags ConvertUINTtoFlags (uint flags)
        {
            const Primitive.ParticleSystem.ParticleDataFlags returnval = Primitive.ParticleSystem.ParticleDataFlags.None;

            return returnval;
        }

        protected Primitive.ParticleSystem getNewParticleSystemWithSLDefaultValues ()
        {
            Primitive.ParticleSystem ps = new Primitive.ParticleSystem {
                PartStartColor = new Color4 (1.0f, 1.0f, 1.0f, 1.0f),
                PartEndColor = new Color4 (1.0f, 1.0f, 1.0f, 1.0f),
                PartStartScaleX = 1.0f,
                PartStartScaleY = 1.0f,
                PartEndScaleX = 1.0f,
                PartEndScaleY = 1.0f,
                BurstSpeedMin = 1.0f,
                BurstSpeedMax = 1.0f,
                BurstRate = 0.1f,
                PartMaxAge = 10.0f,
                BurstPartCount = 1,
                BlendFuncSource = (byte)((int)ScriptBaseClass.PSYS_PART_BF_SOURCE_ALPHA),
                BlendFuncDest = (byte)((int)ScriptBaseClass.PSYS_PART_BF_ONE_MINUS_SOURCE_ALPHA),
                PartStartGlow = 0.0f,
                PartEndGlow = 0.0f
            };

            // TODO find out about the other defaults and add them here
            return ps;
        }


        void SetParticleSystem (ISceneChildEntity part, LSL_List rules)
        {
            if (rules.Length == 0) {
                part.RemoveParticleSystem ();
            } else {
                Primitive.ParticleSystem prules = getNewParticleSystemWithSLDefaultValues ();
                LSL_Vector tempv = new LSL_Vector ();
                float tempf = 0;
                int tmpi = 0;

                for (int i = 0; i < rules.Length; i += 2) {
                    LSL_Integer rule = rules.GetLSLIntegerItem (i);
                    if (rule == (int)ScriptBaseClass.PSYS_PART_FLAGS) {
                        prules.PartDataFlags =
                            (Primitive.ParticleSystem.ParticleDataFlags)(uint)rules.GetLSLIntegerItem (i + 1);
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_START_COLOR) {
                        tempv = rules.GetVector3Item (i + 1);
                        prules.PartStartColor.R = (float)tempv.x;
                        prules.PartStartColor.G = (float)tempv.y;
                        prules.PartStartColor.B = (float)tempv.z;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_START_ALPHA) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.PartStartColor.A = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_END_COLOR) {
                        tempv = rules.GetVector3Item (i + 1);
                        prules.PartEndColor.R = (float)tempv.x;
                        prules.PartEndColor.G = (float)tempv.y;
                        prules.PartEndColor.B = (float)tempv.z;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_END_ALPHA) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.PartEndColor.A = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_START_SCALE) {
                        tempv = rules.GetVector3Item (i + 1);
                        prules.PartStartScaleX = (float)tempv.x;
                        prules.PartStartScaleY = (float)tempv.y;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_END_SCALE) {
                        tempv = rules.GetVector3Item (i + 1);
                        prules.PartEndScaleX = (float)tempv.x;
                        prules.PartEndScaleY = (float)tempv.y;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_MAX_AGE) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.PartMaxAge = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_ACCEL) {
                        tempv = rules.GetVector3Item (i + 1);
                        prules.PartAcceleration.X = (float)tempv.x;
                        prules.PartAcceleration.Y = (float)tempv.y;
                        prules.PartAcceleration.Z = (float)tempv.z;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_PATTERN) {
                        tmpi = rules.GetLSLIntegerItem (i + 1);
                        prules.Pattern = (Primitive.ParticleSystem.SourcePattern)tmpi;
                    }

                      // PSYS_SRC_INNERANGLE and PSYS_SRC_ANGLE_BEGIN use the same variables. The
                      // PSYS_SRC_OUTERANGLE and PSYS_SRC_ANGLE_END also use the same variable. The
                      // client tells the difference between the two by looking at the 0x02 bit in
                      // the PartFlags variable.
                      else if (rule == (int)ScriptBaseClass.PSYS_SRC_INNERANGLE) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.InnerAngle = tempf;
                        prules.PartFlags &= 0xFFFFFFFD; // Make sure new angle format is off.
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_OUTERANGLE) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.OuterAngle = tempf;
                        prules.PartFlags &= 0xFFFFFFFD; // Make sure new angle format is off.
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_BLEND_FUNC_SOURCE) {
                        tmpi = rules.GetLSLIntegerItem (i + 1);
                        prules.BlendFuncSource = (byte)tmpi;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_BLEND_FUNC_DEST) {
                        tmpi = rules.GetLSLIntegerItem (i + 1);
                        prules.BlendFuncDest = (byte)tmpi;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_START_GLOW) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.PartStartGlow = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_PART_END_GLOW) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.PartEndGlow = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_TEXTURE) {
                        prules.Texture = KeyOrName (rules.GetLSLStringItem (i + 1), AssetType.Texture, false);
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_BURST_RATE) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.BurstRate = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_BURST_PART_COUNT) {
                        prules.BurstPartCount = (byte)(int)rules.GetLSLIntegerItem (i + 1);
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_BURST_RADIUS) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.BurstRadius = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_BURST_SPEED_MIN) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.BurstSpeedMin = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_BURST_SPEED_MAX) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.BurstSpeedMax = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_MAX_AGE) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.MaxAge = tempf;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_TARGET_KEY) {
                        UUID key = UUID.Zero;
                        prules.Target = UUID.TryParse (rules.Data [i + 1].ToString (), out key) ? key : part.UUID;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_OMEGA) {
                        // AL: This is an assumption, since it is the only thing that would match.
                        tempv = rules.GetVector3Item (i + 1);
                        prules.AngularVelocity.X = (float)tempv.x;
                        prules.AngularVelocity.Y = (float)tempv.y;
                        prules.AngularVelocity.Z = (float)tempv.z;
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_ANGLE_BEGIN) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.InnerAngle = tempf;
                        prules.PartFlags |= 0x02; // Set new angle format.
                    } else if (rule == (int)ScriptBaseClass.PSYS_SRC_ANGLE_END) {
                        tempf = (float)rules.GetLSLFloatItem (i + 1);
                        prules.OuterAngle = tempf;
                        prules.PartFlags |= 0x02; // Set new angle format.
                    }
                }
                prules.CRC = 1;

                part.AddNewParticleSystem (prules);
            }
            part.ScheduleUpdate (PrimUpdateFlags.Particles);
        }

        protected UUID GetTaskInventoryItem (string name)
        {
            lock (m_host.TaskInventory) {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory) {
                    if (inv.Value.Name == name)
                        return inv.Key;
                }
            }

            return UUID.Zero;
        }


        protected void SitTarget (ISceneChildEntity part, LSL_Vector offset, LSL_Rotation rot)
        {
            // LSL quaternions can normalize to 0, normal Quaternions can't.
            if (FloatAlmostEqual (rot.s, 0) &&
                FloatAlmostEqual (rot.x, 0) &&
                FloatAlmostEqual (rot.y, 0) &&
                FloatAlmostEqual (rot.z, 0))
                rot.z = 1; // ZERO_ROTATION = 0,0,0,1

            part.SitTargetPosition = new Vector3 ((float)offset.x, (float)offset.y, (float)offset.z); ;
            part.SitTargetOrientation = Rot2Quaternion (rot); ;
            part.ParentEntity.HasGroupChanged = true;
        }


        protected ObjectShapePacket.ObjectDataBlock SetPrimitiveBlockShapeParams (ISceneChildEntity part, int holeshape,
                                                                                 LSL_Vector cut, float hollow,
                                                                                 LSL_Vector twist)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = new ObjectShapePacket.ObjectDataBlock ();

            if (holeshape != (int)ScriptBaseClass.PRIM_HOLE_DEFAULT &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_CIRCLE &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_SQUARE &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_TRIANGLE) {
                holeshape = ScriptBaseClass.PRIM_HOLE_DEFAULT;
            }
            shapeBlock.ProfileCurve = (byte)holeshape;
            if (cut.x < 0f) {
                cut.x = 0f;
            }
            if (cut.x > 1f) {
                cut.x = 1f;
            }
            if (cut.y < 0f) {
                cut.y = 0f;
            }
            if (cut.y > 1f) {
                cut.y = 1f;
            }
            if (cut.y - cut.x < 0.02f) {
                cut.x = cut.y - 0.02f;
                if (cut.x < 0.0f) {
                    cut.x = 0.0f;
                    cut.y = 0.02f;
                }
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * cut.x);
            shapeBlock.ProfileEnd = (ushort)(50000 * (1 - cut.y));
            if (hollow < 0f) {
                hollow = 0f;
            }
            if (hollow > 0.99) {
                hollow = 0.99f;
            }
            shapeBlock.ProfileHollow = (ushort)(50000 * hollow);
            if (twist.x < -1.0f) {
                twist.x = -1.0f;
            }
            if (twist.x > 1.0f) {
                twist.x = 1.0f;
            }
            if (twist.y < -1.0f) {
                twist.y = -1.0f;
            }
            if (twist.y > 1.0f) {
                twist.y = 1.0f;
            }
            // A fairly large precision error occurs for some calculations,
            // if a float or double is directly cast to a byte or sbyte
            // variable, in both .Net and Mono. In .Net, coding
            // "(sbyte)(float)(some expression)" corrects the precision
            // errors. But this does not work for Mono. This longer coding
            // form of creating a tempoary float variable from the
            // expression first, then casting that variable to a byte or
            // sbyte, works for both .Net and Mono. These types of
            // assignments occur in SetPrimtiveBlockShapeParams and
            // SetPrimitiveShapeParams in support of llSetPrimitiveParams.
            float tempFloat = (float)(100.0d * twist.x);
            shapeBlock.PathTwistBegin = (sbyte)tempFloat;
            tempFloat = (float)(100.0d * twist.y);
            shapeBlock.PathTwist = (sbyte)tempFloat;

            shapeBlock.ObjectLocalID = part.LocalId;

            // retain pathcurve
            shapeBlock.PathCurve = part.Shape.PathCurve;

            part.Shape.SculptEntry = false;
            return shapeBlock;
        }

        protected void SetPrimitiveShapeParams (ISceneChildEntity part, int holeshape, LSL_Vector cut, float hollow,
                                               LSL_Vector twist, LSL_Vector taper_b, LSL_Vector topshear, byte fudge)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = SetPrimitiveBlockShapeParams (part, holeshape, cut, hollow,
                                                                                        twist);

            shapeBlock.ProfileCurve += fudge;

            if (taper_b.x < 0f) {
                taper_b.x = 0f;
            }
            if (taper_b.x > 2f) {
                taper_b.x = 2f;
            }
            if (taper_b.y < 0f) {
                taper_b.y = 0f;
            }
            if (taper_b.y > 2f) {
                taper_b.y = 2f;
            }
            float tempFloat = (float)(100.0d * (2.0d - taper_b.x));
            shapeBlock.PathScaleX = (byte)tempFloat;
            tempFloat = (float)(100.0d * (2.0d - taper_b.y));
            shapeBlock.PathScaleY = (byte)tempFloat;
            if (topshear.x < -0.5f) {
                topshear.x = -0.5f;
            }
            if (topshear.x > 0.5f) {
                topshear.x = 0.5f;
            }
            if (topshear.y < -0.5f) {
                topshear.y = -0.5f;
            }
            if (topshear.y > 0.5f) {
                topshear.y = 0.5f;
            }
            tempFloat = (float)(100.0d * topshear.x);
            shapeBlock.PathShearX = (byte)tempFloat;
            tempFloat = (float)(100.0d * topshear.y);
            shapeBlock.PathShearY = (byte)tempFloat;

            part.Shape.SculptEntry = false;
            part.UpdateShape (shapeBlock);
        }

        protected void SetPrimitiveShapeParams (ISceneChildEntity part, int holeshape, LSL_Vector cut, float hollow,
                                               LSL_Vector twist, LSL_Vector dimple, byte fudge)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = SetPrimitiveBlockShapeParams (part, holeshape, cut, hollow,
                                                                                        twist);

            // profile/path swapped for a sphere
            shapeBlock.PathBegin = shapeBlock.ProfileBegin;
            shapeBlock.PathEnd = shapeBlock.ProfileEnd;

            shapeBlock.ProfileCurve += fudge;

            shapeBlock.PathScaleX = 100;
            shapeBlock.PathScaleY = 100;

            if (dimple.x < 0f) {
                dimple.x = 0f;
            }
            if (dimple.x > 1f) {
                dimple.x = 1f;
            }
            if (dimple.y < 0f) {
                dimple.y = 0f;
            }
            if (dimple.y > 1f) {
                dimple.y = 1f;
            }
            if (dimple.y - dimple.x < 0.02f) {
                dimple.x = dimple.y - 0.02f;
                if (dimple.x < 0.0f) {
                    dimple.x = 0.0f;
                    dimple.y = 0.02f;
                }
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * dimple.x);
            shapeBlock.ProfileEnd = (ushort)(50000 * (1 - dimple.y));

            part.Shape.SculptEntry = false;
            part.UpdateShape (shapeBlock);
        }

        protected void SetPrimitiveShapeParams (ISceneChildEntity part, int holeshape, LSL_Vector cut, float hollow,
                                               LSL_Vector twist, LSL_Vector holesize, LSL_Vector topshear,
                                               LSL_Vector profilecut, LSL_Vector taper_a, float revolutions,
                                               float radiusoffset, float skew, byte fudge)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = SetPrimitiveBlockShapeParams (part, holeshape, cut, hollow,
                                                                                        twist);

            shapeBlock.ProfileCurve += fudge;

            // profile/path swapped for a torrus, tube, ring
            shapeBlock.PathBegin = shapeBlock.ProfileBegin;
            shapeBlock.PathEnd = shapeBlock.ProfileEnd;

            if (holesize.x < 0.01f) {
                holesize.x = 0.01f;
            }
            if (holesize.x > 1f) {
                holesize.x = 1f;
            }
            if (holesize.y < 0.01f) {
                holesize.y = 0.01f;
            }
            if (holesize.y > 0.5f) {
                holesize.y = 0.5f;
            }
            float tempFloat = (float)(100.0d * (2.0d - holesize.x));
            shapeBlock.PathScaleX = (byte)tempFloat;
            tempFloat = (float)(100.0d * (2.0d - holesize.y));
            shapeBlock.PathScaleY = (byte)tempFloat;
            if (topshear.x < -0.5f) {
                topshear.x = -0.5f;
            }
            if (topshear.x > 0.5f) {
                topshear.x = 0.5f;
            }
            if (topshear.y < -0.5f) {
                topshear.y = -0.5f;
            }
            if (topshear.y > 0.5f) {
                topshear.y = 0.5f;
            }
            tempFloat = (float)(100.0d * topshear.x);
            shapeBlock.PathShearX = (byte)tempFloat;
            tempFloat = (float)(100.0d * topshear.y);
            shapeBlock.PathShearY = (byte)tempFloat;
            if (profilecut.x < 0f) {
                profilecut.x = 0f;
            }
            if (profilecut.x > 1f) {
                profilecut.x = 1f;
            }
            if (profilecut.y < 0f) {
                profilecut.y = 0f;
            }
            if (profilecut.y > 1f) {
                profilecut.y = 1f;
            }
            if (profilecut.y - profilecut.x < 0.02f) {
                profilecut.x = profilecut.y - 0.02f;
                if (profilecut.x < 0.0f) {
                    profilecut.x = 0.0f;
                    profilecut.y = 0.02f;
                }
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * profilecut.x);
            shapeBlock.ProfileEnd = (ushort)(50000 * (1 - profilecut.y));
            if (taper_a.x < -1f) {
                taper_a.x = -1f;
            }
            if (taper_a.x > 1f) {
                taper_a.x = 1f;
            }
            if (taper_a.y < -1f) {
                taper_a.y = -1f;
            }
            if (taper_a.y > 1f) {
                taper_a.y = 1f;
            }
            tempFloat = (float)(100.0d * taper_a.x);
            shapeBlock.PathTaperX = (sbyte)tempFloat;
            tempFloat = (float)(100.0d * taper_a.y);
            shapeBlock.PathTaperY = (sbyte)tempFloat;
            if (revolutions < 1f) {
                revolutions = 1f;
            }
            if (revolutions > 4f) {
                revolutions = 4f;
            }
            tempFloat = 66.66667f * (revolutions - 1.0f);
            shapeBlock.PathRevolutions = (byte)tempFloat;
            // limits on radiusoffset depend on revolutions and hole size
            float taper_y_magnitude = (float)Math.Abs (taper_a.y);
            if (radiusoffset * taper_a.y < 0) {
                taper_y_magnitude = 0;
            }
            float holesize_y_mag = (float)Math.Abs (holesize.y);
            float max_radius_mag = 1f - holesize_y_mag * (1f - taper_y_magnitude) / (1f - holesize_y_mag);
            if (Math.Abs (radiusoffset) > max_radius_mag) {
                radiusoffset = Math.Sign (radiusoffset) * max_radius_mag;
            }
            tempFloat = 100.0f * radiusoffset;
            shapeBlock.PathRadiusOffset = (sbyte)tempFloat;
            float min_skew_mag = (float)(1f - 1f / (revolutions * holesize.x + 1f));
            if (Math.Abs (revolutions - 1.0) < 0.001) {
                min_skew_mag = 0f;
            }
            if (Math.Abs (skew) < min_skew_mag) {
                skew = min_skew_mag * Math.Sign (skew);
            }
            if (skew < -0.95f) {
                skew = -0.95f;
            }
            if (skew > 0.95f) {
                skew = 0.95f;
            }
            tempFloat = 100.0f * skew;
            shapeBlock.PathSkew = (sbyte)tempFloat;

            part.Shape.SculptEntry = false;
            part.UpdateShape (shapeBlock);
        }

        protected void SetPrimitiveShapeParams (ISceneChildEntity part, string map, int type)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = new ObjectShapePacket.ObjectDataBlock ();
            UUID sculptId = KeyOrName (map, AssetType.Texture, true);

            if (sculptId == UUID.Zero)
                return;

            shapeBlock.ObjectLocalID = part.LocalId;
            shapeBlock.PathScaleX = 100;
            shapeBlock.PathScaleY = 150;

            int onlytype = (type & (ScriptBaseClass.PRIM_SCULPT_FLAG_INVERT | ScriptBaseClass.PRIM_SCULPT_FLAG_MIRROR));
            //Removes the sculpt flags according to libOMV
            if (onlytype != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_CYLINDER &&
                onlytype != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_PLANE &&
                onlytype != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_SPHERE &&
                onlytype != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_TORUS &&
                onlytype != (int)ScriptBaseClass.PRIM_SCULPT_TYPE_MESH) {
                // default
                type |= ScriptBaseClass.PRIM_SCULPT_TYPE_SPHERE;
            }

            // retain pathcurve
            shapeBlock.PathCurve = part.Shape.PathCurve;
            bool changedTextureID = part.Shape.SculptTexture != sculptId;
            part.Shape.SetSculptProperties ((byte)type, sculptId);
            part.Shape.SculptEntry = true;
            part.UpdateShape (shapeBlock, changedTextureID);
        }


        public void SetPrimParams (IEntity part, LSL_List rules, bool allowOpenSimParams)
        {
            int idx = 0;

            while (idx < rules.Length) {
                int code = rules.GetLSLIntegerItem (idx++);

                int remain = rules.Length - idx;

                int face;
                LSL_Vector v;

                if (code == (int)ScriptBaseClass.PRIM_NAME) {
                    if (remain < 1)
                        return;

                    string name = rules.Data [idx++].ToString ();
                    if (part is ISceneChildEntity)
                        (part as ISceneChildEntity).Name = name;
                } else if (code == (int)ScriptBaseClass.PRIM_DESC) {
                    if (remain < 1)
                        return;

                    string desc = rules.Data [idx++].ToString ();
                    if (part is ISceneChildEntity)
                        (part as ISceneChildEntity).Description = desc;
                } else if (code == (int)ScriptBaseClass.PRIM_ROT_LOCAL) {
                    if (remain < 1)
                        return;
                    LSL_Rotation lr = rules.GetQuaternionItem (idx++);
                    if (part is ISceneChildEntity)
                        SetRot ((part as ISceneChildEntity), Rot2Quaternion (lr));
                } else if (code == (int)ScriptBaseClass.PRIM_POSITION) {
                    if (remain < 1)
                        return;

                    v = rules.GetVector3Item (idx++);
                    if (part is ISceneChildEntity)
                        //SetPos(part as ISceneChildEntity, GetPartLocalPos(part as ISceneChildEntity) + v, true);
                        SetPos (part as ISceneChildEntity, v, true);
                    else if (part is IScenePresence) {
                        (part as IScenePresence).OffsetPosition = new Vector3 ((float)v.x, (float)v.y, (float)v.z);
                        (part as IScenePresence).SendTerseUpdateToAllClients ();
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_POS_LOCAL) {
                    if (remain < 1)
                        return;

                    v = rules.GetVector3Item (idx++);
                    if (part is ISceneChildEntity) {
                        if (((ISceneChildEntity)part).ParentID != 0)
                            ((ISceneChildEntity)part).OffsetPosition = new Vector3 ((float)v.x, (float)v.y,
                                                                                    (float)v.z);
                        else
                            part.AbsolutePosition = new Vector3 ((float)v.x, (float)v.y, (float)v.z);
                    } else if (part is IScenePresence) {
                        (part as IScenePresence).OffsetPosition = new Vector3 ((float)v.x, (float)v.y, (float)v.z);
                        (part as IScenePresence).SendTerseUpdateToAllClients ();
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_SIZE) {
                    if (remain < 1)
                        return;


                    v = rules.GetVector3Item (idx++);
                    if (part is ISceneChildEntity)
                        SetScale (part as ISceneChildEntity, v);
                } else if (code == (int)ScriptBaseClass.PRIM_ROTATION) {
                    if (remain < 1)
                        return;

                    LSL_Rotation q = rules.GetQuaternionItem (idx++);
                    if (part is ISceneChildEntity) {
                        // try to let this work as in SL...
                        if ((part as ISceneChildEntity).ParentID == 0) {
                            // special case: If we are root, rotate complete SOG to new rotation
                            SetRot (part as ISceneChildEntity, Rot2Quaternion (q));
                        } else {
                            // we are a child. The rotation values will be set to the one of root modified by rot, as in SL. Don't ask.
                            ISceneEntity group = (part as ISceneChildEntity).ParentEntity;
                            if (group != null) // a bit paranoid, maybe
                            {
                                ISceneChildEntity rootPart = group.RootChild;
                                if (rootPart != null) // again, better safe than sorry
                                {
                                    SetRot ((part as ISceneChildEntity), rootPart.GetRotationOffset () * Rot2Quaternion (q));
                                }
                            }
                        }
                    } else if (part is IScenePresence) {
                        IScenePresence sp = (IScenePresence)part;
                        ISceneChildEntity childObj = sp.Scene.GetSceneObjectPart (sp.SittingOnUUID);
                        if (childObj != null) {
                            sp.Rotation = childObj.ParentEntity.GroupRotation * Rot2Quaternion (q);
                            sp.SendTerseUpdateToAllClients ();
                        }
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_TYPE) {
                    if (remain < 3)
                        return;

                    if (part is ISceneChildEntity) {
                    } else
                        return;

                    code = rules.GetLSLIntegerItem (idx++);

                    remain = rules.Length - idx;
                    float hollow;
                    LSL_Vector twist;
                    LSL_Vector taper_b;
                    LSL_Vector topshear;
                    float revolutions;
                    float radiusoffset;
                    float skew;
                    LSL_Vector holesize;
                    LSL_Vector profilecut;

                    if (code == (int)ScriptBaseClass.PRIM_TYPE_BOX) {
                        if (remain < 6)
                            return;

                        face = rules.GetLSLIntegerItem (idx++);
                        v = rules.GetVector3Item (idx++); // cut
                        hollow = (float)rules.GetLSLFloatItem (idx++);
                        twist = rules.GetVector3Item (idx++);
                        taper_b = rules.GetVector3Item (idx++);
                        topshear = rules.GetVector3Item (idx++);

                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Straight;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), face, v, hollow, twist, taper_b, topshear,
                                                1);
                    } else if (code == (int)ScriptBaseClass.PRIM_TYPE_CYLINDER) {
                        if (remain < 6)
                            return;

                        face = rules.GetLSLIntegerItem (idx++); // holeshape
                        v = rules.GetVector3Item (idx++); // cut
                        hollow = (float)rules.GetLSLFloatItem (idx++);
                        twist = rules.GetVector3Item (idx++);
                        taper_b = rules.GetVector3Item (idx++);
                        topshear = rules.GetVector3Item (idx++);
                        (part as ISceneChildEntity).Shape.ProfileShape = ProfileShape.Circle;
                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Straight;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), face, v, hollow, twist, taper_b, topshear,
                                                0);
                    } else if (code == (int)ScriptBaseClass.PRIM_TYPE_PRISM) {
                        if (remain < 6)
                            return;

                        face = rules.GetLSLIntegerItem (idx++); // holeshape
                        v = rules.GetVector3Item (idx++); //cut
                        hollow = (float)rules.GetLSLFloatItem (idx++);
                        twist = rules.GetVector3Item (idx++);
                        taper_b = rules.GetVector3Item (idx++);
                        topshear = rules.GetVector3Item (idx++);
                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Straight;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), face, v, hollow, twist, taper_b, topshear,
                                                3);
                    } else if (code == (int)ScriptBaseClass.PRIM_TYPE_SPHERE) {
                        if (remain < 5)
                            return;

                        face = rules.GetLSLIntegerItem (idx++); // holeshape
                        v = rules.GetVector3Item (idx++); // cut
                        hollow = (float)rules.GetLSLFloatItem (idx++);
                        twist = rules.GetVector3Item (idx++);
                        taper_b = rules.GetVector3Item (idx++); // dimple
                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Curve1;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), face, v, hollow, twist, taper_b, 5);
                    } else if (code == (int)ScriptBaseClass.PRIM_TYPE_TORUS) {
                        if (remain < 11)
                            return;

                        face = rules.GetLSLIntegerItem (idx++); // holeshape
                        v = rules.GetVector3Item (idx++); //cut
                        hollow = (float)rules.GetLSLFloatItem (idx++);
                        twist = rules.GetVector3Item (idx++);
                        holesize = rules.GetVector3Item (idx++);
                        topshear = rules.GetVector3Item (idx++);
                        profilecut = rules.GetVector3Item (idx++);
                        taper_b = rules.GetVector3Item (idx++); // taper_a
                        revolutions = (float)rules.GetLSLFloatItem (idx++);
                        radiusoffset = (float)rules.GetLSLFloatItem (idx++);
                        skew = (float)rules.GetLSLFloatItem (idx++);
                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Curve1;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), face, v, hollow, twist, holesize, topshear,
                                                profilecut, taper_b,
                                                revolutions, radiusoffset, skew, 0);
                    } else if (code == (int)ScriptBaseClass.PRIM_TYPE_TUBE) {
                        if (remain < 11)
                            return;

                        face = rules.GetLSLIntegerItem (idx++); // holeshape
                        v = rules.GetVector3Item (idx++); //cut
                        hollow = (float)rules.GetLSLFloatItem (idx++);
                        twist = rules.GetVector3Item (idx++);
                        holesize = rules.GetVector3Item (idx++);
                        topshear = rules.GetVector3Item (idx++);
                        profilecut = rules.GetVector3Item (idx++);
                        taper_b = rules.GetVector3Item (idx++); // taper_a
                        revolutions = (float)rules.GetLSLFloatItem (idx++);
                        radiusoffset = (float)rules.GetLSLFloatItem (idx++);
                        skew = (float)rules.GetLSLFloatItem (idx++);
                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Curve1;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), face, v, hollow, twist, holesize, topshear,
                                                profilecut, taper_b,
                                                revolutions, radiusoffset, skew, 1);
                    } else if (code == (int)ScriptBaseClass.PRIM_TYPE_RING) {
                        if (remain < 11)
                            return;

                        face = rules.GetLSLIntegerItem (idx++); // holeshape
                        v = rules.GetVector3Item (idx++); //cut
                        hollow = (float)rules.GetLSLFloatItem (idx++);
                        twist = rules.GetVector3Item (idx++);
                        holesize = rules.GetVector3Item (idx++);
                        topshear = rules.GetVector3Item (idx++);
                        profilecut = rules.GetVector3Item (idx++);
                        taper_b = rules.GetVector3Item (idx++); // taper_a
                        revolutions = (float)rules.GetLSLFloatItem (idx++);
                        radiusoffset = (float)rules.GetLSLFloatItem (idx++);
                        skew = (float)rules.GetLSLFloatItem (idx++);
                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Curve1;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), face, v, hollow, twist, holesize, topshear,
                                                profilecut, taper_b,
                                                revolutions, radiusoffset, skew, 3);
                    } else if (code == (int)ScriptBaseClass.PRIM_TYPE_SCULPT) {
                        if (remain < 2)
                            return;

                        string map = rules.Data [idx++].ToString ();
                        face = rules.GetLSLIntegerItem (idx++); // type
                        (part as ISceneChildEntity).Shape.PathCurve = (byte)Extrusion.Curve1;
                        SetPrimitiveShapeParams ((part as ISceneChildEntity), map, face);
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_TEXTURE) {
                    if (remain < 5)
                        return;
                    if (part is ISceneChildEntity) {
                    } else
                        return;
                    face = rules.GetLSLIntegerItem (idx++);
                    string tex = rules.Data [idx++].ToString ();
                    LSL_Vector repeats = rules.GetVector3Item (idx++);
                    LSL_Vector offsets = rules.GetVector3Item (idx++);
                    double rotation = rules.GetLSLFloatItem (idx++);

                    SetTexture ((part as ISceneChildEntity), tex, face);
                    ScaleTexture ((part as ISceneChildEntity), repeats.x, repeats.y, face);
                    OffsetTexture ((part as ISceneChildEntity), offsets.x, offsets.y, face);
                    RotateTexture ((part as ISceneChildEntity), rotation, face);
                } else if (code == (int)ScriptBaseClass.PRIM_COLOR) {
                    if (remain < 3)
                        return;
                    if (part is ISceneChildEntity) {
                    } else
                        return;
                    face = rules.GetLSLIntegerItem (idx++);
                    LSL_Vector color = rules.GetVector3Item (idx++);
                    double alpha = rules.GetLSLFloatItem (idx++);

                    (part as ISceneChildEntity).SetFaceColor (
                        new Vector3 ((float)color.x, (float)color.y, (float)color.z), face);
                    SetAlpha ((part as ISceneChildEntity), alpha, face);
                } else if (code == (int)ScriptBaseClass.PRIM_FLEXIBLE) {
                    if (remain < 7)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    bool flexi = rules.GetLSLIntegerItem (idx++);
                    int softness = rules.GetLSLIntegerItem (idx++);
                    float gravity = (float)rules.GetLSLFloatItem (idx++);
                    float friction = (float)rules.GetLSLFloatItem (idx++);
                    float wind = (float)rules.GetLSLFloatItem (idx++);
                    float tension = (float)rules.GetLSLFloatItem (idx++);
                    LSL_Vector force = rules.GetVector3Item (idx++);

                    SetFlexi ((part as ISceneChildEntity), flexi, softness, gravity, friction, wind, tension, force);
                } else if (code == (int)ScriptBaseClass.PRIM_POINT_LIGHT) {
                    if (remain < 5)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    bool light = rules.GetLSLIntegerItem (idx++);
                    LSL_Vector lightcolor = rules.GetVector3Item (idx++);
                    float intensity = (float)rules.GetLSLFloatItem (idx++);
                    float radius = (float)rules.GetLSLFloatItem (idx++);
                    float falloff = (float)rules.GetLSLFloatItem (idx++);

                    SetPointLight ((part as ISceneChildEntity), light, lightcolor, intensity, radius, falloff);
                } else if (code == (int)ScriptBaseClass.PRIM_GLOW) {
                    if (remain < 2)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    face = rules.GetLSLIntegerItem (idx++);
                    float glow = (float)rules.GetLSLFloatItem (idx++);

                    SetGlow ((part as ISceneChildEntity), face, glow);
                } else if (code == (int)ScriptBaseClass.PRIM_BUMP_SHINY) {
                    if (remain < 3)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    face = rules.GetLSLIntegerItem (idx++);
                    int shiny = rules.GetLSLIntegerItem (idx++);
                    Bumpiness bump = (Bumpiness)Convert.ToByte ((int)rules.GetLSLIntegerItem (idx++));

                    SetShiny (part as ISceneChildEntity, face, shiny, bump);
                } else if (code == (int)ScriptBaseClass.PRIM_FULLBRIGHT) {
                    if (remain < 2)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    face = rules.GetLSLIntegerItem (idx++);
                    bool st = rules.GetLSLIntegerItem (idx++);
                    SetFullBright (part as ISceneChildEntity, face, st);
                } else if (code == (int)ScriptBaseClass.PRIM_MATERIAL) {
                    if (remain < 1)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    int mat = rules.GetLSLIntegerItem (idx++);
                    if (mat < 0 || mat > 7)
                        return;

                    (part as ISceneChildEntity).UpdateMaterial (mat);
                } else if (code == (int)ScriptBaseClass.PRIM_PHANTOM) {
                    if (remain < 1)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    string ph = rules.Data [idx++].ToString ();

                    bool phantom = ph.Equals ("1");

                    (part as ISceneChildEntity).ScriptSetPhantomStatus (phantom);
                } else if (code == (int)ScriptBaseClass.PRIM_PHYSICS) {
                    if (remain < 1)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    string phy = rules.Data [idx++].ToString ();

                    m_host.ParentEntity.ScriptSetPhysicsStatus (phy.Equals ("1"));
                } else if (code == (int)ScriptBaseClass.PRIM_TEMP_ON_REZ) {
                    if (remain < 1)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    string temp = rules.Data [idx++].ToString ();

                    bool tempOnRez = temp.Equals ("1");

                    (part as ISceneChildEntity).ScriptSetTemporaryStatus (tempOnRez);
                } else if (code == (int)ScriptBaseClass.PRIM_TEXGEN) {
                    if (remain < 2)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    //face,type
                    face = rules.GetLSLIntegerItem (idx++);
                    int style = rules.GetLSLIntegerItem (idx++);
                    SetTexGen ((part as ISceneChildEntity), face, style);
                } else if (code == (int)ScriptBaseClass.PRIM_TEXT) {
                    if (remain < 3)
                        return;
                    if (!(part is ISceneChildEntity))
                        return;
                    string primText = rules.GetLSLStringItem (idx++);
                    LSL_Vector primTextColor = rules.GetVector3Item (idx++);
                    LSL_Float primTextAlpha = rules.GetLSLFloatItem (idx++);
                    Vector3 av3 = new Vector3 (Util.Clip ((float)primTextColor.x, 0.0f, 1.0f),
                                              Util.Clip ((float)primTextColor.y, 0.0f, 1.0f),
                                              Util.Clip ((float)primTextColor.z, 0.0f, 1.0f));
                    (part as ISceneChildEntity).SetText (primText, av3, Util.Clip ((float)primTextAlpha, 0.0f, 1.0f));
                } else if (code == (int)ScriptBaseClass.PRIM_OMEGA) {
                    if (remain < 3)
                        return;
                    LSL_Vector direction = rules.GetVector3Item (idx++);
                    LSL_Float spinrate = rules.GetLSLFloatItem (idx++);
                    LSL_Float gain = rules.GetLSLFloatItem (idx++);
                    if (part is ISceneChildEntity)
                        llTargetOmega (direction, spinrate, gain);
                } else if (code == (int)ScriptBaseClass.PRIM_PHYSICS_SHAPE_TYPE) {
                    bool UsePhysics = ((m_host.Flags & PrimFlags.Physics) != 0);
                    bool IsTemporary = ((m_host.Flags & PrimFlags.TemporaryOnRez) != 0);
                    bool IsPhantom = ((m_host.Flags & PrimFlags.Phantom) != 0);
                    bool IsVolumeDetect = m_host.VolumeDetectActive;
                    ObjectFlagUpdatePacket.ExtraPhysicsBlock [] blocks = new ObjectFlagUpdatePacket.ExtraPhysicsBlock [1];
                    blocks [0] = new ObjectFlagUpdatePacket.ExtraPhysicsBlock {
                        Density = m_host.Density,
                        Friction = m_host.Friction,
                        GravityMultiplier = m_host.GravityMultiplier
                    };
                    LSL_Integer shapeType = rules.GetLSLIntegerItem (idx++);
                    if (shapeType == ScriptBaseClass.PRIM_PHYSICS_SHAPE_PRIM)
                        blocks [0].PhysicsShapeType = (byte)shapeType.value;
                    else if (shapeType == ScriptBaseClass.PRIM_PHYSICS_SHAPE_NONE)
                        blocks [0].PhysicsShapeType = (byte)shapeType.value;
                    else //if(shapeType == ScriptBaseClass.PRIM_PHYSICS_SHAPE_CONVEX)
                        blocks [0].PhysicsShapeType = (byte)shapeType.value;
                    blocks [0].Restitution = m_host.Restitution;
                    if (part is ISceneChildEntity)
                        if ((part as ISceneChildEntity).UpdatePrimFlags (UsePhysics,
                                                                        IsTemporary, IsPhantom, IsVolumeDetect, blocks))
                            (part as ISceneChildEntity).ParentEntity.RebuildPhysicalRepresentation (true, null);
                } else if (code == (int)ScriptBaseClass.PRIM_LINK_TARGET) {
                    if (remain < 1)
                        return;
                    LSL_Integer nextLink = rules.GetLSLIntegerItem (idx++);
                    List<IEntity> entities = GetLinkPartsAndEntities (nextLink);
                    if (entities.Count > 0)
                        part = entities [0];
                } else if (code == (int)ScriptBaseClass.OS_PRIM_PROJECTION) {
                    if (remain < 5 || !allowOpenSimParams)
                        return;
                    bool projection = rules.GetLSLIntegerItem (idx++) != 0;
                    string texture = rules.GetLSLStringItem (idx++);
                    UUID textureKey;
                    UUID.TryParse (texture, out textureKey);
                    float fov = (float)rules.GetLSLFloatItem (idx++);
                    float focus = (float)rules.GetLSLFloatItem (idx++);
                    float ambiance =
                            (float)rules.GetLSLFloatItem (idx++);

                    if (part is ISceneChildEntity) {
                        (part as ISceneChildEntity).Shape.ProjectionEntry = projection;
                        (part as ISceneChildEntity).Shape.ProjectionTextureUUID = textureKey;
                        (part as ISceneChildEntity).Shape.ProjectionFOV = fov;
                        (part as ISceneChildEntity).Shape.ProjectionFocus = focus;
                        (part as ISceneChildEntity).Shape.ProjectionAmbiance = ambiance;

                        (part as ISceneChildEntity).ScheduleUpdate (PrimUpdateFlags.FullUpdate);
                    }
                } else if (code == (int)ScriptBaseClass.OS_PRIM_VELOCITY) {
                    if (remain < 1 || !allowOpenSimParams)
                        return;
                    LSL_Vector velocity = rules.GetVector3Item (idx++);
                    (part as ISceneChildEntity).Velocity = velocity.ToVector3 ();
                    (part as ISceneChildEntity).ScheduleTerseUpdate ();
                } else if (code == (int)ScriptBaseClass.OS_PRIM_ACCELERATION) {
                    if (remain < 1 || !allowOpenSimParams)
                        return;
                    LSL_Vector accel = rules.GetVector3Item (idx++);
                    (part as ISceneChildEntity).Acceleration = accel.ToVector3 ();
                    (part as ISceneChildEntity).ScheduleTerseUpdate ();
                }
            }
        }


        public LSL_List GetLinkPrimitiveParams (ISceneChildEntity part, LSL_List rules, bool allowOpenSimParams)
        {
            LSL_List res = new LSL_List ();
            int idx = 0;
            while (idx < rules.Length) {
                int code = rules.GetLSLIntegerItem (idx++);
                int remain = rules.Length - idx;
                Primitive.TextureEntry tex = part.Shape.Textures;
                int face = 0;

                if (code == (int)ScriptBaseClass.PRIM_NAME) {
                    res.Add (new LSL_String (part.Name));
                } else if (code == (int)ScriptBaseClass.PRIM_DESC) {
                    res.Add (new LSL_String (part.Description));
                } else if (code == (int)ScriptBaseClass.PRIM_MATERIAL) {
                    res.Add (new LSL_Integer (part.Material));
                } else if (code == (int)ScriptBaseClass.PRIM_PHYSICS) {
                    res.Add ((part.GetEffectiveObjectFlags () & (uint)PrimFlags.Physics) != 0
                                ? new LSL_Integer (1)
                                : new LSL_Integer (0));
                } else if (code == (int)ScriptBaseClass.PRIM_TEMP_ON_REZ) {
                    res.Add ((part.GetEffectiveObjectFlags () & (uint)PrimFlags.TemporaryOnRez) != 0
                                ? new LSL_Integer (1)
                                : new LSL_Integer (0));
                } else if (code == (int)ScriptBaseClass.PRIM_PHANTOM) {
                    res.Add ((part.GetEffectiveObjectFlags () & (uint)PrimFlags.Phantom) != 0
                                ? new LSL_Integer (1)
                                : new LSL_Integer (0));
                } else if (code == (int)ScriptBaseClass.PRIM_POSITION) {
                    Vector3 tmp = part.AbsolutePosition;
                    LSL_Vector v = new LSL_Vector (tmp.X,
                                                  tmp.Y,
                                                  tmp.Z);
                    // For some reason, the part.AbsolutePosition.* values do not change if the
                    // linkset is rotated; they always reflect the child prim's world position
                    // as though the linkset is unrotated. This is incompatible behavior with SL's
                    // implementation, so will break scripts imported from there (not to mention it
                    // makes it more difficult to determine a child prim's actual inworld position).
                    if (part.ParentID != 0) {
                        LSL_Rotation rtmp = llGetRootRotation ();
                        LSL_Vector rpos = llGetRootPosition ();
                        v = ((v - rpos) * rtmp) + rpos;
                    }
                    res.Add (v);
                } else if (code == (int)ScriptBaseClass.PRIM_POS_LOCAL) {
                    res.Add (GetLocalPos (part));
                } else if (code == (int)ScriptBaseClass.PRIM_SIZE) {
                    Vector3 tmp = part.Scale;
                    res.Add (new LSL_Vector (tmp.X,
                                           tmp.Y,
                                           tmp.Z));
                } else if (code == (int)ScriptBaseClass.PRIM_ROTATION) {
                    res.Add (GetPartRot (part));
                } else if (code == (int)ScriptBaseClass.PRIM_TYPE) {
                    // implementing box
                    PrimitiveBaseShape Shape = part.Shape;
                    int primType = (int)part.GetPrimType ();
                    res.Add (new LSL_Integer (primType));
                    double topshearx = (sbyte)Shape.PathShearX / 100.0; // Fix negative values for PathShearX
                    double topsheary = (sbyte)Shape.PathShearY / 100.0; // and PathShearY.
                    if (primType == ScriptBaseClass.PRIM_TYPE_BOX ||
                        primType == ScriptBaseClass.PRIM_TYPE_CYLINDER ||
                        primType == ScriptBaseClass.PRIM_TYPE_PRISM) {
                        res.Add (new LSL_Integer (Shape.ProfileCurve));
                        res.Add (new LSL_Vector (Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));
                        res.Add (new LSL_Float (Shape.ProfileHollow / 50000.0));
                        res.Add (new LSL_Vector (Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));
                        res.Add (new LSL_Vector (1 - (Shape.PathScaleX / 100.0 - 1), 1 - (Shape.PathScaleY / 100.0 - 1), 0));
                        res.Add (new LSL_Vector (topshearx, topsheary, 0));
                    }

                    if (primType == ScriptBaseClass.PRIM_TYPE_SPHERE) {
                        res.Add (new LSL_Integer (Shape.ProfileCurve));
                        res.Add (new LSL_Vector (Shape.PathBegin / 50000.0, 1 - Shape.PathEnd / 50000.0, 0));
                        res.Add (new LSL_Float (Shape.ProfileHollow / 50000.0));
                        res.Add (new LSL_Vector (Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));
                        res.Add (new LSL_Vector (Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));
                    }

                    if (primType == ScriptBaseClass.PRIM_TYPE_SCULPT) {
                        res.Add (Shape.SculptTexture.ToString ());
                        res.Add (new LSL_Integer (Shape.SculptType));
                    }
                    if (primType == ScriptBaseClass.PRIM_TYPE_RING ||
                        primType == ScriptBaseClass.PRIM_TYPE_TUBE ||
                        primType == ScriptBaseClass.PRIM_TYPE_TORUS) {
                        // holeshape
                        res.Add (new LSL_Integer (Shape.ProfileCurve));

                        // cut
                        res.Add (new LSL_Vector (Shape.PathBegin / 50000.0, 1 - Shape.PathEnd / 50000.0, 0));

                        // hollow
                        res.Add (new LSL_Float (Shape.ProfileHollow / 50000.0));

                        // twist
                        res.Add (new LSL_Vector (Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));

                        // vector holesize
                        res.Add (new LSL_Vector (1 - (Shape.PathScaleX / 100.0 - 1), 1 - (Shape.PathScaleY / 100.0 - 1), 0));

                        // vector topshear
                        res.Add (new LSL_Vector (topshearx, topsheary, 0));

                        // vector profilecut
                        res.Add (new LSL_Vector (Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));

                        // vector tapera
                        res.Add (new LSL_Vector (Shape.PathTaperX / 100.0, Shape.PathTaperY / 100.0, 0));

                        // float revolutions
                        res.Add (
                            new LSL_Float (Math.Round (Shape.PathRevolutions * 0.015d, 2, MidpointRounding.AwayFromZero)) +
                            1.0d);
                        // Slightly inaccurate, because an unsigned byte is being used to represent
                        // the entire range of floating-point values from 1.0 through 4.0 (which is how
                        // SL does it).
                        //
                        // Using these formulas to store and retrieve PathRevolutions, it is not
                        // possible to use all values between 1.00 and 4.00. For instance, you can't
                        // represent 1.10. You can represent 1.09 and 1.11, but not 1.10. So, if you
                        // use llSetPrimitiveParams to set revolutions to 1.10 and then retreive them
                        // with llGetPrimitiveParams, you'll retrieve 1.09. You can also see a similar
                        // behavior in the viewer as you cannot set 1.10. The viewer jumps to 1.11.
                        // In SL, llSetPrimitveParams and llGetPrimitiveParams can set and get a value
                        // such as 1.10. So, SL must store and retreive the actual user input rather
                        // than only storing the encoded value.

                        // float radiusoffset
                        res.Add (new LSL_Float (Shape.PathRadiusOffset / 100.0));

                        // float skew
                        res.Add (new LSL_Float (Shape.PathSkew / 100.0));
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_TEXTURE) {
                    if (remain < 1)
                        return res;
                    face = rules.GetLSLIntegerItem (idx++);
                    if (face == ScriptBaseClass.ALL_SIDES) {
                        for (face = 0; face < GetNumberOfSides (part); face++) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);

                            res.Add (new LSL_String (texface.TextureID.ToString ()));
                            res.Add (new LSL_Vector (texface.RepeatU,
                                                   texface.RepeatV,
                                                   0));
                            res.Add (new LSL_Vector (texface.OffsetU,
                                                   texface.OffsetV,
                                                   0));
                            res.Add (new LSL_Float (texface.Rotation));
                        }
                    } else {
                        if (face >= 0 && face < GetNumberOfSides (part)) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);

                            res.Add (new LSL_String (texface.TextureID.ToString ()));
                            res.Add (new LSL_Vector (texface.RepeatU,
                                                   texface.RepeatV,
                                                   0));
                            res.Add (new LSL_Vector (texface.OffsetU,
                                                   texface.OffsetV,
                                                   0));
                            res.Add (new LSL_Float (texface.Rotation));
                        }
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_COLOR) {
                    if (remain < 1)
                        return res;
                    face = rules.GetLSLIntegerItem (idx++);
                    tex = part.Shape.Textures;
                    Color4 texcolor;
                    if (face == ScriptBaseClass.ALL_SIDES) {
                        for (face = 0; face < GetNumberOfSides (part); face++) {
                            texcolor = tex.GetFace ((uint)face).RGBA;
                            res.Add (new LSL_Vector (texcolor.R,
                                                   texcolor.G,
                                                   texcolor.B));
                            res.Add (new LSL_Float (texcolor.A));
                        }
                    } else {
                        texcolor = tex.GetFace ((uint)face).RGBA;
                        res.Add (new LSL_Vector (texcolor.R,
                                               texcolor.G,
                                               texcolor.B));
                        res.Add (new LSL_Float (texcolor.A));
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_BUMP_SHINY) {
                    if (remain < 1)
                        return res;

                    face = rules.GetLSLIntegerItem (idx++);

                    if (face == ScriptBaseClass.ALL_SIDES) {
                        for (face = 0; face < GetNumberOfSides (part); face++) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);
                            // Convert Shininess to PRIM_SHINY_*
                            res.Add (new LSL_Integer ((uint)texface.Shiny >> 6));
                            // PRIM_BUMP_*
                            res.Add (new LSL_Integer ((int)texface.Bump));
                        }
                    } else {
                        if (face >= 0 && face < GetNumberOfSides (part)) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);
                            // Convert Shininess to PRIM_SHINY_*
                            res.Add (new LSL_Integer ((uint)texface.Shiny >> 6));
                            // PRIM_BUMP_*
                            res.Add (new LSL_Integer ((int)texface.Bump));
                        }
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_FULLBRIGHT) {
                    if (remain < 1)
                        return res;

                    face = rules.GetLSLIntegerItem (idx++);
                    tex = part.Shape.Textures;
                    if (face == ScriptBaseClass.ALL_SIDES) {
                        for (face = 0; face < GetNumberOfSides (part); face++) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);
                            res.Add (new LSL_Integer (texface.Fullbright ? 1 : 0));
                        }
                    } else {
                        if (face >= 0 && face < GetNumberOfSides (part)) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);
                            res.Add (new LSL_Integer (texface.Fullbright ? 1 : 0));
                        }
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_FLEXIBLE) {
                    PrimitiveBaseShape shape = part.Shape;

                    res.Add (shape.FlexiEntry ? new LSL_Integer (1) : new LSL_Integer (0));
                    res.Add (new LSL_Integer (shape.FlexiSoftness)); // softness
                    res.Add (new LSL_Float (shape.FlexiGravity)); // gravity
                    res.Add (new LSL_Float (shape.FlexiDrag)); // friction
                    res.Add (new LSL_Float (shape.FlexiWind)); // wind
                    res.Add (new LSL_Float (shape.FlexiTension)); // tension
                    res.Add (new LSL_Vector (shape.FlexiForceX, // force
                                           shape.FlexiForceY,
                                           shape.FlexiForceZ));
                } else if (code == (int)ScriptBaseClass.PRIM_TEXGEN) {
                    if (remain < 1)
                        return res;

                    face = rules.GetLSLIntegerItem (idx++);
                    if (face == ScriptBaseClass.ALL_SIDES) {
                        for (face = 0; face < GetNumberOfSides (part); face++) {
                            MappingType texgen = tex.GetFace ((uint)face).TexMapType;
                            // Convert MappingType to PRIM_TEXGEN_DEFAULT, PRIM_TEXGEN_PLANAR etc.
                            res.Add (new LSL_Integer ((uint)texgen >> 1));
                        }
                    } else {
                        if (face >= 0 && face < GetNumberOfSides (part)) {
                            MappingType texgen = tex.GetFace ((uint)face).TexMapType;
                            res.Add (new LSL_Integer ((uint)texgen >> 1));
                        }
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_POINT_LIGHT) {
                    PrimitiveBaseShape shape = part.Shape;

                    res.Add (shape.LightEntry ? new LSL_Integer (1) : new LSL_Integer (0));
                    res.Add (new LSL_Vector (shape.LightColorR, // color
                                           shape.LightColorG,
                                           shape.LightColorB));
                    res.Add (new LSL_Float (shape.LightIntensity)); // intensity
                    res.Add (new LSL_Float (shape.LightRadius)); // radius
                    res.Add (new LSL_Float (shape.LightFalloff)); // falloff
                } else if (code == (int)ScriptBaseClass.PRIM_GLOW) {
                    if (remain < 1)
                        return res;

                    face = rules.GetLSLIntegerItem (idx++);
                    if (face == ScriptBaseClass.ALL_SIDES) {
                        for (face = 0; face < GetNumberOfSides (part); face++) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);
                            res.Add (new LSL_Float (texface.Glow));
                        }
                    } else {
                        if (face >= 0 && face < GetNumberOfSides (part)) {
                            Primitive.TextureEntryFace texface = tex.GetFace ((uint)face);
                            res.Add (new LSL_Float (texface.Glow));
                        }
                    }
                } else if (code == (int)ScriptBaseClass.PRIM_TEXT) {
                    Color4 textColor = part.GetTextColor ();
                    res.Add (new LSL_String (part.Text));
                    res.Add (new LSL_Vector (textColor.R,
                                           textColor.G,
                                           textColor.B));
                    res.Add (new LSL_Float (1 - textColor.A));
                } else if (code == (int)ScriptBaseClass.PRIM_ROT_LOCAL) {
                    Quaternion rtmp = part.GetRotationOffset ();
                    res.Add (new LSL_Rotation (rtmp.X, rtmp.Y, rtmp.Z, rtmp.W));
                } else if (code == (int)ScriptBaseClass.PRIM_OMEGA) {
                    Vector3 axis = part.OmegaAxis;
                    LSL_Float spinRate = part.OmegaSpinRate;
                    LSL_Float gain = part.OmegaGain;
                    res.Add (axis);
                    res.Add (spinRate);
                    res.Add (gain);
                } else if (code == (int)ScriptBaseClass.PRIM_PHYSICS_SHAPE_TYPE) {
                    res.Add (new LSL_Integer (part.PhysicsType));
                } else if (code == (int)ScriptBaseClass.PRIM_LINK_TARGET) {
                    if (remain < 1)
                        continue;
                    LSL_Integer nextLink = rules.GetLSLIntegerItem (idx++);
                    List<ISceneChildEntity> entities = GetLinkParts (nextLink);
                    if (entities.Count > 0)
                        part = entities [0];
                } else if (code == (int)ScriptBaseClass.OS_PRIM_PROJECTION) {
                    if (!allowOpenSimParams)
                        return null;
                    res.Add ((LSL_Integer)(
                            part.Shape.ProjectionEntry ? 1 : 0));
                    res.Add ((LSL_Key)part.Shape.ProjectionTextureUUID.ToString ());
                    res.Add ((LSL_Float)part.Shape.ProjectionFOV);
                    res.Add ((LSL_Float)part.Shape.ProjectionFocus);
                    res.Add ((LSL_Float)part.Shape.ProjectionAmbiance);
                } else if (code == (int)ScriptBaseClass.OS_PRIM_VELOCITY) {
                    if (!allowOpenSimParams)
                        return null;
                    res.Add (new LSL_Vector (part.Velocity));
                } else if (code == (int)ScriptBaseClass.OS_PRIM_ACCELERATION) {
                    if (!allowOpenSimParams)
                        return null;
                    res.Add (new LSL_Vector (part.Acceleration));
                }
            }
            return res;
        }


        LSL_List GetPrimMediaParams (ISceneChildEntity obj, int face, LSL_List rules)
        {
            IMoapModule module = World.RequestModuleInterface<IMoapModule> ();
            if (null == module)
                throw new Exception ("Media on a prim functions not available");

            MediaEntry me = module.GetMediaEntry (obj, face);

            // As per http://wiki.secondlife.com/wiki/LlGetPrimMediaParams
            if (null == me)
                return new LSL_List ();

            LSL_List res = new LSL_List ();

            for (int i = 0; i < rules.Length; i++) {
                int code = rules.GetLSLIntegerItem (i);

                if (code == ScriptBaseClass.PRIM_MEDIA_ALT_IMAGE_ENABLE) {
                    // Not implemented
                    res.Add (new LSL_Integer (0));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_CONTROLS) {
                    res.Add (me.Controls == MediaControls.Standard
                                ? new LSL_Integer (ScriptBaseClass.PRIM_MEDIA_CONTROLS_STANDARD)
                                : new LSL_Integer (ScriptBaseClass.PRIM_MEDIA_CONTROLS_MINI));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_CURRENT_URL) {
                    res.Add (new LSL_String (me.CurrentURL));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_HOME_URL) {
                    res.Add (new LSL_String (me.HomeURL));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_LOOP) {
                    res.Add (me.AutoLoop ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_PLAY) {
                    res.Add (me.AutoPlay ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_SCALE) {
                    res.Add (me.AutoScale ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_ZOOM) {
                    res.Add (me.AutoZoom ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_FIRST_CLICK_INTERACT) {
                    res.Add (me.InteractOnFirstClick ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_WIDTH_PIXELS) {
                    res.Add (new LSL_Integer (me.Width));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_HEIGHT_PIXELS) {
                    res.Add (new LSL_Integer (me.Height));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_WHITELIST_ENABLE) {
                    res.Add (me.EnableWhiteList ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_WHITELIST) {
                    string [] urls = (string [])me.WhiteList.Clone ();

                    for (int j = 0; j < urls.Length; j++)
                        urls [j] = Uri.EscapeDataString (urls [j]);

                    res.Add (new LSL_String (string.Join (", ", urls)));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_PERMS_INTERACT) {
                    res.Add (new LSL_Integer ((int)me.InteractPermissions));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_PERMS_CONTROL) {
                    res.Add (new LSL_Integer ((int)me.ControlPermissions));
                }
            }

            return res;
        }


        void ClearPrimMedia (ISceneChildEntity entity, LSL_Integer face)
        {
            // LSL Spec http://wiki.secondlife.com/wiki/LlClearPrimMedia says to fail silently if face is invalid
            // Assuming silently fail means sending back STATUS_OK.  Ideally, need to check this.
            // FIXME: Don't perform the media check directly
            if (face < 0 || face > entity.GetNumberOfSides () - 1)
                return;

            IMoapModule module = World.RequestModuleInterface<IMoapModule> ();
            if (null == module)
                throw new Exception ("Media on a prim functions not available");

            module.ClearMediaEntry (entity, face);
        }


        public LSL_Integer SetPrimMediaParams (ISceneChildEntity obj, int face, LSL_List rules)
        {
            IMoapModule module = World.RequestModuleInterface<IMoapModule> ();
            if (null == module)
                throw new Exception ("Media on a prim functions not available");

            MediaEntry me = module.GetMediaEntry (obj, face) ?? new MediaEntry ();

            int i = 0;

            while (i < rules.Length - 1) {
                int code = rules.GetLSLIntegerItem (i++);

                if (code == ScriptBaseClass.PRIM_MEDIA_ALT_IMAGE_ENABLE) {
                    me.EnableAlterntiveImage = (rules.GetLSLIntegerItem (i++) != 0);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_CONTROLS) {
                    int v = rules.GetLSLIntegerItem (i++);
                    me.Controls = ScriptBaseClass.PRIM_MEDIA_CONTROLS_STANDARD == v
                                      ? MediaControls.Standard
                                      : MediaControls.Mini;
                } else if (code == ScriptBaseClass.PRIM_MEDIA_CURRENT_URL) {
                    me.CurrentURL = rules.GetLSLStringItem (i++);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_HOME_URL) {
                    me.HomeURL = rules.GetLSLStringItem (i++);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_LOOP) {
                    me.AutoLoop = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem (i++));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_PLAY) {
                    me.AutoPlay = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem (i++));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_SCALE) {
                    me.AutoScale = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem (i++));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_AUTO_ZOOM) {
                    me.AutoZoom = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem (i++));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_FIRST_CLICK_INTERACT) {
                    me.InteractOnFirstClick = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem (i++));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_WIDTH_PIXELS) {
                    me.Width = rules.GetLSLIntegerItem (i++);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_HEIGHT_PIXELS) {
                    me.Height = rules.GetLSLIntegerItem (i++);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_WHITELIST_ENABLE) {
                    me.EnableWhiteList = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem (i++));
                } else if (code == ScriptBaseClass.PRIM_MEDIA_WHITELIST) {
                    string [] rawWhiteListUrls = rules.GetLSLStringItem (i++).ToString ().Split (new [] { ',' });
                    List<string> whiteListUrls = new List<string> ();

                    Array.ForEach (
                        rawWhiteListUrls, rawUrl => whiteListUrls.Add (rawUrl.Trim ()));

                    me.WhiteList = whiteListUrls.ToArray ();
                } else if (code == ScriptBaseClass.PRIM_MEDIA_PERMS_INTERACT) {
                    me.InteractPermissions = (MediaPermission)(byte)(int)rules.GetLSLIntegerItem (i++);
                } else if (code == ScriptBaseClass.PRIM_MEDIA_PERMS_CONTROL) {
                    me.ControlPermissions = (MediaPermission)(byte)(int)rules.GetLSLIntegerItem (i++);
                }
            }

            module.SetMediaEntry (obj, face, me);

            return ScriptBaseClass.STATUS_OK;
        }





        internal UUID ScriptByName (string name)
        {
            lock (m_host.TaskInventory) {
                foreach (TaskInventoryItem item in m_host.TaskInventory.Values) {
                    if (item.Type == 10 && item.Name == name)
                        return item.ItemID;
                }
            }

            return UUID.Zero;
        }

        /// <summary>
        /// Reports the script error in the viewer's Script Warning/Error dialog and shouts it on the debug channel.
        /// </summary>
        /// <param name="command">The name of the command that generated the error.</param>
        /// <param name="message">The error message to report to the user.</param>
        internal void Error (string command, string message)
        {
            string text = command + ": " + message;
            if (text.Length > 1023) {
                text = text.Substring (0, 1023);
            }

            IWorldComm wComm = World.RequestModuleInterface<IWorldComm> ();
            if (wComm != null) {
                wComm.DeliverMessage (ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, text);
            }
        }

        /// <summary>
        /// Reports that the command is not implemented as a script error.
        /// </summary>
        /// <param name="command">The name of the command that is not implemented.</param>
        /// <param name="message">Additional information to report to the user. (Optional)</param>
        internal void NotImplemented (string command, string message = "")
        {
            if (throwErrorOnNotImplemented) {

                if (message != "") {
                    message = " - " + message;
                }

                throw new NotImplementedException ("Command not implemented: " + command + message);
            } else {
                string text = "Command not implemented";
                if (message != "") {
                    text = text + " - " + message;
                }

                Error (command, text);
            }
        }

        /// <summary>
        /// Reports that the command is deprecated as a script error.
        /// </summary>
        /// <param name="command">The name of the command that is deprecated.</param>
        /// <param name="message">Additional information to report to the user. (Optional)</param>
        internal void Deprecated (string command, string message = "")
        {
            string text = "Command deprecated";
            if (message != "") {
                text = text + " - " + message;
            }

            Error (command, text);
        }

        public delegate void AssetRequestCallback (UUID assetID, AssetBase asset);

        protected void WithNotecard (UUID assetID, AssetRequestCallback cb)
        {
            World.AssetService.Get (assetID.ToString (), this,
                                   delegate (string i, object sender, AssetBase a) {
                                       UUID uuid = UUID.Zero;
                                       UUID.TryParse (i, out uuid);
                                       cb (uuid, a);
                                   });
        }


        bool InBoundingBox (IScenePresence avatar, Vector3 point)
        {
            IAvatarAppearanceModule appModule = avatar.RequestModuleInterface<IAvatarAppearanceModule> ();
            if (appModule == null || appModule.Appearance == null)
                return false;

            float height = appModule.Appearance.AvatarHeight;
            Vector3 b1 = avatar.AbsolutePosition + new Vector3 (-0.22f, -0.22f, -height / 2);
            Vector3 b2 = avatar.AbsolutePosition + new Vector3 (0.22f, 0.22f, height / 2);

            if (point.X > b1.X && point.X < b2.X &&
                point.Y > b1.Y && point.Y < b2.Y &&
                point.Z > b1.Z && point.Z < b2.Z)
                return true;
            return false;
        }

        ContactResult [] AvatarIntersection (Vector3 rayStart, Vector3 rayEnd)
        {
            List<ContactResult> contacts = new List<ContactResult> ();

            Vector3 ab = rayEnd - rayStart;

            World.ForEachScenePresence (delegate (IScenePresence sp) {
                Vector3 ac = sp.AbsolutePosition - rayStart;
                //                Vector3 bc = sp.AbsolutePosition - rayEnd;

                double d = Math.Abs (Vector3.Mag (Vector3.Cross (ab, ac)) / Vector3.Distance (rayStart, rayEnd));

                if (d > 1.5)
                    return;

                double d2 = Vector3.Dot (Vector3.Negate (ab), ac);

                if (d2 > 0)
                    return;

                double dp = Math.Sqrt (Vector3.Mag (ac) * Vector3.Mag (ac) - d * d);
                Vector3 p = rayStart + Vector3.Divide (Vector3.Multiply (ab, (float)dp), Vector3.Mag (ab));

                if (!InBoundingBox (sp, p))
                    return;

                ContactResult result = new ContactResult ();
                result.ConsumerID = sp.LocalId;
                result.Depth = Vector3.Distance (rayStart, p);
                result.Normal = Vector3.Zero;
                result.Pos = p;

                contacts.Add (result);
            });

            return contacts.ToArray ();
        }

        ContactResult [] ObjectIntersection (Vector3 rayStart, Vector3 rayEnd, bool includePhysical, bool includeNonPhysical, bool includePhantom, int max)
        {
            List<ContactResult> contacts = World.PhysicsScene.RaycastWorld (rayStart, Vector3.Normalize (rayEnd - rayStart), Vector3.Distance (rayEnd, rayStart), max);

            for (int i = 0; i < contacts.Count; i++) {
                ISceneEntity grp = World.GetGroupByPrim (contacts [i].ConsumerID);
                if (grp == null || (!includePhysical && grp.RootChild.PhysActor.IsPhysical) ||
                    (!includeNonPhysical && !grp.RootChild.PhysActor.IsPhysical))
                    contacts.RemoveAt (i--);
            }

            if (includePhantom) {
                Ray ray = new Ray (rayStart, Vector3.Normalize (rayEnd - rayStart));

                Vector3 ab = rayEnd - rayStart;

                ISceneEntity [] objlist = World.Entities.GetEntities ();
                foreach (ISceneEntity group in objlist) {
                    if (m_host.ParentEntity == group)
                        continue;

                    if (group.IsAttachment)
                        continue;

                    if (group.RootChild.PhysActor != null)
                        continue;


                    // Find the radius ouside of which we don't even need to hit test
                    float minX;
                    float maxX;
                    float minY;
                    float maxY;
                    float minZ;
                    float maxZ;

                    float radius = 0.0f;

                    group.GetAxisAlignedBoundingBoxRaw (out minX, out maxX, out minY, out maxY, out minZ, out maxZ);

                    if (Math.Abs (minX) > radius)
                        radius = Math.Abs (minX);
                    if (Math.Abs (minY) > radius)
                        radius = Math.Abs (minY);
                    if (Math.Abs (minZ) > radius)
                        radius = Math.Abs (minZ);
                    if (Math.Abs (maxX) > radius)
                        radius = Math.Abs (maxX);
                    if (Math.Abs (maxY) > radius)
                        radius = Math.Abs (maxY);
                    if (Math.Abs (maxZ) > radius)
                        radius = Math.Abs (maxZ);
                    radius = radius * 1.413f;
                    Vector3 ac = group.AbsolutePosition - rayStart;
                    //                Vector3 bc = group.AbsolutePosition - rayEnd;

                    double d = Math.Abs (Vector3.Mag (Vector3.Cross (ab, ac)) / Vector3.Distance (rayStart, rayEnd));

                    // Too far off ray, don't bother
                    if (d > radius)
                        continue;

                    // Behind ray, drop
                    double d2 = Vector3.Dot (Vector3.Negate (ab), ac);
                    if (d2 > 0)
                        continue;

                    ray = new Ray (rayStart, Vector3.Normalize (rayEnd - rayStart));
                    EntityIntersection intersection = group.TestIntersection (ray, true, false);
                    // Miss.
                    if (!intersection.HitTF)
                        continue;

                    Vector3 b1 = new Vector3 (minX, minY, minZ);
                    Vector3 b2 = new Vector3 (maxX, maxY, maxZ);
                    //m_log.DebugFormat("[LLCASTRAY]: min<{0},{1},{2}>, max<{3},{4},{5}> = hitp<{6},{7},{8}>", b1.X,b1.Y,b1.Z,b2.X,b2.Y,b2.Z,intersection.ipoint.X,intersection.ipoint.Y,intersection.ipoint.Z);
                    if (!(intersection.ipoint.X >= b1.X && intersection.ipoint.X <= b2.X &&
                        intersection.ipoint.Y >= b1.Y && intersection.ipoint.Y <= b2.Y &&
                        intersection.ipoint.Z >= b1.Z && intersection.ipoint.Z <= b2.Z))
                        continue;

                    ContactResult result = new ContactResult ();
                    result.ConsumerID = group.LocalId;
                    result.Depth = intersection.distance;
                    result.Normal = intersection.normal;
                    result.Pos = intersection.ipoint;

                    contacts.Add (result);
                }
            }

            return contacts.ToArray ();
        }

        struct Tri
        {
            public Vector3 p1;
            public Vector3 p2;
            public Vector3 p3;
        }

        ContactResult? GroundIntersection (Vector3 rayStart, Vector3 rayEnd)
        {
            ITerrainChannel heightfield = World.RequestModuleInterface<ITerrainChannel> ();
            List<ContactResult> contacts = new List<ContactResult> ();

            double min = 2048.0;
            double max = 0.0;

            // Find the min and max of the heightfield
            for (int x = 0; x < heightfield.Width; x++) {
                for (int y = 0; y < heightfield.Height; y++) {
                    if (heightfield [x, y] > max)
                        max = heightfield [x, y];
                    if (heightfield [x, y] < min)
                        min = heightfield [x, y];
                }
            }


            // A ray extends past rayEnd, but doesn't go back before
            // rayStart. If the start is above the highest point of the ground
            // and the ray goes up, we can't hit the ground. Ever.
            if (rayStart.Z > max && rayEnd.Z >= rayStart.Z)
                return null;

            // Same for going down
            if (rayStart.Z < min && rayEnd.Z <= rayStart.Z)
                return null;

            List<Tri> trilist = new List<Tri> ();

            // Create our triangle list
            for (int x = 1; x < heightfield.Width; x++) {
                for (int y = 1; y < heightfield.Height; y++) {
                    Tri t1 = new Tri ();
                    Tri t2 = new Tri ();

                    Vector3 p1 = new Vector3 (x - 1, y - 1, heightfield [x - 1, y - 1]);
                    Vector3 p2 = new Vector3 (x, y - 1, heightfield [x, y - 1]);
                    Vector3 p3 = new Vector3 (x, y, heightfield [x, y]);
                    Vector3 p4 = new Vector3 (x - 1, y, heightfield [x - 1, y]);

                    t1.p1 = p1;
                    t1.p2 = p2;
                    t1.p3 = p3;

                    t2.p1 = p3;
                    t2.p2 = p4;
                    t2.p3 = p1;

                    trilist.Add (t1);
                    trilist.Add (t2);
                }
            }

            // Ray direction
            Vector3 rayDirection = rayEnd - rayStart;

            foreach (Tri t in trilist) {
                // Compute triangle plane normal and edges
                Vector3 u = t.p2 - t.p1;
                Vector3 v = t.p3 - t.p1;
                Vector3 n = Vector3.Cross (u, v);

                if (n == Vector3.Zero)
                    continue;

                Vector3 w0 = rayStart - t.p1;
                double a = -Vector3.Dot (n, w0);
                double b = Vector3.Dot (n, rayDirection);

                // Not intersecting the plane, or in plane (same thing)
                // Ignoring this MAY cause the ground to not be detected
                // sometimes
                if (Math.Abs (b) < 0.000001)
                    continue;

                double r = a / b;

                // ray points away from plane
                if (r < 0.0)
                    continue;

                Vector3 ip = rayStart + Vector3.Multiply (rayDirection, (float)r);

                float uu = Vector3.Dot (u, u);
                float uv = Vector3.Dot (u, v);
                float vv = Vector3.Dot (v, v);
                Vector3 w = ip - t.p1;
                float wu = Vector3.Dot (w, u);
                float wv = Vector3.Dot (w, v);
                float d = uv * uv - uu * vv;

                float cs = (uv * wv - vv * wu) / d;
                if (cs < 0 || cs > 1.0)
                    continue;
                float ct = (uv * wu - uu * wv) / d;
                if (ct < 0 || (cs + ct) > 1.0)
                    continue;

                // Add contact point
                ContactResult result = new ContactResult ();
                result.ConsumerID = 0;
                result.Depth = Vector3.Distance (rayStart, ip);
                result.Normal = n;
                result.Pos = ip;

                contacts.Add (result);
            }

            if (contacts.Count == 0)
                return null;

            contacts.Sort (delegate (ContactResult a, ContactResult b) {
                return a.Depth.CompareTo (b.Depth);
            });

            return contacts [0];
        }

        void castRaySort (Vector3 pos, ref List<ContactResult> list)
        {
            list.Sort ((a, b) => Vector3.DistanceSquared (a.Pos, pos).CompareTo (Vector3.DistanceSquared (b.Pos, pos)));
        }


        public void SetPrimitiveParamsEx (LSL_Key prim, LSL_List rules)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.High, "osSetPrimitiveParams", m_host, "OSSL", m_itemID))
                return;
            ISceneChildEntity obj = World.GetSceneObjectPart (prim);
            if (obj == null)
                return;

            if (obj.OwnerID != m_host.OwnerID)
                return;

            SetPrimParams (obj, rules, m_allowOpenSimParams);
        }

        public LSL_List GetLinkPrimitiveParamsEx (LSL_Key prim, LSL_List rules)
        {
            ISceneChildEntity obj = World.GetSceneObjectPart (prim);
            if (obj == null)
                return new LSL_List ();

            if (obj.OwnerID == m_host.OwnerID)
                return new LSL_List ();

            return GetLinkPrimitiveParams (obj, rules, true);
        }

        public void print (string str)
        {
            if (!ScriptProtection.CheckThreatLevel (ThreatLevel.Severe, "print", m_host, "LSL", m_itemID))
                return;

            if (m_ScriptEngine.Config.GetBoolean ("AllowosConsoleCommand", false)) {
                if (World.Permissions.CanRunConsoleCommand (m_host.OwnerID)) {
                    // yes, this is a real LSL function. See: http://wiki.secondlife.com/wiki/Print
                    MainConsole.Instance.Fatal ("LSL print():" + str);
                }
            }
        }


    }


}
