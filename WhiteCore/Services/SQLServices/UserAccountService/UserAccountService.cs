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

#undef TEST_USERS       // developers only here :)

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
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.SQLServices.UserAccountService
{
    public class UserAccountService : ConnectorBase, IUserAccountService, IService
    {
        #region Declares

        protected IProfileConnector m_profileConnector;
        protected IAuthenticationService m_AuthenticationService;
        protected IUserAccountData m_Database;
        protected GenericAccountCache<UserAccount> m_cache = new GenericAccountCache<UserAccount> ();
        protected string [] m_userNameSeed;
        protected string m_defaultDataPath;
        protected int m_newUserStipend = 0;

        #endregion

        #region IService Members

        public virtual string Name {
            get { return GetType ().Name; }
        }

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs ["Handlers"];
            if (handlerConfig.GetString ("UserAccountHandler", "") != Name)
                return;

            var simBase = registry.RequestModuleInterface<ISimulationBase> ();
            m_defaultDataPath = simBase.DefaultDataPath;

            Configure (config, registry);
            Init (registry, Name, serverPath: "/user/", serverHandlerName: "UserAccountServerURI");

            // check for user name seed
            IConfig loginConfig = config.Configs ["LoginService"];
            if (loginConfig != null) {
                string userNameSeed = loginConfig.GetString ("UserNameSeed", "");
                if (userNameSeed != "")
                    m_userNameSeed = userNameSeed.Split (',');
            }

            // check for initial stipend payment for new users
            IConfig currConfig = config.Configs ["Currency"];
            if (currConfig != null)
                m_newUserStipend = currConfig.GetInt ("NewUserStipend", 0);

        }

        public void Configure (IConfigSource config, IRegistryCore registry)
        {
            registry.RegisterModuleInterface<IUserAccountService> (this);
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            m_AuthenticationService = registry.RequestModuleInterface<IAuthenticationService> ();
            m_Database = Framework.Utilities.DataManager.RequestPlugin<IUserAccountData> ();
            m_profileConnector = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector> ();
        }

        public void FinishedStartup ()
        {
            // these are only valid if we are local
            if (IsLocalConnector)
                AddCommands ();
        }

        void AddCommands ()
        {
            if (IsLocalConnector && (MainConsole.Instance != null)) {
                MainConsole.Instance.Commands.AddCommand (
                    "create user",
                    "create user [<first> [<last> [<pass> [<email>]]]] [--system] [--uuid]",
                    "Create a new user. If optional parameters are not supplied required details will be prompted\n" +
                    "  --system : Enter user scope UUID\n" +
                    "  --uuid : Enter a specific UUID for the user",
                    HandleCreateUser, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "delete user",
                    "delete user  [<first> [<last>]] ",
                    "Deletes an existing user",
                    HandleDeleteUser, false, true);

                // Alternative user commands for those more familiar with *nix systems
                MainConsole.Instance.Commands.AddCommand (
                    "adduser",
                    "adduser [<first> [<last> [<pass> [<email>]]]] [--system] [--uuid]",
                    "Create a new user. If optional parameters are not supplied required details will be prompted\n" +
                    "  --system : Enter user scope UUID\n" +
                    "  --uuid : Enter a specific UUID for the user",
                    HandleAddUser, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "deluser",
                    "deluser  [<first> [<last>]] ",
                    "Deletes an existing user",
                    HandleDeleteUser, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "disable user",
                    "disable user  [<first> [<last>]] ",
                    "Disable an existing user",
                    HandleDisableUser, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "enable user",
                    "enable user  [<first> [<last>]] ",
                    "Enables an existing user that was previously disabled",
                    HandleEnableUser, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "reset user password",
                    "reset user password [<first> [<last> [<password>]]]",
                    "Reset a user password",
                    HandleResetUserPassword, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set user email",
                    "set user email [<first> [<last> [<email@address>]]]",
                    "Set an email address for a user",
                    HandleSetUserEmail, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "show account",
                    "show account [<first> [<last>]]",
                    "Show account details for the given user",
                    HandleShowAccount, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "show user account",
                    "show user account [<first> [<last>]]",
                    "Show account details for the given user",
                    HandleShowUserAccount, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set user level",
                    "set user level [<first> [<last> [<level>]]]",
                    "Set user level. If the user's level is > 0, this account will be treated as god-mode.\n" +
                    "It will also affect the 'login level' command. ",
                    HandleSetUserLevel, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set user type",
                    "set user type [<first> [<last> [<type>]]]",
                    "Set the user account type. I.e. Guest, Resident, Member etc (Used for stipend payments)",
                    HandleSetUserType, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "rename user",
                    "rename user",
                    "Renames a current user account.",
                    HandleRenameUser, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set user profile title",
                    "set user profile title [<first> [<last> [<Title>]]]",
                    "Sets the title (Normally resident) in a user's title to some custom value.",
                    HandleSetTitle, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set partner",
                    "set partner",
                    "Sets the partner in a user's profile.",
                    HandleSetPartner, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "reset partner",
                    "reset partner",
                    "Resets the partner in a user's profile.",
                    HandleResetPartner, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "import users",
                    "import user [<CSV file>]",
                    "Import users from a CSV file into WhiteCore",
                    HandleLoadUsers, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "export users",
                    "export users [<CSV file>] [--salted]",
                    "Exports all users from WhiteCore into a CSV file, optionally with salt",
                    HandleSaveUsers, false, true);

#if TEST_USERS
                MainConsole.Instance.Commands.AddCommand(
                    "create test users",
                    "create test users",
                    "Create multiple users for testing purposes",
                    HandleTestUsers, false, true);
#endif

            }
        }

        #endregion

        #region IUserAccountService Members

        public virtual IUserAccountService InnerService {
            get { return this; }
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public UserAccount GetUserAccount (List<UUID> scopeIDs, string firstName, string lastName)
        {
            UserAccount account;
            if (m_cache.Get (firstName + " " + lastName, out account))
                return AllScopeIDImpl.CheckScopeIDs (scopeIDs, account);

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("UserAccountServerURI", scopeIDs, firstName, lastName);
                if (remoteValue != null) {
                    UserAccount acc = (UserAccount)remoteValue;
                    m_cache.Cache (acc.PrincipalID, acc);

                    return acc;
                }
                return new UserAccount ();
            }

            UserAccount [] d;

            d = m_Database.Get (scopeIDs,
                               new [] { "FirstName", "LastName" },
                               new [] { firstName, lastName });

            //try for different capitalization if needed
            if (d.Length < 1) {
                // try first character capitals
                firstName = char.ToUpper (firstName [0]) + firstName.Substring (1);

                d = m_Database.Get (scopeIDs,
                    new [] { "FirstName", "LastName" },
                    new [] { firstName, lastName });

                if (d.Length < 1) {
                    // try last name as well
                    lastName = char.ToUpper (lastName [0]) + lastName.Substring (1);

                    d = m_Database.Get (scopeIDs,
                        new [] { "FirstName", "LastName" },
                        new [] { firstName, lastName });
                }

            }

            if (d.Length < 1)
                return null;

            CacheAccount (d [0]);
            return d [0];
        }

        public void CacheAccount (UserAccount account)
        {
            if ((account == null) || (account.UserLevel <= -1))
                return;

            m_cache.Cache (account.PrincipalID, account);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public UserAccount GetUserAccount (List<UUID> scopeIDs, string name)
        {
            if (string.IsNullOrEmpty (name))
                return null;
            UserAccount account;
            if (m_cache.Get (name, out account))
                return AllScopeIDImpl.CheckScopeIDs (scopeIDs, account);

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("UserAccountServerURI", scopeIDs, name);
                if (remoteValue != null) {
                    UserAccount acc = (UserAccount)remoteValue;
                    m_cache.Cache (acc.PrincipalID, acc);

                    return acc;
                }
                return new UserAccount ();
            }

            UserAccount [] d;

            d = m_Database.Get (scopeIDs,
                               new [] { "Name" },
                               new [] { name });

            //try for different capitalization if needed
            if (d.Length < 1) {
                var newName = name.Split (' ');
                if (newName.Length == 2)                    // in case of a bogus names
                {
                    var fName = newName [0];
                    var lName = newName [1];

                    // try first character capitals
                    fName = char.ToUpper (fName [0]) + fName.Substring (1);

                    d = m_Database.Get (scopeIDs,
                        new [] { "Name" },
                        new [] { fName + " " + lName });

                    if (d.Length < 1) {
                        // try last name as well
                        lName = char.ToUpper (lName [0]) + lName.Substring (1);

                        d = m_Database.Get (scopeIDs,
                            new [] { "Name" },
                            new [] { fName + " " + lName });
                    }
                }

            }

            if (d.Length < 1) {
                string [] split = name.Split (' ');
                if (split.Length == 2)
                    return GetUserAccount (scopeIDs, split [0], split [1]);

                return null;
            }

            CacheAccount (d [0]);
            return d [0];
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low, RenamedMethod = "GetUserAccountUUID")]
        public UserAccount GetUserAccount (List<UUID> scopeIDs, UUID principalID)
        {
            UserAccount account;
            if (m_cache.Get (principalID, out account))
                return AllScopeIDImpl.CheckScopeIDs (scopeIDs, account);

            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("UserAccountServerURI", scopeIDs, principalID);
                if (remoteValue != null) {
                    UserAccount acc = (UserAccount)remoteValue;
                    m_cache.Cache (principalID, acc);

                    return acc;
                }
                return new UserAccount ();
            }

            UserAccount [] d;

            d = m_Database.Get (scopeIDs,
                               new [] { "PrincipalID" },
                               new [] { principalID.ToString () });

            if (d.Length < 1) {
                m_cache.Cache (principalID, null);
                return null;
            }

            CacheAccount (d [0]);
            return d [0];
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public bool StoreUserAccount (UserAccount data)
        {
            /*object remoteValue = DoRemoteByURL("UserAccountServerURI", data);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool)remoteValue;*/

            m_registry.RequestModuleInterface<ISimulationBase> ()
                      .EventManager.FireGenericEventHandler ("UpdateUserInformation", data.PrincipalID);
            return m_Database.Store (data);
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<UserAccount> GetUserAccounts (List<UUID> scopeIDs, string query)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("UserAccountServerURI", scopeIDs, query);
                if (remoteValue != null)
                    return (List<UserAccount>)remoteValue;
                return new List<UserAccount> ();
            }

            UserAccount [] d = m_Database.GetUsers (scopeIDs, query);
            if (d == null)
                return new List<UserAccount> ();

            List<UserAccount> ret = new List<UserAccount> (d);
            return ret;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<UserAccount> GetUserAccounts (List<UUID> scopeIDs, string query, uint? start, uint? count)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("UserAccountServerURI", scopeIDs, query);
                if (remoteValue != null)
                    return (List<UserAccount>)remoteValue;
                return new List<UserAccount> ();
            }

            UserAccount [] d = m_Database.GetUsers (scopeIDs, query, start, count);
            if (d == null)
                return new List<UserAccount> ();

            List<UserAccount> ret = new List<UserAccount> (d);
            return ret;
        }

        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public List<UserAccount> GetUserAccounts (List<UUID> scopeIDs, int level, int flags)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("UserAccountServerURI", level, flags);
                if (remoteValue != null)
                    return (List<UserAccount>)remoteValue;
                return new List<UserAccount> ();
            }

            UserAccount [] d = m_Database.GetUsers (scopeIDs, level, flags);
            if (d == null)
                return new List<UserAccount> ();

            List<UserAccount> ret = new List<UserAccount> (d);
            return ret;
        }


        [CanBeReflected (ThreatLevel = ThreatLevel.Low)]
        public uint NumberOfUserAccounts (List<UUID> scopeIDs, string query)
        {
            if (m_doRemoteOnly) {
                object remoteValue = DoRemoteByURL ("UserAccountServerURI", scopeIDs, query);
                if (remoteValue != null)
                    return (uint)remoteValue;
                return 0;
            }

            var userCount = m_Database.NumberOfUsers (scopeIDs, query);

            if (userCount < Constants.SystemUserCount)
                return 0;
            return userCount - Constants.SystemUserCount;
        }

        /// <summary>
        /// Creates a basic user.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="md5password">Md5password.</param>
        /// <param name="email">Email.</param>
        public void CreateUser (string name, string md5password, string email)
        {
            CreateNewUser (new UserAccount (UUID.Zero, UUID.Random (), name, email), md5password, "");
        }

        /// <summary>
        ///     Create a user
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="scopeID"></param>
        /// <param name="name"></param>
        /// <param name="md5password"></param>
        /// <param name="email"></param>
        public string CreateUser (UUID userID, UUID scopeID, string name, string md5password, string email)
        {
            return CreateNewUser (new UserAccount (scopeID, userID, name, email), md5password, "");
        }

        /// <summary>
        ///     Create a user
        /// </summary>
        /// <param name="newAccount"></param>
        /// <param name="md5password"></param>
        public string CreateUser (UserAccount newAccount, string md5password)
        {
            return CreateNewUser (newAccount, md5password, "");
        }

        /// <summary>
        /// Creates the user with salt.
        /// </summary>
        /// <returns>The salted user.</returns>
        /// <param name="newAccount">New account.</param>
        /// <param name="passHash">Pass hash.</param>
        /// <param name="passSalt">Pass salt.</param>
        public string CreateSaltedUser (UserAccount newAccount, string passHash, string passSalt)
        {
            return CreateNewUser (newAccount, passHash, passSalt);
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        string CreateNewUser (UserAccount newAccount, string passHash, string passSalt)
        {
            /*object remoteValue = DoRemoteByURL("UserAccountServerURI", newAcc, password);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? "" : remoteValue.ToString();*/

            UserAccount account = GetUserAccount (null, newAccount.PrincipalID);
            UserAccount nameaccount = GetUserAccount (null, newAccount.Name);
            if (account != null || nameaccount != null) {
                MainConsole.Instance.ErrorFormat ("[User account service]: A user with the name {0} already exists!", newAccount.Name);
                return "A user with the same name already exists";
            }

            // This one is available...
            if (!StoreUserAccount (newAccount)) {
                MainConsole.Instance.ErrorFormat ("[User account service]: Account creation failed for account {0}", newAccount.Name);
                return "Unable to save account";
            }

            bool success;
            if (passSalt != "")
                success = SetSaltedPassword (newAccount.PrincipalID, passHash, passSalt);
            else
                success = SetHashedPassword (newAccount.PrincipalID, passHash);

            if (!success) {
                MainConsole.Instance.WarnFormat (
                    "[User account service]: Unable to set password for account {0}.", newAccount.Name);
                return "Unable to set password";
            }
            //            }

            MainConsole.Instance.InfoFormat ("[User account service]: Account {0} created successfully", newAccount.Name);
            //Cache it as well
            CacheAccount (newAccount);
            m_registry.RequestModuleInterface<ISimulationBase> ()
                              .EventManager.FireGenericEventHandler ("CreateUserInformation", newAccount.PrincipalID);

            // create a profile for the new user
            if (m_profileConnector != null) {
                m_profileConnector.CreateNewProfile (newAccount.PrincipalID);
                IUserProfileInfo profile = m_profileConnector.GetUserProfile (newAccount.PrincipalID);

                // if (AvatarArchive != "")
                //    profile.AArchiveName = AvatarArchive;
                profile.MembershipGroup = "Resident";
                profile.IsNewUser = true;
                m_profileConnector.UpdateUserProfile (profile);
            }

            // top up the wallet?
            if ((m_newUserStipend > 0) && !Utilities.IsSystemUser (newAccount.PrincipalID)) {
                IMoneyModule money = m_registry.RequestModuleInterface<IMoneyModule> ();
                if (money != null) {
                    money.Transfer (
                        newAccount.PrincipalID,
                        (UUID)Constants.BankerUUID,
                        m_newUserStipend,
                        "New user stipend",
                        TransactionType.SystemGenerated
                    );
                }
            }
            return "";

        }

        bool SetHashedPassword (UUID userId, string passHash)
        {
            var success = false;
            if (m_AuthenticationService != null && passHash != "")
                success = m_AuthenticationService.SetPasswordHashed (userId, "UserAccount", passHash);
            return success;
        }


        bool SetSaltedPassword (UUID userId, string passHash, string passSalt)
        {
            bool success = false;
            if (m_AuthenticationService != null && passHash != "" && passSalt != "")
                success = m_AuthenticationService.SetSaltedPassword (userId, "UserAccount", passHash, passSalt);
            return success;
        }


        public void DeleteUser (UUID userID, string name, string password, bool archiveInformation, bool wipeFromDatabase)
        {
            //if (password != "" && m_AuthenticationService.Authenticate(userID, "UserAccount", password, 0) == "")
            //    return; //Not authenticated

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser (userID)) {
                MainConsole.Instance.Warn ("[User account service]: Deleting a system user account is not a good idea!");
                return;
            }


            if (!m_Database.DeleteAccount (userID, archiveInformation)) {
                MainConsole.Instance.WarnFormat (
                    "[User account service]: Failed to remove the account for {0}, please check that the database is valid after this operation!",
                    userID);
                return;
            }

            if (wipeFromDatabase)
                m_registry.RequestModuleInterface<ISimulationBase> ()
                          .EventManager.FireGenericEventHandler ("DeleteUserInformation", userID);
            m_cache.Remove (userID, name);
        }

        #endregion

        #region Console commands

        /// <summary>
        /// Handles the set partner command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdParams">Cmd parameters.</param>
        protected void HandleSetPartner (IScene scene, string [] cmdParams)
        {
            string first = MainConsole.Instance.Prompt ("First User's name (<first> <last>)");
            string second = MainConsole.Instance.Prompt ("Second User's name (<first> <last>)");
            if (second == first) {
                MainConsole.Instance.Error ("[User account service]: You are not able to set yourself as your partner");
                return;
            }

            if (m_profileConnector != null) {
                IUserProfileInfo firstProfile =
                    m_profileConnector.GetUserProfile (GetUserAccount (null, first).PrincipalID);
                IUserProfileInfo secondProfile =
                    m_profileConnector.GetUserProfile (GetUserAccount (null, second).PrincipalID);

                if (firstProfile == null || secondProfile == null) {
                    MainConsole.Instance.Warn ("[User account service]: At least one of these users does not have a profile?");
                    return;
                }

                firstProfile.Partner = secondProfile.PrincipalID;
                secondProfile.Partner = firstProfile.PrincipalID;

                m_profileConnector.UpdateUserProfile (firstProfile);
                m_profileConnector.UpdateUserProfile (secondProfile);

                MainConsole.Instance.Warn ("[User account service]: Partner information updated.");
            }

        }

        /// <summary>
        /// Handles the reset partner command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdParams">Cmd parameters.</param>
        protected void HandleResetPartner (IScene scene, string [] cmdParams)
        {
            string first = MainConsole.Instance.Prompt ("First User's name (<first> <last>)");

            if (m_profileConnector != null) {
                IUserProfileInfo firstProfile =
                    m_profileConnector.GetUserProfile (GetUserAccount (null, first).PrincipalID);

                // Find the second partner through the first user details
                if (firstProfile.Partner == UUID.Zero) {
                    MainConsole.Instance.Error ("[User account service]: This user doesn't have a partner");
                    return;
                }

                if (firstProfile.Partner == firstProfile.PrincipalID) {
                    MainConsole.Instance.Error ("[User account service]: Deadlock situation avoided, this avatar is his own partner");
                    firstProfile.Partner = UUID.Zero;
                    m_profileConnector.UpdateUserProfile (firstProfile);
                } else {
                    IUserProfileInfo secondProfile =
                            m_profileConnector.GetUserProfile (GetUserAccount (null, firstProfile.Partner).PrincipalID);

                    firstProfile.Partner = UUID.Zero;
                    secondProfile.Partner = UUID.Zero;

                    m_profileConnector.UpdateUserProfile (firstProfile);
                    m_profileConnector.UpdateUserProfile (secondProfile);

                    MainConsole.Instance.Warn ("[User account service]: Partner information updated. ");
                }
            }
        }

        /// <summary>
        /// Handles the user set title command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSetTitle (IScene scene, string [] cmdparams)
        {
            string firstName;
            string lastName;
            string title;

            firstName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt ("First name") : cmdparams [4];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 6 ? MainConsole.Instance.Prompt ("Last name") : cmdparams [5];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.Warn ("[User account service]: No such user");
                return;
            }
            title = cmdparams.Length < 7 ? MainConsole.Instance.Prompt ("User Title") : Util.CombineParams (cmdparams, 6);
            if (m_profileConnector != null) {
                IUserProfileInfo profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                if (profile != null) {
                    // this is not right is it?  >> profile.MembershipGroup = title;
                    profile.CustomType = title;
                    m_profileConnector.UpdateUserProfile (profile);
                } else {
                    MainConsole.Instance.Warn ("[User account service]: There does not appear to be a profile for this user?");
                    return;
                }

            }
            bool success = StoreUserAccount (account);
            if (!success)
                MainConsole.Instance.InfoFormat ("[User account service]: Unable to set user profile title for account {0} {1}.", firstName,
                                                lastName);
            else
                MainConsole.Instance.InfoFormat ("[User account service]: User profile title set for user {0} {1} to {2}", firstName, lastName,
                                                title);
        }

        /// <summary>
        /// Handles the set user level command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSetUserLevel (IScene scene, string [] cmdparams)
        {
            string firstName;
            string lastName;
            string rawLevel;
            int level;

            firstName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt ("First name") : cmdparams [3];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt ("Last name") : cmdparams [4];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.Warn ("[User account service]: Unable to locate this user");
                return;
            }

            // ensure the protected system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID)) {
                MainConsole.Instance.Warn ("[User account service]: Changing system users is not a good idea!");
                return;
            }

            rawLevel = cmdparams.Length < 6 ? MainConsole.Instance.Prompt ("User level") : cmdparams [5];
            int.TryParse (rawLevel, out level);

            if (level > 255 || level < 0) {
                MainConsole.Instance.Warn ("Invalid user level");
                return;
            }

            account.UserLevel = level;

            bool success = StoreUserAccount (account);
            if (!success)
                MainConsole.Instance.InfoFormat ("[User account service]: Unable to set user level for account {0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat ("[User account service]: User level set for user {0} {1} to {2}", firstName, lastName, level);
        }

        int UserTypeToUserFlags (string userType)
        {
            switch (userType) {
            case "Guest":
                return Constants.USER_FLAG_GUEST;
            case "Resident":
                return Constants.USER_FLAG_RESIDENT;
            case "Member":
                return Constants.USER_FLAG_MEMBER;
            case "Contractor":
                return Constants.USER_FLAG_CONTRACTOR;
            case "Charter_Member":
                return Constants.USER_FLAG_CHARTERMEMBER;
            default:
                return Constants.USER_FLAG_GUEST;
            }
        }

        string UserFlagToType (int userFlags)
        {
            switch (userFlags) {
            case Constants.USER_FLAG_GUEST:
                return "Guest";
            case Constants.USER_FLAG_RESIDENT:
                return "Resident";
            case Constants.USER_FLAG_MEMBER:
                return "Member";
            case Constants.USER_FLAG_CONTRACTOR:
                return "Contractor";
            case Constants.USER_FLAG_CHARTERMEMBER:
                return "Charter_Member";
            default:
                return "Guest";
            }
        }

        /// <summary>
        /// Users 'god' level.
        /// </summary>
        /// <returns>The god level description.</returns>
        /// <param name="level">Level.</param>
        public string UserGodLevel (int level)
        {
            switch (level) {
            case Constants.USER_DISABLED:
                return "Disabled";
            case Constants.USER_BANNED:
                return "Banned";
            case Constants.USER_NORMAL:
                return "User";
            case Constants.USER_GOD_LIKE:
                return "Elevated user";
            case Constants.USER_GOD_CUSTOMER_SERVICE:
                return "Customer service";
            case Constants.USER_GOD_LIASON:
                return "Liaison";
            case Constants.USER_GOD_FULL:
                return "A God";
            case Constants.USER_GOD_MAINTENANCE:
                return "Maintenance God";
            default:
                return "User";
            }
        }


        protected void HandleShowUserAccount (IScene scene, string [] cmd)
        {
            // remove 'user' from the cmd 
            var cmdparams = new List<string> (cmd);
            cmdparams.RemoveAt (1);
            cmd = cmdparams.ToArray ();

            HandleShowAccount (scene, cmd);

        }

        /// <summary>
        /// Handles the show account command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleShowAccount (IScene scene, string [] cmdparams)
        {
            string firstName;
            string lastName;

            firstName = cmdparams.Length < 3 ? MainConsole.Instance.Prompt ("First name") : cmdparams [2];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt ("Last name") : cmdparams [3];
            if (lastName == "")
                return;

            UserAccount ua = GetUserAccount (null, firstName, lastName);
            if (ua == null) {
                MainConsole.Instance.InfoFormat ("[User account service]: Unable to find user '{0} {1}'", firstName, lastName);
                return;
            }

            MainConsole.Instance.CleanInfo ("  Name   : " + ua.Name);
            MainConsole.Instance.CleanInfo ("  ID     : " + ua.PrincipalID);
            MainConsole.Instance.CleanInfo ("  E-mail : " + ua.Email);
            MainConsole.Instance.CleanInfo ("  Created: " + Utils.UnixTimeToDateTime (ua.Created));
            MainConsole.Instance.CleanInfo ("  Level  : " + UserGodLevel (ua.UserLevel));
            MainConsole.Instance.CleanInfo ("  Type   : " + UserFlagToType (ua.UserFlags));
        }

        /// <summary>
        /// Handles the set user level command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSetUserType (IScene scene, string [] cmdparams)
        {
            string firstName;
            string lastName;
            List<string> userTypes = new List<string> (new [] { "Guest", "Resident", "Member", "Contractor", "Charter_Member" });
            int userFlags;

            firstName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt ("First name") : cmdparams [3];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt ("Last name") : cmdparams [4];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.Warn ("[User account service]: Unable to locate this user");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID)) {
                MainConsole.Instance.Warn ("[User account service]: Changing system users is not a good idea!");
            }

            // Get user type (for payments etc)
            var userType = MainConsole.Instance.Prompt ("User type", "Resident", userTypes);
            userFlags = UserTypeToUserFlags (userType);

            account.UserFlags = userFlags;

            bool success = StoreUserAccount (account);
            if (success) {
                MainConsole.Instance.InfoFormat ("[User account service]: User '{0} {1}' set to {2}", firstName, lastName, userType);

                // update profile for the user as well
                if (m_profileConnector != null) {
                    IUserProfileInfo profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    if (profile == null) {
                        m_profileConnector.CreateNewProfile (account.PrincipalID);          // create a profile for the user
                        profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    }

                    // if (AvatarArchive != "")
                    //    profile.AArchiveName = AvatarArchive;
                    profile.MembershipGroup = UserFlagToType (account.UserFlags);
                    profile.IsNewUser = true;
                    m_profileConnector.UpdateUserProfile (profile);
                }
            } else
                MainConsole.Instance.InfoFormat ("[User account service]: Unable to set user type for account '{0} {1}'.", firstName, lastName);

        }


        protected void HandleRenameUser (IScene scene, string [] cmdparams)
        {
            string firstName;
            string lastName;

            firstName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt ("First name") : cmdparams [3];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt ("Last name") : cmdparams [4];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.Warn ("[User account service]: Unable to locate this user");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID)) {
                MainConsole.Instance.Warn ("[User account service]: Changing system users is not a good idea!");
            }

            // new name
            var newName = MainConsole.Instance.Prompt ("New user name to use <First Last>: ", "");
            if (newName == "")
                return;

            string [] split = newName.Split (' ');
            if (split.Length < 2) {
                MainConsole.Instance.Warn ("[User account service]: Sorry! Names must in the format 'Firstname Lastname'.");
                return;
            }

            // verify that this is ok...
            var chkAcct = GetUserAccount (null, newName);
            if (chkAcct != null) {
                MainConsole.Instance.Warn ("[User account service]: Sorry! This name is already assigned.");
                return;
            }

            account.Name = newName;
            bool success = StoreUserAccount (account);
            if (!success)
                MainConsole.Instance.WarnFormat ("[User account service]: Unable to set the new name for {0} {1}", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat ("[User account service]: User '{0} {1}' has been renamed to '{2}'",
                    firstName, lastName, newName);

        }


        public List<string> GetAvatarArchivesFiles ()
        {
            IAvatarAppearanceArchiver avieArchiver = m_registry.RequestModuleInterface<IAvatarAppearanceArchiver> ();
            List<string> archives = avieArchiver.GetAvatarArchiveFilenames ();

            return archives;

        }

        protected void HandleAddUser (IScene scene, string [] cmd)
        {
            // short form command
            var newcmds = new List<string> (cmd);
            newcmds.Insert (1, "dummy");
            HandleCreateUser (scene, newcmds.ToArray ());
        }

        /// <summary>
        ///     Handle the create (add) user command from the console.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname, password, email</param>
        protected void HandleCreateUser (IScene scene, string [] cmd)
        {
            string userName = "";
            string password, email, uuid, scopeID;
            bool sysFlag = false;
            bool uuidFlag = false;
            List<string> userTypes = new List<string> (new [] { "Guest", "Resident", "Member", "Contractor", "Charter_Member" });

            List<string> cmdparams = new List<string> (cmd);
            foreach (string param in cmd) {
                if (param.StartsWith ("--system", StringComparison.Ordinal)) {
                    sysFlag = true;
                    cmdparams.Remove (param);
                }
                if (param.StartsWith ("--uuid", StringComparison.Ordinal)) {
                    uuidFlag = true;
                    cmdparams.Remove (param);
                }
            }

            // check for provided user name
            if (cmdparams.Count >= 4) {
                userName = cmdparams [2] + " " + cmdparams [3];
            } else {
                var ufNames = new Utilities.MarkovNameGenerator ();
                var ulNames = new Utilities.MarkovNameGenerator ();
                string [] nameSeed = m_userNameSeed == null ? Utilities.UserNames : m_userNameSeed;

                string firstName = ufNames.FirstName (nameSeed, 3, 4);
                string lastName = ulNames.FirstName (nameSeed, 5, 6);
                string enteredName = firstName + " " + lastName;
                if (userName != "")
                    enteredName = userName;

                do {
                    userName = MainConsole.Instance.Prompt ("User Name (? for suggestion, 'quit' to abort)", enteredName);
                    if (userName.ToLower () == "quit")
                        return;
                    
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
            }

            // we have the name so check to make sure it is allowed
            UserAccount ua = GetUserAccount (null, userName);
            if (ua != null) {
                MainConsole.Instance.WarnFormat ("[User account service]: This user, '{0}' already exists!", userName);
                return;
            }

            // password as well?
            if (cmdparams.Count < 5) {
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
            } else
                password = cmdparams [4];

            // maybe even an email?
            if (cmdparams.Count < 6) {
                email = MainConsole.Instance.Prompt ("Email for password recovery. ('none' if unknown)", "none");
            } else
                email = cmdparams [5];

            if ((email.ToLower () != "none") && !Utilities.IsValidEmail (email)) {
                MainConsole.Instance.CleanInfo ("This does not look like a valid email address. ('none' if unknown)");
                email = MainConsole.Instance.Prompt ("Email", email);
            }

            // Get user type (for payments etc)
            var userType = MainConsole.Instance.Prompt ("User type", "Resident", userTypes);

            // Get available user avatar archives
            var userAvatarArchive = "";
            var avatarArchives = GetAvatarArchivesFiles ();
            if (avatarArchives.Count > 0) {
                avatarArchives.Add ("None");
                userAvatarArchive = MainConsole.Instance.Prompt ("Avatar archive to use", "None", avatarArchives);
                if (userAvatarArchive == "None")
                    userAvatarArchive = "";
            }

            // Allow the modification the UUID if required - for matching user UUID with other Grids etc like SL
            uuid = UUID.Random ().ToString ();
            if (uuidFlag)
                while (true) {
                    uuid = MainConsole.Instance.Prompt ("Required avatar UUID)", uuid);
                    UUID test;
                    if (UUID.TryParse (uuid, out test))
                        break;

                    MainConsole.Instance.Error ("There was a problem verifying this UUID. Please retry.");
                }

            // this really should not be altered so hide it normally
            scopeID = UUID.Zero.ToString ();
            if (sysFlag) {
                scopeID = MainConsole.Instance.Prompt ("Scope (Don't change unless you know what this is)", scopeID);
            }

            // we should be good to go
            CreateUser (UUID.Parse (uuid), UUID.Parse (scopeID), userName, Util.Md5Hash (password), email);
            // CreateUser will tell us success or problem
            //MainConsole.Instance.InfoFormat("[User account service]: User '{0}' created", name);

            // check for success
            UserAccount account = GetUserAccount (null, userName);
            if (account != null) {
                account.UserFlags = UserTypeToUserFlags (userType);
                StoreUserAccount (account);

                // update profile for the user as well
                if (m_profileConnector != null) {
                    IUserProfileInfo profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    if (profile == null) {
                        m_profileConnector.CreateNewProfile (account.PrincipalID);          // create a profile for the user
                        profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    }

                    if (userAvatarArchive != "")
                        profile.AArchiveName = userAvatarArchive + ".aa";

                    profile.MembershipGroup = UserFlagToType (account.UserFlags);
                    profile.IsNewUser = true;
                    m_profileConnector.UpdateUserProfile (profile);
                }
            } else
                MainConsole.Instance.WarnFormat ("[User account service]: There was a problem creating the account for '{0}'", userName);

        }

        /// <summary>
        /// Handles the delete user command.
        /// Delete or disable a user account
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname, password</param>
        protected void HandleDeleteUser (IScene scene, string [] cmd)
        {
            string firstName, lastName, password;

            // check for passed username
            firstName = cmd.Length < 3 ? MainConsole.Instance.Prompt ("First name") : cmd [2];
            if (firstName == "")
                return;
            lastName = cmd.Length < 4 ? MainConsole.Instance.Prompt ("Last name") : cmd [3];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.Warn ("[User account service]: No user with that name!");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID)) {
                MainConsole.Instance.Warn ("[User account service]: You cannot delete system users!");
                return;
            }

            // password as well?
            password = cmd.Length < 5 ? MainConsole.Instance.PasswordPrompt ("Password") : cmd [4];

            bool archive;
            bool all = false;

            archive = MainConsole.Instance.Prompt ("Archive Information? (Disable login, but keep their information): (yes/no)", "yes").ToLower () == "yes";
            if (!archive)
                all = MainConsole.Instance.Prompt ("Remove all user information (yes/no)", "yes").ToLower () == "yes";

            if (archive || all) {
                DeleteUser (account.PrincipalID, account.Name, password, archive, all);
                if (all)
                    MainConsole.Instance.InfoFormat ("[User account service]: User account '{0}' deleted", account.Name);
                else
                    MainConsole.Instance.InfoFormat ("[User account service]: User account '{0}' disabled", account.Name);
            }
        }

        /// <summary>
        /// Handles the disable user command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname.</param>
        protected void HandleDisableUser (IScene scene, string [] cmd)
        {
            string firstName, lastName;

            // check for passed username
            firstName = cmd.Length < 3 ? MainConsole.Instance.Prompt ("First name") : cmd [2];
            if (firstName == "")
                return;
            lastName = cmd.Length < 4 ? MainConsole.Instance.Prompt ("Last name") : cmd [3];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.Warn ("[User account service]: Unable to locate this user!");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID)) {
                MainConsole.Instance.Warn ("[User account service]: You cannot modify system users!");
                return;
            }

            // if the user is disabled details will exist with a level set @ -2
            if (account.UserLevel < 0) {
                MainConsole.Instance.Warn ("[User account service]: User is already disabled!");
                return;
            }

            account.UserLevel = -2;
            bool success = StoreUserAccount (account);
            if (!success)
                MainConsole.Instance.InfoFormat ("[User account service]: Unable to disable account {0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat ("[User account service]: User account {0} {1} disabled.", firstName, lastName);
        }

        /// <summary>
        /// Handles the enable user command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname.</param>
        protected void HandleEnableUser (IScene scene, string [] cmd)
        {
            string firstName, lastName;

            // check for passed username
            firstName = cmd.Length < 3 ? MainConsole.Instance.Prompt ("First name") : cmd [2];
            if (firstName == "")
                return;
            lastName = cmd.Length < 4 ? MainConsole.Instance.Prompt ("Last name") : cmd [3];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.Warn ("[User account service]: Unable to locate this user!");
                return;
            }

            // quietly ensure the system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID))
                return;

            // if the user is disabled details will exist with a level set @ -2
            account.UserLevel = 0;

            bool success = StoreUserAccount (account);
            if (!success)
                MainConsole.Instance.InfoFormat ("[User account service]: Unable to enable account {0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat ("[User account service]: User account {0} {1} enabled.", firstName, lastName);
        }

        /// <summary>
        /// Handles the reset user password command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname, newpassword,</param>
        protected void HandleResetUserPassword (IScene scene, string [] cmd)
        {
            string firstName, lastName;
            string newPassword;

            // check for passed username
            firstName = cmd.Length < 4 ? MainConsole.Instance.Prompt ("First name") : cmd [3];
            if (firstName == "")
                return;
            lastName = cmd.Length < 5 ? MainConsole.Instance.Prompt ("Last name") : cmd [4];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.ErrorFormat ("[User account service]: Unable to locate this user");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID)) {
                MainConsole.Instance.Warn ("[User account service]: Changing system users is not a good idea!");
                return;
            }

            // password as well?
            if (cmd.Length < 6) {
                bool passMatch;
                do {
                    newPassword = MainConsole.Instance.PasswordPrompt ("New Password ('cancel' to abort)");
                    if (newPassword == "" || newPassword.ToLower () == "cancel")
                        return;

                    string confPass = MainConsole.Instance.PasswordPrompt ("Please confirm new Password");
                    passMatch = (newPassword == confPass);
                    if (!passMatch)
                        MainConsole.Instance.Error ("  Password confirmation does not match");

                } while (!passMatch);

            } else
                newPassword = cmd [5];

            bool success = false;
            if (m_AuthenticationService != null)
                success = m_AuthenticationService.SetPassword (account.PrincipalID, "UserAccount", newPassword);
            if (!success)
                MainConsole.Instance.ErrorFormat (
                    "[User account service]: Unable to reset password for account '{0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat ("[User account service]: Password reset for user '{0} {1}", firstName, lastName);
        }

        /// <summary>
        /// Handles the set user email command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        protected void HandleSetUserEmail (IScene scene, string [] cmd)
        {
            string firstName, lastName;
            string newEmail;

            // check for passed username
            firstName = cmd.Length < 4 ? MainConsole.Instance.Prompt ("First name") : cmd [3];
            if (firstName == "")
                return;
            lastName = cmd.Length < 5 ? MainConsole.Instance.Prompt ("Last name") : cmd [4];
            if (lastName == "")
                return;


            UserAccount account = GetUserAccount (null, firstName, lastName);
            if (account == null) {
                MainConsole.Instance.ErrorFormat ("[User account service]: Unable to locate this user");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser (account.PrincipalID)) {
                MainConsole.Instance.Warn ("[User account service]: Changing system users is not a good idea!");
                return;
            }

            // email address as well?
            newEmail = account.Email;
            newEmail = cmd.Length < 6 ? MainConsole.Instance.Prompt ("Email address", newEmail) : cmd [5];
            if (!Utilities.IsValidEmail (newEmail)) {
                MainConsole.Instance.Error (" This email address appears to be incorrect");
                do {
                    newEmail = MainConsole.Instance.Prompt ("Email address ('cancel' to abort)", newEmail);
                    if (newEmail == "" || newEmail.ToLower () == "cancel")
                        return;
                } while (!Utilities.IsValidEmail (newEmail));
            }

            account.Email = newEmail;
            bool success = StoreUserAccount (account);
            if (!success)
                MainConsole.Instance.WarnFormat ("[User account service]: Unable to set Email for {0} {1}", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat ("[User account service]: Email for {0} {1} set to {2}", firstName, lastName, account.Email);

        }

        /// <summary>
        /// Handles the load users command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdParams">Cmdparams.</param>
        protected void HandleLoadUsers (IScene scene, string [] cmdParams)
        {
            string fileName = "users.csv";
            if (cmdParams.Length < 3) {
                fileName = MainConsole.Instance.Prompt ("Please enter the user CSV file to load", fileName);
                if (fileName == "")
                    return;
            } else
                fileName = cmdParams [2];

            int userNo = 0;
            string firstName;
            string lastName;
            string password;
            string email;
            string rezday;
            string passHash;
            string passSalt;
            UUID userUUID;

            fileName = PathHelpers.VerifyReadFile (fileName, "csv", m_defaultDataPath + "/Updates");
            if (fileName == "") {
                MainConsole.Instance.Error ("The file " + fileName + " does not exist. Please check and retry");
                return;
            }

            // good to go...
            using (var rd = new StreamReader (fileName)) {
                while (!rd.EndOfStream) {
                    var userInfo = rd.ReadLine ().Split (',');
                    if (userInfo.Length < 5) {
                        MainConsole.Instance.Error ("[User Load]: Insufficient details; Skipping " + userInfo);
                        continue;
                    }

                    userUUID = (UUID)userInfo [0];
                    firstName = userInfo [1];
                    lastName = userInfo [2];
                    password = userInfo [3];
                    email = userInfo.Length > 4 ? userInfo [4] : "";
                    rezday = userInfo.Length > 5 ? userInfo [5] : "";
                    passHash = userInfo.Length > 6 ? userInfo [6] : "";
                    passSalt = userInfo.Length > 7 ? userInfo [7] : "";

                    string userCreated = "";
                    if (userInfo.Length <= 6) {
                        // we only have the basics here
                        userCreated = CreateUser (new UserAccount (UUID.Zero, userUUID, firstName + " " + lastName, email), Util.Md5Hash (password));
                    } else {
                        // we have full details
                        userCreated = CreateSaltedUser (new UserAccount (UUID.Zero, userUUID, firstName + " " + lastName, email), passHash, passSalt);
                    }
                    if (userCreated != "") {
                        MainConsole.Instance.ErrorFormat ("Couldn't create the user '{0} {1}'. Reason: {2}",
                                                          firstName, lastName, userCreated);
                        continue;
                    }

                    //set user levels and status  (if needed)
                    var account = GetUserAccount (null, userUUID);
                    //account.UserLevel = 0;
                    account.UserFlags = Constants.USER_FLAG_RESIDENT;
                    StoreUserAccount (account);

                    // [NEW] Set the users rezdate
                    if ((rezday != "") && (m_profileConnector != null)) {
                        IUserProfileInfo profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                        profile.Created = int.Parse (rezday);
                        bool success = m_profileConnector.UpdateUserProfile (profile);
                        if (!success)
                            MainConsole.Instance.InfoFormat ("[User account service]: Unable to change rezday for {0} {1}.", account.FirstName, account.LastName);
                        else
                            MainConsole.Instance.InfoFormat ("[User account service]: Account {0} {1} has a rezday set.", account.FirstName, account.LastName);
                    }

                    userNo++;

                }
                MainConsole.Instance.InfoFormat ("File: {0} loaded,  {1} users added", Path.GetFileName (fileName), userNo);
            }

        }

        /// <summary>
        /// Handles the save users command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmdparams.</param>
        protected void HandleSaveUsers (IScene scene, string [] cmd)
        {
            string fileName = "users.csv";
            var salted = false;
            var m_auth = Framework.Utilities.DataManager.RequestPlugin<IAuthenticationData> ();

            // check for options
            List<string> cmdParams = new List<string> (cmd);
            foreach (string param in cmd) {
                if (param.StartsWith ("--salted", StringComparison.Ordinal)) {
                    salted = true;
                    cmdParams.Remove (param);
                }
            }

            if (cmdParams.Count < 3) {
                fileName = MainConsole.Instance.Prompt ("Please enter the user CSV file to save", fileName);
                if (fileName == "")
                    return;
            } else
                fileName = cmdParams [2];

            int userNo = 0;

            fileName = PathHelpers.VerifyWriteFile (fileName, "csv", m_defaultDataPath + "/Updates", true);
            if (fileName == "")
                return;

            // good to go...
            var accounts = GetUserAccounts (null, "*");
            if (accounts != null)                                                       // unlikely but you never know
            {
                FileStream stream = new FileStream (fileName, FileMode.Create);         // always start fresh
                StreamWriter streamWriter = new StreamWriter (stream);
                try {
                    //Add the user
                    streamWriter.BaseStream.Position += streamWriter.BaseStream.Length;

                    foreach (UserAccount user in accounts) {
                        if (Utilities.IsSystemUser (user.PrincipalID))
                            continue;

                        var lineToWrite = user.PrincipalID + "," + user.FirstName + "," + user.LastName + ",," + user.Email;
                        if (m_profileConnector != null) {
                            var userProfile = m_profileConnector.GetUserProfile (user.PrincipalID);
                            lineToWrite = lineToWrite + "," + userProfile.Created;
                        } else {
                            lineToWrite = lineToWrite + ",";
                        }
                        if (salted && (m_auth != null)) {
                            var userauth = m_auth.Get (user.PrincipalID, "UserAccount");
                            if (userauth != null)
                                lineToWrite = lineToWrite + "," + userauth.PasswordHash + "," + userauth.PasswordSalt;
                        }
                        streamWriter.WriteLine (lineToWrite);

                        userNo++;
                    }
                    streamWriter.Flush ();
                    streamWriter.Close ();

                    MainConsole.Instance.InfoFormat ("File: {0} saved with {1} users", Path.GetFileName (fileName), userNo);
                } catch {
                    if (streamWriter != null)
                        streamWriter.Close ();
                }
            }
        }

        // Developer testing only
        // Generates multiple users
#if TEST_USERS
        protected void HandleTestUsers(IScene scene, string[] cmdParams)
        {
            string checkOk;
            checkOk = MainConsole.Instance.Prompt ("[Testing]:  Caution!! This will add random users for testing purposes. Continue? (yes, no)", "no").ToLower ();
            if (!checkOk.StartsWith("y"))
                return;

            int addUsers = 0;
            addUsers = int.Parse (MainConsole.Instance.Prompt ("Number of test users to add?", "0"));
            if (addUsers == 0)
                return;

            // make sure
            checkOk = MainConsole.Instance.Prompt ("[Testing]: You are about to add " + addUsers + " to your database! Are you sure? (yes, no)", "no").ToLower ();
            if (!checkOk.StartsWith("y"))
                return;

            var startTime = DateTime.Now;
            int userNo = 0;
            string firstName = "Test";
            string lastName = "User";
            string password = "none";
            string email = "none";
            UUID userUUID;

            for (userNo = 0; userNo < addUsers; userNo++) {
                UserUUID = UUID.Random ();

                string check = CreateUser (userUUID, UUID.Zero, firstName + " " + lastName+userNo, Util.Md5Hash(password), email);
                if (check != "") {
                    MainConsole.Instance.Error ("Couldn't create the user. Reason: " + check);
                    continue;
                }

                //set user levels and status  (if needed)
                var account = GetUserAccount (null, userUUID);
                //account.UserLevel = 0;
                account.UserFlags = Constants.USER_FLAG_RESIDENT;
                StoreUserAccount (account);
            }

            var elapsed = DateTime.Now - startTime;

            MainConsole.Instance.InfoFormat ("Added {0} test users in {1}", addUsers, elapsed.ToString());

        }
#endif

        #endregion
    }
}
