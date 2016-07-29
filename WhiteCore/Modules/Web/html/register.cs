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
using System.IO;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;


namespace WhiteCore.Modules.Web
{
    public class RegisterPage : IWebInterfacePage
    {

        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/register.html"
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

        string ShortMonthToNumber(string key)
        {
            switch ( key )
            {
            case "Jan_Short":
                return  "1";
            case "Feb_Short":
                return  "2";
            case "Mar_Short":
                return  "3";
            case "Apr_Short":
                return  "4";
            case "May_Short":
                return  "5";
            case "Jun_Short":
                return   "6";
            case "Jul_Short":
                return   "7";
            case "Aug_Short":
                return   "8";
            case "Sep_Short":
                return   "9";
            case "Oct_Short":
                return  "10";
            case "Nov_Short":
                return  "11";
            case "Dec_Short":
                return  "12";
            default:
                return "1";
            }
        }
            

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();
            var settings = webInterface.GetWebUISettings();

            bool adminUser = Authenticator.CheckAdminAuthentication(httpRequest, Constants.USER_GOD_CUSTOMER_SERVICE);
            bool allowRegistration = settings.WebRegistration;
            bool anonymousLogins;

            // allow configuration to override the web settings
            var simBase = webInterface.Registry.RequestModuleInterface<ISimulationBase>();
            IConfig loginServerConfig = simBase.ConfigSource.Configs["LoginService"];
            if (loginServerConfig != null)
            {
                anonymousLogins = loginServerConfig.GetBoolean ("AllowAnonymousLogin", allowRegistration);
                allowRegistration = (allowRegistration || anonymousLogins);
            }

            if (!adminUser && !allowRegistration)
            {
                vars.Add("ErrorMessage", "");
                vars.Add("RegistrationText", translator.GetTranslatedString("RegistrationText"));
                vars.Add("RegistrationsDisabled", translator.GetTranslatedString("RegistrationsDisabled"));
                vars.Add("Registrations", false);
                vars.Add("NoRegistrations", true);
                return vars;
             }

