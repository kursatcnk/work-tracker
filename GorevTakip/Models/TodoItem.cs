using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
    public Priority Priority { get; set; } = Priority.Normal;
    public ObservableCollection<Stage> Stages { get; set; } = new();

    // aşağıdakiler json'a yazılmıyor, sadece ekranda göstermek için

    [JsonIgnore] public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    [JsonIgnore] public bool HasReminder => ReminderAt.HasValue;

    // çalmayı bekleyen hatırlatma: zamanı kurulmuş, henüz çalmamış, görev de açık
    [JsonIgnore] public bool IsReminderPending => ReminderAt.HasValue && !ReminderFired && !IsCompleted;

    [JsonIgnore] public bool IsOverdue => ReminderAt.HasValue && !IsCompleted && ReminderAt.Value <= DateTime.Now;

    [JsonIgnore] public string ReminderText => ReminderAt is DateTime r ? Fmt.Friendly(r) : "";
    [JsonIgnore] public string CreatedText => "Eklendi " + Fmt.Friendly(CreatedAt);
    [JsonIgnore] public string CompletedText => CompletedAt is DateTime c ? "Tamamlandı " + Fmt.Friendly(c) : "";

    [JsonIgnore]
    public string PriorityText => Priority switch
    {
        Priority.Acil => "ACİL",
        Priority.Yuksek => "YÜKSEK",
        _ => "",
    };

    [JsonIgnore] public bool HasStages => Stages.Count > 0;
    [JsonIgnore] public int DoneStageCount => Stages.Count(s => s.IsDone);
    [JsonIgnore] public string StageProgressText => $"{DoneStageCount}/{Stages.Count}";
    [JsonIgnore] public double StageProgress => Stages.Count == 0 ? 0 : (double)DoneStageCount / Stages.Count;

    // kartta ne gösterelim: yapılmamış ilk aşama varsa o, hepsi bittiyse en sonuncusu
    [JsonIgnore]
    public string CurrentStageText
    {
        get
        {
            var next = Stages.FirstOrDefault(s => !s.IsDone);
            if (next != null) return "Sıradaki: " + next.Text;
            var last = Stages.LastOrDefault();
            return last == null ? "" : "Son aşama: " + last.Text;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // tek tek property bildirmekle uğraşmamak için boş isimle hepsini yeniletiyoruz
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
