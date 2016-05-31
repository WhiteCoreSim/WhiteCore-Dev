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
using System.Linq;
using System.Net;
using System.Threading;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
//using WhiteCore.Framework.Servers;
//using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

/*****************************************************
 *
 * XMLRPCModule
 *
 * Module for accepting incoming communications from
 * external XMLRPC client and calling a remote data
 * procedure for a registered data channel/prim.
 *
 *
 * 1. On module load, open a listener port
 * 2. Attach an XMLRPC handler
 * 3. When a request is received:
 * 3.1 Parse into components: channel key, int, string
 * 3.2 Look up registered channel listeners
 * 3.3 Call the channel (prim) remote data method
 * 3.4 Capture the response (llRemoteDataReply)
 * 3.5 Return response to client caller
 * 3.6 If no response from llRemoteDataReply within
 *     RemoteReplyScriptTimeout, generate script timeout fault
 *
 * Prims in script must:
 * 1. Open a remote data channel
 * 1.1 Generate a channel ID
 * 1.2 Register primid,channelid pair with module
 * 2. Implement the remote data procedure handler
 *
 * llOpenRemoteDataChannel
 * llRemoteDataReply
 * remote_data(integer type, key channel, key messageid, string sender, integer ival, string sval)
 * llCloseRemoteDataChannel
 *
 * **************************************************/

namespace WhiteCore.Modules.Scripting
{
    public class XMLRPCModule : IService, INonSharedRegionModule, IXMLRPC
    {
        IRegistryCore m_registry;
        protected IHttpServer m_server;
        
        readonly object XMLRPCListLock = new object();
        int RemoteReplyScriptTimeout = 9000;
        int RemoteReplyScriptWait = 300;
        int m_remoteDataPort = 20800;
        bool m_httpServerStarted;

        string m_name = "XMLRPCModule";

        // <channel id, RPCChannelInfo>
        Dictionary<UUID, RPCChannelInfo> m_openChannels;
        Dictionary<UUID, SendRemoteDataRequest> m_pendingSRDResponses;

        Dictionary<UUID, RPCRequestInfo> m_rpcPending;
        Dictionary<UUID, RPCRequestInfo> m_rpcPendingResponses;
        IScriptModule m_scriptModule;

        #region IService Members
        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
            if (config.Configs ["XMLRPC"] != null)
                m_remoteDataPort = config.Configs ["XMLRPC"].GetInt ("XmlRpcPort", m_remoteDataPort);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
            if (IsEnabled () && ! ServerStarted())
            {
                m_httpServerStarted = true;
                // Start http server
                // Attach xmlrpc handlers
                MainConsole.Instance.Info ("[XMLRPC]: " +
                    "Starting up XMLRPC Server on port " + m_remoteDataPort +
                    " for llRemoteData commands.");
                //IHttpServer httpServer = new BaseHttpServer ((uint)m_remoteDataPort, MainServer.Instance.HostName,
                //    false, 1);
                //httpServer.AddXmlRPCHandler ("llRemoteData", XmlRpcRemoteData);
                //httpServer.Start ();

                var simBase = m_registry.RequestModuleInterface<ISimulationBase> ();
                m_server = simBase.GetHttpServer ((uint)m_remoteDataPort);
                m_server.AddXmlRPCHandler ("llRemoteData", XmlRpcRemoteData);

            }
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {

            // We need to create these early because the scripts might be calling
            // But since this gets called for every region, we need to make sure they
            // get called only one time (or we lose any open channels)
            if (m_openChannels == null)
            {
                m_openChannels = new Dictionary<UUID, RPCChannelInfo>();
                m_rpcPending = new Dictionary<UUID, RPCRequestInfo>();
                m_rpcPendingResponses = new Dictionary<UUID, RPCRequestInfo>();
                m_pendingSRDResponses = new Dictionary<UUID, SendRemoteDataRequest>();

            }
        }

        public void AddRegion (IScene scene)
        {
            scene.RegisterModuleInterface<IXMLRPC>(this);
        }

        public void RemoveRegion(IScene scene)
        {
            scene.UnregisterModuleInterface<IXMLRPC>(this);
        }

