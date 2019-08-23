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


using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services.DataService
{
    /// <summary>
    ///     Some background to this class
    ///     This class saves any class that implements the IDataTransferable interface.
    ///     When implementing the IDataTransferable interface, it is heavily recommending to implement ToOSD and FromOSD first, then use the Utility methods to convert OSDMaps into Dictionaries, as shown in the LandData class.
    ///     This method of saving uses 4 columns in the database, OwnerID, Type, Key, and Value
    ///     - OwnerID : This is a way to be able to save Agent or Region or anything with a UUID into the database and have it be set to that UUID only.
    ///     - Type : What made this data? This just tells what module created the given row in the database.
    ///     - Key : Another identifying setting so that you can store more than one row under an OwnerID and Type
    ///     - Value : The value of the row
    ///     This class deals with the Getting/Setting/Removing of these generic interfaces.
    /// </summary>
    public class GenericUtils
    {
        /// <summary>
        ///     Gets a list of generic T's from the database
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <param name="genData"></param>
        /// <returns></returns>
        public static List<T> GetGenerics<T> (UUID ownerID, string type, IGenericData genData) where T : IDataTransferable
        {
            List<OSDMap> retVal = GetGenerics (ownerID, type, genData);

            List<T> values = new List<T> ();
            foreach (OSDMap map in retVal) {
                T data = (T)System.Activator.CreateInstance (typeof (T));
                data.FromOSD (map);
                values.Add (data);
            }

            return values;
        }

        /// <summary>
        ///     Gets a list of OSDMaps from the database
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <param name="genData"></param>
        /// <returns></returns>
        public static List<OSDMap> GetGenerics (UUID ownerID, string type, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            filter.andFilters ["Type"] = type;
            List<string> retVal = genData.Query (new string [1] { "`value`" }, "generics", filter, null, null, null);

            List<OSDMap> values = new List<OSDMap> ();
            foreach (string ret in retVal) {
                OSDMap map = (OSDMap)OSDParser.DeserializeJson (ret);
                if (map != null)
                    values.Add (map);
            }

            return values;
        }

        /// <summary>
        ///     Gets a Generic type as set by T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="genData"></param>
        /// <returns></returns>
        public static T GetGeneric<T> (UUID ownerID, string type, string key, IGenericData genData)
            where T : IDataTransferable
        {
            OSDMap map = GetGeneric (ownerID, type, key, genData);
            if (map == null)
                return null;

            T data = (T)System.Activator.CreateInstance (typeof (T));
            data.FromOSD (map);

            return data;
        }

        /// <summary>
        ///     Gets a Generic type as set by T
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="genData"></param>
        /// <returns></returns>
        public static OSDMap GetGeneric (UUID ownerID, string type, string key, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            filter.andFilters ["Type"] = type;
            filter.andFilters ["`Key`"] = key;
            List<string> retVal = genData.Query (new string [1] { "`value`" }, "generics", filter, null, null, null);

            if (retVal.Count == 0)
                return null;

            return (OSDMap)OSDParser.DeserializeJson (retVal [0]);
        }

        /// <summary>
        ///     Gets the number of generic entries
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <param name="genData"></param>
        /// <returns></returns>
        public static int GetGenericCount (UUID ownerID, string type, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            filter.andFilters ["Type"] = type;
            List<string> retVal = genData.Query (new string [1] { "COUNT(*)" }, "generics", filter, null, null, null);

            return (retVal == null || retVal.Count == 0) ? 0 : int.Parse (retVal [0]);
        }

        /// <summary>
        ///     Gets the number of generic entries
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="genData"></param>
        /// <returns></returns>
        public static int GetGenericCount (UUID ownerID, string type, string key, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            filter.andFilters ["Type"] = type;
            filter.andFilters ["`Key`"] = key;
            List<string> retVal = genData.Query (new string [1] { "COUNT(*)" }, "generics", filter, null, null, null);

            return (retVal == null || retVal.Count == 0) ? 0 : int.Parse (retVal [0]);
        }

        /// <summary>
        ///     Gets the number of generic entries
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="genData"></param>
        /// <returns></returns>
        public static int GetGenericCount (UUID ownerID, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            List<string> retVal = genData.Query (new string [1] { "COUNT(*)" }, "generics", filter, null, null, null);

            return (retVal == null || retVal.Count == 0) ? 0 : int.Parse (retVal [0]);
        }

        /// <summary>
        ///     Adds a generic into the database
        /// </summary>
        /// <param name="ownerID">ID of the entity that owns the generic data</param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="genData"></param>
        public static void AddGeneric (UUID ownerID, string type, string key, OSDMap value, IGenericData genData)
        {
            Dictionary<string, object> row = new Dictionary<string, object> (4);
            row ["OwnerID"] = ownerID;
            row ["Type"] = type;
            row ["`Key`"] = key;
            row ["`Value`"] = OSDParser.SerializeJsonString (value);

            genData.Replace ("generics", row);
        }

        /// <summary>
        ///     Remove generic data for the specified owner by type and key
        /// </summary>
        /// <param name="ownerID">ID of the entity that owns the generic data</param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="genData"></param>
        public static void RemoveGenericByKeyAndType (UUID ownerID, string type, string key, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            filter.andFilters ["Type"] = type;
            filter.andFilters ["`Key`"] = key;

            genData.Delete ("generics", filter);
        }

        /// <summary>
        ///     Removes a generic from the database
        /// </summary>
        /// <param name="ownerID">ID of the entity that owns the generic data</param>
        /// <param name="key"></param>
        /// <param name="genData"></param>
        public static void RemoveGenericByKey (UUID ownerID, string key, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            filter.andFilters ["`Key`"] = key;

            genData.Delete ("generics", filter);
        }

        /// <summary>
        ///     Removes a generic from the database
        /// </summary>
        /// <param name="ownerID">ID of the entity that owns the generic data</param>
        /// <param name="type"></param>
        /// <param name="genData"></param>
        public static void RemoveGenericByType (UUID ownerID, string type, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["OwnerID"] = ownerID;
            filter.andFilters ["Type"] = type;

            genData.Delete ("generics", filter);
        }

        public static List<UUID> GetOwnersByGeneric (string type, string key, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["Type"] = type;
            filter.andFilters ["`Key`"] = key;

            return
                genData.Query (new string [1] { "OwnerID" }, "generics", filter, null, null, null)
                  .ConvertAll<UUID> (x => new UUID (x));
        }

        public static List<UUID> GetOwnersByGeneric (string type, string key, OSDMap value, IGenericData genData)
        {
            QueryFilter filter = new QueryFilter ();
            filter.andFilters ["Type"] = type;
            filter.andFilters ["`Key`"] = key;
            filter.andFilters ["`Value`"] = OSDParser.SerializeJsonString (value);

            return
                genData.Query (new string [1] { "OwnerID" }, "generics", filter, null, null, null)
                  .ConvertAll<UUID> (x => new UUID (x));
        }
    }
}