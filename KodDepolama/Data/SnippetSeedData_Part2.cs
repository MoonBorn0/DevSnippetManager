using KodDepolama;
using System;
using System.Collections.Generic;

namespace DevSnippetManager.Data
{
    public static class SnippetSeedData_Part2
    {
        public static DateTime AddMoreProfessionalSnippets(List<Snippet> list, DateTime now)
        {
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
                now = now.AddMinutes(-5); // Spread them
            }

            // ==========================================
            // C# (15 New Professional Examples)
            // ==========================================
            Add("C#", "MediatR ile CQRS (Command/Query Separation)", "mediatr, cqrs, architecture, aspnet", "Uygulama iş mantığını API Controller'dan ayırmak ve decouple (bağları koparmak) için MediatR kullanımı.", 
                "public class GetUserQuery : IRequest<UserDto> { public int Id { get; set; } }\n\npublic class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>\n{\n    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)\n    {\n        // Veritabanından kullanıcıyı getir\n        return new UserDto();\n    }\n}");

            Add("C#", "Channel<T> ile Arka Plan Kuyruğu (Producer/Consumer)", "channels, concurrency, background, queue", "Yüksek performanslı, thread-safe bellek içi kuyruk (In-Memory Queue) yönetimi için System.Threading.Channels kullanımı.", 
                "var channel = Channel.CreateBounded<string>(100);\n\n// Üretici (Producer)\nawait channel.Writer.WriteAsync(\"Yeni İşlem\");\n\n// Tüketici (Consumer - Worker Service'de çalışır)\nawait foreach (var item in channel.Reader.ReadAllAsync())\n{\n    Console.WriteLine($\"İşleniyor: {item}\");\n}");

            Add("C#", "Moq ve xUnit ile Birim Testi (Unit Testing)", "testing, xunit, moq, tdd", "Bağımlılıkları taklit ederek (mocking) servis katmanı için yazılmış profesyonel bir xUnit test metodu.", 
                "[Fact]\npublic async Task GetUser_ReturnsUser_WhenExists()\n{\n    var mockRepo = new Mock<IUserRepository>();\n    mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new User { Id = 1 });\n    var service = new UserService(mockRepo.Object);\n    \n    var result = await service.GetUserAsync(1);\n    \n    Assert.NotNull(result);\n    Assert.Equal(1, result.Id);\n}");

            Add("C#", "Minimal API Endpoint Tanımlama", "minimal-api, aspnetcore, webapi", "ASP.NET Core 6+ ile gelen, Controller kullanmadan doğrudan Program.cs üzerinde hafif (lightweight) endpoint oluşturma.", 
                "app.MapPost(\"/api/users\", async (UserDto dto, IValidator<UserDto> validator, AppDbContext db) => \n{\n    var validationResult = await validator.ValidateAsync(dto);\n    if (!validationResult.IsValid) return Results.ValidationProblem(validationResult.ToDictionary());\n    \n    db.Users.Add(new User(dto));\n    await db.SaveChangesAsync();\n    return Results.Created($\"/api/users/{dto.Id}\", dto);\n});");

            Add("C#", "SignalR Hub ile Gerçek Zamanlı İletişim", "signalr, websockets, realtime, aspnet", "Web istemcilerine anlık bildirim göndermek için kullanılan SignalR Hub sınıfı altyapısı.", 
                "public class NotificationHub : Hub\n{\n    public async Task SendNotificationToGroup(string groupName, string message)\n    {\n        // İlgili gruptaki tüm istemcilere mesaj gönder\n        await Clients.Group(groupName).SendAsync(\"ReceiveNotification\", message);\n    }\n}");

            Add("C#", "Dapper ExecuteAsync ile SQL Transaction", "dapper, sql, transaction, orm", "Dapper mikro-orm kullanarak çoklu insert/update işlemlerini güvenli bir Transaction içinde yönetme.", 
                "using var connection = new SqlConnection(connString);\nawait connection.OpenAsync();\nusing var transaction = connection.BeginTransaction();\ntry\n{\n    await connection.ExecuteAsync(\"UPDATE Accounts SET Bal -= 100 WHERE Id = 1\", transaction: transaction);\n    await connection.ExecuteAsync(\"UPDATE Accounts SET Bal += 100 WHERE Id = 2\", transaction: transaction);\n    transaction.Commit();\n}\ncatch\n{\n    transaction.Rollback();\n}");

