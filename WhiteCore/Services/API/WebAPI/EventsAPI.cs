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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Utilities;
using DataPlugins = WhiteCore.Framework.Utilities.DataManager;

namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        
		OSDMap GetEvents(OSDMap map)
		{
            var resp = new OSDMap ();
            var directory = DataPlugins.RequestPlugin<IDirectoryServiceConnector> ();

			if (directory != null) {

				var events = new List<EventData> ();
				var timeframe = map.Keys.Contains ("timeframe")
                                   ? map ["timeframe"].AsInteger ()
                                   : 24;
				var category = map.Keys.Contains ("category")
                                  ? map ["category"].AsInteger ()
                                  :(int)DirectoryManager.EventCategories.All;
				int eventMaturity = map.Keys.Contains ("maturity")
                                       ? map ["maturity"].AsInteger ()
                                       : Util.ConvertEventMaturityToDBMaturity (DirectoryManager.EventFlags.PG);
                
				events = directory.GetAllEvents(timeframe, category , eventMaturity);

				if (events.Count > 0) {

					// build a list of classifieds
					var evarry = new OSDArray ();
					foreach (var evnt in events)
						evarry.Add (evnt.ToOSD ());

					resp ["events"] = evarry;
					resp ["count"] = evarry.Count.ToString ();

					return resp;
				}
			}

			// no eventss
			resp ["events"] = new OSDArray ();
			resp ["count"] = "0";

			return resp;
		}
	}
}
