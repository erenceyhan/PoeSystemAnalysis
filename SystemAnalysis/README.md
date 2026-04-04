# SystemAnalysis

SystemAnalysis, Windows uzerinde ekran koordinatlari, klavye tuslari ve `Ctrl+C` ile alinan item metni uzerinden craft akislarini yoneten bir masaustu otomasyon uygulamasidir.

Bu surumde:

- arka plandaki otomasyon mantigi korunur
- arayuz `WPF` ile calisir
- gunluk kullanim icin sade release klasoru `publish/win-x64/` altindadir

## Teknoloji

- Dil: `C#`
- Platform: `.NET 10`
- Arayuz: `WPF`
- Windows entegrasyonu: `Win32 API`
- Clipboard kontrolu: `Ctrl+C` veya `Ctrl+Alt+C`

## Uygulamanin Temel Mantigi

Program genel olarak su sekilde calisir:

1. secili currency noktasina gider
2. item noktasina tiklar
3. item metnini kopyalayip kontrol eder
4. secili craft moduna gore devam eder veya itemi tamamlar
5. birden fazla item varsa tur mantigiyla itemlar arasinda doner

## Craft Modlari

`Craft Modu` alaninda 5 mod vardir.

### 1 Mod Alteration

- sadece alteration kullanir
- craft listesinde secili modlardan herhangi biri gelene kadar devam eder
- mod bulundugunda item tamamlanir
- augment kullanmaz

### Flask Modu

- once alteration ile `Craft Ayarlari 1-2` icindeki hedeflerden biri aranir
- ilk hedef geldikten sonra bir kez augment uygulanir
- son kontrolde:
  - craft modlarindan en az biri
  - augment modlarindan en az biri
  birlikte varsa item tamamlanir
- yoksa alteration dongusu bastan baslar

### 2 Mod Alteration + Augment

- alteration asamasinda craft ve augment listelerinin birlesimi aranir
- secili hedeflerden biri gelince augment asamasina gecer
- augment sonrasi itemde secili hedeflerden toplam en az `2 farkli mod` bulunmalidir
- 2 farkli hedef mod yoksa alteration dongusu basa doner

Bu mod genelde su senaryo icin kullanilir:

- ilk mod alteration ile gelir
- ikinci mod augment ile gelir
- iki hedef de tamamlandiginda item biter

### Fracture Cluster

Bu modun akisi ozeldir:

1. alteration noktasina sag tik
2. `Shift` basili tutulur
3. iteme bir kez sol tik
4. `Shift` basiliyken `Alt` tusu da basilir
5. iteme bir kez daha sol tik
6. `Alt` birakilir
7. item `Ctrl+C` ile kontrol edilir
8. hedef mod bulunmadiysa `Shift` basili kalirken dongu devam eder

Bu mod icin zamanlama sekmesinde ayri `Shift+Alt` araligi vardir.

### Flask Basma Modu

Bu mod digerlerinden tamamen ayri calisir.

- mouse koordinati kullanmaz
- item kontrol etmez
- log yazmaz
- ustte kalan durum penceresi gostermez
- otomatik fare/klavye mudahalesi ile durmaz
- `F8` ile baslar
- tekrar `F8` ile durur

Yaptigi tek sey:

1. `Flask Basma Modu` zamanina gore beklemek
2. klavyedeki `1` tusuna insan gibi kisa sure basmak
3. tekrar beklemek
4. sen `F8` ile durdurana kadar devam etmek

Zaman hesabı:

- `Sabit`
- `Ek Min`
- `Ek Max`

Toplam bekleme:

- `Sabit + rastgele(Ek Min..Ek Max)`

Tus basili kalma suresi:

- dahili olarak `60-100 ms`

## Mod Esik Mantigi

Her `Aranan Mod` ve `Augment Mod` satirinin kendi esik kutusu vardir.

Kural:

- mod metninde `#` varsa, yanindaki esik kullanilir
- mod metninde `#` yoksa, esik kutusu yok sayilir
- eslesme mantigi `>=` seklindedir

Ornek:

- mod: `#% increased Evasion Rating during Effect`
- esik: `55`

Kabul edilir:

- `55% ...`
- `56% ...`
- `60% ...`

Kabul edilmez:

- `54% ...`

### Guvenlik Kontrolu

Eger secili bir modda `#` varsa ama yanindaki esik bos ise:

- `F8` ile baslatmaz
- loga hata yazar
- kisa uyari sesi calar

### Mod Degisince

