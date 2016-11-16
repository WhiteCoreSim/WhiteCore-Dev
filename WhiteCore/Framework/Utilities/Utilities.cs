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

//Uses the following website for the creation of the encrypted hashes
//http://www.obviex.com/samples/Encryption.aspx

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.Servers;

namespace WhiteCore.Framework.Utilities
{
    public static class Utilities
    {
        static string EncryptorType = "SHA1";
        static int EncryptIterations = 2;
        static int KeySize = 256;
        static string CachedExternalIP = "";
        public static string HostName = "";

        /// <summary>
        ///     Get the URL to the release notes for the current version of WhiteCore
        /// </summary>
        /// <returns></returns>
        public static string GetServerReleaseNotesURL ()
        {
            return (MainServer.Instance.Secure ? "https://" : "http://") + MainServer.Instance.HostName +
                   ":" + MainServer.Instance.Port + "/WhiteCoreServerRelease" + WhiteCoreServerVersion () + ".html";
        }

        /// <summary>
        ///     Get the URL to our sim
        /// </summary>
        /// <returns></returns>
        public static string GetAddress ()
        {
            return (MainServer.Instance.Secure ? "https://" : "http://") + MainServer.Instance.HostName + ":" +
                   MainServer.Instance.Port;
        }

        public static string GetShortRegionMaturity (int Maturity)
        {
            switch (Maturity) {
            case 13:
                return "(PG)";
            case 21:
                return "(M)";
            case 42:
                return "(AO)";
            default:
                return "(G)";
            }
        }

        public static string GetRegionMaturity (int Maturity)
        {
            switch (Maturity) {
            case 13:
                return "PG";
            case 21:
                return "Mature";
            case 42:
                return "Adult";
            default:
                return "Unknown";
            }
        }

        public static string GetMaxMaturity (int Maturity)
        {
            switch (Maturity) {
            case 0:
                return "PG";
            case 1:
                return "M";
            case 2:
                return "A";
            default:
                return "PG";
            }
        }

        /// <summary>
        ///     What is our version?
        /// </summary>
        /// <returns></returns>
        public static string WhiteCoreServerVersion ()
        {
            return VersionInfo.VERSION_NUMBER;
        }

        public static void SetEncryptorType (string type)
        {
            if (type == "SHA1" || type == "MD5") {
                EncryptorType = type;
            }
        }

        /// <summary>
        ///     This is for encryption, it sets the number of times to iterate through the encryption
        /// </summary>
        /// <param name="iterations"></param>
        public static void SetEncryptIterations (int iterations)
        {
            EncryptIterations = iterations;
        }

        /// <summary>
        ///     This is for encryption, it sets the size of the key
        /// </summary>
        /// <param name="size"></param>
        public static void SetKeySize (int size)
        {
            KeySize = size;
        }

        /// <summary>
        ///     Encrypts specified plaintext using Rijndael symmetric key algorithm
        ///     and returns a base64-encoded result.
        /// </summary>
        /// <param name="plainText">
        ///     Plaintext value to be encrypted.
        /// </param>
        /// <param name="passPhrase">
        ///     Passphrase from which a pseudo-random password will be derived. The
        ///     derived password will be used to generate the encryption key.
        ///     Passphrase can be any string. In this example we assume that this
        ///     passphrase is an ASCII string.
        /// </param>
        /// <param name="saltValue">
        ///     Salt value used along with passphrase to generate password. Salt can
        ///     be any string. In this example we assume that salt is an ASCII string.
        /// </param>
        /// <returns>
        ///     Encrypted value formatted as a base64-encoded string.
        /// </returns>
        public static string Encrypt (string plainText,
                                     string passPhrase,
                                     string saltValue)
        {
            // Convert strings into byte arrays.
            // Let us assume that strings only contain ASCII codes.
            // If strings include Unicode characters, use Unicode, UTF7, or UTF8 
            // encoding.
            byte [] initVectorBytes = Encoding.ASCII.GetBytes ("@IBAg3D4e5E6g7H5");
            byte [] saltValueBytes = Encoding.ASCII.GetBytes (saltValue);

            // Convert our plaintext into a byte array.
            // Let us assume that plaintext contains UTF8-encoded characters.
            byte [] plainTextBytes = Encoding.UTF8.GetBytes (plainText);

            // First, we must create a password, from which the key will be derived.
            // This password will be generated from the specified passphrase and 
            // salt value. The password will be created using the specified hash 
            // algorithm. Password creation can be done in several iterations.
            PasswordDeriveBytes password = new PasswordDeriveBytes (
                passPhrase,
                saltValueBytes,
                EncryptorType,
                EncryptIterations);

            // Use the password to generate pseudo-random bytes for the encryption
            // key. Specify the size of the key in bytes (instead of bits).
            byte [] keyBytes = password.GetBytes (KeySize / 8);
            password.Dispose ();

            // Create uninitialized Rijndael encryption object.
            RijndaelManaged symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC };

