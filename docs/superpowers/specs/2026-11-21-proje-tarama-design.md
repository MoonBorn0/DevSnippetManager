# Projeden snippet çıkarma — tasarım

Tarih: 2026-11-21 · Durum: onay bekliyor

## Amaç ve neden

Kullanıcı bir öğrenci. Projelerindeki kodun büyük kısmını yapay zekâ yazıyor ve her parçasını bilmiyor. DevVault'u bir **öğrenme ve hatırlama aracı** olarak kullanmak istiyor: yapay zekânın yazdığı projeden fonksiyonları, sınıfları ve metotları ayrı snippet'ler olarak kasaya alıp her birinin **ne işe yaradığına** kolayca bakabilmek. Birden çok dil önemli, çünkü aynı mantık dilden dile farklı yazılıyor ve unutulduğunda doğru örneği hızlıca bulmak istiyor.

Bu yüzden asıl değer kodun kendisi değil, yanındaki **açıklama**. Kodun yanına açıklama eklenemezse özellik amacına ulaşmaz.

## Hedefler

- Kullanıcının seçtiği dosyalardan veya bir klasörden kod bloklarını (fonksiyon, metot, sınıf vb.) bulup snippet olarak **doğrudan** eklemek. Önizleme ya da seçim ekranı yok.
- Her proje kendi klasöründe dursun: `Folder` = proje adı.
- Açıklama iki kaynaktan gelsin:
  1. Kodun içindeki yorumdan (XML doc, docstring, JSDoc, bloğun hemen üstündeki yorum satırları). Uzun vadede kod Türkçe açıklama yorumlarıyla yazılacak ve açıklamalar buradan otomatik gelecek.
  2. Bir **"Yapay zekâya sor"** butonundan. Uygulama hiçbir yapay zekâya bağlanmaz. Buton, kodu ve hazır bir Türkçe soru metnini panoya kopyalar. Kullanıcı bunu istediği araca (ChatGPT, Claude, Gemini…) yapıştırır ve cevabı açıklama alanına yapıştırıp kaydeder.
- Aynı dosyalar tekrar tarandığında:
  - Değişen bloklar güncellenir ve eski hâli sürüm geçmişine düşer (mevcut özellik).
  - Yeni bloklar eklenir.
  - Kaynakta artık olmayan bloklar **silinmez**, "kaynakta yok" olarak işaretlenir.
  - Kullanıcının yazdığı açıklama **asla ezilmez**.
- Tarama bitince kısa bir özet gösterilir: "KodDepolama: 142 eklendi, 12 güncellendi, 3 kaynakta yok, 2 dosya atlandı".

## Hedef olmayanlar (bu sürümde yok)

- Klasörleri otomatik izleme (FileSystemWatcher). Kullanıcı bunu bilerek istemedi, tarama elle başlatılır.
- Uygulama içinden yapay zekâ API'sine bağlanma, API anahtarı ve ücret.
- Önizleme veya seçim ekranı.
- Kaynakta silinen blokların kasadan silinmesi.
- Aynı mantığın farklı dillerdeki karşılıklarını otomatik eşleştirme (ileride düşünülebilir).
- Yapılandırma ve işaretleme dosyalarından blok çıkarma: JSON, YAML, XML, Markdown, HTML, Dockerfile, CSS.

## Anti-hedefler (çalışsa bile başarısızlık sayılır)

- Kasanın gürültüyle dolması: üretilmiş dosyalar, `bin/obj/node_modules` içerikleri, tek satırlık getter'lar.
- Kullanıcının açıklamasının veya favori, kullanım sayısı gibi meta verilerinin yeniden taramada kaybolması.
- Kodun kullanıcının haberi olmadan bir yere gönderilmesi.
- Büyük bir projede taramanın arayüzü dondurması.

## Kısıtlar

