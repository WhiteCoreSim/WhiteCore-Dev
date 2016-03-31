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


using System.IO;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Implementation;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services
{
    public class ProductInfoRequest : ICapsServiceConnector
    {
        IRegionClientCapsService m_service;

        #region ICapsServiceConnector Members

        public void RegisterCaps (IRegionClientCapsService service)
        {
            m_service = service;
            m_service.AddStreamHandler ("ProductInfoRequest",
                new GenericStreamHandler ("GET", m_service.CreateCAPS ("ProductInfoRequest", ""), ProductInfoRequestCAP));
        }

        public void DeregisterCaps ()
        {
            m_service.RemoveStreamHandler ("ProductInfoRequest", "GET");
        }

        public void EnteringRegion ()
        {
        }

        #endregion

        byte[] ProductInfoRequestCAP (string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            //OSDMap data = m_service.GetCAPS ();
            var data = new OSDArray();

            var mf = new OSDMap ();
            mf ["name"] = "mainland_full";
            mf ["description"] = "Mainland / Full Region";
            mf ["sku"] = "001";
            data.Add(mf);
            
            var mh = new OSDMap ();
            mh ["name"] = "mainland_homestead";
            mh ["description"] = "Mainland / Homestead";
            mh ["sku"] = "011";
            data.Add(mh);

            var mo = new OSDMap ();
            mo ["name"] = "mainland_openspace";
            mo ["description"] = "Mainland / Openspace";
            mo ["sku"] = "021";
            data.Add(mo);

            var ef = new OSDMap ();
            ef ["name"] = "estate_full";
            ef ["description"] = "Estate / Full Region";
            ef ["sku"] = "002";
            data.Add(ef);

            var eh = new OSDMap ();
            eh ["name"] = "estate_homestead";
            eh ["description"] = "Estate / Homestead";
            eh ["sku"] = "012";
            data.Add(eh);

            var eo = new OSDMap ();
            eo ["name"] = "estate_openspace";
            eo ["description"] = "Estate / Openspace";
            eo ["sku"] = "022";
            data.Add(eo);

            var wh = new OSDMap ();
            wh ["name"] = "whitecore_homes";
            wh ["description"] = "WhiteCore Homes / Full Region";
            wh ["sku"] = "101";
            data.Add(wh);

            return OSDParser.SerializeLLSDXmlBytes (data);
        }
    }
}