            // It is reasonable to set encryption mode to Cipher Block Chaining
            // (CBC). Use default options for other symmetric key parameters.

            // Generate encryptor from the existing key bytes and initialization 
            // vector. Key size will be defined based on the number of the key 
            // bytes.
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor (keyBytes, initVectorBytes);


            // Define memory stream which will be used to hold encrypted data.
            string cipherText = string.Empty;
            MemoryStream memoryStream = new MemoryStream ();
            CryptoStream cryptoStream = null;
            try {
                // Define cryptographic stream (always use Write mode for encryption).
                cryptoStream = new CryptoStream (memoryStream, encryptor, CryptoStreamMode.Write);
                // Start encrypting.
                cryptoStream.Write (plainTextBytes, 0, plainTextBytes.Length);

                // Finish encrypting.
                cryptoStream.FlushFinalBlock ();

                // Convert our encrypted data from a memory stream into a byte array.
                byte [] cipherTextBytes = memoryStream.ToArray ();

                // Close both streams.
                cryptoStream.Close ();

                // Convert encrypted data into a base64-encoded string.
                cipherText = Convert.ToBase64String (cipherTextBytes);

            } catch {
                if (cryptoStream != null)
                    cryptoStream.Close ();
            }

            // Return encrypted string.
            return cipherText;
        }

        /// <summary>
        ///     Decrypts specified ciphertext using Rijndael symmetric key algorithm.
        /// </summary>
        /// <param name="cipherText">
        ///     Base64-formatted ciphertext value.
        /// </param>
        /// <param name="passPhrase">
        ///     Passphrase from which a pseudo-random password will be derived. The
        ///     derived password will be used to generate the encryption key.
        ///     Passphrase can be any string. In this example we assume that this
        ///     passphrase is an ASCII string.
        /// </param>
        /// <param name="saltValue">
        ///     Salt value used along with passphrase to generate password. Salt can
        ///     be any string. In this example we assume that salt is an ASCII string.
        /// </param>
        /// <returns>
        ///     Decrypted string value.
        /// </returns>
        /// <remarks>
        ///     Most of the logic in this function is similar to the Encrypt
        ///     logic. In order for decryption to work, all parameters of this function
        ///     - except cipherText value - must match the corresponding parameters of
        ///     the Encrypt function which was called to generate the
        ///     ciphertext.
        /// </remarks>
        public static string Decrypt (string cipherText,
                                     string passPhrase,
                                     string saltValue)
        {
            // Convert strings defining encryption key characteristics into byte
            // arrays. Let us assume that strings only contain ASCII codes.
            // If strings include Unicode characters, use Unicode, UTF7, or UTF8
            // encoding.
            byte [] initVectorBytes = Encoding.ASCII.GetBytes ("@IBAg3D4e5E6g7H5");
            byte [] saltValueBytes = Encoding.ASCII.GetBytes (saltValue);

            // Convert our ciphertext into a byte array.
            byte [] cipherTextBytes = Convert.FromBase64String (cipherText);

            // First, we must create a password, from which the key will be 
            // derived. This password will be generated from the specified 
            // passphrase and salt value. The password will be created using
            // the specified hash algorithm. Password creation can be done in
            // several iterations.
            PasswordDeriveBytes password = new PasswordDeriveBytes (
                passPhrase,
                saltValueBytes,
                EncryptorType,
                EncryptIterations);

            // Use the password to generate pseudo-random bytes for the encryption
            // key. Specify the size of the key in bytes (instead of bits).
            byte [] keyBytes = password.GetBytes (KeySize / 8);
            password.Dispose ();

            // Create uninitialized Rijndael encryption object.
            RijndaelManaged symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC };

