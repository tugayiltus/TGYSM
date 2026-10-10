using Tgysm.Domain.Assets;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Knowledge;

namespace Tgysm.Application.Features.Pilot;

internal sealed record PilotLocation(string Name, string? Parent);

internal sealed record PilotCategory(string Name, string? Parent, CategoryScope Scope);

internal sealed record PilotRequestType(string Name, bool RequiresApproval);

internal sealed record PilotModel(string Brand, AssetType Type, string Name);

internal sealed record PilotIncident(string Category, string Subject, string Description);

internal sealed record PilotRequest(string RequestType, string Category, string Subject, string Description);

internal sealed record PilotArticle(string Category, string Title, string Summary, string Body, ArticleVisibility Visibility);

/// <summary>The fixed reference data and text templates of the pilot seed (Turkish: it is what pilot users read).</summary>
internal static class PilotSeedData
{
    public const string ItDepartment = "Bilgi Teknolojileri";
    public const string IntakeTeam = "Servis Masası";
    public const string FieldTeam = "Saha Destek";
    public const string AdminFirstName = "Pilot";
    public const string AdminLastName = "Yönetici";
    public const string AdminEmailLocalPart = "admin";
    public const string ApproverEmailPrefix = "onay";
    public const string ItStaffEmailPrefix = "it";
    public const string EmployeeEmailPrefix = "personel";
    public const string SerialPrefix = "PLT";
    public const string LostReason = "Envanter sayımında bulunamadı.";
    public const string RetireReason = "Ekonomik ömrünü tamamladı.";
    public const string RejectReason = "Bütçe bu çeyrekte uygun değil.";
    public const string PriorityReason = "Etki ve aciliyet kullanıcıyla görüşülerek güncellendi.";
    public const string CancelReason = "Kullanıcı talebin artık gerekmediğini bildirdi.";
    public const string RootCause = "Yapılandırma hatası.";

    public static readonly string[] Departments = [ItDepartment, "İnsan Kaynakları", "Muhasebe ve Finans", "Satış", "Üretim", "Lojistik"];

    public static readonly PilotLocation[] Locations =
    [
        new("Merkez Ofis", null),
        new("Merkez Ofis / Zemin Kat", "Merkez Ofis"),
        new("Merkez Ofis / 1. Kat", "Merkez Ofis"),
        new("Merkez Ofis / 2. Kat", "Merkez Ofis"),
        new("Fabrika", null),
        new("Fabrika / Üretim Hattı", "Fabrika"),
        new("Fabrika / Depo", "Fabrika"),
        new("Ankara Bölge Ofisi", null),
    ];

    public static readonly PilotCategory[] Categories =
    [
        new("Donanım", null, CategoryScope.Both),
        new("Bilgisayar", "Donanım", CategoryScope.Both),
        new("Yazıcı ve tarayıcı", "Donanım", CategoryScope.Both),
        new("Telefon ve tablet", "Donanım", CategoryScope.Both),
        new("Yazılım", null, CategoryScope.Both),
        new("Ofis uygulamaları", "Yazılım", CategoryScope.Both),
        new("İş uygulamaları", "Yazılım", CategoryScope.Both),
        new("Ağ ve erişim", null, CategoryScope.Both),
        new("İnternet ve Wi-Fi", "Ağ ve erişim", CategoryScope.Incident),
        new("VPN", "Ağ ve erişim", CategoryScope.Both),
        new("Klasör ve yetki", "Ağ ve erişim", CategoryScope.Request),
        new("E-posta", null, CategoryScope.Incident),
        new(PilotSeed.PilotCategoryName, null, CategoryScope.Incident),
    ];

    public static readonly PilotRequestType[] RequestTypes =
    [
        new("Yeni bilgisayar", true),
        new("Yazılım lisansı", true),
        new("Klasör erişimi", true),
        new("Yeni telefon hattı", false),
        new("Sarf malzeme", false),
    ];

    public static readonly PilotModel[] Models =
    [
        new("Dell", AssetType.Laptop, "Latitude 5440"),
        new("Dell", AssetType.Laptop, "Latitude 7440"),
        new("Dell", AssetType.Desktop, "OptiPlex 7010"),
        new("HP", AssetType.Laptop, "EliteBook 840 G10"),
        new("HP", AssetType.Laptop, "ProBook 450 G10"),
        new("HP", AssetType.Desktop, "EliteDesk 800 G9"),
        new("Lenovo", AssetType.Laptop, "ThinkPad T14 Gen 4"),
        new("Lenovo", AssetType.Desktop, "ThinkCentre M70q"),
        new("Lenovo", AssetType.Tablet, "Tab P12"),
        new("Apple", AssetType.Laptop, "MacBook Air 13"),
        new("Apple", AssetType.Phone, "iPhone 15"),
        new("Apple", AssetType.Tablet, "iPad 10"),
        new("Samsung", AssetType.Phone, "Galaxy A55"),
        new("Samsung", AssetType.Phone, "Galaxy S24"),
        new("Samsung", AssetType.Tablet, "Galaxy Tab S9"),
    ];

