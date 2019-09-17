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
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Physics;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Framework.Servers;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Services.ClassHelpers.Assets;
using WhiteCore.Framework.Services.ClassHelpers.Inventory;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using WhiteCore.Framework.Utilities;
using WhiteCore.ScriptEngine.DotNetEngine.Plugins;
using WhiteCore.ScriptEngine.DotNetEngine.Runtime;
using GridRegion = WhiteCore.Framework.Services.GridRegion;
using LSL_Float = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLFloat;
using LSL_Integer = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLInteger;
using LSL_Key = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_List = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.List;
using LSL_Rotation = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Quaternion;
using LSL_String = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.LSLString;
using LSL_Vector = WhiteCore.ScriptEngine.DotNetEngine.LSL_Types.Vector3;
using PrimType = WhiteCore.Framework.SceneInfo.PrimType;
using RegionFlags = WhiteCore.Framework.Services.RegionFlags;

namespace WhiteCore.ScriptEngine.DotNetEngine.APIs
{
    public partial class LSL_Api : MarshalByRefObject, IScriptApi
    {
        public LSL_String llEscapeURL(string url) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_String();

            try {
                return Uri.EscapeDataString(url);
            } catch (Exception ex) {
                return "llEscapeURL: " + ex;
            }
        }

        public LSL_String llUnescapeURL(string url) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            try {
                return Uri.UnescapeDataString(url);
            } catch (Exception ex) {
                return "llUnescapeURL: " + ex;
            }
        }

        public LSL_String llHTTPRequest(string url, LSL_List parameters, string body) {
            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/LlHTTPRequest
            // parameter flags support are implemented in ScriptsHttpRequests.cs
            //   in StartHttpRequest

            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return "";

            IHttpRequestModule httpScriptMod =
                World.RequestModuleInterface<IHttpRequestModule>();

            List<string> param = parameters.Data.Select(o => o.ToString()).ToList();

            Vector3 position = m_host.AbsolutePosition;
            Vector3 velocity = m_host.Velocity;
            Quaternion rotation = m_host.GetRotationOffset();
            string ownerName = string.Empty;
            IScenePresence scenePresence = World.GetScenePresence(m_host.OwnerID);
            ownerName = scenePresence == null ? resolveName(m_host.OwnerID) : scenePresence.Name;

            RegionInfo regionInfo = World.RegionInfo;

            Dictionary<string, string> httpHeaders = new Dictionary<string, string>();

            string shard = "OpenSim";
            IConfigSource config = m_ScriptEngine.ConfigSource;
            if (config.Configs["LSLRemoting"] != null)
                shard = config.Configs["LSLRemoting"].GetString("shard", shard);

            httpHeaders["X-SecondLife-Shard"] = shard;
            httpHeaders["X-SecondLife-Object-Name"] = m_host.Name;
            httpHeaders["X-SecondLife-Object-Key"] = m_host.UUID.ToString();
            httpHeaders["X-SecondLife-Region"] = string.Format("{0} ({1}, {2})", regionInfo.RegionName,
                                                               regionInfo.RegionLocX, regionInfo.RegionLocY);
            httpHeaders["X-SecondLife-Local-Position"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})",
                                                                       position.X, position.Y, position.Z);
            httpHeaders["X-SecondLife-Local-Velocity"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})",
                                                                       velocity.X, velocity.Y, velocity.Z);
            httpHeaders["X-SecondLife-Local-Rotation"] =
                string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000})", rotation.X, rotation.Y,
                              rotation.Z, rotation.W);
            httpHeaders["X-SecondLife-Owner-Name"] = ownerName;
            httpHeaders["X-SecondLife-Owner-Key"] = m_host.OwnerID.ToString();
            string userAgent = "";
            if (config.Configs["LSLRemoting"] != null)
                userAgent = config.Configs["LSLRemoting"].GetString("user_agent", null);

            if (userAgent != null)
                httpHeaders["User-Agent"] = userAgent;

            const string authregex = @"^(https?:\/\/)(\w+):(\w+)@(.*)$";
            Regex r = new Regex(authregex);
            Match m = r.Match(url);
            if (m.Success) {
                //for (int i = 1; i < gnums.Length; i++) {
                //    //System.Text.RegularExpressions.Group g = m.Groups[gnums[i]];
                //    //CaptureCollection cc = g.Captures;
                //}
                if (m.Groups.Count == 5) {
                    httpHeaders["Authorization"] =
                        string.Format("Basic {0}",
                                      Convert.ToBase64String(Encoding.ASCII.GetBytes(m.Groups[2] + ":" + m.Groups[3])));
                    url = m.Groups[1].ToString() + m.Groups[4];
                }
            }

            UUID reqID = httpScriptMod.
                StartHttpRequest(m_host.UUID, m_itemID, url, param, httpHeaders, body);

            if (reqID != UUID.Zero)
                return reqID.ToString();
            else
                return new LSL_String("");
        }


        public void llSetContentType(LSL_Key id, LSL_Integer type) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;

            string content_type = "text/plain";

            if (type == ScriptBaseClass.CONTENT_TYPE_HTML)
                content_type = "text/html";
            else if (type == ScriptBaseClass.CONTENT_TYPE_XML)
                content_type = "application/xml";
            else if (type == ScriptBaseClass.CONTENT_TYPE_XHTML)
                content_type = "application/xhtml+xml";
            else if (type == ScriptBaseClass.CONTENT_TYPE_ATOM)
                content_type = "application/atom+xml";
            else if (type == ScriptBaseClass.CONTENT_TYPE_JSON)
                content_type = "application/json";
            else if (type == ScriptBaseClass.CONTENT_TYPE_LLSD)
                content_type = "application/llsd+xml";
            else if (type == ScriptBaseClass.CONTENT_TYPE_FORM)
                content_type = "application/x-www-form-urlencoded";
            else if (type == ScriptBaseClass.CONTENT_TYPE_RSS)
                content_type = "application/rss+xml";
            else
                content_type = "text/plain";

            if (m_UrlModule != null)
                m_UrlModule.SetContentType(id, content_type);
        }

        public void llHTTPResponse(LSL_Key id, int status, string body) {
            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/llHTTPResponse

            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return;


            if (m_UrlModule != null)
                m_UrlModule.HttpResponse(id, status, body);
        }

        public LSL_Integer llGetFreeURLs() {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return new LSL_Integer();

            if (m_UrlModule != null)
                return new LSL_Integer(m_UrlModule.GetFreeUrls());
            return new LSL_Integer(0);
        }


        public DateTime llLoadURL(string avatar_id, string message, string url) {
            if (!ScriptProtection.CheckThreatLevel(ThreatLevel.None, "LSL", m_host, "LSL", m_itemID))
                return DateTime.Now;


            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();
            if (null != dm)
                dm.SendUrlToUser(
                    new UUID(avatar_id), m_host.Name, m_host.UUID, m_host.OwnerID, false, message, url);

            return PScriptSleep(m_sleepMsOnLoadURL);
        }



        public LSL_String llJsonGetValue(LSL_String json, LSL_List specifiers) {
            try {
                OSD o = OSDParser.DeserializeJson(json);
                OSD specVal = JsonGetSpecific(o, specifiers, 0);
                if (specVal != null)
                    return specVal.AsString();
                else
                    return ScriptBaseClass.JSON_INVALID;
            } catch (Exception) {
                return ScriptBaseClass.JSON_INVALID;
            }
        }

        public LSL_List llJson2List(LSL_String json) {
            try {
                OSD o = OSDParser.DeserializeJson(json);
                return (LSL_List)ParseJsonNode(o);
            } catch (Exception) {
                return new LSL_List(ScriptBaseClass.JSON_INVALID);
            }
        }

        object ParseJsonNode(OSD node) {
            if (node.Type == OSDType.Integer)
                return new LSL_Integer(node.AsInteger());
            if (node.Type == OSDType.Boolean)
                return new LSL_Integer(node.AsBoolean() ? 1 : 0);
            if (node.Type == OSDType.Real)
                return new LSL_Float(node.AsReal());
            if (node.Type == OSDType.UUID || node.Type == OSDType.String)
                return new LSL_String(node.AsString());
            if (node.Type == OSDType.Array) {
                LSL_List resp = new LSL_List();
                OSDArray ar = node as OSDArray;
                foreach (OSD o in ar)
                    resp.Add(ParseJsonNode(o));
                return resp;
            }
            if (node.Type == OSDType.Map) {
                LSL_List resp = new LSL_List();
                OSDMap ar = node as OSDMap;
                foreach (KeyValuePair<string, OSD> o in ar) {
                    resp.Add(new LSL_String(o.Key));
                    resp.Add(ParseJsonNode(o.Value));
                }
                return resp;
            }
            throw new Exception(ScriptBaseClass.JSON_INVALID);
        }

        public LSL_String llList2Json(LSL_String type, LSL_List values) {
            try {
                if (type == ScriptBaseClass.JSON_ARRAY) {
                    OSDArray array = new OSDArray();
                    foreach (object o in values.Data) {
                        array.Add(ListToJson(o));
                    }
                    return OSDParser.SerializeJsonString(array);
                } else if (type == ScriptBaseClass.JSON_OBJECT) {
                    OSDMap map = new OSDMap();
                    for (int i = 0; i < values.Data.Length; i += 2) {
                        if (!(values.Data[i] is LSL_String))
                            return ScriptBaseClass.JSON_INVALID;
                        map.Add(((LSL_String)values.Data[i]).m_string, ListToJson(values.Data[i + 1]));
                    }
                    return OSDParser.SerializeJsonString(map);
                }
                return ScriptBaseClass.JSON_INVALID;
            } catch (Exception ex) {
                return ex.Message;
            }
        }

        OSD ListToJson(object o) {
            if (o is LSL_Float)
                return OSD.FromReal(((LSL_Float)o).value);
            if (o is LSL_Integer) {
                int i = ((LSL_Integer)o).value;
                if (i == 0)
                    return OSD.FromBoolean(false);
                else if (i == 1)
                    return OSD.FromBoolean(true);
                return OSD.FromInteger(i);
            }
            if (o is LSL_Rotation)
                return OSD.FromString(((LSL_Rotation)o).ToString());
            if (o is LSL_Vector)
                return OSD.FromString(((LSL_Vector)o).ToString());
            if (o is LSL_String) {
                string str = ((LSL_String)o).m_string;
                if (str == ScriptBaseClass.JSON_NULL)
                    return new OSD();
                return OSD.FromString(str);
            }
            throw new Exception(ScriptBaseClass.JSON_INVALID);
        }

        OSD JsonGetSpecific(OSD o, LSL_List specifiers, int i) {
            object spec = specifiers.Data[i];
            OSD nextVal = null;
            if (o is OSDArray) {
                if (spec is LSL_Integer)
                    nextVal = ((OSDArray)o)[((LSL_Integer)spec).value];
            }
            if (o is OSDMap) {
                if (spec is LSL_String)
                    nextVal = ((OSDMap)o)[((LSL_String)spec).m_string];
            }
            if (nextVal != null) {
                if (specifiers.Data.Length - 1 > i)
                    return JsonGetSpecific(nextVal, specifiers, i + 1);
            }
            return nextVal;
        }

        public LSL_String llJsonSetValue(LSL_String json, LSL_List specifiers, LSL_String value) {
            try {
                OSD o = OSDParser.DeserializeJson(json);
                JsonSetSpecific(o, specifiers, 0, value);
                return OSDParser.SerializeJsonString(o);
            } catch (Exception) {
            }
            return ScriptBaseClass.JSON_INVALID;
        }

        void JsonSetSpecific(OSD o, LSL_List specifiers, int i, LSL_String val) {
            object spec = specifiers.Data[i];
            // 20131224 not used            object specNext = i+1 == specifiers.Data.Length ? null : specifiers.Data[i+1];
            OSD nextVal = null;
            if (o is OSDArray) {
                OSDArray array = ((OSDArray)o);
                if (spec is LSL_Integer) {
                    int v = ((LSL_Integer)spec).value;
                    if (v >= array.Count)
                        array.Add(JsonBuildRestOfSpec(specifiers, i + 1, val));
                    else
                        nextVal = ((OSDArray)o)[v];
                } else if (spec is LSL_String && ((LSL_String)spec) == ScriptBaseClass.JSON_APPEND)
                    array.Add(JsonBuildRestOfSpec(specifiers, i + 1, val));
            }
            if (o is OSDMap) {
                if (spec is LSL_String) {
                    OSDMap map = ((OSDMap)o);
                    if (map.ContainsKey(((LSL_String)spec).m_string))
                        nextVal = map[((LSL_String)spec).m_string];
                    else
                        map.Add(((LSL_String)spec).m_string, JsonBuildRestOfSpec(specifiers, i + 1, val));
                }
            }
            if (nextVal != null) {
                if (specifiers.Data.Length - 1 > i) {
                    JsonSetSpecific(nextVal, specifiers, i + 1, val);
                    return;
                }
            }
        }

        OSD JsonBuildRestOfSpec(LSL_List specifiers, int i, LSL_String val) {
            object spec = i >= specifiers.Data.Length ? null : specifiers.Data[i];
            // 20131224 not used            object specNext = i+1 >= specifiers.Data.Length ? null : specifiers.Data[i+1];

            if (spec == null)
                return OSD.FromString(val);

            if (spec is LSL_Integer ||
                (spec is LSL_String && ((LSL_String)spec) == ScriptBaseClass.JSON_APPEND)) {
                OSDArray array = new OSDArray();
                array.Add(JsonBuildRestOfSpec(specifiers, i + 1, val));
                return array;
            }
            if (spec is LSL_String) {
                OSDMap map = new OSDMap();
                map.Add((LSL_String)spec, JsonBuildRestOfSpec(specifiers, i + 1, val));
                return map;
            }
            return new OSD();
        }

        public LSL_String llJsonValueType(LSL_String json, LSL_List specifiers) {
            OSD o = OSDParser.DeserializeJson(json);
            OSD specVal = JsonGetSpecific(o, specifiers, 0);
            if (specVal == null)
                return ScriptBaseClass.JSON_INVALID;
            switch (specVal.Type) {
                case OSDType.Array:
                    return ScriptBaseClass.JSON_ARRAY;
                case OSDType.Boolean:
                    return specVal.AsBoolean() ? ScriptBaseClass.JSON_TRUE : ScriptBaseClass.JSON_FALSE;
                case OSDType.Integer:
                case OSDType.Real:
                    return ScriptBaseClass.JSON_NUMBER;
                case OSDType.Map:
                    return ScriptBaseClass.JSON_OBJECT;
                case OSDType.String:
                case OSDType.UUID:
                    return ScriptBaseClass.JSON_STRING;
                case OSDType.Unknown:
                    return ScriptBaseClass.JSON_NULL;
            }
            return ScriptBaseClass.JSON_INVALID;
        }
    }
}
