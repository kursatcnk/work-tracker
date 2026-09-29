using System;
using System.Globalization;
using System.Windows;
using GorevTakip.Helpers;

namespace GorevTakip.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this);
        TodayText.Text = DateTime.Now.ToString("d MMMM yyyy, dddd", CultureInfo.GetCultureInfo("tr-TR"));
    }
}
