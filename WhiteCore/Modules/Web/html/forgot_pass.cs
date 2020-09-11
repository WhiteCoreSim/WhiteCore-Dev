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
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class ForgotPassMain : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/forgot_pass.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return false; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();

            //string error = "";
            if (requestParameters.ContainsKey ("Submit")) {
                string username = requestParameters ["username"].ToString ();
                string UserEmail = requestParameters ["UserEmail"].ToString ();

                UserAccount userAcct =
                    webInterface.Registry.RequestModuleInterface<IUserAccountService> ()
                        .GetUserAccount (null, username);

                if (!userAcct.Valid) {
                    response = webInterface.UserMsg("!Please enter a valid username", false);
                    return null;
                }

                // email user etc here...
                if (userAcct.Email == "") {
                    response = webInterface.UserMsg("!Sorry! Your account has no email details<br/>Please contact the administrator to correct", true);

                    /*response = "<h3>Sorry! Your account has no email details. Please contact the administrator to correct</h3>" +
                        "<script language=\"javascript\">" +
                        "setTimeout(function() {window.location.href = \"index.html\";}, 5000);" +
                        "</script>";
                    */
                    return null;
                }

                var emailAddress = userAcct.Email;
                if (UserEmail != emailAddress) {
                    response = webInterface.UserMsg("!Sorry! Unable to authenticate your account<br/>Please contact the administrator to correct", false);

                   /* response = "<h3>Sorry! Unable to authenticate your account.</h3><br />Please contact the administrator to correct" +
                        "<script language=\"javascript\">" +
                        "setTimeout(function() {window.location.href = \"index.html\";}, 5000);" +
                        "</script>";
                   */
                    return null;
                }

                IEmailModule Email = webInterface.Registry.RequestModuleInterface<IEmailModule> ();
                if ((Email != null) && (!Email.LocalOnly ())) {
                    var newPassword = Utilities.RandomPassword.Generate (2, 1, 0);
                    var authService = webInterface.Registry.RequestModuleInterface<IAuthenticationService> ();
                    var gridName = webInterface.Registry.RequestModuleInterface<IGridInfo> ().GridName;
                    bool success = false; 

                    if (authService != null)
                        success = authService.SetPassword (userAcct.PrincipalID, "UserAccount", newPassword);

                    if (success) {
                        Email.SendEmail (
                            UUID.Zero,
                            emailAddress,
                            "Password reset request",
                            string.Format ("This request was made via the {0} WebUi at {1}\n\nYour new passsword is : {2}",
                                gridName, Culture.LocaleTimeDate (), newPassword),
                            null);
                        response = webInterface.UserMsg("An email has been sent with your new password", true);

                        //response = "<h3>An email has been sent with your new password</h3>Redirecting to main page";
                    } else
                        response = webInterface.UserMsg("!Sorry! Your password was not able to be reset<br/>Please contact the administrator", true);
                    //response = "<h3>Sorry! Your password was not able to be reset.<h3>Please contact the administrator directly<br>Redirecting to main page</h3>";
                } else
                    response = webInterface.UserMsg("!The email functions are local to the grid or have not yet been set up<br/>Please contact the administrator", true);
                    //response = "<h3>The email functions are local to the grid or have not yet been set up<h3>Please contact the administrator directly<br>Redirecting to main page</h3>";


                /*response = response +
                    "<script language=\"javascript\">" +
                    "setTimeout(function() {window.location.href = \"index.html\";}, 5000);" +
                        "</script>";
                */
                return null;
            }


            //vars.Add ("ErrorMessage", error);
            vars.Add ("ForgotPassword", translator.GetTranslatedString ("ForgotPassword"));
            vars.Add ("UserNameText", translator.GetTranslatedString ("UserName"));
            vars.Add ("UserEmailText", translator.GetTranslatedString ("UserEmailText"));
            vars.Add ("Submit", translator.GetTranslatedString ("Submit"));

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
