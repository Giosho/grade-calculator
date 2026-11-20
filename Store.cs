using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace GradeCalc
{
    public class SubjectData
    {
        public string Name { get; set; }
        public string Grades { get; set; }
        public SubjectData() { Name = ""; Grades = ""; }
    }

    public class AppData
    {
        public string ThemeMode { get; set; }   // auto | light | dark
        public bool WinAccent { get; set; }     // ползвай акцентния цвят на Windows
        public double Goal { get; set; }
        public List<SubjectData> Subjects { get; set; }
        public AppData() { ThemeMode = "auto"; WinAccent = true; Goal = 5.5; Subjects = new List<SubjectData>(); }
    }

    static class Store
    {
        public static AppData Data = new AppData();
        public static bool NoPersist;

        static string PathFile()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GradeCalc");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "data.json");
        }

        public static void Load()
        {
            try
            {
                string p = PathFile();
                if (File.Exists(p))
                {
                    AppData d = new JavaScriptSerializer().Deserialize<AppData>(File.ReadAllText(p, System.Text.Encoding.UTF8));
                    if (d != null)
                    {
                        if (d.Subjects == null) d.Subjects = new List<SubjectData>();
                        if (d.Goal < 2 || d.Goal > 6) d.Goal = 5.5;
                        if (d.ThemeMode != "light" && d.ThemeMode != "dark") d.ThemeMode = "auto";
                        Data = d;
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            if (NoPersist) return;
            try { File.WriteAllText(PathFile(), new JavaScriptSerializer().Serialize(Data), System.Text.Encoding.UTF8); }
            catch { }
        }
    }

    static class GradeUtil
    {
        public static bool TryNum(string s, out double v)
        {
            return double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        // "6 5 5,50 4" -> списък; невалидна оценка = false
        public static bool TryParse(string text, out List<double> list)
        {
            list = new List<double>();
            if (text == null) return true;
            foreach (string tok in text.Split(new char[] { ' ', ';', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // запетая между оценки без интервал: "6,5,5" – всяка цифра отделно
                string[] parts = tok.Split(new char[] { ',' });
                if (parts.Length > 1 && IsSimpleDigits(parts))
                {
                    foreach (string p in parts) { double v; if (!TryNum(p, out v) || v < 2 || v > 6) return false; list.Add(v); }
                    continue;
                }
                double w;
                if (!TryNum(tok, out w) || w < 2 || w > 6) return false;
                list.Add(w);
            }
            return true;
        }

        static bool IsSimpleDigits(string[] parts)
        {
            foreach (string p in parts) { if (p.Length != 1 || p[0] < '2' || p[0] > '6') return false; }
            return true;
        }

        public static double Sum(List<double> l) { double s = 0; foreach (double g in l) s += g; return s; }
        public static string Fmt(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        public static string FmtGrade(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }

    class SubStat
    {
        public string Name;
        public List<double> Grades;
        public double Sum { get { return GradeUtil.Sum(Grades); } }
        public int N { get { return Grades.Count; } }
        public double Avg { get { return Grades.Count == 0 ? 0 : Sum / Grades.Count; } }
    }
}
