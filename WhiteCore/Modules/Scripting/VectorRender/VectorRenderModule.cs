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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.Modules;
using WhiteCore.Framework.SceneInfo;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.Modules.Scripting
{
    public class VectorRenderModule : INonSharedRegionModule, IDynamicTextureRender
    {
        string m_fontName = "Arial";
        Graphics m_graph;

        string m_name = "VectorRenderModule";
        IScene m_scene;
        IDynamicTextureManager m_textureManager;

        #region IDynamicTextureRender Members

        public string GetContentType ()
        {
            return ("vector");
        }

        public string GetName ()
        {
            return m_name;
        }

        public bool SupportsAsynchronous ()
        {
            return true;
        }

        public byte [] ConvertUrl (string url, string extraParams)
        {
            return null;
        }

        public byte [] ConvertStream (Stream data, string extraParams)
        {
            return null;
        }

        public bool AsyncConvertUrl (UUID id, string url, string extraParams)
        {
            return false;
        }

        public bool AsyncConvertData (UUID id, string bodyData, string extraParams)
        {
            Draw (bodyData, id, extraParams);
            return true;
        }

        public void GetDrawStringSize (string text, string fontName, int fontSize,
                                      out double xSize, out double ySize)
        {
            Font myFont = new Font (fontName, fontSize);
            SizeF stringSize = new SizeF ();
            stringSize = m_graph.MeasureString (text, myFont);
            xSize = stringSize.Width;
            ySize = stringSize.Height;
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise (IConfigSource config)
        {
            IConfig cfg = config.Configs ["VectorRender"];
            if (null != cfg) {
                m_fontName = cfg.GetString ("font_name", m_fontName);
            }
        }

        public void AddRegion (IScene scene)
        {
            m_scene = scene;

            Bitmap bitmap = new Bitmap (1024, 1024, PixelFormat.Format32bppArgb);
            m_graph = Graphics.FromImage (bitmap);
        }

        public void RemoveRegion (IScene scene)
        {
        }

        public void RegionLoaded (IScene scene)
        {
            m_textureManager = m_scene.RequestModuleInterface<IDynamicTextureManager> ();
            if (m_textureManager != null)
                m_textureManager.RegisterRender (GetContentType (), this);
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        public void Close ()
        {
        }

        public string Name {
            get { return m_name; }
        }

        #endregion

        void Draw (string data, UUID id, string extraParams)
        {
            // We need to cater for old scripts that didn't use extraParams neatly, they use either an integer size which represents both width and height, or setalpha
            // we will now support multiple comma separated parameters in the form  width:256,height:512,alpha:255
            int width = 256;
            int height = 256;
            int alpha = 255; // 0 is transparent
            Color bgColour = Color.White; // Default background color
            char altDataDelim = ';';

            char [] paramDelimiter = { ',' };
            char [] nvpDelimiter = { ':' };

            extraParams = extraParams.Trim ();
            extraParams = extraParams.ToLower ();

            string [] nvps = extraParams.Split (paramDelimiter);

            int temp = -1;
            foreach (string pair in nvps) {
                string [] nvp = pair.Split (nvpDelimiter);
                string name = "";
                string value = "";

                if (nvp [0] != null) {
                    name = nvp [0].Trim ();
                }

                if (nvp.Length == 2) {
                    value = nvp [1].Trim ();
                }

                switch (name) {
                case "width":
                    temp = parseIntParam (value);
                    if (temp != -1) {
                        if (temp < 1) {
                            width = 1;
                        } else if (temp > 2048) {
                            width = 2048;
                        } else {
                            width = temp;
                        }
                    }
                    break;
                case "height":
                    temp = parseIntParam (value);
                    if (temp != -1) {
                        if (temp < 1) {
                            height = 1;
                        } else if (temp > 2048) {
                            height = 2048;
                        } else {
                            height = temp;
                        }
                    }
                    break;
                case "alpha":
                    temp = parseIntParam (value);
                    if (temp != -1) {
                        if (temp < 0) {
                            alpha = 0;
                        } else if (temp > 255) {
                            alpha = 255;
                        } else {
                            alpha = temp;
                        }
                    }
                    // Allow a bitmap w/o the alpha component to be created
                    else if (value.ToLower () == "false") {
                        alpha = 256;
                    }
                    break;
                case "bgcolour":
                case "bgcolor":
                    int hex = 0;
                    bgColour = int.TryParse (value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex)
                                   ? Color.FromArgb (hex)
                                   : Color.FromName (value);
                    break;
                case "altdatadelim":
                    altDataDelim = value [0];
                    break;
                case "":
                    // blank string has been passed do nothing just use defaults
                    break;
                default: // this is all for backwards compatibility, all a bit ugly hopefully can be removed in future
                         // could be either set alpha or just an int
                    if (name == "setalpha") {
                        alpha = 0; // set the texture to have transparent background (maintains backwards compatibility)
                    } else {
                        // this function used to accept an int on its own that represented both 
                        // width and height, this is to maintain backwards compatibility, could be removed
                        // but would break existing scripts
                        temp = parseIntParam (name);
                        if (temp != -1) {
                            if (temp > 1024)
                                temp = 1024;

                            if (temp < 128)
                                temp = 128;

                            width = temp;
                            height = temp;
                        }
                    }
                    break;
                }
            }

            Bitmap bitmap;

            bitmap = alpha == 256
                         ? new Bitmap (width, height, PixelFormat.Format32bppRgb)
                         : new Bitmap (width, height, PixelFormat.Format32bppArgb);

            Graphics graph = Graphics.FromImage (bitmap);
            // this is really just to save people filling the 
            // background color in their scripts, only do when fully opaque
            if (alpha >= 255) {
                graph.FillRectangle (new SolidBrush (bgColour), 0, 0, width, height);
            }

            FastBitmap fastBitmap = new FastBitmap (bitmap);
            fastBitmap.LockBitmap ();
            try {
                for (int w = 0; w < bitmap.Width; w++) {
                    if (alpha <= 255) {
                        for (int h = 0; h < bitmap.Height; h++) {
                            fastBitmap.SetPixel (w, h, Color.FromArgb (alpha, fastBitmap.GetPixel (w, h)));
                        }
                    }
                }
            } catch {
                MainConsole.Instance.Error (
                    "[Vector rendering]: FastBMP encode Failed!");
            }
            fastBitmap.UnlockBitmap ();
            bitmap = fastBitmap.Bitmap ();

            graph.SmoothingMode = SmoothingMode.AntiAlias;
            graph.CompositingQuality = CompositingQuality.HighQuality;
            graph.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            GDIDraw (data, graph, altDataDelim);

            graph.Dispose ();

            byte [] imageJ2000 = new byte [0];
            try {
                imageJ2000 = OpenJPEG.EncodeFromImage (bitmap, false);
            } catch (Exception) {
                MainConsole.Instance.Error (
                    "[Vector rendering]: OpenJpeg Encode Failed.  Empty byte data returned!");
            }

            m_textureManager.ReturnData (id, imageJ2000);
        }

        int parseIntParam (string strInt)
        {
            bool retVal;
            if (bool.TryParse (strInt, out retVal))
                return -1;
            int parsed;
            try {
                parsed = Convert.ToInt32 (strInt);
            } catch (Exception) {
                //Ckrinke: Add a WriteLine to remove the warning about 'e' defined but not used
                // MainConsole.Instance.Debug("Problem with Draw. Please verify parameters." + e.ToString());
                parsed = -1;
            }

            return parsed;
        }

        /*
                void CairoDraw(string data, System.Drawing.Graphics graph)
                {
                    using (Win32Surface draw = new Win32Surface(graph.GetHdc()))
                    {
                        Context contex = new Context(draw);

                        contex.Antialias = Antialias.None;    //fastest method but low quality
                        contex.LineWidth = 7;
                        char[] lineDelimiter = { ';' };
                        char[] partsDelimiter = { ',' };
                        string[] lines = data.Split(lineDelimiter);

                        foreach (string line in lines)
                        {
                            string nextLine = line.Trim();

                            if (nextLine.StartsWith("MoveTO"))
                            {
                                float x = 0;
                                float y = 0;
                                GetParams(partsDelimiter, ref nextLine, ref x, ref y);
                                contex.MoveTo(x, y);
                            }
                            else if (nextLine.StartsWith("LineTo"))
                            {
                                float x = 0;
                                float y = 0;
                                GetParams(partsDelimiter, ref nextLine, ref x, ref y);
                                contex.LineTo(x, y);
                                contex.Stroke();
                            }
                        }
                    }
                    graph.ReleaseHdc();
                }
        */

        void GDIDraw (string data, Graphics graph, char dataDelim)
        {
            Point startPoint = new Point (0, 0);
            Point endPoint = new Point (0, 0);
            Pen drawPen = new Pen (Color.Black, 7);
            string fontName = m_fontName;
            float fontSize = 14;
            Font myFont = new Font (fontName, fontSize);
            SolidBrush myBrush = new SolidBrush (Color.Black);

            char [] lineDelimiter = { dataDelim };
            char [] partsDelimiter = { ',' };
            string [] lines = data.Split (lineDelimiter);

            foreach (string line in lines) {
                string nextLine = line.Trim ();
                //replace with switch, or even better, do some proper parsing
                if (nextLine.StartsWith ("MoveTo", StringComparison.Ordinal)) {
                    float x = 0;
                    float y = 0;
                    GetParams (partsDelimiter, ref nextLine, 6, ref x, ref y);
                    startPoint.X = (int)x;
                    startPoint.Y = (int)y;
                } else if (nextLine.StartsWith ("LineTo", StringComparison.Ordinal)) {
                    float x = 0;
                    float y = 0;
                    GetParams (partsDelimiter, ref nextLine, 6, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    graph.DrawLine (drawPen, startPoint, endPoint);
                    startPoint.X = endPoint.X;
                    startPoint.Y = endPoint.Y;
                } else if (nextLine.StartsWith ("Text", StringComparison.Ordinal)) {
                    nextLine = nextLine.Remove (0, 4);
                    nextLine = nextLine.Trim ();
                    myFont = new Font (myFont, FontStyle.Bold);
                    graph.DrawString (nextLine, myFont, myBrush, startPoint);
                } else if (nextLine.StartsWith ("Image", StringComparison.Ordinal)) {
                    float x = 0;
                    float y = 0;
                    GetParams (partsDelimiter, ref nextLine, 5, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    Image image = ImageHttpRequest (nextLine);
                    if (image != null) {
                        graph.DrawImage (image, startPoint.X, startPoint.Y, x, y);
                    } else {
                        graph.DrawString ("URL couldn't be resolved or is", new Font (m_fontName, 6),
                                         myBrush, startPoint);
                        graph.DrawString ("not an image. Please check URL.", new Font (m_fontName, 6),
                                         myBrush, new Point (startPoint.X, 12 + startPoint.Y));
                        graph.DrawRectangle (drawPen, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                    }
                    startPoint.X += endPoint.X;
                    startPoint.Y += endPoint.Y;
                } else if (nextLine.StartsWith ("Rectangle", StringComparison.Ordinal)) {
                    float x = 0;
                    float y = 0;
                    GetParams (partsDelimiter, ref nextLine, 9, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    graph.DrawRectangle (drawPen, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                    startPoint.X += endPoint.X;
                    startPoint.Y += endPoint.Y;
                } else if (nextLine.StartsWith ("FillRectangle", StringComparison.Ordinal)) {
                    float x = 0;
                    float y = 0;
                    GetParams (partsDelimiter, ref nextLine, 13, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    graph.FillRectangle (myBrush, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                    startPoint.X += endPoint.X;
                    startPoint.Y += endPoint.Y;
                } else if (nextLine.StartsWith ("FillPolygon", StringComparison.Ordinal)) {
                    PointF [] points = null;
                    GetParams (partsDelimiter, ref nextLine, 11, ref points);
                    graph.FillPolygon (myBrush, points);
                } else if (nextLine.StartsWith ("Polygon", StringComparison.Ordinal)) {
                    PointF [] points = null;
                    GetParams (partsDelimiter, ref nextLine, 7, ref points);
                    graph.DrawPolygon (drawPen, points);
                } else if (nextLine.StartsWith ("Ellipse", StringComparison.Ordinal)) {
                    float x = 0;
                    float y = 0;
                    GetParams (partsDelimiter, ref nextLine, 7, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    graph.DrawEllipse (drawPen, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                    startPoint.X += endPoint.X;
                    startPoint.Y += endPoint.Y;
                } else if (nextLine.StartsWith ("FontSize", StringComparison.Ordinal)) {
                    nextLine = nextLine.Remove (0, 8);
                    nextLine = nextLine.Trim ();
                    fontSize = Convert.ToSingle (nextLine, CultureInfo.InvariantCulture);
                    myFont = new Font (fontName, fontSize);
                } else if (nextLine.StartsWith ("FontProp", StringComparison.Ordinal)) {
                    nextLine = nextLine.Remove (0, 8);
                    nextLine = nextLine.Trim ();

                    string [] fprops = nextLine.Split (partsDelimiter);
                    foreach (string prop in fprops) {
                        switch (prop) {
                        case "B":
                            if (!(myFont.Bold))
                                myFont = new Font (myFont, myFont.Style | FontStyle.Bold);
                            break;
                        case "I":
                            if (!(myFont.Italic))
                                myFont = new Font (myFont, myFont.Style | FontStyle.Italic);
                            break;
                        case "U":
                            if (!(myFont.Underline))
                                myFont = new Font (myFont, myFont.Style | FontStyle.Underline);
                            break;
                        case "S":
                            if (!(myFont.Strikeout))
                                myFont = new Font (myFont, myFont.Style | FontStyle.Strikeout);
                            break;
                        case "R":
                            myFont = new Font (myFont, FontStyle.Regular);
                            break;
                        }
                    }
                } else if (nextLine.StartsWith ("FontName", StringComparison.Ordinal)) {
                    nextLine = nextLine.Remove (0, 8);
                    fontName = nextLine.Trim ();
                    myFont = new Font (fontName, fontSize);
                } else if (nextLine.StartsWith ("PenSize", StringComparison.Ordinal)) {
                    nextLine = nextLine.Remove (0, 7);
                    nextLine = nextLine.Trim ();
                    float size = Convert.ToSingle (nextLine, CultureInfo.InvariantCulture);
                    drawPen.Width = size;
                } else if (nextLine.StartsWith ("PenCap", StringComparison.Ordinal)) {
                    bool start = true, end = true;
                    nextLine = nextLine.Remove (0, 6);
                    nextLine = nextLine.Trim ();
                    string [] cap = nextLine.Split (partsDelimiter);
                    if (cap [0].ToLower () == "start")
                        end = false;
                    else if (cap [0].ToLower () == "end")
                        start = false;
                    else if (cap [0].ToLower () != "both")
                        return;
                    string type = cap [1].ToLower ();

                    if (end) {
                        switch (type) {
                        case "arrow":
                            drawPen.EndCap = LineCap.ArrowAnchor;
                            break;
                        case "round":
                            drawPen.EndCap = LineCap.RoundAnchor;
                            break;
                        case "diamond":
                            drawPen.EndCap = LineCap.DiamondAnchor;
                            break;
                        case "flat":
                            drawPen.EndCap = LineCap.Flat;
                            break;
                        }
                    }
                    if (start) {
                        switch (type) {
                        case "arrow":
                            drawPen.StartCap = LineCap.ArrowAnchor;
                            break;
                        case "round":
                            drawPen.StartCap = LineCap.RoundAnchor;
                            break;
                        case "diamond":
                            drawPen.StartCap = LineCap.DiamondAnchor;
                            break;
                        case "flat":
                            drawPen.StartCap = LineCap.Flat;
                            break;
                        }
                    }
                } else if (nextLine.StartsWith ("PenColour", StringComparison.Ordinal) || nextLine.StartsWith ("PenColor", StringComparison.Ordinal)) {
                    nextLine = nextLine.Remove (0, 9);
                    nextLine = nextLine.Trim ();
                    int hex = 0;

                    Color newColour;
                    newColour = int.TryParse (nextLine, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex)
                                    ? Color.FromArgb (hex)
                                    : Color.FromName (nextLine);

                    myBrush.Color = newColour;
                    drawPen.Color = newColour;
                }
            }
            myFont.Dispose ();
            drawPen.Dispose ();
            myBrush.Dispose ();
        }

        static void GetParams (char [] partsDelimiter, ref string line, int startLength, ref float x, ref float y)
        {
            line = line.Remove (0, startLength);
            string [] parts = line.Split (partsDelimiter);
            if (parts.Length == 2) {
                string xVal = parts [0].Trim ();
                string yVal = parts [1].Trim ();
                x = Convert.ToSingle (xVal, CultureInfo.InvariantCulture);
                y = Convert.ToSingle (yVal, CultureInfo.InvariantCulture);
            } else if (parts.Length > 2) {
                string xVal = parts [0].Trim ();
                string yVal = parts [1].Trim ();
                x = Convert.ToSingle (xVal, CultureInfo.InvariantCulture);
                y = Convert.ToSingle (yVal, CultureInfo.InvariantCulture);

                line = "";
                for (int i = 2; i < parts.Length; i++) {
                    line = line + parts [i].Trim ();
                    line = line + " ";
                }
            }
        }

        static void GetParams (char [] partsDelimiter, ref string line, int startLength, ref PointF [] points)
        {
            line = line.Remove (0, startLength);
            string [] parts = line.Split (partsDelimiter);
            if (parts.Length > 1 && parts.Length % 2 == 0) {
                points = new PointF [parts.Length / 2];
                for (int i = 0; i < parts.Length; i = i + 2) {
                    string xVal = parts [i].Trim ();
                    string yVal = parts [i + 1].Trim ();
                    float x = Convert.ToSingle (xVal, CultureInfo.InvariantCulture);
                    float y = Convert.ToSingle (yVal, CultureInfo.InvariantCulture);
                    PointF point = new PointF (x, y);
                    points [i / 2] = point;
                }
            }
        }

        Bitmap ImageHttpRequest (string url)
        {
            try {
                WebRequest request = WebRequest.Create (url);
                //Ckrinke: Comment out for now as 'str' is unused. Bring it back into play later when it is used.
                //Ckrinke            Stream str = null;
                HttpWebResponse response = (HttpWebResponse)(request).GetResponse ();
                var statusCode = response.StatusCode;
                response.Dispose ();
                if (statusCode == HttpStatusCode.OK) {
                    Bitmap image = new Bitmap (response.GetResponseStream ());
                    return image;
                }

            } catch {
            }
            return null;
        }
    }
}