        public void RegionLoaded(IScene scene)
        {
            m_scriptModule = scene.RequestModuleInterface<IScriptModule>();
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        #endregion

        #region IXMLRPC Members

        public int Port
        {
            get { return m_remoteDataPort; }
        }

        public bool IsEnabled()
        {
            return (m_remoteDataPort > 0);
        }

        public bool ServerStarted()
        {
            return m_httpServerStarted;
        }

        /**********************************************
         * OpenXMLRPCChannel
         *
         * Generate a UUID channel key and add it and
         * the prim id to dictionary <channelUUID, primUUID>
         *
         * A custom channel key can be proposed.
         * Otherwise, passing UUID.Zero will generate
         * and return a random channel
         *
         * First check if there is a channel assigned for
         * this itemID.  If there is, then someone called
         * llOpenRemoteDataChannel twice.  Just return the
         * original channel.  Other option is to delete the
         * current channel and assign a new one.
         *
         * ********************************************/

        public UUID OpenXMLRPCChannel(UUID primID, UUID itemID, UUID channelID)
        {
            UUID newChannel = UUID.Zero;

            // This should no longer happen, but the check is reasonable anyway
            if (null == m_openChannels)
            {
                MainConsole.Instance.Warn("[XMLRPC]: Attempt to open channel before initialization is complete");
                return newChannel;
            }

            //Is a dupe?
            foreach (RPCChannelInfo ci in m_openChannels.Values.Where(ci => ci.GetItemID().Equals(itemID)))
            {
                // return the original channel ID for this item
                newChannel = ci.GetChannelID();
                break;
            }

            if (newChannel == UUID.Zero)
            {
                newChannel = (channelID == UUID.Zero) ? UUID.Random() : channelID;
                RPCChannelInfo rpcChanInfo = new RPCChannelInfo(primID, itemID, newChannel);
                lock (XMLRPCListLock)
                {
                    m_openChannels.Add(newChannel, rpcChanInfo);
                }
            }

            //Make sure that the cmd handler thread is running
            m_scriptModule.PokeThreads(itemID);

            return newChannel;
        }

        // Delete channels based on itemID
        // for when a script is deleted
        public void DeleteChannels(UUID itemID)
        {
            if (m_openChannels != null)
            {
                lock (XMLRPCListLock)
                {
                    int cnt = m_openChannels.Values.Count(li => li.GetItemID().Equals(itemID));
                    for(int i = 0; i < cnt; i++)
                        m_openChannels.Remove(itemID);
                }
            }

            //Make sure that the cmd handler thread is running
            m_scriptModule.PokeThreads(itemID);
        }

        /**********************************************
         * Remote Data Reply
         *
         * Response to RPC message
         *
         *********************************************/

        public void RemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            UUID message_key = new UUID(message_id);
            UUID channel_key = new UUID(channel);

            RPCRequestInfo rpcInfo = null;

            if (message_key == UUID.Zero)
            {
                foreach (
                    RPCRequestInfo oneRpcInfo in
                        m_rpcPendingResponses.Values.Where(oneRpcInfo => oneRpcInfo.GetChannelKey() == channel_key))
                    rpcInfo = oneRpcInfo;
            }
            else
            {
                m_rpcPendingResponses.TryGetValue(message_key, out rpcInfo);
            }

            if (rpcInfo != null)
            {
                rpcInfo.SetStrRetval(sdata);
                rpcInfo.SetIntRetval(idata);
                rpcInfo.SetProcessed(true);
                m_rpcPendingResponses.Remove(message_key);

                //Make sure that the cmd handler thread is running
                m_scriptModule.PokeThreads(rpcInfo.GetItemID());
            }
            else
            {
                MainConsole.Instance.Warn("[XMLRPC]: Channel or message_id not found");
            }
        }

        /**********************************************
         * CloseXMLRPCChannel
         *
         * Remove channel from dictionary
         *
         *********************************************/

        public void CloseXMLRPCChannel(UUID channelKey)
        {
            if (m_openChannels.ContainsKey(channelKey))
                m_openChannels.Remove(channelKey);
        }


        public bool hasRequests()
        {
            lock (XMLRPCListLock)
            {
                if (m_rpcPending != null)
                    if (m_rpcPending.Count > 0)
                        return true;
                if (m_pendingSRDResponses != null)
                    if (m_pendingSRDResponses.Count > 0)
                        return true;
                return false;
            }
        }

        public IXmlRpcRequestInfo GetNextCompletedRequest()
        {
            if (m_rpcPending != null)
            {
                if (m_rpcPending.Count == 0)
                    return null;
                lock (XMLRPCListLock)
                {
                    foreach (RPCRequestInfo luid in m_rpcPending.Values.Where(luid => !luid.IsProcessed()))
                    {
                        return luid;
                    }
                }
            }
            return null;
        }

        public void RemoveCompletedRequest(UUID id)
        {
            lock (XMLRPCListLock)
            {
                RPCRequestInfo tmp;
                if (m_rpcPending.TryGetValue(id, out tmp))
                {
                    m_rpcPending.Remove(id);
                    m_rpcPendingResponses.Add(id, tmp);
                }
                else
                {
                    MainConsole.Instance.Error("[XMLRPC]: Unable to complete request");
                }
            }
        }

