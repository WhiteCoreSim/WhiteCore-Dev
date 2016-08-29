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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Xsl;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public class WebInterface : IService, IWebInterfaceModule
    {
        #region Declares
		
		#pragma warning disable 0649
		// Putting this here to clear out some warnings - 29082016 Fly-man-

        protected const int CLIENT_CACHE_TIME = 86400;  // 1 day
        protected uint _port = 8002;                    // assuming grid mode here
        protected bool _enabled = true;
        protected Dictionary<string, IWebInterfacePage> _pages = new Dictionary<string, IWebInterfacePage> ();
        protected List<ITranslator> _translators = new List<ITranslator> ();
        protected ITranslator _defaultTranslator;
        protected string m_localHtmlPath = "";

        // webpages and settings cacheing
        internal GridPage webPages;
        internal WebUISettings webUISettings;
        public GridSettings gridSettings;


        #endregion

        #region Public Properties

        public IRegistryCore Registry { get; protected set; }

        public string GridName { get; private set; }
        public string LoginURL { get; private set; }

        public string HomeScreenURL {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/"; }
        }

        public string LoginScreenURL {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/welcomescreen/"; }
        }

        public string WebProfileURL {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/webprofile/"; }
        }

        public string RegistrationScreenURL {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/register.html"; }
        }

        public string ForgotPasswordScreenURL {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/forgot_pass.html"; }
        }

        public string HelpScreenURL {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/help.html"; }
        }

        public string GridURL {
            get { return MainServer.Instance.HostName + ":" + _port; }
        }

        public ITranslator EnglishTranslator {
            get { return _translators.FirstOrDefault (t => t.LanguageName == "en"); }
        }

        #endregion

        #region IService Members

        public void Initialize (IConfigSource config, IRegistryCore registry)
        {
            Registry = registry;

            var wbPages = WhiteCoreModuleLoader.PickupModules<IWebInterfacePage> ();
            foreach (var pages in wbPages) {
                foreach (var page in pages.FilePath) {
                    _pages.Add (page, pages);
                }
            }

            _translators = WhiteCoreModuleLoader.PickupModules<ITranslator> ();
            _defaultTranslator = _translators [0];
        }

        public void Start (IConfigSource config, IRegistryCore registry)
        {
            IConfig con = config.Configs ["WebInterface"];
            if (con != null) {
                _enabled = con.GetString ("Module", "BuiltIn") == "BuiltIn";

                var webPort = con.GetUInt ("Port", 0);
                if (webPort == 0)                               // use default
                    _port = MainServer.Instance.Port;
                else
                    _port = webPort;                            // user defined

                string defaultLanguage = con.GetString ("DefaultLanguage", "en");
                _defaultTranslator = _translators.FirstOrDefault (t => t.LanguageName == defaultLanguage);
                if (_defaultTranslator == null)
                    _defaultTranslator = _translators [0];
            }
            if (_enabled) {
                Registry.RegisterModuleInterface<IWebInterfaceModule> (this);
                var server = registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (_port);
                server.AddStreamHandler (new GenericStreamHandler ("GET", "/", FindAndSendPage));
                server.AddStreamHandler (new GenericStreamHandler ("POST", "/", FindAndSendPage));

                // set local path in case..
                if (m_localHtmlPath == "") {
                    var defpath = registry.RequestModuleInterface<ISimulationBase> ().DefaultDataPath;
                    m_localHtmlPath = Path.Combine (defpath, Constants.DEFAULT_USERHTML_DIR);
                }

                MainConsole.Instance.Info ("[WebUI]: Default language is " + _defaultTranslator.LanguageName.ToUpper ());
            }
        }

        public void FinishedStartup ()
        {
            if (_enabled) {
                IGridInfo gridInfo = Registry.RequestModuleInterface<IGridInfo> ();
                GridName = gridInfo.GridName;
                LoginURL = gridInfo.GridLoginURI;

                if (PagesMigrator.RequiresInitialUpdate ())
                    PagesMigrator.ResetToDefaults ();
                if (SettingsMigrator.RequiresInitialUpdate ())
                    SettingsMigrator.ResetToDefaults (this);
            }
        }

        #endregion

        #region Page Sending

        public IWebInterfacePage GetPage (string path)
        {
            IWebInterfacePage page;
            string directory = string.Join ("/", path.Split ('/'), 0, path.Split ('/').Length - 1) + "/";
            if (!_pages.TryGetValue (path, out page) &&
                !_pages.TryGetValue (directory, out page))
                page = null;
            return page;
        }

        protected byte [] FindAndSendPage (string path, Stream request, OSHttpRequest httpRequest,
                                         OSHttpResponse httpResponse)
        {
            byte [] response;
            string filename = GetFileNameFromHTMLPath (path, httpRequest.Query);
            if (filename == null)
                return MainServer.BlankResponse;

            //httpResponse.KeepAlive = true;
            if (httpRequest.HttpMethod == "POST")
                httpResponse.KeepAlive = false;

            MainConsole.Instance.Debug ("[WebInterface]: Serving " + filename + ", keep-alive: " + httpResponse.KeepAlive);
            IWebInterfacePage page = GetPage (filename);
            if (page != null) {
                // dynamic pages
                httpResponse.ContentType = GetContentType (filename, httpResponse);
                string text;
                if (!File.Exists (filename)) {
                    if (!page.AttemptFindPage (filename, ref httpResponse, out text))
                        return MainServer.BadRequest;
                } else
                    text = File.ReadAllText (filename);

                var requestParameters = request != null
                                            ? ParseQueryString (HttpServerHandlerHelpers.ReadString (request))
                                            : new Dictionary<string, object> ();
                if (filename.EndsWith (".xsl", StringComparison.Ordinal)) {
                    WhiteCoreXmlDocument vars = GetXML (filename, httpRequest, httpResponse, requestParameters);

                    var xslt = new XslCompiledTransform ();
                    if (File.Exists (path)) xslt.Load (GetFileNameFromHTMLPath (path, httpRequest.Query));
                    else if (text != "") {
                        xslt.Load (new XmlTextReader (new StringReader (text)));
                    }
                    var stm = new MemoryStream ();
                    xslt.Transform (vars, null, stm);
                    stm.Position = 1;
                    var sr = new StreamReader (stm);
                    string results = sr.ReadToEnd ().Trim ();

                    return Encoding.UTF8.GetBytes (Regex.Replace (results, @"[^\u0000-\u007F]", string.Empty));
                } else {
                    string respStr;
                    var vars = AddVarsForPage (filename, filename, httpRequest, httpResponse, requestParameters, out respStr);

                    AddDefaultVarsForPage (ref vars);

                    if (!string.IsNullOrEmpty (respStr))
                        return Encoding.UTF8.GetBytes (respStr);

                    if (httpResponse.StatusCode != 200)
                        return MainServer.BlankResponse;

                    if (vars == null)
                        return MainServer.BadRequest;

                    response = Encoding.UTF8.GetBytes (
                        ConvertHTML (filename, text, httpRequest, httpResponse, requestParameters, vars));
                }
            } else {
                // static files
                if (!File.Exists(filename))
                    return MainServer.BadRequest;
                
                httpResponse.ContentType = GetContentType (filename, httpResponse);
                if (httpResponse.ContentType == null)
                    return MainServer.BadRequest;
                
                response = File.ReadAllBytes (filename);
            }
            return response;
        }

        #endregion

        #region Helpers

        protected void AddDefaultVarsForPage (ref Dictionary<string, object> vars)
        {
            if (vars != null) {
                vars.Add ("SystemURL", MainServer.Instance.FullHostName + ":" + _port);
                vars.Add ("SystemName", GridName);
                vars.Add ("LoginURL", LoginURL);
            }
        }

        protected Dictionary<string, object> AddVarsForPage (string filename, string parentFileName,
                                                            OSHttpRequest httpRequest, OSHttpResponse httpResponse,
                                                            Dictionary<string, object> requestParameters,
                                                            out string response)
        {
            response = null;
            Dictionary<string, object> vars;
            IWebInterfacePage page = GetPage (filename);
            if (page != null) {
                ITranslator translator = null;
                if (httpRequest.Query.ContainsKey ("language")) {
                    translator =
                        _translators.FirstOrDefault (t => t.LanguageName == httpRequest.Query ["language"].ToString ());
                    httpResponse.AddCookie (new HttpCookie ("language", httpRequest.Query ["language"].ToString ()));
                } else if (httpRequest.Cookies.Get ("language") != null) {
                    var cookie = httpRequest.Cookies.Get ("language");
                    translator = _translators.FirstOrDefault (t => t.LanguageName == cookie.Value);
                }
                if (translator == null)
                    translator = _defaultTranslator;

                if (page.RequiresAuthentication) {
                    if (!Authenticator.CheckAuthentication (httpRequest))
                        return null;
                }
                if (page.RequiresAdminAuthentication) {
                    if (!Authenticator.CheckAdminAuthentication (httpRequest))
                        return null;
                }
                vars = page.Fill (this, parentFileName, httpRequest, httpResponse, requestParameters,
                                  translator, out response);
                return vars;
            }
            return null;
        }

        WhiteCoreXmlDocument GetXML (string filename, OSHttpRequest httpRequest, OSHttpResponse httpResponse,
                                         Dictionary<string, object> requestParameters)
        {
            IWebInterfacePage page = GetPage (filename);
            if (page != null) {
                ITranslator translator = null;
                if (httpRequest.Query.ContainsKey ("language"))
                    translator =
                        _translators.FirstOrDefault (t => t.LanguageName == httpRequest.Query ["language"].ToString ());
                if (translator == null)
                    translator = _defaultTranslator;

                if (page.RequiresAuthentication) {
                    if (!Authenticator.CheckAuthentication (httpRequest))
                        return null;
                    if (page.RequiresAdminAuthentication) {
                        if (!Authenticator.CheckAdminAuthentication (httpRequest))
                            return null;
                    }
                }
                string response;
                var pageVars = page.Fill (this, filename, httpRequest, httpResponse, requestParameters,
                                          translator, out response);
                if (pageVars != null)
                    return (WhiteCoreXmlDocument)pageVars ["xml"];
            }
            return null;
        }

        protected string ConvertHTML (string originalFileName, string file, OSHttpRequest request,
                                     OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                     Dictionary<string, object> vars)
        {
            string html = CSHTMLCreator.BuildHTML (file, vars);

            string [] lines = html.Split ('\n');
            StringBuilder sb = new StringBuilder ();
            for (int pos = 0; pos < lines.Length; pos++) {
                string line = lines [pos];
                string cleanLine = line.Trim ();
                if (cleanLine.StartsWith ("<!--#include file=", StringComparison.Ordinal)) {
                    string [] split = line.Split (new string [] { "<!--#include file=\"", "\" -->" },
                                                StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < split.Length; i += 2) {
                        string filename = GetFileNameFromHTMLPath (split [i], request.Query);
                        if (filename != null) {
                            string response;
                            Dictionary<string, object> newVars = AddVarsForPage (filename, originalFileName,
                                                                     request, httpResponse, requestParameters,
                                                                     out response);
                            AddDefaultVarsForPage (ref newVars);
                            sb.AppendLine (ConvertHTML (filename, File.ReadAllText (filename),
                                request, httpResponse, requestParameters, newVars));
                        }
                    }
                } else if (cleanLine.StartsWith ("<!--#include folder=", StringComparison.Ordinal)) {
                    string [] split = line.Split (new string [] { "<!--#include folder=\"", "\" -->" },
                                                StringSplitOptions.RemoveEmptyEntries);
                    for (int i = split.Length % 2 == 0 ? 0 : 1; i < split.Length; i += 2) {
                        string filename = GetFileNameFromHTMLPath (split [i], request.Query).Replace ("index.html", "");
                        if (filename != null) {
                            if (Directory.Exists (filename)) {
                                string response;
                                Dictionary<string, object> newVars = AddVarsForPage (filename, filename, request,
                                                                     httpResponse,
                                                                     requestParameters, out response);
                                string [] files = Directory.GetFiles (filename);
                                foreach (string f in files) {
                                    if (!f.EndsWith (".html", StringComparison.Ordinal))
                                        continue;

                                    Dictionary<string, object> newVars2 =
                                        AddVarsForPage (f, filename, request, httpResponse, requestParameters, out response) ??
                                        new Dictionary<string, object> ();
                                    foreach (
                                    KeyValuePair<string, object> pair in
                                        newVars.Where (pair => !newVars2.ContainsKey (pair.Key)))
                                        newVars2.Add (pair.Key, pair.Value);
                                    AddDefaultVarsForPage (ref newVars2);
                                    sb.AppendLine (ConvertHTML (f, File.ReadAllText (f), request, httpResponse,
                                        requestParameters, newVars2));
                                }
                            }
                        }
                    }
                } else if (cleanLine.StartsWith ("{", StringComparison.Ordinal)) {
                    int indBegin, indEnd;
                    if ((indEnd = cleanLine.IndexOf ("ArrayBegin}", StringComparison.Ordinal)) != -1) {
                        string keyToCheck = cleanLine.Substring (1, indEnd - 1);
                        int posToCheckFrom;
                        List<string> repeatedLines = ExtractLines (lines, pos, keyToCheck, "ArrayEnd", out posToCheckFrom);
                        pos = posToCheckFrom;
                        if (vars.ContainsKey (keyToCheck)) {
                            List<Dictionary<string, object>> dicts =
                                vars [keyToCheck] as List<Dictionary<string, object>>;
                            if (dicts != null)
                                foreach (var dict in dicts)
                                    sb.AppendLine (ConvertHTML (originalFileName,
                                                              string.Join ("\n", repeatedLines.ToArray ()), request,
                                                              httpResponse, requestParameters, dict));
                        }
                    } else if ((indEnd = cleanLine.IndexOf ("AuthenticatedBegin}", StringComparison.Ordinal)) != -1) {
                        string key = cleanLine.Substring (1, indEnd - 1) + "AuthenticatedEnd";
                        int posToCheckFrom = FindLines (lines, pos, "", key);
                        if (!CheckAuth (cleanLine, request))
                            pos = posToCheckFrom;
                    } else if ((indBegin = cleanLine.IndexOf ("{If", StringComparison.Ordinal)) != -1 &&
                               (indEnd = cleanLine.IndexOf ("Begin}", StringComparison.Ordinal)) != -1) {
                        string key = cleanLine.Substring (indBegin + 3, indEnd - indBegin - 3);
                        int posToCheckFrom = FindLines (lines, pos, "If" + key, "End");
                        if (!vars.ContainsKey (key) || (!(bool)vars [key]))
                            pos = posToCheckFrom;
                    } else if ((cleanLine.IndexOf ("{If", StringComparison.Ordinal)) != -1 &&
                               (cleanLine.IndexOf ("End}", StringComparison.Ordinal)) != -1) {
                        //end of an if statement, just ignore it
                    } else if ((cleanLine.IndexOf ("{Is", StringComparison.Ordinal)) != -1 &&
                               (cleanLine.IndexOf ("End}", StringComparison.Ordinal)) != -1) {
                        //end of an is statement, just ignore it
                    } else
                        sb.AppendLine (line);
                } else
                    sb.AppendLine (line);
            }

            return sb.ToString ();
        }

        /// <summary>
        ///     Returns false if the authentication was wrong
        /// </summary>
        /// <param name="p"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        bool CheckAuth (string p, OSHttpRequest request)
        {
            if (p.StartsWith ("{IsAuthenticatedBegin}", StringComparison.Ordinal))
                return Authenticator.CheckAuthentication (request);

            if (p.StartsWith ("{IsNotAuthenticatedBegin}", StringComparison.Ordinal))
                return !Authenticator.CheckAuthentication (request);

            if (p.StartsWith ("{IsAdminAuthenticatedBegin}", StringComparison.Ordinal))
                return Authenticator.CheckAdminAuthentication (request);

            if (p.StartsWith ("{IsNotAdminAuthenticatedBegin}", StringComparison.Ordinal))
                return !Authenticator.CheckAdminAuthentication (request);

            return false;
        }

        static int FindLines (string [] lines, int pos, string keyToCheck, string type)
        {
            int posToCheckFrom = pos + 1;
            while (!lines [posToCheckFrom++].TrimStart ().StartsWith ("{" + keyToCheck + type + "}", StringComparison.Ordinal))
                continue;

            return posToCheckFrom - 1;
        }

        static List<string> ExtractLines (string [] lines, int pos,
                                                 string keyToCheck, string type, out int posToCheckFrom)
        {
            posToCheckFrom = pos + 1;
            List<string> repeatedLines = new List<string> ();
            while (!lines [posToCheckFrom].Trim ().StartsWith ("{" + keyToCheck + type + "}", StringComparison.Ordinal))
                repeatedLines.Add (lines [posToCheckFrom++]);
            return repeatedLines;
        }

        protected string GetContentType (string filename, OSHttpResponse response)
        {
            var setCache = true;    // default is to cache
            var mimeType = "";

            var ext = Path.GetExtension (filename);
            switch (ext) {
            case ".jpeg":
            case ".jpg":
                mimeType = "image/jpeg";
                break;
            case ".gif":
                mimeType = "image/gif";
                break;
            case ".png":
                mimeType = "image/png";
                break;
            case ".tiff":
                mimeType = "image/tiff";
                break;
            case ".woff":
                mimeType = "application/font-woff";
                break;
            case ".woff2":
                mimeType = "application/font-woff2";
                break;
            case ".ttf":
                mimeType = "application/font-ttf";
                break;
            case ".css":
                setCache = !filename.StartsWith ("styles", StringComparison.Ordinal);
                mimeType = "text/css";
                break;
            case ".html":
            case ".htm":
            case ".xsl":
                setCache = false;
                mimeType = "text/html";
                break;
            case ".js":
                setCache = !filename.Contains ("menu");     // must not cache menu generation
                mimeType = "application/javascript";
                break;
            default:
                mimeType = "text/plain";
                break;
            }

            if (setCache)
                response.AddHeader ("Cache-Control", "public, max-age=" + CLIENT_CACHE_TIME);
            else
                response.AddHeader ("Cache-Control", "no-cache");

            return mimeType;
        }

        protected string GetFileNameFromHTMLPath (string path, Hashtable query)
        {
            try {
                string filePath = path.StartsWith ("/", StringComparison.Ordinal)
                                      ? path.Remove (0, 1) 
                                      : path;
                filePath = filePath.IndexOf ('?') >= 0 ? filePath.Substring (0, filePath.IndexOf ('?')) : filePath;

                if (filePath == "")
                    filePath = "index.html";
                if (filePath [filePath.Length - 1] == '/')
                    filePath = filePath + "index.html";

                string file;
                if (filePath.StartsWith ("local/", StringComparison.Ordinal))                      // local included files 
                {
                    file = Path.Combine (m_localHtmlPath, filePath.Remove (0, 6));
                }
                else {                                                    // 'normal' page processing

                    // try for files in the user data path first
                    file = Path.Combine (m_localHtmlPath, filePath);
                    if (Path.GetFileName (file) == "") {
                        file = Path.Combine (file, "index.html");
                        MainConsole.Instance.Info ("Using the Data/html page");
                    }

                    if (!File.Exists (file)) {
                        // use the default pages
                        //MainConsole.Instance.Info ("Using the bin page");
                        file = Path.Combine ("html/", filePath);
                        if (!Path.GetFullPath (file).StartsWith (Path.GetFullPath ("html/"), StringComparison.Ordinal)) {
                            MainConsole.Instance.Info ("Using the Data/html page");
                            return "html/index.html";
                        }
                        if (Path.GetFileName (file) == "")
                            file = Path.Combine (file, "index.html");
                    }

                    if (query.ContainsKey ("page") && _pages.ContainsKey ("html/" + query ["page"] + ".html")) {
                        file = _pages ["html/" + query ["page"] + ".html"].FilePath [0];
                    }
                }
                if (!File.Exists (file)) {
                    MainConsole.Instance.DebugFormat ("WebInterface]: Unknown page request, {0}", file);
                    return "html/http_404.html";
                }

                return file;
            } catch {
                return "html/http_404.html";
            }
        }


        public static Dictionary<string, object> ParseQueryString (string query)
        {
            Dictionary<string, object> result = new Dictionary<string, object> ();
            string [] terms = query.Split (new [] { '&' });

            if (terms.Length == 0)
                return result;

            foreach (string t in terms) {
                string [] elems = t.Split (new [] { '=' });
                if (elems.Length == 0)
                    continue;

                string name = HttpUtility.UrlDecode (elems [0]);
                string value = string.Empty;

                if (elems.Length > 1)
                    value = HttpUtility.UrlDecode (elems [1]);

                if (name.EndsWith ("[]", StringComparison.Ordinal)) {
                    string cleanName = name.Substring (0, name.Length - 2);
                    if (result.ContainsKey (cleanName)) {
                        if (!(result [cleanName] is List<string>))
                            continue;

                        List<string> l = (List<string>)result [cleanName];

                        l.Add (value);
                    } else {
                        List<string> newList = new List<string> { value };

                        result [cleanName] = newList;
                    }
                } else {
                    if (!result.ContainsKey (name))
                        result [name] = value;
                }
            }

            return result;
        }



        internal GridPage GetGridPages ()
        {
            if (webPages == null) {
                IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
                GridPage rootPage = generics.GetGeneric<GridPage> (UUID.Zero, "WebPages", "Root");
                if (rootPage == null)
                    rootPage = new GridPage ();

                return rootPage;
            }

            return webPages;
        }

        internal WebUISettings GetWebUISettings ()
        {
            if (webUISettings == null) {
                IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
                var settings = generics.GetGeneric<WebUISettings> (UUID.Zero, "WebUISettings", "Settings");
                if (settings == null) {
                    settings = new WebUISettings ();

                    var simbase = Registry.RequestModuleInterface<ISimulationBase> ();
                    settings.MapCenter.X = simbase.MapCenterX;
                    settings.MapCenter.Y = simbase.MapCenterY;
                }
                return settings;
            }

            return webUISettings;
        }

        internal void SaveWebUISettings (WebUISettings settings)
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
            generics.AddGeneric (UUID.Zero, "WebUISettings", "Settings", settings.ToOSD ());

            webUISettings = settings;
        }

        public GridSettings GetGridSettings ()
        {
            if (gridSettings == null) {
                IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
                var settings = generics.GetGeneric<GridSettings> (UUID.Zero, "GridSettings", "Settings");
                if (settings == null)
                    settings = new GridSettings ();

                return settings;
            }

            return gridSettings;
        }

        public void SaveGridSettings (GridSettings settings)
        {
            IGenericsConnector generics = Framework.Utilities.DataManager.RequestPlugin<IGenericsConnector> ();
            generics.AddGeneric (UUID.Zero, "GridSettings", "Settings", settings.ToOSD ());

            gridSettings = settings;

            // change what's appropriate...
            ILoginService loginService = Registry.RequestModuleInterface<ILoginService> ();
            loginService.WelcomeMessage = settings.WelcomeMessage;

        }


        #endregion

        internal void Redirect (OSHttpResponse httpResponse, string url)
        {
            httpResponse.StatusCode = (int)HttpStatusCode.Redirect;
            httpResponse.AddHeader ("Location", url);
            httpResponse.KeepAlive = false;
        }
    }

    class GridNewsItem : IDataTransferable
    {
        public static readonly GridNewsItem NoNewsItem = new GridNewsItem () {
            ID = -1,
            Text = "No news to report",
            Time = DateTime.Now,
            Title = "No news to report"
        };

        public string Title;
        public string Text;
        public DateTime Time;
        public int ID;

        public override OSDMap ToOSD ()
        {
            OSDMap map = new OSDMap ();
            map ["Title"] = Title;
            map ["Text"] = Text;
            map ["Time"] = Time;
            map ["ID"] = ID;
            return map;
        }

        public override void FromOSD (OSDMap map)
        {
            Title = map ["Title"];
            Text = map ["Text"];
            Time = map ["Time"];
            ID = map ["ID"];
        }

        public Dictionary<string, object> ToDictionary ()
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object> ();

            //dictionary.Add("NewsDate", Time.ToShortDateString());
            dictionary.Add ("NewsDate", Culture.LocaleDate (Time));
            dictionary.Add ("NewsTitle", Title);
            dictionary.Add ("NewsText", Text);
            dictionary.Add ("NewsID", ID);

            return dictionary;
        }
    }

    class GridWelcomeScreen : IDataTransferable
    {
        public static readonly GridWelcomeScreen Default = new GridWelcomeScreen {
            SpecialWindowMessageTitle = "Nothing to report at this time.",
            SpecialWindowMessageText = "Grid is up and running.",
            SpecialWindowMessageColor = "white",
            SpecialWindowActive = true,
            GridStatus = true
        };

        public string SpecialWindowMessageTitle;
        public string SpecialWindowMessageText;
        public string SpecialWindowMessageColor;
        public bool SpecialWindowActive;
        public bool GridStatus;

        public override OSDMap ToOSD ()
        {
            OSDMap map = new OSDMap ();
            map ["SpecialWindowMessageTitle"] = SpecialWindowMessageTitle;
            map ["SpecialWindowMessageText"] = SpecialWindowMessageText;
            map ["SpecialWindowMessageColor"] = SpecialWindowMessageColor;
            map ["SpecialWindowActive"] = SpecialWindowActive;
            map ["GridStatus"] = GridStatus;
            return map;
        }

        public override void FromOSD (OSDMap map)
        {
            SpecialWindowMessageTitle = map ["SpecialWindowMessageTitle"];
            SpecialWindowMessageText = map ["SpecialWindowMessageText"];
            SpecialWindowMessageColor = map ["SpecialWindowMessageColor"];
            SpecialWindowActive = map ["SpecialWindowActive"];
            GridStatus = map ["GridStatus"];
        }
    }

    class GridPage : IDataTransferable
    {
        public List<GridPage> Children = new List<GridPage> ();
        public bool ShowInMenu = false;
        public int MenuPosition = -1;
        public string MenuID = "";
        public string MenuTitle = "";
        public string MenuToolTip = "";
        public string Location = "";
        public bool LoggedInRequired = false;
        public bool LoggedOutRequired = false;
        public bool AdminRequired = false;
        public int AdminLevelRequired = 1;

        public GridPage ()
        {
        }

        public GridPage (OSD map)
        {
            var mp = (OSDMap) map;

            ShowInMenu = mp ["ShowInMenu"];
            MenuPosition = mp ["MenuPosition"];
            MenuID = mp ["MenuID"];
            MenuTitle = mp ["MenuTitle"];
            MenuToolTip = mp ["MenuToolTip"];
            Location = mp ["Location"];
            LoggedInRequired = mp ["LoggedInRequired"];
            LoggedOutRequired = mp ["LoggedOutRequired"];
            AdminRequired = mp ["AdminRequired"];
            AdminLevelRequired = mp ["AdminLevelRequired"];
            Children = ((OSDArray)mp ["Children"]).ConvertAll (o => new GridPage (o));

        }

        public override void FromOSD (OSDMap map)
        {
            ShowInMenu = map ["ShowInMenu"];
            MenuPosition = map ["MenuPosition"];
            MenuID = map ["MenuID"];
            MenuTitle = map ["MenuTitle"];
            MenuToolTip = map ["MenuToolTip"];
            Location = map ["Location"];
            LoggedInRequired = map ["LoggedInRequired"];
            LoggedOutRequired = map ["LoggedOutRequired"];
            AdminRequired = map ["AdminRequired"];
            AdminLevelRequired = map ["AdminLevelRequired"];
            Children = ((OSDArray)map ["Children"]).ConvertAll (o => new GridPage (o));
        }

        public override OSDMap ToOSD ()
        {
            OSDMap map = new OSDMap ();

            map ["ShowInMenu"] = ShowInMenu;
            map ["MenuPosition"] = MenuPosition;
            map ["MenuID"] = MenuID;
            map ["MenuTitle"] = MenuTitle;
            map ["MenuToolTip"] = MenuToolTip;
            map ["Location"] = Location;
            map ["LoggedInRequired"] = LoggedInRequired;
            map ["LoggedOutRequired"] = LoggedOutRequired;
            map ["AdminRequired"] = AdminRequired;
            map ["AdminLevelRequired"] = AdminLevelRequired;
            map ["Children"] = Children.ToOSDArray ();
            return map;
        }

        public GridPage GetPage (string item)
        {
            return GetPage (item, null);
        }

        public GridPage GetPage (string item, GridPage rootPage)
        {
            if (rootPage == null)
                rootPage = this;
            foreach (var page in rootPage.Children) {
                if (page.MenuID == item)
                    return page;

                if (page.Children.Count > 0) {
                    var p = GetPage (item, page);
                    if (p != null)
                        return p;
                }
            }
            return null;
        }

        public GridPage GetPageByLocation (string item)
        {
            return GetPageByLocation (item, null);
        }

        public GridPage GetPageByLocation (string item, GridPage rootPage)
        {
            if (rootPage == null)
                rootPage = this;
            foreach (var page in rootPage.Children) {
                if (page.Location == item)
                    return page;

                if (page.Children.Count > 0) {
                    var p = GetPageByLocation (item, page);
                    if (p != null)
                        return p;
                }
            }
            return null;
        }

        public void ReplacePage (string menuItem, GridPage replacePage)
        {
            foreach (var page in Children) {
                if (page.MenuID == menuItem) {
                    page.FromOSD (replacePage.ToOSD ());
                    return;
                }

                if (page.Children.Count > 0) {
                    var p = GetPage (menuItem, page);
                    if (p != null) {
                        p.FromOSD (replacePage.ToOSD ());
                        return;
                    }
                }
            }
        }

        public void RemovePage (string MenuID, GridPage replacePage)
        {
            GridPage foundPage = null;
            foreach (var page in Children) {
                if (page.MenuID == MenuID) {
                    foundPage = page;
                    break;
                }

                if (page.Children.Count > 0) {
                    var p = GetPage (MenuID, page);
                    if (p != null) {
                        page.Children.Remove (p);
                        return;
                    }
                }
            }
            if (foundPage != null)
                Children.Remove (foundPage);
        }

        public void RemovePageByLocation (string menuLocation, GridPage replacePage)
        {
            GridPage foundPage = null;
            foreach (var page in Children) {
                if (page.Location == menuLocation) {
                    foundPage = page;
                    break;
                }

                if (page.Children.Count > 0) {
                    var p = GetPageByLocation (menuLocation, page);
                    if (p != null) {
                        page.Children.Remove (p);
                        return;
                    }
                }
            }
            if (foundPage != null)
                Children.Remove (foundPage);
        }

        public GridPage GetParent (GridPage page)
        {
            return GetParent (page, null);
        }

        public GridPage GetParent (GridPage item, GridPage toCheck)
        {
            if (toCheck == null)
                toCheck = this;
            foreach (var p in toCheck.Children) {
                if (item.Location == p.Location)
                    return toCheck;

                if (p.Children.Count > 0) {
                    var pp = GetParent (item, p);
                    if (pp != null)
                        return pp;
                }
            }
            return null;
        }
    }

    public class GridSettings : IDataTransferable
    {
        public string Gridname = "WhiteCore Grid";
        public string Gridnick = "WhiteCore";
        public string WelcomeMessage = "Welcome to WhiteCore, <USERNAME>!";
        public string GovernorName = Constants.GovernorName;
        public string RealEstateOwnerName = Constants.RealEstateOwnerName;
        public string BankerName = Constants.BankerName;
        public string MarketplaceOwnerName = Constants.MarketplaceOwnerName;
        public string MainlandEstateName = Constants.MainlandEstateName;
        public string SystemEstateName = Constants.SystemEstateName;

        public GridSettings ()
        {
        }

        public GridSettings (OSD map)
        {
            var mp = (OSDMap)map;

            Gridname = mp ["Gridname"];
            Gridnick = mp ["Gridnick"];
            WelcomeMessage = mp ["WelcomeMessage"];
            GovernorName = mp ["GovernorName"];
            RealEstateOwnerName = mp ["RealEstateOwnerName"];
            BankerName = mp ["BankerName"];
            MarketplaceOwnerName = mp ["MarketplaceOwnerName"];
            MainlandEstateName = mp ["MainlandEstateName"];
            SystemEstateName = mp ["SystemEstateName"];
        }

        public override void FromOSD (OSDMap map)
        {
            Gridname = map ["Gridname"];
            Gridnick = map ["Gridnick"];
            WelcomeMessage = map ["WelcomeMessage"];
            GovernorName = map ["GovernorName"];
            RealEstateOwnerName = map ["RealEstateOwnerName"];
            BankerName = map ["BankerName"];
            MarketplaceOwnerName = map ["MarketplaceOwnerName"];
            MainlandEstateName = map ["MainlandEstateName"];
            SystemEstateName = map ["SystemEstateName"];
        }

        public override OSDMap ToOSD ()
        {
            OSDMap map = new OSDMap ();

            map ["Gridname"] = Gridname;
            map ["Gridnick"] = Gridnick;
            map ["WelcomeMessage"] = WelcomeMessage;
            map ["GovernorName"] = GovernorName;
            map ["RealEstateOwnerName"] = RealEstateOwnerName;
            map ["BankerName"] = BankerName;
            map ["MarketplaceOwnerName"] = MarketplaceOwnerName;
            map ["MainlandEstateName"] = MainlandEstateName;
            map ["SystemEstateName"] = SystemEstateName;

            return map;
        }
    }

    class WebUISettings : IDataTransferable
    {
        public Vector2 MapCenter = Vector2.Zero;
        public uint LastPagesVersionUpdateIgnored = 0;
        public uint LastSettingsVersionUpdateIgnored = 0;
        public bool HideLanguageTranslatorBar = false;
        public bool HideStyleBar = false;
        public UUID DefaultScopeID = UUID.Zero;
        public bool WebRegistration = false;
        public bool HideSlideshowBar = false;
        public string LocalFrontPage = "local/frontpage.html";
        public string LocalCSS = "local/";

        public WebUISettings ()
        {
            MapCenter.X = Constants.DEFAULT_REGIONSTART_X;
            MapCenter.Y = Constants.DEFAULT_REGIONSTART_Y;
        }

        public WebUISettings (OSD map)
        {
            var mp = (OSDMap)map;

            MapCenter = mp ["MapCenter"];
            LastPagesVersionUpdateIgnored = mp ["LastPagesVersionUpdateIgnored"];
            LastSettingsVersionUpdateIgnored = mp ["LastSettingsVersionUpdateIgnored"];
            HideLanguageTranslatorBar = mp ["HideLanguageTranslatorBar"];
            HideStyleBar = mp ["HideStyleBar"];
            DefaultScopeID = mp ["DefaultScopeID"];
            WebRegistration = mp ["WebRegistration"];
            HideSlideshowBar = mp ["HideSlideshowBar"];
            LocalFrontPage = mp ["LocalFrontPage"];
            LocalCSS = mp ["LocalCSS"];
        }

        public override void FromOSD (OSDMap map)
        {
            MapCenter = map ["MapCenter"];
            LastPagesVersionUpdateIgnored = map ["LastPagesVersionUpdateIgnored"];
            LastSettingsVersionUpdateIgnored = map ["LastSettingsVersionUpdateIgnored"];
            HideLanguageTranslatorBar = map ["HideLanguageTranslatorBar"];
            HideStyleBar = map ["HideStyleBar"];
            DefaultScopeID = map ["DefaultScopeID"];
            WebRegistration = map ["WebRegistration"];
            HideSlideshowBar = map ["HideSlideshowBar"];
            LocalFrontPage = map ["LocalFrontPage"];
            LocalCSS = map ["LocalCSS"];
        }

        public override OSDMap ToOSD ()
        {
            OSDMap map = new OSDMap ();

            map ["MapCenter"] = MapCenter;
            map ["LastPagesVersionUpdateIgnored"] = LastPagesVersionUpdateIgnored;
            map ["LastSettingsVersionUpdateIgnored"] = LastSettingsVersionUpdateIgnored;
            map ["HideLanguageTranslatorBar"] = HideLanguageTranslatorBar;
            map ["HideStyleBar"] = HideStyleBar;
            map ["DefaultScopeID"] = DefaultScopeID;
            map ["WebRegistration"] = WebRegistration;
            map ["HideSlideshowBar"] = HideSlideshowBar;
            map ["LocalFrontPage"] = LocalFrontPage;
            map ["LocalCSS"] = LocalCSS;

            return map;
        }
    }

}
