# ESN Görev Takip

Kurulum gerektirmeyen, USB'den çift tıkla çalışan küçük bir görev ve hatırlatma uygulaması.
Müşteri takibi gibi "şu gün tekrar yazmam lazım" işleri unutmamak için yazıldı.

## Özellikler

- **Hatırlatıcı:** Zamanı gelince ekranın en önüne sesli, yanıp sönen bir uyarı çıkar. Buradan erteleyebilir (10 dk, 30 dk, 1 saat, yarın 09:00), tamamlayabilir ya da yeni tarih verebilirsin.
- **Kaçırılan hatırlatmalar:** Program kapalıyken zamanı geçen hatırlatmalar, program açıldığında "kaçırıldı" diye gösterilir.
- **Aşamalar:** Bir görevi adımlara bölebilirsin (ör. *API düzeltilecek* → *endpointler düzeltildi* ✓ → *portlar check edilecek*). Kartta ilerleme çubuğu ve sıradaki adım görünür.
- **Öncelik:** Normal / Yüksek / Acil. Acil işler listenin en üstünde durur ve renkli şeritle işaretlenir.
- **Sağ tık menüsü:** Tamamla, düzenle, aşama ekle, hatırlatıcı kur/kaldır, metni kopyala, sil.
- **Arama:** Başlık, not ve aşamalarda Türkçe karakter duyarlı arama.
- **Sistem tepsisi:** Pencereyi kapatınca program saatin yanındaki simgeye iner ve hatırlatmalar için çalışmaya devam eder.
- **Taşınabilir veri:** Tüm notlar exe'nin yanındaki `gorevler.json` dosyasına kaydedilir, USB ile birlikte taşınır.

## Kısayollar

| Tuş | İşlev |
| --- | --- |
| `Ctrl+N` | Yeni not |
| `Ctrl+F` | Ara |
| `Ctrl+E` | Seçili görevin aşamaları |
| `Ctrl+C` | Seçili görevi metin olarak kopyala |
| `Enter` / çift tık | Düzenle |
| `Del` | Sil |
| `Ctrl+Enter` | Düzenleme penceresinde kaydet, aşama penceresinde "yapıldı" olarak ekle |

## Geliştirme

Gereksinim: .NET 10 SDK (Windows)

```powershell
dotnet build
dotnet run --project GorevTakip
```

### USB için exe üretmek

```powershell
powershell -ExecutionPolicy Bypass -File tools\publish.ps1
```

Çıktı: `publish/GorevTakip.exe` (tek dosya, ~65 MB, .NET kurulumu gerektirmez).

### İkonu yeniden üretmek

```powershell
powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
```

## Proje yapısı

```
GorevTakip/
├─ Assets/      uygulama ikonu
├─ Controls/    ESN logosu
├─ Helpers/     tarih/saat biçimleme, koyu başlık çubuğu
├─ Models/      görev, aşama, öncelik
├─ Services/    json depolama, alarm sesi
├─ Themes/      koyu tema (renkler ve kontrol stilleri)
└─ Views/       ana pencere, düzenleme, aşamalar, alarm, onay pencereleri
tools/          yayınlama ve ikon scriptleri
```

## Notlar

- Hatırlatmalar yalnızca program açıkken çalışır. Pencereyi kapatmak programı kapatmaz; tamamen çıkmak için tepsideki simgeye sağ tıklayıp **Çıkış**'ı seç.
- Bazı şirket bilgisayarları imzasız exe'leri engelleyebilir (SmartScreen / AppLocker).
- Aynı anda yalnızca bir kopya çalışır; ikinci kez açmaya çalışınca açık olan pencere öne gelir.
