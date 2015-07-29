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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using ProtoBuf;
using WhiteCore.Framework.ClientInterfaces;

namespace WhiteCore.Framework.SceneInfo
{
    [Serializable, ProtoContract(UseProtoMembersOnly = false)]
    public class RegionSettings
    {
        #region Delegates

        public delegate void SaveDelegate(RegionSettings rs);

        #endregion

        /// <value>
        ///     These appear to be terrain textures that are shipped with the client.
        /// </value>
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_1 = new UUID("b8d3965a-ad78-bf43-699b-bff8eca6c975");
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_2 = new UUID("abb783e6-3e93-26c0-248a-247666855da3");
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_3 = new UUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_4 = new UUID("beb169c7-11ea-fff2-efe5-0f24dc881df2");

        int m_AgentLimit = 40;
        bool m_AllowLandJoinDivide = true;
        bool m_AllowLandResell = true;
        UUID m_Covenant = UUID.Zero;
        double m_Elevation1NE = 10;
        double m_Elevation1NW = 10;
        double m_Elevation1SE = 10;
        double m_Elevation1SW = 10;
        double m_Elevation2NE = 60;
        double m_Elevation2NW = 60;
        double m_Elevation2SE = 60;
        double m_Elevation2SW = 60;
        OSDMap m_Generic = new OSDMap();
        String m_LoadedCreationID = String.Empty;
        double m_ObjectBonus = 1.0;

        UUID m_RegionUUID = UUID.Zero;
        double m_TerrainLowerLimit = -100;
        double m_TerrainRaiseLimit = 100;
        UUID m_TerrainTexture1 = UUID.Zero;
        UUID m_TerrainTexture2 = UUID.Zero;
        UUID m_TerrainTexture3 = UUID.Zero;
        UUID m_TerrainTexture4 = UUID.Zero;
        bool m_UseEstateSun = true;
        double m_WaterHeight = 20;
        Telehub m_telehub = new Telehub ();

        [ProtoMember(1)]
        public UUID RegionUUID
        {
            get { return m_RegionUUID; }
            set { m_RegionUUID = value; }
        }

        [ProtoMember(2)]
        public bool BlockTerraform { get; set; }

        [ProtoMember(3)]
        public bool BlockFly { get; set; }

        [ProtoMember(4)]
        public bool AllowDamage { get; set; }

        [ProtoMember(5)]
        public bool RestrictPushing { get; set; }

        [ProtoMember(6)]
        public bool AllowLandResell
        {
            get { return m_AllowLandResell; }
            set { m_AllowLandResell = value; }
        }

        [ProtoMember(7)]
        public bool AllowLandJoinDivide
        {
            get { return m_AllowLandJoinDivide; }
            set { m_AllowLandJoinDivide = value; }
        }

        [ProtoMember(8)]
        public bool BlockShowInSearch { get; set; }

        [ProtoMember(9)]
        public int AgentLimit
        {
            get { return m_AgentLimit; }
            set { m_AgentLimit = value; }
        }

        [ProtoMember(10)]
        public double ObjectBonus
        {
            get { return m_ObjectBonus; }
            set { m_ObjectBonus = value; }
        }

        [ProtoMember(11)]
        public int Maturity { get; set; }

        [ProtoMember(12)]
        public bool DisableScripts { get; set; }

        [ProtoMember(13)]
        public bool DisableCollisions { get; set; }

        [ProtoMember(14)]
        public bool DisablePhysics { get; set; }

        [ProtoMember(15)]
        public int MinimumAge { get; set; }

        [ProtoMember(16)]
        public UUID TerrainTexture1
        {
            get { return m_TerrainTexture1; }
            set { m_TerrainTexture1 = value == UUID.Zero ? DEFAULT_TERRAIN_TEXTURE_1 : value; }
        }

        [ProtoMember(17)]
        public UUID TerrainTexture2
        {
            get { return m_TerrainTexture2; }
            set { m_TerrainTexture2 = value == UUID.Zero ? DEFAULT_TERRAIN_TEXTURE_2 : value; }
        }

        [ProtoMember(18)]
        public UUID TerrainTexture3
        {
            get { return m_TerrainTexture3; }
            set { m_TerrainTexture3 = value == UUID.Zero ? DEFAULT_TERRAIN_TEXTURE_3 : value; }
        }

        [ProtoMember(19)]
        public UUID TerrainTexture4
        {
            get { return m_TerrainTexture4; }
            set { m_TerrainTexture4 = value == UUID.Zero ? DEFAULT_TERRAIN_TEXTURE_4 : value; }
        }

        [ProtoMember(20)]
        public double Elevation1NW
        {
            get { return m_Elevation1NW; }
            set { m_Elevation1NW = value; }
        }

