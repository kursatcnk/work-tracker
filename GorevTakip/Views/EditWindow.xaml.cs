using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GorevTakip.Helpers;
using GorevTakip.Models;

namespace GorevTakip.Views;

public partial class EditWindow : Window
{
    private readonly TodoItem? _existing;

    // alanları doldururken tetiklenen event'ler hatırlatıcıyı kendiliğinden açmasın diye
    private bool _loading = true;

    public TodoItem? Result { get; private set; }
    public bool ReminderChanged { get; private set; }

    public EditWindow(TodoItem? existing, bool focusReminder)
    {
        InitializeComponent();
        _existing = existing;
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this);

        HeaderText.Text = existing == null ? "Yeni not" : "Notu düzenle";
        Title = HeaderText.Text;

        if (existing != null)
        {
            TitleBox.Text = existing.Title;
            NoteBox.Text = existing.Note;
            PrioHigh.IsChecked = existing.Priority == Priority.Yuksek;
            PrioUrgent.IsChecked = existing.Priority == Priority.Acil;
        }

        if (existing?.ReminderAt is DateTime r)
        {
            ReminderCheck.IsChecked = true;
            SetFields(r);
        }
        else
        {
            // hatırlatıcı yoksa varsayılan olarak bir sonraki tam saati öner
            var next = DateTime.Now.AddHours(1);
            SetFields(next.Date.AddHours(next.Hour));
            ReminderCheck.IsChecked = focusReminder;
        }

        _loading = false;
        UpdatePreview();

        Loaded += (_, _) =>
        {
            if (focusReminder) { TimeBox.Focus(); TimeBox.SelectAll(); }
            else { TitleBox.Focus(); TitleBox.CaretIndex = TitleBox.Text.Length; }
        };
    }

    private void SetFields(DateTime when)
    {
        DateBox.SelectedDate = when.Date;
        TimeBox.Text = when.ToString("HH:mm");
    }

    private bool TryGetReminder(out DateTime when, out string error)
    {
        when = default;
        error = "";
        if (DateBox.SelectedDate is not DateTime date) { error = "Lütfen bir tarih seç."; return false; }
        if (!Fmt.TryParseTime(TimeBox.Text, out var time)) { error = "Saat anlaşılamadı. Örnek: 14:30"; return false; }
        when = date.Date + time;
        return true;
    }

    private void UpdatePreview()
    {
        if (_loading) return;
        if (ReminderCheck.IsChecked != true)
        {
            Preview.Text = "Hatırlatıcı kapalı. Tarih/saat seçince otomatik açılır.";
            Preview.Foreground = (Brush)FindResource("TextFaint");
            return;
        }
        if (!TryGetReminder(out var when, out var err))
        {
            Preview.Text = err;
            Preview.Foreground = (Brush)FindResource("Danger");
            return;
        }
        if (when <= DateTime.Now)
        {
            Preview.Text = $"{Fmt.Friendly(when)} geçmişte kaldı. Kaydedersen hemen uyarı verir.";
            Preview.Foreground = (Brush)FindResource("Warning");
        }
        else
        {
            Preview.Text = $"Uyarı zamanı: {Fmt.Friendly(when)}  ({Fmt.Relative(when)})";
            Preview.Foreground = (Brush)FindResource("AccentSoftText");
        }
    }

    // tarih ya da saatle oynandıysa kullanıcı hatırlatıcı istiyordur, kutucuğu kendimiz işaretleyelim
    private void TurnReminderOn()
    {
        if (!_loading && ReminderCheck.IsChecked != true) ReminderCheck.IsChecked = true;
    }

    private void Reminder_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    private void DateBox_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        TurnReminderOn();
        UpdatePreview();
    }

    private void TimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TurnReminderOn();
        UpdatePreview();
    }

    private void Quick_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        var when = (((Button)sender).Tag as string) switch
        {
            "30m" => now.AddMinutes(30),
            "2h" => now.AddHours(2),
            "today17" => DateTime.Today.AddHours(17),
            "tomorrow9" => DateTime.Today.AddDays(1).AddHours(9),
            "week" => DateTime.Today.AddDays(7).AddHours(9),
            _ => now,
        };
        SetFields(Fmt.TrimSeconds(when));
        ReminderCheck.IsChecked = true;
        UpdatePreview();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // not kutusunda Enter satır atlıyor, kaydetmek için ctrl+enter
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            Save_Click(this, new RoutedEventArgs());
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        var note = NoteBox.Text.Trim();

        if (title.Length == 0)
        {
            if (note.Length == 0)
            {
                ShowError("Başlık ya da not yazmalısın.");
                TitleBox.Focus();
                return;
            }
            // başlık boş bırakıldıysa notun ilk satırı başlık olsun
            title = note.Split('\n')[0].Trim();
            if (title.Length > 60) title = title[..60].TrimEnd() + "…";
        }

        DateTime? reminder = null;
        if (ReminderCheck.IsChecked == true)
        {
            if (!TryGetReminder(out var when, out var err))
            {
                ShowError(err);
                TimeBox.Focus();
                return;
            }
            reminder = when;
        }

        var item = _existing ?? new TodoItem();
        ReminderChanged = item.ReminderAt != reminder;
        item.Title = title;
        item.Note = note;
        item.Priority = PrioUrgent.IsChecked == true ? Priority.Acil
                      : PrioHigh.IsChecked == true ? Priority.Yuksek
                      : Priority.Normal;
        if (ReminderChanged)
        {
            // zaman değiştiyse tekrar çalabilmesi için "çaldı" bilgisini sıfırla
            item.ReminderAt = reminder;
            item.ReminderFired = false;
        }

        Result = item;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
