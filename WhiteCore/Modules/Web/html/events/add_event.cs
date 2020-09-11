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

using System;
using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class EventsAddMain : IWebInterfacePage
    {
        public string[] FilePath {
            get {
                return new[] {
                    "html/events/add_event.html"
                };
            }
        }

        public bool RequiresAuthentication {
            get { return true; }
        }

        public bool RequiresAdminAuthentication {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response) {
            UserAccount ourAccount = Authenticator.GetAuthentication(httpRequest);
            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule>();
            var currencySymbol = "$";
            if (moneyModule != null)
                currencySymbol = moneyModule.InWorldCurrencySymbol;

            response = null;
            var vars = new Dictionary<string, object>();
            var duration = 10;

            if (requestParameters.ContainsKey("additem")) {
                string eventName = requestParameters["eventName"].ToString();
                string eventDate = requestParameters["eventDate"].ToString();
                string eventTime = requestParameters["eventTime"].ToString();
                string eventDuration = requestParameters["eventDuration"].ToString();
                string eventLocation = requestParameters["eventLocation"].ToString();
                string eventCategory = requestParameters["eventCategory"].ToString();
                string eventCoverCharge = requestParameters["eventCoverCharge"].ToString();
                string eventDescription = requestParameters["eventDescription"].ToString();

                var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector>();
                var regionData = Framework.Utilities.DataManager.RequestPlugin<IRegionData>();

                var selParcel = eventLocation.Split(',');
                // Format: parcelLocationX, parcelLocationY, parcelLandingX, parcelLandingY, parcelLandingZ, parcelUUID
                // "1020,995,128,28,25,d436261b-7186-42a6-dcd3-b80c1bcafaa4"

                Framework.Services.GridRegion region = null;
                var parcel = directoryService.GetParcelInfo((UUID)selParcel[5]);
                if (parcel != null)
                    region = regionData.Get(parcel.RegionID, null);
                if (region == null) {
                    response = webInterface.UserMsg("!Location details not found", false);

                    //var error = "Parcel details not found!";
                    //vars.Add("ErrorMessage", "<h3>" + error + "</h3>");
                    //response = "<h3>" + error + "</h3>";
                    return null;
                }

                // we have details...
                var eventDT = DateTime.Parse(eventDate + " " + eventTime);
                var localPos = new Vector3(int.Parse(selParcel[0]), int.Parse(selParcel[0]), 0);

                var nEvent = directoryService.CreateEvent(
                    ourAccount.PrincipalID,
                    region.RegionID,
                    (UUID)selParcel[5],
                    eventDT,
                    uint.Parse(eventCoverCharge),
                    (DirectoryManager.EventFlags)Util.ConvertAccessLevelToMaturity(region.Access),
                    region.Access,
                    uint.Parse(eventDuration),
                    localPos,
                    eventName,
                    eventDescription,
                    eventCategory
                );

                if (nEvent != null)
                    response = webInterface.UserMsg("Event added successfully", true);

                    //response = "<h3>Event added successfully, redirecting to main page</h3>" +
                    //    "<script language=\"javascript\">" +
                    //    "setTimeout(function() {window.location.href = \"/?page=events\";}, 0);" +
                    //    "</script>";

                return null;
            }

            // Time selections
            var nearestHalf = Utilities.RoundUp(DateTime.Now, TimeSpan.FromMinutes(30)).ToString("HH\\:mm\\:ss");
            vars.Add("EventDate", DateTime.Now.AddDays(1).ToShortDateString());
            vars.Add("EventTimes", WebHelpers.EventTimeSelections(nearestHalf));

            // event durations
            vars.Add("EventDurations", WebHelpers.EventDurationSelections(duration));

            // event locations
            vars.Add("EventLocations", WebHelpers.UserLocations(ourAccount, webInterface.Registry, ""));

            vars.Add("EventCategories", WebHelpers.EventCategorySelections(-1, translator));
            vars.Add("EventCoverCharge", "0");

            // labels
            vars.Add("AddEventText", translator.GetTranslatedString("AddEventText"));
            vars.Add("EventNameText", translator.GetTranslatedString("EventNameText"));
            vars.Add("EventDateText", translator.GetTranslatedString("EventDateText"));
            vars.Add("EventTimeText", translator.GetTranslatedString("TimeText"));
            vars.Add("EventTimeInfoText", translator.GetTranslatedString("EventTimeInfoText"));
            vars.Add("EventDurationText", translator.GetTranslatedString("DurationText"));
            vars.Add("EventLocationText", translator.GetTranslatedString("EventLocationText"));
            vars.Add("EventCategoryText", translator.GetTranslatedString("CategoryText"));
            vars.Add("EventCoverChargeText", translator.GetTranslatedString("CoverChargeText") + " " + currencySymbol);
            vars.Add("EventDescriptionText", translator.GetTranslatedString("DescriptionText"));


            vars.Add("ErrorMessage", "");
            vars.Add("Cancel", translator.GetTranslatedString("Cancel"));
            vars.Add("Submit", translator.GetTranslatedString("AddEventText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text) {
            text = "";
            return false;
        }
    }
}
