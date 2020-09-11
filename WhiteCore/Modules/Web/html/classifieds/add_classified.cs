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
    public class ClassifiedAddMain : IWebInterfacePage
    {
        public string[] FilePath {
            get {
                return new[] {
                    "html/classifieds/add_classified.html"
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

            if (requestParameters.ContainsKey("additem")) {
                string classifiedName = requestParameters["classifiedName"].ToString();
                string classifiedTags = requestParameters["classifiedTags"].ToString();
                string classifiedLocation = requestParameters["classifiedLocation"].ToString();
                string classifiedOwnerID = requestParameters["ownerID"].ToString();
                string classifiedCategory = requestParameters["classifiedCategory"].ToString();
                string classifiedPrice = requestParameters["classifiedPrice"].ToString();
                string classifiedDescription = requestParameters["classifiedDescription"].ToString();

                bool autoRelist = requestParameters.ContainsKey("classifiedAutoRelist")
                                  && requestParameters["ToSAccept"].ToString() == "Yes";


                var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector>();
                var regionData = Framework.Utilities.DataManager.RequestPlugin<IRegionData>();

                var selParcel = classifiedLocation.Split(',');
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
                var localPos = new Vector3(int.Parse(selParcel[0]), int.Parse(selParcel[0]), 0);

                /* TODO - Add appropriate functionality
                var nClassified = directoryService.CreateEvent(
                    ourAccount.PrincipalID,
                    region.RegionID,
                    (UUID)selParcel[5],
                    uint.Parse(classifiedPrice),
                    region.Access,
                    localPos,
                    classifiedName,
                    classifiedDescription,
                    classifiedCategory
                );

                if (nClassified != null)
                    response = webInterface.UserMsg("Classified added successfully", true);

                    //response = "<h3>Event added successfully, redirecting to main page</h3>" +
                    //    "<script language=\"javascript\">" +
                    //    "setTimeout(function() {window.location.href = \"/?page=events\";}, 0);" +
                    //    "</script>";
                */
                return null;
            }


            // classified item locations
            vars.Add("ClassifiedLocations", WebHelpers.UserLocations(ourAccount, webInterface.Registry, ""));

            vars.Add("ClassifiedCategories", WebHelpers.ClassifiedCategorySelections(-1, translator));
            vars.Add("Maturity", WebHelpers.MaturitySelections(-1, translator));
            vars.Add("ClassifiedPrice", "0");

            // labels
            vars.Add("AddClassifiedText", translator.GetTranslatedString("AddClassifiedText"));
            vars.Add("ClassifiedtNameText", translator.GetTranslatedString("ClassifiedText"));
            vars.Add("ClassifiedTagsText", translator.GetTranslatedString("TagsText"));
            vars.Add("ClassifiedLocationText", translator.GetTranslatedString("LocationText"));
            vars.Add("CategoryText", translator.GetTranslatedString("CategoryText"));
            vars.Add("MaturityText", translator.GetTranslatedString("MaturityText"));
            vars.Add("ClassifiedPriceText", translator.GetTranslatedString("PriceOfListingText") + " " + currencySymbol);
            vars.Add("DescriptionText", translator.GetTranslatedString("DescriptionText"));
            vars.Add("AutoRelistText", translator.GetTranslatedString("ClassifiedRelistText"));
            vars.Add("YesText", translator.GetTranslatedString("Yes"));


            vars.Add("ErrorMessage", "");
            vars.Add("Cancel", translator.GetTranslatedString("Cancel"));
            vars.Add("Submit", translator.GetTranslatedString("AddClassifiedText"));

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text) {
            text = "";
            return false;
        }
    }
}
