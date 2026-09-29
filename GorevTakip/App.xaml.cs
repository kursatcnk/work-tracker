using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using GorevTakip.Helpers;
using GorevTakip.Services;
using GorevTakip.Views;

namespace GorevTakip;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // tarih/gün isimleri makine dili ne olursa olsun türkçe çıksın (datepicker dahil)
        var tr = Fmt.Tr;
        CultureInfo.DefaultThreadCurrentCulture = tr;
        CultureInfo.DefaultThreadCurrentUICulture = tr;
        Thread.CurrentThread.CurrentCulture = tr;
        Thread.CurrentThread.CurrentUICulture = tr;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("tr-TR")));

        DispatcherUnhandledException += (_, ex) =>
        {
            Storage.Log(ex.Exception);
            MessageBox.Show("Beklenmeyen bir hata oluştu:\n\n" + ex.Exception.Message +
                            "\n\nAyrıntılar hata.log dosyasına yazıldı.",
                "ESN Görev Takip", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }
}
