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
        const string ExtractJs = @"(function(){
  var G=/^(?:(?:Слаб|Среден|Добър|Мн\.?\s*добър|Отличен)\s*)?\(?([2-6](?:[.,][0-9]+)?)\)?$/i;
  var M=/^[2-6](?:[.,][0-9]+)?(?:[\s,;]+[2-6](?:[.,][0-9]+)?)+$/;
  var HAS=/[A-Za-zА-Яа-я]{3,}/;
  var out=[];
  var rows=document.querySelectorAll('tr');
  for(var i=0;i<rows.length;i++){
    var leaves=rows[i].querySelectorAll('*'); var subj=null; var gr=[];
    for(var j=0;j<leaves.length;j++){
      var el=leaves[j]; if(el.children.length>0) continue;
      var t=(el.innerText||el.textContent||'').trim(); if(!t) continue;
      var m=G.exec(t);
      if(m){gr.push(m[1]);}
      else if(M.test(t)){var p=t.split(/[\s,;]+/);for(var k=0;k<p.length;k++)gr.push(p[k]);}
      else if(!subj && HAS.test(t) && t.length<70 && !/^\d/.test(t)){subj=t.split('\n')[0].trim();}
    }
    if(subj&&gr.length>0) out.push(subj+'\t'+gr.join(' '));
  }
  if(out.length==0){
    var lines=(document.body.innerText||'').split('\n');
    var R=/^(.*[A-Za-zА-Яа-я]{3,}.*?)[\s:–-]+((?:[2-6](?:[.,][0-9]+)?[\s,;]*)+)$/;
    for(var i=0;i<lines.length;i++){
      var m2=R.exec(lines[i].trim());
      if(m2) out.push(m2[1].trim()+'\t'+m2[2].trim().replace(/[,;]+/g,' '));
    }
  }
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
                foreach (string line in data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length < 2) continue;
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
                ShowPicker(list);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Грешка"); }
        }

        void ShowPicker(List<Subject> list)
        {
            Form f = new Form();
            f.Text = "Намерени предмети";
            f.Font = Font;
            f.StartPosition = FormStartPosition.CenterParent;
            f.Size = new Size(Theme.S(640), Theme.S(480));
            f.MinimumSize = new Size(Theme.S(460), Theme.S(320));

            ListView lv = new ListView();
            lv.View = View.Details; lv.FullRowSelect = true; lv.MultiSelect = false; lv.HideSelection = false;
            lv.Dock = DockStyle.Fill;
            lv.Columns.Add("Предмет", Theme.S(200));
            lv.Columns.Add("Оценки", Theme.S(220));
            lv.Columns.Add("Среден", Theme.S(70));
            foreach (Subject s in list)
            {
                List<string> gs = new List<string>();
                foreach (double g in s.Grades) gs.Add(GradeUtil.FmtGrade(g));
                ListViewItem it = new ListViewItem(s.Name);
                it.SubItems.Add(string.Join(" ", gs.ToArray()));
                it.SubItems.Add(s.Avg.ToString("0.00", CultureInfo.InvariantCulture));
                it.Tag = s;
                lv.Items.Add(it);
            }
            if (lv.Items.Count > 0) lv.Items[0].Selected = true;
            f.Controls.Add(lv);

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
            Button all = new Button(); all.Text = "Общ успех → калкулатор"; all.AutoSize = true;
            all.Click += delegate
            {
                double sum = 0; foreach (Subject s in list) sum += s.Avg;
                main.ApplyAverage(sum / list.Count, list.Count);
                f.Close(); Close();
            };
            bp.Controls.Add(saveAll); bp.Controls.Add(ok); bp.Controls.Add(all);
            f.Controls.Add(bp);
            f.ShowDialog(this);
        }
    }
}
