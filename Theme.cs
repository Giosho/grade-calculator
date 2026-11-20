using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace GradeCalc
{
    // Визия: ученическа тетрадка на каре. Светла (хартия) и тъмна (нощна тетрадка) тема.
    // Цветът на мастилото се взима от акцентния цвят на Windows.
    static class Theme
    {
        public static float Scale = 1f;
        public static int S(int v) { return (int)Math.Round(v * Scale); }

        public static bool Dark;
        public static Color Paper, Grid, RuleLine, MarginLine, Ink, OnInk, Red, Pencil,
            Highlight, OnHighlight, Green, Field, RulerFill, RulerEdge, RulerTick, Bg, Border, Accent;

        static Theme() { Apply(false, null); }

        // ---------- откриване на темата на компютъра ----------
        public static bool SystemDark()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                return v is int && (int)v == 0;
            }
            catch { return false; }
        }

        public static Color? SystemAccent()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null);
                if (v is int)
                {
                    uint u = unchecked((uint)(int)v); // 0xAABBGGRR
                    return Color.FromArgb((int)(u & 0xFF), (int)((u >> 8) & 0xFF), (int)((u >> 16) & 0xFF));
                }
                v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "ColorizationColor", null);
                if (v is int)
                {
                    uint u = unchecked((uint)(int)v); // 0xAARRGGBB
                    return Color.FromArgb((int)((u >> 16) & 0xFF), (int)((u >> 8) & 0xFF), (int)(u & 0xFF));
                }
            }
            catch { }
            return null;
        }

        public static double Lum(Color c) { return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0; }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        // прави мастилото четливо върху хартията
        static Color Fit(Color c, bool dark)
        {
            for (int i = 0; i < 30; i++)
            {
                double l = Lum(c);
                if (!dark && l > 0.30) c = Mix(c, Color.Black, 0.12);
                else if (dark && l < 0.58) c = Mix(c, Color.White, 0.12);
                else break;
            }
            return c;
        }

        public static void Apply(bool dark, Color? accent)
        {
            Dark = dark;
            if (!dark)
            {
                Paper = Color.FromArgb(250, 252, 255);
                Grid = Color.FromArgb(212, 224, 241);
                RuleLine = Color.FromArgb(178, 196, 225);
                MarginLine = Color.FromArgb(238, 150, 150);
                Red = Color.FromArgb(206, 38, 48);
                Pencil = Color.FromArgb(104, 108, 118);
                Highlight = Color.FromArgb(255, 233, 102);
                OnHighlight = Color.FromArgb(40, 36, 10);
                Green = Color.FromArgb(24, 120, 68);
                Field = Color.White;
                RulerFill = Color.FromArgb(246, 236, 186);
                RulerEdge = Color.FromArgb(205, 188, 120);
                RulerTick = Color.FromArgb(120, 100, 40);
                Bg = Color.FromArgb(243, 246, 252);
            }
            else
            {
                Paper = Color.FromArgb(23, 27, 39);
                Grid = Color.FromArgb(37, 44, 63);
                RuleLine = Color.FromArgb(70, 82, 112);
                MarginLine = Color.FromArgb(150, 72, 84);
                Red = Color.FromArgb(255, 112, 112);
                Pencil = Color.FromArgb(160, 168, 188);
                Highlight = Color.FromArgb(222, 186, 58);
                OnHighlight = Color.FromArgb(24, 20, 0);
                Green = Color.FromArgb(94, 212, 146);
                Field = Color.FromArgb(33, 38, 54);
                RulerFill = Color.FromArgb(86, 76, 42);
                RulerEdge = Color.FromArgb(128, 110, 60);
                RulerTick = Color.FromArgb(205, 185, 115);
                Bg = Color.FromArgb(30, 35, 50);
            }
            Color baseInk = accent.HasValue ? accent.Value : (dark ? Color.FromArgb(140, 170, 255) : Color.FromArgb(28, 50, 138));
            Ink = Fit(baseInk, dark);
            OnInk = Lum(Ink) > 0.5 ? Color.FromArgb(18, 22, 34) : Color.White;
            Border = RuleLine;
            Accent = Ink;
        }

        // ---------- шрифтове ----------
        static bool Has(string n)
        {
            foreach (FontFamily f in FontFamily.Families) if (f.Name == n) return true;
            return false;
        }
        static string handName, uiName;
        public static Font Hand(float size, FontStyle st)
        {
            if (handName == null) handName = Has("Segoe Script") ? "Segoe Script" : (Has("Segoe Print") ? "Segoe Print" : "Segoe UI");
            return new Font(handName, size, st);
        }
        public static Font Ui(float size, FontStyle st)
        {
            if (uiName == null) uiName = Has("Bahnschrift") ? "Bahnschrift" : "Segoe UI";
            return new Font(uiName, size, st);
        }

        // ---------- рисуване "на ръка" ----------
        public static void Scribble(Graphics g, RectangleF r, Color c, float w, int seed)
        {
            Random rnd = new Random(seed);
            int n = 44;
            PointF[] pts = new PointF[n + 1];
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            for (int i = 0; i <= n; i++)
            {
                double a = -0.6 + i * (2 * Math.PI + 1.0) / n;
                double k = 1.0 + 0.03 * Math.Sin(i * 0.8 + seed) + 0.07 * i / n;
                float x = cx + (float)(Math.Cos(a) * r.Width / 2 * k) + (float)(rnd.NextDouble() - 0.5) * 1.4f;
                float y = cy + (float)(Math.Sin(a) * r.Height / 2 * k) + (float)(rnd.NextDouble() - 0.5) * 1.4f;
                pts[i] = new PointF(x, y);
            }
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(cx, cy); g.RotateTransform(-5f); g.TranslateTransform(-cx, -cy);
            using (Pen p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawCurve(p, pts, 0.5f);
            }
            g.Restore(st);
        }

        public static void Underline(Graphics g, float x0, float x1, float y, Color c, float w, int seed)
        {
            Random rnd = new Random(seed);
            List<PointF> pts = new List<PointF>();
            for (float x = x0; x < x1; x += 18f) pts.Add(new PointF(x, y + (float)(rnd.NextDouble() - 0.5) * 1.8f));
            pts.Add(new PointF(x1, y + (float)(rnd.NextDouble() - 0.5) * 1.8f));
            if (pts.Count < 2) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(c, w)) { p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; g.DrawCurve(p, pts.ToArray(), 0.4f); }
            g.SmoothingMode = old;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            float d = rad * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
