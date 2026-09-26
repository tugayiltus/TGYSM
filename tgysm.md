# ITSM Platformu
## Proje Açıklaması, Fonksiyonel Tasarım ve Geliştirme Yol Haritası

- Sürüm: 3.0 — Birleştirilmiş analiz
- Tarih: 2026-09-26
- Belge türü: Ürün analizi ve uygulama tasarımı; geliştirilmiş özellik beyanı değildir.
- Anlatım kaynağı: İlk proje analiz dosyası.
- Karar kaynağı: İkinci, revize proje analiz dosyası ve kullanıcı tarafından onaylanan kapsam.

## Yönetici özeti

Bu projenin amacı; şirket çalışanlarının IT arızalarını ve hizmet taleplerini kolaylıkla iletebildiği, IT ekiplerinin işleri hızlı şekilde yönetebildiği, gerektiğinde başka personele veya ekibe transfer edebildiği ve yönetimin operasyonu SLA, SLI ve KPI'larla ölçebildiği sade bir ITSM platformu geliştirmektir.

Platformun temel yaklaşımı:

> **Kullanıcı için basit, IT personeli için hızlı, yönetim için ölçülebilir, sistem açısından tamamen izlenebilir.**

Bu destek yapısına IT envanteri ve Asset Management dahildir. Şirketin masaüstü bilgisayarları, dizüstü bilgisayarları, telefonları ve tabletleri aynı platformda izlenir. Bir cihazın nerede olduğu, kime teslim edildiği, hangi arızaları yaşadığı ve ne zaman iade edildiği birbirinden kopuk listeler yerine ilişkili kayıtlardan görülebilir.

Uygulama tek şirketin birden fazla departman ve lokasyonuna hizmet eder. Çalışan portalı, IT çalışma alanı ve ayrı `/admin` yönetim alanı aynı web uygulaması ve backend üzerinde çalışır. Her kullanıcı yalnızca rolünün ve kayıt kapsamının izin verdiği işlemlere erişir.

İlk sürümün omurgası: Incident, Request, envanter/varlık, ekip/personel ataması, transfer, tek aşamalı talep onayı, durum takibi, SLA, e-posta, timeline/audit, bilgi bankası ve gelişmiş raporlamadır. Arayüz sadeleşir; onaylanan ilk sürüm kapsamı daraltılmaz.

## Proje vizyonu ve tasarım ilkeleri

> **İnsanların gerçekten kullanmak isteyeceği kadar sade fakat IT yöneticisinin operasyonu profesyonel şekilde yönetebileceği kadar güçlü bir sistem.**

Başarı ölçüsü: doğru süreç, doğru veri ve minimum kullanıcı karmaşıklığı.

### Kullanıcı işini anlatsın

Çalışanın Incident, Request, SLA veya CMDB terimlerini bilmesi gerekmez. Ana ekrandaki iki temel seçim günlük dildedir:

```text
Bir şey çalışmıyor → Arıza bildirimi → Incident
Bir şey istiyorum → Hizmet talebi   → Request
```

“Bilgisayarım açılmıyor” bir arızadır. “Yeni bilgisayar istiyorum” bir hizmet talebidir. Her ikisi aynı platformda izlenir; iş akışları ve ölçümleri ayrı kalır.

### IT personeli tek merkezden çalışsın

IT çalışma alanı; ekip kuyruğunu, kişinin üzerine aldığı işleri, bekleyen kayıtları ve SLA risklerini bir araya getirir. Personel işi üzerine alır, kullanıcıya yanıt verir, iç not ekler, gerektiğinde gerekçeyle transfer eder ve çözüm bilgisiyle tamamlar.

### Yönetim veriye ulaşabilsin

Dashboard yalnızca sayı göstermez. Yönetici ilgili göstergeyi açarak kayıt listesine ulaşır; ekip, departman, lokasyon ve kategori bazında nedenleri inceler. Raporlama için yeterli veri tutulur; çalışan ekranına bu karmaşıklık taşınmaz.

### Yetki ve geçmiş sistemde korunsun

İlk dosyadaki ekran/aksiyon bazlı yetki yaklaşımı korunur; ikinci dosyadaki üç sabit rol paketiyle uygulanır. Kullanıcıya özel doğrudan yetki tasarımı ilk sürümde bulunmaz. Kritik hareketler audit edilir; kullanıcıya gösterilen timeline yetkili olaylarla sınırlıdır.

### Gelecekte genişleyebilsin

Clean Architecture sınırları ve modüler yapı, sonraki entegrasyonları kolaylaştırır. İlk sürüm; tam CMDB, genel workflow motoru veya mikroservis zorunluluğuyla genişletilmez. Envanter ve varlık yönetimi ise geleceğe bırakılmaz; ilk sürümün parçasıdır.

## Ürün nasıl kullanılacak?

### Senaryo A — Arıza bildirimi

Çalışan “Bir şey çalışmıyor” seçer. Konu, açıklama, kategori ve lokasyon girer; isterse zimmetli bilgisayarını ve ekran görüntüsünü ekler. Kayıt numarası oluşur ve karşılama ekibine düşer.

IT personeli kaydı üzerine alır. Çalışana açık yanıt verir; teknik incelemelerini iç nota yazar. Uzman ekip gerekiyorsa gerekçeli transfer yapar. Transfer SLA başlangıcını değiştirmez. Sorun giderildiğinde çözüm açıklaması girilir; kayıt doğrudan kapanır ve çalışana e-posta gönderilir.

### Senaryo B — Yeni cihaz talebi

Çalışan “Bir şey istiyorum” üzerinden cihaz talebi açar. Talep, departmanın tanımlı yöneticisinin tek aşamalı onayına gider. Onaydan sonra IT kuyruğunda işleme alınır. Stoktaki cihaz teslim edilir; zimmet ve Request bağlantısı kaydedilir. IT tamamlayınca kayıt kapanır. Onay reddedilirse gerekçesiyle iptal edilir.

### Senaryo C — Arızanın aslında erişim talebi olması

Çalışan “SAP'a giremiyorum” diye Incident açar. IT, sistemin çalıştığını fakat çalışanın erişim yetkisi olmadığını belirler. Incident'ın türü değiştirilmez; ilişkili Request açılır. Gerekli onay yeni Request üzerinde yürür. Incident “Hizmet talebi gerekli” sonucuyla tamamlanabilir; ilk kaydın geçmişi ve ölçümleri korunur.

### Senaryo D — Cihaz iadesi ve yeniden teslim

Çalışan cihazını iade eder. IT aktif zimmeti kapatır, cihazın durumunu ve lokasyonunu kaydeder. Uygun durumdaki cihaz başka çalışana teslim edildiğinde yeni zimmet oluşur. Eski kullanıcının teslim/iade geçmişi kaybolmaz; cihazın aynı anda iki aktif zimmeti olamaz.

### Senaryo E — Operasyon incelemesi

Yönetici SLA ihlal göstergesini seçer; ihlal eden kayıtlara iner. Lokasyon, kategori ve ekip filtreleriyle yoğunlaşan sorunları inceler. Aynı filtreyle Excel çıktısı alır. Varlık kartından tekrar eden cihaz arızalarını, bilgi bankasından ilgili çözüm makalelerini görebilir; tüm erişimler yetki kapsamındadır.

## Belgeyi okuma ve karar önceliği

