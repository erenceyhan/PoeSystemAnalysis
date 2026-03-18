# SystemAnalysis

 Bu surum, kucuk bir arayuz uzerinden aradigin metni yazip birden fazla item noktasi uzerinde akisi sirayla calistirmana izin verir. Belirledigin noktalara normal hizda tiklar ve her tiklamadan sonra ayni item noktasinda `Ctrl+C` ile bilgiyi panoya alip istedigin modun gelip gelmedigini kontrol eder.

## Ozellikler

- `hold_shift_spam`: secili her item icin currency noktasina bir kez sag tik, `Shift` basili tutarak ayni item noktasina tekrar tekrar sol tik
- Her sol tiktan sonra kesin olarak kontrol yapar
- Kullanici fareyi oynatirsa, tiklarsa veya klavyede tusa basarsa otomatik durur
- Arayuzden birden fazla aranan mod ve zamanlama degerlerini girebilirsin
- `F6` ile currency noktasi, `F7` ile ise istedigin kadar item noktasi ekleyebilirsin
- `F8` ile baslat, `F9` ile durdur, `Esc` ile cik
- `F8` ile baslayinca secilen log klasorunde tarih-saat adli bir `.txt` log dosyasi olusur
- Her item icin kac sol tik atildigi ve ne kadar surdugu loglanir
- Tum secili itemler bitince kisa bir uyari sesi calar

## Arayuz Mantigi

- `Currency Noktasi`: basilacak currency
- `Item Noktalari`: sirasiyla islenecek tum itemler

Kontrol icin ayri bir nokta secilmez. Program her itemde kontrolu dogrudan o item noktasinda yapar.

## Programin Calisma Mantigi

- Aktif ve dolu olan `Aranan Mod` satirlarindan herhangi biri, kopyalanan item metninin herhangi bir yerinde gecerse eslesme sayilir ve islem durur.
- Kontrol her zaman her tek tiklamadan sonra yapilir.
- Kontrol sirasinda program mouse'u o an islenen item noktasina getirir ve varsayilan olarak `Ctrl+C` gonderir.
- Kopyalanan metin panodan okunur ve `Aranan Mod` ile karsilastirilir.
- Bir itemde eslesme bulunursa o item tamamlanir ve siradaki iteme gecilir.
- Eslesme bulunmazsa ayni item icin dongu devam eder.

## Modlarin Farki

- `Shift Basili Tekrarli Tiklama`: her item icin once `Currency Noktasi`na bir kez sag tik yapar, sonra `Shift` tusuna basili tutar ve o item noktasina tekrar tekrar sol tik yapar. Her sol tiktan sonra `Shift` birakilmadan ayni itemden kontrol eder. Mod bulununca `Shift` birakilir ve siradaki iteme gecer.

## Zamanlama Mantigi

- Her zamanlama alani artik `Min` ve `Max` olarak calisir.
- Program her adimda bu iki deger arasinda rastgele bir milisaniye secer.
- `Min` ve `Max` ayniysa sabit bekleme kullanilir.
- Secilen gercek bekleme suresi loga yazilir.
- `Baslamadan Once`: `F8` bastiktan sonra ilk aksiyondan once bekler.
- `Aksiyon Arasi`: sag tik ile sol tik gibi ard arda gelen aksiyonlar arasinda bekler.
- `Craft Sonrasi`: iteme tikladiktan sonra kontrol oncesi bekler.
- `Inspect Oncesi`: mouse item noktasina geldikten sonra `Ctrl+C` oncesi bekler.
- `Kisayol Sonrasi`: `Ctrl+C` gonderildikten sonra panoyu okumadan once bekler.

## Guvenlik ve Durdurma

- `F8`: baslat
- `F9`: durdur
- `Esc`: pencereyi kapat
- `F6`: mevcut mouse konumunu `Currency Noktasi` olarak kaydet
- `F7`: mevcut mouse konumunu item listesine yeni `Item Noktasi` olarak ekle

Program calisirken sen fareyi hareket ettirirsen, tiklarsan veya klavyede bir tusa basarsan otomatik durur. Bu sayede islem devam ederken kontrolu geri alabilirsin.

## Loglama

- Arayuzde `Log Klasoru` alani vardir.
- `Klasor Sec` ile loglarin yazilacagi klasoru belirleyebilirsin.
- `Kopyala Goster` kutusu aciksa manuel veya otomatik `Ctrl+C` ile gelen metinler de loga yazilir.
- `F8` ile her yeni baslatmada tarih-saat adli yeni bir `.txt` dosyasi olusur.
- Ekrandaki loglar ayni anda bu dosyaya da yazilir.
- Her item bittiginde o item icin `sol tik` ve `sure` ozeti yazilir.
- Islem dursa bile toplam item, toplam sol tik ve toplam sure ozeti yazilir.
- Boylece sonradan hangi saatte ne oldugunu inceleyebilirsin.

## Dogru Kullanim Icin Notlar

- `Aranan Mod` icin tam satir yazmak en net sonuc verir, ama parcali metin de calisir.
- Bos bir `Aranan Mod` satiri tamamen yok sayilir.
- Sadece isaretli olan `Aranan Mod` satirlari kontrol edilir.
- Buyuk-kucuk harf duyarliligi su an varsayilan olarak kapali, yani metin karsilastirmasi buyuk-kucuk harf farkina bakmaz.
- `Ctrl+C` ile item bilgisinin gercekten panoya geldiginden once manuel test yapman iyi olur.
- `F7` ile itemleri ekleme sirasi isleme sirasi olur.
- Program sadece kaydettigin koordinatlara tiklar; pencere yer degisir veya arayuz kayarsa koordinatlari yeniden kaydetmen gerekir.

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
