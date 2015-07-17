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
    public static class Constants
    {
        public const double FloatDifference = .0000005;

        // some predefined folders
        public const string DEFAULT_CONFIG_DIR = "../Config";
        public const string DEFAULT_DATA_DIR = "../Data";
        public const string DEFAULT_ASSETCACHE_DIR = DEFAULT_DATA_DIR+"/AssetCache";
        public const string DEFAULT_SCRIPTENGINE_DIR = DEFAULT_DATA_DIR+"/ScriptEngines";
        public const string DEFAULT_FILEASSETS_DIR = DEFAULT_DATA_DIR+"/FileAssets";
        public const string DEFAULT_AVATARARCHIVE_DIR = DEFAULT_DATA_DIR+"/AvatarArchives";
        public const string DEFAULT_OARARCHIVE_DIR = DEFAULT_DATA_DIR + "/OarFiles";
        public const string DEFAULT_USERINVENTORY_DIR = DEFAULT_DATA_DIR+"/UserArchives";

        public const string DEFAULT_USERHTML_DIR = DEFAULT_DATA_DIR+"/html/";

        public const int RegionSize = 256;
        public const int RegionHeight = 10000;
        public const byte TerrainPatchSize = 16;
        public const float TerrainCompression = 100.0f;
        public const int MaxRegionSize = 4096;
        public const int MinRegionSize = 16;

		public const int SystemUserCount = 5;

        // System library Avatar Account
        public const string LibraryOwner = "11111111-1111-0000-0000-000100bba000";
        public const string LibraryRootFolderID = "00000112-000f-0000-0000-000100bba000";

        // System Real Estate Avatar Account
        public const string RealEstateOwnerUUID = "bbb55499-7938-4752-ab7c-f7136e36cced";
		public const string RealEstateOwnerName = "RealEstate Owner";

        // System Governor Avatar Account
        public const string GovernorUUID = "3d6181b0-6a4b-97ef-18d8-722652995cf1";
        public const string GovernorName = "Governor White";
        
        // System Estate
        public const string SystemEstateName = "WhiteCore Estate";
        public const int SystemEstateID = 1;

        // System Real Estate Maintenance Group
		public const string RealEstateGroupUUID = "dc7b21cd-3c89-fcaa-31c8-25f9ffd224cd";
		public const string RealEstateGroupName = "Maintenance";

        // System Banker Avatar
        public const string BankerUUID = "f4261829-2796-4688-bfe2-085190cb639b";
        public const string BankerName = "WhiteCore Banker";

        // System Marketplace Avatar
        public const string MarketplaceOwnerUUID = "198e72a6-cef6-4bbb-ae08-c0a79e6b7d1e";
        public const string MarketplaceOwnerName = "Marketplace Concierge";


        // user levels
        public const int USER_DISABLED = -2;
        public const int USER_BANNED = -1;
        public const int USER_NORMAL = 0;
        public const int USER_GOD_LIKE = 1;                 //?? bit low, are some other levels needed??
        public const int USER_GOD_CUSTOMER_SERVICE = 100;
        public const int USER_GOD_LIASON = 150;
        public const int USER_GOD_FULL = 200;
        public const int USER_GOD_MAINTENANCE = 250;

        // user flags (account types)
        public const int USER_FLAG_GUEST      = 0;          // Temporary: (Default) No payment info on account    
        public const int USER_FLAG_RESIDENT   = 200;        // Resident: Payment info on account
        public const int USER_FLAG_PAY        = 300;        // Testing: Payment info on account
        public const int USER_FLAG_NOPAY      = 400;        // Testing: No Payment info on account
        public const int USER_FLAG_MEMBER     = 600;        // Member Estate: Payment info on account
        public const int USER_FLAG_CONTRACTOR = 800;        // Contracted
        public const int USER_FLAG_CHARTERMEMBER = 3840;    // Charter member

        public const int SCHEDULER_INTERVAL = 300;          // seconds between scheduler checks

        public const string STIPEND_PAY_DAY = "tuesday";    // the day stipend payments are processed
        public const string STIPEND_PAY_TIME = "00:05";     // the time, hh:mm, when stipend payments are processed
        public const string STIPEND_PAY_PERIOD = "week";    // how often we process stipends
        public const int STIPEND_PAY_INTERVAL = 1;          // number of period between payments (hours, weeks etc.)
        public const int STIPEND_RECENT_LOGIN_PERIOD = 7 * 24 * 60 * 60;    // week (of seconds)

        public const int GROUP_PAYMENTS_DELAY = 15;         // minutes to wait after stipend payments before processing group payments
    }
}
