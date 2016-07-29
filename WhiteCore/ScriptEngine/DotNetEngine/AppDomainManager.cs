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
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;

#if LINUX
using System.Collections;
using System.Net;
#endif

namespace WhiteCore.ScriptEngine.DotNetEngine
{
    /// <summary>
    ///     This manages app domains and controls what app domains are created/destroyed
    /// </summary>
    public class AppDomainManager
    {
        readonly List<AppDomainStructure> appDomains = new List<AppDomainStructure> ();

        readonly object m_appDomainLock = new object ();
        readonly ScriptEngine m_scriptEngine;
        int AppDomainNameCount;
        AppDomainStructure currentAD;

        bool loadAllScriptsIntoCurrentDomain;
        bool loadAllScriptsIntoOneDomain = true;
        string m_PermissionLevel = "Internet";
        int maxScriptsPerAppDomain = 1;

        public AppDomainManager (ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
            ReadConfig ();
        }

        public string PermissionLevel {
            get { return m_PermissionLevel; }
        }

        public int NumberOfAppDomains {
            get { 
                lock (m_appDomainLock)
                    return appDomains.Count;
            }
        }

        // Internal list of all AppDomains

        public void ReadConfig()
        {
            maxScriptsPerAppDomain = m_scriptEngine.ScriptConfigSource.GetInt(
                "ScriptsPerAppDomain", 1);
            m_PermissionLevel = m_scriptEngine.ScriptConfigSource.GetString(
                "AppDomainPermissions", "Internet");
            loadAllScriptsIntoCurrentDomain =
                m_scriptEngine.ScriptConfigSource.GetBoolean("LoadAllScriptsIntoCurrentAppDomain", false);
            loadAllScriptsIntoOneDomain = m_scriptEngine.ScriptConfigSource.GetBoolean(
                "LoadAllScriptsIntoOneAppDomain", true);
        }

        // Find a free AppDomain, creating one if necessary
        AppDomainStructure GetFreeAppDomain()
        {
            // use only the current domain?
            if (loadAllScriptsIntoCurrentDomain)
            {
                // existing?
                lock (m_appDomainLock) {
                    if (currentAD != null)
                        return currentAD;
                }

                // create a new one then
                lock (m_appDomainLock) {
                    currentAD = new AppDomainStructure {CurrentAppDomain = AppDomain.CurrentDomain};
                    AppDomain.CurrentDomain.AssemblyResolve += m_scriptEngine.AssemblyResolver.OnAssemblyResolve;
                    return currentAD;
                }
            }


            // use a single script domain?
            if (loadAllScriptsIntoOneDomain) {
                lock (m_appDomainLock) {
                    if (currentAD == null) {
                        // Create a new current AppDomain
                        currentAD = new AppDomainStructure { CurrentAppDomain = PrepareNewAppDomain () };
                    }
                    return currentAD;
                }
            }

            // multiple script domains then    
            lock( m_appDomainLock) {
                // Current full?
                if (currentAD != null && currentAD.ScriptsLoaded >= maxScriptsPerAppDomain) {
                    // Add it to AppDomains list and empty current
                    appDomains.Add (currentAD);
                    currentAD = null;
                }
                // NO or New current
                if (currentAD == null) {
                    // Create a new current AppDomain
                    currentAD = new AppDomainStructure { CurrentAppDomain = PrepareNewAppDomain () };
                }
                return currentAD;
            }
        }

        // Create and prepare a new AppDomain for scripts
        AppDomain PrepareNewAppDomain()
        {
            // Create and prepare a new AppDomain
            AppDomainNameCount++;

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup
                                     {
                                         ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                                         DisallowBindingRedirects = true,
                                         DisallowCodeDownload = true,
                                         LoaderOptimization = LoaderOptimization.MultiDomainHost,
                                         ShadowCopyFiles = "false",
                                         ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
                                     };
            // Disable shadowing

            AppDomain AD = CreateRestrictedDomain(m_PermissionLevel,
                                                  "ScriptAppDomain_" + AppDomainNameCount, ads);

            AD.AssemblyResolve += m_scriptEngine.AssemblyResolver.OnAssemblyResolve;

            // Return the new AppDomain
            return AD;
        }


