using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GorevTakip.Helpers;
using GorevTakip.Models;
using GorevTakip.Services;

namespace GorevTakip.Views;

public enum AlarmAction
{
    None,       // bir şey seçilmeden kapandı, "Kapat" gibi davranıyoruz
    Silent,     // program kendisi kapattı (görev silindi/düzenlendi vs.), göreve dokunma
    Complete,
    Snooze,
    Edit,
}

public partial class AlarmWindow : Window
{
    private readonly int _index;

    public AlarmAction Action { get; private set; } = AlarmAction.None;
    public DateTime SnoozeUntil { get; private set; }

    public AlarmWindow(TodoItem item, int index)
    {
        InitializeComponent();
        _index = index;

        TitleText.Text = item.Title;
        Title = "Hatırlatma: " + item.Title;
        NoteText.Text = item.Note;
        NoteBorder.Visibility = item.HasNote ? Visibility.Visible : Visibility.Collapsed;
        if (item.HasLabel)
        {
            var (bg, fg) = Labels.BrushesFor(item.Label);
            LabelBorder.Background = bg;
            LabelText.Foreground = fg;
            LabelText.Text = item.Label;
        }
        else
        {
            // etiket yoksa başlık eski yerine otursun
            LabelBorder.Visibility = Visibility.Collapsed;
            TitleText.Margin = new Thickness(0, 18, 0, 0);
        }

        StageText.Text = $"{item.StageProgressText} · {item.CurrentStageText}";
        StageBorder.Visibility = item.HasStages ? Visibility.Visible : Visibility.Collapsed;

        // program kapalıyken zamanı geçmişse bunu belli edelim
        var at = item.ReminderAt ?? DateTime.Now;
        TimeText.Text = (DateTime.Now - at).TotalMinutes >= 2
            ? $"{Fmt.Friendly(at)} için kurulmuştu (kaçırıldı)"
            : Fmt.Friendly(at);

        // çerçevesiz pencere, boş bir yerinden tutup sürüklenebilsin
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        };
        Loaded += OnLoaded;
        Closed += (_, _) => AlarmSound.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // aynı anda birkaç alarm çıkarsa üst üste binmesin, biraz kaydır
        if (_index > 0)
        {
            Left += _index * 28;
            Top += _index * 28;
        }

        AlarmSound.Start();

        Glow.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.15, TimeSpan.FromSeconds(0.8))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        });

        // zil sallanma animasyonu: kısa sallanıp bir süre duruyor
        var shake = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(16, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-16, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));
        BellRotate.BeginAnimation(RotateTransform.AngleProperty, shake);

        Activate();
    }

    public void CloseWith(AlarmAction action)
    {
        Action = action;
        Close();
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        var now = Fmt.TrimSeconds(DateTime.Now);
        SnoozeUntil = (((Button)sender).Tag as string) switch
        {
            "10" => now.AddMinutes(10),
            "30" => now.AddMinutes(30),
            "60" => now.AddHours(1),
            _ => DateTime.Today.AddDays(1).AddHours(9),
        };
        CloseWith(AlarmAction.Snooze);
    }

    private void Complete_Click(object sender, RoutedEventArgs e) => CloseWith(AlarmAction.Complete);
    private void Dismiss_Click(object sender, RoutedEventArgs e) => CloseWith(AlarmAction.None);
    private void Edit_Click(object sender, RoutedEventArgs e) => CloseWith(AlarmAction.Edit);

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        AlarmSound.Silence();
        MuteIcon.Text = "";
        MuteButton.ToolTip = "Ses kapatıldı";
    }
}
