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

namespace WhiteCore.Services.SQLServices.UserAccountService
{
    public class UserAccountService : ConnectorBase, IUserAccountService, IService
    {
        #region Declares

        protected IProfileConnector m_profileConnector;
        protected IAuthenticationService m_AuthenticationService;
        protected IUserAccountData m_Database;
        protected GenericAccountCache<UserAccount> m_cache = new GenericAccountCache<UserAccount>();

        #endregion

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
 
        }

        public void Configure(IConfigSource config, IRegistryCore registry)
        {
            if (MainConsole.Instance != null)
            {
                if (!m_doRemoteCalls)
                {
                    MainConsole.Instance.Commands.AddCommand(
                        "create user",
                        "create user [<first> [<last> [<pass> [<email>]]]]",
                        "Create a new user",
                        HandleCreateUser, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "add user",
                        "add user [<first> [<last> [<pass> [<email>]]]]",
                        "Add (Create) a new user",
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
                        "set user profile title",
                        "set user profile title [<first> [<last> [<Title>]]]",
                        "Sets the title (Normally resident) in a user's title to some custom value.",
                        HandleSetTitle, false, true);

                    MainConsole.Instance.Commands.AddCommand(
                        "set partner",
                        "set partner",
                        "Sets the partner in a user's profile.",
                        HandleSetPartner, false, true);
                }
            }
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
            // check and/or create default RealEstate user
            if (!m_doRemoteCalls)
                CheckRealEstateUserInfo ();

        }