            Add("C#", "Redis Dağıtık Önbellek (Distributed Cache)", "redis, caching, performance, distributed", "IDistributedCache arayüzü ile Redis üzerinden asenkron veri okuma ve yazma (Absolute Expiration ile).", 
                "var cacheKey = $\"user_{userId}\";\nvar cachedData = await _distributedCache.GetStringAsync(cacheKey);\nif (cachedData != null) return JsonSerializer.Deserialize<User>(cachedData);\n\nvar user = await _db.Users.FindAsync(userId);\nvar options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10));\nawait _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(user), options);\nreturn user;");

            Add("C#", "Serilog ile Loglama ve Dosyaya Yazma", "logging, serilog, diagnostics, aspnet", "Uygulama loglarını yapılandırılmış (structured) biçimde JSON olarak dosyaya yazdıran Serilog konfigürasyonu.", 
                "Log.Logger = new LoggerConfiguration()\n    .MinimumLevel.Information()\n    .Enrich.FromLogContext()\n    .WriteTo.Console()\n    .WriteTo.File(new CompactJsonFormatter(), \"logs/log.txt\", rollingInterval: RollingInterval.Day)\n    .CreateLogger();");

            Add("C#", "Refit ile Tip Güvenli (Type-Safe) REST İstemci", "refit, httpclient, rest, api", "Refit kütüphanesi kullanarak, Interface üzerinden otomatik HTTP API istemcisi (client) oluşturma.", 
                "public interface IGitHubApi\n{\n    [Get(\"/users/{user}\")]\n    Task<User> GetUserAsync(string user);\n}\n// Kullanımı:\n// var gitHubApi = RestService.For<IGitHubApi>(\"https://api.github.com\");\n// var myUser = await gitHubApi.GetUserAsync(\"octocat\");");

            Add("C#", "FluentValidation ile Kapsamlı Doğrulama", "validation, fluentvalidation, ddd", "İş kurallarını entity'den ayırıp FluentValidation ile detaylı, kurallı ve lokalize (dil destekli) doğrulama yapma.", 
                "public class CustomerValidator : AbstractValidator<Customer>\n{\n    public CustomerValidator() \n    {\n        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage(\"Geçerli bir e-posta giriniz.\");\n        RuleFor(x => x.Discount).InclusiveBetween(0, 100).When(x => x.IsPremium);\n    }\n}");

            Add("C#", "Task.WhenAny ile İlk Biten Görevi Alma", "async, tasks, concurrency, timeout", "Birden fazla sunucuya aynı anda istek atıp, ilk yanıt vereni (en hızlı olanı) alıp diğerlerini iptal etme tekniği.", 
                "using var cts = new CancellationTokenSource();\nvar task1 = FetchDataAsync(\"ServerA\", cts.Token);\nvar task2 = FetchDataAsync(\"ServerB\", cts.Token);\n\nvar firstFinishedTask = await Task.WhenAny(task1, task2);\ncts.Cancel(); // Geriye kalanı iptal et\nvar result = await firstFinishedTask;");

            Add("C#", "Custom Authorization Attribute", "security, auth, aspnet, mvc", "Sadece belirli yetkilere (Claims) sahip olan kullanıcıların erişebileceği özel bir Yetkilendirme filtresi.", 
                "public class RequirePermissionAttribute : TypeFilterAttribute\n{\n    public RequirePermissionAttribute(string permission) : base(typeof(PermissionFilter))\n    {\n        Arguments = new object[] { permission };\n    }\n}\n// [RequirePermission(\"Edit_Users\")] şeklinde Controller'a eklenir.");

            Add("C#", "Yield Return ile Özel Koleksiyon Gezintisi", "yield, enumerator, collections, c#", "Kendi koleksiyon yapımızda bellek dostu iterasyon (dolaşma) sağlamak için IEnumerable ve yield return kullanımı.", 
                "public IEnumerable<int> GenerateFibonacci(int count)\n{\n    int a = 0, b = 1;\n    for (int i = 0; i < count; i++)\n    {\n        yield return a;\n        int temp = a;\n        a = b;\n        b = temp + b;\n    }\n}");

            Add("C#", "Action Delegate ve Func Kullanımı", "delegates, func, action, linq", "Bir metoda davranış (fonksiyon) parametresi geçerek strateji desenini (Strategy Pattern) çok kısa yoldan uygulama.", 
                "public void ProcessItems(List<int> items, Func<int, bool> filter, Action<int> processor)\n{\n    foreach(var item in items.Where(filter))\n    {\n        processor(item);\n    }\n}\n// Kullanımı: ProcessItems(liste, x => x > 10, x => Console.WriteLine(x));");