        /// From MRMModule.cs by Adam Frisby
        /// <summary>
        ///     Create an AppDomain that contains policy restricting code to execute
        ///     with only the permissions granted by a named permission set
        /// </summary>
        /// <param name="permissionSetName">name of the permission set to restrict to</param>
        /// <param name="appDomainName">'friendly' name of the appdomain to be created</param>
        /// <param name="ads"></param>
        /// <exception cref="ArgumentNullException">
        ///     if <paramref name="permissionSetName" /> is null
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     if <paramref name="permissionSetName" /> is empty
        /// </exception>
        /// <returns>AppDomain with a restricted security policy</returns>
        /// <remarks>
        ///     Substantial portions of this function from: http://blogs.msdn.com/shawnfa/archive/2004/10/25/247379.aspx
        ///     Valid permissionSetName values are:
        ///     * FullTrust
        ///     * SkipVerification
        ///     * Execution
        ///     * Nothing
        ///     * LocalIntranet
        ///     * Internet
        ///     * Everything
        /// </remarks>
        public AppDomain CreateRestrictedDomain(string permissionSetName, string appDomainName, AppDomainSetup ads)
        {
            if (permissionSetName == null)
                throw new ArgumentNullException("permissionSetName");
            if (permissionSetName.Length == 0)
                throw new ArgumentOutOfRangeException("permissionSetName", permissionSetName,
                                                      "Cannot have an empty permission set name");

            // Default to all code getting everything
            PermissionSet setIntersection = new PermissionSet(PermissionState.Unrestricted);
            AppDomain restrictedDomain = null;

#if LINUX
	#pragma warning disable 612, 618
            PolicyStatement emptyPolicy = new PolicyStatement(new PermissionSet(PermissionState.None));
            UnionCodeGroup policyRoot = new UnionCodeGroup(new AllMembershipCondition(), emptyPolicy);

            bool foundName = false;
            // iterate over each policy level
            IEnumerator levelEnumerator = SecurityManager.PolicyHierarchy();
            while (levelEnumerator.MoveNext())
            {
                PolicyLevel level = levelEnumerator.Current as PolicyLevel;

                // if this level has defined a named permission set with the
                // given name, then intersect it with what we've retrieved
                // from all the previous levels
                if (level != null)
                {
                    PermissionSet levelSet = level.GetNamedPermissionSet(permissionSetName);
                    if (levelSet != null)
                    {
                        foundName = true;
                        if (setIntersection != null)
                            setIntersection = setIntersection.Intersect(levelSet);
                    }
                }
            }

            // Intersect() can return null for an empty set, so convert that
            // to an empty set object. Also return an empty set if we didn't find
            // the named permission set we were looking for
            if (setIntersection == null || !foundName)
                setIntersection = new PermissionSet(PermissionState.None);
            else
                setIntersection = new NamedPermissionSet(permissionSetName, setIntersection);

            // if no named permission sets were found, return an empty set,
            // otherwise return the set that was found
            setIntersection.AddPermission(new SocketPermission(PermissionState.Unrestricted));
            setIntersection.AddPermission(new WebPermission(PermissionState.Unrestricted));
            setIntersection.AddPermission(new SecurityPermission(PermissionState.Unrestricted));

            PolicyStatement permissions = new PolicyStatement(setIntersection);
            policyRoot.AddChild(new UnionCodeGroup(new AllMembershipCondition(), permissions));

            // create an AppDomain policy level for the policy tree
            PolicyLevel appDomainLevel = PolicyLevel.CreateAppDomainLevel();
            appDomainLevel.RootCodeGroup = policyRoot;

            // create an AppDomain where this policy will be in effect
            restrictedDomain = AppDomain.CreateDomain(appDomainName, null, ads);
            restrictedDomain.SetAppDomainPolicy(appDomainLevel);
	#pragma warning restore 612, 618
#else
            SecurityZone zone = SecurityZone.MyComputer;
            try
            {
                zone = (SecurityZone) Enum.Parse(typeof (SecurityZone), permissionSetName);
            }
            catch
            {
                zone = SecurityZone.MyComputer;
            }

            Evidence ev = new Evidence();
            ev.AddHostEvidence(new Zone(zone));
            setIntersection = SecurityManager.GetStandardSandbox(ev);
            setIntersection.AddPermission(new System.Net.SocketPermission(PermissionState.Unrestricted));
            setIntersection.AddPermission(new System.Net.WebPermission(PermissionState.Unrestricted));
            setIntersection.AddPermission(
                new System.Security.Permissions.SecurityPermission(PermissionState.Unrestricted));

            // create an AppDomain where this policy will be in effect
            restrictedDomain = AppDomain.CreateDomain(appDomainName, ev, ads, setIntersection, null);
#endif

            return restrictedDomain;
        }

