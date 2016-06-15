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

using System.Collections.Generic;
using OpenMetaverse;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services.ClassHelpers.Profile;

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

        public Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest httpRequest,
                                               OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                               ITranslator translator, out string response)
        {
            response = null;
            var vars = new Dictionary<string, object> ();
            var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
            List<Dictionary<string, object>> classifiedListVars = new List<Dictionary<string, object>> ();


            vars.Add ("Classifieds", "Classifieds"); //translator.GetTranslatedString ("Classifieds"));
            vars.Add ("ClassifiedTitle", "ClassifiedTitle"); //translator.GetTranslatedString ("ClassifiedTitle"));
            vars.Add ("ClassifiedText", "ClassifiedText"); //translator.GetTranslatedString ("ClassifiedText"));

            vars.Add ("Classified", "Classified"); //translator.GetTranslatedString ("Classified"));
            vars.Add ("ClassifiedDateText", "Date"); // translator.GetTranslatedString ("DateText"));
            vars.Add ("ClassifiedTitleText", "Title"); //translator.GetTranslatedString ("TitleText"));

            if (directoryService != null) {

                var classifieds = new List<Classified> ();
                classifieds = directoryService.GetAllClassifieds ((int)DirectoryManager.ClassifiedCategories.Any,
                                                               (uint)DirectoryManager.ClassifiedFlags.None);

                if (classifieds.Count == 0) {       // not sure if this is needed actually... return empty list?
                    classifiedListVars.Add (new Dictionary<string, object> {
                        { "ClassifiedUUID", "" },
                        //{ "CreatorUUID", classified.CreatorUUID) },
                        { "CreationDate", "" },
                        { "ExpirationDate", "" },
                        { "Category", "" },
                        { "Name", "" },
                        { "Description", "" },
                        //{ "ParcelUUID", OSD.FromUUID (ParcelUUID) },
                        //{ "ParentEstate", OSD.FromUInteger (ParentEstate) },
                        //{ "SnapshotUUID", OSD.FromUUID (SnapshotUUID) },
                        //{ "ScopeID", OSD.FromUUID (ScopeID) },
                        //{ "SimName", OSD.FromString (SimName) },
                        //{ "GPosX", OSD.FromReal (GlobalPos.X).ToString () },
                        //{ "GPosY", OSD.FromReal (GlobalPos.Y).ToString () },
                        //{ "GPosZ", OSD.FromReal (GlobalPos.Z).ToString () },
                        // "ParcelName", OSD.FromString (ParcelName) },
                        { "ClassifiedFlags", "" },
                        { "PriceForListing", "" }
                    });
                } else {
                    foreach (var classified in classifieds) {
                        classifiedListVars.Add (new Dictionary<string, object> {
                                { "ClassifiedUUID", classified.ClassifiedUUID },
                                //{ "CreatorUUID", classified.CreatorUUID) },
                                { "CreationDate", classified.CreationDate },
                                { "ExpirationDate", classified.ExpirationDate },
                                { "Category", classified.Category },
                                { "Name", classified.Name },
                                { "Description", classified.Description },
                                //{ "ParcelUUID", OSD.FromUUID (ParcelUUID) },
                                //{ "ParentEstate", OSD.FromUInteger (ParentEstate) },
                                //{ "SnapshotUUID", OSD.FromUUID (SnapshotUUID) },
                                //{ "ScopeID", OSD.FromUUID (ScopeID) },
                                //{ "SimName", OSD.FromString (SimName) },
                                //{ "GPosX", OSD.FromReal (GlobalPos.X).ToString () },
                                //{ "GPosY", OSD.FromReal (GlobalPos.Y).ToString () },
                                //{ "GPosZ", OSD.FromReal (GlobalPos.Z).ToString () },
                                // "ParcelName", OSD.FromString (ParcelName) },
                                { "ClassifiedFlags", classified.ClassifiedFlags },
                                { "PriceForListing", classified.PriceForListing }
                        });
                    }
                }
                vars.Add ("ClassifiedList", classifiedListVars);
            }

            return vars;
        }

        public bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text)
        {
            text = "";
            return false;
        }
    }
}
