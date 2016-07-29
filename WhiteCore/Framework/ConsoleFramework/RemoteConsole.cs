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
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;

namespace WhiteCore.Framework.ConsoleFramework
{
    public class ConsoleConnection
    {
        public int last;
        public long lastLineSeen;
        public bool newConnection = true;
    }

    // A console that uses REST interfaces
    //
    public class RemoteConsole : CommandConsole
    {
        readonly Dictionary<UUID, ConsoleConnection> m_Connections = new Dictionary<UUID, ConsoleConnection> ();

        readonly ManualResetEvent m_DataEvent = new ManualResetEvent (false);
        readonly List<string> m_InputData = new List<string> ();
        readonly List<string> m_Scrollback = new List<string> ();
        long m_LineNumber;

        string m_Password = string.Empty;
        IHttpServer m_Server;
        string m_UserName = string.Empty;

        public override string Name {
            get { return "RemoteConsole"; }
        }

        public override void Initialize (IConfigSource source, ISimulationBase simBase)
        {
            uint m_consolePort = 0;

            if (source.Configs ["Console"] != null) {
                //if (source.Configs["Console"].GetString("RemoteConsole", string.Empty) != "enable")
                if (source.Configs ["Console"].GetString ("Console", string.Empty) != Name)
                    return;

                m_consolePort = (uint)source.Configs ["Console"].GetInt ("remote_console_port", 0);
                m_UserName = source.Configs ["Console"].GetString ("RemoteConsoleUser", string.Empty);
                m_Password = source.Configs ["Console"].GetString ("RemoteConsolePass", string.Empty);
            } else
                return;

            simBase.ApplicationRegistry.RegisterModuleInterface<ICommandConsole> (this);
            MainConsole.Instance = this;

            SetServer (m_consolePort == 0 ? MainServer.Instance : simBase.GetHttpServer (m_consolePort));

            m_Commands.AddCommand (
                "help",
                "help",
                "Get a general command list",
                Help, false, true);
        }

        public void SetServer (IHttpServer server)
        {
            m_Server = server;

            m_Server.AddStreamHandler (new GenericStreamHandler ("GET", "/StartSession/", HandleHttpStartSession));
            m_Server.AddStreamHandler (new GenericStreamHandler ("GET", "/CloseSession/", HandleHttpCloseSession));
            m_Server.AddStreamHandler (new GenericStreamHandler ("GET", "/SessionCommand/", HandleHttpSessionCommand));
        }

        public override void Output (string text, Level level)
        {
            lock (m_Scrollback) {
                while (m_Scrollback.Count >= 1000)
                    m_Scrollback.RemoveAt (0);
                m_LineNumber++;
                m_Scrollback.Add (string.Format ("{0}", m_LineNumber) + ":" + level + ":" + text);
            }
            Console.WriteLine (text.Trim ());
        }

        public override string ReadLine (string p, bool isCommand, bool e)
        {
            string cmdinput;

            if (isCommand)
                Output ("+++" + p, Threshold);
            else
                Output ("-++" + p, Threshold);

            lock (m_InputData) {
                m_DataEvent.WaitOne ();

                if (m_InputData.Count == 0) {
                    m_DataEvent.Reset ();
                    return "";
                }

                cmdinput = m_InputData [0];
                m_InputData.RemoveAt (0);
                if (m_InputData.Count == 0)
                    m_DataEvent.Reset ();
            }

            if (isCommand) {
                string [] cmd = Commands.Resolve (Parser.Parse (cmdinput));

                if (cmd.Length != 0) {
                    int i;

                    for (i = 0; i < cmd.Length; i++) {
                        if (cmd [i].Contains (" "))
                            cmd [i] = "\"" + cmd [i] + "\"";
                    }
                    return string.Empty;
                }
            }
            return cmdinput;
        }

        void DoExpire ()
        {
            List<UUID> expired = new List<UUID> ();

            lock (m_Connections) {
                expired.AddRange (from kvp in m_Connections
                                  where Environment.TickCount - kvp.Value.last > 500000
                                  select kvp.Key);

                foreach (UUID id in expired) {
                    m_Connections.Remove (id);
                    CloseConnection (id);
                }
            }
        }

