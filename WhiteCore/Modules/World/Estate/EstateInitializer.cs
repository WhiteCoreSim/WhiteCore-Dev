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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse.StructuredData;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using System;

namespace WhiteCore.Modules.Estate
{
    public class EstateInitializer : ISharedRegionStartupModule, IWhiteCoreBackupModule
    {
        private string LastEstateName = "";
        private string LastEstateOwner = Constants.RealEstateOwnerName;
        protected IRegistryCore m_registry;
         

        public void Initialise(IScene scene, IConfigSource source, ISimulationBase simBase)
        {
            scene.StackModuleInterface<IWhiteCoreBackupModule>(this);
            m_registry = simBase.ApplicationRegistry;
        }

        private EstateSettings CreateEstateInfo(IScene scene)
        {
            EstateSettings ES = new EstateSettings();
            while (true)
            {
                IEstateConnector EstateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();

                // check for regionType to determine if this is 'Mainland' or an 'Estate'
                string regType = scene.RegionInfo.RegionType.ToLower ();
                if (regType.StartsWith ("m"))
                {
                    // region is Mainland... assign to RealEstateOwner & System Estate
                    ES.EstateOwner = (UUID) Constants.RealEstateOwnerUUID;
                    ES.EstateName = Constants.SystemEstateName;
                    ES.EstateID = (uint) EstateConnector.GetEstate(ES.EstateOwner, ES.EstateName);

                    // link region to the 'Mainland'
                    if (EstateConnector.LinkRegion(scene.RegionInfo.RegionID, (int) ES.EstateID))
                    {
                        if ((ES = EstateConnector.GetEstateSettings(scene.RegionInfo.RegionID)) == null ||
                            ES.EstateID == 0)
                        {
                            MainConsole.Instance.Warn("Unable to link region to the 'Mainland'!\nPossibly a problem with the server connection, please link this region later.");
                            break;
                        }
                        MainConsole.Instance.Warn("Successfully joined the 'Mainland'!");
                        break;
                    }

                    MainConsole.Instance.Warn("Joining the 'Mainland' failed. Please link this region later.");
                    break;

                }

                // This is and 'Estate' so get some details....
                string name = MainConsole.Instance.Prompt("Estate owner name", LastEstateOwner);
                UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.AllScopeIDs, name);

                if (account == null)
                {
                    string createNewUser =
                        MainConsole.Instance.Prompt(
                            "Could not find user " + name + ". Would you like to create this user?", "yes");

                    if (createNewUser == "yes")
                    {
                        // Create a new account
                        string password = MainConsole.Instance.PasswordPrompt(name + "'s password");
                        string email = MainConsole.Instance.Prompt(name + "'s email", "");

                        //TODO: This breaks if we are running in Grid mode as the local connector is not able to create a user.
                        scene.UserAccountService.CreateUser(name, Util.Md5Hash(password), email);
                        account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.AllScopeIDs, name);

                        if (account == null)
                        {
                            MainConsole.Instance.ErrorFormat(
                                "[EstateService]: Unable to store account. If this simulator is connected to a grid, you must create the estate owner account first at the grid level.");
                            continue;
                        }
                    }
                    else
                        continue;
                }

                LastEstateOwner = account.Name;