            if (requestParameters.ContainsKey("Submit"))
            {
                string AvatarName = requestParameters["AvatarName"].ToString();
                string AvatarPassword = requestParameters["AvatarPassword"].ToString();
                string AvatarPasswordCheck = requestParameters["AvatarPassword2"].ToString();
                string FirstName = requestParameters["FirstName"].ToString();
                string LastName = requestParameters["LastName"].ToString();
                //removed - greythane - deemed not used
                //string UserAddress = requestParameters["UserAddress"].ToString();
                //string UserZip = requestParameters["UserZip"].ToString();
                string UserCity = requestParameters["UserCity"].ToString();
                string UserEmail = requestParameters["UserEmail"].ToString();
                string UserHomeRegion = requestParameters["UserHomeRegion"].ToString();
                string UserDOBMonth = requestParameters["UserDOBMonth"].ToString();
                string UserDOBDay = requestParameters["UserDOBDay"].ToString();
                string UserDOBYear = requestParameters["UserDOBYear"].ToString();
                string AvatarArchive = requestParameters.ContainsKey("AvatarArchive")
                                           ? requestParameters["AvatarArchive"].ToString()
                                           : "";
                bool ToSAccept = requestParameters.ContainsKey("ToSAccept") &&
                                 requestParameters["ToSAccept"].ToString() == "Accepted";

                string UserType = requestParameters.ContainsKey("UserType")         // only admins can set membership
                    ? requestParameters ["UserType"].ToString ()
                    : "Resident";

                // revise UserDOBMonth to a number
                UserDOBMonth = ShortMonthToNumber(UserDOBMonth);

                // revise Type flags
                int UserFlags = webInterface.UserTypeToUserFlags (UserType);

                // a bit of idiot proofing
                if (AvatarName == "")  {
                    response = "<h3>" + translator.GetTranslatedString ("AvatarNameError") + "</h3>";   
                    return null;
                }
                if ( (AvatarPassword == "") || (AvatarPassword != AvatarPasswordCheck) )
                {
                    response = "<h3>" + translator.GetTranslatedString ("AvatarPasswordError") + "</h3>";   
                    return null;
                } 
                if (UserEmail == "")
                {
                    response = "<h3>" + translator.GetTranslatedString ("AvatarEmailError") + "</h3>";   
                    return null;
                }

                // Thish -  Only one space is allowed in the name to seperate First and Last of the avatar name
                if (AvatarName.Split (' ').Length != 2)
                {
                    response = "<h3>" + translator.GetTranslatedString("AvatarNameSpacingError") + "</h3>";
                    return null;
                }

                // so far so good...
                if (ToSAccept)
                {
                    AvatarPassword = Util.Md5Hash(AvatarPassword);

                    IUserAccountService accountService =
                        webInterface.Registry.RequestModuleInterface<IUserAccountService>();
                    UUID userID = UUID.Random();
                    string error = accountService.CreateUser(userID, settings.DefaultScopeID, AvatarName, AvatarPassword,
                                                             UserEmail);
                    if (error == "")
                    {
                        // set the user account type
                        UserAccount account = accountService.GetUserAccount (null, userID);
                        account.UserFlags = UserFlags;
                        accountService.StoreUserAccount (account);

                        // create and save agent info
                        IAgentConnector con = Framework.Utilities.DataManager.RequestPlugin<IAgentConnector> ();
                        con.CreateNewAgent (userID);
                        IAgentInfo agent = con.GetAgent (userID);
                        agent.OtherAgentInformation ["RLFirstName"] = FirstName;
                        agent.OtherAgentInformation ["RLLastName"] = LastName;
                        //agent.OtherAgentInformation ["RLAddress"] = UserAddress;
                        agent.OtherAgentInformation ["RLCity"] = UserCity;
                        //agent.OtherAgentInformation ["RLZip"] = UserZip;
                        agent.OtherAgentInformation ["UserDOBMonth"] = UserDOBMonth;
                        agent.OtherAgentInformation ["UserDOBDay"] = UserDOBDay;
                        agent.OtherAgentInformation ["UserDOBYear"] = UserDOBYear;
                        agent.OtherAgentInformation ["UserFlags"] = UserFlags;
                        /*if (activationRequired)
                        {
                            UUID activationToken = UUID.Random();
                            agent.OtherAgentInformation["WebUIActivationToken"] = Util.Md5Hash(activationToken.ToString() + ":" + PasswordHash);
                            resp["WebUIActivationToken"] = activationToken;
                        }*/
                        con.UpdateAgent (agent);

                        // create user profile details
                        IProfileConnector profileData =
                            Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ();
                        if (profileData != null)
                        {
                            IUserProfileInfo profile = profileData.GetUserProfile (userID);
                            if (profile == null)
                            {
                                profileData.CreateNewProfile (userID);
                                profile = profileData.GetUserProfile (userID);
                            }

                            if (AvatarArchive != "")
                            {
                                profile.AArchiveName = AvatarArchive;

                                List<AvatarArchive> avarchives = webInterface.Registry.RequestModuleInterface<IAvatarAppearanceArchiver>().GetAvatarArchives();
                                UUID snapshotUUID = UUID.Zero;
                                foreach (var archive in avarchives)
                                    if (archive.FolderName == AvatarArchive)
                                        snapshotUUID = archive.Snapshot;

                                if (snapshotUUID != UUID.Zero)
                                    profile.Image = snapshotUUID;
                            }
                            profile.MembershipGroup = webInterface.UserFlagToType (UserFlags, webInterface.EnglishTranslator);    // membership is english
                            profile.IsNewUser = true;
                            profileData.UpdateUserProfile (profile);
                        }

                        IAgentInfoService agentInfoService = webInterface.Registry.RequestModuleInterface<IAgentInfoService> ();
                        Vector3 position = new Vector3 (128, 128, 25);
                        Vector3 lookAt = new Vector3 (0, 0, 22);

                        if (agentInfoService != null)
                            agentInfoService.SetHomePosition (userID.ToString (), (UUID) UserHomeRegion, position, lookAt);

                        vars.Add ("ErrorMessage", "Successfully created account, redirecting to main page");
                        response = "<h3>Successfully created account, redirecting to main page</h3>" +
                        "<script language=\"javascript\">" +
                        "setTimeout(function() {window.location.href = \"index.html\";}, 3000);" +
                        "</script>";
                    } else
                    {
                        vars.Add ("ErrorMessage", "<h3>" + error + "</h3>");
                        response = "<h3>" + error + "</h3>";
                    }
                }
                else
                    response = "<h3>You did not accept the Terms of Service agreement.</h3>";
                return null;
            }

            List<Dictionary<string, object>> daysArgs = new List<Dictionary<string, object>>();
            for (int i = 1; i <= 31; i++)
                daysArgs.Add(new Dictionary<string, object> {{"Value", i}});

