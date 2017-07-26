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
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Servers.HttpServer;
using WhiteCore.Framework.Servers.HttpServer.Interfaces;
using WhiteCore.Framework.Services.ClassHelpers.Profile;
using DataPlugins = WhiteCore.Framework.Utilities.DataManager;

namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {

		OSDMap GetClassifieds (OSDMap map)
		{
            var resp = new OSDMap ();

            var directory = DataPlugins.RequestPlugin<IDirectoryServiceConnector> ();

			if (directory != null) {

				var classifieds = new List<Classified> ();

                int category = map.Keys.Contains ("category")
                                  ? map ["category"].AsInteger ()
                                  : (int)DirectoryManager.ClassifiedCategories.Any;
				int maturity = map.Keys.Contains  ("maturity")
                                  ? map ["maturity"].AsInteger ()
                                  : (int)DirectoryManager.ClassifiedFlags.None;

                classifieds = directory.GetAllClassifieds (category, (uint)maturity);

                if (classifieds.Count > 0) {

                    // build a list of classifieds
                    var clarry = new OSDArray ();
                    foreach (var classified in classifieds)
                        clarry.Add (classified.ToOSD ());

                    resp ["classifieds"] = clarry;
                    resp ["count"] = clarry.Count.ToString ();

                    return resp;
                }
			}

			// no classifieds
			resp ["classifieds"] = new OSDArray ();
			resp ["count"] = "0";

			return resp;

		}
	}
}
