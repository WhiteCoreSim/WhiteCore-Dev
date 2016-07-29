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

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using WhiteCore.Framework.Modules;

namespace WhiteCore.Framework.ClientInterfaces
{
    public class AvatarArchive : IDataTransferable
    {
        /// <summary>
        ///     Appearance in the archive
        /// </summary>
        public AvatarAppearance Appearance;

        /// <summary>
        ///     true/false if its public
        /// </summary>
        public bool IsPublic;

        /// <summary>
        ///     true/false if it is a portabl;e archive
        /// </summary>
        public bool IsPortable;

        /// <summary>
        ///     Name of the archive
        /// </summary>
        public string FileName;

        /// <summary>
        ///     Folder to load this archive into
        /// </summary>
        public string FolderName;

        /// <summary>
        ///     uuid of a text that shows off this archive
        /// </summary>
        public UUID Snapshot;

        /// <summary>
        ///     filename of the local snapshot that shows off this archive (for portable use)
        /// </summary>
        public string LocalSnapshot;

        public OSDMap AssetsMap;

        public OSDMap ItemsMap;

        public OSDMap BodyMap;

        public override void FromOSD(OSDMap map)
        {
            AssetsMap = ((OSDMap)map["Assets"]);
            ItemsMap = ((OSDMap)map["Items"]);
            BodyMap = ((OSDMap)map["Body"]);
            FolderName = map["FolderName"];
            Snapshot = map["Snapshot"];
            LocalSnapshot = map ["LocalSnapshot"];
            IsPublic = map["Public"];
            IsPortable = map["Portable"];
        }

        public override OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();

            map["Assets"] = AssetsMap;
            map["Items"] = ItemsMap;
            map["Body"] = BodyMap;
            map["FolderName"] = FolderName;
            map["Snapshot"] = Snapshot;
            map["LocalSnapshot"] = LocalSnapshot;
            map["Public"] = IsPublic;
            map["Portable"] = IsPortable;

            return map;
        }
    }
}
