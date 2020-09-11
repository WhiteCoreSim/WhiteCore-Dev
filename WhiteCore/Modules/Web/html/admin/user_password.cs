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
using OpenMetaverse;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;

namespace WhiteCore.Modules.Web
{
    public class AdminPasswordPage : IWebInterfacePage
    {
        public string[] FilePath {
            get {
                return new[] {
                    "html/admin/user_password.html"
                };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return true; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
            OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
            ITranslator translator, out string response) {

            response = null;
            var vars = new Dictionary<string, object>();

            //UserAccount user = Authenticator.GetAuthentication(httpRequest);
            //if (user == null) {
            //    response = webInterface.UserMsg("!Unable to authenticate user details", true);
            //    return null;
            //}
            UUID userID = httpRequest.Query.ContainsKey("userid")
                ? UUID.Parse(httpRequest.Query["userid"].ToString())
                : UUID.Parse(requestParameters["userid"].ToString());

            IUserAccountService userService = webInterface.Registry.RequestModuleInterface<IUserAccountService>();
            UserAccount userAcct = new UserAccount();

            if (userService != null)
                userAcct = userService.GetUserAccount(null, userID);
            if (!userAcct.Valid)
            {
                response = webInterface.UserMsg("!Unable to authenticate user details", true);
                return null;
            }

            // who we are dealing with...
            vars.Add("UserName", userAcct.Name);

            // password change
            if (requestParameters.ContainsKey("update")) {
                string passwordnew = requestParameters["passwordnew"].ToString();
                string passwordconf = requestParameters["passwordconf"].ToString();

                if (passwordconf != passwordnew) {
                    response = webInterface.UserMsg("!Passwords do not match");
                    return null;
                }

                IAuthenticationService authService =
                    webInterface.Registry.RequestModuleInterface<IAuthenticationService>();
                if (authService != null) {
                    bool success = authService.SetPassword(userAcct.PrincipalID, "UserAccount", passwordnew);
                    if (success) {
                        response = webInterface.UserMsg("Password has been updated");
                    } else {
                        response = webInterface.UserMsg("!Failed to reset password");
                    }
                } else
                    response = webInterface.UserMsg("!No authentication service was available to change the password", true);

                return null;
            }

            // Page variables
            vars.Add("ChangePasswordText", translator.GetTranslatedString("ChangePasswordText"));
            vars.Add("NewPasswordText", translator.GetTranslatedString("NewPasswordText"));
            vars.Add("NewPasswordConfirmationText", translator.GetTranslatedString("NewPasswordConfirmationText"));
            vars.Add("UserNameText", translator.GetTranslatedString("UserNameText"));
            // vars.Add("PasswordText", translator.GetTranslatedString("PasswordText"));
            vars.Add("CancelText", translator.GetTranslatedString("Cancel"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text) {
            text = "";
            return false;
        }
    }
}
