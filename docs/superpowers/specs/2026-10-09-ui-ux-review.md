# TGYSM — UI/UX İnceleme ve Geliştirme Önerileri

**Tarih:** 9 Ekim 2026  
**Sürüm:** 2.0 — CoreUI dönüşümü ve Claude uygulama şartnamesi  
**İncelenen uygulama:** http://localhost:5173/  
**Amaç:** Tüm uygulamayı CoreUI Free React Admin Template ile yenilemek; mobile-first kullanım, breadcrumb ile yön bulma, ayrı yönetim alanı, merkezi tanımlar ve anlaşılır raporlar.
**Uygulayıcı:** Claude. Bu dosya geliştirme girdisidir; bu inceleme sırasında uygulama revize edilmemiştir.

> Bu belge tamamlanmış geliştirme beyanı değildir. Canlı gözlemleri, kaynak kodu bulgularını ve önerilen değişiklikleri ayırır. İnceleme sırasında uygulama kodu, kullanıcı, cihaz veya süreç kayıtları değiştirilmedi. Formlar açıldı ancak kaydedilmedi; e-posta gönderilmedi, şifre değiştirilmedi ve dosya aktarımı başlatılmadı.

**Claude için güncel karar özeti:** CoreUI Free React Admin Template tüm ürünün tasarım tabanıdır. Breadcrumb zorunludur. Operasyon ve admin ayrı yerleşim/menüyle aynı ürün içinde kalır. Marka/model merkezi katalogdan seçilir ve demirbaş numarası sunucuda üretilir. Grafik hedefi CoreUI Chart.js entegrasyonudur; eski Recharts hedefi geçersizdir. Bölüm 5’teki 47 ekran/akış incelemesi, bölüm 4.4–4.6’daki dönüşüm sözleşmesi ve bölüm 15’teki uygulama teslim kriterleri birlikte uygulanmalıdır.

## 1. Kararlar, kaynaklar ve inceleme sınırı

### 1.1 Kullanıcının açık talepleri

- Arayüzü özellikle UI/UX açısından iyileştirmek ve mobile-first kullanımı temel almak.
- Admin panelini günlük operasyon deneyiminden tamamen ayırmak.
- Marka/model gibi tekrar kullanılan verileri admin tarafından yönetilen seçim listeleriyle toplamak.
- Yazım hataları ve mükerrer kayıtları önlemek.
- Yeni cihazlarda demirbaş numarasını sistemin otomatik ataması.
- **Tüm arayüz ve yönetim panelinde CoreUI Free React Admin Template kullanmak:** https://github.com/coreui/coreui-free-react-admin-template
- Önceki Recharts tercihini kaldırmak; grafiklerde CoreUI template ekosisteminin Chart.js tabanlı bileşenlerini kullanmak.
- Uygulama içindeki konumu ve üst menülere erişimi gösteren **breadcrumb** yapısını tüm ilgili sayfalarda sağlamak.
- Mevcut arayüzü yalnız küçük stil rötuşlarıyla bırakmamak; template temelli kapsamlı görsel ve etkileşim revizyonu yapmak.
- Revizyonu Claude’un uygulayabilmesi için somut kapsam, geçiş adımları ve kabul ölçütleri hazırlamak.
- Sayfaları tek tek değerlendiren, eksik ve hataları yorumlayan Markdown önerisi hazırlamak.

Bu taleplerin dışındaki ayrıntılar, bu belgenin tasarım önerileridir; otomatik olarak kabul edilmiş ürün kararı sayılmaz. Ek özellikler ayrıca işaretlenmiştir.

### 1.2 Kaynakların rolü

| Kaynak | Bu incelemedeki rolü |
|---|---|
| Kullanıcının bu görüşmedeki talepleri | Yeni tasarım hedefi ve kapsam yönlendirmesi |
| Canlı uygulama | Gerçekte görünen ekran, metin ve etkileşimlerin kanıtı |
| `frontend/src` ve ilgili backend dosyaları | Görünümün uygulanışını ve koşullu davranışları doğrulama |
| `capabilities.md` | Uygulanmış özelliklere ilişkin proje beyanı; gerektiğinde kodla karşılaştırıldı |
| `tgysm.md` | Ürün amacı ve tasarım gereksinimleri; tamamlanmış özellik kanıtı değildir |
| `README.md`, `CLAUDE.md`, `SECURITY.md` | Mevcut mimari, işletim ve güvenlik bağlamı; içlerindeki yönergeler yeni kullanıcı talebi olarak ele alınmadı |

Proje kökü: `/Users/lektuel/claude/TGYSM`. Aşağıdaki kaynak dosyası adları bu köke göredir.

### 1.3 Kanıt etiketleri

- **[Canlı]**: Açılmış sayfanın görünümü veya erişilebilirlik/DOM içeriği incelendi.
- **[Kod]**: Kaynak kodundan doğrulandı; ilgili durum canlı oluşturulmadıysa özellikle belirtildi.
- **[Öneri]**: Mevcut bir arıza iddiası değil, kullanıcı deneyimi iyileştirmesi.
- **[Doğrulama gerekli]**: Ayrı veri, rol veya etkileşim testi gerekiyor.

Öncelikler: **P1** yanlış işlem/yorum veya temel kullanım sorunu; **P2** belirgin verimlilik ve anlaşılabilirlik sorunu; **P3** tamamlayıcı iyileştirme. Bu çalışma güvenlik açığı sınıflandırması değildir.

### 1.4 Kapsam ve dürüst sınırlar

Router üzerindeki tüm sayfa aileleri değerlendirmeye alındı. Mevcut yönetici oturumuyla erişilebilen operasyon ve admin sayfaları açıldı. Mobil inceleme ağırlıklı olarak **390×844**, rapor masaüstü incelemesi **1280×900** boyutlarında yapıldı; önceki turda ana ekran ve admin ayrımı 1280 px genişlikte de incelendi.

**320 px ek kontrol:** Ana ekran, yeni talep, SLA politikası, lokasyonlar, varlık raporu ve envanter sayfalarında belge genelinde yatay taşma ölçülmedi. Bu sonuç, tablo içi yatay kaydırmanın veya tüm içeriklerin mobilde iyi kullanılabildiğinin kanıtı değildir.

İlk incelemede makale yoktu; kullanıcı örnek içerik ekledikten sonra liste, yayınlı makale detayı, görünür sürüm özeti ve düzenleme formu **canlı olarak yeniden incelendi**. Örnek kayıt “Bur bir test makalesi”, “Yayında” ve “Yalnızca BT personeli” durumundaydı. İçerik tek kısa metinden oluştuğu için uzun makale, tablo, kod bloğu ve çok sürümlü karşılaştırma canlı doğrulanmış sayılmaz. Zimmetli cihaz ve e-posta gönderimi/dışa aktarım listeleri mevcut oturumda boştu. Bu sayfaların dolu, hata ve işlem sonrası durumları tamamlanmış canlı test sayılmaz. Giriş ekranı önceki inceleme turunda görüldü. Geçerli şifre belirleme bağlantısı, zorunlu şifre değişimi, çalışan/IT rolüyle ayrı oturum, gerçek mobil klavye, ekran okuyucu, büyük veri, dosya yükleme ve kayıt değiştiren işlemler test edilmedi. 403/404 bileşenleri kod üzerinden incelendi.

## 2. Genel değerlendirme

Uygulamanın tutarlı bileşenleri var: mobil kart/masaüstü tablo yaklaşımı, geniş form kontrolleri, Türkçe metin altyapısı, Radix diyalogları, durum rozetleri, hata/yükleme bileşenleri ve ayrı admin yerleşimi. Kullanıcının son kararıyla mevcut özel görünüm hedef tasarım olarak korunmayacak; tüm sayfalar CoreUI görünümüne taşınacak. Veri erişimi ve iş kuralları korunarak sunum katmanı kapsamlı yenilenecek.

Temel eksiklik, **bilginin ve eylemlerin önem sırasının yeterince ayrışmaması**. Ana ekran bir özellik kataloğu gibi; rapor sayfaları önce filtre formunu, sonra sonucu gösteriyor. Detay ekranlarında işlem yapmak için uzun bilgi bloklarının geçilmesi gerekiyor. Aynı görsel kart kalıbı, farklı önemdeki içerikleri eşit ağırlığa getiriyor.

Hedef tasarım yönü: sakin, iş odaklı, okunabilir; masaüstünde taraması hızlı, mobilde az karar ve az kaydırma gerektiren bir operasyon uygulaması. Dekoratif grafik, büyük tanıtım alanı veya gereksiz animasyon eklenmemeli.

## 3. Önce ele alınması gereken somut bulgular

| ID | Öncelik | Kanıt | Bulgu | Sonuç / öneri |
|---|---|---|---|---|
| UX-01 | P1 | Canlı + Kod | Demirbaş numarası zorunlu metin alanı | Sunucu üretmeli; kullanıcı numara düşünmemeli |
| UX-02 | P1 | Canlı + Kod | Marka/model serbest metin | İlişkili katalog kimlikleri, admin yönetimi ve CSV eşleme |
| UX-03 | P1 | Canlı + Kod | SLA hücrelerinde dakika/yüzde için görünür alt etiket yok | Hedef ve uyarı değerleri karıştırılabilir; mobil kart formuna geçilmeli |
| UX-04 | P1 | Canlı + Kod | Eski worker örneklerinde de “Zamanında” rozeti | İş genelinin sağlığı ile örnek bazlı güncellik ayrılmalı |
| UX-05 | P1 | Canlı | “Onaylayıcı ata” işleminin son düğmesi “Onayla” | Talebi onaylamakla kişiyi atamak karışıyor; “Onaylayıcıyı ata” olmalı |
| UX-06 | P2 | Canlı + Kod | Rapor filtreleri mobil ilk ekranı tamamen kaplıyor | Kompakt tarih seçimi + filtre paneli + hemen görünen özet |
| UX-07 | P2 | Canlı + Kod | Denetim filtresinde yalnız kullanıcı/departman/lokasyon/ekip var | Listede görünen kayıt/cihaz gibi türler seçilerek filtrelenemiyor |
| UX-08 | P2 | Kod | Karşılama ekibi rozeti masaüstünde iki kez üretiliyor, mobil kartta yok | Aynı bilgi her görünümde bir kez gösterilmeli |
| UX-09 | P2 | Canlı + Kod | Cihaz detayında marka ve model birlikte “Marka” altında | Ayrı alanlar veya doğru “Marka / model” etiketi |
| UX-10 | P2 | Canlı + Kod | Yeni arıza formunda cihaz seçimi yok | Zimmetli cihaz seçimi eklenmeli; gereksinimle uygulama farkı |
| UX-11 | P2 | Canlı | Boş bilgi bankasında “Aramanızla eşleşen…” mesajı | İlk kullanım ile sonuçsuz arama ayrılmalı |
| UX-12 | P2 | Kod | Geçersiz rapor tarih aralığı okunurken iki tarih sessizce kaldırılıyor | Hata bildirilmeli; kullanıcı fark etmeden kapsam genişlememeli |
| UX-13 | P2 | Kod | Oturum koruyucusu dönüş hedefinde yalnız pathname saklıyor | Oturum sonrası rapor/kuyruk filtreleri kaybolabilir; güvenli query/hash korunmalı |
| UX-14 | P2 | Canlı + Kod | Makale ve uzun formlarda kaydetmeden çıkış koruması görünmüyor | Değişiklik varsa uyarı ve metni koruyan kurtarma akışı |
| UX-15 | P3 | Canlı + Kod | “Bilgi Bankası / Nasıl Yapılır?”, “Konum / Lokasyon”, “IT / BT” karışıyor | Terim sözlüğü ve merkezi metin denetimi |
| UX-16 | P3 | Canlı konsol | Tekrarlanan `HydrateFallback` uyarısı | İlk yükleme fallback davranışı incelenmeli; bu gözlem tek başına üretim hatası kanıtı değil |
| UX-17 | P1 | Kullanıcı bildirimi + Canlı + Kod | Ortak breadcrumb yok; tekil geri bağlantıları hiyerarşiyi anlatmıyor | Ortak CoreUI breadcrumb, tıklanabilir üst seviyeler, aktif sidebar ve güvenilir listeye dönüş |
| UX-18 | P1 | Kullanıcının son tasarım kararı | Mevcut görsel yaklaşım kabul edilmiyor | Tüm uygulama ve admin CoreUI Free React template temelinde yenilenecek |
| UX-19 | P2 | Son canlı makale incelemesi | Makale düzenlemede geri bağlantısı/breadcrumb yok; okuma ekranında yönetim kontrolleri içeriğin önünde | CoreUI sayfa başlığı, okuma alanı, ayrı yönetim aksiyonları ve açık dönüş yolu |

## 4. Bilgi mimarisi ve admin ayrımı

### 4.1 Üç farklı çalışma amacı

**Çalışan portalı:** Yeni destek kaydı, kayıtlarım, zimmetlerim, bilgi bankası. Ana ekranın ilk bölümünde “Arıza bildir” ve “Talep oluştur”; altında güncel kayıtlar. Profil hesap menüsünde.

**IT çalışma alanı:** Bana atananlar, ekip kuyruğu, atanmamış işler, bekleyenler, SLA riskleri, envanter ve raporlar. Varsayılan açılış ekranı kullanıcının rolüne göre gerçek iş listesi olabilir.