İlk dosyanın proje amacı, süreç açıklamaları ve örnekleri aşağıdaki tasarıma işlendi. Çelişkiler ikinci dosyanın kararlarıyla çözüldü. İkinci dosyada “öneri”, “taslak” veya “varsayım” olarak belirtilen ayrıntılar bu belgede de kesin kullanıcı kararı sayılmaz.

- Bölüm 1–4: Kapsam, kurum, kullanıcı ve yetki kararları.
- Bölüm 5–13: Süreçler, modüller ve ekranlar.
- Bölüm 14–18: Dil, mimari, veri, güvenlik ve dokümantasyon.
- Bölüm 19–21: Yol haritası, kabul ölçütleri ve açık kararlar.
- Ek A: İki dosyanın karşılaştırması ve birleştirme sonucu.

## 1. Kesinleşen kararlar

| Başlık | Karar |
|---|---|
| Kurum | Tek şirket; birden fazla departman ve lokasyon |
| Ürün | Web uygulaması; mobile-first |
| Çekirdek modüller | Incident Management, Request Management, IT envanteri, Asset Management |
| Yönetim | Aynı web uygulamasında ayrı `/admin` alanı; backend yetki kontrolü |
| Kullanıcı oluşturma | Yalnızca yönetim panelinden; ilk yönetici kurulum sırasında |
| Giriş | Yerel hesap/şifre; ilk girişte zorunlu şifre değişimi |
| Roller | Çalışan, IT personeli, yönetici |
| Varlık türleri | Masaüstü bilgisayar, dizüstü bilgisayar, telefon, tablet |
| Envanter girişi | Elle ve CSV |
| Talep onayı | Gereken talep türlerinde tek aşamalı yönetici onayı |
| Kapanış | IT tamamladığında doğrudan kapanış; kullanıcı onayı beklenmez |
| İlk sürüm | SLA, e-posta, bilgi bankası ve gelişmiş raporlama dahil |
| Backend | .NET 10, EF Core, Clean Architecture |
| Veritabanı | PostgreSQL |
| Frontend | React, Vite, TypeScript |
| Dil | Türkçe UI; i18n; Türkçe ünlü uyumu ek yardımcıları |
| Mimari kayıtlar | `docs/adr/`; Michael Nygard formatı; tarih artan numaralandırma |
| Ürün referansı | Repo kökünde `capabilities.md` |
| Kullanıcı rehberi | `docs/user-guide/`; `combined.md` senkronizasyonu; PDF için belge içi bağlantılar |

“Sade” yaklaşımı özellikleri ilk sürümden çıkarmak anlamına gelmez. Ekranlar, veri girişi ve süreçler sade tutulur. Aşağıdaki geliştirme fazları tek ilk sürümün teslim parçalarıdır.

## 2. Ürün amacı ve sınırlar

Çalışan arıza veya hizmet talebi oluşturur; kendi kayıtlarını ve zimmetli cihazlarını izler. IT personeli ekip kuyruğunu, atamaları, çözüm süreçlerini ve varlık hareketlerini yönetir. Yönetici kullanıcıları, tanımları, onayları, SLA kurallarını ve raporları yönetir.

Envanter, cihazın ne olduğunu ve nerede bulunduğunu gösterir. Asset Management aynı cihazın zimmet, teslim, iade, bakım ve kullanım dışına çıkarma geçmişini yönetir. İki ayrı cihaz kaydı oluşturulmaz; ortak varlık modeli kullanılır.

İlk sürüm dışında:

- Çok şirketli/çok kiracılı yapı.
- Açık üyelik, sosyal giriş, LDAP/SSO, Entra ID ve otomatik kullanıcı senkronizasyonu.
- Kullanıcıya doğrudan ek yetki tanımlama; sınırsız özel rol tasarımı.
- Otomatik cihaz keşfi, Intune, ağ taraması, cihaz ajanı.
- Tam CMDB ve servis bağımlılık haritası.
- Satın alma, sözleşme, yazılım lisansı ve amortisman muhasebesi.
- Full Problem Management, Change Management, CAB ve Release Management.
- Çok aşamalı onay, genel amaçlı BPM/otomasyon motoru.
- Gelen e-postadan kayıt açma, SMS, WhatsApp, chatbot.
- Native mobil uygulama, çevrimdışı işlem ve uzaktan masaüstü.

## 3. Kurum ve kullanıcı modeli

- Şirket tek kayıtla temsil edilir; tenant ayrımı tasarlanmaz.
- Departman ve lokasyon birbirinden bağımsız tanımlardır.
- Lokasyon hiyerarşisi: yerleşke → bina/alan; gerekirse alt lokasyon.
- Kullanıcı: ad, soyad, benzersiz giriş e-postası, departman, lokasyon, rol, aktiflik.
- IT ekipleri ve ekip üyelikleri ayrıca tutulur.
- Departman için aktif bir onaylayıcı yönetici tanımlanır.
- Kullanıcı/ekip/departman/lokasyon silmek yerine pasifleştirilir; geçmiş ilişkiler korunur.
- Pasifleştirme öncesi açık işler ve zimmetler gösterilir; sorumluluk devri yapılır. Geçmiş zimmetler otomatik silinmez.

### 3.1 Hesap yaşam döngüsü

1. İlk yönetici, kurulum sırasında tek kullanımlık güvenli kurulum işlemiyle oluşturulur.
2. Sabit varsayılan şifre veya sürekli açık kurulum endpoint'i bulunmaz.
3. Sonraki kullanıcıları yalnızca yetkili yönetici `/admin` üzerinden oluşturur.
4. İlk giriş için süreli, tek kullanımlık şifre belirleme bağlantısı veya geçici şifre kullanılır; normal kullanımdan önce kalıcı şifre zorunludur.
5. Şifre sıfırlama mevcut hesaplar içindir; yeni hesap oluşturmaz. Yanıtlar hesap varlığını açığa çıkarmaz.
6. Rol değişimi ve pasifleştirme aktif oturumları geçersizleştirir; son aktif yönetici devre dışı bırakılamaz.

**Kullanıcı CSV içe aktarımı yoktur. CSV özelliği yalnızca varlık envanteri içindir.**

## 4. Paneller ve yetkilendirme

Tek React uygulaması ve ortak API kullanılır. `/admin`, ayrı gezinme ve ekran düzenine sahip yönetim alanıdır; ayrı frontend dağıtımı değildir.

| İşlem | Çalışan | IT personeli | Yönetici |
|---|---|---|---|
| Incident/Request açma | Kendi adına | Kendi adına | Kendi adına |
| Kayıt okuma | Kendi kayıtları | Kendi kayıtları + yetkili ekip kayıtları | Tüm kayıtlar |
| Kullanıcıya açık yorum | Kendi kayıtları | Erişebildiği kayıtlar | Tüm kayıtlar |
| İç not/atama/transfer/kapatma | Yok | Yetkili ekip kayıtları | Tüm kayıtlar |
| Zimmet görüntüleme | Kendi zimmetleri | Envanter erişimi | Tüm varlıklar |
| Varlık oluşturma/CSV/hareket | Yok | Var | Var |
| Talep onayı | Yok | Yok | Yalnızca kendisine atanmış onaylar |
| Bilgi bankası okuma | Çalışana açık içerik | Açık + iç içerik | Tüm içerik |
| Bilgi bankası düzenleme | Yok | Var | Var |
| Raporlar ve dışa aktarım | Yok | Yetkili ekip kayıtları; envanter raporları | Tüm raporlar |
| Kullanıcı/kurum/SLA/ayar yönetimi | Yok | Yok | Var |
| Audit ve teknik bildirim logları | Yok | Yok | Var |