        [ProtoMember(21)]
        public double Elevation2NW
        {
            get { return m_Elevation2NW; }
            set { m_Elevation2NW = value; }
        }

        [ProtoMember(22)]
        public double Elevation1NE
        {
            get { return m_Elevation1NE; }
            set { m_Elevation1NE = value; }
        }

        [ProtoMember(23)]
        public double Elevation2NE
        {
            get { return m_Elevation2NE; }
            set { m_Elevation2NE = value; }
        }

        [ProtoMember(24)]
        public double Elevation1SE
        {
            get { return m_Elevation1SE; }
            set { m_Elevation1SE = value; }
        }

        [ProtoMember(25)]
        public double Elevation2SE
        {
            get { return m_Elevation2SE; }
            set { m_Elevation2SE = value; }
        }

        [ProtoMember(26)]
        public double Elevation1SW
        {
            get { return m_Elevation1SW; }
            set { m_Elevation1SW = value; }
        }

        [ProtoMember(27)]
        public double Elevation2SW
        {
            get { return m_Elevation2SW; }
            set { m_Elevation2SW = value; }
        }

        [ProtoMember(28)]
        public double WaterHeight
        {
            get { return m_WaterHeight; }
            set { m_WaterHeight = value; }
        }

        [ProtoMember(29)]
        public double TerrainRaiseLimit
        {
            get { return m_TerrainRaiseLimit; }
            set { m_TerrainRaiseLimit = value; }
        }

        [ProtoMember(30)]
        public double TerrainLowerLimit
        {
            get { return m_TerrainLowerLimit; }
            set { m_TerrainLowerLimit = value; }
        }

        [ProtoMember(31)]
        public bool UseEstateSun
        {
            get { return m_UseEstateSun; }
            set { m_UseEstateSun = value; }
        }

        [ProtoMember(32)]
        public bool Sandbox { get; set; }

        [ProtoMember(33)]
        public Vector3 SunVector { get; set; }

        /// <summary>
        ///     Terrain (and probably) prims asset ID for the map
        /// </summary>
        [ProtoMember(34)]
        public UUID TerrainImageID { get; set; }

        /// <summary>
        ///     Displays which lands are for sale (and for auction)
        /// </summary>
        [ProtoMember(35)]
        public UUID ParcelMapImageID { get; set; }

        /// <summary>
        ///     Terrain only asset ID for the map
        /// </summary>
        [ProtoMember(36)]
        public UUID TerrainMapImageID { get; set; }

        /// <summary>
        ///     Time that the map tile was last created
        /// </summary>
        [ProtoMember(37)]
        public DateTime TerrainMapLastRegenerated { get; set; }

        [ProtoMember(38)]
        public bool FixedSun { get; set; }

        [ProtoMember(39)]
        public double SunPosition { get; set; }

        [ProtoMember(40)]
        public UUID Covenant
        {
            get { return m_Covenant; }
            set { m_Covenant = value; }
        }

        [ProtoMember(41)]
        public int CovenantLastUpdated { get; set; }

        [ProtoMember(42)]
        public int LoadedCreationDateTime { get; set; }

        public String LoadedCreationDate
        {
            get
            {
                TimeSpan ts = new TimeSpan(0, 0, LoadedCreationDateTime);
                DateTime stamp = new DateTime(1970, 1, 1) + ts;
                return stamp.ToLongDateString();
            }
        }

        public String LoadedCreationTime
        {
            get
            {
                TimeSpan ts = new TimeSpan(0, 0, LoadedCreationDateTime);
                DateTime stamp = new DateTime(1970, 1, 1) + ts;
                return stamp.ToLongTimeString();
            }
        }

        [ProtoMember(44)]
        public String LoadedCreationID
        {
            get { return m_LoadedCreationID; }
            set { m_LoadedCreationID = value; }
        }

//       [ProtoMember(45)]
        public Telehub TeleHub
        {
            get { return m_telehub ?? (m_telehub = new Telehub()); }
            set { m_telehub = value; }
        }

        public void AddGeneric(string key, OSD value)
        {
            m_Generic[key] = value;
        }

        public void RemoveGeneric(string key)
        {
            if (m_Generic.ContainsKey(key))
                m_Generic.Remove(key);
        }

        public OSD GetGeneric(string key)
        {
            OSD value;
            m_Generic.TryGetValue(key, out value);
            return value;
        }

        public OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();