        public UUID SendRemoteData(UUID primID, UUID itemID, string channel, string dest, int idata, string sdata)
        {
            SendRemoteDataRequest req = new SendRemoteDataRequest(
                primID, itemID, channel, dest, idata, sdata
                );
            m_pendingSRDResponses.Add(req.GetReqID(), req);
            req.Process();

            //Make sure that the cmd handler thread is running
            m_scriptModule.PokeThreads(itemID);
            return req.ReqID;
        }

        public IServiceRequest GetNextCompletedSRDRequest()
        {
            if (m_pendingSRDResponses != null)
            {
                if (m_pendingSRDResponses.Count == 0)
                    return null;
                lock (XMLRPCListLock)
                {
                    foreach (SendRemoteDataRequest luid in m_pendingSRDResponses.Values.Where(luid => luid.Finished))
                    {
                        return luid;
                    }
                }
            }
            return null;
        }

        public void RemoveCompletedSRDRequest(UUID id)
        {
            lock (XMLRPCListLock)
            {
                SendRemoteDataRequest tmpReq;
                if (m_pendingSRDResponses.TryGetValue(id, out tmpReq))
                {
                    m_pendingSRDResponses.Remove(id);
                }
            }
        }

        public void CancelSRDRequests(UUID itemID)
        {
            if (m_pendingSRDResponses != null)
            {
                lock (XMLRPCListLock)
                {
                    foreach (
                        SendRemoteDataRequest li in m_pendingSRDResponses.Values.Where(li => li.ItemID.Equals(itemID)))
                    {
                        m_pendingSRDResponses.Remove(li.GetReqID());
                    }
                }
            }
        }

        #endregion

        public XmlRpcResponse XmlRpcRemoteData(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            Hashtable requestData = (Hashtable) request.Params[0];
            bool GoodXML = (requestData.Contains("Channel") && requestData.Contains("IntValue") &&
                            requestData.Contains("StringValue"));

            if (GoodXML)
            {
                UUID channel = new UUID((string) requestData["Channel"]);
                RPCChannelInfo rpcChanInfo;
                if (m_openChannels.TryGetValue(channel, out rpcChanInfo))
                {
                    string intVal = Convert.ToInt32(requestData["IntValue"]).ToString();
                    string strVal = (string) requestData["StringValue"];

                    RPCRequestInfo rpcInfo;

                    lock (XMLRPCListLock)
                    {
                        rpcInfo =
                            new RPCRequestInfo(rpcChanInfo.GetPrimID(), rpcChanInfo.GetItemID(), channel, strVal,
                                               intVal);
                        m_rpcPending.Add(rpcInfo.GetMessageID(), rpcInfo);
                    }

                    int timeoutCtr = 0;

                    while (!rpcInfo.IsProcessed() && (timeoutCtr < RemoteReplyScriptTimeout))
                    {
                        Thread.Sleep(RemoteReplyScriptWait);
                        timeoutCtr += RemoteReplyScriptWait;
                    }
                    if (rpcInfo.IsProcessed())
                    {
                        Hashtable param = new Hashtable();
                        param["StringValue"] = rpcInfo.GetStrRetval();
                        param["IntValue"] = rpcInfo.GetIntRetval();

                        ArrayList parameters = new ArrayList {param};

                        response.Value = parameters;
                        rpcInfo = null;
                    }
                    else
                    {
                        response.SetFault(-1, "Script timeout");
                        rpcInfo = null;
                    }
                }
                else
                {
                    response.SetFault(-1, "Invalid channel");
                }
            }

            //Make sure that the cmd handler thread is running
            m_scriptModule.PokeThreads(UUID.Zero);

            return response;
        }
    }

    public class RPCRequestInfo : IXmlRpcRequestInfo
    {
        readonly UUID m_ChannelKey;
        readonly string m_IntVal;
        readonly UUID m_ItemID;
        readonly UUID m_MessageID;
        readonly UUID m_PrimID;
        readonly string m_StrVal;
        bool m_processed;
        int m_respInt;
        string m_respStr;

        public RPCRequestInfo(UUID primID, UUID itemID, UUID channelKey, string strVal, string intVal)
        {
            m_PrimID = primID;
            m_StrVal = strVal;
            m_IntVal = intVal;
            m_ItemID = itemID;
            m_ChannelKey = channelKey;
            m_MessageID = UUID.Random();
            m_processed = false;
            m_respStr = string.Empty;
            m_respInt = 0;
        }

        #region IXmlRpcRequestInfo Members

        public bool IsProcessed()
        {
            return m_processed;
        }

        public UUID GetChannelKey()
        {
            return m_ChannelKey;
        }

        public void SetProcessed(bool processed)
        {
            m_processed = processed;
        }