            Add("C#", "Records ile Immutable Veri Taşıma (DTO)", "c#9, record, immutable, dto", "C# 9+ ile gelen, eşitlik (equality) karşılaştırması otomatik yapılan, değiştirilemez (immutable) Record DTO tanımı.", 
                "public record ProductDto(int Id, string Name, decimal Price);\n\n// var p1 = new ProductDto(1, \"Laptop\", 15000);\n// var p2 = p1 with { Price = 16000 }; // Kopyalayarak değiştir");


            // ==========================================
            // JavaScript/TypeScript (15 New Professional Examples)
            // ==========================================
            Add("JavaScript", "Redux Toolkit - Slice Oluşturma", "react, redux, rtk, state", "React uygulamaları için Redux Toolkit (RTK) ile standart, boilerplate kodu azaltılmış modern state slice yapısı.", 
                "import { createSlice } from '@reduxjs/toolkit';\n\nconst counterSlice = createSlice({\n  name: 'counter',\n  initialState: { value: 0 },\n  reducers: {\n    increment: (state) => { state.value += 1; },\n    decrement: (state) => { state.value -= 1; }\n  }\n});\nexport const { increment, decrement } = counterSlice.actions;\nexport default counterSlice.reducer;");

            Add("JavaScript", "IntersectionObserver ile Lazy Loading", "dom, performance, lazy-load, images", "Kullanıcı sayfayı aşağı kaydırdıkça (scroll), resimler ekrana girdikçe yüklenmesini sağlayan modern performans optimizasyonu.", 
                "const images = document.querySelectorAll('img[data-src]');\nconst imgObserver = new IntersectionObserver((entries, observer) => {\n  entries.forEach(entry => {\n    if (entry.isIntersecting) {\n      const img = entry.target;\n      img.src = img.getAttribute('data-src');\n      observer.unobserve(img);\n    }\n  });\n});\nimages.forEach(img => imgObserver.observe(img));");

            Add("JavaScript", "Express.js Global Hata Yönetimi", "express, node, error-handling, middleware", "Tüm API endpointlerindeki hataları tek bir noktada yakalayan ve formatlayıp JSON dönen Express middleware'i.", 
                "app.use((err, req, res, next) => {\n  console.error(err.stack);\n  const statusCode = err.statusCode || 500;\n  res.status(statusCode).json({\n    error: true,\n    message: err.message || 'Sunucu içi bilinmeyen bir hata oluştu',\n    stack: process.env.NODE_ENV === 'development' ? err.stack : undefined\n  });\n});");

            Add("JavaScript", "GraphQL Apollo Client İstemi", "graphql, apollo, react, fetching", "React bileşeni içerisinde Apollo Client kancası (useQuery) ile verimli GraphQL sorgusu çekme ve durum (loading/error) yönetimi.", 
                "import { useQuery, gql } from '@apollo/client';\n\nconst GET_USERS = gql`\n  query GetUsers { users { id name email } }\n`;\n\nfunction UsersList() {\n  const { loading, error, data } = useQuery(GET_USERS);\n  if (loading) return <p>Yükleniyor...</p>;\n  if (error) return <p>Hata: {error.message}</p>;\n  return data.users.map(u => <div key={u.id}>{u.name}</div>);\n}");

            Add("JavaScript", "RxJS ile Arama Çubuğu (Autocomplete)", "rxjs, reactive, events, streams", "Gereksiz API isteklerini önlemek için debounceTime ve switchMap kullanan reaktif arama çubuğu (autocomplete) akışı.", 
                "import { fromEvent } from 'rxjs';\nimport { debounceTime, map, distinctUntilChanged, switchMap } from 'rxjs/operators';\n\nconst searchBox = document.getElementById('search');\nfromEvent(searchBox, 'input').pipe(\n  map(e => e.target.value),\n  debounceTime(500),\n  distinctUntilChanged(),\n  switchMap(term => fetch(`/api/search?q=${term}`).then(res => res.json()))\n).subscribe(data => renderResults(data));");

            Add("JavaScript", "Vue 3 Composition API Setup", "vue, composition-api, reactivity", "Vue 3'ün modern Composition API'si ile reaktif state ve computed property (hesaplanmış değişken) tanımı.", 
                "import { ref, computed } from 'vue';\n\nexport default {\n  setup() {\n    const count = ref(0);\n    const doubleCount = computed(() => count.value * 2);\n    const increment = () => { count.value++ };\n\n    return { count, doubleCount, increment };\n  }\n}");

