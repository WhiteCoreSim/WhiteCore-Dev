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
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class ClassifiedsMain : IWebInterfacePage
    {
        public string[] FilePath
        {
            get
            {
                return new[]
                           {
                               "html/classifieds.html"
                           };
            }
        }

        public bool RequiresAuthentication
        {
            get { return false; }
        }

        public bool RequiresAdminAuthentication
        {
            get { return false; }
        }

        public Dictionary<string, object> Fill (WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();
            var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
            var classifiedListVars = new List<Dictionary<string, object>> ();
            IMoneyModule moneyModule = webInterface.Registry.RequestModuleInterface<IMoneyModule> ();

            var currencySymbol = "$";
            if (moneyModule != null)
                currencySymbol = moneyModule.InWorldCurrencySymbol;

            var pg_checked = "checked";
            var ma_checked = "";
            var ao_checked = "";
            var classifiedLevel = (uint)DirectoryManager.ClassifiedQueryFlags.PG;
            var category = (int)DirectoryManager.ClassifiedCategories.Any;
            if (requestParameters.ContainsKey ("Submit")) {
                uint level = 0;
                pg_checked = "";
                ma_checked = "";
                ao_checked = "";
                if (requestParameters.ContainsKey ("display_pg")) {
                    level += (uint)DirectoryManager.ClassifiedQueryFlags.PG;
                    pg_checked = "checked";
                }
                if (requestParameters.ContainsKey ("display_ma")) {
                    level += (uint)DirectoryManager.ClassifiedQueryFlags.Mature;
                    ma_checked = "checked";
                }
                if (requestParameters.ContainsKey ("display_ao")) {
                    level += (uint)DirectoryManager.ClassifiedQueryFlags.Adult;
                    ao_checked = "checked";
                }
                classifiedLevel = level;
                string cat = requestParameters ["category"].ToString ();
                category = int.Parse (cat);
            }

            // build category selection
            vars.Add ("CategoryType", WebHelpers.ClassifiedCategorySelections(category, translator));

            // maturity selections
            vars.Add ("PG_checked", pg_checked);
            vars.Add ("MA_checked", ma_checked);
            vars.Add ("AO_checked", ao_checked);

            // get some classifieds
            if (directoryService != null) {

                var classifieds = new List<Classified> ();
                classifieds = directoryService.GetAllClassifieds ( category, classifiedLevel);

                if (classifieds.Count == 0) { 
                    classifiedListVars.Add (new Dictionary<string, object> {
                        { "ClassifiedUUID", "" },
                        //{ "CreatorUUID", "" },
                        { "CreationDate", "" },
                        { "ExpirationDate", "" },
                        { "Category", "" },
                        { "Name", "" },
                        { "Description", translator.GetTranslatedString("NoDetailsText") },
                        //{ "ParcelUUID", "") },
                        //{ "ParentEstate", "" },
                        { "SnapshotUUID", "" },
                        //{ "ScopeID", "" },
                        { "SimName", "" },
                        { "GPosX", "" },
                        { "GPosY", "" },
                        { "GPosZ", "" },
                        { "ParcelName", "" },
                        { "Maturity", "" },
                        { "PriceForListing", "" }
                    });
                } else {
                    foreach (var classified in classifieds) {
                        classifiedListVars.Add (new Dictionary<string, object> {
                            { "ClassifiedUUID", classified.ClassifiedUUID },
                            //{ "CreatorUUID", classified.CreatorUUID) },
                            { "CreationDate", Util.ToDateTime (classified.CreationDate).ToShortDateString () },
                            { "ExpirationDate", Util.ToDateTime (classified.ExpirationDate).ToShortDateString () },
                            { "Category", WebHelpers.ClassifiedCategory(classified.Category, translator) },
                            { "Name", classified.Name },
                            { "Description", classified.Description },
                            //{ "ParcelUUID", classified.ParcelUUID },
                            //{ "ParentEstate",classified.(ParentEstate },
                            { "SnapshotUUID", classified.SnapshotUUID },
                            //{ "ScopeID",clasified.ScopeID },
                            { "SimName", classified.SimName },
                            { "GPosX", classified.GlobalPos.X.ToString () },
                            { "GPosY", classified.GlobalPos.Y.ToString () },
                            { "GPosZ",classified.GlobalPos.Z.ToString () },
                            { "ParcelName", classified.ParcelName },
                            { "Maturity", WebHelpers.ClassifiedMaturity(classified.ClassifiedFlags) },
                            { "PriceForListing", currencySymbol + " " + classified.PriceForListing }
                        });
                    }
                }
                vars.Add ("ClassifiedList", classifiedListVars);
            }
            
            vars.Add ("Classifieds", translator.GetTranslatedString ("Classifieds"));

			// labels
            vars.Add ("ClassifiedsText", translator.GetTranslatedString("ClassifiedsText"));
            vars.Add ("CreationDateText", translator.GetTranslatedString ("CreationDateText"));
            vars.Add ("CategoryText", translator.GetTranslatedString ("CategoryText"));
            vars.Add ("ClassifiedNameText", translator.GetTranslatedString ("ClassifiedText"));
            vars.Add ("DescriptionText", translator.GetTranslatedString ("DescriptionText"));
            vars.Add ("MaturityText", translator.GetTranslatedString ("MaturityText"));
            vars.Add ("PriceOfListingText", translator.GetTranslatedString ("PriceOfListingText"));
            vars.Add ("ExpirationDateText", translator.GetTranslatedString ("ExpirationDateText"));

            return vars;
        }



        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