        public void SetStrRetval(string resp)
        {
            m_respStr = resp;
        }

        public string GetStrRetval()
        {
            return m_respStr;
        }

        public void SetIntRetval(int resp)
        {
            m_respInt = resp;
        }

        public int GetIntRetval()
        {
            return m_respInt;
        }

        public UUID GetPrimID()
        {
            return m_PrimID;
        }

        public UUID GetItemID()
        {
            return m_ItemID;
        }

        public string GetStrVal()
        {
            return m_StrVal;
        }

        public int GetIntValue()
        {
            return int.Parse(m_IntVal);
        }

        public UUID GetMessageID()
        {
            return m_MessageID;
        }

        #endregion
    }

    public class RPCChannelInfo
    {
        readonly UUID m_ChannelKey;
        readonly UUID m_itemID;
        readonly UUID m_primID;

        public RPCChannelInfo(UUID primID, UUID itemID, UUID channelID)
        {
            m_ChannelKey = channelID;
            m_primID = primID;
            m_itemID = itemID;
        }

        public UUID GetItemID()
        {
            return m_itemID;
        }

        public UUID GetChannelID()
        {
            return m_ChannelKey;
        }

        public UUID GetPrimID()
        {
            return m_primID;
        }
    }

    public class SendRemoteDataRequest : ISendRemoteDataRequest
    {
        public string Channel { get; set; }
        public string DestURL { get; set; }
        public int Idata { get; set; }

        public XmlRpcRequest Request { get; set; }
        public int ResponseIdata { get; set; }
        public string ResponseSdata { get; set; }
        public string Sdata { get; set; }
        bool _finished;
        Thread httpThread;

        public SendRemoteDataRequest(UUID primID, UUID itemID, string channel, string dest, int idata, string sdata)
        {
            Channel = channel;
            DestURL = dest;
            Idata = idata;
            Sdata = sdata;
            ItemID = itemID;
            PrimID = primID;

            ReqID = UUID.Random();
        }

        #region IServiceRequest Members

        public bool Finished
        {
            get { return _finished; }
            set { _finished = value; }
        }

        public UUID ItemID { get; set; }

        public UUID PrimID { get; set; }

        public UUID ReqID { get; set; }

        public void Process()
        {
            httpThread = new Thread(SendRequest)
                             {Name = "HttpRequestThread", Priority = ThreadPriority.BelowNormal, IsBackground = true};
            _finished = false;
            httpThread.Start();
        }

        /*
         * TODO: More work on the response codes.  Right now
         * returning 200 for success or 499 for exception
         */

        public void SendRequest()
        {
            Culture.SetCurrentCulture();
            Hashtable param = new Hashtable();

            // Check if channel is an UUID
            // if not, use as method name
            UUID parseUID;
            string mName = "llRemoteData";
            if (!string.IsNullOrEmpty(Channel))
                if (!UUID.TryParse(Channel, out parseUID))
                    mName = Channel;
                else
                    param["Channel"] = Channel;

            param["StringValue"] = Sdata;
            param["IntValue"] = Convert.ToString(Idata);

            ArrayList parameters = new ArrayList {param};
            XmlRpcRequest req = new XmlRpcRequest(mName, parameters);
            try
            {
                XmlRpcResponse resp = req.Send(DestURL, 30000);
                if (resp != null)
                {
                    Hashtable respParms;
                    if (resp.Value.GetType().Equals(typeof (Hashtable)))
                    {
                        respParms = (Hashtable) resp.Value;
                    }
                    else
                    {
                        ArrayList respData = (ArrayList) resp.Value;
                        respParms = (Hashtable) respData[0];
                    }
                    if (respParms != null)
                    {
                        if (respParms.Contains("StringValue"))
                        {
                            Sdata = (string) respParms["StringValue"];
                        }
                        if (respParms.Contains("IntValue"))
                        {
                            Idata = Convert.ToInt32(respParms["IntValue"]);
                        }
                        if (respParms.Contains("faultString"))
                        {
                            Sdata = (string) respParms["faultString"];
                        }
                        if (respParms.Contains("faultCode"))
                        {
                            Idata = Convert.ToInt32(respParms["faultCode"]);
                        }
                    }
                }
            }
            catch (Exception we)
            {
                Sdata = we.Message;
                MainConsole.Instance.Warn("[SendRemoteDataRequest]: Request failed");
                MainConsole.Instance.Warn(we.StackTrace);
            }

            _finished = true;
        }

        public void Stop()
        {
            try
            {
                httpThread.Abort();
            }
            catch (Exception)
            {
            }
        }

        #endregion

        public UUID GetReqID()
        {
            return ReqID;
        }
    }
}
