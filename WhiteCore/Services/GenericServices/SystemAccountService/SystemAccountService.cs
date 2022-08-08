/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
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
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using WhiteCore.Services.GenericServices.SystemEstateService;

namespace WhiteCore.Services.GenericServices.SystemAccountService
{
    /// <summary>
    ///     Basically a provision to allow user configuration of the system user accounts
    /// </summary>
    public class SystemAccountService : ISystemAccountService, IService
    {
        IUserAccountService m_accountService;

        string governorName = Constants.GovernorName;
        string realEstateOwnerName = Constants.RealEstateOwnerName;
        string bankerName = Constants.BankerName;
        string marketplaceOwnerName = Constants.MarketplaceOwnerName;
        string libraryOwnerName = Constants.LibraryOwnerName;
        string [] m_userNameSeed;

        IRegistryCore m_registry;
        IConfigSource m_config;

        #region ISystemAccountService Members

        public UUID GovernorUUID {
            get { return (UUID)Constants.GovernorUUID; }
        }

        public string GovernorName {
            get { return governorName; }
        }

        public UUID SystemEstateOwnerUUID {
            get { return (UUID)Constants.RealEstateOwnerUUID; }
        }

        public string SystemEstateOwnerName {
            get { return realEstateOwnerName; }
        }

        public UUID BankerUUID {
            get { return (UUID)Constants.BankerUUID; }
        }

        public string BankerName {
            get { return bankerName; }
        }

        public UUID MarketplaceOwnerUUID {
            get { return (UUID)Constants.MarketplaceOwnerUUID; }
        }

        public string MarketplaceOwnerName {
            get { return marketplaceOwnerName; }
        }

        public UUID LibraryOwnerUUID {
            get { return (UUID)Constants.LibraryOwnerUUID; }
        }

        public string LibraryOwnerName {
            get { return libraryOwnerName; }
        }

        public string GetSystemEstateOwnerName (int estateID)
        {
            if (estateID == Constants.MainlandEstateID)
                return governorName;

            // System estate then
            return realEstateOwnerName;
        }

