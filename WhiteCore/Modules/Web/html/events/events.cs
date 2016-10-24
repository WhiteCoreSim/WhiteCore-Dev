/*
 * Copyright (c) Contributors, http://whitecore-sim.org/
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


using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class EventsMain : IWebInterfacePage
    {
        public string [] FilePath {
            get {
                return new []
                           {
                               "html/events/events.html"
                           };
            }
        }

        public bool RequiresAuthentication {
            get { return false; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();
            var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
            var eventListVars = new List<Dictionary<string, object>> ();
            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule> ();

            var currencySymbol = "$";
            if (moneyModule != null)
                currencySymbol = moneyModule.InWorldCurrencySymbol;

            var eventLevel = Util.ConvertEventMaturityToDBMaturity (DirectoryManager.EventFlags.PG);
            var category = (int)DirectoryManager.EventCategories.All;
            var timeframe = 24;

            var pg_checked = "checked";
            var ma_checked = "";
            var ao_checked = "";

            if (requestParameters.ContainsKey ("Submit")) {
                int level = 0;
                pg_checked = "";
                ma_checked = "";
                ao_checked = "";
                if (requestParameters.ContainsKey ("display_pg")) {
                    //level += 1;
                    level += Util.ConvertEventMaturityToDBMaturity (DirectoryManager.EventFlags.PG);
                    pg_checked = "checked";
                }
                if (requestParameters.ContainsKey ("display_ma")) {
                    //level += 2;
                    level += Util.ConvertEventMaturityToDBMaturity (DirectoryManager.EventFlags.Mature);
                    ma_checked = "checked";
                }
                if (requestParameters.ContainsKey ("display_ao")) {
                    //level += 4;
                    level += Util.ConvertEventMaturityToDBMaturity (DirectoryManager.EventFlags.Adult);
                    ao_checked = "checked";
                }
                eventLevel = level;

                string cat = requestParameters ["category"].ToString ();
                category = int.Parse (cat);

                string timsel = requestParameters ["timeframe"].ToString ();
                timeframe = int.Parse (timsel);
            }

            // maturity selections
            vars.Add ("PG_checked", pg_checked);
            vars.Add ("MA_checked", ma_checked);
            vars.Add ("AO_checked", ao_checked);

            // build category selection
            vars.Add ("CategoryType", WebHelpers.EventCategorySelections (category, translator));

            // build timeframes
            vars.Add ("TimeFrame", WebHelpers.EventTimeframesSelections (timeframe, translator));

            // get some events
            if (directoryService != null) {

                var events = new List<EventData> ();
                events = directoryService.GetAllEvents (timeframe, category, eventLevel);

                if (events.Count == 0) {
                    eventListVars.Add (new Dictionary<string, object> {
                        { "EventID", "" },
                        { "CreatorUUID", "" },
                        { "EventDate", "" },
                        { "EventDateUTC", "" },
                        { "CoverCharge", "" },
                        { "Duration", "" },
                        { "Name", "" },
                        { "Description", translator.GetTranslatedString("NoDetailsText") },
                        { "SimName", "" },
                        { "GPosX", "" },
                        { "GPosY", "" },
                        { "GPosZ", "" },
                        { "LocalPosX", "" },
                        { "LocalPosY", "" },
                        { "LocalPosZ", "" },
                        { "Maturity", "" },
                        { "EventFlags", "" },   // same as maturity??
                        { "Category", "" }
                });
                } else {
                    foreach (var evnt in events) {
                        var evntDateTime = Util.ToDateTime (evnt.dateUTC).ToLocalTime ();
                        eventListVars.Add (new Dictionary<string, object> {
                            { "EventID", evnt.eventID },
                            { "CreatorUUID", evnt.creator },
                            { "EventDate", evnt.date },
                            { "EventDateUTC", Culture.LocaleShortDateTime(evntDateTime)},
                            { "CoverCharge", currencySymbol + " " + evnt.amount },
                            { "Duration", WebHelpers.EventDuration((int)evnt.duration,translator) },
                            { "Name", evnt.name },
                            { "Description", evnt.description },
                            { "SimName", evnt.simName },
                            { "GPosX", evnt.globalPos.X.ToString () },
                            { "GPosY", evnt.globalPos.Y.ToString () },
                            { "GPosZ", evnt.globalPos.Z.ToString () },
                            { "LocalPosX", evnt.regionPos.X.ToString () },
                            { "LocalPosY", evnt.regionPos.Y.ToString () },
                            { "LocalPosZ",evnt.regionPos.Z.ToString () },
                            { "Maturity", WebHelpers.EventMaturity(evnt.maturity) },
                            { "EventFlags", evnt.eventFlags },
                            { "Category",  WebHelpers.EventCategory(int.Parse(evnt.category), translator) }
                        });
                    }
                }
                vars.Add ("EventList", eventListVars);
            }

            vars.Add ("Events", translator.GetTranslatedString ("Events"));

            // labels
            vars.Add ("EventsText", translator.GetTranslatedString ("EventsText"));
            vars.Add ("AddEventText", translator.GetTranslatedString ("AddEventText"));
            vars.Add ("EventDateText", translator.GetTranslatedString ("EventDateText"));
            vars.Add ("CategoryText", translator.GetTranslatedString ("CategoryText"));
            vars.Add ("LocationText", translator.GetTranslatedString ("LocationText"));
            vars.Add ("DescriptionText", translator.GetTranslatedString ("DescriptionText"));
            vars.Add ("MaturityText", translator.GetTranslatedString ("MaturityText"));
            vars.Add ("GeneralText", translator.GetTranslatedString ("GeneralText"));
            vars.Add ("MatureText", translator.GetTranslatedString ("MatureText"));
            vars.Add ("AdultText", translator.GetTranslatedString ("AdultText"));
            vars.Add ("CoverChargeText", translator.GetTranslatedString ("CoverChargeText"));
            vars.Add ("DurationText", translator.GetTranslatedString ("DurationText"));
            vars.Add ("SearchText", translator.GetTranslatedString ("SearchText"));

            return vars;
        }

        public bool AttemptFindPage (string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
