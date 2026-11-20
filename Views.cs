using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace GradeCalc
{
    // среден успех на един предмет
    class AvgBadge : PaperControl
    {
        public double Avg = double.NaN;
        public bool Bad;
        public AvgBadge() { Size = new Size(Theme.S(70), Theme.S(40)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            string t = Bad ? "?" : (double.IsNaN(Avg) ? "–" : GradeUtil.Fmt(Avg));
            Color c = Bad ? Theme.Red : (double.IsNaN(Avg) ? Theme.Pencil : Theme.Ink);
            using (Font f = Theme.Hand(15f, FontStyle.Regular))
            using (Brush b = new SolidBrush(c))
            {
                StringFormat sf = new StringFormat(); sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                g.DrawString(t, f, b, new RectangleF(0, 0, Width, Height), sf);
            }
        }
    }

    // общ успех, обиколен с червено
    class OverallBadge : PaperControl
    {
        public double Avg = double.NaN;
        public int Count;
        public OverallBadge() { Height = Theme.S(104); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (double.IsNaN(Avg))
            {
                using (Font f = Theme.Hand(13f, FontStyle.Regular))
                using (Brush b = new SolidBrush(Theme.Pencil))
                    g.DrawString("Добави предмет и напиши оценките му – общият успех се смята сам.", f, b, new RectangleF(0, Theme.S(20), Width, Height));
                return;
            }
            string num = GradeUtil.Fmt(Avg);
            using (Font big = Theme.Hand(34f, FontStyle.Regular))
            using (Font note = Theme.Hand(15f, FontStyle.Regular))
            using (Font small = Theme.Ui(10.5f, FontStyle.Regular))
            using (Brush ink = new SolidBrush(Theme.Ink))
            using (Brush pen = new SolidBrush(Theme.Pencil))
            using (Brush red = new SolidBrush(Theme.Red))
            {
                SizeF ns = g.MeasureString(num, big);
                float cx = Theme.S(8), cy = Theme.S(6);
                g.DrawString(num, big, ink, cx + Theme.S(14), cy + Theme.S(5));
                RectangleF circ = new RectangleF(cx, cy, ns.Width + Theme.S(20), ns.Height + Theme.S(8));
                Theme.Scribble(g, circ, Theme.Red, 2.4f, 31);
                float tx = circ.Right + Theme.S(20);
                g.DrawString("Общ успех", note, red, tx, cy + Theme.S(4));
                int rounded = (int)Math.Floor(Avg + 0.5);
                if (rounded > 6) rounded = 6;
                g.DrawString("по " + Count + (Count == 1 ? " предмет" : " предмета") + ", закръглен " + rounded, small, pen, tx, cy + Theme.S(46));
            }
        }
    }

    // ред за един предмет: име, оценки, среден, действия
    class SubjectRow : TableLayoutPanel
    {
        public SubjectData Data;
        public InkField NameField, GradesField;
        AvgBadge badge = new AvgBadge();
        public event EventHandler Changed, Removed, ToCalc;

        public SubjectRow(SubjectData d)
        {
            Data = d;
            BackColor = Color.Transparent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Dock = DockStyle.Fill;
            ColumnCount = 3;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
            ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Margin = new Padding(0, 0, 0, Theme.S(6));

            NameField = new InkField("предмет");
            NameField.Dock = DockStyle.Fill;
            NameField.Margin = new Padding(0, 0, Theme.S(12), 0);
            NameField.Box.Text = d.Name;
            GradesField = new InkField("оценки, напр. 6 5 5");
            GradesField.Dock = DockStyle.Fill;
            GradesField.Margin = new Padding(0, 0, Theme.S(8), 0);
            GradesField.Box.Text = d.Grades;

            FlowLayoutPanel act = new FlowLayoutPanel();
            act.AutoSize = true; act.AutoSizeMode = AutoSizeMode.GrowAndShrink; act.WrapContents = false;
            act.BackColor = Color.Transparent; act.Margin = new Padding(0); act.Dock = DockStyle.Fill;
            badge.Margin = new Padding(0, 0, Theme.S(4), 0);
            Chip calc = new Chip(); calc.Text = "Смятай"; calc.Margin = new Padding(0, Theme.S(2), Theme.S(4), 0);
            Chip del = new Chip(); del.LinkStyle = true; del.Danger = true; del.Text = "×"; del.Margin = new Padding(0, Theme.S(2), 0, 0);
            ToolTip tip = new ToolTip();
            tip.SetToolTip(calc, "Прехвърли оценките в калкулатора");
            tip.SetToolTip(del, "Премахни предмета");
            calc.Click += delegate { if (ToCalc != null) ToCalc(this, EventArgs.Empty); };
            del.Click += delegate { if (Removed != null) Removed(this, EventArgs.Empty); };
            act.Controls.Add(badge); act.Controls.Add(calc); act.Controls.Add(del);

            Controls.Add(NameField); Controls.Add(GradesField); Controls.Add(act);
            NameField.Box.TextChanged += delegate { Sync(); };
            GradesField.Box.TextChanged += delegate { Sync(); };
            Refresh2();
        }

        void Refresh2()
        {
            List<double> l;
            bool ok = GradeUtil.TryParse(GradesField.Box.Text, out l);
            badge.Bad = !ok;
            badge.Avg = (ok && l.Count > 0) ? GradeUtil.Sum(l) / l.Count : double.NaN;
            badge.Invalidate();
        }

        void Sync()
        {
            Data.Name = NameField.Box.Text;
            Data.Grades = GradesField.Box.Text;
            Refresh2();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    // хоризонтални стълбове: среден по предмет, с червена линия за целта
    class BarsView : PaperControl
    {
        List<SubStat> items = new List<SubStat>();
        public double Goal = 5.5;

        public void SetData(List<SubStat> l, double goal)
        {
            items = new List<SubStat>(l);
            items.Sort(delegate (SubStat a, SubStat b) { return b.Avg.CompareTo(a.Avg); });
            Goal = goal;
            Height = items.Count == 0 ? 0 : Theme.S(30) + items.Count * Theme.S(34);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (items.Count == 0) return;
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rowH = Theme.S(34), top = Theme.S(28);
            int nameW = Math.Min(Theme.S(190), (int)(Width * 0.34));
            float x0 = nameW + Theme.S(10), x1 = Width - Theme.S(60);
            if (x1 - x0 < 40) return;
            float gx = x0 + (float)((Math.Max(2, Math.Min(6, Goal)) - 2) / 4.0) * (x1 - x0);

            using (Pen ax = new Pen(Theme.RuleLine, 1.5f))
                g.DrawLine(ax, x0, top - Theme.S(4), x0, top + items.Count * rowH);
            using (Pen gp = new Pen(Theme.Red, 2.2f))
                g.DrawLine(gp, gx, top - Theme.S(8), gx, top + items.Count * rowH);
            using (Font hf = Theme.Hand(11f, FontStyle.Regular))
            using (Brush rb = new SolidBrush(Theme.Red))
            {
                StringFormat c = new StringFormat(); c.Alignment = StringAlignment.Center;
                g.DrawString("цел " + GradeUtil.Fmt(Goal), hf, rb, Math.Min(Width - Theme.S(30), Math.Max(Theme.S(30), gx)), 0, c);
            }
            using (Font nf = Theme.Ui(10.5f, FontStyle.Regular))
            using (Font vf = Theme.Ui(10.5f, FontStyle.Bold))
            using (Brush nb = new SolidBrush(Theme.Ink))
            using (Brush vb = new SolidBrush(Theme.Pencil))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    SubStat s = items[i];
                    float y = top + i * rowH;
                    StringFormat sf = new StringFormat(); sf.Trimming = StringTrimming.EllipsisCharacter; sf.FormatFlags = StringFormatFlags.NoWrap; sf.LineAlignment = StringAlignment.Center;
                    string nm = string.IsNullOrEmpty(s.Name) ? "(без име)" : s.Name;
                    g.DrawString(nm, nf, nb, new RectangleF(0, y, nameW, rowH), sf);
                    float len = Math.Max(Theme.S(4), (float)((Math.Max(2, Math.Min(6, s.Avg)) - 2) / 4.0) * (x1 - x0));
                    RectangleF bar = new RectangleF(x0, y + Theme.S(8), len, Theme.S(18));
                    Color bc = s.Avg >= Goal - 1e-9 ? Theme.Green : Theme.Ink;
                    using (GraphicsPath p = Theme.Round(bar, Theme.S(3)))
                    using (SolidBrush fb = new SolidBrush(Color.FromArgb(215, bc)))
                        g.FillPath(fb, p);
                    StringFormat vs = new StringFormat(); vs.LineAlignment = StringAlignment.Center;
                    g.DrawString(GradeUtil.Fmt(s.Avg), vf, vb, new RectangleF(x0 + len + Theme.S(8), y, Theme.S(60), rowH), vs);
                }
            }
        }
    }

    // чертички като в тетрадка: колко оценки от всеки вид
    class TallyView : PaperControl
    {
        int[] counts = new int[5]; // 6,5,4,3,2
        public void SetData(List<SubStat> l)
        {
            counts = new int[5];
            foreach (SubStat s in l)
                foreach (double v in s.Grades)
                {
                    int r = (int)Math.Floor(v + 0.5);
                    if (r < 2) r = 2; if (r > 6) r = 6;
                    counts[6 - r]++;
                }
            Height = Theme.S(8) + 5 * Theme.S(46);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rowH = Theme.S(46);
            for (int i = 0; i < 5; i++)
            {
                int grade = 6 - i;
                float y = Theme.S(4) + i * rowH;
                using (Pen ln = new Pen(Theme.RuleLine, 1.5f))
                    g.DrawLine(ln, 0, y + rowH - 2, Width, y + rowH - 2);
                RectangleF circ = new RectangleF(Theme.S(4), y + Theme.S(3), Theme.S(36), Theme.S(36));
                using (Font f = Theme.Hand(15f, FontStyle.Regular))
                using (Brush b = new SolidBrush(Theme.Ink))
                {
                    StringFormat c = new StringFormat(); c.Alignment = StringAlignment.Center; c.LineAlignment = StringAlignment.Center;
                    g.DrawString(grade.ToString(), f, b, circ, c);
                }
                Theme.Scribble(g, circ, Theme.Red, 1.8f, 50 + grade);

                int n = counts[i];
                float tx = Theme.S(62);
                if (n == 0)
                {
                    using (Font f = Theme.Ui(10.5f, FontStyle.Regular))
                    using (Brush b = new SolidBrush(Theme.Pencil))
                        g.DrawString("няма", f, b, tx, y + Theme.S(12));
                    continue;
                }
                float avail = Width - tx - Theme.S(50);
                int groups = (n + 4) / 5;
                float gw = Theme.S(36);
                if (groups * gw <= avail)
                {
                    Random rnd = new Random(grade * 7);
                    using (Pen sp = new Pen(Theme.Ink, 2.2f))
                    {
                        sp.StartCap = LineCap.Round; sp.EndCap = LineCap.Round;
                        int left = n;
                        for (int gi = 0; gi < groups; gi++)
                        {
                            float gx = tx + gi * gw;
                            int k = Math.Min(5, left); left -= k;
                            int bars = Math.Min(4, k);
                            for (int b = 0; b < bars; b++)
                            {
                                float jx = (float)(rnd.NextDouble() - 0.5) * 1.6f;
                                g.DrawLine(sp, gx + b * Theme.S(7) + jx, y + Theme.S(8), gx + b * Theme.S(7) - jx, y + Theme.S(34));
                            }
                            if (k == 5)
                                g.DrawLine(sp, gx - Theme.S(4), y + Theme.S(30), gx + Theme.S(26), y + Theme.S(11));
                        }
                    }
                }
                using (Font f = Theme.Ui(11f, FontStyle.Bold))
                using (Brush b = new SolidBrush(Theme.Pencil))
                {
                    StringFormat r = new StringFormat(); r.Alignment = StringAlignment.Far;
                    g.DrawString(n.ToString(), f, b, new RectangleF(Width - Theme.S(48), y + Theme.S(12), Theme.S(46), Theme.S(24)), r);
                }
            }
        }
    }

    class NextStep
    {
        public string Name; public double Avg, Target; public int K;
    }

    // какво да поправиш първо: до закръгляне нагоре
    class NextStepsView : PaperControl
    {
        List<NextStep> items = new List<NextStep>();

        public static List<NextStep> Build(List<SubStat> l)
        {
            List<NextStep> r = new List<NextStep>();
            foreach (SubStat s in l)
            {
                if (s.N == 0) continue;
                double a = s.Avg;
                double t = Math.Floor(a - 0.5 + 1e-9) + 1.5;
                if (t > 5.5 + 1e-9) continue; // вече е шест
                int k = (int)Math.Ceiling((t * s.N - s.Sum) / (6 - t) - 1e-9);
                if (k < 1) k = 1;
                NextStep n = new NextStep(); n.Name = s.Name; n.Avg = a; n.Target = t; n.K = k;
                r.Add(n);
            }
            r.Sort(delegate (NextStep x, NextStep y)
            {
                int c = x.K.CompareTo(y.K);
                return c != 0 ? c : (y.Avg).CompareTo(x.Avg);
            });
            if (r.Count > 6) r.RemoveRange(6, r.Count - 6);
            return r;
        }

        public void SetData(List<NextStep> l)
        {
            items = l;
            Height = items.Count == 0 ? Theme.S(50) : items.Count * Theme.S(58);
            Invalidate();
        }

        static string SixWord(int k) { return k == 1 ? "шестица" : "шестици"; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            if (items.Count == 0)
            {
                using (Font f = Theme.Hand(13f, FontStyle.Regular))
                using (Brush b = new SolidBrush(Theme.Green))
                    g.DrawString("Всичко е на шест. Браво! ✔", f, b, 0, Theme.S(8));
                return;
            }
            int rowH = Theme.S(58);
            for (int i = 0; i < items.Count; i++)
            {
                NextStep n = items[i];
                float y = i * rowH;
                using (Pen ln = new Pen(Theme.RuleLine, 1.5f))
                    g.DrawLine(ln, 0, y + rowH - 2, Width, y + rowH - 2);
                string nm = string.IsNullOrEmpty(n.Name) ? "(без име)" : n.Name;
                int shown = (int)Math.Floor(n.Target + 0.5);
                using (Font nf = Theme.Ui(11.5f, FontStyle.Bold))
                using (Font sf = Theme.Ui(10f, FontStyle.Regular))
                using (Font hf = Theme.Hand(15f, FontStyle.Regular))
                using (Brush nb = new SolidBrush(Theme.Ink))
                using (Brush pb = new SolidBrush(Theme.Pencil))
                using (Brush rb = new SolidBrush(Theme.Red))
                {
                    StringFormat tr = new StringFormat(); tr.Trimming = StringTrimming.EllipsisCharacter; tr.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(nm, nf, nb, new RectangleF(Theme.S(4), y + Theme.S(6), Width * 0.5f, Theme.S(26)), tr);
                    g.DrawString(GradeUtil.Fmt(n.Avg) + " → " + GradeUtil.Fmt(n.Target) + ", става " + shown, sf, pb, Theme.S(4), y + Theme.S(32));
                    StringFormat r = new StringFormat(); r.Alignment = StringAlignment.Far; r.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString("още " + n.K + " " + SixWord(n.K), hf, rb, new RectangleF(Width * 0.4f, y + Theme.S(10), Width * 0.6f - Theme.S(4), Theme.S(34)), r);
                }
            }
        }
    }
}
