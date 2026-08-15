# DevVault

DevVault, kod parcalarinizi yerel bilgisayarda saklayan basit bir WinForms snippet yoneticisidir.

## Ozellikler

- Baslik, dil, etiket, not ve kod alanlariyla snippet kaydetme
- Baslik, etiket, not ve kod iceriginde arama
- Dile gore filtreleme
- JSON ice aktarma ve disa aktarma
- Olusturma ve guncelleme tarihlerini takip etme
- Pencere boyutuna uyum saglayan daha esnek arayuz
- Ilk acilista C#, Java, Python, SQL, JavaScript, HTML/CSS ve Diger icin 50'ser ornek snippet

## Veri Saklama

Uygulama verileri kullanici profilindeki uygulama verisi klasorunde saklanir:

```text
%APPDATA%\DevSnippetManager\snippets.json
```

SQL Server bagimliligi kaldirildi. Boylece uygulama ek veritabani kurulumu olmadan calisir.

Veri dosyasi henuz yoksa uygulama ilk acilista 7 dil/kategori icin toplam 350 ornek snippet olusturur. Eski 7 ornekli demo veri dosyasi algilanirsa otomatik olarak 350 orneklik yeni sete yukseltir.

## Gelistirme

Projeyi derlemek icin:

```powershell
dotnet build KodDepolama\DevSnippetManager.csproj
```
