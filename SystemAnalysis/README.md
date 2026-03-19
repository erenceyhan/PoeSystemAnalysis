# SystemAnalysis

SystemAnalysis, ekrandaki belirli noktalara tiklayip item metnini `Ctrl+C` ile okuyarak craft akislarini kontrollu sekilde yoneten Windows Forms uygulamasidir.

## Neler Yapabilir

- Bir veya iki currency noktasi ile calisir
- Birden fazla item noktasi tanimlayabilir
- Her tiklamadan sonra itemi ayni noktada kontrol eder
- Itemlar arasinda tur mantigiyla doner
- Tamamlanan itemlari hem mantiksal listeden hem UI listesinden kaldirir
- Ekranda ustte kalan canli durum penceresi gosterir
- Loglari hem ekranda hem `.txt` dosyasinda tutar
- Kullanici fare/klavye ile mudahale ederse otomatik durur

## Calisma Akislari

Program iki ana akis destekler:

- `Sadece Alteration`
- `Alteration + Augment`

### Sadece Alteration

Her item icin:

1. `Alteration` noktasina sag tik yapar
2. `Shift` basili tutar
3. iteme sol tik atar
4. ayni item ustunde `Ctrl+C` ile kontrol eder
5. `Aranan Mod` kutularindan biri gelirse item tamamlanir

### Alteration + Augment

Her item icin:

1. Once alteration asamasi calisir
2. `Craft Ayarlari 1-2` secimlerinden biri gelene kadar alteration denenir
3. Hedef mod bulununca `Augment` noktasina bir kez sag tik yapar
4. iteme bir kez sol tik yapar
5. tekrar kontrol eder
6. Hem alteration tarafindaki modlardan biri, hem de `Augment` sekmesindeki modlardan biri varsa item tamamlanir
7. Yoksa alteration dongusu basa doner

## Item Tur Limiti

`Item Tur Limiti` alani bir itemin tek turda en fazla kac sol tik alacagini belirler.

Ornek:

- deger `300` ise bir item tek turda en fazla `300` sol tik alir
- tamamlanmazsa siradaki itema gecer
- tur sonuna gelindiginde tamamlanmayan itemlarla basa donulur

Bu sayede tek itemda takilip kalinmaz.

## Mod ve Esik Mantigi

### Aranan Modlar

- `Craft Ayarlari 1`: `Aranan Mod 1-7`
- `Craft Ayarlari 2`: `Aranan Mod 8-15`
- `Augment`: `Augment Mod 1-8`

Her satirda:

- bir `combobox`
- o satira ait bir esik kutusu

### Esik Kurali

- Mod metninde `#` varsa, o satirin yanindaki esik kutusu kullanilir
- Mod metninde `#` yoksa, yanindaki kutu dikkate alinmaz
- Kural `>=` seklindedir

Ornek:

- mod: `#% increased Evasion Rating during Effect`
- esik: `55`

Kabul edilen degerler:

- `55% ...`
- `56% ...`
- `60% ...`

### Guvenlik Kurali

Eger secili bir modda `#` varsa ama o satirin esik kutusu bossa:

- `F8` ile baslatamazsin
- loga hata yazilir
- kisa bir uyari sesi calar

### Secim Degisince Ne Olur

Bir mod secimi degistiginde:

- o satirin esik kutusu otomatik temizlenir

Boylece eski bir sayi unutulup yanlis eslesme yaratmaz.

## Arayuz Sekmeleri

### Craft Ayarlari 1

- `Aranan Mod 1-7`
- `Item Tur Limiti`
- `Augment Akisi`
- `Log Klasoru`
- `Kopyala Goster`

### Craft Ayarlari 2

- `Aranan Mod 8-15`

### Augment

- `Augment Mod 1-8`

### Zamanlama

Tum beklemeler `Min/Max` araliginda rastgele secilir:

- `Baslamadan Once`
- `Aksiyon Arasi`
- `Craft Sonrasi`
- `Inspect Oncesi`
- `Kisayol Sonrasi`

### Noktalar

- `F6` currency noktalarini sirayla kaydeder
  - ilk `F6`: `Alteration`
  - ikinci `F6`: `Augment`
  - sonra tekrar basa doner
- `F7` her basista yeni item noktasi ekler
- `Currencyleri Sifirla`
- `Sonuncuyu Sil`
- `Listeyi Temizle`

Not:

- item noktalarini oturumluk tutar
- program kapaninca item noktasi listesi sifirlanir
- diger ayarlar kalici kaydedilir

### Kayitli Modlar

