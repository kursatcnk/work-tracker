using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GorevTakip.Helpers;
using GorevTakip.Models;
using GorevTakip.Services;

namespace GorevTakip.Views;

public partial class MainWindow : Window
{
    private enum Tab { Active, Reminders, Done }

    // ctrl+e ile seçili görevin aşamalarını açmak için
    public static readonly RoutedUICommand StagesCommand = new("Aşamalar", nameof(StagesCommand), typeof(MainWindow));

    private readonly ObservableCollection<TodoItem> _items;
    private readonly ListCollectionView _view;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<Guid, AlarmWindow> _openAlarms = new();
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _exiting;
    private bool _trayHintShown;
    private bool _saveErrorShown;
    private int _lastMinute = -1;

    // null = tüm etiketler
    private string? _labelFilter;
    private string _labelBarKey = "";

    // silme sonrası geri alma
    private static readonly TimeSpan UndoDuration = TimeSpan.FromSeconds(10);
    private List<TodoItem> _lastDeleted = new();
    private DispatcherTimer? _undoTimer;

    // kart şablonu bunu dinliyor: açıkken tamamla yuvarlağı yerine seçim kutusu çıkıyor
    public static readonly DependencyProperty IsSelectionModeProperty = DependencyProperty.Register(
        nameof(IsSelectionMode), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public bool IsSelectionMode
    {
        get => (bool)GetValue(IsSelectionModeProperty);
        set => SetValue(IsSelectionModeProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();

        _items = new ObservableCollection<TodoItem>(Storage.Load());
        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(_items);
        _view.Filter = FilterItem;
        _view.CustomSort = new ItemComparer(() => CurrentTab);
        List.ItemsSource = _view;

        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this);
        SetupTray();

        // 5 sn'de bir kontrol yeterli, alarmın birkaç saniye geç çalması sorun değil
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        Loaded += (_, _) =>
        {
            RefreshAll();
            Tick();
            QuickBox.Focus();
        };
    }

    // InitializeComponent sırasında sekmeler daha oluşmadan çağrılabiliyor, o yüzden ?. var
    private Tab CurrentTab =>
        TabDone?.IsChecked == true ? Tab.Done :
        TabReminders?.IsChecked == true ? Tab.Reminders : Tab.Active;

    #region hatırlatma döngüsü

    private void Tick()
    {
        var now = DateTime.Now;
        foreach (var item in _items.Where(i => i.IsReminderPending && i.ReminderAt <= now && !_openAlarms.ContainsKey(i.Id)).ToList())
            ShowAlarm(item);

        // "zamanı geldi" etiketleri ve "Bugün/Yarın" yazıları güncel kalsın diye dakikada bir yenile
        if (now.Minute != _lastMinute)
        {
            _lastMinute = now.Minute;
            foreach (var i in _items) i.Refresh();
            UpdateStatus();
        }
    }

    private void ShowAlarm(TodoItem item)
    {
        var alarm = new AlarmWindow(item, _openAlarms.Count);
        _openAlarms[item.Id] = alarm;
        alarm.Closed += (_, _) =>
        {
            _openAlarms.Remove(item.Id);

            // çıkış yaparken açık alarm pencereleri de kapanıyor. bunu "kullanıcı kapattı" sayarsak
            // bir dahaki açılışta alarm tekrar çıkmaz, o yüzden hiçbir şey yapmıyoruz
            if (_exiting) return;

            switch (alarm.Action)
            {
                case AlarmAction.Complete:
                    SetCompleted(item, true);
                    break;
                case AlarmAction.Snooze:
                    item.ReminderAt = alarm.SnoozeUntil;
                    item.ReminderFired = false;
                    break;
                case AlarmAction.Edit:
                    item.ReminderFired = true;
                    Save();
                    ShowFromTray();
                    OpenEditor(item, focusReminder: true);
                    return;
                case AlarmAction.Silent:
                    return;
                default:
                    item.ReminderFired = true;
                    break;
            }
            item.Refresh();
            Save();
            RefreshAll();
        };
        alarm.Show();
    }

    private void CloseAlarmSilently(TodoItem item)
    {
        if (_openAlarms.TryGetValue(item.Id, out var w)) w.CloseWith(AlarmAction.Silent);
    }

    #endregion

    #region liste / görünüm

    private static bool InTab(TodoItem i, Tab tab) => tab switch
    {
        Tab.Active => !i.IsCompleted,
        Tab.Reminders => !i.IsCompleted && i.HasReminder,
        _ => i.IsCompleted,
    };

    private bool MatchesLabelFilter(TodoItem i) => _labelFilter == null || Labels.Same(i.Label, _labelFilter);

    private bool FilterItem(object o)
    {
        var i = (TodoItem)o;
        if (!InTab(i, CurrentTab) || !MatchesLabelFilter(i)) return false;

        var q = SearchBox.Text.Trim();
        if (q.Length == 0) return true;
        // türkçe büyük/küçük harf (İ/i, I/ı) doğru eşleşsin diye tr culture ile arıyoruz
        var ci = Fmt.Tr.CompareInfo;
        return ci.IndexOf(i.Title, q, CompareOptions.IgnoreCase) >= 0 ||
               ci.IndexOf(i.Note, q, CompareOptions.IgnoreCase) >= 0 ||
               ci.IndexOf(i.Label, q, CompareOptions.IgnoreCase) >= 0 ||
               i.Stages.Any(s => ci.IndexOf(s.Text, q, CompareOptions.IgnoreCase) >= 0);
    }

    private void RefreshAll()
    {
        BuildLabelBar();
        _view.Refresh();

        // sekme sayıları seçili etikete göre, böylece "bu müşteride kaç açık iş var" direkt görünüyor
        var scoped = _items.Where(MatchesLabelFilter).ToList();
        TabActive.Content = $"Aktif  {scoped.Count(i => InTab(i, Tab.Active))}";
        TabReminders.Content = $"Hatırlatmalı  {scoped.Count(i => InTab(i, Tab.Reminders))}";
        TabDone.Content = $"Tamamlanan  {scoped.Count(i => InTab(i, Tab.Done))}";

        bool empty = _view.IsEmpty;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            if (SearchBox.Text.Trim().Length > 0)
            {
                EmptyTitle.Text = "Aramaya uyan not yok";
                EmptyHint.Text = "Farklı bir kelime dene.";
            }
            else if (_labelFilter != null)
            {
                EmptyTitle.Text = $"\"{_labelFilter}\" için bu sekmede görev yok";
                EmptyHint.Text = "Filtreyi kaldırmak için yukarıdaki \"Tümü\"ye tıkla.";
            }
            else
            {
                (EmptyTitle.Text, EmptyHint.Text) = CurrentTab switch
                {
                    Tab.Reminders => ("Bekleyen hatırlatma yok", "Bir nota sağ tıklayıp \"Hatırlatıcı kur\" diyebilirsin."),
                    Tab.Done => ("Henüz tamamlanan yok", "Bir notu bitirince soldaki yuvarlağa tıkla ya da sağ tık > Tamamlandı."),
                    _ => ("Her şey yolunda, bekleyen iş yok", "Yukarıdan hızlıca not ekleyebilir ya da Yeni Not ile hatırlatıcı kurabilirsin."),
                };
            }
        }
        UpdateStatus();
        UpdateBulkBar();
    }

