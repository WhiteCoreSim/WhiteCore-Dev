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
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class UserClassifiedsEdit : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/user/classifieds_edit.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return true; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return false; }
        }

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object>();
            var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector>();

            // the classified id can come in a number of ways
            UUID classfdid = httpRequest.Query.ContainsKey("userid")
                            ? UUID.Parse(httpRequest.Query["userid"].ToString())
                            : UUID.Parse(requestParameters["userid"].ToString());

            if (classfdid == UUID.Zero)
            {
                response = webInterface.UserMsg("!Classified details not supplied", true);
                return null;
            }

            var currencySymbol = "$";
            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule>();
            if (moneyModule != null)
                currencySymbol = moneyModule.InWorldCurrencySymbol;

            UserAccount user = Authenticator.GetAuthentication(httpRequest);

            //var pg_checked = "checked";
            //var ma_checked = "checked";
            //var ao_checked = "checked";
            //var classifiedLevel = (uint)DirectoryManager.ClassifiedQueryFlags.All;
            //var category = (int)DirectoryManager.ClassifiedCategories.Any;


            // maturity selections
            //vars.Add ("PG_checked", pg_checked);
            //vars.Add ("MA_checked", ma_checked);
            //vars.Add ("AO_checked", ao_checked);

            // get the classified to edit
            Classified classified;

            if (directoryService != null)
            {

                classified = directoryService.GetClassifiedByID(classfdid);
                if (classified == null)
                {
                    response = webInterface.UserMsg("!Classified not found");
                    return null;
                }
            }
            else
            {
                response = webInterface.UserMsg("!Directory service unabailable", true);
                return null;
            }

            // fill in the details
            vars.Add("ClassifiedID", classified.ClassifiedUUID);
            //vars.Add("CreatorUUID", classified.CreatorUUID);,
            vars.Add("CreationDate", Util.ToDateTime(classified.CreationDate).ToShortDateString());
            vars.Add("ExpirationDate", Util.ToDateTime(classified.ExpirationDate).ToShortDateString());
            vars.Add("Category", WebHelpers.ClassifiedCategory(classified.Category, translator));
            vars.Add("Name", classified.Name);
            vars.Add("Description", classified.Description);
            //vars.Add("ParcelUUID", classified.ParcelUUID);
            //vars.Add("ParentEstate",classified.(ParentEstate);
            vars.Add("SnapshotUUID", classified.SnapshotUUID);
            //vars.Add( "ScopeID",clasified.ScopeID );
            vars.Add("SimName", classified.SimName);
            vars.Add("GPosX", classified.GlobalPos.X.ToString());
            vars.Add("GPosY", classified.GlobalPos.Y.ToString());
            vars.Add("GPosZ", classified.GlobalPos.Z.ToString());
            vars.Add("ParcelName", classified.ParcelName);
            vars.Add("Maturity", WebHelpers.ClassifiedMaturity(classified.ClassifiedFlags));
            vars.Add("PriceForListing", currencySymbol + " " + classified.PriceForListing);


            // build selection
            //vars.Add("ClassifiedLocations", WebHelpers.UserLocations(user, webInterface.Registry, classified.ParcelUUID));
            vars.Add("ClassifiedCategories", WebHelpers.ClassifiedCategorySelections((int)classified.Category, translator));
            vars.Add("MaturityLevels", WebHelpers.MaturitySelections(classified.ClassifiedFlags, translator));

            vars.Add ("Classifieds", translator.GetTranslatedString ("Classifieds"));

            // labels
            vars.Add("UserName", user.Name);
            vars.Add ("ClassifiedsText", translator.GetTranslatedString("ClassifiedsText"));
            vars.Add("AddClassifiedText", translator.GetTranslatedString("AddClassifiedText"));
            vars.Add ("CreationDateText", translator.GetTranslatedString ("CreationDateText"));
            vars.Add ("CategoryText", translator.GetTranslatedString ("CategoryText"));
            vars.Add ("ClassifiedNameText", translator.GetTranslatedString ("ClassifiedText"));
            vars.Add ("DescriptionText", translator.GetTranslatedString ("DescriptionText"));
            vars.Add ("MaturityText", translator.GetTranslatedString ("MaturityText"));
            vars.Add ("GeneralText", translator.GetTranslatedString ("GeneralText"));
            vars.Add ("MatureText", translator.GetTranslatedString ("MatureText"));
            vars.Add ("AdultText", translator.GetTranslatedString ("AdultText"));
            vars.Add ("PriceOfListingText", translator.GetTranslatedString ("PriceOfListingText"));
            vars.Add ("ExpirationDateText", translator.GetTranslatedString ("ExpirationDateText"));
            vars.Add ("SearchText", translator.GetTranslatedString ("SearchText"));

            return vars;
        }



        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