        /// <summary>
        /// Checks and creates the real estate user.
        /// </summary>
        private void CheckRealEstateUserInfo()
        {
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService> ();
            if (accountService == null)
                return;

            UserAccount uinfo = accountService.GetUserAccount(null, UUID.Parse(Constants.RealEstateOwnerUUID));

            if (uinfo == null)
            {
                MainConsole.Instance.Warn ("Creating System User " + Constants.RealEstateOwnerName);
                var newPassword = Utilities.RandomPassword.Generate (2, 1, 0);

                accountService.CreateUser (
                    (UUID)Constants.RealEstateOwnerUUID,    // UUID
                    UUID.Zero,                              // ScopeID
                    Constants.RealEstateOwnerName,          // Name
                    newPassword, "");                                // password , email

                MainConsole.Instance.Info (" The password for the RealEstate user is : " + newPassword);

                // Create Standard Inventory
                IInventoryService inventoryService = m_registry.RequestModuleInterface<IInventoryService> ();
                inventoryService.CreateUserInventory ((UUID)Constants.RealEstateOwnerUUID, true);

                //set as "Maintenace" level
                var account = accountService.GetUserAccount(null, UUID.Parse(Constants.RealEstateOwnerUUID));
                account.UserLevel = 250;
                bool success = StoreUserAccount(account);

                if (success)
                    MainConsole.Instance.Info (" The RealEstate user has been elevated to 'Maintenance' level");
                    
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

            // ensure the main library/realestate owner is left alone!
            var libraryOwner = new UUID (Constants.LibraryOwner);
            var realestateOwner = new UUID(Constants.RealEstateOwnerUUID);

            if ( (userID == libraryOwner) || (userID == realestateOwner) )
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

            if (m_profileConnector != null)
            {
                IUserProfileInfo firstProfile =
                    m_profileConnector.GetUserProfile(GetUserAccount(null, first).PrincipalID);
                IUserProfileInfo secondProfile =
                    m_profileConnector.GetUserProfile(GetUserAccount(null, second).PrincipalID);

                if (firstProfile == null || secondProfile == null)
                {
                    MainConsole.Instance.Warn ("[USER ACCOUNT SERVICE]: At least one of these users does not have a profile?");
                    return;
                }

                firstProfile.Partner = secondProfile.PrincipalID;
                secondProfile.Partner = firstProfile.PrincipalID;

                m_profileConnector.UpdateUserProfile(firstProfile);
                m_profileConnector.UpdateUserProfile(secondProfile);

                MainConsole.Instance.Warn("[USER ACCOUNT SERVICE]: Partner information updated. ");
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
                    profile.MembershipGroup = title;
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

            // ensure the main library/realestate owner is left alone!
            var libraryOwner = new UUID (Constants.LibraryOwner);
            var realestateOwner = new UUID(Constants.RealEstateOwnerUUID);
            if ( (account.PrincipalID == libraryOwner) || (account.PrincipalID == realestateOwner) )
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
            MainConsole.Instance.CleanInfo("  Level  : " + (ua.UserLevel < 0 ? "Disabled" : ua.UserLevel.ToString ()) );
            MainConsole.Instance.CleanInfo("  Flags  : " + ua.UserFlags);
        }

        /// <summary>
        ///     Handle the create (add) user command from the console.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">string array with parameters: firstname, lastname, password, email</param>
        protected void HandleCreateUser(IScene scene, string[] cmd)
        {
            string firstName = "Default";
            string lastName = "User";
            string password, email, uuid, scopeID;
            bool sysFlag = false;

            List<string> cmdparams = new List<string>(cmd);
            foreach (string param in cmd)
            {
                if (param.StartsWith("--system"))
                {
                    sysFlag = true;
                    cmdparams.Remove(param);
                }
            }

            // check for passed username
            firstName = cmdparams.Count < 3 ? MainConsole.Instance.Prompt("First name") : cmdparams[2];
            if (firstName == "")
                return;

            lastName = cmdparams.Count < 4 ? MainConsole.Instance.Prompt("Last name") : cmdparams[3];
            if (lastName == "")
                return;

            // password as well?
            password = cmdparams.Count < 5 ? MainConsole.Instance.PasswordPrompt("Password") : cmdparams[4];

            // maybe even an email?
            if (cmdparams.Count < 6 )
            { 
                email = MainConsole.Instance.Prompt ("Email");
            }
            else
                email = cmdparams[5];

            if (!Utilities.IsValidEmail(email))
            {
                MainConsole.Instance.Warn ("This does not look like a vaild email address. Please re-enter");
                email = MainConsole.Instance.Prompt ("Email", email);
            }

            // Allow the user to modify the UUID  - for matching with other Grids etc eg SL
            uuid = UUID.Random().ToString();
            uuid = MainConsole.Instance.Prompt("UUID (Don't change unless you have a reason)", uuid);

            // this really should not be altered so hide it normally
            scopeID = UUID.Zero.ToString ();
            if (sysFlag)
            {
                scopeID = MainConsole.Instance.Prompt("Scope (Don't change unless you know what this is)", scopeID);
            }

            // check to make sure
            UserAccount ua = GetUserAccount(null, firstName, lastName);
            if (ua != null)
            {
                MainConsole.Instance.WarnFormat("[USER ACCOUNT SERVICE]: This user, '{0} {1}' already exists!", firstName, lastName);
                return;
            }

            string name = firstName + " " + lastName;
            CreateUser(UUID.Parse(uuid), UUID.Parse(scopeID), name, Util.Md5Hash(password), email);
            // CreateUser will tell us success or problem
            //MainConsole.Instance.InfoFormat("[USER ACCOUNT SERVICE]: User '{0}' created", name);
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

            // ensure the main library/realestate owner is left alone!
            var libraryOwner = new UUID (Constants.LibraryOwner);
            var realestateOwner = new UUID(Constants.RealEstateOwnerUUID);
            if ( (account.PrincipalID == libraryOwner) || (account.PrincipalID == realestateOwner) )
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

            // ensure the main library/realestate owner is left alone!
            var libraryOwner = new UUID (Constants.LibraryOwner);
            var realestateOwner = new UUID(Constants.RealEstateOwnerUUID);
            if ( (account.PrincipalID == libraryOwner) || (account.PrincipalID == realestateOwner) )
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

        #endregion
    }
}