    // sekmedeki görevlerin etiketleri, en kalabalık müşteri başta
    private void BuildLabelBar()
    {
        var groups = _items
            .Where(i => i.HasLabel && InTab(i, CurrentTab))
            .GroupBy(i => i.Label.Trim(), Labels.Comparer)
            .Select(g => (Label: g.First().Label.Trim(), Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Label, Labels.Comparer)
            .ToList();

        // seçili etiketin bu sekmede görevi kalmadıysa da çipi gösteriyoruz, yoksa filtre görünmez kalıyor
        if (_labelFilter != null && !groups.Any(g => Labels.Same(g.Label, _labelFilter)))
            groups.Add((_labelFilter, 0));

        LabelBar.Visibility = groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // her yenilemede baştan çizersek tıklanan çip elimizden gidiyor, sadece değiştiyse çiz
        var key = string.Join("|", groups.Select(g => g.Label + ":" + g.Count)) + "#" + _labelFilter;
        if (key == _labelBarKey) return;
        _labelBarKey = key;

        LabelBar.Children.Clear();
        LabelBar.Children.Add(MakeLabelChip(null, "Tümü"));
        foreach (var g in groups)
            LabelBar.Children.Add(MakeLabelChip(g.Label, $"{g.Label}  {g.Count}"));
    }

    private RadioButton MakeLabelChip(string? label, string text)
    {
        var chip = new RadioButton
        {
            Style = (Style)FindResource("FilterChip"),
            GroupName = "labels",
            IsChecked = label == null ? _labelFilter == null : Labels.Same(label, _labelFilter),
        };

        if (label == null)
        {
            chip.Content = text;
        }
        else
        {
            // renkli nokta, karttaki etiket rengiyle aynı
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = Labels.BrushesFor(label).Fg,
                Margin = new Thickness(0, 1, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(dot);
            panel.Children.Add(new TextBlock { Text = text });
            chip.Content = panel;
        }

        // IsChecked yukarıda verildiği için bu event ilk oluşturmada tetiklenmiyor
        chip.Checked += (_, _) => SetLabelFilter(label);
        return chip;
    }

    private void SetLabelFilter(string? label)
    {
        _labelFilter = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        RefreshAll();
    }

    // düzenleme penceresinde öneri olarak çıkacak etiketler, en çok kullanılan başta
    private List<string> KnownLabels() => _items
        .Where(i => i.HasLabel)
        .GroupBy(i => i.Label.Trim(), Labels.Comparer)
        .OrderByDescending(g => g.Count())
        .Select(g => g.First().Label.Trim())
        .ToList();

    private void UpdateStatus()
    {
        Logo.Subtitle = DateTime.Now.ToString("d MMMM yyyy, dddd", Fmt.Tr);

        var next = _items.Where(i => i.IsReminderPending).OrderBy(i => i.ReminderAt).FirstOrDefault();
        NextText.Text = next == null
            ? "Bekleyen hatırlatma yok"
            : $"Sonraki hatırlatma: {Fmt.Friendly(next.ReminderAt!.Value)}  ·  {next.Title}";

        if (_tray != null)
        {
            // NotifyIcon.Text 63 karakterden uzun olunca exception atıyor
            var text = next == null ? "ESN Görev Takip" : $"ESN Görev Takip – sonraki: {Fmt.Friendly(next.ReminderAt!.Value)}";
            _tray.Text = text.Length > 63 ? text[..63] : text;
        }
    }

    private void Save()
    {
        try
        {
            Storage.Save(_items);
            _saveErrorShown = false;
        }
        catch (Exception ex)
        {
            Storage.Log(ex);
            // usb çıkarılınca her kayıtta pencere açılmasın, bir kere uyarmak yeter
            if (_saveErrorShown) return;
            _saveErrorShown = true;
            ConfirmWindow.Info(this, "Kaydedilemedi",
                "Notlar dosyaya yazılamadı. USB bellek çıkarılmış ya da yazmaya kapalı olabilir.\n\n" + ex.Message);
        }
    }

    #endregion

    #region görev işlemleri

    private void AddItem(TodoItem item)
    {
        _items.Add(item);
        Save();
        if (CurrentTab == Tab.Done) TabActive.IsChecked = true;
        RefreshAll();
        List.SelectedItem = item;
        List.ScrollIntoView(item);
    }

    private void SetCompleted(TodoItem item, bool completed)
    {
        item.IsCompleted = completed;
        item.CompletedAt = completed ? DateTime.Now : null;
        if (completed) CloseAlarmSilently(item);
        item.Refresh();
    }

    public void OpenEditor(TodoItem? item, bool focusReminder = false)
    {
        // bir müşteriye filtrelenmişken yeni not açılırsa etiket hazır gelsin
        var dlg = new EditWindow(item, focusReminder, KnownLabels(), item == null ? _labelFilter : null);
        if (IsVisible) dlg.Owner = this;
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        if (dlg.ShowDialog() != true || dlg.Result == null) return;

        if (item == null)
        {
            AddItem(dlg.Result);
            return;
        }
        // alarm açıkken saati ileri aldıysa o alarm penceresi artık geçersiz
        if (dlg.ReminderChanged) CloseAlarmSilently(item);
        item.Refresh();
        Save();
        RefreshAll();
    }

    // tekli ve toplu silme aynı yoldan geçiyor: önce onay, sonra birkaç saniyelik "geri al" şansı
    private void DeleteItems(IReadOnlyList<TodoItem> items)
    {
        if (items.Count == 0) return;

        bool single = items.Count == 1;
        var message = single
            ? $"\"{items[0].Title}\" silinsin mi?"
            : $"Seçili {items.Count} görev silinsin mi?";
        if (!ConfirmWindow.Ask(this, single ? "Notu sil" : "Görevleri sil",
                message + "\n\nSildikten sonra birkaç saniye içinde geri alabilirsin.",
                single ? "Sil" : $"{items.Count} görevi sil", "Vazgeç", danger: true))
            return;

        foreach (var item in items)
        {
            CloseAlarmSilently(item);
            _items.Remove(item);
        }
        Save();

        ExitSelectionMode();
        RefreshAll();
        ShowUndo(items, single ? "Not silindi" : $"{items.Count} görev silindi");
    }

    private void ShowUndo(IReadOnlyList<TodoItem> deleted, string text)
    {
        // üst üste silinirse sadece son silme geri alınabiliyor, gmail'deki gibi
        _lastDeleted = deleted.ToList();
        UndoText.Text = text;
        UndoBar.Visibility = Visibility.Visible;
        UpdateBulkBar();

        _undoTimer?.Stop();
        _undoTimer = new DispatcherTimer { Interval = UndoDuration };
        _undoTimer.Tick += (_, _) => HideUndo();
        _undoTimer.Start();
    }

    private void HideUndo()
    {
        _undoTimer?.Stop();
        _lastDeleted.Clear();
        UndoBar.Visibility = Visibility.Collapsed;
        UpdateBulkBar();
    }

    // --- toplu seçim ---

    private List<TodoItem> SelectedTasks() => List.SelectedItems.Cast<TodoItem>().ToList();

    private void EnterSelectionMode()
    {
        HideUndo();
        IsSelectionMode = true;
        List.Focus();
        UpdateBulkBar();
    }

    private void ExitSelectionMode()
    {
        IsSelectionMode = false;
        List.UnselectAll();
        UpdateBulkBar();
    }

    private void UpdateBulkBar()
    {
        int n = List.SelectedItems.Count;
        BulkBar.Visibility = IsSelectionMode && UndoBar.Visibility != Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;
        BulkCountText.Text = n == 0 ? "Görev seç" : $"{n} görev seçili";
        BulkDeleteButton.IsEnabled = n > 0;
        BulkCompleteButton.IsEnabled = n > 0;
        BulkCompleteText.Text = CurrentTab == Tab.Done ? "Geri al" : "Tamamla";

        SelectModeText.Text = IsSelectionMode ? "Seçimi bitir" : "Seç";
        SelectModeButton.Foreground = (Brush)FindResource(IsSelectionMode ? "AccentSoftText" : "TextMuted");

        // yüzen çubuk son kartın üstünü kapatmasın
        bool barShown = BulkBar.Visibility == Visibility.Visible || UndoBar.Visibility == Visibility.Visible;
        List.Padding = new Thickness(0, 0, 0, barShown ? 70 : 0);
    }

    private void OpenStages(TodoItem item)
    {
        var dlg = new StagesWindow(item);
        if (IsVisible) dlg.Owner = this;
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dlg.ShowDialog();

        if (!dlg.Changed && !dlg.CompleteRequested) return;
        if (dlg.CompleteRequested) SetCompleted(item, true);
        item.Refresh();
        Save();
        RefreshAll();
    }

    // müşteriye yazarken işe yarıyor: başlık + not + aşamalar düz metin olarak panoya
    private void CopyItem(TodoItem item)
    {
        var text = item.HasLabel ? $"[{item.Label}] {item.Title}" : item.Title;
        if (item.HasNote) text += Environment.NewLine + item.Note;
        foreach (var s in item.Stages)
            text += Environment.NewLine + (s.IsDone ? "[x] " : "[ ] ") + s.Text;

        // pano başka bir programda açıksa exception atabiliyor, sorun etmeyelim
        try { Clipboard.SetText(text); } catch { }
    }

    // menü/kart butonlarından hangi görevin tıklandığını DataContext'ten buluyoruz
    private static TodoItem? ItemOf(object sender) => (sender as FrameworkElement)?.DataContext as TodoItem;

    #endregion

    #region olaylar

    private void NewButton_Click(object sender, RoutedEventArgs e) => OpenEditor(null);
    private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e) => OpenEditor(null);

    private void FindCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void StagesCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (List.SelectedItem is TodoItem item) OpenStages(item);
    }