- Mevcut yapı korunur: WPF, .NET 10, EF Core SQLite, CommunityToolkit.Mvvm, MaterialDesign 4.9. Eski veritabanları otomatik yükseltilir (`InitializeAsync`'teki ALTER/CREATE IF NOT EXISTS deseni).
- Mevcut davranışlar bozulmaz: kaydedilmemiş değişiklik uyarısı, sürüm geçmişi, içe aktarma, Gist, temalar.
- Testler `tests/DevVault.Tests` içinde. Yalıtım `DEVVAULT_DATA_DIR` ile yapılır, gerçek kullanıcı verisine dokunulmaz.

## Yaklaşımlar

1. **Her dil için kural tabanlı bulucu.** Metinleri ve yorumları atlayan bir ön tarayıcı kullanılır. Süslü parantezli dillerde blok sınırları `{}` dengesiyle, Python'da girintiyle, SQL'de `;`/`GO` ile bulunur. Ek bağımlılık gerektirmez ve her dilde çalışır, ama karmaşık durumlarda bloğu ara sıra yanlış kesebilir.
2. **Her dil için gerçek ayrıştırıcı (Roslyn + tree-sitter).** En doğru sonucu verir. Ancak tree-sitter her dil için yerel kütüphane ister ve .NET/Windows desteği zayıftır, kurulumu ve bakımı ağırdır.
3. **Karışık — SEÇİLEN.** C# için Roslyn kullanılır. Kullanıcının kodunun çoğu C# ve Roslyn kusursuz sonuç verir. Diğer diller için 1. yaklaşımdaki kural tabanlı bulucu kullanılır. Ağır bağımlılık olmadan en çok kullanılan dilde hata payı sıfıra iner. Kural tabanlı bulucu gerçek dosyalarla test edilerek zamanla iyileştirilir. Kullanıcı bu yaklaşımı onayladı.

## Kullanıcının verdiği kararlar

- Açıklama: kod yorumu + "Yapay zekâya sor" (panoya kopyala, elle yapıştır). API entegrasyonu yok.
- Tarama elle başlatılır. Kullanıcı dosyaları (veya bir klasörü) kendisi seçer. Otomatik izleme yok.
- Bulunan bloklar önizlemesiz, doğrudan eklenir.
- Kaynakta değişen veya silinen bloklar kasadan silinmez.
- Blok bulma yaklaşımı: 3 (Roslyn + kural tabanlı).

## Tasarım (Claude'un kararları — gerekirse düzeltilebilir)

### Veri modeli
`Snippet`'e eklenecek alanlar (eski veritabanları için ALTER ile):
- `SourceRoot`: proje kök klasörünün tam yolu.
- `SourceFile`: köke göre göreli dosya yolu.
- `SourceSymbol`: bloğun kimliği. Örnek: `DevSnippetManager.ViewModels.MainViewModel.SaveSnippetAsync()`. C#'ta aşırı yüklemeleri ayırmak için parametre tipleri de dahil edilir, diğer dillerde `Sınıf.metot` veya `fonksiyon`.
- `SourceComment`: koddan çıkarılan son yorum metni.
- `IsSourceMissing`: kaynakta artık yok (bool).
- `LastScannedAt`.

Bir bloğun kimliği `(SourceRoot, SourceFile, SourceSymbol)` üçlüsüdür. Elle eklenmiş snippet'lerde bu alanlar boştur ve tarama onlara hiç dokunmaz.

Aynı kimlik bir dosyada birden fazla kez bulunursa (örneğin aynı adlı iki yerel fonksiyon), sonuna `#2`, `#3` eklenir.

### Proje kökü ve adı
Kullanıcı dosya seçerse, seçilen dosyalardan yukarı doğru çıkılarak ilk `.git`, `*.sln`, `*.slnx`, `*.csproj`, `package.json`, `pyproject.toml`, `go.mod` veya `Cargo.toml` bulunan klasör kök sayılır. Hiçbiri bulunamazsa dosyanın klasörü kök olur. Kullanıcı bir klasör seçerse o klasör köktür. Proje adı kök klasörün adıdır ve `Folder` alanına yazılır.

### Hangi dosyalar taranır
- Uzantıdan dil eşlemesi `LanguageCatalog`'a eklenir: `.cs .java .js .jsx .mjs .ts .tsx .c .h .cpp .hpp .cc .go .rs .php .kt .swift .py .rb .ps1 .psm1 .sh .bash .sql`. Uzantı tanınmazsa dosya atlanır. Dil tahmini (`LanguageDetector`) yalnızca uzantı belirsizse yedek olarak kullanılır (örneğin `.h` → C mi C++ mı).
- Klasör taramasında atlanan klasörler: `bin obj node_modules .git .vs .idea dist build out packages target vendor __pycache__ .venv venv` ve `monaco` gibi vendored klasörler.
- Atlanan dosyalar: `*.g.cs`, `*.Designer.cs`, `*.min.js`, `*.generated.*` ve EF `Migrations` klasörü.
- 1 MB'tan büyük dosyalar ve ikili dosyalar (içinde NUL karakteri olan) atlanır. Bunlar özette "atlandı" olarak sayılır.

### Blok çıkarma
- **C# (Roslyn, `Microsoft.CodeAnalysis.CSharp`):** metotlar, constructor'lar, gövdeli property'ler, operatörler ve indexer'lar ayrı bloklardır. Local fonksiyonlar ayrı blok değildir, içinde bulundukları metodun parçasıdır.
  - Tiplerin kuralı:
    - enum, interface, record ve struct bütün olarak tek blok alınır. Üyeleri ayrıca alınmaz.
    - class asla bütün olarak alınmaz, üyeleri ayrı ayrı alınır. Tek istisna, çıkarılabilir hiç üyesi olmayan sınıftır (yalnızca alanları olan bir sınıf gibi); o sınıf bütün olarak alınır.
    - Bu kural satır sayısına bağlı değildir. Böylece sınıf büyüyüp küçüldükçe bloklar el değiştirmez.
    - İç içe tiplerde kimlik `Dış.İç.Üye` biçimindedir. `partial` sınıfların üyeleri hangi dosyadaysa o dosyaya aittir.
  - Yorum: üye üzerindeki XML doc yorumunun düz metni (`<summary>`, `<param>`, `<returns>`) okunur. XML doc yoksa hemen üstteki `//` satırları alınır.
  - Sözdizimi hatalı dosyalar Roslyn ile yine ayrıştırılır. Ama dosyada hata tanısı (diagnostic) varsa bu dosya için "eksik" işaretlemesi yapılmaz (bkz. Gözden geçirme sonrası kararlar, madde 8).
- **Diğer diller (kural tabanlı):** önce metinleri, karakter dizilerini ve yorumları maskeleyen bir ön tarayıcı çalışır. Ardından dil başına başlangıç kalıpları uygulanır:
  - **Süslü parantezli diller** (Java, JS/TS, C/C++, Go, Rust, PHP, Kotlin, Swift, PowerShell): fonksiyon, metot ve sınıf imzası bulunur, eşleşen `}` blok sonudur. Sınıfın içindeki metotlar `Sınıf.metot` adıyla alınır, sınıf kuralı C# ile aynıdır.
  - **Python ve Ruby:** `def` ve `class` ile başlar. Python'da blok girintiyle, Ruby'de `end` ile biter. Bloğun başındaki docstring yorum olarak alınır.
  - **Bash:** `name() {` ya da `function name` ile başlar.
  - **SQL:** `CREATE [OR ALTER] PROCEDURE|FUNCTION|VIEW|TRIGGER|TABLE` ile başlar, `GO` ya da bir sonraki `CREATE`'e kadar sürer.
  - Yorum olarak bloğun hemen üstündeki yorum satırları veya JSDoc `/** */` / `///` blokları alınır.
- Boş olmayan satır sayısı 3'ten az olan bloklar alınmaz.
- Kodun girintisi ortak en küçük girinti kadar azaltılır (CodePreview'deki mantık).

### Snippet alanlarının doldurulması
- **Title:** `Sınıf.Metot` veya `fonksiyon`. Tipler için tip adı.
- **Language:** dosyanın dili. **Folder:** proje adı. **Tags:** `proje-tarama`, blok türü (`metot`, `sınıf`, `fonksiyon`…) ve dosya adı.
- **Code:** blok metni (üstündeki yorum hariç).
- **Description:** ilk eklemede `SourceComment` (boş olabilir). Yeniden taramada Description yalnızca hâlâ eski `SourceComment`'e eşitse yenisiyle güncellenir. Kullanıcı açıklamayı değiştirdiyse dokunulmaz.

### Yeniden tarama
Aynı köke ait bir tarama sırasında, bulunan her blok kimliğiyle eşlenir:
- Kod, başlık veya yorum değiştiyse güncellenir. Mevcut sürüm geçmişi mekanizması eski hâli otomatik saklar.
- Hiçbir şey değişmediyse dokunulmaz.
- Daha önce eksik işaretlenmiş bir blok tekrar bulunursa işaret kaldırılır.
- Kasada bu kökten olup **taranan dosyalarda** bulunamayan bloklar `IsSourceMissing = true` yapılır. Taranmayan dosyalara dokunulmaz. Kaynakta tamamen silinmiş bir dosya taranan kümedeyse içindeki bloklar da eksik sayılır.
- Favori, kullanım sayısı ve Description (yukarıdaki kural dışında) korunur.

Kaydedilmemiş değişikliği olan açık snippet de veritabanında güncellenir, ama editör yeniden yüklenmez ve bir snackbar ile bildirilir ("Açık snippet kaynakta değişti; kaydederseniz tarama sonucu sürüm geçmişine düşer"). Tüm tarama tek bir işlemle (transaction) yazılır ve hata olursa hiçbir şey yazılmaz.

### Arayüz
- Kenar çubuğuna "Projeden ekle" butonu (ikon `FolderSearchOutline`). Tıklanınca iki seçenek sunulur: "Dosya seç…" (çoklu seçim) ve "Klasör seç…".
- Kasada en az bir taranmış proje varsa, klasör filtresi bir projeyi gösterirken "Projeyi yeniden tara" komutu çıkar. Bu komut o kökten daha önce taranmış dosyaları yeniden tarar. Kök klasör artık yoksa uyarı verir.
- Tarama arka planda çalışır. Bir ilerleme göstergesi ve "İptal" butonu olur. Bitince özet snackbar'da ve tepside gösterilir.
- Snippet editöründe kaynaktan gelen snippet'ler için bir bilgi satırı: "Kaynak: KodDepolama/ViewModels/MainViewModel.cs" ve "Dosyayı aç" bağlantısı (varsayılan editörde açar). Kaynakta yoksa "kaynakta artık yok" etiketi.
- Listede "kaynakta yok" snippet'leri soluk görünür. Filtre menüsüne "Kaynakta olmayanlar" seçeneği eklenir.
- Editörde **"Yapay zekâya sor"** butonu, panoya aşağıdaki metni kopyalar:

```
Aşağıdaki <dil> kodunu programlamayı öğrenen bir öğrenciye anlatır gibi Türkçe açıkla.
1) Ne işe yarar (1-2 cümle)  2) Nasıl çalışır (adım adım)  3) Kullandığı önemli kavramlar
4) Dikkat edilmesi gerekenler. Cevabı Markdown olarak ver.

Kaynak: <proje>/<dosya> — <sembol>
```<dil>
<kod>
```
```

  Ardından "Soru panoya kopyalandı — cevabı açıklama alanına yapıştırın" snackbar'ı gösterilir ve açıklama alanı düzenleme moduna geçer.

### Testler
`tests/DevVault.Tests` içinde:
- Dil başına çıkarıcı testleri, gerçekçi örnek dosyalarla (iç içe sınıflar, metin içinde `{`, yorum içinde `}`, şablon karakter dizileri, Python dekoratörleri, aşırı yüklemeler).
- Kimlik ve yeniden tarama: güncelleme, değişmeyenin dokunulmadan kalması, eksik işaretleme ve geri alma, açıklama koruma kuralı, meta veri koruma, transaction geri alma.
- Proje kökü bulma ve atlanan klasörler/dosyalar.
- DevVault'un kendi kaynak kodu üzerinde bir "duman testi": hiçbir istisna atmamalı ve makul sayıda blok çıkmalı.

## Gözden geçirme sonrası kararlar (Claude'un kararları)

1. **Dışa aktarma, içe aktarma, Gist ve yedek:**
   - `Source*` alanları, `IsSourceMissing` ve `LastScannedAt` JSON'a dahil edilir (kaynak bilgisi taşınır).
   - `ImportMerger`'da kimlik kuralı şöyledir: gelen snippet'in `SourceSymbol`'u doluysa eşleşme `(Folder, SourceFile, SourceSymbol)` üzerinden yapılır. `SourceRoot` kullanılmaz, çünkü başka bilgisayarda yol farklıdır. `SourceSymbol` boşsa mevcut (Başlık, Dil) kuralı geçerlidir.
   - Elle eklenmiş ve taranmış snippet'ler birbiriyle eşleşmez.
2. **Hangi alan kimin:**
   - Taramaya ait alanlar: `Code`, `Language`, `SourceComment`, `Source*`, `IsSourceMissing`, `LastScannedAt`.
   - `Title`, `Tags` ve `Folder` yalnızca ilk eklemede doldurulur, sonra kullanıcıya aittir ve tarama bunları değiştirmez.
   - `Description` için "eski yoruma eşitse güncelle" kuralı geçerlidir.
   - Favori ve kullanım sayısı hiç değiştirilmez.
3. **Yeniden adlandırma ve taşıma:**
   - Yeni bulunan bir blok, aynı projede eksik işaretlenecek bir bloğun koduyla **birebir aynıysa** onun devamı sayılır. Bunun için girinti ve satır sonları normalize edilmiş kodun özet değeri (hash) karşılaştırılır.
   - Bu durumda kimlik yeni ada veya dosyaya taşınır, açıklama ve meta veri korunur.
   - Kod da değiştiyse eşleşme kurulmaz: eski blok "kaynakta yok" kalır, yenisi eklenir. Açıklama eskisinde durur.
   - Daha bulanık bir eşleştirme bu sürümde yok.
4. **Aynı adlı bloklar ve isimsiz bloklar:**
   - Aynı kimlik bir dosyada birden fazla geçerse sıraya göre `#2`, `#3` eklenir. Araya yeni bir kopya girerse sahiplik kayabilir; bu kabul edilen bir sınırlamadır.
   - JS/TS'de bir değişkene atanmış ok veya anonim fonksiyon değişkenin adını alır (`const foo = () => {}` → `foo`).
   - `export default function` adı yoksa `default` adını alır.
   - Tamamen isimsiz bloklar alınmaz: callback'ler, IIFE'ler.
   - Python'da iç içe fonksiyonlar ayrı blok değildir. Dekoratörler bloğa dahil edilir.
5. **Depoya yazma:**
   - Yeni bir `ISnippetRepository.ApplyScanAsync(ScanResult)` metodu taramanın tamamını tek bir transaction içinde yazar.
   - Mevcut sürüm geçmişi mantığını yeniden kullanır: içerik değiştiyse eski hâl sürüm olarak saklanır, snippet başına en fazla 20 sürüm tutulur.
   - İçerik değişen snippet'te `UpdatedAt` güncellenir.
   - `IsSourceMissing` ve `LastScannedAt` değişiklikleri sürüm oluşturmaz.
   - Hata olursa transaction geri alınır ve change tracker temizlenir.
6. **Proje kimliği `SourceRoot`'tur** (normalize edilmiş, büyük-küçük harf duyarsız tam yol).
   - "Projeyi yeniden tara" klasör filtresine bağlı değildir. Ayrı bir "Taranan projeler" menüsünde her kök listelenir ve oradan yeniden taranır.
   - İki farklı kökün adı aynıysa yeni projenin `Folder`'ı `ad (üst-klasör)` biçiminde ayrıştırılır.
7. **Proje taşınırsa:**
   - "Taranan projeler" menüsünde kökü artık bulunamayan bir proje için "Konumu değiştir…" seçeneği vardır.
   - Yeni klasör seçilir ve o projenin snippet'lerinin `SourceRoot`'u güncellenir; göreli dosya yolları zaten aynıdır.
   - Yol karşılaştırmaları normalize ve büyük-küçük harf duyarsız yapılır.
8. **Okunamayan veya ayrıştırılamayan dosyalar** "atlandı" sayılır ve o dosyanın mevcut bloklarına **dokunulmaz** (eksik işaretlenmez). Sözdizimi hatası olan C# dosyası için de aynı kural uygulanır: bloklar eklenir ve güncellenir, ama eksik işaretlemesi yapılmaz.
9. **Etiketler** sabittir: `proje-tarama` ve blok türü (`metot`, `fonksiyon`, `sınıf`, `arayüz`, `kayıt`, `yapı`, `enum`, `özellik`, `kurucu`, `prosedür`). Kod içinde sabitler olarak tanımlanır.
10. **Diğer kararlar:**
    - "Tek satırlık getter" filtresi yalnızca "en az 3 dolu satır" kuralıyla sağlanır.
    - Tepside zaten bir balon bildirimi API'si var (Faz 4), o kullanılır.
    - Roslyn uygulamaya yaklaşık 10–15 MB ekler; bu kabul edildi.
    - Kendi kaynak kodu üzerindeki duman testi, depo kökünü `DevSnippetManager.slnx` dosyasını arayarak yukarı doğru bulur.

## Builder'a bırakılanlar
- Kural tabanlı bulucunun iç yapısı ve dil başına kalıpların tam hâli.
- İlerleme ve iptal mekanizmasının ayrıntısı.
- "Projeden ekle" seçim arayüzünün tam görünümü (menü veya iki buton).
