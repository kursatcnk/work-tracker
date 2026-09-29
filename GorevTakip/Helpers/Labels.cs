using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GorevTakip.Helpers;

public static class Labels
{
    // "km kümsan" ile "KM Kümsan" aynı etiket sayılsın (türkçe İ/ı dahil)
    public static readonly StringComparer Comparer = StringComparer.Create(Fmt.Tr, ignoreCase: true);

    public static bool Same(string? a, string? b) => Comparer.Equals(a?.Trim() ?? "", b?.Trim() ?? "");

    // koyu temada okunur (arka plan, yazı) çiftleri
    private static readonly (Color Bg, Color Fg)[] Palette =
    {
        (Rgb(0x1B2A45), Rgb(0x8DB8FF)),
        (Rgb(0x2B2145), Rgb(0xB79CFF)),
        (Rgb(0x13332F), Rgb(0x5FD4C0)),
        (Rgb(0x3A1E30), Rgb(0xF28BC4)),
        (Rgb(0x3A2A17), Rgb(0xF5A860)),
        (Rgb(0x1C3326), Rgb(0x6FD08F)),
        (Rgb(0x123240), Rgb(0x62CFF0)),
    };

    private static readonly Dictionary<string, (SolidColorBrush Bg, SolidColorBrush Fg)> Cache = new(Comparer);

    // her etiket hep aynı rengi alsın. string.GetHashCode her açılışta değiştiği için kendi hash'imizi kullanıyoruz
    public static (SolidColorBrush Bg, SolidColorBrush Fg) BrushesFor(string label)
    {
        label = label.Trim();
        if (Cache.TryGetValue(label, out var cached)) return cached;

        int h = 0;
        foreach (var ch in label.ToLower(Fmt.Tr)) h = unchecked(h * 31 + ch);
        var (bg, fg) = Palette[(h & 0x7fffffff) % Palette.Length];

        var pair = (new SolidColorBrush(bg), new SolidColorBrush(fg));
        pair.Item1.Freeze();
        pair.Item2.Freeze();
        Cache[label] = pair;
        return pair;
    }

    private static Color Rgb(int v) => Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
}

// xaml'da etiket rengini bağlamak için: ConverterParameter=bg ya da fg
public class LabelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return Brushes.Transparent;
        var (bg, fg) = Labels.BrushesFor(s);
        return parameter as string == "fg" ? fg : bg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
