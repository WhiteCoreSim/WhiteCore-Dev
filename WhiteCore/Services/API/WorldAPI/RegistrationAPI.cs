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
using GridRegion = WhiteCore.Framework.Services.GridRegion;


namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        #region Registration

        OSDMap CheckIfUserExists (OSDMap map)
        {
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount user = accountService.GetUserAccount (null, map ["Name"].AsString ());

            bool Verified = user != null;
            OSDMap resp = new OSDMap ();
            resp ["Verified"] = OSD.FromBoolean (Verified);
            resp ["UUID"] = OSD.FromUUID (Verified ? user.PrincipalID : UUID.Zero);
            return resp;
        }

        OSDMap CreateAccount (OSDMap map)
        {
            bool Verified = false;
            string Name = map ["Name"].AsString ();

            string Password = "";

            if (map.ContainsKey ("Password")) {
                Password = map ["Password"].AsString ();
            } else {
                Password = map ["PasswordHash"].AsString (); //is really plaintext password, the system hashes it later. Not sure why it was called PasswordHash to start with. I guess the original design was to have the PHP code generate the salt and password hash, then just simply store it here
            }

            //string PasswordSalt = map["PasswordSalt"].AsString(); //not being used
            string HomeRegion = map ["HomeRegion"].AsString ();
            string Email = map ["Email"].AsString ();
            string AvatarArchive = map ["AvatarArchive"].AsString ();
            int userLevel = map ["UserLevel"].AsInteger ();
            string UserTitle = map ["UserTitle"].AsString ();

            //server expects: 0 is PG, 1 is Mature, 2 is Adult - use this when setting MaxMaturity and MaturityRating
            //viewer expects: 13 is PG, 21 is Mature, 42 is Adult

            int MaxMaturity = 2; //set to adult by default
            if (map.ContainsKey ("MaxMaturity")) //MaxMaturity is the highest level that they can change the maturity rating to in the viewer
            {
                MaxMaturity = map ["MaxMaturity"].AsInteger ();
            }

            int MaturityRating = MaxMaturity; //set the default to whatever MaxMaturity was set tom incase they didn't define MaturityRating

            if (map.ContainsKey ("MaturityRating")) //MaturityRating is the rating the user wants to be able to see
            {
                MaturityRating = map ["MaturityRating"].AsInteger ();
            }

            bool activationRequired = map.ContainsKey ("ActivationRequired") ? map ["ActivationRequired"].AsBoolean () : false;

            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            if (accountService == null)
                return null;

            if (!Password.StartsWith ("$1$", System.StringComparison.Ordinal))
                Password = "$1$" + Util.Md5Hash (Password);
            Password = Password.Remove (0, 3); //remove $1$

            accountService.CreateUser (Name, Password, Email);
            UserAccount user = accountService.GetUserAccount (null, Name);
            IAgentInfoService agentInfoService = m_registry.RequestModuleInterface<IAgentInfoService> ();
            IGridService gridService = m_registry.RequestModuleInterface<IGridService> ();
            if (agentInfoService != null && gridService != null) {
                GridRegion r = gridService.GetRegionByName (null, HomeRegion);
                if (r != null) {
                    agentInfoService.SetHomePosition (user.PrincipalID.ToString (), r.RegionID, new Vector3 (r.RegionSizeX / 2, r.RegionSizeY / 2, 20), Vector3.Zero);
                } else {
                    MainConsole.Instance.DebugFormat ("[API]: Could not set home position for user {0}, region \"{1}\" did not produce a result from the grid service", Name, HomeRegion);
                }
            }

            Verified = user != null;
            UUID userID = UUID.Zero;

            OSDMap resp = new OSDMap ();
            resp ["Verified"] = OSD.FromBoolean (Verified);

            if (Verified) {
                userID = user.PrincipalID;
                user.UserLevel = userLevel;

                // could not find a way to save this data here.
                DateTime RLDOB = map ["RLDOB"].AsDate ();
                string RLGender = map ["RLGender"].AsString ();
                string RLName = map ["RLName"].AsString ();
                string RLAddress = map ["RLAddress"].AsString ();
                string RLCity = map ["RLCity"].AsString ();
                string RLZip = map ["RLZip"].AsString ();
                string RLCountry = map ["RLCountry"].AsString ();
                string RLIP = map ["RLIP"].AsString ();



                IAgentConnector con = DataPlugins.RequestPlugin<IAgentConnector> ();
                con.CreateNewAgent (userID);

                IAgentInfo agent = con.GetAgent (userID);

                agent.MaxMaturity = MaxMaturity;
                agent.MaturityRating = MaturityRating;

                agent.OtherAgentInformation ["RLDOB"] = RLDOB;
                agent.OtherAgentInformation ["RLGender"] = RLGender;
                agent.OtherAgentInformation ["RLName"] = RLName;
                agent.OtherAgentInformation ["RLAddress"] = RLAddress;
                agent.OtherAgentInformation ["RLCity"] = RLCity;
                agent.OtherAgentInformation ["RLZip"] = RLZip;
                agent.OtherAgentInformation ["RLCountry"] = RLCountry;
                agent.OtherAgentInformation ["RLIP"] = RLIP;
                if (activationRequired) {
                    UUID activationToken = UUID.Random ();
                    agent.OtherAgentInformation ["WebUIActivationToken"] = Util.Md5Hash (activationToken.ToString () + ":" + Password);
                    resp ["WebUIActivationToken"] = activationToken;
                }
                con.UpdateAgent (agent);

                accountService.StoreUserAccount (user);

                IProfileConnector profileData = DataPlugins.RequestPlugin<IProfileConnector> ();
                IUserProfileInfo profile = profileData.GetUserProfile (user.PrincipalID);
                if (profile == null) {
                    profileData.CreateNewProfile (user.PrincipalID);
                    profile = profileData.GetUserProfile (user.PrincipalID);
                }
                if (AvatarArchive.Length > 0) {
                    profile.AArchiveName = AvatarArchive;
                }
                //    MainConsole.Instance.InfoFormat("[WebUI] Triggered Archive load of " + profile.AArchiveName);
                profile.IsNewUser = true;

                profile.MembershipGroup = UserTitle;
                profile.CustomType = UserTitle;

                profileData.UpdateUserProfile (profile);
                //   MainConsole.Instance.RunCommand("load avatar archive " + user.FirstName + " " + user.LastName + " Devil");
            }

            resp ["UUID"] = OSD.FromUUID (userID);
            return resp;
        }


        OSDMap Authenticated (OSDMap map)
        {
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount user = accountService.GetUserAccount (null, map ["UUID"].AsUUID ());

            bool Verified = user != null;
            OSDMap resp = new OSDMap ();
            resp ["Verified"] = OSD.FromBoolean (Verified);

            if (Verified) {
                user.UserLevel = 0;
                accountService.StoreUserAccount (user);
                IAgentConnector con = DataPlugins.RequestPlugin<IAgentConnector> ();
                IAgentInfo agent = con.GetAgent (user.PrincipalID);
                if (agent != null && agent.OtherAgentInformation.ContainsKey ("WebUIActivationToken")) {
                    agent.OtherAgentInformation.Remove ("WebUIActivationToken");
                    con.UpdateAgent (agent);
                }
            }

            return resp;
        }

        OSDMap ActivateAccount (OSDMap map)
        {
            OSDMap resp = new OSDMap ();
            resp ["Verified"] = OSD.FromBoolean (false);

            if (map.ContainsKey ("UserName") && map.ContainsKey ("PasswordHash") && map.ContainsKey ("ActivationToken")) {
                IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
                UserAccount user = accountService.GetUserAccount (null, map ["UserName"].ToString ());
                if (user != null) {
                    IAgentConnector con = DataPlugins.RequestPlugin<IAgentConnector> ();
                    IAgentInfo agent = con.GetAgent (user.PrincipalID);
                    if (agent != null && agent.OtherAgentInformation.ContainsKey ("WebUIActivationToken")) {
                        UUID activationToken = map ["ActivationToken"];
                        string WebUIActivationToken = agent.OtherAgentInformation ["WebUIActivationToken"];
                        string PasswordHash = map ["PasswordHash"];
                        if (!PasswordHash.StartsWith ("$1$")) {
                            PasswordHash = "$1$" + Util.Md5Hash (PasswordHash);
                        }
                        PasswordHash = PasswordHash.Remove (0, 3); //remove $1$

                        bool verified = Utils.MD5String (activationToken.ToString () + ":" + PasswordHash) == WebUIActivationToken;
                        resp ["Verified"] = verified;
                        if (verified) {
                            user.UserLevel = 0;
                            accountService.StoreUserAccount (user);
                            agent.OtherAgentInformation.Remove ("WebUIActivationToken");
                            con.UpdateAgent (agent);
                        }
                    }
                }
            }

            return resp;
        }

        #endregion
    }
}