            map["AgentLimit"] = AgentLimit;
            map["AllowDamage"] = AllowDamage;
            map["AllowLandJoinDivide"] = AllowLandJoinDivide;
            map["AllowLandResell"] = AllowLandResell;
            map["BlockFly"] = BlockFly;
            map["BlockShowInSearch"] = BlockShowInSearch;
            map["BlockTerraform"] = BlockTerraform;
            map["Covenant"] = Covenant;
            map["CovenantLastUpdated"] = CovenantLastUpdated;
            map["DisableCollisions"] = DisableCollisions;
            map["DisablePhysics"] = DisablePhysics;
            map["DisableScripts"] = DisableScripts;
            map["Elevation1NE"] = Elevation1NE;
            map["Elevation1NW"] = Elevation1NW;
            map["Elevation1SE"] = Elevation1SE;
            map["Elevation1SW"] = Elevation1SW;
            map["Elevation2NE"] = Elevation2NE;
            map["Elevation2NW"] = Elevation2NW;
            map["Elevation2SE"] = Elevation2SE;
            map["Elevation2SW"] = Elevation2SW;
            map["FixedSun"] = FixedSun;
            map["LoadedCreationDateTime"] = LoadedCreationDateTime;
            map["LoadedCreationID"] = LoadedCreationID;
            map["Maturity"] = Maturity;
            map["MinimumAge"] = MinimumAge;
            map["ObjectBonus"] = ObjectBonus;
            map["RegionUUID"] = RegionUUID;
            map["RestrictPushing"] = RestrictPushing;
            map["Sandbox"] = Sandbox;
            map["SunPosition"] = SunPosition;
            map["SunVector"] = SunVector;
            map["TerrainImageID"] = TerrainImageID;
            map["ParcelMapImageID"] = ParcelMapImageID;
            map["TerrainLowerLimit"] = TerrainLowerLimit;
            map["TerrainMapImageID"] = TerrainMapImageID;
            map["TerrainMapLastRegenerated"] = TerrainMapLastRegenerated;
            map["TerrainRaiseLimit"] = TerrainRaiseLimit;
            map["TerrainTexture1"] = TerrainTexture1;
            map["TerrainTexture2"] = TerrainTexture2;
            map["TerrainTexture3"] = TerrainTexture3;
            map["TerrainTexture4"] = TerrainTexture4;
            map["UseEstateSun"] = UseEstateSun;
            map["WaterHeight"] = WaterHeight;
            if (TeleHub != null)
                map["Telehub"] = TeleHub.ToOSD();

            return map;
        }

        public void FromOSD(OSDMap map)
        {
            AgentLimit = map["AgentLimit"];
            AllowLandJoinDivide = map["AllowLandJoinDivide"];
            AllowLandResell = map["AllowLandResell"];
            BlockFly = map["BlockFly"];
            BlockShowInSearch = map["BlockShowInSearch"];
            BlockTerraform = map["BlockTerraform"];
            Covenant = map["Covenant"];
            CovenantLastUpdated = map["CovenantLastUpdated"];
            DisableCollisions = map["DisableCollisions"];
            DisablePhysics = map["DisablePhysics"];
            DisableScripts = map["DisableScripts"];
            Elevation1NE = map["Elevation1NE"];
            Elevation1NW = map["Elevation1NW"];
            Elevation1SE = map["Elevation1SE"];
            Elevation1SW = map["Elevation1SW"];
            Elevation2NE = map["Elevation2NE"];
            Elevation2NW = map["Elevation2NW"];
            Elevation2SE = map["Elevation2SE"];
            Elevation2SW = map["Elevation2SW"];
            FixedSun = map["FixedSun"];
            LoadedCreationDateTime = map["LoadedCreationDateTime"];
            LoadedCreationID = map["LoadedCreationID"];
            Maturity = map["Maturity"];
            MinimumAge = map["MinimumAge"];
            ObjectBonus = map["ObjectBonus"];
            RegionUUID = map["RegionUUID"];
            RestrictPushing = map["RestrictPushing"];
            Sandbox = map["Sandbox"];
            SunPosition = map["SunPosition"];
            SunVector = map["SunVector"];
            TerrainImageID = map["TerrainImageID"];
            TerrainMapImageID = map["TerrainMapImageID"];
            TerrainMapLastRegenerated = map["TerrainMapLastRegenerated"];
            ParcelMapImageID = map["ParcelMapImageID"];
            TerrainLowerLimit = map["TerrainLowerLimit"];
            TerrainRaiseLimit = map["TerrainRaiseLimit"];
            TerrainTexture1 = map["TerrainTexture1"];
            TerrainTexture2 = map["TerrainTexture2"];
            TerrainTexture3 = map["TerrainTexture3"];
            TerrainTexture4 = map["TerrainTexture4"];
            UseEstateSun = map["UseEstateSun"];
            WaterHeight = map["WaterHeight"];
            if (map.ContainsKey ("TeleHub"))
            {
                TeleHub = new Telehub ();
                TeleHub.FromOSD ((OSDMap)map ["Telehub"]);
            }

        }
    }
}