    public static readonly string[] FirstNames =
    [
        "Ayşe", "Mehmet", "Fatma", "Mustafa", "Zeynep", "Ahmet", "Elif", "Emre", "Merve", "Burak",
        "Selin", "Can", "Deniz", "Gökhan", "Hülya", "Kemal", "Leyla", "Murat", "Nur", "Okan",
    ];

    public static readonly string[] LastNames =
    [
        "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Aydın", "Öztürk", "Arslan", "Doğan",
        "Kılıç", "Koç", "Kurt", "Özdemir", "Aslan", "Polat", "Erdoğan", "Güneş",
    ];

    public static readonly PilotIncident[] Incidents =
    [
        new("Bilgisayar", "Bilgisayar çok yavaş açılıyor", "Sabah bilgisayarı açtığımda masaüstünün gelmesi on dakikayı buluyor."),
        new("Bilgisayar", "Dizüstü bilgisayar şarj olmuyor", "Adaptör takılı olduğu halde pil şarj olmuyor, ışık yanmıyor."),
        new("Bilgisayar", "Mavi ekran hatası", "Gün içinde birkaç kez mavi ekran verip yeniden başlıyor."),
        new("Yazıcı ve tarayıcı", "Yazıcı kağıt sıkıştırıyor", "Kat yazıcısı her baskıda kağıt sıkıştırıyor."),
        new("Yazıcı ve tarayıcı", "Tarayıcı e-postaya gönderemiyor", "Taranan belgeler e-posta adresime ulaşmıyor."),
        new("Telefon ve tablet", "Telefonda e-posta senkronize olmuyor", "Şirket telefonunda dünden beri yeni e-posta gelmiyor."),
        new("Ofis uygulamaları", "Excel dosyası açılmıyor", "Paylaşılan bütçe dosyası açılırken uygulama kapanıyor."),
        new("Ofis uygulamaları", "Word belgeleri bozuk görünüyor", "Eski belgelerdeki Türkçe karakterler bozuk çıkıyor."),
        new("İş uygulamaları", "ERP ekranında hata alıyorum", "Sipariş kaydederken 'işlem tamamlanamadı' uyarısı geliyor."),
        new("İnternet ve Wi-Fi", "Toplantı odasında Wi-Fi yok", "Toplantı odasında kablosuz ağa bağlanılamıyor."),
        new("İnternet ve Wi-Fi", "İnternet bağlantısı sık kopuyor", "Bağlantı yarım saatte bir kopup geri geliyor."),
        new("VPN", "Evden VPN bağlantısı kurulamıyor", "VPN istemcisi kimlik doğrulama hatası veriyor."),
        new("E-posta", "Gönderdiğim e-postalar geri dönüyor", "Dış adreslere gönderdiğim e-postalar teslim edilemedi diye dönüyor."),
        new("E-posta", "Posta kutum dolu uyarısı", "Posta kutusu kotası doldu uyarısı alıyorum, e-posta gönderemiyorum."),
    ];

    public static readonly PilotRequest[] Requests =
    [
        new("Yeni bilgisayar", "Bilgisayar", "Yeni başlayan çalışan için dizüstü bilgisayar", "Pazartesi işe başlayacak çalışanımız için bilgisayar hazırlanmasını rica ederim."),
        new("Yeni bilgisayar", "Bilgisayar", "Eski bilgisayarın yenilenmesi", "Bilgisayarım beş yaşında, yenisiyle değiştirilmesini talep ediyorum."),
        new("Yazılım lisansı", "İş uygulamaları", "Çizim programı lisansı", "Teknik çizimler için çizim programı lisansına ihtiyacım var."),
        new("Yazılım lisansı", "Ofis uygulamaları", "Ofis paketi lisansı", "Yeni bilgisayarıma ofis paketi kurulmasını istiyorum."),
        new("Klasör erişimi", "Klasör ve yetki", "Muhasebe ortak klasörüne erişim", "Ay sonu kapanışı için muhasebe ortak klasörüne okuma yetkisi rica ederim."),
        new("Klasör erişimi", "Klasör ve yetki", "Proje klasörüne yazma yetkisi", "Yeni projede çalıştığım için proje klasörüne yazma yetkisi gerekiyor."),
        new("Yeni telefon hattı", "Telefon ve tablet", "Saha ekibi için telefon hattı", "Saha ekibine katılan arkadaşımız için hat ve cihaz talep ediyoruz."),
        new("Sarf malzeme", "Yazıcı ve tarayıcı", "Toner talebi", "Kat yazıcısının toneri bitmek üzere."),
    ];

