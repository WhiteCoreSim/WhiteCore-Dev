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

using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.DataService
{
    public class LocalProfileConnector : ConnectorBase, IProfileConnector
    {
        //We can use a cache because we are the only place that profiles will be served from
        readonly Dictionary<UUID, IUserProfileInfo> UserProfilesCache = new Dictionary<UUID, IUserProfileInfo>();
        IGenericData GD;
        string m_userProfileTable = "user_profile";
        string m_userPicksTable = "user_picks";
        string m_userClassifiedsTable = "user_classifieds";


        #region IProfileConnector Members

        public void Initialize(IGenericData GenericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            GD = GenericData;

            if (source.Configs[Name] != null)
                defaultConnectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);

            if (GD != null)
                GD.ConnectToDatabase(defaultConnectionString, "Agent",
                                     source.Configs["WhiteCoreConnectors"].GetBoolean("ValidateTables", true));

            Framework.Utilities.DataManager.RegisterPlugin(Name + "Local", this);

            if (source.Configs["WhiteCoreConnectors"].GetString("ProfileConnector", "LocalConnector") == "LocalConnector")
            {
                Framework.Utilities.DataManager.RegisterPlugin(this);
            }
            Init(simBase, Name);
        }

        public string Name
        {
            get { return "IProfileConnector"; }
        }

        /// <summary>
        ///     Get a user's profile
        /// </summary>
        /// <param name="agentID"></param>
        /// <returns></returns>
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public IUserProfileInfo GetUserProfile(UUID agentID)
        {
            IUserProfileInfo UserProfile = new IUserProfileInfo();
            lock (UserProfilesCache)
            {
                //Try from the user profile first before getting from the DB
                if (UserProfilesCache.TryGetValue(agentID, out UserProfile))
                    return UserProfile;
            }

            object remoteValue = DoRemote(agentID);
            if (remoteValue != null || m_doRemoteOnly)
            {
                UserProfile = (IUserProfileInfo)remoteValue;
                //Add to the cache
                lock (UserProfilesCache)
                    UserProfilesCache[agentID] = UserProfile;
                return UserProfile;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["ID"] = agentID;
            filter.andFilters["`Key`"] = "LLProfile";
            List<string> query = null;
            //Grab it from the almost generic interface
            query = GD.Query(new[] { "Value" }, m_userProfileTable, filter, null, null, null);

            if (query == null || query.Count == 0)
                return null;
            //Pull out the OSDmap
            OSDMap profile = (OSDMap) OSDParser.DeserializeLLSDXml(query[0]);

            UserProfile = new IUserProfileInfo();
            UserProfile.FromOSD(profile);

            //Add to the cache
            lock(UserProfilesCache)
                UserProfilesCache[agentID] = UserProfile;

            return UserProfile;
        }

        /// <summary>
        ///     Update a user's profile (Note: this does not work if the user does not have a profile)
        /// </summary>
        /// <param name="Profile"></param>
        /// <returns></returns>
        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool UpdateUserProfile(IUserProfileInfo Profile)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (Profile);
                return remoteValue != null && (bool)remoteValue;
            }

            IUserProfileInfo previousProfile = GetUserProfile(Profile.PrincipalID);
            //Make sure the previous one exists
            if (previousProfile == null)
                return false;
            //Now fix values that the sim cannot change
            Profile.Partner = previousProfile.Partner;
            Profile.CustomType = previousProfile.CustomType;
            Profile.MembershipGroup = previousProfile.MembershipGroup;
            Profile.Created = previousProfile.Created;

            Dictionary<string, object> values = new Dictionary<string, object>(1);
            values["Value"] = OSDParser.SerializeLLSDXmlString(Profile.ToOSD());

            QueryFilter filter = new QueryFilter();
            filter.andFilters["ID"] = Profile.PrincipalID.ToString();
            filter.andFilters["`Key`"] = "LLProfile";

            //Update cache
            lock(UserProfilesCache)
                UserProfilesCache[Profile.PrincipalID] = Profile;

            return GD.Update(m_userProfileTable, values, null, filter, null, null);
        }

        public void ClearCache(UUID agentID)
        {
            lock (UserProfilesCache)
                UserProfilesCache.Remove(agentID);
        }

        /// <summary>
        ///     Create a new profile for a user
        /// </summary>
        /// <param name="AgentID"></param>
        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public void CreateNewProfile(UUID AgentID)
        {
            /*object remoteValue = DoRemote(AgentID);
            if (remoteValue != null || m_doRemoteOnly)
                return;*/

            List<object> values = new List<object> {AgentID.ToString(), "LLProfile"};

            //Create a new basic profile for them
            IUserProfileInfo profile = new IUserProfileInfo {PrincipalID = AgentID};

            values.Add(OSDParser.SerializeLLSDXmlString(profile.ToOSD())); //Value which is a default Profile

            GD.Insert(m_userProfileTable, values.ToArray());
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool AddClassified(Classified classified)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (classified);
                return remoteValue != null && (bool)remoteValue;
            }

            if (GetUserProfile(classified.CreatorUUID) == null)
                return false;
            string keywords = classified.Description;
            if (keywords.Length > 512)
                keywords = keywords.Substring(keywords.Length - 512, 512);
            //It might be updating, delete the old
            QueryFilter filter = new QueryFilter();
            filter.andFilters["ClassifiedUUID"] = classified.ClassifiedUUID;
            GD.Delete(m_userClassifiedsTable, filter);
            List<object> values = new List<object>
                                      {
                                          classified.Name,
                                          classified.Category,
                                          classified.SimName,
                                          classified.CreatorUUID,
                                          classified.ScopeID,
                                          classified.ClassifiedUUID,
                                          OSDParser.SerializeJsonString(classified.ToOSD()),
                                          classified.PriceForListing,
                                          keywords
                                      };
            return GD.Insert(m_userClassifiedsTable, values.ToArray());
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<Classified> GetClassifieds(UUID ownerID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (ownerID);
                return remoteValue != null ? (List<Classified>)remoteValue : new List<Classified> ();
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["OwnerUUID"] = ownerID;

            List<string> query = GD.Query(new[] { "*" }, m_userClassifiedsTable, filter, null, null, null);

            List<Classified> classifieds = new List<Classified>();
            for (int i = 0; i < query.Count; i += 9)
            {
                Classified classified = new Classified();
                classified.FromOSD((OSDMap) OSDParser.DeserializeJson(query[i + 6]));
                classifieds.Add(classified);
            }
            return classifieds;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public Classified GetClassified(UUID queryClassifiedID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (queryClassifiedID);
                return remoteValue != null ? (Classified)remoteValue : null;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["ClassifiedUUID"] = queryClassifiedID;

            List<string> query = GD.Query(new[] { "*" }, m_userClassifiedsTable, filter, null, null, null);

            if (query.Count < 9)
                return null;
            
            Classified classified = new Classified();
            classified.FromOSD((OSDMap) OSDParser.DeserializeJson(query[6]));
            return classified;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void RemoveClassified(UUID queryClassifiedID)
        {
            if (m_doRemoteOnly) {
                DoRemote (queryClassifiedID);
                return;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["ClassifiedUUID"] = queryClassifiedID;
            GD.Delete(m_userClassifiedsTable, filter);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public bool AddPick(ProfilePickInfo pick)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (pick);
                return remoteValue != null && (bool)remoteValue;
            }

            if (GetUserProfile(pick.CreatorUUID) == null)
                return false;

            //It might be updating, delete the old
            QueryFilter filter = new QueryFilter();
            filter.andFilters["PickUUID"] = pick.PickUUID;
            GD.Delete(m_userPicksTable, filter);
            List<object> values = new List<object>
                                      {
                                          pick.Name,
                                          pick.SimName,
                                          pick.CreatorUUID,
                                          pick.PickUUID,
                                          OSDParser.SerializeJsonString(pick.ToOSD())
                                      };
            return GD.Insert(m_userPicksTable, values.ToArray());
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public ProfilePickInfo GetPick(UUID queryPickID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (queryPickID);
                return remoteValue != null ? (ProfilePickInfo)remoteValue : null;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["PickUUID"] = queryPickID;

            List<string> query = GD.Query(new[] { "*" }, m_userPicksTable, filter, null, null, null);

            if (query.Count < 5)
                return null;
            ProfilePickInfo pick = new ProfilePickInfo();
            pick.FromOSD((OSDMap) OSDParser.DeserializeJson(query[4]));
            return pick;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<ProfilePickInfo> GetPicks(UUID ownerID)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemote (ownerID);
                return remoteValue != null ? (List<ProfilePickInfo>)remoteValue : new List<ProfilePickInfo> ();
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["OwnerUUID"] = ownerID;

            List<string> query = GD.Query(new[] { "*" }, m_userPicksTable, filter, null, null, null);

            List<ProfilePickInfo> picks = new List<ProfilePickInfo>();
            for (int i = 0; i < query.Count; i += 5)
            {
                ProfilePickInfo pick = new ProfilePickInfo();
                pick.FromOSD((OSDMap) OSDParser.DeserializeJson(query[i + 4]));
                picks.Add(pick);
            }
            return picks;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public void RemovePick(UUID queryPickID)
        {
            if (m_doRemoteOnly) {
                DoRemote (queryPickID);
                return;
            }

            QueryFilter filter = new QueryFilter();
            filter.andFilters["PickUUID"] = queryPickID;
            GD.Delete(m_userPicksTable, filter);
        }

        #endregion

        public void Dispose()
        {
        }
    }
}