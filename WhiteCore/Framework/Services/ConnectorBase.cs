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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Nini.Config;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Framework.Services
{
    public static class ConnectorRegistry
    {
        public static List<ConnectorBase> Connectors = new List<ConnectorBase> ();

        public static void RegisterConnector (ConnectorBase con)
        {
            Connectors.Add (con);
        }
        public static List<ConnectorBase> ServerHandlerConnectors = new List<ConnectorBase> ();
        public static void RegisterServerHandlerConnector (ConnectorBase con)
        {
            ServerHandlerConnectors.Add (con);
        }
    }

    public class ConnectorBase
    {
        protected IRegistryCore m_registry;

        protected IConfigurationService m_configService {
            get { return m_registry.RequestModuleInterface<IConfigurationService> (); }
        }

        protected bool m_doRemoteCalls;
        protected bool m_startedServer;
        protected string m_name;
        protected bool m_doRemoteOnly;
        protected int m_OSDRequestTryCount = 7;
        protected string m_password = "";

        public bool IsLocalConnector { get { return !m_doRemoteCalls; } }

        public string ServerHandlerName {
            get;
            private set;
        }

        public string ServerHandlerPath {
            get;
            private set;
        }

        public uint ServerHandlerPort {
            get;
            private set;
        }

        public string PluginName {
            get { return m_name; }
        }

        public bool Enabled { get; set; }

        public void Init (IRegistryCore registry, string name, string password = "", string serverPath = "", string serverHandlerName = "")
        {
            Enabled = true;
            m_registry = registry;
            m_name = name;
            m_password = password;
            bool openServerHandler = false;
            uint serverHandlerPort = 0;
            ISimulationBase simBase = registry == null ? null : registry.RequestModuleInterface<ISimulationBase> ();
            if (simBase != null) {
                IConfigSource source = registry.RequestModuleInterface<ISimulationBase> ().ConfigSource;
                IConfig config;
                if ((config = source.Configs ["WhiteCoreConnectors"]) != null) {
                    m_doRemoteCalls = config.Contains (name + "DoRemoteCalls")
                                          ? config.GetBoolean (name + "DoRemoteCalls", false)
                                          : config.GetBoolean ("DoRemoteCalls", false);

                    if ((config = source.Configs ["Handlers"]) != null) {
                        openServerHandler = config.GetBoolean (name + "OpenServerHandler", false);
                        serverHandlerPort = config.GetUInt (name + "ServerHandlerPort", serverHandlerPort);
                    }
                }
                if ((config = source.Configs ["Configuration"]) != null)
                    m_OSDRequestTryCount = config.GetInt ("OSDRequestTryCount", m_OSDRequestTryCount);
            }
            if (m_doRemoteCalls)
                m_doRemoteOnly = true; // Lock out local + remote for now

            ConnectorRegistry.RegisterConnector (this);

            ServerHandlerName = serverHandlerName;
            if (MainServer.Instance == null && serverHandlerPort == 0)
                openServerHandler = false;
            else {
                ServerHandlerPort = serverHandlerPort == 0 ? MainServer.Instance.Port : serverHandlerPort;
                ServerHandlerPath = serverPath;
            }

            if (openServerHandler)
                CreateServerHandler (serverHandlerPort, serverPath, serverHandlerName);
        }

        protected void CreateServerHandler (uint port, string urlPath, string serverHandlerName)
        {
            var server = m_registry.RequestModuleInterface<ISimulationBase> ().GetHttpServer (port);

            server.AddStreamHandler (new ServerHandler (urlPath, m_registry, this));
            ConnectorRegistry.RegisterServerHandlerConnector (this);
            m_startedServer = true;
        }

        public void SetPassword (string password)
        {
            m_password = password;
        }

        public void SetDoRemoteCalls (bool doRemoteCalls)
        {
            m_doRemoteCalls = doRemoteCalls;
            m_doRemoteOnly = doRemoteCalls;
        }

        #region OSD Sending

        public object DoRemote (params object [] o)
        {
            return DoRemoteCallGet (false, "ServerURI", o);
        }

        public object DoRemoteByURL (string url, params object [] o)
        {
            return DoRemoteCallGet (false, url, o);
        }

        public void DoRemotePost (params object [] o)
        {
            DoRemoteCallPost (false, "ServerURI", o);
        }

        public void DoRemotePostByURL (string url, params object [] o)
        {
            DoRemoteCallPost (false, url, o);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="forced">Whether this remote call is forced (if false, it will only call if m_doRemoteCalls is true)</param>
        /// <param name="url">The URL to call</param>
        /// <param name="o">The objects to send</param>
        /// <returns></returns>
        public object DoRemoteCallGet (bool forced, string url, params object [] o)
        {
            if (!m_doRemoteCalls && !forced)
                return null;
            MethodInfo method;
            OSDMap map;
            string serverURL;
            if (!PrepRemoteCall (url, o, out method, out map, out serverURL))
                return null;

            return GetResponse (method, map, serverURL);
        }

        public void DoRemoteCallPost (bool forced, string url, params object [] o)
        {
            MethodInfo method;
            OSDMap map;
            string serverURL;
            if (!PrepRemoteCall (url, o, out method, out map, out serverURL))
                return;

            WebUtils.PostToService (serverURL, map);
        }

        bool PrepRemoteCall (string url, object [] o, out MethodInfo method, out OSDMap map, out string serverURL)
        {
            var stackTrace = new StackTrace ();
            int upStack = 1;
            var frame = stackTrace.GetFrame (1);
            if (frame.GetMethod ().Name.Contains ("DoRemote")) {
                upStack = 2;
                frame = stackTrace.GetFrame (2);
                if (frame.GetMethod ().Name.Contains ("DoRemote"))
                    upStack = 3;
            }

            CanBeReflected reflection;
            GetReflection (upStack, stackTrace, out method, out reflection);
            string methodName = reflection != null && reflection.RenamedMethod != ""
                                    ? reflection.RenamedMethod
                                    : method.Name;
            map = new OSDMap ();
            map ["Method"] = methodName;
            if (reflection != null && reflection.UsePassword)
                map ["Password"] = m_password;
            int i = 0;
            var parameters = method.GetParameters ();
            if (o.Length != parameters.Length) {
                MainConsole.Instance.ErrorFormat (
                    "[ConnectorBase]: Failed to get valid number of parameters to send  in remote call to {0}, expected {1}, got {2}",
                    methodName, parameters.Length, o.Length);
                serverURL = "";
                return false;
            }
            foreach (ParameterInfo info in parameters) {
                OSD osd = o [i] == null ? null : Util.MakeOSD (o [i], o [i].GetType ());
                if (osd != null)
                    map.Add (info.Name, osd);
                else
                    map.Add (info.Name, new OSD ());
                i++;
            }

            serverURL = m_configService == null ? "" : m_configService.FindValueOf (url);
            if (serverURL == "")
                serverURL = url;
            return true;
        }

        object GetResponse (MethodInfo method, OSDMap map, string serverURL)
        {
            OSDMap response = null;

            for (int index = 0; index < m_OSDRequestTryCount; index++) {
                if (GetOSDMap (serverURL, map, out response))
                    break;
            }
            if (response == null || !response)
                return null;
            object inst = null;
            try {
                if (method.ReturnType == typeof (string))
                    inst = string.Empty;
                else if (method.ReturnType == typeof (void))
                    return null;
                else if (method.ReturnType == typeof (System.Drawing.Image))
                    inst = null;
                else if (method.ReturnType == typeof (byte []))
                    return response ["Value"].AsBinary ();
                else
                    inst = Activator.CreateInstance (method.ReturnType);
            } catch {
                if (method.ReturnType == typeof (string))
                    inst = string.Empty;
            }
            if (response ["Value"] == "null")
                return null;
            var instance = inst as IDataTransferable;
            if (instance != null) {
                instance.FromOSD ((OSDMap)response ["Value"]);
                return instance;
            }
            return Util.OSDToObject (response ["Value"], method.ReturnType);
        }

        void GetReflection (int upStack, StackTrace stackTrace, out MethodInfo method,
                           out CanBeReflected reflection)
        {
            method = (MethodInfo)stackTrace.GetFrame (upStack).GetMethod ();
            reflection = (CanBeReflected)Attribute.GetCustomAttribute (method, typeof (CanBeReflected));
            if (reflection != null && reflection.NotReflectableLookUpAnotherTrace)
                GetReflection (upStack + 1, stackTrace, out method, out reflection);
        }

        bool GetOSDMap (string url, OSDMap map, out OSDMap response)
        {
            response = null;
            var resp = WebUtils.PostToService (url, map);

            if (string.IsNullOrEmpty (resp) || resp.StartsWith ("<", StringComparison.Ordinal))
                return false;

            try {
                response = (OSDMap)OSDParser.DeserializeJson (resp);
            } catch {
                response = null;
                return false;
            }
            return response ["Success"];
        }

        public bool CheckPassword (string password)
        {
            return password == m_password;
        }

        #endregion
    }

    public class ServerHandler : BaseRequestHandler
    {
        protected IRegistryCore m_registry;
        protected Dictionary<string, List<MethodImplementation>> m_methods = null;

        public ServerHandler (string url, IRegistryCore registry, ConnectorBase conn) :
            base ("POST", url)
        {
            m_registry = registry;
            if (m_methods == null) {
                m_methods = new Dictionary<string, List<MethodImplementation>> ();
                var alreadyRunPlugins = new List<string> ();
                List<ConnectorBase> connectors = conn == null
                                                     ? ConnectorRegistry.Connectors
                                                     : new List<ConnectorBase> { conn };
                foreach (ConnectorBase plugin in connectors) {
                    if (alreadyRunPlugins.Contains (plugin.PluginName))
                        continue;
                    alreadyRunPlugins.Add (plugin.PluginName);
                    foreach (MethodInfo method in plugin.GetType ().GetMethods ()) {
                        var reflection =
                            (CanBeReflected)Attribute.GetCustomAttribute (method, typeof (CanBeReflected));
                        
                        if (reflection != null) {
                            string methodName = reflection.RenamedMethod == "" ? method.Name : reflection.RenamedMethod;
                            var methods = new List<MethodImplementation> ();
                            var imp = new MethodImplementation {
                                Method = method,
                                Reference = plugin,
                                Attribute = reflection
                            };
                            if (!m_methods.TryGetValue (methodName, out methods))
                                m_methods.Add (methodName, (methods = new List<MethodImplementation> ()));

                            methods.Add (imp);
                        }
                    }
                }
            }
        }

        public override byte [] Handle (string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            var body = HttpServerHandlerHelpers.ReadString (request).Trim ();

            try {
                var args = WebUtils.GetOSDMap (body, false);
                if (args != null)
                    return HandleMap (args);
            } catch (Exception ex) {
                MainConsole.Instance.Warn ("[ServerHandler]: Error occurred: " + ex);
            }
            return MainServer.BadRequest;
        }

        public byte [] HandleMap (OSDMap args)
        {
            if (args.ContainsKey ("Method")) {
                var method = args ["Method"].AsString ();
                try {
                    MethodImplementation methodInfo;
                    if (GetMethodInfo (method, args.Count - 1, out methodInfo)) {
                        var paramInfo = methodInfo.Method.GetParameters ();
                        object [] parameters = new object [paramInfo.Length];
                        int paramNum = 0;
                        foreach (ParameterInfo param in paramInfo) {
                            if (param.ParameterType == typeof (bool) && !args.ContainsKey (param.Name))
                                parameters [paramNum++] = false;
                            else if (args [param.Name].Type == OSDType.Unknown)
                                parameters [paramNum++] = null;
                            else if (param.ParameterType == typeof (OSD))
                                parameters [paramNum++] = args [param.Name];
                            else
                                parameters [paramNum++] = Util.OSDToObject (args [param.Name], param.ParameterType);
                        }

                        var obj = methodInfo.Method.FastInvoke (paramInfo, methodInfo.Reference, parameters);
                        var response = new OSDMap ();
                        if (obj == null) //void method
                            response ["Value"] = "null";
                        else
                            response ["Value"] = Util.MakeOSD (obj, methodInfo.Method.ReturnType);
                        response ["Success"] = true;
                        return Encoding.UTF8.GetBytes (OSDParser.SerializeJsonString (response, true));
                    }
                } catch (Exception ex) {
                    MainConsole.Instance.WarnFormat ("[ServerHandler]: Error occurred for method {0}: {1}", method,
                                                    ex.ToString ());
                }
            } else
                MainConsole.Instance.Warn ("[ServerHandler]: Post did not have a method block");

            return MainServer.BadRequest;
        }

        bool GetMethodInfo (string method, int parameters, out MethodImplementation methodInfo)
        {
            var methods = new List<MethodImplementation> ();
            if (m_methods.TryGetValue (method, out methods)) {
                if (methods.Count == 1) {
                    methodInfo = methods [0];
                    return true;
                }
                foreach (MethodImplementation m in methods) {
                    if (m.Method.GetParameters ().Length == parameters) {
                        methodInfo = m;
                        return true;
                    }
                }
            }
            MainConsole.Instance.Warn ("COULD NOT FIND METHOD: " + method);
            methodInfo = null;
            return false;
        }
    }

    public class MethodImplementation
    {
        public MethodInfo Method;
        public ConnectorBase Reference;
        public CanBeReflected Attribute;
    }
}
