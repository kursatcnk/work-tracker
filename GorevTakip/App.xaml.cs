using System;
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
    private const string AppId = "GorevTakip-5f3c9a1e";
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;

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

        // iki kopya açılırsa aynı json'a yazıp birbirini ezerler, alarmlar da iki kere çalar.
        // ikinci kopya ilkine "öne gel" sinyali gönderip kapanıyor
        _mutex = new Mutex(true, AppId, out bool isFirst);
        if (!isFirst)
        {
            try { using var ev = EventWaitHandle.OpenExisting(AppId + "-show"); ev.Set(); } catch { }
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, AppId + "-show");

        DispatcherUnhandledException += (_, ex) =>
        {
            Storage.Log(ex.Exception);
            MessageBox.Show("Beklenmeyen bir hata oluştu:\n\n" + ex.Exception.Message +
                            "\n\nAyrıntılar hata.log dosyasına yazıldı.",
                "WorkTracker", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        var main = new MainWindow();
        MainWindow = main;
        main.Show();

        var signal = _showSignal;
        new Thread(() =>
        {
            while (signal.WaitOne())
                Dispatcher.BeginInvoke(new Action(main.ShowFromTray));
        }) { IsBackground = true }.Start();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // windows kapanırken sormadan kaydet ve çık
        (MainWindow as MainWindow)?.ExitApp(ask: false);
        base.OnSessionEnding(e);
    }
}
