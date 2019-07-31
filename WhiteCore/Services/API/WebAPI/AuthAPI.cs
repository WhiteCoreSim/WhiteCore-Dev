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

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using DataPlugins = WhiteCore.Framework.Utilities.DataManager;


namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        #region Login

        OSDMap Login (OSDMap map, bool asAdmin)
        {
            bool Verified = false;
            string Name = map ["Name"].AsString ();
            string Password = map ["Password"].AsString ();

            var loginService = m_registry.RequestModuleInterface<ILoginService> ();
            var accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            var resp = new OSDMap ();

            resp ["Verified"] = OSD.FromBoolean (false);

            if (accountService == null || CheckIfUserExists (map) ["Verified"] != true) {
                return resp;
            }

            UserAccount userAcct = accountService.GetUserAccount (null, Name);
            if (!userAcct.Valid)
                return resp;            // something nasty here
        

            if (loginService.VerifyClient (userAcct.PrincipalID, Name, "UserAccount", Password)) {
                if (asAdmin) {
                    IAgentInfo agent = DataPlugins.RequestPlugin<IAgentConnector> ().GetAgent (userAcct.PrincipalID);
                    if (agent.OtherAgentInformation ["WebUIEnabled"].AsBoolean () == false) {
                        return resp;
                    }
                }
                resp ["UUID"] = OSD.FromUUID (userAcct.PrincipalID);
                resp ["FirstName"] = OSD.FromString (userAcct.FirstName);
                resp ["LastName"] = OSD.FromString (userAcct.LastName);
                resp ["Email"] = OSD.FromString (userAcct.Email);
                Verified = true;
            }

            resp ["Verified"] = OSD.FromBoolean (Verified);

            return resp;
        }

        OSDMap Login2 (OSDMap map)
        {
            string Name = map ["Name"].AsString ();
            string Password = map ["Password"].AsString ();

            var resp = new OSDMap ();
            resp ["GoodLogin"] = OSD.FromBoolean (false);

            var loginService = m_registry.RequestModuleInterface<ILoginService> ();
            var accountService = m_registry.RequestModuleInterface<IUserAccountService> ();

            if (accountService == null || CheckIfUserExists (map) ["Verified"] != true) {
                resp ["Error"] = OSD.FromString ("AccountNotFound");
                return resp;
            }

            UserAccount userAcct = accountService.GetUserAccount (null, Name);
            if (!userAcct.Valid)
                return resp;            // something nasty here
               
            if (loginService.VerifyClient (userAcct.PrincipalID, Name, "UserAccount", Password)) {
                UUID agentID = userAcct.PrincipalID;

                var agentInfo = DataPlugins.RequestPlugin<IAgentConnector> ().GetAgent (agentID);

                bool banned = ((agentInfo.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan) || ((agentInfo.Flags & IAgentFlags.PermBan) == IAgentFlags.PermBan);

                if (banned) //get ban type
                {

                    if ((agentInfo.Flags & IAgentFlags.PermBan) == IAgentFlags.PermBan) {
                        resp ["Error"] = OSD.FromString ("PermBan");
                    } else if ((agentInfo.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan) {
                        resp ["Error"] = OSD.FromString ("TempBan");

                        if (agentInfo.OtherAgentInformation.ContainsKey ("TemporaryBanInfo") == true) {
                            resp ["BannedUntil"] = OSD.FromInteger (Util.ToUnixTime (agentInfo.OtherAgentInformation ["TemporaryBanInfo"]));
                        } else {
                            resp ["BannedUntil"] = OSD.FromInteger (0);
                        }
                    } else {
                        resp ["Error"] = OSD.FromString ("UnknownBan");
                    }

                    return resp;
                }

                resp ["GoodLogin"] = OSD.FromBoolean (true);

                resp ["UserLevel"] = OSD.FromInteger (userAcct.UserLevel);
                resp ["UUID"] = OSD.FromUUID (agentID);
                resp ["FirstName"] = OSD.FromString (userAcct.FirstName);
                resp ["LastName"] = OSD.FromString (userAcct.LastName);
                resp ["Email"] = OSD.FromString (userAcct.Email);

                return resp;
            }

            resp ["Error"] = OSD.FromString ("BadPassword");
            return resp;
        }

        // TODO:  This probably should be replaced with the auth key processing
        OSDMap SetWebLoginKey (OSDMap map)
        {
            var resp = new OSDMap ();
            var principalID = map ["PrincipalID"].AsUUID ();
            var webLoginKey = UUID.Random ();
            var authService = m_registry.RequestModuleInterface<IAuthenticationService> ();

            if (authService != null) {
                //Remove the old
                DataPlugins.RequestPlugin<IAuthenticationData> ().Delete (principalID, "WebLoginKey");
                authService.SetPlainPassword (principalID, "WebLoginKey", webLoginKey.ToString ());
                resp ["WebLoginKey"] = webLoginKey;
            }

            resp ["Failed"] = OSD.FromString (string.Format ("No auth service, cannot set WebLoginKey for user {0}.", map ["PrincipalID"].AsUUID ()));

            return resp;
        }

        #endregion
    }
}
