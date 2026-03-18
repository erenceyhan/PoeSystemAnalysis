# SystemAnalysis

Bu surum, ekrandaki belirli noktalara tiklayip item metnini `Ctrl+C` ile okuyarak craft akislarini sirali ve kontrollu sekilde yoneten Windows Forms uygulamasidir.

## Ne Yapar

- Bir veya iki currency noktasi ile calisir.
- Birden fazla item noktasi tanimlayabilirsin.
- Her tiklamadan sonra itemi ayni noktada kontrol eder.
- Fare hareketi, tiklama veya klavye tusu algilanirsa otomatik durur.
- Loglari hem ekranda hem de `.txt` dosyasinda tutar.
- Itemlar tamamlandikca UI listesinden kaldirilir.

## Temel Mantik

Program iki ana akisi destekler:

- `Sadece Alteration`
- `Alteration + Augment`

Hangi akisin calisacagi `Augment Akisi` kutusuna gore belirlenir.

### Sadece Alteration

Her item icin:

1. `Alteration` noktasina sag tik yapar.
2. `Shift` basili tutar.
3. Iteme sol tik atar.
4. Ayni item ustunde `Ctrl+C` ile metni okur.
5. `Aranan Mod` kutularindan biri gelirse item tamamlanir.
6. Gelmezse devam eder.

### Alteration + Augment

Her item icin:

1. Once alteration asamasi calisir.
2. `Craft Ayarlari 1` ve `Craft Ayarlari 2` sekmelerindeki modlardan biri gelene kadar alteration denenir.
3. Hedef mod bulunduktan sonra `Augment` noktasina bir kez sag tik yapar.
4. Iteme bir kez sol tik yapar.
5. Tekrar kontrol eder.
6. Hem alteration tarafindaki modlardan biri, hem de `Augment` sekmesindeki modlardan biri varsa item tamamlanir.
7. Yoksa alteration dongusu basa doner.

## Item Tur Limiti

`Item Tur Limiti` alani itemlar arasinda donmeli deneme yapmak icindir.

Ornek:

- deger `300` ise
- bir item bu turda en fazla `300` sol tik alir
- tamamlanmazsa siradaki itema gecer
- tum itemler bir tur donunce tamamlanmayanlarla basa doner

Bu sayede tek itemda takilip kalmaz.

## Esik Mantigi

Artik her mod satirinin kendi esik kutusu vardir.

Kural:

- Mod metninde `#` varsa, ayni satirdaki esik kutusundaki sayi kullanilir.
- Mod metninde `#` yoksa, yanindaki kutu dikkate alinmaz.

Ornek:

- mod: `#% increased Evasion Rating during Effect`
- esik: `55`

Su degerler kabul edilir:

- `55% increased Evasion Rating during Effect`
- `56% increased Evasion Rating during Effect`
- `60% increased Evasion Rating during Effect`

Yani kural `>=` seklindedir.

Eger secili bir modda `#` varsa ama o satirin esik kutusu bossa, `F8` ile baslatirken uygulama baslamaz ve logda uyari verir.

## Arayuz Sekmeleri

### Craft Ayarlari 1

- `Aranan Mod 1-7`
- her satirda:
  - bir `combobox`
  - bir esik kutusu
- `Item Tur Limiti`
- `Augment Akisi`
- `Log Klasoru`
- `Kopyala Goster`

### Craft Ayarlari 2

- `Aranan Mod 8-15`
- her satirda:
  - bir `combobox`
  - bir esik kutusu

### Augment

- `Augment Mod 1-8`
- her satirda:
  - bir `combobox`
  - bir esik kutusu

### Zamanlama

Tum beklemeler `Min/Max` araliginda rastgele secilir:

- `Baslamadan Once`
- `Aksiyon Arasi`
- `Craft Sonrasi`
- `Inspect Oncesi`
- `Kisayol Sonrasi`

### Noktalar

- `F6` currency noktalarini sirayla kaydeder:
  - ilk `F6`: `Alteration`
  - ikinci `F6`: `Augment`
  - sonra tekrar basa doner
- `F7` her basista yeni item noktasi ekler
- `Currencyleri Sifirla`
- `Sonuncuyu Sil`
- `Listeyi Temizle`

Not:

- `Item Noktalari` oturumluktur
- program kapaninca item noktasi listesi sifirlanir
- diger ayarlar kaydedilir

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
- item noktalarindan oturum disi saklanan ayarlar

## Kisa Yol Tuslari

- `F6`: currency noktalarini sirayla kaydet
- `F7`: yeni item noktasi ekle
- `F8`: baslat
- `F9`: durdur
- `Esc`: aktif islemi durdur

Program calisirken sen:

- fareyi hareket ettirirsen
- fare ile tiklarsan
- klavyede tusa basarsan

otomatik durur.

## Loglama

- `F8` ile her calistirmada secili klasorde tarih-saat adli yeni bir `.txt` dosyasi olusur.
- Ekrandaki loglar ayni anda dosyaya da yazilir.
- `Kopyala Goster` aciksa clipboard’a gelen metinler de loga yazilir.
- Her item icin alteration, augment, toplam sol tik ve sure ozetlenir.
- Tum islem sonunda genel ozet yazilir.

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

- `Ctrl+C` ile item bilgisinin gercekten panoya geldigini once manuel denemek iyi olur.
- Item penceresi veya oyun arayuzu yer degistirirse koordinatlari yeniden kaydetmek gerekir.
- `Aranan Mod` ve `Augment Mod` satirlarinda bos olan kutular tamamen yok sayilir.
- Item tamamlandiginda UI listesinden de silinir.

## Debug'da Calistirma

```powershell
cd C:\Users\gamer\Desktop\crafter\SystemAnalysis
$env:DOTNET_CLI_HOME='C:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet build
.\bin\Debug\net10.0-windows\SystemAnalysis.exe
```

Dogrudan exe acmak istersen:

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

Release exe’yi acmak icin:

```powershell
& "C:\Users\gamer\Desktop\crafter\SystemAnalysis\bin\Release\net10.0-windows\win-x64\publish\SystemAnalysis.exe"
```

## Repo Duzeni

- kaynak kodlar: `SystemAnalysis/`
- guncel publish kopyasi: `publish/win-x64/`
- debug/release build klasorleri: `bin/`, `obj/`

## Hatirlatma

Projeye geri dondugunde en hizli baslangic genelde su olur:

1. Debug test icin `dotnet build` + debug exe
2. Son kullanilan ayarlari kontrol et
3. Item noktalarini yeniden sec
4. Gerekirse release publish al
