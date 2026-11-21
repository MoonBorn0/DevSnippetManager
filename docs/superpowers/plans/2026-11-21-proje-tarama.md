# Projeden Snippet Çıkarma — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Bu projede yürütme:** Görevleri Claude yazmaz, dağıtır. Görev 2 → Codex (küçük, UI'siz), diğerleri → Antigravity, Görev 10 → Sonnet. Ajanlar git komutu çalıştırmaz. "Commit" adımlarını koordinatör (Claude) derleme ve testleri doğruladıktan sonra yapar.

**Goal:** Kullanıcının seçtiği dosya/klasörlerden kod bloklarını (metot, fonksiyon, tip…) bulup snippet olarak eklemek, yeniden taramada güncellemek; açıklamayı kod yorumundan ve "Yapay zekâya sor" panosu akışından almak.

**Architecture:** UI'siz bir `Services/ProjectScan/` katmanı: dosya bulma (ProjectLocator) → dil başına blok çıkarma (Roslyn C# + kural tabanlı diğerleri) → saf bir uzlaştırıcı (ScanReconciler) mevcut snippet'lerle karşılaştırıp bir `ScanPlan` üretir → depo tek transaction ile uygular (`ApplyScanAsync`) → `ProjectScanService` hepsini arka planda, ilerleme/iptal ile yönetir. Arayüz bu servisi çağırır.

**Tech Stack:** .NET 10 WPF, EF Core 10 SQLite, CommunityToolkit.Mvvm 8.4, MaterialDesign 4.9, Microsoft.CodeAnalysis.CSharp (yeni), xUnit.

**Spec:** `docs/superpowers/specs/2026-11-21-proje-tarama-design.md`

## Global Constraints

- Tüm yeni tipler `DevSnippetManager.Services.ProjectScan` namespace'inde, UI tipleri hariç.
- WPF `_wpftmp` derlemesinde implicit using yok — her dosyada açık `using`.
- Verbatim string içinde tırnak `""` — asla `\"`.
- Kullanıcıya görünen metinler Türkçe; UTF-8 korunur.
- Eski veritabanları otomatik yükseltilir (mevcut `EnsureOrganizationColumnsAsync` deseni: `PRAGMA table_info` + `ALTER TABLE ADD COLUMN`).
- Testler `tests/DevVault.Tests`; yalıtım `DEVVAULT_DATA_DIR` ile (mevcut `TestEnvironmentInitializer`); gerçek `%APPDATA%\DevVault`'a dokunulmaz.
- Doğrulama komutu her görevde: `dotnet build DevSnippetManager.slnx` (0 hata) ve `dotnet test DevSnippetManager.slnx` (hepsi geçer).
- Sabit etiketler: `proje-tarama`, ve blok türü: `metot`, `fonksiyon`, `sınıf`, `arayüz`, `kayıt`, `yapı`, `enum`, `özellik`, `kurucu`, `prosedür`.
- Atlanan klasörler: `bin obj node_modules .git .vs .idea dist build out packages target vendor __pycache__ .venv venv monaco`. Atlanan dosyalar: `*.g.cs *.Designer.cs *.min.js *.generated.*`, `Migrations` klasörü, > 1 MB, NUL içeren.
- En az 3 dolu satırdan kısa bloklar alınmaz.
- Sürüm sınırı mevcut `SnippetRepository.MaxVersionsPerSnippet` (20).

## Review Focus

