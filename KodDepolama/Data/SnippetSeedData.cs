using KodDepolama;
using System;
using System.Collections.Generic;

namespace DevSnippetManager.Data
{
    public static class SnippetSeedData
    {
        public static List<Snippet> GetProfessionalSnippets()
        {
            var now = DateTime.Now;
            var list = new List<Snippet>();

            // Helper to add
            void Add(string lang, string title, string tags, string desc, string code)
            {
                list.Add(new Snippet
                {
                    Title = title,
                    Language = lang,
                    Tags = tags,
                    Description = desc,
                    Code = code,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                now = now.AddMinutes(-15); // Spread them in time
            }

            // ==========================================
            // C# (15 Professional Examples)
            // ==========================================
            Add("C#", "Polly ile HTTP Retry (Yeniden Deneme) Mekanizması", "httpclient, polly, resilience, api", "Geçici ağ hatalarına (Transient Faults) karşı Polly kütüphanesi kullanarak üstel geri çekilme (exponential backoff) ile HTTP isteklerini yeniden deneme şablonu.", 
                "var retryPolicy = HttpPolicyExtensions\n    .HandleTransientHttpError()\n    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));\n\nvar client = new HttpClient();\nvar response = await retryPolicy.ExecuteAsync(() => client.GetAsync(\"https://api.ornek.com/veri\"));");

            Add("C#", "Entity Framework Core - Compiled Query", "efcore, performance, database, linq", "Sık çalışan veritabanı sorgularının performansını artırmak için EF Core 'Compiled Query' kullanımı.", 
                "private static readonly Func<AppDbContext, int, IEnumerable<User>> GetUsersByAge =\n    EF.CompileQuery((AppDbContext context, int age) =>\n        context.Users.Where(u => u.Age >= age).ToList());\n\n// Kullanımı:\n// var users = GetUsersByAge(dbContext, 18);");

            Add("C#", "Dependency Injection - Factory Pattern", "di, ioc, design-pattern, factory", "ASP.NET Core DI konteynerini kullanarak duruma göre (runtime) farklı servis implementasyonlarını çözme (resolve) sağlayan Factory deseni.", 
                "services.AddTransient<PaymentServiceA>();\nservices.AddTransient<PaymentServiceB>();\nservices.AddTransient<Func<string, IPaymentService>>(serviceProvider => key =>\n{\n    return key switch\n    {\n        \"Stripe\" => serviceProvider.GetRequiredService<PaymentServiceA>(),\n        \"PayPal\" => serviceProvider.GetRequiredService<PaymentServiceB>(),\n        _ => throw new KeyNotFoundException()\n    };\n});");

            Add("C#", "Asenkron SemaphoreSlim ile Rate Limiting", "async, threading, concurrency, ratelimit", "Aynı anda sadece belirli sayıda asenkron görevin (Task) çalışmasına izin veren hafif sıklet kilit mekanizması.", 
                "private static readonly SemaphoreSlim _gate = new SemaphoreSlim(5); // Maks 5 eşzamanlı işlem\n\npublic async Task ProcessDataAsync(List<int> ids)\n{\n    var tasks = ids.Select(async id =>\n    {\n        await _gate.WaitAsync();\n        try\n        {\n            await CallExternalApiAsync(id);\n        }\n        finally\n        {\n            _gate.Release();\n        }\n    });\n    await Task.WhenAll(tasks);\n}");

            Add("C#", "IAsyncEnumerable ile Streaming Veri Okuma", "c#8, async, stream, yield", "Büyük veri setlerini belleğe (RAM) yüklemeden parça parça asenkron olarak işlemek için IAsyncEnumerable kullanımı.", 
                "public async IAsyncEnumerable<string> ReadLinesAsync(string filePath)\n{\n    using var reader = new StreamReader(filePath);\n    string? line;\n    while ((line = await reader.ReadLineAsync()) != null)\n    {\n        yield return line;\n    }\n}\n\n// await foreach (var line in ReadLinesAsync(\"data.txt\")) { ... }");

            Add("C#", "JWT Token Doğrulama (Middleware)", "security, jwt, authentication, aspnet", "ASP.NET Core API projelerinde gelen JWT token'ı manuel olarak doğrulayan ve claimleri okuyan kod bloğı.", 
                "var tokenHandler = new JwtSecurityTokenHandler();\ntokenHandler.ValidateToken(token, new TokenValidationParameters\n{\n    ValidateIssuerSigningKey = true,\n    IssuerSigningKey = new SymmetricSecurityKey(key),\n    ValidateIssuer = false,\n    ValidateAudience = false\n}, out SecurityToken validatedToken);\n\nvar jwtToken = (JwtSecurityToken)validatedToken;\nvar userId = jwtToken.Claims.First(x => x.Type == \"id\").Value;");

            Add("C#", "ConcurrentDictionary ile Thread-Safe Cache", "cache, collections, thread-safe, concurrency", "Çoklu thread ortamlarında kilit (lock) kullanmadan güvenli önbellekleme yapısı.", 
                "private static readonly ConcurrentDictionary<string, User> _cache = new();\n\npublic User GetOrFetchUser(string email)\n{\n    return _cache.GetOrAdd(email, key => \n    {\n        // Eğer cache'de yoksa veritabanından çek\n        return FetchUserFromDb(key);\n    });\n}");

            Add("C#", "Span<T> ile Sıfır Tahsisli (Zero-Allocation) String Parse", "performance, memory, span, parsing", "String metotları (Substring, Split) kullanmadan, bellek tahsisi (allocation) yapmadan yüksek performanslı metin ayrıştırma.", 
                "ReadOnlySpan<char> text = \"USER:12345:ACTIVE\";\nint firstColon = text.IndexOf(':');\nint lastColon = text.LastIndexOf(':');\n\nReadOnlySpan<char> idSpan = text.Slice(firstColon + 1, lastColon - firstColon - 1);\nint id = int.Parse(idSpan); // Bellek tahsisi = 0 byte");

            Add("C#", "Dapper ile Çoklu Tablo (Multi-Mapping) Okuma", "dapper, orm, sql, mapping", "Dapper mikro ORM kullanarak tek bir SQL sorgusu ile birbirine bağlı nesneleri (1-N ilişkisi) belleğe alma.", 
                "var sql = \"SELECT * FROM Orders o INNER JOIN Users u ON o.UserId = u.Id\";\nvar orders = await connection.QueryAsync<Order, User, Order>(\n    sql,\n    (order, user) => \n    {\n        order.User = user;\n        return order;\n    },\n    splitOn: \"Id\"\n);");

            Add("C#", "BackgroundService (Worker) Tanımlama", "worker, background, aspnetcore, daemon", "Arka planda sürekli çalışan ve belirli aralıklarla görev yapan ASP.NET Core Hosted Service örneği.", 
                "public class MyWorker : BackgroundService\n{\n    protected override async Task ExecuteAsync(CancellationToken stoppingToken)\n    {\n        while (!stoppingToken.IsCancellationRequested)\n        {\n            Console.WriteLine(\"Worker çalışıyor...\");\n            await Task.Delay(5000, stoppingToken);\n        }\n    }\n}");

            // ==========================================
            // JavaScript / TypeScript (10 Professional Examples)
            // ==========================================
            Add("JavaScript", "Axios ile Interceptor Kullanımı", "axios, api, interceptor, token", "Tüm giden HTTP isteklerine otomatik olarak Authorization (Bearer) header ekleyen Axios interceptor yapısı.", 
                "axios.interceptors.request.use(config => {\n  const token = localStorage.getItem('token');\n  if (token) {\n    config.headers.Authorization = `Bearer ${token}`;\n  }\n  return config;\n}, error => {\n  return Promise.reject(error);\n});");

            Add("JavaScript", "Debounce Fonksiyonu", "performance, events, dom, utils", "Arama kutusuna hızlıca yazarken her tuşta API isteği atmamak için kullanılan bekleme (debounce) fonksiyonu.", 
                "function debounce(func, timeout = 300){\n  let timer;\n  return (...args) => {\n    clearTimeout(timer);\n    timer = setTimeout(() => { func.apply(this, args); }, timeout);\n  };\n}\n// const saveInput = debounce(() => fetch('/api/search'), 500);");

            Add("JavaScript", "JS Proxy ile Obje Validasyonu", "proxy, validation, object, metaprogramming", "Bir objeye değer atandığında (setter) otomatik doğrulama (validation) yapılmasını sağlayan Proxy deseni.", 
                "const validator = {\n  set: function(obj, prop, value) {\n    if (prop === 'age' && (!Number.isInteger(value) || value < 0)) {\n      throw new TypeError('Yaş pozitif bir tam sayı olmalıdır.');\n    }\n    obj[prop] = value;\n    return true;\n  }\n};\nconst person = new Proxy({}, validator);");

            Add("JavaScript", "Promise.allSettled ile Paralel İstekler", "async, promises, parallel, fetch", "Birden fazla asenkron isteğin paralel olarak çalıştırılması ve biri çökse bile diğerlerinin sonucunun alınması.", 
                "const urls = ['/api/users', '/api/posts', '/api/comments'];\nconst requests = urls.map(url => fetch(url).then(res => res.json()));\n\nPromise.allSettled(requests).then(results => {\n  results.forEach((res, index) => {\n    if (res.status === 'fulfilled') console.log(`Başarılı ${index}:`, res.value);\n    if (res.status === 'rejected') console.error(`Hata ${index}:`, res.reason);\n  });\n});");

            Add("JavaScript", "React Custom Hook (useLocalStorage)", "react, hooks, localstorage, state", "React projelerinde state'i localStorage üzerinde kalıcı hale getiren profesyonel Custom Hook mimarisi.", 
                "function useLocalStorage(key, initialValue) {\n  const [val, setVal] = useState(() => {\n    try {\n      const item = window.localStorage.getItem(key);\n      return item ? JSON.parse(item) : initialValue;\n    } catch (error) {\n      return initialValue;\n    }\n  });\n\n  const setValue = value => {\n    setVal(value);\n    window.localStorage.setItem(key, JSON.stringify(value));\n  };\n  return [val, setValue];\n}");

            Add("JavaScript", "Node.js EventEmitter Kullanımı", "node, events, pubsub, architecture", "Node.js uygulamasında modüller arası bağları (coupling) azaltmak için Event Driven (Pub/Sub) mimarisi.", 
                "const EventEmitter = require('events');\nclass OrderEmitter extends EventEmitter {}\nconst orderEvents = new OrderEmitter();\n\n// Dinleyici (Listener)\norderEvents.on('orderCreated', (order) => {\n  console.log(`Sipariş faturası kesiliyor: ${order.id}`);\n});\n\n// Tetikleyici (Publisher)\norderEvents.emit('orderCreated', { id: 1001, total: 50 });");

            Add("JavaScript", "Web Worker ile Ağır Hesaplama", "worker, threads, performance, browser", "Ana (UI) thread'i dondurmadan arka planda yoğun CPU hesaplaması yapmak için Web Worker kullanımı.", 
                "// worker.js\nonmessage = function(e) {\n  let result = 0;\n  for(let i=0; i<e.data; i++) result += i;\n  postMessage(result);\n}\n\n// main.js\nconst worker = new Worker('worker.js');\nworker.onmessage = (e) => console.log('Sonuç:', e.data);\nworker.postMessage(1000000000);");

            // ==========================================
            // SQL (8 Professional Examples)
            // ==========================================
            Add("SQL", "CTE (Common Table Expression) ile Hiyerarşik Sorgu", "cte, recursion, hierarchy, sqlserver", "Kategori veya organizasyon şeması gibi ebeveyn-çocuk ilişkisi olan tabloları (tree) rekürsif olarak çekme.", 
                "WITH RecursiveCategories AS (\n  SELECT Id, Name, ParentId, 0 AS Level\n  FROM Categories\n  WHERE ParentId IS NULL\n  UNION ALL\n  SELECT c.Id, c.Name, c.ParentId, rc.Level + 1\n  FROM Categories c\n  INNER JOIN RecursiveCategories rc ON c.ParentId = rc.Id\n)\nSELECT * FROM RecursiveCategories;");

            Add("SQL", "Window Functions (ROW_NUMBER) ile Mükerrer Kayıt Temizleme", "window-functions, duplicates, cleanup, data", "Veritabanındaki tekrarlanan (duplicate) kayıtları tespit edip, en güncel olanı bırakarak diğerlerini silme tekniği.", 
                "WITH CTE AS (\n  SELECT Id, Email,\n  ROW_NUMBER() OVER(PARTITION BY Email ORDER BY CreatedAt DESC) as RowNum\n  FROM Users\n)\nDELETE FROM CTE WHERE RowNum > 1;");

            Add("SQL", "Pivot Tablo Oluşturma", "pivot, reporting, aggregation, matrix", "Satır bazlı dönen verileri (Örn: Aylar) sütun bazlı matriks (raporlama) formatına çevirme.", 
                "SELECT * FROM (\n  SELECT Year, Month, SalesAmount\n  FROM MonthlySales\n) AS SourceData\nPIVOT (\n  SUM(SalesAmount)\n  FOR Month IN ([Jan], [Feb], [Mar], [Apr])\n) AS PivotTable;");

            Add("SQL", "TRY...CATCH ve TRANSACTION Blokları", "transaction, error-handling, rollback, atomicity", "SQL Server içerisinde hata yönetimi ve işlemlerin (atomicity) güvenli geri alınması (Rollback).", 
                "BEGIN TRY\n  BEGIN TRANSACTION;\n  \n  UPDATE Accounts SET Balance = Balance - 100 WHERE Id = 1;\n  UPDATE Accounts SET Balance = Balance + 100 WHERE Id = 2;\n  \n  COMMIT TRANSACTION;\nEND TRY\nBEGIN CATCH\n  IF @@TRANCOUNT > 0\n    ROLLBACK TRANSACTION;\n    \n  PRINT ERROR_MESSAGE();\nEND CATCH;");

            Add("SQL", "JSON Veri Okuma (OPENJSON)", "json, parsing, sqlserver2016", "Bir sütunda tutulan JSON verisini parçalayarak ilişkisel (satır/sütun) tablo verisine dönüştürme.", 
                "DECLARE @json NVARCHAR(MAX) = N'[{\"Ad\":\"Ali\", \"Yas\":30}, {\"Ad\":\"Ayşe\", \"Yas\":25}]';\n\nSELECT *\nFROM OPENJSON(@json)\nWITH (\n  Ad NVARCHAR(50) '$.Ad',\n  Yas INT '$.Yas'\n);");

            Add("SQL", "Index Fragmentasyon (Bozulma) Kontrolü", "maintenance, performance, dba, indexes", "Veritabanındaki indekslerin parçalanma oranlarını listeleme. Performans düşüşlerini tespit etmek için DBA komutu.", 
                "SELECT \n    dbschemas.[name] as 'Schema',\n    dbtables.[name] as 'Table',\n    dbindexes.[name] as 'Index',\n    indexstats.avg_fragmentation_in_percent\nFROM sys.dm_db_index_physical_stats (DB_ID(), NULL, NULL, NULL, NULL) AS indexstats\nINNER JOIN sys.tables dbtables on dbtables.[object_id] = indexstats.[object_id]\nINNER JOIN sys.schemas dbschemas on dbtables.[schema_id] = dbschemas.[schema_id]\nINNER JOIN sys.indexes AS dbindexes ON dbindexes.[object_id] = indexstats.[object_id]\nAND indexstats.index_id = dbindexes.index_id\nWHERE indexstats.database_id = DB_ID()\nORDER BY indexstats.avg_fragmentation_in_percent desc;");

            // ==========================================
            // Python (8 Professional Examples)
            // ==========================================
            Add("Python", "Pandas ile Eksik Veri (NaN) Doldurma", "pandas, data-science, preprocessing", "Veri analizi sırasında boş (NaN) gelen hücreleri, ilgili sütunun ortalamasıyla doldurma.", 
                "import pandas as pd\n\ndf = pd.read_csv('data.csv')\n# Eksik 'Maas' değerlerini sütun ortalaması ile doldur\ndf['Maas'].fillna(df['Maas'].mean(), inplace=True)");

            Add("Python", "Multiprocessing ile Paralel CPU İşlemi", "multiprocessing, performance, cpu, parallel", "Python'ın GIL (Global Interpreter Lock) limitini aşmak için threading yerine Multiprocessing Process havuzu kullanımı.", 
                "import multiprocessing\n\ndef agir_islem(x):\n    return x * x * x\n\nif __name__ == '__main__':\n    veri_seti = [1, 2, 3, 4, 5, 6, 7, 8]\n    with multiprocessing.Pool(processes=4) as pool:\n        sonuclar = pool.map(agir_islem, veri_seti)\n    print(sonuclar)");

            Add("Python", "Decorator ile Metot Çalışma Süresi Ölçümü", "decorators, profiling, metaprogramming, performance", "Bir fonksiyonun kaç saniyede çalıştığını otomatik olarak konsola yazdıran performans ölçüm dekoratörü.", 
                "import time\n\ndef zaman_olc(func):\n    def wrapper(*args, **kwargs):\n        baslangic = time.time()\n        sonuc = func(*args, **kwargs)\n        bitis = time.time()\n        print(f'{func.__name__} {bitis - baslangic:.4f} saniyede çalıştı.')\n        return sonuc\n    return wrapper\n\n@zaman_olc\ndef uzun_islem():\n    time.sleep(1)");

            Add("Python", "Requests ile Session (Oturum) Kullanımı", "requests, api, http, scraping", "Birden fazla HTTP isteği atarken aynı TCP bağlantısını ve çerezleri (cookies) korumak için Session kullanımı.", 
                "import requests\n\nsession = requests.Session()\n# Session seviyesinde header ayarlama\nsession.headers.update({'Authorization': 'Bearer TOKEN'})\n\nres1 = session.get('https://api.ornek.com/endpoint1')\nres2 = session.get('https://api.ornek.com/endpoint2')");

            Add("Python", "Generator (yield) ile Bellek Dostu Okuma", "generators, memory, optimization, streaming", "Gigabaytlarca büyüklükteki bir dosyayı belleğe yüklemeden satır satır okumayı sağlayan generator yapısı.", 
                "def buyuk_dosya_okuyucu(dosya_yolu):\n    with open(dosya_yolu, 'r') as dosya:\n        for satir in dosya:\n            yield satir.strip()\n\n# Kullanımı: Sadece o anki satır RAM'de durur\n# for satir in buyuk_dosya_okuyucu('büyük_veri.txt'):\n#     print(satir)");

            Add("Python", "SQLAlchemy ORM ile Transaction Yönetimi", "sqlalchemy, database, orm, transaction", "SQLAlchemy kullanırken bir hata oluşursa veritabanı işlemlerini güvenli şekilde geri alan Context Manager yapısı.", 
                "from sqlalchemy.orm import sessionmaker\nSession = sessionmaker(bind=engine)\n\nwith Session.begin() as session:\n    try:\n        yeni_kullanici = User(name=\"Ali\")\n        session.add(yeni_kullanici)\n        # session.commit() otomatik çalışır\n    except Exception:\n        session.rollback()\n        raise");

            // ==========================================
            // Java (6 Professional Examples)
            // ==========================================
            Add("Java", "CompletableFuture ile Asenkron Zincirleme", "concurrency, async, future, java8", "Modern Java'da bir asenkron görev bitince diğerini tetikleyen ve sonuçları birleştiren CompletableFuture yapısı.", 
                "CompletableFuture<String> gelecek = CompletableFuture.supplyAsync(() -> {\n    return veriTabanindanGetir();\n}).thenApply(veri -> {\n    return veri.toUpperCase();\n});\n\n// Non-blocking okuma\ngelecek.thenAccept(sonuc -> System.out.println(sonuc));");

            Add("Java", "Optional<T> ile NullPointerException Önleme", "optional, null-safety, java8", "Geriye null dönebilecek metotlarda if (x != null) yerine güvenli (safe) Optional sarmalayıcısı kullanımı.", 
                "public Optional<User> findUserById(int id) {\n    User user = database.getUser(id);\n    return Optional.ofNullable(user);\n}\n\n// Kullanımı:\n// findUserById(5).ifPresent(u -> System.out.println(u.getName()));");

            Add("Java", "Singleton Pattern (Thread-Safe & Lazy Initialization)", "design-patterns, singleton, concurrency", "Çoklu thread ortamlarında performansı düşürmeden sadece bir kere instance oluşturan Double-Checked Locking kalıbı.", 
                "public class ConfigurationManager {\n    private static volatile ConfigurationManager instance;\n    private ConfigurationManager() {}\n\n    public static ConfigurationManager getInstance() {\n        if (instance == null) {\n            synchronized (ConfigurationManager.class) {\n                if (instance == null) {\n                    instance = new ConfigurationManager();\n                }\n            }\n        }\n        return instance;\n    }\n}");

            Add("Java", "Stream API - GroupBy (Gruplama)", "streams, collections, data-processing, java8", "Bir listeyi içerisindeki özelliğe göre (Örn: Yaş) bir HashMap olarak gruplayan Stream API örneği.", 
                "List<Person> people = getPeople();\nMap<Integer, List<Person>> byAge = people.stream()\n    .collect(Collectors.groupingBy(Person::getAge));\n\n// Sonuç: {25: [Ali, Veli], 30: [Ayşe]}");

            Add("Java", "Custom Annotation (Özel Notasyon) Tanımlama", "annotations, metaprogramming, reflection", "Java'da kendi @Logla anotasyonunuzu (annotation) yazma ve çalışma zamanında saklanmasını sağlama.", 
                "@Retention(RetentionPolicy.RUNTIME)\n@Target(ElementType.METHOD)\npublic @interface Logla {\n    String seviye() default \"INFO\";\n    boolean dbYaz() default false;\n}");

            // ==========================================
            // HTML/CSS (5 Professional Examples)
            // ==========================================
            Add("HTML/CSS", "CSS Grid ile Kutsal Kase (Holy Grail) Düzeni", "css, grid, layout, responsive", "Header, Footer, Sol Menü, Sağ Menü ve Ana İçerikten oluşan klasik web düzeninin en kısa CSS Grid kodu.", 
                ".container {\n  display: grid;\n  grid-template-areas:\n    \"header header header\"\n    \"nav article aside\"\n    \"footer footer footer\";\n  grid-template-rows: auto 1fr auto;\n  grid-template-columns: 200px 1fr 200px;\n  min-height: 100vh;\n}\n.header { grid-area: header; }\n.nav { grid-area: nav; } /* vs... */");

            Add("HTML/CSS", "Cam Efekti (Glassmorphism)", "css, ui, design, effects", "Arka planı bulanık, yarı saydam ve premium görünen modern cam (glassmorphism) tasarım CSS kodları.", 
                ".glass-panel {\n  background: rgba(255, 255, 255, 0.25);\n  box-shadow: 0 8px 32px 0 rgba(31, 38, 135, 0.37);\n  backdrop-filter: blur(10px);\n  -webkit-backdrop-filter: blur(10px);\n  border-radius: 10px;\n  border: 1px solid rgba(255, 255, 255, 0.18);\n}");

            Add("HTML/CSS", "CSS Değişkenleri ile Karanlık Tema", "css, dark-mode, variables, theming", "Uygulamada tek bir class değiştirerek aydınlık ve karanlık tema (dark mode) arasında geçiş yapma altyapısı.", 
                ":root {\n  --bg-color: #ffffff;\n  --text-color: #333333;\n}\n[data-theme=\"dark\"] {\n  --bg-color: #121212;\n  --text-color: #f1f1f1;\n}\nbody {\n  background-color: var(--bg-color);\n  color: var(--text-color);\n  transition: all 0.3s ease;\n}");

            Add("HTML/CSS", "Erişilebilir (a11y) Modal Şablonu", "html, accessibility, a11y, dialog", "Ekran okuyucular (Screen Readers) için %100 uyumlu (aria attributelarına sahip) HTML popup (modal) diyaloğu.", 
                "<div role=\"dialog\" aria-labelledby=\"modalTitle\" aria-modal=\"true\" tabindex=\"-1\" class=\"modal\">\n  <h2 id=\"modalTitle\">Kullanıcı Sözleşmesi</h2>\n  <p>İçerik buraya...</p>\n  <button aria-label=\"Kapat\" onclick=\"closeModal()\">X</button>\n</div>");

            Add("HTML/CSS", "Performanslı CSS Animasyonu", "css, animation, performance, hardware-accel", "Tarayıcıyı yormadan (sadece GPU kullanarak) yapılan yüksek performanslı donanım hızlandırmalı dönme animasyonu.", 
                ".spinner {\n  width: 50px;\n  height: 50px;\n  border: 5px solid #f3f3f3;\n  border-top: 5px solid #3498db;\n  border-radius: 50%;\n  /* transform ve opacity donanım ivmeli çalışır (GPU) */\n  animation: spin 1s linear infinite;\n}\n@keyframes spin {\n  0% { transform: rotate(0deg); }\n  100% { transform: rotate(360deg); }\n}");

            // Combine with extra parts
            now = SnippetSeedData_Part2.AddMoreProfessionalSnippets(list, now);
            now = SnippetSeedData_Part3.AddMoreProfessionalSnippets(list, now);

            return list;
        }
    }
}
