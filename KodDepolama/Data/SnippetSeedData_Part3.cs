using KodDepolama;
using System;
using System.Collections.Generic;

namespace DevSnippetManager.Data
{
    public static class SnippetSeedData_Part3
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
            // Python (12 New Professional Examples)
            // ==========================================
            Add("Python", "FastAPI ile Asenkron API Endpoint", "fastapi, async, web, pydantic", "FastAPI ve Pydantic kullanarak, asenkron veritabanı isteği atan ve veri doğrulaması (validation) yapan endpoint.", 
                "from fastapi import FastAPI, HTTPException\nfrom pydantic import BaseModel\n\napp = FastAPI()\n\nclass Item(BaseModel):\n    name: str\n    price: float\n\n@app.post(\"/items/\")\nasync def create_item(item: Item):\n    # await db.save(item)\n    return {\"status\": \"success\", \"data\": item.dict()}");

            Add("Python", "Celery Asenkron Görev Kuyruğu", "celery, background, queue, tasks", "Uzun süren işlemleri (Örn: E-posta gönderimi, rapor oluşturma) arka planda asenkron işlemek için Celery worker yapısı.", 
                "from celery import Celery\nimport time\n\napp = Celery('tasks', broker='redis://localhost:6379/0')\n\n@app.task\ndef send_email(user_email):\n    time.sleep(5)  # E-posta gönderme simülasyonu\n    print(f\"{user_email} adresine e-posta gönderildi!\")\n    return True");

            Add("Python", "BeautifulSoup ile Web Scraping", "scraping, html, parsing, bs4", "Requests ve BeautifulSoup kütüphanelerini kullanarak bir web sitesindeki tüm haber başlıklarını çekme botu.", 
                "import requests\nfrom bs4 import BeautifulSoup\n\nurl = 'https://news.ycombinator.com/'\nresponse = requests.get(url)\nsoup = BeautifulSoup(response.text, 'html.parser')\n\ntitles = soup.find_all('span', class_='titleline')\nfor title in titles:\n    print(title.find('a').text)");

            Add("Python", "Pytest ile Parametrik Birim Testi", "testing, pytest, unit-test", "Pytest kütüphanesindeki parametrize dekoratörü ile bir fonksiyonu birden fazla veri setiyle tek seferde test etme.", 
                "import pytest\nfrom math_utils import add\n\n@pytest.mark.parametrize(\"a,b,expected\", [\n    (1, 2, 3),\n    (-1, 1, 0),\n    (10, 20, 30)\n])\ndef test_add(a, b, expected):\n    assert add(a, b) == expected");

            Add("Python", "Scikit-Learn Basit Makine Öğrenimi", "ml, scikit-learn, regression, ai", "Veri kümesindeki girdilerle basit bir Lineer Regresyon modeli eğitip yeni veriler için tahminde bulunma.", 
                "from sklearn.linear_model import LinearRegression\nimport numpy as np\n\n# Eğitim verisi (Alan, Oda Sayısı) -> Fiyat\nX = np.array([[100, 2], [150, 3], [120, 2], [200, 4]])\ny = np.array([300, 450, 350, 600])\n\nmodel = LinearRegression()\nmodel.fit(X, y)\n\ntahmin = model.predict([[180, 3]])\nprint(\"Tahmini Fiyat:\", tahmin[0])");

            Add("Python", "Regex (Düzenli İfadeler) ile İleri Düzey Ayıklama", "regex, parsing, string, text-processing", "Bir metin içerisindeki gizli e-posta adreslerini ve telefon numaralarını Regex (re) modülü kullanarak bulma.", 
                "import re\ntext = \"İletişim için ali@ornek.com veya veli.yilmaz@sirket.org kullanın.\"\n\n# E-posta deseni\npattern = r'[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\\.[a-zA-Z0-9-.]+'\nemails = re.findall(pattern, text)\nprint(\"Bulunan E-postalar:\", emails)");

