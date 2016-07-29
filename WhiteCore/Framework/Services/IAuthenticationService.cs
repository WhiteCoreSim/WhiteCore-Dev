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

using OpenMetaverse;

namespace WhiteCore.Framework.Services
{
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need 
    // verifiable identification.
    //
    public interface IAuthenticationService
    {
        //////////////////////////////////////////////////////
        // Authentication
        //
        // These methods will return a token, which can be used to access
        // various services.
        //
        string Authenticate(UUID principalID, string authType, string password, int lifetime);

        //////////////////////////////////////////////////////
        // Verification
        //
        // Allows to verify the authenticity of a token
        //
        // Tokens expire after 30 minutes and can be refreshed by
        // re-verifying.
        //
        bool Verify(UUID principalID, string authType, string token, int lifetime);

        //////////////////////////////////////////////////////
        // Teardown
        //
        // A token can be returned before the timeout. This
        // invalidates it and it can not subsequently be used
        // or refreshed.
        //
        bool Release(UUID principalID, string authType, string token);

        //////////////////////////////////////////////////////
        // SetPassword for a principal
        //
        // This method exists for the service, but may or may not
        // be served remotely. That is, the authentication
        // handlers may not include one handler for this,
        // because it's a bit risky. Such handlers require
        // authentication/authorization.
        //
        bool SetPassword(UUID principalID, string authType, string passwd);
        bool SetPasswordHashed(UUID UUID, string authType, string passwd);
        bool SetPlainPassword(UUID principalID, string authType, string passwd);
        bool SetSaltedPassword (UUID principalID, string authType, string salt, string passwd);

        /// <summary>
        ///     Check whether the given principalID has a password set
        /// </summary>
        /// <param name="principalID"></param>
        /// <param name="authType"></param>
        /// <returns></returns>
        bool CheckExists(UUID principalID, string authType);

        //////////////////////////////////////////////////////
        // Grid
        //
        // We no longer need a shared secret between grid
        // servers. Anything a server requests from another
        // server is either done on behalf of a user, in which
        // case there is a token, or on behalf of a region,
        // which has a session. So, no more keys.
        // If sniffing on the local lan is an issue, admins
        // need to take approriate action (IPSec is recommended)
        // to secure inter-server traffic.

        //////////////////////////////////////////////////////
        // NOTE
        //
        // Session IDs are not handled here. After obtaining
        // a token, the session ID regions use can be
        // obtained from the presence service.
    }
}