            // It is reasonable to set encryption mode to Cipher Block Chaining
            // (CBC). Use default options for other symmetric key parameters.

            // Generate decryptor from the existing key bytes and initialization 
            // vector. Key size will be defined based on the number of the key 
            // bytes.
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor (keyBytes, initVectorBytes);

            // Define memory stream which will be used to hold encrypted data.
            MemoryStream memoryStream = new MemoryStream (cipherTextBytes);

            // Define cryptographic stream (always use Read mode for encryption).
            CryptoStream cryptoStream = new CryptoStream (memoryStream, decryptor, CryptoStreamMode.Read);

            // Since at this point we don't know what the size of decrypted data
            // will be, allocate the buffer long enough to hold ciphertext;
            // plaintext is never longer than ciphertext.
            byte [] plainTextBytes = new byte [cipherTextBytes.Length];

            // Start decrypting.
            int decryptedByteCount = 0;
            try {
                decryptedByteCount = cryptoStream.Read (plainTextBytes, 0, plainTextBytes.Length);
            } catch (Exception) {
                return "";
            }

            // Close both streams.
            cryptoStream.Close ();

            // Convert decrypted data into a string. 
            // Let us assume that the original plaintext string was UTF8-encoded.
            string plainText = Encoding.UTF8.GetString (plainTextBytes, 0, decryptedByteCount);

