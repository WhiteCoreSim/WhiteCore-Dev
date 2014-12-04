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

using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace WhiteCore.Framework.ConsoleFramework
{
    /// <summary>
    ///     This is a special class designed to take over control of the command console prompt of
    ///     the server instance to allow for the input and output to the server to be redirected
    ///     by an external application, in this case a GUI based application on Windows.
    /// </summary>
    public class GUIConsole : ICommandConsole
    {
        public bool m_isPrompting;
        public int m_lastSetPromptOption;
        public List<string> m_promptOptions = new List<string>();
        public bool HasProcessedCurrentCommand { get; set; }

        public string LogPath
        {
            get{ return MainConsole.Instance.LogPath; }
            set{ MainConsole.Instance.LogPath = value;}
        }

        public virtual void Initialize(IConfigSource source, ISimulationBase baseOpenSim)
        {
            if (source.Configs["Console"] == null ||
                source.Configs["Console"].GetString("Console", String.Empty) != Name)
                return;

            baseOpenSim.ApplicationRegistry.RegisterModuleInterface<ICommandConsole>(this);
            MainConsole.Instance = this;

            m_Commands.AddCommand("help", "help", "Get a general command list", Help, false, true);

            MainConsole.Instance.Info("[GUIConsole] initialised.");
        }

        public void Help(IScene scene, string[] cmd)
        {
            List<string> help = m_Commands.GetHelp(cmd);

            foreach (string s in help)
                Output(s, Level.Off);
        }

        /// <summary>
        ///     Display a command prompt on the console and wait for user input
        /// </summary>
        public void Prompt()
        {
            // Set this culture for the thread 
            // to en-US to avoid number parsing issues
            Culture.SetCurrentCulture();
            /*string line = */
            ReadLine(m_defaultPrompt + "# ", true, true);

//            result.AsyncWaitHandle.WaitOne(-1);

//            if (line != String.Empty && line.Replace(" ", "") != String.Empty) //If there is a space, its fine
//            {
//                MainConsole.Instance.Info("[GUICONSOLE] Invalid command");
//            }
        }

        public void RunCommand(string cmd)
        {
            string[] parts = Parser.Parse(cmd);
            m_Commands.Resolve(parts);
            Output("", Threshold);
        }

        /// <summary>
        ///     Method that reads a line of text from the user.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="isCommand"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public virtual string ReadLine(string p, bool isCommand, bool e)
        {
            string oldDefaultPrompt = m_defaultPrompt;
            m_defaultPrompt = p;
//            System.Console.Write("{0}", p);
            string cmdinput = Console.ReadLine();

//            while (cmdinput.Equals(null))
//            {
//                ;
//            }

            if (isCommand)
            {
                string[] cmd = m_Commands.Resolve(Parser.Parse(cmdinput));

                if (cmd.Length != 0)
                {
                    int i;

                    for (i = 0; i < cmd.Length; i++)
                    {
                        if (cmd[i].Contains(" "))
                            cmd[i] = "\"" + cmd[i] + "\"";
                    }
                    return String.Empty;
                }
            }
            m_defaultPrompt = oldDefaultPrompt;
            return cmdinput;
        }

        public string Prompt(string p)
        {
            m_isPrompting = true;
            string line = ReadLine(String.Format("{0}: ", p), false, true);
            m_isPrompting = false;
            return line;
        }

        public string Prompt(string p, string def)
        {
            m_isPrompting = true;
            string ret = ReadLine(String.Format("{0} [{1}]: ", p, def), false, true);
            if (ret == String.Empty)
                ret = def;

            m_isPrompting = false;
            return ret;
        }

        public string Prompt(string p, string def, List<char> excludedCharacters)
        {
            m_isPrompting = true;
            bool itisdone = false;
            string ret = String.Empty;
            while (!itisdone)
            {
                itisdone = true;
                ret = Prompt(p, def);

                if (ret == String.Empty)
                {
                    ret = def;
                }
                else
                {
                    string ret1 = ret;

                    foreach (char c in excludedCharacters.Where(c => ret1.Contains(c.ToString())))
                    {
                        Console.WriteLine("The character \"" + c.ToString() + "\" is not permitted.");
                        itisdone = false;
                    }
                }
            }
            m_isPrompting = false;

            return ret;
        }

        // Displays a command prompt and returns a default value, user may only enter 1 of 2 options
        public string Prompt(string prompt, string defaultresponse, List<string> options)
        {
            m_isPrompting = true;
            m_promptOptions = new List<string>(options);

            bool itisdone = false;

            string optstr = options.Aggregate(String.Empty, (current, s) => current + (" " + s));

            string temp = Prompt(prompt, defaultresponse);
            while (itisdone == false)
            {
                if (options.Contains(temp))
                {
                    itisdone = true;
                }
                else
                {
                    Console.WriteLine("Valid options are" + optstr);
                    temp = Prompt(prompt, defaultresponse);
                }
            }
            m_isPrompting = false;
            m_promptOptions.Clear();
            return temp;
        }

        // Displays a prompt and waits for the user to enter a string, then returns that string
        // (Done with no echo and suitable for passwords)
        public string PasswordPrompt(string p)
        {
            m_isPrompting = true;
            string line = ReadLine(p + ": ", false, false);
            m_isPrompting = false;
            return line;
        }

        public virtual void Output(string text, Level level)
        {
        }

        public virtual void OutputNoTime(string text, Level level)
        {
        }

        public virtual void LockOutput()
        {
        }

        public virtual void UnlockOutput()
        {
        }

        public virtual bool CompareLogLevels(string a, string b)
        {
            Level aa = (Level)Enum.Parse(typeof(Level), a, true);
            Level bb = (Level)Enum.Parse(typeof(Level), b, true);
            return aa <= bb;
        }

        /// <summary>
        ///     The default prompt text.
        /// </summary>
        public virtual string DefaultPrompt
        {
            set { m_defaultPrompt = value; }
            get { return m_defaultPrompt; }
        }

        protected string m_defaultPrompt;

        public virtual string Name
        {
            get { return "GUIConsole"; }
        }

        public Commands m_Commands = new Commands();

        public Commands Commands
        {
            get { return m_Commands; }
            set { m_Commands = value; }
        }

        public List<IScene> ConsoleScenes
        {
            get { return m_ConsoleScenes; }
            set { m_ConsoleScenes = value; }
        }

        public IScene ConsoleScene
        {
            get { return m_ConsoleScene; }
            set { m_ConsoleScene = value; }
        }

        public List<IScene> m_ConsoleScenes = new List<IScene>();
        public IScene m_ConsoleScene = null;

        public void Dispose()
        {
        }

        /// <summary>
        ///     Starts the prompt for the console. This will never stop until the region is closed.
        /// </summary>
        public void ReadConsole()
        {
            while (true)
            {
                Prompt();
            }
        }

        private void t_Elapsed(object sender, ElapsedEventArgs e)
        {
            //Tell the GUI that we are still here and it needs to keep checking
            Console.Write((char) 0);
        }

        public Level Threshold { get; set; }

        #region ILog Members

        public bool IsDebugEnabled
        {
            get { return Threshold <= Level.Debug; }
        }

        public bool IsErrorEnabled
        {
            get { return Threshold <= Level.Error; }
        }

        public bool IsFatalEnabled
        {
            get { return Threshold <= Level.Fatal; }
        }

        public bool IsInfoEnabled
        {
            get { return Threshold <= Level.Info; }
        }

        public bool IsWarnEnabled
        {
            get { return Threshold <= Level.Warn; }
        }

        public bool IsTraceEnabled
        {
            get { return Threshold <= Level.Trace; }
        }

        public void Debug(object message)
        {
            Output(message.ToString(), Level.Debug);
        }

        public void DebugFormat(string format, params object[] args)
        {
            Output(string.Format(format, args), Level.Debug);
        }

        public void Error(object message)
        {
            Output(message.ToString(), Level.Error);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            Output(string.Format(format, args), Level.Error);
        }

        public void Fatal(object message)
        {
            Output(message.ToString(), Level.Fatal);
        }

        public void FatalFormat(string format, params object[] args)
        {
            Output(string.Format(format, args), Level.Fatal);
        }

        public void Format(Level level, string format, params object[] args)
        {
            Output(string.Format(format, args), level);
        }

        public void FormatNoTime(Level level, string format, params object[] args)
        {
            OutputNoTime(string.Format(format, args), level);
        }
        public void Info(object message)
        {
            Output(message.ToString(), Level.Info);
        }

        public void CleanInfo(object message)
        {
            OutputNoTime(message.ToString(), Level.Info);
        }

        public void CleanInfoFormat(string format, params object[] args)
        {
            OutputNoTime(string.Format(format, args), Level.Error);
        }

        public void Ticker()
        {
            Console.Write(".");
        }

        public void InfoFormat(string format, params object[] args)
        {
            Output(string.Format(format, args), Level.Info);
        }

        public void Log(Level level, object message)
        {
            Output(message.ToString(), level);
        }

        public void Trace(object message)
        {
            Output(message.ToString(), Level.Trace);
        }

        public void TraceFormat(string format, params object[] args)
        {
            Output(string.Format(format, args), Level.Trace);
        }

        public void Warn(object message)
        {
            Output(message.ToString(), Level.Warn);
        }

        public void WarnFormat(string format, params object[] args)
        {
            Output(string.Format(format, args), Level.Warn);
        }

        #endregion
    }
}