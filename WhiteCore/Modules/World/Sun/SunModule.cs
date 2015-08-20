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


using System;
using System.Collections.Generic;
using System.Linq;
using Nini.Config;
using OpenMetaverse;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.PresenceInfo;
using WhiteCore.Framework.SceneInfo;

namespace WhiteCore.Modules.Sun
{
    public class SunModule : ISunModule
    {
        //
        // Global Constants used to determine where in the sky the sun is
        //
        const double m_SeasonalTilt = 0.03 * Math.PI;       // A daily shift of approximately 1.7188 degrees
        const double m_AverageTilt = -0.25 * Math.PI;       // A 45 degree tilt
        const double m_SunCycle = 2.0D * Math.PI;           // A perfect circle measured in radians
        const double m_SeasonalCycle = 2.0D * Math.PI;      // Ditto
        const int TICKS_PER_SECOND = 10000000;

        double HorizonShift;                                // Axis offset to skew day and night
        float Magnitude;                                    // Normal tilt
        float OrbitalPosition;                              // Orbital placement at a point in time
        ulong PosTime;
        Vector3 Position = Vector3.Zero;
        double SeasonSpeed;                                 // Rate of change for seasonal effects
        double SeasonalOffset;                              // Seasonal variation of tilt

        //
        //    Per Region Values
        //

        uint SecondsPerSunCycle;                            // Length of a virtual day in RW seconds
        uint SecondsPerYear;                                // Length of a virtual year in RW seconds
        double SunSpeed;                                    // Rate of passage in radians/second
        long TicksToEpoch;                                  // Elapsed time for 1/1/1970
        // double HoursToRadians;                             // Rate of change for seasonal effects
        long TicksUTCOffset;                                // seconds offset from UTC
        Quaternion Tilt = new Quaternion (1.0f, 0.0f, 0.0f, 0.0f);        // Calculated every update
        double TotalDistanceTravelled;                      // Distance since beginning of time (in radians)
        Vector3 Velocity = Vector3.Zero;
        double d_DayTimeSunHourScale = 0.5;                 // Day/Night hours are equal
        double d_day_length = 4;                            // A VW day is 4 RW hours long
        double d_day_night = 0.5;                           // axis offset: Default Horizon shift to try and closely match the sun model in LL Viewer
        int d_frame_mod = 25;                               // Every 2 seconds (actually less)
        string d_mode = "SL";
        int d_year_length = 60;                             // There are 60 VW days in a VW year
        double m_DayLengthHours;
        double m_DayTimeSunHourScale;
        double m_HorizonShift;
        string m_RegionMode = "SL";

        // Used to fix the sun in the sky so it doesn't move based on current time
        bool m_SunFixed;
        float m_SunFixedHour;
        int m_UpdateInterval;
        int m_YearLengthDays;
        // This solves a chick before the egg problem
        // the local SunFixedHour and SunFixed variables MUST be updated
        // at least once with the proper Region Settings before we start
        // updating those region settings in GenSunPos()
        IConfigSource m_config;
        uint m_frame;

        // Cached Scene reference
        IScene m_scene;
        bool m_sunIsReadyToRun;
        bool ready;


        // Current time in elapsed seconds since Jan 1st 1970
        ulong CurrentTime
        {
            get { return (ulong)(((DateTime.Now.Ticks) - TicksToEpoch + TicksUTCOffset)); }
        }

        // Time in seconds since UTC to use to calculate sun position.

        #region ISunModule Members

        public float GetCurrentTimeAsLindenSunHour ()
        {
            if (m_SunFixed)
                return m_SunFixedHour + 6;

            return GetCurrentSunHour () + 6.0f;
        }

        public double GetSunParameter (string param)
        {
            switch (param.ToLower ())
            {
            case "year_length":
                return m_YearLengthDays;

            case "day_length":
                return m_DayLengthHours;

            case "day_night_offset":
                return m_HorizonShift;

            case "day_time_sun_hour_scale":
                return m_DayTimeSunHourScale;

            case "update_interval":
                return m_UpdateInterval;

            default:
                throw new Exception ("Unknown sun parameter.");
            }
        }