        // Unload appdomains that are full and have only dead scripts
        void UnloadAppDomains()
        {
            lock (m_appDomainLock)
            {
                // Go through all

                foreach (
                    AppDomainStructure ads in appDomains.Where(ads => ads.ScriptsLoaded <= ads.ScriptsWaitingUnload))
                {
                    // Remove from internal list
                    appDomains.Remove(ads);

                    try
                    {
                        // Unload
                        if (ads != null) {
                            AppDomain.Unload (ads.CurrentAppDomain);
                            if (currentAD != null) {
                                if (ads.CurrentAppDomain == currentAD.CurrentAppDomain)
                                    currentAD = null;
                            }
                        }
                    }
                    catch
                    {
                    }
                    if (ads != null)
                        ads.CurrentAppDomain = null;
                }

                if (currentAD != null)
                {
                    if (currentAD.ScriptsLoaded <= currentAD.ScriptsWaitingUnload)
                    {
                        if (currentAD.CurrentAppDomain.Id != AppDomain.CurrentDomain.Id)
                            //Don't kill the current app domain!
                        {
                            try
                            {
                                // Unload
                                AppDomain.Unload(currentAD.CurrentAppDomain);
                            }
                            catch
                            {
                            }
                            currentAD.CurrentAppDomain = null;
                            currentAD = null;
                        }
                    }
                }
            }
        }

        public IScript LoadScript(string fileName, string typeName, out AppDomain ad)
        {
            // Find next available AppDomain to put it in
            AppDomainStructure FreeAppDomain = GetFreeAppDomain();
            lock (m_appDomainLock) {
                IScript mbrt = (IScript)
                           FreeAppDomain.CurrentAppDomain.CreateInstanceFromAndUnwrap (
                               fileName, typeName);
                FreeAppDomain.ScriptsLoaded++;
                ad = FreeAppDomain.CurrentAppDomain;

                return mbrt;
            }
        }


        // Increase "dead script" counter for an AppDomain
        public void UnloadScriptAppDomain(AppDomain ad)
        {
            lock (m_appDomainLock)
            {
                // Check if it is current AppDomain
                if (currentAD.CurrentAppDomain == ad)
                {
                    // Yes - increase
                    currentAD.ScriptsWaitingUnload++;
                }
                else
                {
                    // Lopp through all AppDomains
                    foreach (AppDomainStructure ads in appDomains.Where(ads => ads.CurrentAppDomain == ad))
                    {
                        // Found it
                        ads.ScriptsWaitingUnload++;
                        break;
                    }
                }
            }

            UnloadAppDomains(); // Outsite lock, has its own GetLock
        }

        #region Nested type: AppDomainStructure

        class AppDomainStructure
        {
            // The AppDomain itself
            public AppDomain CurrentAppDomain;

            // Number of scripts loaded into AppDomain
            public int ScriptsLoaded;

            // Number of dead scripts
            public int ScriptsWaitingUnload;
        }

        #endregion
    }
}