Bir combobox secimi degistiginde:

- yanindaki esik kutusu otomatik temizlenir

Bu, eski sayinin unutulup hatali eslesme yaratmasini engeller.

## Item Dongusu ve Tur Mantigi

Program birden fazla item noktasi ile calisabilir.

- `F7` ile her basista yeni item noktasi eklenir
- itemlar sirayla islenir
- tek itemda sonsuza kadar takilmaz

`Item Tur Limiti`:

- bir itemin tek turda kac kez denenebilecegini belirler
- limit dolarsa siradaki iteme gecer
- tur bitince tamamlanmayan itemlarla basa doner

## Alteration Noktasi Limiti

Bir alteration noktasi icin maksimum kullanim:

- `Alteration Nokta Limiti`

Program:

- `F6` ile birden fazla alteration noktasi alabilir
- bir noktanin kullanim limiti dolarsa siradaki alteration noktasina gecer

Ek stuck guvenligi:

- ayni alteration noktasinda arka arkaya `2` item takilirse
- o nokta bitmis kabul edilir
- siradaki alteration noktasina gecilir

## Stuck Algoritmasi

Program ayni itemde ayni clipboard metni `3 kez` ust uste gorurse bunu stuck kabul eder.

Bu durumda:

1. item takildi olarak isaretlenir
2. item konumuna gidilir
3. `Ctrl` basili halde `3` kez sol tik yapilir
4. item stash'e gonderilmeye zorlanir
5. item UI listesinden kaldirilir
6. siradaki iteme gecilir

Bu adim:

- normal craft sayacina eklenmez
- tamamlanan item ile ayni statude sayilmaz
- ozet logda ayri gorunur

## Tamamlanan Itemin Stashe Gonderilmesi

`Tamamlanan itemi Ctrl ile stashe gonder` kutusu vardir.

Aciksa:

- item tamamlandiginda `Ctrl + sol tik` ile stash'e gonderilir

Kapaliysa:

- item tamamlanir
- listeden duser
- ama yerinde birakilir

Not:

- stuck item stash'e gonderme davranisi her durumda ayri guvenlik adimi olarak calisir

## Ustte Kalan Durum Penceresi

Craft modlari calisirken ustte kalan kucuk bir pencere acilir.

Bu pencere su sayaclari gosterir:

- `Sol Tik`
- `Tamamlanan Item`
- `Takilan Item`

Bu pencere:

- ana pencere arkada kalsa bile gorunur
- craft biterse veya durursa kapanir
- `Flask Basma Modu`nda hic acilmaz

## Loglama

Normal craft modlarinda:

- ekrandaki log dolu kalir
- secili log klasorunde zaman damgali `.txt` dosyasi olusur
- istersen `Kopyalanan metni logla` ile clipboard metnini de loga eklersin

Loglarda gorulebilen seyler:

- secilen craft modu
- item dongusu
- secilen alteration noktasi
- secilen bekleme sureleri
- eslesmeler
- tur limiti bilgisi
- stuck algilama
- stash gonderimi
- item bazli ozet
- genel toplam

`Flask Basma Modu`nda:

- performans icin log yazilmaz

## Arayuz Sekmeleri

### Craft Ayarlari 1

- `Aranan Mod 1-7`

### Craft Ayarlari 2

- `Aranan Mod 8-15`

### Craft Modlari

- `Item Tur Limiti`
- `Alteration Nokta Limiti`
- `Craft Modu`
- `Tamamlanan itemi Ctrl ile stashe gonder`
- `Log Klasoru`

### Augment

- `Augment Mod 1-8`

### Zamanlama

Beklemeler rastgele araliklardan secilir:

- `Baslamadan Once`
- `Aksiyon Arasi`
- `Craft Sonrasi`
- `Shift+Alt`
- `Inspect Oncesi`
- `Kisayol Sonrasi`
- `Flask Basma Modu`
  - `Sabit`
  - `Ek Min`
  - `Ek Max`

### Noktalar

- `F6`: alteration noktasi ekler
- `F10`: augment noktasini kaydeder
- `F7`: item noktasi ekler
- alteration listesi
- augment noktasi
- item noktasi listesi

Not:

- item noktalari oturumluktur
- program kapaninca item listesi sifirlanir
- diger ayarlar kalicidir

### Kayitli Modlar

- yeni mod ekleme
- secileni silme
- hepsini temizleme
- tum combobox listelerini bu kayitli modlar besler

## Alt Kontroller

