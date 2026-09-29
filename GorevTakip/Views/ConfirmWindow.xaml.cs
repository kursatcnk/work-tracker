using System.Windows;
using GorevTakip.Helpers;

namespace GorevTakip.Views;

// standart MessageBox koyu temada bembeyaz açılıyordu, onun yerine bu
public partial class ConfirmWindow : Window
{
    private ConfirmWindow(Window? owner, string title, string message, string yes, string? no, bool danger)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this);

        // ana pencere tepsideyken owner verirsek pencere görünmüyor
        if (owner is { IsVisible: true }) Owner = owner;
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        HeaderText.Text = title;
        MessageText.Text = message;
        YesButton.Content = yes;
        if (danger) YesButton.Style = (Style)FindResource("DangerBtn");
        if (no == null) NoButton.Visibility = Visibility.Collapsed;
        else NoButton.Content = no;

        Loaded += (_, _) => YesButton.Focus();
    }

    public static bool Ask(Window? owner, string title, string message, string yes, string no, bool danger = false)
        => new ConfirmWindow(owner, title, message, yes, no, danger).ShowDialog() == true;

    public static void Info(Window? owner, string title, string message)
        => new ConfirmWindow(owner, title, message, "Tamam", null, false).ShowDialog();

    private void Yes_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void No_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
