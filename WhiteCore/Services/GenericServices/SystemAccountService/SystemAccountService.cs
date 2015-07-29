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
using System.IO;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

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

        IRegistryCore m_registry;

        #region ISystemAccountService Members

        public UUID GovernorUUID
        {
            get { return (UUID) Constants.GovernorUUID; }
        }

        public string GovernorName
        {
            get { return governorName; }
        }

        public UUID SystemEstateOwnerUUID
        {
            get { return (UUID) Constants.RealEstateOwnerUUID; }
        }

        public string SystemEstateOwnerName
        {
            get { return realEstateOwnerName; }
        }

        public UUID BankerUUID
        {
            get { return (UUID) Constants.BankerUUID; }
        }

        public string BankerName
        {
            get { return bankerName; }
        }

        public UUID MarketplaceOwnerUUID
        {
            get { return (UUID) Constants.MarketplaceOwnerUUID; }
        }

        public string MarketplaceOwnerName
        {
            get { return marketplaceOwnerName; }
        }

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {

            IConfig estConfig = config.Configs["SystemUserService"];
            if (estConfig != null)
            {
                governorName = estConfig.GetString("GovernorName", governorName);
                realEstateOwnerName = estConfig.GetString("RealEstateOwnerName", realEstateOwnerName);
                bankerName = estConfig.GetString("BankerName", bankerName);
                marketplaceOwnerName = estConfig.GetString("MarketplaceOwnerName", marketplaceOwnerName);
            }

            registry.RegisterModuleInterface<ISystemAccountService>(this);
            m_registry = registry;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            m_accountService = m_registry.RequestModuleInterface<IUserAccountService>();

            // these are only valid if we are local
            if (!m_accountService.RemoteCalls())
            {
                // check and/or create default RealEstate user
                CheckSystemUserInfo ();

                AddCommands ();
            }

        }

        #endregion

        void AddCommands()
        {
            if (MainConsole.Instance != null)
            {
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
        void CheckSystemUserInfo()
        {
            if (m_accountService == null)
                return;

            VerifySystemUserInfo("Governor", GovernorUUID, GovernorName, 250);
            VerifySystemUserInfo("RealEstate", SystemEstateOwnerUUID, SystemEstateOwnerName, 150);
            VerifySystemUserInfo("Banker", BankerUUID, BankerName, 100);
            VerifySystemUserInfo("Marketplace", MarketplaceOwnerUUID, MarketplaceOwnerName, 100);

        }

        void VerifySystemUserInfo(string usrType, UUID usrUUID, string usrName, int usrLevel)
        {

            var userAccount = m_accountService.GetUserAccount (null, usrUUID);
            var userPassword = Utilities.RandomPassword.Generate (2, 1, 0);

            if (userAccount == null)
            {
                MainConsole.Instance.WarnFormat ("Creating the {0} user '{1}'", usrType, usrName);

                var error = m_accountService.CreateUser (
                    usrUUID,                                // user UUID
                    UUID.Zero,                              // scope
                    usrName,                                // name
                    Util.Md5Hash (userPassword),            // password
                    "");                                    // email

                if (error == "")
                {
                    SaveSystemUserPassword (usrType, usrName, userPassword);
                    MainConsole.Instance.InfoFormat (" The password for '{0}' is : {1}", usrName, userPassword);

                } else
                {
                    MainConsole.Instance.WarnFormat (" Unable to create the {0} user : {1}", usrType, error);
                    return;
                }

                //set  "God" level
                var account = m_accountService.GetUserAccount (null, usrUUID);
                account.UserLevel = usrLevel;
                account.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                bool success = m_accountService.StoreUserAccount (account);

                if (success)
                    MainConsole.Instance.InfoFormat (" The {0} user has been elevated to '{1}' level", usrType, m_accountService.UserGodLevel(usrLevel));

                return;

            }

            // we already have the account.. verify details in case of a configuration change
            if (userAccount.Name != usrName)
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();

                userAccount.Name = usrName;
                bool updatePass = authService.SetPassword(userAccount.PrincipalID, "UserAccount", userPassword);
                bool updateAcct = m_accountService.StoreUserAccount (userAccount);

                if (updatePass && updateAcct)
                {
                    SaveSystemUserPassword (usrType, usrName, userPassword);
                    MainConsole.Instance.InfoFormat (" The {0} user has been updated to '{1}'", usrType, usrName);
                }
                else
                    MainConsole.Instance.WarnFormat (" There was a problem updating the {0} user", usrType);
            }

        }

        // Save passwords for later
        void SaveSystemUserPassword(string userType, string userName, string password)
        {
            string passFile = Constants.DEFAULT_DATA_DIR + "/" + userType + ".txt";
            string userInfo = userType + " user";

            if (File.Exists (passFile))
                File.Delete (passFile);

            using (var pwFile = new StreamWriter(passFile))
            {
                pwFile.WriteLine (userInfo.PadRight(20) + " : '" + userName + "' was created: " + Culture.LocaleLogStamp ());
                pwFile.WriteLine ("Password             : " + password);
            }
        }

        #endregion

        #region Commands
        protected void HandleResetGovernorPassword(IScene scene, string[] cmd)
        {
            ResetSystemPassword ("Governor", GovernorName);
        }

        protected void HandleResetRealEstatePassword(IScene scene, string[] cmd)
        {
            ResetSystemPassword ("RealEstate", SystemEstateOwnerName);
        }

        protected void HandleResetBankerPassword(IScene scene, string[] cmd)
        {
            ResetSystemPassword ("Banker", BankerName);
        }

        protected void HandleResetMarketplacePassword(IScene scene, string[] cmd)
        {
            ResetSystemPassword ("Marketplace", MarketplaceOwnerName);
        }

        void ResetSystemPassword(string userType, string systemUserName)
        {
            string question;

            question = MainConsole.Instance.Prompt("Are you really sure that you want to reset the " + userType +  " user password ? (yes/no)", "no");

            if (question.StartsWith("y"))
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();
                var newPassword = Utilities.RandomPassword.Generate(2, 1, 0);

                UserAccount account = m_accountService.GetUserAccount(null, systemUserName);
                bool success = false;

                if (authService != null)
                    success = authService.SetPassword(account.PrincipalID, "UserAccount", newPassword);

                if (!success)
                    MainConsole.Instance.ErrorFormat ("[SYSTEM ACCOUNT SERVICE]: Unable to reset password for the " + userType);
                else
                {
                    SaveSystemUserPassword (userType, systemUserName, newPassword);
                    MainConsole.Instance.Info ("[SYSTEM ACCOUNT SERVICE]: The new password for '" + account.Name + "' is : " + newPassword);
                }
            }
        }


        #endregion
    }
}