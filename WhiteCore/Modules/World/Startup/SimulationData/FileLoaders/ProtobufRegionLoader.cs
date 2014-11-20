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

using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Region;
using System;
using System.Collections.Generic;
using System.IO;

namespace WhiteCore.Modules
{
    public class ProtobufRegionDataLoader : IRegionDataLoader
    {
        public string FileType
        {
            get { return ".sim"; }
        }

        public RegionData LoadBackup(string file)
        {
            if (!File.Exists(file))
                return null;
            try
            {
                FileStream stream = File.OpenRead(file);
                RegionData regiondata = ProtoBuf.Serializer.Deserialize<RegionData>(stream);
                stream.Close();

                List<SceneObjectGroup> grps = new List<SceneObjectGroup>();

                if (regiondata.Groups != null)
                {
                    foreach (SceneObjectGroup grp in regiondata.Groups)
                    {
                        SceneObjectGroup sceneObject = new SceneObjectGroup(grp.ChildrenList[0], null);
                        foreach (SceneObjectPart part in grp.ChildrenList)
                        {
                            if (part.UUID == sceneObject.UUID)
                                continue;
                            sceneObject.AddChild(part, part.LinkNum);

                            part.StoreUndoState();
                        }
                        grps.Add(sceneObject);
                    }
                    regiondata.Groups = grps;
                }
                else
                    regiondata.Groups = new List<SceneObjectGroup>();
                return regiondata;
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Warn("[ProtobufRegionLoader]: Failed to load backup: " + ex.ToString());
                return null;
            }
        }

        public bool SaveBackup(string file, RegionData regiondata)
        {
            FileStream stream = null;
            try
            {
                stream = File.OpenWrite(file);
                ProtoBuf.Serializer.Serialize<RegionData>(stream, regiondata);
            }
            catch (Exception ex)
            {
                MainConsole.Instance.Warn("[ProtobufRegionLoader]: Failed to save backup: " + ex.ToString());
                return false;
            }
            finally
            {
                if (stream != null && stream.CanWrite)
                    stream.Close();
            }
            return true;
        }
    }
}