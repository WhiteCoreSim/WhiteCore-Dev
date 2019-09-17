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


using System.Text;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Services;

namespace WhiteCore.Modules.Archivers
{
    /// <summary>
    ///     Resolves OpenSim Profile Anchors (OSPA).  An OSPA is a string used to provide information for
    ///     identifying user profiles or supplying a simple name if no profile is available.
    /// </summary>
    public class OspResolver
    {
        public const string OSPA_PREFIX = "ospa:";
        public const string OSPA_NAME_KEY = "n";
        public const string OSPA_NAME_VALUE_SEPARATOR = " ";
        public const string OSPA_TUPLE_SEPARATOR = "|";
        public const string OSPA_PAIR_SEPARATOR = "=";
        public static readonly char [] OSPA_TUPLE_SEPARATOR_ARRAY = OSPA_TUPLE_SEPARATOR.ToCharArray ();

        /// <summary>
        ///     Make an OSPA given a user UUID
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userService"></param>
        /// <returns>The OSPA.  Null if a user with the given UUID could not be found.</returns>
        public static string MakeOspa (UUID userId, IUserAccountService userService)
        {
            UserAccount userAcct = userService.GetUserAccount (null, userId);
            if (userAcct.Valid)
                return MakeOspa (userAcct.FirstName, userAcct.LastName);

            return null;
        }

        /// <summary>
        ///     Make an OSPA given a user name
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <returns></returns>
        public static string MakeOspa (string firstName, string lastName)
        {
            return
                OSPA_PREFIX + OSPA_NAME_KEY + OSPA_PAIR_SEPARATOR + firstName + OSPA_NAME_VALUE_SEPARATOR + lastName;
        }

        /// <summary>
        ///     Resolve an osp string into the most suitable internal OpenSim identifier.
        /// </summary>
        /// In some cases this will be a UUID if a suitable profile exists on the system.  In other cases, this may
        /// just return the same identifier after creating a temporary profile.
        /// <param name="ospa"></param>
        /// <param name="userService"></param>
        /// <returns>
        ///     A suitable UUID for use in Second Life client communication.  If the string was not a valid ospa, then UUID.Zero
        ///     is returned.
        /// </returns>
        public static UUID ResolveOspa (string ospa, IUserAccountService userService)
        {
            if (!ospa.StartsWith (OSPA_PREFIX, System.StringComparison.Ordinal))
                return UUID.Zero;

            //            MainConsole.Instance.DebugFormat("[OSP RESOLVER]: Resolving {0}", ospa);

            string ospaMeat = ospa.Substring (OSPA_PREFIX.Length);
            string [] ospaTuples = ospaMeat.Split (OSPA_TUPLE_SEPARATOR_ARRAY);

            foreach (string tuple in ospaTuples) {
                int tupleSeparatorIndex = tuple.IndexOf (OSPA_PAIR_SEPARATOR, System.StringComparison.Ordinal);

                if (tupleSeparatorIndex < 0) {
                    MainConsole.Instance.WarnFormat ("[OSP RESOLVER]: Ignoring non-tuple component {0} in OSPA {1}",
                                                    tuple, ospa);
                    continue;
                }

                string key = tuple.Remove (tupleSeparatorIndex).Trim ();
                string value = tuple.Substring (tupleSeparatorIndex + 1).Trim ();

                if (OSPA_NAME_KEY == key)
                    return ResolveOspaName (value, userService);
            }

            return UUID.Zero;
        }

        /// <summary>
        ///     Hash a profile name into a UUID
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static UUID HashName (string name)
        {
            return new UUID (Utils.MD5 (Encoding.Unicode.GetBytes (name)), 0);
        }

        /// <summary>
        ///     Resolve an OSPI name by querying existing persistent user profiles.  If there is no persistent user profile
        ///     then a temporary user profile is inserted in the cache.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="userService"></param>
        /// <returns>
        ///     An OpenSim internal identifier for the name given.  Returns null if the name was not valid
        /// </returns>
        protected static UUID ResolveOspaName (string name, IUserAccountService userService)
        {
            if (userService == null)
                return UUID.Zero;

            int nameSeparatorIndex = name.IndexOf (OSPA_NAME_VALUE_SEPARATOR, System.StringComparison.Ordinal);

            if (nameSeparatorIndex < 0) {
                MainConsole.Instance.WarnFormat ("[OSP RESOLVER]: Ignoring un-separated name {0}", name);
                return UUID.Zero;
            }

            string firstName = name.Remove (nameSeparatorIndex).TrimEnd ();
            string lastName = name.Substring (nameSeparatorIndex + 1).TrimStart ();

            UserAccount userAcct = userService.GetUserAccount (null, firstName, lastName);
            if (userAcct.Valid)
                return userAcct.PrincipalID;

            // XXX: Disable temporary user profile creation for now as implementation is incomplete - justincc
            /*
            UserProfileData tempUserProfile = new UserProfileData();
            tempUserProfile.FirstName = firstName;
            tempUserProfile.SurName = lastName;
            tempUserProfile.ID = HashName(tempUserProfile.Name);
            
            MainConsole.Instance.DebugFormat(
                "[OSP RESOLVER]: Adding temporary user profile for {0} {1}", tempUserProfile.Name, tempUserProfile.ID);
            commsManager.UserService.AddTemporaryUserProfile(tempUserProfile);
            
            return tempUserProfile.ID;
            */

            return UUID.Zero;
        }
    }
}