            Add("Python", "Asyncio.gather ile Eşzamanlı (Concurrent) Görevler", "asyncio, concurrency, async, parallel", "Birden fazla asenkron fonksiyonu beklemek yerine asyncio.gather() ile hepsini eşzamanlı çalıştırıp süreyi kısaltma.", 
                "import asyncio\n\nasync def fetch_data(id):\n    await asyncio.sleep(2) # Ağ gecikmesi\n    return f\"Veri {id}\"\n\nasync def main():\n    # İki işlemi 4 saniye yerine 2 saniyede bitir\n    results = await asyncio.gather(fetch_data(1), fetch_data(2))\n    print(results)\n\nasyncio.run(main())");

            Add("Python", "Requests ve urllib3 ile Gelişmiş Retry (Yeniden Deneme)", "requests, http, retry, resilience", "Bağlantı koptuğunda veya 500 hatası geldiğinde Requests kütüphanesinin otomatik tekrar denemesini sağlayan bağdaştırıcı.", 
                "import requests\nfrom requests.adapters import HTTPAdapter\nfrom urllib3.util.retry import Retry\n\nsession = requests.Session()\nretry = Retry(total=3, backoff_factor=1, status_forcelist=[500, 502, 503, 504])\nadapter = HTTPAdapter(max_retries=retry)\n\nsession.mount('http://', adapter)\nsession.mount('https://', adapter)\n\nresponse = session.get('https://api.ornek.com')");

            Add("Python", "ContextManager ile Veritabanı Bağlantısı Yönetimi", "database, contextmanager, resource, with", "Veritabanı bağlantılarını ve commit/rollback işlemlerini otomatik halleden temiz with-bloğu yöneticisi (@contextmanager).", 
                "from contextlib import contextmanager\nimport sqlite3\n\n@contextmanager\ndef db_baglantisi(db_name):\n    conn = sqlite3.connect(db_name)\n    try:\n        yield conn.cursor()\n        conn.commit()\n    except Exception:\n        conn.rollback()\n        raise\n    finally:\n        conn.close()\n\n# Kullanımı:\n# with db_baglantisi('test.db') as cursor:\n#     cursor.execute('INSERT INTO Tablo VALUES (?)', ('Test',))");

            Add("Python", "Boto3 ile AWS S3 Dosya Yükleme", "aws, s3, cloud, boto3", "Amazon Web Services (AWS) S3 bucket'ına Python ile programatik olarak dosya yükleme (Upload) fonksiyonu.", 
                "import boto3\nfrom botocore.exceptions import NoCredentialsError\n\ndef upload_to_s3(local_file, bucket, s3_file):\n    s3 = boto3.client('s3', aws_access_key_id='ANAHTAR', aws_secret_access_key='GIZLI')\n    try:\n        s3.upload_file(local_file, bucket, s3_file)\n        print(\"Yükleme Başarılı\")\n    except FileNotFoundError:\n        print(\"Dosya bulunamadı\")\n    except NoCredentialsError:\n        print(\"Kimlik bilgileri hatalı\")");

            Add("Python", "Python Logging Formatlama", "logging, sysadmin, diagnostics, debug", "Sadece print() kullanmak yerine, zaman, seviye ve dosya adını gösteren kurumsal loglama (logging) konfigürasyonu.", 
                "import logging\n\nlogging.basicConfig(\n    level=logging.INFO,\n    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',\n    handlers=[\n        logging.FileHandler(\"app.log\"),\n        logging.StreamHandler()\n    ]\n)\nlogger = logging.getLogger(__name__)\nlogger.info(\"Uygulama başlatıldı.\")");

            Add("Python", "Map, Filter, Reduce Fonksiyonel Programlama", "functional, map, filter, reduce", "Liste işlemlerinde fonksiyonel programlama pratiklerini kullanarak dizileri tek satırda dönüştürme ve indirgeme.", 
                "from functools import reduce\n\nsayilar = [1, 2, 3, 4, 5, 6, 7, 8]\nciftler = list(filter(lambda x: x % 2 == 0, sayilar))\nkareler = list(map(lambda x: x**2, ciftler))\ntoplam = reduce(lambda x, y: x + y, kareler)\n\nprint(f\"Çiftlerin kareleri toplamı: {toplam}\")");

