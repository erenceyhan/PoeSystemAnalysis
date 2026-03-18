# SystemAnalysis

Bu surum, kucuk bir arayuz uzerinden aradigin metni yazip craft akisina baslamana izin verir. Belirledigin noktalara normal hizda tiklar ve her tiklamadan sonra `Ctrl+C` ile item bilgisini panoya alip istedigin modun gelip gelmedigini kontrol eder.

## Ozellikler

- `single_craft`: currency noktasina sag tik, item noktasina sol tik, sonra kontrol
- `hold_shift_spam`: `Shift` basili tutarak currency noktasina bir kez sag tik, sonra item noktasina tekrar tekrar sol tik
- Her sol tiktan sonra kesin olarak kontrol yapar
- Kullanici fareyi oynatirsa, tiklarsa veya klavyede tusa basarsa otomatik durur
- Arayuzden birden fazla aranan mod ve zamanlama degerlerini girebilirsin
- `F6`, `F7`, `F10` ile mevcut fare konumunu kaydedebilirsin
- `F8` ile baslat, `F9` ile durdur, `Esc` ile cik
- `F8` ile baslayinca secilen log klasorunde tarih-saat adli bir `.txt` log dosyasi olusur

## Arayuz Mantigi

- `Currency Noktasi`: basilacak currency
- `Item Noktasi`: craft yapilan item
- `Kontrol Noktasi`: cogu durumda yine ayni item noktasi

Path of Exile benzeri kullanimda item yer degistirmez, sadece icerigi degisir. Bu nedenle cogu durumda `Item Noktasi` ile `Kontrol Noktasi` ayni koordinat olur.

## Programin Calisma Mantigi

- Aktif ve dolu olan `Aranan Mod` satirlarindan herhangi biri, kopyalanan item metninin herhangi bir yerinde gecerse eslesme sayilir ve islem durur.
- Kontrol her zaman her tek tiklamadan sonra yapilir.
- Kontrol sirasinda program mouse'u `Kontrol Noktasi` uzerine getirir ve varsayilan olarak `Ctrl+C` gonderir.
- Kopyalanan metin panodan okunur ve `Aranan Mod` ile karsilastirilir.
- Eslesme bulunursa islem hemen biter.
- Eslesme bulunmazsa dongu devam eder.

## Modlarin Farki

- `Tekli Uretim`: her turda `Currency Noktasi`na sag tik yapar, sonra `Item Noktasi`na sol tik yapar, sonra kontrol eder.
- `Shift Basili Tekrarli Tiklama`: once `Currency Noktasi`na bir kez sag tik yapar, sonra `Shift` tusuna basili tutar ve `Item Noktasi`na tekrar tekrar sol tik yapar. Her sol tiktan sonra `Shift` birakilmadan yine kontrol eder. Islem bitince `Shift` birakilir.

## Zamanlama Mantigi

- Her zamanlama alani artik `Min` ve `Max` olarak calisir.
- Program her adimda bu iki deger arasinda rastgele bir milisaniye secer.
- `Min` ve `Max` ayniysa sabit bekleme kullanilir.
- Secilen gercek bekleme suresi loga yazilir.
- `Baslamadan Once`: `F8` bastiktan sonra ilk aksiyondan once bekler.
- `Aksiyon Arasi`: sag tik ile sol tik gibi ard arda gelen aksiyonlar arasinda bekler.
- `Craft Sonrasi`: iteme tikladiktan sonra kontrol oncesi bekler.
- `Inspect Oncesi`: mouse kontrol noktasina geldikten sonra `Ctrl+C` oncesi bekler.
- `Kisayol Sonrasi`: `Ctrl+C` gonderildikten sonra panoyu okumadan once bekler.

## Guvenlik ve Durdurma

- `F8`: baslat
- `F9`: durdur
- `Esc`: pencereyi kapat
- `F6`: mevcut mouse konumunu `Currency Noktasi` olarak kaydet
- `F7`: mevcut mouse konumunu `Item Noktasi` olarak kaydet
- `F10`: mevcut mouse konumunu `Kontrol Noktasi` olarak kaydet

Program calisirken sen fareyi hareket ettirirsen, tiklarsan veya klavyede bir tusa basarsan otomatik durur. Bu sayede islem devam ederken kontrolu geri alabilirsin.

## Loglama

- Arayuzde `Log Klasoru` alani vardir.
- `Klasor Sec` ile loglarin yazilacagi klasoru belirleyebilirsin.
- `Kopyala Goster` kutusu aciksa manuel veya otomatik `Ctrl+C` ile gelen metinler de loga yazilir.
- `F8` ile her yeni baslatmada tarih-saat adli yeni bir `.txt` dosyasi olusur.
- Ekrandaki loglar ayni anda bu dosyaya da yazilir.
- Boylece sonradan hangi saatte ne oldugunu inceleyebilirsin.

## Dogru Kullanim Icin Notlar

- Cogu senaryoda `Item Noktasi` ile `Kontrol Noktasi` ayni olur.
- `Aranan Mod` icin tam satir yazmak en net sonuc verir, ama parcali metin de calisir.
- Bos bir `Aranan Mod` satiri tamamen yok sayilir.
- Sadece isaretli olan `Aranan Mod` satirlari kontrol edilir.
- Buyuk-kucuk harf duyarliligi su an varsayilan olarak kapali, yani metin karsilastirmasi buyuk-kucuk harf farkina bakmaz.
- `Ctrl+C` ile item bilgisinin gercekten panoya geldiginden once manuel test yapman iyi olur.
- Program sadece kaydettigin koordinatlara tiklar; pencere yer degisir veya oyun arayuzu kayarsa koordinatlari yeniden kaydetmen gerekir.

## Calistirma

```powershell
$env:DOTNET_CLI_HOME='c:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet run --project .\SystemAnalysis\SystemAnalysis.csproj
```

## Uygulamayi Debug'da Calistirmak Icin Yazilacaklar

```powershell
cd C:\Users\gamer\Desktop\crafter\SystemAnalysis
$env:DOTNET_CLI_HOME='C:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet build
.\bin\Debug\net10.0-windows\SystemAnalysis.exe
```

## Exe Uretme

```powershell
$env:DOTNET_CLI_HOME='c:\Users\gamer\Desktop\crafter\.dotnet-home'
dotnet publish .\SystemAnalysis\SystemAnalysis.csproj -c Release -r win-x64 --self-contained true
```

Olusan exe:

`bin\Release\net10.0-windows\win-x64\publish\SystemAnalysis.exe`

## Repo Duzeni

- Kaynak kodlar `SystemAnalysis/` klasorundedir.
- Stabil publish ciktisi repo kokundeki `publish/win-x64/` klasorune kopyalanabilir.
- `bin/` ve `obj/` gibi build klasorleri git'e eklenmez.