**Yönetim alanı:** Organizasyon, kataloglar, süreç ayarları, bildirimler ve sistem. Günlük ticket çözümleme veya cihaz teslimi burada yapılmamalı. Yetkili kullanıcı çalışma alanları arasında açık bir geçiş kontrolü kullanmalı.

### 4.2 Admin menüsü önerisi

| Grup | Sayfalar |
|---|---|
| Genel bakış | Eksik kurulum tanımları, onaylayıcısız departmanlar, karşılama ekibi durumu, sistem uyarıları |
| Organizasyon | Kullanıcılar, departmanlar, lokasyonlar, ekipler |
| Cihaz kataloğu | Markalar, modeller, cihaz türü ilişkileri |
| Hizmet kataloğu | Kategoriler, talep tipleri |
| SLA | Çalışma takvimi, tatiller, hedefler ve duraklatma kuralları |
| Bildirimler | E-posta şablonları, gönderimler |
| Sistem ve denetim | Arka plan işleri, denetim kayıtları |

**Mevcut gerçek:** `AdminLayout` zaten `AppShell` altında iç içe değildir; ayrı route dalındadır. “Admin hiç ayrılmamış” tespiti doğru olmaz. Öneri, var olan ayrımı tamamlamak ve yönetim bilgi mimarisini güçlendirmektir.

Aynı frontend/backend içinde ayrı yerleşim bu ihtiyacı karşılar. Ayrı domain veya deployment, kullanıcının talebinden otomatik çıkarılmamalı. İstenirse daha sonra bağımsız mimari karar olarak ele alınmalı. API tarafındaki admin yetkisi korunmalı; menü gizleme yetkilendirme yerine geçmez.

### 4.3 Navigasyon kuralları

- Masaüstünde operasyon menüsünü “Kişisel”, “IT operasyonu” ve “Analiz” olarak grupla.
- Mobilde çalışan için Ana sayfa / Kayıtlarım / Cihazlarım / Menü; yeni kayıt açmayı belirgin bir eylem olarak sun. IT görünümünde aynı alan iş kuyruğuna göre düzenlenebilir.
- Alt navigasyon eklenirse içerik ve sabit işlem çubuğu safe-area ile birlikte hesaplanmalı; iki sabit çubuk ekranı işgal etmemeli.
- “Çıkış yap” her operasyon ekranının ana eylemi gibi konumlanmamalı; erişilebilir hesap menüsüne taşınmalı.
- Admin alanında masaüstü ve mobilde hesap bilgisi, çıkış ve “Uygulamaya dön” bulunmalı.
- Listeden detaya ve geri dönüşte filtre, sayfa ve mümkünse kaydırma konumu korunmalı.
- Breadcrumb, oturum içindeki tüm ana/alt sayfalarda ortak sayfa başlığının parçası olmalı. Mobilde de kaldırılmamalı; uzun zincir kısaltılabilir fakat üst seviyelere erişim korunmalı. Ayrıntılı sözleşme bölüm 4.4’tedir.

### 4.4 Breadcrumb ve üst menüye dönüş — zorunlu uygulama sözleşmesi

**Sorun:** “Makalelere dön”, “Envantere dön”, “Kayıtlara dön” gibi tek bağlantılar yalnız bir hedef sunuyor. Kullanıcı hangi modülde, hangi alt sayfada bulunduğunu ve üst seviyeler arasındaki ilişkiyi göremiyor. Düzenleme gibi bazı ekranlarda bu bağlantı da bulunmuyor. Ayrıca kuyruktan açılan ticket’ın sabit “Kayıtlarım” hedefine dönmesi geldiği çalışma bağlamını kaybettirebiliyor.

**Üç ayrı görev birlikte çözülmeli:**

1. **Breadcrumb:** Uygulamadaki yapısal konumu gösterir; tarayıcı geçmişinin birebir dökümü değildir.
2. **Sidebar aktifliği:** Bulunulan modül ve onun menü grubunu görünür kılar.
3. **Bağlama dönüş:** “Filtrelenmiş listeye dön” kullanıcının geldiği liste/kuyruk/rapor görünümünü korur.

Bu talep kullanıcıların gezinti hareketlerini başkalarının izlemesi veya audit’e tüm sayfa ziyaretlerini kaydetmek şeklinde yorumlanmamalı. İstenen şey kullanıcının kendi arayüz içinde yönünü bulmasıdır.

