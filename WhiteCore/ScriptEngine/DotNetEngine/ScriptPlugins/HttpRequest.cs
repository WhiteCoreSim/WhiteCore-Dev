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
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;

namespace WhiteCore.ScriptEngine.DotNetEngine.Plugins
{
    public class HttpRequestPlugin : IScriptPlugin
    {
        readonly List<IHttpRequestModule> m_modules = new List<IHttpRequestModule>();
        public ScriptEngine m_ScriptEngine;

        #region IScriptPlugin Members

        public bool RemoveOnStateChange
        {
            get { return false; }
        }

        public void Initialize(ScriptEngine engine)
        {
            m_ScriptEngine = engine;
        }

        public void AddRegion(IScene scene)
        {
            m_modules.Add(scene.RequestModuleInterface<IHttpRequestModule>());
        }

        public bool Check()
        {
            bool needToContinue = false;
            foreach (IHttpRequestModule iHttpReq in m_modules)
            {
                IServiceRequest httpInfo = null;

                if (iHttpReq != null)
                {
                    httpInfo = iHttpReq.GetNextCompletedRequest();
                    if (!needToContinue)
                        needToContinue = iHttpReq.GetRequestCount() > 0;
                }

                if (httpInfo == null)
                    continue;

                while (httpInfo != null)
                {
                    IHttpRequestClass info = (IHttpRequestClass) httpInfo;
                    //MainConsole.Instance.Debug("[AsyncLSL]:" + httpInfo.response_body + httpInfo.status);

                    // Deliver data to prim's remote_data handler

                    iHttpReq.RemoveCompletedRequest(info);

                    object[] resobj = {
                        new LSL_Types.LSLString(info.ReqID.ToString()),
                        new LSL_Types.LSLInteger(info.Status),
                        new LSL_Types.list(info.Metadata),
                        new LSL_Types.LSLString(info.ResponseBody)
                    };

                    m_ScriptEngine.AddToObjectQueue(info.PrimID, "http_response", new DetectParams[0], resobj);
                    if (info.Status == 499 && //Too many for this prim
                        info.VerbroseThrottle)
                    {
                        ISceneChildEntity part = m_ScriptEngine.Scene.GetSceneObjectPart(info.PrimID);
                        if (part != null)
                        {
                            IChatModule chatModule = m_ScriptEngine.Scene.RequestModuleInterface<IChatModule>();
                            if (chatModule != null)
                                chatModule.SimChat(
                                    part.Name + "(" + part.AbsolutePosition +
                                    ") http_response error: Too many outgoing requests.", ChatTypeEnum.DebugChannel,
                                    2147483647, part.AbsolutePosition, part.Name, part.UUID, false, m_ScriptEngine.Scene);
                        }
                    }
                    httpInfo = iHttpReq.GetNextCompletedRequest();
                }
            }
            return needToContinue;
        }

        public string Name
        {
            get { return "HttpRequest"; }
        }

        public OSD GetSerializationData(UUID itemID, UUID primID)
        {
            return "";
        }

        public void CreateFromData(UUID itemID, UUID objectID, OSD data)
        {
        }

        public void RemoveScript(UUID primID, UUID itemID)
        {
            foreach (IHttpRequestModule iHttpReq in m_modules)
            {
                iHttpReq.StopHttpRequest(primID, itemID);
            }
        }

        #endregion

        public void Dispose()
        {
        }
    }
}
