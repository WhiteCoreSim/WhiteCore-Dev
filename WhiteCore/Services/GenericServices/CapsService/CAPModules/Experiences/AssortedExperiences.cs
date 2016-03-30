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
using System.IO;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services
{
    public class AssortedExperiencesCAPS : ICapsServiceConnector
    {
        protected IRegionClientCapsService m_service;

        public void RegisterCaps (IRegionClientCapsService service)
        {
            m_service = service;
            
            service.AddStreamHandler ("ExperiencePreferences",
                                      new GenericStreamHandler ("POST", service.CreateCAPS ("ExperiencePreferences", ""), ExperiencePreferences));
            
            service.AddStreamHandler ("FindExperienceByName",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("FindExperienceByName", ""), FindExperienceByName));
            
            service.AddStreamHandler ("GetExperiences",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("GetExperiences", ""), GetExperiences));

            service.AddStreamHandler ("GetExperienceInfo",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("GetExperienceInfo", ""), GetExperienceInfo));
            
            service.AddStreamHandler ("GetAdminExperiences",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("GetAdminExperiences", ""), GetAdminExperiences));
            
            service.AddStreamHandler ("GetCreatorExperiences",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("GetCreatorExperiences", ""), GetCreatorExperiences));
            
            service.AddStreamHandler ("UpdateExperience",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("UpdateExperience", ""), UpdateExperience));
            
            service.AddStreamHandler ("IsExperienceAdmin",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("IsExperienceAdmin", ""), IsExperienceAdmin));

            service.AddStreamHandler ("IsExperienceContributor",
                                      new GenericStreamHandler ("POST", service.CreateCAPS("IsExperienceContributor", ""), IsExperienceContributor));
        }

        public void EnteringRegion ()
        {
        }

        public void DeregisterCaps ()
        {
            m_service.RemoveStreamHandler ("ExperiencePreferences", "POST");
            m_service.RemoveStreamHandler ("FindExperienceByName", "POST");
            m_service.RemoveStreamHandler ("GetExperiences", "POST");
        }
        
        public byte[] ExperiencePreferences (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[ExperiencePreferences] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] FindExperienceByName (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[ExperiencePreferences] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] GetExperiences (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[ExperiencePreferences] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] GetExperienceInfo (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[GetExperienceInfo] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] GetAdminExperiences (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[GetAdminExperiences] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] GetCreatorExperiences (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[GetCreatorExperiences] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] UpdateExperience (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[UpdateExperience] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] IsExperienceAdmin (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[IsExperienceAdmin] Call = {0}", httpRequest);
        	return null;
        }
        
        public byte[] IsExperienceContributor (string path, Stream request, OSHttpRequest httpRequest,
                                      OSHttpResponse httpResponse)
        {
        	MainConsole.Instance.DebugFormat("[IsExperienceContributor] Call = {0}", httpRequest);
        	return null;
        }
    }
}
