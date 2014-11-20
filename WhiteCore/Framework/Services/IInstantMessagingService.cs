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

using WhiteCore.Framework.ClientInterfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace WhiteCore.Framework.Services
{
    public interface IInstantMessagingService
    {
        /// <summary>
        ///     The client calls the ChatSessionRequest CAP, which in turn runs this
        ///     This creates conference sessions, does admin functions for sessions, adds users to conferences, and deals with voice calling
        /// </summary>
        /// <param name="caps"></param>
        /// <param name="req"></param>
        /// <returns></returns>
        string ChatSessionRequest(IRegionClientCapsService caps, OSDMap req);

        /// <summary>
        ///     Sends a chat message to a session
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="im"></param>
        void SendChatToSession(UUID agentID, GridInstantMessage im);

        /// <summary>
        ///     Removes a member from a session
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="im"></param>
        void DropMemberFromSession(UUID agentID, GridInstantMessage im);

        /// <summary>
        ///     Creates a new group conference session by the given user (will not replace an existing session)
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="im"></param>
        void CreateGroupChat(UUID agentID, GridInstantMessage im);

        /// <summary>
        ///     Checks to make sure a group conference session exsits for the given group
        /// </summary>
        /// <param name="groupID"></param>
        void EnsureSessionIsStarted(UUID groupID);
    }
}