- Sabit rol paketleri backend politikalarına eşlenir. Ekranı gizlemek yetkilendirme değildir.
- Her API, kayıt/ekip/görünürlük kapsamını denetler; arama, ek indirme ve dışa aktarım da aynı kurallara tabidir.
- Yönetici rolü, kendisine atanmamış onayı otomatik onaylama hakkı vermez.
- Doğrudan kullanıcı yetkileri ilk sürümde yoktur.

## 5. Incident Management

Incident: çalışan IT hizmetinin kesintiye uğraması veya kalitesinin düşmesi. Amaç hizmeti yeniden kullanılabilir hale getirmek, kullanıcıya ne yapıldığını göstermek ve tekrar eden sorunların incelenebilmesi için doğru veri tutmaktır.

Örnekler: internet çalışmıyor, VPN bağlantısı kurulamıyor, Outlook e-posta göndermiyor, bilgisayar açılmıyor. Kayıt açmak için çalışanın teknik kök nedeni bilmesi gerekmez.

### 5.1 Form ve alanlar

- Konu, açıklama, kategori, lokasyon zorunlu.
- Etkilenen varlık ve ek dosyalar isteğe bağlı.
- Çalışan kendi zimmetini seçebilir; ortak cihaz sorununu varlık seçmeden de bildirebilir.
- IT personeli etki, aciliyet, öncelik, ekip ve sorumluyu düzenler.
- Numara: `INC-2026-000001`; yıl bazlı, eşzamanlı oluşturmalarda benzersiz.
- Tespit, kök neden ve çözüm alanları ayrı tutulur. Çözüm kapanışta zorunlu; kök neden varsayılan olarak isteğe bağlı.
- Kök neden/iç teknik değerlendirme otomatik olarak kullanıcıya gönderilmez.

### 5.2 Öncelik

Etki ve aciliyet üçer seviyedir. Başlangıç matrisi aşağıdaki gibidir; üretim öncesi operasyonla doğrulanır.

| Etki / Aciliyet | Yüksek | Orta | Düşük |
|---|---|---|---|
| Yüksek | P1 | P2 | P3 |
| Orta | P2 | P3 | P4 |
| Düşük | P3 | P4 | P4 |

Öncelik değişikliği gerekçesi ve eski/yeni değer kaydedilir. Çalışan kendisini doğrudan P1 atayamaz.

## 6. Request Management ve onay

Request: yeni cihaz, erişim, kurulum veya standart IT hizmeti talebi.

- Numara: `REQ-2026-000001`.
- Konu, açıklama, talep türü, kategori, lokasyon; isteğe bağlı varlık ve ekler.
- Talep türü bazında onay gereksinimi tanımlanır. Başlangıç önerisi: cihaz ve erişim talepleri onaylı; bilgi/destek talepleri onaysız.
- Genel form ve yönetilebilir talep türleri kullanılır; gelişmiş dinamik form tasarımcısı kapsam dışıdır.

### 6.1 Tek aşamalı onay akışı

1. Onay gerektiren talep açılır; departmanın yöneticisine onay atanır.
2. Kayıt `Beklemede`; neden `Yönetici onayı bekleniyor` olur.
3. Onay kaydı ayrı durum taşır: `Bekliyor`, `Onaylandı`, `Reddedildi`, `Geçersiz`.
4. Onaylanınca talep `Açık` durumuna ve IT kuyruğuna geçer; hizmetin yerine getirilmesi başlar.
5. Reddedilirse gerekçe zorunlu; talep `İptal`, sonuç `Onay reddedildi` olur.
6. Onaylayıcı yoksa veya talep sahibi onaylayıcıyla aynı kişiyse talep beklemede kalır; başka yönetici atanması gerekir. Kendini onaylama yoktur.
7. Onaydan sonra hizmet kapsamı değişirse önceki onay geçersizleştirilir ve tekrar aynı tek aşamalı süreç başlatılır.
8. Onaylanmadan hizmet tamamlama/cihaz teslim etme backend tarafından engellenir.

Onaylayıcı değişimi gerekçeli ve audit kayıtlıdır. Aynı onaya eşzamanlı iki karar verilmesi engellenir. Tek aktif yönetici kendi onaylı talebini açmışsa ikinci yönetici tanımlanana kadar işlem bekler.

### 6.2 Incident üzerinden ilişkili Request

Incident türü doğrudan değiştirilmez. İlişkili Request oluşturulur; konu/açıklama, talep sahibi, lokasyon ve uygun varlık bağlantıları taşınır. İç notlar veya iç dosyalar kullanıcıya açık alana kopyalanmaz.

İlişki iki yönlüdür. Incident, `Hizmet talebi gerekli` sonucu ile tamamlanabilir; yeni Request kendi onayını ve SLA'sını başlatır. Bu Incident sonucu gerçek arıza çözümünden raporlarda ayrı gösterilir.

## 7. Ortak kayıt akışı

```text
Açık → İşlemde → Tamamlandı
          ↕
       Beklemede

Açık / İşlemde / Beklemede → İptal
```

- `Tamamlandı` kapanmış kayıttır; ek kullanıcı onayı veya ayrı kapanış adımı yoktur.
- Tamamlamada kullanıcıya açık çözüm/yerine getirme açıklaması zorunludur.
- İptal yalnızca yetkili IT personeli/yönetici tarafından gerekçeyle yapılır; onay reddi sistemsel iptal nedenidir.
- Bekleme nedeni: kullanıcı, yönetici onayı, tedarikçi, teknik ekip, üçüncü taraf, planlanan tarih veya diğer.
- Yeniden açma açıkça kararlaştırılmadığından ilk sürüm taslağında yoktur; sorun sürerse eski kayda bağlı yeni kayıt açılır.
- Tamamlanan kayıtta normal düzenleme kapatılır; yetkili açıklayıcı ek notlar geçmişi değiştirmeden eklenebilir.

### 7.1 Atama ve transfer

- Her kayıt bir ekibe bağlıdır; sorumlu personel isteğe bağlıdır.
- Varsayılan karşılama ekibi vardır; ekip olmadan kayıt kaybolmaz.
- Personel yalnızca yetkili olduğu ekip kuyruğundaki işi üzerine alabilir.
- Hedef sorumlu aktif ve hedef ekibin üyesi olmalıdır.
- Ekip/personel transferinde neden zorunlu; eski ve yeni atama tarihçede tutulur.
- Transfer SLA'yı sıfırlamaz. Eski ekip, kayıt kapsamından çıktıysa erişimini kaybeder.
- Eşzamanlı “üzerime al”, durum ve zimmet işlemlerinde çakışma kontrolü uygulanır.

Transfer formu: yeni ekip, yeni sorumlu, transfer nedeni ve açıklama. Neden seçenekleri: uzmanlık gerekiyor, yanlış atama, başka ekip sorumluluğunda, yetki gerekiyor, iş yoğunluğu, eskalasyon, lokasyon veya diğer. `Diğer` seçeneğinde açıklama zorunludur.

