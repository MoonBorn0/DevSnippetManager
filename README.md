# DevVault

DevVault, kod parçacıklarınızı (snippet) yerel **SQLite** veritabanında saklayan, **WPF + MaterialDesign** arayüzlü ve **Monaco Editor** destekli bir masaüstü kod parçacığı yöneticisidir. Harici veritabanı veya SQL Server kurulumu gerektirmez.

## Özellikler

- **Modern Arayüz:** WPF ve MaterialDesignThemes tabanlı sade ve duyarlı tasarım
- **Yerel Monaco Editor:** WebView2 üzerinde çalışan, uygulama ile birlikte gelen yerel Monaco Editor (çevrimdışı çalışır, sözdizimi renklendirmesi destekler)
- **Gelişmiş Arama:** Başlık, etiket, açıklama ve kod içeriğinde büyük/küçük harf duyarsız arama
- **Dile Göre Filtreleme:** C#, Java, Python, SQL, JavaScript, HTML/CSS ve Diğer dillerine göre hızlı filtreleme
- **Panoya Kopyalama:** Editördeki kodu tek tıkla veya `Ctrl + Shift + C` ile panoya kopyalama
- **Kaydedilmemiş Değişiklik Uyarısı:** Farklı bir kayda geçerken, yeni kayıt oluştururken veya pencereyi kapatırken kaydedilmemiş değişiklikler için onay istemi
- **Akıllı JSON İçe Aktarma ve Dışa Aktarma:** Üç farklı içe aktarma moduyla güvenli aktarım:
  - **Birleştir (varsayılan):** Aynı başlık (büyük/küçük harf duyarsız) ve dildeki kayıtlar içerik olarak farklıysa güncellenir, aynıysa atlanır; yeni kayıtlar eklenir.
  - **Kopyaları atla:** Mevcut kayıtlarla eşleşenler atlanır, yalnızca yeni kayıtlar eklenir.
  - **Tümünü değiştir:** Önce otomatik yedek alınır, ardından mevcut tüm kayıtlar silinerek dosyadaki kayıtlar eklenir.
- **Otomatik ve Manuel Yedekleme:** Belirlenen aralıklarla otomatik JSON yedeği alma, eski yedekleri otomatik temizleme, tek tıkla anlık yedek alma ve yedek klasörünü açma
- **Hazır Örnek Veriler:** İlk çalıştırmada örnek kod parçacıklarını otomatik olarak veritabanına ekler

## Klavye Kısayolları

- **Global Kısayol (uygulama çalışırken):**
  - `Ctrl + Shift + S` : Pencereyi öne getirir ve yeni kod parçacığı oluşturur
- **Uygulama İçi Kısayollar:**
  - `Ctrl + S` : Kaydet
  - `Ctrl + N` : Yeni kod parçacığı
  - `Ctrl + F` : Arama kutusuna odaklan
  - `Ctrl + Shift + C` : Kodu panoya kopyala

## Veri Saklama, Ayarlar ve Yedekler

Uygulama SQL Server gerektirmez; tüm veriler ve ayarlar kullanıcı profilinde saklanır:

- **Veritabanı (SQLite):** `%APPDATA%\DevVault\devvault.db`
- **Otomatik Yedekler:** `%APPDATA%\DevVault\Backups` (`devvault-yedek-yyyyMMdd-HHmmss.json`)
- **Ayar Dosyası:** `%APPDATA%\DevVault\settings.json`

`settings.json` içinde desteklenen anahtarlar:

| Anahtar | Açıklama | Varsayılan |
| --- | --- | --- |
| `DatabasePath` | SQLite veritabanı dosya yolu | `%APPDATA%\DevVault\devvault.db` |
| `BackupFolder` | Otomatik ve manuel yedeklerin kaydedileceği klasör | `%APPDATA%\DevVault\Backups` |
| `BackupIntervalHours` | Otomatik yedekleme aralığı (saat; `0` veya negatif ise kapalı) | `24` |
| `MaxBackups` | Saklanacak en fazla yedek dosyası sayısı | `10` |
| `LastBackupAt` | Son yedekleme zamanı (otomatik güncellenir) | `null` |

> **Eski SQL Server sürümünden geçiş notu:** Önceki SQL Server tabanlı sürümü kullanıyorsanız, eski sürümde kayıtlarınızı JSON olarak dışa aktarın ve yeni SQLite sürümünde **İçe Aktar** özelliğiyle içeri aktarın.

## Derleme

Gereksinim: .NET 10 SDK (Windows).

Projeyi derlemek için:

```powershell
dotnet build KodDepolama\DevSnippetManager.csproj
```
