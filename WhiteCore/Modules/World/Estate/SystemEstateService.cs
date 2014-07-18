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
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;
using Nini.Config;
using OpenMetaverse;
using System;
using System.IO;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;
using WhiteCore.Framework.DatabaseInterfaces;

namespace WhiteCore.Modules.Estate
{
    /// <summary>
    ///     Basically a provision to allow user configuration of the system Estate Owner 'name and Estate' details
    /// </summary>
    public class SystemEstateService : ISystemEstateService, IService
    {
        IUserAccountService m_accountService;

        string realEstateOwnerName = Constants.RealEstateOwnerName;
        string systemEstateName = Constants.SystemEstateName;

        private IRegistryCore m_registry;

        #region ISystemEstateService Members

        public UUID SystemEstateOwnerUUID
        {
            get { return (UUID) Constants.RealEstateOwnerUUID; }
        }

        public string SystemEstateOwnerName
        {
            get { return realEstateOwnerName; }
        }
            
        public string SystemEstateName
        {
            get { return systemEstateName; }
        }


        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {

            IConfig estConfig = config.Configs["EstateService"];
            if (estConfig != null)
            {
                realEstateOwnerName = estConfig.GetString("SystemEstateOwnerName", realEstateOwnerName);
                systemEstateName = estConfig.GetString("SystemEstateName", systemEstateName);
            }

            registry.RegisterModuleInterface<ISystemEstateService>(this);
            m_registry = registry;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_accountService = registry.RequestModuleInterface<IUserAccountService>();
        }

        public void FinishedStartup()
        {
            // these are only valid if we are local
            if (!m_accountService.RemoteCalls())
            {
                // check and/or create default RealEstate user
                CheckRealEstateUserInfo ();
                AddCommands ();
            }
        }

        #endregion

        private void AddCommands()
        {
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "reset realestate password",
                    "reset realestate password",
                    "Resets the password of the system Estate Owner in case you lost it",
                    HandleResetRealEstatePassword, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "reset system estate",
                    "reset system estate",
                    "Resets the system estate owner and name to those configured",
                    HandleResetSystemEstate, false, true);
            }
        }


        /// <summary>
        /// Checks and creates the real estate user.
        /// </summary>
        private void CheckRealEstateUserInfo()
        {
            if (m_accountService == null)
                return;

            UserAccount uinfo = m_accountService.GetUserAccount (null, UUID.Parse (Constants.RealEstateOwnerUUID));

            if (uinfo == null)
            {
                MainConsole.Instance.Warn ("Creating System User '" + SystemEstateOwnerName + "'");
                var newPassword = Utilities.RandomPassword.Generate (2, 1, 0);

                var error = m_accountService.CreateUser (
                    (UUID)Constants.RealEstateOwnerUUID,    // UUID
                    UUID.Zero,                              // ScopeID
                    SystemEstateOwnerName,                  // Name
                    Util.Md5Hash (newPassword),             // password
                    "");                                    // email

                if (error == "")
                {
                    SaveRealEstatePassword (newPassword);
                    MainConsole.Instance.Info (" The password for '" + SystemEstateOwnerName + "' is : " + newPassword);

                } else
                {
                    MainConsole.Instance.Warn (" Unable to create user : " + error);
                    return;
                }

                //set as "Maintenace" level
                var account = m_accountService.GetUserAccount (null, UUID.Parse (Constants.RealEstateOwnerUUID));
                account.UserLevel = 250;
                account.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                bool success = m_accountService.StoreUserAccount (account);

                if (success)
                    MainConsole.Instance.Info (" The RealEstate user has been elevated to 'Maintenance' level");

            }
        }

        private void SaveRealEstatePassword(string password)
        {
            var configDir = Constants.DEFAULT_DATA_DIR;
            using (StreamWriter pwFile = new StreamWriter(configDir + "/SystemEstate.txt"))
            {
                pwFile.WriteLine("System user : '" + SystemEstateOwnerName + "' was created: " + Culture.LocaleLogStamp());
                pwFile.WriteLine("Password    : " + password);
            }
        }

        protected void HandleResetRealEstatePassword(IScene scene, string[] cmd)
        {
            string question;

            question = MainConsole.Instance.Prompt("Are you really sure that you want to reset the RealEstate User password ? (yes/no)");

            if (question.StartsWith("y"))
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();

                var newPassword = Utilities.RandomPassword.Generate(2, 1, 0);

                UserAccount account = m_accountService.GetUserAccount(null, SystemEstateOwnerName);
                bool success = false;

                if (authService != null)
                    success = authService.SetPassword(account.PrincipalID, "UserAccount", newPassword);

                if (!success)
                    MainConsole.Instance.ErrorFormat ("[USER ACCOUNT SERVICE]: Unable to reset password for RealEstate Owner");
                else
                {
                    SaveRealEstatePassword (newPassword);
                    MainConsole.Instance.Info ("[USER ACCOUNT SERVICE]: The new password for '" + SystemEstateOwnerName + "' is : " + newPassword);
                }
            }
        }

        protected void HandleResetSystemEstate(IScene scene, string[] cmd)
        {
            // delete and recreate the system estate
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();

            bool update = false;
    
            // verify that the estate does exist
            EstateSettings ES;
            ES = estateConnector.GetEstateSettings (Constants.SystemEstateName);
            if (ES == null)
            {
                ES = estateConnector.GetEstateSettings (SystemEstateName);
                if (ES == null)
                {
                    MainConsole.Instance.ErrorFormat ("[EstateService]: The estate '{0}' does not exist yet!", SystemEstateName);
                    MainConsole.Instance.Warn ("[EstateService]: It will be created when you link a region to the estate");
                }
            }

            // A system Estate exists?
            if (ES != null)
            {
                if (ES.EstateName != SystemEstateName)
                {
                    ES.EstateName = SystemEstateName;
                    update = true;
                }
 
                if (ES.EstateOwner != SystemEstateOwnerUUID)
                {
                    ES.EstateOwner = SystemEstateOwnerUUID;
                    update = true;
                }
            
                // save any updates
                if (update)
                {
                    estateConnector.SaveEstateSettings (ES);
                    MainConsole.Instance.Warn ("[EstateService]: Estate details have been updated");
                }
            }

            // check the System estate owner details
            UserAccount uinfo;
            uinfo = m_accountService.GetUserAccount (null, UUID.Parse (Constants.RealEstateOwnerUUID));
            if (uinfo == null)
            {
                MainConsole.Instance.Warn ("[EstateService]: The system estate user does not exist yet!");
                MainConsole.Instance.Warn ("[EstateService]: This account will be created automatically");
            }

            if ((uinfo != null) && (uinfo.Name != SystemEstateOwnerName))
            {
                //string[] name = uinfo.Name.Split (' ');
                //uinfo.FirstName = name [0];
                //uinfo.LastName = name [1];
                uinfo.Name = SystemEstateOwnerName;
                m_accountService.StoreUserAccount (uinfo);
                update = true;
            }

            if(update)
                MainConsole.Instance.InfoFormat("[EstateService]: The system Estate details have been reset");
            else
                MainConsole.Instance.InfoFormat("[EstateService]: Estate details are correct as configured");


        }
    }
}