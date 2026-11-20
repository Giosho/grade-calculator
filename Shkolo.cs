using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GradeCalc
{
    public class Subject
    {
        public string Name;
        public List<double> Grades = new List<double>();
        public double Avg
        {
            get { double s = 0; foreach (double g in Grades) s += g; return Grades.Count == 0 ? 0 : s / Grades.Count; }
        }
    }

    // вграден браузър (WebView2) – влизаш сам в Школо, а приложението чете оценките от страницата
    class ShkoloForm : Form
    {
        // Таблицата на Школо: № | Предмет | Текущи | Срочна | ... | Годишна, най-долу ред "Средноаритметично".
        // Взимаме оценките само от колоните "Текущи" (по позиция на заглавията), без номера на редовете и без средните.
        const string ExtractJs = @"(function(){
  var out=[]; var avg='';
  var SKIPNAME=/^(общ успех|средно|годишен|срок)/i;
  var heads=[];
  var all=document.querySelectorAll('th,td');
  for(var a=0;a<all.length;a++){
    var tt=(all[a].innerText||'').trim();
    if(/^текущи$/i.test(tt)){var r=all[a].getBoundingClientRect(); heads.push([r.left,r.right]);}
  }
  var rows=document.querySelectorAll('tr');
  for(var i=0;i<rows.length;i++){
    var cells=rows[i].children; if(cells.length<3) continue;
    var rowText=(rows[i].innerText||'');
    if(/средноаритметично/i.test(rowText)){var mm=/(\d[.,]\d+)/.exec(rowText); if(mm) avg=mm[1].replace(',','.'); continue;}
    var first=(cells[0].innerText||'').trim();
    if(!/^\d+$/.test(first)) continue;
    var subj=(cells[1].innerText||'').trim().split('\n')[0].trim();
    if(!subj||SKIPNAME.test(subj)) continue;
    var gr=[];
    for(var c=2;c<cells.length;c++){
      if(heads.length>0){
        var cr=cells[c].getBoundingClientRect(); var cx=(cr.left+cr.right)/2; var ok=false;
        for(var h=0;h<heads.length;h++){ if(cx>=heads[h][0]&&cx<=heads[h][1]) ok=true; }
        if(!ok) continue;
      }
      var toks=(cells[c].innerText||'').split(/[\s;]+/);
      for(var k=0;k<toks.length;k++){ var t=toks[k].replace(/[,*]+$/,''); if(/^[2-6]$/.test(t)) gr.push(t); }
    }
    if(gr.length>0) out.push(subj+'\t'+gr.join(' '));
  }
  if(out.length==0){
    var lines=(document.body.innerText||'').split('\n');
    var R=/^(.*[A-Za-zА-Яа-я]{3,}.*?)[\s:–-]+((?:[2-6][\s,;]*)+)$/;
    for(var i=0;i<lines.length;i++){
      var m2=R.exec(lines[i].trim());
      if(m2 && !SKIPNAME.test(m2[1].trim()) && !/средно|общ/i.test(m2[1])) out.push(m2[1].trim()+'\t'+m2[2].trim().replace(/[,;]+/g,' '));
    }
  }
  if(avg) out.push('@avg\t'+avg);
  return out.join('\n');
})()";

        MainForm main;
        WebView2 web;
        TextBox url;

        public ShkoloForm(MainForm m)
        {
            main = m;
            Text = "Школо – влез и отвори страницата с оценките";
            Font = Theme.Ui(10f, FontStyle.Regular);
            Size = new Size(Theme.S(1100), Theme.S(780));
            StartPosition = FormStartPosition.CenterParent;
            try { Icon = main.Icon; } catch { }

            web = new WebView2();
            web.Dock = DockStyle.Fill;
            Controls.Add(web);

            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Top;
            bar.AutoSize = true;
            bar.ColumnCount = 3;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.Padding = new Padding(Theme.S(8));
            bar.BackColor = Theme.Bg;

            url = new TextBox();
            url.Text = "https://app.shkolo.bg";
            url.Dock = DockStyle.Fill;
            url.Margin = new Padding(0, Theme.S(4), Theme.S(8), 0);
            url.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && web.CoreWebView2 != null)
                {
                    e.SuppressKeyPress = true;
                    string u = url.Text.Trim();
                    if (!u.StartsWith("http")) u = "https://" + u;
                    web.CoreWebView2.Navigate(u);
                }
            };
            bar.Controls.Add(url);

            Chip imp = new Chip();
            imp.Primary = true;
            imp.Text = "Вземи оценките от тази страница";
            imp.Margin = new Padding(0, 0, Theme.S(8), 0);
            imp.BackColor = Theme.Bg;
            imp.Click += async delegate { await DoImport(); };
            bar.Controls.Add(imp);

            Chip dbg = new Chip();
            dbg.Text = "Запази текста (за диагностика)";
            dbg.Margin = new Padding(0);
            dbg.BackColor = Theme.Bg;
            dbg.Click += async delegate { await SavePage(); };
            bar.Controls.Add(dbg);
            Controls.Add(bar);

            Load += async delegate { await InitBrowser(); };
        }

        async System.Threading.Tasks.Task InitBrowser()
        {
            try
            {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GradeCalc", "WebView2");
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, folder);
                await web.EnsureCoreWebView2Async(env);
                web.NavigationCompleted += delegate { try { url.Text = web.Source.AbsoluteUri; } catch { } };
                web.CoreWebView2.Navigate(url.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не мога да стартирам вградения браузър:\n" + ex.Message, "Грешка");
            }
        }

        async System.Threading.Tasks.Task<string> GetScriptString(string js)
        {
            string r = await web.ExecuteScriptAsync(js);
            if (r == null || r == "null") return "";
            return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(r) ?? "";
        }

        async System.Threading.Tasks.Task SavePage()
        {
            try
            {
                string text = await GetScriptString("document.body.innerText");
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GradeCalc");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "page.txt");
                System.IO.File.WriteAllText(path, text, System.Text.Encoding.UTF8);
                MessageBox.Show(this, "Запазено локално в:\n" + path + "\n\nТой съдържа текста на страницата (възможно е и лични данни) и не се изпраща никъде.", "Готово");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Грешка"); }
        }

        async System.Threading.Tasks.Task DoImport()
        {
            try
            {
                string data = await GetScriptString(ExtractJs);
                List<Subject> list = new List<Subject>();
                string official = "";
                foreach (string line in data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length < 2) continue;
                    if (parts[0] == "@avg") { official = parts[1].Trim(); continue; }
                    Subject s = new Subject(); s.Name = parts[0];
                    foreach (string t in parts[1].Split(' '))
                    {
                        double v;
                        if (double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 2 && v <= 6) s.Grades.Add(v);
                    }
                    if (s.Grades.Count > 0) list.Add(s);
                }
                if (list.Count == 0)
                {
                    MessageBox.Show(this, "Не намерих оценки на тази страница.\nОтвори страницата с оценките (не началната) и опитай пак.\nАко пак не стане, натисни „Запази текста“ и ми го покажи.", "Няма оценки");
                    return;
                }
                ShowPicker(list, official);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Грешка"); }
        }

        void ShowPicker(List<Subject> list, string official)
        {
            Form f = new Form();
            f.Text = "Намерени предмети";
            f.Font = Font;
            f.StartPosition = FormStartPosition.CenterParent;
            f.Size = new Size(Theme.S(700), Theme.S(560));
            f.MinimumSize = new Size(Theme.S(480), Theme.S(360));

            int total = 0; double sum = 0;
            foreach (Subject s in list) { total += s.Grades.Count; foreach (double g in s.Grades) sum += g; }
            double avgAll = total == 0 ? 0 : sum / total;
            string head = "Намерих " + total + " оценки в " + list.Count + " предмета, среден " + avgAll.ToString("0.00", CultureInfo.InvariantCulture) + ".";
            if (official.Length > 0)
            {
                double o;
                bool same = double.TryParse(official, NumberStyles.Float, CultureInfo.InvariantCulture, out o) && Math.Abs(o - avgAll) < 0.006;
                head += same ? "  Школо показва " + official + " ✔ съвпада." : "  Школо показва " + official + " – не съвпада, провери списъка.";
            }
            Label hl = new Label();
            hl.Text = head; hl.Dock = DockStyle.Top; hl.AutoSize = false; hl.Height = Theme.S(48);
            hl.Padding = new Padding(Theme.S(10), Theme.S(10), Theme.S(10), 0);
            hl.Font = Theme.Ui(10.5f, FontStyle.Bold);

            ListView lv = new ListView();
            lv.View = View.Details; lv.FullRowSelect = true; lv.MultiSelect = false; lv.HideSelection = false;
            lv.Dock = DockStyle.Fill;
            lv.Columns.Add("Предмет", Theme.S(300));
            lv.Columns.Add("Оценки", Theme.S(200));
            lv.Columns.Add("Брой", Theme.S(50));
            lv.Columns.Add("Среден", Theme.S(70));
            foreach (Subject s in list)
            {
                List<string> gs = new List<string>();
                foreach (double g in s.Grades) gs.Add(GradeUtil.FmtGrade(g));
                ListViewItem it = new ListViewItem(s.Name);
                it.SubItems.Add(string.Join(" ", gs.ToArray()));
                it.SubItems.Add(s.Grades.Count.ToString());
                it.SubItems.Add(s.Avg.ToString("0.00", CultureInfo.InvariantCulture));
                it.Tag = s;
                lv.Items.Add(it);
            }
            if (lv.Items.Count > 0) lv.Items[0].Selected = true;
            f.Controls.Add(lv);
            f.Controls.Add(hl);

            FlowLayoutPanel bp = new FlowLayoutPanel();
            bp.Dock = DockStyle.Bottom; bp.AutoSize = true; bp.Padding = new Padding(Theme.S(8));
            bp.FlowDirection = FlowDirection.RightToLeft;
            Button saveAll = new Button(); saveAll.Text = "Запази всички в „Предмети“"; saveAll.AutoSize = true;
            saveAll.Click += delegate { main.ImportSubjects(list); f.Close(); Close(); };
            Button ok = new Button(); ok.Text = "Избраният предмет → калкулатор"; ok.AutoSize = true;
            ok.Click += delegate
            {
                if (lv.SelectedItems.Count == 0) return;
                Subject s = (Subject)lv.SelectedItems[0].Tag;
                main.ApplyGrades(s.Grades);
                f.Close(); Close();
            };
            Button all = new Button(); all.Text = "Всички оценки → калкулатор"; all.AutoSize = true;
            all.Click += delegate
            {
                List<double> every = new List<double>();
                foreach (Subject s in list) every.AddRange(s.Grades);
                main.ApplyGrades(every);
                f.Close(); Close();
            };
            bp.Controls.Add(saveAll); bp.Controls.Add(ok); bp.Controls.Add(all);
            f.Controls.Add(bp);
            f.ShowDialog(this);
        }
    }
}
