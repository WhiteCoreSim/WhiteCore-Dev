/*
 * Copyright (c) Contributors, http://WhiteCore-sim.org/
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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services.API
{
    class APIAuthItem : IDataTransferable
    {
        public static readonly APIAuthItem NoAuthItem = new APIAuthItem {
            KeyID = -1,
            APIKey = "",
            KeyDate = DateTime.Now,
            Username = "",
            UserID = UUID.Zero
        };

        public string Username;
        public UUID UserID;
        public string APIKey;
        public DateTime KeyDate;
        public int KeyID;

        public override OSDMap ToOSD ()
        {
            var map = new OSDMap ();
            map ["Username"] = Username;
            map ["UserID"] = UserID;
            map ["APIKey"] = APIKey;
            map ["KeyDate"] = KeyDate;
            map ["KeyID"] = KeyID;
            return map;
        }

        public override void FromOSD (OSDMap map)
        {
            Username = map ["Username"];
            UserID = map ["UserID"];
            APIKey = map ["APIKey"];
            KeyDate = map ["KeyDate"];
            KeyID = map ["KeyID"];
        }

        public Dictionary<string, object> ToDictionary ()
        {
            var dictionary = new Dictionary<string, object> ();

            dictionary.Add ("KeyDate", Culture.LocaleDate (KeyDate));
            dictionary.Add ("APIKey", APIKey);
            dictionary.Add ("KeyID", KeyID);
            dictionary.Add ("Username", Username);
            dictionary.Add ("UserID", UserID);

            return dictionary;
        }
    }


    public class APIService : IService
    {
        uint m_port = 8004;
        IHttpServer m_server;
        IRegistryCore m_registry;
        bool m_enabled = true;
        IGenericsConnector generics;

        string m_servernick = "whitecore_api";

        public string Name {
            get { return GetType ().Name; }
        }

        #region IService

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            if (config.Configs ["GridInfoService"] != null)
                m_servernick = config.Configs ["GridInfoService"].GetString ("gridnick", m_servernick);
            m_registry = registry;

            var apiConfig = config.Configs ["APIService"];
            if (apiConfig != null) {
                m_enabled = apiConfig.GetBoolean ("Enabled", m_enabled);
                m_port = apiConfig.GetUInt ("Port", m_port);
            }

            generics = DataManager.RequestPlugin<IGenericsConnector> ();
            m_enabled = (generics != null);

        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            if (m_enabled) {
                m_server = registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (m_port);
                m_server.AddStreamHandler (new APIHandler (m_registry, "POST"));
                m_server.AddStreamHandler (new APIHandler (m_registry, "GET"));

                AddCommands ();
                MainConsole.Instance.Info ("[API]: API service active on port " + m_port);

            }
        }


        void AddCommands ()
        {
            MainConsole.Instance.Commands.AddCommand (
                "api promote user",
                "api promote user [<first last>]",
                "Grants the specified user access to the API.",
                PromoteAPIUser, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "api demote user",
                "api demote user [<first last>]",
                "Grants the specified user access to the API.",
                DemoteAPIUser, false, true);

            MainConsole.Instance.Commands.AddCommand (
                "api show keys",
                "api show keys",
                "Show current API key information.",
                ShowAPIUsers, false, true);
        }


        public void FinishedStartup ()
        {
        }

        #endregion
        #region Console Commands

        /// <summary>
        /// Promotes a user to allow API access.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void PromoteAPIUser (IScene scene, string [] cmd)
        {
            var userName = MainConsole.Instance.Prompt ("Name of user <First> <Last>");
            var account = m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, userName);

            if (account == null) {
                MainConsole.Instance.Error ("Sorry! Unable to locate this user.");
                return;
            }

            var authKey = UUID.Random ().ToString ();

            var apiItem = new APIAuthItem { Username = account.Name, KeyDate = DateTime.Now, APIKey = authKey };
            apiItem.KeyID = generics.GetGenericCount ((UUID)Constants.GovernorUUID, "APIKey") + 1;
            generics.AddGeneric ((UUID)Constants.GovernorUUID, "APIKey", authKey, apiItem.ToOSD ());

            MainConsole.Instance.InfoFormat ("[API]: User {0} {1} - API key : {2}", account.FirstName, account.LastName, authKey);


        }

        /// <summary>
        /// Demotes an API User.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void DemoteAPIUser (IScene scene, string [] cmd)
        {
            var userName = MainConsole.Instance.Prompt ("Name of user <First> <Last>");
            var account = m_registry.RequestModuleInterface<IUserAccountService> ().GetUserAccount (null, userName);
            if (account == null) {
                MainConsole.Instance.Error ("[API]: User does not exist!");
                return;
            }

            var apiKeys = generics.GetGenerics<APIAuthItem> ((UUID)Constants.GovernorUUID, "APIKey");
            if (apiKeys == null) {
                MainConsole.Instance.Warn ("No API keys are currently active.");
                return;
            }

            var apiKey = "";
            foreach (var apiItem in apiKeys) {
                if (apiItem.UserID == account.PrincipalID) {
                    apiKey = apiItem.APIKey;
                    generics.RemoveGeneric (UUID.Zero, "APIKey", apiKey);   // need something specific here to remove individual keys
                    MainConsole.Instance.Info ("[API]: Removed API key for " + account.Name);
                    break;
                }
            }

            if (apiKey == "")
                MainConsole.Instance.Warn ("[API]: Unable to locate a key for " + account.Name);
        }

        /// <summary>
        /// Shows the current API user details.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="cmd">Cmd.</param>
        void ShowAPIUsers (IScene scene, string [] cmd)
        {
            string apiInfo;
            var apiKeys = generics.GetGenerics<APIAuthItem> ((UUID)Constants.GovernorUUID, "APIKey");
            if (apiKeys == null) {
                MainConsole.Instance.Warn ("No API keys are currently active.");
                return;
            }

            apiInfo = string.Format ("{0, -20}", "User");
            apiInfo += string.Format ("{0, -14}", "Date");
            apiInfo += string.Format ("{0, -26}", "API key");

            MainConsole.Instance.CleanInfo (apiInfo);
            MainConsole.Instance.CleanInfo ("--------------------------------------------------------------------------------------------------------");

            foreach (var apiKey in apiKeys) {
                apiInfo = string.Format ("{0, -20}", apiKey.Username);
                apiInfo += string.Format ("{0, -14}", apiKey.KeyDate);
                apiInfo += string.Format ("{0, -26}", apiKey.APIKey);
                MainConsole.Instance.CleanInfo (apiInfo);
            }
            MainConsole.Instance.CleanInfo ("");
        }

        #endregion
    }


    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        IRegistryCore m_registry;
        Dictionary<string, MethodInfo> APIPostMethods = new Dictionary<string, MethodInfo> ();
        Dictionary<string, MethodInfo> APIRestMethods = new Dictionary<string, MethodInfo> ();
        bool verified = false;

        public APIHandler (IRegistryCore registry, string method) : base (method, "/API")
        {
            m_registry = registry;
            MethodInfo [] methods = GetType ().GetMethods (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method == "POST") {
                for (uint i = 0; i < methods.Length; ++i) {
                    if (methods [i].IsPrivate &&
                        methods [i].ReturnType == typeof (OSDMap) &&
                        methods [i].GetParameters ().Length == 1 &&
                        methods [i].GetParameters () [0].ParameterType == typeof (OSDMap)) {
                        APIPostMethods [methods [i].Name] = methods [i];
                    }
                }
            } else {
                for (uint i = 0; i < methods.Length; ++i) {
                    if (methods [i].IsPrivate &&
                        methods [i].ReturnType == typeof (OSDMap) &&
                        methods [i].GetParameters ().Length == 1 &&
                        methods [i].GetParameters () [0].ParameterType == typeof (string [])) {
                        APIRestMethods [methods [i].Name] = methods [i];
                    }
                }
            }
        }


        #region BaseStreamHandler

        public override byte [] Handle (string path, Stream request,
                                        OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string [] req = path.Split ('/');

            //Remove the /API/
            //string method = httpRequest.RawUrl.Remove (0, 5);
            string method = req [2];
            var httpMethod = httpRequest.HttpMethod;
            var parms = SplitParams (path);

            var body = HttpServerHandlerHelpers.ReadString (request);
            var response = new OSDMap ();

            var requestData = new OSDMap();
            //string method = "";
            if (body != "") {
                requestData = (OSDMap)OSDParser.DeserializeLLSDXml (body);
                if (requestData != "")
                    method = requestData ["Method"].AsString ();
            }

            string authKey = httpRequest.Headers ["authentication"];
            string reqKey = httpRequest.Query.ContainsKey ("key")
                                       ? httpRequest.Query ["key"].ToString ()
                                       : string.Empty;



            try {
                //Make sure that the person who is calling can access the api service
                if (requestData.ContainsKey ("key"))
                    reqKey = requestData ["key"];
                verified = VerifyAuthentication (reqKey, authKey);
                response ["Verified"] = OSD.FromBoolean (verified);

                if (verified) {
                    MainConsole.Instance.Debug ("[API]: Request authorised");

                    if (httpMethod == "POST") {
                        if (APIPostMethods.ContainsKey (method)) {
                            var args = new object [] { requestData };
                            response = (OSDMap)APIPostMethods [method].Invoke (this, args);
                        }
                    } else if (method == "Image") {
                        httpResponse.ContentType = "image/jpeg";
                        return GetImages (parms);                           // This will return a byte [] of the image data
                    } else if (APIRestMethods.ContainsKey (method)) {
                        var args = new object [] { parms };
                        response = (OSDMap)APIRestMethods [method].Invoke (this, args);
                    } else {
                        var error = string.Format("[API] Unsupported method called ({0})", method);
                        MainConsole.Instance.Trace (error);
                        response.Add ("success", false);
                        response.Add ("response", OSD.FromString (error));
                    }
                }
            } catch (Exception e) {
                MainConsole.Instance.TraceFormat ("[API] Exception calling method ({0})", method);
                response.Add ("success", false);
                response.Add ("response", OSD.FromString ("Exception calling method : " + e));
            }

            if (response.Count == 0) {
                response.Add ("success", false);
                response.Add ("response", OSD.FromString ("No content"));
            } else if (!response.ContainsKey("success"))
                response.Add ("success", true);


            string xmlString = OSDParser.SerializeLLSDXmlString (response);
            var encoding = new UTF8Encoding ();
            return encoding.GetBytes (xmlString);
        }


        bool VerifyAuthentication (string reqKey, string authKey)
        {
            var authority = string.Empty;
            var authorityKey = string.Empty;
            Uri authUri;

            // try for an embedded key 
            // authKey = httpRequest.Headers["authentication"];
            var generics = DataManager.RequestPlugin<IGenericsConnector> ();
            var apiAuth = generics.GetGeneric<APIAuthItem> ((UUID)Constants.GovernorUUID, "APIKey", authKey);

            if (apiAuth != null)
                return true;

            // try for an inline key 
            // http://whitecore-sim_grid:8004/API/?key=<uuid>
            if (!string.IsNullOrEmpty (reqKey) && reqKey != "None") {
                if (Uri.TryCreate (reqKey, UriKind.Absolute, out authUri)) {
                    authority = authUri.Authority;
                    authorityKey = authUri.PathAndQuery.Trim ('/');
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region general

        /// <summary>
        /// Onlines status of the grid.
        /// </summary>
        /// <returns>The current grid status.</returns>
        /// <param name="args">Arguments.</param>
        OSDMap OnlineStatus (string [] args)
        {
            ILoginService loginService = m_registry.RequestModuleInterface<ILoginService> ();
            bool LoginEnabled = loginService.MinLoginLevel == 0;

            var resp = new OSDMap ();
            resp ["Online"] = OSD.FromBoolean (true);
            resp ["LoginEnabled"] = OSD.FromBoolean (LoginEnabled);

            return resp;
        }

        /// <summary>
        /// Grids info.
        /// </summary>
        /// <returns>The info.</returns>
        /// <param name="args">Arguments.</param>
        OSDMap GridInfo (string [] args)
        {
            var response = new OSDMap ();
            var gridInfo = new OSDMap ();

            //Add our grid service URIs 
            IGridInfo gridInfoService = m_registry.RequestModuleInterface<IGridInfo> ();
            if (gridInfoService != null) {
                var localInfo = gridInfoService.GetGridInfoHashtable ();
                foreach (string key in localInfo.Keys)
                    gridInfo.Add (key, (string)localInfo [key]);
            }

            response ["GridInfo"] = gridInfo;
            return response;
        }

        #endregion

        #region images
        public byte [] GetImages (string [] parms)
        {
            var method = parms [1];                     // parms[0] => Image
            var uri = parms [2];

            if (method == "GridTexture")                //  ../Image/GridTexture/<textureUUID>
                return GridTexture (uri);
            if (method == "AvatarImage")                //  ../Image/AvatarImage/<imageURL>
                return AvatarImage (uri);
            if (method == "WebImage")                   //  ../Image/WebImage/<image.jpg>
                return WebImage (uri);


            // if all else fails
            return new byte [0];
        }

        byte [] GridTexture (string uri)
        {
            byte [] jpeg = new byte [0];
            var imageUUID = uri;

            // check for bogies
            if (imageUUID == "" || imageUUID == UUID.Zero.ToString ())
                return jpeg;

            IAssetService m_AssetService = m_registry.RequestModuleInterface<IAssetService> ();

            using (MemoryStream imgstream = new MemoryStream ()) {
                // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                // non-async because we know we have the asset immediately.
                byte [] mapasset = m_AssetService.GetData (imageUUID);

                if (mapasset != null) {
                    // Decode image to System.Drawing.Image
                    Image image;
                    ManagedImage managedImage;
                    var myEncoderParameters = new EncoderParameters ();
                    myEncoderParameters.Param [0] = new EncoderParameter (System.Drawing.Imaging.Encoder.Quality, 75L);
                    if (OpenJPEG.DecodeToImage (mapasset, out managedImage, out image)) {
                        // Save to bitmap
                        var texture = ResizeBitmap (image, 256, 256);
                        try {
                            var encInfo = GetEncoderInfo ("image/jpeg");
                            if (encInfo != null)
                                texture.Save (imgstream, encInfo, myEncoderParameters);

                            // Write the stream to a byte array for output
                            jpeg = imgstream.ToArray ();
                        } catch {
                            MainConsole.Instance.Debug ("[API]: GridTexture request exception");
                        }
                    }
                    myEncoderParameters.Dispose ();
                    if (image != null)
                        image.Dispose ();
                }
            }

            if (jpeg.Length > 0)
                return jpeg;

            // no UUID here so...
            var nouuid = "html/images/icons/no_image.png";
            try {
                return File.ReadAllBytes (nouuid);
            } catch {
                MainConsole.Instance.Debug ("[API]: GridTexture fallback exception. 'no_image.jpg' does not exist?");
            }

            return new byte [0];
        }


        public byte [] AvatarImage (string uri)
        {
            var basepath = "../Data/AvatarArchives/";
            var imageurl = uri;
            var nourl = "html/images/icons/no_avatar.jpg";

            try {
                // try for raw url
                if (File.Exists (uri)) {
                    return File.ReadAllBytes (imageurl);
                }
                // add basepath and retry
                imageurl = basepath + imageurl;
                if (File.Exists (imageurl)) {
                    return File.ReadAllBytes (imageurl);
                }
                return File.ReadAllBytes (nourl);
            } catch {
                MainConsole.Instance.Debug ("[API]: AvatarImage request exception");
            }

            return new byte [0];
        }


        public byte [] WebImage (string uri)
        {
            var basepath = "../Data/html/images/";
            var imageurl = uri;
            var nourl = "html/images/noimage.jpg";

            try {
                // try for raw url
                if (File.Exists (imageurl)) {
                    return File.ReadAllBytes (imageurl);
                }
                // add basepath and retry
                imageurl = basepath + imageurl;
                if (File.Exists (imageurl)) {
                    return File.ReadAllBytes (imageurl);
                }

                return File.ReadAllBytes (nourl);
            } catch {
                MainConsole.Instance.Debug ("[API]: WebImage request exception");
            }

            return new byte [0];
        }

        #region ImageHelpers
        Bitmap ResizeBitmap (Image b, int nWidth, int nHeight)
        {
            var newsize = new Bitmap (nWidth, nHeight);
            Graphics temp = Graphics.FromImage (newsize);
            temp.DrawImage (b, 0, 0, nWidth, nHeight);
            temp.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            temp.DrawString ("WhiteCore", new Font ("Arial", 8, FontStyle.Regular),
                            new SolidBrush (Color.FromArgb (90, 255, 255, 50)), new Point (2, nHeight - 13));

            temp.Dispose ();
            return newsize;
        }

        static ImageCodecInfo GetEncoderInfo (string mimeType)
        {
            ImageCodecInfo [] encoders;
            try {
                encoders = ImageCodecInfo.GetImageEncoders ();
            } catch {
                return null;
            }

            for (int j = 0; j < encoders.Length; ++j) {
                if (encoders [j].MimeType == mimeType)
                    return encoders [j];
            }
            return null;
        }
        #endregion //ImageHelpers
        #endregion

    }
}
