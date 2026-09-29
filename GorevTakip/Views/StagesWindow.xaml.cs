using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GorevTakip.Helpers;
using GorevTakip.Models;

namespace GorevTakip.Views;

// görevin aşamalarını doğrudan item.Stages üzerinde değiştiriyoruz,
// kaydetme işi pencere kapanınca ana pencerede yapılıyor
public partial class StagesWindow : Window
{
    private readonly TodoItem _item;

    public bool Changed { get; private set; }
    public bool CompleteRequested { get; private set; }

    public StagesWindow(TodoItem item)
    {
        InitializeComponent();
        _item = item;
        SourceInitialized += (_, _) => NativeMethods.UseDarkTitleBar(this);

        TaskTitle.Text = item.Title;
        StageList.ItemsSource = item.Stages;
        UpdateSummary();

        Loaded += (_, _) => NewStageBox.Focus();
    }

    private void UpdateSummary()
    {
        int total = _item.Stages.Count;
        int done = _item.Stages.Count(s => s.IsDone);

        SummaryText.Text = total == 0 ? "Henüz aşama eklenmedi" : $"{done} / {total} aşama tamamlandı";
        Bar.Value = total == 0 ? 0 : (double)done / total;
        EmptyText.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;

        // her şey bittiyse ve görev hala açıksa görevi kapatmayı önerelim
        AllDoneBanner.Visibility = total > 0 && done == total && !_item.IsCompleted
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void AddStage(bool done)
    {
        var text = NewStageBox.Text.Trim();
        if (text.Length == 0)
        {
            NewStageBox.Focus();
            return;
        }

        var stage = new Stage { Text = text };
        if (done)
        {
            stage.IsDone = true;
            stage.DoneAt = DateTime.Now;
        }
        _item.Stages.Add(stage);
        Changed = true;

        NewStageBox.Clear();
        NewStageBox.Focus();
        UpdateSummary();
        Scroller.ScrollToEnd();
    }

    private void NewStageBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddStage(done: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }

    private void AddNext_Click(object sender, RoutedEventArgs e) => AddStage(done: false);
    private void AddDone_Click(object sender, RoutedEventArgs e) => AddStage(done: true);

    private void StageToggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Stage stage) return;
        stage.Toggle();
        Changed = true;
        UpdateSummary();
    }

    private void StageDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Stage stage) return;
        _item.Stages.Remove(stage);
        Changed = true;
        UpdateSummary();
    }

    private void CompleteTask_Click(object sender, RoutedEventArgs e)
    {
        CompleteRequested = true;
        Close();
    }
}