1. Metin/yorum/karakter/şablon dizgisi içindeki `{` `}` (C#'ta `$"{x}"`, JS'te `` `${a}` ``, regex literal) blok sınırını bozmamalı → Görev 4 testleri `Extract_IgnoresBracesInStringsAndComments`.
2. Aynı dosyaları iki kez taramak sıfır değişiklik ve sıfır yeni sürüm üretmeli → Görev 5 `Reconcile_SecondScanIsNoOp`, Görev 6 `ApplyScan_RescanCreatesNoVersions`.
3. Kullanıcının değiştirdiği Title/Tags/Folder/Description yeniden taramada korunmalı → Görev 5 `Reconcile_PreservesUserOwnedFields`.
4. Binlerce dosyalı klasör arayüzü dondurmamalı; iptal veritabanını değiştirmemeli → Görev 8 `Scan_CancelledLeavesDatabaseUnchanged`.
5. UTF-8 BOM, CRLF ve Türkçe Windows-1254 kodlu dosyalar doğru okunmalı (bozuk karakter değil) → Görev 8 `ReadSource_DetectsEncoding`.

---

### Task 1: Snippet kaynak alanları, şema yükseltme, JSON  *(Antigravity)*

**Files:**
- Modify: `KodDepolama/Snippet.cs`, `KodDepolama/Data/AppDbContext.cs`, `KodDepolama/Data/SnippetRepository.cs` (`EnsureOrganizationColumnsAsync`)
- Test: `tests/DevVault.Tests/SnippetRepositoryTests.cs`

**Interfaces:**
- Produces: `Snippet` üzerinde `string SourceRoot = ""`, `string SourceFile = ""`, `string SourceSymbol = ""`, `string SourceComment = ""`, `bool IsSourceMissing`, `DateTime? LastScannedAt`, ve `[NotMapped, JsonIgnore] bool IsFromSource => SourceSymbol.Length > 0`. Kaynak alanları JSON'a dahildir (JsonIgnore yok).

- [ ] **Step 1: Failing testler:** `SchemaUpgrade_AddsSourceColumns` (eski şemalı DB → InitializeAsync → 6 kolon var, eski satırlar boş/false/NULL), `SourceFields_RoundTripThroughSaveAndJson` (kaydet-yükle ve `JsonSerializer` gidiş-dönüş aynı değerler).
- [ ] **Step 2:** `dotnet test --filter FullyQualifiedName~SnippetRepositoryTests` → FAIL.
- [ ] **Step 3:** Alanları ekle; ALTER kolonları: `SourceRoot TEXT NOT NULL DEFAULT ''`, `SourceFile TEXT NOT NULL DEFAULT ''`, `SourceSymbol TEXT NOT NULL DEFAULT ''`, `SourceComment TEXT NOT NULL DEFAULT ''`, `IsSourceMissing INTEGER NOT NULL DEFAULT 0`, `LastScannedAt TEXT NULL`; `(SourceRoot, SourceFile, SourceSymbol)` üzerine index.
- [ ] **Step 4:** build + test → PASS.
- [ ] **Step 5: Commit** (koordinatör): `feat: snippet kaynak alanları ve şema yükseltme`.

### Task 2: Tarama modelleri ve ProjectLocator  *(Codex)*

**Files:**
- Create: `KodDepolama/Services/ProjectScan/ScanModels.cs`, `KodDepolama/Services/ProjectScan/ProjectLocator.cs`
- Test: `tests/DevVault.Tests/ProjectLocatorTests.cs`

**Interfaces:**
- Produces (ScanModels.cs):
  - `enum BlockKind { Method, Function, Class, Interface, Record, Struct, Enum, Property, Constructor, Procedure }`
  - `static class BlockKindTags { const string ScanTag = "proje-tarama"; static string ToTag(BlockKind k); }` — eşleme Global Constraints'teki sıra ile (Method→metot, Function→fonksiyon, Class→sınıf, Interface→arayüz, Record→kayıt, Struct→yapı, Enum→enum, Property→özellik, Constructor→kurucu, Procedure→prosedür).
  - `sealed record ExtractedBlock(string Symbol, string DisplayName, BlockKind Kind, string Code, string Comment, int StartLine)`
  - `sealed record ExtractionResult(IReadOnlyList<ExtractedBlock> Blocks, bool HasErrors)`
  - `interface IBlockExtractor { bool CanHandle(string language); ExtractionResult Extract(string source, string language); }`
  - `sealed record ScannedFile(string RelativePath, string Language, ExtractionResult? Result, bool Exists)` — `Result == null && Exists` = okunamadı/atlandı; `!Exists` = dosya silinmiş.
  - `static class CodeNormalizer { static string Dedent(string code); static string ContentHash(string code); }` — Dedent: ortak en küçük girintiyi kaldırır, satır sonlarını `\n` yapar, sondaki boşlukları kırpar; ContentHash: Dedent sonucu üzerinde SHA-256 hex.
- Produces (ProjectLocator.cs, static):
  - `string NormalizePath(string path)` — tam yol, `\` ayraç, sonda ayraç yok.
  - `bool PathEquals(string a, string b)` — normalize + OrdinalIgnoreCase.
  - `string FindProjectRoot(string filePath)` — yukarı doğru ilk `.git` (klasör/dosya), `*.sln`, `*.slnx`, `*.csproj`, `package.json`, `pyproject.toml`, `go.mod`, `Cargo.toml`; yoksa dosyanın klasörü.
  - `IReadOnlyList<string> EnumerateSourceFiles(string folder)` — Global Constraints'teki atlamalarla, desteklenen uzantılar.
  - `bool ShouldSkipFile(string path)` — ad kalıpları ve `Migrations` klasörü (boyut/NUL kontrolü Görev 8'de okuma sırasında).
  - `string? LanguageFromPath(string path)` — `.cs`→C#, `.java`→Java, `.js .jsx .mjs .cjs`→JavaScript, `.ts .tsx`→TypeScript, `.c`→C, `.h`→C (içerikte `class`/`std::`/`template` varsa C++ — bu karar Görev 8'de içerikle; burada C), `.cpp .hpp .cc .cxx`→C++, `.go`→Go, `.rs`→Rust, `.php`→PHP, `.kt .kts`→Kotlin, `.swift`→Swift, `.py`→Python, `.rb`→Ruby, `.ps1 .psm1`→PowerShell, `.sh .bash`→Bash, `.sql`→SQL; diğerleri null.
  - `string ProjectFolderName(string root, IReadOnlyCollection<(string Root, string Folder)> existing)` — kök klasör adı; aynı adı farklı bir kök kullanıyorsa `ad (üst-klasör)`.

- [ ] **Step 1: Failing testler:** `FindProjectRoot_StopsAtCsprojOrGit`, `FindProjectRoot_FallsBackToFileFolder`, `EnumerateSourceFiles_SkipsBinObjNodeModulesAndGenerated`, `LanguageFromPath_MapsAllExtensions` (Theory, yukarıdaki tablo), `ProjectFolderName_DisambiguatesSameName` (`"src"` başka kökle var → `"src (KodDepolama)"`), `PathEquals_IgnoresCaseAndTrailingSlash`, `Dedent_RemovesCommonIndentAndNormalizesNewlines`, `ContentHash_SameForCrlfAndLf`, `BlockKindTags_AllKindsHaveTurkishTag`. Test klasörleri `Path.GetTempPath()` altında oluşturulup silinir.
- [ ] **Step 2:** test → FAIL. **Step 3:** uygula. **Step 4:** build + test → PASS.
- [ ] **Step 5: Commit** (koordinatör): `feat: tarama modelleri ve proje bulucu`.

### Task 3: C# blok çıkarıcı (Roslyn)  *(Antigravity)*

Koordinatör önce `Microsoft.CodeAnalysis.CSharp` paketini (kararlı güncel sürüm) uygulama projesine ekler.

**Files:**
- Create: `KodDepolama/Services/ProjectScan/CSharpBlockExtractor.cs`
- Test: `tests/DevVault.Tests/CSharpBlockExtractorTests.cs`

**Interfaces:**
- Consumes: Görev 2 tipleri, `CodeNormalizer.Dedent`.
- Produces: `sealed class CSharpBlockExtractor : IBlockExtractor` (`CanHandle("C#")`).
  - Symbol: `Namespace.Tip[.İçTip].Üye(ParamTip1,ParamTip2)`; ctor üye adı `.ctor`; property `Ad` (parametresiz); tipler `Namespace.Tip`. Tekrar eden Symbol'e `#2`, `#3`.
  - DisplayName: `Tip.Üye` (namespace'siz); tipler için `Tip`.
  - Code: üyenin metni (leading trivia'daki yorumlar hariç), Dedent edilmiş. Comment: XML doc düz metni (`summary`, sonra `param` satırları `- ad: metin`, `returns` → `Döndürür: metin`); yoksa hemen üstteki ardışık `//` satırları.
  - Kurallar: spec "Blok çıkarma — C#" bölümü (class asla bütün değil, üyesiz class bütün; enum/interface/record/struct bütün ve üyeleri ayrıca alınmaz; local fonksiyonlar ayrı değil; gövdesiz/abstract üyeler alınmaz; < 3 dolu satır alınmaz).
  - `HasErrors = tree.GetDiagnostics().Any(d => d.Severity == Error)`.

- [ ] **Step 1: Failing testler:** `Extract_MethodsCtorsPropertiesAsSeparateBlocks`, `Extract_OverloadsGetParameterTypesInSymbol`, `Extract_ClassWithoutMembersTakenWhole`, `Extract_EnumInterfaceRecordStructWholeOnly`, `Extract_NestedTypesUseDottedSymbol`, `Extract_LocalFunctionStaysInsideParent`, `Extract_XmlDocBecomesComment`, `Extract_LineCommentsAboveBecomeComment`, `Extract_SkipsBlocksShorterThan3Lines`, `Extract_SyntaxErrorSetsHasErrors`, `Extract_IgnoresBracesInStringsAndComments` (C# `$"{a}"`, `@"}"`, `/* } */`).
- [ ] **Step 2:** FAIL. **Step 3:** uygula. **Step 4:** PASS.
- [ ] **Step 5: Commit** (koordinatör): `feat: Roslyn ile C# blok çıkarıcı`.

### Task 4: Kural tabanlı çıkarıcı (diğer diller)  *(Antigravity)*

**Files:**
- Create: `KodDepolama/Services/ProjectScan/SourceMasker.cs`, `KodDepolama/Services/ProjectScan/RuleBasedBlockExtractor.cs`
- Test: `tests/DevVault.Tests/RuleBasedBlockExtractorTests.cs`, örnek dosyalar `tests/DevVault.Tests/TestData/scan/`

**Interfaces:**
- Produces:
  - `static class SourceMasker { static string Mask(string source, string language); }` — aynı uzunlukta metin döner; karakter dizileri, karakter literal'leri, yorumlar (ve JS/TS şablon dizgileri ile regex literal'leri, Python üçlü tırnaklar, SQL `--`/`/* */`, PowerShell `<# #>`/`#`, Bash/Ruby `#`) boşlukla değiştirilir, satır sonları korunur. Böylece indeksler orijinal metinle birebir eşleşir.
  - `sealed class RuleBasedBlockExtractor : IBlockExtractor` — `CanHandle` dili: Java, JavaScript, TypeScript, C, C++, Go, Rust, PHP, Kotlin, Swift, Python, Ruby, PowerShell, Bash, SQL.
  - Symbol: `Sınıf.ad` veya `ad`; tekrarlara `#2`; DisplayName = Symbol (`#n` hariç). Kurallar spec "Diğer diller" bölümü ve "Gözden geçirme sonrası kararlar" madde 4 (değişkene atanmış ok/anonim fonksiyon değişken adını alır; `export default` isimsiz → `default`; isimsiz callback/IIFE alınmaz; Python iç içe fonksiyonlar ayrı değil, dekoratör bloğa dahil; sınıflar bütün değil, metotları `Sınıf.metot`, metotsuz sınıf bütün). Comment: bloğun hemen üstündeki yorum satırları / JSDoc / `///`; Python'da docstring.
  - `HasErrors`: süslü parantez dengesi tutmazsa true.

- [ ] **Step 1: Failing testler** (her biri TestData'daki gerçekçi bir örnekle, beklenen Symbol listesi ve bir bloğun tam Code/Comment'i ile): `Java_ClassMethodsAndOverloads`, `JavaScript_FunctionsArrowsClassesExportDefault`, `TypeScript_InterfacesTypedMethods`, `C_FunctionsAndStructs`, `Cpp_ClassMethodsAndNamespaces`, `Go_FuncsAndMethodsWithReceivers` (`(s *Server) Start` → `Server.Start`), `Rust_FnImplBlocks`, `Php_ClassMethods`, `Python_DefClassDecoratorsDocstringIndent`, `Ruby_DefClassEnd`, `PowerShell_Functions`, `Bash_Functions`, `Sql_ProceduresFunctionsViewsUntilGo`, `Extract_IgnoresBracesInStringsAndComments` (JS `` `${a}}` ``, `"}"`, `// }`, `/re}/`), `Extract_UnbalancedBracesSetsHasErrors`, `Extract_AnonymousCallbacksNotExtracted`.
- [ ] **Step 2:** FAIL. **Step 3:** uygula. **Step 4:** PASS.
- [ ] **Step 5: Commit** (koordinatör): `feat: kural tabanlı blok çıkarıcı`.

### Task 5: ScanReconciler (saf uzlaştırma mantığı)  *(Antigravity)*

**Files:**
- Create: `KodDepolama/Services/ProjectScan/ScanReconciler.cs`
- Test: `tests/DevVault.Tests/ScanReconcilerTests.cs`

**Interfaces:**
- Consumes: Görev 1 `Snippet` alanları; Görev 2 `ScannedFile`, `ExtractedBlock`, `BlockKindTags`, `CodeNormalizer`.
- Produces:
  - `sealed class ScanPlan { List<Snippet> ToInsert; List<Snippet> ToUpdate; List<int> MarkMissing; List<int> ClearMissing; List<int> Touch /* yalnız LastScannedAt */; int Unchanged; int Relinked; int SkippedFiles; }` — `ToUpdate` öğeleri yeni değerleri taşıyan **ayrık** (detached) Snippet nesneleri, `Id` dolu.
  - `static ScanPlan Reconcile(string root, string projectFolder, IReadOnlyList<ScannedFile> files, IReadOnlyList<Snippet> existingForRoot, DateTime now)`.
- Kurallar (spec "Yeniden tarama" + "Gözden geçirme sonrası kararlar" 2, 3, 8):
  - Eşleme anahtarı `(SourceFile, SourceSymbol)`, büyük/küçük harf duyarsız dosya yolu.
  - Yeni blok → Insert: Title=DisplayName, Language, Folder=projectFolder, Tags=`proje-tarama, <tür>, <dosya adı>`, Code, Description=Comment, SourceComment=Comment, Source*, LastScannedAt=now.
  - Eşleşen blok: Code/Language/SourceComment değiştiyse Update (Title/Tags/Folder/favori/kullanım kopyalanır, değiştirilmez); Description yalnızca eski SourceComment'e eşitse yeni Comment olur; IsSourceMissing true ise ClearMissing. Değişmediyse Touch + Unchanged.
  - Relink: taranan dosyalarda bulunamayan bir mevcut bloğun `ContentHash`'i, eşleşmeyen yeni bir bloğun hash'ine eşitse → Insert yerine Update (yeni SourceFile/SourceSymbol, açıklama ve meta korunur), Relinked++.
  - Kalan bulunamayanlar: dosyası taranan kümede ve (`Result != null && !HasErrors` veya `!Exists`) ise MarkMissing; dosya atlandıysa (`Result == null && Exists`) ya da `HasErrors` ise dokunulmaz; dosya taranan kümede değilse dokunulmaz.
  - SkippedFiles = `Result == null && Exists` sayısı.

- [ ] **Step 1: Failing testler:** `Reconcile_NewBlocksInserted`, `Reconcile_SecondScanIsNoOp`, `Reconcile_ChangedCodeUpdated`, `Reconcile_PreservesUserOwnedFields` (Title/Tags/Folder/IsFavorite/UsageCount kullanıcı değiştirmiş → korunur), `Reconcile_DescriptionUpdatedOnlyIfUntouched` (iki durum), `Reconcile_MissingBlockMarked`, `Reconcile_ReappearingBlockClearsMissing`, `Reconcile_RenamedWithSameCodeRelinked`, `Reconcile_RenamedAndChangedNotRelinked`, `Reconcile_SkippedOrErrorFileDoesNotMarkMissing`, `Reconcile_DeletedFileMarksMissing`, `Reconcile_FilesOutsideScanUntouched`.
- [ ] **Step 2:** FAIL. **Step 3:** uygula. **Step 4:** PASS.
- [ ] **Step 5: Commit** (koordinatör): `feat: tarama uzlaştırıcısı`.

### Task 6: Depo — ApplyScanAsync ve kök taşıma  *(Antigravity)*

**Files:**
- Modify: `KodDepolama/ISnippetRepository.cs`, `KodDepolama/Data/SnippetRepository.cs`
- Test: `tests/DevVault.Tests/SnippetRepositoryTests.cs`

**Interfaces:**
- Consumes: Görev 5 `ScanPlan`.
- Produces:
  - `Task<ScanApplyResult> ApplyScanAsync(ScanPlan plan)` ve `sealed record ScanApplyResult(int Added, int Updated, int MarkedMissing, int Restored, int Unchanged, int Relinked)` (ScanModels.cs'e).
  - `Task<int> RelocateSourceRootAsync(string oldRoot, string newRoot)` — eşleşen tüm snippet'lerin SourceRoot'unu değiştirir, sürüm oluşturmaz, etkilenen sayıyı döner.
- Davranış: tek transaction; Update'lerde mevcut sürüm mantığı (eski içerik → SnippetVersion, 20 sınırı) — `AddOrUpdateAsync`'teki sürüm kodu ortak bir private metoda çıkarılıp ikisi de onu kullanır; içerik değişen snippet'te UpdatedAt=now; MarkMissing/ClearMissing/Touch sürüm oluşturmaz; hata → rollback + `ChangeTracker.Clear()` + rethrow.

- [ ] **Step 1: Failing testler:** `ApplyScan_InsertsUpdatesAndMarks`, `ApplyScan_UpdateCreatesVersionWithOldCode`, `ApplyScan_RescanCreatesNoVersions`, `ApplyScan_MissingFlagCreatesNoVersion`, `ApplyScan_FailureRollsBackEverything` (geçersiz bir Insert ile), `RelocateSourceRoot_ChangesOnlyMatchingRoot`.
- [ ] **Step 2:** FAIL. **Step 3:** uygula. **Step 4:** PASS (mevcut AddOrUpdate sürüm testleri de geçmeli).
- [ ] **Step 5: Commit** (koordinatör): `feat: taramayı tek işlemde uygulama`.

### Task 7: İçe aktarma kimliği  *(Antigravity)*

**Files:**
- Modify: `KodDepolama/Services/ImportMerger.cs`
- Test: `tests/DevVault.Tests/ImportMergerTests.cs`

**Interfaces:**
- Davranış: gelen snippet'in `SourceSymbol`'ü doluysa anahtar `(Folder, SourceFile, SourceSymbol)` (tr-TR büyük/küçük harf duyarsız); boşsa mevcut (Başlık, Dil) kuralı. Kaynaklı ile elle eklenmiş snippet birbiriyle eşleşmez. Merge güncellemesi kaynak alanlarını da kopyalar, `SourceRoot` hariç (yerel kök korunur; yerel kayıt yoksa gelen değer).
- [ ] **Step 1: Failing testler:** `Merge_SourceSnippetsMatchBySourceIdentityNotTitle`, `Merge_SameTitleDifferentFilesNotDuplicates`, `Merge_SourceAndManualNeverMatch`, `Merge_KeepsLocalSourceRoot`.
- [ ] **Step 2–4:** FAIL → uygula → PASS (mevcut ImportMerger testleri de).
- [ ] **Step 5: Commit** (koordinatör): `feat: kaynaklı snippet'lerde içe aktarma kimliği`.

### Task 8: ProjectScanService (orkestrasyon)  *(Antigravity)*

**Files:**
- Create: `KodDepolama/Services/ProjectScan/ProjectScanService.cs`
- Test: `tests/DevVault.Tests/ProjectScanServiceTests.cs`

**Interfaces:**
- Consumes: Görev 2–7.
- Produces:
  - `sealed record ScanProgress(int Done, int Total, string CurrentFile)`
  - `sealed record ScanSummary(string ProjectName, string Root, ScanApplyResult Result, int SkippedFiles)` — `ToString()` Türkçe özet: `"<ad>: X eklendi, Y güncellendi, Z kaynakta yok, N dosya atlandı"` (sıfır olanlar yazılmaz; hiçbir şey değişmediyse `"<ad>: değişiklik yok"`).
  - `sealed record ScannedProject(string Root, string Folder, int SnippetCount, bool RootExists, DateTime? LastScannedAt)`
  - `ProjectScanService(ISnippetRepository repo, IReadOnlyList<IBlockExtractor> extractors)`
  - `Task<IReadOnlyList<ScanSummary>> ScanPathsAsync(IReadOnlyList<string> paths, IProgress<ScanProgress>? progress, CancellationToken ct)` — dosya ve klasörler karışık olabilir; köklere göre gruplanır, her kök için bir ApplyScan.
  - `Task<ScanSummary> RescanProjectAsync(string root, IProgress<ScanProgress>? progress, CancellationToken ct)` — o kökten daha önce gelmiş SourceFile'ları tarar.
  - `IReadOnlyList<ScannedProject> GetScannedProjects(IEnumerable<Snippet> all)`
  - `static string ReadSource(string path)` — BOM'a göre kodlama; BOM yoksa katı UTF-8 dene, geçersizse Windows-1254 (`CodePagesEncodingProvider` kaydı); > 1 MB veya NUL içerirse `null` (atlandı).
- Davranış: dosya okuma ve çıkarma `Task.Run` içinde; `ct` her dosyada kontrol edilir; iptal edilirse ApplyScan çağrılmaz (veritabanı değişmez). `.h` dosyasında içerik `class`/`std::`/`template<`/`namespace` içeriyorsa C++. Çıkarıcı istisna fırlatırsa dosya atlanır.

- [ ] **Step 1: Failing testler** (temp klasörde küçük bir sahte proje + temp DB): `ScanPaths_FolderGroupsByRootAndApplies`, `ScanPaths_MixedFilesFromTwoProjects`, `Rescan_UsesPreviouslyScannedFiles`, `Scan_CancelledLeavesDatabaseUnchanged`, `ReadSource_DetectsEncoding` (UTF-8 BOM, UTF-8 BOM'suz Türkçe, Windows-1254 Türkçe, CRLF), `ReadSource_SkipsLargeAndBinary`, `Summary_ToStringTurkish`, `SmokeTest_ScansDevVaultOwnSource` (depo kökünü `DevSnippetManager.slnx` arayarak bulur; `KodDepolama` klasörünü temp DB'ye tarar; istisna yok, > 100 blok, `MainViewModel.SaveSnippetAsync` sembolü var).
- [ ] **Step 2:** FAIL. **Step 3:** uygula. **Step 4:** PASS.
- [ ] **Step 5: Commit** (koordinatör): `feat: proje tarama servisi`.

### Task 9: Arayüz  *(Antigravity)*

**Files:**
- Modify: `KodDepolama/ViewModels/MainViewModel.cs`, `KodDepolama/MainWindow.xaml(.cs)`, `KodDepolama/Converters.cs`, `README.md`
- Create: `KodDepolama/ViewModels/ProjectScanViewModel.cs` (MainViewModel'de `ProjectScan` özelliği olarak)

**Interfaces:**
- Consumes: Görev 8 servis ve kayıtları.
- Produces (bağlanacak adlar): `ScanFilesCommand`, `ScanFolderCommand`, `RescanProjectCommand(ScannedProject)`, `RelocateProjectCommand(ScannedProject)`, `CancelScanCommand`, `ObservableCollection<ScannedProject> ScannedProjects`, `bool IsScanning`, `string ScanProgressText`, `double ScanProgressValue`; MainViewModel'de `AskAiCommand`, `ShowMissingOnly` filtresi (FolderFilters'a "Kaynakta olmayanlar" seçeneği), editör için `EditSourceInfo` (`"Kaynak: <proje>/<dosya>"` veya boş), `EditIsSourceMissing`, `OpenSourceFileCommand`.
- Davranış (spec "Arayüz" bölümü): kenar çubuğunda "Projeden ekle" (ikon `FolderSearchOutline`) → "Dosya seç…" (çoklu, desteklenen uzantı filtresi) / "Klasör seç…" (`OpenFolderDialog`) ve "Taranan projeler" alt menüsü (her proje: ad, snippet sayısı, "Yeniden tara", kök yoksa "Konumu değiştir…"). Tarama sırasında ilerleme çubuğu + "İptal"; bitince her özet snackbar'da ve tepsi balonunda (`_notifyIcon.ShowBalloonTip`). Tarama sonrası liste yenilenir; açık ve kaydedilmemiş snippet etkilendiyse editör yeniden yüklenmez, spec'teki snackbar gösterilir. Liste öğesinde `IsSourceMissing` → Opacity 0.5 + `LinkOff` ikonu. "Yapay zekâya sor": editördeki **güncel** kodla spec'teki metni (dil, proje/dosya/sembol, kod çit bloğu dilin Monaco kimliğiyle) panoya koyar, snackbar "Soru panoya kopyalandı — cevabı açıklama alanına yapıştırın", açıklamayı düzenleme moduna alır. Kaynak bilgisi satırı ve "Dosyayı aç" (`Process.Start` UseShellExecute, yalnız dosya varsa). Açık ve koyu temada doğru görünmeli; 900 px'te kırpılmamalı. README'ye "Projeden snippet çıkarma" bölümü.
- [ ] **Step 1:** Uygula. **Step 2:** `dotnet build` + `dotnet test` → 0 hata, hepsi geçer.
- [ ] **Step 3: Commit** (koordinatör): `feat: proje tarama arayüzü ve Yapay zekâya sor`.

### Task 10: Uçtan uca doğrulama  *(Sonnet)*

- [ ] Kod okuma + `dotnet build` / `dotnet test`; Review Focus'taki 5 maddenin testlerinin var ve anlamlı olduğunu kontrol.
- [ ] Yalıtılmış veriyle (`DEVVAULT_DATA_DIR`) uygulamayı açıp gerçek bir projeyi (DevVault'un kendi kaynağı) tara; snackbar özeti, liste, kaynak bilgisi, "Yapay zekâya sor" pano içeriği, yeniden tara (değişiklik yok), bir dosyayı scratch kopyada değiştirip yeniden tara (sürüm geçmişi) — ekran görüntüleri açık/koyu.
- [ ] Hata listesi → düzeltme turu (Antigravity) → tekrar doğrulama.
