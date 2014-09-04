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


namespace WhiteCore.Framework.Utilities
{
    public class Constants
    {

        // some predefined folders
        public const string DEFAULT_CONFIG_DIR = "../Config";
        public const string DEFAULT_DATA_DIR = "../Data";
        public const string DEFAULT_ASSETCACHE_DIR = DEFAULT_DATA_DIR+"/AssetCache";
        public const string DEFAULT_SCRIPTENGINE_DIR = DEFAULT_DATA_DIR+"/ScriptEngines";
        public const string DEFAULT_FILEASSETS_DIR = DEFAULT_DATA_DIR+"/FileAssets";
        public const string DEFAULT_AVATARARCHIVE_DIR = DEFAULT_DATA_DIR+"/AvatarArchives";
        public const string DEFAULT_OARARCHIVE_DIR = DEFAULT_DATA_DIR + "/OarFiles";
        public const string DEFAULT_USERINVENTORY_DIR = DEFAULT_DATA_DIR+"/UserArchives";

        public const int RegionSize = 256;
        public const int RegionHeight = 10000;
        public const byte TerrainPatchSize = 16;
        public const float TerrainCompression = 100.0f;
        public const int MinRegionSize = 16;

		public const int SystemUserCount = 2;
        public const string LibraryOwner = "11111111-1111-0000-0000-000100bba000";
        public const string LibraryRootFolderID = "00000112-000f-0000-0000-000100bba000"; 

		public const string RealEstateOwnerUUID = "3d6181b0-6a4b-97ef-18d8-722652995cf1";
		public const string RealEstateOwnerName = "RealEstate Owner";
        public const string SystemEstateName = "WhiteCore Estate";
        public const int SystemEstateID = 1;

		public const string RealEstateGroupUUID = "dc7b21cd-3c89-fcaa-31c8-25f9ffd224cd";
		public const string RealEstateGroupName = "Maintenance";


        // user levels
        public const int USER_DISABLED = -2;
        public const int USER_BANNED = -1;
        public const int USER_NORMAL = 0;
        public const int USER_GOD_LIKE = 1;     //?? bit low, are some other levels needed??
        public const int USER_GOD_CUSTOMER_SERVICE = 100;
        public const int USER_GOD_LIASON = 150;
        public const int USER_GOD_FULL = 200;
        public const int USER_GOD_MAINTENANCE = 250;

        // user flags (account types)
        public const int USER_FLAG_GUEST      = 0;         // Temporary: (Default) No payment info on account    
        public const int USER_FLAG_RESIDENT   = 200;        // Resident: Payment info on account
        public const int USER_FLAG_PAY        = 300;        // Testing: Payment info on account
        public const int USER_FLAG_NOPAY      = 400;        // Testing: No Payment info on account
        public const int USER_FLAG_MEMBER     = 600;        // Member Estate: Payment info on account
        public const int USER_FLAG_CONTRACTOR = 800;        // Contracted
        public const int USER_FLAG_CHARTERMEMBER = 3840;    // Charter member


    }
}