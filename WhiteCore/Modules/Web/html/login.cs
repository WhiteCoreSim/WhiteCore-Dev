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

using WhiteCore.Framework;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using OpenMetaverse;
using System;
using System.Collections.Generic;

namespace WhiteCore.Modules.Web
{
    public class LoginPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/login.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return false; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();

            string error = "";
            if (requestParameters.ContainsKey("username") && requestParameters.ContainsKey("password"))
            {
                string username = requestParameters["username"].ToString();
                string password = requestParameters["password"].ToString();

                ILoginService loginService = webInterface.Registry.RequestModuleInterface<ILoginService>();
                if (loginService.VerifyClient(UUID.Zero, username, "UserAccount", password))
                {
                    UUID sessionID = UUID.Random();
                    UserAccount account =
                        webInterface.Registry.RequestModuleInterface<IUserAccountService>()
                                    .GetUserAccount(null, username);
                    Authenticator.AddAuthentication(sessionID, account);
                    if (account.UserLevel > 0)
                        Authenticator.AddAdminAuthentication(sessionID, account);
                    httpResponse.AddCookie(new System.Web.HttpCookie("SessionID", sessionID.ToString())
                                               {
                                                   Expires =
                                                       DateTime
                                                       .MinValue,
                                                   Path = ""
                                               });

                    response = "<h3>Successfully logged in, redirecting to main page</h3>" +
                               "<script language=\"javascript\">" +
                               "setTimeout(function() {window.location.href = \"index.html\";}, 0);" +
                               "</script>";
                }
                else
                    response = "<h3>Failed to verify user name and password</h3>";
                return null;
            }

            vars.Add("ErrorMessage", error);
            vars.Add("Login", translator.GetTranslatedString("Login"));
            vars.Add("UserNameText", translator.GetTranslatedString("UserName"));
            vars.Add("PasswordText", translator.GetTranslatedString("Password"));
            vars.Add("ForgotPassword", translator.GetTranslatedString("ForgotPassword"));
            vars.Add("Submit", translator.GetTranslatedString("Submit"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}