    private void QuickBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var text = QuickBox.Text.Trim();
        if (text.Length == 0) { OpenEditor(null); return; }
        QuickBox.Clear();
        AddItem(new TodoItem { Title = text, Label = _labelFilter ?? "" });
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (_view == null) return;
        // başka sekmedeki seçim görünmez kalıp yanlışlıkla silinmesin
        List.UnselectAll();
        RefreshAll();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_view == null) return;
        RefreshAll();
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not TodoItem item) return;
        SetCompleted(item, !item.IsCompleted);
        Save();
        RefreshAll();
    }

    // sağ tıklayınca o kart seçili olsun, yoksa menü başka kart seçiliymiş gibi görünüyor.
    // çoklu seçim varken seçili bir karta sağ tıklanırsa seçimi bozmuyoruz
    private void Item_PreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem li || li.IsSelected) return;
        if (!IsSelectionMode) List.UnselectAll();
        li.IsSelected = true;
    }

    // seçim modunda karta tıklamak seçimi aç/kapa yapıyor (ctrl'ye basmaya gerek kalmasın)
    private void Item_PreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsSelectionMode || sender is not ListBoxItem li) return;
        li.IsSelected = !li.IsSelected;
        li.Focus();
        e.Handled = true;
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // ctrl/shift ile ikinci görev seçilince seçim modunu kendiliğinden aç
        if (!IsSelectionMode && List.SelectedItems.Count > 1) EnterSelectionMode();
        else UpdateBulkBar();
    }

    private void SelectMode_Click(object sender, RoutedEventArgs e)
    {
        if (IsSelectionMode) ExitSelectionMode();
        else EnterSelectionMode();
    }

    private void BulkSelectAll_Click(object sender, RoutedEventArgs e)
    {
        List.SelectAll();
        List.Focus();
    }

    private void BulkComplete_Click(object sender, RoutedEventArgs e)
    {
        var items = SelectedTasks();
        if (items.Count == 0) return;
        // tamamlanan sekmesinde bu buton "geri al" oluyor
        bool complete = CurrentTab != Tab.Done;
        foreach (var item in items) SetCompleted(item, complete);
        Save();
        ExitSelectionMode();
        RefreshAll();
    }

    private void BulkDelete_Click(object sender, RoutedEventArgs e) => DeleteItems(SelectedTasks());
    private void BulkCancel_Click(object sender, RoutedEventArgs e) => ExitSelectionMode();

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        var restored = _lastDeleted.ToList();
        HideUndo();
        foreach (var item in restored) _items.Add(item);
        Save();
        RefreshAll();
        // tek görevse seçili gelsin ki nereye döndüğü belli olsun. çokluyu seçersek
        // seçim modu kendiliğinden açılıyor, o da kafa karıştırıyor
        if (restored.Count == 1) List.SelectedItem = restored[0];
        if (restored.Count > 0) List.ScrollIntoView(restored[0]);
    }

    private void UndoClose_Click(object sender, RoutedEventArgs e) => HideUndo();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsSelectionMode)
        {
            ExitSelectionMode();
            e.Handled = true;
        }
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsSelectionMode) return;
        var src = e.OriginalSource as DependencyObject;
        // tik yuvarlağına hızlı iki kere basınca düzenleme açılmasın
        if (src != null && FindAncestor<Button>(src) != null) return;
        if (ItemsControl.ContainerFromElement(List, src) is ListBoxItem li && li.DataContext is TodoItem item)
            OpenEditor(item);
    }

    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            DeleteItems(SelectedTasks());
            e.Handled = true;
            return;
        }

        if (List.SelectedItem is not TodoItem item) return;
        switch (e.Key)
        {
            case Key.Enter when !IsSelectionMode: OpenEditor(item); e.Handled = true; break;
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control: CopyItem(item); e.Handled = true; break;
        }
    }

    private void MenuToggleComplete_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not TodoItem item) return;
        SetCompleted(item, !item.IsCompleted);
        Save();
        RefreshAll();
    }

    private void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is TodoItem item) OpenEditor(item);
    }

    private void MenuStages_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is TodoItem item) OpenStages(item);
    }

    private void StageRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (ItemOf(sender) is not TodoItem item) return;
        e.Handled = true;
        OpenStages(item);
    }

    private void LabelChip_Click(object sender, MouseButtonEventArgs e)
    {
        if (ItemOf(sender) is not TodoItem item || !item.HasLabel) return;
        e.Handled = true;
        SetLabelFilter(item.Label);
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is TodoItem item) CopyItem(item);
    }

    private void MenuReminder_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is TodoItem item) OpenEditor(item, focusReminder: true);
    }

    private void MenuRemoveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not TodoItem item) return;
        CloseAlarmSilently(item);
        item.ReminderAt = null;
        item.ReminderFired = false;
        item.Refresh();
        Save();
        RefreshAll();
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is TodoItem item) DeleteItems(new[] { item });
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            // Run gibi text elemanları visual değil, onlarda logical tree'den çıkmak lazım
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    #endregion

    #region tray / kapatma

    private void SetupTray()
    {
        // ikonu gömülü kaynaktan tepsi boyutunda (16px) alıyoruz, exe'den çekince bulanık çıkıyordu
        System.Drawing.Icon? icon = null;
        try
        {
            var res = Application.GetResourceStream(new Uri("pack://application:,,,/GorevTakip;component/Assets/app.ico"));
            if (res != null)
                icon = new System.Drawing.Icon(res.Stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        }
        catch { }
        if (icon == null)
        {
            try { if (Environment.ProcessPath is string p) icon = System.Drawing.Icon.ExtractAssociatedIcon(p); } catch { }
        }

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            Text = "ESN Görev Takip",
            Visible = true,
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Aç", null, (_, _) => ShowFromTray());
        menu.Items.Add("Yeni Not…", null, (_, _) => { ShowFromTray(); OpenEditor(null); });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => ExitApp(ask: true));
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) ShowFromTray();
        };
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        // windows bazen arka plandaki pencerenin öne gelmesine izin vermiyor, topmost aç-kapa hilesi
        Topmost = true;
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // X'e basınca program kapanmıyor, tepsiye iniyor. kapanırsa hatırlatmalar çalışmaz
        if (!_exiting)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown && _tray != null)
            {
                _trayHintShown = true;
                _tray.ShowBalloonTip(5000, "ESN Görev Takip arka planda çalışıyor",
                    "Hatırlatmalar için açık kalıyor. Tamamen kapatmak için saat yanındaki simgeye sağ tıklayıp Çıkış'ı seç.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
        }
        base.OnClosing(e);
    }

    public void ExitApp(bool ask)
    {
        if (_exiting) return;
        int pending = _items.Count(i => i.IsReminderPending);
        if (ask && pending > 0)
        {
            ShowFromTray();
            if (!ConfirmWindow.Ask(this, "Programdan çık",
                    $"{pending} bekleyen hatırlatma var. Program kapalıyken hatırlatma yapılamaz.\n\nYine de çıkılsın mı?",
                    "Çık", "Vazgeç"))
                return;
        }

        _exiting = true;
        _timer.Stop();
        Save();
        foreach (var w in _openAlarms.Values.ToList()) w.Close();
        if (_tray != null)
        {
            // dispose edilmezse simge fare üstüne gelene kadar tepside asılı kalıyor
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        Application.Current.Shutdown();
    }

    #endregion

    // aktif sekmelerde: önce öncelik (acil en üstte), sonra hatırlatmalılar en yakın zamana göre,
    // kalanlar en yeni üstte. tamamlananlarda: en son biten üstte
    private sealed class ItemComparer : IComparer
    {
        private readonly Func<Tab> _tab;
        public ItemComparer(Func<Tab> tab) => _tab = tab;

        public int Compare(object? x, object? y)
        {
            var a = (TodoItem)x!;
            var b = (TodoItem)y!;
            if (_tab() == Tab.Done) return Nullable.Compare(b.CompletedAt, a.CompletedAt);
            if (a.Priority != b.Priority) return b.Priority.CompareTo(a.Priority);
            if (a.ReminderAt.HasValue != b.ReminderAt.HasValue) return a.ReminderAt.HasValue ? -1 : 1;
            if (a.ReminderAt.HasValue) return a.ReminderAt!.Value.CompareTo(b.ReminderAt!.Value);
            return b.CreatedAt.CompareTo(a.CreatedAt);
        }
    }
}
