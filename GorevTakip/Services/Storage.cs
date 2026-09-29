using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using GorevTakip.Models;

namespace GorevTakip.Services;

public static class Storage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // türkçe karakterler ç diye kaçmasın, dosya elle açılınca okunabilsin
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DataDir { get; }
    public static string DataPath { get; }

    static Storage()
    {
        // veriyi exe'nin yanına yazıyoruz ki usb ile beraber taşınsın.
        // oraya yazılamıyorsa (program files, salt okunur usb vs.) appdata'ya düşüyor
        var dir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        if (!IsWritable(dir))
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GorevTakip");
        Directory.CreateDirectory(dir);
        DataDir = dir;
        DataPath = Path.Combine(dir, "gorevler.json");
    }

    public static List<TodoItem> Load()
    {
        if (!File.Exists(DataPath)) return new List<TodoItem>();
        try
        {
            return JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(DataPath), Options) ?? new List<TodoItem>();
        }
        catch (Exception ex)
        {
            // dosya bozuksa üstüne boş liste yazıp her şeyi kaybetmeyelim, kenara kopyalıyorum
            var broken = Path.Combine(DataDir, $"gorevler.bozuk-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try { File.Copy(DataPath, broken, true); } catch { }
            Log(ex);
            return new List<TodoItem>();
        }
    }

    public static void Save(IEnumerable<TodoItem> items)
    {
        var json = JsonSerializer.Serialize(items, Options);

        // önce tmp'ye yazıp sonra yer değiştiriyoruz. yazma sırasında usb çekilirse
        // en azından eski dosya sağlam kalıyor
        var tmp = DataPath + ".tmp";
        File.WriteAllText(tmp, json);
        if (!File.Exists(DataPath))
        {
            File.Move(tmp, DataPath);
            return;
        }
        try
        {
            File.Replace(tmp, DataPath, DataPath + ".bak");
        }
        catch
        {
            // fat32 usb'lerde File.Replace hata verebiliyor, elle yapıyoruz
            File.Copy(DataPath, DataPath + ".bak", true);
            File.Copy(tmp, DataPath, true);
            File.Delete(tmp);
        }
    }

    public static void Log(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(DataDir, "hata.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n"); }
        catch { }
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".yazma-testi");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