            Add("JavaScript", "Jest ile Fonksiyon Mocklama (Birim Testi)", "jest, testing, mock, unit-test", "Jest kullanarak bir modülü (Örn: Axios) tamamen taklit etme (mock) ve API çağrısı yapmadan test yazma tekniği.", 
                "import axios from 'axios';\nimport { fetchUser } from './userService';\n\njest.mock('axios');\n\ntest('kullanıcıyı doğru çeker', async () => {\n  axios.get.mockResolvedValue({ data: { name: 'Ali' } });\n  const user = await fetchUser(1);\n  expect(axios.get).toHaveBeenCalledWith('/users/1');\n  expect(user.name).toBe('Ali');\n});");

            Add("JavaScript", "Service Worker ile Çevrimdışı Önbellek (Offline Cache)", "pwa, service-worker, offline, cache", "Kullanıcı internetten koptuğunda (Offline) web sitesinin çalışmaya devam etmesi için Cache API stratejisi.", 
                "self.addEventListener('fetch', event => {\n  event.respondWith(\n    caches.match(event.request).then(cachedResponse => {\n      // Önbellekte varsa onu dön, yoksa ağa (network) istek at\n      return cachedResponse || fetch(event.request).then(response => {\n        return caches.open('v1').then(cache => {\n          cache.put(event.request, response.clone());\n          return response;\n        });\n      });\n    })\n  );\n});");

            Add("JavaScript", "WebSockets ile Çift Yönlü Canlı Veri", "websockets, realtime, browser, api", "Tarayıcıda native WebSocket API kullanarak sunucu ile anlık, sürekli açık bağlantı kurma ve mesaj dinleme.", 
                "const socket = new WebSocket('wss://api.ornek.com/chat');\n\nsocket.onopen = () => {\n  console.log('Bağlantı kuruldu');\n  socket.send(JSON.stringify({ type: 'GREETING', payload: 'Merhaba!' }));\n};\n\nsocket.onmessage = (event) => {\n  const message = JSON.parse(event.data);\n  console.log('Sunucudan gelen:', message);\n};");

            Add("JavaScript", "JWT Decode (Kütüphanesiz)", "jwt, security, base64, utils", "Herhangi bir dış kütüphane kullanmadan, tarayıcıda Base64 ile kodlanmış JWT token içeriğini (payload) okuma fonksiyonu.", 
                "function parseJwt(token) {\n  try {\n    const base64Url = token.split('.')[1];\n    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');\n    const jsonPayload = decodeURIComponent(atob(base64).split('').map(function(c) {\n        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);\n    }).join(''));\n    return JSON.parse(jsonPayload);\n  } catch (e) { return null; }\n}");

            Add("JavaScript", "TypeScript Generic Arayüzler (Interfaces)", "typescript, generics, types, strict", "Geriye dönecek veri tipini dinamik olarak dışarıdan belirleyebildiğimiz jenerik TypeScript API yanıt arayüzü.", 
                "interface ApiResponse<T> {\n  status: number;\n  message: string;\n  data: T;\n}\n\n// Kullanımı:\nasync function getUser(): Promise<ApiResponse<User>> {\n  const res = await fetch('/api/user');\n  return res.json();\n}");

            Add("JavaScript", "Next.js getServerSideProps (SSR)", "nextjs, ssr, react, server", "Next.js'te sayfa istemciye yüklenmeden önce SEO ve performans için sunucu tarafında (Server-Side Rendering) veri çekme.", 
                "export async function getServerSideProps(context) {\n  const res = await fetch(`https://api.com/data/${context.params.id}`);\n  const data = await res.json();\n\n  if (!data) return { notFound: true }; // 404 sayfası göster\n\n  return { props: { data } }; // Bileşene (Component) prop olarak geçer\n}");

            Add("JavaScript", "Canvas API ile Ekrana Çizim", "canvas, graphics, browser, drawing", "HTML5 Canvas kullanarak ekrana dinamik bir çember ve metin çizme grafik işlemleri.", 
                "const canvas = document.getElementById('myCanvas');\nconst ctx = canvas.getContext('2d');\n\nctx.beginPath();\nctx.arc(100, 100, 50, 0, 2 * Math.PI);\nctx.fillStyle = 'blue';\nctx.fill();\n\nctx.font = '20px Arial';\nctx.fillStyle = 'white';\nctx.fillText('HTML5', 70, 105);");