Örnek: kayıt 09:00'da açılır, 09:10'da üzerine alınır, 10:00'da transfer edilir ve 11:30'da tamamlanır. Kesintisiz çalışma takviminde ve duraklama olmadan toplam çözüm süresi 2 saat 30 dakikadır; yeni sorumluya geçiş süreyi yeniden başlatmaz.

### 7.2 Yorumlar, ekler ve timeline

- Kullanıcıya açık yanıt ve iç not ayrı görünürlük taşır.
- İç nota eklenen dosya aynı iç görünürlüğü devralır.
- Timeline, kullanıcının görebileceği olaylardan oluşur; audit logunun tamamı değildir.
- Başlangıç dosya sınırı: dosya başına 10 MB; yükleme isteğinde en fazla 5 dosya. Yönetilebilir teknik varsayımdır.
- İzin verilen başlangıç türleri: PDF, PNG, JPEG, TXT ve CSV. Uzantı/MIME/içerik doğrulanır; çalıştırılabilir içerik kabul edilmez.
- Dosyalar web kökü dışında saklanır; erişim yetkili API üzerinden yapılır. Tarama tamamlanana kadar indirmeye açılmaz.
- Zengin metin kullanılırsa güvenli şekilde temizlenir; kullanıcı içeriği çalıştırılmaz.

Kullanıcıya açık yanıt örneği: “Ağ ekibimiz kontrollerine devam ediyor.” İç not örneği: “SFP tarafında CRC hatası tespit edildi; uzman ekip inceleyecek.” İç notun alıcı ve ekleri kullanıcı yanıtından ayrıdır.

Timeline; açılış, atama, kullanıcıya yanıt, görünür durum değişimi ve çözüm gibi olaylarla kaydın hikâyesini gösterir. Audit ise yetkili yöneticinin incelemesi için daha ayrıntılı işlem kanıtı tutar.

## 8. IT envanteri ve Asset Management

### 8.1 Varlık kartı

| Alan | Kural |
|---|---|
| Sistem kimliği | Değişmez benzersiz kimlik |
| Demirbaş numarası | Zorunlu, benzersiz |
| Tür | Masaüstü bilgisayar / dizüstü bilgisayar / telefon / tablet |
| Marka, model | Zorunlu |
| Seri numarası | Biliniyorsa girilir; boş olmayan değerlerde benzersizlik |
| Durum | Stokta / Zimmetli / Bakımda / Kayıp / Kullanım dışı |
| Lokasyon | Zorunlu |
| Departman | İsteğe bağlı; ortak cihazlarda kullanılabilir |
| Zimmetli kullanıcı | Aktif zimmetten türetilir; doğrudan düzenlenmez |
| Garanti bitişi | İsteğe bağlı |
| Satın alma tarihi | İsteğe bağlı; satın alma süreci oluşturmaz |
| Açıklama | İsteğe bağlı; şifre veya erişim sırrı tutulmaz |

Seri/demirbaş normalizasyonu boşluk ve büyük/küçük harf kurallarını açık tanımlar. Bilinmeyen seri numarası için ortak `YOK` değeri yerine boş değer kullanılır.

### 8.2 Zimmet ve yaşam döngüsü

- Bir varlığın aynı anda en fazla bir aktif zimmeti olabilir; bir kullanıcıda birden çok varlık bulunabilir.
- Teslim: stoktaki cihaz → aktif kullanıcı; tarih, teslim eden, açıklama ve varsa Request bağlantısı kaydedilir.
- İade: aktif zimmet kapanır; cihaz durumu ve lokasyonu belirlenir.
- Kullanıcılar arası devir, eski zimmeti kapatıp yenisini açan tek atomik işlemdir.
- Bakım hareketinde kullanıcı sorumluluğunun sürüp sürmediği açıkça kaydedilir. Bakımdaki cihaz yeni kullanıcıya teslim edilemez.
- Kayıp olayında mevcut kullanıcı ilişkisi geçmişte korunur; cihaz yeniden teslim edilemez.
- Kullanım dışına çıkarma gerekçe gerektirir; varsa aktif zimmet kontrollü olarak kapatılır.
- Çalışan zimmetlerini görüntüler. İlk sürümde e-imza veya hukuki teslim belgesi üretimi yoktur.
- Zimmet işlemleri cihaz güncellemesi, hareket kaydı ve audit ile aynı transaction içinde tamamlanır.

### 8.3 Varlık–kayıt ilişkisi

Bir Incident/Request birden fazla varlıkla ilişkilendirilebilir. Varlık kartından geçmiş arızalar, talepler ve zimmet hareketleri görülür; her bağlantıda kayıt yetkisi tekrar kontrol edilir. Çalışan başkalarının arızalarını veya kişisel bilgilerini varlık geçmişinden göremez.

### 8.4 CSV içe aktarımı

1. Türkçe kolon açıklamaları içeren UTF-8 şablon indirilir.
2. Dosya yüklenir; kolon eşleme ve önizleme gösterilir.
3. Zorunlu alan, tür, durum, lokasyon, dosya içi/veritabanı mükerrerleri doğrulanır.
4. Satır bazlı hata raporu sunulur; kullanıcı düzeltir.
5. Kullanıcı önizlemeyi onayladıktan sonra aktarım yapılır.

Başlangıç üst sınırı: 5.000 satır ve 10 MB. İlk sürümde tüm dosya geçerliyse atomik aktarım; hatalı dosyada hiçbir satır yazılmaz. Bu sınır gerçek veri hacmine göre doğrulanır.

- CSV yeni cihaz ekler; mevcut cihazları sessizce güncellemez.
- CSV üzerinden kullanıcı veya zimmet oluşturulmaz. Zimmet, teslim akışından yapılır.
- Mükerrer gönderim iş kimliği ve benzersiz kısıtlarla engellenir.
- Dosya adı, başlatan kişi, zaman, sonuç, satır sayıları audit edilir.
- Excel/CSV çıktılarında formül enjeksiyonuna karşı hücreler güvenli yazılır.

## 9. SLA ve zaman hesaplama

İlk sürümde Incident ilk yanıt/çözüm; Request ilk yanıt/yerine getirme SLA'ları bulunur.

- İlk yanıt: IT personelinin ilk kullanıcıya açık yanıtı; otomatik e-posta, atama ve iç not ilk yanıt sayılmaz.
- Çözüm/yerine getirme: oluşturulmadan `Tamamlandı` anına kadar, ilgili çalışma takvimi ve duraklama kurallarıyla hesaplanır.
- Oluşturulmada kural sürümü kayda bağlanır; sonraki ayar değişiklikleri eski ölçümleri sessizce değiştirmez.
- Öncelik değişiminde yeni hedef, ilk başlangıç anı ve birikmiş süre korunarak uygulanır; eski hedef ve ihlal geçmişi saklanır.
- Transfer hiçbir saati sıfırlamaz.
- Çalışma günleri/saatleri, tatiller ve kurum saat dilimi yönetilebilir.
- Tarihler UTC saklanır; hesaplamada kurum takvimi, gösterimde yapılandırılmış saat dilimi kullanılır.
- Bekleme nedeninin hangi SLA saatini durdurduğu ayrı ayardır.
- Başlangıç önerisi: kullanıcı/yönetici onayı bekleme çözüm süresini durdurur; ilk yanıt süresini durdurmaz. İç ekip bekleme süreyi durdurmaz.
- Hem takvim süresi hem SLA'ya sayılan süre raporlanır. Aktif personel çalışma süresi bu farktan otomatik çıkarılamaz.
- Uyarı eşiği, ihlal olayı ve bildirim durumu kaydedilir; aynı eşik için mükerrer uyarı önlenir.
- İptal/ret ve “Hizmet talebi gerekli” sonuçları başarılı çözüm metriklerinden ayrı raporlanır.