            // ==========================================
            // Java (10 New Professional Examples)
            // ==========================================
            Add("Java", "Spring Boot REST Controller", "spring, rest, api, annotations", "Spring Boot framework ile JSON dönen, bağımlılık enjeksiyonu (DI) kullanan temel bir REST Controller.", 
                "@RestController\n@RequestMapping(\"/api/users\")\npublic class UserController {\n    private final UserService userService;\n\n    @Autowired\n    public UserController(UserService userService) { this.userService = userService; }\n\n    @GetMapping(\"/{id}\")\n    public ResponseEntity<UserDto> getUser(@PathVariable Long id) {\n        return ResponseEntity.ok(userService.getUserById(id));\n    }\n}");

            Add("Java", "Spring Data JPA Custom Repository", "jpa, spring-data, database, orm", "JPA içerisinde hazır metotlar dışında, kendi özel SQL sorgumuzu (@Query) tanımladığımız Repository arayüzü.", 
                "@Repository\npublic interface UserRepository extends JpaRepository<User, Long> {\n    @Query(\"SELECT u FROM User u WHERE u.email = ?1 AND u.isActive = true\")\n    Optional<User> findActiveUserByEmail(String email);\n}");

            Add("Java", "Mockito ve JUnit5 Birim Testi", "testing, mockito, junit, tdd", "Mockito ile veritabanı sınıfını mock'layarak (sahte obje) Spring servis katmanını veritabanına bağlanmadan test etme.", 
                "@ExtendWith(MockitoExtension.class)\nclass UserServiceTest {\n    @Mock private UserRepository userRepository;\n    @InjectMocks private UserService userService;\n\n    @Test\n    void findUser_ReturnsUser() {\n        when(userRepository.findById(1L)).thenReturn(Optional.of(new User(1L, \"Ali\")));\n        User result = userService.getUser(1L);\n        assertEquals(\"Ali\", result.getName());\n    }\n}");

            Add("Java", "Java 14+ Record Sınıfları", "java14, records, dto, boilerplate", "Getter, setter, equals ve hashCode metotlarını otomatik yazan, sadece veritabanı/API nesnesi taşımak için immutable Record.", 
                "public record CustomerDto(Long id, String fullName, String email) {\n    // Parametreleri doğrulayan compact constructor\n    public CustomerDto {\n        if (email == null || !email.contains(\"@\")) {\n            throw new IllegalArgumentException(\"Geçersiz E-posta\");\n        }\n    }\n}");

            Add("Java", "HttpClient ile Asenkron Ağ İsteği (Java 11+)", "httpclient, async, java11, web", "Java 11 ile yerleşik gelen yeni HttpClient kütüphanesini kullanarak asenkron GET isteği atma.", 
                "HttpClient client = HttpClient.newHttpClient();\nHttpRequest request = HttpRequest.newBuilder()\n      .uri(URI.create(\"https://api.github.com/\"))\n      .build();\n\nclient.sendAsync(request, HttpResponse.BodyHandlers.ofString())\n      .thenApply(HttpResponse::body)\n      .thenAccept(System.out::println)\n      .join();");

            Add("Java", "ReentrantReadWriteLock ile Performanslı Senkronizasyon", "concurrency, threads, locks, performance", "Çoklu thread ortamlarında salt-okuma işlemlerini engellemeyip, sadece yazma işlemi varken kilitleyen (lock) yapı.", 
                "private final ReentrantReadWriteLock lock = new ReentrantReadWriteLock();\nprivate String sharedData = \"Veri\";\n\npublic String readData() {\n    lock.readLock().lock();\n    try { return sharedData; }\n    finally { lock.readLock().unlock(); }\n}\n\npublic void writeData(String newData) {\n    lock.writeLock().lock();\n    try { sharedData = newData; }\n    finally { lock.writeLock().unlock(); }\n}");