            Add("JavaScript", "Web Share API (Yerel Paylaşım Menüsü)", "mobile, share, pwa, browser-api", "Kullanıcının telefonundaki doğal paylaşım (WhatsApp, Mail, vb.) tepsisini açan modern tarayıcı API'si.", 
                "async function shareArticle() {\n  if (navigator.share) {\n    try {\n      await navigator.share({\n        title: 'Harika bir makale',\n        text: 'Bunu kesinlikle okumalısın',\n        url: 'https://ornek.com/makale',\n      });\n      console.log('Başarıyla paylaşıldı');\n    } catch (error) { console.error('Paylaşım hatası', error); }\n  }\n}");

            Add("JavaScript", "Deep Clone (Derin Kopyalama) structuredClone", "utils, cloning, object, modern-api", "ES2022 ile tarayıcılara yerleşik (native) olarak eklenen, JSON.stringify()'dan daha yetenekli derin kopyalama komutu.", 
                "const orjinal = {\n  isim: 'Ali',\n  tarih: new Date(),\n  ayarlar: { tema: 'koyu' }\n};\n\n// Geleneksel JSON.stringify hatalı olarak Date objesini string yapar.\n// structuredClone ise orijinal referansları bozmadan kusursuz bir kopya üretir.\nconst kopya = structuredClone(orjinal);");


            // ==========================================
            // SQL (12 New Professional Examples)
            // ==========================================
            Add("SQL", "MERGE İfadesi (UPSERT / Ekle veya Güncelle)", "sqlserver, merge, upsert, synchronization", "Kaynak tablodaki veriye bakarak hedef tabloda kayıt varsa UPDATE, yoksa INSERT, fazlaysa DELETE yapan güçlü senkronizasyon komutu.", 
                "MERGE INTO TargetTable AS T\nUSING SourceTable AS S ON T.Id = S.Id\nWHEN MATCHED THEN\n  UPDATE SET T.Name = S.Name\nWHEN NOT MATCHED BY TARGET THEN\n  INSERT (Id, Name) VALUES (S.Id, S.Name)\nWHEN NOT MATCHED BY SOURCE THEN\n  DELETE;");

            Add("SQL", "Dinamik SQL Yürütme (sp_executesql)", "dynamic-sql, stored-procedure, execution", "Tablo adı veya sütun adının değişken olduğu durumlarda SQL Injection riskini önleyerek dinamik sorgu çalıştırma.", 
                "DECLARE @TableName NVARCHAR(128) = N'Users';\nDECLARE @Sql NVARCHAR(MAX);\nDECLARE @Count INT;\n\nSET @Sql = N'SELECT @Cnt = COUNT(*) FROM ' + QUOTENAME(@TableName);\n\nEXEC sp_executesql \n    @stmt = @Sql, \n    @params = N'@Cnt INT OUTPUT',\n    @Cnt = @Count OUTPUT;\n\nPRINT @Count;");

            Add("SQL", "Pagination (OFFSET FETCH) Yöntemi", "pagination, paging, query, performance", "Eski TOP komutu yerine SQL Server 2012+ standartlarına uygun verimli sayfalama (paging) mimarisi.", 
                "DECLARE @PageNumber INT = 3;\nDECLARE @RowsPerPage INT = 50;\n\nSELECT Id, Name, CreatedDate\nFROM Products\nORDER BY CreatedDate DESC\nOFFSET (@PageNumber - 1) * @RowsPerPage ROWS\nFETCH NEXT @RowsPerPage ROWS ONLY;");

            Add("SQL", "Trigger (Tetikleyici) ile Audit Log", "trigger, audit, history, security", "Bir tabloda güncelleme yapıldığında, eski ve yeni değerleri otomatik olarak Log (Geçmiş) tablosuna yazan tetikleyici.", 
                "CREATE TRIGGER trg_UpdateEmployee \nON Employees AFTER UPDATE AS \nBEGIN\n  INSERT INTO EmployeeAudit (EmpId, OldSalary, NewSalary, ChangedAt)\n  SELECT d.Id, d.Salary, i.Salary, GETDATE()\n  FROM deleted d\n  INNER JOIN inserted i ON d.Id = i.Id\n  WHERE d.Salary <> i.Salary; -- Sadece maaş değiştiyse\nEND;");

