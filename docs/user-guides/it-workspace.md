# IT iş listesi

Bu sayfa IT personeli ve yöneticiler içindir: size ve ekiplerinize düşen kayıtları bulmak, üstlenmek ve çözmek.

## İş listem

Menüden **İş listem** sayfasını açın. Her kart bir kuyruğu ve içindeki kayıt sayısını gösterir:

| Kuyruk | İçindekiler |
|---|---|
| **Bana atanan** | Size atanmış kayıtlar. |
| **Ekiplerim** | Üyesi olduğunuz ekiplerin kayıtları. |
| **Atanmamış** | Ekiplerinizde henüz kimsenin üstlenmediği kayıtlar. |
| **Beklemede** | Ekiplerinizde beklemeye alınmış kayıtlar. |
| **SLA Riski** | SLA hedefi yaklaşan veya geçmiş açık kayıtlar. Önce bunlara bakın. |

![İş listem](images/workspace-desktop.png)

Bir karta basınca o kuyruk açılır. Ana sayfadaki **İş kuyruklarım** bölümü de aynı kartlardan üçünü gösterir.

## Kuyruk

Kuyrukta kayıtlar tablo halinde görünür: numara, konu, durum, öncelik, atanan kişi, SLA ve son işlem zamanı.

- **Numara veya konu ara**, **Durum** ve **Öncelik** ile listeyi daraltın.
- Kaydı açmak için **Aç** düğmesine basın.
- Kayıttan sayfa yolundaki **Kuyruk** adımıyla veya **Kayıtlara dön** düğmesiyle aynı filtrelerle listeye dönersiniz.

![Ekiplerim kuyruğu](images/workspace-queue-desktop.png)

SLA sütununda iki ölçü vardır: **İlk yanıt** ve **Çözüm**. "Kalan 4 sa 0 dk" hedefe kalan mesai süresini, "Risk" hedefin yaklaştığını, "İhlal" hedefin geçtiğini, "Duraklatıldı" sürenin beklemede durduğunu gösterir.

## Kayıt üzerinde çalışma

Kaydı açınca sağda **İşlemler** kartı görünür. Kartta yalnızca kaydın o anki durumunda yapılabilecek işlemler çıkar.

![Bir kaydın işlemleri](images/ticket-work-desktop.png)

| İşlem | Ne yapar |
|---|---|
| **Üstlen** | Kaydı kendinize atar. Atanmamış kayıtlarda görünür. |
| **Başla** | **Açık** kaydı **İşlemde** yapar. |
| **Beklet** | Kaydı **Beklemede** yapar. **Bekleme nedeni** seçin; **Diğer** seçerseniz **Not** yazmak zorunludur. |
| **Devam et** | Bekleyen kaydı tekrar çalışmaya alır. Yönetici onayı bekleyen taleplerde görünmez; onları onay açar. |
| **Tamamla** | Kaydı kapatır (aşağıya bakın). |
| **Öncelik değiştir** | **Etki** ve **Aciliyet** seçin; öncelik bunlardan hesaplanır. |
| **Aktar** | Kaydı başka ekibe veya kişiye verir. **Hedef ekip**, **Atanacak kişi** (ya da **Ekipte kimse**) ve **Aktarma nedeni** seçin. |
| **İptal et** | Kaydı iptal eder. **Neden** yazmak zorunludur. |
| **Onayı geçersiz kıl** | Onaylanmış talebin onayını iptal eder; talep yeniden onaya düşer. |

Beklemeye almak bazı nedenlerde SLA süresini durdurur (hangi nedenlerin durdurduğunu yönetici belirler). Aktarma SLA süresini sıfırlamaz.

## Kaydı tamamlama

1. **Tamamla** düğmesine basın.
2. **Çözüm (kullanıcı görür)** alanına ne yaptığınızı kullanıcının anlayacağı dille yazın.
3. Arızalarda **Sonuç** seçin: **Çözüldü** ya da **Hizmet talebi gerekli**.
4. İsterseniz **Kök neden (yalnızca IT görür)** alanını doldurun. Çalışan bunu görmez.
5. **Onayla** düğmesine basın.

![Tamamlama penceresi](images/ticket-complete-desktop.png)

Kurallar:

- **Hizmet talebi gerekli** seçmek için önce kayda bağlı bir talep olmalıdır (**İlgili talep oluştur**).
- Onay gerektiren bir talep, onaylanmadan tamamlanamaz.
- Tamamlanan kayıt yeniden açılmaz; yazışma eklenebilir.

## Kullanıcıya yanıt ve iç not

Kayıt sayfasındaki **Yazışmalar** bölümünde iki sekme vardır:

- **Yanıt**: Kullanıcı görür ve e-posta alır. Kayda yazdığınız ilk yanıt **İlk yanıt** SLA süresini durdurur.
- **İç not**: Yalnızca IT personeli görür. Sarı zeminle gösterilir.

Mesajı **Mesajınız** alanına yazın, gerekirse **Dosya ekle** ile dosya ekleyin ve **Gönder** düğmesine basın. **Makale ekle** ile bilgi bankasından bir makalenin bağlantısını mesaja ekleyebilirsiniz; kullanıcıya giden yanıta yalnızca çalışanlara açık makaleler eklenebilir.

## Cihaz, ilgili talep ve makale bağlama

- **Bağlı cihazlar** kartında **Cihaz bağla** ile envanterden bir cihazı kayda bağlayın (**Demirbaş no ile ara**). **Ayır** bağlantıyı kaldırır.
- **İlgili kayıtlar** kartında **İlgili talep oluştur**, bir arızadan yeni bir talep açar; örneğin onarılamayan bir bilgisayar için "Yeni bilgisayar" talebi. Yeni talep arızaya bağlı görünür.
- **İlgili makaleler** kartında **Makale bağla** ile çözümü anlatan makaleyi kayda bağlayın.

## İpuçları

- Güne **SLA Riski** kuyruğuyla başlayın.
- Kullanıcıdan bilgi beklerken kaydı **Beklet** ile **Kullanıcıdan yanıt bekleniyor** nedeniyle bekletin; kullanıcı yanıt yazınca **Devam et** ile sürdürün.
- Başka biri kaydı siz düzenlerken değiştirdiyse "Bu kayıt siz düzenlerken başka biri tarafından değiştirildi" uyarısı çıkar. Sayfayı yenileyip tekrar deneyin.
