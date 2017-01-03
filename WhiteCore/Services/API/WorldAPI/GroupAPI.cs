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
using DataPlugins = WhiteCore.Framework.Utilities.DataManager;

namespace WhiteCore.Services.API
{
    public partial class APIHandler : BaseRequestHandler, IStreamedRequestHandler
    {
        UUID AdminAgentID = UUID.Zero;

        #region Groups

        static OSDMap GroupRecord2OSDMap (GroupRecord group)
        {
            var resp = new OSDMap ();

            resp ["GroupID"] = group.GroupID;
            resp ["GroupName"] = group.GroupName;
            resp ["AllowPublish"] = group.AllowPublish;
            resp ["MaturePublish"] = group.MaturePublish;
            resp ["Charter"] = group.Charter;
            resp ["FounderID"] = group.FounderID;
            resp ["GroupPicture"] = group.GroupPicture;
            resp ["MembershipFee"] = group.MembershipFee;
            resp ["OpenEnrollment"] = group.OpenEnrollment;
            resp ["OwnerRoleID"] = group.OwnerRoleID;
            resp ["ShowInList"] = group.ShowInList;

            return resp;
        }

        OSDMap GetGroups (OSDMap map)
        {
            var resp = new OSDMap ();
            var start = map.ContainsKey ("Start") ? map ["Start"].AsUInteger () : 0;
            resp ["Start"] = start;
            resp ["Total"] = 0;

            var groups = DataPlugins.RequestPlugin<IGroupsServiceConnector> ();
            var Groups = new OSDArray ();

            if (groups != null) {
                var sort = new Dictionary<string, bool> ();
                var boolFields = new Dictionary<string, bool> ();

                if (map.ContainsKey ("Sort") && map ["Sort"].Type == OSDType.Map) {
                    var fields = (OSDMap)map ["Sort"];
                    foreach (string field in fields.Keys) {
                        sort [field] = int.Parse (fields [field]) != 0;
                    }
                }
                if (map.ContainsKey ("BoolFields") && map ["BoolFields"].Type == OSDType.Map) {
                    var fields = (OSDMap)map ["BoolFields"];
                    foreach (string field in fields.Keys) {
                        boolFields [field] = int.Parse (fields [field]) != 0;
                    }
                }
                var reply = groups.GetGroupRecords (
                    AdminAgentID,
                    start,
                    map.ContainsKey ("Count") ? map ["Count"].AsUInteger () : 10,
                    sort,
                    boolFields
                );
                if (reply.Count > 0) {
                    foreach (GroupRecord groupReply in reply) {
                        Groups.Add (GroupRecord2OSDMap (groupReply));
                    }
                }
                resp ["Total"] = groups.GetNumberOfGroups (AdminAgentID, boolFields);
            }

            resp ["Groups"] = Groups;
            return resp;
        }

        OSDMap GetGroup (OSDMap map)
        {
            var resp = new OSDMap ();
            var groups = DataPlugins.RequestPlugin<IGroupsServiceConnector> ();
            resp ["Group"] = false;

            if (groups != null && (map.ContainsKey ("Name") || map.ContainsKey ("UUID"))) {
                UUID groupID = map.ContainsKey ("UUID") ? UUID.Parse (map ["UUID"].ToString ()) : UUID.Zero;
                string name = map.ContainsKey ("Name") ? map ["Name"].ToString () : "";
                GroupRecord reply = groups.GetGroupRecord (AdminAgentID, groupID, name);
                if (reply != null) {
                    resp ["Group"] = GroupRecord2OSDMap (reply);
                }
            }
            return resp;
        }


        OSDMap GroupAsNewsSource (OSDMap map)
        {
            var resp = new OSDMap ();
            resp ["Verified"] = OSD.FromBoolean (false);
            var generics = DataPlugins.RequestPlugin<IGenericsConnector> ();
            UUID groupID;
            if (generics != null && map.ContainsKey ("Group") == true && map.ContainsKey ("Use") && UUID.TryParse (map ["Group"], out groupID) == true) {
                if (map ["Use"].AsBoolean ()) {
                    var useValue = new OSDMap ();
                    useValue ["Use"] = OSD.FromBoolean (true);
                    generics.AddGeneric (groupID, "Group", "WebUI_newsSource", useValue);
                } else {
                    generics.RemoveGeneric (groupID, "Group", "WebUI_newsSource");
                }
                resp ["Verified"] = OSD.FromBoolean (true);
            }
            return resp;
        }

