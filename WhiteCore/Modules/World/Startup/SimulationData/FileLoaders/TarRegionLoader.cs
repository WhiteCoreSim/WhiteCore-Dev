﻿/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
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
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.SceneInfo.Entities;
using WhiteCore.Framework.Serialization;
using WhiteCore.Region;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace WhiteCore.Modules
{
    public class TarRegionDataLoader : IRegionDataLoader
    {
        public string FileType
        {
            get { return ".abackup"; }
        }

        public RegionData LoadBackup(string file)
        {
            if (!File.Exists(file))
                return null;

            var stream = ArchiveHelpers.GetStream(file);
            if (stream == null)
                return null;

			MainConsole.Instance.Warn("[TarRegionDataLoader]: loading region data: " + file);
 
			GZipStream m_loadStream = new GZipStream(stream, CompressionMode.Decompress);
            TarArchiveReader reader = new TarArchiveReader(m_loadStream);
            List<uint> foundLocalIDs = new List<uint>();
            RegionData regiondata = new RegionData();
            regiondata.Init();

            byte[] data;
            string filePath;
            TarArchiveReader.TarEntryType entryType;
            System.Collections.Concurrent.ConcurrentQueue<byte[]> groups =
                new System.Collections.Concurrent.ConcurrentQueue<byte[]>();
            //Load the archive data that we need
            while ((data = reader.ReadEntry(out filePath, out entryType)) != null)
            {
				MainConsole.Instance.Warn(".");
               	if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                    continue;

                if (filePath.StartsWith("parcels/"))
                {
                    //Only use if we are not merging
                    LandData parcel = new LandData();
                    OSD parcelData = OSDParser.DeserializeLLSDBinary(data);
                    parcel.FromOSD((OSDMap) parcelData);
                    if (parcel.OwnerID != UUID.Parse("05948863-b678-433e-87a4-e44d17678d1d"))
                        //The default owner of the 'default' region
                        regiondata.Parcels.Add(parcel);
                }
                else if (filePath.StartsWith("newstyleterrain/"))
                {
                    regiondata.Terrain = data;
                }
                else if (filePath.StartsWith("newstylerevertterrain/"))
                {
                    regiondata.RevertTerrain = data;
                }
                else if (filePath.StartsWith("newstylewater/"))
                {
                    regiondata.Water = data;
                }
                else if (filePath.StartsWith("newstylerevertwater/"))
                {
                    regiondata.RevertWater = data;
                }
                else if (filePath.StartsWith("entities/"))
                {
                    groups.Enqueue(data);
                }
                else if (filePath.StartsWith("regioninfo/"))
                {
                    RegionInfo info = new RegionInfo();
                    info.FromOSD((OSDMap) OSDParser.DeserializeLLSDBinary(data));
                    regiondata.RegionInfo = info;
                }
                data = null;
            }
            m_loadStream.Close();
            m_loadStream = null;

            int threadCount = groups.Count > 16 ? 16 : groups.Count;
            System.Threading.Thread[] threads = new System.Threading.Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new System.Threading.Thread(() =>
                                                             {
                                                                 byte[] groupData;
                                                                 while (groups.TryDequeue(out groupData))
                                                                 {
                                                                     MemoryStream ms = new MemoryStream(groupData);
                                                                     ISceneEntity sceneObject =
                                                                         SceneEntitySerializer.SceneObjectSerializer
                                                                                              .FromXml2Format(ref ms,
                                                                                                              null);
                                                                     ms.Close();
                                                                     ms = null;
                                                                     data = null;
                                                                     if (sceneObject != null)
                                                                     {
                                                                         foreach (
                                                                             ISceneChildEntity part in
                                                                                 sceneObject.ChildrenEntities())
                                                                         {
                                                                             lock (foundLocalIDs)
                                                                             {
                                                                                 if (
                                                                                     !foundLocalIDs.Contains(
                                                                                         part.LocalId))
                                                                                     foundLocalIDs.Add(part.LocalId);
                                                                                 else
                                                                                     part.LocalId = 0;
                                                                                         //Reset it! Only use it once!
                                                                             }
                                                                         }
                                                                         regiondata.Groups.Add(
                                                                             sceneObject as SceneObjectGroup);
                                                                     }
                                                                 }
                                                             });
                threads[i].Start();
            }
            for (int i = 0; i < threadCount; i++)
                threads[i].Join();

            foundLocalIDs.Clear();

			MainConsole.Instance.Warn("[TarRegionDataLoader]: completed: ");

            return regiondata;
        }

        public bool SaveBackup(string fileName, RegionData regiondata)
        {
            try
            {
                bool oldFileExists = File.Exists(fileName);
                //Do new style saving here!
                GZipStream m_saveStream = new GZipStream(new FileStream(fileName + ".tmp", FileMode.Create),
                                                         CompressionMode.Compress);
                TarArchiveWriter writer = new TarArchiveWriter(m_saveStream);
                GZipStream m_loadStream = new GZipStream(new FileStream(fileName, FileMode.Open),
                                                         CompressionMode.Decompress);
                TarArchiveReader reader = new TarArchiveReader(m_loadStream);

                writer.WriteDir("parcels");

                foreach (LandData parcel in regiondata.Parcels)
                {
                    OSDMap parcelMap = parcel.ToOSD();
                    var binary = OSDParser.SerializeLLSDBinary(parcelMap);
                    writer.WriteFile("parcels/" + parcel.GlobalID.ToString(),
                                     binary);
                    binary = null;
                    parcelMap = null;
                }

                writer.WriteDir("newstyleterrain");
                writer.WriteDir("newstylerevertterrain");

                writer.WriteDir("newstylewater");
                writer.WriteDir("newstylerevertwater");

                writer.WriteDir("regioninfo");
                byte[] regionData = OSDParser.SerializeLLSDBinary(regiondata.RegionInfo.PackRegionInfoData());
                writer.WriteFile("regioninfo/regioninfo", regionData);

                try
                {
                    writer.WriteFile("newstyleterrain/" + regiondata.RegionInfo.RegionID.ToString() + ".terrain",
                                     regiondata.Terrain);

                    writer.WriteFile(
                        "newstylerevertterrain/" + regiondata.RegionInfo.RegionID.ToString() + ".terrain",
                        regiondata.RevertTerrain);

                    if (regiondata.Water != null)
                    {
                        writer.WriteFile("newstylewater/" + regiondata.RegionInfo.RegionID.ToString() + ".terrain",
                                         regiondata.Water);

                        writer.WriteFile(
                            "newstylerevertwater/" + regiondata.RegionInfo.RegionID.ToString() + ".terrain",
                            regiondata.RevertWater);
                    }
                }
                catch (Exception ex)
                {
                    MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                }

                List<UUID> entitiesToSave = new List<UUID>();
                foreach (ISceneEntity entity in regiondata.Groups)
                {
                    try
                    {
                        if (entity.IsAttachment ||
                            ((entity.RootChild.Flags & PrimFlags.Temporary) == PrimFlags.Temporary)
                            || ((entity.RootChild.Flags & PrimFlags.TemporaryOnRez) == PrimFlags.TemporaryOnRez))
                            continue;
                        if (entity.HasGroupChanged || !oldFileExists)
                        {
                            entity.HasGroupChanged = false;
                            //Write all entities
                            writer.WriteFile("entities/" + entity.UUID.ToString(), entity.ToBinaryXml2());
                        }
                        else
                            entitiesToSave.Add(entity.UUID);
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                        entitiesToSave.Add(entity.UUID);
                    }
                }

                if (oldFileExists)
                {
                    byte[] data;
                    string filePath;
                    TarArchiveReader.TarEntryType entryType;
                    //Load the archive data that we need
                    try
                    {
                        while ((data = reader.ReadEntry(out filePath, out entryType)) != null)
                        {
                            if (TarArchiveReader.TarEntryType.TYPE_DIRECTORY == entryType)
                                continue;
                            if (filePath.StartsWith("entities/"))
                            {
                                UUID entityID = UUID.Parse(filePath.Remove(0, 9));
                                if (entitiesToSave.Contains(entityID))
                                {
                                    writer.WriteFile(filePath, data);
                                    entitiesToSave.Remove(entityID);
                                }
                            }
                            data = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        MainConsole.Instance.WarnFormat("[Backup]: Exception caught: {0}", ex);
                    }

                    if (entitiesToSave.Count > 0)
                    {
                        MainConsole.Instance.Fatal(entitiesToSave.Count +
                                                   " PRIMS WERE NOT GOING TO BE SAVED! FORCE SAVING NOW! ");
                        foreach (ISceneEntity entity in regiondata.Groups)
                        {
                            if (entitiesToSave.Contains(entity.UUID))
                            {
                                if (entity.IsAttachment ||
                                    ((entity.RootChild.Flags & PrimFlags.Temporary) == PrimFlags.Temporary)
                                    || ((entity.RootChild.Flags & PrimFlags.TemporaryOnRez) == PrimFlags.TemporaryOnRez))
                                    continue;
                                //Write all entities
                                byte[] xml = entity.ToBinaryXml2();
                                writer.WriteFile("entities/" + entity.UUID.ToString(), xml);
                                xml = null;
                            }
                        }
                    }
                }

                reader.Close();
                writer.Close();
                m_loadStream.Close();
                m_saveStream.Close();
                GC.Collect();
            }
            catch (Exception ex)
            {
				MainConsole.Instance.Warn("[TarRegionDataLoader]: Failed to save backup: " + ex.ToString());
                return false;
            }
            return true;
        }
    }
}