# Raporlar

Bu sayfa IT personeli ve yöneticiler içindir: arıza, talep, operasyon ve envanter sayılarını görmek, kayıt listelerini açmak ve Excel'e aktarmak.

## Rapor açma

1. Menüden **Raporlar** sayfasını açın.
2. Üstteki sekmelerden bir rapor seçin:
   - **Olaylar**: Arızaların sayısı, ilk yanıt ve çözüm süreleri, SLA uyumu.
   - **Talepler**: Taleplerin karşılanma süresi, onay bekleme süresi, ret oranı.
   - **Operasyon**: Ekip ve kişi yükü, kategori, lokasyon ve departmana göre dağılım, bekletme nedenleri.
   - **Transferler**: Aktarma sayısı ve oranı, yeniden atamalar, arızadan açılan talepler.
   - **Varlıklar**: Türe ve duruma göre cihazlar, aktif zimmetler, stok.
   - **Garanti**: Garantisi bitmiş, bitecek ve garanti tarihi girilmemiş cihazlar.

![Olaylar raporu](images/reports-incidents-desktop.png)

Filtrelerin altında **Kapsam: tüm kayıtlar** veya **Kapsam: yalnızca ekiplerinizin kayıtları** yazar. IT personeli yalnızca ekiplerinin kayıtlarını, yöneticiler tüm kayıtları görür.

## Dönem ve filtreler

- Hızlı dönem için **Son 7 gün**, **Son 30 gün**, **Bu ay**, **Geçen ay** veya **Bu yıl** düğmelerinden birine basın.
- Belirli bir aralık için **Başlangıç tarihi** ve **Bitiş tarihi** girin.
- **Öncelik**, **Kategori**, **Ekip**, **Atanan kişi**, **Departman**, **Lokasyon**, **Durum**, **SLA durumu** gibi filtrelerle daraltın. Hangi filtrelerin çıktığı rapora göre değişir.
- Tüm filtreleri kaldırmak için **Temizle** düğmesine basın.

Telefonda filtreler gizlidir: **Filtreler (0)** düğmesine basın, seçimleri yapın ve **Uygula** ile kapatın. Parantez içindeki sayı seçili filtre sayısıdır.

![Olaylar raporu (telefon)](images/reports-incidents-mobile.png)

Süreler iki türlü gösterilir: **Takvim süresi** gerçek geçen zamandır; **SLA süresi (mesai)** yalnızca SLA takvimindeki çalışma saatlerini sayar.

## Rakamın arkasındaki kayıtları görme

1. Bir kartın başlığına (örneğin "Açılan kayıt") basın. O rakamı oluşturan kayıtların listesi açılır.
2. Oran gösteren kartlarda alttaki "Tümünü listele" bağlantısı paydadaki tüm kayıtları açar.
3. Listede kayıt numarasına basarak kaydı açabilirsiniz.
4. Rapora **Rapora dön** düğmesiyle dönün; dönem ve filtreler korunur.

## Excel'e aktarma

1. Kayıt listesinde **Excel'e aktar** düğmesine basın. Dosya hemen iner.
2. Liste çok büyükse "Bu liste anında indirilemeyecek kadar büyük" uyarısı çıkar. **Arka planda hazırla** düğmesine basın.
3. Hazırlanan dosyalar **Dışa aktarımlarım** sayfasında görünür. Menüden veya rapor sayfasının sağ üstündeki **Dışa aktarımlarım** düğmesinden açın.

![Dışa aktarımlarım](images/reports-exports-desktop.png)

Dışa aktarım durumları: **Sırada**, **Hazırlanıyor**, **Hazır**, **Başarısız**, **Süresi doldu**. **Hazır** olan dosyayı **İndir** ile indirin. Dosyalar "Son indirme" tarihine kadar saklanır; sonra silinir ve yeniden hazırlamanız gerekir.

## İpuçları

- Rapor adresi seçtiğiniz dönem ve filtreleri içerir. Adresi bir iş arkadaşınıza gönderirseniz aynı görünümü açar (kendi yetkisi kadarını görür).
- Çalışanlar raporları göremez; adresi açarlarsa "Bu sayfayı görme yetkiniz yok" uyarısı çıkar.