            Add("SQL", "INCLUDE Sütunlu Kapsayıcı İndeks", "performance, indexes, tuning, dba", "WHERE şartını hızlandırıp aynı zamanda SELECT edilen sütunları yaprak seviyesine ekleyerek sorguyu uçuran (Covering Index) yapısı.", 
                "CREATE NONCLUSTERED INDEX IX_Users_Department\nON Users (DepartmentId)\nINCLUDE (FirstName, LastName, Email); -- Bu sütunlar ağaca değil, yaprağa eklenir\n\n-- Bu sorgu doğrudan indeksten döner, tabloya (Key Lookup) gitmez:\n-- SELECT FirstName, Email FROM Users WHERE DepartmentId = 5;");

            Add("SQL", "Geçici Tablo (Temp Table) vs Tablo Değişkeni", "temp-tables, performance, memory, architecture", "Karmaşık raporlarda ara sonuçları depolamak için #TempTable kullanımı ve indeks oluşturma özelliği.", 
                "CREATE TABLE #TempReport (\n  UserId INT PRIMARY KEY,\n  TotalSales DECIMAL(18,2)\n);\n\nINSERT INTO #TempReport\nSELECT UserId, SUM(Amount) FROM Sales GROUP BY UserId;\n\nSELECT u.Name, t.TotalSales \nFROM Users u INNER JOIN #TempReport t ON u.Id = t.UserId;\n\nDROP TABLE #TempReport;");

            Add("SQL", "TRY_CAST ve COALESCE", "functions, casting, null-handling, safe", "Veri tiplerini dönüştürürken hata almamak için TRY_CAST ve NULL durumunda varsayılan değer atamak için COALESCE.", 
                "SELECT \n  Id,\n  COALESCE(Email, Phone, 'İletişim Bilgisi Yok') AS ContactInfo,\n  TRY_CAST(PriceString AS DECIMAL(10,2)) AS SafePrice\nFROM RawData;");

            Add("SQL", "CROSS APPLY ile Fonksiyon/Parçalama", "apply, table-valued-functions, joining", "Her bir satır için ayrı bir Tablo Döndüren Fonksiyon (TVF) veya JSON/XML parçalama işlemini çapraz bağlama.", 
                "SELECT e.DepartmentName, s.SkillName\nFROM Departments e\nCROSS APPLY (\n    -- Her departman için sadece en iyi 3 yeteneği getir\n    SELECT TOP 3 SkillName \n    FROM Skills \n    WHERE DeptId = e.Id \n    ORDER BY DemandScore DESC\n) s;");

            Add("SQL", "Gecikmeli Çalıştırma (WAITFOR DELAY)", "scripts, debugging, simulation, async", "Uzun süren işlemleri test etmek veya belirli bir süre SQL komutunu bekletmek için bekleme komutu.", 
                "PRINT 'İşlem Başladı: ' + CONVERT(VARCHAR, GETDATE(), 120);\n\n-- 5 saniye bekle\nWAITFOR DELAY '00:00:05';\n\nPRINT 'İşlem Bitti: ' + CONVERT(VARCHAR, GETDATE(), 120);");

            Add("SQL", "STRING_AGG ile Metin Birleştirme", "string-functions, aggregation, concatenation", "Bir kişiye ait birden fazla sipariş numarasını tek bir satırda virgülle ayrılmış şekilde gruplama (SQL Server 2017+).", 
                "SELECT \n  UserId, \n  STRING_AGG(RoleName, ', ') WITHIN GROUP (ORDER BY RoleName ASC) AS Roles\nFROM UserRoles\nGROUP BY UserId;");

            Add("SQL", "Tablo Şeması Bilgisi (INFORMATION_SCHEMA)", "metadata, reflection, schema, columns", "Veritabanındaki bir tablonun tüm sütun isimlerini, tiplerini ve boyutlarını programatik olarak çekme.", 
                "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE\nFROM INFORMATION_SCHEMA.COLUMNS\nWHERE TABLE_NAME = 'Users'\nORDER BY ORDINAL_POSITION;");

            Add("SQL", "Output Parametreli Stored Procedure", "stored-procedure, output, parameters", "Veri ekleme işlemini yapıp, aynı anda yeni oluşan ID'yi dışarı (out) aktaran prosedür.", 
                "CREATE PROCEDURE sp_AddUser\n  @Name NVARCHAR(50),\n  @NewId INT OUTPUT\nAS\nBEGIN\n  INSERT INTO Users (Name) VALUES (@Name);\n  SET @NewId = SCOPE_IDENTITY(); -- Eklenen son ID'yi al\nEND;");
                
            return now;
        }
    }
}
