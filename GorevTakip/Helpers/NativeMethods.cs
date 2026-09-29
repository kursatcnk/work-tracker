using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GorevTakip.Helpers;

internal static class NativeMethods
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    // wpf başlık çubuğunu kendisi boyamıyor, koyu temada beyaz kalıyordu.
    // eski win10 sürümleri 20 yerine 19 numarayı tanıyor, ikisini de deniyoruz
    public static void UseDarkTitleBar(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));

            // sadece win11'de çalışıyor, arka plan rengiyle (#17181C) aynı olsun. format 0x00BBGGRR
            int caption = 0x001C1817;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        }
        catch { }
    }
}
