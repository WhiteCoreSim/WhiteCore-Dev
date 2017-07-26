/*
 * Copyright (c) Contributors, http://whitecore-sim.org/
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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using DataPlugins = WhiteCore.Framework.Utilities.DataManager;
using FriendInfo = WhiteCore.Framework.Services.FriendInfo;
using GridRegion = WhiteCore.Framework.Services.GridRegion;

namespace WhiteCore.Services.API
{
    //public class ResidentWorldAPI : IService
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        /*		#region IService implementation

                public void Initialize(IConfigSource config, IRegistryCore registry)
                {
                    throw new System.NotImplementedException();
                }

                public void Start(IConfigSource config, IRegistryCore registry)
                {
                    throw new System.NotImplementedException();
                }

                public void FinishedStartup()
                {
                    throw new System.NotImplementedException();
                }

                #endregion
                */

        #region user

        /// <summary>
        /// Gets user information for change user info page on site
        /// </summary>
        /// <param name="map">UUID</param>
        /// <returns>Verified, HomeName, HomeUUID, Online, Email, FirstName, LastName</returns>
        OSDMap GetGridUserInfo (OSDMap map)
        {
            string uuid = string.Empty;
            uuid = map ["UUID"].AsString ();

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount user = accountService.GetUserAccount (null, map ["UUID"].AsUUID ());
            IAgentInfoService agentService = m_registry.RequestModuleInterface<IAgentInfoService> ();

            UserInfo userinfo;
            OSDMap resp = new OSDMap ();
            bool verified = user != null;
            resp ["Verified"] = OSD.FromBoolean (verified);
            if (verified) {
                userinfo = agentService.GetUserInfo (uuid);
                IGridService gs = m_registry.RequestModuleInterface<IGridService> ();
                GridRegion gr = null;
                if (userinfo != null) {
                    gr = gs.GetRegionByUUID (null, userinfo.HomeRegionID);
                }

                resp ["UUID"] = OSD.FromUUID (user.PrincipalID);
                resp ["HomeUUID"] = OSD.FromUUID ((userinfo == null) ? UUID.Zero : userinfo.HomeRegionID);
                resp ["HomeName"] = OSD.FromString ((userinfo == null) ? "" : gr.RegionName);
                resp ["Online"] = OSD.FromBoolean ((userinfo == null) ? false : userinfo.IsOnline);
                resp ["Email"] = OSD.FromString (user.Email);
                resp ["Name"] = OSD.FromString (user.Name);
                resp ["FirstName"] = OSD.FromString (user.FirstName);
                resp ["LastName"] = OSD.FromString (user.LastName);
            }

            return resp;
        }

        OSDMap GetProfile (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            string Name = map ["Name"].AsString ();
            UUID userID = map ["UUID"].AsUUID ();

            UserAccount account = Name != "" ?
                m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, Name) :
                 m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, userID);
            if (account != null) {
                OSDMap accountMap = new OSDMap ();

                accountMap ["Created"] = account.Created;
                accountMap ["Name"] = account.Name;
                accountMap ["PrincipalID"] = account.PrincipalID;
                accountMap ["Email"] = account.Email;

                TimeSpan diff = DateTime.Now - Util.ToDateTime (account.Created);
                int years = (int)diff.TotalDays / 356;
                int days = years > 0 ? (int)diff.TotalDays / years : (int)diff.TotalDays;
                accountMap ["TimeSinceCreated"] = years + " years, " + days + " days"; // if we're sending account.Created do we really need to send this string ?

                IProfileConnector profileConnector = DataPlugins.RequestPlugin<IProfileConnector> ();
                IUserProfileInfo profile = profileConnector.GetUserProfile (account.PrincipalID);
                if (profile != null) {
                    resp ["profile"] = profile.ToOSD (false);//not trusted, use false

                    if (account.UserFlags == 0)
                        account.UserFlags = 2; //Set them to no info given

                    string flags = ((IUserProfileInfo.ProfileFlags)account.UserFlags).ToString ();
                    IUserProfileInfo.ProfileFlags.NoPaymentInfoOnFile.ToString ();

                    accountMap ["AccountInfo"] = (profile.CustomType != "" ? profile.CustomType :
                        account.UserFlags == 0 ? "Resident" : "Admin") + "\n" + flags;
                    UserAccount partnerAccount = m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, profile.Partner);
                    if (partnerAccount != null) {
                        accountMap ["Partner"] = partnerAccount.Name;
                        accountMap ["PartnerUUID"] = partnerAccount.PrincipalID;
                    } else {
                        accountMap ["Partner"] = "";
                        accountMap ["PartnerUUID"] = UUID.Zero;
                    }

                }
                IAgentConnector agentConnector = DataPlugins.RequestPlugin<IAgentConnector> ();
                IAgentInfo agent = agentConnector.GetAgent (account.PrincipalID);
                if (agent != null) {
                    OSDMap agentMap = new OSDMap ();
                    agentMap ["RLName"] = agent.OtherAgentInformation ["RLName"].AsString ();
                    agentMap ["RLGender"] = agent.OtherAgentInformation ["RLGender"].AsString ();
                    agentMap ["RLAddress"] = agent.OtherAgentInformation ["RLAddress"].AsString ();
                    agentMap ["RLZip"] = agent.OtherAgentInformation ["RLZip"].AsString ();
                    agentMap ["RLCity"] = agent.OtherAgentInformation ["RLCity"].AsString ();
                    agentMap ["RLCountry"] = agent.OtherAgentInformation ["RLCountry"].AsString ();
                    resp ["agent"] = agentMap;
                }
                resp ["account"] = accountMap;
            }

            return resp;
        }

        OSDMap FindUsers (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            int start = map ["Start"].AsInteger ();
            int end = map ["End"].AsInteger ();
            string Query = map ["Query"].AsString ();
            List<UserAccount> accounts = m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccounts (null, Query);

            OSDArray users = new OSDArray ();
            MainConsole.Instance.TraceFormat ("{0} accounts found", accounts.Count);
            for (int i = start; i < end && i < accounts.Count; i++) {
                UserAccount acc = accounts [i];
                OSDMap userInfo = new OSDMap ();
                userInfo ["PrincipalID"] = acc.PrincipalID;
                userInfo ["UserName"] = acc.Name;
                userInfo ["Created"] = acc.Created;
                userInfo ["UserFlags"] = acc.UserFlags;
                users.Add (userInfo);
            }
            resp ["Users"] = users;

            resp ["Start"] = OSD.FromInteger (start);
            resp ["End"] = OSD.FromInteger (end);
            resp ["Query"] = OSD.FromString (Query);

            return resp;
        }

        OSDMap GetFriends (OSDMap map)
        {
            OSDMap resp = new OSDMap ();

            if (map.ContainsKey ("UserID") == false) {
                resp ["Failed"] = OSD.FromString ("User ID not specified.");
                return resp;
            }

            IFriendsService friendService = m_registry.RequestModuleInterface<IFriendsService> ();

            if (friendService == null) {
                resp ["Failed"] = OSD.FromString ("No friend service found.");
                return resp;
            }

            List<FriendInfo> friendsList = new List<FriendInfo> (friendService.GetFriends (map ["UserID"].AsUUID ()));
            OSDArray friends = new OSDArray (friendsList.Count);
            foreach (FriendInfo friendInfo in friendsList) {
                UserAccount account = m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, UUID.Parse (friendInfo.Friend));
                OSDMap friend = new OSDMap (4);
                friend ["PrincipalID"] = friendInfo.Friend;
                friend ["Name"] = account.Name;
                friend ["MyFlags"] = friendInfo.MyFlags;
                friend ["TheirFlags"] = friendInfo.TheirFlags;
                friends.Add (friend);
            }

            resp ["Friends"] = friends;

            return resp;
        }


        #endregion

        #region Email

        /// <summary>
        /// After conformation the email is saved
        /// </summary>
        /// <param name="map">UUID, Email</param>
        /// <returns>Verified</returns>
        OSDMap SaveEmail (OSDMap map)
        {
            string email = map ["Email"].AsString ();

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount user = accountService.GetUserAccount (null, map ["UUID"].AsUUID ());
            OSDMap resp = new OSDMap ();

            bool verified = user != null;
            resp ["Verified"] = OSD.FromBoolean (verified);
            if (verified) {
                user.Email = email;
                user.UserLevel = 0;
                accountService.StoreUserAccount (user);
            }
            return resp;
        }

        OSDMap ConfirmUserEmailName (OSDMap map)
        {
            string Name = map ["Name"].AsString ();
            string Email = map ["Email"].AsString ();

            OSDMap resp = new OSDMap ();
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount user = accountService.GetUserAccount (null, Name);
            bool verified = user != null;
            resp ["Verified"] = OSD.FromBoolean (verified);

            if (verified) {
                resp ["UUID"] = OSD.FromUUID (user.PrincipalID);
                if (user.UserLevel >= 0) {
                    if (user.Email.ToLower () != Email.ToLower ()) {
                        MainConsole.Instance.TraceFormat ("User email for account \"{0}\" is \"{1}\" but \"{2}\" was specified.", Name, user.Email.ToString (), Email);
                        resp ["Error"] = OSD.FromString ("Email does not match the user name.");
                        resp ["ErrorCode"] = OSD.FromInteger (3);
                    }
                } else {
                    resp ["Error"] = OSD.FromString ("This account is disabled.");
                    resp ["ErrorCode"] = OSD.FromInteger (2);
                }
            } else {
                resp ["Error"] = OSD.FromString ("No such user.");
                resp ["ErrorCode"] = OSD.FromInteger (1);
            }

            return resp;
        }

        #endregion

        #region password

        OSDMap ChangePassword (OSDMap map)
        {
            string Password = map ["Password"].AsString ();
            string newPassword = map ["NewPassword"].AsString ();
            UserAccount account;
            OSDMap resp = new OSDMap ();

            ILoginService loginService = m_registry.RequestModuleInterface<ILoginService> ();
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UUID userID = map ["UUID"].AsUUID ();
            if (accountService != null) {
                account = accountService.GetUserAccount (null, userID);
                bool cliVerified = loginService.VerifyClient (account.PrincipalID, account.Name, "UserAccount", Password);
                resp ["Verified"] = OSD.FromBoolean (cliVerified);

                if (cliVerified) {
                    IAuthenticationService auths = m_registry.RequestModuleInterface<IAuthenticationService> ();
                    if (auths != null) {
                        if ((auths.Authenticate (userID, "UserAccount", Util.Md5Hash (Password), 100) != string.Empty) && (cliVerified)) 
                            auths.SetPassword (userID, "UserAccount", newPassword);
                    }
                    resp ["Verified"] = OSD.FromBoolean (false);
                }
            }
            return resp;
        }

        OSDMap ForgotPassword (OSDMap map)
        {
            UUID UUDI = map ["UUID"].AsUUID ();
            string Password = map ["Password"].AsString ();

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount user = accountService.GetUserAccount (null, UUDI);

            OSDMap resp = new OSDMap ();
            bool verified = user != null;
            resp ["Verified"] = OSD.FromBoolean (verified);
            resp ["UserLevel"] = OSD.FromInteger (0);
            if (verified) {
                resp ["UserLevel"] = OSD.FromInteger (user.UserLevel);
                if (user.UserLevel >= 0) {
                    IAuthenticationService auths = m_registry.RequestModuleInterface<IAuthenticationService> ();
                    auths.SetPassword (user.PrincipalID, "UserAccount", Password);
                } else {
                    resp ["Verified"] = OSD.FromBoolean (false);
                }
            }

            return resp;
        }

        #endregion

        #region edituser

        OSDMap DeleteUser (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            resp ["Finished"] = OSD.FromBoolean (true);

            UUID agentID = map ["UserID"].AsUUID ();
            IAgentInfo GetAgent = DataPlugins.RequestPlugin<IAgentConnector> ().GetAgent (agentID);

            if (GetAgent != null) {
                GetAgent.Flags &= ~IAgentFlags.PermBan;
                DataPlugins.RequestPlugin<IAgentConnector> ().UpdateAgent (GetAgent);
            }
            return resp;
        }

        OSDMap SetUserLevel (OSDMap map) //just sets a user level
        {
            OSDMap resp = new OSDMap ();

            UUID agentID = map ["UserID"].AsUUID ();
            int userLevel = map ["UserLevel"].AsInteger ();

            IUserAccountService userService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount account = userService.GetUserAccount (null, agentID);

            if (account != null) //found
            {
                account.UserLevel = userLevel;

                userService.StoreUserAccount (account);

                resp ["UserFound"] = OSD.FromBoolean (true);
                resp ["Updated"] = OSD.FromBoolean (true);
            } else //not found
              {
                resp ["UserFound"] = OSD.FromBoolean (false);
                resp ["Updated"] = OSD.FromBoolean (false);
            }

            return resp;
        }

        /// <summary>
        /// Changes user name
        /// </summary>
        /// <param name="map">UUID, FirstName, LastName</param>
        /// <returns>Verified</returns>
        OSDMap ChangeName (OSDMap map)
        {
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount user = accountService.GetUserAccount (null, map ["UUID"].AsUUID ());
            OSDMap resp = new OSDMap ();

            bool verified = user != null;
            resp ["Verified"] = OSD.FromBoolean (verified);
            if (verified) {
                user.Name = map ["Name"].AsString ();
                resp ["Stored"] = OSD.FromBoolean (accountService.StoreUserAccount (user));
            }

            return resp;
        }

        OSDMap EditUser (OSDMap map)
        {
            bool editRLInfo = (map.ContainsKey ("RLName") && map.ContainsKey ("RLAddress") && map.ContainsKey ("RLZip") && map.ContainsKey ("RLCity") && map.ContainsKey ("RLCountry"));
            OSDMap resp = new OSDMap ();
            resp ["agent"] = OSD.FromBoolean (!editRLInfo); // if we have no RLInfo, editing account is assumed to be successful.
            resp ["account"] = OSD.FromBoolean (false);
            UUID principalID = map ["UserID"].AsUUID ();
            UserAccount account = m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, principalID);
            if (account != null) {
                account.Email = map ["Email"];
                if (m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, map ["Name"].AsString ()) == null) {
                    account.Name = map ["Name"];
                }

                if (editRLInfo) {
                    IAgentConnector agentConnector = DataPlugins.RequestPlugin<IAgentConnector> ();
                    IAgentInfo agent = agentConnector.GetAgent (account.PrincipalID);
                    if (agent == null) {
                        agentConnector.CreateNewAgent (account.PrincipalID);
                        agent = agentConnector.GetAgent (account.PrincipalID);
                    }
                    if (agent != null) {
                        agent.OtherAgentInformation ["RLName"] = map ["RLName"];
                        agent.OtherAgentInformation ["RLAddress"] = map ["RLAddress"];
                        agent.OtherAgentInformation ["RLZip"] = map ["RLZip"];
                        agent.OtherAgentInformation ["RLCity"] = map ["RLCity"];
                        agent.OtherAgentInformation ["RLCountry"] = map ["RLCountry"];
                        agentConnector.UpdateAgent (agent);
                        resp ["agent"] = OSD.FromBoolean (true);
                    }
                }
                resp ["account"] = OSD.FromBoolean (m_registry.RequestModuleInterface<IUserAccountService> ().StoreUserAccount (account));
            }
            return resp;
        }

        #endregion

        #region banning

        OSDMap KickUser (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            resp ["Finished"] = OSD.FromBoolean (true);

            UUID agentID = map ["UserID"].AsUUID ();

            IGridWideMessageModule messageModule = m_registry.RequestModuleInterface<IGridWideMessageModule> ();
            if (messageModule != null)
                messageModule.KickUser (agentID, map ["Message"].AsString ());

            return resp;
        }

        void doBan (UUID agentID, DateTime? until)
        {
            var conn = DataPlugins.RequestPlugin<IAgentConnector> ();
            IAgentInfo agentInfo = conn.GetAgent (agentID);
            if (agentInfo != null) {
                agentInfo.Flags |= (until.HasValue) ? IAgentFlags.TempBan : IAgentFlags.PermBan;

                if (until.HasValue) {
                    agentInfo.OtherAgentInformation ["TemporaryBanInfo"] = until.Value;
                    MainConsole.Instance.TraceFormat ("Temporarily ban for {0} until {1}", agentID, until.Value.ToString ("s"));
                }
                conn.UpdateAgent (agentInfo);
            }
        }

        OSDMap BanUser (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            resp ["Finished"] = OSD.FromBoolean (true);
            UUID agentID = map ["UserID"].AsUUID ();
            doBan (agentID, null);

            return resp;
        }

        OSDMap TempBanUser (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            resp ["Finished"] = OSD.FromBoolean (true);
            UUID agentID = map ["UserID"].AsUUID ();
            DateTime until = Util.ToDateTime (map ["BannedUntil"].AsInteger ()); //BannedUntil is a unix timestamp of the date and time the user should be banned till
            doBan (agentID, until);

            return resp;
        }

        OSDMap UnBanUser (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            resp ["Finished"] = OSD.FromBoolean (true);

            UUID agentID = map ["UserID"].AsUUID ();
            IAgentInfo GetAgent = DataPlugins.RequestPlugin<IAgentConnector> ().GetAgent (agentID);

            if (GetAgent != null) {
                GetAgent.Flags &= IAgentFlags.PermBan;
                GetAgent.Flags &= IAgentFlags.TempBan;
                if (GetAgent.OtherAgentInformation.ContainsKey ("TemporaryBanInfo") == true)
                    GetAgent.OtherAgentInformation.Remove ("TemporaryBanInfo");

                DataPlugins.RequestPlugin<IAgentConnector> ().UpdateAgent (GetAgent);
            }

            return resp;
        }

        OSDMap CheckBan (OSDMap map)
        {
            OSDMap resp = new OSDMap ();

            UUID agentID = map ["UserID"].AsUUID ();
            IAgentInfo agentInfo = DataPlugins.RequestPlugin<IAgentConnector> ().GetAgent (agentID);

            if (agentInfo != null) //found
            {
                resp ["UserFound"] = OSD.FromBoolean (true);

                bool banned = ((agentInfo.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan) || ((agentInfo.Flags & IAgentFlags.PermBan) == IAgentFlags.PermBan);

                resp ["banned"] = OSD.FromBoolean (banned);

                if (banned) //get ban type
                {
                    if ((agentInfo.Flags & IAgentFlags.PermBan) == IAgentFlags.PermBan) {
                        resp ["BanType"] = OSD.FromString ("PermBan");
                    } else if ((agentInfo.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan) {
                        resp ["BanType"] = OSD.FromString ("TempBan");
                        if (agentInfo.OtherAgentInformation.ContainsKey ("TemporaryBanInfo") == true) {
                            resp ["BannedUntil"] = OSD.FromInteger (Util.ToUnixTime (agentInfo.OtherAgentInformation ["TemporaryBanInfo"]));
                        } else {
                            resp ["BannedUntil"] = OSD.FromInteger (0);
                        }
                    } else {
                        resp ["BanType"] = OSD.FromString ("Unknown");
                    }
                }
            } else //not found
              {
                resp ["UserFound"] = OSD.FromBoolean (false);
            }

            return resp;
        }

        #endregion

    }
}
