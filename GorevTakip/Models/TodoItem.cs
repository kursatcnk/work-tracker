using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using GorevTakip.Helpers;

namespace GorevTakip.Models;

public class TodoItem : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReminderAt { get; set; }
    public bool ReminderFired { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }

    // aşağıdakiler json'a yazılmıyor, sadece ekranda göstermek için

    [JsonIgnore] public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    [JsonIgnore] public bool HasReminder => ReminderAt.HasValue;

    // çalmayı bekleyen hatırlatma: zamanı kurulmuş, henüz çalmamış, görev de açık
    [JsonIgnore] public bool IsReminderPending => ReminderAt.HasValue && !ReminderFired && !IsCompleted;

    [JsonIgnore] public bool IsOverdue => ReminderAt.HasValue && !IsCompleted && ReminderAt.Value <= DateTime.Now;

    [JsonIgnore] public string ReminderText => ReminderAt is DateTime r ? Fmt.Friendly(r) : "";
    [JsonIgnore] public string CreatedText => "Eklendi " + Fmt.Friendly(CreatedAt);
    [JsonIgnore] public string CompletedText => CompletedAt is DateTime c ? "Tamamlandı " + Fmt.Friendly(c) : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    // tek tek property bildirmekle uğraşmamak için boş isimle hepsini yeniletiyoruz
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
