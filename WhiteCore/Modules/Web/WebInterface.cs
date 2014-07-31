﻿/*
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
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
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

namespace WhiteCore.Modules.Web
{
    public class WebInterface : IService, IWebInterfaceModule
    {
        #region Declares

        protected const int CLIENT_CACHE_TIME = 86400;  // 1 day
        protected uint _port = 8002;                    // assuming grid mode here
        protected bool _enabled = true;
        protected Dictionary<string, IWebInterfacePage> _pages = new Dictionary<string, IWebInterfacePage>();
        protected List<ITranslator> _translators = new List<ITranslator>();
        protected ITranslator _defaultTranslator;

        #endregion

        #region Public Properties

        public IRegistryCore Registry { get; protected set; }

        public string GridName { get; private set; }

        public string LoginScreenURL
        {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/welcomescreen/"; }
        }

        public string WebProfileURL
        {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/webprofile/"; }
        }

        public string RegistrationScreenURL
        {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/register.html"; }
        }

        public string ForgotPasswordScreenURL
        {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/forgot_pass.html"; }
        }

        public string HelpScreenURL
        {
            get { return MainServer.Instance.FullHostName + ":" + _port + "/help.html"; }
        }

        public string GridURL
        {
            get { return MainServer.Instance.HostName + ":" + _port; }
        }

        public ITranslator EnglishTranslator
        {
            get { return _translators.FirstOrDefault (t => t.LanguageName == "en"); }
        }

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            Registry = registry;

            var webPages = WhiteCoreModuleLoader.PickupModules<IWebInterfacePage>();
            foreach (var pages in webPages)
            {
                foreach (var page in pages.FilePath)
                {
                    _pages.Add(page, pages);
                }
            }

            _translators = WhiteCoreModuleLoader.PickupModules<ITranslator>();
            _defaultTranslator = _translators[0];
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            IConfig con = config.Configs["WebInterface"];
            if (con != null)
            {
                _enabled = con.GetString("Module", "BuiltIn") == "BuiltIn";

                var webPort = con.GetUInt("Port", 0);
                if (webPort == 0)                               // use default
                    _port = MainServer.Instance.Port;
                else
                    _port = webPort;                            // user defined

                string defaultLanguage = con.GetString("DefaultLanguage", "en");
                _defaultTranslator = _translators.FirstOrDefault(t => t.LanguageName == defaultLanguage);
                if (_defaultTranslator == null)
                    _defaultTranslator = _translators[0];
            }
            if (_enabled)
            {
                Registry.RegisterModuleInterface<IWebInterfaceModule>(this);
                var server = registry.RequestModuleInterface<ISimulationBase>().GetHttpServer(_port);
                server.AddStreamHandler(new GenericStreamHandler("GET", "/", FindAndSendPage));
                server.AddStreamHandler(new GenericStreamHandler("POST", "/", FindAndSendPage));
            }
        }

        public void FinishedStartup()
        {
            if (_enabled)
            {
                IGridInfo gridInfo = Registry.RequestModuleInterface<IGridInfo>();
                GridName = gridInfo.GridName;

                if (PagesMigrator.RequiresInitialUpdate())
                    PagesMigrator.ResetToDefaults();
                if (SettingsMigrator.RequiresInitialUpdate())
                    SettingsMigrator.ResetToDefaults();
            }
        }

        #endregion

        #region Page Sending

        public IWebInterfacePage GetPage(string path)
        {
            IWebInterfacePage page;
            string directory = string.Join("/", path.Split('/'), 0, path.Split('/').Length - 1) + "/";
            if (!_pages.TryGetValue(path, out page) &&
                !_pages.TryGetValue(directory, out page))
                page = null;
            return page;
        }

        protected byte[] FindAndSendPage(string path, Stream request, OSHttpRequest httpRequest,
                                         OSHttpResponse httpResponse)
        {
            byte[] response = MainServer.BlankResponse;
            string filename = GetFileNameFromHTMLPath(path, httpRequest.Query);
            if (filename == null)
                return MainServer.BlankResponse;
            if (httpRequest.HttpMethod == "POST")
                httpResponse.KeepAlive = false;
            MainConsole.Instance.Debug("[WebInterface]: Serving " + filename + ", keep-alive: " + httpResponse.KeepAlive);
            IWebInterfacePage page = GetPage(filename);
            if (page != null)
            {
                httpResponse.ContentType = GetContentType(filename, httpResponse);
                string text;
                if (!File.Exists(filename))
                {
                    if (!page.AttemptFindPage(filename, ref httpResponse, out text))
                        return MainServer.BadRequest;
                }
                else
                    text = File.ReadAllText(filename);

                var requestParameters = request != null
                                            ? ParseQueryString(HttpServerHandlerHelpers.ReadString(request))
                                            : new Dictionary<string, object>();
                if (filename.EndsWith(".xsl"))
                {
                    WhiteCoreXmlDocument vars = GetXML(filename, httpRequest, httpResponse, requestParameters);

                    var xslt = new XslCompiledTransform();
                    if (File.Exists(path)) xslt.Load(GetFileNameFromHTMLPath(path, httpRequest.Query));
                    else if (text != "")
                    {
                        xslt.Load(new XmlTextReader(new StringReader(text)));
                    }
                    var stm = new MemoryStream();
                    xslt.Transform(vars, null, stm);
                    stm.Position = 1;
                    var sr = new StreamReader(stm);
                    string results = sr.ReadToEnd().Trim();
                    return Encoding.UTF8.GetBytes(Regex.Replace(results, @"[^\u0000-\u007F]", string.Empty));
                }
                else
                {
                    string respStr = null;
                    var vars = AddVarsForPage(filename, filename, httpRequest,
                                              httpResponse, requestParameters, out respStr);

                    AddDefaultVarsForPage(ref vars);

                    if (!string.IsNullOrEmpty(respStr))
                        return response = Encoding.UTF8.GetBytes(respStr);
                    if (httpResponse.StatusCode != 200)
                        return MainServer.BlankResponse;
                    if (vars == null)
                        return MainServer.BadRequest;
                    response = Encoding.UTF8.GetBytes(ConvertHTML(filename, text, httpRequest, httpResponse,
                                                                  requestParameters, vars));
                }
            }
            else
            {
                httpResponse.ContentType = GetContentType(filename, httpResponse);
                if (httpResponse.ContentType == null || !File.Exists(filename))
                    return MainServer.BadRequest;
                response = File.ReadAllBytes(filename);
            }
            return response;
        }

        #endregion

        #region Helpers

        protected void AddDefaultVarsForPage(ref Dictionary<string, object> vars)
        {
            if (vars != null)
            {
                vars.Add("SystemURL", MainServer.Instance.FullHostName + ":" + _port);
                vars.Add("SystemName", GridName);
            }
        }

        protected Dictionary<string, object> AddVarsForPage(string filename, string parentFileName,
                                                            OSHttpRequest httpRequest, OSHttpResponse httpResponse,
                                                            Dictionary<string, object> requestParameters,
                                                            out string response)
        {
            response = null;
            Dictionary<string, object> vars = new Dictionary<string, object>();
            IWebInterfacePage page = GetPage(filename);
            if (page != null)
            {
                ITranslator translator = null;
                if (httpRequest.Query.ContainsKey("language"))
                {
                    translator =
                        _translators.FirstOrDefault(t => t.LanguageName == httpRequest.Query["language"].ToString());
                    httpResponse.AddCookie(new System.Web.HttpCookie("language",
                                                                     httpRequest.Query["language"].ToString()));
                }
                else if (httpRequest.Cookies.Get("language") != null)
                {
                    var cookie = httpRequest.Cookies.Get("language");
                    translator = _translators.FirstOrDefault(t => t.LanguageName == cookie.Value);
                }
                if (translator == null)
                    translator = _defaultTranslator;

                if (page.RequiresAuthentication)
                {
                    if (!Authenticator.CheckAuthentication(httpRequest))
                        return null;
                }
                if (page.RequiresAdminAuthentication)
                {
                    if (!Authenticator.CheckAdminAuthentication(httpRequest))
                        return null;
                }
                vars = page.Fill(this, parentFileName, httpRequest, httpResponse, requestParameters, translator,
                                 out response);
                return vars;
            }
            return null;
        }

        private WhiteCoreXmlDocument GetXML(string filename, OSHttpRequest httpRequest, OSHttpResponse httpResponse,
                                         Dictionary<string, object> requestParameters)
        {
            IWebInterfacePage page = GetPage(filename);
            if (page != null)
            {
                ITranslator translator = null;
                if (httpRequest.Query.ContainsKey("language"))
                    translator =
                        _translators.FirstOrDefault(t => t.LanguageName == httpRequest.Query["language"].ToString());
                if (translator == null)
                    translator = _defaultTranslator;

                if (page.RequiresAuthentication)
                {
                    if (!Authenticator.CheckAuthentication(httpRequest))
                        return null;
                    if (page.RequiresAdminAuthentication)
                    {
                        if (!Authenticator.CheckAdminAuthentication(httpRequest))
                            return null;
                    }
                }
                string response = null;
                return
                    (WhiteCoreXmlDocument)
                    page.Fill(this, filename, httpRequest, httpResponse, requestParameters, translator, out response)[
                        "xml"];
            }
            return null;
        }

        protected string ConvertHTML(string originalFileName, string file, OSHttpRequest request,
                                     OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                     Dictionary<string, object> vars)
        {
            string html = CSHTMLCreator.BuildHTML(file, vars);

            string[] lines = html.Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int pos = 0; pos < lines.Length; pos++)
            {
                string line = lines[pos];
                string cleanLine = line.Trim();
                if (cleanLine.StartsWith("<!--#include file="))
                {
                    string[] split = line.Split(new string[2] {"<!--#include file=\"", "\" -->"},
                                                StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < split.Length; i += 2)
                    {
                        string filename = GetFileNameFromHTMLPath(split[i], request.Query);
                        string response = null;
                        Dictionary<string, object> newVars = AddVarsForPage(filename, originalFileName,
                                                                            request, httpResponse, requestParameters,
                                                                            out response);
                        AddDefaultVarsForPage(ref newVars);
                        sb.AppendLine(ConvertHTML(filename, File.ReadAllText(filename),
                                                  request, httpResponse, requestParameters, newVars));
                    }
                }
                else if (cleanLine.StartsWith("<!--#include folder="))
                {
                    string[] split = line.Split(new string[2] {"<!--#include folder=\"", "\" -->"},
                                                StringSplitOptions.RemoveEmptyEntries);
                    for (int i = split.Length%2 == 0 ? 0 : 1; i < split.Length; i += 2)
                    {
                        string filename = GetFileNameFromHTMLPath(split[i], request.Query).Replace("index.html", "");
                        if (Directory.Exists(filename))
                        {
                            string response = null;
                            Dictionary<string, object> newVars = AddVarsForPage(filename, filename, request,
                                                                                httpResponse,
                                                                                requestParameters, out response);
                            string[] files = Directory.GetFiles(filename);
                            foreach (string f in files)
                            {
                                if (!f.EndsWith(".html")) continue;
                                Dictionary<string, object> newVars2 =
                                    AddVarsForPage(f, filename, request, httpResponse, requestParameters, out response) ??
                                    new Dictionary<string, object>();
                                foreach (
                                    KeyValuePair<string, object> pair in
                                        newVars.Where(pair => !newVars2.ContainsKey(pair.Key)))
                                    newVars2.Add(pair.Key, pair.Value);
                                AddDefaultVarsForPage(ref newVars2);
                                sb.AppendLine(ConvertHTML(f, File.ReadAllText(f), request, httpResponse,
                                                          requestParameters, newVars2));
                            }
                        }
                    }
                }
                else if (cleanLine.StartsWith("{"))
                {
                    int indBegin, indEnd;
                    if ((indEnd = cleanLine.IndexOf("ArrayBegin}")) != -1)
                    {
                        string keyToCheck = cleanLine.Substring(1, indEnd - 1);
                        int posToCheckFrom;
                        List<string> repeatedLines = ExtractLines(lines, pos, keyToCheck, "ArrayEnd", out posToCheckFrom);
                        pos = posToCheckFrom;
                        if (vars.ContainsKey(keyToCheck))
                        {
                            List<Dictionary<string, object>> dicts =
                                vars[keyToCheck] as List<Dictionary<string, object>>;
                            if (dicts != null)
                                foreach (var dict in dicts)
                                    sb.AppendLine(ConvertHTML(originalFileName,
                                                              string.Join("\n", repeatedLines.ToArray()), request,
                                                              httpResponse, requestParameters, dict));
                        }
                    }
                    else if ((indEnd = cleanLine.IndexOf("AuthenticatedBegin}")) != -1)
                    {
                        string key = cleanLine.Substring(1, indEnd - 1) + "AuthenticatedEnd";
                        int posToCheckFrom = FindLines(lines, pos, "", key);
                        if (!CheckAuth(cleanLine, request))
                            pos = posToCheckFrom;
                    }
                    else if ((indBegin = cleanLine.IndexOf("{If")) != -1 &&
                             (indEnd = cleanLine.IndexOf("Begin}")) != -1)
                    {
                        string key = cleanLine.Substring(indBegin + 3, indEnd - indBegin - 3);
                        int posToCheckFrom = FindLines(lines, pos, "If" + key, "End");
                        if (!vars.ContainsKey(key) || ((bool) vars[key]) == false)
                            pos = posToCheckFrom;
                    }
                    else if ((indBegin = cleanLine.IndexOf("{If")) != -1 &&
                             (indEnd = cleanLine.IndexOf("End}")) != -1)
                    {
                        //end of an if statement, just ignore it
                    }
                    else if ((indBegin = cleanLine.IndexOf("{Is")) != -1 &&
                             (indEnd = cleanLine.IndexOf("End}")) != -1)
                    {
                        //end of an if statement, just ignore it
                    }
                    else
                        sb.AppendLine(line);
                }
                else
                    sb.AppendLine(line);
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Returns false if the authentication was wrong
        /// </summary>
        /// <param name="p"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private bool CheckAuth(string p, OSHttpRequest request)
        {
            if (p.StartsWith("{IsAuthenticatedBegin}"))
            {
                return Authenticator.CheckAuthentication(request);
            }
            else if (p.StartsWith("{IsNotAuthenticatedBegin}"))
            {
                return !Authenticator.CheckAuthentication(request);
            }
            else if (p.StartsWith("{IsAdminAuthenticatedBegin}"))
            {
                return Authenticator.CheckAdminAuthentication(request);
            }
            else if (p.StartsWith("{IsNotAdminAuthenticatedBegin}"))
            {
                return !Authenticator.CheckAdminAuthentication(request);
            }
            return false;
        }

        private static int FindLines(string[] lines, int pos, string keyToCheck, string type)
        {
            int posToCheckFrom = pos + 1;
            while (!lines[posToCheckFrom++].TrimStart().StartsWith("{" + keyToCheck + type + "}"))
                continue;

            return posToCheckFrom - 1;
        }

        private static List<string> ExtractLines(string[] lines, int pos,
                                                 string keyToCheck, string type, out int posToCheckFrom)
        {
            posToCheckFrom = pos + 1;
            List<string> repeatedLines = new List<string>();
            while (!lines[posToCheckFrom].Trim().StartsWith("{" + keyToCheck + type + "}"))
                repeatedLines.Add(lines[posToCheckFrom++]);
            return repeatedLines;
        }

        protected string GetContentType(string filename, OSHttpResponse response)
        {
            switch (Path.GetExtension(filename))
            {
                case ".jpeg":
                case ".jpg":
                    response.AddHeader("Cache-Control", "Public;max-age=" + CLIENT_CACHE_TIME.ToString());
                    return "image/jpeg";
                case ".gif":
                    response.AddHeader("Cache-Control", "Public;max-age=" + CLIENT_CACHE_TIME.ToString());
                    return "image/gif";
                case ".png":
                    response.AddHeader("Cache-Control", "Public;max-age=" + CLIENT_CACHE_TIME.ToString());
                    return "image/png";
                case ".tiff":
                    response.AddHeader("Cache-Control", "Public;max-age=" + CLIENT_CACHE_TIME.ToString());
                    return "image/tiff";
                case ".html":
                case ".htm":
                case ".xsl":
                    response.AddHeader("Cache-Control", "no-cache");
                    return "text/html";
                case ".css":
                    //response.AddHeader("Cache-Control", "max-age=" + CLIENT_CACHE_TIME.ToString() + ", public");
                    response.AddHeader("Cache-Control", "no-cache");
                    return "text/css";
                case ".js":
                    //response.AddHeader("Cache-Control", "max-age=" + CLIENT_CACHE_TIME.ToString() + ", public");
                    return "application/javascript";
            }
            return "text/plain";
        }

        protected string GetFileNameFromHTMLPath(string path, Hashtable query)
        {
            try
            {
                string filePath = path.StartsWith("/") ? path.Remove(0, 1) : path;
                filePath = filePath.IndexOf('?') >= 0 ? filePath.Substring(0, filePath.IndexOf('?')) : filePath;

                if (filePath == "")
                    filePath = "index.html";
                if (filePath[filePath.Length - 1] == '/')
                    filePath = filePath + "index.html";

                string file = Path.Combine("html/", filePath);
                if (!Path.GetFullPath(file).StartsWith(Path.GetFullPath("html/")))
                {
                    return "html/index.html";
                }
                if (Path.GetFileName(file) == "")
                    file = Path.Combine(file, "index.html");

                if (query.ContainsKey("page") && _pages.ContainsKey("html/" + query["page"].ToString() + ".html"))
                {
                    file = _pages["html/" + query["page"].ToString() + ".html"].FilePath[0];
                }
                if (!File.Exists(file))
                    return "html/http_404.html";

                return file;
            }
            catch
            {
                return null;
            }
        }


        public static Dictionary<string, object> ParseQueryString(string query)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            string[] terms = query.Split(new[] { '&' });

            if (terms.Length == 0)
                return result;

            foreach (string t in terms)
            {
                string[] elems = t.Split(new[] { '=' });
                if (elems.Length == 0)
                    continue;

                string name = HttpUtility.UrlDecode(elems[0]);
                string value = String.Empty;

                if (elems.Length > 1)
                    value = HttpUtility.UrlDecode(elems[1]);

                if (name.EndsWith("[]"))
                {
                    string cleanName = name.Substring(0, name.Length - 2);
                    if (result.ContainsKey(cleanName))
                    {
                        if (!(result[cleanName] is List<string>))
                            continue;

                        List<string> l = (List<string>)result[cleanName];

                        l.Add(value);
                    }
                    else
                    {
                        List<string> newList = new List<string> { value };

                        result[cleanName] = newList;
                    }
                }
                else
                {
                    if (!result.ContainsKey(name))
                        result[name] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Webpage UL type arguments.
        /// </summary>
        /// <returns>The type arguments.</returns>
        /// <param name="translator">Translator.</param>
        public List<Dictionary<string, object>> UserTypeArgs(ITranslator translator)
        { 
            var args = new List<Dictionary<string, object>>();
            args.Add(new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Guest")}, {"Index","0"} });
            args.Add(new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Resident")}, {"Index","1"} });
            args.Add(new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Member")}, {"Index","2"} });
            args.Add(new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Contractor")}, {"Index","3"} });
            args.Add(new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Charter_Member")}, {"Index","4"} });
            return args;
        }

        /// <summary>
        /// Convert to to user flags.
        /// </summary>
        /// <returns>The type to user flags.</returns>
        /// <param name="userType">User type Index.</param>
        public int UserTypeToUserFlags(string userType)
        {
            switch (userType)
            {
            case "0":
                return Constants.USER_FLAG_GUEST;
            case "1":
                return Constants.USER_FLAG_RESIDENT;
            case "2":
                return Constants.USER_FLAG_MEMBER;
            case "3":
                return Constants.USER_FLAG_CONTRACTOR;
            case "4":
                return Constants.USER_FLAG_CHARTERMEMBER;
            default:
                return Constants.USER_FLAG_GUEST;
            }
        }

        /// <summary>
        /// User flags to type string.
        /// </summary>
        /// <returns>The flag to type.</returns>
        /// <param name="userFlags">User flags.</param>
        /// <param name = "translator"></param>
        public  string UserFlagToType(int userFlags, ITranslator translator)
        {
            if (translator == null)
                translator = EnglishTranslator;

            switch (userFlags)
            {
            case Constants.USER_FLAG_GUEST:
                return translator.GetTranslatedString("Guest");
            case Constants.USER_FLAG_RESIDENT:
                return translator.GetTranslatedString("Resident");
            case Constants.USER_FLAG_MEMBER:
                return translator.GetTranslatedString("Member");
            case Constants.USER_FLAG_CONTRACTOR:
                return translator.GetTranslatedString("Contractor");
            case Constants.USER_FLAG_CHARTERMEMBER:
                return translator.GetTranslatedString("Charter_Member");
            default:
                return translator.GetTranslatedString("Guest");
            }
        }

        #endregion

        internal void Redirect(OSHttpResponse httpResponse, string url)
        {
            httpResponse.StatusCode = (int) HttpStatusCode.Redirect;
            httpResponse.AddHeader("Location", url);
            httpResponse.KeepAlive = false;
        }
    }

    internal class GridNewsItem : IDataTransferable
    {
        public static readonly GridNewsItem NoNewsItem = new GridNewsItem()
                                                             {
                                                                 ID = -1,
                                                                 Text = "No news to report",
                                                                 Time = DateTime.Now,
                                                                 Title = "No news to report"
                                                             };

        public string Title;
        public string Text;
        public DateTime Time;
        public int ID;

        public override OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();
            map["Title"] = Title;
            map["Text"] = Text;
            map["Time"] = Time;
            map["ID"] = ID;
            return map;
        }

        public override void FromOSD(OSDMap map)
        {
            Title = map["Title"];
            Text = map["Text"];
            Time = map["Time"];
            ID = map["ID"];
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            //dictionary.Add("NewsDate", Time.ToShortDateString());
            dictionary.Add("NewsDate", Culture.LocaleDate(Time));
            dictionary.Add("NewsTitle", Title);
            dictionary.Add("NewsText", Text);
            dictionary.Add("NewsID", ID);

            return dictionary;
        }
    }

    internal class GridWelcomeScreen : IDataTransferable
    {
        public static readonly GridWelcomeScreen Default = new GridWelcomeScreen
                                                               {
                                                                   SpecialWindowMessageTitle =
                                                                       "Nothing to report at this time.",
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

        public override OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();
            map["SpecialWindowMessageTitle"] = SpecialWindowMessageTitle;
            map["SpecialWindowMessageText"] = SpecialWindowMessageText;
            map["SpecialWindowMessageColor"] = SpecialWindowMessageColor;
            map["SpecialWindowActive"] = SpecialWindowActive;
            map["GridStatus"] = GridStatus;
            return map;
        }

        public override void FromOSD(OSDMap map)
        {
            SpecialWindowMessageTitle = map["SpecialWindowMessageTitle"];
            SpecialWindowMessageText = map["SpecialWindowMessageText"];
            SpecialWindowMessageColor = map["SpecialWindowMessageColor"];
            SpecialWindowActive = map["SpecialWindowActive"];
            GridStatus = map["GridStatus"];
        }
    }

    internal class GridPage : IDataTransferable
    {
        public List<GridPage> Children = new List<GridPage>();
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

        public GridPage()
        {
        }

        public GridPage(OSD map)
        {
            FromOSD(map as OSDMap);
        }

        public override void FromOSD(OSDMap map)
        {
            ShowInMenu = map["ShowInMenu"];
            MenuPosition = map["MenuPosition"];
            MenuID = map["MenuID"];
            MenuTitle = map["MenuTitle"];
            MenuToolTip = map["MenuToolTip"];
            Location = map["Location"];
            LoggedInRequired = map["LoggedInRequired"];
            LoggedOutRequired = map["LoggedOutRequired"];
            AdminRequired = map["AdminRequired"];
            AdminLevelRequired = map["AdminLevelRequired"];
            Children = ((OSDArray) map["Children"]).ConvertAll<GridPage>(o => new GridPage(o));
        }

        public override OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();

            map["ShowInMenu"] = ShowInMenu;
            map["MenuPosition"] = MenuPosition;
            map["MenuID"] = MenuID;
            map["MenuTitle"] = MenuTitle;
            map["MenuToolTip"] = MenuToolTip;
            map["Location"] = Location;
            map["LoggedInRequired"] = LoggedInRequired;
            map["LoggedOutRequired"] = LoggedOutRequired;
            map["AdminRequired"] = AdminRequired;
            map["AdminLevelRequired"] = AdminLevelRequired;
            map["Children"] = Children.ToOSDArray();
            return map;
        }

        public GridPage GetPage(string item)
        {
            return GetPage(item, null);
        }

        public GridPage GetPage(string item, GridPage rootPage)
        {
            if (rootPage == null)
                rootPage = this;
            foreach (var page in rootPage.Children)
            {
                if (page.MenuID == item)
                    return page;
                else if (page.Children.Count > 0)
                {
                    var p = GetPage(item, page);
                    if (p != null)
                        return p;
                }
            }
            return null;
        }

        public GridPage GetPageByLocation(string item)
        {
            return GetPageByLocation(item, null);
        }

        public GridPage GetPageByLocation(string item, GridPage rootPage)
        {
            if (rootPage == null)
                rootPage = this;
            foreach (var page in rootPage.Children)
            {
                if (page.Location == item)
                    return page;
                else if (page.Children.Count > 0)
                {
                    var p = GetPageByLocation(item, page);
                    if (p != null)
                        return p;
                }
            }
            return null;
        }

        public void ReplacePage(string MenuItem, GridPage replacePage)
        {
            foreach (var page in this.Children)
            {
                if (page.MenuID == MenuItem)
                {
                    page.FromOSD(replacePage.ToOSD());
                    return;
                }
                else if (page.Children.Count > 0)
                {
                    var p = GetPage(MenuItem, page);
                    if (p != null)
                    {
                        p.FromOSD(replacePage.ToOSD());
                        return;
                    }
                }
            }
        }

        public void RemovePage(string MenuID, GridPage replacePage)
        {
            GridPage foundPage = null;
            foreach (var page in this.Children)
            {
                if (page.MenuID == MenuID)
                {
                    foundPage = page;
                    break;
                }
                else if (page.Children.Count > 0)
                {
                    var p = GetPage(MenuID, page);
                    if (p != null)
                    {
                        page.Children.Remove(p);
                        return;
                    }
                }
            }
            if (foundPage != null)
                this.Children.Remove(foundPage);
        }

        public void RemovePageByLocation(string MenuLocation, GridPage replacePage)
        {
            GridPage foundPage = null;
            foreach (var page in this.Children)
            {
                if (page.Location == MenuLocation)
                {
                    foundPage = page;
                    break;
                }
                else if (page.Children.Count > 0)
                {
                    var p = GetPageByLocation(MenuLocation, page);
                    if (p != null)
                    {
                        page.Children.Remove(p);
                        return;
                    }
                }
            }
            if (foundPage != null)
                this.Children.Remove(foundPage);
        }

        public GridPage GetParent(GridPage page)
        {
            return GetParent(page, null);
        }

        public GridPage GetParent(GridPage item, GridPage toCheck)
        {
            if (toCheck == null)
                toCheck = this;
            foreach (var p in toCheck.Children)
            {
                if (item.Location == p.Location)
                    return toCheck;
                else if (p.Children.Count > 0)
                {
                    var pp = GetParent(item, p);
                    if (pp != null)
                        return pp;
                }
            }
            return null;
        }
    }

    internal class GridSettings : IDataTransferable
    {
        public Vector2 MapCenter = Vector2.Zero;
        public uint LastPagesVersionUpdateIgnored = 0;
        public uint LastSettingsVersionUpdateIgnored = 0;
        public bool HideLanguageTranslatorBar = false;
        public bool HideStyleBar = false;
        public UUID DefaultScopeID = UUID.Zero;
        public bool WebRegistration = false;

        public GridSettings()
        {
        }

        public GridSettings(OSD map)
        {
            FromOSD(map as OSDMap);
        }

        public override void FromOSD(OSDMap map)
        {
            MapCenter = map["MapCenter"];
            LastPagesVersionUpdateIgnored = map["LastPagesVersionUpdateIgnored"];
            LastSettingsVersionUpdateIgnored = map["LastSettingsVersionUpdateIgnored"];
            HideLanguageTranslatorBar = map["HideLanguageTranslatorBar"];
            HideStyleBar = map["HideStyleBar"];
            DefaultScopeID = map["DefaultScopeID"];
            WebRegistration = map["WebRegistration"];
        }

        public override OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();

            map["MapCenter"] = MapCenter;
            map["LastPagesVersionUpdateIgnored"] = LastPagesVersionUpdateIgnored;
            map["LastSettingsVersionUpdateIgnored"] = LastSettingsVersionUpdateIgnored;
            map["HideLanguageTranslatorBar"] = HideLanguageTranslatorBar;
            map["HideStyleBar"] = HideStyleBar;
            map["DefaultScopeID"] = DefaultScopeID;
            map["WebRegistration"] = WebRegistration;

            return map;
        }
    }
}