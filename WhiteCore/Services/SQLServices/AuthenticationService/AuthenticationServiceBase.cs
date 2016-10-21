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
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Services
{
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need 
    // verifiable identification.
    //
    public class AuthenticationServiceBase
    {
        protected IAuthenticationData m_Database;
        protected bool m_authenticateUsers = true;

        /// <summary>
        /// Checks is a user exists.
        /// </summary>
        /// <returns><c>true</c>, if exists was checked, <c>false</c> otherwise.</returns>
        /// <param name="principalID">Principal identifier.</param>
        /// <param name="authType">Auth type.</param>
        public bool CheckExists (UUID principalID, string authType)
        {
            return m_Database.Get (principalID, authType) != null;
        }

        public bool Verify (UUID principalID, string authType, string token, int lifetime)
        {
            return m_Database.CheckToken (principalID, token, lifetime);
        }

        public virtual bool Release (UUID principalID, string token, string authType)
        {
            return m_Database.CheckToken (principalID, token, 0);
        }

        /// <summary>
        /// Sets the password for a user. Password is saved as an md5 hash
        /// </summary>
        /// <returns><c>true</c>, if password was set, <c>false</c> otherwise.</returns>
        /// <param name="principalID">Principal identifier.</param>
        /// <param name="authType">Auth type.</param>
        /// <param name="password">Password.</param>
        public virtual bool SetPassword (UUID principalID, string authType, string password)
        {
            string passwordSalt = Util.Md5Hash (UUID.Random ().ToString ());
            string md5PasswdHash = Util.Md5Hash (Util.Md5Hash (password) + ":" + passwordSalt);

            AuthData auth = m_Database.Get (principalID, authType);
            if (auth == null) {
                auth = new AuthData { PrincipalID = principalID, AccountType = authType };
            }
            auth.PasswordHash = md5PasswdHash;
            auth.PasswordSalt = passwordSalt;
            return SaveAuth (auth, principalID);
        }

        /// <summary>
        /// Remove the specified user.
        /// </summary>
        /// <param name="principalID">Principal identifier.</param>
        /// <param name="authType">Auth type.</param>
        public virtual bool Remove (UUID principalID, string authType)
        {
            return m_Database.Delete (principalID, authType);
        }

        protected string GetToken (UUID principalID, int lifetime)
        {
            UUID token = UUID.Random ();

            if (m_Database.SetToken (principalID, token.ToString (), lifetime))
                return token.ToString ();

            return string.Empty;
        }

        /// <summary>
        /// Sets a hashed password for a user (default use).
        /// </summary>
        /// <returns><c>true</c>, if password hashed was set, <c>false</c> otherwise.</returns>
        /// <param name="principalID">Principal identifier.</param>
        /// <param name="authType">Auth type.</param>
        /// <param name="passHash">Password hash.</param>
        public virtual bool SetPasswordHashed (UUID principalID, string authType, string passHash)
        {
            string passwordSalt = Util.Md5Hash (UUID.Random ().ToString ());
            string md5PasswdHash = Util.Md5Hash (passHash + ":" + passwordSalt);

            AuthData auth = m_Database.Get (principalID, authType);
            if (auth == null) {
                auth = new AuthData { PrincipalID = principalID, AccountType = authType };
            }
            auth.PasswordHash = md5PasswdHash;
            auth.PasswordSalt = passwordSalt;
            return SaveAuth (auth, principalID);
        }

        /// <summary>
        /// Sets a plain password for a user.
        /// </summary>
        /// <returns><c>true</c>, if plain password was set, <c>false</c> otherwise.</returns>
        /// <param name="principalID">Principal identifier.</param>
        /// <param name="authType">Auth type.</param>
        /// <param name="pass">Password.</param>
        public virtual bool SetPlainPassword (UUID principalID, string authType, string pass)
        {
            AuthData auth = m_Database.Get (principalID, authType);
            if (auth == null) {
                auth = new AuthData { PrincipalID = principalID, AccountType = authType };
            }
            auth.PasswordHash = pass;
            auth.PasswordSalt = "";
            return SaveAuth (auth, principalID);
        }

        /// <summary>
        /// Sets the salted password for a user.
        /// </summary>
        /// <returns><c>true</c>, if salted password was set, <c>false</c> otherwise.</returns>
        /// <param name="principalID">Principal identifier.</param>
        /// <param name="authType">Auth type.</param>
        /// <param name="passHash">Password hash.</param>
        /// <param name="saltHash">Salt hash.</param>
        public virtual bool SetSaltedPassword (UUID principalID, string authType, string passHash, string saltHash)
        {
            AuthData auth = m_Database.Get (principalID, authType);
            if (auth == null) {
                auth = new AuthData { PrincipalID = principalID, AccountType = authType };
            }
            auth.PasswordHash = passHash;
            auth.PasswordSalt = saltHash;
            return SaveAuth (auth, principalID);
        }

        bool SaveAuth (AuthData auth, UUID principalID)
        {
            if (!m_Database.Store (auth)) {
                MainConsole.Instance.DebugFormat ("[Authentication DB]: Failed to store authentication data");
                return false;
            }

            MainConsole.Instance.InfoFormat ("[Authentication DB]: Set password for principalID {0}", principalID);
            return true;

        }

    }
}