            List<Dictionary<string, object>> monthsArgs = new List<Dictionary<string, object>>();
            //for (int i = 1; i <= 12; i++)
            //    monthsArgs.Add(new Dictionary<string, object> {{"Value", i}});

            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Jan_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Feb_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Mar_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Apr_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("May_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Jun_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Jul_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Aug_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Sep_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Oct_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Nov_Short")}});
            monthsArgs.Add(new Dictionary<string, object> {{"Value", translator.GetTranslatedString("Dec_Short")}});



            List<Dictionary<string, object>> yearsArgs = new List<Dictionary<string, object>>();
            for (int i = 1950; i <= 2013; i++)
                yearsArgs.Add(new Dictionary<string, object> {{"Value", i}});

            vars.Add("Days", daysArgs);
            vars.Add("Months", monthsArgs);
            vars.Add("Years", yearsArgs);

            var sortBy = new Dictionary<string, bool>();
            sortBy.Add("RegionName", true);

            var RegionListVars = new List<Dictionary<string, object>>();
            var regions = Framework.Utilities.DataManager.RequestPlugin<IRegionData>().Get((RegionFlags) 0,
                RegionFlags.Hyperlink |
                RegionFlags.Foreign |
                RegionFlags.Hidden,
                null, null, sortBy);
            foreach (var region in regions)
            {

                RegionListVars.Add (new Dictionary<string, object> {
                    { "RegionName", region.RegionName },
                    { "RegionUUID", region.RegionID }
                });
            }

            vars.Add("RegionList", RegionListVars);
            vars.Add("UserHomeRegionText", translator.GetTranslatedString("UserHomeRegionText"));


            vars.Add("UserTypeText", translator.GetTranslatedString("UserTypeText"));
            vars.Add("UserType", webInterface.UserTypeArgs(translator)) ;

            var avArchiver = webInterface.Registry.RequestModuleInterface<IAvatarAppearanceArchiver> ();
            var archives = avArchiver.GetAvatarArchives ();
           
            List<Dictionary<string, object>> avatarArchives = new List<Dictionary<string, object>>();
            IWebHttpTextureService webTextureService = webInterface.Registry.RequestModuleInterface<IWebHttpTextureService>();
            foreach (var archive in archives) {
                var archiveInfo = new Dictionary<string, object> ();
                archiveInfo.Add ("AvatarArchiveName", archive.FolderName);
                archiveInfo.Add ("AvatarArchiveSnapshotID", archive.Snapshot);
                archiveInfo.Add ("AvatarArchiveSnapshotURL", archive.LocalSnapshot != ""
                                 ? webTextureService.GetAvatarImageURL (archive.LocalSnapshot)
                                 : webTextureService.GetTextureURL (archive.Snapshot)
                                );
                avatarArchives.Add (archiveInfo);
            }
            vars.Add("AvatarArchive", avatarArchives);


            string tosLocation = "";
            if (loginServerConfig != null && loginServerConfig.GetBoolean("UseTermsOfServiceOnFirstLogin", false))
            {
                tosLocation = loginServerConfig.GetString("FileNameOfTOS", "");
                tosLocation = PathHelpers.VerifyReadFile (tosLocation,  ".txt", simBase.DefaultDataPath);
            }
            string ToS = "There are no Terms of Service currently. This may be changed at any point in the future.";

            if (tosLocation != "")
            {
                StreamReader reader = new StreamReader(Path.Combine(Environment.CurrentDirectory, tosLocation));
                try
                {
                    ToS = reader.ReadToEnd();
                }
                finally
                {
                    reader.Close();
                }
            }

            // check for user name seed
            string[] m_userNameSeed = null;
            if (loginServerConfig != null)
            {
                string userNameSeed = loginServerConfig.GetString ("UserNameSeed", "");
                if (userNameSeed != "")
                    m_userNameSeed = userNameSeed.Split (',');
            }

            Utilities.MarkovNameGenerator ufNames = new Utilities.MarkovNameGenerator ();
            Utilities.MarkovNameGenerator ulNames = new Utilities.MarkovNameGenerator ();
            string[] nameSeed = m_userNameSeed == null ? Utilities.UserNames : m_userNameSeed;

            string firstName = ufNames.FirstName (nameSeed, 3, 4);
            string lastName = ulNames.FirstName (nameSeed, 5, 6);
            string enteredName = firstName + " " + lastName;

            vars.Add("AvatarName",enteredName);
            vars.Add("ToSMessage", ToS);
            vars.Add("TermsOfServiceAccept", translator.GetTranslatedString("TermsOfServiceAccept"));
            vars.Add("TermsOfServiceText", translator.GetTranslatedString("TermsOfServiceText"));
            vars.Add("RegistrationsDisabled", "");
            //vars.Add("RegistrationsDisabled", translator.GetTranslatedString("RegistrationsDisabled"));
            vars.Add("RegistrationText", translator.GetTranslatedString("RegistrationText"));
            vars.Add("AvatarNameText", translator.GetTranslatedString("AvatarNameText"));
            vars.Add("AvatarPasswordText", translator.GetTranslatedString("Password"));
            vars.Add("AvatarPasswordConfirmationText", translator.GetTranslatedString("PasswordConfirmation"));
            vars.Add("AvatarScopeText", translator.GetTranslatedString("AvatarScopeText"));
            vars.Add("FirstNameText", translator.GetTranslatedString("FirstNameText"));
            vars.Add("LastNameText", translator.GetTranslatedString("LastNameText"));
            vars.Add("UserAddressText", translator.GetTranslatedString("UserAddressText"));
            vars.Add("UserZipText", translator.GetTranslatedString("UserZipText"));
            vars.Add("UserCityText", translator.GetTranslatedString("UserCityText"));
            vars.Add("UserCountryText", translator.GetTranslatedString("UserCountryText"));
            vars.Add("UserDOBText", translator.GetTranslatedString("UserDOBText"));
            vars.Add("UserEmailText", translator.GetTranslatedString("UserEmailText"));
            vars.Add("Accept", translator.GetTranslatedString("Accept"));
            vars.Add("Submit", translator.GetTranslatedString("Submit"));
            vars.Add("SubmitURL", "register.html");
            vars.Add("ErrorMessage", "");
            vars.Add("Registrations", true);
            vars.Add("NoRegistrations", false);

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