                List<EstateSettings> ownerEstates = EstateConnector.GetEstates(account.PrincipalID);
                string response = (ownerEstates != null && ownerEstates.Count > 0) ? "yes" : "no";
                if (ownerEstates != null && ownerEstates.Count > 0)
                {
                    MainConsole.Instance.WarnFormat("Found user. {0} has {1} estates currently. {2}", account.Name,
                                                    ownerEstates.Count,
                                                    "These estates are the following:");
                    foreach (EstateSettings t in ownerEstates)
                    {
                        MainConsole.Instance.Warn(t.EstateName);
                    }
                    response =
                        MainConsole.Instance.Prompt(
                            "Do you wish to join one of these existing estates? (yes/no/cancel)",
                            response);
                }
                else
                {
                    MainConsole.Instance.WarnFormat("Found user. {0} has no estates currently. Creating a new estate.",
                                                    account.Name);
                }
                if (response == "no")
                {
                    // Create a new estate
                    // ES could be null 
                    ES.EstateName = MainConsole.Instance.Prompt("New estate name (or cancel to go back)", "My Estate");
                    if (ES.EstateName == "cancel")
                        continue;
                    //Set to auto connect to this region next
                    LastEstateName = ES.EstateName;
                    ES.EstateOwner = account.PrincipalID;

                    ES.EstateID = (uint) EstateConnector.CreateNewEstate(ES, scene.RegionInfo.RegionID);
                    if (ES.EstateID == 0)
                    {
                        MainConsole.Instance.Warn("There was an error in creating this estate: " + ES.EstateName);
                            //EstateName holds the error. See LocalEstateConnector for more info.
                        continue;
                    }
                    break;
                }
                if (response == "yes")
                {
                    if (ownerEstates != null && ownerEstates.Count != 1)
                    {
                        if (LastEstateName == "")
                            LastEstateName = ownerEstates[0].EstateName;

                        List<string> responses = ownerEstates.Select(settings => settings.EstateName).ToList();

                        responses.Add("None");
                        responses.Add("Cancel");
                        response = MainConsole.Instance.Prompt("Estate name to join", LastEstateName, responses);
                        if (response == "None" || response == "Cancel")
                            continue;
                        LastEstateName = response;
                    }
                    else if (ownerEstates != null) LastEstateName = ownerEstates[0].EstateName;

                    int estateID = EstateConnector.GetEstate(account.PrincipalID, LastEstateName);
                    if (estateID == 0)
                    {
                        MainConsole.Instance.Warn("The name you have entered matches no known estate. Please try again");
                        continue;
                    }

                    //We save the Password because we have to reset it after we tell the EstateService about it, as it clears it for security reasons
                    if (EstateConnector.LinkRegion(scene.RegionInfo.RegionID, estateID))
                    {
                        if ((ES = EstateConnector.GetEstateSettings(scene.RegionInfo.RegionID)) == null ||
                            ES.EstateID == 0)
                            //We could do by EstateID now, but we need to completely make sure that it fully is set up
                        {
                            MainConsole.Instance.Warn("The connection to the server was broken, please try again soon.");
                            continue;
                        }
                        MainConsole.Instance.Warn("Successfully joined the estate!");
                        break;
                    }

                    MainConsole.Instance.Warn("Joining the estate failed. Please try again.");
                    continue;
                }
            }
            return ES;
        }

        public void PostInitialise(IScene scene, IConfigSource source, ISimulationBase openSimBase)
        {
        }

        public void FinishStartup(IScene scene, IConfigSource source, ISimulationBase openSimBase)
        {
            IEstateConnector EstateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            CheckSystemEstateInfo (EstateConnector);

            if (EstateConnector != null)
            {
                EstateSettings ES = EstateConnector.GetEstateSettings(scene.RegionInfo.RegionID);
                if (ES == null)
                {
                    //It could not find the estate service, wait until it can find it
                    MainConsole.Instance.Warn(
                        "We could not find the estate service for this region. Please make sure that your URLs are correct in grid mode.");
                    while (true)
                    {
                        MainConsole.Instance.Prompt("Press enter to try again.");
                        if ((ES = EstateConnector.GetEstateSettings(scene.RegionInfo.RegionID)) == null ||
                            ES.EstateID == 0)
                        {
                            ES = CreateEstateInfo(scene);
                            break;
                        }
                        if (ES != null)
                            break;
                    }
                }
                else if (ES.EstateID == 0)
                {
                    //It found the estate service, but found no estates for this region, make a new one
                    MainConsole.Instance.Warn("[EstateInitializer]: Your region '" + scene.RegionInfo.RegionName +
                        "' is not part of an estate.");

                    ES = CreateEstateInfo(scene);
                }
                scene.RegionInfo.EstateSettings = ES;
            }
        }

        public void PostFinishStartup(IScene scene, IConfigSource source, ISimulationBase openSimBase)
        {
        }

        public void StartupComplete()
        {
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand (
                    "change estate",
                    "change estate",
                    "change info about the estate for the given region",
                    ChangeEstate, true, false);

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

            }
        }

        public void Close(IScene scene)
        {
        }

        public void DeleteRegion(IScene scene)
        {
        }

        /// <summary>
        /// Checks for a valid system estate. Adds or corrects if required
        /// </summary>
        /// <param name="estateConnector">Estate connector.</param>
        private void CheckSystemEstateInfo(IEstateConnector estateConnector)
        {
            // these should have already been checked but just make sure...
            if (estateConnector == null)
                return;

            if (estateConnector.RemoteCalls ())
                return;

            EstateSettings ES;
            ES = estateConnector.GetEstateSettings (Constants.SystemEstateName);
            if (ES != null)
            {   
                if (ES.EstateID != Constants.SystemEstateID)
                    UpdateSystemEstates (estateConnector, ES);
                
                return;
            }

            // Create a new estate
            ES = new EstateSettings();
            ES.EstateName = Constants.SystemEstateName;
            ES.EstateOwner = (UUID) Constants.RealEstateOwnerUUID;

            ES.EstateID = (uint) estateConnector.CreateNewEstate(ES);
            if (ES.EstateID == 0)
            {
                MainConsole.Instance.Warn ("There was an error in creating the system estate: " + ES.EstateName);
                //EstateName holds the error. See LocalEstateConnector for more info.

            } else 
            {
                MainConsole.Instance.InfoFormat("[EstateService]: The estate '{0}' owned by '{1}' has been created.", 
                    Constants.SystemEstateName, Constants.RealEstateOwnerName);
            }
        }

        /// <summary>
        /// Correct the system estate ID and update any linked regions.
        /// </summary>
        /// <param name="ES">EstateSettings</param>
        private void  UpdateSystemEstates(IEstateConnector estateConnector, EstateSettings ES)
        {
            uint oldEstateID = ES.EstateID;

            MainConsole.Instance.Info ("System estate present but the ID was corrected.");

            // get existing linked regions
            var regions = estateConnector.GetRegions ((int) oldEstateID);

            // recreate the correct estate
            estateConnector.DeleteEstate ((int) oldEstateID);
            int newEstateID = estateConnector.CreateNewEstate (ES);

            // re-link regions
            foreach ( UUID regID in regions)
            {
                estateConnector.LinkRegion(regID, newEstateID);
            }
            if (regions.Count > 0)
                MainConsole.Instance.InfoFormat("Relinked {0} regions",regions.Count);
        }


        protected void ChangeEstate(IScene scene, string[] cmd)
        {
            IEstateConnector EstateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            if (EstateConnector != null)
            {
                string removeFromEstate =
                    MainConsole.Instance.Prompt(
                        "Are you sure you want to change the estate for region '" + scene.RegionInfo.RegionName + "'? (yes/no)",
                        "yes");
                if (removeFromEstate == "yes")
                {
                    if (!EstateConnector.DelinkRegion(scene.RegionInfo.RegionID))
                    {
                        MainConsole.Instance.Warn("Unable to remove this region from the estate.");
                        return;
                    }
                    scene.RegionInfo.EstateSettings = CreateEstateInfo(scene);
                }
                else
                    MainConsole.Instance.Warn("No action has been taken.");
            }
        }


        protected void CreateEstateCommand(IScene scene, string[] cmd)
        {
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            IUserAccountService accountService = m_registry.RequestModuleInterface<IUserAccountService>();
 
            string estateName = "";
            string estateOwner = Constants.RealEstateOwnerName;

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
                string createUser = MainConsole.Instance.Prompt("Do you wish to create this user?  (yes/no)","yes").ToLower();
                if (!createUser.StartsWith("y"))
                   return;

                // Create a new account
                string password = MainConsole.Instance.PasswordPrompt(estateOwner + "'s password");
                string email = MainConsole.Instance.Prompt(estateOwner + "'s email", "");

                accountService.CreateUser(estateOwner, Util.Md5Hash(password), email);
                // CreateUser will tell us success or problem
                account = accountService.GetUserAccount(null, estateOwner);

                if (account == null)
                {
                    MainConsole.Instance.ErrorFormat(
                        "[EstateService]: Unable to store account details.\n   If this simulator is connected to a grid, create the estate owner account first at the grid level.");
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
            ES = estateConnector.GetEstateSettings (Constants.SystemEstateName);
            if (ES != null)
                if (estateConnector.LinkRegion (region.RegionID, (int) ES.EstateID))
                    MainConsole.Instance.Warn ("'" + regionName + "' has been placed in the '" +
                    Constants.SystemEstateName + "' estate until re-assigned");
                    
        }

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
            MainConsole.Instance.CleanInfo("\n\n");

        }

        public bool IsArchiving
        {
            get { return false; }
        }

        public void SaveModuleToArchive(TarArchiveWriter writer, IScene scene)
        {
            MainConsole.Instance.Debug("[Archive]: Writing estates to archive");

            EstateSettings settings = scene.RegionInfo.EstateSettings;
            if (settings == null)
                return;
            writer.WriteDir("estatesettings");
            writer.WriteFile("estatesettings/" + scene.RegionInfo.RegionName,
                             OSDParser.SerializeLLSDBinary(settings.ToOSD()));

            MainConsole.Instance.Debug("[Archive]: Finished writing estates to archive");
            MainConsole.Instance.Debug("[Archive]: Writing region info to archive");

            writer.WriteDir("regioninfo");
            RegionInfo regionInfo = scene.RegionInfo;

            writer.WriteFile("regioninfo/" + scene.RegionInfo.RegionName,
                             OSDParser.SerializeLLSDBinary(regionInfo.PackRegionInfoData()));

            MainConsole.Instance.Debug("[Archive]: Finished writing region info to archive");
        }

        public void LoadModuleFromArchive(byte[] data, string filePath, TarArchiveReader.TarEntryType type, IScene scene)
        {
            if (filePath.StartsWith("estatesettings/"))
            {
                EstateSettings settings = new EstateSettings();
                settings.FromOSD((OSDMap) OSDParser.DeserializeLLSDBinary(data));
                scene.RegionInfo.EstateSettings = settings;
            }
            else if (filePath.StartsWith("regioninfo/"))
            {
                string m_merge =
                    MainConsole.Instance.Prompt(
                        "Should we load the region information from the archive (region name, region position, etc)?",
                        "false");
                RegionInfo settings = new RegionInfo();
                settings.UnpackRegionInfoData((OSDMap) OSDParser.DeserializeLLSDBinary(data));
                if (m_merge == "false")
                {
                    //Still load the region settings though
                    scene.RegionInfo.RegionSettings = settings.RegionSettings;
                    return;
                }
                settings.RegionSettings = scene.RegionInfo.RegionSettings;
                settings.EstateSettings = scene.RegionInfo.EstateSettings;
                scene.RegionInfo = settings;
            }
        }

        public void BeginLoadModuleFromArchive(IScene scene)
        {
        }

        public void EndLoadModuleFromArchive(IScene scene)
        {
        }
    }
}