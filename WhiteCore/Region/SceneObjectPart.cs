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
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using ProtoBuf;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Utilities;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;

namespace WhiteCore.Region
{
    [Serializable, ProtoContract()]
    public class SceneObjectPart : ISceneChildEntity
    {
        /// <value>
        ///     Denote all sides of the prim
        /// </value>
        public const int ALL_SIDES = -1;

        public virtual Quaternion GroupRotation
        {
            get { return ParentGroup.GroupRotation; }
        }

        #region ISceneChildEntity Members

        /// <value>
        ///     Is this sop a root part?
        /// </value>
        [XmlIgnore]
        public bool IsRoot
        {
            get { return ParentGroup.RootPart == this; }
        }

        public virtual Quaternion Rotation
        {
            get { return ParentGroup.GroupRotation; }
            set { }
        }

        //---------------

        public void ApplyNextOwnerPermissions()
        {
            _baseMask &= _nextOwnerMask;
            _ownerMask &= _nextOwnerMask;
            _everyoneMask &= _nextOwnerMask;

            Inventory.ApplyNextOwnerPermissions();
        }

        public Color4 GetTextColor()
        {
            Color color = Color;
            return new Color4(color.R, color.G, color.B, (byte) (0xFF - color.A));
        }

        public void SetSoundQueueing(int queue)
        {
            UseSoundQueue = queue;
        }

        public void SetConeOfSilence(double radius)
        {
            ISoundModule module = m_parentGroup.Scene.RequestModuleInterface<ISoundModule>();
            //TODO: Save SetConeOfSilence
            if (module != null)
            {
                if (radius != 0)
                    module.AddConeOfSilence(UUID, AbsolutePosition, radius);
                else
                    module.RemoveConeOfSilence(UUID);
            }
        }

        #endregion

        #region Fields

        readonly List<uint> m_lastColliders = new List<uint>();
        [XmlIgnore] public scriptEvents AggregateScriptEvents;
        [XmlIgnore] public bool IgnoreUndoUpdate;
        [XmlIgnore] public bool IsWaitingForFirstSpinUpdatePacket;
        [XmlIgnore] PrimFlags LocalFlags;
        [XmlIgnore] public Vector3 RotationAxis = Vector3.One;
        [XmlIgnore] public Quaternion SpinOldOrientation = Quaternion.Identity;
        [XmlIgnore] public uint TimeStampLastActivity; // Will be used for AutoReturn

        [XmlIgnore]
        public bool Undoing { get; set; }

        UUID _creatorID;

        [XmlIgnore] UUID m_AttachedAvatar;
        [XmlIgnore] Dictionary<int, string> m_CollisionFilter = new Dictionary<int, string>();
        [XmlIgnore] bool m_IsAttachment;
        [XmlIgnore] int[] m_PayPrice = {-2, -2, -2, -2, -2};
        [XmlIgnore] bool m_ValidpartOOB; // control recalculation
        protected Vector3 m_acceleration;
        protected Vector3 m_angularVelocity;
        byte m_clickAction;
        UUID m_collisionSound;
        float m_collisionSoundVolume;
        UUID m_collisionSprite;
        protected uint m_crc;
        string m_creatorData = string.Empty;
        string m_description = String.Empty;
        protected Vector3 m_groupPosition;
        Vector3 m_initialPIDLocation = Vector3.Zero;
        protected SceneObjectPartInventory m_inventory;
        protected Vector3 m_lastAcceleration;
        protected Vector3 m_lastAngularVelocity;
        protected Vector3 m_lastGroupPosition;
        protected Vector3 m_lastPosition;
        protected Quaternion m_lastRotation;
        protected Vector3 m_lastVelocity;
        int m_linkNum;
        protected uint m_localId;

        /// <summary>
        ///     Stores media texture data
        /// </summary>
        protected string m_mediaUrl;

        protected string m_name;
        protected Vector3 m_offsetPosition;

        protected SceneObjectGroup m_parentGroup;
        [XmlIgnore] float m_partBSphereRadiusSQ; // the square of the radius of a sphere containing the oob

        [XmlIgnore] Vector3 m_partOOBoffset;
        // the position center of the bounding box relative to it's Position

        [XmlIgnore] Vector3 m_partOOBsize;
        // the size of a bounding box oriented as prim, is future will consider cutted prims, meshs etc

        protected byte[] m_particleSystem = Utils.EmptyBytes;
        int m_passTouches;

        PhysicsActor m_physActor;
        bool m_pidActive;
        bool m_pidhoverActive;
        protected ulong m_regionHandle;
        [XmlIgnore] int m_scriptAccessPin;
        [XmlIgnore] Dictionary<UUID, scriptEvents> m_scriptEvents = new Dictionary<UUID, scriptEvents>();
        protected PrimitiveBaseShape m_shape;
        string m_sitAnimation = "SIT";
        string m_sitName = String.Empty;
        UUID m_sound;
        string m_text = String.Empty;
        string m_touchName = String.Empty;

        readonly object _undoLock = new object ();
        readonly object _redoLock = new object ();

        UndoStack<UndoState> m_undo = new UndoStack<UndoState>(5);
        UndoStack<UndoState> m_redo = new UndoStack<UndoState> (5);

        protected UUID m_uuid;
        bool m_volumeDetectActive;

        [XmlIgnore]
        [ProtoMember(92)]
        public bool RETURN_AT_EDGE { get; set; }

        [XmlIgnore]
        [ProtoMember(93)]
        public bool BlockGrab { get; set; }

        [XmlIgnore]
        [ProtoMember(94)]
        public bool BlockGrabObject { get; set; }

        [XmlIgnore]
        public bool IsLoading { get; set; }

        [XmlIgnore]
        [ProtoMember(96)]
        public bool StatusSandbox { get; set; }

        [XmlIgnore]
        [ProtoMember(97)]
        public Vector3 StatusSandboxPos { get; set; }

        [XmlIgnore]
        [ProtoMember(98)]
        public int STATUS_ROTATE_X { get; set; }

        [XmlIgnore]
        [ProtoMember(99)]
        public int STATUS_ROTATE_Y { get; set; }

        [XmlIgnore]
        [ProtoMember(100)]
        public int STATUS_ROTATE_Z { get; set; }

        [XmlIgnore]
        public bool ValidpartOOB
        {
            set { m_ValidpartOOB = value; }
        }

        // the size of a bounding box oriented as the prim, is future will consider cutted prims, meshs etc
        [XmlIgnore]
        public Vector3 OOBsize
        {
            get
            {
                if (!m_ValidpartOOB)
                    UpdateOOBfromOOBs();
                return m_partOOBsize;
            }
        }

        // the position center of the bounding box relative to it's Position
        // on complex forms this will not be zero
        [XmlIgnore]
        public Vector3 OOBoffset
        {
            get
            {
                if (!m_ValidpartOOB)
                    UpdateOOBfromOOBs();
                return m_partOOBoffset;
            }
        }

        // the square of the radius of a sphere containing the oobb
        [XmlIgnore]
        public float BSphereRadiusSQ
        {
            get
            {
                if (!m_ValidpartOOB)
                    UpdateOOBfromOOBs();
                return m_partBSphereRadiusSQ;
            }
        }

        [ProtoMember(1)]
        public bool AllowedDrop { get; set; }

        [XmlIgnore]
        [ProtoMember(91)]
        public bool DIE_AT_EDGE { get; set; }

        [XmlIgnore]
        [ProtoMember(90)]
        public int UseSoundQueue { get; set; }

        [ProtoMember(70, OverwriteList = true)]
        public int[] PayPrice
        {
            get { return m_PayPrice; }
            set { m_PayPrice = value; }
        }

        [XmlIgnore]
        public PhysicsActor PhysActor
        {
            get { return m_physActor; }
            set
            {
//                MainConsole.Instance.DebugFormat("[SOP]: PhysActor set to {0} for {1} {2}", value, Name, UUID);
                m_physActor = value;
            }
        }

        [XmlIgnore]
        [ProtoMember(71)]
        public UUID Sound
        {
            get { return m_sound; }
            set { m_sound = value; }
        }

        [XmlIgnore]
        [ProtoMember(72)]
        public byte SoundFlags { get; set; }

        [XmlIgnore]
        [ProtoMember(73)]
        public double SoundGain { get; set; }

        [XmlIgnore]
        [ProtoMember(74)]
        public double SoundRadius { get; set; }

        [XmlIgnore]
        [ProtoMember(75)]
        public Vector3 PIDTarget { get; set; }

        [XmlIgnore]
        [ProtoMember(76)]
        public bool PIDActive
        {
            get { return m_pidActive; }
            set
            {
                IScene s = ParentGroup == null ? null : ParentGroup.Scene ?? null;
                if (s != null)
                {
                    if (value)
                        s.EventManager.OnFrame += UpdateLookAt;
                    else
                        s.EventManager.OnFrame -= UpdateLookAt;
                }
                m_pidActive = value;
            }
        }

        [XmlIgnore]
        [ProtoMember(77)]
        public float PIDTau { get; set; }

        [ProtoMember(78)]
        public float PIDHoverHeight { get; set; }

        [ProtoMember(79)]
        public float PIDHoverTau { get; set; }

        [ProtoMember(80)]
        public bool PIDHoverActive
        {
            get { return m_pidhoverActive; }
            set { m_pidhoverActive = value; }
        }

        [ProtoMember(81)]
        public PIDHoverType PIDHoverType { get; set; }

        [XmlIgnore]
        [ProtoMember(82)]
        public UUID FromUserInventoryItemID { get; set; }

        [XmlIgnore]
        [ProtoMember(83)]
        public UUID FromUserInventoryAssetID { get; set; }

        [XmlIgnore]
        public bool IsAttachment
        {
            get { return m_IsAttachment; }
            set { m_IsAttachment = value; }
        }

        [XmlIgnore]
        public UUID AttachedAvatar
        {
            get { return m_AttachedAvatar; }
            set { m_AttachedAvatar = value; }
        }

        [XmlIgnore]
        public Vector3 AttachedPos { get; set; }

        /// <summary>
        ///     NOTE: THIS WILL NOT BE UP TO DATE AS THEY WILL BE ONE REV BEHIND
        ///     Used to save attachment pos and point over rezzing/taking
        /// </summary>
        [XmlIgnore]
        public int AttachmentPoint { get; set; }

        /// <summary>
        ///     NOTE: THIS WILL NOT BE UP TO DATE AS THEY WILL BE ONE REV BEHIND
        ///     Used to save attachment pos and point over rezzing/taking
        /// </summary>
        [ProtoMember(84)]
        public Vector3 SavedAttachedPos { get; set; }


        [ProtoMember(85)]
        public int SavedAttachmentPoint { get; set; }

        [XmlIgnore]
        [ProtoMember(86)]
        public bool VolumeDetectActive
        {
            get { return m_volumeDetectActive; }
            set { m_volumeDetectActive = value; }
        }

        /// <summary>
        ///     This part's inventory
        /// </summary>
        [XmlIgnore]
        public IEntityInventory Inventory
        {
            get { return m_inventory; }
        }

        [ProtoMember(87)]
        public Vector3 CameraEyeOffset { get; set; }

        [ProtoMember(88)]
        public Vector3 CameraAtOffset { get; set; }

        [ProtoMember(89)]
        public bool ForceMouselook { get; set; }

        [ProtoMember(102)]
        public KeyframeAnimation KeyframeAnimation { get; set; }

        [ProtoMember(103)]
        public Dictionary<UUID, StateSave> StateSaves { get; set; }

        [ProtoMember(104)]
        public OSDArray RenderMaterials { get; set; }

        public Vector3 GroupScale()
        {
            return m_parentGroup.GroupScale();
        }

        #endregion Fields

        #region Constructors

        /// <summary>
        ///     No arg constructor called by region restore db code
        /// </summary>
        public SceneObjectPart()
        {
            SitTargetAvatar = new List<UUID>();
            StateSaves = new Dictionary<UUID, StateSave>();

            m_inventory = new SceneObjectPartInventory(this);
            m_shape = new PrimitiveBaseShape();
            Material = (int) OpenMetaverse.Material.Wood;
        }

        /// <summary>
        ///     Create a completely new SceneObjectPart (prim).  This will need to be added separately to a SceneObjectGroup
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="shape"></param>
        /// <param name="groupPosition"></param>
        /// <param name="rotationOffset"></param>
        /// <param name="offsetPosition"></param>
        /// <param name="name"></param>
        public SceneObjectPart(
            UUID ownerID, PrimitiveBaseShape shape, Vector3 groupPosition,
            Quaternion rotationOffset, Vector3 offsetPosition, string name)
        {
            m_name = name;

            CreationDate = (int) Utils.DateTimeToUnixTime(DateTime.Now);
            _ownerID = ownerID;
            _creatorID = _ownerID;
            LastOwnerID = UUID.Zero;
            UUID = UUID.Random();
            Shape = shape;
            CRC = 1;
            _ownershipCost = 0;
            _flags = 0;
            _groupID = UUID.Zero;
            _objectSaleType = 0;
            _salePrice = 0;
            _category = 0;
            LastOwnerID = _creatorID;
            m_groupPosition = groupPosition;
            m_offsetPosition = offsetPosition;
            RotationOffset = rotationOffset;
            Velocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            Acceleration = Vector3.Zero;
            SitTargetAvatar = new List<UUID>();
            StateSaves = new Dictionary<UUID, StateSave>();

            ValidpartOOB = false;

            // Prims currently only contain a single folder (Contents).  From looking at the Second Life protocol,
            // this appears to have the same UUID (!) as the prim.  If this isn't the case, one can't drag items from
            // the prim into an agent inventory (Linden client reports that the "Object not found for drop" in its log

            Flags = 0;
            CreateSelected = true;

            TrimPermissions();
            //m_undo = new UndoStack<UndoState>(ParentGroup.GetSceneMaxUndo());

            m_inventory = new SceneObjectPartInventory(this);
            Material = (int) OpenMetaverse.Material.Wood;
        }

        public override int GetHashCode()
        {
            return UUID.GetHashCode();
        }

        #endregion Constructors

        #region XML Schema

        uint _baseMask = (uint) PermissionMask.All;
        uint _category;
        uint _everyoneMask = (uint) PermissionMask.None;
        PrimFlags _flags = PrimFlags.None;
        UUID _groupID;
        uint _groupMask = (uint) PermissionMask.None;
        uint _nextOwnerMask = (uint) PermissionMask.All;
        byte _objectSaleType;
        UUID _ownerID;
        uint _ownerMask = (uint) PermissionMask.All;
        int _ownershipCost;
        uint _parentID;
        int _salePrice;
        byte[] m_ParticleSystem;
        string m_currentMediaVersion = "x-mv:0000000001/00000000-0000-0000-0000-000000000000";
        int m_passCollision;
        byte[] m_textureAnimation;

        [XmlIgnore]
        public string CurrentMediaVersion
        {
            get { return m_currentMediaVersion; }
            set { m_currentMediaVersion = value; }
        }

        /// <summary>
        ///     Used by the DB layer to retrieve / store the entire user identification.
        ///     The identification can either be a simple UUID or a string of the form
        ///     uuid[;profile_url[;name]]
        /// </summary>
        public string CreatorIdentification
        {
            get
            {
                if (!string.IsNullOrEmpty(m_creatorData))
                    return _creatorID.ToString() + ';' + m_creatorData;
                
                return _creatorID.ToString();
            }
            set
            {
                if ((value == null) || (value != null && value == string.Empty))
                {
                    m_creatorData = string.Empty;
                    return;
                }

                if (!value.Contains(";")) // plain UUID
                {
                    UUID uuid = UUID.Zero;
                    UUID.TryParse(value, out uuid);
                    _creatorID = uuid;
                }
                else // <uuid>[;<endpoint>[;name]]
                {
                    string name = "Unknown User";
                    string[] parts = value.Split(';');
                    if (parts.Length >= 1)
                    {
                        UUID uuid = UUID.Zero;
                        UUID.TryParse(parts[0], out uuid);
                        _creatorID = uuid;
                    }
                    if (parts.Length >= 2)
                        m_creatorData = parts[1];
                    if (parts.Length >= 3)
                        name = parts[2];

                    m_creatorData += ';' + name;
                }
            }
        }

        /// <summary>
        ///     This is identical to the Flags property, except that the returned value is uint rather than PrimFlags
        /// </summary>
        [Obsolete("Use Flags property instead")]
        public uint ObjectFlags
        {
            get { return (uint) Flags; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                Flags = (PrimFlags) value;
            }
        }

        Quaternion m_APIDTarget;

        [XmlIgnore, ProtoMember(65)]
        public Quaternion APIDTarget
        {
            get { return m_APIDTarget; }
            set
            {
                IScene s = ParentGroup == null ? null : ParentGroup.Scene ?? null;
                if (s != null)
                {
                    if (value != Quaternion.Identity)
                        s.EventManager.OnFrame += UpdateLookAt;
                    else
                        s.EventManager.OnFrame -= UpdateLookAt;
                }
                m_APIDTarget = value;
            }
        }

        [XmlIgnore, ProtoMember(66)]
        public float APIDDamp { get; set; }

        [XmlIgnore, ProtoMember(67)]
        public float APIDStrength { get; set; }

        [ProtoMember(68)]
        public int APIDIterations { get; set; }

        [ProtoMember(69)]
        public bool APIDEnabled { get; set; }

        [XmlIgnore, ProtoMember(64)]
        public float Damage { get; set; }

        public Vector3 RelativePosition
        {
            get
            {
                if (IsRoot)
                {
                    if (IsAttachment)
                        return OffsetPosition;
                    
                    return AbsolutePosition;
                }
               
                return OffsetPosition;
            }
        }

        [ProtoMember(2)]
        public UUID CreatorID
        {
            get { return _creatorID; }
            set { _creatorID = value; }
        }

        /// <summary>
        ///     Data about the creator in the form profile_url;name
        /// </summary>
        [ProtoMember(3)]
        public string CreatorData
        {
            get { return m_creatorData; }
            set { m_creatorData = value; }
        }