Gerçek P1–P4 hedefleri, Request türü hedefleri, mesai takvimi ve duraklama politikası canlıya geçmeden kesinleştirilecektir; örnek süreler kurumsal taahhüt değildir.

## 10. E-posta bildirimleri

İlk sürüm olayları: kayıt açılması, atama/transfer, kullanıcıya açık yanıt, onay bekleme/karar, tamamlama, SLA uyarısı/ihlali, hesap aktivasyonu ve şifre sıfırlama.

- Türkçe şablonlar kullanılır; olayın alıcısı ve içerik görünürlüğü backend'de belirlenir.
- Tamamlanma e-postası kullanıcıya açık çözümü içerir; iç not, özel kök neden veya iç ek içermez.
- Veritabanı değişikliği ve outbox olayı aynı transaction içinde yazılır.
- Arka plan worker gönderir; artan aralıklı sınırlı retry, başarısız işler listesi ve yönetici yeniden deneme işlemi bulunur.
- SMTP hatası ana işlemi geri aldırmaz. Sağlayıcı belirsizliğinde mutlak tek teslim garantisi verilmez; uygulama düzeyinde mükerrer iş engellenir.
- Log: olay, alıcı, zaman, deneme sayısı, sonuç ve temizlenmiş hata. Şifre/reset token loglanmaz.
- Kullanıcılara şifre e-postayla gönderilmez; süreli şifre belirleme bağlantısı tercih edilir.

E-posta şablonları kod içine gömülmez; `/admin` üzerinden yönetilir. Kullanılabilir değişkenler kontrollü listeden seçilir: kayıt numarası, konu, durum, kullanıcıya açık çözüm ve tamamlanma zamanı. Şablon değişkenleri iç veriye sınırsız erişim sağlamaz.

Örnek tamamlanma iletisi:

```text
Konu: [INC-2026-002415] Arıza bildiriminiz tamamlandı

Bildirim: Bina 06 internet bağlantısı yok.
Çözüm: Arızalı bağlantı modülü değiştirildi; bağlantı doğrulandı.
Durum: Tamamlandı

Ayrıntıları kayıt ekranından görüntüleyebilirsiniz.
```

Çözümün ne olduğunu anlatmak esastır; teknik iç not ve özel kök neden bilgileri otomatik e-postaya taşınmaz.

## 11. Bilgi bankası

İlk sürüm kapsamındadır.

- Makale: başlık, özet, içerik, kategori, görünürlük, durum, yazar ve güncelleme zamanı.
- Durum: taslak, yayımlanmış, arşivlenmiş.
- Görünürlük: çalışanlara açık veya yalnızca IT/yönetici. “Açık” anonim internet erişimi anlamına gelmez; giriş gerekir.
- Başlık/içerik/kategori araması; Türkçe karakterlerle tutarlı arama.
- IT personeli makale oluşturur/düzenler/yayımlar; değişiklikler izlenir.
- Kayıt çözümüne makale bağlanabilir. İç makale kullanıcıya açık yanıta sızdırılamaz.
- Otomatik ticket → makale üretimi ilk sürüm gereği değildir.

Örnek içerikler: VPN bağlantısı nasıl yapılır, MFA nasıl etkinleştirilir, kurumsal Wi-Fi bağlantısı, Outlook kurulumu ve şifre değiştirme. “Nasıl Yapılır?” erişimi, çalışanın kayıt açmadan çözüm bulmasına yardımcı olur.

## 12. Gelişmiş raporlama ve dashboard

İlk sürümde bulunur; yalnızca özet kutularla sınırlanmaz.

| Alan | Ölçümler |
|---|---|
| Incident | Hacim, ilk yanıt, çözüm süresi/MTTR, açık iş ve yaşlandırma, SLA uyumu |
| Request | Hacim, yerine getirme süresi, onay bekleme süresi, ret oranı, SLA uyumu |
| Operasyon | Ekip/personel yükü, kategori/lokasyon/departman dağılımı, bekleme nedenleri |
| Transfer | Transfer sayısı/oranı, yeniden atama, Incident → Request ilişkisi |
| Varlık | Tür/durum/lokasyon dağılımı, zimmetler, stok, bakım/kayıp/kullanım dışı |
| Garanti | Süresi dolan ve seçilen dönemde dolacak cihazlar |

- Tarih, kayıt türü, öncelik, kategori, ekip, personel, departman, lokasyon, durum ve SLA filtreleri.
- Gösterge → filtrelenmiş kayıt listesi drill-down.
- Excel dışa aktarımı; büyük sonuçlar için arka plan işi ve süreli indirme.
- Ekran, drill-down ve çıktı aynı yetki kapsamına sahiptir; çıktı indirilirken yetki tekrar denetlenir.
- MTTR: seçilen dönemde tamamlanan gerçek Incident'ların toplam çözüm süresi / uygun Incident sayısı; takvim ve SLA süresi ayrı etiketlenir.
- SLA uyumu: hedef içinde tamamlanan uygun kayıtlar / tamamlanan uygun kayıtlar. Açık ihlaller ayrıca gösterilir; rapor kapsamı/payda görünürdür.
- Ret, iptal ve tür yanlışlığı sonuçları ayrı sayılır; sıfır payda `Veri yok` gösterir.
- Mevcut ekip yükü ile geçmiş işlem anındaki ekip performansı ayrı raporlanır; geçmiş atamalar korunur.

## 13. Ekranlar ve mobile-first deneyim

### Çalışan alanı

Ana sayfa, Arıza Bildir, Hizmet Talep Et, Kayıtlarım, Kayıt Detayı, Zimmetlerim, Bilgi Bankası, Profil/Şifre.

### IT çalışma alanı

Ekip Kuyruğu, Bana Atananlar, SLA Riski, Incident/Request listeleri, Kayıt Detayı, Envanter, Varlık Kartı, Teslim/İade, CSV Aktarımı, Bilgi Bankası Yönetimi, Raporlar.

### `/admin`

Kullanıcılar, Departmanlar, Lokasyonlar, Ekipler/Üyelikler, Onaylayıcılar/Onaylarım, Kategoriler, Talep Türleri, Varlık Tanımları, SLA/Takvim, E-posta Şablonları/Logları, Audit, Sistem Ayarları.

### Ana ekran ve kayıt detayı

Çalışan ana ekranı: “Bir şey çalışmıyor”, “Bir şey istiyorum”, “Kayıtlarım”, “Zimmetlerim” ve “Nasıl Yapılır?”. Kullanıcıya Incident/Request terminolojisi zorunlu tutulmaz.

IT çalışma alanının kartları: Bana Atananlar, Ekibimdeki Kayıtlar, SLA Riski, Beklemede ve Bugün Tamamladıklarım. Kartlar ilgili filtreli kuyruğu açar. “Atanmamış”, ekip atanmış fakat personeli belirlenmemiş kayıtları ifade eder.

Kayıt listesi sütunları: numara, tür, konu, talep sahibi, lokasyon, durum, ekip, sorumlu, öncelik ve SLA. Mobil görünümde özet alanlar gösterilir; detaylar kayıt ekranında açılır.

