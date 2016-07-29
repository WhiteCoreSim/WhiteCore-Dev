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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using ProtoBuf;

namespace WhiteCore.Framework.Modules
{
    public interface IAuctionModule
    {
        void StartAuction(int localID, UUID snapshotID);
        void SetAuctionInfo(int localID, AuctionInfo info);
        void AddAuctionBid(int localID, UUID userID, int bid);
        void AuctionEnd(int localID);
    }

    [Serializable, ProtoContract(UseProtoMembersOnly = false)]
    public class AuctionInfo
    {
        /// <summary>
        ///     Auction length (in days)
        /// </summary>
        [ProtoMember(1)] public int AuctionLength = 7;

        /// <summary>
        ///     Date the auction started
        /// </summary>
        [ProtoMember(2)] public DateTime AuctionStart = DateTime.Now;

        /// <summary>
        ///     Description of the parcel
        /// </summary>
        [ProtoMember(3)] public string Description = "";

        /// <summary>
        ///     List of bids on the auction so far
        /// </summary>
        [ProtoMember(4)] public List<AuctionBid> AuctionBids = new List<AuctionBid>();

        public void FromOSD(OSDMap map)
        {
            AuctionStart = map["AuctionStart"];
            Description = map["Description"];
            AuctionLength = map["AuctionLength"];
            foreach (OSD o in (OSDArray) map["AuctionBids"])
            {
                AuctionBid bid = new AuctionBid();
                bid.FromOSD((OSDMap) o);
                AuctionBids.Add(bid);
            }
        }

        public OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();
            map["AuctionStart"] = AuctionStart;
            map["AuctionLength"] = AuctionLength;
            map["Description"] = Description;
            OSDArray array = new OSDArray();
            foreach (AuctionBid bid in AuctionBids)
                array.Add(bid.ToOSD());
            map["AuctionBids"] = array;
            return map;
        }
    }

    [Serializable, ProtoContract(UseProtoMembersOnly = false)]
    public class AuctionBid
    {
        /// <summary>
        ///     The person who bid on the auction
        /// </summary>
        [ProtoMember(1)] public UUID AuctionBidder;

        /// <summary>
        ///     The amount bid on the auction
        /// </summary>
        [ProtoMember(2)] public int Amount;

        /// <summary>
        ///     The time the bid was added
        /// </summary>
        [ProtoMember(3)] public DateTime TimeBid;

        public OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();
            map["TimeBid"] = TimeBid;
            map["Amount"] = Amount;
            map["AuctionBidder"] = AuctionBidder;
            return map;
        }

        public void FromOSD(OSDMap map)
        {
            TimeBid = map["TimeBid"];
            Amount = map["Amount"];
            AuctionBidder = map["AuctionBidder"];
        }
    }
}