            Add("Java", "Jackson ile JSON Serileştirme", "json, jackson, parsing, dtos", "Bir Java nesnesini JSON string'ine çevirme veya JSON string'ini Java nesnesine (ObjectMapper) cast etme.", 
                "ObjectMapper mapper = new ObjectMapper();\n\n// Java objesini JSON string yapmak\nString json = mapper.writeValueAsString(new User(1, \"Veli\"));\n\n// JSON string'i Java nesnesine çevirmek\nString incomingJson = \"{\\\"id\\\":2, \\\"name\\\":\\\"Ayşe\\\"}\";\nUser user = mapper.readValue(incomingJson, User.class);");

            Add("Java", "Davranışsal Enum (Enum with Behavior)", "enum, oop, strategy, design", "Sadece sabit değerler tutmak yerine, her bir değeri farklı iş kuralları (metotlar) içeren gelişmiş Enum yapısı.", 
                "public enum Operation {\n    ADD { public int apply(int x, int y) { return x + y; } },\n    SUBTRACT { public int apply(int x, int y) { return x - y; } },\n    MULTIPLY { public int apply(int x, int y) { return x * y; } };\n\n    public abstract int apply(int x, int y);\n}\n// int result = Operation.ADD.apply(5, 3); // 8");

            Add("Java", "Scheduled (Zamanlanmış) Görev (Cron)", "spring, cron, scheduling, background", "Spring Boot'ta her gün gece 02:00'de otomatik olarak çalışacak periyodik (cron job) temizlik/raporlama metodu.", 
                "@Component\npublic class ReportScheduler {\n    // saniye dakika saat gün ay haftanın_günü\n    @Scheduled(cron = \"0 0 2 * * ?\")\n    public void generateDailyReport() {\n        System.out.println(\"Günlük rapor oluşturuluyor...\");\n        // veritabanı raporlama kodları\n    }\n}");

            Add("Java", "Spring Security - Şifre Hashleme (BCrypt)", "security, passwords, bcrypt, spring", "Kullanıcı şifrelerini veritabanına kaydetmeden önce BCrypt algoritması ile güvenli şekilde hash'leyen konfigürasyon.", 
                "@Bean\npublic PasswordEncoder passwordEncoder() {\n    return new BCryptPasswordEncoder(12); // Work factor\n}\n\n// Kullanımı:\n// String hashliSifre = passwordEncoder.encode(\"123456\");\n// boolean dogruMu = passwordEncoder.matches(\"123456\", dbHash);");

            // ==========================================
            // HTML/CSS (8 New Professional Examples)
            // ==========================================
            Add("HTML/CSS", "Neumorphism (Soft UI) Buton", "css, design, ui, neumorphism", "Arka planla aynı renge sahip, ışık ve gölge oyunlarıyla 3 boyutlu derinlik verilmiş modern Neumorphism buton tasarımı.", 
                "button {\n  background: #e0e5ec;\n  color: #333;\n  border: none;\n  padding: 15px 30px;\n  border-radius: 10px;\n  box-shadow: 9px 9px 16px rgb(163,177,198,0.6), -9px -9px 16px rgba(255,255,255, 0.5);\n  transition: all 0.2s;\n}\nbutton:active {\n  box-shadow: inset 6px 6px 10px 0 rgba(163,177,198,0.6), inset -6px -6px 10px 0 rgba(255,255,255,0.5);\n}");

            Add("HTML/CSS", "CSS clamp() ile Responsive Tipografi", "css, responsive, typography, clamp", "Medya sorgusu (media query) kullanmadan, ekran küçüldükçe yazının da orantılı olarak küçülmesini sağlayan modern CSS fonksiyonu.", 
                "h1 {\n  /* Minimum 1.5rem, ekran genişliğine göre 5vw, Maksimum 3rem */\n  font-size: clamp(1.5rem, 5vw, 3rem);\n  line-height: 1.2;\n}");

