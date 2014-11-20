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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using System.Collections.Generic;
using System.IO;
using System;
using System.Globalization;

namespace WhiteCore.Services.SQLServices.UserAccountService
{
    public class UserAccountService : ConnectorBase, IUserAccountService, IService
    {
        #region Declares

        protected IProfileConnector m_profileConnector;
        protected IAuthenticationService m_AuthenticationService;
        protected IUserAccountData m_Database;
        protected GenericAccountCache<UserAccount> m_cache = new GenericAccountCache<UserAccount>();
        protected string[] m_userNameSeed;

        #endregion

        public bool RemoteCalls()
        {
            return m_doRemoteCalls;
        }

        #region IService Members

        public virtual string Name
        {
            get { return GetType().Name; }
        }

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("UserAccountHandler", "") != Name)
                return;
            Configure(config, registry);
            Init(registry, Name, serverPath: "/user/", serverHandlerName: "UserAccountServerURI");
 
            // check for user name seed
            IConfig loginConfig = config.Configs ["LoginService"];
            if (loginConfig != null)
            {
                string userNameSeed = loginConfig.GetString ("UserNameSeed", "");
                if (userNameSeed != "")
                    m_userNameSeed = userNameSeed.Split (',');
            }

        }

        public void Configure(IConfigSource config, IRegistryCore registry)
        {
            registry.RegisterModuleInterface<IUserAccountService>(this);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_AuthenticationService = registry.RequestModuleInterface<IAuthenticationService>();
            m_Database = Framework.Utilities.DataManager.RequestPlugin<IUserAccountData>();
            m_profileConnector = Framework.Utilities.DataManager.RequestPlugin<IProfileConnector>();
        }

        public void FinishedStartup()
        {
            // these are only valid if we are local
            if (!m_doRemoteCalls)
                AddCommands ();
        }

        private void AddCommands()
        {
            if (MainConsole.Instance != null)
            {
                if (!m_doRemoteCalls)
                {
                    MainConsole.Instance.Commands.AddCommand(
                        "create user",
                        "create user [<first> [<last> [<pass> [<email>]]]] [--system] [--uuid]",
                        "Create a new user. If optional parameters are not supplied required details will be prompted\n"+
                        "  --system : Enter user scope UUID\n"+
                        "  --uuid : Enter a specific UUID for the user",
                        HandleCreateUser, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "add user",
                        "add user [<first> [<last> [<pass> [<email>]]]] [--system] [--uuid]",
                        "Add (Create) a new user.  If optional parameters are not supplied required details will be prompted\n"+
                        "  --system : Enter user scope UUID\n"+
                        "  --uuid : Enter a specific UUID for the user",
                        HandleCreateUser, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "delete user",
                        "delete user  [<first> [<last>]] ",
                        "Deletes an existing user",
                        HandleDeleteUser, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "disable user",
                        "disable user  [<first> [<last>]] ",
                        "Disable an existing user",
                        HandleDisableUser, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "enable user",
                        "enable user  [<first> [<last>]] ",
                        "Enables an existing user that was previously disabled",
                        HandleEnableUser, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "reset user password",
                        "reset user password [<first> [<last> [<password>]]]",
                        "Reset a user password",
                        HandleResetUserPassword, false, true);
                        
                    MainConsole.Instance.Commands.AddCommand(
                        "show account",
                        "show account [<first> [<last>]]",
                        "Show account details for the given user",
                        HandleShowAccount, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "show user account",
                        "show user account [<first> [<last>]]",
                        "Show account details for the given user",
                        HandleShowUserAccount, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "set user level",
                        "set user level [<first> [<last> [<level>]]]",
                        "Set user level. If the user's level is > 0, this account will be treated as god-moded.\n" +
                        "It will also affect the 'login level' command. ",
                        HandleSetUserLevel, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "set user type",
                        "set user type [<first> [<last> [<type>]]]",
                        "Set the user account type. I.e. Guest, Resident, Member etc (Used for stipend payments)",
                        HandleSetUserType, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "set user profile title",
                        "set user profile title [<first> [<last> [<Title>]]]",
                        "Sets the title (Normally resident) in a user's title to some custom value.",
                        HandleSetTitle, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "set partner",
                        "set partner",
                        "Sets the partner in a user's profile.",
                        HandleSetPartner, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "reset partner",
                        "reset partner",
                        "Resets the partner in a user's profile.",
                        HandleResetPartner, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "load users",
                        "load user [<CSV file>]",
                        "Loads users from a CSV file into WhiteCore",
                        HandleLoadUsers, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "save users",
                        "save users [<CSV file>]",
                        "Saves all users from WhiteCore into a CSV file",
                        HandleSaveUsers, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "set user rezday",
                        "set user rezday [<first> [<last>]]",
                        "Sets the users creation date",
                        HandleSetRezday, false, true);
                }
            }
        }

        #endregion

        #region IUserAccountService Members

        public virtual IUserAccountService InnerService
        {
            get { return this; }
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public UserAccount GetUserAccount(List<UUID> scopeIDs, string firstName, string lastName)
        {
            UserAccount account;
            if (m_cache.Get(firstName + " " + lastName, out account))
                return AllScopeIDImpl.CheckScopeIDs(scopeIDs, account);

            object remoteValue = DoRemoteByURL("UserAccountServerURI", scopeIDs, firstName, lastName);
            if (remoteValue != null || m_doRemoteOnly)
            {
                UserAccount acc = (UserAccount) remoteValue;
                if (remoteValue != null)
                    m_cache.Cache(acc.PrincipalID, acc);

                return acc;
            }

            UserAccount[] d;

            d = m_Database.Get(scopeIDs,
                               new[] {"FirstName", "LastName"},
                               new[] {firstName, lastName});

            //try for different capitalization if needed
            if (d.Length < 1)
            {
               // try first character capitals
                firstName = char.ToUpper (firstName [0]) + firstName.Substring(1);

                d = m_Database.Get (scopeIDs,
                    new[] {"FirstName", "LastName"},
                    new[] {firstName, lastName});

                if (d.Length < 1)
                {
                    // try last name as well
                    lastName = char.ToUpper (lastName [0]) + lastName.Substring (1);

                    d = m_Database.Get (scopeIDs,
                        new[] {"FirstName", "LastName"},
                        new[] {firstName, lastName});
                }

            }

            if (d.Length < 1)
                return null;

            CacheAccount(d[0]);
            return d[0];
        }

        public void CacheAccount(UserAccount account)
        {
            if ((account != null) && (account.UserLevel <= -1))
                return;
            m_cache.Cache(account.PrincipalID, account);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public UserAccount GetUserAccount(List<UUID> scopeIDs, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            UserAccount account;
            if (m_cache.Get(name, out account))
                return AllScopeIDImpl.CheckScopeIDs(scopeIDs, account);

            object remoteValue = DoRemoteByURL("UserAccountServerURI", scopeIDs, name);
            if (remoteValue != null || m_doRemoteOnly)
            {
                UserAccount acc = (UserAccount) remoteValue;
                if (remoteValue != null)
                    m_cache.Cache(acc.PrincipalID, acc);

                return acc;
            }

            UserAccount[] d;

            d = m_Database.Get(scopeIDs,
                               new[] {"Name"},
                               new[] {name});

            //try for different capitalization if needed
            if (d.Length < 1)
            {
                var newName = name.Split (' ');
                if (newName.Length == 2)                    // in case of a bogus names
                {
                    var fName = newName [0];
                    var lName = newName [1];
  
                    // try first character capitals
                    fName = char.ToUpper (fName [0]) + fName.Substring (1);

                    d = m_Database.Get (scopeIDs,
                        new[] { "Name" },
                        new[] { fName + " " + lName });

                    if (d.Length < 1)
                    {
                        // try last name as well
                        lName = char.ToUpper (lName [0]) + lName.Substring (1);

                        d = m_Database.Get (scopeIDs,
                            new[] { "Name" },
                            new[] { fName + " " + lName });
                    }
                }

            }

            if (d.Length < 1)
            {
                string[] split = name.Split(' ');
                if (split.Length == 2)
                    return GetUserAccount(scopeIDs, split[0], split[1]);

                return null;
            }

            CacheAccount(d[0]);
            return d[0];
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low, RenamedMethod = "GetUserAccountUUID")]
        public UserAccount GetUserAccount(List<UUID> scopeIDs, UUID principalID)
        {
            UserAccount account;
            if (m_cache.Get(principalID, out account))
                return AllScopeIDImpl.CheckScopeIDs(scopeIDs, account);

            object remoteValue = DoRemoteByURL("UserAccountServerURI", scopeIDs, principalID);
            if (remoteValue != null || m_doRemoteOnly)
            {
                UserAccount acc = (UserAccount) remoteValue;
                if (remoteValue != null)
                    m_cache.Cache(principalID, acc);

                return acc;
            }

            UserAccount[] d;

            d = m_Database.Get(scopeIDs,
                               new[] {"PrincipalID"},
                               new[] {principalID.ToString()});

            if (d.Length < 1)
            {
                m_cache.Cache(principalID, null);
                return null;
            }

            CacheAccount(d[0]);
            return d[0];
        }

        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public bool StoreUserAccount(UserAccount data)
        {
            /*object remoteValue = DoRemoteByURL("UserAccountServerURI", data);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? false : (bool)remoteValue;*/

            m_registry.RequestModuleInterface<ISimulationBase>()
                      .EventManager.FireGenericEventHandler("UpdateUserInformation", data.PrincipalID);
            return m_Database.Store(data);
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<UserAccount> GetUserAccounts(List<UUID> scopeIDs, string query)
        {
            object remoteValue = DoRemoteByURL("UserAccountServerURI", scopeIDs, query);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<UserAccount>) remoteValue;

            UserAccount[] d = m_Database.GetUsers(scopeIDs, query);

            if (d == null)
                return new List<UserAccount>();

            List<UserAccount> ret = new List<UserAccount>(d);
            return ret;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<UserAccount> GetUserAccounts(List<UUID> scopeIDs, string query, uint? start, uint? count)
        {
            object remoteValue = DoRemoteByURL("UserAccountServerURI", scopeIDs, query);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<UserAccount>) remoteValue;

            UserAccount[] d = m_Database.GetUsers(scopeIDs, query, start, count);

            if (d == null)
                return new List<UserAccount>();

            List<UserAccount> ret = new List<UserAccount>(d);
            return ret;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public List<UserAccount> GetUserAccounts(List<UUID> scopeIDs, int level, int flags)
        {
            object remoteValue = DoRemoteByURL("UserAccountServerURI", level, flags);
            if (remoteValue != null || m_doRemoteOnly)
                return (List<UserAccount>) remoteValue;

            UserAccount[] d = m_Database.GetUsers(scopeIDs, level, flags);

            if (d == null)
                return new List<UserAccount>();

            List<UserAccount> ret = new List<UserAccount>(d);
            return ret;
        }

        [CanBeReflected(ThreatLevel = ThreatLevel.Low)]
        public uint NumberOfUserAccounts(List<UUID> scopeIDs, string query)
        {
            object remoteValue = DoRemoteByURL("UserAccountServerURI", scopeIDs, query);
            if (remoteValue != null || m_doRemoteOnly)
                return (uint) remoteValue;

            var userCount = m_Database.NumberOfUsers(scopeIDs, query);
            return userCount - Constants.SystemUserCount;
        }

        public void CreateUser(string name, string password, string email)
        {
            CreateUser(UUID.Random(), UUID.Zero, name, password, email);
        }

        /// <summary>
        ///     Create a user
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="scopeID"></param>
        /// <param name="name"></param>
        /// <param name="password"></param>
        /// <param name="email"></param>
        public string CreateUser(UUID userID, UUID scopeID, string name, string password, string email)
        {
            return CreateUser(new UserAccount(scopeID, userID, name, email), password);
        }

        /// <summary>
        ///     Create a user
        /// </summary>
        /// <param name="newAccount"></param>
        /// <param name="password"></param>
        //[CanBeReflected(ThreatLevel = ThreatLevel.Full)]
        public string CreateUser(UserAccount newAccount, string password)
        {
            /*object remoteValue = DoRemoteByURL("UserAccountServerURI", newAcc, password);
            if (remoteValue != null || m_doRemoteOnly)
                return remoteValue == null ? "" : remoteValue.ToString();*/

            UserAccount account = GetUserAccount(null, newAccount.PrincipalID);
            UserAccount nameaccount = GetUserAccount(null, newAccount.Name);
            if (account == null && nameaccount == null)
            {
                if (StoreUserAccount(newAccount))
                {
                    bool success;
                    if (m_AuthenticationService != null && password != "")
                    {
                        success = m_AuthenticationService.SetPasswordHashed(newAccount.PrincipalID, "UserAccount", password);
                        if (!success)
                        {
                            MainConsole.Instance.WarnFormat(
                                "[USER ACCOUNT SERVICE]: Unable to set password for account {0}.",
                                newAccount.Name);
                            return "Unable to set password";
                        }
                    }

                    MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Account {0} created successfully",
                                                    newAccount.Name);
                    //Cache it as well
                    CacheAccount(newAccount);
                    m_registry.RequestModuleInterface<ISimulationBase>()
                              .EventManager.FireGenericEventHandler("CreateUserInformation", newAccount.PrincipalID);

                    // create a profile for the new user as well
                    if (m_profileConnector != null)
                    {
                        m_profileConnector.CreateNewProfile (newAccount.PrincipalID);
                        IUserProfileInfo profile = m_profileConnector.GetUserProfile (newAccount.PrincipalID);

                        // if (AvatarArchive != "")
                        //    profile.AArchiveName = AvatarArchive;
                        profile.MembershipGroup = "Resident";
                        profile.IsNewUser = true;
                        m_profileConnector.UpdateUserProfile (profile);
                    }

                    return "";
                }

                MainConsole.Instance.ErrorFormat("[USER ACCOUNT SERVICE]: Account creation failed for account {0}", newAccount.Name);
                return "Unable to save account";

            }

            MainConsole.Instance.ErrorFormat("[USER ACCOUNT SERVICE]: A user with the name {0} already exists!", newAccount.Name);
            return "A user with the same name already exists";

        }

        public void DeleteUser(UUID userID, string name, string password, bool archiveInformation, bool wipeFromDatabase)
        {
            //if (password != "" && m_AuthenticationService.Authenticate(userID, "UserAccount", password, 0) == "")
            //    return; //Not authed

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser(userID))
            {
                MainConsole.Instance.Warn ("[USER ACCOUNT SERVICE]: Deleting a system user account is not a good idea!");
                return;
            }


            if (!m_Database.DeleteAccount(userID, archiveInformation))
            {
                MainConsole.Instance.WarnFormat(
                    "[USER ACCOUNT SERVICE]: Failed to remove the account for {0}, please check that the database is valid after this operation!",
                    userID);
                return;
            }

            if (wipeFromDatabase)
                m_registry.RequestModuleInterface<ISimulationBase>()
                          .EventManager.FireGenericEventHandler("DeleteUserInformation", userID);
            m_cache.Remove(userID, name);
        }

        #endregion

        #region Console commands

        /// <summary>
        /// Handles the set partner command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdParams">Cmd parameters.</param>
        protected void HandleSetPartner(IScene scene, string[] cmdParams)
        {
            string first = MainConsole.Instance.Prompt("First User's name (<first> <last>)");
            string second = MainConsole.Instance.Prompt("Second User's name (<first> <last>)");
            if (second == first)
            {
                MainConsole.Instance.Error("[USER ACCOUNT SERVICE]: You are not able to set yourself as your partner");
                return;
            }
            else
            {
                if (m_profileConnector != null)
                {
                    IUserProfileInfo firstProfile =
                        m_profileConnector.GetUserProfile(GetUserAccount(null, first).PrincipalID);
                    IUserProfileInfo secondProfile =
                        m_profileConnector.GetUserProfile(GetUserAccount(null, second).PrincipalID);

                    if (firstProfile == null || secondProfile == null)
                    {
                        MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: At least one of these users does not have a profile?");
                        return;
                    }

                    firstProfile.Partner = secondProfile.PrincipalID;
                    secondProfile.Partner = firstProfile.PrincipalID;

                    m_profileConnector.UpdateUserProfile(firstProfile);
                    m_profileConnector.UpdateUserProfile(secondProfile);

                    MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Partner information updated. ");
                }
            }
        }

        /// <summary>
        /// Handles the reset partner command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdParams">Cmd parameters.</param>
        protected void HandleResetPartner(IScene scene, string[] cmdParams)
        {
            string first = MainConsole.Instance.Prompt("First User's name (<first> <last>)");

            if (m_profileConnector != null)
            {
                IUserProfileInfo firstProfile =
                    m_profileConnector.GetUserProfile(GetUserAccount(null, first).PrincipalID);

                // Find the second partner through the first user details
                if (firstProfile.Partner == UUID.Zero)
                {
                    MainConsole.Instance.Error("[USER ACCOUNT SERVICE]: This user doesn't have a partner");
                    return;
                }
                else if (firstProfile.Partner == firstProfile.PrincipalID)
                {
                    MainConsole.Instance.Error("[USER ACCOUNT SERVICE]: Deadlock situation avoided, this avatar is his own partner");
                    firstProfile.Partner = UUID.Zero;
                    m_profileConnector.UpdateUserProfile(firstProfile);
                }
                else
                {
                    IUserProfileInfo secondProfile =
                            m_profileConnector.GetUserProfile(GetUserAccount(null, firstProfile.Partner).PrincipalID);

                    firstProfile.Partner = UUID.Zero;
                    secondProfile.Partner = UUID.Zero;

                    m_profileConnector.UpdateUserProfile(firstProfile);
                    m_profileConnector.UpdateUserProfile(secondProfile);

                    MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Partner information updated. ");
                }
            }
        }

        /// <summary>
        /// Handles the user set title command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSetTitle(IScene scene, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string title;

            firstName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt("First name") : cmdparams[4];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 6 ? MainConsole.Instance.Prompt("Last name") : cmdparams[5];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
            {
                    MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: No such user");
                return;
            }
            title = cmdparams.Length < 7 ? MainConsole.Instance.Prompt("User Title") : Util.CombineParams(cmdparams, 6);
            if (m_profileConnector != null)
            {
                IUserProfileInfo profile = m_profileConnector.GetUserProfile(account.PrincipalID);
                if (profile != null)
                {
                    // this is not right is it?  >> profile.MembershipGroup = title;
                    profile.CustomType = title;
                    m_profileConnector.UpdateUserProfile (profile);
                }
                else {
                        MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: There does not appear to be a profile for this user?");
                    return;
                }

            }
            bool success = StoreUserAccount(account);
            if (!success)
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Unable to set user profile title for account {0} {1}.", firstName,
                                                lastName);
            else
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: User profile title set for user {0} {1} to {2}", firstName, lastName,
                                                title);
        }

        /// <summary>
        /// Handles the set user level command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSetUserLevel(IScene scene, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string rawLevel;
            int level;

            firstName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt("First name") : cmdparams[3];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt("Last name") : cmdparams[4];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
            {
                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Unable to locate this user");
                return;
            }

            // ensure the protected system users are left alone!
            if (Utilities.IsSystemUser(account.PrincipalID))
            {
                MainConsole.Instance.Warn ("[USER ACCOUNT SERVICE]: Changing system users is not a good idea!");
                return;
            }

            rawLevel = cmdparams.Length < 6 ? MainConsole.Instance.Prompt("User level") : cmdparams[5];
            int.TryParse (rawLevel, out level);

            if (level > 255 || level < 0)
            {
                MainConsole.Instance.Warn("Invalid user level");
                return;
            }

            account.UserLevel = level;

            bool success = StoreUserAccount(account);
            if (!success)
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Unable to set user level for account {0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: User level set for user {0} {1} to {2}", firstName, lastName, level);
        }

        private int UserTypeToUserFlags(string userType)
        {
            switch (userType)
            {
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

        private string UserFlagToType(int userFlags)
        {
            switch (userFlags)
            {
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

        string UserGodLevel(int level)
        {
            switch (level)
            {
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
                return "Liason";
            case Constants.USER_GOD_FULL:
                return "A God";
            case Constants.USER_GOD_MAINTENANCE:
                return"Super God";
            default:
                return "User";
            }
        }


        protected void HandleShowUserAccount(IScene scene, string[] cmd)
        {
            // remove 'user' from the cmd 
            var cmdparams = new List<string>(cmd);
            cmdparams.RemoveAt (1);
            cmd = cmdparams.ToArray();

            HandleShowAccount(scene, cmd);

        }

        /// <summary>
        /// Handles the show account command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleShowAccount(IScene scene, string[] cmdparams)
        {
            string firstName;
            string lastName;

            firstName = cmdparams.Length < 3 ? MainConsole.Instance.Prompt("First name") : cmdparams[2];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt("Last name") : cmdparams[3];
            if (lastName == "")
                return;

            UserAccount ua = GetUserAccount(null, firstName, lastName);
            if (ua == null)
            {
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Unable to find user '{0} {1}'", firstName, lastName);
                return;
            }

            MainConsole.Instance.CleanInfo("  Name   : " + ua.Name);
            MainConsole.Instance.CleanInfo("  ID     : " + ua.PrincipalID);
            MainConsole.Instance.CleanInfo("  E-mail : " + ua.Email);
            MainConsole.Instance.CleanInfo("  Created: " + Utils.UnixTimeToDateTime(ua.Created));
            MainConsole.Instance.CleanInfo("  Level  : " + UserGodLevel(ua.UserLevel));
            MainConsole.Instance.CleanInfo("  Type   : " + UserFlagToType(ua.UserFlags));
        }
            
        /// <summary>
        /// Handles the set user level command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSetUserType(IScene scene, string[] cmdparams)
        {
            string firstName;
            string lastName;
            List <string> userTypes = new List<string>(new [] {"Guest", "Resident", "Member", "Contractor", "Charter_Member"});
            int userFlags;

            firstName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt("First name") : cmdparams[3];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt("Last name") : cmdparams[4];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
            {
                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Unable to locate this user");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser(account.PrincipalID))
            {
                MainConsole.Instance.Warn ("[USER ACCOUNT SERVICE]: Changing system users is not a good idea!");
            }

            // Get user type (for payments etc)
            var userType = MainConsole.Instance.Prompt("User type", "Resident", userTypes);
            userFlags = UserTypeToUserFlags (userType);

            account.UserFlags = userFlags;

            bool success = StoreUserAccount(account);
            if (success)
            {
                MainConsole.Instance.InfoFormat ("[USER ACCOUNT SERVICE]: User '{0} {1}' set to {2}", firstName, lastName, userType);

                // update profile for the user as well
                if (m_profileConnector != null)
                {
                    IUserProfileInfo profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    if (profile == null)
                    {
                        m_profileConnector.CreateNewProfile (account.PrincipalID);          // create a profile for the user
                        profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    }

                    // if (AvatarArchive != "")
                    //    profile.AArchiveName = AvatarArchive;
                    profile.MembershipGroup = UserFlagToType(account.UserFlags);
                    profile.IsNewUser = true;
                    m_profileConnector.UpdateUserProfile (profile);
                }
            }
            else
                MainConsole.Instance.InfoFormat ("[USER ACCOUNT SERVICE]: Unable to set user type for account '{0} {1}'.", firstName, lastName);

        }

        public List<string> GetAvatarArchivesFiles()
        {
            IAvatarAppearanceArchiver avieArchiver = m_registry.RequestModuleInterface<IAvatarAppearanceArchiver>();
            List<string> archives =  avieArchiver.GetAvatarArchiveFilenames();

            return archives;

        }

        /// <summary>
        ///     Handle the create (add) user command from the console.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname, password, email</param>
        protected void HandleCreateUser(IScene scene, string[] cmd)
        {
            //string firstName = "Default";
            //string lastName = "User";
            string userName = "";
            string password, email, uuid, scopeID;
            bool sysFlag = false;
            bool uuidFlag = false;
            List <string> userTypes = new List<string>(new [] {"Guest", "Resident", "Member", "Contractor", "Charter_Member"});

            List<string> cmdparams = new List<string>(cmd);
            foreach (string param in cmd)
            {
                if (param.StartsWith("--system"))
                {
                    sysFlag = true;
                    cmdparams.Remove(param);
                }
                if (param.StartsWith("--uuid"))
                {
                    uuidFlag = true;
                    cmdparams.Remove(param);
                }

            }

            // check for provided user name
            if (cmdparams.Count >= 4)
            {
                userName = cmdparams [2] + " " + cmdparams [3];
            } else
            {
                Utilities.MarkovNameGenerator ufNames = new Utilities.MarkovNameGenerator ();
                Utilities.MarkovNameGenerator ulNames = new Utilities.MarkovNameGenerator ();
                string[] nameSeed = m_userNameSeed == null ? Utilities.UserNames : m_userNameSeed;

                string firstName = ufNames.FirstName (nameSeed, 3, 4);
                string lastName = ulNames.FirstName (nameSeed, 5, 6);
                string enteredName = firstName + " " + lastName;
                if (userName != "")
                    enteredName = userName;

                do
                {
                    userName = MainConsole.Instance.Prompt ("User Name (? for suggestion)", enteredName);
                    if (userName == "" || userName == "?")
                    {
                        enteredName = ufNames.NextName + " " + ulNames.NextName;
                        userName = "";
                        continue;
                    }
                } while (userName == "");
                ufNames.Reset ();
                ulNames.Reset ();
            }

            // we have the name so check to make sure it is allowed
            UserAccount ua = GetUserAccount(null, userName);
            if (ua != null)
            {
                MainConsole.Instance.WarnFormat("[USER ACCOUNT SERVICE]: This user, '{0}' already exists!", userName);
                return;
            }

            // password as well?
            password = cmdparams.Count < 5 ? MainConsole.Instance.PasswordPrompt("Password") : cmdparams[4];

            // maybe even an email?
            if (cmdparams.Count < 6 )
            { 
                email = MainConsole.Instance.Prompt ("Email for password recovery. ('none' if unknown)","none");
            }
            else
                email = cmdparams[5];

            if ((email.ToLower() != "none") && !Utilities.IsValidEmail(email))
            {
                MainConsole.Instance.Warn ("This does not look like a vaild email address. ('none' if unknown)");
                email = MainConsole.Instance.Prompt ("Email", email);
            }

            // Get user type (for payments etc)
            var userType = MainConsole.Instance.Prompt("User type", "Resident", userTypes);

            // Get available user avatar acrchives
            var userAvatarArchive = "";
            var avatarArchives = GetAvatarArchivesFiles ();
            if (avatarArchives.Count > 0)
            {
                avatarArchives.Add("None");
                userAvatarArchive = MainConsole.Instance.Prompt("Avatar archive to use", "None", avatarArchives);
                if (userAvatarArchive == "None")
                    userAvatarArchive = "";
            }

            // Allow the modifcation the UUID if required - for matching user UUID with other Grids etc eg SL
            uuid = UUID.Random().ToString();
            if (uuidFlag)
                while (true)
                {
                    uuid = MainConsole.Instance.Prompt("UUID (Required avatar UUID)", uuid);
                    UUID test;
                    if (UUID.TryParse(uuid, out test))
                        break;

                    MainConsole.Instance.Error("There was a problem verifying this UUID. Please retry.");
                }

            // this really should not be altered so hide it normally
            scopeID = UUID.Zero.ToString ();
            if (sysFlag)
            {
                scopeID = MainConsole.Instance.Prompt("Scope (Don't change unless you know what this is)", scopeID);
            }

            // we should be good to go
            CreateUser(UUID.Parse(uuid), UUID.Parse(scopeID), userName, Util.Md5Hash(password), email);
            // CreateUser will tell us success or problem
            //MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: User '{0}' created", name);

            // check for success
            UserAccount account = GetUserAccount(null, userName);
            if (account != null)
            {
                account.UserFlags = UserTypeToUserFlags (userType);
                StoreUserAccount(account);

                // update profile for the user as well
                if (m_profileConnector != null)
                {
                    IUserProfileInfo profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    if (profile == null)
                    {
                        m_profileConnector.CreateNewProfile (account.PrincipalID);          // create a profile for the user
                        profile = m_profileConnector.GetUserProfile (account.PrincipalID);
                    }

                    if (userAvatarArchive != "")
                        profile.AArchiveName = userAvatarArchive+".aa";
                    profile.MembershipGroup = UserFlagToType(account.UserFlags);
                    profile.IsNewUser = true;
                    m_profileConnector.UpdateUserProfile (profile);
                }
            } else
                MainConsole.Instance.WarnFormat("[USER ACCOUNT SERVICE]: There was a problem creating the account for '{0}'", userName);

        }

        /// <summary>
        /// Handles the delete user command.
        /// Delete or disable a user account
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname, password</param>
        protected void HandleDeleteUser(IScene scene, string[] cmd)
        {
            string firstName, lastName, password;

            // check for passed username
            firstName = cmd.Length < 3 ? MainConsole.Instance.Prompt("First name") : cmd[2];
            if (firstName == "")
                return;
            lastName = cmd.Length < 4 ? MainConsole.Instance.Prompt("Last name") : cmd[3];
            if (lastName == "")
                return;

            // password as well?
            password = cmd.Length < 5 ? MainConsole.Instance.PasswordPrompt("Password") : cmd[4];

            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
            {
                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: No user with that name!");
                return;
            }

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser(account.PrincipalID))
            {
                MainConsole.Instance.Warn ("[USER ACCOUNT SERVICE]: Naughty!! You cannot delete system users!");
                return;
            }

            bool archive;
            bool all = false;

            archive = MainConsole.Instance.Prompt("Archive Information (just disable their login, but keep their information): (yes/no)", "yes").ToLower() == "yes";
            if (!archive)
                all = MainConsole.Instance.Prompt("Remove all user information (yes/no)", "yes").ToLower() == "yes";

            if (archive || all)
            {
                DeleteUser (account.PrincipalID, account.Name, password, archive, all);
                if (all)
                    MainConsole.Instance.InfoFormat ("[USER ACCOUNT SERVICE]: User account '{0}' deleted", account.Name);
                else
                    MainConsole.Instance.InfoFormat ("[USER ACCOUNT SERVICE]: User account '{0}' disabled", account.Name);
            }
        }

        /// <summary>
        /// Handles the disable user command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname.</param>
        protected void HandleDisableUser(IScene scene, string[] cmd)
        {
            string firstName, lastName;

            // check for passed username
            firstName = cmd.Length < 3 ? MainConsole.Instance.Prompt("First name") : cmd[2];
            if (firstName == "")
                return;
            lastName = cmd.Length < 4 ? MainConsole.Instance.Prompt("Last name") : cmd[3];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
            {
                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Unable to locate this user!");
                return;
            }

            // if the user is disabled details will exist with a level set @ -2
            if (account.UserLevel < 0)
            {
                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: User is already diabled!");
                return;
            }

            account.UserLevel = -2;
            bool success = StoreUserAccount(account);
            if (!success)
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Unable to disable account {0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: User account {0} {1} disabled.", firstName, lastName);
        }

        /// <summary>
        /// Handles the enable user command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname.</param>
        protected void HandleEnableUser(IScene scene, string[] cmd)
        {
            string firstName, lastName;

            // check for passed username
            firstName = cmd.Length < 3 ? MainConsole.Instance.Prompt("First name") : cmd[2];
            if (firstName == "")
                return;
            lastName = cmd.Length < 4 ? MainConsole.Instance.Prompt("Last name") : cmd[3];
            if (lastName == "")
                return;
             
            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
            {
                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Unable to locate this user!");
                return;
            }

            // if the user is disabled details will exist with a level set @ -2
            account.UserLevel = 0;

            bool success = StoreUserAccount(account);
            if (!success)
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Unable to enable account {0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: User account {0} {1} enabled.", firstName, lastName);
        }

        /// <summary>
        /// Handles the reset user password command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname, newpassword,</param>
        protected void HandleResetUserPassword(IScene scene, string[] cmd)
        {
            string firstName, lastName;
            string newPassword;

            // check for passed username
            firstName = cmd.Length < 4 ? MainConsole.Instance.Prompt("First name") : cmd[3];
            if (firstName == "")
                return;
            lastName = cmd.Length < 5 ? MainConsole.Instance.Prompt("Last name") : cmd[4];
            if (lastName == "")
                return;

            // password as well?
            newPassword = cmd.Length < 6 ? MainConsole.Instance.PasswordPrompt("New Password") : cmd[5];

            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
                MainConsole.Instance.ErrorFormat("[USER ACCOUNT SERVICE]: Unable to locate this user");

            // ensure the system users are left alone!
            if (Utilities.IsSystemUser(account.PrincipalID))
            {
                MainConsole.Instance.Warn ("[USER ACCOUNT SERVICE]: Changing system users is not a good idea!");
                return;
            }

            bool success = false;
            if (m_AuthenticationService != null)
                success = m_AuthenticationService.SetPassword(account.PrincipalID, "UserAccount", newPassword);
            if (!success)
                MainConsole.Instance.ErrorFormat(
                    "[USER ACCOUNT SERVICE]: Unable to reset password for account '{0} {1}.", firstName, lastName);
            else
                MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Password reset for user '{0} {1}", firstName, lastName);
        }

        /// <summary>
        /// Handles the load users command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleLoadUsers(IScene scene, string[] cmdParams)
        {
            string fileName = "users.csv";
            if (cmdParams.Length < 3)
            {
                fileName = MainConsole.Instance.Prompt ("Please enter the user CSV file to load", fileName);
                if (fileName == "")
                    return;
            } else
                fileName = cmdParams [2];

            int userNo = 0;
            string FirstName;
            string LastName;
            string Password;
            string Email;
            UUID UserUUID;

            fileName = PathHelpers.VerifyReadFile(fileName,"csv", Constants.DEFAULT_DATA_DIR+"/Updates");
            if(fileName == "")
            {
                MainConsole.Instance.Error("The file " + fileName + " does not exist. Please check and retry");
                return;
            }

            // good to go...
            using (var rd = new StreamReader (fileName))
            {
                while (!rd.EndOfStream)
                {
                    var userInfo = rd.ReadLine ().Split (',');
                    if (userInfo.Length < 4)
                    {
                        MainConsole.Instance.Error ("[User Load]: Insufficient details; Skipping " + userInfo);
                        continue;
                    }

                    UserUUID = (UUID)userInfo [0];
                    FirstName = userInfo [1];
                    LastName = userInfo [2];
                    Password = userInfo [3];
                    Email = userInfo.Length < 6 ? userInfo [4] : "";

                    string check = CreateUser (UserUUID, UUID.Zero, FirstName + " " + LastName, Util.Md5Hash(Password), Email);
                    if (check != "")
                    {
                        MainConsole.Instance.Error ("Couldn't create the user. Reason: " + check);
                        continue;
                    }

                    //set user levels and status  (if needed)
                    var account = GetUserAccount (null, UserUUID);
                    //account.UserLevel = 0;
                    account.UserFlags = Constants.USER_FLAG_RESIDENT;
                    StoreUserAccount (account);

                    userNo++;

                }
                MainConsole.Instance.InfoFormat ("File: {0} loaded,  {1} users added", Path.GetFileName(fileName), userNo);
            }

        }
        
        /// <summary>
        /// Handles the save users command.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmdparams">Cmdparams.</param>
        protected void HandleSaveUsers(IScene scene, string[] cmdParams)
        {
            string fileName = "users.csv";
            if (cmdParams.Length < 3)
            {
                fileName = MainConsole.Instance.Prompt ("Please enter the user CSV file to save", fileName);
                if (fileName == "")
                    return;
            } else
                fileName = cmdParams [2];

            int userNo = 0;

            fileName = PathHelpers.VerifyWriteFile(fileName,"csv", Constants.DEFAULT_DATA_DIR+"/Updates", true);
            if(fileName == "")
                return;
    
            // good to go...
            var accounts = GetUserAccounts(null,"*");


            //Add the user
            FileStream stream = new FileStream(fileName, FileMode.Create);          // always start fresh
            StreamWriter streamWriter = new StreamWriter(stream);
            streamWriter.BaseStream.Position += streamWriter.BaseStream.Length;

            foreach (UserAccount user in accounts)
            {
                if (Utilities.IsSystemUser (user.PrincipalID))
                    continue;
  
                // TODO: user accounts do not have a clear password so we need to save the salt and password hashes instead
                // This will mean changes to the csv format
                string LineToWrite = user.PrincipalID + "," + user.FirstName + "," + user.LastName + ",," + user.Email;
                streamWriter.WriteLine (LineToWrite);

                userNo++;
            }
            streamWriter.Flush();
            streamWriter.Close();

            MainConsole.Instance.InfoFormat ("File: {0} saved with {1} users", Path.GetFileName(fileName), userNo);

        }

        protected void HandleSetRezday(IScene scene, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string rawDate;

            firstName = cmdparams.Length < 4 ? MainConsole.Instance.Prompt("First name") : cmdparams[3];
            if (firstName == "")
                return;

            lastName = cmdparams.Length < 5 ? MainConsole.Instance.Prompt("Last name") : cmdparams[4];
            if (lastName == "")
                return;

            UserAccount account = GetUserAccount(null, firstName, lastName);
            if (account == null)
            {
                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Unable to locate this user");
                return;
            }

            rawDate = MainConsole.Instance.Prompt("Date (mm/dd/yyyy)");

            // Make a new DateTime from rawDate
            DateTime newDate = DateTime.ParseExact(rawDate, "MM/dd/yyyy", CultureInfo.InvariantCulture);
            // Get difference between the 2 dates
            TimeSpan parsedDate = (newDate - new DateTime(1970, 1, 1, 0, 0, 0));
            // Return Unix Timestamp
            if (m_profileConnector != null)
            {
            	IUserProfileInfo profile = m_profileConnector.GetUserProfile (account.PrincipalID);
            	profile.Created = (int)parsedDate.TotalSeconds;
            	bool success = m_profileConnector.UpdateUserProfile (profile);
            	if (!success)
            		MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: Unable to change rezday for {0} {1}.", firstName, lastName);
            	else
            		MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: User account {0} {1} has a new rezday.", firstName, lastName);
            }
        }
        #endregion
    }
}