        /// <value>
        ///     Access should be via Inventory directly - this property temporarily remains for xml serialization purposes
        /// </value>
        [ProtoMember(4)]
        public uint InventorySerial
        {
            get { return m_inventory.Serial; }
            set { m_inventory.Serial = value; }
        }

        /// <value>
        ///     Access should be via Inventory directly - this property temporarily remains for xml serialization purposes
        /// </value>
        [ProtoMember(5)]
        public TaskInventoryDictionary TaskInventory
        {
            get { return m_inventory.Items; }
            set { m_inventory.Items = value; }
        }

        [ProtoMember(6)]
        public UUID UUID
        {
            get { return m_uuid; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_uuid = value;

                // This is necessary so that TaskInventoryItem parent ids correctly reference the new uuid of this part
                if (ParentGroup != null && Inventory != null)
                    Inventory.ResetObjectID();
            }
        }

        [ProtoMember(7)]
        public uint LocalId
        {
            get { return m_localId; }
            set { m_localId = value; }
        }

        [XmlIgnore]
        public uint CRC { get; set; }

        [ProtoMember(8)]
        public virtual string Name
        {
            get { return m_name; }
            set
            {
                if (m_name != value)
                {
                    if (ParentGroup != null)
                        ParentGroup.HasGroupChanged = true;
                    m_name = value;
                }
            }
        }

        [ProtoMember(9)]
        public int Material { get; set; }

        public void UpdateMaterial(int val)
        {
            if (Material == val)
                return;

            if (ParentGroup != null)
                ParentGroup.HasGroupChanged = true;

            Material = val;
            switch (val)
            {
                case (int)OpenMetaverse.Material.Flesh:
                    Friction = 0.9f;
                    Restitution = 0.3f;
                    break;
                case (int)OpenMetaverse.Material.Glass:
                    Friction = 0.2f;
                    Restitution = 0.7f;
                    break;
                case (int)OpenMetaverse.Material.Metal:
                    Friction = 0.3f;
                    Restitution = 0.4f;
                    break;
                case (int)OpenMetaverse.Material.Plastic:
                    Friction = 0.4f;
                    Restitution = 0.7f;
                    break;
                case (int)OpenMetaverse.Material.Rubber:
                    Friction = 0.9f;
                    Restitution = 0.9f;
                    break;
                case (int)OpenMetaverse.Material.Stone:
                    Friction = 0.8f;
                    Restitution = 0.4f;
                    break;
                case (int)OpenMetaverse.Material.Wood:
                    Friction = 0.6f;
                    Restitution = 0.5f;
                    break;
                default:
                    //?????
                    break;
            }
            if (PhysActor != null)
                PhysActor.SetMaterial(val, Friction, Restitution, GravityMultiplier, Density);
        }

        [ProtoMember(10)]
        public int PassTouch
        {
            get { return m_passTouches; }
            set
            {
                m_passTouches = value;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
            }
        }

        [ProtoMember(11)]
        public int PassCollisions
        {
            get { return m_passCollision; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_passCollision = value;
            }
        }


        [XmlIgnore, ProtoMember(12)]
        public Dictionary<int, string> CollisionFilter
        {
            get { return m_CollisionFilter; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_CollisionFilter = value;
            }
        }

        [ProtoMember(13)]
        public int ScriptAccessPin
        {
            get { return m_scriptAccessPin; }
            set { m_scriptAccessPin = value; }
        }

        [ProtoMember(60, OverwriteList = true)]
        public byte[] TextureAnimation
        {
            get { return m_textureAnimation; }
            set { m_textureAnimation = value; }
        }

        [XmlIgnore]
        [ProtoMember(61, OverwriteList = true)]
        public byte[] ParticleSystem
        {
            get { return m_ParticleSystem; }
            set { m_ParticleSystem = value; }
        }

        [XmlIgnore, ProtoMember(62)]
        public DateTime Expires { get; set; }

        [XmlIgnore, ProtoMember(63)]
        public DateTime Rezzed { get; set; }

        /// <summary>
        ///     The position of the entire group that this prim belongs to.
        /// </summary>
        [ProtoMember(14)]
        public Vector3 GroupPosition
        {
            get { return GetGroupPosition(); }
            set { FixGroupPosition(value, false); }
        }

        public Vector3 GetGroupPosition()
        {
            // If this is a linkset, we don't want the physics engine mucking up our group position here.
            PhysicsActor actor = PhysActor;
            if (actor != null && _parentID == 0)
            {
                m_groupPosition = actor.Position;
            }

            if (IsAttachment)
            {
                IScenePresence sp = m_parentGroup.Scene.GetScenePresence(AttachedAvatar);
                if (sp != null)
                    return sp.AbsolutePosition;
            }

            return m_groupPosition;
        }

        [ProtoMember(15)]
        public Vector3 OffsetPosition
        {
            get { return m_offsetPosition; }
            set
            {
                m_offsetPosition = value;
                ValidpartOOB = false;
            }
        }

        [ProtoMember(16)]
        public Quaternion RotationOffset { get; set; }

        public Quaternion GetRotationOffset()
        {
            PhysicsActor actor = m_physActor;
            if (_parentID == 0 && (m_shape.PCode != 9 || m_shape.State == 0) && actor != null)
            {
                if (actor.Orientation.X != 0f || actor.Orientation.Y != 0f
                    || actor.Orientation.Z != 0f || actor.Orientation.W != 0f)
                {
                    RotationOffset = actor.Orientation;
                }
            }
            return RotationOffset;
        }

        Vector3 m_tempVelocity = Vector3.Zero;

        /// <summary>
        /// </summary>
        [ProtoMember(17)]
        public Vector3 Velocity
        {
            get
            {
                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    if (actor.IsPhysical)
                    {
                        return actor.Velocity;
                    }
                }

                return m_tempVelocity;
            }

