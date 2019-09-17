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
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Services;

namespace WhiteCore.Services.DataService
{
    /// <summary>
    ///     Some background to this class
    ///     This class saves any class that implements the IDataTransferable interface.
    ///     When implementing the IDataTransferable interface, it is heavily recommending to implement ToOSD and FromOSD first, then use the Utility methods to convert OSDMaps into Dictionarys, as shown in the LandData class.
    ///     This method of saving uses 4 columns in the database, OwnerID, Type, Key, and Value
    ///     - OwnerID : This is a way to be able to save Agent or Region or anything with a UUID into the database and have it be set to that UUID only.
    ///     - Type : What made this data? This just tells what module created the given row in the database.
    ///     - Key : Another identifying setting so that you can store more than one row under an OwnerID and Type
    ///     - Value : The value of the row
    ///     This class deals with the Getting/Setting/Removing of these generic interfaces.
    /// </summary>
    public class LocalGenericsConnector : IGenericsConnector
    {
        IGenericData genData;

        #region IGenericsConnector Members

        public void Initialize(IGenericData genericData, IConfigSource source, IRegistryCore simBase,
                               string defaultConnectionString)
        {
            if (source.Configs["WhiteCoreConnectors"].GetString("GenericsConnector", "LocalConnector") == "LocalConnector")
            {
                genData = genericData;

                if (source.Configs[Name] != null)
                    defaultConnectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);

                if (genData != null)
                {
                    genData.ConnectToDatabase (defaultConnectionString, "Generics",
                        source.Configs ["WhiteCoreConnectors"].GetBoolean ("ValidateTables", true));

                    Framework.Utilities.DataManager.RegisterPlugin (this);
                }
            }
        }

        public string Name
        {
            get { return "IGenericsConnector"; }
        }

        /// <summary>
        ///     Gets a Generic type as set by the ownerID, Type, and Key
        /// </summary>
        /// <typeparam name="T">return value of type IDataTransferable</typeparam>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetGeneric<T>(UUID ownerID, string type, string key) where T : IDataTransferable
        {
            return GenericUtils.GetGeneric<T>(ownerID, type, key, genData);
        }

        /// <summary>
        ///     Gets a list of generic T's from the database
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public List<T> GetGenerics<T>(UUID ownerID, string type) where T : IDataTransferable
        {
            return GenericUtils.GetGenerics<T>(ownerID, type, genData);
        }

        /// <summary>
        ///     Gets the number of list of generic T's from the database
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public int GetGenericCount(UUID ownerID, string type)
        {
            return GenericUtils.GetGenericCount(ownerID, type, genData);
        }

        /// <summary>
        ///     Adds a generic IDataTransferable into the database
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void AddGeneric(UUID agentID, string type, string key, OSDMap value)
        {
            GenericUtils.AddGeneric(agentID, type, key, value, genData);
        }

        /// <summary>
        ///     Removes a generic IDataTransferable from the database
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="type"></param>
        /// <param name="key"></param>
        public void RemoveGeneric(UUID agentID, string type, string key)
        {
            GenericUtils.RemoveGenericByKeyAndType(agentID, type, key, genData);
        }

        /// <summary>
        ///     Removes a generic IDataTransferable from the database
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="type"></param>
        public void RemoveGeneric(UUID agentID, string type)
        {
            GenericUtils.RemoveGenericByType(agentID, type, genData);
        }

        public List<UUID> GetOwnersByGeneric(string type, string key)
        {
            return GenericUtils.GetOwnersByGeneric(type, key, genData);
        }

        public List<UUID> GetOwnersByGeneric(string type, string key, OSDMap value)
        {
            return GenericUtils.GetOwnersByGeneric(type, key, value, genData);
        }

        #endregion

        public void Dispose()
        {
        }
    }
}