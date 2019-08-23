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
using System.IO;
using Nini.Config;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services
{
    public class BannedUserLoginModule : ILoginModule
    {
        protected IAuthenticationService m_AuthenticationService;
        protected ILoginService m_LoginService;
        protected bool m_UseTOS = false;
        protected string m_TOSLocation = "";

        public string Name {
            get { return GetType ().Name; }
        }

        public void Initialize (ILoginService service, IConfigSource config, IRegistryCore registry)
        {
            IConfig loginServerConfig = config.Configs ["LoginService"];
            if (loginServerConfig != null) {
                m_UseTOS = loginServerConfig.GetBoolean ("UseTermsOfServiceOnFirstLogin", false);
                m_TOSLocation = loginServerConfig.GetString ("FileNameOfTOS", "");

                if (m_TOSLocation.Length > 0) {
                    if (m_TOSLocation.ToLower ().StartsWith ("http://", StringComparison.Ordinal))
                        m_TOSLocation = m_TOSLocation.Replace ("ServersHostname", MainServer.Instance.HostName);
                    else {
                        var simBase = registry.RequestModuleInterface<ISimulationBase> ();
                        var TOSFileName = PathHelpers.VerifyReadFile (m_TOSLocation, ".txt", simBase.DefaultDataPath + "/html");
                        if (TOSFileName == "") {
                            m_UseTOS = false;
                            MainConsole.Instance.ErrorFormat ("Unable to locate the Terms of Service file : '{0}'", m_TOSLocation);
                            MainConsole.Instance.Error ("Displaying 'Terms of Service' for a new user login is disabled!");
                        } else
                            m_TOSLocation = TOSFileName;
                    }
                } else
                    m_UseTOS = false;
            }

            m_AuthenticationService = registry.RequestModuleInterface<IAuthenticationService> ();
            m_LoginService = service;
        }

        public LoginResponse Login (Hashtable request, UserAccount account, IAgentInfo agentInfo, string authType,
                                   string password, out object data)
        {
            IAgentConnector agentData = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector> ();
            data = null;

            if (request == null)
                return null;
            // If its null, its just a verification request, allow them to see things even if they are banned

            bool tosExists = false;
            string tosAccepted = "";
            if (request.ContainsKey ("agree_to_tos")) {
                tosExists = true;
                tosAccepted = request ["agree_to_tos"].ToString ();
            }

            // MAC BANNING START
            var mac = (string)request ["mac"];
            if (mac == "") {
                data = "Bad Viewer Connection";
                return new LLFailedLoginResponse (LoginResponseEnum.Indeterminant, data.ToString (), false);
            }

            // TODO: Some TPV's now send their version in the Channel
            /*
            string channel = "Unknown";
            if (request.Contains("channel") && request["channel"] != null)
                channel = request["channel"].ToString();
            */

            bool acceptedNewTOS = false;
            // This gets if the viewer has accepted the new TOS
            if (!agentInfo.AcceptTOS && tosExists) {
                if (tosAccepted == "0")
                    acceptedNewTOS = false;
                else if (tosAccepted == "1")
                    acceptedNewTOS = true;
                else
                    acceptedNewTOS = bool.Parse (tosAccepted);

                if (agentInfo.AcceptTOS != acceptedNewTOS) {
                    agentInfo.AcceptTOS = acceptedNewTOS;
                    agentData.UpdateAgent (agentInfo);
                }
            }
            if (!acceptedNewTOS && !agentInfo.AcceptTOS && m_UseTOS) {
                data = "TOS not accepted";
                if (m_TOSLocation.ToLower ().StartsWith ("http://", StringComparison.Ordinal))
                    return new LLFailedLoginResponse (LoginResponseEnum.ToSNeedsSent, m_TOSLocation, false);

                // text file
                var tosText = File.ReadAllText (Path.Combine (Environment.CurrentDirectory, m_TOSLocation));
                return new LLFailedLoginResponse (LoginResponseEnum.ToSNeedsSent, tosText, false);
            }
            if ((agentInfo.Flags & IAgentFlags.PermBan) == IAgentFlags.PermBan) {
                MainConsole.Instance.InfoFormat (
                    "[Login service]: Login failed for user {0}, reason: user is permanently banned.", account.Name);
                data = "Permanently banned";
                return LLFailedLoginResponse.PermanentBannedProblem;
            }

            if ((agentInfo.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan) {
                bool isBanned = true;
                string until = "";

                if (agentInfo.OtherAgentInformation.ContainsKey ("TemporaryBanInfo")) {
                    DateTime bannedTime = agentInfo.OtherAgentInformation ["TemporaryBanInfo"].AsDate ();
                    until = string.Format (" until {0} {1}",
                                           bannedTime.ToLocalTime ().ToShortDateString (),
                                           bannedTime.ToLocalTime ().ToLongTimeString ());

                    // Check to make sure the time hasn't expired
                    if (bannedTime.Ticks < DateTime.Now.ToUniversalTime ().Ticks) {
                        // The banned time is less than now, let the user in.
                        isBanned = false;
                    }
                }

                if (isBanned) {
                    MainConsole.Instance.InfoFormat (
                        "[Login service]: Login failed for user {0}, reason: user is temporarily banned {1}.",
                        account.Name, until);
                    data = string.Format ("You are blocked from connecting to this service{0}.", until);
                    return new LLFailedLoginResponse (LoginResponseEnum.Indeterminant, data.ToString (), false);
                }
            }

            return null;        // not banned or otherwise in error
        }
    }
}