        public void SetSunParameter (IScene scene, string param, double value)
        {
            HandleSunConsoleCommand (scene, new[] { param, value.ToString () });
        }

        public float GetCurrentSunHour ()
        {
            float ticksleftover = (CurrentTime / TICKS_PER_SECOND) % SecondsPerSunCycle;

            return (24.0f * (ticksleftover / SecondsPerSunCycle));
        }

        #endregion

        #region IRegion Methods

        public void Initialise (IConfigSource config)
        {
            m_frame = 0;
            m_config = config;
        }


        public void AddRegion (IScene scene)
        {
            m_scene = scene;
            // This one enables the ability to type just "sun" without any parameters
            if (MainConsole.Instance != null)
            {
                foreach (KeyValuePair<string, string> kvp in GetParamList())
                {
                    MainConsole.Instance.Commands.AddCommand (
                        String.Format ("sun {0}", kvp.Key),
                        String.Format ("sun {0}", kvp.Key),
                        String.Format ("{0} - {1}", kvp.Key, kvp.Value),
                        HandleSunConsoleCommand, true, false);
                }
            }


            TimeZone local = TimeZone.CurrentTimeZone;
            TicksUTCOffset = local.GetUtcOffset (local.ToLocalTime (DateTime.Now)).Ticks;
            //MainConsole.Instance.Debug("[SUN]: localtime offset is " + TicksUTCOffset);

            // Align ticks with Second Life

            TicksToEpoch = new DateTime (1970, 1, 1).Ticks;

            // Just in case they don't have the stanzas
            try
            {
                if (m_config.Configs ["Sun"] != null)
                {
                    // Mode: determines how the sun is handled
                    m_RegionMode = m_config.Configs ["Sun"].GetString ("mode", d_mode);
                    // Year length in days
                    m_YearLengthDays = m_config.Configs ["Sun"].GetInt ("year_length", d_year_length);
                    // Day length in decimal hours
                    m_DayLengthHours = m_config.Configs ["Sun"].GetDouble ("day_length", d_day_length);

                    // Horizon shift, this is used to shift the sun's orbit, this affects the day / night ratio
                    // must hard code to ~.5 to match sun position in LL based viewers
                    m_HorizonShift = m_config.Configs ["Sun"].GetDouble ("day_night_offset", d_day_night);

                    // Scales the sun hours 0...12 vs 12...24, essentially makes daylight hours longer/shorter vs nighttime hours
                    m_DayTimeSunHourScale = m_config.Configs ["Sun"].GetDouble ("day_time_sun_hour_scale",
                        d_DayTimeSunHourScale);
                    // Update frequency in frames
                    m_UpdateInterval = m_config.Configs ["Sun"].GetInt ("update_interval", d_frame_mod);
                } else
                {
                    m_RegionMode = d_mode;
                    m_YearLengthDays = d_year_length;
                    m_DayLengthHours = d_day_length;
                    m_HorizonShift = d_day_night;
                    m_UpdateInterval = d_frame_mod;
                    m_DayTimeSunHourScale = d_DayTimeSunHourScale;
                }
            } catch (Exception e)
            {
                MainConsole.Instance.Debug ("[SUN]: Configuration access failed, using defaults. Reason: " + e.Message);
                m_RegionMode = d_mode;
                m_YearLengthDays = d_year_length;
                m_DayLengthHours = d_day_length;
                m_HorizonShift = d_day_night;
                m_UpdateInterval = d_frame_mod;
                m_DayTimeSunHourScale = d_DayTimeSunHourScale;

                // m_latitude    = d_latitude;
                // m_longitude   = d_longitude;
            }
            switch (m_RegionMode)
            {
            case "T1":
            default:
            case "SL":
                // Time taken to complete a cycle (day and season)
                SecondsPerSunCycle = (uint)(m_DayLengthHours * 60 * 60);
                SecondsPerYear = (uint)(SecondsPerSunCycle * m_YearLengthDays);

                // Ratio of real-to-virtual time
                // VWTimeRatio        = 24/m_day_length;

                // Speed of rotation needed to complete a cycle in the
                // designated period (day and season)
                SunSpeed = m_SunCycle / SecondsPerSunCycle;
                SeasonSpeed = m_SeasonalCycle / SecondsPerYear;

                // Horizon translation
                HorizonShift = m_HorizonShift; // Z axis translation

                // HoursToRadians    = (SunCycle/24)*VWTimeRatio;

                //  Insert our event handling hooks
                scene.EventManager.OnFrame += SunUpdate;
                m_scene.EventManager.OnStartupComplete += EventManager_OnStartupComplete;
                scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
                scene.EventManager.OnEstateToolsSunUpdate += EstateToolsSunUpdate;

                ready = true;
                break;
            }

            scene.RegisterModuleInterface<ISunModule> (this);
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public void RegionLoaded (IScene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close ()
        {
            ready = false;

            // Remove our hooks
            m_scene.EventManager.OnStartupComplete -= EventManager_OnStartupComplete;
            m_scene.EventManager.OnFrame -= SunUpdate;
            m_scene.EventManager.OnAvatarEnteringNewParcel -= AvatarEnteringParcel;
            m_scene.EventManager.OnEstateToolsSunUpdate -= EstateToolsSunUpdate;
        }

        public string Name
        {
            get { return "SunModule"; }
        }

        void EventManager_OnStartupComplete (IScene scene, List<string> data)
        {
            //Get the old sun data
            if (m_scene.RegionInfo.RegionSettings.UseEstateSun)
            {
                m_SunFixedHour = (float)m_scene.RegionInfo.EstateSettings.SunPosition;
                m_SunFixed = m_scene.RegionInfo.EstateSettings.FixedSun;
            } else
            {
                m_SunFixedHour = (float)m_scene.RegionInfo.RegionSettings.SunPosition;
                m_SunFixed = m_scene.RegionInfo.RegionSettings.FixedSun;
            }
            m_sunIsReadyToRun = true;
        }

        #endregion

        #region EventManager Events

        public void SunToClient (IClientAPI client)
        {
            if (m_RegionMode != "T1")
            {
                if (ready)
                {
                    client.SendSunPos (Position, Velocity, m_SunFixed ? PosTime : CurrentTime,
                        SecondsPerSunCycle, SecondsPerYear, OrbitalPosition);
                }
            }
        }

        public void SunUpdate ()
        {
            if (((m_frame++ % m_UpdateInterval) != 0) || !ready || m_SunFixed /* || !receivedEstateToolsSunUpdate*/)
            {
                return;
            }

            GenSunPos (); // Generate shared values once

            SunUpdateToAllClients ();
        }

        /// <summary>
        ///     When an avatar enters the region, it's probably a good idea to send them the current sun info
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="oldParcel"></param>
        void AvatarEnteringParcel (IScenePresence avatar, ILandObject oldParcel)
        {
            SunToClient (avatar.ControllingClient);
        }

        /// <summary>
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="fixedSun">Is the sun's position fixed?</param>
        /// <param name="useEstateTime">Use the Region or Estate Sun hour?</param>
        /// <param name="fixedSunHour">What hour of the day is the Sun Fixed at?</param>
        public void EstateToolsSunUpdate (ulong regionHandle, bool fixedSun, bool useEstateTime, float fixedSunHour)
        {
            if (m_scene.RegionInfo.RegionHandle == regionHandle)
            {
                // Must limit the Sun Hour to 0 ... 24
                while (fixedSunHour > 24.0f)
                    fixedSunHour -= 24;

                while (fixedSunHour < 0)
                    fixedSunHour += 24;


                m_SunFixedHour = fixedSunHour;
                m_SunFixed = fixedSun;
                
                // Generate shared values
                GenSunPos ();

                // When sun settings are updated, we should update all clients with new settings.
                SunUpdateToAllClients ();


                //MainConsole.Instance.DebugFormat("[SUN]: PosTime : {0}", PosTime.ToString());
            }
        }

        #endregion

        /// <summary>
        ///     Calculate the sun's orbital position and its velocity.
        /// </summary>
        void GenSunPos ()
        {
            if (!m_sunIsReadyToRun)
                return; //We haven't set up the time for this region yet!
            // Time in seconds since UTC to use to calculate sun position.
            PosTime = (CurrentTime / TICKS_PER_SECOND);

            if (m_SunFixed)
            {
                // SunFixedHour represents the "hour of day" we would like
                // It's represented in 24hr time, with 0 hour being sun-rise
                // Because our day length is probably not 24hrs {LL is 6} we need to do a bit of math

                // Determine the current "day" from current time, so we can use "today"
                // to determine Seasonal Tilt and whatnot

                // Integer math rounded is on purpose to drop fractional day, determines number 
                // of virtual days since Epoch
                PosTime = (CurrentTime / TICKS_PER_SECOND) / SecondsPerSunCycle;

                // Since we want number of seconds since Epoch, multiply back up
                PosTime *= SecondsPerSunCycle;

                // Then offset by the current Fixed Sun Hour
                // Fixed Sun Hour needs to be scaled to reflect the user configured Seconds Per Sun Cycle
                PosTime += (ulong)((m_SunFixedHour / 24.0) * SecondsPerSunCycle);
            } else
            {
                if (m_DayTimeSunHourScale != 0.5f)
                {
                    ulong CurDaySeconds = (CurrentTime / TICKS_PER_SECOND) % SecondsPerSunCycle;
                    double CurDayPercentage = (double)CurDaySeconds / SecondsPerSunCycle;

                    ulong DayLightSeconds = (ulong)(m_DayTimeSunHourScale * SecondsPerSunCycle);
                    ulong NightSeconds = SecondsPerSunCycle - DayLightSeconds;

                    PosTime = (CurrentTime / TICKS_PER_SECOND) / SecondsPerSunCycle;
                    PosTime *= SecondsPerSunCycle;

                    if (CurDayPercentage < 0.5)
                    {
                        PosTime += (ulong)((CurDayPercentage / .5) * DayLightSeconds);
                    } else
                    {
                        PosTime += DayLightSeconds;
                        PosTime += (ulong)(((CurDayPercentage - 0.5) / .5) * NightSeconds);
                    }
                }
            }

            TotalDistanceTravelled = SunSpeed * PosTime; // distance measured in radians

            OrbitalPosition = (float)(TotalDistanceTravelled % m_SunCycle); // position measured in radians

            // TotalDistanceTravelled += HoursToRadians-(0.25*Math.PI)*Math.Cos(HoursToRadians)-OrbitalPosition;
            // OrbitalPosition         = (float) (TotalDistanceTravelled%SunCycle);

            SeasonalOffset = SeasonSpeed * PosTime;
            // Present season determined as total radians travelled around season cycle
            Tilt.W = (float)(m_AverageTilt + (m_SeasonalTilt * Math.Sin (SeasonalOffset)));
            // Calculate seasonal orbital N/S tilt

            // MainConsole.Instance.Debug("[SUN] Total distance travelled = "+TotalDistanceTravelled+", present position = "+OrbitalPosition+".");
            // MainConsole.Instance.Debug("[SUN] Total seasonal progress = "+SeasonalOffset+", present tilt = "+Tilt.W+".");

            // The sun rotates about the Z axis

            Position.X = (float)Math.Cos (-TotalDistanceTravelled);
            Position.Y = (float)Math.Sin (-TotalDistanceTravelled);
            Position.Z = 0;

            // For interest we rotate it slightly about the X access.
            // Celestial tilt is a value that ranges .025

            Position *= Tilt;

            // Finally we shift the axis so that more of the
            // circle is above the horizon than below. This
            // makes the nights shorter than the days.

            Position = Vector3.Normalize (Position);
            Position.Z = Position.Z + (float)HorizonShift;
            Position = Vector3.Normalize (Position);

            // MainConsole.Instance.Debug("[SUN] Position("+Position.X+","+Position.Y+","+Position.Z+")");

            Velocity.X = 0;
            Velocity.Y = 0;
            Velocity.Z = (float)SunSpeed;

            // Correct angular velocity to reflect the seasonal rotation

            Magnitude = Position.Length ();
            if (m_SunFixed)
            {
                Velocity.X = 0;
                Velocity.Y = 0;
                Velocity.Z = 0;
            } else
            {
                Velocity = (Velocity * Tilt) * (1.0f / Magnitude);
            }
            m_scene.RegionInfo.RegionSettings.SunVector = Position;
            m_scene.RegionInfo.RegionSettings.SunPosition = GetCurrentTimeAsLindenSunHour ();
        }

        void SunUpdateToAllClients ()
        {
            m_scene.ForEachScenePresence (delegate(IScenePresence sp) {
                if (!sp.IsChildAgent)
                {
                    SunToClient (sp.ControllingClient);
                }
            });
        }

        public void HandleSunConsoleCommand (IScene scene, string[] cmdparams)
        {
            MainConsole.Instance.InfoFormat ("[Sun]: Processing command.");

            foreach (string output in ParseCmdParams(cmdparams))
            {
                MainConsole.Instance.Info ("[SUN] " + output);
            }
        }

        Dictionary<string, string> GetParamList ()
        {
            Dictionary<string, string> Params = new Dictionary<string, string> {
                { "year_length", "number of days to a year" },
                { "day_length", "number of seconds to a day" },
                { "day_night_offset", "induces a horizon shift" },
                { "update_interval", "how often to update the sun's position in frames" },
                { "day_time_sun_hour_scale", "scales day light vs night hours to change day/night ratio" }
            };

            return Params;
        }

        List<string> ParseCmdParams(string[] args)
        {
            List<string> Output = new List<string>();

            if ((args.Length == 1) || (args[1].ToLower() == "help") || (args[1].ToLower() == "list"))
            {
                Output.Add("The following parameters can be changed or viewed:");
                Output.AddRange(GetParamList().Select(kvp => String.Format("{0} - {1}", kvp.Key, kvp.Value)));
                return Output;
            }

            if (args.Length == 2)
            {
                try
                {
                    double value = GetSunParameter(args[1]);
                    Output.Add(String.Format("Parameter {0} is {1}.", args[1], value));
                }
                catch (Exception)
                {
                    Output.Add(String.Format("Unknown parameter {0}.", args[1]));
                }
            }
            else if (args.Length == 3)
            {
                float value;
                if (!float.TryParse(args[2], out value))
                {
                    Output.Add(String.Format("The parameter value {0} is not a valid number.", args[2]));
                }

                switch (args[1].ToLower())
                {
                    case "year_length":
                        m_YearLengthDays = (int) value;
                        break;

                    case "day_length":
                        m_DayLengthHours = value;
                        break;

                    case "day_night_offset":
                        m_HorizonShift = value;
                        break;

                    case "day_time_sun_hour_scale":
                        m_DayTimeSunHourScale = value;
                        break;

                    case "update_interval":
                        m_UpdateInterval = (int) value;
                        break;

                    default:
                        Output.Add(String.Format("Unknown parameter {0}.", args[1]));
                        return Output;
                }

                Output.Add(String.Format("Parameter {0} set to {1}.", args[1], value));

                // Generate shared values
                GenSunPos();

                // When sun settings are updated, we should update all clients with new settings.
                SunUpdateToAllClients();
            }

            return Output;
        }
    }
}