        public UUID GetSystemEstateOwner (int estateID)
        {
            if (estateID == Constants.MainlandEstateID) 
                return GovernorUUID;

            // System estate then
            return SystemEstateOwnerUUID;
        }

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {

            IConfig estConfig = config.Configs ["SystemUserService"];
            if (estConfig != null) {
                governorName = estConfig.GetString ("GovernorName", governorName);
                realEstateOwnerName = estConfig.GetString ("RealEstateOwnerName", realEstateOwnerName);
                bankerName = estConfig.GetString ("BankerName", bankerName);
                marketplaceOwnerName = estConfig.GetString ("MarketplaceOwnerName", marketplaceOwnerName);
            }

            IConfig libConfig = config.Configs ["LibraryService"];
            if (libConfig != null) {
                libraryOwnerName = libConfig.GetString ("LibraryOwnerName", libraryOwnerName);
            }

            registry.RegisterModuleInterface<ISystemAccountService> (this);
            m_registry = registry;
            m_config = config;

            // check for WebUI overrides
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
            var settings = generics.GetGeneric<Modules.Web.GridSettings> (UUID.Zero, "GridSettings", "Settings");
            if (settings != null) {
                governorName = settings.GovernorName;
                realEstateOwnerName = settings.RealEstateOwnerName;
                bankerName = settings.BankerName;
                marketplaceOwnerName = settings.MarketplaceOwnerName;
            }

            // final checks in case of configuration errors
            if (string.IsNullOrEmpty(governorName))
                governorName = Constants.GovernorName;
            if (string.IsNullOrEmpty(realEstateOwnerName))
                realEstateOwnerName = Constants.RealEstateOwnerName;
            if (string.IsNullOrEmpty(bankerName))
                bankerName = Constants.BankerName;
            if (string.IsNullOrEmpty(marketplaceOwnerName))
                marketplaceOwnerName = Constants.MarketplaceOwnerName;
            if (string.IsNullOrEmpty(libraryOwnerName))
                libraryOwnerName  = Constants.LibraryOwnerName;

        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup ()
        {
            m_accountService = m_registry.RequestModuleInterface<IUserAccountService> ();

            // these are only valid if we are local
            if (m_accountService.IsLocalConnector) {
                var sysEstateSvc = m_registry.RequestModuleInterface<ISystemEstateService> ();

                // if this is the initial run, create the grid owner user and estate
                var users = m_accountService.NumberOfUserAccounts (null, "");
                if (users == 0) {
                    MainConsole.Instance.Info ("Creating grid owner");
                    CreateGridOwnerUser ();
                    sysEstateSvc.CheckGridOwnerEstate ();
                }

                // check and/or create default system users
                MainConsole.Instance.Info ("Verifying system users");
                CheckSystemUserInfo ();
                sysEstateSvc.CheckSystemEstates ();

                AddCommands ();
            }

        }

        #endregion

        void AddCommands ()
        {
            if (MainConsole.Instance != null) {
                MainConsole.Instance.Commands.AddCommand (
                    "reset governor password",
                    "reset governor password",
                    "Resets the password of the system Governor",
                    HandleResetGovernorPassword, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "reset realestate password",
                    "reset realestate password",
                    "Resets the password of the system Estate Owner",
                    HandleResetRealEstatePassword, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "reset banker password",
                    "reset banker password",
                    "Resets the password of the system Banker",
                    HandleResetBankerPassword, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "reset marketplace password",
                    "reset marketplace password",
                    "Resets the password of the system Marketplace Owner",
                    HandleResetMarketplacePassword, false, true);

            }
        }

        #region systemUsers

        /// <summary>
        /// Checks and creates the system users.
        /// </summary>
        void CheckSystemUserInfo ()
        {
            // a bit of protection in case of configuration errors
            if (GovernorName == "")
                governorName = Constants.GovernorName;
            if (SystemEstateOwnerName == "")
                realEstateOwnerName = Constants.RealEstateOwnerName;
            if (BankerName == "")
                bankerName = Constants.BankerName;
            if (MarketplaceOwnerName == "")
                marketplaceOwnerName = Constants.MarketplaceOwnerName;
            if (LibraryOwnerName == "")
                libraryOwnerName = Constants.LibraryOwnerName;

            if (m_accountService == null) {
                MainConsole.Instance.Info ("No user account service available");
                return;
            }

            VerifySystemUserInfo("Governor", GovernorUUID, GovernorName, Constants.USER_GOD_MAINTENANCE);
            VerifySystemUserInfo("RealEstate", SystemEstateOwnerUUID, SystemEstateOwnerName, Constants.USER_GOD_LIASON);
            VerifySystemUserInfo("Banker", BankerUUID, BankerName, Constants.USER_GOD_CUSTOMER_SERVICE);
            VerifySystemUserInfo("Marketplace", MarketplaceOwnerUUID, MarketplaceOwnerName, Constants.USER_DISABLED);
            VerifySystemUserInfo("Library", LibraryOwnerUUID, LibraryOwnerName, Constants.USER_DISABLED);
        }

        void VerifySystemUserInfo (string usrType, UUID usrUUID, string usrName, int usrLevel)
        {
            MainConsole.Instance.Info ("Checking system account for " + usrType);
                
            var userAcct = m_accountService.GetUserAccount (null, usrUUID);
            var userPassword = Utilities.RandomPassword.Generate (2, 3, 0);

            if (!userAcct.Valid) {
                MainConsole.Instance.WarnFormat ("Creating the {0} user '{1}'", usrType, usrName);

                var error = m_accountService.CreateUser (
                    usrUUID,                                // user UUID
                    UUID.Zero,                              // scope
                    usrName,                                // name
                    Util.Md5Hash (userPassword),            // password
                    "");                                    // email

                if (error == "") {
                    SaveSystemUserPassword (usrType, usrName, userPassword);
                    MainConsole.Instance.InfoFormat (" The password for '{0}' is : {1}", usrName, userPassword);

                } else {
                    MainConsole.Instance.WarnFormat (" Unable to create the {0} user : {1}", usrType, error);
                    return;
                }

                //set  "God" level
                var godAcct = m_accountService.GetUserAccount (null, usrUUID);
                godAcct.UserLevel = usrLevel;
                godAcct.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                bool success = m_accountService.StoreUserAccount (godAcct);

                if (success)
                    MainConsole.Instance.InfoFormat (" The {0} user has been set to '{1}' level", usrType, m_accountService.UserGodLevel (usrLevel));

                return;

            }

            MainConsole.Instance.Info ("Found system account for " + usrType);

            // we already have the account.. verify details in case of a configuration change
            if (userAcct.Name != usrName) {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();

                userAcct.Name = usrName;
                bool updatePass = authService.SetPassword (userAcct.PrincipalID, "UserAccount", userPassword);
                bool updateAcct = m_accountService.StoreUserAccount (userAcct);

                if (updatePass && updateAcct) {
                    SaveSystemUserPassword (usrType, usrName, userPassword);
                    MainConsole.Instance.InfoFormat (" The {0} user has been updated to '{1}'", usrType, usrName);
                } else
                    MainConsole.Instance.WarnFormat (" There was a problem updating the {0} user", usrType);
            }

        }

        // Save passwords for later
        void SaveSystemUserPassword (string userType, string userName, string password)
        {
            var simBase = m_registry.RequestModuleInterface<ISimulationBase> ();
            string logpath = MainConsole.Instance.LogPath;
            if (logpath == "")
                logpath = simBase.DefaultDataPath;
            string passFile = Path.Combine (logpath, userType + ".txt");
            string userInfo = userType + " user";

            if (File.Exists (passFile))
                File.Delete (passFile);

            using (var pwFile = new StreamWriter (passFile)) {
                pwFile.WriteLine (userInfo.PadRight (20) + " : '" + userName + "' was created: " + Culture.LocaleLogStamp ());
                pwFile.WriteLine ("Password             : " + password);
            }
        }

        /// <summary>
        /// Creates the grid owner user on a clean startup.
        /// Sets user to 'Charter Meneber" and elevates to  "God" status
        /// </summary>
        void CreateGridOwnerUser ()
        {

            string userName = "";
            string password, email, uuid;

            // Get user name
            // check for user name seed
            IConfig loginConfig = m_config.Configs ["LoginService"];
            if (loginConfig != null) {
                string userNameSeed = loginConfig.GetString ("UserNameSeed", "");
                if (userNameSeed != "")
                    m_userNameSeed = userNameSeed.Split (',');
            }

            var ufNames = new Utilities.MarkovNameGenerator ();
            var ulNames = new Utilities.MarkovNameGenerator ();
            string [] nameSeed = m_userNameSeed == null ? Utilities.UserNames : m_userNameSeed;

            string firstName = ufNames.FirstName (nameSeed, 3, 4);
            string lastName = ulNames.FirstName (nameSeed, 5, 6);
            string enteredName = firstName + " " + lastName;
            if (userName != "")
                enteredName = userName;

            MainConsole.Instance.CleanInfo ("");
            MainConsole.Instance.Warn ("Please enter the user name of the grid owner");

            do {
                userName = MainConsole.Instance.Prompt ("Grid owner user name (? for suggestion)", enteredName);
                if (userName == "" || userName == "?") {
                    enteredName = ufNames.NextName + " " + ulNames.NextName;
                    userName = "";
                    continue;
                }
                var fl = userName.Split (' ');
                if (fl.Length < 2) {
                    MainConsole.Instance.CleanInfo ("    User name must be <firstname> <lastname>");
                    userName = "";
                }
            } while (userName == "");
            ufNames.Reset ();
            ulNames.Reset ();

            // password 
            var pwmatch = false;
            do {
                password = MainConsole.Instance.PasswordPrompt ("Password");
                if (password == "") {
                    MainConsole.Instance.CleanInfo (" .... password must not be empty, please re-enter");
                    continue;
                }
                var passwordAgain = MainConsole.Instance.PasswordPrompt ("Re-enter Password");
                pwmatch = (password == passwordAgain);
                if (!pwmatch)
                    MainConsole.Instance.CleanInfo (" .... passwords did not match, please re-enter");
            } while (!pwmatch);

            // email
            email = MainConsole.Instance.Prompt ("Email for password recovery. ('none' if unknown)", "none");

            if ((email.ToLower () != "none") && !Utilities.IsValidEmail (email)) {
                MainConsole.Instance.CleanInfo ("This does not look like a valid email address. ('none' if unknown)");
                email = MainConsole.Instance.Prompt ("Email", email);
            }

            // Get available user avatar archives
            var userAvatarArchive = "";
            IAvatarAppearanceArchiver avieArchiver = m_registry.RequestModuleInterface<IAvatarAppearanceArchiver> ();
            if (avieArchiver != null) {
                List<string> avatarArchives = avieArchiver.GetAvatarArchiveFilenames ();

                if (avatarArchives.Count > 0) {
                    avatarArchives.Add ("None");
                    userAvatarArchive = MainConsole.Instance.Prompt ("Avatar archive to use", "None", avatarArchives);
                    if (userAvatarArchive == "None")
                        userAvatarArchive = "";
                }
            }

            // Allow the modification of UUID if required - for matching user UUID with other Grids etc like SL
            uuid = UUID.Random ().ToString ();
            while (true) {
                uuid = MainConsole.Instance.Prompt ("Required avatar UUID (optional))", uuid);
                UUID test;
                if (UUID.TryParse (uuid, out test))
                    break;

                MainConsole.Instance.Error ("There was a problem verifying this UUID. Please retry.");
            }

            // this really should not normally be altered so hide it
            //scopeID = UUID.Zero.ToString ();
            //if (sysFlag) {
            //    scopeID = MainConsole.Instance.Prompt ("Scope (Don't change unless you know what this is)", scopeID);
            //}

            // we should be good to go
            //m_accountService.CreateUser (UUID.Parse (uuid), UUID.Parse (scopeID), userName, Util.Md5Hash (password), email);
            m_accountService.CreateUser (UUID.Parse (uuid), UUID.Zero, userName, Util.Md5Hash (password), email);
            // CreateUser will tell us success or problem
            //MainConsole.Instance.InfoFormat("[User account service]: User '{0}' created", name);

            // check for success
            UserAccount userAcct = m_accountService.GetUserAccount (null, userName);
            if (userAcct.Valid) {
                userAcct.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                userAcct.UserLevel = 250;
                m_accountService.StoreUserAccount (userAcct);

                // update profile for the user as well
                var profileConnector = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ();
                if (profileConnector != null) {
                    var profile = profileConnector.GetUserProfile (userAcct.PrincipalID);
                    if (profile == null) {
                        profileConnector.CreateNewProfile (userAcct.PrincipalID);          // create a profile for the user
                        profile = profileConnector.GetUserProfile (userAcct.PrincipalID);
                    }

                    if (userAvatarArchive != "")
                        profile.AArchiveName = userAvatarArchive + ".aa";
                    profile.MembershipGroup = "Charter_Member";
                    profile.IsNewUser = true;
                    profileConnector.UpdateUserProfile (profile);
                }
            } else
               MainConsole.Instance.WarnFormat ("[System User account service]: There was a problem creating the account for '{0}'", userName);
        }

        #endregion

        #region Commands
        protected void HandleResetGovernorPassword (IScene scene, string [] cmd)
        {
            ResetSystemPassword ("Governor", GovernorName);
        }

        protected void HandleResetRealEstatePassword (IScene scene, string [] cmd)
        {
            ResetSystemPassword ("RealEstate", SystemEstateOwnerName);
        }

        protected void HandleResetBankerPassword (IScene scene, string [] cmd)
        {
            ResetSystemPassword ("Banker", BankerName);
        }

        protected void HandleResetMarketplacePassword (IScene scene, string [] cmd)
        {
            ResetSystemPassword ("Marketplace", MarketplaceOwnerName);
        }

        void ResetSystemPassword (string userType, string systemUserName)
        {
            string question;

            question = MainConsole.Instance.Prompt ("Are you really sure that you want to reset the " + userType + " user password ? (yes/no)", "no");
            question = question.ToLower ();
            if (question.StartsWith ("y", StringComparison.Ordinal)) {
                var newPassword = Utilities.RandomPassword.Generate (2, 3, 0);

                UserAccount userAcct = m_accountService.GetUserAccount (null, systemUserName);
                bool success = false;

                if (userAcct.Valid) {
                    IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();
                    if (authService != null)
                        success = authService.SetPassword (userAcct.PrincipalID, "UserAccount", newPassword);

                    if (!success)
                        MainConsole.Instance.ErrorFormat ("[System account service]: Unable to reset password for the " + userType);
                    else {
                        SaveSystemUserPassword (userType, systemUserName, newPassword);
                        MainConsole.Instance.Info ("[System account service]: The new password for '" + userAcct.Name + "' is : " + newPassword);
                    }
                }
            }
        }


        #endregion
    }
}
