using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GorevTakip.Helpers;

public static class Fmt
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    // "Bugün 14:30", "Yarın 09:00", "Cuma 10:00", "15 Ekim Çar 11:00" gibi
    public static string Friendly(DateTime d)
    {
        int days = (d.Date - DateTime.Today).Days;
        string day = days switch
        {
            0 => "Bugün",
            1 => "Yarın",
            -1 => "Dün",
            >= 2 and <= 6 => d.ToString("dddd", Tr),
            _ when d.Year == DateTime.Today.Year => d.ToString("d MMMM ddd", Tr),
            _ => d.ToString("d MMMM yyyy", Tr),
        };
        return $"{day} {d:HH:mm}";
    }

    public static string Relative(DateTime d)
    {
        var span = d - DateTime.Now;
        if (span.TotalMinutes < 1) return "şimdi";
        if (span.TotalHours < 1) return $"{Math.Ceiling(span.TotalMinutes)} dk sonra";
        if (span.TotalDays < 1)
        {
            int h = (int)span.TotalHours, m = span.Minutes;
            return m > 0 ? $"{h} saat {m} dk sonra" : $"{h} saat sonra";
        }
        return $"{Math.Round(span.TotalDays)} gün sonra";
    }

    // saat kutusuna 14:30 dışında 14.30, 1430, 9 gibi şeyler de yazılabiliyor, hızlı giriş için
    public static bool TryParseTime(string input, out TimeSpan time)
    {
        time = default;
        var s = input.Trim().Replace('.', ':').Replace(',', ':').Replace(' ', ':');
        if (Regex.IsMatch(s, @"^\d{3,4}$")) s = s.PadLeft(4, '0').Insert(2, ":");
        else if (Regex.IsMatch(s, @"^\d{1,2}$")) s += ":00";

        var m = Regex.Match(s, @"^(\d{1,2}):(\d{2})$");
        if (!m.Success) return false;
        int h = int.Parse(m.Groups[1].Value), min = int.Parse(m.Groups[2].Value);
        if (h > 23 || min > 59) return false;
        time = new TimeSpan(h, min, 0);
        return true;
    }

    public static DateTime TrimSeconds(DateTime d) => new(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0);
}