            Add("HTML/CSS", "Yalnızca CSS ile Tooltip (İpucu Balonu)", "css, tooltip, attr, pseudo-elements", "Hiçbir JavaScript kullanmadan, data-* özelliklerini okuyarak HTML elementinin üzerine gelince açılan balon (tooltip).", 
                ".tooltip {\n  position: relative;\n  cursor: help;\n}\n.tooltip::after {\n  content: attr(data-tooltip);\n  position: absolute;\n  bottom: 100%;\n  left: 50%;\n  transform: translateX(-50%);\n  background: #333;\n  color: #fff;\n  padding: 5px 10px;\n  border-radius: 4px;\n  opacity: 0;\n  transition: opacity 0.3s;\n}\n.tooltip:hover::after { opacity: 1; }");

            Add("HTML/CSS", "Skeleton Yükleme Ekranı Animasyonu", "css, loading, ux, skeleton", "Veri gelirken boş ekranda gösterilen, soldan sağa kayan parlayan grilik animasyonu (Skeleton Screen).", 
                ".skeleton {\n  background: #eee;\n  background: linear-gradient(110deg, #ececec 8%, #f5f5f5 18%, #ececec 33%);\n  background-size: 200% 100%;\n  animation: shimmer 1.5s linear infinite;\n}\n@keyframes shimmer {\n  0% { background-position: 200% 0; }\n  100% { background-position: -200% 0; }\n}");

            Add("HTML/CSS", "Scroll Snapping (Kaydırma Kilidi)", "css, scroll, ui, snapping", "TikTok veya Instagram Reels gibi, aşağı kaydırıldığında bir sonraki bölüme otomatik oturan (hizalanan) CSS yapısı.", 
                ".container {\n  scroll-snap-type: y mandatory;\n  overflow-y: scroll;\n  height: 100vh;\n}\n.section {\n  scroll-snap-align: start;\n  height: 100vh;\n}");

            Add("HTML/CSS", "CSS Grid Masonry (Pinterest Tipi Düzen)", "css, grid, masonry, layout", "Pinterest tarzı farklı boylardaki kartların aralarında boşluk kalmayacak şekilde otomatik dizilmesi (Masonry) taslağı.", 
                ".masonry {\n  column-count: 3;\n  column-gap: 20px;\n}\n.masonry-item {\n  break-inside: avoid;\n  margin-bottom: 20px;\n  width: 100%;\n}");

            Add("HTML/CSS", "Picture Etiketi ile Duyarlı Resimler", "html, images, responsive, optimization", "Farklı ekran boyutlarında (mobil, tablet, masaüstü) farklı boyutlarda/çözünürlükte görselleri yükleyerek veri tasarrufu sağlayan HTML5 bileşeni.", 
                "<picture>\n  <source media=\"(min-width: 1024px)\" srcset=\"large.jpg\">\n  <source media=\"(min-width: 768px)\" srcset=\"medium.jpg\">\n  <img src=\"small.jpg\" alt=\"Açıklama\" loading=\"lazy\">\n</picture>");

            Add("HTML/CSS", "Özel Tasarım Checkbox (Görünmez Input)", "css, forms, ui, checkbox", "Çirkin varsayılan tarayıcı checkbox'ını gizleyip, yerine label ve CSS pseudo-element ile şık bir onay kutusu tasarlama.", 
                "input[type=\"checkbox\"] {\n  display: none; /* Asıl kutuyu gizle */\n}\nlabel {\n  position: relative;\n  padding-left: 25px;\n  cursor: pointer;\n}\nlabel::before {\n  content: \"\";\n  position: absolute;\n  left: 0; top: 0;\n  width: 16px; height: 16px;\n  border: 2px solid #ccc;\n}\ninput[type=\"checkbox\"]:checked + label::before {\n  background: #2196F3;\n  border-color: #2196F3;\n}");
                
            return now;
        }
    }
}
