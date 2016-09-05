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

using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using OpenMetaverse;
using System.Collections.Generic;

namespace WhiteCore.Framework.DatabaseInterfaces
{
    public interface IEstateConnector : IWhiteCoreDataPlugin
    {
        /// <summary>
        /// Are we doing local or remote calls.
        /// </summary>
        bool RemoteCalls();

        /// <summary>
        ///     Loads the estate data for the given region
        /// </summary>
        /// <param name="regionID"></param>
        /// <returns></returns>
        EstateSettings GetEstateSettings(UUID regionID);

        /// <summary>
        ///     Loads the estate data for the specified estateID
        /// </summary>
        /// <param name="estateID"></param>
        /// <returns></returns>
        EstateSettings GetEstateSettings(int estateID);

        /// <summary>
        ///     Loads the estate data for the specified estate name (local only)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        EstateSettings GetEstateSettings(string name);

        /// <summary>
        /// Creates new estate.
        /// </summary>
        /// <returns>The the estateID</returns>
        /// <param name="es">Es.</param>
        int CreateNewEstate (EstateSettings es);

        /// <summary>
        ///     Creates a new estate from the given info, and link to a region
        /// </summary>
        /// <param name="ES"></param>
        /// <param name="regionID"></param>
        /// <returns>0 if the estate could not be created, otherwise, the estateID</returns>
        int CreateNewEstate(EstateSettings ES, UUID regionID);

        /// <summary>
        ///     Updates the given Estate data in the database
        /// </summary>
        /// <param name="es"></param>
        void SaveEstateSettings(EstateSettings es);

        /// <summary>
        ///     Gets the estateID for the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The EstateID</returns>
        int GetEstateID(string name);

        /// <summary>
        ///     Gets the estates that have the given name and owner
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        int GetEstate(UUID ownerID, string name);

        /// <summary>
        ///     Get all regions in the current estate
        /// </summary>
        /// <param name="estateID"></param>
        /// <returns></returns>
        List<UUID> GetRegions(int estateID);

        /// <summary>
        ///     Gets all available estates
        /// </summary>
        /// <returns>List of estates</returns>
        List<string> GetEstateNames();

        /// <summary>
        ///     Gets all user estates
        /// </summary>
        /// <param name="ownerID"></param>
        /// <returns>List of estates</returns>
        List<string> GetEstateNames (UUID ownerId);

        /// <summary>
        ///     Gets the estates that have the given owner
        /// </summary>
        /// <param name="ownerID"></param>
        /// <returns></returns>
        List<EstateSettings> GetEstates(UUID ownerID);

        /// <summary>
        ///     Gets the estates that have the specified owner, with optional filters.
        /// </summary>
        /// <param name="OwnerID"></param>
        /// <param name="boolFields"></param>
        /// <returns></returns>
        List<EstateSettings> GetEstates(UUID OwnerID, Dictionary<string, bool> boolFields);

        /// <summary>
        ///     Gets the EstateID for the specified RegionID
        /// </summary>
        /// <param name="regionID"></param>
        /// <returns></returns>
        int GetEstateID(UUID regionID);

        /// <summary>
        ///     Add a new region to the estate, authenticates with the password
        /// </summary>
        /// <param name="regionID"></param>
        /// <param name="estateID"></param>
        /// <returns></returns>
        bool LinkRegion(UUID regionID, int estateID);

        /// <summary>
        ///     Remove an existing region from the estate
        /// </summary>
        /// <param name="regionID"></param>
        /// <returns></returns>
        bool DelinkRegion(UUID regionID);

        /// <summary>
        ///     Deletes the given estate by its estate ID
        /// </summary>
        /// <param name="estateID"></param>
        /// <returns></returns>
        bool DeleteEstate(int estateID);

        /// <summary>
        ///  Check to see if Estate exists.
        /// </summary>
        /// <returns><c>true</c>, if exists, <c>false</c> otherwise.</returns>
        /// <param name="name">Name.</param>
        bool EstateExists (string name);

        /// <summary>
        /// Cheks to see if region is part of an estate.
        /// </summary>
        /// <returns><c>true</c>, if region is part of estate, <c>false</c> otherwise.</returns>
        /// <param name="estateID">Estate ID</param>
        /// <param name="regionID">Region ID</param>
        bool EstateRegionExists(int estateID, UUID regionID);

    }
}