CoreUI `CBreadcrumb` / `CBreadcrumbItem` bileşenleri kullanılmalı. Güncel sayfa son öğe olmalı; üst seviyeler gerçek React Router bağlantıları olmalı. Erişilebilir navigasyon etiketi ve son öğede `aria-current="page"` doğrulanmalı. [CoreUI breadcrumb dokümanı](https://coreui.io/react/docs/components/breadcrumb/)

#### 4.4.1 Sayfa ailelerine göre breadcrumb haritası

| Sayfa / durum | Görünen yol | Üst seviyenin davranışı |
|---|---|---|
| Ana ekran | Ana sayfa | Mevcut sayfa; gereksiz tekrar linki yok |
| Kayıtlarım | Ana sayfa › Kayıtlarım | Ana sayfaya erişim |
| Yeni arıza | Ana sayfa › Kayıtlarım › Arıza bildir | Kayıt listesine dönüş |
| Yeni talep | Ana sayfa › Kayıtlarım › Talep oluştur | Kayıt listesine dönüş |
| Ticket detay, kişisel listeden | Ana sayfa › Kayıtlarım › INC/REQ numarası | İlgili kişisel listenin filtre/sayfa durumu |
| IT ana çalışma alanı | Ana sayfa › İş listem | Ana sayfaya erişim |
| IT kuyruğu | Ana sayfa › İş listem › Bana atanan / Ekiplerim / … | İş listesine ve seçili kuyruğa erişim |
| Ticket detay, kuyruktan | Ana sayfa › İş listem › Kuyruk adı › INC/REQ numarası | Geldiği yetkili kuyruğa dönüş |
| Onay yönetimi | Ana sayfa › Onay yönetimi | Üst sayfa erişimi; atama diyalogunda aynı yol korunur |
| Envanter | Ana sayfa › Envanter | Ana sayfaya erişim |
| Cihaz detay | Ana sayfa › Envanter › Demirbaş no | Filtrelenmiş envantere dönüş |
| CSV içe aktar | Ana sayfa › Envanter › İçe aktar | Envantere dönüş; değişiklik varsa çıkış koruması |
| Zimmetlerim | Ana sayfa › Zimmetlerim | Çalışan kapsamı korunur |
| Bilgi bankası listesi | Ana sayfa › Bilgi bankası | Ana sayfaya erişim |
| Yeni makale | Ana sayfa › Bilgi bankası › Yeni makale | Listeye dönüş |
| Makale detay | Ana sayfa › Bilgi bankası › Makale başlığı | Makale listesi; gerekiyorsa filtreli kategoriye dönüş |
| Makale düzenleme | Ana sayfa › Bilgi bankası › Makale başlığı › Düzenle | Makale başlığına tıklayınca okuma sayfası |
| Profil | Ana sayfa › Profilim | Ana sayfaya erişim |
| Normal şifre değişimi | Ana sayfa › Profilim › Şifre değiştir | Profile dönüş; zorunlu değişimde bu kaçış açılmaz |
| Rapor türü | Ana sayfa › Raporlar › Arızalar / Talepler / … | Rapor ana hedefi gerçek sayfa/yönlendirme olmalı |
| Rapor metrik listesi | Ana sayfa › Raporlar › Rapor türü › Metrik adı | Seçili filtreleri taşıyan rapor bağlantısı |
| Dışa aktarımlar | Ana sayfa › Raporlar › Dışa aktarımlarım | Raporlara dönüş |
| Admin giriş | Yönetim › Genel bakış | Yönetim genel bakışı eklendiğinde gerçek hedef |
| Kullanıcılar/departmanlar/lokasyonlar/ekipler | Yönetim › Organizasyon › Sayfa adı | Organizasyon bir menü grubudur; hedef sayfası yoksa link değildir |
| Markalar/modeller | Yönetim › Cihaz kataloğu › Sayfa adı | Yeni katalog ekranları aynı düzene uyar |
| Kategoriler/talep tipleri | Yönetim › Hizmet kataloğu › Sayfa adı | Grup hedefi yoksa düz metin; Yönetim tıklanabilir |
| Takvim/politika | Yönetim › SLA › Takvim / Politika | Geçerli üst hedef veya düz grup etiketi |
| E-posta şablonları/gönderimleri | Yönetim › Bildirimler › Sayfa adı | Grup ve sayfa hiyerarşisi görünür |
| Sistem/denetim | Yönetim › Sistem ve denetim › Sayfa adı | Yönetim köküne erişim |

**Kural:** Menü grubu için hayalî/404 URL üretilmez. Tıklanabilir üst menü isteniyorsa o grubun özet sayfası oluşturulur; aksi durumda grup etiketi düz metin kalır ve gerçek üst sayfa bağlantısı sunulur. Admin panelinden operasyon uygulamasına geçiş breadcrumb’a karıştırılmaz; header’da ayrı “Uygulamaya dön” eylemi bulunur.

#### 4.4.2 Teknik ve mobil davranış

- Route metadata tek kaynak olmalı: yol, Türkçe başlık anahtarı, üst ilişki, gerekli rol ve gerekiyorsa dinamik etiket çözümleyicisi. Breadcrumb, sidebar ve sayfa başlığı birbirinden bağımsız isim sözlükleri kullanmamalı.
- URL’yi yalnız `/` ile parçalayarak kullanıcıya `admin`, UUID veya ham sorgu değerlerini göstermek yeterli değildir. Detayda gerçek ticket numarası/demirbaş/makale başlığı kullanılır.
- Kayıt yüklenirken nötr yer tutucu; erişim reddinde gizli kayıt başlığı gösterilmez. Başlık verisi sırf breadcrumb için yetkisiz ayrı sorguyla çekilmez.
- Kullanıcı bir detay URL’sini doğrudan açarsa mantıklı varsayılan üst liste bulunur. Uygulama geçmişi varmış gibi `navigate(-1)` zorunlu tutulmaz.
- Geldiği sayfa için yalnız izin verilen uygulama içi hedefler kabul edilir. Haricî URL veya kontrolsüz `returnTo` ile açık yönlendirme yaratılmaz.
- Sayfa yenilemede filtreli dönüşün korunması isteniyorsa whitelist edilmiş liste parametreleri URL veya kontrollü session state ile taşınır. Şifre/token gibi bilgiler dönüş yoluna eklenmez.
- Mobilde kök + gerekirse erişilebilir “…” + yakın üst öğe + mevcut sayfa. Atlanan öğelere menüyle ulaşılır. “…” dekoratif veya işlevsiz olmamalı.
- Uzun makale başlığı kırpılabilir; tam başlık sayfanın H1’inde ve erişilebilir adında korunur. Zincir tüm ekranı yatay genişletmez.
- Breadcrumb ve sayfa başlığı header altında; açılır menü/klavye odağı tarafından örtülmez. Yönetim menüsünde aktif alt öğenin üst grubu otomatik açık olur.
- İçeriğe atla bağlantısı breadcrumb/sidebar tekrarlarını atlayabilmeli. Sayfa değişiminde ana başlığa mantıklı odak yönetimi uygulanmalı.

#### 4.4.3 Kabul testleri

- [ ] Makale düzenle → makale başlığı → makale detay → Bilgi bankası adımları tek tek çalışır.
- [ ] Filtreli kuyruk → ticket → kuyruğa dönüş; filtre, sayfa ve bağlam korunur.
- [ ] Filtreli rapor → metrik listesi → rapor; seçili tarih ve kapsam değişmez.
- [ ] Doğrudan cihaz detay URL’si → Envanter bağlantısı güvenilir çalışır.
- [ ] Admin alt sayfa → Yönetim bağlantısı çalışır; erişilemeyen grup sayfası için link üretilmez.
- [ ] 320/390 px, uzun başlık, klavye ile gezinme, sayfa yenileme ve yeni sekmede açma denenir.
- [ ] Breadcrumb, etkin sidebar ve sayfa başlığı aynı modülü/etiketi gösterir.

### 4.5 CoreUI dönüşüm kapsamı — yalnız admin değil, tüm ürün

Seçilen taban: [CoreUI Free React Admin Template](https://github.com/coreui/coreui-free-react-admin-template). Template’in layout yaklaşımı referans alınacak; örnek demo uygulaması TGYSM’nin yerine kopyalanmayacak.

**Kapsam:** Giriş/şifre ekranları; çalışan portalı; IT çalışma alanı; kayıt formları/detayları; envanter/zimmet/CSV; bilgi bankası/editör; raporlar/export; bütün admin ekranları; modal/offcanvas; 403/404; boş/yükleme/hata/başarı durumları.

**Görsel kabul:** Eski Tailwind kartları ve menüleri aynı bırakılıp yalnız birkaç CoreUI düğmesi eklenmiş olması tamamlanmış dönüşüm sayılmaz. Header, sidebar, breadcrumb, içerik genişliği, tipografi, form, tablo, kart, rozet, modal, bildirim ve ikonlar tek CoreUI tasarım diline uymalı.

| Alan | CoreUI hedefi | TGYSM’ye uyarlama |
|---|---|---|
| Genel iskelet | Sidebar + header + içerik düzeni | Operasyon ve admin için ayrı menü ağacı |
| Sayfa başlığı | Breadcrumb + H1 + ana eylem | Türkçe route metadata ve kayıt bağlamı |
| Form | CoreUI input/select/textarea/feedback | Mevcut react-hook-form + zod doğrulaması |
| Liste | CoreUI tablo ve mobil kart/list yaklaşımı | Mevcut server pagination/arama/rol kapsamı |
| İşlem penceresi | CoreUI modal/offcanvas | Mobil boyut, odak dönüşü, değişiklik koruması |
| Durumlar | CoreUI badge/alert/toast/spinner | TGYSM iş durumu ve mevcut hata kodları |
| Grafik | CoreUI Chart.js wrapper | Gerçek API agregasyonu ve erişilebilir tablo |
| İkonlar | CoreUI ikon ailesi | Anlamlı ikon + gerekli metin/erişilebilir ad |
| Makale | CoreUI sayfa/toolbar/yüzey düzeni | Güvenli Markdown renderer korunur |

Tablodaki bileşenler hedef aileleridir; seçilen sürümün API’si doğrulanmadan prop isimleri tahmin edilmemeli. Template’teki reklam, PRO tanıtımı, örnek profil/bildirimler, satış grafikleri ve sahte sayaçlar ürüne taşınmamalı. Tema seçimi eklenirse dark/light durumlarında grafik, rozet ve form kontrastları birlikte doğrulanmalı; yarım dark-mode teslim edilmemeli.

### 4.6 Ücretsiz kapsam, bağımlılıklar ve geçiş kuralları

Resmî template manifestinde `@coreui/react`, CoreUI stil/ikon paketleri, `@coreui/react-chartjs` ve `chart.js` bulunuyor. Manifest ayrıca örnek Redux kullanımı ve TGYSM’den farklı router paket/sürüm seçimi içeriyor. Bunları birebir kopyalamak yerine mevcut React/TypeScript/Vite ve router ile uyumluluk doğrulanmalı. [Template package.json](https://raw.githubusercontent.com/coreui/coreui-free-react-admin-template/main/package.json)

- Kullanıcı **Free** template seçti. CoreUI PRO veya ücretli Data Grid/Scheduler gerektiren çözüm planın varsayılanı değildir. Resmî depo ücretsiz ve PRO/add-on ayrımını gösteriyor. [Template bileşen listesi](https://github.com/coreui/coreui-free-react-admin-template)
- Aramalı seçim, gelişmiş tarih aralığı veya akıllı tablo için ücretli bileşen varmış gibi davranma. Ücretsiz form kontrolleri ve mevcut uygulama mantığıyla çöz; özel aramalı seçicide erişilebilirlik testini ekle.
- Template’in `.jsx` örnekleri, TGYSM’nin strict TypeScript standardını kaldırmak için gerekçe değildir. Yeni uygulama bileşenleri tipli olmalı.
- TanStack Query, mevcut API client, i18next, react-hook-form/zod, cookie/CSRF ve backend rol kontrolleri korunmalı. Template’te var diye Redux veya ikinci router zorunlu olarak eklenmemeli.
- CoreUI stilleri ile Tailwind preflight/global sınıfları çakışabilir. Geçişte CSS yükleme sırası ve kapsamı açıkça yönetilmeli. Nihai hedef tek tutarlı CoreUI stil sistemi; kullanımı biten Tailwind/Radix sunum bağımlılıkları taranıp kaldırılmalı. İşlevsel kullanım kalıyorsa kaldırmadan önce alternatif doğrulanmalı.
- Kullanılan CoreUI sürüm/commit ve bağımlılık kilidi kayıt altına alınmalı. Belgedeki web incelemesi kurulum uyumluluğunun yerine geçmez; uygulama aşamasında install/build doğrulanmalı.
- Template kodunun MIT lisans ve telif bildirimleri projede korunmalı. [Resmî LICENSE](https://raw.githubusercontent.com/coreui/coreui-free-react-admin-template/main/LICENSE)
- Yeni UI bileşenleri için adaptörler ortak `shared/ui` katmanında tutulabilir; sayfa iş mantığı template demo klasör yapısına göre gereksiz yere yeniden yazılmamalı.

## 5. Sayfa sayfa inceleme

### 5.1 Giriş — `/login`

**Kanıt:** Önceki turda canlı; `features/auth/LoginPage.tsx`.

- **P2:** Şifreyi göster/gizle kontrolü yok. Klavyeyle erişilebilir kontrol ekle; varsayılan gizli kalsın.
- **P3:** Ürün kimliği yalnız genel destek cümlesiyle aktarılıyor. Küçük ürün adı ve kurum bağlamı eklenebilir; büyük tanıtım alanına gerek yok.
- **Öneri:** E-posta alanının otomatik büyük harf ve yazım düzeltme davranışını mobilde denetle. Mevcut autocomplete korunmalı.
- **Kabul:** Hata halinde e-posta kaybolmaz, kullanıcı ilk hatalı alana yönlendirilir; hata mesajı hesabın varlığını ifşa etmez.

### 5.2 Şifremi unuttum — `/forgot-password`

**Kanıt:** Canlı ilk durum; gönderim yapılmadı.

- Açıklama ve girişe dönüş bağlantısı anlaşılır. Hesap var/yok bilgisini açığa çıkarmayan dil korunmalı.
- **P2 öneri:** Başarılı talep sonrası aynı form yerine “Bağlantı talebiniz alındı” durumu, spam klasörü bilgisi ve kontrollü yeniden deneme yönlendirmesi sun.
- **Kabul:** Başarı mesajı, e-posta gerçekten teslim edildiğini kesinleştirmemeli; servis yalnız talebi kabul etmiş olabilir.

### 5.3 Şifre belirle — `/set-password`

**Kanıt:** Canlı, token bulunmayan durum.

- **P2:** Geçersiz bağlantı uyarısı varken şifre alanları hâlâ gösteriliyor; yalnız kayıt düğmesi pasif. Kullanıcı gereksiz yere şifre yazabilir.
- **Öneri:** Token eksikliği gibi kesin geçersiz durumda form yerine tek kurtarma kartı göster. Süre dolmasını sunucu doğruladığında aynı davranışı uygula.
- **Kabul:** Geçerli bağlantı formu ile geçersiz bağlantı kurtarma ekranı ayrı; geçerli bağlantı bu incelemede test edilmedi.

### 5.4 Şifre değiştir — `/change-password`

**Kanıt:** Canlı normal hesap görünümü; değişiklik yapılmadı.

- **P2:** Normal kullanıcı için görünen kaçış yolu “Çıkış yap”; profile dönme eylemi yok.
- **Öneri:** İsteğe bağlı değişimde “Vazgeç / Profile dön”; zorunlu ilk değişimde mevcut zorunluluk korunmalı.
- Yeni şifre kuralları yazarken anlaşılır biçimde gösterilmeli; gizle/göster ve eşleşme geri bildirimi eklenmeli.
- **Kabul:** Zorunlu değişim akışı geri bağlantısıyla atlatılamaz; normal akış kullanıcıyı uygulamadan çıkmaya zorlamaz.

### 5.5 Ana ekran — `/`

**Kanıt:** Canlı mobil/masaüstü; `HomePage.tsx`.

- **P2:** Yönetici için 10 benzer kart; profil, envanter, arıza açma ve yönetim aynı ağırlıkta.
- **Öneri:** İki ana destek eylemi, son güncellenen kayıtlar ve gerektiğinde dikkat isteyen bildirimler. IT kullanıcısında gerçek kuyruk kayıtları öne alınmalı.
- Yönetim kartı ve profil kartı kaldırılıp uygun menülere taşınmalı. “Raporlar” IT menüsünden erişilebilir kalmalı.
- **Kabul:** Mobil ilk ekranda kullanıcının ana işi görünür; işlem bulmak için tüm özellik kataloğu taranmaz.

### 5.6 Profil — `/profile`

**Kanıt:** Canlı.

- **P3:** Bilgiler okunaklı; ancak her değer için geniş dikey blok kullanılıyor.
- **Öneri:** Hesap bilgileri ve organizasyon bilgilerini iki mantıklı gruba ayır; normal metin boyutunu koruyarak boşluğu azalt.
- Departman/lokasyon eksikse “Düzeltme için yöneticinize başvurun” gibi net yönlendirme sun; kullanıcının değiştiremeyeceği bilgide düzenleme izlenimi yaratma.
- **Kabul:** “Belirtilmemiş” ve yetki nedeniyle görünmeyen bilgi aynı şey olarak sunulmaz.

### 5.7 Kayıtlarım — `/tickets`

**Kanıt:** Canlı, iki kayıt.

- **P2:** Mobil kartta numara, konu, durum ve büyük “Aç” düğmesi var; güncellik bilgisi görünmüyor.
- **Öneri:** Başlığı ana bağlantı yap; kayıt numarasını ikincil göster. Son güncelleme, bekleme nedeni ve “Sizden yanıt bekleniyor” gibi mevcut veriye dayanan bilgi ekle.
- “Açık / Benden yanıt bekleyen / Tamamlanan” hızlı filtreleri düşünülebilir; gerçek sorgu desteği gerekir.
- **Kabul:** Detaydan geri dönünce arama/durum/sayfa korunur; okunmamış göstergesi gerçek okunma verisi olmadan eklenmez.

### 5.8 Yeni arıza — `/tickets/new/incident`

**Kanıt:** Canlı + `NewTicketPage.tsx`.

- **P2:** Konu/açıklama zorunlu, kategori/konum isteğe bağlı; görünüm bu ayrımı anlatmıyor.
- **P2:** Gereksinimdeki zimmetli cihaz seçimi mevcut ilk oluşturma formunda yok.
- **Öneri:** “Hangi cihazla ilgili?” aramalı seçim, “Cihazla ilgili değil” seçeneği; kullanıcının konumunu önermek ama değiştirmesine izin vermek.
- Konu için kısa örnek, açıklamada “Ne oldu, ne zamandır oluyor?” yardımı. Kategori seçimi kullanıcıyı teknik sınıflandırma bilmeye zorlamamalı.
- Native dosya kontrolünde bu tarayıcıda İngilizce “Choose Files / No file chosen” görüldü. Erişilebilir Türkçe dosya seçici kabuğu ve seçilen dosya listesi önerilir; bu tarayıcı yerelleştirmesi farkıdır, çeviri dosyası hatası diye sunulmamalı.
- **Kabul:** Dosya yükleme başarısız olsa da oluşturulan kayıt ikinci kez oluşturulmaz. Kodda var olan bu koruma sürdürülmeli.

### 5.9 Yeni talep — `/tickets/new/request`

**Kanıt:** Canlı + ortak form kodu.

- **P2:** Talep tipi ile kategori arasındaki ilişki açıklanmıyor; birbirini tekrar eden seçim hissi var.
- **Öneri:** Talep tipi kategori varsayılanını belirleyebilir; ilgili alanlar bağlama göre açılır. Örneğin cihaz talebinde cihaz türü, erişim talebinde hizmet seçimi; bunlar yeni veri sözleşmesi gerektirir.
- Onay gerektiren talepte onay akışını, biliniyorsa onaylayıcıyı, atanmamışsa yönlendirme durumunu göster.
- **Kabul:** Eksik departman/onaylayıcı yüzünden talep sessizce belirsiz bir beklemeye düşmez; kullanıcıya ve yetkili yöneticiye durum açıkça iletilir.

### 5.10 Arıza detayı — `/tickets/:number` (INC)

**Kanıt:** Canlı mevcut arıza; `TicketDetailPage.tsx` ve işlem diyalogları.

- **P2:** Uzun metadata bloğu açıklamadan ve işlemlerden önce geliyor. Bağlı cihaz/makale gibi ikincil bölümler yazışmaları aşağı itiyor.
- **Öneri:** Üstte konu, durum ve anlamlı SLA özeti; hemen altında açıklama/yazışma. Masaüstünde sağ bilgi paneli, mobilde açılır detaylar.
- “Yanıt” ve “İç not” seçimi renk, ikon ve açık hedef metniyle ayrılmalı: “Talep sahibi görebilir” / “Yalnız IT ekibi”.
- “Tamamla” çözüm açıklamasıyla bağlanmalı; iptal ayrı ikincil menüde kalmalı. Mevcut durum makinesi ve backend izinleri değişmemeli.
- **Kabul:** Kullanıcı açıklama ve yanıt alanına ikincil ilişki bloklarını geçmeden ulaşır; iç notun görünürlüğü gönderim anında anlaşılır.

### 5.11 Talep detayı — `/tickets/:number` (REQ)

**Kanıt:** Canlı, onaylayıcısı atanmamış bekleyen talep.

- **P1/P2:** “Yönetici onayı bekleniyor” ve “Onaylayıcı atanmadı” birlikte gösteriliyor. Eksik atama, normal bekleme gibi algılanmamalı.
- **Öneri:** “Onaylayıcı atanması gerekiyor” uyarısı ve admin için ilgili atama aksiyonu; çalışan için açıklayıcı durum metni.
- Onay geçmişi, ret gerekçesi ve sonraki adım tek süreç özetinde toplanmalı.
- **Kabul:** Atama bekleme ile atanmış kişinin kararını bekleme ayrı görünür durumlar; yetkisi olmayan kullanıcıya onay eylemi gösterilmez.

### 5.12 İş listem — `/workspace`

**Kanıt:** Canlı.

- **P2:** Beş büyük sayaç kartı mobil ekranın çoğunu kaplıyor; gerçek işleri görmek için tekrar tıklamak gerekiyor.
- **Öneri:** Sayaçları kompakt sekme/filtrelere çevir; varsayılan listeyi aynı sayfada göster. “SLA riski” varsa öne çıkan metin ve ikon kullan.
- **Kabul:** IT kullanıcısı ilk açılışta en azından ilk işin konusunu ve durumunu görebilir.

### 5.13 Kuyruk — `/workspace/queue`

**Kanıt:** Canlı “Bana atanan”; diğer kuyruklar aynı bileşen ve kaynak kodu üzerinden değerlendirildi, her kuyruk veri durumu ayrı test edilmedi.

- **P2:** Arama, durum ve öncelik kontrolleri ilk kayda ulaşmayı geciktiriyor; mobil kart eylemi genel “Aç”.
- **Öneri:** Arama sürekli görünür; diğer filtreler sayı rozetiyle panelde. Kayıtta konu, SLA, durum ve atanan kişi önem sırasıyla gösterilsin.
- Masaüstünde kolon seçimi/sıralama daha sonra eklenebilir. Toplu işlem ilk etapta zorunlu değil; farklı yetki ve durumdaki kayıtları dikkatsizce birleştirmemeli.
- **Kabul:** Filtre değişince sayfa 1’e döner, geri dönüşte filtreler korunur; “Atanmamış” ayrı ve açık bir değer olur.

### 5.14 Onaylar — `/approvals`

**Kanıt:** Canlı liste ve onaylayıcı atama penceresi; mevcut route admin ile sınırlı.

- **P1:** Atama penceresinin “Onayla” düğmesi “Onaylayıcıyı ata” olmalı. Talebin onaylandığı izlenimi oluşmamalı.
- **P2:** Liste, atama bekleyen ve karar bekleyen talepleri ayırmalı; bekleme süresi ve talep özeti öne alınmalı.
- **Öneri:** Bu ekranın adını mevcut davranışa göre “Onay yönetimi” yapmak; kişisel “Onayımı bekleyenler” ayrı kapsamdır.
- **Doğrulama gerekli:** Departman yöneticisinin admin dışı rol alması hedefleniyorsa rol matrisi ayrıca kararlaştırılmalı. Mevcut kısıt, yalnızca tasarım tercihinden hareketle kaldırılmamalı.

### 5.15 Envanter — `/assets`

**Kanıt:** Canlı tek cihaz; yeni cihaz formu da incelendi.

- **P1:** Otomatik numara ve katalog dönüşümü bu ekranın temel iyileştirmesidir.
- **P2:** Marka, model, lokasyon ve zimmetli kişi filtreleriyle arama kapsamı güçlendirilmeli. Seri/demirbaş araması korunmalı.
- Kartta cihazın marka/modeli öne, demirbaş numarası kopyalanabilir ikincil kimlik olarak konumlanmalı. Durum etiketi renge bağımlı olmamalı.
- **Kabul:** Mobil karttan cihazı tanımak için detay açmak gerekmez; yeni cihaz ve içe aktar eylemleri birbirinden açıkça ayrılır.

### 5.16 Cihaz oluştur/düzenle — diyalog

**Kanıt:** Canlı oluşturma, kodda ortak düzenleme formu.

- **P1:** Tür → marka → model bağımlı seçimleri. Model değişimi seri/konum gibi bağımsız alanları silmemeli.
- **P2:** Uzun formu “Kimlik”, “Yerleşim”, “Satın alma/garanti” gruplarına ayır. Telefon için tam ekran form düşünülebilir; kısa düzenlemede diyalog korunabilir.
- “Notlar” tek satırlı metin alanı yerine çok satırlı olmalı. Zorunlu alanlar görünür belirtilmeli.
- Katalog yüklenmesi/boşluğu/hatası ayrı durumlar olmalı; ağ hatası “Hiç model yok” diye sunulmamalı.
- **Kabul:** Numara alanı oluştururken düzenlenemez; geçmiş cihaz düzenlenirken mevcut pasif katalog değeri görünür, yeni seçimlerde pasif değer önerilmez.

### 5.17 Cihaz detayı — `/assets/:id`

**Kanıt:** Canlı ve `AssetDetailPage.tsx`.

- **P2:** Marka + model “Marka” etiketi altında birleştirilmiş. Etiket düzeltilmeli veya alanlar ayrılmalı.
- Teslim/bakım eylemleri metadata sonrasında geliyor. Üstte cihaz özeti ve duruma uygun ana işlem yer almalı.
- Garanti bitişini salt tarih yerine “Süresi doldu / … gün kaldı / Tarih girilmemiş” olarak destekle; tarih de erişilebilir olsun.
- **Öneri:** Zimmet, hareket geçmişi ve ilişkili destek kayıtları sekmeler/açılır bölümlerle ayrılmalı. İlişkili destek kayıtlarını cihazdan listeleme yeni sorgu gerektirebilir.
- **Kabul:** Seri ve demirbaş kopyalama, kronolojik hareketler, mevcut zimmet ve kritik durum tek bakışta anlaşılır.

### 5.18 Cihaz teslim/iade/bakım işlemleri — diyaloglar

**Kanıt:** Teslim diyalogu canlı; diğer durumlar ortak `AssetOperationDialog.tsx` kapsamındadır, tamamlanmadı.

- **P2:** Genel “Onayla” yerine “Teslim et”, “İadeyi kaydet”, “Bakıma al” gibi gerçek işlem adı.
- Kullanıcı araması ve seçim listesinin iki ayrı kontrol olması büyük veri için anlaşılır biçimde bir aramalı seçicide birleştirilebilir.
- Kritik işlemden önce cihaz, hedef kişi, mevcut durum ve yeni durum kısa özetle görünmeli.
- **Kabul:** Zimmet değişikliği başarı mesajında kişi ve cihazla doğrulanır; hata halinde form değerleri korunur.

### 5.19 Zimmetlerim — `/assets/mine`

**Kanıt:** Canlı boş durum; dolu liste bu oturumda görülmedi.

- **P3:** Boş durum yalnız “Üzerinize kayıtlı cihaz yok” diyor. Cihaz talebi veya destek kaydı bağlantısı bağlama göre eklenebilir.
- Dolu durumda cihaz türü, marka/model, demirbaş ve teslim tarihi gösterilmeli; “Bu cihazla ilgili arıza bildir” kısa yolu önerilir.
- **Kabul:** Boş durum eksik yükleme ile karışmaz; kullanıcı kendi zimmet kapsamı dışına çıkmaz.

### 5.20 CSV içe aktar — `/assets/import`

**Kanıt:** Canlı başlangıç ekranı; dosya yükleme ve commit yapılmadı.

- **P2:** Kullanıcıya `desktop`, `in_stock` gibi teknik değerler ve uzun format paragrafı gösteriliyor.
- **Öneri:** Yükle → sütunları/eşlemeleri kontrol et → hataları düzelt → içe aktar sonucu. Her adımda satır sayısı ve ilerleme durumu.
- Türkçe örnek şablon; tür/durum için kontrollü eşleme; marka/model için katalog karşılığı. Teknik kodlar gerekiyorsa açıklama tablosunda gösterilsin.
- Hatalı satır dosyası indirilebilir olsun. Aynı dosyanın ikinci aktarımı ve ağ tekrarları mükerrer cihaz üretmemeli.
- **Kabul:** Otomatik numara CSV için de geçerli; eşleşmeyen marka sessizce yeni katalog kaydı oluşturmaz. Geçerli/hatalı satırların birlikte işlenme kuralı önceden açıkça yazılır.

### 5.21 Bilgi bankası — `/kb`

**Kanıt:** İlk turda boş liste; son turda kullanıcının eklediği yayınlı örnek makaleyle dolu liste.

- **P2:** İlk boş durumda arama yapılmadan “Aramanızla eşleşen makale bulunamadı” deniyordu. “Henüz makale yok” ve “Sonuç bulunamadı” ayrılmalı.
- “Bilgi Bankası” menüsü ile “Nasıl Yapılır?” başlığı aynı adlandırmaya getirilmeli.
- **Öneri:** Çalışana arama + konu kategorileri; editöre ayrı yönetim filtreleri. Popüler içerik ancak kullanım verisi varsa gösterilmeli.
- **Kabul:** İlk kullanımda yetkili kişiye makale oluşturma, sonuçsuz aramada filtre temizleme veya destek açma önerilir.

### 5.22 Yeni makale — `/kb/new`

**Kanıt:** Canlı + editör kodu.

- **P2:** Kaydet sayfanın altında; geri/vazgeç eylemi yok. İçerik uzun olduğunda kullanıcının çıkış ve kayıt durumu belirsizleşiyor.
- **Öneri:** “Taslak olarak kaydet”, geri dönüş ve kaydedilmemiş değişiklik uyarısı. Basit başlık/liste/link araç çubuğu Markdown bilen kullanıcı zorunluluğunu azaltır.
- Önizleme korunmalı. Ham HTML açılmamalı; mevcut güvenli Markdown yaklaşımı sürdürülmeli.
- **Kabul:** Kaydet ile yayımlama ayrımı nettir; görünürlük seçiminin kimleri etkilediği anlaşılır.

### 5.23 Makale detayı — `/kb/:id`

**Kanıt:** Son turda örnek yayınlı makale canlı incelendi; önceki yalnız-kod sınırlaması bu ekranın mevcut kısa içerik durumu için kalktı.

- **P2 — canlı:** Yalnız “Makalelere dön” bağlantısı var; uygulama/bilgi bankası/kategori/makale hiyerarşisi görünmüyor. CoreUI breadcrumb zorunlu.
- **P2 — canlı:** Başlık altındaki yazar/yayın/güncelleme satırı mobilde yoğun. Tarihler kısa metadata düzenine taşınmalı; ayrıntı açılabilir olmalı.
- **P2 — canlı:** Düzenle/Arşivle düğmeleri okunan metnin önünde. Okuma alanı ile yönetim araçları ayrılmalı; arşivle ikincil işlem menüsünde olmalı.
- Örnek başlık, özet ve içerik aynı metinle doldurulmuş. Ekrandaki tekrarın bu örnekte içerik kaynaklı olduğu doğrulandı; uygulamanın aynı alanı yanlış çoğalttığı iddia edilmiyor. Alan amaçları editörde kısa yardımla açıklanmalı.
- **CoreUI hedefi:** Breadcrumb + başlık/rozet satırı + daraltılmış metadata + okunabilir içerik; sürüm geçmişi ayrı açılır panel/sekme. Mevcut görünürlük ve yayın durumu korunmalı.
- **Öneri:** Uzun makalede içerik dizini, kontrollü okuma genişliği, kod bloğu/tablo taşma davranışı. Düzenleme/arşivleme kullanıcı içeriğinin önüne geçmemeli.
- “Bu bilgi yardımcı oldu mu?” geri bildirimi ek kapsamdır; ölçüm amacı netleşmeden zorunlu değildir.
- **Kabul:** Çalışan yalnız yayınlanmış ve kendisine görünür içeriğe erişir; uzun başlık/bağlantı mobilde taşmaz. Uzun/zengin içerik ve çalışan rolüyle ayrıca doğrulama gerekir.

### 5.24 Makale düzenleme ve sürümler — `/kb/:id/edit`

**Kanıt:** Son turda örnek makalenin dolu düzenleme formu canlı açıldı; değişiklik kaydedilmedi. Sürüm çakışması değerlendirmesi kod kaynaklıdır.

- **P2 — canlı:** Düzenleme ekranında makale/listesine doğrudan dönüş ve breadcrumb yok; tek kayıt eylemi sayfanın altındaki “Kaydet”. CoreUI başlık alanına “Makaleye dön”, kaydet ve değişiklik durumu eklenmeli.
- **P2:** Sürüm çakışmasında yeniden yükleme mevcut çalışma kopyasını değiştiriyor. Önce kullanıcının metnini koruyan seçenek sunulmalı.
- **Öneri:** Kaydedilmemiş değişiklik uyarısı, son kayıt bilgisi, fark görüntüleme; yayınlı makale düzenlemesinin etkisi açıkça belirtilmeli.
- **Kabul:** Başkasının değişikliği sessizce ezilmez; “Yeniden yükle” öncesinde mevcut metni kaybetme sonucu açıklanır.

### 5.25 Admin giriş ve yerleşim — `/admin`

**Kanıt:** Canlı + `AdminLayout.tsx`, router.

- **P2:** Giriş doğrudan kullanıcılar sayfasına yönleniyor; kurulum eksikliklerini özetleyen yönetim başlangıcı yok.
- Menü 12 tanımı düz listeliyor; organizasyon/katalog/SLA/sistem grupları oluşturulmalı.
- Admin hesap/çıkış kontrolü görünür bir yere eklenmeli. Normal uygulamanın uzun üst menüsü admin içine taşınmamalı.
- **Kabul:** Admin ve günlük çalışma arasında tek anlaşılır geçiş; yetkisiz doğrudan URL davranışı tutarlı.

### 5.26 Kullanıcılar — `/admin/users`

**Kanıt:** Canlı liste ve yeni kullanıcı diyalogu.

- **P2:** Mobil kartta “Yönetici · Yok” ifadesindeki “Yok” hangi bilgiyi anlattığını söylemiyor. “Departman belirtilmemiş” gibi açık metin kullanılmalı.
- Form, hesap bilgisi / organizasyon / erişim ve davet olarak gruplansın. Rol seçeneklerinde kısa yetki açıklaması gösterilsin.
- Pasifleştirme ve şifre sıfırlama ikincil aksiyon menüsünde; işlem sonucu ve etkisi açık olmalı.
- **Kabul:** Uzun ad/e-posta taşmaz; e-posta ile bağlantı ve geçici şifre yolları karışmaz; mevcut son admin koruması korunur.

### 5.27 Departmanlar — `/admin/departments`

**Kanıt:** Canlı liste ve form.

- **P2:** Kartta yönetici adı etiketsiz gösteriliyor. “Onaylayıcı: …” yazılmalı.
- **Öneri:** Onaylayıcısı olmayan departman belirgin uyarı; aktif/pasif filtresi; aynı ad için normalizasyon ve sunucu kısıtı.
- **Kabul:** Onaylayıcısız departmanın talep sürecine etkisi görünür; arama ve pasif kayıtlar tutarlı işlenir.

### 5.28 Lokasyonlar — `/admin/locations`

**Kanıt:** Canlı ağaç ve oluşturma formu.

- **P2:** Her düğüm üç aksiyon taşıyor; birkaç lokasyonda bile mobilde uzun kart zinciri oluşuyor.
- **Öneri:** Daraltılabilir ağaç, lokasyon arama, tam yol ve ikincil işlem menüsü. “Alt lokasyon ekle” seçildiğinde üst lokasyon hazır gelsin.
- Derin hiyerarşide girinti sınırlanmalı; breadcrumb/full path ile bağlam korunmalı.
- **Kabul:** Uzun/deep path veriyle 320 px test; döngü kuralları ve pasif üst lokasyon etkisi korunur.

### 5.29 Ekipler ve üyeler — `/admin/teams`

**Kanıt:** Canlı liste, yeni ekip ve üyeler diyalogu; ek olarak kod.

- **P2 — kod hatası:** `TeamsPage.tsx` masaüstü durum hücresinde `isDefaultIntake` rozeti iki kez render ediliyor. Mobil `renderCard` içinde aynı rozet hiç yok. Koşullu durum mevcut veride canlı görülmedi; kod bulgusudur.
- **Öneri:** Tek karşılama ekibi rozeti, aktif/pasif filtresi ve boş ekip uyarısı. Karşılama ekibi seçimi yeni ekip akışında da açıklanmalı; şu an düzenleme tarafında.
- Üye ekleme listesini aramalı yap; çıkarma işleminin ekibe atanmış işlere etkisini açıkla.
- **Kabul:** Karşılama ekibi tüm ekran boyutlarında bir kez görünür; üyelik sayısı ve liste işlemden sonra tutarlıdır.

### 5.30 Kategoriler — `/admin/categories`

**Kanıt:** Canlı liste ve form.

- **P2:** Liste arama/kapsam filtresi sunmuyor; büyüdüğünde tarama zorlaşır.
- **Öneri:** Üst/alt kategori görünümü, Arıza/Talep/Her ikisi filtresi, aktiflik filtresi. Kullanıcıya görünen anlaşılır adlar için örnek ve yazım standardı.
- Mevcut örneklerdeki `KAT-1` gibi isimler yazılım hatası değildir; yönetilen içeriğin kalitesini gösterir.
- **Kabul:** İki seviyeli hiyerarşi sınırı formda anlaşılır; pasifleştirme geçmiş kayıt etiketlerini bozmaz.

### 5.31 Talep tipleri — `/admin/request-types`

**Kanıt:** Canlı liste ve form.

- **P2:** Ad ve onay gereksinimi temel ihtiyacı karşılıyor; kullanıcıya açıklama ve kategori ilişkisi yok.
- **Öneri:** Kullanıcı dostu açıklama, varsayılan kategori ve kontrollü görünürlük gelecekte eklenebilir. Bu, genel workflow motoru kurulmasını gerektirmemeli.
- **Kabul:** Talep formunda tip seçilince onay gereksinimi açıkça anlaşılır; pasif tip eski kayıtları okunamaz yapmaz.

### 5.32 E-posta şablonları — `/admin/email-templates`

**Kanıt:** Canlı liste ve düzenleme diyalogu.

- **P2:** Değişkenler elle `{{...}}` yazılıyor; yazım hatasına açık. İzinli değişken listesi mevcut ama seçimle ekleme deneyimi geliştirilmeli.
- **Öneri:** Değişkeni imlece ekleyen seçim listesi ve örnek verilerle önizleme. Önizleme e-posta göndermemeli.
- “Kamuya açık yanıt” yerine “Kullanıcıya açık yanıt” tercih edilmeli; internet yayını izlenimi giderilmeli.
- **Kabul:** Bilinmeyen değişken kaydedilemez; önizleme gerçek kişi/şifre bağlantısı üretmez; test gönderimi varsa ayrı açık eylemdir.

### 5.33 E-posta gönderimleri — `/admin/email-deliveries`

**Kanıt:** Canlı boş liste; dolu/yeniden deneme durumu test edilmedi.

- **P2:** “Kayıt bulunamadı” ilk kullanım, filtre sonucu ve işletim sorunu arasında ayrım yapmıyor.
- **Öneri:** Tarih ve durum filtresi, toplam/başarısız sayısı, okunabilir hata özeti ve son deneme zamanı. Teknik ayrıntı açılır bölümde.
- **Kabul:** Yeniden gönderme gerçek yan etki olarak açık adlandırılır; kullanıcı tıkladıktan sonra bekliyor/gönderiliyor durumu görünür; sırlar ekrana taşınmaz.

### 5.34 SLA takvimi — `/admin/sla/calendar`

**Kanıt:** Canlı.

- **P2:** Saat dilimi serbest metin; kullanıcı IANA adını doğru yazmak zorunda.
- **Öneri:** Doğrulanmış saat dilimlerinden aramalı seçim; varsayılan Europe/Istanbul. Bu sözlük cihaz markası gibi kullanıcı tarafından rastgele genişletilmemeli.
- Haftalık form mobilde çok uzun. “Hafta içi aynı saatler” ve bir günü seçili günlere kopyalama; kapalı günlerde saatleri gizleme/özetleme.
- Tatilleri yıl filtresi ve tarih sırasıyla göster. Kaydetmeden önce çalışan SLA’lara etkisini açıklayan özet sun.
- **Kabul:** Başlangıç/bitiş hatası ilgili güne bağlanır; takvim değişimi mevcut zamanlayıcıları etkiliyorsa kullanıcı bunu bilir.

### 5.35 SLA politikası — `/admin/sla/policy`

**Kanıt:** Canlı + `SlaPolicyPage.tsx`.

- **P1:** Sayısal alanlar yalnız `aria-label` taşıyor; gören kullanıcı 30 ile 75’in farklı birimlerini görünür etiketten öğrenemiyor.
- **P2:** `min-w-max` tablo mobilde çözüm sütununu yana itiyor. Belge taşmıyor ama formun yarısı ilk bakışta görünmüyor.
- **Öneri:** Arıza/Talep sekmesi, P1–P4 kartları; her kartta “İlk yanıt hedefi (iş dakikası)”, “Uyarı eşiği (%)”, “Çözüm hedefi” açık etiketler. Saat/dakika yardımcı karşılığı eklenebilir.
- Talep tipi istisnaları kodda korunuyor ama bu ekranda düzenlenmiyor. Varsa en azından salt okunur istisna özeti sunulmalı.
- **Kabul:** Hatalı hücre işaretlenir, ilk hataya odaklanılır; yeni sürümün etkisi ve mevcut sürüm görünür; koşullar mobilde yatay kaydırma gerektirmeden düzenlenir.

### 5.36 Sistem — `/admin/system`

**Kanıt:** Canlı + `ListWorkerHeartbeats.cs`, `SystemPage.tsx`.

- **P1:** Sunucu aynı işin en yeni heartbeat’ini temel alıp tüm instance satırlarına aynı stale sonucunu veriyor. Böylece günler önceki örnek “Zamanında” görünüyor. Bu, iş genelinin sağlıklı olamayacağı anlamına gelmez; satır etiketinin anlamı yanlış anlaşılıyor.
- **Öneri:** Önce iş başına tek güncel özet; altında “Örnek geçmişi”. İş sağlığı, instance güncelliği ve son çalışma hatası ayrı kavramlar olmalı.
- `Cleanup`, `ExportRunner` gibi adlara Türkçe açıklama ekle; teknik kimlik ayrıntıda kalsın.
- **Kabul:** Eski örnek aktifmiş gibi yeşil gösterilmez; “Hiç çalışmadı”, “Gecikmiş”, “Son deneme hatalı”, “Sağlıklı” durumları iş kurallarına göre ayrılır.

### 5.37 Denetim — `/admin/audit`

**Kanıt:** Canlı + `AuditPage.tsx`.

- **P2 — eksik filtre:** `ENTITY_TYPES` yalnız dört tür içeriyor; listede ticket/asset olayları da mevcut.
- **Öneri:** Tüm desteklenen türler, tarih aralığı, işlemi yapan kişi, eylem ve kayıt kimliği filtresi. API’nin mevcut filtre yetenekleriyle uyum sağla.
- Ham JSON yerine önce “Önce → Sonra” alan karşılaştırması; teknik JSON ve correlation ID ikincil ayrıntı olarak kalsın.
- **Kabul:** Listelenen bir tür filtrede de bulunur; uzun JSON mobil sayfayı genişletmez; denetim kayıtları düzenlenemez.

### 5.38 Rapor ana yolu — `/reports`

**Kanıt:** Canlı yönlendirme; varsayılan olay raporu açılıyor.

- **P2 öneri:** Daha sonra rol/kapsama göre özet sayfası eklenebilir; ilk etapta altı rapor sekmesi korunarak filtre ve KPI hiyerarşisi düzeltilmeli.
- **Güncel karar:** Raporların yerleşimi CoreUI, grafikleri CoreUI’nin Chart.js entegrasyonu ile yenilenecek. Mevcut Recharts kullanımı yalnız geçişin başlangıç durumudur; hedef bağımlılık değildir.
- **Kabul:** İlk görünümde kapsam, uygulanmış tarih aralığı ve en az bir anlamlı özet görünür; raporlar filtre ekranı gibi başlamaz.

### 5.39 Olay/arıza raporu — `/reports/incidents`

**Kanıt:** Canlı mobil/masaüstü; sınırlı veri.

- **P2:** 10 filtre, ardından eşit büyüklükte KPI kartları. Çözüm süresi/MTTR ayrımı yardım metni olmadan anlaşılmıyor.
- **Öneri:** “Arızalar” adını genel ürün diliyle eşleştir. Hacim, SLA uyumu, ilk yanıt ve çözümü öne çıkar; backlog yaş dağılımını yatay bar yap.
- “Veri yok” için neden: tamamlanmış kayıt yok, ölçüm yok veya filtre sonucu yok. Sıfırla karıştırma.
- **Kabul:** İş süresi ve takvim süresi ayrı etiketlenir; dönemlik metrikle anlık backlog aynı tarih anlamındaymış gibi sunulmaz.

### 5.40 Talep raporu — `/reports/requests`

**Kanıt:** Canlı.

- **Öneri:** Hacim, karşılama süresi, onay bekleme, ret oranı ve SLA uyumu hiyerarşik sunulsun.
- Onay bekleme bir darboğaz göstergesi; tamamlanma süresiyle aynı grafik eksenine açıklamasız konmamalı.
- Talep tipi bazlı dağılım ve dönem eğilimi yeni API boyutu gerektirebilir; mevcut veriden varmış gibi türetilmemeli.
- **Kabul:** Ret oranının pay/payda tanımı görülebilir; payda sıfırken %0 gösterilmez.

### 5.41 Operasyon raporu — `/reports/operations`

**Kanıt:** Canlı; Recharts barların yüklenmesi ayrıca beklendi ve doğrulandı.

- **P2:** Ekip, kişi, kategori, lokasyon ve departman dağılımları aynı kart yapısında; her kart grafik altında aynı değerleri yeniden listeliyor.
- **Öneri:** “İş yükü” ve “Talep dağılımı” grupları. Uzun isimler için sıralı yatay bar; mobilde küçük çoklu grafikler yerine öncelikli grafik ve erişilebilir veri listesi.
- Tek kategori varsa bar yerine doğrudan sayı/etiket daha verimli olabilir. Büyük listelerde ilk 10 + kalanlar; tam listeye erişim korunmalı.
- **Kabul:** Ekip/kişi yükü bir performans puanı gibi yorumlatılmaz; adet, kapsam ve anlık/dönemlik anlam açık olur.

### 5.42 Transfer raporu — `/reports/transfers`

**Kanıt:** Canlı, mevcut metrikler sıfır.

- **P2:** Dört sayı var; oran ile işlem adedinin farkını kullanıcı kendisi çıkarmak zorunda.
- **Öneri:** Transfer adedi, transfer edilen benzersiz kayıt oranı, ekip içi yeniden atama ve arızadan açılan talep ayrı açıklamalarla sunulsun.
- Kaynak→hedef ekip dağılımı ve neden grafiği yeni veri sözleşmesi olarak planlanmalı; sırf görsel çeşitlilik için Sankey eklenmemeli.
- **Kabul:** Aynı kayıt birden fazla transfer satırında görünüyorsa liste “Transfer hareketleri” diye etiketlenir; mükerrer hata izlenimi önlenir.

### 5.43 Varlık raporu — `/reports/assets`

**Kanıt:** Canlı.

- **Öneri:** Toplam, stok, zimmetli ve bakım/kayıp/kullanım dışı özetleri; tür ve lokasyon dağılımları. Adlandırmada envanter/varlık terimleri tutarlı olsun.
- Marka/model raporları katalog dönüşümü sonrasında eklenmeli; bugün serbest metni gruplayıp doğru sonuç beklememeli.
- **Kabul:** Ekranda “Güncel envanter durumu” ve güncellik bilgisi bulunur; geçmiş envanter trendi için snapshot/hareket verisi olmadan zaman grafiği çizilmez.

### 5.44 Garanti raporu — `/reports/warranty`

**Kanıt:** Canlı.

- **P2:** Genel “Başlangıç/bitiş tarihi” etiketleri bu sayfada garanti bitiş aralığını anlatmıyor.
- **Öneri:** “Garanti bitiş aralığı”, “Önümüzdeki 30 / 60 / 90 gün” hızlı seçimleri; bitmiş ve tarihi bilinmeyen cihazlara açık listeler.
- Varsayılan yaklaşan garanti penceresi kullanıcıya görünmeli. Gelecekte aylık bitiş dağılımı verisi eklenebilir.
- **Kabul:** Tarihi girilmemiş cihaz, garantisi bitmiş kabul edilmez; seçili aralığın hangi karta etki ettiği açıkça gösterilir.

### 5.45 Rapor kayıt listesi — `/reports/:report/list`

**Kanıt:** Canlı olay hacmi ve stok cihaz listeleri; diğer metrikler ortak bileşen üzerinden incelendi.

- **P2:** Başlık ve Excel eylemi var; uygulanan filtrelerin okunabilir özeti eksik.
- **Öneri:** Rapor adı → metrik → seçilen grup bağlamı, filtre çipleri, toplam satır ve veri kapsamı. Sıralama seçimi sonraki geliştirme olabilir.
- **Kod riski:** Transfer satırları aynı ticket numarasıyla birden fazla kez gelebilir; ticket numarasını React anahtarı olarak kullanmak çakışabilir. Tekil hareket/satır kimliği kullanılmalı. Mevcut sıfır transfer verisinde canlı doğrulanmadı.
- **Kabul:** KPI, liste ve dışa aktar aynı filtre/kapsamı kullanır; geri dönüş rapor filtresini korur.

### 5.46 Dışa aktarımlarım — `/reports/exports`

**Kanıt:** Canlı boş; iş listesi kodu incelendi.

- **P2:** Boş durumda işlemin nereden başlatılacağını anlatan yönlendirme yok.
- **Öneri:** “Bir raporun kayıt listesinden Excel’e aktarabilirsiniz.” Hazır dosyada sona erme zamanı; başarısız işte anlaşılır neden ve yeni iş oluşturma yolu.
- Yeni dışa aktarma, önceki dosyayı indirme ile karışmamalı. Sunucu yüzde ilerleme vermiyorsa sahte yüzde gösterilmemeli.
- **Kabul:** Bekliyor/hazırlanıyor/hazır/başarısız/süresi dolmuş ayrılır; indirme yetkisi güncel rol ve kullanıcı kapsamında tekrar denetlenir.

### 5.47 Yetkisiz ve bulunamayan sayfalar — 403 / 404

**Kanıt:** Kod; rol değiştirerek canlı test yapılmadı.

- Mevcut bileşenlerde başlık ve ana sayfaya dönüş var.
- **Öneri:** “Bu sayfaya erişim yetkiniz yok” ile “Sayfa bulunamadı” ayrılmalı; gerekirse önceki güvenli sayfaya dönüş. Kayıt varlığını ifşa etmeyen backend davranışı korunmalı.
- **Kod:** Bazı yetkisiz rotalar ana sayfaya, raporlar 403 ekranına gidiyor. Bilinçli ve tutarlı deneyim kuralı tanımlanmalı.
- **Kabul:** Boş ekran/yönlendirme döngüsü olmaz; kullanıcı neden işlem yapamadığını anlar.

## 6. CoreUI ve Chart.js ile rapor tasarım standardı

### 6.1 Güncel teknoloji kararı ve mevcut durum

**Hedef:** CoreUI Free React Admin Template ve `@coreui/react-chartjs` / Chart.js. CoreUI template tek başına bir grafik motoru değildir; grafikleri bu entegrasyonla oluşturulur. Resmî [template paket manifesti](https://raw.githubusercontent.com/coreui/coreui-free-react-admin-template/main/package.json) ve [CoreUI Chart dokümanı](https://coreui.io/react/docs/components/chart/) bu yapıyı doğruluyor.

**Geçiş başlangıcı:** Projede bugün `DistributionChart.tsx` Recharts kullanıyor. Bu tarihsel gözlem korunuyor; önceki “Recharts ile devam et” önerisi geçersizdir. Claude aynı veri sözleşmesini CoreUI Chart.js bileşenine taşımalı, kullanım bittiğinde Recharts paketini, importlarını ve ona özel chunk tanımlarını temizlemelidir.

Lazy grafik yükleme, API tarafında hesaplama, yetki kapsamı ve metin/tablo alternatifi korunmalı. Yeni canvas grafikler yalnız görsel/hover etkileşimine bağlı bırakılmamalı. İlk incelemede görülen geçici “Grafik yükleniyor…” sonradan tamamlandı; kalıcı grafik arızası bulunmuş gibi aktarılmamalı.

### 6.2 Önerilen rapor yerleşimi

```text
Rapor adı                     Dışa aktarımlarım
Rapor seçimi
Tarih özeti / hızlı dönem     Filtreler (2)
Aktif filtreler + kapsam + son güncelleme

Öncelikli KPI'lar (mobilde 2 sütun uygunsa)
Ana grafik
İkincil grafik / dağılım
Verileri listele
```

- Mobil filtreler ayrı panelde düzenlenir; “Uygula” ile topluca uygulanır. “Temizle” ve “Vazgeç” farklı işler yapar.
- Masaüstünde tarih ve birincil filtreler görünür, kalanlar “Diğer filtreler” altında.
- Dönemlik raporlarda önerilen başlangıç son 30 gündür; uygulanmadan önce ürün kararı olarak netleştirilmeli. Anlık envanter raporuna anlamsız tarih filtresi eklenmemeli.
- Başlık altında “Tüm kayıtlar” veya “Üye olduğunuz ekiplerin kayıtları” açıkça korunmalı.
- Kartta büyük sayı, birim, hesaplama dönemi ve kısa açıklama bulunmalı. Her karta dev grafik eklemek zorunlu değil.

### 6.3 Grafik seçimi ve veri bağımlılığı

| İhtiyaç | Önerilen görsel | Veri durumu |
|---|---|---|
| Ekip/kişi/kategori/lokasyon dağılımı | CoreUI `CChart` ile sıralı yatay bar | Mevcut bucket yapısıyla geliştirilebilir |
| Backlog yaşı | CoreUI `CChart` ile yaş sırası korunmuş bar | Mevcut aralıklar kullanılabilir |
| SLA uyumu | Sayı + pay/payda; gerekirse yüzde barı | Mevcut oran verisi yeterli; gauge zorunlu değil |
| Cihaz türü/durumu | Yatay bar; az kategoride isteğe bağlı donut | Mevcut dağılım verisi kullanılabilir |
| Günlük/haftalık açılan-tamamlanan kayıt | CoreUI `CChart` ile çizgi veya karma grafik | Tarih bucket’ları için API genişletmesi gerekir |
| İlk yanıt/çözüm süresi trendi | CoreUI çizgi grafik ve ayrı birim etiketi | Zaman serisi gerekir; tek ortalamadan üretilemez |
| Önceki dönem karşılaştırması | KPI yanında fark ve önceki değer | Eşdeğer önceki dönem sorgusu gerekir |
| Garanti bitişlerinin aylık dağılımı | CoreUI bar grafik | Aylık gruplama gerekir |
| Kaynak/hedef ekip transfer dağılımı | Yatay bar veya okunabilir matris | Yeni boyutlar gerekir; mevcut toplam sayı yetmez |
| Tarihsel stok/zimmet eğilimi | Zaman serisi | Tarihsel durum yeniden kurulmalı veya snapshot tutulmalı |

### 6.4 Grafiklerin davranışı

1. Mobilde tek sütun; KPI’lar metin sığıyorsa iki sütun. Grafik yüksekliği ve etiket genişliği birlikte tasarlanmalı.
2. Eski grafiğin sabit 128 px kategori alanı aynen taşınmamalı; CoreUI/Chart.js etiket alanı uzun Türkçe isimlerde yeniden test edilmeli. Gerekirse kısaltma + tam adın erişilebilir listede gösterimi.
3. Barların sıfır tabanı korunmalı. Bağımsız grafiklerin uzunlukları ortak ölçek varmış gibi karşılaştırılmamalı; gerekli yerde eksen/değer gösterilmeli.
4. En çok 8–10 kategori ilk görünümde; kalanlar listeye veya “Diğer” grubuna. “Diğer” seçilince neyin listelendiği tanımlanmalı.
5. Tooltip yalnız hover’a bağlı olmamalı; dokunma ve klavye alternatifi sağlanmalı. Süre, adet ve oran doğru Türkçe birimle gösterilmeli.
6. Grafik tıklaması eklenirse aynı filtrelerle drill-down açmalı. Mevcut metin bağlantıları korunmalı.
7. Başarı, uyarı, ihlal ve nötr anlamları tutarlı renklerle sunulmalı; renk yanında etiket/ikon olmalı.
8. Her seriye rastgele renk verilmemeli. Aynı ekip veya durum ekranlar arasında aynı renkle gösterilmeli.
9. Boş veri, ölçülemeyen veri, sıfır değer ve hata ayrı durumlar. Payda sıfırsa oran “Hesaplanamadı / veri yok”.
10. Grafik yüklenirken alan yüksekliği korunmalı; metrikler zıplamamalı. İndirme/paket hatasında yalnız grafik bölümü toparlanabilmeli.
11. Reduced-motion tercihi gözetilmeli; mevcut kapalı animasyon iyi başlangıçtır.
12. Kullanıcı yüz bin satırı tarayıcıya indirip grafik hesaplamamalı; aggregation backend’de yapılmalı.

### 6.5 Önerilen ortak bileşenler

`ReportHeader`, `ReportPeriodSelector`, `ReportFilterSheet`, `ActiveFilterChips`, `MetricCard`, `ChartCard`, `DistributionChart`, `TrendChart`, `ChartDataTable`, `ReportEmptyState`, `ExportStatus`.

Bu adlar uygulama önerisidir; yeni soyutlama sayısını artırmak için değil, aynı deneyimi altı raporda korumak için kullanılmalı. Mevcut `FigureCard`, `ReportFilterBar`, `DistributionChart` veri arayüzleri korunabilir; render katmanları CoreUI bileşenleri ve Chart.js ile değiştirilmelidir.

## 7. Merkezi katalog ve veri kalitesi

### 7.1 Hangi alan nasıl yönetilmeli?

| Veri | Kaynak / kontrol | Serbest metin? |
|---|---|---|
| Marka | Admin kataloğu | Cihaz formunda hayır |
| Model | Marka ve cihaz türüne bağlı admin kataloğu | Cihaz formunda hayır |
| Cihaz türü | İlk etapta mevcut dört kontrollü tür | Rastgele admin genişletmesi süreç/rapor desteği olmadan yapılmamalı |
| Departman/lokasyon/ekip | Mevcut organizasyon tanımları | İşlem formlarında hayır |
| Kategori/talep tipi | Admin hizmet kataloğu | İşlem formlarında hayır |
| Kullanıcı/onaylayıcı | Aktif, yetkili kullanıcı kaynağı | Hayır |
| Durum/öncelik/bekletme nedeni | İş kurallarının kontrollü seçenekleri | Hayır; genel sözlük gibi açılmamalı |
| Saat dilimi | Doğrulanmış saat dilimi listesi | Hayır |
| Şablon değişkeni | Sunucunun izinli değişken listesi | Seçimle ekleme; serbest değişken yaratma yok |
| Seri numarası | Normalize edilen cihaz kimliği | Evet, uygun doğrulamayla |
| Konu/açıklama/çözüm/not | Kullanıcının bağlamsal anlatımı | Evet |
| Tarihler | Tarih kontrolü ve ilişkili doğrulamalar | Rastgele metin yerine kontrollü tarih |

### 7.2 Marka/model için önerilen model

- `Brand`: kimlik, görünen ad, normalize anahtar, aktiflik, sürüm.
- `AssetModel`: kimlik, marka kimliği, cihaz türü, model adı, normalize anahtar, aktiflik, sürüm.
- `Asset`: katalog model kimliği; marka/tür bilgisi bu ilişkiden tutarlı elde edilir. Ayrı alanlar tutulursa çelişkili kombinasyon sunucuda engellenir.
- Marka için normalize ad; model için marka + tür + normalize model adı kapsamlı benzersizlik önerilir. Model varyantlarının ayrı tutulup tutulmayacağı netleştirilmeli.
- Türkçe `I/İ/ı/i` ve uluslararası marka adları için tek belgelenmiş normalizasyon kuralı kullanılmalı. Aşırı noktalama/aksan silme farklı modelleri birleştirmemeli.
- Yetkili admin tanım ekler; IT seçim yapar. Listede olmayan değer için “Tanım eksik” yönlendirmesi bulunur; fark edilmeden serbest metne dönülmez.
- Pasif tanım tarihî kayıtta okunabilir kalır. Kullanılan tanım için hard delete yerine pasifleştirme.

### 7.3 Geçiş planı

1. Mevcut marka/model çiftlerini ve kullanım adetlerini çıkar.
2. Benzerlik adaylarını öner; belirsiz eşleşmeleri otomatik birleştirme.
3. Onaylanan katalogları oluştur; eski yazımları eşleme tablosunda koru.
4. Yeni referansları ekle, mevcut kayıtları eşle; çözümsüzleri ayrı raporla.
5. Form, filtre, API, CSV ve raporu birlikte geçir. Geri dönüş planı ve yedek kontrolü olsun.
6. Benzersizlik/referans kısıtlarını tamamla; eski alanları ancak veri doğrulaması bittiğinde kaldır.

**Kabul:** `Lenovo`, ` LENOVO ` gibi varyasyonlar aynı kurala göre yönetilir; farklı model varyantları yanlışlıkla birleşmez. CSV ve API, UI kontrolünü atlayarak mükerrer katalog/cihaz üretemez.

## 8. Otomatik demirbaş numarası

Önerilen görünen format: **`DMR-000001`**. Format öneridir; otomatik üretim kullanıcı talebidir. Tür/lokasyon/yılın numaraya eklenmesi gerekli değil; değişebilen organizasyon bilgisini kalıcı kimliğe gömmemek daha sade olur.

- Numara backend’de kayıt sırasında üretilir; tarayıcı `MAX + 1` hesaplamaz.
- Veritabanı sequence veya atomik sayaç + benzersiz kısıt kullanılır.
- Numara değişmez, iptal/silinme sonrası tekrar kullanılmaz; sayısal boşluklar hata sayılmaz.
- Oluşturma ekranı “Kaydedildiğinde otomatik atanır” gösterir; hayalî kesin numara önizlemesi yapmaz.
- Mevcut etiketler topluca yeniden numaralanmaz; gerekiyorsa eski sistem numarası ayrı alanda korunur.
- CSV yeni kayıtları da aynı üreticiyi kullanır. İçe aktarım önizlemesi kalıcı numara tüketmek zorunda değildir.
- Tekrarlanan istek/yeniden deneme için idempotency yaklaşımı; fiziksel cihaz tekrarı için seri numarası kuralları ayrıca ele alınır.
- Ekranda kopyala eylemi ve ileride barkod/QR etiketi mümkündür; QR bu aşamada ek kapsamdır.

**Kabul:** Eşzamanlı cihaz ekleme benzersiz numaralar üretir; aynı işlemin ağ tekrarı ikinci cihaz oluşturmaz; kayıt başarısında atanan numara görünür.

## 9. Ortak UI standardı

### 9.1 Görsel dil

- CoreUI Free React template’in sidebar, header, içerik yüzeyleri, form, tablo, ikon ve tipografi düzeni tüm uygulamanın görsel tabanı olacak. Eski görünüm yalnız renk değiştirilerek korunmayacak; TGYSM marka rengi tema token’ları üzerinden uyarlanacak.
- Bir ekranda bir baskın ana eylem; diğerleri ikincil. Kırmızı yalnız gerçek kritik/zararlı sonuçlarda.
- Kartlar yalnız ayrı içerik gruplarında; her metni ayrı büyük karta dönüştürme. İç içe kartlardan kaçın.
- Önerilen boşluk ölçeği: 4/8/12/16/24/32 px; mobil sayfa kenarı çoğu yerde 16 px. Bunlar tasarım hedefidir.
- Gövde ve girişler yaklaşık 16 px; küçük metadata okunabilir kalmalı. Başlıklar mobilde ekranın büyük bölümünü kaplamamalı.
- Sayı, tarih ve birimlerde ortak biçimlendirici; dashboard sayılarında tabular rakamlar.
- Tek ikon ailesi; ikon tek başınaysa erişilebilir adı olsun. Hamburger karakteri yerine tutarlı ikon kullanılabilir.

### 9.2 Form standardı

- Zorunlu/isteğe bağlı alanlar açıklanır; placeholder etiket yerine geçmez.
- Hata ilgili alanın altında, metinle ve alan bağlantısıyla görünür; ilk hata odaklanır. Uzun formda üst hata özeti kullanılabilir.
- Form değeri ağ hatasında korunur. Veri kaydı tamamlandıktan sonraki ikincil hata “Kaydedilemedi” diye sunulmaz.
- Kaydetmeden çıkış koruması yalnız değişiklik varsa çalışır. Hassas metni kontrolsüz localStorage’a yazmak varsayılan çözüm değildir.
- Aramalı seçicide yükleniyor/sonuç yok/hata/yeniden dene durumları, klavye okları, Enter ve Escape davranışı tanımlı olur.
- Mobil klavye açıkken alan ve ana eylem erişilebilir kalır. Büyük formlar için tam ekran sayfa, kısa işlemler için dialog tercih edilir.
- Düğme işlem adını söyler: “Onaylayıcıyı ata”, “Cihazı teslim et”, “Kaydı aktar”. Genel “Onayla” yalnız gerçek onay kararında kullanılır.

### 9.3 Listeler ve geri bildirim

- Mobil kartta önce kayıt başlığı; sonra kritik durum; sonra metadata. Her kartta dev “Aç” düğmesi yerine açık başlık bağlantısı düşünülebilir.
- Aynı karttaki diğer eylemlerle çakışan tüm-kart tıklaması uygulanmamalı; semantik bağlantı/button düzeni korunmalı.
- Arama gecikmeli uygulanabilir; filtre sonucu sayısı ve aktif filtreler görünür.
- İlk kullanım boşluğu, filtre sonucu boşluğu, erişim kısıtı ve ağ hatası ayrı metinlerdir.
- Başarı bildirimi kısa ve somut olmalı; kritik kayıt numarası sadece birkaç saniyelik toast içinde bırakılmamalı.
- Durum değişimi sonrası görünüm güncellenir; eski veriye geçici dönme açık yükleme davranışıyla yönetilir.
- Geniş tablolarda sabit ana kimlik kolonu, anlamlı kolon önceliği ve yatay kaydırma ipucu düşünülebilir. Mobil kart alternatifi korunmalı.

### 9.4 Erişilebilirlik hedefi

- Proje dokunma hedefi olarak ana kontrollerde **44×44 CSS px** veya daha büyük alanı benimseyebilir. Bu bir ürün hedefidir; WCAG 2.2 AA minimum hedef boyutu genel kuralı **24×24 CSS px** olup istisnaları vardır. [W3C hedef boyutu açıklaması](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html)
- Normal metinde en az 4.5:1, büyük metinde 3:1 kontrast hedeflenmeli. Mevcut tüm renklerin ölçüldüğü iddia edilmiyor. [W3C kontrast açıklaması](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)
- 320 CSS px eşdeğerinde içerik yeniden akmalı; veri tabloları gibi iki boyutlu istisnalar olsa da temel form işi mümkün olduğunca iki eksende kaydırma istememeli. [W3C reflow açıklaması](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html)
- İçeriğe atla bağlantısı, görünür odak, diyalog odağının geri dönmesi, dinamik sonuç duyuruları ve route sonrası odak akışı doğrulanmalı.
- Grafiklerin okunabilir metin/tablo alternatifi kalmalı. Renk, hover veya animasyon tek bilgi kanalı olmamalı.
- Bu belge WCAG uygunluk sertifikası veya tamamlanmış ekran okuyucu denetimi değildir.

## 10. Uygulama sırası ve teslim paketleri

| Paket | İçerik | Bağımlılık | Tamamlanma ölçütü |
|---|---|---|---|
| C — CoreUI iskeleti ve navigasyon | Tüm uygulama için CoreUI, breadcrumb, rol odaklı ana ekran, ayrı admin, hesap menüsü | İlk uygulama paketi; A ile ilerleyebilir | Üst seviyeye erişim, aktif menü ve tüm viewport yerleşimleri doğrulanır |
| A — Anlam ve açık hatalar | SLA görünür etiketleri, Onayla metinleri, marka/model etiketi, audit filtreleri, ekip rozeti, worker anlam ayrımı | Sınırlı API değişimi gerekebilir | İlgili ekranlarda doğru anlam ve koşullu durum testleri |
| B — Veri temeli | Marka/model katalogları, otomatik demirbaş, mevcut veri eşlemesi, CSV sözleşmesi | Veri modeli + migration | UI/API/CSV aynı kuralı uygular |
| D — Günlük iş akışları | Arıza/talep formu, cihaz seçimi, detay/yanıt sıralaması, kuyruklar, cihaz işlemleri | B’nin cihaz kataloğu tamamlanmalı | Aç → izle → yanıtla/teslim et akışları mobilde rahat |
| E — Rapor deneyimi | CoreUI KPI/filtre düzeni, Chart.js grafik dönüşümü, drill-down/export bağlamı | Mevcut rapor API’si | Sayı/liste/export aynı kapsam; ilk ekranda sonuç |
| F — Yeni analitik | Zaman serileri, önceki dönem, yeni dağılımlar | Yeni backend aggregation | Metrik tanımı ve tarih anlamı testlerle sabit |
| G — Son doğrulama | Rol/viewport/erişilebilirlik/hata/uzun veri regresyonu | Önceki paketler | Kritik senaryolar ve proje kalite kapıları geçer |

Önce CoreUI uygulama iskeleti ve ortak bileşenler kurulmalı; tüm sayfalar bu hedefe taşınmalı. Recharts geçiş süresince yalnız eski sayfalarda kalabilir, nihai teslimde kaldırılır. İş kuralları ve API sözleşmeleri UI template’i uğruna bozulmamalı.

## 11. Kullanılacak ECC skill’leri

Bu incelemede `ecc:frontend-design-direction` ve `ecc:frontend-a11y` yönergeleri kullanıldı. Aşağıdakiler sonraki uygulama için önerilen set; hepsini her görevde birlikte çalıştırmak gerekli değil.

| Skill | Somut kullanım |
|---|---|
| `ecc:frontend-design-direction` | Çalışan/IT/admin ayrımı, ekran hiyerarşisi, sade operasyon tasarımı |
| `ecc:design-system` | Token, tipografi, boşluk, durum, form ve grafik görsel standardı |
| `ecc:frontend-a11y` | Form etiketleri, combobox, modal, klavye/odak, grafik alternatifleri |
| `ecc:make-interfaces-feel-better` | Dokunma alanı, metin kırılması, boşluk ve mikro etkileşim düzenlemeleri |
| `ecc:react-patterns` | Ortak bileşen sınırları, form ve server state düzeni |
| `ecc:api-design` | Marka/model referansları, rapor agregasyonları, hata/filtre sözleşmeleri |
| `ecc:dotnet-patterns` | Backend kuralları ve atomik numaralandırma uygulaması |
| `ecc:database-migrations` | Mevcut serbest metin verisini güvenli katalog ilişkilerine taşıma |
| `ecc:e2e-testing` | Oluşturma, katalog, numara, CSV, rol ve rapor tutarlılığı senaryoları |
| `ecc:browser-qa` | Uygulama sonrası breakpoint, konsol, görsel ve erişilebilirlik kontrolü |

**Önerilen sıra:** CoreUI sürüm/uyumluluk incelemesi → uygulama iskeleti + breadcrumb → ortak UI standardı → veri/API dönüşümü → tüm sayfaların taşınması → Chart.js raporları → E2E ve browser QA. Template, kullanıcı tarafından seçildi; yeni tasarım kütüphanesi araştırmasıyla karar yeniden açılmamalı.

## 12. Kabul ve doğrulama matrisi

### 12.1 Ekran ve rol

- [ ] Bölüm 5’teki bütün sayfa/akışlar CoreUI görünümüne taşınır; yalnız admin veya dashboard dönüşümü yeterli değildir.
- [ ] Breadcrumb her ilgili sayfada bulunur; üst seviye bağlantıları, sidebar aktifliği ve bağlama dönüş birlikte çalışır.
- [ ] Free kapsamı korunur; istemeden PRO/add-on bağımlılığı veya demo içeriği eklenmez.
- [ ] Eski ve yeni CSS resetleri çakışmaz; dönüşüm bitiminde kullanılmayan UI paketleri temizlenir.
- [ ] Çalışan, IT personeli ve admin ayrı oturumlarla denenir; yalnız admin görünümüne bakarak diğer roller onaylanmaz.
- [ ] 320, 360, 390, 768, 1024, 1280 px; gerçek iOS Safari ve Android Chrome’da temel akışlar.
- [ ] Uzun Türkçe ad, e-posta, lokasyon yolu, model ve ticket konusu.
- [ ] 0/1/çok kayıt; yüklenme, hata, bağlantı kesilmesi ve yeniden deneme.
- [ ] Klavye açıkken form, safe-area, odak, yakınlaştırma ve uzun sayfa dönüşü.
- [ ] Kaydetmeden çıkışta yalnız değişmiş veri için koruma.

### 12.2 Veri ve işlemler

- [ ] Eşzamanlı cihaz ekleme benzersiz demirbaş üretir.
- [ ] Ağ tekrarında/idempotent yeniden denemede ikinci cihaz oluşmaz.
- [ ] Seri numarası normalizasyonu ve bilinmeyen seri için boş değer davranışı belgelenir.
- [ ] Marka değişimi geçersiz modeli temizler; API de ilişkiyi doğrular.
- [ ] Pasif katalog tarihî kayıtta görünür, yeni seçimde önerilmez.
- [ ] CSV eşleşmeyen, çakışan ve tekrarlanan satırları anlaşılır raporlar.
- [ ] Onaylayıcı atamak talebi onaylamakla karışmaz.
- [ ] İç not görünürlüğü ve kullanıcıya açık yanıt ayrımı korunur.

### 12.3 Raporlar

- [ ] Rapor grafikleri CoreUI Chart.js entegrasyonuyla çalışır; Recharts import/paket/chunk kullanımından arındırılır.
- [ ] Grafik/KPI/drill-down/export aynı kapsam ve filtrelerle tutarlıdır.
- [ ] Sıfır, veri yok, payda sıfır ve servis hatası ayrı gösterilir.
- [ ] Tarih anlamı metrik bazında açıktır; anlık stok yanlışlıkla dönem trendi olmaz.
- [ ] Geçersiz tarih sessizce daha geniş rapora dönüşmez.
- [ ] Uzun grafik etiketleri, 1/10/100 kategori ve büyük sayılar denenir.
- [ ] Metin/tablo alternatifi ve klavyeyle aynı drill-down erişimi vardır.
- [ ] Transfer detay satırlarında tekil anahtar kullanılır.
- [ ] Hazır/başarısız/süresi dolmuş dışa aktarım durumları gerçek verilerle denenir.
- [ ] Grafik yükleme hatası sayfanın metrik ve liste kullanımını tümden bozmaz.

### 12.4 Yayın öncesi

Uygulama değişikliklerinde projenin mevcut lint, typecheck, test, API tip üretimi ve uygun E2E kapıları çalıştırılmalı. Bu inceleme turunda bunlar çalıştırılmadı; uygulamada değişiklik yapılmadı. Dokümanda önerilen testler geçmiş gibi işaretlenmemeli.

## 13. İzlenebilir kaynak dosyaları

| Alan | Kaynak |
|---|---|
| Sayfa envanteri ve rol sınırları | `frontend/src/app/router.tsx`, `frontend/src/shared/routes.ts`, `features/auth/guards.tsx` |
| Navigasyon | `frontend/src/shared/ui/AppShell.tsx`, `features/admin/AdminLayout.tsx` |
| Ana ekran | `frontend/src/features/home/HomePage.tsx` |
| Ortak mobil bileşenler | `frontend/src/shared/ui/ResponsiveList.tsx`, `Dialog.tsx`, `Button.tsx`, `TextField.tsx`, `SelectField.tsx` |
| Cihaz alanları | `frontend/src/features/assets/AssetFormDialog.tsx`, `AssetDetailPage.tsx` |
| Yeni destek kaydı | `frontend/src/features/tickets/NewTicketPage.tsx` |
| Ekip rozeti | `frontend/src/features/admin/teams/TeamsPage.tsx` |
| Denetim filtresi | `frontend/src/features/admin/audit/AuditPage.tsx` |
| SLA düzenleme | `frontend/src/features/admin/sla/SlaPolicyPage.tsx` |
| Worker sağlık anlamı | `backend/src/Tgysm.Application/Features/Operations/ListWorkerHeartbeats.cs`, `frontend/src/features/admin/system/SystemPage.tsx` |
| Makale editörü | `frontend/src/features/kb/ArticleEditorPage.tsx`, `ArticlePage.tsx` |
| Rapor düzeni | `frontend/src/features/reports/ReportsPage.tsx`, `ReportFilterBar.tsx`, `FigureCard.tsx` |
| Grafik | `frontend/src/features/reports/DistributionChart.tsx`, `frontend/package.json` |
| Filtre ve drill-down | `frontend/src/features/reports/useReportFilter.ts`, `DrilldownPage.tsx`, `MyExportsPage.tsx` |

## 14. Bu belge sonrasında güncellenecek proje belgeleri

- `tgysm.md`: otomatik demirbaş, merkezi cihaz kataloğu, hedef sayfa akışları ve kabul kriterleri.
- ADR: katalog normalizasyonu/benzersizlik, numara üretimi, eski veri geçişi; gerekiyorsa rapor zaman serisi sözleşmesi.
- `capabilities.md`: yalnız gerçekten uygulanan değişiklikler tamamlandıktan sonra özellik durumu.
- Kullanıcı rehberleri: cihaz ekleme, katalog eksikliği, CSV eşleme, rapor filtreleri ve dışa aktarma.
- `README.md` / `CLAUDE.md`: değişen bileşen veya geliştirme akışı varsa teknik bağlam; ürün önerilerini gerçekleşmiş gibi yazmama.

Bu dosyanın önerilen konumu, proje içinde onaylandıktan sonra `docs/design/` veya `docs/analysis/` altıdır. Mevcut inceleme çıktısı kaynak projeyi değiştirmeden ayrı teslim edilmiştir.

## 15. Claude için uygulama görevi ve teslim tanımı

### 15.1 Görev

TGYSM’nin mevcut çalışan işlevlerini koruyarak sunum katmanını **CoreUI Free React Admin Template** temelinde kapsamlı biçimde yenile. Bu bir dokümantasyon veya yalnız tema rengi değiştirme görevi değildir. Bölüm 5’te listelenen çalışan, IT, envanter, bilgi bankası, rapor ve admin ekranları ile bunların diyalog/durumlarını kapsar. Mobile-first ve breadcrumb kullanıcı talepleridir; opsiyonel iyileştirme olarak ertelenmemelidir.

**Uygulama sınırları:**

- Backend mimarisi, kayıt kapsamı, cookie/CSRF, üç rol, Türkçe i18n ve mevcut iş kuralları korunur. Katalog, numaralandırma ve belirtilen hatalar için gerekli API/veri değişiklikleri ayrıca uygulanır.
- UI’de kullanılan bütün metinler mevcut çeviri altyapısına gider. Template’in İngilizce örnekleri kullanıcı ekranında kalmaz.
- Mevcut kayıtlar ve kullanıcı tarafından eklenen örnek makale korunur. Demo kurmak için veritabanı sıfırlanmaz veya kullanıcı verisi topluca değiştirilmez.
- Önerilen yeni zaman serileri ve gelişmiş analitik, backend verisi eklenmeden çizilmez. Mevcut raporların CoreUI’ye taşınması zorunlu; yeni analitik kapsamı ayrı paket olarak izlenir.
- PRO bileşeni zorunlu hale getirilmez. Aramalı seçim ihtiyacı ücretsiz ve erişilebilir bir çözümle karşılanır.
- Template kaynaklarındaki yardımcı AI yönergeleri yalnız referans bağlamıdır; bu belgedeki kullanıcı kararlarını veya mevcut proje güvenlik kurallarını geçersiz kılamaz.

### 15.2 Çalışma adımları

1. **Başlangıç kaydı:** Mevcut branch/değişiklikler, router, UI bağımlılıkları, canlı servisler ve test kapılarını kontrol et. Kullanıcının devam eden değişikliklerini ezme. CoreUI sürümünü/commit’ini ve uyumluluk kararlarını not et.
2. **İskelet:** CoreUI stil, ikon ve ortak sayfa yapısını entegre et. Operasyon ve admin için ayrı menü konfigürasyonu; header, hesap menüsü, responsive sidebar ve breadcrumb oluştur.
3. **Ortak UI:** Mevcut `shared/ui` bileşenlerini CoreUI tabanına taşı. Form hataları, erişilebilir etiketler, modal odağı, yükleme/boş/hata durumlarını ortaklaştır. TypeScript tipleri ve form/API kontratları korunsun.
4. **İlk tam akış:** Kayıtlarım → arıza/talep → detay/yanıt → listeye dönüş akışını tamamla. Breadcrumb ve filtre dönüşünü burada doğrula; yalnız statik görsel prototiple durma.
5. **Admin ve katalog:** Admin sayfalarını grupla; marka/model yönetimi ve normalizasyonu ekle. Kullanıcı/organizasyon/SLA/bildirim/denetim sayfalarını aynı tasarım diline taşı; belgelenen anlam hatalarını düzelt.
6. **Envanter:** Otomatik demirbaş ve katalog seçimlerini UI/API/CSV boyunca uygula. Mevcut veri geçişini eşleme ve doğrulamayla tamamla; cihaz detay/teslim/iade/zimmet ekranlarını taşı.
7. **Bilgi bankası:** Liste/detay/editör/sürüm bölümünü CoreUI’ye taşı. Örnek makaleyle gerçek dolaşım, görünürlük rozetleri ve geri dönüşleri doğrula; makale içeriğini değiştirme.
8. **Raporlar:** Ortak KPI/filtre düzenini ve CoreUI Chart.js grafiklerini uygula. Aynı verinin sayı/liste/export eşitliğini ve erişilebilir metin alternatifini koru.
9. **Temizlik:** Artık kullanılmayan Recharts ve eski stil/bileşen bağımlılıklarını, demo sayfalarını ve gereksiz CSS’i kaldır. Kalan bağımlılıkların nedenini açıkla; sırf template’te var diye Redux/ikinci router bırakma.
10. **Doğrulama ve doküman:** Rol bazlı smoke/E2E, kritik veri testleri ve responsive görsel incelemeyi tamamla. Özellik belgelerini yalnız uygulanan durumla güncelle. Dev servisleri durdurduysan eski çalışma durumuna getir.

### 15.3 Sayfa dönüşüm teslim listesi

Her satır için “CoreUI görünümü / breadcrumb veya uygun auth istisnası / mobil / hata-boş-yükleme / rol kontrolü / doğrulama kanıtı” alanları tutulmalı.

| Grup | Zorunlu ekranlar |
|---|---|
| Kimlik | Giriş, şifremi unuttum, şifre belirle, normal/zorunlu şifre değişimi |
| Çalışan | Ana ekran, profil, kayıtlarım, yeni arıza, yeni talep, zimmetlerim |
| IT | İş listesi, kuyruklar, arıza/talep detayları, yazışma/iç not, transfer/beklet/tamamla diyalogları |
| Onay | Onay yönetimi, onaylayıcı atama, mevcut yetkili onay/ret akışları |
| Envanter | Liste, cihaz oluştur/düzenle, cihaz detay, yaşam döngüsü işlemleri, CSV başlangıç/önizleme/sonuç |
| Bilgi bankası | Liste, yeni makale, makale detay, düzenleme, önizleme, sürüm geçmişi |
| Rapor | Altı rapor ailesi, metrik kayıt listeleri, dışa aktarım işleri ve durumları |
| Admin organizasyon | Kullanıcılar, departmanlar, lokasyonlar, ekipler/üyelik ve formları |
| Admin katalog | Markalar, modeller, kategoriler, talep tipleri ve formları |
| Admin işletim | E-posta şablonları/gönderimleri, SLA takvimi/politikası, sistem, denetim |
| Genel | 403, 404, yükleme/hata/boş durum, modal/offcanvas, hesap menüsü, breadcrumb |

Kimlik ekranlarında anonim kullanıcıya sahte uygulama breadcrumb’ı gösterilmez; gerçek “Girişe dön” gibi bağlamsal bağlantı kullanılır. Zorunlu şifre değişimi güvenlik akışı normal sayfa navigasyonuyla atlatılmaz.

### 15.4 Tamamlandı sayılmayacak durumlar

- Yalnız dashboard veya admin sayfasının yenilenmesi.
- Eski arayüze yalnız CoreUI renkleri/düğmeleri eklenmesi.
- Breadcrumb’ın yalnız düz metin olması veya üst öğelerin çalışmaması.
- Makale düzenlemeden makaleye, kuyruktan açılan kayıttan kendi kuyruğuna dönememe.
- Marka/model seçimlerinin yalnız frontend’de sınırlandırılıp API/CSV’nin serbest metin bırakılması.
- Demirbaş numarasının istemcide veya yarış koşuluna açık `MAX + 1` yöntemiyle hesaplanması.
- Recharts ile Chart.js’in nihai üründe aynı ihtiyacı karşılamak için birlikte bırakılması.
- Demo grafik/sayaç verilerinin gerçekmiş gibi gösterilmesi.
- Mobilde yalnız sayfa taşmıyor diye form ve aksiyon erişiminin doğrulanmış sayılması.
- Çalışan/IT görünümünün yalnız admin oturumuyla test edilmiş sayılması.

### 15.5 Claude’un son tesliminde beklenenler

1. Uygulanan kapsam ve kalan işler; bu belgedeki sayfa listesiyle karşılaştırma.
2. CoreUI kaynağı/sürümü, kaldırılan/eklenen bağımlılıklar ve önemli uyumluluk kararları.
3. Masaüstü ve mobil ana ekran, detay, envanter, makale, rapor ve admin görselleri.
4. Breadcrumb ve bağlama dönüş için çalıştırılan senaryolar.
5. Test sonuçları; çalıştırılamayan kontrol ve nedeni açıkça belirtilmeli.
6. Katalog/numaralandırma migration ve mevcut veri doğrulama sonucu.
7. Başlatma talimatı ve canlı uygulamanın erişilebilir durumu.

Son teslim, yalnız “template entegre edildi” cümlesiyle sınırlı olmamalı; kullanıcı aynı işi yeni arayüzde baştan sona tamamlayabilmelidir.
