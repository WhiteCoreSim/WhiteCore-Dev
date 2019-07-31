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
using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class EditUserPage : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/admin/edit_user.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return true; }
        }


        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();

            string error = "";
            UUID userID = httpRequest.Query.ContainsKey ("userid")
                            ? UUID.Parse (httpRequest.Query ["userid"].ToString ())
                            : UUID.Parse (requestParameters ["userid"].ToString ());

            IUserAccountService userService = webInterface.Registry.RequestModuleInterface<IUserAccountService> ();
            UserAccount userAcct = new UserAccount();

            if (userService != null)
                userAcct = userService.GetUserAccount (null, userID);

            var agentService = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector> ();
            IAgentInfo agent = agentService.GetAgent (userID);

            if (agent == null)
                error = "No agent information is available";

            // Set user type
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitSetUserType") {

                string UserType = requestParameters ["UserType"].ToString ();
                int UserFlags = WebHelpers.UserTypeToUserFlags (UserType);

                // set the user account type
                if (userAcct.Valid) {
                    userAcct.UserFlags = UserFlags;
                    userService.StoreUserAccount (userAcct);
                } else {
                    response = "User account not found - Unable to update!'";
                    return null;
                }

                if (agent != null) {
                    agent.OtherAgentInformation ["UserFlags"] = UserFlags;
                    agentService.UpdateAgent (agent);
                } else {
                    response = "Agent information is not available! Has the user logged in yet?";
                    return null;
                }

                IProfileConnector profileData = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ();
                if (profileData != null) {
                    IUserProfileInfo profile = profileData.GetUserProfile (userID);
                    if (profile == null) {
                        profileData.CreateNewProfile (userID);
                        profile = profileData.GetUserProfile (userID);
                    }

                    profile.MembershipGroup = WebHelpers.UserFlagToType (UserFlags, webInterface.EnglishTranslator);    // membership is english
                    profileData.UpdateUserProfile (profile);
                }

                response = "User account has been updated.";
                return null;
            }

            // Password change
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitPasswordChange") {
                string password = requestParameters ["password"].ToString ();
                string passwordconf = requestParameters ["passwordconf"].ToString ();

                if (password != passwordconf)
                    response = "Passwords do not match";
                else {
                    IAuthenticationService authService =
                        webInterface.Registry.RequestModuleInterface<IAuthenticationService> ();
                    if (authService != null)
                        response = authService.SetPassword (userID, "UserAccount", password)
                                       ? "Successfully set password"
                                       : "Failed to set your password, try again later";
                    else
                        response = "No authentication service was available to change the account passwor!";
                }
                return null;
            }

            // Email change
            if (requestParameters.ContainsKey ("Submit") &&
                     requestParameters ["Submit"].ToString () == "SubmitEmailChange") {
                string email = requestParameters ["email"].ToString ();

                if (userAcct.Valid) {
                    userAcct.Email = email;
                    userService.StoreUserAccount (userAcct);
                    response = "Successfully updated email";
                } else
                    response = "No authentication service was available to change the email details!";
                return null;
            }

            // Delete user
            if (requestParameters.ContainsKey ("Submit") &&
                     requestParameters ["Submit"].ToString () == "SubmitDeleteUser") {
                string username = requestParameters ["username"].ToString ();
                if (userAcct.Valid) {
                    if (username == userAcct.Name) {
                        userService.DeleteUser (userAcct.PrincipalID, userAcct.Name, "", false, false);
                        response = "User has been successfully deleted";
                    } else
                        response = "The user name did not match!";
                } else
                    response = "No account details to verify user against!";

                return null;
            }

            // Temp Ban user
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitTempBanUser") {
                int timeDays = int.Parse (requestParameters ["TimeDays"].ToString ());
                int timeHours = int.Parse (requestParameters ["TimeHours"].ToString ());
                int timeMinutes = int.Parse (requestParameters ["TimeMinutes"].ToString ());

                if (agent != null) {
                    agent.Flags |= IAgentFlags.TempBan;
                    DateTime until = DateTime.Now.AddDays (timeDays).AddHours (timeHours).AddMinutes (timeMinutes);
                    agent.OtherAgentInformation ["Temperory BanInfo"] = until;
                    agentService.UpdateAgent (agent);
                    response = "User has been banned.";
                } else
                    response = "Agent information is not available! Has the user logged in yet?";

                return null;
            }

            // Ban user
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitBanUser") {
                if (agent != null) {
                    agent.Flags |= IAgentFlags.PermBan;
                    agentService.UpdateAgent (agent);
                    response = "User has been banned.";
                } else
                    response = "Agent information is not available! Has the user logged in yet?";

                return null;
            }

            //UnBan user
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitUnbanUser") {

                if (agent != null) {
                    agent.Flags &= ~IAgentFlags.TempBan;
                    agent.Flags &= ~IAgentFlags.PermBan;
                    agent.OtherAgentInformation.Remove ("Temporary BanInfo");
                    agentService.UpdateAgent (agent);
                    response = "User has been unbanned.";
                } else
                    response = "Agent information is not available! Has the user logged in yet?";

                return null;
            }

            // Login as user
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitLoginAsUser") {
                Authenticator.ChangeAuthentication (httpRequest, userAcct);
                webInterface.Redirect (httpResponse, "/");
                return vars;
            }

            // Kick user
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitKickUser") {
                string message = requestParameters ["KickMessage"].ToString ();
                if (userAcct.Valid) {
                    IGridWideMessageModule messageModule =
                        webInterface.Registry.RequestModuleInterface<IGridWideMessageModule> ();
                    if (messageModule != null)
                        messageModule.KickUser (userAcct.PrincipalID, message);
                    response = "User has been kicked.";
                } else
                    response = "Unable to determine user to  kick!";
                return null;
            }

            // Message user
            if (requestParameters.ContainsKey ("Submit") &&
                requestParameters ["Submit"].ToString () == "SubmitMessageUser") {
                string message = requestParameters ["Message"].ToString ();
                if (userAcct.Valid) {
                    IGridWideMessageModule messageModule =
                        webInterface.Registry.RequestModuleInterface<IGridWideMessageModule> ();
                    if (messageModule != null) {
                        messageModule.MessageUser (userAcct.PrincipalID, message);
                        response = "User has been sent the message.";
                    }
                } else 
                    response = "User account details are unavailable to send the message!";

                return null;
            }

            // page variables
            string bannedUntil = "";
            bool userBanned = false;
            if (agent != null)
                userBanned =  ((agent.Flags & IAgentFlags.PermBan) == IAgentFlags.PermBan ||
                               (agent.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan);
            bool TempUserBanned = false;
            if (userBanned) {
                if ((agent.Flags & IAgentFlags.TempBan) == IAgentFlags.TempBan &&
                    agent.OtherAgentInformation ["Temporary BanInfo"].AsDate () < DateTime.Now.ToUniversalTime ()) {
                    userBanned = false;
                    agent.Flags &= ~IAgentFlags.TempBan;
                    agent.Flags &= ~IAgentFlags.PermBan;
                    agent.OtherAgentInformation.Remove ("Temporary BanInfo");
                    agentService.UpdateAgent (agent);
                } else {
                    DateTime bannedTime = agent.OtherAgentInformation ["Temporary BanInfo"].AsDate ();
                    TempUserBanned = bannedTime != Util.UnixEpoch;
                    bannedUntil = string.Format ("{0} {1}", bannedTime.ToShortDateString (), bannedTime.ToLongTimeString ());
                }
            }
            bool userOnline = false;
            IAgentInfoService agentInfoService = webInterface.Registry.RequestModuleInterface<IAgentInfoService> ();
            if (agentInfoService != null) {
                UserInfo Info = null;
                if (userAcct.Valid)
                    Info = agentInfoService.GetUserInfo (userAcct.PrincipalID.ToString ());
                userOnline = Info != null && Info.IsOnline;
            }

            if (userAcct.Valid) {
                vars.Add ("EmailValue", userAcct.Email);
                vars.Add ("UserID", userAcct.PrincipalID);
                vars.Add ("UserName", userAcct.Name);
            } else {
                vars.Add ("EmailValue", "");
                vars.Add ("UserID", "");
                vars.Add ("UserName", "");
            }

            vars.Add ("UserOnline", userOnline);
            vars.Add ("NotUserBanned", !userBanned);
            vars.Add ("UserBanned", userBanned);
            vars.Add ("TempUserBanned", TempUserBanned);
            vars.Add ("BannedUntil", bannedUntil);
            vars.Add ("ErrorMessage", error);
            vars.Add ("ChangeUserInformationText", translator.GetTranslatedString ("ChangeUserInformationText"));
            vars.Add ("ChangePasswordText", translator.GetTranslatedString ("ChangePasswordText"));
            vars.Add ("NewPasswordText", translator.GetTranslatedString ("NewPasswordText"));
            vars.Add ("NewPasswordConfirmationText", translator.GetTranslatedString ("NewPasswordConfirmationText"));
            vars.Add ("ChangeEmailText", translator.GetTranslatedString ("ChangeEmailText"));
            vars.Add ("NewEmailText", translator.GetTranslatedString ("NewEmailText"));
            vars.Add ("UserNameText", translator.GetTranslatedString ("UserNameText"));
            vars.Add ("PasswordText", translator.GetTranslatedString ("PasswordText"));
            vars.Add ("DeleteUserText", translator.GetTranslatedString ("DeleteUserText"));
            vars.Add ("DeleteText", translator.GetTranslatedString ("DeleteText"));
            vars.Add ("DeleteUserInfoText", translator.GetTranslatedString ("DeleteUserInfoText"));
            vars.Add ("Submit", translator.GetTranslatedString ("Submit"));
            vars.Add ("Login", translator.GetTranslatedString ("Login"));
            vars.Add ("TypeUserNameToConfirm", translator.GetTranslatedString ("TypeUserNameToConfirm"));

            vars.Add ("AdminUserTypeInfoText", translator.GetTranslatedString ("AdminUserTypeInfoText"));
            vars.Add ("AdminSetUserTypeText", translator.GetTranslatedString ("UserTypeText"));

            vars.Add ("AdminLoginInAsUserText", translator.GetTranslatedString ("AdminLoginInAsUserText"));
            vars.Add ("AdminLoginInAsUserInfoText", translator.GetTranslatedString ("AdminLoginInAsUserInfoText"));
            vars.Add ("AdminDeleteUserText", translator.GetTranslatedString ("AdminDeleteUserText"));
            vars.Add ("AdminDeleteUserInfoText", translator.GetTranslatedString ("AdminDeleteUserInfoText"));
            vars.Add ("AdminUnbanUserText", translator.GetTranslatedString ("AdminUnbanUserText"));
            vars.Add ("AdminTempBanUserText", translator.GetTranslatedString ("AdminTempBanUserText"));
            vars.Add ("AdminTempBanUserInfoText", translator.GetTranslatedString ("AdminTempBanUserInfoText"));
            vars.Add ("AdminBanUserText", translator.GetTranslatedString ("AdminBanUserText"));
            vars.Add ("AdminBanUserInfoText", translator.GetTranslatedString ("AdminBanUserInfoText"));
            vars.Add ("BanText", translator.GetTranslatedString ("BanText"));
            vars.Add ("UnbanText", translator.GetTranslatedString ("UnbanText"));
            vars.Add ("TimeUntilUnbannedText", translator.GetTranslatedString ("TimeUntilUnbannedText"));
            vars.Add ("EdittingText", translator.GetTranslatedString ("EdittingText"));
            vars.Add ("BannedUntilText", translator.GetTranslatedString ("BannedUntilText"));

            vars.Add ("KickAUserInfoText", translator.GetTranslatedString ("KickAUserInfoText"));
            vars.Add ("KickAUserText", translator.GetTranslatedString ("KickAUserText"));
            vars.Add ("KickMessageText", translator.GetTranslatedString ("KickMessageText"));
            vars.Add ("KickUserText", translator.GetTranslatedString ("KickUserText"));

            vars.Add ("MessageAUserText", translator.GetTranslatedString ("MessageAUserText"));
            vars.Add ("MessageAUserInfoText", translator.GetTranslatedString ("MessageAUserInfoText"));
            vars.Add ("MessageUserText", translator.GetTranslatedString ("MessageUserText"));

            List<Dictionary<string, object>> daysArgs = new List<Dictionary<string, object>> ();
            for (int i = 0; i <= 100; i++)
                daysArgs.Add (new Dictionary<string, object> { { "Value", i } });

            List<Dictionary<string, object>> hoursArgs = new List<Dictionary<string, object>> ();
            for (int i = 0; i <= 23; i++)
                hoursArgs.Add (new Dictionary<string, object> { { "Value", i } });

            List<Dictionary<string, object>> minutesArgs = new List<Dictionary<string, object>> ();
            for (int i = 0; i <= 59; i++)
                minutesArgs.Add (new Dictionary<string, object> { { "Value", i } });

            vars.Add ("Days", daysArgs);
            vars.Add ("Hours", hoursArgs);
            vars.Add ("Minutes", minutesArgs);
            vars.Add ("DaysText", translator.GetTranslatedString ("DaysText"));
            vars.Add ("HoursText", translator.GetTranslatedString ("HoursText"));
            vars.Add ("MinutesText", translator.GetTranslatedString ("MinutesText"));

            vars.Add ("UserType", WebHelpers.UserTypeArgs (translator));

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