Kayıt detayında konu/durum, kullanıcı/lokasyon, ekip/sorumlu, öncelik/SLA, ilişkili varlıklar, onay bilgisi ve timeline bulunur. Yetkiye ve duruma göre “Üzerime Al”, “Transfer Et”, “Beklemeye Al”, “İlişkili Talep Oluştur” ve “Tamamla” aksiyonları gösterilir. İç not ile kullanıcıya yanıt girişi açık biçimde ayrılır.

### Arama ve kategori yapısı

Arama; kayıt numarası, konu, açıklama, lokasyon, kategori ve bilgi bankasını kapsar. Yetkili IT/yönetici kullanıcı veya varlık numarasıyla da ilişkili kayıtları bulabilir. Sonuçların başlıkları, sayıları ve önizlemeleri dahil erişim kapsamı korunur.

Kategori ağacı kısa tutulur. Örnekler: Ağ → İnternet/VPN/Wi-Fi; Kurumsal Uygulamalar → Erişim/Hata; Microsoft 365 → Outlook/Teams/OneDrive; Cihazlar → Bilgisayar/Telefon/Tablet. Kategori ile varlık türü aynı kavram değildir.

### Tasarım kuralları

- Önce küçük ekran; liste yerine gerektiğinde kartlar, erişilebilir filtre çekmeceleri ve kısa formlar.
- Kritik işlemler yalnızca hover ile erişilebilir olmaz.
- Klavye kullanımı, görünür odak, form etiketleri, hata açıklaması ve yeterli renk kontrastı sağlanır.
- En az 360 px genişlikte temel akışlar yatay kaydırma gerektirmez. Büyük rapor tabloları kontrollü yatay kaydırılabilir.
- Yükleniyor/boş/hata/başarılı durumları tasarlanır; çift gönderim engellenir.
- Çalışan kayıt oluşturma hedefi 30–60 saniye; pilotta ölçülür.

## 14. Türkçe i18n ve ek yardımcıları

- Menü, buton, doğrulama, hata, boş durum, e-posta ve rapor etiketleri Türkçedir.
- UI metinleri bileşene gömülmez; i18n anahtarları kullanılır.
- API sabit hata kodu döndürür; kullanıcıya gösterilecek Türkçe karşılık merkezi kaynaktan gelir.
- Tarih/sayı biçimlendirmesi `tr-TR`; tarih saat dilimi ayrıca yapılandırılır.
- Büyük/küçük harf dönüşümü ve aramada `I`, `İ`, `ı`, `i` farkı gözetilir.
- Ek yardımcıları son sesliye göre iki/dört yönlü ünlü uyumunu destekler; gerektiğinde kaynaştırma harfi ve `d/t` uyumu da ele alınır.
- Özel isim, kısaltma, rakam ve yabancı marka için yalnızca yazımdan doğru telaffuz varsayılmaz. Telaffuz/istisna girdisi veya eksiz alternatif cümle kullanılır.
- Örnek doğrulamalar: `Ankara'ya`, `İzmir'e`, `tabletler`, `telefonlar`.
- Rastgele ada ek yapıştırmak yerine uygun yerlerde `Atanan kişi: …` gibi güvenli metin tercih edilir.

## 15. Teknik mimari

Modüler monolit; .NET 10 API, EF Core ve PostgreSQL. React + Vite + TypeScript frontend. Mikroservis zorunluluğu yoktur.

```text
React Web: Çalışan + IT + /admin
                 │ HTTPS
                 ▼
           ASP.NET Core API
                 │
             Application
                 │
               Domain

Infrastructure → Application / Domain
  EF Core / PostgreSQL / Dosya / SMTP / Worker
```

### Katmanlar

| Katman | Sorumluluk |
|---|---|
| Domain | İş kuralları, varlıklar, durum geçişleri; EF/HTTP bağımlılığı yok |
| Application | Kullanım senaryoları, doğrulama, erişim kapsamı, arayüzler |
| Infrastructure | EF Core, PostgreSQL, dosya saklama, kimlik ve e-posta adaptörleri |
| API | HTTP sözleşmeleri, kimlik doğrulama, politikalar, hata eşleme, composition root |
| Worker | Outbox, SLA kontrolü, aktarım/rapor işleri; aynı iş kurallarını kullanır |

API iş verisine doğrudan DbContext üzerinden müdahale etmez; Application kullanım senaryolarını çağırır. Composition root altyapı implementasyonlarını bağlar.

### Teknik esaslar

- ASP.NET Core Identity tabanlı yerel hesaplar; güvenli hash ve giriş denemesi sınırı.
- Aynı origin dağıtım hedefiyle Secure/HttpOnly oturum cookie'si; durum değiştiren isteklerde CSRF koruması. Dağıtım farklı origin gerektirirse ADR ile yeniden değerlendirilir.
- EF Core/Npgsql sürümleri seçilen .NET/EF sürümleriyle uyumlu olarak sabitlenir; migration'lar sürümlenir.
- PostgreSQL foreign key, benzersiz kısıt ve index'leri iş kurallarını destekler.
- Transaction: zimmet, onay geçişi, kayıt + audit + outbox gibi atomik işlemler.
- Eşzamanlılık token'ı; çakışmada sessiz son yazan kazanır yerine anlamlı hata.
- Liste API'lerinde sunucu taraflı sayfalama, filtre, sıralama ve üst sınır.
- OpenAPI sözleşmesi; frontend DTO'larıyla senkronizasyon.
- Dosya metadata'sı veritabanında; içerik saklama adaptörü yayın ortamına göre seçilir.
- Secrets kaynak kodda, frontend paketinde veya loglarda bulunmaz.
- Worker işleri yeniden başlatmaya dayanıklı ve birden fazla instance altında güvenli olmalıdır.

## 16. Temel veri modeli

| Grup | Varlıklar |
|---|---|
| Kimlik/kurum | User, Role, Department, Location, Team, TeamMembership |
| Destek | Ticket, IncidentDetails, RequestDetails, Category, RequestType |
| Süreç | AssignmentHistory, StatusHistory, Approval, TicketRelation |
| İletişim | Comment, Attachment, NotificationOutbox, EmailDelivery |
| Varlık | Asset, AssetAssignment, AssetMovement, TicketAsset, ImportJob |
| SLA | SlaPolicyVersion, BusinessCalendar, SlaInstance, SlaPause, SlaEvent |
| Bilgi | KnowledgeArticle, ArticleRevision, TicketArticle |
| Takip | AuditEvent, ExportJob |

Ortak Ticket tabanı kullanılması Incident/Request ayrımını kaldırmaz; tür, iş kuralı, detay ve numaralandırma ayrıdır. Bu model taslak uygulama yönüdür; nihai şema ADR ve migration ile somutlaştırılır.

Temel kısıtlar: benzersiz kullanıcı e-postası, kayıt numarası ve demirbaş numarası; varlık başına tek aktif zimmet; Request başına tek bekleyen onay; geçerli ekip üyeliği ve ilişki referansları.

## 17. Audit, güvenlik ve veri yaşam döngüsü