    public static readonly string[] RequesterReplies =
    [
        "Bilgi için teşekkürler, bekliyorum.",
        "Sorun hâlâ devam ediyor, ekran görüntüsünü ekledim.",
        "Öğleden sonra masamda olacağım.",
        "Hızlı dönüşünüz için teşekkür ederim.",
    ];

    public static readonly string[] InternalNotes =
    [
        "Kullanıcıyla telefonda görüşüldü, uzaktan bağlanılacak.",
        "Benzer kayıt geçen ay da açılmıştı, kök nedene bakılmalı.",
        "Tedarikçiden parça bekleniyor.",
        "Sürücü güncellemesi denenecek.",
    ];

    public static readonly string[] WorkerReplies =
    [
        "Kaydınızı aldık, en kısa sürede ilgileneceğiz.",
        "Uzaktan bağlanıp kontrol ettim, lütfen tekrar dener misiniz?",
        "Cihazınızı yerinde kontrol etmek için uğrayacağız.",
    ];

    public static readonly string[] HoldNotes =
    [
        "Kullanıcıdan dönüş bekleniyor.",
        "Tedarikçi yanıtı bekleniyor.",
        "Dış servis randevusu bekleniyor.",
    ];

    public static readonly string[] Resolutions =
    [
        "Sürücüler güncellendi, sorun giderildi.",
        "Ayarlar düzeltildi, kullanıcı ile birlikte test edildi.",
        "Arızalı parça değiştirildi.",
        "Talep karşılandı, kullanıcı bilgilendirildi.",
    ];

    public static readonly PilotArticle[] Articles =
    [
        new("VPN", "VPN bağlantısı nasıl kurulur", "Evden şirket ağına güvenli bağlanma adımları.", "1. VPN istemcisini açın.\n2. Şirket e-posta adresinizle giriş yapın.\n3. Doğrulama kodunu girin.", ArticleVisibility.Employees),
        new("E-posta", "Posta kutusu doluysa ne yapmalı", "Kota uyarısında yer açmanın yolları.", "Büyük ekleri silin, eski e-postaları arşiv klasörüne taşıyın ve Silinmiş Öğeler klasörünü boşaltın.", ArticleVisibility.Employees),
        new("Yazıcı ve tarayıcı", "Kağıt sıkışması nasıl giderilir", "Yazıcıda sıkışan kağıdı güvenle çıkarma.", "Yazıcıyı kapatın, arka kapağı açın ve kağıdı yavaşça kendinize doğru çekin.", ArticleVisibility.Employees),
        new("İnternet ve Wi-Fi", "Misafir Wi-Fi ağına bağlanma", "Ziyaretçiler için kablosuz ağ bilgisi.", "Misafir ağına bağlanın ve resepsiyondan aldığınız günlük kodu girin.", ArticleVisibility.Employees),
        new("Bilgisayar", "Bilgisayar yavaşladığında ilk adımlar", "Yavaşlık şikâyetinde kontrol listesi.", "Bilgisayarı yeniden başlatın, gereksiz uygulamaları kapatın, güncellemeleri tamamlayın.", ArticleVisibility.Employees),
        new("Ofis uygulamaları", "Türkçe karakter sorunu", "Belgelerde bozuk Türkçe karakterleri düzeltme.", "Belgeyi açarken kodlama olarak UTF-8 seçin.", ArticleVisibility.Employees),
        new("Telefon ve tablet", "Şirket e-postasını telefona ekleme", "Mobil cihazda e-posta hesabı kurulumu.", "Ayarlar > Hesaplar > Hesap ekle adımlarını izleyin ve şirket adresinizi girin.", ArticleVisibility.Employees),
        new("İş uygulamaları", "ERP oturum zaman aşımı", "ERP ekranında oturum düşmesi.", "Tarayıcı önbelleğini temizleyin ve yeniden giriş yapın.", ArticleVisibility.Employees),
        new("Bilgisayar", "Dizüstü bilgisayar teslim kontrol listesi", "Yeni cihaz hazırlarken izlenecek adımlar.", "Disk şifrelemesini açın, etki alanına alın, standart yazılımları kurun.", ArticleVisibility.Internal),
        new("VPN", "VPN sertifikası yenileme", "Süresi dolan VPN sertifikasının yenilenmesi.", "Sertifika sunucusunda yeni sertifika üretin ve istemcilere dağıtın.", ArticleVisibility.Internal),
        new("E-posta", "Geri dönen e-postaların incelenmesi", "Teslim edilemeyen e-postalarda kontrol adımları.", "Gönderen alan adının SPF ve DKIM kayıtlarını kontrol edin.", ArticleVisibility.Internal),
        new("Yazıcı ve tarayıcı", "Taramayı e-postaya gönderme ayarı", "Tarayıcının e-posta sunucusu ayarları.", "Tarayıcı yönetim panelinde SMTP sunucusunu ve gönderici adresini güncelleyin.", ArticleVisibility.Internal),
    ];
}