            // Return decrypted string.   
            return plainText;
        }

        /// <summary>
        ///     Get OUR external IP
        /// </summary>
        /// <returns></returns>
        public static string GetExternalIp ()
        {
            if (CachedExternalIP == "") {
                // External IP Address (get your external IP locally)
                String externalIp = "";
                UTF8Encoding utf8 = new UTF8Encoding ();

                WebClient webClient = new WebClient ();
                try {
                    //Ask what is my ip for it
                    externalIp = utf8.GetString (webClient.DownloadData ("http://checkip.dyndns.org/"));
                    //Remove the HTML stuff
                    externalIp =
                        externalIp.Remove (0, 76).Split (new string [] { "</body>" }, StringSplitOptions.RemoveEmptyEntries)
                            [0];
                    NetworkUtils.InternetSuccess ();
                } catch (Exception) {
                    try {
                        externalIp =
                            utf8.GetString (webClient.DownloadData ("http://automation.whatismyip.com/n09230945.asp"));
                        NetworkUtils.InternetSuccess ();
                    } catch (Exception iex) {
                        NetworkUtils.InternetFailure ();
                        MainConsole.Instance.Error ("[Utilities]: Failed to get external IP, " + iex +
                                                   ", please check your internet connection (if this applies), setting to internal...");
                        externalIp = "127.0.0.1";
                    }
                }

                webClient.Dispose ();
                CachedExternalIP = externalIp;
                return externalIp;
            }

            return CachedExternalIP;
        }

        /// <summary>
        /// Get local IP address of system
        /// (Assumes only one address present... or gives the first valid address if mmultiple)
        /// </summary>
        /// <returns>The local ip.</returns>
        public static string GetLocalIp ()
        {

            IPHostEntry host;
            string localIP = "?";
            host = Dns.GetHostEntry (Dns.GetHostName ());
            foreach (IPAddress ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    localIP = ip.ToString ();
                    break;
                }
            }
            return localIP;
        }

        /// <summary>
        ///     Read a website into a string
        /// </summary>
        /// <param name="URL">URL to change into a string</param>
        /// <returns></returns>
        public static string ReadExternalWebsite (string URL)
        {
            String website = "";
            UTF8Encoding utf8 = new UTF8Encoding ();

            WebClient webClient = new WebClient ();
            if (NetworkUtils.CheckInternetConnection ()) {
                try {
                    byte [] bytes = webClient.DownloadData (URL);
                    website =
                        utf8.GetString (webClient.ResponseHeaders ["Content-Encoding"] == "gzip"
                                           ? UnGzip (bytes, 0)
                                           : bytes);
                    NetworkUtils.InternetSuccess ();
                } catch (Exception) {
                    NetworkUtils.InternetFailure ();
                }
            }
            webClient.Dispose ();
            return website;
        }

        static byte [] UnGzip (byte [] data, int start)
        {
            int size = BitConverter.ToInt32 (data, data.Length - 4);
            byte [] uncompressedData = new byte [size];
            MemoryStream memStream = new MemoryStream (data, start, (data.Length - start)) { Position = 0 };
            GZipStream gzStream = new GZipStream (memStream, CompressionMode.Decompress);

            try {
                gzStream.Read (uncompressedData, 0, size);
            } catch (Exception) {
                throw;
            }

            gzStream.Close ();
            return uncompressedData;
        }


        /// <summary>
        ///     Download the file from downloadLink and save it to WhiteCore + Version +
        /// </summary>
        /// <param name="downloadLink">Link to the download</param>
        /// <param name="filename">Name to put the download in</param>
        public static void DownloadFile (string downloadLink, string filename)
        {
            WebClient webClient = new WebClient ();
            try {
                MainConsole.Instance.Warn ("Downloading new file from " + downloadLink + " now into file " + filename +
                                          ".");
                webClient.DownloadFile (downloadLink, filename);
            } catch (Exception) {
            }
            webClient.Dispose ();
        }

        /// <summary>
        ///     Downloads a file async
        /// </summary>
        /// <param name="downloadLink"></param>
        /// <param name="filename"></param>
        /// <param name="onProgress">can be null</param>
        /// <param name="onComplete">can be null</param>
        public static void DownloadFileAsync (string downloadLink, string filename,
                                             DownloadProgressChangedEventHandler onProgress,
                                             AsyncCompletedEventHandler onComplete)
        {
            WebClient webClient = new WebClient ();
            try {
                MainConsole.Instance.Warn ("Downloading new file from " + downloadLink + " now into file " + filename +
                                          ".");
                if (onProgress != null)
                    webClient.DownloadProgressChanged += onProgress;
                if (onComplete != null)
                    webClient.DownloadFileCompleted += onComplete;
                webClient.DownloadFileAsync (new Uri (downloadLink), filename);
            } catch (Exception) {
            }
            webClient.Dispose ();
        }


        /// <summary>
        /// Determines whether a string is a valid email address.
        /// </summary>
        /// <returns><c>true</c> if the string is a valid email address; otherwise, <c>false</c>.</returns>
        /// <param name="address">Address.</param>
        public static bool IsValidEmail (string address)
        {
            const string EMailpatternStrict = @"^(([^<>()[\]\\.,;:\s@\""]+"
                                              + @"(\.[^<>()[\]\\.,;:\s@\""]+)*)|(\"".+\""))@"
                                              + @"((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}"
                                              + @"\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+"
                                              + @"[a-zA-Z]{2,}))$";
            Regex EMailreStrict = new Regex (EMailpatternStrict);
            return EMailreStrict.IsMatch (address);
        }

        /// <summary>
        /// Determines whether the specified userID is a system user.
        /// </summary>
        /// <returns><c>true</c> if the specified userID is a system user; otherwise, <c>false</c>.</returns>
        /// <param name="userID">User I.</param>
        public static bool IsSystemUser (OpenMetaverse.UUID userID)
        {
            var userId = userID.ToString ();
            return (userId == Constants.GovernorUUID ||
                     userId == Constants.RealEstateOwnerUUID ||
                     userId == Constants.LibraryOwner ||
                     userId == Constants.BankerUUID ||
                     userId == Constants.MarketplaceOwnerUUID
            );
        }

        public static bool IsLinuxOs {
            get {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        public static bool Is64BitOs {
            get {
                return Environment.Is64BitOperatingSystem;
            }
        }

        public static DateTime GetNextWeekday (DateTime start, DayOfWeek day)
        {
            // The (... + 7) % 7 ensures we end up with a value in the range [0, 6]
            int daysToAdd = ((int)day - (int)start.DayOfWeek + 7) % 7;
            return start.AddDays (daysToAdd);
        }

        public static class RandomPassword
        {
            static Random rand = new Random ();

            static readonly char [] VOWELS = { 'a', 'e', 'i', 'o', 'u' };
            static readonly char [] CONSONANTS = { 'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l', 'm', 'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'y', 'z' };
            static readonly char [] SYMBOLS = { '*', '?', '/', '\\', '%', '$', '#', '@', '!', '~' };
            static readonly char [] NUMBERS = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            /// <summary>
            /// Generates a random, human-readable password.
            ///
            /// </summary>
            /// <param name="numSyllables">Number of syllables the password will contain</param>
            /// <param name="numNumeric">Number of numbers the password will contain</param>
            /// <param name="numSymbols">Number of symbols the password will contain</param>
            /// <returns></returns>
            public static string Generate (int numSyllables, int numNumeric, int numSymbols)
            {
                StringBuilder pw = new StringBuilder ();
                for (int i = 0; i < numSyllables; i++) {
                    pw.Append (MakeSyllable ());

                    if (numNumeric > 0 && ((rand.Next () % 2) == 0)) {
                        pw.Append (MakeNumeric ());
                        numNumeric--;
                    }

                    if (numSymbols > 0 && ((rand.Next () % 2) == 0)) {
                        pw.Append (MakeSymbol ());
                        numSymbols--;
                    }
                }

                while (numNumeric > 0) {
                    pw.Append (MakeNumeric ());
                    numNumeric--;
                }

                while (numSymbols > 0) {
                    pw.Append (MakeSymbol ());
                    numSymbols--;
                }

                return pw.ToString ();
            }

            static char MakeSymbol ()
            {
                return SYMBOLS [rand.Next (SYMBOLS.Length)];
            }

            static char MakeNumeric ()
            {
                return NUMBERS [rand.Next (NUMBERS.Length)];
            }

            static string MakeSyllable ()
            {
                int len = rand.Next (3, 5); // will return either 3 or 4

                StringBuilder syl = new StringBuilder ();
                for (int i = 0; i < len; i++) {
                    char c;
                    if (i == 1) // the second should be a vowel, all else a consonant
                        c = VOWELS [rand.Next (VOWELS.Length)];
                    else
                        c = CONSONANTS [rand.Next (CONSONANTS.Length)];

                    // only first character can be uppercase
                    if (i == 0 && (rand.Next () % 2) == 0)
                        c = char.ToUpper (c);

                    // append
                    syl.Append (c);
                }

                return syl.ToString ();
            }
        }

        public class MarkovNameGenerator
        {
            //constructor
            public string FirstName (IEnumerable<string> sampleNames, int order, int minLength)
            {
                //fix parameter values
                if (order < 1)
                    order = 1;
                if (minLength < 1)
                    minLength = 1;

                _order = order;
                _minLength = minLength;

                //split comma delimited lines
                foreach (string line in sampleNames) {
                    string [] tokens = line.Split (',');
                    foreach (string word in tokens) {
                        string upper = word.Trim ().ToUpper ();
                        if (upper.Length < order + 1)
                            continue;
                        _samples.Add (upper);
                    }
                }

                //Build chains            
                foreach (string word in _samples) {
                    for (int letter = 0; letter < word.Length - order; letter++) {
                        string token = word.Substring (letter, order);
                        List<char> entry = null;
                        if (_chains.ContainsKey (token))
                            entry = _chains [token];
                        else {
                            entry = new List<char> ();
                            _chains [token] = entry;
                        }
                        entry.Add (word [letter + order]);
                    }
                }

                return NextName;
            }

            //Get the next random name
            public string NextName {
                get {
                    //get a random token somewhere in middle of sample word                
                    string s = "";
                    do {
                        int n = _rnd.Next (_samples.Count);
                        int nameLength = _samples [n].Length;
                        s = _samples [n].Substring (_rnd.Next (0, _samples [n].Length - _order), _order);
                        while (s.Length < nameLength) {
                            string token = s.Substring (s.Length - _order, _order);
                            char c = GetLetter (token);
                            if (c != '?')
                                s += GetLetter (token);
                            else
                                break;
                        }

                        if (s.Contains (" ")) {
                            string [] tokens = s.Split (' ');
                            s = "";
                            for (int t = 0; t < tokens.Length; t++) {
                                if (tokens [t] == "")
                                    continue;
                                if (tokens [t].Length == 1)
                                    tokens [t] = tokens [t].ToUpper ();
                                else
                                    tokens [t] = tokens [t].Substring (0, 1) + tokens [t].Substring (1).ToLower ();
                                if (s != "")
                                    s += " ";
                                s += tokens [t];
                            }
                        } else
                            s = s.Substring (0, 1) + s.Substring (1).ToLower ();
                    }
                    while (_used.Contains (s) || s.Length < _minLength);
                    _used.Add (s);
                    return s;
                }
            }

            //Reset the used names
            public void Reset ()
            {
                _used.Clear ();
            }

            //private members
            Dictionary<string, List<char>> _chains = new Dictionary<string, List<char>> ();
            List<string> _samples = new List<string> ();
            List<string> _used = new List<string> ();
            Random _rnd = new Random ();
            int _order;
            int _minLength;

            //Get a random letter from the chain
            char GetLetter (string token)
            {
                if (!_chains.ContainsKey (token))
                    return '?';
                List<char> letters = _chains [token];
                int n = _rnd.Next (letters.Count);
                return letters [n];
            }
        }

        public static string [] RegionNames = {
            "Aboyck","Aldburg", "Almaida", "Alqualonde", "Andunie", "Annuminas", "Armenelos", "Avallone",
            "Adaldoc", "Adaltha", "Adalva", "Aldlight", "Amamroth", "Amardas", "Amarmac", "Andor", "Arador",
            "Belegost", "Bree", "Brithombar", "Budgeford", "Baradthyryr", "Baranadan", "Baranadan", "Barangul",
            "Baranroth", "Belost", "Berigilda", "Blackflower", "Brighthall", "Brighttown", "Bymeadow",
            "Calembel", "Caras Galadhon", "Carn Dum", "Chelddon", "Combe", "Celandic", "Corwyn",
            "Dale", "Dol", "Dol Amroth", "Dol Guldur", "Doriver", "Dunharrow", "Dyke", "Dinoas", "Dinodine",
            "Dinodrida", "Dodinlot", "Dungroth",
            "Edhellond", "Eglarest", "Eldalonde", "Ephel Brandir", "Esgaroth", "Ethring", "Eastloch", "Eridell",
            "Forlond", "Formenos", "Fornost", "Framsburg", "Fairedge", "Fallfield",
            "Galabas", "Goblin Town", "Gondolin", "Gilthyryr", "Gorbatha", "Gorbava", "Gorhendic", "Gormadoc",
            "Gormagilda", "Grassedge", "Griffinland", "Gulthyryr",
            "Harlond", "Havens of Sirion", "Havens of the Falas", "Hobbiton", "Hyarastorni", "Havenbush",
            "Icecliff", "Isenamroth", "Isengorn", "Isenia", "Isenriel",
            "Linhir", "Lakemarsh", "Lothadan",
            "Mana", "Marbleburn", "Marblegate", "Mardoc", "Marmagilda", "Marmalac", "Melimac", "Mendic",
            "Mendrida", "Menedine", "Menegard", "Menemac", "Menlot", "Minas",
            "Minas Morgul", "Minas Tirith", "Mithlond", "Moria", "Nindamos", "Nogrod", "Newden",
            "Obel Halad", "Ondosto", "Osgiliath", "Orodruin", "Pelargir", 
            "Rivendell", "Romenna", "Raybourne", "Redcastle", "Rorilac","Rorimac",
            "Scary", "Snentham", "Sadrida", "Shoremoor", "Silverwald", "Springcoast", "Starryview", "Stonewind",
            "Stock", "Swaldton", "Senruin",
            "Tarnost", "Tharbad", "Tighfield", "Tirion", "Treton", 
            "Umbar", "Undertowers", "Upbourn", "Valmar", "Vinyamar", 
            "Wayness", "Wellspell", "Wildegrass", "Woodbutter","Waymeet"
        };

        public static string [] UserNames = {
            "Ada", "Aelgifu", "Aelith", "Almaric", "Amber", "Angerbotha", "Anselm", "Arathalion",
            "Arwen", "Assi", "Autumn", "Avice", "Aikins", "Alethea", "Alysia", "Andrea", "Avril",
            "Badacin", "Balcardil", "Banjo", "Bebba", "Belladonna",
            "Beofrith", "Beornwyn", "Beregond", "Bimbli", "Bonirun", "Boromir", "Brand", "Brodhrimgiel",
            "Bamburg", "Bastarache", "Battle", "Beeler", "Belle", "Boissonneault", "Briana", "Brianna", 
            "Brough", "Bulger", "Burrowes",
            "Carahir", "Celebrian", "Ceolwine", "Chalcedony", "Citrine", "Coina", "Copal", "Cugwelion",
            "Cwenthryth", "Daunless", "Dodpecil", "Dondercar", "Dora", "Draugwing", "Drogo", "Durduilorn",
            "Caroline", "Christofferson", "Chrystal", "Cleopatra", "Courtright", "Cowher", "Crafton",
            "Cranfield", "Crunk", "Crystle",
            "Demasi", "Donna", "Dora",
            "Effie", "Egil", "Einar", "Eistein", "Ellie", "Elrond", "Engeram", "Eomer", "Eowyn", "Erkenbrand",
            "Esau", "Escferth", "Ethelflaed", "Ethelhild", "Eustace", "Faerindeth", "Faith", "Flame", "Florian",
            "Edge", "Elana", "Elke", "Ellan", "Elvis", "English", "Estelle", "Eusebio", "Evins",
            "Frogo", "Fulke", "Galadriel", "Garnet", "Gili", "Gilraen", "Giluen", "Grimwald", "Guilford",
            "Gabrielle", "Gaddis", "Gidget", "Glasscock","Guluin",
            "Haematite", "Hamodoc", "Haneding", "Hawise", "Helm", "Hengest", "Herb", "Hereswith",
            "Herewulf", "Hirithelion", "Hollins", "Hrolfur", "Hugh", "Hunun", 
            "Idhil", "Ilirgar", "Ines", "Ioreth", "Isreal", "Isolde", "Ivandur", "Izagar",
            "Jacelyn", "Janett", "Jazmine", "Jerlene", "Ji", "Judd", "June", "Jewel",
            "Ketil", "Khazakal", "Kvelki", "Katheryn", "Keitha", "Koerber",
            "Ladboroc", "Lazuli", "Larger", "Lasandra", "Lauzon", "Leighty", "Lissa", "Lita", "Lyla",
            "Legolas", "Leo", "Leofwen", "Leofwyn", "Livina", "Lorund", "Lothlariel", "Lotho", "Luta", 
            "Madison", "Marcie", "Margarete", "Mccardle", "Megan", "Meneely", "Mickley", "Min", "Muntz",
            "Manham", "Manwise", "Maribel", "Maude", "Merewenna", "Meriadoc", "Mortardur", "Nauriel",
            "Naurmiriel", "Nelda", "Nora", "Norlyg", "Oddtuna", "Osric", "Paladin", "Pato", "Peridot", "Petronilla",
            "Philbo", "Polo", "Parsley", "Partin", "Pilar", "Pinnell",
            "Ranulf", "Rhodonite", "Richenda", "Ringal", "Rob", "Rorin", "Ruby", "Rufus", "Redden", "Ridlon",
            "Rosalie", "Rosario", "Rutha", "Ruindil", "Ruthedhail",
            "Sabelina", "Saexburgha", "Sarabelle", "Sardonyx", "Saul", "Sawny",
            "Sedryneth", "Snorin", "Snorkuld", "Snorri", "Sombur", "Soronthalion", "Spamfast", "Spirit",
            "Stathard", "Sunstone", "Sebastian", "Shane", "Shelby", "Speer", "Stephan", "Suzie",
            "Thadhronthael", "Theolaf", "Tholrondir", "Thorstein", "Thrili", "Tindomelos",
            "Tourmaline", "Trali", "Tulip", "Turid", "Tuxton", "Tanguay", "Teeters", "Tew", "Tomasa",
            "Ulfraed", "Van", "Verret", "Villani", "Villasenor ","Voquo",
            "Warin", "Wilbordic", "Wilf", "Wulfwaru", "Warren", "Wheless", "Winkles", "Witte", "Yule",
            "Zahradnik", "Zwiebel"
        };


        public static string TransactionTypeInfo (TransactionType transType)
        {
            switch (transType) {
            // One-Time Charges
            case TransactionType.GroupCreate: return "Group creation fee";
            case TransactionType.GroupJoin: return "Group joining fee";
            case TransactionType.UploadCharge: return "Upload charge";
            case TransactionType.LandAuction: return "Land auction fee";
            case TransactionType.ClassifiedCharge: return "Classified advert fee";
            // Recurrent Charges
            case TransactionType.ParcelDirFee: return "Parcel directory fee";
            case TransactionType.ClassifiedRenew: return "Classified renewal";
            case TransactionType.ScheduledFee: return "Scheduled fee";
            // Inventory Transactions
            case TransactionType.GiveInventory: return "Give inventory";
            // Transfers Between Users
            case TransactionType.ObjectSale: return "Object sale";
            case TransactionType.Gift: return "Gift";
            case TransactionType.LandSale: return "Land sale";
            case TransactionType.ReferBonus: return "Refer bonus";
            case TransactionType.InvntorySale: return "Inventory sale";
            case TransactionType.RefundPurchase: return "Purchase refund";
            case TransactionType.LandPassSale: return "Land parcel sale";
            case TransactionType.DwellBonus: return "Dwell bonus";
            case TransactionType.PayObject: return "Pay object";
            case TransactionType.ObjectPays: return "Object pays";
            case TransactionType.BuyMoney: return "Money purchase";
            case TransactionType.MoveMoney: return "Move money";
            // Group Transactions
            case TransactionType.GroupLiability: return "Group liability";
            case TransactionType.GroupDividend: return "Group dividend";
            // Event Transactions
            case TransactionType.EventFee: return "Event fee";
            case TransactionType.EventPrize: return "Event prize";
            // Stipend Credits
            case TransactionType.StipendPayment: return "Stipend payment";

            default: return "System Generated";
            }
        }

        /// <summary>
        /// Rounds up a DateTime to the nearest
        /// </summary>
        /// <returns>The up.</returns>
        /// <param name="dt">DateTime</param>
        /// <param name="d">Delta time</param>
        //public static DateTime RoundUp (DateTime dt, TimeSpan d)
        //{
        //    return new DateTime (((dt.Ticks + d.Ticks - 1) / d.Ticks) * d.Ticks);
        //}


        public static DateTime RoundUp (DateTime dt, TimeSpan d)
        {
            var modTicks = dt.Ticks % d.Ticks;
            var delta = modTicks != 0 ? d.Ticks - modTicks : 0;
            return new DateTime (dt.Ticks + delta, dt.Kind);
        }

        public static DateTime RoundDown (DateTime dt, TimeSpan d)
        {
            var delta = dt.Ticks % d.Ticks;
            return new DateTime (dt.Ticks - delta, dt.Kind);
        }

        public static DateTime RoundToNearest (DateTime dt, TimeSpan d)
        {
            var delta = dt.Ticks % d.Ticks;
            bool roundUp = delta > d.Ticks / 2;
            var offset = roundUp ? d.Ticks : 0;

            return new DateTime (dt.Ticks + offset - delta, dt.Kind);
        }
    }
}
