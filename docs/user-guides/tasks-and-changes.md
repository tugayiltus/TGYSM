# Görevler ve değişiklikler

Bu sayfa IT personeli ve yöneticiler içindir. Çalışanlar görevleri ve değişiklikleri görmez.

## Görevler

Görev, bir ekibe verilen tek bir iştir. Numarası `TSK-` ile başlar (örneğin `TSK-2026-000012`). Bir görev tek başına açılabilir, bir kayıttan açılabilir veya bir değişikliğin aşamasına bağlanabilir. Görevlerin SLA süresi yoktur; yalnızca isteğe bağlı bir bitiş tarihi vardır.

### Görev açma

1. Menüden **Görevler** sayfasını açın ve **Yeni görev** düğmesine basın.
2. **Başlık**, **Açıklama** ve **Ekip** alanlarını doldurun.
3. İsterseniz **Atanan** listesinden ekipten birini seçin. Seçmezseniz **Ekipten biri üstlensin** kalır.
4. **Öncelik** ve **Bitiş tarihi** seçin.
5. **Görevi aç** düğmesine basın.

Bir kayıt üzerinde çalışırken kaydın **Görevler** bölümündeki **Görev ekle** ile de görev açabilirsiniz. Görev o kayda bağlı kalır. Açık görevi olan bir kaydı tamamlarken uyarı çıkar; kayıt yine de tamamlanır.

### Görev listesi

**Görünüm** filtresiyle listeyi seçin:

| Görünüm | İçindekiler |
|---|---|
| **Görevlerim** | Size atanmış görevler. |
| **Ekibimin görevleri** | Ekiplerinizin görevleri. |
| **Atanmamış görevler** | Ekiplerinizde kimsenin üstlenmediği görevler. |
| **Geciken görevler** | Bitiş tarihi geçmiş açık görevler. |

**Görev ara**, **Durum** ve **Öncelik** ile listeyi daraltın. **İş listem** sayfasında da **Görevlerim**, **Ekibimin görevleri** ve **Geciken görevler** kartları vardır.

### Görev üzerinde çalışma

Görevi açınca **İşlemler** kartında yalnızca o anki durumda yapılabilecek işlemler çıkar.

| İşlem | Ne yapar |
|---|---|
| **Üstlen** | Atanmamış görevi size atar. Aynı anda iki kişi üstlenirse yalnızca biri alır. |
| **Başla** | Görevi **İşlemde** durumuna alır. |
| **Beklet** / **Devam et** | Görevi bir nedenle bekletir veya yeniden başlatır. |
| **Tamamla** | **Sonuç notu** ister; görev **Tamamlandı** olur. |
| **İptal et** | Neden ister; görev **İptal edildi** olur. |
| **Yeniden aç** | Tamamlanmış veya iptal edilmiş görevi yeniden açar. |
| **Ata** | Görevi ekipten başka birine verir. |
| **Aktar** | Görevi başka bir ekibe verir. Atanan kişi boşalır ve görev yeniden açık olur. |

Sayfanın altındaki **Geçmiş ve notlar** bölümünde görevin tüm geçmişi görünür. **Not ekle** ile iç not yazabilirsiniz.

Görev size atanınca e-posta alırsınız. Atanmamış bir görev ekibinize aktarılınca ekibin tüm aktif üyelerine e-posta gider.

## Değişiklikler

Değişiklik, sistemlerde planlı bir işlemdir: sunucu güncellemesi, ağ ayarı, yazılım kurulumu gibi. Numarası `CHG-` ile başlar. Bir değişiklik çalışmaya başlamadan önce başka bir yönetici onaylar.

### Değişikliğin durumları

| Durum | Anlamı |
|---|---|
| **Taslak** | Plan yazılıyor. |
| **Onay bekliyor** | Plan onaya gönderildi. |
| **Planlandı** | Onaylandı; bakım penceresi bekleniyor. |
| **Uygulanıyor** | Uygulama görevleri çalışıyor. |
| **Doğrulama** | Uygulama bitti; sonuç kontrol ediliyor. |
| **Kapalı** | Sonuç: **Başarılı**, **Geri alındı** veya **Başarısız**. |
| **İptal** | Çalışmadan iptal edildi. |

### Değişiklik oluşturma

