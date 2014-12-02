/*
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

using System.Collections.Generic;
using OpenMetaverse;

namespace WhiteCore.Framework.ClientInterfaces
{
    public static class SLUtil
    {
        #region SL / file extension / content-type conversions

        public static string SLAssetTypeToContentType(int assetType)
        {
            switch ((AssetType) assetType)
            {
                case AssetType.Texture:
                    return "image/x-j2c";
                case AssetType.Sound:
                    return "audio/ogg";
                case AssetType.CallingCard:
                    return "application/vnd.ll.callingcard";
                case AssetType.Landmark:
                    return "application/vnd.ll.landmark";
                case AssetType.Clothing:
                    return "application/vnd.ll.clothing";
                case AssetType.Object:
                    return "application/vnd.ll.primitive";
                case AssetType.Notecard:
                    return "application/vnd.ll.notecard";
                case AssetType.Folder:
                    return "application/vnd.ll.folder";
                case AssetType.RootFolder:
                    return "application/vnd.ll.rootfolder";
                case AssetType.LSLText:
                    return "application/vnd.ll.lsltext";
                case AssetType.LSLBytecode:
                    return "application/vnd.ll.lslbyte";
                case AssetType.TextureTGA:
                case AssetType.ImageTGA:
                    return "image/tga";
                case AssetType.Bodypart:
                    return "application/vnd.ll.bodypart";
                case AssetType.TrashFolder:
                    return "application/vnd.ll.trashfolder";
                case AssetType.SnapshotFolder:
                    return "application/vnd.ll.snapshotfolder";
                case AssetType.LostAndFoundFolder:
                    return "application/vnd.ll.lostandfoundfolder";
                case AssetType.SoundWAV:
                    return "audio/x-wav";
                case AssetType.ImageJPEG:
                    return "image/jpeg";
                case AssetType.Animation:
                    return "application/vnd.ll.animation";
                case AssetType.Gesture:
                    return "application/vnd.ll.gesture";
                case AssetType.Simstate:
                    return "application/x-metaverse-simstate";
                case AssetType.FavoriteFolder:
                    return "application/vnd.ll.favoritefolder";
                case AssetType.Link:
                    return "application/vnd.ll.link";
                case AssetType.LinkFolder:
                    return "application/vnd.ll.linkfolder";
                case AssetType.CurrentOutfitFolder:
                    return "application/vnd.ll.currentoutfitfolder";
                case AssetType.OutfitFolder:
                    return "application/vnd.ll.outfitfolder";
                case AssetType.MyOutfitsFolder:
                    return "application/vnd.ll.myoutfitsfolder";
                case AssetType.Unknown:
                default:
                    return "application/octet-stream";
            }
        }

        public static string SLInvTypeToContentType(int invType)
        {
            switch ((InventoryType) invType)
            {
                case InventoryType.Animation:
                    return "application/vnd.ll.animation";
                case InventoryType.CallingCard:
                    return "application/vnd.ll.callingcard";
                case InventoryType.Folder:
                    return "application/vnd.ll.folder";
                case InventoryType.Gesture:
                    return "application/vnd.ll.gesture";
                case InventoryType.Landmark:
                    return "application/vnd.ll.landmark";
                case InventoryType.LSL:
                    return "application/vnd.ll.lsltext";
                case InventoryType.Notecard:
                    return "application/vnd.ll.notecard";
                case InventoryType.Attachment:
                case InventoryType.Object:
                    return "application/vnd.ll.primitive";
                case InventoryType.Sound:
                    return "audio/ogg";
                case InventoryType.Snapshot:
                case InventoryType.Texture:
                    return "image/x-j2c";
                case InventoryType.Wearable:
                    return "application/vnd.ll.clothing";
                default:
                    return "application/octet-stream";
            }
        }

        public static sbyte ContentTypeToSLAssetType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                    return (sbyte) AssetType.Texture;
                case "application/ogg":
                case "audio/ogg":
                    return (sbyte) AssetType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte) AssetType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte) AssetType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                    return (sbyte) AssetType.Clothing;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte) AssetType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte) AssetType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte) AssetType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte) AssetType.RootFolder;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                    return (sbyte) AssetType.LSLText;
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte) AssetType.LSLBytecode;
                case "image/tga":
                    // Note that AssetType.TextureTGA will be converted to AssetType.ImageTGA
                    return (sbyte) AssetType.ImageTGA;
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte) AssetType.Bodypart;
                case "application/vnd.ll.trashfolder":
                    return (sbyte) AssetType.TrashFolder;
                case "application/vnd.ll.snapshotfolder":
                    return (sbyte) AssetType.SnapshotFolder;
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte) AssetType.LostAndFoundFolder;
                case "audio/x-wav":
                    return (sbyte) AssetType.SoundWAV;
                case "image/jpeg":
                    return (sbyte) AssetType.ImageJPEG;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte) AssetType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte) AssetType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte) AssetType.Simstate;
                case "application/vnd.ll.favoritefolder":
                    return (sbyte) AssetType.FavoriteFolder;
                case "application/vnd.ll.link":
                    return (sbyte) AssetType.Link;
                case "application/vnd.ll.linkfolder":
                    return (sbyte) AssetType.LinkFolder;
                case "application/vnd.ll.currentoutfitfolder":
                    return (sbyte) AssetType.CurrentOutfitFolder;
                case "application/vnd.ll.outfitfolder":
                    return (sbyte) AssetType.OutfitFolder;
                case "application/vnd.ll.myoutfitsfolder":
                    return (sbyte) AssetType.MyOutfitsFolder;
                case "application/octet-stream":
                default:
                    return (sbyte) AssetType.Unknown;
            }
        }

        public static sbyte ContentTypeToSLInvType(string contentType)
        {
            switch (contentType)
            {
                case "image/x-j2c":
                case "image/jp2":
                case "image/tga":
                case "image/jpeg":
                    return (sbyte) InventoryType.Texture;
                case "application/ogg":
                case "audio/ogg":
                case "audio/x-wav":
                    return (sbyte) InventoryType.Sound;
                case "application/vnd.ll.callingcard":
                case "application/x-metaverse-callingcard":
                    return (sbyte) InventoryType.CallingCard;
                case "application/vnd.ll.landmark":
                case "application/x-metaverse-landmark":
                    return (sbyte) InventoryType.Landmark;
                case "application/vnd.ll.clothing":
                case "application/x-metaverse-clothing":
                case "application/vnd.ll.bodypart":
                case "application/x-metaverse-bodypart":
                    return (sbyte) InventoryType.Wearable;
                case "application/vnd.ll.primitive":
                case "application/x-metaverse-primitive":
                    return (sbyte) InventoryType.Object;
                case "application/vnd.ll.notecard":
                case "application/x-metaverse-notecard":
                    return (sbyte) InventoryType.Notecard;
                case "application/vnd.ll.folder":
                    return (sbyte) InventoryType.Folder;
                case "application/vnd.ll.rootfolder":
                    return (sbyte) InventoryType.RootCategory;
                case "application/vnd.ll.lsltext":
                case "application/x-metaverse-lsl":
                case "application/vnd.ll.lslbyte":
                case "application/x-metaverse-lso":
                    return (sbyte) InventoryType.LSL;
                case "application/vnd.ll.trashfolder":
                case "application/vnd.ll.snapshotfolder":
                case "application/vnd.ll.lostandfoundfolder":
                    return (sbyte) InventoryType.Folder;
                case "application/vnd.ll.animation":
                case "application/x-metaverse-animation":
                    return (sbyte) InventoryType.Animation;
                case "application/vnd.ll.gesture":
                case "application/x-metaverse-gesture":
                    return (sbyte) InventoryType.Gesture;
                case "application/x-metaverse-simstate":
                    return (sbyte) InventoryType.Snapshot;
                case "application/octet-stream":
                default:
                    return (sbyte) InventoryType.Unknown;
            }
        }

        #endregion SL / file extension / content-type conversions

        /// <summary>
        ///     Parse a notecard in Linden format to a string of ordinary text.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        public static string ParseNotecardToString(string rawInput)
        {
            string[] output = ParseNotecardToList(rawInput).ToArray();

//            foreach (string line in output)
//                MainConsole.Instance.DebugFormat("[PARSE NOTECARD]: ParseNotecardToString got line {0}", line);

            return string.Join("\n", output);
        }

        /// <summary>
        ///     Parse a notecard in Linden format to a list of ordinary lines.
        /// </summary>
        /// <param name="rawInput"></param>
        /// <returns></returns>
        public static List<string> ParseNotecardToList(string rawInput)
        {
			string[] input;
            int idx = 0;
            int level = 0;
            List<string> output = new List<string>();
            string[] words;
			            
			//The Linden format always ends with a } after the input data.
			//Strip off trailing } so there is nothing after the input data.
			int i = rawInput.LastIndexOf("}");
			rawInput = rawInput.Remove(i, rawInput.Length-i);
			input = rawInput.Replace("\r", "").Split('\n');

            while (idx < input.Length)
            {
                if (input[idx] == "{")
                {
                    level++;
                    idx++;
                    continue;
                }

                if ((input[idx] == "}") && (level > 0))
                {
                    level--;
                    idx++;
                    continue;
                }

                if ((input[idx].EndsWith("}")) && (level > 0))
                {
                    input[idx] = input[idx].Remove(input[idx].Length - 1, 1);
                    level--;
                }

                if (input[idx].StartsWith("{"))
                {
                    input[idx] = input[idx].Remove(0, 1);
                    level++;
                }

                switch (level)
                {
                    case 0:
                        words = input[idx].Split(' '); // Linden text ver
                        // Notecards are created *really* empty. Treat that as "no text" (just like after saving an empty notecard)
                        if (words.Length < 3)
                            return output;

                        int version = int.Parse(words[3]);
                        if (version != 2)
                            return output;
                        break;
                    case 1:
                        words = input[idx].Split(' ');
                        if (words[0] == "LLEmbeddedItems")
                            break;
                        if (words[0] == "Text")
                        {
                            idx++;

							//Number of lines in notecard.
							int lines = input.Length - idx;
						    int line = 0;

						while (line < lines)
                            {
								//m_log.DebugFormat("[PARSE NOTECARD]: Adding line {0}", input[idx]);
								output.Add(input[idx]);
								idx++;
								line++;
                            }

                            return output;
                        }
                        break;
                    case 2:
                        words = input[idx].Split(' '); // count
                        if (words[0] == "count")
                        {
                            int c = int.Parse(words[1]);
                            if (c > 0)
                                return output;
                            break;
                        }
                        break;
                }
                idx++;
            }

            return output;
        }
    }
}