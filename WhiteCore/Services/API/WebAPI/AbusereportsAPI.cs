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

using System.Collections.Generic;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        #region IAbuseReports

        OSDMap GetAbuseReports (OSDMap map)
        {
            var resp = new OSDMap ();
            var areports = m_registry.RequestModuleInterface<IAbuseReports> ();

            int start = map ["Start"].AsInteger ();
            int count = map ["Count"].AsInteger ();
            bool active = map ["Active"].AsBoolean ();

            List<AbuseReport> arList = areports.GetAbuseReports (start, count, active);
            var AbuseReports = new OSDArray ();

            if (arList != null) {
                foreach (AbuseReport rpt in arList) {
                    AbuseReports.Add (rpt.ToOSD ());
                }
            }

            resp ["AbuseReports"] = AbuseReports;
            resp ["Start"] = OSD.FromInteger (start);
            resp ["Count"] = OSD.FromInteger (count); 
            resp ["Active"] = OSD.FromBoolean (active);

            return resp;
        }

        OSDMap AbuseReportMarkComplete (OSDMap map)
        {
            var resp = new OSDMap ();
            var areports = m_registry.RequestModuleInterface<IAbuseReports> ();
            var rpt = areports.GetAbuseReport (map ["Number"].AsInteger (), map ["WebPassword"].AsString ());
            if (rpt != null) {
                rpt.Active = false;
                areports.UpdateAbuseReport (rpt, map ["WebPassword"].AsString ());
                resp ["Finished"] = OSD.FromBoolean (true);
            } else {
                resp ["Finished"] = OSD.FromBoolean (false);
                resp ["Failed"] = OSD.FromString (string.Format ("No abuse report found with specified number {0}", map ["Number"].AsInteger ()));
            }

            return resp;
        }

        OSDMap AbuseReportSaveNotes (OSDMap map)
        {
            var resp = new OSDMap ();
            var areports = m_registry.RequestModuleInterface<IAbuseReports> ();
            var rpt = areports.GetAbuseReport (map ["Number"].AsInteger (), map ["WebPassword"].AsString ());
            if (rpt != null) {
                rpt.Notes = map ["Notes"].ToString ();
                areports.UpdateAbuseReport (rpt, map ["WebPassword"].AsString ());
                resp ["Finished"] = OSD.FromBoolean (true);
            } else {
                resp ["Finished"] = OSD.FromBoolean (false);
                resp ["Failed"] = OSD.FromString (string.Format ("No abuse report found with specified number {0}", map ["Number"].AsInteger ()));
            }

            return resp;
        }

        #endregion
    }
}