Buraya eklenen modlar kalici olarak saklanir ve tum secim kutularinda gorunur.

## Alt Butonlar

- `Baslat (F8)`
- `Durdur (F9)`
- `Temizle`
- `Kapat`

`Temizle` sunlari yapar:

- tum `Aranan Mod` secimlerini bosaltir
- tum `Augment Mod` secimlerini bosaltir
- tum esik kutularini temizler

Ama sunlara dokunmaz:

- kayitli mod listesi
- currency noktalari
- kalici ayarlar

## Kisa Yol Tuslari

- `F6`: currency noktalarini sirayla kaydet
- `F7`: item noktasi ekle
- `F8`: baslat
- `F9`: durdur
- `Esc`: aktif islemi durdur

Program calisirken:

- fareyi hareket ettirirsen
- fare ile tiklarsan
- klavyede tusa basarsan

otomatik durur.

## Ustte Kalan Durum Penceresi

`F8` ile calisma basladiginda ayri bir kucuk pencere acilir.

Bu pencere:

- her seyin ustunde kalir
- sag ust tarafta gorunur
- iki buyuk sayi gosterir:
  - `Sol Tik`
  - `Tamamlanan Item`

Boylece ana pencere arkada kalsa bile kac tik atildigini ve kac itemin bittigini gorebilirsin.

Islem bitince veya durunca bu pencere kapanir.

## Loglama ve Uyari

- `F8` ile her calistirmada secili klasorde tarih-saat adli yeni bir `.txt` log dosyasi olusur
- ekrandaki loglar ayni anda bu dosyaya da yazilir
- `Kopyala Goster` aciksa clipboard'a gelen metinler de loga yazilir
- baslatma oncesi hata varsa kisa bir uyari sesi calar
- her item icin alteration, augment, toplam sol tik ve sure ozetlenir
- tum islem sonunda genel ozet yazilir

Loglarda gorulebilecek onemli satirlar:

- hangi akisin calistigi
- hangi itemda olundugu
- secilen bekleme suresi
- bulunan eslesmeler
- tur limiti doldu bilgisi
- tamamlanan itemin listeden kaldirildigi bilgisi

## Ayarlarin Kaydi

Kalici olarak saklananlar:

- secili `Aranan Mod` kutulari
- secili `Augment Mod` kutulari
- satir bazli esik kutulari
- `Item Tur Limiti`
- `Augment Akisi`
- zamanlama degerleri
- `Log Klasoru`
- `Kopyala Goster`
- currency noktalari
- kayitli mod listesi

Kalici olmayan:

- item noktasi listesi

## Dogru Kullanim Icin Notlar

- `Ctrl+C` ile item bilgisinin gercekten panoya geldigini once manuel denemek iyi olur
- item penceresi veya oyun arayuzu yer degistirirse koordinatlari yeniden kaydetmek gerekir
- bos mod kutulari tamamen yok sayilir
- item tamamlandiginda UI listesinden de silinir

## Debug'da Calistirma

```powershell
cd C:\Users\gamer\Desktop\crafter\SystemAnalysis
$env:DOTNET_CLI_HOME='C:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet build
.\bin\Debug\net10.0-windows\SystemAnalysis.exe
```

Dogrudan debug exe acmak icin:

```powershell
& "C:\Users\gamer\Desktop\crafter\SystemAnalysis\bin\Debug\net10.0-windows\SystemAnalysis.exe"
```

## Release Publish Alma

```powershell
cd C:\Users\gamer\Desktop\crafter
$env:DOTNET_CLI_HOME='C:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet restore .\SystemAnalysis\SystemAnalysis.csproj -r win-x64
dotnet publish .\SystemAnalysis\SystemAnalysis.csproj -c Release -r win-x64 --self-contained true
```

Olusan exe:

```text
C:\Users\gamer\Desktop\crafter\SystemAnalysis\bin\Release\net10.0-windows\win-x64\publish\SystemAnalysis.exe
```

Release exe acmak icin:

```powershell
& "C:\Users\gamer\Desktop\crafter\SystemAnalysis\bin\Release\net10.0-windows\win-x64\publish\SystemAnalysis.exe"
```

## Repo Duzeni

- kaynak kodlar: `SystemAnalysis/`
- guncel publish kopyasi: `publish/win-x64/`
- debug/release build klasorleri: `bin/`, `obj/`

## Hatirlatma

Projeye geri dondugunde en hizli baslangic genelde su olur:

1. debug test icin `dotnet build` + debug exe
2. son kullanilan ayarlari kontrol et
3. item noktalarini yeniden sec
4. gerekirse release publish al