        byte [] HandleHttpStartSession (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            DoExpire ();

            Hashtable post = DecodePostString (HttpServerHandlerHelpers.ReadString (request));

            httpResponse.StatusCode = 401;
            httpResponse.ContentType = "text/plain";
            if (m_UserName == string.Empty)
                return MainServer.BlankResponse;

            if (post ["USER"] == null || post ["PASS"] == null)
                return MainServer.BlankResponse;

            if (m_UserName != post ["USER"].ToString () ||
                m_Password != post ["PASS"].ToString ())
                return MainServer.BlankResponse;

            ConsoleConnection c = new ConsoleConnection { last = Environment.TickCount, lastLineSeen = 0 };

            UUID sessionID = UUID.Random ();

            lock (m_Connections) {
                m_Connections [sessionID] = c;
            }

            string uri = "/ReadResponses/" + sessionID + "/";

            m_Server.AddPollServiceHTTPHandler (uri, new PollServiceEventArgs (null, HasEvents, GetEvents, NoEvents,
                                                                        sessionID));

            XmlDocument xmldoc = new XmlDocument ();
            XmlNode xmlnode = xmldoc.CreateNode (XmlNodeType.XmlDeclaration, "", "");

            xmldoc.AppendChild (xmlnode);
            XmlElement rootElement = xmldoc.CreateElement ("", "ConsoleSession", "");

            xmldoc.AppendChild (rootElement);

            XmlElement id = xmldoc.CreateElement ("", "SessionID", "");
            id.AppendChild (xmldoc.CreateTextNode (sessionID.ToString ()));

            rootElement.AppendChild (id);

            XmlElement prompt = xmldoc.CreateElement ("", "Prompt", "");
            prompt.AppendChild (xmldoc.CreateTextNode (DefaultPrompt));

            rootElement.AppendChild (prompt);

            httpResponse.StatusCode = 200;
            httpResponse.ContentType = "text/xml";
            return Encoding.UTF8.GetBytes (xmldoc.InnerXml);
        }

        byte [] HandleHttpCloseSession (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            DoExpire ();

            Hashtable post = DecodePostString (HttpServerHandlerHelpers.ReadString (request));

            httpResponse.StatusCode = 401;
            httpResponse.ContentType = "text/plain";
            if (post ["ID"] == null)
                return MainServer.BlankResponse;

            UUID id;
            if (!UUID.TryParse (post ["ID"].ToString (), out id))
                return MainServer.BlankResponse;

            lock (m_Connections) {
                if (m_Connections.ContainsKey (id)) {
                    m_Connections.Remove (id);
                    CloseConnection (id);
                }
            }

            XmlDocument xmldoc = new XmlDocument ();
            XmlNode xmlnode = xmldoc.CreateNode (XmlNodeType.XmlDeclaration, "", "");

            xmldoc.AppendChild (xmlnode);
            XmlElement rootElement = xmldoc.CreateElement ("", "ConsoleSession", "");

            xmldoc.AppendChild (rootElement);

            XmlElement res = xmldoc.CreateElement ("", "Result", "");
            res.AppendChild (xmldoc.CreateTextNode ("OK"));

            rootElement.AppendChild (res);

            httpResponse.StatusCode = 200;
            httpResponse.ContentType = "text/xml";
            return Encoding.UTF8.GetBytes (xmldoc.InnerXml);
        }

        byte [] HandleHttpSessionCommand (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            DoExpire ();

            Hashtable post = DecodePostString (HttpServerHandlerHelpers.ReadString (request));

            httpResponse.StatusCode = 401;
            httpResponse.ContentType = "text/plain";
            if (post ["ID"] == null)
                return MainServer.BlankResponse;

            UUID id;
            if (!UUID.TryParse (post ["ID"].ToString (), out id))
                return MainServer.BlankResponse;

            lock (m_Connections) {
                if (!m_Connections.ContainsKey (id))
                    return MainServer.BlankResponse;
            }

            if (post ["COMMAND"] == null)
                return MainServer.BlankResponse;

            lock (m_InputData) {
                m_DataEvent.Set ();
                m_InputData.Add (post ["COMMAND"].ToString ());
            }

            XmlDocument xmldoc = new XmlDocument ();
            XmlNode xmlnode = xmldoc.CreateNode (XmlNodeType.XmlDeclaration, "", "");

            xmldoc.AppendChild (xmlnode);
            XmlElement rootElement = xmldoc.CreateElement ("", "ConsoleSession", "");

            xmldoc.AppendChild (rootElement);

            XmlElement res = xmldoc.CreateElement ("", "Result", "");
            res.AppendChild (xmldoc.CreateTextNode ("OK"));

            rootElement.AppendChild (res);

            httpResponse.StatusCode = 200;
            httpResponse.ContentType = "text/xml";
            return Encoding.UTF8.GetBytes (xmldoc.InnerXml);
        }

