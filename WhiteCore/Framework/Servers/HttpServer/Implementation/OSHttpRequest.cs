/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://opensimulator.org/
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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Framework.Servers.HttpServer.Implementation
{
    public class OSHttpRequest
    {
        protected HttpListenerRequest _request = null;
        protected Dictionary<string, HttpFile> _files = new Dictionary<string, HttpFile>();

        public sealed class HttpFile : IDisposable
        {
            /// <summary>
            ///     Gets or sets form element name
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            ///     Gets or sets client side file name
            /// </summary>
            public string OriginalFileName { get; set; }

            /// <summary>
            ///     Gets or sets mime content type
            /// </summary>
            public string ContentType { get; set; }

            /// <summary>
            ///     Gets or sets full path to local file
            /// </summary>
            public string TempFileName { get; set; }

            /// <summary>
            ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            /// <filterpriority>2</filterpriority>
            public void Dispose()
            {
                File.Delete(TempFileName);
            }
        }

        public Dictionary<string, HttpFile> Files
        {
            get { return _files; }
            set { _files = value; }
        }

        public string[] AcceptTypes
        {
            get { return _request.AcceptTypes; }
        }

        public Encoding ContentEncoding
        {
            get { return _contentEncoding; }
        }

        private Encoding _contentEncoding;

        public long ContentLength
        {
            get { return _request.ContentLength64; }
        }

        public long ContentLength64
        {
            get { return ContentLength; }
        }

        public string ContentType
        {
            get { return _contentType; }
        }

        string _contentType;

        public HttpCookieCollection Cookies
        {
            get
            {
                CookieCollection cookies = _request.Cookies;
                HttpCookieCollection httpCookies = new HttpCookieCollection();
                foreach (Cookie cookie in cookies)
                    httpCookies.Add(new HttpCookie(cookie.Name, cookie.Value));
                return httpCookies;
            }
        }

        public NameValueCollection Headers
        {
            get { return _request.Headers; }
        }

        public string HttpMethod
        {
            get { return _request.HttpMethod; }
        }

        public Stream InputStream
        {
            get { return _request.InputStream; }
        }

        public bool KeepAlive
        {
            get { return _request.KeepAlive; }
        }

        public NameValueCollection QueryString
        {
            get { return _queryString; }
        }

        NameValueCollection _queryString;

        public Hashtable Query
        {
            get { return _query; }
        }

        Hashtable _query;

        /// <value>
        ///     POST request values, if applicable
        /// </value>
        //        public Hashtable Form { get; private set; }
        public string RawUrl
        {
            get { return _request.RawUrl; }
        }

        public IPEndPoint RemoteIPEndPoint
        {
            get { return _request.RemoteEndPoint; }
        }

        public Uri Url
        {
            get { return _request.Url; }
        }

        public OSHttpRequest()
        {
        }

        public OSHttpRequest(HttpListenerContext context)
        {
            _request = context.Request;

            if (null != _request.Headers["content-encoding"])
                _contentEncoding = Encoding.GetEncoding(_request.Headers["content-encoding"]);
            if (null != _request.Headers["content-type"])
                _contentType = _request.Headers["content-type"];
            _queryString = new NameValueCollection();
            _query = new Hashtable();
            try
            {
                foreach (string item in _request.QueryString.Keys)
                {
                    if (item == null)
                        continue;

                    try
                    {
                        _queryString.Add(item, _request.QueryString[item]);
                        _query[item] = _request.QueryString[item];
                    }
                    catch (InvalidCastException)
                    {
                        MainConsole.Instance.DebugFormat("[OSHttpRequest]: error parsing {0} query item, skipping it",
                                                         item);
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                MainConsole.Instance.Error("[OSHttpRequest]: Error parsing query-string");
            }

            if (ContentType != null && ContentType.StartsWith("multipart/form-data"))
            {
                HttpMultipart.Element element;
                var boundry = "";
                var multipart = new HttpMultipart(InputStream, boundry, ContentEncoding ?? Encoding.UTF8);

                while ((element = multipart.ReadNextElement()) != null)
                {
                    if (string.IsNullOrEmpty(element.Name))
                        throw new FormatException("Error parsing request. Missing value name.\nElement: " + element);

                    if (!string.IsNullOrEmpty(element.Filename))
                    {
                        if (string.IsNullOrEmpty(element.ContentType))
                            throw new FormatException("Error parsing request. Value '" + element.Name +
                                                      "' lacks a content type.");

                        // Read the file data
                        var buffer = new byte[element.Length];
                        InputStream.Seek(element.Start, SeekOrigin.Begin);
                        InputStream.Read(buffer, 0, (int) element.Length);

                        // Generate a filename
                        var originalFileName = element.Filename;
                        var internetCache = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);

                        // if the internet path doesn't exist, assume mono and /var/tmp
                        var path = string.IsNullOrEmpty(internetCache)
                                       ? Path.Combine("var", "tmp")
                                       : Path.Combine(internetCache.Replace("\\\\", "\\"), "tmp");

                        element.Filename = Path.Combine(path, Math.Abs(element.Filename.GetHashCode()) + ".tmp");

                        // If the file exists generate a new filename
                        while (File.Exists(element.Filename))
                            element.Filename = Path.Combine(path, Math.Abs(element.Filename.GetHashCode() + 1) + ".tmp");

                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);

                        File.WriteAllBytes(element.Filename, buffer);

                        var file = new HttpFile
                                       {
                                           Name = element.Name,
                                           OriginalFileName = originalFileName,
                                           ContentType = element.ContentType,
                                           TempFileName = element.Filename
                                       };
                        Files.Add(element.Name, file);
                    }
                    /*else
                    {
                        var buffer = new byte[element.Length];
                        message.Body.Seek(element.Start, SeekOrigin.Begin);
                        message.Body.Read(buffer, 0, (int)element.Length);

                        form.Add(Uri.UnescapeDataString(element.Name), message.ContentEncoding.GetString(buffer));
                    }*/
                }
            }
        }

        public override string ToString()
        {
            StringBuilder me = new StringBuilder();
            me.Append(String.Format("OSHttpRequest: {0} {1}\n", HttpMethod, RawUrl));
            foreach (string k in Headers.AllKeys)
            {
                me.Append(String.Format("    {0}: {1}\n", k, Headers[k]));
            }
            if (null != RemoteIPEndPoint)
            {
                me.Append(String.Format("    IP: {0}\n", RemoteIPEndPoint));
            }

            return me.ToString();
        }
    }
}