- Kritik olaylar: kullanıcı/rol, giriş güvenlik olayları, atama, transfer, öncelik, durum, onay, zimmet, CSV, SLA ayarı, içerik görünürlüğü ve dışa aktarım.
- Audit: kim, ne, hangi kayıt, UTC zaman, gerekçe, uygun eski/yeni değer, correlation ID.
- Şifre, token, SMTP sırrı ve gereksiz kişisel içerik audit/loga yazılmaz.
- Audit normal uygulama akışıyla değiştirilemez; yönetici okuyabilir, içerik düzenleyemez.
- Ticket ve hareket geçmişi normal UI üzerinden hard delete edilmez. Yanlış kayıt gerekçeli iptal edilir.
- Referans veriler pasifleştirilir. Makaleler arşivlenir. Teknik soft delete genel bir “geçmişi gizle” mekanizmasına dönüşmez.
- Saklama süreleri ve özel silme/anonimleştirme prosedürü işletme kararı olarak ayrıca belirlenir; süresiz saklama varsayılmaz.
- Uygulama, dosya ve PostgreSQL yedeği birlikte değerlendirilir; geri yükleme provası yapılır.
- Yapılandırılmış loglar, sağlık kontrolü, worker/SMTP başarısızlık görünürlüğü ve alarm sahipliği tanımlanır.

## 18. ADR ve dokümantasyon kuralları

### 18.1 Mimari karar kayıtları

- Konum: `docs/adr/`.
- Michael Nygard formatı: başlık, durum, bağlam, karar, sonuçlar.
- Kararlar tarih artan sırayla numaralandırılır: `0001-YYYY-MM-DD-kisa-baslik.md`.
- Önemli mimari karar commit edildiğinde ilgili ADR eklenir; PR tamamlanmadan güncelliği kontrol edilir.
- Eski karar sessizce değiştirilmez; yeni ADR ile yerini alan karar bağlantılanır.
- ADR hazırlarken `/ecc:architecture-decision-records` kullanılır.

İlk ADR konuları: Clean Architecture/modüler monolit, ortak web uygulaması ve `/admin`, yerel kimlik ve rol politikaları, PostgreSQL/EF Core, varlık/zimmet tutarlılığı, SLA takvimi, outbox, Türkçe i18n.

Bu analiz belgesi ADR yerine geçmez; henüz bir kod deposunda mimari commit veya ADR oluşturulmuş değildir.

### 18.2 Ürün ve kullanıcı belgeleri

- Repo kökündeki `capabilities.md`, uygulamanın ne yaptığının ve teknoloji yığınının yetkili özetidir.
- Özellik tamamlandığında `capabilities.md` güncellenir; planlanan ve mevcut özellik açıkça ayrılır.
- Rehber: `docs/user-guide/`; çalışan, IT ve yönetici akışlarını kapsar.
- Rehber değişikliklerinde `docs/user-guide/combined.md` aynı değişiklikte senkronize edilir.
- PDF hedefinde localhost bağlantıları ilgili belge içi anchor'a dönüştürülür; kırık anchor kontrolü yapılır.
- Bu dosya analiz/yol haritası kaynağıdır; uygulama sonrası özellik envanterinin yerini almaz.

### 18.3 Geliştirme çalışma kuralları

- İletişim kısa ve doğrudan; kod, yol, komut, URL ve sayılar aynen korunur.
- Commit mesajlarında `Co-Authored-By:` trailer bulunmaz.
- Context Mode araçları varsa tercih edilir; önce arama/özet, sonra gerekli kaynak bölümü okunur.
- Gereksiz tam dosya okuma, yinelenen arama ve bağlam tekrarı önlenir.
- Proje hafızası istendiğinde `~/.claude/projects/<project>/memory/` altında her olgu ayrı `name.md`; `MEMORY.md` içinde tek satırlık işaretçi tutulur. Bu çalışma kapsamında hafıza dosyası oluşturulmaz.

## 19. Geliştirme yol haritası

Ekip büyüklüğü, çalışma kapasitesi ve yayın ortamı bilinmediğinden takvim günü taahhüdü verilmez. Plan bağımlılık ve çıkış ölçütleriyle ilerler. Üretim planlama becerisinden yalnızca kapasiteye göre sıralama, darboğaz ve değişiklik kontrolü yaklaşımı alınır; üretim hattı/OEE kuralları yazılım gereksinimi değildir.

| Faz | İçerik | Bağımlılık | Çıkış ölçütü |
|---|---|---|---|
| 0 — Tasarım | Akışlar, rol matrisi, mobil ekranlar, SLA/şema, ADR taslakları | Kapsam kararı | Veri/akış sözleşmeleri ve açık karar sahipleri belli |
| 1 — Temel | Katmanlar, DB/migration, ilk yönetici, giriş, `/admin`, kurum/ekip, audit | Faz 0 | Kullanıcı yalnızca panelden açılır; backend erişim sınırları doğrulanır |
| 2 — Varlık | Envanter, CSV, zimmet, teslim/iade, geçmiş | Faz 1 | Aynı cihaz çift zimmetlenemez; hatalı CSV veri yazmaz |
| 3 — Destek | Incident/Request, atama, yorum/ek, onay, varlık ilişkisi, kapanış | Faz 1; varlık bağlantısı Faz 2 | Uçtan uca talep/arıza/onay akışları çalışır |
| 4 — SLA/E-posta | Takvim, saatler, uyarı/ihlal, outbox/worker, şablonlar | Faz 3 olay modeli | Transfer zamanı korur; SMTP kesintisi iş kaybettirmez |
| 5 — Bilgi/Rapor | Bilgi bankası, tüm raporlar, filtre/drill-down/Excel | Faz 2–4 verileri | Görünürlük ve rapor hesapları tutarlı |
| 6 — İlk sürüm | Entegrasyon/E2E, mobil kontrol, restore provası, rehber, pilot | Faz 1–5 | İlk sürüm kabul ölçütlerinin tamamı sağlanır |

Bilgi bankası çalışması ekip kapasitesi uygunsa veri sözleşmeleri sabitlendikten sonra paralel yürütülebilir. Envanter, onay, SLA ve rapor veri modeli kritik bağımlılıklardır. Kapsam değişiminde etkilenen faz ve kabul ölçütleri güncellenir; özellikler sessizce sonraki sürüme taşınmaz.

## 20. Test ve ilk sürüm kabul ölçütleri

1. Anonim kullanıcı hesap açamaz; çalışan/IT kullanıcı oluşturma API'sine erişemez.
2. Kurulum ilk yönetici oluşturulduktan sonra tekrar hesap oluşturma yolu açmaz.
3. İlk girişte şifre değişimi zorunlu; pasif hesabın oturumu geçersizdir.
4. Çalışan başka kullanıcının kaydını, iç notunu, ekini veya zimmetini ID değiştirerek okuyamaz.
5. IT personeli yetkisiz ekip kaydına arama/rapor/çıktı üzerinden erişemez.
6. Onaylanmamış Request tamamlanamaz; talep sahibi kendi onayını veremez.
7. IT tamamlama işlemi kaydı doğrudan kapatır; kullanıcı onayı beklenmez.
8. Eşzamanlı teslim işlemlerinde tek aktif zimmet oluşur; iade/devir geçmişi kaybolmaz.
9. CSV mükerrer/hatalı satırları bildirir; başarısız aktarım kısmi veri bırakmaz; tekrar gönderim çoğaltmaz.
10. Kayıt ve varlık bağlantıları karşılıklı görünür; erişim kapsamı korunur.
11. SLA tatil, mesai sınırı, saat dilimi, duraklama ve öncelik değişiminde tutarlıdır; transfer sıfırlamaz.
12. Outbox kayıtla atomiktir; worker/SMTP yeniden başlatıldığında bekleyen işler işlenir.
13. Bilgi bankasının iç içerikleri çalışan aramasında ve doğrudan URL erişiminde görünmez.
14. Rapor, drill-down ve Excel aynı filtre/payda/yetkiyle sonuç verir.
15. Türkçe karakterler, i18n eksik anahtarları ve ek yardımcıları doğrulanır.
16. 360 px ekran, klavye ve form hata akışları kontrol edilir.
17. Temel akışlar unit/integration/E2E testleriyle; ilişkisel kısıtlar gerçek PostgreSQL üzerinde doğrulanır.
18. Pilot veri hacminde liste/rapor/CSV performansı ölçülür; sayısal hedefler kapasite kararı sonrası belirlenir.
19. Yedek veritabanı ve dosyalar yeni ortama geri yüklenir; ilişki ve oturum güvenliği kontrol edilir.
20. `capabilities.md`, ADR'ler, kullanıcı rehberi ve `combined.md` teslimle uyumludur.

