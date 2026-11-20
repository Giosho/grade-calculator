using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GradeCalc
{
    // страницата на тетрадката: каре + червено поле; скролира заедно със съдържанието
    class GridPanel : Panel
    {
        public GridPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Paper);
            int cell = Theme.S(22);
            int oy = AutoScrollPosition.Y;
            using (Pen p = new Pen(Theme.Grid, 1))
            {
                int startY = ((oy % cell) + cell) % cell;
                for (int y = startY; y < ClientSize.Height; y += cell) g.DrawLine(p, 0, y, ClientSize.Width, y);
                for (int x = 0; x < ClientSize.Width; x += cell) g.DrawLine(p, x, 0, x, ClientSize.Height);
            }
            using (Pen m = new Pen(Theme.MarginLine, 2))
                g.DrawLine(m, Theme.S(64), 0, Theme.S(64), ClientSize.Height);
        }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; } // WS_EX_COMPOSITED
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x115 || m.Msg == 0x20A) Invalidate(true);
        }
    }

    abstract class PaperControl : Control
    {
        protected PaperControl()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }
    }

    class ThemedLabel : Label
    {
        public bool Strong;
        public ThemedLabel()
        {
            AutoSize = true;
            BackColor = Color.Transparent;
            Font = Theme.Ui(10.5f, FontStyle.Regular);
            ForeColor = Theme.Pencil;
        }
        public void ApplyTheme() { ForeColor = Strong ? Theme.Ink : Theme.Pencil; }
    }

    class TitleBlock : PaperControl
    {
        public string Title = "", Sub = "";
        public TitleBlock(string title, string sub) { Title = title; Sub = sub; Height = Theme.S(104); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Font f = Theme.Hand(26f, FontStyle.Regular))
            using (Font s = Theme.Ui(10.5f, FontStyle.Regular))
            using (Brush ink = new SolidBrush(Theme.Ink))
            using (Brush pen = new SolidBrush(Theme.Pencil))
            {
                g.DrawString(Title, f, ink, 0, 0);
                float w = g.MeasureString(Title, f).Width;
                Theme.Underline(g, 2, w, Theme.S(54), Theme.Ink, 2.2f, 3);
                Theme.Underline(g, 8, Math.Max(20, w - 14), Theme.S(60), Theme.Ink, 1.4f, 7);
                g.DrawString(Sub, s, pen, 2, Theme.S(72));
            }
        }
    }

    class SectionTitle : PaperControl
    {
        public string Text2 = "";
        public SectionTitle(string t) { Text2 = t; Height = Theme.S(46); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Font f = Theme.Hand(15f, FontStyle.Regular))
            using (Brush ink = new SolidBrush(Theme.Ink))
            {
                g.DrawString(Text2, f, ink, 0, Theme.S(2));
                float w = g.MeasureString(Text2, f).Width;
                Theme.Underline(g, 2, w, Theme.S(38), Theme.Red, 1.8f, 5);
            }
        }
    }

    // кратка бележка, когато няма данни
    class EmptyNote : PaperControl
    {
        public string Message = "";
        public EmptyNote() { Height = Theme.S(80); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (Font f = Theme.Hand(13f, FontStyle.Regular))
            using (Brush b = new SolidBrush(Theme.Pencil))
                e.Graphics.DrawString(Message, f, b, new RectangleF(0, Theme.S(8), Width, Height));
        }
    }

    // поле за въвеждане като празно място за попълване с подчертаване
    class InkField : Control
    {
        public TextBox Box = new TextBox();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        public InkField(string hint)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Theme.Ui(13f, FontStyle.Regular);
            Box.BackColor = Theme.Field;
            Box.ForeColor = Theme.Ink;
            Controls.Add(Box);
            Height = Box.PreferredHeight + Theme.S(16);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            if (hint != null) Box.HandleCreated += delegate { SendMessage(Box.Handle, 0x1501, (IntPtr)1, hint); };
        }
        public void ApplyTheme()
        {
            Box.BackColor = Theme.Field;
            Box.ForeColor = Theme.Ink;
            Invalidate();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Box.SetBounds(Theme.S(8), Theme.S(7), Math.Max(10, Width - Theme.S(16)), Box.PreferredHeight);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Field);
            bool f = Box.Focused;
            using (Pen p = new Pen(f ? Theme.Red : Theme.Ink, f ? 3 : 2))
                g.DrawLine(p, 0, Height - 2, Width, Height - 2);
        }
    }

    // малък бутон: цел, действие или връзка
    class Chip : Control
    {
        public bool Selected, Primary, LinkStyle, Danger;
        bool hover;
        public Chip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
        }
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            using (Font f = Theme.Ui(11f, FontStyle.Regular))
            {
                Size sz = TextRenderer.MeasureText(Text, f);
                Size = new Size(sz.Width + Theme.S(Primary ? 28 : 20), Theme.S(36));
            }
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            RectangleF r = new RectangleF(1, 3, Width - 3, Height - 6);
            Color fore = Theme.Ink;
            FontStyle fs = FontStyle.Regular;
            if (LinkStyle)
            {
                fore = hover ? Theme.Red : (Danger ? Theme.Red : Theme.Pencil);
            }
            else if (Primary)
            {
                using (GraphicsPath p = Theme.Round(r, Theme.S(4)))
                using (SolidBrush b = new SolidBrush(hover ? Theme.Mix(Theme.Ink, Theme.Dark ? Color.White : Color.Black, 0.15) : Theme.Ink))
                    g.FillPath(b, p);
                fore = Theme.OnInk; fs = FontStyle.Bold;
            }
            else if (Selected)
            {
                using (GraphicsPath p = Theme.Round(r, Theme.S(3)))
                using (SolidBrush b = new SolidBrush(Theme.Highlight))
                    g.FillPath(b, p);
                fore = Theme.OnHighlight; fs = FontStyle.Bold;
            }
            else
            {
                using (GraphicsPath p = Theme.Round(r, Theme.S(4)))
                using (Pen pen = new Pen(hover ? Theme.Ink : Theme.RuleLine, 1.5f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(220, Theme.Field)))
                {
                    g.FillPath(b, p);
                    g.DrawPath(pen, p);
                }
            }
            using (Font f = Theme.Ui(11f, fs))
            using (Brush tb = new SolidBrush(fore))
            {
                StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Text, f, tb, new RectangleF(0, 0, Width, Height), sf);
                if (LinkStyle && hover)
                {
                    SizeF m = g.MeasureString(Text, f);
                    using (Pen u = new Pen(Theme.Red, 1)) g.DrawLine(u, (Width - m.Width) / 2 + 3, Height / 2 + m.Height / 2 - 1, (Width + m.Width) / 2 - 3, Height / 2 + m.Height / 2 - 1);
                }
            }
            if (Focused && !LinkStyle)
                using (Pen fp = new Pen(Theme.Red, 1) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(fp, 1, 1, Width - 3, Height - 3);
        }
    }

    // раздели като в тетрадка
    class TabStrip : Control
    {
        public string[] Tabs = new string[0];
        public int Selected;
        public event EventHandler SelectedChanged;
        int hot = -1;

        public TabStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
            Height = Theme.S(50);
            Dock = DockStyle.Top;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        List<Rectangle> Rects()
        {
            List<Rectangle> l = new List<Rectangle>();
            int x = Theme.S(76);
            using (Font f = Theme.Ui(11.5f, FontStyle.Bold))
                for (int i = 0; i < Tabs.Length; i++)
                {
                    int w = TextRenderer.MeasureText(Tabs[i], f).Width + Theme.S(34);
                    l.Add(new Rectangle(x, Height - Theme.S(40) + (i == Selected ? 0 : Theme.S(5)), w, Theme.S(40) - (i == Selected ? 0 : Theme.S(5))));
                    x += w + Theme.S(4);
                }
            return l;
        }

        int Hit(Point p)
        {
            List<Rectangle> r = Rects();
            for (int i = 0; i < r.Count; i++) if (r[i].Contains(p)) return i;
            return -1;
        }

        public void Select(int i)
        {
            if (i < 0 || i >= Tabs.Length || i == Selected) return;
            Selected = i;
            Invalidate();
            if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); int h = Hit(e.Location); if (h >= 0) Select(h); }
        protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right) Select(Math.Min(Tabs.Length - 1, Selected + 1));
            else if (e.KeyCode == Keys.Left) Select(Math.Max(0, Selected - 1));
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Paper);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Pen m = new Pen(Theme.MarginLine, 2))
                g.DrawLine(m, Theme.S(64), 0, Theme.S(64), Height);
            using (Pen bl = new Pen(Theme.RuleLine, 1.5f))
                g.DrawLine(bl, 0, Height - 1, Width, Height - 1);

            List<Rectangle> rects = Rects();
            using (Font fb = Theme.Ui(11.5f, FontStyle.Bold))
            using (Font fr = Theme.Ui(11.5f, FontStyle.Regular))
                for (int i = 0; i < rects.Count; i++)
                {
                    Rectangle r = rects[i];
                    bool sel = i == Selected;
                    float rad = Theme.S(9);
                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddLine(r.X, r.Bottom, r.X, r.Y + rad);
                        p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
                        p.AddLine(r.X + rad, r.Y, r.Right - rad, r.Y);
                        p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
                        p.AddLine(r.Right, r.Y + rad, r.Right, r.Bottom);
                        using (SolidBrush b = new SolidBrush(sel ? Theme.Paper : Theme.Field))
                            g.FillPath(b, p);
                        using (Pen pen = new Pen(Theme.RuleLine, 1.5f))
                            g.DrawPath(pen, p);
                    }
                    if (sel)
                        using (Pen cover = new Pen(Theme.Paper, 3))
                            g.DrawLine(cover, r.X + 2, Height - 1, r.Right - 2, Height - 1);
                    StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    Color tc = sel ? Theme.Ink : (hot == i ? Theme.Ink : Theme.Pencil);
                    using (Brush tb = new SolidBrush(tc))
                        g.DrawString(Tabs[i], sel ? fb : fr, tb, new RectangleF(r.X, r.Y + 1, r.Width, r.Height), sf);
                    if (sel)
                    {
                        float tw = g.MeasureString(Tabs[i], fb).Width;
                        Theme.Underline(g, r.X + (r.Width - tw) / 2 + 2, r.X + (r.Width + tw) / 2 - 2, r.Bottom - Theme.S(7), Theme.Red, 1.8f, 9 + i);
                    }
                }
            if (Focused)
                using (Pen fp = new Pen(Theme.Red, 1) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(fp, Theme.S(70), 2, Width - Theme.S(78), Height - 5);
        }
    }

    // резюме: обиколен успех + бележка на учителя + линийка
    class SummaryBlock : PaperControl
    {
        public bool Valid;
        public double Cur, Target;
        public int N;
        public string Message = "";
        public Color MsgColor = Theme.Pencil;
        public bool MsgIsPencil;   // при смяна на тема цветът се взима наново
        public bool MsgIsRed, MsgIsGreen;

        public SummaryBlock() { Sync(); }

        public void SetMsgColor(Color c)
        {
            MsgIsPencil = c == Theme.Pencil; MsgIsRed = c == Theme.Red; MsgIsGreen = c == Theme.Green;
            MsgColor = c;
        }
        public void ApplyTheme()
        {
            if (MsgIsPencil) MsgColor = Theme.Pencil; else if (MsgIsRed) MsgColor = Theme.Red; else if (MsgIsGreen) MsgColor = Theme.Green;
            Invalidate();
        }

        public void Sync()
        {
            Height = Valid ? Theme.S(244) : Theme.S(84);
            Invalidate();
        }

        static float Pos(double v) { return (float)((Math.Max(2, Math.Min(6, v)) - 2) / 4.0); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (!Valid)
            {
                using (Font f = Theme.Hand(13f, FontStyle.Regular))
                using (Brush b = new SolidBrush(MsgColor))
                    g.DrawString(Message, f, b, new RectangleF(0, Theme.S(14), Width, Height - Theme.S(14)));
                return;
            }

            string num = Cur.ToString("0.00", CultureInfo.InvariantCulture);
            using (Font big = Theme.Hand(40f, FontStyle.Regular))
            using (Font note = Theme.Hand(17f, FontStyle.Regular))
            using (Font small = Theme.Ui(10.5f, FontStyle.Regular))
            using (Brush ink = new SolidBrush(Theme.Ink))
            using (Brush pen = new SolidBrush(Theme.Pencil))
            using (Brush msg = new SolidBrush(MsgColor))
            {
                SizeF ns = g.MeasureString(num, big);
                float cx = Theme.S(10), cy = Theme.S(8);
                g.DrawString(num, big, ink, cx + Theme.S(16), cy + Theme.S(6));
                RectangleF circ = new RectangleF(cx, cy, ns.Width + Theme.S(24), ns.Height + Theme.S(10));
                Theme.Scribble(g, circ, Theme.Red, 2.6f, 11);

                float tx = circ.Right + Theme.S(22);
                float tw = Width - tx;
                if (tw > Theme.S(150))
                {
                    g.DrawString(Message, note, msg, new RectangleF(tx, cy + Theme.S(4), tw, Theme.S(70)));
                    g.DrawString(N + (N == 1 ? " оценка" : " оценки") + " в момента", small, pen, tx, cy + Theme.S(78));
                    g.DrawString("Цел: " + Target.ToString("0.00", CultureInfo.InvariantCulture), small, pen, tx, cy + Theme.S(98));
                }

                float x0 = Theme.S(4), x1 = Width - Theme.S(4);
                float top = Height - Theme.S(86), rh = Theme.S(34);
                RectangleF ruler = new RectangleF(x0, top, x1 - x0, rh);
                using (SolidBrush rb = new SolidBrush(Theme.RulerFill))
                using (Pen rp = new Pen(Theme.RulerEdge, 1.2f))
                {
                    g.FillRectangle(rb, ruler);
                    g.DrawRectangle(rp, ruler.X, ruler.Y, ruler.Width, ruler.Height);
                }
                using (Pen tk = new Pen(Theme.RulerTick, 1))
                using (Font tf = Theme.Ui(8.5f, FontStyle.Regular))
                using (Brush tb = new SolidBrush(Theme.RulerTick))
                {
                    for (int i = 0; i <= 40; i++)
                    {
                        float x = x0 + (x1 - x0) * i / 40f;
                        float len = i % 10 == 0 ? Theme.S(14) : (i % 5 == 0 ? Theme.S(10) : Theme.S(6));
                        g.DrawLine(tk, x, top, x, top + len);
                        if (i % 10 == 0)
                        {
                            StringFormat c = new StringFormat(); c.Alignment = StringAlignment.Center;
                            g.DrawString((2 + i / 10).ToString(), tf, tb, x, top + Theme.S(15), c);
                        }
                    }
                }
                float px = x0 + Pos(Cur) * (x1 - x0);
                PointF[] tri = new PointF[] { new PointF(px - Theme.S(8), top - Theme.S(16)), new PointF(px + Theme.S(8), top - Theme.S(16)), new PointF(px, top - 1) };
                g.FillPolygon(ink, tri);
                float gx = x0 + Pos(Target) * (x1 - x0);
                using (Pen rp = new Pen(Theme.Red, 2.4f))
                    g.DrawLine(rp, gx, top - Theme.S(6), gx, top + rh + Theme.S(4));
                using (Font hf = Theme.Hand(11f, FontStyle.Regular))
                using (Brush rb = new SolidBrush(Theme.Red))
                {
                    StringFormat c = new StringFormat(); c.Alignment = StringAlignment.Center;
                    float lx = Math.Max(Theme.S(24), Math.Min(Width - Theme.S(24), gx));
                    g.DrawString("цел", hf, rb, lx, top + rh + Theme.S(4), c);
                }
            }
        }
    }

    // ред от резултати: обиколена оценка + текст върху линия на тетрадка
    class ResultRow : PaperControl
    {
        public string Big = "", Title = "", Sub = "";
        public bool Disabled;
        public int Seed = 1;

        public ResultRow() { Height = Theme.S(70); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen ln = new Pen(Theme.RuleLine, 1.5f))
                g.DrawLine(ln, 0, Height - 2, Width, Height - 2);

            int d = Theme.S(46);
            RectangleF circ = new RectangleF(Theme.S(6), (Height - d) / 2f - Theme.S(2), d, d);
            using (Font bf = Theme.Hand(20f, FontStyle.Regular))
            using (Brush bb = new SolidBrush(Disabled ? Theme.Pencil : Theme.Ink))
            {
                StringFormat c = new StringFormat(); c.Alignment = StringAlignment.Center; c.LineAlignment = StringAlignment.Center;
                g.DrawString(Big, bf, bb, circ, c);
            }
            if (!Disabled) Theme.Scribble(g, circ, Theme.Red, 2.4f, Seed);

            float tx = circ.Right + Theme.S(18);
            using (Font tf = Theme.Hand(15f, Disabled ? FontStyle.Strikeout : FontStyle.Regular))
            using (Font sf = Theme.Ui(10f, FontStyle.Regular))
            using (Brush tb = new SolidBrush(Disabled ? Theme.Pencil : Theme.Ink))
            using (Brush sb = new SolidBrush(Theme.Pencil))
            {
                StringFormat fmt = new StringFormat(); fmt.Trimming = StringTrimming.EllipsisCharacter; fmt.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(Title, tf, tb, new RectangleF(tx, Theme.S(8), Width - tx, Theme.S(32)), fmt);
                g.DrawString(Sub, sf, sb, new RectangleF(tx, Theme.S(40), Width - tx, Theme.S(22)), fmt);
            }
        }
    }

    // подрежда редовете на колони според ширината
    class ResultsPanel : Panel
    {
        public int Gap { get { return Theme.S(18); } }
        public ResultsPanel() { DoubleBuffered = true; BackColor = Color.Transparent; }

        public void Relayout()
        {
            int w = ClientSize.Width;
            if (w <= 0) return;
            int cols = w >= Theme.S(640) ? 2 : 1;
            int cw = (w - Gap * (cols - 1)) / cols;
            int i = 0, y = 0, rowH = 0;
            foreach (Control c in Controls)
            {
                int col = i % cols;
                if (col == 0 && i > 0) { y += rowH; rowH = 0; }
                c.SetBounds(col * (cw + Gap), y, cw, c.Height);
                rowH = Math.Max(rowH, c.Height);
                i++;
            }
            int h = Controls.Count == 0 ? 0 : y + rowH;
            if (Height != h) Height = h;
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }
    }
}
