using System;
using System.IO;
using System.Media;
using System.Windows.Threading;

namespace GorevTakip.Services;

// aynı anda birden fazla alarm penceresi açık olabiliyor. her biri kendi sesini çalarsa
// birbirini kesiyor, o yüzden ses tek yerden sayaçla yönetiliyor
public static class AlarmSound
{
    // masadan kalkmışsan sonsuza kadar çalmasın, pencere zaten ekranda duruyor
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(2);

    private static int _count;
    private static SoundPlayer? _player;
    private static DispatcherTimer? _beeper;
    private static DispatcherTimer? _limit;

    public static void Start()
    {
        if (_count++ > 0) return;
        Play();
        _limit = new DispatcherTimer { Interval = MaxDuration };
        _limit.Tick += (_, _) => Silence();
        _limit.Start();
    }

    public static void Stop()
    {
        if (_count == 0 || --_count > 0) return;
        Silence();
    }

    public static void Silence()
    {
        _limit?.Stop(); _limit = null;
        _beeper?.Stop(); _beeper = null;
        if (_player != null) { _player.Stop(); _player.Dispose(); _player = null; }
    }

    private static void Play()
    {
        try
        {
            var wav = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Alarm01.wav");
            if (File.Exists(wav))
            {
                _player = new SoundPlayer(wav);
                _player.PlayLooping();
                return;
            }
        }
        catch { }

        // alarm sesi silinmiş/kısıtlı makinelerde en azından sistem sesi çalsın
        SystemSounds.Exclamation.Play();
        _beeper = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _beeper.Tick += (_, _) => SystemSounds.Exclamation.Play();
        _beeper.Start();
    }
}
