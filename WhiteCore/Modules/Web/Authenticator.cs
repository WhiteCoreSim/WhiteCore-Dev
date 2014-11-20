/*
 * Copyright (c) Contributors, http://whitecore-sim.org/
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

using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;
using OpenMetaverse;
using System.Collections.Generic;
using System.Linq;

namespace WhiteCore.Modules.Web
{
    public class Authenticator
    {
        private static Dictionary<UUID, UserAccount> _authenticatedUsers = new Dictionary<UUID, UserAccount>();
        private static Dictionary<UUID, UserAccount> _authenticatedAdminUsers = new Dictionary<UUID, UserAccount>();

        public static bool CheckAuthentication(OSHttpRequest request)
        {
            if (request.Cookies["SessionID"] != null)
            {
                if (_authenticatedUsers.ContainsKey(UUID.Parse(request.Cookies["SessionID"].Value)))
                    return true;
            }
            return false;
        }

        public static bool CheckAdminAuthentication(OSHttpRequest request)
        {
            if (request.Cookies["SessionID"] != null)
            {
                if (_authenticatedAdminUsers.ContainsKey(UUID.Parse(request.Cookies["SessionID"].Value)))
                    return true;
            }
            return false;
        }

        public static bool CheckAdminAuthentication(OSHttpRequest request, int adminLevelRequired)
        {
            if (request.Cookies["SessionID"] != null)
            {
                var session =
                    _authenticatedAdminUsers.FirstOrDefault(
                        (acc) => acc.Key == UUID.Parse(request.Cookies["SessionID"].Value));
                if (session.Value != null && session.Value.UserLevel >= adminLevelRequired)
                    return true;
            }
            return false;
        }

        public static void AddAuthentication(UUID sessionID, UserAccount account)
        {
            _authenticatedUsers.Add(sessionID, account);
        }

        public static void AddAdminAuthentication(UUID sessionID, UserAccount account)
        {
            _authenticatedAdminUsers.Add(sessionID, account);
        }

        public static void RemoveAuthentication(OSHttpRequest request)
        {
            UUID sessionID = GetAuthenticationSession(request);
            _authenticatedUsers.Remove(sessionID);
            _authenticatedAdminUsers.Remove(sessionID);
        }

        public static UserAccount GetAuthentication(OSHttpRequest request)
        {
            if (request.Cookies["SessionID"] != null)
            {
                UUID sessionID = UUID.Parse(request.Cookies["SessionID"].Value);
                if (_authenticatedUsers.ContainsKey(sessionID))
                    return _authenticatedUsers[sessionID];
            }
            return null;
        }

        public static UUID GetAuthenticationSession(OSHttpRequest request)
        {
            if (request.Cookies["SessionID"] != null)
                return UUID.Parse(request.Cookies["SessionID"].Value);
            return UUID.Zero;
        }

        public static void ChangeAuthentication(OSHttpRequest request, UserAccount account)
        {
            if (request.Cookies["SessionID"] != null)
            {
                UUID sessionID = UUID.Parse(request.Cookies["SessionID"].Value);
                if (_authenticatedUsers.ContainsKey(sessionID))
                    _authenticatedUsers[sessionID] = account;
                if (_authenticatedAdminUsers.ContainsKey(sessionID))
                    _authenticatedAdminUsers[sessionID] = account;
            }
        }
    }
}