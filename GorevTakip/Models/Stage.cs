using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using GorevTakip.Helpers;

namespace GorevTakip.Models;

// bir görevin adımları. ör: "api düzeltilecek" görevine
// "endpointler düzeltildi" (yapıldı) -> "portlar check edilecek" (sıradaki) gibi
public class Stage : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }

    [JsonIgnore]
    public string DateText => IsDone && DoneAt is DateTime d
        ? "Yapıldı · " + Fmt.Friendly(d)
        : "Eklendi · " + Fmt.Friendly(CreatedAt);

    public void Toggle()
    {
        IsDone = !IsDone;
        DoneAt = IsDone ? DateTime.Now : null;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
