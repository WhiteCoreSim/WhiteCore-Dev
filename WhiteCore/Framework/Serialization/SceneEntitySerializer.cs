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
using System.Xml;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using OpenMetaverse;

namespace WhiteCore.Framework.Serialization
{
    public interface ISceneObjectSerializer
    {
        ISceneEntity FromOriginalXmlFormat(string serialization, IRegistryCore scene);

        ISceneEntity FromOriginalXmlFormat(UUID fromUserInventoryItemID, string xmlData,
                                           IRegistryCore scene);

        string ToOriginalXmlFormat(ISceneEntity sceneObject);
        ISceneEntity FromXml2Format(string xmlData, IScene scene);
        ISceneEntity FromXml2Format(ref MemoryStream ms, IScene scene);
        string ToXml2Format(ISceneEntity sceneObject);
        byte[] ToBinaryXml2Format(ISceneEntity sceneObject);
        void ToXmlFormat(ISceneChildEntity part, XmlTextWriter writer);
        void AddSerializer(string p, ISOPSerializerModule serializer);
    }

    public class SceneEntitySerializer
    {
        public static ISceneObjectSerializer SceneObjectSerializer;
    }
}