- `Baslat (F8)`
- `Durdur (F9)`
- `Temizle`
- `Kapat`
- `Kopyalanan metni logla`

`Temizle`:

- craft mod secimlerini bosaltir
- augment mod secimlerini bosaltir
- esik kutularini bosaltir

Ama sunlara dokunmaz:

- kayitli mod listesi
- alteration noktalari
- augment noktasi
- genel kalici ayarlar

## Klavye Kisayollari

Genel modlarda:

- `F6`: alteration noktasi ekle
- `F7`: item noktasi ekle
- `F8`: baslat
- `F9`: durdur
- `F10`: augment noktasi kaydet
- `Esc`: aktif islemi durdur

Normal craft modlari calisirken:

- fare hareketi
- fare tiki
- klavye tusu

islemi otomatik durdurur.

`Flask Basma Modu` istisnadir:

- mouse/klavye mudahalesi ile durmaz
- tekrar `F8` ile durdurulur

## Ayar Dosyalari

Guncel kullanilan ayar dosyasi:

```text
C:\Users\gamer\Desktop\crafter\publish\win-x64\user-settings.json
```

Varsayilan sablon dosyasi:

```text
C:\Users\gamer\Desktop\crafter\publish\win-x64\appsettings.json
```

Mantik:

- program once `user-settings.json` kullanir
- yoksa `appsettings.json` kopyalanir
- eski LocalAppData ayari varsa oradan da tasiyabilir

Bu sayede:

- ayarlarini GitHub'a koyabilirsin
- baska bilgisayara tasiyabilirsin
- publish klasorunu yedekleyebilirsin

## Hangi Bilgiler Kalici

Kalici olanlar:

- craft modu secimi
- craft mod hedefleri
- augment hedefleri
- tum esik kutulari
- zamanlama degerleri
- alteration noktalari
- augment noktasi
- log klasoru
- `Kopyalanan metni logla`
- `Tamamlanan itemi Ctrl ile stashe gonder`
- kayitli mod listesi
- `Item Tur Limiti`
- `Alteration Nokta Limiti`

Kalici olmayan:

- item noktasi listesi

## Klasor Duzeni

Kaynak kod:

```text
C:\Users\gamer\Desktop\crafter\SystemAnalysis
```

Kullanilacak sade release klasoru:

```text
C:\Users\gamer\Desktop\crafter\publish\win-x64
```

Calistirilacak exe:

```text
C:\Users\gamer\Desktop\crafter\publish\win-x64\SystemAnalysis.exe
```

`bin/` ve `obj/` klasorleri:

- gecici build klasorleridir
- gunluk kullanim icin gerekli degildir

## Debug'da Calistirma

```powershell
cd C:\Users\gamer\Desktop\crafter
$env:DOTNET_CLI_HOME='C:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet build .\SystemAnalysis\SystemAnalysis.csproj
& "C:\Users\gamer\Desktop\crafter\SystemAnalysis\bin\Debug\net10.0-windows\SystemAnalysis.exe"
```

## Release Publish Alma

Sade release klasorune publish almak icin:

```powershell
cd C:\Users\gamer\Desktop\crafter
$env:DOTNET_CLI_HOME='C:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet publish .\SystemAnalysis\SystemAnalysis.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

Sonra dogrudan bunu calistir:

```powershell
& "C:\Users\gamer\Desktop\crafter\publish\win-x64\SystemAnalysis.exe"
```

## Git ve Push Hakkinda

Repoya genelde su gruplar gider:

- kaynak kodlar `SystemAnalysis/`
- sade release kopyasi `publish/win-x64/`
- istersen `user-settings.json`

Repoya gitmeyenler:

- `bin/`
- `obj/`

Bu sayede:

- kaynak kodu cekip yeniden build alabilirsin
- ister sadece publish klasorunu kullanabilirsin
- istersen kullanilan ayar dosyasini da birlikte tasiyabilirsin

## Pratik Kullanim Onerisi

Geri dondugunde en rahat akis genelde su olur:

1. `publish/win-x64/SystemAnalysis.exe` ac
2. craft modunu kontrol et
3. gerekiyorsa item noktalarini yeniden sec
4. log klasoru ve mod secimlerini gozden gecir
5. `F8` ile baslat

## Not

Bu branch'te arayuz `WPF`'ye gecirilmis durumdadir. Arka plandaki otomasyon mantigi korunur; hedef, sadece UI tarafini daha hizli ve daha bakimi kolay hale getirmektir.
