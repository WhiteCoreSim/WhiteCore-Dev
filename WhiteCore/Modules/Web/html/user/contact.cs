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
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;

namespace WhiteCore.Modules.Web
{
    public class UserContactPage : IWebInterfacePage
    {
        public string[] FilePath {
            get {
                return new[] {
                    "html/user/contact.html"
                };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
            OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
            ITranslator translator, out string response) {

            response = null;
            var vars = new Dictionary<string, object>();

            UserAccount user = Authenticator.GetAuthentication(httpRequest);
            if (user == null) {
                response = webInterface.UserMsg("!Unable to authenticate user details", true);
                return null;
            }

            // who we are dealing with...
            vars.Add("UserName", user.Name);
            //  IUserAccountService acctSrvc = webInterface.Registry.RequestModuleInterface<IUserAccountService>();
            IAgentConnector agentSrvc = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector>();
            IAgentInfo agent = null;

            if (agentSrvc != null) {
                agent = agentSrvc.GetAgent(user.PrincipalID);
            }

            // contact details change
            if (requestParameters.ContainsKey("update")) {
                string UserAddress = requestParameters["useraddress"].ToString();
                string UserZip = requestParameters["userzip"].ToString();
                string UserCity = requestParameters["usercity"].ToString();
                string UserCountry = requestParameters["usercountry"].ToString();

                if (agent != null) {
                    agent.OtherAgentInformation["RLAddress"] = UserAddress;
                    agent.OtherAgentInformation["RLCity"] = UserCity;
                    agent.OtherAgentInformation["RLZip"] = UserZip;
                    agent.OtherAgentInformation["RLCountry"] = UserCountry;
                    agentSrvc.UpdateAgent(agent);

                    response = webInterface.UserMsg("Contact details updated", true);
                } else
                    response = webInterface.UserMsg("!The agent service was not available to update your details", true);

                return null;
            }


            // Page variables
            if (agent != null) {
                vars.Add("RLAddress", agent.OtherAgentInformation["RLAddress"]);
                vars.Add("RLCity", agent.OtherAgentInformation["RLCity"]);
                vars.Add("RLZip", agent.OtherAgentInformation["RLZip"]);
                vars.Add("RLCountry", agent.OtherAgentInformation["RLCountry"]);
                vars.Add("UserAddressText", agent.OtherAgentInformation["RLAddress"]);
                vars.Add("UserCityText", agent.OtherAgentInformation["RLCity"]);
                vars.Add("UserZipText", agent.OtherAgentInformation["RLZip"]);
                vars.Add("UserCountryText", agent.OtherAgentInformation["RLCountry"]);
            } else {
                vars.Add("RLAddress", "");
                vars.Add("RLCity", "");
                vars.Add("RLZip", "");
                vars.Add("RLCountry", "");
                vars.Add("UserAddressText", translator.GetTranslatedString("UserAddressText"));
                vars.Add("UserCityText", translator.GetTranslatedString("UserCityText"));
                vars.Add("UserZipText", translator.GetTranslatedString("UserZipText"));
                vars.Add("UserCountryText", translator.GetTranslatedString("UserCountryText"));
            }

            vars.Add("ChangeContactText", translator.GetTranslatedString("ChangeContactText"));
            //vars.Add("UserNameText", translator.GetTranslatedString("UserNameText"));
            vars.Add("CancelText", translator.GetTranslatedString("Cancel"));
            vars.Add("UpdateContactText", translator.GetTranslatedString("UpdateContactText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text) {
            text = "";
            return false;
        }
    }
}