        Hashtable DecodePostString (string data)
        {
            Hashtable result = new Hashtable ();

            string [] terms = data.Split (new [] { '&' });

            foreach (string term in terms) {
                string [] elems = term.Split (new [] { '=' });
                if (elems.Length == 0)
                    continue;

                string name = HttpUtility.UrlDecode (elems [0]);
                string value = string.Empty;

                if (elems.Length > 1)
                    value = HttpUtility.UrlDecode (elems [1]);

                result [name] = value;
            }

            return result;
        }

        public void CloseConnection (UUID id)
        {
            try {
                string uri = "/ReadResponses/" + id + "/";
                m_Server.RemovePollServiceHTTPHandler ("", uri);
            } catch (Exception) {
            }
        }

        bool HasEvents (UUID RequestID, UUID sessionID)
        {
            ConsoleConnection c;

            lock (m_Connections) {
                if (!m_Connections.ContainsKey (sessionID))
                    return false;
                c = m_Connections [sessionID];
            }
            c.last = Environment.TickCount;
            lock (m_Scrollback) {
                if (c.lastLineSeen < m_LineNumber)
                    return true;
                return false;
            }
        }

        byte [] GetEvents (UUID RequestID, UUID sessionID, string req, OSHttpResponse response)
        {
            ConsoleConnection c;

            lock (m_Connections) {
                if (!m_Connections.ContainsKey (sessionID))
                    return NoEvents (RequestID, UUID.Zero, response);
                c = m_Connections [sessionID];
            }
            c.last = Environment.TickCount;
            lock (m_Scrollback) {
                if (c.lastLineSeen >= m_LineNumber)
                    return NoEvents (RequestID, UUID.Zero, response);
            }
            XmlDocument xmldoc = new XmlDocument ();
            XmlNode xmlnode = xmldoc.CreateNode (XmlNodeType.XmlDeclaration, "", "");

            xmldoc.AppendChild (xmlnode);
            XmlElement rootElement = xmldoc.CreateElement ("", "ConsoleSession", "");

            if (c.newConnection) {
                c.newConnection = false;
                Output ("+++" + DefaultPrompt, Threshold);
            }

            lock (m_Scrollback) {
                long startLine = m_LineNumber - m_Scrollback.Count;
                long sendStart = startLine;
                if (sendStart < c.lastLineSeen)
                    sendStart = c.lastLineSeen;

                for (long i = sendStart; i < m_LineNumber; i++) {
                    XmlElement res = xmldoc.CreateElement ("", "Line", "");
                    long line = i + 1;
                    res.SetAttribute ("Number", line.ToString ());
                    res.AppendChild (xmldoc.CreateTextNode (m_Scrollback [(int)(i - startLine)]));

                    rootElement.AppendChild (res);
                }
                c.lastLineSeen = m_LineNumber;
            }

            xmldoc.AppendChild (rootElement);


            response.StatusCode = 200;
            response.ContentType = "application/xml";

            return Encoding.UTF8.GetBytes (xmldoc.InnerXml);
        }

        byte [] NoEvents (UUID RequestID, UUID id, OSHttpResponse response)
        {
            XmlDocument xmldoc = new XmlDocument ();
            XmlNode xmlnode = xmldoc.CreateNode (XmlNodeType.XmlDeclaration, "", "");

            xmldoc.AppendChild (xmlnode);
            XmlElement rootElement = xmldoc.CreateElement ("", "ConsoleSession", "");

            xmldoc.AppendChild (rootElement);

            response.StatusCode = 200;
            response.ContentType = "text/xml";
            return Encoding.UTF8.GetBytes (xmldoc.InnerXml);
        }
    }
}