1. Menüden **Değişiklikler** sayfasını açın ve **Yeni değişiklik** düğmesine basın.
2. **Başlık**, **Gerekçe** ve **Etkilenen hizmet** alanlarını doldurun.
3. **Risk** ve **Etki** seçin.
4. **Pencere başlangıcı** ve **Pencere bitişi** ile bakım penceresini girin. Bitiş başlangıçtan sonra olmalı.
5. **Uygulama planı**, **Doğrulama planı** ve **Geri dönüş planı** alanlarını yazın.
6. **Sahip ekip** ve **Sahip** seçin.
7. **Etkilenen varlıklar** bölümünde **Varlık ara** ile etkilenen cihazları ekleyin.
8. **Değişikliği oluştur** düğmesine basın. Değişiklik **Taslak** olarak açılır.

### Aşama görevleri

Değişikliğin işleri görevlerle yürür. Değişiklik sayfasındaki **Görevler** bölümünde her aşamanın kendi listesi vardır. **Görev ekle** ile aşamaya görev açın. Görevi herhangi bir ekibe verebilirsiniz.

| Aşama | Görev ne zaman çalışır |
|---|---|
| **Hazırlık** | Değişiklik planlanınca, uygulama ve doğrulama sırasında. |
| **Uygulama** | Yalnızca değişiklik uygulanırken. |
| **Doğrulama** | Yalnızca değişiklik doğrulamadayken. |
| **Geri dönüş** | Yalnızca geri dönüş başlayınca. |

Aşaması henüz başlamamış bir görevin sayfasında, düğmelerin yanında ne zaman başlatılabileceği yazar.

Başka bir ekip yalnızca kendi görevinin bulunduğu değişikliğin özetini görür: numara, başlık, durum, pencere, risk ve kendi aşamasının planı. Bu kişiler sayfanın üstünde **Bu değişikliğin çalışma özetini görüyorsunuz.** uyarısını görür.

### Onaya gönderme

Onaya göndermeden önce şunlar gerekir: bakım penceresi, üç plan ve en az bir **Uygulama** görevi.

1. Değişiklik sayfasında **Onaya gönder** düğmesine basın. Durum **Onay bekliyor** olur.
2. Onay verebilecek yöneticilere e-posta gider.
3. Karar gelmeden geri almak için **Geri çek** düğmesine basın. Değişiklik **Taslak** durumuna döner.

Onay kararını şu kişiler veremez: değişikliği oluşturan kişi, değişikliğin sahibi ve planı bir kez bile yazan veya değiştiren herkes. Onaylayabilecek başka yönetici yoksa sayfada **Onaylayabilecek başka yönetici yok** yazar. Bu durumda başka bir yöneticinin tanımlanması gerekir.

Karar verilince oluşturan kişiye ve sahibe e-posta gider. **Reddet** veya **Düzeltmeye gönder** kararında değişiklik gerekçeyle birlikte **Taslak** durumuna döner.

### Onaydan sonra düzenleme

**Planlandı** durumunda pencereyi, riski, etkiyi, planları, varlıkları veya uygulama görevlerini değiştirirseniz onay geçersiz olur. Kaydetmeden önce **Onay geçersiz olacak** uyarısı çıkar. **Yine de kaydet** ile kaydederseniz değişiklik **Onay bekliyor** durumuna döner. Onayı veren yöneticiye e-posta gider. Gerekçe alanında **Plan değişti** yazar.

Başlık, gerekçe ve kapanış notu değişince onay geçersiz olmaz.

**Uygulanıyor** durumundan sonra plan alanları değiştirilemez.

### Uygulama ve kapatma

1. Bakım penceresi gelince **Uygulamaya başla** düğmesine basın. **Uygulama** görevleri çalışabilir hale gelir.
2. Uygulama görevleri tamamlanınca **Uygulama bitti** düğmesine basın. Değişiklik **Doğrulama** durumuna geçer.
3. **Doğrulama** görevlerini tamamlayın.
4. **Kapat** düğmesine basın, sonucu ve notları girin.

Sonuçların kuralları:

- **Başarılı**: Değişiklik **Doğrulama** durumunda olmalı. Açık uygulama görevi kalmamalı ve en az bir uygulama görevi tamamlanmış olmalı. Tüm doğrulama görevleri tamamlanmış olmalı. Doğrulama notu gerekir. Gerçek bitiş, başlangıçtan önce olamaz. Açık geri dönüş görevleri **Gerekmedi** gerekçesiyle iptal edilir.
- **Geri alındı** veya **Başarısız**: Önce **Geri dönüşü başlat** gerekir. En az bir geri dönüş görevi olmalı ve hepsi kapanmış olmalı. Geri dönüş notu gerekir.

Geri dönüş başlayınca geri dönüş görevlerinin atananlarına e-posta gider. Görev atanmamışsa ekibin aktif üyelerine gider.