## 21. Açık kararlar ve uygulama varsayımları

Aşağıdakiler ilk sürüm kapsamını azaltmaz; uygulama/operasyon ayrıntısıdır.

| Konu | Mevcut durum / gerekli karar |
|---|---|
| Yayın | Şirket içi sunucu veya bulut henüz seçilmedi |
| Kapasite | Kullanıcı, eşzamanlı kullanıcı, cihaz, aylık ticket/ek hacmi bilinmiyor |
| Takvim | Kurum saat dilimi, mesai ve tatil günleri kesinleştirilecek |
| SLA | P1–P4 ve talep türü hedefleri; bekleme duraklama kuralları doğrulanacak |
| E-posta | SMTP sağlayıcısı, gönderen alan adı ve üretim ayarları seçilecek |
| Dosya | Saklama adaptörü, zararlı içerik taraması, kota ve saklama süresi seçilecek |
| Yedekleme | RPO/RTO, periyot, saklama, erişim ve sorumlu belirlenecek |
| Veri saklama | Ticket, audit, ek, e-posta logu ve kullanıcı verisi süreleri belirlenecek |
| İlk veri | Departman/lokasyon, karşılama ekibi, onaylayıcılar, kategoriler hazırlanacak |
| Yeniden açma | Taslakta yok; ilişkili yeni kayıt yaklaşımı kullanılıyor |
| Teknik limitler | 10 MB dosya, 5 ek/istek, 5.000 CSV satırı başlangıç varsayımı |
| Takvim tahmini | Ekip kapasitesi ve yukarıdaki kararlar sonrası süre tahmini yapılacak |

## Ek A — İki dosyanın karşılaştırması

| Konu | İlk dosya | İkinci dosya | Bu belgede uygulanan sonuç |
|---|---|---|---|
| Proje açıklaması | Çalışan, IT ve yönetim açısından güçlü anlatım | Kısa ürün/kapsam özeti | İlk dosyanın amacı, vizyonu ve senaryoları esas |
| Kullanıcı deneyimi | “Bir şey çalışmıyor / istiyorum”; sade ekranlar | Mobile-first ekran ve erişilebilirlik kuralları | İlk anlatım + ikinci tasarım kararları |
| Varlık yönetimi | İlk sürüm dışında | İlk sürümde envanter, zimmet, CSV | İkinci dosya geçerli |
| Yönetim paneli | Ayrı admin alanı yok | Aynı uygulamada `/admin` | İkinci dosya geçerli |
| Yetkiler | Esnek permission paketleri ve doğrudan kullanıcı yetkisi | Üç sabit rol, backend politika/kayıt kapsamı | İkinci karar; ilk dosyanın aksiyon denetimi korunur |
| Kullanıcı oluşturma | Panelle sınırlama açık değil | Yalnızca panel; ilk yönetici kurulumda | İkinci dosya geçerli |
| Kurum | Şirket/lokasyon kavramları; sınırlar belirsiz | Tek şirket, çok departman/lokasyon | İkinci dosya geçerli; gereksiz şirket filtresi yok |
| Incident/Request | Ayrı türler, ilişkili Request akışı | Aynı yaklaşım, erişim/onay ayrıntıları | İlk anlatım + ikinci iş kuralları |
| Onay | Bekleme nedeni olarak var; süreç sınırlı | Tek aşamalı onay | İkinci dosya geçerli |
| Kapanış | Tamamlandı terminal durum | IT tamamlayınca doğrudan kapanış | Kullanıcı onayı eklenmez |
| Transfer | Neden, ekip/personel, SLA korunması | Kapsam ve eşzamanlılık kuralları | İlk örnekler + ikinci kurallar |
| SLA | İlk yanıt/çözüm; örnek süreler | Sürüm, takvim, duraklama, açık hedefler | İkinci kurallar; örnekler taahhüt değil |
| E-posta | Problem/tespit/çözüm anlatımı; şablonlar | Yalnızca kullanıcıya açık çözüm; outbox | Açıklayıcı ileti + ikinci gizlilik ve teslim modeli |
| Bilgi bankası | Açık/iç içerik | Giriş gerektiren çalışan/iç görünürlük | İlk kullanım örnekleri + ikinci erişim kararı |
| Raporlar | Göstergeler, drill-down, Excel | Formüller, veri/yetki kapsamı, varlık raporları | Her ikisi; tamamı ilk sürüm |
| Silme | İptal ve soft delete vurgusu | İptal/pasifleştirme; geçmişin korunması | İkinci veri yaşam döngüsü geçerli |
| Teknik yığın | Genel modüler/API yaklaşımı | .NET 10, EF Core, PostgreSQL, React/Vite/TypeScript | İkinci dosya geçerli |
| Yol haritası | Helpdesk'ten genişleyen fazlar | Varlık dahil; tüm modüller ilk sürüm teslimi | İkinci fazlar; bağımlılık ve çıkış ölçütleri korunur |
| Dokümantasyon | Ayrıntılı analiz metni | ADR, capabilities ve rehber kuralları | İkinci çalışma kuralları korunur |

## Ek B — Birleştirme ve kalite ölçütleri

`ecc:prompt-optimizer` yaklaşımından amaç analizi, eksik bağlam ayrımı, kapsam sınırı ve doğrulanabilir kabul ölçütleri kullanıldı. İstenen çıktı yeni proje belgesi olduğundan sonuç yalnızca yeniden kullanılabilir bir promptla sınırlanmadı.

Birleştirme ilkeleri:

1. Projenin neden var olduğunu ve günlük kullanımını ilk dosyanın anlatımıyla açıkla.
2. Kapsam, teknoloji, panel, kullanıcı ve iş akışı çelişkilerinde ikinci dosyayı esas al.
3. İlk dosyadaki örnekleri yeni kararlara ve Türkçe arayüze uyumla.
4. İkinci dosyanın kabul ölçütlerini ve açık kararlarını kaybetme.
5. Varsayımları kesin kullanıcı kararı gibi sunma; kapsamı kendiliğinden büyütme.
6. Kaynak dosyaları değiştirmeden tek başına anlaşılabilen yeni Markdown üret.

Bu belgedeki sayısal limitler ve henüz kararlaştırılmamış operasyon ayrıntıları Bölüm 21'de görünür tutulur. Yayın ortamı ve kapasite bilinmeden kesin teslim tarihi veya performans taahhüdü verilmez.
