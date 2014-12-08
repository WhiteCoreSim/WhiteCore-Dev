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


        public void PostInitialise(IScene scene, IConfigSource source, ISimulationBase simBase)
        {
        }

        public void FinishStartup(IScene scene, IConfigSource source, ISimulationBase simBase)
        {
            IEstateConnector EstateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();

            if (EstateConnector != null)
            {
                EstateSettings ES = EstateConnector.GetEstateSettings(scene.RegionInfo.RegionID);
                if (ES == null)
                {
                    //Could not locate the estate service, wait until it can find it
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
                    //Found the estate service, but found no estates for this region, make a new one
                    MainConsole.Instance.Warn("[EstateInitializer]: Your region '" + scene.RegionInfo.RegionName +
                        "' is not part of an estate.");

                    ES = CreateEstateInfo(scene);
                }
                scene.RegionInfo.EstateSettings = ES;
            }
        }

        public void PostFinishStartup(IScene scene, IConfigSource source, ISimulationBase simBase)
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
            }
        }

        public void Close(IScene scene)
        {
        }

        public void DeleteRegion(IScene scene)
        {
        }

        /// <summary>
        /// Links the region to the mainland estate.
        /// </summary>
        /// <returns>The mainland estate.</returns>
        /// <param name="regionID">Region I.</param>
        private EstateSettings LinkMainlandEstate(UUID regionID)
        {
            // link region to the Mainland... assign to RealEstateOwner & System Estate
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            EstateSettings ES;

            // link region to the 'Mainland'
            if (estateConnector.LinkRegion(regionID, Constants.SystemEstateID))
            {
                ES = estateConnector.GetEstateSettings (regionID);     // refresh to check linking
                if ( (ES == null) || (ES.EstateID == 0) )
                {
                    MainConsole.Instance.Warn("An error was encountered linking the region to the 'Mainland'!\nPossibly a problem with the server connection, please link this region later.");
                    return null;
                }
                MainConsole.Instance.Warn("Successfully joined the 'Mainland'!");
                return ES;
            }

            MainConsole.Instance.Warn("Joining the 'Mainland' failed. Please link this region later.");
            return null;
        }

        /// <summary>
        /// Creates the estate info for a region.
        /// </summary>
        /// <returns>The estate info.</returns>
        /// <param name="scene">Scene.</param>
        private EstateSettings CreateEstateInfo(IScene scene)
        {

            // check for regionType to determine if this is 'Mainland' or an 'Estate'
            string regType = scene.RegionInfo.RegionType.ToLower ();
            if (regType.StartsWith ("m"))
            {
                return LinkMainlandEstate (scene.RegionInfo.RegionID);
            }

            // we are linking to a user estate
            IEstateConnector estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            ISystemEstateService sysEstateInfo = m_registry.RequestModuleInterface<ISystemEstateService>();
             
            string sysEstateOwnerName;
            var sysAccount = scene.UserAccountService.GetUserAccount (scene.RegionInfo.AllScopeIDs, (UUID) Constants.RealEstateOwnerUUID);

            if (sysAccount == null)
                sysEstateOwnerName = sysEstateInfo.SystemEstateOwnerName;
            else
                sysEstateOwnerName = sysAccount.Name;


            // This is an 'Estate' so get some details....
            LastEstateOwner = sysEstateOwnerName;
            while (true)
            {
                UserAccount account;
                string estateOwner;
 
                estateOwner = MainConsole.Instance.Prompt("Estate owner name (" + sysEstateOwnerName +"/User Name)", LastEstateOwner);
 
                // we have a prospective estate owner...
                List<EstateSettings> ownerEstates = null;
                account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.AllScopeIDs, estateOwner);
                if (account != null)
                {
                    // we have a user account...
                    LastEstateOwner = account.Name;

                    ownerEstates = estateConnector.GetEstates (account.PrincipalID);
                }

                if (account == null || ownerEstates == null || ownerEstates.Count == 0)
                {
                    if (account == null)
                        MainConsole.Instance.Warn ("[Estate]: Unable to locate the user " + estateOwner);
                    else
                        MainConsole.Instance.WarnFormat("[Estate]: The user, {0}, has no estates currently.", account.Name);

                    string joinMainland = MainConsole.Instance.Prompt(
                        "Do you want to 'park' the region with the system owner/estate? (yes/no)", "yes");
                    if (joinMainland.ToLower().StartsWith("y"))                      // joining 'mainland'
                        return LinkMainlandEstate (scene.RegionInfo.RegionID);

                    continue;
                }

                if ( ownerEstates.Count > 1)
                {
                    MainConsole.Instance.InfoFormat("[Estate]: User {0} has {1} estates currently. {2}",
                        account.Name, ownerEstates.Count, "These estates are the following:");
                    foreach (EstateSettings t in ownerEstates)
                        MainConsole.Instance.CleanInfo("         " + t.EstateName);

                    LastEstateName = ownerEstates[0].EstateName;

                    List<string> responses = ownerEstates.Select(settings => settings.EstateName).ToList();
                    responses.Add("Cancel");

                    do
                    {
                        //TODO: This could be a problem if we have a lot of estates
                        string response = MainConsole.Instance.Prompt("Estate name to join", LastEstateName, responses);    
                        if (response == "None" || response == "Cancel")
                        {
                            LastEstateName = "";
                            break;
                        }
                        LastEstateName = response;
                    } while (LastEstateName == "");
                    if (LastEstateName == "")
                        continue;

                } else 
                    LastEstateName = ownerEstates[0].EstateName;
            
             
                // we should have a user account and estate name by now
                int estateID = estateConnector.GetEstate(account.PrincipalID, LastEstateName);
                if (estateID == 0)
                {
                    MainConsole.Instance.Warn("[Estate]: The name you have entered matches no known estate. Please try again");
                    continue;
                }

                // link up the region
                EstateSettings ES;
                if (estateConnector.LinkRegion(scene.RegionInfo.RegionID, estateID))
                {
                    if ((ES = estateConnector.GetEstateSettings(scene.RegionInfo.RegionID)) == null ||
                         ES.EstateID == 0)
                        //We could do by EstateID now, but we need to completely make sure that it fully is set up
                    {
                        MainConsole.Instance.Warn("[Estate]: The connection to the server was broken, please try again.");
                        continue;
                    }
                } else
                {
                    MainConsole.Instance.WarnFormat("[Estate]: Joining the {0} estate failed. Please try again.", LastEstateName);
                    continue;
                }

                MainConsole.Instance.InfoFormat("[Estate]: Successfully joined the {0} estate!", LastEstateName);
                return ES;
            }
        }

        /// <summary>
        /// Changes the region estate.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        protected void ChangeEstate(IScene scene, string[] cmd)
        {
            IEstateConnector EstateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector>();
            if (EstateConnector != null)
            {
                // a bit of info re 'Mainland'
                string regType = scene.RegionInfo.RegionType.ToLower ();
                if (regType.StartsWith ("m") && (scene.RegionInfo.EstateSettings.EstateID == Constants.SystemEstateID) )
                {
                    MainConsole.Instance.Info("[Estate]: This region is already part of the Mainland system estate");
                    return;
                }

                string removeFromEstate =
                    MainConsole.Instance.Prompt(
                        "Are you sure you want to change the estate for region '" + scene.RegionInfo.RegionName + "'? (yes/no)",
                        "yes");
 
                if (removeFromEstate == "yes")
                {
                    if (regType.StartsWith ("m"))
                        MainConsole.Instance.Info("[Estate]: Mainland type regions can only be part of the Mainland system estate");

                    if (!EstateConnector.DelinkRegion(scene.RegionInfo.RegionID))
                    {
                        MainConsole.Instance.Warn("[Estate]: Unable to remove this region from the estate.");
                        return;
                    }
                    scene.RegionInfo.EstateSettings = CreateEstateInfo(scene);
                }
                else
                    MainConsole.Instance.Warn("[Estate]: No action has been taken.");
            }
        }

 
        public bool IsArchiving
        {
            get { return false; }
        }

        /// <summary>
        /// Saves the estate settings to an archive.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="scene">Scene.</param>
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

        /// <summary>
        /// Loads the estate settings from an archive.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="filePath">File path.</param>
        /// <param name="type">Type.</param>
        /// <param name="scene">Scene.</param>
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