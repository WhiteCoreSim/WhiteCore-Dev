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
    public class ChangeUserInformationPage : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/change_user_information.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return true; }
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
            UserAccount user = Authenticator.GetAuthentication(httpRequest);
            if (user == null)
            {
                response = "No authentication service was available to change user details";
                return null;
            }

            // who we are dealing with...
            vars.Add ("UserName", user.Name);

            // password change
            if (requestParameters.ContainsKey("Submit") &&
                requestParameters["Submit"].ToString() == "SubmitPasswordChange")
            {
                string password = requestParameters["password"].ToString();
                string passwordconf = requestParameters["passwordconf"].ToString();
                response = "Success";
                if (passwordconf != password)
                    response = "Passwords do not match";
                else
                {
                    IAuthenticationService authService =
                        webInterface.Registry.RequestModuleInterface<IAuthenticationService>();
                    if (authService != null)
                        response = authService.SetPassword(user.PrincipalID, "UserAccount", password)
                            ? "Your password has been updated"
                            : "Failed to set your password, try again later";

                    else
                        response = "No authentication service was available to change your password";
                }
                return null;
            }

            // email change
            if (requestParameters.ContainsKey("Submit") &&
                     requestParameters["Submit"].ToString() == "SubmitEmailChange")
            {
                string email = requestParameters["email"].ToString();

                IUserAccountService userService = webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                if (userService != null)
                {
                    user.Email = email;
                    userService.StoreUserAccount(user);
                    response = "Success";
                }
                else
                    response = "No authentication service was available to change your password";
                return null;
            }

            // Delete User
            if (requestParameters.ContainsKey("Submit") &&
                     requestParameters["Submit"].ToString() == "SubmitDeleteUser")
            {
                string username = requestParameters["username"].ToString();
                string password = requestParameters["password"].ToString();

                ILoginService loginService = webInterface.Registry.RequestModuleInterface<ILoginService>();
                if (loginService.VerifyClient(UUID.Zero, username, "UserAccount", password))
                {
                    IUserAccountService userService =
                        webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                    if (userService != null)
                    {
                        userService.DeleteUser(user.PrincipalID, user.Name, password, true, false);
                        response = "Successfully deleted account.";
                    }
                    else
                        response = "User service unavailable, please try again later";
                }
                else
                    response = "Wrong username or password";
                return null;
            }

            // Page variables
            vars.Add("ErrorMessage", error);
            vars.Add("ChangeUserInformationText", translator.GetTranslatedString("ChangeUserInformationText"));
            vars.Add("ChangePasswordText", translator.GetTranslatedString("ChangePasswordText"));
            vars.Add("NewPasswordText", translator.GetTranslatedString("NewPasswordText"));
            vars.Add("NewPasswordConfirmationText", translator.GetTranslatedString("NewPasswordConfirmationText"));
            vars.Add("ChangeEmailText", translator.GetTranslatedString("ChangeEmailText"));
            vars.Add("NewEmailText", translator.GetTranslatedString("NewEmailText"));
            vars.Add("UserNameText", translator.GetTranslatedString("UserNameText"));
            vars.Add("PasswordText", translator.GetTranslatedString("PasswordText"));
            vars.Add("DeleteUserText", translator.GetTranslatedString("DeleteUserText"));
            vars.Add("DeleteText", translator.GetTranslatedString("DeleteText"));
            vars.Add("DeleteUserInfoText", translator.GetTranslatedString("DeleteUserInfoText"));
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