        OSDMap GroupNotices (OSDMap map)
        {
            var resp = new OSDMap ();
            resp ["GroupNotices"] = new OSDArray ();
            resp ["Total"] = 0;
            var groups = DataPlugins.RequestPlugin<IGroupsServiceConnector> ();

            if (map.ContainsKey ("Groups") && groups != null && map ["Groups"].Type.ToString () == "Array") {
                var groupIDs = (OSDArray)map ["Groups"];
                var GroupIDs = new List<UUID> ();
                foreach (string groupID in groupIDs) {
                    UUID foo;
                    if (UUID.TryParse (groupID, out foo)) {
                        GroupIDs.Add (foo);
                    }
                }
                if (GroupIDs.Count > 0) {
                    var start = map.ContainsKey ("Start") ? uint.Parse (map ["Start"]) : 0;
                    var count = map.ContainsKey ("Count") ? uint.Parse (map ["Count"]) : 10;
                    var groupNoticeList = groups.GetGroupNotices (AdminAgentID, start, count, GroupIDs);
                    var groupNotices = new OSDArray (groupNoticeList.Count);

                    foreach (GroupNoticeData GND in groupNoticeList) {
                        var gnd = new OSDMap ();

                        gnd ["GroupID"] = OSD.FromUUID (GND.GroupID);
                        gnd ["NoticeID"] = OSD.FromUUID (GND.NoticeID);
                        gnd ["Timestamp"] = OSD.FromInteger ((int)GND.Timestamp);
                        gnd ["FromName"] = OSD.FromString (GND.FromName);
                        gnd ["Subject"] = OSD.FromString (GND.Subject);
                        gnd ["HasAttachment"] = OSD.FromBoolean (GND.HasAttachment);
                        gnd ["ItemID"] = OSD.FromUUID (GND.ItemID);
                        gnd ["AssetType"] = OSD.FromInteger ((int)GND.AssetType);
                        gnd ["ItemName"] = OSD.FromString (GND.ItemName);

                        var notice = groups.GetGroupNotice (AdminAgentID, GND.NoticeID);
                        gnd ["Message"] = OSD.FromString (groups.GetGroupNotice (AdminAgentID, GND.NoticeID).Message);
                        groupNotices.Add (gnd);
                    }
                    resp ["GroupNotices"] = groupNotices;
                    resp ["Total"] = (int)groups.GetNumberOfGroupNotices (AdminAgentID, GroupIDs);
                }
            }

            return resp;
        }

        OSDMap NewsFromGroupNotices (OSDMap map)
        {
            var resp = new OSDMap ();
            resp ["GroupNotices"] = new OSDArray ();
            resp ["Total"] = 0;
            var generics = DataPlugins.RequestPlugin<IGenericsConnector> ();
            var groups = DataPlugins.RequestPlugin<IGroupsServiceConnector> ();
            if (generics == null || groups == null) {
                return resp;
            }

            var useValue = new OSDMap ();
            useValue ["Use"] = OSD.FromBoolean (true);
            var GroupIDs = generics.GetOwnersByGeneric ("Group", "WebUI_newsSource", useValue);
            if (GroupIDs.Count <= 0) {
                return resp;
            }
            foreach (UUID groupID in GroupIDs) {
                var group = groups.GetGroupRecord (AdminAgentID, groupID, "");
                if (!group.ShowInList) {
                    GroupIDs.Remove (groupID);
                }
            }

            var start = map.ContainsKey ("Start") ? uint.Parse (map ["Start"].ToString ()) : 0;
            var count = map.ContainsKey ("Count") ? uint.Parse (map ["Count"].ToString ()) : 10;

            var args = new OSDMap ();
            args ["Start"] = OSD.FromString (start.ToString ());
            args ["Count"] = OSD.FromString (count.ToString ());
            args ["Groups"] = new OSDArray (GroupIDs.ConvertAll (x => OSD.FromString (x.ToString ())));

            return GroupNotices (args);
        }

        #endregion
	}
}
