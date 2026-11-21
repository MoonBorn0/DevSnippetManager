# DevVault

DevVault, kod parçacıklarınızı (snippet) yerel **SQLite** veritabanında saklayan, **WPF + MaterialDesign** arayüzlü ve **Monaco Editor** destekli bir masaüstü kod parçacığı yöneticisidir. Harici veritabanı veya SQL Server kurulumu gerektirmez.

## Özellikler

- **Modern Arayüz:** WPF ve MaterialDesignThemes tabanlı sade ve duyarlı tasarım
- **Yerel Monaco Editor:** WebView2 üzerinde çalışan, uygulama ile birlikte gelen yerel Monaco Editor (çevrimdışı çalışır, sözdizimi renklendirmesi destekler)
- **Organizasyon ve Filtreleme:** Dil, klasör, çoklu etiket çipleri, favoriler ve sıralama seçenekleri (ada göre, son güncellenen, en yeni, en çok kullanılan)
- **Hızlı Arama ve Bulanık Eşleştirme (Fuzzy Search):** Herhangi bir uygulamadayken `Ctrl + Shift + Space` ile açılan hızlı arama penceresi; başlık, etiket, klasör, açıklama ve kod üzerinde bulanık arama yapar, eşleşen karakterleri vurgular, `Enter` ile panoya kopyalar ve `Ctrl + Enter` ile ana pencerede açar
- **Şablon Değişkenleri:** Kod parçacıklarında `${Ad}` ve varsayılan değerli `${Ad:varsayılan}` yer tutucuları tanımlanabilir; kopyalama sırasında değişken değerleri bir pencereyle sorulup doldurulur. Değişken olarak yorumlanmaması için `$${` kaçış dizisi kullanılır (çıktıda `${` olur)
- **Panodan Yeni Snippet (Otomatik Dil Algılama):** `Panodan` butonu veya `Ctrl + Shift + V` ile panodaki kodu otomatik dil algılamayla yeni snippet olarak oluşturma
- **Sistem Tepsisi (Tray) Desteği:** Pencere kapatıldığında uygulama arka planda sistem tepsisinde çalışmaya devam eder (`MinimizeToTray`); tepsi simgesine çift tıklayarak pencere açılır, sağ tık menüsüyle hızlı arama, yeni snippet, panodan ekleme, tepsiye küçültme ayarı ve çıkış yapılabilir
- **Panoya Kopyalama:** Editördeki kodu tek tıkla veya `Ctrl + Shift + C` ile panoya kopyalama (kullanım sayısını otomatik takip eder)
- **Kaydedilmemiş Değişiklik Uyarısı:** Farklı bir kayda geçerken, yeni kayıt oluştururken veya uygulamadan çıkarken kaydedilmemiş değişiklikler için onay istemi
- **Akıllı JSON İçe Aktarma ve Dışa Aktarma:** Üç farklı içe aktarma moduyla güvenli aktarım:
  - **Birleştir (varsayılan):** Aynı başlık (büyük/küçük harf duyarsız) ve dildeki kayıtlar içerik olarak farklıysa güncellenir, aynıysa atlanır; yeni kayıtlar eklenir.
  - **Kopyaları atla:** Mevcut kayıtlarla eşleşenler atlanır, yalnızca yeni kayıtlar eklenir.
  - **Tümünü değiştir:** Önce otomatik yedek alınır, ardından mevcut tüm kayıtlar silinerek dosyadaki kayıtlar eklenir.
- **Otomatik ve Manuel Yedekleme:** Belirlenen aralıklarla otomatik JSON yedeği alma, eski yedekleri otomatik temizleme, tek tıkla anlık yedek alma ve yedek klasörünü açma
- **Hazır Örnek Veriler:** İlk çalıştırmada örnek kod parçacıklarını otomatik olarak veritabanına ekler

## Sürüm geçmişi

Mevcut bir snippet'in başlık, dil, etiket, açıklama, klasör veya kod içeriği değiştirilip kaydedildiğinde (veya içe aktarmada birleştirme ile güncellendiğinde) önceki hali otomatik olarak **Sürüm Geçmişi**ne kaydedilir (snippet başına en fazla 20 sürüm saklanır; yalnızca favori/kullanım sayısı güncellemeleri sürüm oluşturmaz). Editör alt çubuğundaki **Geçmiş** butonuyla sürümleri satır bazlı fark (diff) görünümü ve *"Sadece değişiklikler"* filtresiyle inceleyebilir, seçtiğiniz bir sürümü tek tıkla editöre geri yükleyebilirsiniz.

## Projeden snippet çıkarma

Sol paneldeki **Projeden ekle** butonuyla seçtiğiniz kaynak dosyalarını veya bir proje klasörünü tarayarak metot, fonksiyon, sınıf, arayüz, kayıt, yapı, enum, özellik, kurucu ve SQL prosedürlerini doğrudan snippet olarak kasaya ekleyebilirsiniz:

- **Dil Desteği:** C# için Roslyn ayrıştırıcısı; Java, JavaScript, TypeScript, C, C++, Go, Rust, PHP, Kotlin, Swift, Python, Ruby, PowerShell, Bash ve SQL için kural tabanlı blok çıkarıcı.
- **Akıllı Yeniden Tarama:** *"Taranan projeler"* menüsünden mevcut projeleri yeniden tarayabilirsiniz. Değişen bloklar güncellenir (eski hâli sürüm geçmişine düşer), kullanıcı açıklamaları/başlıkları/etiketleri/favorileri korunur, yeniden adlandırılan veya taşınan aynı kodlu bloklar otomatik yeniden bağlanır, kaynakta artık bulunmayan bloklar silinmek yerine *"kaynakta artık yok"* olarak işaretlenir.
- **Yapay zekâya sor:** Editördeki **Yapay zekâya sor** butonu, mevcut kodu ve kaynak bilgisini öğrenci dostu bir Türkçe açıklama istemiyle panoya kopyalar ve açıklama alanını düzenleme moduna geçirir.

## Klavye Kısayolları

- **Global Kısayollar (uygulama tepside çalışırken de etkindir):**
  - `Ctrl + Shift + Space` : Hızlı arama penceresini aç/kapat
  - `Ctrl + Shift + S` : Pencereyi öne getirir ve yeni kod parçacığı oluşturur
- **Uygulama İçi Kısayollar:**
  - `Ctrl + S` : Kaydet
  - `Ctrl + N` : Yeni kod parçacığı
  - `Ctrl + Shift + V` : Panodan yeni snippet oluştur (metin kutusu veya editör dışındayken)
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
| `MinimizeToTray` | Pencere kapatıldığında sistem tepsisine küçült | `true` |
| `LastBackupAt` | Son yedekleme zamanı (otomatik güncellenir) | `null` |
| `CustomLanguages` | Kullanıcının eklediği özel programlama dilleri | `[]` |

> **Eski SQL Server sürümünden geçiş notu:** Önceki SQL Server tabanlı sürümü kullanıyorsanız, eski sürümde kayıtlarınızı JSON olarak dışa aktarın ve yeni SQLite sürümünde **İçe Aktar** özelliğiyle içeri aktarın.

## Derleme

Gereksinim: .NET 10 SDK (Windows).

Projeyi derlemek için:

```powershell
dotnet build DevSnippetManager.slnx
```

## Testler

Birim testlerini çalıştırmak için:

```powershell
dotnet test DevSnippetManager.slnx
```

Testler izole bir geçici `DEVVAULT_DATA_DIR` klasöründe çalışır ve `%APPDATA%\DevVault` verilerinize dokunmaz.
