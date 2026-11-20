using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GradeCalc
{
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        static readonly string[] TabNames = new string[] { "Калкулатор", "Предмети", "Статистика", "Настройки" };

        TabStrip tabs;
        Panel host;
        GridPanel[] pages = new GridPanel[4];
        Timer saveTimer;

        // калкулатор
        TextBox tbGrades, tbAvg, tbCount, tbTarget;
        SummaryBlock summary;
        ResultsPanel results;
        List<Chip> presets = new List<Chip>();
        double[] presetVals = new double[] { 3.0, 3.5, 4.5, 5.5, 6.0 };

        // предмети
        OverallBadge overall;
        TableLayoutPanel rowsPanel;
        List<SubjectRow> rows = new List<SubjectRow>();

        // статистика
        EmptyNote statsEmpty;
        ThemedLabel highlights;
        SectionTitle sectBars, sectTally, sectNext;
        BarsView bars;
        TallyView tally;
        NextStepsView nextView;

        // настройки
        List<Chip> themeChips = new List<Chip>(), accentChips = new List<Chip>();
        ThemedLabel envInfo;

        public MainForm()
        {
            Text = "Калкулатор за оценки";
            Font = Theme.Ui(10f, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(780), Theme.S(740));
            MinimumSize = new Size(Theme.S(620), Theme.S(560));
            try { Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico")); } catch { }

            host = new Panel();
            host.Dock = DockStyle.Fill;
            Controls.Add(host);
            tabs = new TabStrip();
            tabs.Tabs = TabNames;
            Controls.Add(tabs);

            pages[0] = BuildCalc();
            pages[1] = BuildSubjects();
            pages[2] = BuildStats();
            pages[3] = BuildSettings();
            foreach (GridPanel p in pages) { p.Dock = DockStyle.Fill; p.Visible = false; host.Controls.Add(p); }

            saveTimer = new Timer();
            saveTimer.Interval = 500;
            saveTimer.Tick += delegate { saveTimer.Stop(); Store.Save(); };

            tabs.SelectedChanged += delegate { ShowTab(tabs.Selected); };
            tbGrades.TextChanged += delegate { Calculate(); };
            tbAvg.TextChanged += delegate { Calculate(); };
            tbCount.TextChanged += delegate { Calculate(); };
            tbTarget.TextChanged += delegate { Calculate(); };
            Resize += delegate { results.Relayout(); };

            ApplyTheme();
            Calculate();
            RefreshOverall();
            RefreshStats();
            RefreshSettingsUI();
            tabs.Selected = Math.Max(0, Math.Min(3, Program.StartTab));
            ShowTab(tabs.Selected);

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnPrefChanged;
            FormClosed += delegate { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnPrefChanged; Store.Save(); };
            Shown += delegate { ApplyTheme(); if (tabs.Selected == 0) tbGrades.Focus(); };
        }

        void OnPrefChanged(object s, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (!IsHandleCreated) return;
            BeginInvoke(new Action(delegate { ApplyTheme(); RefreshSettingsUI(); }));
        }

        void ShowTab(int i)
        {
            for (int k = 0; k < pages.Length; k++) pages[k].Visible = (k == i);
            if (tabs.Selected != i) { tabs.Selected = i; tabs.Invalidate(); }
            if (i == 2) RefreshStats();
            if (i == 0) results.Relayout();
        }

        // ============ ТЕМА ============
        public void ApplyTheme()
        {
            bool dark;
            if (Program.ForceDark >= 0) dark = Program.ForceDark == 1;
            else if (Store.Data.ThemeMode == "dark") dark = true;
            else if (Store.Data.ThemeMode == "light") dark = false;
            else dark = Theme.SystemDark();
            Color? acc = Program.ForceAccent.HasValue ? Program.ForceAccent : (Store.Data.WinAccent ? Theme.SystemAccent() : null);
            Theme.Apply(dark, acc);

            BackColor = Theme.Paper;
            host.BackColor = Theme.Paper;
            if (IsHandleCreated)
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref v, 4);
                foreach (GridPanel p in pages) { try { SetWindowTheme(p.Handle, dark ? "DarkMode_Explorer" : "Explorer", null); } catch { } }
            }
            Retheme(this);
            summary.ApplyTheme();
            Color? sys = Theme.SystemAccent();
            string hex = sys.HasValue ? string.Format("#{0:X2}{1:X2}{2:X2}", sys.Value.R, sys.Value.G, sys.Value.B) : "няма";
            envInfo.Text = "Windows сега е в " + (Theme.SystemDark() ? "тъмен" : "светъл") + " режим, акцентният му цвят е " + hex + ".";
            Invalidate(true);
        }

        void Retheme(Control c)
        {
            InkField f = c as InkField;
            if (f != null) f.ApplyTheme();
            ThemedLabel l = c as ThemedLabel;
            if (l != null) l.ApplyTheme();
            c.Invalidate();
            foreach (Control ch in c.Controls) Retheme(ch);
        }

        // ============ СТРАНИЦИ ============
        GridPanel NewPage(string title, string sub, out TableLayoutPanel stack)
        {
            GridPanel p = new GridPanel();
            p.AutoScroll = true;
            p.Padding = new Padding(Theme.S(92), Theme.S(22), Theme.S(32), Theme.S(28));
            stack = new TableLayoutPanel();
            stack.Dock = DockStyle.Top;
            stack.AutoSize = true;
            stack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            stack.BackColor = Color.Transparent;
            stack.ColumnCount = 1;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            p.Controls.Add(stack);
            TitleBlock tb = new TitleBlock(title, sub);
            tb.Dock = DockStyle.Fill;
            tb.Margin = new Padding(0, 0, 0, Theme.S(6));
            stack.Controls.Add(tb);
            return p;
        }

        TableLayoutPanel NewInputTable()
        {
            TableLayoutPanel input = new TableLayoutPanel();
            input.Dock = DockStyle.Fill;
            input.AutoSize = true;
            input.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            input.BackColor = Color.Transparent;
            input.ColumnCount = 2;
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            input.Margin = new Padding(0, 0, 0, Theme.S(10));
            return input;
        }

        // ---------- калкулатор ----------
        GridPanel BuildCalc()
        {
            TableLayoutPanel stack;
            GridPanel page = NewPage("Калкулатор за оценки", "Колко шестици или петици ти трябват, за да стигнеш целта?", out stack);

            TableLayoutPanel input = NewInputTable();
            AddLabel(input, "Твоите оценки", 2);
            tbGrades = AddBox(input, "напр.  6 5 5 4 6", 2);
            AddLabel(input, "…или ако знаеш само успеха си", 2);
            tbAvg = AddBox(input, "сегашен успех, напр. 5.20", 1);
            tbCount = AddBox(input, "брой оценки", 1);
            AddLabel(input, "Какъв успех искаш да стигнеш", 2);

            FlowLayoutPanel fl = new FlowLayoutPanel();
            fl.AutoSize = true;
            fl.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fl.Dock = DockStyle.Fill;
            fl.WrapContents = true;
            fl.Margin = new Padding(0);
            fl.BackColor = Color.Transparent;

            InkField tf = new InkField(null);
            tf.Width = Theme.S(84);
            tf.Box.Text = GradeUtil.Fmt(Store.Data.Goal);
            tf.Margin = new Padding(0, 0, Theme.S(12), 0);
            tbTarget = tf.Box;
            fl.Controls.Add(tf);

            ToolTip tip = new ToolTip();
            string[] tips = new string[] { "Среден 3", "Добър 4", "Много добър 5", "Отличен 6 (от 5.50)", "Шест" };
            for (int i = 0; i < presetVals.Length; i++)
            {
                Chip c = new Chip();
                c.Text = GradeUtil.Fmt(presetVals[i]);
                tip.SetToolTip(c, tips[i]);
                c.Margin = new Padding(0, 0, Theme.S(6), 0);
                double pv = presetVals[i];
                c.Click += delegate { tbTarget.Text = GradeUtil.Fmt(pv); };
                presets.Add(c);
                fl.Controls.Add(c);
            }
            Chip clear = new Chip();
            clear.LinkStyle = true;
            clear.Text = "Изчисти";
            clear.Margin = new Padding(Theme.S(4), 0, 0, 0);
            clear.Click += delegate { tbGrades.Text = ""; tbAvg.Text = ""; tbCount.Text = ""; tbGrades.Focus(); };
            fl.Controls.Add(clear);
            input.Controls.Add(fl);
            input.SetColumnSpan(fl, 2);

            Chip sh = new Chip();
            sh.Primary = true;
            sh.Text = "Вземи оценките от Школо";
            sh.Margin = new Padding(0, Theme.S(14), 0, 0);
            sh.Click += delegate { OpenShkolo(); };
            input.Controls.Add(sh);
            input.SetColumnSpan(sh, 2);
            stack.Controls.Add(input);

            summary = new SummaryBlock();
            summary.Dock = DockStyle.Fill;
            summary.Margin = new Padding(0, Theme.S(6), 0, Theme.S(12));
            stack.Controls.Add(summary);

            results = new ResultsPanel();
            results.Dock = DockStyle.Fill;
            results.Margin = new Padding(0);
            stack.Controls.Add(results);
            return page;
        }

        // ---------- предмети ----------
        GridPanel BuildSubjects()
        {
            TableLayoutPanel stack;
            GridPanel page = NewPage("Предмети", "Оценките ти по предмети. Запазват се сами.", out stack);

            overall = new OverallBadge();
            overall.Dock = DockStyle.Fill;
            overall.Margin = new Padding(0, 0, 0, Theme.S(8));
            stack.Controls.Add(overall);

            rowsPanel = new TableLayoutPanel();
            rowsPanel.Dock = DockStyle.Fill;
            rowsPanel.AutoSize = true;
            rowsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            rowsPanel.BackColor = Color.Transparent;
            rowsPanel.ColumnCount = 1;
            rowsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rowsPanel.Margin = new Padding(0, 0, 0, Theme.S(8));
            stack.Controls.Add(rowsPanel);

            FlowLayoutPanel fl = new FlowLayoutPanel();
            fl.AutoSize = true; fl.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fl.Dock = DockStyle.Fill; fl.WrapContents = true; fl.BackColor = Color.Transparent; fl.Margin = new Padding(0);
            Chip add = new Chip(); add.Primary = true; add.Text = "+ Добави предмет"; add.Margin = new Padding(0, 0, Theme.S(8), 0);
            add.Click += delegate { AddSubject(); };
            Chip sh = new Chip(); sh.Text = "Вземи от Школо"; sh.Margin = new Padding(0);
            sh.Click += delegate { OpenShkolo(); };
            fl.Controls.Add(add); fl.Controls.Add(sh);
            stack.Controls.Add(fl);

            foreach (SubjectData d in Store.Data.Subjects) AddRow(d);
            return page;
        }

        void AddRow(SubjectData d)
        {
            SubjectRow r = new SubjectRow(d);
            r.Changed += delegate { ScheduleSave(); RefreshOverall(); RefreshStats(); };
            r.Removed += delegate
            {
                if (!string.IsNullOrEmpty(d.Grades) &&
                    MessageBox.Show(this, "Да премахна ли „" + (string.IsNullOrEmpty(d.Name) ? "предмета" : d.Name) + "“ и оценките му?", "Премахване", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Store.Data.Subjects.Remove(d);
                rows.Remove(r);
                rowsPanel.Controls.Remove(r);
                r.Dispose();
                ScheduleSave(); RefreshOverall(); RefreshStats();
            };
            r.ToCalc += delegate
            {
                List<double> l;
                if (!GradeUtil.TryParse(d.Grades, out l) || l.Count == 0) { MessageBox.Show(this, "Първо напиши валидни оценки (между 2 и 6).", "Няма оценки"); return; }
                ApplyGrades(l);
                ShowTab(0);
            };
            rows.Add(r);
            rowsPanel.Controls.Add(r);
        }

        void AddSubject()
        {
            SubjectData d = new SubjectData();
            Store.Data.Subjects.Add(d);
            AddRow(d);
            SubjectRow r = rows[rows.Count - 1];
            pages[1].ScrollControlIntoView(r);
            r.NameField.Box.Focus();
            RefreshOverall();
        }

        void RebuildRows()
        {
            foreach (SubjectRow r in rows) { rowsPanel.Controls.Remove(r); r.Dispose(); }
            rows.Clear();
            foreach (SubjectData d in Store.Data.Subjects) AddRow(d);
            RefreshOverall();
            RefreshStats();
        }

        public void ImportSubjects(List<Subject> list)
        {
            foreach (Subject s in list)
            {
                SubjectData found = null;
                foreach (SubjectData d in Store.Data.Subjects)
                    if (string.Equals(d.Name.Trim(), s.Name.Trim(), StringComparison.CurrentCultureIgnoreCase)) { found = d; break; }
                List<string> gs = new List<string>();
                foreach (double g in s.Grades) gs.Add(GradeUtil.FmtGrade(g));
                if (found == null) { found = new SubjectData(); found.Name = s.Name; Store.Data.Subjects.Add(found); }
                found.Grades = string.Join(" ", gs.ToArray());
            }
            Store.Save();
            RebuildRows();
            ShowTab(1);
        }

        List<SubStat> GetStats()
        {
            List<SubStat> l = new List<SubStat>();
            foreach (SubjectData d in Store.Data.Subjects)
            {
                List<double> g;
                if (!GradeUtil.TryParse(d.Grades, out g) || g.Count == 0) continue;
                SubStat s = new SubStat(); s.Name = d.Name; s.Grades = g;
                l.Add(s);
            }
            return l;
        }

        void RefreshOverall()
        {
            List<SubStat> l = GetStats();
            double sum = 0; foreach (SubStat s in l) sum += s.Avg;
            overall.Avg = l.Count == 0 ? double.NaN : sum / l.Count;
            overall.Count = l.Count;
            overall.Invalidate();
        }

        void ScheduleSave() { saveTimer.Stop(); saveTimer.Start(); }

        // ---------- статистика ----------
        GridPanel BuildStats()
        {
            TableLayoutPanel stack;
            GridPanel page = NewPage("Статистика", "Къде си силен и какво да поправиш първо.", out stack);

            statsEmpty = new EmptyNote();
            statsEmpty.Message = "Още няма предмети с оценки. Добави ги в раздел „Предмети“ или ги вземи от Школо.";
            statsEmpty.Dock = DockStyle.Fill;
            stack.Controls.Add(statsEmpty);

            highlights = new ThemedLabel();
            highlights.Strong = true;
            highlights.Font = Theme.Ui(11f, FontStyle.Regular);
            highlights.Margin = new Padding(0, 0, 0, Theme.S(6));
            stack.Controls.Add(highlights);

            sectBars = new SectionTitle("Среден успех по предмет"); sectBars.Dock = DockStyle.Fill; stack.Controls.Add(sectBars);
            bars = new BarsView(); bars.Dock = DockStyle.Fill; bars.Margin = new Padding(0, 0, 0, Theme.S(12)); stack.Controls.Add(bars);
            sectTally = new SectionTitle("Колко оценки от всеки вид"); sectTally.Dock = DockStyle.Fill; stack.Controls.Add(sectTally);
            tally = new TallyView(); tally.Dock = DockStyle.Fill; tally.Margin = new Padding(0, 0, 0, Theme.S(12)); stack.Controls.Add(tally);
            sectNext = new SectionTitle("Какво да поправиш първо"); sectNext.Dock = DockStyle.Fill; stack.Controls.Add(sectNext);
            nextView = new NextStepsView(); nextView.Dock = DockStyle.Fill; stack.Controls.Add(nextView);
            return page;
        }

        void RefreshStats()
        {
            List<SubStat> l = GetStats();
            bool has = l.Count > 0;
            statsEmpty.Visible = !has;
            highlights.Visible = has;
            sectBars.Visible = sectTally.Visible = sectNext.Visible = has;
            bars.Visible = tally.Visible = nextView.Visible = has;
            if (!has) return;
            bars.SetData(l, Store.Data.Goal);
            tally.SetData(l);
            nextView.SetData(NextStepsView.Build(l));
            SubStat best = l[0], worst = l[0];
            foreach (SubStat s in l) { if (s.Avg > best.Avg) best = s; if (s.Avg < worst.Avg) worst = s; }
            int total = 0; foreach (SubStat s in l) total += s.N;
            string nm1 = string.IsNullOrEmpty(best.Name) ? "(без име)" : best.Name;
            string nm2 = string.IsNullOrEmpty(worst.Name) ? "(без име)" : worst.Name;
            highlights.Text = l.Count == 1
                ? "Имаш " + total + " оценки по 1 предмет."
                : "Най-силен: " + nm1 + " (" + GradeUtil.Fmt(best.Avg) + "). Най-слаб: " + nm2 + " (" + GradeUtil.Fmt(worst.Avg) + "). Общо " + total + " оценки.";
        }

        // ---------- настройки ----------
        GridPanel BuildSettings()
        {
            TableLayoutPanel stack;
            GridPanel page = NewPage("Настройки", "Тема, цвят и цел по подразбиране.", out stack);
            TableLayoutPanel input = NewInputTable();

            AddLabel(input, "Тема", 2);
            input.Controls.Add(ChipRow(themeChips, new string[] { "Като Windows", "Светла", "Тъмна" }, new string[] { "auto", "light", "dark" }, true));
            input.SetColumnSpan(input.Controls[input.Controls.Count - 1], 2);

            AddLabel(input, "Цвят на мастилото", 2);
            input.Controls.Add(ChipRow(accentChips, new string[] { "Цвят от Windows", "Класическо синьо" }, new string[] { "win", "classic" }, false));
            input.SetColumnSpan(input.Controls[input.Controls.Count - 1], 2);

            envInfo = new ThemedLabel();
            envInfo.Margin = new Padding(0, Theme.S(8), 0, Theme.S(6));
            input.Controls.Add(envInfo);
            input.SetColumnSpan(envInfo, 2);

            AddLabel(input, "Цел по подразбиране", 2);
            InkField gf = new InkField("напр. 5.50");
            gf.Width = Theme.S(110);
            gf.Anchor = AnchorStyles.Left;
            gf.Box.Text = GradeUtil.Fmt(Store.Data.Goal);
            gf.Margin = new Padding(0, 0, 0, Theme.S(4));
            gf.Box.TextChanged += delegate
            {
                double v;
                if (GradeUtil.TryNum(gf.Box.Text, out v) && v >= 2 && v <= 6)
                {
                    Store.Data.Goal = v; ScheduleSave();
                    tbTarget.Text = GradeUtil.Fmt(v);
                    RefreshStats();
                }
            };
            input.Controls.Add(gf);
            input.SetColumnSpan(gf, 2);

            Chip wipe = new Chip(); wipe.LinkStyle = true; wipe.Danger = true; wipe.Text = "Изтрий всички предмети";
            wipe.Margin = new Padding(0, Theme.S(18), 0, 0);
            wipe.Click += delegate
            {
                if (MessageBox.Show(this, "Да изтрия ли всички предмети и оценки? Това не може да се върне.", "Изтриване", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                Store.Data.Subjects.Clear(); Store.Save(); RebuildRows();
            };
            input.Controls.Add(wipe);
            input.SetColumnSpan(wipe, 2);

            stack.Controls.Add(input);
            return page;
        }

        FlowLayoutPanel ChipRow(List<Chip> store, string[] names, string[] keys, bool isTheme)
        {
            FlowLayoutPanel fl = new FlowLayoutPanel();
            fl.AutoSize = true; fl.AutoSizeMode = AutoSizeMode.GrowAndShrink; fl.WrapContents = true;
            fl.BackColor = Color.Transparent; fl.Margin = new Padding(0, 0, 0, Theme.S(4)); fl.Dock = DockStyle.Fill;
            for (int i = 0; i < names.Length; i++)
            {
                Chip c = new Chip();
                c.Text = names[i];
                c.Tag = keys[i];
                c.Margin = new Padding(0, 0, Theme.S(8), 0);
                string key = keys[i];
                c.Click += delegate
                {
                    if (isTheme) Store.Data.ThemeMode = key; else Store.Data.WinAccent = key == "win";
                    Store.Save();
                    ApplyTheme();
                    RefreshSettingsUI();
                };
                store.Add(c);
                fl.Controls.Add(c);
            }
            return fl;
        }

        void RefreshSettingsUI()
        {
            foreach (Chip c in themeChips) { c.Selected = (string)c.Tag == Store.Data.ThemeMode; c.Invalidate(); }
            foreach (Chip c in accentChips) { c.Selected = ((string)c.Tag == "win") == Store.Data.WinAccent; c.Invalidate(); }
        }

        // ============ ОБЩИ ============
        public void ApplyGrades(List<double> grades)
        {
            List<string> gs = new List<string>();
            foreach (double g in grades) gs.Add(GradeUtil.FmtGrade(g));
            tbAvg.Text = ""; tbCount.Text = "";
            tbGrades.Text = string.Join(" ", gs.ToArray());
        }

        public void ApplyAverage(double avg, int count)
        {
            tbGrades.Text = "";
            tbAvg.Text = GradeUtil.Fmt(avg);
            tbCount.Text = count.ToString();
        }

        void OpenShkolo()
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GradeCalc");
                System.IO.Directory.CreateDirectory(dir);
                string dll = System.IO.Path.Combine(dir, "WebView2Loader.dll");
                using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("wv2loader.dll"))
                {
                    if (!System.IO.File.Exists(dll) || new System.IO.FileInfo(dll).Length != st.Length)
                    {
                        using (System.IO.FileStream fs = System.IO.File.Create(dll)) st.CopyTo(fs);
                    }
                }
                Program.LoadLibrary(dll);
                new ShkoloForm(this).ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Грешка");
            }
        }

        void AddLabel(TableLayoutPanel t, string text, int span)
        {
            ThemedLabel l = new ThemedLabel();
            l.Text = text;
            l.Margin = new Padding(0, Theme.S(10), 0, Theme.S(4));
            t.Controls.Add(l);
            t.SetColumnSpan(l, span);
        }

        TextBox AddBox(TableLayoutPanel t, string hint, int span)
        {
            InkField f = new InkField(hint);
            f.Dock = DockStyle.Fill;
            f.Margin = new Padding(0, 0, span == 1 ? Theme.S(14) : 0, Theme.S(4));
            t.Controls.Add(f);
            t.SetColumnSpan(f, span);
            return f.Box;
        }

        static string F(double v) { return GradeUtil.Fmt(v); }

        void SetMsg(string text, Color c)
        {
            summary.Valid = false; summary.Message = text; summary.SetMsgColor(c);
            ClearResults();
            summary.Sync();
        }

        void ClearResults()
        {
            foreach (Control c in results.Controls) c.Dispose();
            results.Controls.Clear();
            results.Relayout();
        }

        void Calculate()
        {
            try { CalculateCore(); }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gradecalc.log"), ex.ToString() + "\r\n"); } catch { }
                SetMsg("Грешка: " + ex.Message, Theme.Red);
            }
        }

        void CalculateCore()
        {
            double tv;
            bool tok = GradeUtil.TryNum(tbTarget.Text, out tv);
            for (int i = 0; i < presets.Count; i++)
            {
                presets[i].Selected = tok && Math.Abs(tv - presetVals[i]) < 1e-9;
                presets[i].Invalidate();
            }

            double sum = 0; int n = 0;
            string g = tbGrades.Text.Trim();
            if (g.Length > 0)
            {
                List<double> list;
                if (!GradeUtil.TryParse(g, out list))
                {
                    SetMsg("В списъка има нещо, което не е оценка. Оценките са между 2 и 6.", Theme.Red);
                    return;
                }
                sum = GradeUtil.Sum(list); n = list.Count;
            }
            else
            {
                double avg, cnt;
                if (!GradeUtil.TryNum(tbAvg.Text, out avg) || !GradeUtil.TryNum(tbCount.Text, out cnt))
                {
                    SetMsg("Напиши оценките си горе или сегашния успех и броя оценки.", Theme.Pencil);
                    return;
                }
                if (avg < 2 || avg > 6 || cnt < 1 || cnt != Math.Floor(cnt))
                {
                    SetMsg("Успехът е между 2 и 6, а броят оценки е цяло число.", Theme.Red);
                    return;
                }
                n = (int)cnt; sum = avg * n;
            }
            if (n == 0) { SetMsg("Напиши оценките си горе.", Theme.Pencil); return; }

            double target = tv;
            if (!tok || target < 2 || target > 6)
            {
                SetMsg("Целта е число между 2 и 6.", Theme.Red);
                return;
            }

            double cur = sum / n;
            summary.Valid = true; summary.Cur = cur; summary.Target = target; summary.N = n;
            ClearResults();

            if (cur >= target - 1e-9)
            {
                summary.Message = "Браво! Целта е постигната ✔";
                summary.SetMsgColor(Theme.Green);
                summary.Sync();
                return;
            }
            summary.Message = "Още " + F(target - cur) + " до целта";
            summary.SetMsgColor(Theme.Red);
            summary.Sync();

            bool any = false;
            for (int grade = 6; grade >= 3; grade--)
            {
                ResultRow rc = new ResultRow();
                rc.Big = grade.ToString();
                rc.Seed = 20 + grade;
                if (grade <= target)
                {
                    rc.Disabled = true;
                    rc.Title = "Само " + Word(grade);
                    rc.Sub = "не помагат: не са над целта " + F(target);
                }
                else
                {
                    int k = (int)Math.Ceiling((target * n - sum) / (grade - target) - 1e-9);
                    if (k < 1) k = 1;
                    double after = (sum + k * grade) / (n + k);
                    rc.Title = "Още " + k + " " + Word(grade);
                    rc.Sub = "успехът става " + F(after);
                    any = true;
                }
                results.Controls.Add(rc);
            }

            if (target >= 5 && target < 6)
            {
                List<string> combos = new List<string>();
                for (int six = 1; six <= 12 && combos.Count < 3; six++)
                {
                    for (int b = 1; b <= 80; b++)
                    {
                        if ((sum + 6 * six + 5 * b) / (n + six + b) >= target - 1e-9)
                        {
                            combos.Add(six + "×6 + " + b + "×5");
                            break;
                        }
                    }
                }
                if (combos.Count > 0)
                {
                    ResultRow rc = new ResultRow();
                    rc.Big = "+";
                    rc.Seed = 40;
                    rc.Title = "Или комбинация";
                    rc.Sub = string.Join(",  ", combos.ToArray());
                    results.Controls.Add(rc);
                }
            }

            if (!any)
            {
                summary.Message = "С тези оценки целта не се достига";
                summary.SetMsgColor(Theme.Red);
                summary.Sync();
            }
            results.Relayout();
        }

        static string Word(int grade)
        {
            switch (grade)
            {
                case 6: return "шестици";
                case 5: return "петици";
                case 4: return "четворки";
                default: return "тройки";
            }
        }
    }
}
