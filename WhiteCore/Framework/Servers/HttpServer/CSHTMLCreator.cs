/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
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
using System.Text;
using System.Web;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Framework.Servers.HttpServer
{
    public delegate string HTTPReturned(Dictionary<string, string> variables);

    public class CSHTMLCreator
    {
        public static string AddHTMLPage(string html, string urlToAppend, string methodName,
                                         Dictionary<string, object> variables, HTTPReturned eventDelegate)
        {
            string secret = Util.RandomClass.Next(0, int.MaxValue).ToString();
            string secret2 = Util.RandomClass.Next(0, int.MaxValue).ToString();
            string navUrl = MainServer.Instance.ServerURI +
                            (urlToAppend == "" ? "" : "/") + urlToAppend + "/index.php?method=" + methodName + secret2;
            string url = MainServer.Instance.ServerURI +
                         (urlToAppend == "" ? "" : "/") + urlToAppend + "/index.php?method=" + methodName + secret;
            MainServer.Instance.RemoveStreamHandler("GET", "/index.php?method=" + methodName + secret);
            MainServer.Instance.RemoveStreamHandler("GET", "/index.php?method=" + methodName + secret2);

            variables["url"] = url;
            MainServer.Instance.AddStreamHandler(new GenericStreamHandler("GET", "/index.php?method=" + methodName + secret2,
                                                                        delegate(string path, Stream request,
                                                                                 OSHttpRequest httpRequest,
                                                                                 OSHttpResponse httpResponse)
                                                                            {
                                                                                MainServer.Instance.RemoveStreamHandler
                                                                                    ("GET", "/index.php?method=" + methodName + secret2);
                                                                                return SetUpWebpage(httpResponse, url,
                                                                                                    html, variables);
                                                                            }));
            MainServer.Instance.AddStreamHandler(new GenericStreamHandler("GET",
                                                                        "/index.php?method=" + methodName + secret,
                                                                        delegate(string path, Stream request,
                                                                                 OSHttpRequest httpRequest,
                                                                                 OSHttpResponse httpResponse)
                                                                            {
                                                                                MainServer.Instance.RemoveStreamHandler(
                                                                                    "GET", "/index.php?method=" + methodName +
                                                                                    secret);
                                                                                return HandleResponse(httpRequest,
                                                                                                      httpResponse,
                                                                                                      request,
                                                                                                      urlToAppend,
                                                                                                      variables,
                                                                                                      eventDelegate);
                                                                            }));
            return navUrl;
        }

        static byte[] HandleResponse(OSHttpRequest httpRequest, OSHttpResponse response, Stream stream,
                                             string urlToAppend, Dictionary<string, object> variables,
                                             HTTPReturned eventHandler)
        {
            Uri myUri = new Uri("http://localhost/index.php?" + HttpServerHandlerHelpers.ReadString(stream));
            Dictionary<string, string> newVars = new Dictionary<string, string>();
            foreach (string key in variables.Keys)
            {
                newVars[key] = HttpUtility.ParseQueryString(myUri.Query).Get(key);
            }
            string url = eventHandler(newVars);

            string html = "<html>" +
                          (url == ""
                               ? ""
                               : ("<head>" +
                                  "<meta http-equiv=\"REFRESH\" content=\"0;url=" + url + "\"></HEAD>")) +
                          "</HTML>";
            response.ContentType = "text/html";

            return Encoding.UTF8.GetBytes(html);
        }

        static byte[] SetUpWebpage(OSHttpResponse response, string url, string html,
                                           Dictionary<string, object> vars)
        {
            response.ContentType = "text/html";
            return Encoding.UTF8.GetBytes(BuildHTML(html, vars));
        }

        public static string BuildHTML(string html, Dictionary<string, object> vars)
        {
            if (vars == null) return html;
            foreach (KeyValuePair<string, object> kvp in vars)
            {
                if (!(kvp.Value is IList))
                    html = html.Replace("{" + kvp.Key + "}", (kvp.Value ?? "").ToString());
            }
            return html;
        }
    }
}