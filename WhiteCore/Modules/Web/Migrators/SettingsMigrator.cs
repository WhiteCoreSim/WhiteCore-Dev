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

using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;

namespace WhiteCore.Modules.Web
{
    class SettingsMigrator
    {
        public static readonly string Schema = "WebSettings";
        public static uint CurrentVersion = 2;
        public static GridSettings _settingsGrid;
        public static WebUISettings _settingsWebUI;

        public static void InitializeWebUIDefaults (WebInterface webinterface)
        {
            _settingsWebUI = new WebUISettings ();

            _settingsWebUI.LastSettingsVersionUpdateIgnored = CurrentVersion;
            _settingsWebUI.LastPagesVersionUpdateIgnored = PagesMigrator.GetVersion ();
            _settingsWebUI.MapCenter = new Vector2 (1000, 1000);

            var configSrc = webinterface.Registry.RequestModuleInterface<ISimulationBase> ().ConfigSource;
            var loginConfig = configSrc.Configs ["LoginService"];
            if (loginConfig != null) {
                _settingsWebUI.WebRegistration = loginConfig.GetBoolean ("AllowAnonymousLogin", true);
            }
        }

        public static bool RequiresUpdate ()
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            OSDWrapper version = generics.GetGeneric<OSDWrapper> (UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger () < CurrentVersion;
        }

        public static uint GetVersion ()
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            OSDWrapper version = generics.GetGeneric<OSDWrapper> (UUID.Zero, Schema + "Version", "");
            return version == null ? 0 : (uint)version.Info.AsInteger ();
        }

        public static bool RequiresInitialUpdate ()
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            OSDWrapper version = generics.GetGeneric<OSDWrapper> (UUID.Zero, Schema + "Version", "");
            return version == null || version.Info.AsInteger () < 1;
        }

        public static void GetGridConfigSettings (WebInterface webinterface)
        {
            var configSrc = webinterface.Registry.RequestModuleInterface<ISimulationBase> ().ConfigSource;
            IConfig config;
            _settingsGrid = new GridSettings ();

            // login
            config = configSrc.Configs ["LoginService"];
            if (config != null) {
                _settingsGrid.WelcomeMessage = config.GetString ("WelcomeMessage", _settingsGrid.WelcomeMessage);
            }

            // gridinfo
            config = configSrc.Configs ["GridInfoService"];
            if (config != null) {
                _settingsGrid.Gridname = config.GetString ("gridname", _settingsGrid.Gridname);
                _settingsGrid.Gridnick = config.GetString ("gridnick", _settingsGrid.Gridnick);
            }

            // Library
            //            config =  configSrc.Configs ["LibraryService"];
            //            if (config != null)
            //            {
            //                _settingsGrid.LibraryName = config.GetString("LibraryName", _settingsGrid.LibraryName);
            //                _settingsGrid.LibraryOwnerName = config.GetString("LibraryOwnerName", _settingsGrid.LibraryOwnerName);
            //            }

            // System users
            config = configSrc.Configs ["SystemUserService"];
            if (config != null) {
                _settingsGrid.GovernorName = config.GetString ("GovernorName", _settingsGrid.GovernorName);
                _settingsGrid.RealEstateOwnerName = config.GetString ("RealEstateOwnerName", _settingsGrid.RealEstateOwnerName);
                _settingsGrid.BankerName = config.GetString ("BankerName", _settingsGrid.BankerName);
                _settingsGrid.MarketplaceOwnerName = config.GetString ("MarketplaceOwnerName", _settingsGrid.MarketplaceOwnerName);
            }

            // RealEstate
            config = configSrc.Configs ["EstateService"];
            if (config != null) {
                _settingsGrid.SystemEstateName = config.GetString ("MainlandEstateName", _settingsGrid.MainlandEstateName);
                _settingsGrid.SystemEstateName = config.GetString ("SystemEstateName", _settingsGrid.SystemEstateName);
            }


        }

        public static void ResetToDefaults (WebInterface webinterface)
        {
            InitializeWebUIDefaults (webinterface);
            GetGridConfigSettings (webinterface);

            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();

            //Remove all pages
            generics.RemoveGeneric (UUID.Zero, "WebUISettings");
            generics.RemoveGeneric (UUID.Zero, "GridSettings");
            generics.RemoveGeneric (UUID.Zero, "WebSettingsVersion");

            generics.AddGeneric (UUID.Zero, "WebUISettings", "Settings", _settingsWebUI.ToOSD ());
            generics.AddGeneric (UUID.Zero, "GridSettings", "Settings", _settingsGrid.ToOSD ());
            generics.AddGeneric (UUID.Zero, "WebSettingsVersion", "", new OSDWrapper { Info = CurrentVersion }.ToOSD ());
        }

        public static bool CheckWhetherIgnoredVersionUpdate (uint version)
        {
            return version != CurrentVersion;
        }
    }
}