            set
            {
                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    if (actor.IsPhysical)
                    {
                        actor.Velocity = value;
                        m_tempVelocity = Vector3.Zero;
                    }
                    else
                        m_tempVelocity = value;
                }
                else
                    m_tempVelocity = value;
            }
        }

        /// <summary>
        /// </summary>
        [ProtoMember(18)]
        public Vector3 AngularVelocity
        {
            get
            {
                PhysicsActor actor = PhysActor;
                if ((actor != null) && actor.IsPhysical)
                {
                    m_angularVelocity = actor.RotationalVelocity;
                }
                return m_angularVelocity;
            }
            set 
            { 
                m_angularVelocity = value;
                PhysicsActor actor = PhysActor;
                if ((actor != null) && actor.IsPhysical)
                    actor.RotationalVelocity = m_angularVelocity;
            }
        }

        public void GenerateRotationalVelocityFromOmega()
        {
            if (OmegaGain == 0.0f) //Disable spin
                AngularVelocity = Vector3.Zero;
            else
                AngularVelocity = new Vector3((float) (OmegaAxis.X*OmegaSpinRate),
                                              (float) (OmegaAxis.Y*OmegaSpinRate),
                                              (float) (OmegaAxis.Z*OmegaSpinRate));
        }

        /// <summary>
        /// </summary>
        [ProtoMember(19)]
        public Vector3 Acceleration
        {
            get { return m_acceleration; }
            set { m_acceleration = value; }
        }

        [ProtoMember(20)]
        public string Description
        {
            get { return m_description; }
            set
            {
                if (m_description == value)
                    return;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_description = value;
            }
        }

        /// <value>
        ///     Text color.
        /// </value>
        [XmlIgnore]
        [ProtoMember(21)]
        public Color Color { get; set; }

        public void UpdateColor(Color c, bool triggerChangedColor)
        {
            if (c.A != Color.A ||
                c.B != Color.B ||
                c.G != Color.G ||
                c.R != Color.R)
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                Color = c;
                if (triggerChangedColor)
                    TriggerScriptChangedEvent(Changed.COLOR);

                /* ScheduleFullUpdate() need not be called b/c after
                 * setting the color, the text will be set, so then
                 * ScheduleFullUpdate() will be called. */
                //ScheduleFullUpdate();
            }
        }

        [ProtoMember(22)]
        public string Text
        {
            get
            {
                string returnstr = m_text;
                if (returnstr.Length > 255)
                {
                    returnstr = returnstr.Substring(0, 254);
                }
                return returnstr;
            }
            set
            {
                if (m_text == value)
                    return;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_text = value;
            }
        }


        [ProtoMember(23)]
        public string SitName
        {
            get { return m_sitName; }
            set
            {
                if (m_sitName == value)
                    return;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_sitName = value;
            }
        }

        [ProtoMember(24)]
        public string TouchName
        {
            get { return m_touchName; }
            set
            {
                if (m_touchName == value)
                    return;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_touchName = value;
            }
        }

        [ProtoMember(25)]
        public int LinkNum
        {
            get { return m_linkNum; }
            set
            {
                if (m_linkNum == value)
                    return;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_linkNum = value;
            }
        }

        [ProtoMember(26)]
        public byte ClickAction
        {
            get { return m_clickAction; }
            set
            {
                if (m_clickAction == value)
                    return;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_clickAction = value;
            }
        }

        [ProtoMember(27)]
        public PrimitiveBaseShape Shape
        {
            get { return m_shape; }
            set
            {
                ValidpartOOB = false;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                bool shape_changed = false;
                // TODO: this should really be restricted to the right
                // set of attributes on shape change.  For instance,
                // changing the lighting on a shape shouldn't cause
                // this.
                if (m_shape != null)
                    shape_changed = true;

                m_shape = value;

                if (shape_changed)
                    TriggerScriptChangedEvent(Changed.SHAPE);
            }
        }

        public Vector3 Scale
        {
            get { return m_shape.Scale; }
            set
            {
                ValidpartOOB = false;
                if (m_shape != null)
                {
                    if (m_shape.Scale != value)
                    {
                        StoreUndoState();
                        if (ParentGroup != null)
                            ParentGroup.HasGroupChanged = true;

                        m_shape.Scale = value;

                        PhysicsActor actor = PhysActor;
                        if (actor != null && m_parentGroup != null &&
                            m_parentGroup.Scene != null &&
                            m_parentGroup.Scene.PhysicsScene != null)
                            actor.Size = m_shape.Scale;
                        TriggerScriptChangedEvent(Changed.SCALE);
                    }
                }
            }
        }

        /// <summary>
        ///     Used for media on a prim.
        /// </summary>
        /// Do not change this value directly - always do it through an IMoapModule.
        [ProtoMember(29)]
        public string MediaUrl
        {
            get { return m_mediaUrl; }

            set
            {
                if (m_mediaUrl == value)
                    return;
                m_mediaUrl = value;

                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
            }
        }

        [XmlIgnore]
        public bool CreateSelected { get; set; }

        #endregion

        #region Public Properties with only Get

        UUID _parentUUID = UUID.Zero;
        float m_friction;
        float m_density;
        float m_gravityMultiplier;
        float m_restitution;

        public SceneObjectGroup ParentGroup
        {
            get { return m_parentGroup; }
        }

        public scriptEvents ScriptEvents
        {
            get { return AggregateScriptEvents; }
        }

        public bool Stopped
        {
            get
            {
                double threshold = 0.02;
                return (Math.Abs(Velocity.X) < threshold &&
                        Math.Abs(Velocity.Y) < threshold &&
                        Math.Abs(Velocity.Z) < threshold &&
                        Math.Abs(AngularVelocity.X) < threshold &&
                        Math.Abs(AngularVelocity.Y) < threshold &&
                        Math.Abs(AngularVelocity.Z) < threshold);
            }
        }

        [ProtoMember(39)]
        public uint Category
        {
            get { return _category; }
            set { _category = value; }
        }

        [ProtoMember(40)]
        public int OwnershipCost
        {
            get { return _ownershipCost; }
            set { _ownershipCost = value; }
        }

        [XmlIgnore]
        public virtual UUID RegionID
        {
            get
            {
                if (ParentGroup != null && ParentGroup.Scene != null)
                    return ParentGroup.Scene.RegionInfo.RegionID;
                
                return UUID.Zero;
            }
            set { } // read only
        }

        public Vector3 AbsolutePosition
        {
            get
            {
                if (IsAttachment)
                    return GroupPosition;

                return GetWorldPosition();
            }
            set { }
        }

        public ISceneEntity ParentEntity
        {
            get { return m_parentGroup; }
        }

        [ProtoMember(30)]
        public Quaternion SitTargetOrientation { get; set; }

        [ProtoMember(31)]
        public Vector3 SitTargetPosition { get; set; }

        [ProtoMember(32)]
        public Vector3 OmegaAxis { get; set; }

        [ProtoMember(33)]
        public double OmegaSpinRate { get; set; }

        [ProtoMember(34)]
        public double OmegaGain { get; set; }

        [ProtoMember(35)]
        public uint ParentID
        {
            get { return _parentID; }
            set { _parentID = value; }
        }

        [ProtoMember(36)]
        public int CreationDate { get; set; }

        [ProtoMember(37)]
        public int SalePrice
        {
            get { return _salePrice; }
            set { _salePrice = value; }
        }

        [ProtoMember(38)]
        public byte ObjectSaleType
        {
            get { return _objectSaleType; }
            set { _objectSaleType = value; }
        }

        [ProtoMember(43)]
        public UUID GroupID
        {
            get { return _groupID; }
            set { _groupID = value; }
        }

        [ProtoMember(44)]
        public UUID OwnerID
        {
            get { return _ownerID; }
            set { _ownerID = value; }
        }

        [ProtoMember(45)]
        public UUID LastOwnerID { get; set; }

        [ProtoMember(46)]
        public uint BaseMask
        {
            get { return _baseMask; }
            set { _baseMask = value; }
        }

        [ProtoMember(101)]
        public uint OwnerMask
        {
            get { return _ownerMask; }
            set { _ownerMask = value; }
        }

        [ProtoMember(47)]
        public uint GroupMask
        {
            get { return _groupMask; }
            set { _groupMask = value; }
        }

        [ProtoMember(48)]
        public uint EveryoneMask
        {
            get { return _everyoneMask; }
            set { _everyoneMask = value; }
        }

        [ProtoMember(49)]
        public uint NextOwnerMask
        {
            get { return _nextOwnerMask; }
            set { _nextOwnerMask = value; }
        }

        [ProtoMember(50)]
        public byte PhysicsType { get; set; }

        [ProtoMember(51)]
        public float Density
        {
            get
            {
                if (m_density != 0)
                    return m_density;
                return (m_density = 1000f);
            }
            set { m_density = value; }
        }

        [ProtoMember(52)]
        public float Friction
        {
            get
            {
                if (m_friction != 0)
                    return m_friction;
                return (m_friction = 0.6f);
            }
            set { m_friction = value; }
        }

        [ProtoMember(53)]
        public float Restitution
        {
            get
            {
                if (m_restitution != 0)
                    return m_restitution;
                return (m_restitution = 0.5f);
            }
            set { m_restitution = value; }
        }

        [ProtoMember(54)]
        public float GravityMultiplier
        {
            get
            {
                if (m_gravityMultiplier != 0)
                    return m_gravityMultiplier;
                return (m_gravityMultiplier = 1);
            }
            set { m_gravityMultiplier = value; }
        }

        /// <summary>
        ///     Property flags.  See OpenMetaverse.PrimFlags
        /// </summary>
        /// Example properties are PrimFlags.Phantom and PrimFlags.DieAtEdge
        [ProtoMember(55)]
        public PrimFlags Flags
        {
            get { return _flags; }
            set
            {
//                MainConsole.Instance.DebugFormat("[SOP]: Setting flags for {0} {1} to {2}", UUID, Name, value);
                //if (ParentGroup != null && _flags != value)
                //    ParentGroup.HasGroupChanged = true;
                _flags = value;
            }
        }

        [XmlIgnore]
        public List<UUID> SitTargetAvatar { get; set; }

        [XmlIgnore]
        public UUID ParentUUID
        {
            get
            {
                if (ParentGroup != null)
                    _parentUUID = ParentGroup.UUID;
                return _parentUUID;
            }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                _parentUUID = value;
            }
        }

        [XmlIgnore]
        [ProtoMember(56)]
        public string SitAnimation
        {
            get { return m_sitAnimation; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_sitAnimation = value;
            }
        }

        [ProtoMember(57)]
        public UUID CollisionSound
        {
            get { return m_collisionSound; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_collisionSound = value;
                //Why?
                //aggregateScriptEvents();
            }
        }

        [ProtoMember(58)]
        public UUID CollisionSprite
        {
            get { return m_collisionSprite; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_collisionSprite = value;
            }
        }

        [ProtoMember(59)]
        public float CollisionSoundVolume
        {
            get { return m_collisionSoundVolume; }
            set
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                m_collisionSoundVolume = value;
            }
        }

        #endregion Public Properties with only Get

        #region Methods

        public void FinishedSerializingGenericProperties()
        {
            if ((APIDEnabled || PIDActive) && ParentEntity != null) //Make sure to activate it
                ParentEntity.Scene.EventManager.OnFrame += UpdateLookAt;
        }

        void UpdateOOBfromOOBs()
        {
            m_partOOBoffset = Vector3.Zero;

            Vector3 ts = Scale;

            m_partOOBsize.X = ts.X*0.5f;
            m_partOOBsize.Y = ts.Y*0.5f;
            m_partOOBsize.Z = ts.Z*0.5f;

            m_partBSphereRadiusSQ = m_partOOBsize.LengthSquared();
            ValidpartOOB = true;
        }

        // distance from aabb to point ( untested)
        public float AABdistanceToSQ(Vector3 target)
        {
            // distance to group in world
            Vector3 vtmp = target - m_groupPosition; // assume this updated

            if (ParentID != 0)
            {
                // rotate into group reference         
                vtmp *= Quaternion.Inverse(ParentGroup.GroupRotation);
                // move into offseted local ref
                vtmp -= m_offsetPosition;
            }

            // rotate into local reference ( part or grp )
            vtmp *= Quaternion.Inverse(GetRotationOffset());

            // now oob pos
            vtmp -= OOBoffset; // force update

            Vector3 box = OOBsize;

            // hack distance to box: inside == 0
            if (vtmp.X > 0)
            {
                vtmp.X -= box.X;
                if (vtmp.X < 0.0)
                    vtmp.X = 0.0f;
            }
            else
            {
                vtmp.X += box.X;
                if (vtmp.X > 0.0)
                    vtmp.X = 0.0f;
            }

            if (vtmp.Y > 0)
            {
                vtmp.Y -= box.Y;
                if (vtmp.Y < 0.0)
                    vtmp.Y = 0.0f;
            }
            else
            {
                vtmp.Y += box.Y;
                if (vtmp.Y > 0.0)
                    vtmp.Y = 0.0f;
            }

            if (vtmp.Z > 0)
            {
                vtmp.Z -= box.Z;
                if (vtmp.Z < 0.0)
                    vtmp.Z = 0.0f;
            }
            else
            {
                vtmp.Z += box.Z;
                if (vtmp.Z > 0.0)
                    vtmp.Z = 0.0f;
            }

            return vtmp.LengthSquared();
        }

        // distance from aabb to point ( untested)
        // if smaller than simplied distance to group returns that one
        // hack to send root prims first

        public float clampedAABdistanceToSQ(Vector3 target)
        {
            float grpdSQ = 0;
            // distance to group in world
            Vector3 vtmp = target - m_groupPosition; // assume this updated


            if (ParentID != 0)
            {
                // rotate into group reference         
                vtmp *= Quaternion.Inverse(ParentGroup.GroupRotation);
                // compute distance to grp oob
                Vector3 grpv = vtmp - ParentGroup.OOBoffset;
                grpdSQ = grpv.LengthSquared() - ParentGroup.BSphereRadiusSQ;
                if (grpdSQ < 0)
                    grpdSQ = 0;

                // back
                // move into offseted local ref
                vtmp -= m_offsetPosition;
            }

            // rotate into local reference
            vtmp *= Quaternion.Inverse(GetRotationOffset());
            // now oob pos
            vtmp -= OOBoffset; // force update

            Vector3 box = OOBsize;

            // hack distance to box: inside == 0
            if (vtmp.X > 0)
            {
                vtmp.X -= box.X;
                if (vtmp.X < 0.0)
                    vtmp.X = 0.0f;
            }
            else
            {
                vtmp.X += box.X;
                if (vtmp.X > 0.0)
                    vtmp.X = 0.0f;
            }

            if (vtmp.Y > 0)
            {
                vtmp.Y -= box.Y;
                if (vtmp.Y < 0.0)
                    vtmp.Y = 0.0f;
            }
            else
            {
                vtmp.Y += box.Y;
                if (vtmp.Y > 0.0)
                    vtmp.Y = 0.0f;
            }

            if (vtmp.Z > 0)
            {
                vtmp.Z -= box.Z;
                if (vtmp.Z < 0.0)
                    vtmp.Z = 0.0f;
            }
            else
            {
                vtmp.Z += box.Z;
                if (vtmp.Z > 0.0)
                    vtmp.Z = 0.0f;
            }

            float distSQ = vtmp.LengthSquared();

            if (ParentID != 0 && distSQ < grpdSQ)
                return grpdSQ;
            else
                return distSQ;
        }


        uint ApplyMask(uint val, bool set, uint mask)
        {
            if (set)
            {
                return val |= mask;
            }
            else
            {
                return val &= ~mask;
            }
        }

        void SendObjectPropertiesToClient(UUID AgentID)
        {
            IScenePresence SP = ParentGroup.Scene.GetScenePresence(AgentID);
            if (SP != null)
                m_parentGroup.GetProperties(SP.ControllingClient);
        }

        #endregion Methods

        #region Public Methods

        internal bool m_hasSubscribedToCollisionEvent;

        public void FixOffsetPosition(Vector3 value, bool single)
        {
            bool triggerMoving_End = false;
            if (m_offsetPosition != value)
            {
                triggerMoving_End = true;
                TriggerScriptMovingStartEvent();
            }
            StoreUndoState();
            m_offsetPosition = value;
            if (ParentEntity != null)
                ParentEntity.ScheduleGroupTerseUpdate();
            ValidpartOOB = false;

            if (ParentGroup != null && !ParentGroup.IsDeleted)
            {
                ParentGroup.HasGroupChanged = true;
                PhysicsActor actor = PhysActor;
                if (_parentID != 0 && actor != null && (single || !actor.IsPhysical))
                {
                    actor.Position = GetWorldPosition();
                    actor.Orientation = GetWorldRotation();
                }
            }
            if (triggerMoving_End)
                TriggerScriptMovingEndEvent();
        }


        public bool AddFlag(PrimFlags flag)
        {
            // PrimFlags prevflag = Flags;
            if ((Flags & flag) == 0)
            {
                //MainConsole.Instance.Debug("Adding flag: " + ((PrimFlags) flag).ToString());
                Flags |= flag;

                if (flag == PrimFlags.TemporaryOnRez)
                    ResetExpire();

                object[] o = new object[2];
                o[0] = this;
                o[1] = flag;
                m_parentGroup.Scene.WhiteCoreEventManager.FireGenericEventHandler("ObjectAddedFlag", o);
                return true;
            }
            return false;
            // MainConsole.Instance.Debug("Aprev: " + prevflag.ToString() + " curr: " + Flags.ToString());
        }

        public void AddNewParticleSystem(Primitive.ParticleSystem pSystem)
        {
            ParticleSystem = pSystem.GetBytes();
        }

        public void RemoveParticleSystem()
        {
            ParticleSystem = Utils.EmptyBytes;
        }

        public void AddTextureAnimation(Primitive.TextureAnimation pTexAnim)
        {
            byte[] data;

            if (pTexAnim.Flags == Primitive.TextureAnimMode.ANIM_OFF)
            {
                data = Utils.EmptyBytes;
            }
            else
            {
                data = new byte[16];
                int pos = 0;

                // The flags don't like conversion from uint to byte, so we have to do
                // it the crappy way.  See the above function :(

                data[pos] = ConvertScriptUintToByte((uint)pTexAnim.Flags); pos++;
                data[pos] = (byte)pTexAnim.Face; pos++;
                data[pos] = (byte)pTexAnim.SizeX; pos++;
                data[pos] = (byte)pTexAnim.SizeY; pos++;

                Utils.FloatToBytes(pTexAnim.Start).CopyTo(data, pos);
                Utils.FloatToBytes(pTexAnim.Length).CopyTo(data, pos + 4);
                Utils.FloatToBytes(pTexAnim.Rate).CopyTo(data, pos + 8);
            }

            TextureAnimation = data;
        }

        public void AdjustSoundGain(double volume)
        {
            if (volume > 1)
                volume = 1;
            if (volume < 0)
                volume = 0;

            m_parentGroup.Scene.ForEachScenePresence(delegate(IScenePresence sp)
                                                         {
                                                             if (!sp.IsChildAgent)
                                                                 sp.ControllingClient.SendAttachedSoundGainChange(UUID,
                                                                                                                  (float
                                                                                                                  )
                                                                                                                  volume);
                                                         });
        }

        /// <summary>
        ///     hook to the physics scene to apply impulse
        ///     This is sent up to the group, which then finds the root prim
        ///     and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>
        public void ApplyImpulse(Vector3 impulsei, bool localGlobalTF)
        {
            Vector3 impulse = impulsei;

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei*AXgrot;
                impulse = newimpulse;
            }

            if (m_parentGroup != null)
            {
                m_parentGroup.applyImpulse(impulse);
            }
        }

        /// <summary>
        ///     hook to the physics scene to apply angular impulse
        ///     This is sent up to the group, which then finds the root prim
        ///     and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>
        public void ApplyAngularImpulse(Vector3 impulsei, bool localGlobalTF)
        {
            Vector3 impulse = impulsei;

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei*AXgrot;
                impulse = newimpulse;
            }

            if (m_parentGroup != null)
            {
                m_parentGroup.applyAngularImpulse(impulse);
            }
        }

        /// <summary>
        ///     hook to the physics scene to apply angular impulse
        ///     This is sent up to the group, which then finds the root prim
        ///     and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>
        public void SetAngularImpulse(Vector3 impulsei, bool localGlobalTF)
        {
            Vector3 impulse = impulsei;

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei*AXgrot;
                impulse = newimpulse;
            }

            if (m_parentGroup != null)
            {
                m_parentGroup.setAngularImpulse(impulse);
            }
        }

        public bool GetDieAtEdge()
        {
            if (m_parentGroup == null)
                return false;
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.DIE_AT_EDGE;
        }

        public bool GetReturnAtEdge()
        {
            if (m_parentGroup == null)
                return false;
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.RETURN_AT_EDGE;
        }

        public void SetReturnAtEdge(bool p)
        {
            if (m_parentGroup == null)
                return;
            if (m_parentGroup.IsDeleted)
                return;

            m_parentGroup.RootPart.RETURN_AT_EDGE = p;
        }

        public bool GetBlockGrab(bool wholeObjectBlock)
        {
            if (m_parentGroup == null)
                return false;
            if (m_parentGroup.IsDeleted)
                return false;

            if (wholeObjectBlock)
                return m_parentGroup.RootPart.BlockGrabObject;
            else
                return m_parentGroup.RootPart.BlockGrab;
        }

        public void SetBlockGrab(bool block, bool wholeObjectBlock)
        {
            if (m_parentGroup == null)
                return;
            if (m_parentGroup.IsDeleted)
                return;

            if (wholeObjectBlock)
                m_parentGroup.RootPart.BlockGrabObject = block;
            else
                m_parentGroup.RootPart.BlockGrab = block;
        }

        public void SetStatusSandbox(bool p)
        {
            if (m_parentGroup == null)
                return;
            if (m_parentGroup.IsDeleted)
                return;
            StatusSandboxPos = m_parentGroup.RootPart.AbsolutePosition;
            m_parentGroup.RootPart.StatusSandbox = p;
        }

        public bool GetStatusSandbox()
        {
            if (m_parentGroup == null)
                return false;
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.StatusSandbox;
        }

        public int GetAxisRotation(int axis)
        {
            //Cannot use ScriptBaseClass constants as no referance to it currently.
            if (axis == 2) //STATUS_ROTATE_X
                return STATUS_ROTATE_X;
            if (axis == 4) //STATUS_ROTATE_Y
                return STATUS_ROTATE_Y;
            if (axis == 8) //STATUS_ROTATE_Z
                return STATUS_ROTATE_Z;

            return 0;
        }

        public uint GetEffectiveObjectFlags()
        {
            // Commenting this section of code out since it doesn't actually do anything, as enums are handled by 
            // value rather than reference
//            PrimFlags f = _flags;
//            if (m_parentGroup == null || m_parentGroup.RootPart == this)
//                f &= ~(PrimFlags.Touch | PrimFlags.Money);

            return (uint) Flags | (uint) LocalFlags;
        }

        public Vector3 GetGeometricCenter()
        {
            if (PhysActor != null)
            {
                return new Vector3(PhysActor.CenterOfMass.X, PhysActor.CenterOfMass.Y, PhysActor.CenterOfMass.Z);
            }
            else
            {
                return new Vector3(0, 0, 0);
            }
        }

        public float GetMass()
        {
            if (ParentGroup.RootPart.UUID == UUID)
            {
                if (PhysActor != null)
                {
                    return PhysActor.Mass;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return ParentGroup.RootPart.GetMass();
            }
        }

        public Vector3 GetForce()
        {
            if (PhysActor != null)
                return PhysActor.Force;

            return Vector3.Zero;
        }

        public void GetProperties(IClientAPI client)
        {
            IScenePresence sp;
            if (ParentGroup != null && ParentGroup.Scene != null &&
                (sp = ParentGroup.Scene.GetScenePresence(client.AgentId)) != null)
                sp.SceneViewer.QueuePartsForPropertiesUpdate(new ISceneChildEntity[1] {this});
        }

        /// <summary>
        ///     Method for a prim to get it's world position from the group.
        ///     Remember, the Group Position simply gives the position of the group itself
        /// </summary>
        /// <returns>A Linked Child Prim objects position in world</returns>
        public Vector3 GetWorldPosition()
        {
            return IsRoot ? GroupPosition : GroupPosition + OffsetPosition*ParentGroup.RootPart.GetRotationOffset();
        }

        /// <summary>
        ///     Gets the rotation of this prim offset by the group rotation
        /// </summary>
        /// <returns></returns>
        public Quaternion GetWorldRotation()
        {
            Quaternion newRot = GetRotationOffset();

            if (_parentID != 0)
            {
                Quaternion parentRot = ParentGroup.RootPart.GetRotationOffset();
                newRot = parentRot*newRot;
            }

            return newRot;
        }

        public void MoveToTarget(Vector3 target, float tau)
        {
            if (tau > 0)
            {
                m_parentGroup.moveToTarget(target, tau);
            }
            else
            {
                StopMoveToTarget();
            }
        }

        /// <summary>
        ///     Uses a PID to attempt to clamp the object on the Z axis at the given height over tau seconds.
        /// </summary>
        /// <param name="height">Height to hover.  Height of zero disables hover.</param>
        /// <param name="hoverType">Determines what the height is relative to </param>
        /// <param name="tau">Number of seconds over which to reach target</param>
        public void SetHoverHeight(float height, PIDHoverType hoverType, float tau)
        {
            m_parentGroup.SetHoverHeight(height, hoverType, tau);
        }

        public void PreloadSound(string sound)
        {
            // UUID ownerID = OwnerID;
            UUID objectID = ParentGroup.RootPart.UUID;
            UUID soundID = UUID.Zero;

            if (!UUID.TryParse(sound, out soundID))
            {
                //Trys to fetch sound id from prim's inventory.
                //Prim's inventory doesn't support non script items yet

                lock (TaskInventory)
                {
                    foreach (
                        KeyValuePair<UUID, TaskInventoryItem> item in
                            TaskInventory.Where(item => item.Value.Name == sound))
                    {
                        soundID = item.Value.ItemID;
                        break;
                    }
                }
            }

            m_parentGroup.Scene.ForEachScenePresence(delegate(IScenePresence sp)
                                                         {
                                                             if (sp.IsChildAgent)
                                                                 return;
                                                             if (
                                                                 !(Util.GetDistanceTo(sp.AbsolutePosition,
                                                                                      AbsolutePosition) >= 100))
                                                                 sp.ControllingClient.SendPreLoadSound(objectID,
                                                                                                       objectID, soundID);
                                                         });
        }

        public bool RemFlag(PrimFlags flag)
        {
            // PrimFlags prevflag = Flags;
            if ((Flags & flag) != 0)
            {
                //MainConsole.Instance.Debug("Removing flag: " + ((PrimFlags)flag).ToString());
                Flags &= ~flag;
                object[] o = new object[2];
                o[0] = this;
                o[1] = flag;
                m_parentGroup.Scene.WhiteCoreEventManager.FireGenericEventHandler("ObjectRemovedFlag", o);
                return true;
            }
            return false;
            //MainConsole.Instance.Debug("prev: " + prevflag.ToString() + " curr: " + Flags.ToString());
            //ScheduleFullUpdate();
        }

        public void ResetEntityIDs()
        {
            UUID = UUID.Random();
            //LinkNum = linkNum;
            Inventory.ResetInventoryIDs(false);
            LocalId = ParentGroup.Scene.SceneGraph.AllocateLocalId();

            //Fix the localID now for the physics engine
            if (m_physActor != null)
            {
                m_physActor.LocalID = LocalId;
                PhysActor.UUID = UUID;
            }
            //Fix the rezzed attribute
            Rezzed = DateTime.UtcNow;
        }

        public void RotLookAt(Quaternion target, float strength, float damping)
        {
            if (IsAttachment)
            {
                /*
                    ScenePresence avatar = m_scene.GetScenePresence(rootpart.AttachedAvatar);
                    if (avatar != null)
                    {
                    Rotate the Av?
                    } */
            }
            else
            {
                APIDEnabled = true;
                APIDDamp = damping;
                APIDStrength = strength;
                APIDTarget = target;
            }
        }

        public void startLookAt(Quaternion rot, float damp, float strength)
        {
            APIDEnabled = true;
            APIDDamp = damp;
            APIDStrength = strength;
            APIDTarget = rot;
            APIDIterations = 1 + (int) (Math.PI*APIDStrength);
        }

        /// <summary>
        ///     Schedule a terse update for this prim.  Terse updates only send position,
        ///     rotation, velocity, and rotational velocity information.
        /// </summary>
        public void ScheduleTerseUpdate()
        {
            ScheduleUpdate(PrimUpdateFlags.TerseUpdate);
        }

        /// <summary>
        ///     Tell all avatars in the Scene about the new update
        /// </summary>
        /// <param name="UpdateFlags"></param>
        public void ScheduleUpdate(PrimUpdateFlags UpdateFlags)
        {
            if (m_parentGroup != null && m_parentGroup.Scene != null)
                m_parentGroup.Scene.ForEachScenePresence(avatar => avatar.AddUpdateToAvatar(this, UpdateFlags));
        }

        public void ScriptSetPhantomStatus(bool Phantom)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetPhantomStatus(Phantom);
            }
        }

        public void ScriptSetTemporaryStatus(bool Temporary)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetTemporaryStatus(Temporary);
            }
        }

        public void ScriptSetVolumeDetect(bool SetVD)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetVolumeDetect(SetVD);
            }
        }

        /// <summary>
        ///     Trigger or play an attached sound in this part's inventory.
        /// </summary>
        /// <param name="sound"></param>
        /// <param name="volume"></param>
        /// <param name="triggered"></param>
        /// <param name="flags"></param>
        /// <param name="radius"></param>
        public void SendSound(string sound, double volume, bool triggered, byte flags, float radius)
        {
            if (sound == "" || sound == UUID.Zero.ToString())
                return;
            volume = Util.Clip((float)volume, 0, 1);

            UUID ownerID = _ownerID;
            UUID objectID = ParentGroup.RootPart.UUID;
            UUID parentID = GetRootPartUUID();
            UUID soundID = UUID.Zero;
            Vector3 position = AbsolutePosition; // region local
            ulong regionHandle = m_parentGroup.Scene.RegionInfo.RegionHandle;

            if (!UUID.TryParse(sound, out soundID))
            {
                // search sound file from inventory
                lock (TaskInventory)
                {
                    foreach (
                        KeyValuePair<UUID, TaskInventoryItem> item in
                            TaskInventory.Where(
                                item => item.Value.Name == sound && item.Value.Type == (int) AssetType.Sound))
                    {
                        soundID = item.Value.ItemID;
                        break;
                    }
                }
            }

            if (soundID == UUID.Zero)
                return;

            ISoundModule soundModule = m_parentGroup.Scene.RequestModuleInterface<ISoundModule>();
            if (soundModule != null)
            {
                if (triggered)
                    soundModule.TriggerSound(soundID, ownerID, objectID, parentID, volume, position, regionHandle,
                                                radius);
                else
                    soundModule.PlayAttachedSound(soundID, ownerID, objectID, volume, position, flags, radius);
            }
        }

        public void SetAvatarOnSitTarget(UUID avatarID)
        {
            lock (SitTargetAvatar)
            {
                if (!SitTargetAvatar.Contains(avatarID))
                    SitTargetAvatar.Add(avatarID);
            }
            TriggerScriptChangedEvent(Changed.LINK);
        }

        public void RemoveAvatarOnSitTarget(UUID avatarID)
        {
            lock (SitTargetAvatar)
            {
                if (SitTargetAvatar.Contains(avatarID))
                    SitTargetAvatar.Remove(avatarID);
            }
            TriggerScriptChangedEvent(Changed.LINK);
        }

        public void SetAxisRotation(int axis, int rotate)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.SetAxisRotation(axis, rotate);
            }
            //Cannot use ScriptBaseClass constants as no referance to it currently.
            if ((axis & 2) == 2) //STATUS_ROTATE_X
                STATUS_ROTATE_X = rotate;
            if ((axis & 4) == 4) //STATUS_ROTATE_Y
                STATUS_ROTATE_Y = rotate;
            if ((axis & 8) == 8) //STATUS_ROTATE_Z
                STATUS_ROTATE_Z = rotate;
        }

        public void SetBuoyancy(float fvalue)
        {
            if (PhysActor != null)
            {
                PhysActor.Buoyancy = fvalue;
            }
        }

        public void SetDieAtEdge(bool p)
        {
            if (m_parentGroup == null)
                return;
            if (m_parentGroup.IsDeleted)
                return;

            m_parentGroup.RootPart.DIE_AT_EDGE = p;
        }

        public void SetFloatOnWater(int floatYN)
        {
            if (PhysActor != null)
            {
                PhysActor.FloatOnWater = floatYN == 1;
            }
        }

        public void SetForce(Vector3 force)
        {
            if (PhysActor != null)
            {
                PhysActor.Force = force;
            }
        }

        public void SetVehicleFlags(int param, bool remove)
        {
            if (remove)
                VehicleFlags.Remove(param);
            else
                VehicleFlags.Add(param);

            if (PhysActor != null)
                PhysActor.VehicleFlags(param, remove);
        }

        public void SetVehicleType(int type)
        {
            VehicleType = type;
            if (PhysActor != null)
                PhysActor.VehicleType = type;
        }

        public void SetVehicleFloatParam(int param, float value)
        {
            VehicleParameters[param.ToString()] = value;
            if (PhysActor != null)
                PhysActor.VehicleFloatParam(param, value);
        }

        public void SetVehicleVectorParam(int param, Vector3 value)
        {
            VehicleParameters[param.ToString()] = value;
            if (PhysActor != null)
                PhysActor.VehicleVectorParam(param, value);
        }

        public void SetVehicleRotationParam(int param, Quaternion value)
        {
            VehicleParameters[param.ToString()] = value;
            if (PhysActor != null)
                PhysActor.VehicleRotationParam(param, value);
        }

        /// <summary>
        ///     Set the color of prim faces
        /// </summary>
        /// <param name="color"></param>
        /// <param name="face"></param>
        public void SetFaceColor(Vector3 color, int face)
        {
            Primitive.TextureEntry tex = Shape.Textures;
            Color4 texcolor;
            if (face >= 0 && face < GetNumberOfSides())
            {
                texcolor = tex.CreateFace((uint) face).RGBA;
                texcolor.R = Util.Clip(color.X, 0.0f, 1.0f);
                texcolor.G = Util.Clip(color.Y, 0.0f, 1.0f);
                texcolor.B = Util.Clip(color.Z, 0.0f, 1.0f);
                if (!(tex.FaceTextures[face].RGBA.R == texcolor.R &&
                      tex.FaceTextures[face].RGBA.G == texcolor.G &&
                      tex.FaceTextures[face].RGBA.B == texcolor.B))
                {
                    tex.FaceTextures[face].RGBA = texcolor;
                    UpdateTexture(tex, true);
                }
            }
            else if (face == ALL_SIDES)
            {
                bool changed = false;
                for (uint i = 0; i < GetNumberOfSides(); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.R = Util.Clip(color.X, 0.0f, 1.0f);
                        texcolor.G = Util.Clip(color.Y, 0.0f, 1.0f);
                        texcolor.B = Util.Clip(color.Z, 0.0f, 1.0f);
                        if (!(tex.FaceTextures[i].RGBA.R == texcolor.R &&
                              tex.FaceTextures[i].RGBA.G == texcolor.G &&
                              tex.FaceTextures[i].RGBA.B == texcolor.B))
                            changed = true;
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = Util.Clip(color.X, 0.0f, 1.0f);
                    texcolor.G = Util.Clip(color.Y, 0.0f, 1.0f);
                    texcolor.B = Util.Clip(color.Z, 0.0f, 1.0f);
                    if (!(tex.DefaultTexture.RGBA.R == texcolor.R &&
                          tex.DefaultTexture.RGBA.G == texcolor.G &&
                          tex.DefaultTexture.RGBA.B == texcolor.B))
                        changed = true;
                    tex.DefaultTexture.RGBA = texcolor;
                }
                if (changed)
                    UpdateTexture(tex, true);
            }
        }

        /// <summary>
        ///     Get the number of sides that this part has.
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfSides()
        {
            int ret = 0;
            bool hasCut;
            bool hasHollow;
            bool hasDimple;
            bool hasProfileCut;

            PrimType primType = GetPrimType();
            HasCutHollowDimpleProfileCut(primType, Shape, out hasCut, out hasHollow, out hasDimple, out hasProfileCut);

            switch (primType)
            {
                case PrimType.BOX:
                    ret = 6;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.CYLINDER:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.PRISM:
                    ret = 5;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.SPHERE:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasDimple) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.TORUS:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.TUBE:
                    ret = 4;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.RING:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.SCULPT:
                    // Special mesh handling
                    if (Shape.SculptType == 5)
                    {
                        ret = 8; // its a mesh then max 8 faces
                    }
                    else
                    {
                        ret = 1; // its a sculpt then max 1 faces
                    }
                    break;
            }
            return ret;
        }

        /// <summary>
        ///     Tell us what type this prim is
        /// </summary>
        /// <returns></returns>
        public PrimType GetPrimType()
        {
            if (Shape.SculptEntry)
                return PrimType.SCULPT;
            if ((Shape.ProfileCurve & 0x07) == (byte) ProfileShape.Square)
            {
                if (Shape.PathCurve == (byte) Extrusion.Straight)
                    return PrimType.BOX;
                else if (Shape.PathCurve == (byte) Extrusion.Curve1)
                    return PrimType.TUBE;
            }
            else if ((Shape.ProfileCurve & 0x07) == (byte) ProfileShape.Circle)
            {
                if (Shape.PathCurve == (byte) Extrusion.Straight)
                    return PrimType.CYLINDER;
                    // ProfileCurve seems to combine hole shape and profile curve so we need to only compare against the lower 3 bits
                else if (Shape.PathCurve == (byte) Extrusion.Curve1)
                    return PrimType.TORUS;
            }
            else if ((Shape.ProfileCurve & 0x07) == (byte) ProfileShape.HalfCircle)
            {
                if (Shape.PathCurve == (byte) Extrusion.Curve1 || Shape.PathCurve == (byte) Extrusion.Curve2)
                    return PrimType.SPHERE;
            }
            else if ((Shape.ProfileCurve & 0x07) == (byte) ProfileShape.EquilateralTriangle)
            {
                if (Shape.PathCurve == (byte) Extrusion.Straight)
                    return PrimType.PRISM;
                else if (Shape.PathCurve == (byte) Extrusion.Curve1)
                    return PrimType.RING;
            }

            return PrimType.BOX;
        }

        /// <summary>
        /// </summary>
        public void SetParent(ISceneEntity parent)
        {
            m_parentGroup = (SceneObjectGroup) parent;
        }

        // Use this for attachments!  LocalID should be avatar's localid
        public void SetParentLocalId(uint localID)
        {
            _parentID = localID;
        }

        /// <summary>
        ///     Set the events that this part will pass on to listeners.
        /// </summary>
        /// <param name="scriptid"></param>
        /// <param name="events"></param>
        public void SetScriptEvents(UUID scriptid, long events)
        {
            // scriptEvents oldparts;
            lock (m_scriptEvents)
            {
                if (m_scriptEvents.ContainsKey(scriptid))
                {
                    // oldparts = m_scriptEvents[scriptid];

                    // remove values from aggregated script events
                    //if (m_scriptEvents[scriptid] == (scriptEvents) events)
                    //    return;
                    m_scriptEvents[scriptid] = (scriptEvents) events;
                }
                else
                {
                    m_scriptEvents.Add(scriptid, (scriptEvents) events);
                }
            }
            aggregateScriptEvents();
        }

        public void StopLookAt()
        {
            m_parentGroup.RootPart.stopLookAt();
            m_parentGroup.ScheduleGroupTerseUpdate();
        }

        /// <summary>
        ///     Set the text displayed for this part.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        public void SetText(string text, Vector3 color, double alpha)
        {
            //No triggering Changed_Color, so not using Color
            //Color = ...
            UpdateColor(Color.FromArgb((int) (alpha*0xff),
                                       (int) (color.X*0xff),
                                       (int) (color.Y*0xff),
                                       (int) (color.Z*0xff)), false);
            Text = text;

            ScheduleUpdate(PrimUpdateFlags.Text);
        }

        public void StopMoveToTarget()
        {
            m_parentGroup.stopMoveToTarget();

            m_parentGroup.ScheduleGroupTerseUpdate();
            //m_parentGroup.ScheduleGroupForFullUpdate();
        }

        public void StoreUndoState()
        {
            if (!Undoing)
            {
                if (!IgnoreUndoUpdate)
                {
                    IBackupModule backup = null;
                    if (ParentGroup != null &&
                        ParentGroup.Scene != null)
                        backup = ParentGroup.Scene.RequestModuleInterface<IBackupModule>();

                    if (m_parentGroup != null &&
                        ParentGroup.Scene != null &&
                        (backup == null || (backup != null && !backup.LoadingPrims)))
                    {
                        lock (_undoLock)
                        {
                            if (m_undo.Count > 0)
                            {
                                UndoState last = m_undo.Peek();
                                if (last != null)
                                {
                                    if (last.Compare(this))
                                        return;
                                }
                            }

                            UndoState nUndo = new UndoState(this);
                            m_undo.Push(nUndo);
                        }
                    }
                }
            }
        }

        public EntityIntersection TestIntersectionOBB(Ray iray, Quaternion parentrot, bool frontFacesOnly,
                                                      bool faceCenters)
        {
            // In this case we're using a rectangular prism, which has 6 faces and therefore 6 planes
            // This breaks down into the ray---> plane equation.
            // TODO: Change to take shape into account
            Vector3[] vertexes = new Vector3[8];

            // float[] distance = new float[6];
            Vector3[] FaceA = new Vector3[6]; // vertex A for Facei
            Vector3[] FaceB = new Vector3[6]; // vertex B for Facei
            Vector3[] FaceC = new Vector3[6]; // vertex C for Facei
            Vector3[] FaceD = new Vector3[6]; // vertex D for Facei

            Vector3[] normals = new Vector3[6]; // Normal for Facei
            Vector3[] AAfacenormals = new Vector3[6]; // Axis Aligned face normals

            AAfacenormals[0] = new Vector3(1, 0, 0);
            AAfacenormals[1] = new Vector3(0, 1, 0);
            AAfacenormals[2] = new Vector3(-1, 0, 0);
            AAfacenormals[3] = new Vector3(0, -1, 0);
            AAfacenormals[4] = new Vector3(0, 0, 1);
            AAfacenormals[5] = new Vector3(0, 0, -1);

            Vector3 AmBa = new Vector3(0, 0, 0); // Vertex A - Vertex B
            Vector3 AmBb = new Vector3(0, 0, 0); // Vertex B - Vertex C
            Vector3 cross = new Vector3();

            Vector3 pos = GetWorldPosition();
            Quaternion rot = GetWorldRotation();

            // Variables prefixed with AX are Axiom.Math copies of the LL variety.

            Quaternion AXrot = rot;
            AXrot.Normalize();

            Vector3 AXpos = pos;

            // tScale is the offset to derive the vertex based on the scale.
            // it's different for each vertex because we've got to rotate it
            // to get the world position of the vertex to produce the Oriented Bounding Box

            Vector3 tScale = Vector3.Zero;

            Vector3 AXscale = new Vector3(m_shape.Scale.X*0.5f, m_shape.Scale.Y*0.5f, m_shape.Scale.Z*0.5f);

            //Vector3 pScale = (AXscale) - (AXrot.Inverse() * (AXscale));
            //Vector3 nScale = (AXscale * -1) - (AXrot.Inverse() * (AXscale * -1));

            // rScale is the rotated offset to find a vertex based on the scale and the world rotation.
            Vector3 rScale = new Vector3();

            // Get Vertexes for Faces Stick them into ABCD for each Face
            // Form: Face<vertex>[face] that corresponds to the below diagram

            #region ABCD Face Vertex Map Comment Diagram

            //                   A _________ B
            //                    |         |
            //                    |  4 top  |
            //                    |_________|
            //                   C           D

            //                   A _________ B
            //                    |  Back   |
            //                    |    3    |
            //                    |_________|
            //                   C           D

            //   A _________ B                     B _________ A
            //    |  Left   |                       |  Right  |
            //    |    0    |                       |    2    |
            //    |_________|                       |_________|
            //   C           D                     D           C

            //                   A _________ B
            //                    |  Front  |
            //                    |    1    |
            //                    |_________|
            //                   C           D

            //                   C _________ D
            //                    |         |
            //                    |  5 bot  |
            //                    |_________|
            //                   A           B

            #endregion

            #region Plane Decomposition of Oriented Bounding Box

            tScale = new Vector3(AXscale.X, -AXscale.Y, AXscale.Z);
            rScale = tScale*AXrot;
            vertexes[0] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));
            // vertexes[0].X = pos.X + vertexes[0].X;
            //vertexes[0].Y = pos.Y + vertexes[0].Y;
            //vertexes[0].Z = pos.Z + vertexes[0].Z;

            FaceA[0] = vertexes[0];
            FaceB[3] = vertexes[0];
            FaceA[4] = vertexes[0];

            tScale = AXscale;
            rScale = tScale*AXrot;
            vertexes[1] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            // vertexes[1].X = pos.X + vertexes[1].X;
            // vertexes[1].Y = pos.Y + vertexes[1].Y;
            //vertexes[1].Z = pos.Z + vertexes[1].Z;

            FaceB[0] = vertexes[1];
            FaceA[1] = vertexes[1];
            FaceC[4] = vertexes[1];

            tScale = new Vector3(AXscale.X, -AXscale.Y, -AXscale.Z);
            rScale = tScale*AXrot;

            vertexes[2] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            //vertexes[2].X = pos.X + vertexes[2].X;
            //vertexes[2].Y = pos.Y + vertexes[2].Y;
            //vertexes[2].Z = pos.Z + vertexes[2].Z;

            FaceC[0] = vertexes[2];
            FaceD[3] = vertexes[2];
            FaceC[5] = vertexes[2];

            tScale = new Vector3(AXscale.X, AXscale.Y, -AXscale.Z);
            rScale = tScale*AXrot;
            vertexes[3] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            //vertexes[3].X = pos.X + vertexes[3].X;
            // vertexes[3].Y = pos.Y + vertexes[3].Y;
            // vertexes[3].Z = pos.Z + vertexes[3].Z;

            FaceD[0] = vertexes[3];
            FaceC[1] = vertexes[3];
            FaceA[5] = vertexes[3];

            tScale = new Vector3(-AXscale.X, AXscale.Y, AXscale.Z);
            rScale = tScale*AXrot;
            vertexes[4] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            // vertexes[4].X = pos.X + vertexes[4].X;
            // vertexes[4].Y = pos.Y + vertexes[4].Y;
            // vertexes[4].Z = pos.Z + vertexes[4].Z;

            FaceB[1] = vertexes[4];
            FaceA[2] = vertexes[4];
            FaceD[4] = vertexes[4];

            tScale = new Vector3(-AXscale.X, AXscale.Y, -AXscale.Z);
            rScale = tScale*AXrot;
            vertexes[5] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            // vertexes[5].X = pos.X + vertexes[5].X;
            // vertexes[5].Y = pos.Y + vertexes[5].Y;
            // vertexes[5].Z = pos.Z + vertexes[5].Z;

            FaceD[1] = vertexes[5];
            FaceC[2] = vertexes[5];
            FaceB[5] = vertexes[5];

            tScale = new Vector3(-AXscale.X, -AXscale.Y, AXscale.Z);
            rScale = tScale*AXrot;
            vertexes[6] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            // vertexes[6].X = pos.X + vertexes[6].X;
            // vertexes[6].Y = pos.Y + vertexes[6].Y;
            // vertexes[6].Z = pos.Z + vertexes[6].Z;

            FaceB[2] = vertexes[6];
            FaceA[3] = vertexes[6];
            FaceB[4] = vertexes[6];

            tScale = new Vector3(-AXscale.X, -AXscale.Y, -AXscale.Z);
            rScale = tScale*AXrot;
            vertexes[7] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            // vertexes[7].X = pos.X + vertexes[7].X;
            // vertexes[7].Y = pos.Y + vertexes[7].Y;
            // vertexes[7].Z = pos.Z + vertexes[7].Z;

            FaceD[2] = vertexes[7];
            FaceC[3] = vertexes[7];
            FaceD[5] = vertexes[7];

            #endregion

            // Get our plane normals
            for (int i = 0; i < 6; i++)
            {
                //MainConsole.Instance.Info("[FACECALCULATION]: FaceA[" + i + "]=" + FaceA[i] + " FaceB[" + i + "]=" + FaceB[i] + " FaceC[" + i + "]=" + FaceC[i] + " FaceD[" + i + "]=" + FaceD[i]);

                // Our Plane direction
                AmBa = FaceA[i] - FaceB[i];
                AmBb = FaceB[i] - FaceC[i];

                cross = Vector3.Cross(AmBb, AmBa);

                // normalize the cross product to get the normal.
                normals[i] = cross/cross.Length();

                //MainConsole.Instance.Info("[NORMALS]: normals[ " + i + "]" + normals[i].ToString());
                //distance[i] = (normals[i].X * AmBa.X + normals[i].Y * AmBa.Y + normals[i].Z * AmBa.Z) * -1;
            }

            EntityIntersection result = new EntityIntersection {distance = 1024};

            float c = 0;
            float a = 0;
            float d = 0;
            Vector3 q = new Vector3();

            #region OBB Version 2 Experiment

            //float fmin = 999999;
            //float fmax = -999999;
            //float s = 0;

            //for (int i=0;i<6;i++)
            //{
            //s = iray.Direction.Dot(normals[i]);
            //d = normals[i].Dot(FaceB[i]);

            //if (s == 0)
            //{
            //if (iray.Origin.Dot(normals[i]) > d)
            //{
            //return result;
            //}
            // else
            //{
            //continue;
            //}
            //}
            //a = (d - iray.Origin.Dot(normals[i])) / s;
            //if (iray.Direction.Dot(normals[i]) < 0)
            //{
            //if (a > fmax)
            //{
            //if (a > fmin)
            //{
            //return result;
            //}
            //fmax = a;
            //}

            //}
            //else
            //{
            //if (a < fmin)
            //{
            //if (a < 0 || a < fmax)
            //{
            //return result;
            //}
            //fmin = a;
            //}
            //}
            //}
            //if (fmax > 0)
            //    a= fmax;
            //else
            //     a=fmin;

            //q = iray.Origin + a * iray.Direction;

            #endregion

            // Loop over faces (6 of them)
            for (int i = 0; i < 6; i++)
            {
                AmBa = FaceA[i] - FaceB[i];
                AmBb = FaceB[i] - FaceC[i];
                d = Vector3.Dot(normals[i], FaceB[i]);

                //if (faceCenters)
                //{
                //    c = normals[i].Dot(normals[i]);
                //}
                //else
                //{
                c = Vector3.Dot(iray.Direction, normals[i]);
                //}
                if (c == 0)
                    continue;

                a = (d - Vector3.Dot(iray.Origin, normals[i]))/c;

                if (a < 0)
                    continue;

                // If the normal is pointing outside the object
                if (Vector3.Dot(iray.Direction, normals[i]) < 0 || !frontFacesOnly)
                {
                    //if (faceCenters)
                    //{   //(FaceA[i] + FaceB[i] + FaceC[1] + FaceD[i]) / 4f;
                    //    q =  iray.Origin + a * normals[i];
                    //}
                    //else
                    //{
                    q = iray.Origin + iray.Direction*a;
                    //}

                    float distance2 = (float) GetDistanceTo(q, AXpos);
                    // Is this the closest hit to the object's origin?
                    //if (faceCenters)
                    //{
                    //    distance2 = (float)GetDistanceTo(q, iray.Origin);
                    //}

                    if (distance2 < result.distance)
                    {
                        result.distance = distance2;
                        result.HitTF = true;
                        result.ipoint = q;
                        //MainConsole.Instance.Info("[FACE]:" + i.ToString());
                        //MainConsole.Instance.Info("[POINT]: " + q.ToString());
                        //MainConsole.Instance.Info("[DIST]: " + distance2.ToString());
                        if (faceCenters)
                        {
                            result.normal = AAfacenormals[i]*AXrot;

                            Vector3 scaleComponent = AAfacenormals[i];
                            float ScaleOffset = 0.5f;
                            if (scaleComponent.X != 0) ScaleOffset = AXscale.X;
                            if (scaleComponent.Y != 0) ScaleOffset = AXscale.Y;
                            if (scaleComponent.Z != 0) ScaleOffset = AXscale.Z;
                            ScaleOffset = Math.Abs(ScaleOffset);
                            Vector3 offset = result.normal*ScaleOffset;
                            result.ipoint = AXpos + offset;

                            //pos = (intersectionpoint + offset);
                        }
                        else
                        {
                            result.normal = normals[i];
                        }
                        result.AAfaceNormal = AAfacenormals[i];
                    }
                }
            }
            return result;
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            if (m_parentGroup != null && m_parentGroup.Scene != null)
                m_parentGroup.Scene.EventManager.TriggerOnScriptChangedEvent(this, (uint) val);
        }

        public void TrimPermissions()
        {
            _baseMask &= (uint) PermissionMask.All;
            _ownerMask &= (uint) PermissionMask.All;
            _groupMask &= (uint) PermissionMask.All;
            _everyoneMask &= (uint) PermissionMask.All;
            _nextOwnerMask &= (uint) PermissionMask.All;
        }

        public void Undo()
        {
            var undoCount = 0;
            lock (_undoLock)
                undoCount = m_undo.Count;
            
            if (undoCount > 0)
            {
                lock(_redoLock)
                    m_redo.Push(new UndoState(this));

                UndoState goback = null;
                lock(_undoLock)
                    goback = m_undo.Pop();

                if (goback != null) {
                    goback.PlaybackState(this);
                }
            }
        }

        public void Redo()
        {
            var redoCount = 0;
            lock (_redoLock)
                redoCount = m_redo.Count;

            if (redoCount > 0) {
                lock(_undoLock) {
                    UndoState nUndo = new UndoState (this);
                    m_undo.Push (nUndo);
                }

                UndoState gofwd = null;
                lock(_redoLock)
                    gofwd = m_redo.Pop();
                
                if (gofwd != null)
                    gofwd.PlayfwdState(this);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateOffSet(Vector3 pos)
        {
            if ((pos.X != OffsetPosition.X) ||
                (pos.Y != OffsetPosition.Y) ||
                (pos.Z != OffsetPosition.Z))
            {
                Vector3 newPos = new Vector3(pos.X, pos.Y, pos.Z);

                if (ParentGroup.RootPart.GetStatusSandbox())
                {
                    if (Util.GetDistanceTo(ParentGroup.RootPart.StatusSandboxPos, newPos) > 10)
                    {
                        ParentGroup.ScriptSetPhysicsStatus(false);
                        newPos = OffsetPosition;
                        IChatModule chatModule = ParentGroup.Scene.RequestModuleInterface<IChatModule>();
                        if (chatModule != null)
                            chatModule.SimChat("Hit Sandbox Limit", ChatTypeEnum.DebugChannel, 0x7FFFFFFF,
                                               ParentGroup.RootPart.AbsolutePosition, Name, UUID, false,
                                               ParentGroup.Scene);
                    }
                }
                ValidpartOOB = false;
                FixOffsetPosition(newPos, true);
                ScheduleTerseUpdate();
            }
        }

        public bool UpdatePrimFlags(bool UsePhysics, bool IsTemporary, bool IsPhantom, bool IsVD,
                                    ObjectFlagUpdatePacket.ExtraPhysicsBlock[] blocks)
        {
            bool wasUsingPhysics = ((Flags & PrimFlags.Physics) != 0);
            bool wasTemporary = ((Flags & PrimFlags.TemporaryOnRez) != 0);
            bool wasPhantom = ((Flags & PrimFlags.Phantom) != 0);
            bool wasVD = VolumeDetectActive;

            bool needsPhysicalRebuild = false;

            if (blocks != null && blocks.Length != 0)
            {
                ObjectFlagUpdatePacket.ExtraPhysicsBlock block = blocks[0];
                //These 2 are static properties, and do require rebuilding the entire physical representation
                if (PhysicsType != block.PhysicsShapeType)
                {
                    PhysicsType = block.PhysicsShapeType;
                    needsPhysicalRebuild = true; //Gotta rebuild now
                }
                if (Density != block.Density)
                {
                    Density = block.Density;
                    needsPhysicalRebuild = true; //Gotta rebuild now
                }
                //These 3 are dynamic properties, and don't require rebuilding the physics representation
                Friction = block.Friction;
                Restitution = block.Restitution;
                GravityMultiplier = block.GravityMultiplier;
            }

            if ((UsePhysics == wasUsingPhysics) && (wasTemporary == IsTemporary) && (wasPhantom == IsPhantom) &&
                (IsVD == wasVD))
                return needsPhysicalRebuild;

            // Special cases for VD. VD can only be called from a script 
            // and can't be combined with changes to other states. So we can rely
            // that...
            // ... if VD is changed, all others are not.
            // ... if one of the others is changed, VD is not.
            if (IsVD) // VD is active, special logic applies
            {
                // State machine logic for VolumeDetect
                // More logic below
                bool phanReset = (IsPhantom != wasPhantom) && !IsPhantom;

                if (phanReset) // Phantom changes from on to off switch VD off too
                {
                    IsVD = false; // Switch it of for the course of this routine
                    VolumeDetectActive = false; // and also permanently
                    if (PhysActor != null)
                        PhysActor.VolumeDetect = false; // Let physics know about it too
                }
                else
                {
                    IsPhantom = false;
                    // If volumedetect is active we don't want phantom to be applied.
                    // If this is a new call to VD out of the state "phantom"
                    // this will also cause the prim to be visible to physics
                }
            }

            if (UsePhysics)
                AddFlag(PrimFlags.Physics);
            else
                RemFlag(PrimFlags.Physics);
            if (PhysActor != null)
            {
                PhysActor.IsPhysical = UsePhysics;
                if (!UsePhysics)
                {
                    //Clear out old data
                    Velocity = Vector3.Zero;
                    Acceleration = Vector3.Zero;
                    AngularVelocity = Vector3.Zero;
                    PhysActor.RotationalVelocity = Vector3.Zero;
                    PhysActor.ClearVelocity();
                    GenerateRotationalVelocityFromOmega();
                    if (wasUsingPhysics)
                        ScheduleTerseUpdate(); //Force it out of the client too
                }
            }


            if (IsPhantom || IsAttachment || (Shape.PathCurve == (byte) Extrusion.Flexible))
                // note: this may have been changed above in the case of joints
            {
                AddFlag(PrimFlags.Phantom);
                needsPhysicalRebuild = true; //Gotta rebuild now
            }
            else // Not phantom
            {
                if (wasPhantom)
                {
                    RemFlag(PrimFlags.Phantom);
                    needsPhysicalRebuild = true; //Gotta rebuild now
                }
            }

            if (IsVD && IsVD != VolumeDetectActive)
            {
                // If the above logic worked (this is urgent candidate to unit tests!)
                // we now have a physicsactor.
                // Defensive programming calls for a check here.
                // Better would be throwing an exception that could be catched by a unit test as the internal 
                // logic should make sure, this Physactor is always here.

                //FALSE, you can go from a phantom prim > VD -7/26
                if (PhysActor != null)
                    PhysActor.VolumeDetect = true;
                AddFlag(PrimFlags.Phantom); // We set this flag also if VD is active
                VolumeDetectActive = true;
            }
            else
            {
                if (!IsVD)
                {
                    // Remove VolumeDetect in any case. Note, it's safe to call SetVolumeDetect as often as you like
                    // (mumbles, well, at least if you have infinte CPU powers :-))
                    PhysicsActor pa = PhysActor;
                    if (pa != null)
                        PhysActor.VolumeDetect = false;

                    VolumeDetectActive = false;
                }
            }


            if (IsTemporary)
                AddFlag(PrimFlags.TemporaryOnRez);
            else
                RemFlag(PrimFlags.TemporaryOnRez);

            if (UsePhysics != wasUsingPhysics) //Fire the event
                ParentGroup.Scene.WhiteCoreEventManager.FireGenericEventHandler("ObjectChangedPhysicalStatus", ParentGroup);

            ParentGroup.HasGroupChanged = true;
            ScheduleUpdate(PrimUpdateFlags.PrimFlags);

            return needsPhysicalRebuild;
        }

        public void UpdateRotation(Quaternion rot)
        {
            if ((rot.X != GetRotationOffset().X) ||
                (rot.Y != GetRotationOffset().Y) ||
                (rot.Z != GetRotationOffset().Z) ||
                (rot.W != GetRotationOffset().W))
            {
                SetRotationOffset(true, rot, true);
                ValidpartOOB = false;
                ScheduleTerseUpdate();
            }
        }

        /// <summary>
        ///     Update the shape of this part.
        /// </summary>
        /// <param name="shapeBlock"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            UpdateShape(shapeBlock, true);
        }

        /// <summary>
        ///     Having this function because I found when scripts updated the shape.. over and over, it would fill up the memory
        ///     Having the extra parameter updatePhysics can prevent physics updates on the changes
        ///     The only place this effects is if a script changes the shape
        ///     If the LocklessQueue gets updated this can be removed
        /// </summary>
        /// <param name="shapeBlock"></param>
        /// <param name="UpdatePhysics"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock, bool UpdatePhysics)
        {
            IOpenRegionSettingsModule module = ParentGroup.Scene.RequestModuleInterface<IOpenRegionSettingsModule>();
            if (module != null)
            {
                if (shapeBlock.ProfileHollow > (module.MaximumHollowSize*500) &&
                    module.MaximumHollowSize != -1)
                    //This is so that it works correctly, since the packet sends (N * 500)
                {
                    shapeBlock.ProfileHollow = (ushort) (module.MaximumHollowSize*500);
                }
                if (shapeBlock.PathScaleY > (200 - (module.MinimumHoleSize*100)) &&
                    module.MinimumHoleSize != -1 && shapeBlock.PathCurve == 32)
                    //This is how the packet is set up... so this is how we check for it...
                {
                    shapeBlock.PathScaleY = Convert.ToByte((200 - (module.MinimumHoleSize*100)));
                }
            }

            m_shape.PathBegin = shapeBlock.PathBegin;
            m_shape.PathEnd = shapeBlock.PathEnd;
            m_shape.PathScaleX = shapeBlock.PathScaleX;
            m_shape.PathScaleY = shapeBlock.PathScaleY;
            m_shape.PathShearX = shapeBlock.PathShearX;
            m_shape.PathShearY = shapeBlock.PathShearY;
            m_shape.PathSkew = shapeBlock.PathSkew;
            m_shape.ProfileBegin = shapeBlock.ProfileBegin;
            m_shape.ProfileEnd = shapeBlock.ProfileEnd;
            m_shape.PathCurve = shapeBlock.PathCurve;
            m_shape.ProfileCurve = shapeBlock.ProfileCurve;

            m_shape.ProfileHollow = shapeBlock.ProfileHollow;
            m_shape.PathRadiusOffset = shapeBlock.PathRadiusOffset;
            m_shape.PathRevolutions = shapeBlock.PathRevolutions;
            m_shape.PathTaperX = shapeBlock.PathTaperX;
            m_shape.PathTaperY = shapeBlock.PathTaperY;
            m_shape.PathTwist = shapeBlock.PathTwist;
            m_shape.PathTwistBegin = shapeBlock.PathTwistBegin;

            if (m_shape.SculptEntry && UpdatePhysics)
                m_parentGroup.Scene.AssetService.Get(m_shape.SculptTexture.ToString(), true, AssetReceived);
            else
            {
                Shape = m_shape;

                if ((UpdatePhysics) && (PhysActor != null))
                    PhysActor.Shape = m_shape;
            }

            // This is what makes vehicle trailers work
            // A script in a child prim re-issues
            // llSetPrimitiveParams(PRIM_TYPE) every few seconds. That
            // prevents autoreturn. This also works in SL.
            if (ParentGroup.RootPart != this)
                ParentGroup.RootPart.Rezzed = DateTime.UtcNow;

            ValidpartOOB = false;
            ParentGroup.HasGroupChanged = true;
            ScheduleUpdate(PrimUpdateFlags.FullUpdate);
        }

        /// <summary>
        ///     Update the textures on the part.
        /// </summary>
        /// Added to handle bug in libsecondlife's TextureEntry.ToBytes()
        /// not handling RGBA properly. Cycles through, and "fixes" the color
        /// info
        /// <param name="tex"></param>
        /// <param name="sendChangedEvent"></param>
        public void UpdateTexture(Primitive.TextureEntry tex, bool sendChangedEvent)
        {
            //Color4 tmpcolor;
            //for (uint i = 0; i < 32; i++)
            //{
            //    if (tex.FaceTextures[i] != null)
            //    {
            //        tmpcolor = tex.GetFace((uint) i).RGBA;
            //        tmpcolor.A = tmpcolor.A*255;
            //        tmpcolor.R = tmpcolor.R*255;
            //        tmpcolor.G = tmpcolor.G*255;
            //        tmpcolor.B = tmpcolor.B*255;
            //        tex.FaceTextures[i].RGBA = tmpcolor;
            //    }
            //}
            //tmpcolor = tex.DefaultTexture.RGBA;
            //tmpcolor.A = tmpcolor.A*255;
            //tmpcolor.R = tmpcolor.R*255;
            //tmpcolor.G = tmpcolor.G*255;
            //tmpcolor.B = tmpcolor.B*255;
            //tex.DefaultTexture.RGBA = tmpcolor;
            UpdateTextureEntry(tex.GetBytes(), sendChangedEvent);
        }

        public void aggregateScriptEvents()
        {
            AggregateScriptEvents = 0;

            // Aggregate script events
            lock (m_scriptEvents)
            {
                foreach (scriptEvents s in m_scriptEvents.Values)
                {
                    AggregateScriptEvents |= s;
                }
            }

            uint objectflagupdate = 0;

            if (
                ((AggregateScriptEvents & scriptEvents.touch) != 0) ||
                ((AggregateScriptEvents & scriptEvents.touch_end) != 0) ||
                ((AggregateScriptEvents & scriptEvents.touch_start) != 0)
                )
            {
                objectflagupdate |= (uint) PrimFlags.Touch;
            }

            if ((AggregateScriptEvents & scriptEvents.money) != 0)
            {
                objectflagupdate |= (uint) PrimFlags.Money;
            }

            if (AllowedDrop)
            {
                objectflagupdate |= (uint) PrimFlags.AllowInventoryDrop;
            }

            // subscribe to physics updates.
            //We subscribe by default now... so 'shouldn't' need this
            if ((((AggregateScriptEvents & scriptEvents.collision) != 0) ||
                 ((AggregateScriptEvents & scriptEvents.collision_end) != 0) ||
                 ((AggregateScriptEvents & scriptEvents.collision_start) != 0) ||
                 ((AggregateScriptEvents & scriptEvents.land_collision) != 0) ||
                 ((AggregateScriptEvents & scriptEvents.land_collision_end) != 0) ||
                 ((AggregateScriptEvents & scriptEvents.land_collision_start) != 0)
                ) && PhysActor != null)
            {
                if (!m_hasSubscribedToCollisionEvent)
                {
                    m_hasSubscribedToCollisionEvent = true;
                    PhysActor.OnCollisionUpdate += PhysicsCollision;
                    PhysActor.SubscribeEvents(1000);
                }
            }
            else if (PhysActor != null)
            {
                if (m_hasSubscribedToCollisionEvent)
                {
                    m_hasSubscribedToCollisionEvent = false;
                    PhysActor.OnCollisionUpdate -= PhysicsCollision;
                }
            }

            LocalFlags = (PrimFlags) objectflagupdate;

            if (m_parentGroup != null && m_parentGroup.RootPart == this)
            {
                m_parentGroup.aggregateScriptEvents();
            }
            else
            {
//                MainConsole.Instance.DebugFormat(
//                    "[SCENE OBJECT PART]: Scheduling part {0} {1} for full update in aggregateScriptEvents()", Name, LocalId);
                ScheduleUpdate(PrimUpdateFlags.PrimFlags);
            }
        }

        public int registerTargetWaypoint(Vector3 target, float tolerance)
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.registerTargetWaypoint(target, tolerance);
            }
            return 0;
        }

        public void unregisterTargetWaypoint(int handle)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.unregisterTargetWaypoint(handle);
            }
        }

        public int registerRotTargetWaypoint(Quaternion target, float tolerance)
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.registerRotTargetWaypoint(target, tolerance);
            }
            return 0;
        }

        public void unregisterRotTargetWaypoint(int handle)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.unregisterRotTargetWaypoint(handle);
            }
        }

        #region Vehicle Params

        OSDArray m_VehicleFlags;

        OSDMap m_VehicleParams;

        [ProtoMember(110)]
        public int VehicleType { get; set; }

        [ProtoMember(111)]
        string _vehicleFlags
        {
            get {
                return m_VehicleFlags == null ? "" : OSDParser.SerializeLLSDXmlString(m_VehicleFlags);
            }
            set {
                if (value != null)
                    m_VehicleFlags =  value == "" ? null : (OSDArray)OSDParser.DeserializeLLSDXml(value);
            }
        }

        [ProtoMember(112)]
        string _vehicleParams
        {
            get { 
                return m_VehicleParams == null ? "" : OSDParser.SerializeJsonString(m_VehicleParams, true);
            }
            set {
                if (value != null)
                    m_VehicleParams = value == "" ? null : (OSDMap) OSDParser.DeserializeJson(value);
            }
        }


        public OSDArray VehicleFlags
        {
            get
            {
                if (m_VehicleFlags == null)
                {
                    m_VehicleFlags = new OSDArray ();
                    m_VehicleFlags.Add (0);                 
                }
                return m_VehicleFlags;
            }
            set { m_VehicleFlags = value; }
        }

        public OSDMap VehicleParameters
        {
            get
            {
                if (m_VehicleParams == null)
                {
                    m_VehicleParams = new OSDMap ();
                    //m_VehicleParams ["99"] = 0;
                }
                return m_VehicleParams;
            }
            set { m_VehicleParams = value; }
        }

           

        #endregion

        public void SetRotationOffset(bool UpdatePrimActor, Quaternion value, bool single)
        {
            if (ParentGroup != null)
                ParentGroup.HasGroupChanged = true;
            ValidpartOOB = false;

            RotationOffset = value;

            PhysicsActor actor = PhysActor;
            if (actor != null)
            {
                if (value.W == 0) //We have an issue here... try to normalize it
                    value.Normalize();
                if (actor.PhysicsActorType != (int) ActorTypes.Prim) // for now let other times get updates
                {
                    UpdatePrimActor = true;
                    single = false;
                }
                if (UpdatePrimActor)
                {
                    try
                    {
                        // Root prim gets value directly
                        if (_parentID == 0)
                        {
                            actor.Orientation = value;
                            //MainConsole.Instance.Info("[PART]: RO1:" + actor.Orientation.ToString());
                        }
                        else if (single || !actor.IsPhysical)
                        {
                            // Child prim we have to calculate it's world rotationwel
                            Quaternion resultingrotation = GetWorldRotation();
                            actor.Orientation = resultingrotation;
                            //MainConsole.Instance.Info("[PART]: RO2:" + actor.Orientation.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.Error("[Scene object part]: Rotation offset exception - " + ex.Message);
                    }
                }
            }
        }

        public void SetOffsetPosition(Vector3 value)
        {
            m_offsetPosition = value;
            ValidpartOOB = false;
        }

        public void SetGroupPosition(Vector3 value)
        {
            m_groupPosition = new Vector3(value.X, value.Y, value.Z);
        }


        public void FixGroupPosition(Vector3 value, bool single)
        {
            FixGroupPositionComum(true, value, single);
        }

        public void FixGroupPositionComum(bool updatePrimActor, Vector3 value, bool single)
        {
            bool TriggerMoving_End = false;
            if (m_groupPosition != value)
            {
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
                TriggerMoving_End = true;
                TriggerScriptMovingStartEvent();
            }

            m_groupPosition = value;

            PhysicsActor actor = PhysActor;

            if (actor != null)
            {
                if (actor.PhysicsActorType != (int) ActorTypes.Prim) // for now let other times get updates
                {
                    updatePrimActor = true;
                    single = false;
                }
                if (updatePrimActor)
                {
                    try
                    {
                        // Root prim actually goes at Position
                        if (_parentID == 0)
                        {
                            actor.Position = value;
                        }
                        else if (single || !actor.IsPhysical)
                        {
                            // To move the child prim in respect to the group position and rotation we have to calculate
                            actor.Position = GetWorldPosition();
                            actor.Orientation = GetWorldRotation();
                        }

                        // Tell the physics engines that this prim changed.
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.Error("[Scene object part]: Group position exception - " + e.Message);
                    }
                }
            }

            lock (SitTargetAvatar)
            {
                if (SitTargetAvatar.Count != 0)
                {
                    foreach (UUID avID in SitTargetAvatar.Where(avID => m_parentGroup != null))
                    {
                        IScenePresence avatar;
                        if (m_parentGroup.Scene.TryGetScenePresence(avID, out avatar))
                        {
                            avatar.ParentPosition = GetWorldPosition();
                        }
                    }
                }
            }
            if (TriggerMoving_End)
                TriggerScriptMovingEndEvent();
        }

        public void ResetExpire()
        {
            Expires = DateTime.Now + new TimeSpan(TimeSpan.TicksPerMinute);
        }

        public event AddPhysics OnAddPhysics;
        public event RemovePhysics OnRemovePhysics;

        public void FireOnAddedPhysics()
        {
            if (OnAddPhysics != null)
                OnAddPhysics();
        }

        public void FireOnRemovedPhysics()
        {
            if (OnRemovePhysics != null)
                OnRemovePhysics();
        }

        public void ClearUndoState()
        {
            lock (_undoLock)
                m_undo = new UndoStack<UndoState>(5);
            lock (_redoLock)
                m_redo = new UndoStack<UndoState>(5);

            StoreUndoState();
        }

        public byte ConvertScriptUintToByte(uint indata)
        {
            byte outdata = (byte) TextureAnimFlags.NONE;
            if ((indata & 1) != 0) outdata |= (byte) TextureAnimFlags.ANIM_ON;
            if ((indata & 2) != 0) outdata |= (byte) TextureAnimFlags.LOOP;
            if ((indata & 4) != 0) outdata |= (byte) TextureAnimFlags.REVERSE;
            if ((indata & 8) != 0) outdata |= (byte) TextureAnimFlags.PING_PONG;
            if ((indata & 16) != 0) outdata |= (byte) TextureAnimFlags.SMOOTH;
            if ((indata & 32) != 0) outdata |= (byte) TextureAnimFlags.ROTATE;
            if ((indata & 64) != 0) outdata |= (byte) TextureAnimFlags.SCALE;
            return outdata;
        }

        /// <summary>
        ///     Duplicates this part.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="clonePhys"></param>
        /// <returns></returns>
        public SceneObjectPart Copy(SceneObjectGroup parent, bool clonePhys)
        {
            SceneObjectPart dupe = (SceneObjectPart) MemberwiseClone();
            dupe.m_parentGroup = parent;
            dupe.m_shape = m_shape.Copy();
            dupe.m_partOOBsize = m_partOOBsize;
            dupe.m_partOOBoffset = m_partOOBoffset;
            dupe.m_partBSphereRadiusSQ = m_partBSphereRadiusSQ;
            dupe.m_regionHandle = m_regionHandle;

            //memberwiseclone means it also clones the physics actor reference
            // This will make physical prim 'bounce' if not set to null.

            if (!clonePhys)
                dupe.PhysActor = null;

            dupe._groupID = GroupID;
            dupe.m_groupPosition = m_groupPosition;
            dupe.m_offsetPosition = m_offsetPosition;
            dupe.RotationOffset = RotationOffset;
            dupe.Velocity = new Vector3(0, 0, 0);
            dupe.Acceleration = new Vector3(0, 0, 0);
            dupe.AngularVelocity = new Vector3(0, 0, 0);
            dupe.Flags = Flags;
            dupe.LinkNum = LinkNum;
            dupe.SitTargetAvatar = new List<UUID>();

            dupe.m_ValidpartOOB = false;

            dupe._ownershipCost = _ownershipCost;
            dupe._objectSaleType = _objectSaleType;
            dupe._salePrice = _salePrice;
            dupe._category = _category;
            dupe.Rezzed = Rezzed;

            dupe.m_inventory = new SceneObjectPartInventory(dupe)
                                   {
                                       Items = (TaskInventoryDictionary) m_inventory.Items.Clone(),
                                       HasInventoryChanged = m_inventory.HasInventoryChanged
                                   };

            byte[] extraP = new byte[Shape.ExtraParams.Length];
            Array.Copy(Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;

            dupe.m_scriptEvents = new Dictionary<UUID, scriptEvents>();
            dupe.Shape.SculptData = Shape.SculptData;
            dupe.GenerateRotationalVelocityFromOmega();

            return dupe;
        }

        public void AssetReceived(string id, Object sender, AssetBase asset)
        {
            if (asset != null)
            {
                Shape.SculptEntry = true;
                Shape.SculptData = asset.Data; //Set the asset data
            }

            bool isMesh = asset == null ? false : (asset.Type == (int) AssetType.Mesh);
            if (isMesh)
                Shape.SculptType = (byte) SculptType.Mesh;
            PrimitiveBaseShape shape = Shape.Copy();
            if ((bool) sender && PhysActor != null &&
                (asset != null || (Shape.SculptData != null && Shape.SculptData.Length != 0)))
                //Update physics
            {
                //Get physics to update in a hackish way
                PhysActor.Shape = shape;
            }
            Shape = shape;
        }

        public double GetDistanceTo(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return Math.Sqrt(dx*dx + dy*dy + dz*dz);
        }

        public UUID GetRootPartUUID()
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.UUID;
            }
            return UUID.Zero;
        }

        public void SetMoveToTarget(bool Enabled, Vector3 target, float tau)
        {
            if (Enabled)
            {
                m_initialPIDLocation = AbsolutePosition;
                PIDTarget = target;
                PIDTau = tau;
                PIDActive = true;
            }
            else
            {
                PIDActive = false;
                m_initialPIDLocation = Vector3.Zero;
            }
        }

        public void StopHover()
        {
            m_parentGroup.SetHoverHeight(0f, PIDHoverType.Ground, 0f);
        }

        public virtual void OnGrab(Vector3 offsetPos, IClientAPI remoteClient)
        {
        }

        public void PhysicsCollision(EventArgs e)
        {
            // single threaded here
            if (e == null)
                return;

            CollisionEventUpdate a = (CollisionEventUpdate) e;
            Dictionary<uint, ContactPoint> collissionswith = a.GetCollisionEvents();
            List<uint> thisHitColliders = new List<uint>();
            List<uint> startedColliders = new List<uint>();
            ContactPoint startedCollider = new ContactPoint();

            // calculate things that started colliding this time
            // and build up list of colliders this time
            foreach (uint localID in collissionswith.Keys)
            {
                thisHitColliders.Add(localID);
                if (!m_lastColliders.Contains(localID))
                {
                    startedCollider = collissionswith[localID];
                    startedColliders.Add(localID);
                }
                //MainConsole.Instance.Debug("[OBJECT]: Collided with:" + localid.ToString() + " at depth of: " + collissionswith[localid].ToString());
            }

            // calculate things that ended colliding
            List<uint> endedColliders = m_lastColliders.Where(localID => !thisHitColliders.Contains(localID)).ToList();

            //add the items that started colliding this time to the last colliders list.
            m_lastColliders.AddRange(startedColliders);
            // remove things that ended colliding from the last colliders list
            foreach (uint localID in endedColliders)
                m_lastColliders.Remove(localID);

            if (m_parentGroup == null)
                return;
            if (m_parentGroup.IsDeleted)
                return;

            const string SoundGlassCollision = "6a45ba0b-5775-4ea8-8513-26008a17f873";
            const string SoundMetalCollision = "9e5c1297-6eed-40c0-825a-d9bcd86e3193";
            const string SoundStoneCollision = "9538f37c-456e-4047-81be-6435045608d4";
            const string SoundFleshCollision = "dce5fdd4-afe4-4ea1-822f-dd52cac46b08";
            const string SoundPlasticCollision = "0e24a717-b97e-4b77-9c94-b59a5a88b2da";
            const string SoundRubberCollision = "153c8bf7-fb89-4d89-b263-47e58b1b4774";
            const string SoundWoodCollision = "063c97d3-033a-4e9b-98d8-05c8074922cb";

            // play the sound.
            if (startedColliders.Count > 0 && CollisionSound != UUID.Zero && CollisionSoundVolume > 0.0f)
            {
                SendSound(CollisionSound.ToString(), CollisionSoundVolume, true, 0, 0);
            }
            else if (startedColliders.Count > 0)
            {
                switch (startedCollider.Type)
                {
                    case ActorTypes.Agent:
                        break; // Agents will play the sound so we don't

                    case ActorTypes.Ground:
                        SendSound(SoundWoodCollision, 1, true, 0, 0);
                        break; //Always play the click or thump sound when hitting ground

                    case ActorTypes.Prim:
                        switch (Material)
                        {
                            case (int) OpenMetaverse.Material.Flesh:
                                SendSound(SoundFleshCollision, 1, true, 0, 0);
                                break;
                            case (int) OpenMetaverse.Material.Glass:
                                SendSound(SoundGlassCollision, 1, true, 0, 0);
                                break;
                            case (int) OpenMetaverse.Material.Metal:
                                SendSound(SoundMetalCollision, 1, true, 0, 0);
                                break;
                            case (int) OpenMetaverse.Material.Plastic:
                                SendSound(SoundPlasticCollision, 1, true, 0, 0);
                                break;
                            case (int) OpenMetaverse.Material.Rubber:
                                SendSound(SoundRubberCollision, 1, true, 0, 0);
                                break;
                            case (int) OpenMetaverse.Material.Stone:
                                SendSound(SoundStoneCollision, 1, true, 0, 0);
                                break;
                            case (int) OpenMetaverse.Material.Wood:
                                SendSound(SoundWoodCollision, 1, true, 0, 0);
                                break;
                        }
                        break; //Play based on material type in prim2prim collisions

                    default:
                        break; //Unclear of what this object is, no sounds
                }
            }
            if (CollisionSprite != UUID.Zero && CollisionSoundVolume > 0.0f)
                // The collision volume isn't a mistake, its an SL feature/bug
            {
                // TODO: make a sprite!
            }
            if (((AggregateScriptEvents & scriptEvents.collision) != 0) ||
                ((AggregateScriptEvents & scriptEvents.collision_end) != 0) ||
                ((AggregateScriptEvents & scriptEvents.collision_start) != 0) ||
                ((AggregateScriptEvents & scriptEvents.land_collision_start) != 0) ||
                ((AggregateScriptEvents & scriptEvents.land_collision) != 0) ||
                ((AggregateScriptEvents & scriptEvents.land_collision_end) != 0) ||
                (CollisionSound != UUID.Zero) ||
                PassCollisions != 2)
            {
                if ((AggregateScriptEvents & scriptEvents.collision_start) != 0 ||
                    (AggregateScriptEvents & scriptEvents.collision) != 0)
                {
                    // do event notification
                    if (startedColliders.Count > 0)
                    {
                        ColliderArgs StartCollidingMessage = new ColliderArgs();
                        List<DetectedObject> colliding = new List<DetectedObject>();
                        foreach (uint localId in startedColliders)
                        {
                            if (localId != 0)
                            {
                                // always running this check because if the user deletes the object it would return a null reference.
                                if (m_parentGroup == null)
                                    return;

                                if (m_parentGroup.Scene == null)
                                    return;

                                ISceneChildEntity obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                                string data = "";
                                if (obj != null)
                                {
                                    if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.UUID.ToString()) ||
                                        m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.Name))
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this object
                                        if (found)
                                        {
                                            DetectedObject detobj = new DetectedObject
                                                                        {
                                                                            keyUUID = obj.UUID,
                                                                            nameStr = obj.Name,
                                                                            ownerUUID = obj.OwnerID,
                                                                            posVector = obj.AbsolutePosition,
                                                                            rotQuat = obj.GetWorldRotation(),
                                                                            velVector = obj.Velocity,
                                                                            colliderType = 0,
                                                                            groupUUID = obj.GroupID
                                                                        };
                                            colliding.Add(detobj);
                                        }
                                            //If it is 0, it is to not accept collisions from this object
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this object, so this other object will not work
                                        if (!found)
                                        {
                                            DetectedObject detobj = new DetectedObject
                                                                        {
                                                                            keyUUID = obj.UUID,
                                                                            nameStr = obj.Name,
                                                                            ownerUUID = obj.OwnerID,
                                                                            posVector = obj.AbsolutePosition,
                                                                            rotQuat = obj.GetWorldRotation(),
                                                                            velVector = obj.Velocity,
                                                                            colliderType = 0,
                                                                            groupUUID = obj.GroupID
                                                                        };
                                            colliding.Add(detobj);
                                        }
                                    }
                                }
                                else
                                {
                                    IScenePresence av = ParentGroup.Scene.GetScenePresence(localId);
                                    if (av != null)
                                    {
                                        if (av.LocalId == localId)
                                        {
                                            if (
                                                m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.UUID.ToString()) ||
                                                m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.Name))
                                            {
                                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,
                                                                                                                out data);
                                                //If it is 1, it is to accept ONLY collisions from this avatar
                                                if (found)
                                                {
                                                    DetectedObject detobj = new DetectedObject
                                                                                {
                                                                                    keyUUID = av.UUID,
                                                                                    nameStr = av.ControllingClient.Name,
                                                                                    ownerUUID = av.UUID,
                                                                                    posVector = av.AbsolutePosition,
                                                                                    rotQuat = av.Rotation,
                                                                                    velVector = av.Velocity,
                                                                                    colliderType = 0,
                                                                                    groupUUID =
                                                                                        av.ControllingClient
                                                                                          .ActiveGroupId
                                                                                };
                                                    colliding.Add(detobj);
                                                }
                                                    //If it is 0, it is to not accept collisions from this avatar
                                                else
                                                {
                                                }
                                            }
                                            else
                                            {
                                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,
                                                                                                                out data);
                                                //If it is 1, it is to accept ONLY collisions from this avatar, so this other avatar will not work
                                                if (!found)
                                                {
                                                    DetectedObject detobj = new DetectedObject
                                                                                {
                                                                                    keyUUID = av.UUID,
                                                                                    nameStr = av.ControllingClient.Name,
                                                                                    ownerUUID = av.UUID,
                                                                                    posVector = av.AbsolutePosition,
                                                                                    rotQuat = av.Rotation,
                                                                                    velVector = av.Velocity,
                                                                                    colliderType = 0,
                                                                                    groupUUID =
                                                                                        av.ControllingClient
                                                                                          .ActiveGroupId
                                                                                };
                                                    colliding.Add(detobj);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (colliding.Count > 0)
                        {
                            StartCollidingMessage.Colliders = colliding;
                            // always running this check because if the user deletes the object it would return a null reference.
                            if (m_parentGroup == null)
                                return;

                            if (m_parentGroup.Scene == null)
                                return;
                            //Always send to the prim it is occuring to
                            m_parentGroup.Scene.EventManager.TriggerScriptCollidingStart(this, StartCollidingMessage);
                            if ((UUID != ParentGroup.RootPart.UUID))
                            {
                                const int PASS_IF_NOT_HANDLED = 0;
                                const int PASS_ALWAYS = 1;
                                const int PASS_NEVER = 2;
                                if (PassCollisions == PASS_NEVER)
                                {
                                }
                                if (PassCollisions == PASS_ALWAYS)
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptCollidingStart(
                                        ParentGroup.RootPart, StartCollidingMessage);
                                }
                                else if (((ScriptEvents & scriptEvents.collision_start) == 0) &&
                                         PassCollisions == PASS_IF_NOT_HANDLED)
                                    //If no event in this prim, pass to parent
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptCollidingStart(
                                        ParentGroup.RootPart, StartCollidingMessage);
                                }
                            }
                        }
                    }
                }

                if ((AggregateScriptEvents & scriptEvents.collision) != 0)
                {
                    if (m_lastColliders.Count > 0)
                    {
                        ColliderArgs CollidingMessage = new ColliderArgs();
                        List<DetectedObject> colliding = new List<DetectedObject>();
                        foreach (uint localId in m_lastColliders)
                        {
                            if (localId != 0)
                            {
                                if (m_parentGroup == null)
                                    return;

                                if (m_parentGroup.Scene == null)
                                    return;

                                ISceneChildEntity obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                                string data = "";
                                if (obj != null)
                                {
                                    if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.UUID.ToString()) ||
                                        m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.Name))
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this object
                                        if (found)
                                        {
                                            DetectedObject detobj = new DetectedObject
                                                                        {
                                                                            keyUUID = obj.UUID,
                                                                            nameStr = obj.Name,
                                                                            ownerUUID = obj.OwnerID,
                                                                            posVector = obj.AbsolutePosition,
                                                                            rotQuat = obj.GetWorldRotation(),
                                                                            velVector = obj.Velocity,
                                                                            colliderType = 0,
                                                                            groupUUID = obj.GroupID
                                                                        };
                                            colliding.Add(detobj);
                                        }
                                            //If it is 0, it is to not accept collisions from this object
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this object, so this other object will not work
                                        if (!found)
                                        {
                                            DetectedObject detobj = new DetectedObject
                                                                        {
                                                                            keyUUID = obj.UUID,
                                                                            nameStr = obj.Name,
                                                                            ownerUUID = obj.OwnerID,
                                                                            posVector = obj.AbsolutePosition,
                                                                            rotQuat = obj.GetWorldRotation(),
                                                                            velVector = obj.Velocity,
                                                                            colliderType = 0,
                                                                            groupUUID = obj.GroupID
                                                                        };
                                            colliding.Add(detobj);
                                        }
                                    }
                                }
                                else
                                {
                                    IScenePresence av = ParentGroup.Scene.GetScenePresence(localId);
                                    if (av != null)
                                    {
                                        if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.UUID.ToString()) ||
                                            m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.Name))
                                        {
                                            bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                            //If it is 1, it is to accept ONLY collisions from this avatar
                                            if (found)
                                            {
                                                DetectedObject detobj = new DetectedObject
                                                                            {
                                                                                keyUUID = av.UUID,
                                                                                nameStr = av.ControllingClient.Name,
                                                                                ownerUUID = av.UUID,
                                                                                posVector = av.AbsolutePosition,
                                                                                rotQuat = av.Rotation,
                                                                                velVector = av.Velocity,
                                                                                colliderType = 0,
                                                                                groupUUID =
                                                                                    av.ControllingClient.ActiveGroupId
                                                                            };
                                                colliding.Add(detobj);
                                            }
                                                //If it is 0, it is to not accept collisions from this avatar
                                        }
                                        else
                                        {
                                            bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                            //If it is 1, it is to accept ONLY collisions from this avatar, so this other avatar will not work
                                            if (!found)
                                            {
                                                DetectedObject detobj = new DetectedObject
                                                                            {
                                                                                keyUUID = av.UUID,
                                                                                nameStr = av.ControllingClient.Name,
                                                                                ownerUUID = av.UUID,
                                                                                posVector = av.AbsolutePosition,
                                                                                rotQuat = av.Rotation,
                                                                                velVector = av.Velocity,
                                                                                colliderType = 0,
                                                                                groupUUID =
                                                                                    av.ControllingClient.ActiveGroupId
                                                                            };
                                                colliding.Add(detobj);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (colliding.Count > 0)
                        {
                            CollidingMessage.Colliders = colliding;
                            // always running this check because if the user deletes the object it would return a null reference.
                            if (m_parentGroup == null)
                                return;

                            if (m_parentGroup.Scene == null)
                                return;

                            m_parentGroup.Scene.EventManager.TriggerScriptColliding(this, CollidingMessage);

                            if ((UUID != ParentGroup.RootPart.UUID))
                            {
                                const int PASS_IF_NOT_HANDLED = 0;
                                const int PASS_ALWAYS = 1;
                                const int PASS_NEVER = 2;
                                if (PassCollisions == PASS_NEVER)
                                {
                                }
                                if (PassCollisions == PASS_ALWAYS)
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptColliding(ParentGroup.RootPart,
                                                                                            CollidingMessage);
                                }
                                else if (((ScriptEvents & scriptEvents.collision) == 0) &&
                                         PassCollisions == PASS_IF_NOT_HANDLED)
                                    //If no event in this prim, pass to parent
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptColliding(
                                        ParentGroup.RootPart, CollidingMessage);
                                }
                            }
                        }
                    }
                }

                if ((AggregateScriptEvents & scriptEvents.collision_end) != 0)
                {
                    if (endedColliders.Count > 0)
                    {
                        ColliderArgs EndCollidingMessage = new ColliderArgs();
                        List<DetectedObject> colliding = new List<DetectedObject>();
                        foreach (uint localId in endedColliders)
                        {
                            if (localId != 0)
                            {
                                // always running this check because if the user deletes the object it would return a null reference.
                                if (m_parentGroup == null)
                                    return;
                                if (m_parentGroup.Scene == null)
                                    return;
                                ISceneChildEntity obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                                string data = "";
                                if (obj != null)
                                {
                                    if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.UUID.ToString()) ||
                                        m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.Name))
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this object
                                        if (found)
                                        {
                                            DetectedObject detobj = new DetectedObject
                                                                        {
                                                                            keyUUID = obj.UUID,
                                                                            nameStr = obj.Name,
                                                                            ownerUUID = obj.OwnerID,
                                                                            posVector = obj.AbsolutePosition,
                                                                            rotQuat = obj.GetWorldRotation(),
                                                                            velVector = obj.Velocity,
                                                                            colliderType = 0,
                                                                            groupUUID = obj.GroupID
                                                                        };
                                            colliding.Add(detobj);
                                        }
                                            //If it is 0, it is to not accept collisions from this object
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this object, so this other object will not work
                                        if (!found)
                                        {
                                            DetectedObject detobj = new DetectedObject
                                                                        {
                                                                            keyUUID = obj.UUID,
                                                                            nameStr = obj.Name,
                                                                            ownerUUID = obj.OwnerID,
                                                                            posVector = obj.AbsolutePosition,
                                                                            rotQuat = obj.GetWorldRotation(),
                                                                            velVector = obj.Velocity,
                                                                            colliderType = 0,
                                                                            groupUUID = obj.GroupID
                                                                        };
                                            colliding.Add(detobj);
                                        }
                                    }
                                }
                                else
                                {
                                    IScenePresence av = ParentGroup.Scene.GetScenePresence(localId);
                                    if (av != null)
                                    {
                                        if (av.LocalId == localId)
                                        {
                                            if (
                                                m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.UUID.ToString()) ||
                                                m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.Name))
                                            {
                                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,
                                                                                                                out data);
                                                //If it is 1, it is to accept ONLY collisions from this avatar
                                                if (found)
                                                {
                                                    DetectedObject detobj = new DetectedObject
                                                                                {
                                                                                    keyUUID = av.UUID,
                                                                                    nameStr = av.ControllingClient.Name,
                                                                                    ownerUUID = av.UUID,
                                                                                    posVector = av.AbsolutePosition,
                                                                                    rotQuat = av.Rotation,
                                                                                    velVector = av.Velocity,
                                                                                    colliderType = 0,
                                                                                    groupUUID =
                                                                                        av.ControllingClient
                                                                                          .ActiveGroupId
                                                                                };
                                                    colliding.Add(detobj);
                                                }
                                                    //If it is 0, it is to not accept collisions from this avatar
                                                else
                                                {
                                                }
                                            }
                                            else
                                            {
                                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,
                                                                                                                out data);
                                                //If it is 1, it is to accept ONLY collisions from this avatar, so this other avatar will not work
                                                if (!found)
                                                {
                                                    DetectedObject detobj = new DetectedObject
                                                                                {
                                                                                    keyUUID = av.UUID,
                                                                                    nameStr = av.ControllingClient.Name,
                                                                                    ownerUUID = av.UUID,
                                                                                    posVector = av.AbsolutePosition,
                                                                                    rotQuat = av.Rotation,
                                                                                    velVector = av.Velocity,
                                                                                    colliderType = 0,
                                                                                    groupUUID =
                                                                                        av.ControllingClient
                                                                                          .ActiveGroupId
                                                                                };
                                                    colliding.Add(detobj);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (colliding.Count > 0)
                        {
                            EndCollidingMessage.Colliders = colliding;
                            // always running this check because if the user deletes the object it would return a null reference.
                            if (m_parentGroup == null)
                                return;

                            if (m_parentGroup.Scene == null)
                                return;

                            m_parentGroup.Scene.EventManager.TriggerScriptCollidingEnd(this, EndCollidingMessage);

                            if ((UUID != ParentGroup.RootPart.UUID))
                            {
                                const int PASS_IF_NOT_HANDLED = 0;
                                const int PASS_ALWAYS = 1;
                                const int PASS_NEVER = 2;
                                if (PassCollisions == PASS_NEVER)
                                {
                                }
                                if (PassCollisions == PASS_ALWAYS)
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptCollidingEnd(
                                        ParentGroup.RootPart, EndCollidingMessage);
                                }
                                else if (((ScriptEvents & scriptEvents.collision_end) == 0) &&
                                         PassCollisions == PASS_IF_NOT_HANDLED)
                                    //If no event in this prim, pass to parent
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptCollidingEnd(
                                        ParentGroup.RootPart, EndCollidingMessage);
                                }
                            }
                        }
                    }
                }
                if ((AggregateScriptEvents & scriptEvents.land_collision_start) != 0 ||
                    (AggregateScriptEvents & scriptEvents.land_collision) != 0)
                {
                    if (startedColliders.Count > 0)
                    {
                        ColliderArgs LandStartCollidingMessage = new ColliderArgs();
                        List<DetectedObject> colliding = (from localId in startedColliders
                                                          where localId == 0
                                                          select new DetectedObject
                                                                     {
                                                                         keyUUID = UUID.Zero,
                                                                         nameStr = "",
                                                                         ownerUUID = UUID.Zero,
                                                                         posVector =
                                                                             m_parentGroup.RootPart.AbsolutePosition,
                                                                         rotQuat = Quaternion.Identity,
                                                                         velVector = Vector3.Zero,
                                                                         colliderType = 0,
                                                                         groupUUID = UUID.Zero
                                                                     }).ToList();

                        if (colliding.Count > 0)
                        {
                            LandStartCollidingMessage.Colliders = colliding;
                            // always running this check because if the user deletes the object it would return a null reference.
                            if (m_parentGroup == null)
                                return;

                            if (m_parentGroup.Scene == null)
                                return;

                            m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingStart(this, LandStartCollidingMessage);

                            if ((UUID != ParentGroup.RootPart.UUID))
                            {
                                const int PASS_IF_NOT_HANDLED = 0;
                                const int PASS_ALWAYS = 1;
                                const int PASS_NEVER = 2;
                                if (PassCollisions == PASS_NEVER)
                                {
                                }
                                if (PassCollisions == PASS_ALWAYS)
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingStart(
                                        ParentGroup.RootPart, LandStartCollidingMessage);
                                }
                                else if (((ScriptEvents & scriptEvents.land_collision_start) == 0) &&
                                         PassCollisions == PASS_IF_NOT_HANDLED)
                                    //If no event in this prim, pass to parent
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingStart(
                                        ParentGroup.RootPart, LandStartCollidingMessage);
                                }
                            }
                        }
                    }
                }
                if ((AggregateScriptEvents & scriptEvents.land_collision) != 0)
                {
                    if (m_lastColliders.Count > 0)
                    {
                        ColliderArgs LandCollidingMessage = new ColliderArgs();
                        List<DetectedObject> colliding = new List<DetectedObject>();
                        foreach (uint localId in m_lastColliders)
                        {
                            if (localId == 0)
                            {
                                //Hope that all is left is ground!
                                DetectedObject detobj = new DetectedObject
                                                            {
                                                                keyUUID = UUID.Zero,
                                                                nameStr = "",
                                                                ownerUUID = UUID.Zero,
                                                                posVector = m_parentGroup.RootPart.AbsolutePosition,
                                                                rotQuat = Quaternion.Identity,
                                                                velVector = Vector3.Zero,
                                                                colliderType = 0,
                                                                groupUUID = UUID.Zero
                                                            };
                                colliding.Add(detobj);
                            }
                        }

                        if (colliding.Count > 0)
                        {
                            LandCollidingMessage.Colliders = colliding;
                            // always running this check because if the user deletes the object it would return a null reference.
                            if (m_parentGroup == null)
                                return;

                            if (m_parentGroup.Scene == null)
                                return;

                            m_parentGroup.Scene.EventManager.TriggerScriptLandColliding(this, LandCollidingMessage);

                            if ((UUID != ParentGroup.RootPart.UUID))
                            {
                                const int PASS_IF_NOT_HANDLED = 0;
                                const int PASS_ALWAYS = 1;
                                const int PASS_NEVER = 2;
                                if (PassCollisions == PASS_NEVER)
                                {
                                }
                                if (PassCollisions == PASS_ALWAYS)
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptLandColliding(
                                        ParentGroup.RootPart,
                                        LandCollidingMessage);
                                }
                                else if (((ScriptEvents & scriptEvents.land_collision) == 0) &&
                                         PassCollisions == PASS_IF_NOT_HANDLED)
                                    //If no event in this prim, pass to parent
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptLandColliding(
                                        ParentGroup.RootPart, LandCollidingMessage);
                                }
                            }
                        }
                    }
                }
                if ((AggregateScriptEvents & scriptEvents.land_collision_end) != 0)
                {
                    if (endedColliders.Count > 0)
                    {
                        ColliderArgs LandEndCollidingMessage = new ColliderArgs();
                        List<DetectedObject> colliding = (from localId in startedColliders
                                                          where localId == 0
                                                          select new DetectedObject
                                                                     {
                                                                         keyUUID = UUID.Zero,
                                                                         nameStr = "",
                                                                         ownerUUID = UUID.Zero,
                                                                         posVector =
                                                                             m_parentGroup.RootPart.AbsolutePosition,
                                                                         rotQuat = Quaternion.Identity,
                                                                         velVector = Vector3.Zero,
                                                                         colliderType = 0,
                                                                         groupUUID = UUID.Zero
                                                                     }).ToList();

                        if (colliding.Count > 0)
                        {
                            LandEndCollidingMessage.Colliders = colliding;
                            // always running this check because if the user deletes the object it would return a null reference.
                            if (m_parentGroup == null)
                                return;

                            if (m_parentGroup.Scene == null)
                                return;

                            m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingEnd(this, LandEndCollidingMessage);

                            if ((UUID != ParentGroup.RootPart.UUID))
                            {
                                const int PASS_IF_NOT_HANDLED = 0;
                                const int PASS_ALWAYS = 1;
                                const int PASS_NEVER = 2;
                                if (PassCollisions == PASS_NEVER)
                                {
                                }
                                if (PassCollisions == PASS_ALWAYS)
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingEnd(
                                        ParentGroup.RootPart, LandEndCollidingMessage);
                                }
                                else if (((ScriptEvents & scriptEvents.land_collision_end) == 0) &&
                                         PassCollisions == PASS_IF_NOT_HANDLED)
                                    //If no event in this prim, pass to parent
                                {
                                    m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingEnd(
                                        ParentGroup.RootPart, LandEndCollidingMessage);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void PhysicsOutOfBounds(Vector3 pos)
        {
            MainConsole.Instance.Error("[Physics]: Physical object " + Name + ", localID " + LocalId +
                                       " went out of bounds at " + pos + ".  Stopping at " + AbsolutePosition +
                                       " and making non-physical.");
            lock (ParentGroup.Scene.PhysicsReturns)
            {
                if (!ParentGroup.Scene.PhysicsReturns.Contains(ParentGroup))
                    ParentGroup.Scene.PhysicsReturns.Add(ParentGroup);
            }
        }

        public virtual void PhysicsRequestingTerseUpdate()
        {
            if (PhysActor != null)
            {
//                Vector3 newpos = new Vector3(PhysActor.Position.GetBytes(), 0);
                m_parentGroup.SetAbsolutePosition(false, PhysActor.Position);
                //m_parentGroup.RootPart.m_groupPosition = newpos;
            }
            ScheduleUpdate(PrimUpdateFlags.TerseUpdate);
        }

        public void RemoveScriptEvents(UUID scriptid)
        {
            lock (m_scriptEvents)
            {
                if (m_scriptEvents.ContainsKey(scriptid))
                {
                    scriptEvents oldparts = scriptEvents.None;
                    oldparts = m_scriptEvents[scriptid];

                    // remove values from aggregated script events
                    AggregateScriptEvents &= ~oldparts;
                    m_scriptEvents.Remove(scriptid);
                    aggregateScriptEvents();
                }
            }
        }

        /// <summary>
        ///     Resize this part.
        /// </summary>
        /// <param name="scale"></param>
        public void Resize(Vector3 scale)
        {
            Scale = scale;

            ParentGroup.HasGroupChanged = true;
            ScheduleUpdate(PrimUpdateFlags.Shape);
        }

        public void stopLookAt()
        {
            APIDEnabled = false;
        }

        /// <summary>
        ///     Check to see whether the given flags make it a terse update
        /// </summary>
        /// <param name="flags"></param>
        /// <returns></returns>
        bool IsTerse(PrimUpdateFlags flags)
        {
            return flags.HasFlag((PrimUpdateFlags.TerseUpdate))
                   && !flags.HasFlag((PrimUpdateFlags.AttachmentPoint | PrimUpdateFlags.ClickAction |
                                      PrimUpdateFlags.CollisionPlane | PrimUpdateFlags.ExtraData |
                                      PrimUpdateFlags.FindBest | PrimUpdateFlags.FullUpdate |
                                      PrimUpdateFlags.Joint | PrimUpdateFlags.Material | PrimUpdateFlags.MediaURL |
                                      PrimUpdateFlags.NameValue |
                                      PrimUpdateFlags.ParentID | PrimUpdateFlags.Particles | PrimUpdateFlags.PrimData |
                                      PrimUpdateFlags.PrimFlags |
                                      PrimUpdateFlags.ScratchPad | PrimUpdateFlags.Shape | PrimUpdateFlags.Sound |
                                      PrimUpdateFlags.Text |
                                      PrimUpdateFlags.TextureAnim | PrimUpdateFlags.Textures)) &&
                   !flags.HasFlag(PrimUpdateFlags.ForcedFullUpdate);
        }

        public void SetAttachmentPoint(int attachmentPoint)
        {
            //Update the saved if needed
            if (attachmentPoint == 0 && AttachmentPoint != 0)
            {
                SavedAttachedPos = AttachedPos;
                SavedAttachmentPoint = AttachmentPoint;
            }

            AttachmentPoint = attachmentPoint;

            IsAttachment = attachmentPoint != 0;

            // save the attachment point.
            //if (AttachmentPoint != 0)
            //{
            m_shape.State = (byte) attachmentPoint;
            //}
        }

        public void SetPhysActorCameraPos(Quaternion CameraRotation)
        {
            if (PhysActor != null)
            {
                PhysActor.SetCameraPos(CameraRotation);
            }
        }

        /// <summary>
        ///     Tell us if this object has cut, hollow, dimple, and other factors affecting the number of faces
        /// </summary>
        /// <param name="primType"></param>
        /// <param name="shape"></param>
        /// <param name="hasCut"></param>
        /// <param name="hasHollow"></param>
        /// <param name="hasDimple"></param>
        /// <param name="hasProfileCut"></param>
        protected static void HasCutHollowDimpleProfileCut(PrimType primType, PrimitiveBaseShape shape, out bool hasCut,
                                                           out bool hasHollow,
                                                           out bool hasDimple, out bool hasProfileCut)
        {
            if (primType == PrimType.BOX
                ||
                primType == PrimType.CYLINDER
                ||
                primType == PrimType.PRISM)

                hasCut = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0);
            else
                hasCut = (shape.PathBegin > 0) || (shape.PathEnd > 0);

            hasHollow = shape.ProfileHollow > 0;
            hasDimple = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0); // taken from llSetPrimitiveParms
            hasProfileCut = hasDimple; // is it the same thing?
        }

        public void SetGroup(UUID groupID)
        {
            _groupID = groupID;
        }

        public void SetPhysicsAxisRotation()
        {
            if (PhysActor != null)
                PhysActor.LockAngularMotion(RotationAxis);
        }

        /// <summary>
        ///     Serialize this part to xml.
        /// </summary>
        /// <param name="xmlWriter"></param>
        public void ToXml(XmlTextWriter xmlWriter)
        {
            SceneEntitySerializer.SceneObjectSerializer.ToXmlFormat(this, xmlWriter);
        }

        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            m_shape.ReadInUpdateExtraParam(type, inUse, data);

            if (type == 0x30)
            {
                if (m_shape.SculptEntry && m_shape.SculptTexture != UUID.Zero)
                    m_parentGroup.Scene.AssetService.Get(m_shape.SculptTexture.ToString(), true, AssetReceived);
            }
            ValidpartOOB = false;
            ParentGroup.HasGroupChanged = true;
            ScheduleUpdate(PrimUpdateFlags.Shape);
        }

        public void UpdateGroupPosition(Vector3 pos)
        {
            if ((pos.X != GroupPosition.X) ||
                (pos.Y != GroupPosition.Y) ||
                (pos.Z != GroupPosition.Z))
            {
//                Vector3 newPos = new Vector3(pos.X, pos.Y, pos.Z);
                FixGroupPosition(pos, false);
                ScheduleTerseUpdate();
            }
        }

        public void UpdatePermissions(UUID agentID, byte field, uint localID, uint mask, byte addRemTF)
        {
            bool set = addRemTF == 1;
            bool god = m_parentGroup.Scene.Permissions.IsGod(agentID);

            uint baseMask = _baseMask;
            if (god)
                baseMask = 0x7ffffff0;

            // Are we the owner?
            if (m_parentGroup.Scene.Permissions.CanEditObject(UUID, agentID))
            {
                uint exportPermission = (1 << 30);
                if ((mask & exportPermission) == exportPermission)
                {
                    //Only the creator can set export permissions
                    if (CreatorID != agentID)
                        mask &= exportPermission;
                }

                switch (field)
                {
                    case 1:
                        if (god)
                        {
                            _baseMask = ApplyMask(_baseMask, set, mask);
                            Inventory.ApplyGodPermissions(_baseMask);
                        }

                        break;
                    case 2:
                        _ownerMask = ApplyMask(_ownerMask, set, mask) &
                                     baseMask;
                        break;
                    case 4:
                        _groupMask = ApplyMask(_groupMask, set, mask) &
                                     baseMask;
                        break;
                    case 8:
                        _everyoneMask = ApplyMask(_everyoneMask, set, mask) &
                                        baseMask;
                        break;
                    case 16:
                        _nextOwnerMask = ApplyMask(_nextOwnerMask, set, mask) &
                                         baseMask;
                        // Prevent the client from creating no mod, no copy
                        // objects
                        if ((_nextOwnerMask & (uint) PermissionMask.Copy) == 0)
                            _nextOwnerMask |= (uint) PermissionMask.Transfer;

                        _nextOwnerMask |= (uint) PermissionMask.Move;

                        break;
                }
                ParentGroup.ScheduleGroupUpdate(PrimUpdateFlags.PrimFlags);

                SendObjectPropertiesToClient(agentID);
            }
        }

        /// <summary>
        ///     Update the texture entry for this part.
        /// </summary>
        /// <param name="textureEntry"></param>
        /// <param name="sendChangedEvent"></param>
        public void UpdateTextureEntry(byte[] textureEntry, bool sendChangedEvent)
        {
            Primitive.TextureEntry newTex = new Primitive.TextureEntry(textureEntry, 0, textureEntry.Length);
            Primitive.TextureEntry oldTex = Shape.Textures;

            Changed changeFlags = 0;

            Primitive.TextureEntryFace fallbackNewFace = newTex.DefaultTexture;
            Primitive.TextureEntryFace fallbackOldFace = oldTex.DefaultTexture;

            // On Incoming packets, sometimes newText.DefaultTexture is null.  The assumption is that all 
            // other prim-sides are set, but apparently that's not always the case.  Lets assume packet/data corruption at this point.
            if (fallbackNewFace == null)
            {
                fallbackNewFace = new Primitive.TextureEntry(Util.BLANK_TEXTURE_UUID).CreateFace(0);
                newTex.DefaultTexture = fallbackNewFace;
            }
            if (fallbackOldFace == null)
            {
                fallbackOldFace = new Primitive.TextureEntry(Util.BLANK_TEXTURE_UUID).CreateFace(0);
                oldTex.DefaultTexture = fallbackOldFace;
            }

            bool otherFieldsChanged = false;

            for (int i = 0; i < GetNumberOfSides(); i++)
            {

                Primitive.TextureEntryFace newFace = newTex.DefaultTexture;
                Primitive.TextureEntryFace oldFace = oldTex.DefaultTexture;

                if (oldTex.FaceTextures[i] != null)
                    oldFace = oldTex.FaceTextures[i];
                if (newTex.FaceTextures[i] != null)
                    newFace = newTex.FaceTextures[i];

                Color4 oldRGBA = oldFace.RGBA;
                Color4 newRGBA = newFace.RGBA;

                if (oldRGBA.R != newRGBA.R ||
                    oldRGBA.G != newRGBA.G ||
                    oldRGBA.B != newRGBA.B ||
                    oldRGBA.A != newRGBA.A)
                    changeFlags |= Changed.COLOR;

                if (oldFace.TextureID != newFace.TextureID)
                    changeFlags |= Changed.TEXTURE;

                // Max change, skip the rest of testing
                if (changeFlags == (Changed.TEXTURE | Changed.COLOR))
                    break;

                if (!otherFieldsChanged)
                {
                    if (oldFace.Bump != newFace.Bump) otherFieldsChanged = true;
                    if (oldFace.Fullbright != newFace.Fullbright) otherFieldsChanged = true;
                    if (oldFace.Glow != newFace.Glow) otherFieldsChanged = true;
                    if (oldFace.MediaFlags != newFace.MediaFlags) otherFieldsChanged = true;
                    if (oldFace.OffsetU != newFace.OffsetU) otherFieldsChanged = true;
                    if (oldFace.OffsetV != newFace.OffsetV) otherFieldsChanged = true;
                    if (oldFace.RepeatU != newFace.RepeatU) otherFieldsChanged = true;
                    if (oldFace.RepeatV != newFace.RepeatV) otherFieldsChanged = true;
                    if (oldFace.Rotation != newFace.Rotation) otherFieldsChanged = true;
                    if (oldFace.Shiny != newFace.Shiny) otherFieldsChanged = true;
                    if (oldFace.TexMapType != newFace.TexMapType) otherFieldsChanged = true;
                }
            }
            if (changeFlags == 0 && !otherFieldsChanged)
                return; //They are the same

            Shape.TextureEntry = textureEntry;

            if ((changeFlags & Changed.COLOR) == Changed.COLOR && sendChangedEvent) 
                TriggerScriptChangedEvent(Changed.COLOR);
            if ((changeFlags & Changed.TEXTURE) == Changed.TEXTURE && sendChangedEvent)
                TriggerScriptChangedEvent(Changed.TEXTURE);
            ParentGroup.HasGroupChanged = true;
            ScheduleUpdate(PrimUpdateFlags.FullUpdate);
        }

        public override string ToString()
        {
            return String.Format("{0} {1} linkNum {3} (parent {2}))", Name, UUID, ParentGroup, LinkNum);
        }

        #endregion Public Methods

        public void ApplyPermissions(uint permissions)
        {
            _ownerMask = permissions;
        }

        void UpdateLookAt()
        {
            try
            {
                //PID movement
                // Has to be physical (works with phantom too)
                if (PIDActive && ((Flags & PrimFlags.Physics) != 0))
                {
                    Vector3 _target_velocity =
                        new Vector3(
                            (PIDTarget.X - AbsolutePosition.X) * (1f / PIDTau),
                            (PIDTarget.Y - AbsolutePosition.Y) * (1f / PIDTau),
                            (PIDTarget.Z - AbsolutePosition.Z) * (1f / PIDTau)
                            );
                    if (PIDTarget.ApproxEquals(AbsolutePosition, 0.1f))
                    {
                        ParentGroup.Velocity = Vector3.Zero;
                        AngularVelocity = Vector3.Zero;
                        if (PhysActor != null)
                            PhysActor.ClearVelocity();
                        ParentGroup.SetAbsolutePosition(true, PIDTarget);
                    }
                    else
                    {
                        Velocity = _target_velocity;
                        AngularVelocity = Vector3.Zero;
                        if (PhysActor != null)
                            PhysActor.ForceSetRotVelocity(Vector3.Zero);
                    }
                }
                else if (PIDHoverActive)
                {
                    Vector3 _target_velocity;
                    ITerrainChannel terrain = ParentGroup.Scene.RequestModuleInterface<ITerrainChannel>();
                    if (terrain == null)
                        return;
                    float groundHeight =
                        terrain[(int) ParentGroup.AbsolutePosition.X, (int) ParentGroup.AbsolutePosition.Y];
                    switch (PIDHoverType)
                    {
                        case PIDHoverType.Ground:
                            _target_velocity =
                                new Vector3(
                                    0, 0, ((groundHeight + PIDHoverHeight) - m_initialPIDLocation.Z)*(PIDTau)
                                    );
                            break;
                        case PIDHoverType.GroundAndWater:
                            if (ParentGroup.Scene.RegionInfo.RegionSettings.WaterHeight < groundHeight)
                                groundHeight = (float) ParentGroup.Scene.RegionInfo.RegionSettings.WaterHeight;
                            _target_velocity =
                                new Vector3(
                                    0, 0, ((groundHeight + PIDHoverHeight) - m_initialPIDLocation.Z)*(PIDTau)
                                    );
                            break;
                        default:
                            return;
                    }
                    Velocity = _target_velocity;
                }
                if (APIDEnabled)
                {
                    PhysicsActor pa = ParentGroup.RootPart.PhysActor;
                    if (pa == null || !pa.IsPhysical || APIDStrength < 0.04)
                    {
                        StopLookAt();
                        return;
                    }

                    Quaternion currRot = GetWorldRotation();
                    currRot.Normalize();

                    // difference between current orientation and desired orientation
                    Quaternion dR = currRot / APIDTarget;

                    // find axis and angle of rotation to rotate to desired orientation
                    Vector3 axis = Vector3.UnitX;
                    
                    float angle;
                    dR.GetAxisAngle(out axis, out angle);
                    axis = axis * currRot;

                    // clamp strength to avoid overshoot
                    float strength = 1.0f / APIDStrength;
                    if (strength > 1.0) strength = 1.0f;

                    // set angular velocity to rotate to desired orientation
                    // with velocity proportional to strength and angle
                    AngularVelocity = axis * angle * strength * (float)Math.PI;
                }
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Error("[Physics] " + ex);
            }
        }

        internal void TriggerScriptMovingStartEvent()
        {
            if ((AggregateScriptEvents & scriptEvents.moving_start) == 0)
                return;
            if (m_parentGroup != null && m_parentGroup.Scene != null && m_parentGroup.Scene.EventManager != null)
                m_parentGroup.Scene.EventManager.TriggerOnScriptMovingStartEvent(this);
        }

        internal void TriggerScriptMovingEndEvent()
        {
            if ((AggregateScriptEvents & scriptEvents.moving_end) == 0)
                return;
            if (m_parentGroup != null && m_parentGroup.Scene != null && m_parentGroup.Scene.EventManager != null)
                m_parentGroup.Scene.EventManager.TriggerOnScriptMovingEndEvent(this);
        }
    }
}