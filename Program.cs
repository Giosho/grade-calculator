using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GradeCalc
{
    static class Program
    {
        // за проверка на външния вид: --theme=dark|light  --accent=RRGGBB  --tab=0..3  --demo
        public static int ForceDark = -1;
        public static Color? ForceAccent;
        public static int StartTab;
        public static bool Demo;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibrary(string path);

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static System.Reflection.Assembly Resolve(object s, ResolveEventArgs a)
        {
            string n = new System.Reflection.AssemblyName(a.Name).Name;
            string res = n == "Microsoft.Web.WebView2.Core" ? "wv2core.dll" : (n == "Microsoft.Web.WebView2.WinForms" ? "wv2wf.dll" : null);
            if (res == null) return null;
            using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(res))
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                st.CopyTo(ms);
                return System.Reflection.Assembly.Load(ms.ToArray());
            }
        }

        static void AddDemo(string name, string grades)
        {
            SubjectData d = new SubjectData(); d.Name = name; d.Grades = grades;
            Store.Data.Subjects.Add(d);
        }

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            foreach (string a in args)
            {
                if (a == "--theme=dark") ForceDark = 1;
                else if (a == "--theme=light") ForceDark = 0;
                else if (a.StartsWith("--accent=") && a.Length >= 15)
                {
                    try { string h = a.Substring(9).TrimStart('#'); ForceAccent = Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16)); } catch { }
                }
                else if (a.StartsWith("--tab=")) { int.TryParse(a.Substring(6), out StartTab); }
                else if (a == "--demo") Demo = true;
            }
            Store.Load();
            if (Demo)
            {
                Store.NoPersist = true;
                Store.Data.Subjects.Clear();
                AddDemo("Математика", "6 5 5 4 6");
                AddDemo("Български език", "5 5 6 4");
                AddDemo("Английски", "6 6 6 5");
                AddDemo("История", "4 5 4 5 3");
                AddDemo("Физика", "5 4 4 5");
                AddDemo("Биология", "6 5 6 6");
                AddDemo("Информатика", "6 6 6");
            }
            try { SetProcessDPIAware(); } catch { }
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Theme.Scale = g.DpiX / 96f;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
