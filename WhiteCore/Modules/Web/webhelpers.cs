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
using System.IO;
using OpenMetaverse;
using WhiteCore.Framework.ClientInterfaces;
using WhiteCore.Framework.DatabaseInterfaces;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Services;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Web
{
    public static class WebHelpers
    {
        #region General

        public static List<Dictionary<string, object>> YesNoSelection (ITranslator translator, bool condition)
        {
            var yesNoArgs = new List<Dictionary<string, object>> ();
            yesNoArgs.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Yes")},
                {"selected", condition ? "selected" : ""}
            });
            yesNoArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("No") },
                {"selected", !condition ? "selected" : ""}
            });

            return yesNoArgs;
        }


        public static string YesNo (ITranslator translator, bool condition)
        {
            return condition ? translator.GetTranslatedString ("Yes") : translator.GetTranslatedString ("No");
        }

        public static List<Dictionary<string, object>> ShortMonthSelections (ITranslator translator)
        {
            // index is assumed Jan - 1 etc
            var monthsArgs = new List<Dictionary<string, object>> ();
            monthsArgs.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Jan_Short")} });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Feb_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Mar_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Apr_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("May_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Jun_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Jul_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Aug_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Sep_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Oct_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString ("Nov_Short") } });
            monthsArgs.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString("Dec_Short")}});

            return monthsArgs;
        }

        #endregion

        #region Classifieds

        public static List<Dictionary<string, object>> ClassifiedCategorySelections (int category, ITranslator translator)
        {
            // build category selections for classifieds
            var categories = new List<Dictionary<string, object>> ();
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatAll")},
                {"Index","0"},
                {"selected", category == 0 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatShopping")},
                {"Index","1"},
                {"selected", category == 1 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatLandRental")},
                {"Index","2"},
                {"selected", category == 2 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatPropertyRental")},
                {"Index","3"},
                {"selected", category == 3 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatSpecialAttraction")},
                {"Index","4"},
                {"selected", category == 4 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatNewProducts")},
                {"Index","5"},
                {"selected", category == 5 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatEmployment")},
                {"Index","6"},
                {"selected", category == 6 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatWanted")},
                {"Index","7"},
                {"selected", category == 7 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatService")},
                {"Index","8"},
                {"selected", category == 8 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatPersonal")},
                {"Index","9"},
                {"selected", category == 9 ? "selected" : "" } });

            return categories;
        }

        public static string ClassifiedMaturity (uint maturity)
        {
            switch (maturity) {
            case 4:
                return "PG";
            case 8:
                return "Mature";
            case 64:
                return "Adult";
            default:
                return "All";
            }
        }

        public static string ClassifiedCategory (uint category, ITranslator translator)
        {
            switch (category) {
            case 1: return translator.GetTranslatedString ("CatShopping");
            case 2: return translator.GetTranslatedString ("CatLandRental");
            case 3: return translator.GetTranslatedString ("CatPropertyRental");
            case 4: return translator.GetTranslatedString ("CatSpecialAttraction");
            case 5: return translator.GetTranslatedString ("CatNewProducts");
            case 6: return translator.GetTranslatedString ("CatEmployment");
            case 7: return translator.GetTranslatedString ("CatWanted");
            case 8: return translator.GetTranslatedString ("CatService");
            case 9: return translator.GetTranslatedString ("CatPersonal");
            default:
                return translator.GetTranslatedString ("CatAll");
            }
        }

        #endregion

        #region Events

        public static List<Dictionary<string, object>> EventCategorySelections (int category, ITranslator translator)
        {
            // build category selection
            var categories = new List<Dictionary<string, object>> ();
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatSelect")},
                {"Index","0"},
                {"selected", category == -1 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatAll")},
                {"Index","0"},
                {"selected", category == 0 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatDiscussion")},
                {"Index","18"},
                {"selected", category == 18 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatSports")},
                {"Index","19"},
                {"selected", category == 19 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatLiveMusic")},
                {"Index","20"},
                {"selected", category == 20 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatCommercial")},
                {"Index","22"},
                {"selected", category == 22 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatEntertainment")},
                {"Index","23"},
                {"selected", category == 23 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatGames")},
                {"Index","24"},
                {"selected", category == 24 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatPageants")},
                {"Index","25"},
                {"selected", category == 25 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatEducation")},
                {"Index","26"},
                {"selected", category == 26 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatArtsCulture")},
                {"Index","27"},
                {"selected", category == 27 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatCharitySupport")},
                {"Index","28"},
                {"selected", category == 28 ? "selected" : "" } });
            categories.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("CatMiscellaneous")},
                {"Index","29"},
                {"selected", category == 29 ? "selected" : "" } });

            return categories;
        }

        public static string EventCategoryImage (int category)
        {
            // return the image url for an event category
            var url = "images/icons/";
            switch (category) {
            case 0:  return url + "event.png";
            case 18: return url + "discussion.png";
            case 19: return url + "sports.png";
            case 20: return url + "livemusic.png";
            case 22: return url + "commercial.png";
            case 23: return url + "entertainment.png";
            case 24: return url + "games.png";
            case 25: return url + "pagent.png";
            case 26: return url + "education.png";
            case 27: return url + "arts.png";
            case 28: return url + "charity.png";
            case 29: return url + "misc.png";
            }

            return url + "event.png";
        }

        public static List<Dictionary<string, object>> EventTimeframesSelections (int timeframe, ITranslator translator)
        {

            // build timeframes
            var timeframes = new List<Dictionary<string, object>> ();
            timeframes.Add(new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Next7Days")},
                {"Index","168"},
                {"selected", timeframe == 168 ? "selected" : "" } });
            timeframes.Add(new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Next3Days")},
                {"Index","72"},
                {"selected", timeframe == 72 ? "selected" : "" } });
            timeframes.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Next24Hours")},
                {"Index","24"},
                {"selected", timeframe == 24 ? "selected" : "" } });
            timeframes.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Next10Hours")},
                {"Index","10"},
                {"selected", timeframe == 10 ? "selected" : "" } });
            timeframes.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Next4Hours")},
                {"Index","4"},
                {"selected", timeframe == 4 ? "selected" : "" } });
            timeframes.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Next2Hours")},
                {"Index","2"},
                {"selected", timeframe == 2 ? "selected" : "" } });

            return timeframes;

        }

        // Time selections
        public static List<Dictionary<string, object>> EventTimeSelections (string nearestHalf)
        {
            var timeoptions = new List<Dictionary<string, object>> ();
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "12:00 am"},
                {"Index","00:00:00"},
                {"selected", nearestHalf == "00:00:00" ? "selected" : "" }
            });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "12:30 am"},
                {"Index","00:30:00"},
                {"selected", nearestHalf == "00:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "1:00 am"},
                {"Index","01:00:00"},
                {"selected", nearestHalf == "01:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "1:30 am"},
                {"Index","01:30:00"},
                {"selected", nearestHalf == "01:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "2:00 am"},
                {"Index","02:00:00"},
                {"selected", nearestHalf == "02:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "2:30 am"},
                {"Index","02:30:00"},
                {"selected", nearestHalf == "02:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "3:00 am"},
                {"Index","03:00:00"},
                {"selected", nearestHalf == "03:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "3:30 am"},
                {"Index","03:30:00"},
                {"selected", nearestHalf == "03:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "4:00 am"},
                {"Index","04:00:00"},
                {"selected", nearestHalf == "04:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "4:30 am"},
                {"Index","04:30:00"},
                {"selected", nearestHalf == "04:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "5:00 am"},
                {"Index","05:00:00"},
                {"selected", nearestHalf == "05:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "5:30 am"},
                {"Index","05:30:00"},
                {"selected", nearestHalf == "05:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "6:00 am"},
                {"Index","06:00:00"},
                {"selected", nearestHalf == "06:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "6:30 am"},
                {"Index","06:30:00"},
                {"selected", nearestHalf == "06:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "7:00 am"},
                {"Index","07:00:00"},
                {"selected", nearestHalf == "07:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "7:30 am"},
                {"Index","07:30:00"},
                {"selected", nearestHalf == "07:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "8:00 am"},
                {"Index","08:00:00"},
                {"selected", nearestHalf == "08:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "8:30 am"},
                {"Index","08:30:00"},
                {"selected", nearestHalf == "08:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "9:00 am"},
                {"Index","09:00:00"},
                {"selected", nearestHalf == "09:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "9:30 am"},
                {"Index","09:30:00"},
                {"selected", nearestHalf == "09:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "10:00 am"},
                {"Index","10:00:00"},
                {"selected", nearestHalf == "10:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "10:30 am"},
                {"Index","10:30:00"},
                {"selected", nearestHalf == "10:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "11:00 am"},
                {"Index","11:00:00"},
                {"selected", nearestHalf == "11:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "11:30 am"},
                {"Index","11:30:00"},
                {"selected", nearestHalf == "11:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "12:00 pm"},
                {"Index","12:00:00"},
                {"selected", nearestHalf == "12:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "12:30 pm"},
                {"Index","12:30:00"},
                {"selected", nearestHalf == "12:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "1:00 pm"},
                {"Index","13:00:00"},
                {"selected", nearestHalf == "13:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "1:30 pm"},
                {"Index","13:30:00"},
                {"selected", nearestHalf == "13:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "2:00 pm"},
                {"Index","14:00:00"},
                {"selected", nearestHalf == "14:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "2:30 pm"},
                {"Index","14:30:00"},
                {"selected", nearestHalf == "14:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "3:00 pm"},
                {"Index","15:00:00"},
                {"selected", nearestHalf == "15:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "3:30 pm"},
                {"Index","15:30:00"},
                {"selected", nearestHalf == "15:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "4:00 pm"},
                {"Index","16:00:00"},
                {"selected", nearestHalf == "16:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "4:30 pm"},
                {"Index","16:30:00"},
                {"selected", nearestHalf == "16:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "5:00 pm"},
                {"Index","17:00:00"},
                {"selected", nearestHalf == "17:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "5:30 pm"},
                {"Index","17:30:00"},
                {"selected", nearestHalf == "17:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "6:00 pm"},
                {"Index","18:00:00"},
                {"selected", nearestHalf == "18:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "6:30 pm"},
                {"Index","18:30:00"},
                {"selected", nearestHalf == "18:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "7:00 pm"},
                {"Index","19:00:00"},
                {"selected", nearestHalf == "19:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "7:30 pm"},
                {"Index","19:30:00"},
                {"selected", nearestHalf == "19:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "8:00 pm"},
                {"Index","20:00:00"},
                {"selected", nearestHalf == "20:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "8:30 pm"},
                {"Index","20:30:00"},
                {"selected", nearestHalf == "20:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "9:00 pm"},
                {"Index","21:00:00"},
                {"selected", nearestHalf == "21:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "9:30 pm"},
                {"Index","21:30:00"},
                {"selected", nearestHalf == "21:30:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "10:00 pm"},
                {"Index","22:00:00"},
                {"selected", nearestHalf == "22:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "10:30 pm"},
                {"Index","22:30:00"},
                {"selected", nearestHalf == "22:3:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "11:00 pm"},
                {"Index","23:00:00"},
                {"selected", nearestHalf == "23:00:00" ? "selected" : "" } });
            timeoptions.Add (new Dictionary<string, object> {
                {"Value", "11:30 pm"},
                {"Index","23:30:00"},
                {"selected", nearestHalf == "23:30:00" ? "selected" : "" } });

            return timeoptions;
        }

        // event durations
        public static List<Dictionary<string, object>> EventDurationSelections (int duration)
        {
            var durations = new List<Dictionary<string, object>> ();
            durations.Add (new Dictionary<string, object> {
                {"Value", "10 minutes"},
                {"Index","10"},
                {"selected", duration == 10 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "15 minutes"},
                {"Index","15"},
                {"selected", duration == 15 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "20 minutes"},
                {"Index","20"},
                {"selected", duration == 20 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "25 minutes"},
                {"Index","25"},
                {"selected", duration == 25 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "30 minutes"},
                {"Index","30"},
                {"selected", duration == 30 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "45 minutes"},
                {"Index","45"},
                {"selected", duration == 45 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "1 hour"},
                {"Index","60"},
                {"selected", duration == 60 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "1.5 hours"},
                {"Index","90"},
                {"selected", duration == 90 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "2 hours"},
                {"Index","120"},
                {"selected", duration == 120 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "2.5 hours"},
                {"Index","150"},
                {"selected", duration == 150 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "3 hours"},
                {"Index","180"},
                {"selected", duration == 180 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "4 hours"},
                {"Index","240"},
                {"selected", duration == 240 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "5 hours"},
                {"Index","300"},
                {"selected", duration == 300 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "6 hours"},
                {"Index","360"},
                {"selected", duration == 360 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "7 hours"},
                {"Index","420"},
                {"selected", duration == 420 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "8 hours"},
                {"Index","480"},
                {"selected", duration == 480 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "9 hours"},
                {"Index","540"},
                {"selected", duration == 540 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "10 hours"},
                {"Index","600"},
                {"selected", duration == 600 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "11 hours"},
                {"Index","660"},
                {"selected", duration == 660 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "12 hours"},
                {"Index","720"},
                {"selected", duration == 720 ? "selected" : "" } });
            durations.Add (new Dictionary<string, object> {
                {"Value", "24 hours"},
                {"Index","1440"},
                {"selected", duration == 1440 ? "selected" : "" } });

            return durations;
        }

        public static List<Dictionary<string, object>> ParcelLocations (List<ExtendedLandData> myParcels, string selParcel)
        {
            var regionData = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
            var parcelList = new List<Dictionary<string, object>> ();

            foreach (var parcel in myParcels) {
                var region = regionData.Get (parcel.LandData.RegionID, null);

                if (region == null)     // something screwy here
                    continue;

                // future proofing
                if (region.IsForeign || region.IsHgRegion)
                    continue;

                // this might change
                if (!region.IsOnline)
                    continue;

                var regionMaturity = Utilities.GetShortRegionMaturity (region.Access);
                var regionArea = region.RegionArea < 1000000
                           ? region.RegionArea + " m2"
                           : (region.RegionArea / 1000000) + " km2";
                //var regionLocX = region.RegionLocX / Constants.RegionSize;
                //var regionLocY = region.RegionLocY / Constants.RegionSize;

                var regionName = parcel.RegionName;
                var parcelName = parcel.LandData.Name;
                var parcelLocX = (int)(parcel.GlobalPosX / Constants.RegionSize);
                var parcelLocY = (int)(parcel.GlobalPosY / Constants.RegionSize);
                var parcelUUID = parcel.LandData.GlobalID;
                var parcelUserLanding = parcel.LandData.UserLocation;

                var parcelLocation = parcelLocX.ToString () + ',' + parcelLocY;
                var parcelLanding = parcelUserLanding.X + "," + parcelUserLanding.Y + "," + parcelUserLanding.Z;

                var selected = "";
                if (selParcel != "")
                    if (parcelUUID.ToString () == selParcel)
                        selected = "selected";

                parcelList.Add (new Dictionary<string, object> {
                    {"Value",regionMaturity + " " + parcelName + " in " + regionName + " " + regionArea},
                            {"Index",parcelLocation + "," + parcelLanding + "," + parcelUUID},
                            {"disabled", region.IsOnline ? "" : "disabled"},        // always enabled as offline regions are ignored for now
                            {"selected", selected}
                    });

            }
            return parcelList;
        }

        // event locations
        public static List<Dictionary<string, object>> EventLocations (UserAccount user, IRegistryCore registry, string selParcel)
        {
            // Get current parcels on regions etc
            var regionList = new List<Dictionary<string, object>> ();
            var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();
            var groupService = registry.RequestModuleInterface<IGroupsModule> ();
            var friendsService = registry.RequestModuleInterface<IFriendsService> ();

            regionList.Add (new Dictionary<string, object> {
                {"Value", "---MY PARCELS---"},
                {"Index","0"},
                {"disabled","disabled"},
                {"selected", ""}
            });

            #region user land
            if (user != null) {
                var myParcels = directoryService.GetParcelByOwner (user.PrincipalID);
                if (myParcels.Count > 0)
                    regionList.AddRange (ParcelLocations (myParcels, selParcel));


                // Group owned parcels
                regionList.Add (new Dictionary<string, object> {
                    {"Value", "---GROUP PARCELS---"},
                    {"Index","0"},
                    {"disabled","disabled"},
                    {"selected", ""}
                });

                if (groupService != null) {
                    var grpmembership = groupService.GetMembershipData (user.PrincipalID);
                    if (grpmembership != null) {
                        foreach (var grp in grpmembership) {
                            var groupParcels = directoryService.GetParcelByOwner (grp.GroupID);
                            if (groupParcels.Count > 0)
                                regionList.AddRange (ParcelLocations (groupParcels, selParcel));
                        }
                    }
                }

                // Private Estate parcels
                regionList.Add (new Dictionary<string, object> {
                    {"Value", "---PRIVATE ESTATE PARCELS---"},
                    {"Index","0"},
                    {"disabled","disabled"},
                    {"selected", ""}
                });

            }
            #endregion

            // Public parcels
            regionList.Add (new Dictionary<string, object> {
                {"Value", "---PUBLIC PARCELS---"},
                {"Index","0"},
                {"disabled","disabled"},
                {"selected", ""}
            });
            var mainlandParcels = directoryService.GetParcelByOwner ((UUID)Constants.RealEstateOwnerUUID);
            if (mainlandParcels.Count > 0)
                regionList.AddRange (ParcelLocations (mainlandParcels, selParcel));

            // Friends parcels
            regionList.Add (new Dictionary<string, object> {
                {"Value", "---MY FRIENDS PARCELS---"},
                {"Index","0"},
                {"disabled","disabled"},
                {"selected", ""}
            });

            if (user != null) {
                var friends = friendsService.GetFriends (user.PrincipalID);
                foreach (var friend in friends) {
                    UUID friendID = UUID.Zero;
                    UUID.TryParse (friend.Friend, out friendID);

                    if (friendID != UUID.Zero) {
                        var friendParcels = directoryService.GetParcelByOwner (friendID);
                        if (friendParcels.Count > 0)
                            regionList.AddRange (ParcelLocations (friendParcels, selParcel));
                    }
                }
            }

            return regionList;

            /*  
                <select>
                <option value = "" disabled = "disabled" > &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ---MY PARCELS-- -</ option >
                < option value = "" disabled = "disabled" > &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ---GROUP - OWNED PARCELS-- -</ option >
                < option value = "" disabled = "disabled" > &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ---PRIVATE ISLAND PARCELS ---</ option >
                < option value = "" disabled = "disabled" > &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ---PUBLIC PARCELS ---</ option >
                < option value = "1020,995,0,0,0,d436261b-7186-42a6-dcd3-b80c1bcafaa4" >(G)Bacchus Island in Baffin / 136 / 180 & nbsp; &nbsp; (25232 m²)</ option >
                < option value = "991,1001,45,15,25,d2522787-9685-1b12-d5d4-2092a104f79c" >(M)Bayjou Theater in Bay City -Rollers / 45 / 15 & nbsp; &nbsp; (3360 m²)</ option >
                < option value = "1137,1052,0,0,0,17c0534e-23e2-5367-6ca0-8a33a834aa42" >(M)Blake Sea Rez Zone in Blake Sea -Haggerty / 228 / 228 & nbsp; &nbsp; (2304 m²)</ option >
                < option value = "995,1008,164,102,26,5e288455-3f06-80a7-e20d-262887bc80eb" >(G)Violet Welcome Area and Infohu in Violet / 164 / 102 & nbsp; &nbsp; (65536 m²)</ option >
                < option value = "1012,989,233,161,85,671a1590-9969-f565-1f37-464c0fe65257" >(G)Voss Lakeside Rez Zone in Voss / 233 / 161 & nbsp; &nbsp; (2816 m²)</ option >
                .... etc ....
                < option value = "" disabled = "disabled" > &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; ---FRIEND - OWNED PARCELS-- -</ option >
                </ select >
            */
        }

        // Lookups

        public static string EventMaturity (int maturity)
        {
            // these are shifted from the std EventData.maturity values to allow easier retrieval
            switch (maturity) {
            case 1:
                return "PG";
            case 2:
                return "Mature";
            case 4:
                return "Adult";
            default:
                return "All";
            }
        }


        public static string EventCategory (int category, ITranslator translator)
        {
            switch (category) {
            case 18: return translator.GetTranslatedString ("CatDiscussion");
            case 19: return translator.GetTranslatedString ("CatSports");
            case 20: return translator.GetTranslatedString ("CatLiveMusic");
            case 22: return translator.GetTranslatedString ("CatCommercial");
            case 23: return translator.GetTranslatedString ("CatEntertainment");
            case 24: return translator.GetTranslatedString ("CatGames");
            case 25: return translator.GetTranslatedString ("CatPageants");
            case 26: return translator.GetTranslatedString ("CatEducation");
            case 27: return translator.GetTranslatedString ("CatArtsCulture");
            case 28: return translator.GetTranslatedString ("CatCharitySupport");
            case 29: return translator.GetTranslatedString ("CatMiscellaneous");
            default: // 0
                return translator.GetTranslatedString ("CatAll");
            }

        }

        public static string EventDuration (int duration, ITranslator translator)
        {
            switch (duration) {
            case 10: return "10 " + translator.GetTranslatedString ("MinutesText");
            case 15: return "15 " + translator.GetTranslatedString ("MinutesText");
            case 20: return "20 " + translator.GetTranslatedString ("MinutesText");
            case 25: return "25 " + translator.GetTranslatedString ("MinutesText");
            case 30: return "30 " + translator.GetTranslatedString ("MinutesText");
            case 45: return "45 " + translator.GetTranslatedString ("MinutesText");
            case 60: return "1 " + translator.GetTranslatedString ("HourText");
            case 90: return "1.5 " + translator.GetTranslatedString ("HoursText");
            case 120: return "2 " + translator.GetTranslatedString ("HoursText");
            case 150: return "2.5 " + translator.GetTranslatedString ("HoursText");
            case 180: return "3 " + translator.GetTranslatedString ("HoursText");
            case 240: return "4 " + translator.GetTranslatedString ("HoursText");
            case 300: return "5 " + translator.GetTranslatedString ("HoursText");
            case 360: return "6 " + translator.GetTranslatedString ("HoursText");
            case 420: return "7 " + translator.GetTranslatedString ("HoursText");
            case 480: return "8 " + translator.GetTranslatedString ("HoursText");
            case 540: return "9 " + translator.GetTranslatedString ("HoursText");
            case 600: return "10 " + translator.GetTranslatedString ("HoursText");
            case 660: return "11 " + translator.GetTranslatedString ("HoursText");
            case 720: return "12 " + translator.GetTranslatedString ("HoursText");
            case 1440: return "24 " + translator.GetTranslatedString ("HoursText");
            default:
                return duration.ToString ();
            }
        }

        #endregion

        #region Regions

        public static List<Dictionary<string, object>> RegionTypeArgs (ITranslator translator, string selType)
        {
            if (selType != "")
                selType = selType.ToLower ().Substring (0, 1);
            var args = new List<Dictionary<string, object>> ();
            args.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString("Mainland")},
                { "Index","m"},
                { "selected", selType == "m" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                { "Value", translator.GetTranslatedString("Estate")},
                { "Index","e"},
                { "selected", selType == "e" ? "selected" : "" }
            });
            return args;
        }

        public static List<Dictionary<string, object>> RegionPresetArgs (ITranslator translator, string selPreset)
        {
            if (selPreset != "")
                selPreset = selPreset.ToLower ().Substring (0, 1);
            var args = new List<Dictionary<string, object>> ();
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("FullRegion")},
                {"Index","f"},
                { "selected", selPreset == "f" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Homestead")},
                {"Index","h"},
                { "selected", selPreset == "h" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Openspace")},
                {"Index","o"},
                { "selected", selPreset == "o" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("WhiteCore")},
                {"Index","w"},
                { "selected", selPreset == "w" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Custom")},
                {"Index","c"},
                { "selected", selPreset == "c" ? "selected" : "" }
            });
            return args;
        }

        public static List<Dictionary<string, object>> RegionTerrainArgs (ITranslator translator, string selTerrain)
        {
            if (selTerrain != "")
                selTerrain = selTerrain.ToLower ().Substring (0, 1);
            var args = new List<Dictionary<string, object>> ();
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Flatland")},
                {"Index","f"},
                { "selected", selTerrain == "f" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Grassland")},
                {"Index","g"},
                { "selected", selTerrain == "g" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Island")},
                {"Index","i"},
                { "selected", selTerrain == "i" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Aquatic")},
                {"Index","a"},
                { "selected", selTerrain == "a" ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Custom")},
                {"Index","c"},
                { "selected", selTerrain == "c" ? "selected" : "" }
            });
            return args;
        }

        public static List<Dictionary<string, object>> EstateSelections (IRegistryCore registry, string ownerId, int selEstate)
        {
            var estateList = new List<Dictionary<string, object>> ();
            var estateConnector = Framework.Utilities.DataManager.RequestPlugin<IEstateConnector> ();
            var accountService = registry.RequestModuleInterface<IUserAccountService> ();

            List<string> estates;
            if (ownerId != null) {
                var owner = UUID.Parse (ownerId);
                estates = estateConnector.GetEstateNames (owner);
            } else
                estates = estateConnector.GetEstateNames ();

            foreach (var estate in estates) {
                var estateID = estateConnector.GetEstateID (estate);
                EstateSettings ES = estateConnector.GetEstateIDSettings (estateID);

                if (ES != null) {
                    UserAccount estateOwner = accountService.GetUserAccount (null, ES.EstateOwner);

                    var selected = "";
                    if (selEstate > -1)
                        if (estateID == selEstate)
                            selected = "selected";

                    estateList.Add (new Dictionary<string, object> {
                        {"Value", ES.EstateName + " (" + estateOwner.Name + ")"},
                        {"Index", estateID},
                        {"selected", selected}
                    });

                }
            }
            return estateList;
        }

        /// <summary>
        /// Regions startup selection.
        /// </summary>
        /// <returns>The startup selection.</returns>
        /// <param name="translator">Translator.</param>
        /// <param name="selStartup">Sel startup.</param>
        public static List<Dictionary<string, object>> RegionStartupSelection (ITranslator translator, StartupType selStartup)
        {
            var args = new List<Dictionary<string, object>> ();
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("NormalText")},
                {"Index","n"},
                { "selected", selStartup == StartupType.Normal ? "selected" : "" }
            });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("DelayedText")},
                {"Index","m"},
                { "selected", selStartup == StartupType.Medium ? "selected" : "" }
            });

            return args;
        }

        /// <summary>
        /// Gets region selections.
        /// </summary>
        /// <returns>The selections.</returns>
        /// <param name="registry">Registry.</param>
        public static List<Dictionary<string, object>> RegionSelections (IRegistryCore registry)
        {
            var webTextureService = registry.RequestModuleInterface<IWebHttpTextureService> ();
            var simBase = registry.RequestModuleInterface<ISimulationBase> ();

            var defaultOarDir = Path.Combine (simBase.DefaultDataPath, Constants.DEFAULT_OARARCHIVE_DIR);
            var regionArchives = new List<Dictionary<string, object>> ();


            if (Directory.Exists (defaultOarDir)) {
                var archives = new List<string> (Directory.GetFiles (defaultOarDir, "*.oar"));
                archives.AddRange (new List<string> (Directory.GetFiles (defaultOarDir, "*.tgz")));
                foreach (string file in archives) {
                    var localPic = Path.ChangeExtension (file, "jpg");
                    if (File.Exists (localPic))
                        regionArchives.Add (new Dictionary<string, object> {
                        {"RegionArchiveSnapshotURL", webTextureService.GetImageURL(localPic)},
                        {"RegionArchive", file},
                        {"RegionArchiveName", Path.GetFileNameWithoutExtension(file)}
                    });
                    else
                        regionArchives.Add (new Dictionary<string, object> {
                        {"RegionArchiveSnapshotURL", "../images/icons/no_terrain.jpg"},
                        {"RegionArchive", file},
                        {"RegionArchiveName", Path.GetFileNameWithoutExtension(file)}
                    });
                }
            }
            return regionArchives;
        }


        #endregion

        #region Users
        /// <summary>
        /// Webpage UL type arguments.
        /// </summary>
        /// <returns>The type arguments.</returns>
        /// <param name="translator">Translator.</param>
        public static List<Dictionary<string, object>> UserTypeArgs (ITranslator translator)
        {
            var args = new List<Dictionary<string, object>> ();
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Guest")}, {"Index","0"} });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Resident")}, {"Index","1"} });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Member")}, {"Index","2"} });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Contractor")}, {"Index","3"} });
            args.Add (new Dictionary<string, object> {
                {"Value", translator.GetTranslatedString("Charter_Member")}, {"Index","4"} });
            return args;
        }

        /// <summary>
        /// Convert to to user flags.
        /// </summary>
        /// <returns>The type to user flags.</returns>
        /// <param name="userType">User type Index.</param>
        public static int UserTypeToUserFlags (string userType)
        {
            switch (userType) {
            case "0":
                return Constants.USER_FLAG_GUEST;
            case "1":
                return Constants.USER_FLAG_RESIDENT;
            case "2":
                return Constants.USER_FLAG_MEMBER;
            case "3":
                return Constants.USER_FLAG_CONTRACTOR;
            case "4":
                return Constants.USER_FLAG_CHARTERMEMBER;
            default:
                return Constants.USER_FLAG_GUEST;
            }
        }

        /// <summary>
        /// User flags to type string.
        /// </summary>
        /// <returns>The flag to type.</returns>
        /// <param name="userFlags">User flags.</param>
        /// <param name = "translator"></param>
        public static string UserFlagToType (int userFlags, ITranslator translator)
        {

            switch (userFlags) {
            case Constants.USER_FLAG_GUEST:
                return translator.GetTranslatedString ("Guest");
            case Constants.USER_FLAG_RESIDENT:
                return translator.GetTranslatedString ("Resident");
            case Constants.USER_FLAG_MEMBER:
                return translator.GetTranslatedString ("Member");
            case Constants.USER_FLAG_CONTRACTOR:
                return translator.GetTranslatedString ("Contractor");
            case Constants.USER_FLAG_CHARTERMEMBER:
                return translator.GetTranslatedString ("Charter_Member");
            default:
                return translator.GetTranslatedString ("Guest");
            }
        }


        /// <summary>
        /// User account selections.
        /// </summary>
        /// <returns>The selections.</returns>
        /// <param name="registry">Registry.</param>
        /// <param name="userID">User identifier.</param>
        public static List<Dictionary<string, object>> UserSelections (IRegistryCore registry, UUID userID)
        {
            var userList = new List<Dictionary<string, object>> ();
            var accountService = registry.RequestModuleInterface<IUserAccountService> ();

            var userAccts = accountService.GetUserAccounts (null, "*");

            foreach (var user in userAccts) {
                var selected = "";

                if (userID == user.PrincipalID)
                    selected = "selected";

                userList.Add (new Dictionary<string, object> {
                    {"Value", user.Name},
                    {"Index",user.PrincipalID},
                    {"selected", selected}
                });


            }
            return userList;
        }

        /// <summary>
        /// Builds available Avatar selections.
        /// </summary>
        /// <returns>The selections.</returns>
        /// <param name="registry">Registry.</param>
        public static List<Dictionary<string, object>> AvatarSelections (IRegistryCore registry)
        {
            var avArchiver = registry.RequestModuleInterface<IAvatarAppearanceArchiver> ();
            var webTextureService = registry.RequestModuleInterface<IWebHttpTextureService> ();
            var avatarArchives = new List<Dictionary<string, object>> ();
            var archives = avArchiver.GetAvatarArchives ();

            foreach (var archive in archives) {
                var archiveInfo = new Dictionary<string, object> ();
                archiveInfo.Add ("AvatarArchiveName", archive.FolderName);
                archiveInfo.Add ("AvatarArchiveSnapshotID", archive.Snapshot);
                archiveInfo.Add ("AvatarArchiveSnapshotURL", archive.LocalSnapshot != ""
                                 ? webTextureService.GetAvatarImageURL (archive.LocalSnapshot)
                                 : webTextureService.GetTextureURL (archive.Snapshot)
                                );
                avatarArchives.Add (archiveInfo);
            }

            return avatarArchives;
        }


        // user regions
        /*        public static List<Dictionary<string, object>> UserRegionSelections (UserAccount user, string selRegion)
                {
                    var regionList = new List<Dictionary<string, object>> ();
                    var directoryService = Framework.Utilities.DataManager.RequestPlugin<IDirectoryServiceConnector> ();


                    regionList.Add (new Dictionary<string, object> {
                            {"Value", "---MY REGIONS---"},
                            {"Index","0"},
                            {"disabled","disabled"},
                            {"selected", ""}
                            });

                    if (user != null) {
                        var myParcels = directoryService.GetParcelByOwner (user.PrincipalID);
                        if (myParcels.Count == 0) 
                            return regionList;

                        // build the user region list

                        var regionuuids = new List<UUID> ();
                        foreach (var parcel in myParcels) {
                            if (regionuuids.Contains (parcel.LandData.RegionID))
                                continue;
                            regionuuids.Add (parcel.LandData.RegionID);
                        }

                        var regionData = Framework.Utilities.DataManager.RequestPlugin<IRegionData> ();
                        foreach (UUID regionID in regionuuids) {
                            var region = regionData.Get (regionID, null);

                            // future proofing
                            if (region.IsForeign || region.IsHgRegion)
                                continue;

                            // this might change
                            if (!region.IsOnline)
                                continue;

                            var regionArea = region.RegionArea < 1000000
                                       ? region.RegionArea + " m2"
                                       : (region.RegionArea / 1000000) + " km2";
                            var regionLocX = region.RegionLocX / Constants.RegionSize;
                            var regionLocY = region.RegionLocY / Constants.RegionSize;

                            var regionName = region.RegionName;

                            var selected = "";
                            if (selRegion != "")
                                if (region.RegionID.ToString () == selRegion)
                                    selected = "selected";

                            regionList.Add (new Dictionary<string, object> {
                                    {"Value", region.RegionName + " " + regionArea},
                                    {"Index", region.RegionID},
                                    {"disabled", region.IsOnline ? "" : "disabled"},        // always enabled as offline regions are ignored for now
                                    {"selected", selected},
                                });
                        }

                    }
                    return regionList;
                }
        */
        #endregion

    }
}