**Taslak**, **Onay bekliyor** veya **Planlandı** durumundaki bir değişikliği **İptal et** ile iptal edebilirsiniz. Değişikliğin açık görevleri aynı nedenle iptal olur.

### Pencere çakışması

İki değişikliğin bakım pencereleri üst üste biniyorsa ve ikisi de aynı varlığı ya da aynı hizmeti etkiliyorsa değişiklik sayfasında "Bakım penceresi 1 değişiklikle çakışıyor" gibi bir uyarı çıkar (sayı çakışan değişiklik sayısıdır). Uyarıda her çakışan değişikliğin penceresi ve nedeni yazar: **Ortak varlık** veya **Aynı hizmet**. İki neden birlikte varsa **Ortak varlık** gösterilir. Biri bitince diğeri başlıyorsa pencereler çakışmış sayılmaz.

- Uyarı yalnızca bilgi verir; onaya göndermeyi, onaylamayı veya planlamayı engellemez.
- Çakışma yalnızca **Onay bekliyor**, **Planlandı** ve **Uygulanıyor** durumundaki değişikliklerle aranır.
- Görme yetkiniz olmayan bir değişiklikle çakışma varsa numarası ve başlığı yerine **Görmediğiniz bir değişiklik** yazar.
- Yalnızca çalışma özetini gören kişiler çakışma uyarısını görmez.

### Bakım takvimi

Değişiklik listesindeki **Bakım takvimi** düğmesi takvimi açar.

1. **Ay** veya **Hafta** görünümünü seçin. Telefonda takvim yerine gün gün bir liste gösterilir.
2. **Önceki**, **Sonraki** ve **Bugün** düğmeleriyle gezinin.
3. Bir değişikliğe basarak sayfasını açın.

Takvimde yetkinizin olduğu değişiklikler gösterilir; **Taslak** ve **İptal edildi** durumundakiler gösterilmez. Penceresi başka bir değişiklikle çakışan değişikliğin yanında **Çakışma** işareti olur. İşaret yalnızca ayrıntısını tam gördüğünüz değişikliklerde çıkar. Seçilen aralıktaki değişiklikler yüklenirken bir yükleniyor göstergesi görünür. Takvim en fazla 62 günlük aralık gösterir.

### İlgili kayıtlar

Değişiklik sayfasındaki **İlgili kayıtlar** kartı değişikliğe bağlı arıza ve talepleri gösterir. Kayıtlar iki grupta görünür: **Bu değişikliğe yol açan kayıtlar** ve **Bu değişikliğin yol açtığı olaylar**.

1. **Kayıt bağla** düğmesine basın.
2. **Kayıt numarası** alanına kaydın numarasını yazın.
3. **Bağlantı türü** seçin: **Değişikliğe yol açtı** (kayıt bu değişikliği gerektirdi) veya **Değişiklik sonucu oluştu** (değişiklik bu arızaya yol açtı; yalnızca arızalar için).
4. **Bağla** düğmesine basın.

Bir bağlantıyı kaldırmak için satırdaki **Kaldır** düğmesini kullanın. Bağlama ve kaldırma işlemini yöneticiler ve değişikliğin sahibi ekibin üyeleri yapar. İptal edilmiş bir değişikliğe yeni kayıt bağlanmaz, ama yanlış bir bağlantı kaldırılabilir.

Açma yetkiniz olmayan bir kaydın numarası, başlığı, türü ve durumu görünür; satırda "... ekibinin kaydı" yazar ve kayıt açılmaz. Aynı bağlantı, arıza veya talep sayfasında **İlgili değişiklikler** kartında da görünür. Çalışanlar bu kartları görmez.

### Değişiklik listesi

**Kuyruk** filtresiyle listeyi seçin: **Benim değişikliklerim**, **Ekibimin değişiklikleri**, **Onay bekleyenler**, **Açık değişiklikler**, **Bu hafta uygulanacak**. **Durum** ve **Risk** filtreleri ile **Numara veya başlıkta ara** kutusu listeyi daraltır. **İş listem** sayfasındaki **Değişikliklerim** kartı sahibi olduğunuz değişiklikleri, **Bu hafta uygulanacak** kartı bu hafta başlayacak olanları açar. Size veya ekibinize düşen değişiklikler İş listem'de risk ve pencereyle birlikte görünür.

## İpuçları

- Bir pencere açıkken başka biri kaydı değiştirirse kaydetme sırasında uyarı çıkar ve pencere kapanır. Sayfa güncel bilgiyle yenilenir; işlemi yeniden açın.
- Aynı değişikliği iki yönetici aynı anda karara bağlarsa yalnızca ilk karar geçerli olur.
