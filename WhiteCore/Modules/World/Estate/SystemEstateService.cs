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
        IEstateConnector m_estateConnector;

        string governorName = Constants.GovernorName;
        string realEstateOwnerName = Constants.RealEstateOwnerName;
        string systemEstateName = Constants.SystemEstateName;

        private IRegistryCore m_registry;

        #region ISystemEstateService Members

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
                governorName = estConfig.GetString("GovernorName", governorName);
                realEstateOwnerName = estConfig.GetString("SystemEstateOwnerName", realEstateOwnerName);
                systemEstateName = estConfig.GetString("SystemEstateName", systemEstateName);
            }

            registry.RegisterModuleInterface<ISystemEstateService>(this);
            m_registry = registry;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            m_accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            m_estateConnector  = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();

            // these are only valid if we are local
            if (!m_accountService.RemoteCalls())
            {
                // check and/or create default RealEstate user
                CheckSystemUserInfo ();
                CheckSystemEstateInfo ();

                AddCommands ();
            }

        }

        #endregion

        private void AddCommands()
        {
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "reset governor password",
                    "reset governor password",
                    "Resets the password of the system Governor in case you lost it",
                    HandleResetGovernorPassword, false, true);

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

                MainConsole.Instance.Commands.AddCommand (
                    "create estate",
                    "create estate [name [owner (<firstname> <lastname>)]]",
                    "Creates a new estate with the specified name, owned by the specified user."
                    + "\n    The Estate name must be unique.",
                    CreateEstateCommand, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set estate owner",
                    "set estate owner [<estate-name> [owner (<Firstname> <Lastname>) ]]",
                    "Sets the owner of the specified estate to the specified user. ",
                    SetEstateOwnerCommand, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "set estate name",
                    "set estate name [estate-name [new-name]]",
                    "Sets the name of the specified estate to the specified value. New name must be unique.",
                    SetEstateNameCommand, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "estate link region",
                    "estate link region [estate-name [region-name]]",
                    "Attaches the specified region to the specified estate.",
                    EstateLinkRegionCommand, false, true);

                MainConsole.Instance.Commands.AddCommand (
                    "estate unlink region",
                    "estate unlink region [estate-name [region-name]]",
                    "Removes the specified region from the specified estate.",
                    EstateUnLinkRegionCommand, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "show estates",
                    "show estates",
                    "Show information about all estates in this instance",
                    ShowEstatesCommand, false, true);

                MainConsole.Instance.Commands.AddCommand(
                    "show estate regions",
                    "show estate regions",
                    "Show information about all regions belonging to an estate",
                    ShowEstateRegionsCommand, false, true);

            }
        }

        #region systemEstate
        /// <summary>
        /// Checks and creates the real estate user.
        /// </summary>
        private void CheckSystemUserInfo()
        {
            if (m_accountService == null)
                return;

            CheckGovernorUserInfo ();
            CheckRealEstateUserInfo ();

        }

        private void CheckGovernorUserInfo()
        {

            UserAccount govInfo = m_accountService.GetUserAccount (null, UUID.Parse (Constants.GovernorUUID));
            var govPassword = Utilities.RandomPassword.Generate (2, 1, 0);

            if (govInfo == null)
            {
                MainConsole.Instance.Warn ("Creating the Governor user '" + GovernorName + "'");

                var error = m_accountService.CreateUser (
                    (UUID)Constants.GovernorUUID,           // UUID
                    UUID.Zero,                              // ScopeID
                    GovernorName,                           // Name
                    Util.Md5Hash (govPassword),             // password
                    "");                                    // email

                if (error == "")
                {
                    SaveGovernorPassword (govPassword);
                    MainConsole.Instance.Info (" The password for '" + GovernorName + "' is : " + govPassword);

                } else
                {
                    MainConsole.Instance.Warn (" Unable to create the Governor user : " + error);
                    return;
                }

                //set as "Maintenace" level
                var account = m_accountService.GetUserAccount (null, UUID.Parse (Constants.GovernorUUID));
                account.UserLevel = 250;
                account.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                bool success = m_accountService.StoreUserAccount (account);

                if (success)
                    MainConsole.Instance.Info (" The Governor user has been elevated to 'Maintenance' level");

                return;

            }

            // we already have the Governor account.. verify details in case of a configuration change
            if (govInfo.Name != GovernorName)
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();

                govInfo.Name = GovernorName;
                bool updatePass = authService.SetPassword(govInfo.PrincipalID, "UserAccount", govPassword);
                bool updateAcct = m_accountService.StoreUserAccount (govInfo);

                if (updatePass && updateAcct)
                {
                    SaveGovernorPassword (govPassword);
                    MainConsole.Instance.InfoFormat (" The Governor user has been updated to '{0}'", GovernorName);
                }
                else
                    MainConsole.Instance.Warn (" There was a problem updating the Governor user");
            }

        }

        private void CheckRealEstateUserInfo()
        {

            UserAccount reInfo = m_accountService.GetUserAccount (null, UUID.Parse (Constants.RealEstateOwnerUUID));
            var rePassword = Utilities.RandomPassword.Generate (2, 1, 0);

            if (reInfo == null)
            {
                MainConsole.Instance.Warn ("Creating system real estate user '" + SystemEstateOwnerName + "'");

                var error = m_accountService.CreateUser (
                    (UUID)Constants.RealEstateOwnerUUID,    // UUID
                    UUID.Zero,                              // ScopeID
                    SystemEstateOwnerName,                  // Name
                    Util.Md5Hash (rePassword),              // password
                    "");                                    // email

                if (error == "")
                {
                    SaveRealEstatePassword (rePassword);
                    MainConsole.Instance.Info (" The password for '" + SystemEstateOwnerName + "' is : " + rePassword);

                } else
                {
                    MainConsole.Instance.Warn (" Unable to create the RealEstate user : " + error);
                    return;
                }

                //set as "Maintenace" level
                var account = m_accountService.GetUserAccount (null, UUID.Parse (Constants.RealEstateOwnerUUID));
                account.UserLevel = 150;
                account.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                bool success = m_accountService.StoreUserAccount (account);

                if (success)
                    MainConsole.Instance.Info (" The system real estate user has been elevated to 'Liason' level");

                return;

            }

            // we alreay have an account.. verify details in case of a configuration change
            if (reInfo.Name != SystemEstateOwnerName)
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();

                reInfo.Name = SystemEstateOwnerName;
                bool updatePass = authService.SetPassword(reInfo.PrincipalID, "UserAccount", rePassword);
                bool updateAcct = m_accountService.StoreUserAccount (reInfo);

                if (updatePass && updateAcct)
                {
                    SaveRealEstatePassword (rePassword);
                    MainConsole.Instance.InfoFormat (" The system real estate user has been updated to '{0}'", SystemEstateOwnerName);
                }
                else
                    MainConsole.Instance.Warn (" There was a problem updating the system real estate user");
            }

        }

        private void SaveGovernorPassword(string password)
        {
            const string passFile = Constants.DEFAULT_DATA_DIR + "/Governor.txt";

            if (File.Exists (passFile))
                File.Delete (passFile);

            using (var pwFile = new StreamWriter(passFile))
            {
                 pwFile.WriteLine ("Governor user   : '" + GovernorName + "' was created: " + Culture.LocaleLogStamp ());
                pwFile.WriteLine ("Password        : " + password);
            }
        }

        private void SaveRealEstatePassword(string password)
        {
            const string passFile = Constants.DEFAULT_DATA_DIR + "/SystemEstate.txt";

            if (File.Exists (passFile))
                File.Delete (passFile);

            using (var pwFile = new StreamWriter(passFile))
            {
                pwFile.WriteLine ("RealEstate user : '" + SystemEstateOwnerName + "' was created: " + Culture.LocaleLogStamp ());
                pwFile.WriteLine ("Password        : " + password);
            }
        }

        /// <summary>
        /// Checks for a valid system estate. Adds or corrects if required
        /// </summary>
        /// <param name="estateConnector">Estate connector.</param>
        private void CheckSystemEstateInfo()
        {
            // these should have already been checked but just make sure...
            if (m_estateConnector == null)
                return;

            if (m_estateConnector.RemoteCalls ())
                return;

            EstateSettings ES;
            //            ES = estateConnector.GetEstateSettings (Constants.SystemEstateName);
            //ES = m_estateConnector.GetEstateSettings (SystemEstateName);
            ES = m_estateConnector.GetEstateSettings (Constants.SystemEstateID);
            if (ES != null)
            {   
                // ensure correct ID
                if (ES.EstateID != Constants.SystemEstateID)
                    UpdateSystemEstates (m_estateConnector, ES);

                // in case of configuration changes
                if (ES.EstateName != SystemEstateName)
                {
                    ES.EstateName = SystemEstateName;
                    m_estateConnector.SaveEstateSettings (ES);
                    MainConsole.Instance.Info ("[EstateService]: The system Estate name has been updated to " + SystemEstateName);
                }

                return;
            }

            // Create a new estate
            ES = new EstateSettings();
            ES.EstateName = SystemEstateName;
            ES.EstateOwner = (UUID) Constants.RealEstateOwnerUUID;

            ES.EstateID = (uint) m_estateConnector.CreateNewEstate(ES);
            if (ES.EstateID == 0)
            {
                MainConsole.Instance.Warn ("There was an error in creating the system estate: " + ES.EstateName);
                //EstateName holds the error. See LocalEstateConnector for more info.

            } else 
            {
                MainConsole.Instance.InfoFormat("[EstateService]: The estate '{0}' owned by '{1}' has been created.", 
                    SystemEstateName, SystemEstateOwnerName);
            }
        }

        /// <summary>
        /// Correct the system estate ID and update any linked regions.
        /// </summary>
        /// <param name="ES">EstateSettings</param>
        private void  UpdateSystemEstates(IEstateConnector estateConnector, EstateSettings ES)
        {
            // this may be an ID correction or just an estate name change
            uint oldEstateID = ES.EstateID;
            int newEstateID = Constants.SystemEstateID;

            // get existing linked regions
            var regions = estateConnector.GetRegions ((int) oldEstateID);

            // recreate the correct estate?
            if (oldEstateID != newEstateID)
            {
                estateConnector.DeleteEstate ((int)oldEstateID);
                newEstateID = estateConnector.CreateNewEstate (ES);
                MainConsole.Instance.Info ("System estate present but the ID was corrected.");
            }

            // re-link regions
            foreach ( UUID regID in regions)
            {
                estateConnector.LinkRegion(regID, newEstateID);
            }
            if (regions.Count > 0)
                MainConsole.Instance.InfoFormat("Relinked {0} regions",regions.Count);
        }

        #endregion

        #region Commands
        protected void HandleResetGovernorPassword(IScene scene, string[] cmd)
        {
            string question;

            question = MainConsole.Instance.Prompt("Are you really sure that you want to reset the Governor User password ? (yes/no)");

            if (question.StartsWith("y"))
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService> ();
                var newPassword = Utilities.RandomPassword.Generate(2, 1, 0);

                UserAccount account = m_accountService.GetUserAccount(null, GovernorName);
                bool success = false;

                if (authService != null)
                    success = authService.SetPassword(account.PrincipalID, "UserAccount", newPassword);

                if (!success)
                    MainConsole.Instance.ErrorFormat ("[USER ACCOUNT SERVICE]: Unable to reset password for the Governor");
                else
                {
                    SaveGovernorPassword (newPassword);
                    MainConsole.Instance.Info ("[USER ACCOUNT SERVICE]: The new password for '" + GovernorName + "' is : " + newPassword);
                }
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

        protected void CreateEstateCommand(IScene scene, string[] cmd)
        {
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            //ISystemEstateService sysEstateInfo = m_registry.RequestModuleInterface<ISystemEstateService>();

            string estateName = "";
            string estateOwner = SystemEstateOwnerName;

            // check for passed estate name
            estateName = (cmd.Length < 3) 
                ? MainConsole.Instance.Prompt("Estate name: ") 
                : cmd[2];
            if (estateName == "")
                return;

            // verify that the estate does not already exist
            if (estateConnector.EstateExists(estateName))
            {
                MainConsole.Instance.ErrorFormat("EstateService]: The estate '{0}' already exists!",estateName);
                return;
            }

            // owner?
            estateOwner = (cmd.Length > 3) 
                ? Util.CombineParams(cmd, 4) // in case of spaces in the name eg Allan Allard
                : MainConsole.Instance.Prompt("Estate owner: ", estateOwner); 
            if (estateOwner == "")
                return;


            // check to make sure the user exists
            UserAccount account = accountService.GetUserAccount(null, estateOwner);
            if (account == null)
            {
                MainConsole.Instance.WarnFormat("[USER ACCOUNT SERVICE]: The user, '{0}' was not found!", estateOwner);

                // tempory fix until remote user creation can be corrected
                if (!accountService.RemoteCalls ())
                {
                    string createUser = MainConsole.Instance.Prompt ("Do you wish to create this user?  (yes/no)", "yes").ToLower ();
                    if (!createUser.StartsWith ("y"))
                        return;

                    // Create a new account
                    string password = MainConsole.Instance.PasswordPrompt (estateOwner + "'s password");
                    string email = MainConsole.Instance.Prompt (estateOwner + "'s email", "");

                    accountService.CreateUser (estateOwner, Util.Md5Hash (password), email);
                    // CreateUser will tell us success or problem
                    account = accountService.GetUserAccount (null, estateOwner);

                    if (account == null)
                    {
                        MainConsole.Instance.ErrorFormat (
                            "[EstateService]: Unable to store account details.\n   If this simulator is connected to a grid, create the estate owner account first at the grid level.");
                        return;
                    }
                } else
                {
                    MainConsole.Instance.WarnFormat("[USER ACCOUNT SERVICE]: The user must be created on the Grid before assigning an estate!");
                    MainConsole.Instance.WarnFormat("[USER ACCOUNT SERVICE]: Regions should be assigned to the system user estate until this can be corrected");

                    return;
                }
            }

            // we have an estate name and a user
            // Create a new estate
            EstateSettings ES = new EstateSettings();
            ES.EstateName = estateName;
            ES.EstateOwner = account.PrincipalID;

            ES.EstateID = (uint) estateConnector.CreateNewEstate(ES);
            if (ES.EstateID == 0)
            {
                MainConsole.Instance.Warn("There was an error in creating this estate: " + ES.EstateName);
                //EstateName holds the error. See LocalEstateConnector for more info.

            } else
                MainConsole.Instance.InfoFormat("[EstateService]: The estate '{0}' owned by '{1}' has been created.", estateName, estateOwner);
        }

        protected void SetEstateOwnerCommand(IScene scene, string[] cmd)
        {
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();

            string estateName = "";
            string estateOwner = "";
            UserAccount ownerAccount;

            // check for passed estate name
            estateName = (cmd.Length < 4) 
                ? MainConsole.Instance.Prompt("Estate name ") 
                : cmd[2];
            if (estateName == "")
                return;

            // verify that the estate does exist
            EstateSettings ES = estateConnector.GetEstateSettings (estateName);
            if (ES == null)
            {
                MainConsole.Instance.WarnFormat("[EstateService]: The estate '{0}' does not exist!",estateName);
                return;
            }

            // owner?
            if (cmd.Length < 4) 
            {
                UUID estateOwnerID = ES.EstateOwner;
                ownerAccount = accountService.GetUserAccount(null, estateOwnerID);

                estateOwner = MainConsole.Instance.Prompt ("New owner for this estate", ownerAccount.Name); 
            } else {
                estateOwner = Util.CombineParams(cmd, 5); // in case of spaces in the name eg Allan Allard
            }
            if (estateOwner == "")
                return;

            // check to make sure the user exists
            ownerAccount = accountService.GetUserAccount(null, estateOwner);
            if (ownerAccount == null)
            {
                MainConsole.Instance.WarnFormat ("[User Account Service]: The user, '{0}' was not found!", estateOwner);
                return;
            }

            // We have a valid Estate and user, send it off for processing.
            ES.EstateOwner = ownerAccount.PrincipalID;
            estateConnector.SaveEstateSettings(ES);

            MainConsole.Instance.InfoFormat("[EstateService]: Estate owner for '{0}' changed to '{1}'", estateName, estateOwner);
        }

        protected void SetEstateNameCommand(IScene scene, string[] cmd)
        {
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();

            string estateName = "";
            string estateNewName = "";

            // check for passed estate name
            estateName = (cmd.Length < 4) 
                ? MainConsole.Instance.Prompt("Estate name: ") 
                : cmd[3];
            if (estateName == "")
                return;

            // verify that the estate does exist
            EstateSettings ES = estateConnector.GetEstateSettings (estateName);
            if (ES == null)
            {
                MainConsole.Instance.ErrorFormat("[EstateService]: The estate '{0}' does not exist!",estateName);
                return;
            }

            // check for passed  estate new name
            estateNewName = (cmd.Length < 4) 
                ? MainConsole.Instance.Prompt("Estate new name: ") 
                : cmd[4];
            if (estateNewName == "")
                return;

            // We have a valid Estate and user, send it off for processing.
            ES.EstateName = estateNewName;
            estateConnector.SaveEstateSettings(ES);

            MainConsole.Instance.InfoFormat("[EstateService]: Estate '{0}' changed to '{1}'", estateName, estateNewName);
        }



        private void EstateLinkRegionCommand(IScene scene, string[] cmd)
        {
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            IGridService gridService = m_registry.RequestModuleInterface<IGridService>();

            string estateName = "";
            string regionName = "";

            // check for passed estate name
            estateName = (cmd.Length < 4) 
                ? MainConsole.Instance.Prompt("Estate name: ") 
                : cmd[3];
            if (estateName == "")
                return;

            // verify that the estate does exist
            EstateSettings ES = estateConnector.GetEstateSettings (estateName);
            if (ES == null)
            {
                MainConsole.Instance.ErrorFormat("[EstateService]: The estate '{0}' does not exist!",estateName);
                return;
            }

            // check for passed  region to link to
            if (scene != null)
                regionName = scene.RegionInfo.RegionName;

            regionName = (cmd.Length < 4) 
                ? MainConsole.Instance.Prompt("Link to region: ",regionName) 
                : cmd[4];
            if (regionName == "")
                return;

            // verify that the region does exist
            var region = gridService.GetRegionByName(null, regionName);
            if (region == null)
            {
                MainConsole.Instance.ErrorFormat("[EstateService]: The requestes region '{0}' does not exist!",regionName);
                return;
            }

            // have all details.. do it...
            if (estateConnector.LinkRegion (region.RegionID, (int) ES.EstateID))
            {
                // check for update..
                if (estateConnector.GetEstateSettings(region.RegionID) == null) 
                    MainConsole.Instance.Warn("The region link failed, please try again soon.");
                else
                    MainConsole.Instance.InfoFormat ("Region '{0}' is now attached to estate '{1}'", regionName, estateName);
            } else
                MainConsole.Instance.Warn("Joining the estate failed. Please try again.");

        }

        private void EstateUnLinkRegionCommand(IScene scene, string[] cmd)
        {
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            IGridService gridService = m_registry.RequestModuleInterface<IGridService>();

            string estateName = "";
            string regionName = "";

            // check for passed estate name
            estateName = (cmd.Length < 4) 
                ? MainConsole.Instance.Prompt("Estate name: ") 
                : cmd[3];
            if (estateName == "")
                return;

            // verify that the estate does exist
            EstateSettings ES = estateConnector.GetEstateSettings (estateName);
            if (ES == null)
            {
                MainConsole.Instance.ErrorFormat("[EstateService]: The estate '{0}' does not exist!",estateName);
                return;
            }

            // check for passed  region to link to
            if (scene != null)
                regionName = scene.RegionInfo.RegionName;

            regionName = (cmd.Length < 4) 
                ? MainConsole.Instance.Prompt("Remove region: ",regionName) 
                : cmd[4];
            if (regionName == "")
                return;

            // verify that the region does exist
            var region = gridService.GetRegionByName(null, regionName);
            if (region == null)
            {
                MainConsole.Instance.ErrorFormat("[EstateService]: The requested region '{0}' does not exist!",regionName);
                return;
            }

            // have all details.. do it...
            if (!estateConnector.DelinkRegion (region.RegionID))
            {
                MainConsole.Instance.Warn ("Unlinking the region failed. Please try again.");
                return;
            }

            // unlink was successful..
            //MainConsole.Instance.InfoFormat ("Region '{0}' has been removed from estate '{1}'", regionName, estateName);

            //We really need to attach it to another estate though... 
            ISystemEstateService sysEstateInfo = m_registry.RequestModuleInterface<ISystemEstateService>();
            ES = estateConnector.GetEstateSettings (sysEstateInfo.SystemEstateName);
            if (ES != null)
            if (estateConnector.LinkRegion (region.RegionID, (int) ES.EstateID))
                MainConsole.Instance.Warn ("'" + regionName + "' has been placed in the '" +
                    sysEstateInfo.SystemEstateName + "' estate until re-assigned");

        }

        /// <summary>
        /// Shows details of all estates.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        private void ShowEstatesCommand(IScene scene, string[] cmd)
        {
            // if (scene == null)
            //    return;
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();

            string estateInfo;
            var estates = estateConnector.GetEstates ();

            // headings
            estateInfo = String.Format ("{0, -20}", "Estate");
            estateInfo += String.Format ("{0, -20}", "Owner");
            estateInfo += String.Format ("{0, -10}", "Regions");
            estateInfo += String.Format ("{0, -10}", "Voice");
            estateInfo += String.Format ("{0, -10}", "Price/M");
            estateInfo += String.Format ("{0, -10}", "Public");
            estateInfo += String.Format ("{0, -10}", "Tax Free");
            estateInfo += String.Format ("{0, -10}", "Direct Tp");

            MainConsole.Instance.CleanInfo(estateInfo);
            MainConsole.Instance.CleanInfo ("--------------------------------------------------------------------------------------------------");

            foreach( string Estate in estates) 
            {
                var EstateID = (int) estateConnector.GetEstateID(Estate);
                EstateSettings ES = estateConnector.GetEstateSettings (EstateID);

                //var regInfo = scene.RegionInfo;
                UserAccount EstateOwner = accountService.GetUserAccount (null, ES.EstateOwner);
                var regions = estateConnector.GetRegions (EstateID);

                // todo ... change hardcoded field sizes to public constants
                estateInfo = String.Format ("{0, -20}", ES.EstateName);
                estateInfo += String.Format ("{0, -20}", EstateOwner.Name);
                estateInfo += String.Format ("{0, -10}", regions.Count);
                estateInfo += String.Format ("{0, -10}", (ES.AllowVoice)?"Yes":"No");
                estateInfo += String.Format ("{0, -10}", ES.PricePerMeter);
                estateInfo += String.Format ("{0, -10}", (ES.PublicAccess)?"Yes":"No");
                estateInfo += String.Format ("{0, -10}", (ES.TaxFree)?"Yes":"No");
                estateInfo += String.Format ("{0, -10}", (ES.AllowDirectTeleport)?"Yes":"No");

                MainConsole.Instance.CleanInfo(estateInfo);
            }
            MainConsole.Instance.CleanInfo("\n");

        }

        /// <summary>
        /// Shows estate regions.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        private void ShowEstateRegionsCommand(IScene scene, string[] cmd)
        {

            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();
            IGridService gridService = m_registry.RequestModuleInterface<IGridService> ();

            // check for passed estate name
            string estateName;
            if (cmd.Length < 4)
            {
                do
                {
                    estateName = MainConsole.Instance.Prompt ("Estate name (? for list)", "");
                    if (estateName == "?")
                    {
                        var estates = estateConnector.GetEstates ();
                        MainConsole.Instance.CleanInfo (" Available estates are : ");
                        foreach (string Estate in estates)
                            MainConsole.Instance.CleanInfo ("    " + Estate);
                    }
                } while (estateName == "?");

                if (estateName == "")
                    return;
            } else
                estateName = cmd [3];

            // verify that the estate does exist
            EstateSettings ES = estateConnector.GetEstateSettings (estateName);
            if (ES == null)
            {
                MainConsole.Instance.ErrorFormat("[EstateService]: The estate '{0}' does not exist!",estateName);
                return;
            }

            var estateregions = estateConnector.GetRegions ((int) ES.EstateID);

            int estRegions = 0;
            float estateArea = 0;
            int offLine = 0;

            string regionInfo;

            regionInfo =  String.Format ("{0, -20}", "Region");
            regionInfo += String.Format ("{0, -12}", "Location");
            regionInfo += String.Format ("{0, -14}", "Size");
            regionInfo += String.Format ("{0, -12}", "Area");
            regionInfo += String.Format ("{0, -26}", "Type");
            regionInfo += String.Format ("{0, -10}", "Online");

            MainConsole.Instance.CleanInfo(regionInfo);

            MainConsole.Instance.CleanInfo(
                "----------------------------------------------------------------------------------------------------");

            foreach (UUID regionID in estateregions)
            {
                var region = gridService.GetRegionByUUID (null, regionID);
                if (region == null)     // deleted??
                    continue;

                estRegions++;
                estateArea = estateArea + region.RegionArea;


                if (!region.IsOnline)
                    offLine++;

                // TODO ... change hardcoded field sizes to public constants
                regionInfo =  String.Format ("{0, -20}", region.RegionName);
                regionInfo += String.Format ("{0, -12}", region.RegionLocX / Constants.RegionSize + "," + region.RegionLocY / Constants.RegionSize);
                regionInfo += String.Format ("{0, -14}", region.RegionSizeX + "x" + region.RegionSizeY);
                regionInfo += String.Format ("{0, -12}", region.RegionArea < 1000000? region.RegionArea + " m2": (region.RegionArea/1000000) + " km2");
                regionInfo += String.Format ("{0, -26}", region.RegionType);
                regionInfo += String.Format ("{0, -10}", region.IsOnline?"yes":"no");

                MainConsole.Instance.CleanInfo(regionInfo);
            }
            MainConsole.Instance.CleanInfo ("");
            MainConsole.Instance.CleanInfo(
                "----------------------------------------------------------------------------------------------------");
            MainConsole.Instance.CleanInfo ("Regions : " + estRegions + " regions with an area of " + (estateArea / 1000000) + " km2");
            MainConsole.Instance.CleanInfo ("Offline : " + offLine);
            MainConsole.Instance.CleanInfo (string.Empty);
            MainConsole.Instance.CleanInfo(
                "----------------------------------------------------------------------------------------------------");
            MainConsole.Instance.CleanInfo ("\n");
        }
      
        #endregion
    }
}