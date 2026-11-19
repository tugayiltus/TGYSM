namespace Tgysm.Application.Features.Pilot;

/// <summary>Text of the seeded changes and work tasks (Turkish, as the pilot users write it).</summary>
internal static class PilotSeedChangeData
{
    public static readonly string[] ChangeTitles =
    [
        "Çekirdek anahtar yazılım yükseltmesi",
        "Dosya sunucusu disk genişletme",
        "Mail sunucusu güvenlik yaması",
        "Yedekleme sistemi sürüm geçişi",
        "VPN geçidi sertifika yenileme",
        "Yazıcı sunucusu taşıma",
        "Veritabanı sunucusu bakımı",
        "Wi-Fi denetleyici yazılım güncellemesi",
    ];

    /// <summary>Few services on purpose, so changes planned at the same time often touch the same one.</summary>
    public static readonly string[] AffectedServices = ["E-posta", "Dosya paylaşımı", "VPN", "ERP"];

    public const string ChangeJustification = "Üretici güvenlik ve kararlılık güncellemesi yayımladı.";
    public const string ImplementationPlan = "Bakım penceresinde kullanıcılar bilgilendirilir, güncelleme uygulanır.";
    public const string VerificationPlan = "Servis erişimi ve temel işlemler kontrol edilir.";
    public const string BackoutPlan = "Önceki sürüm yedeğinden geri dönülür.";
    public const string ImplementationTaskTitle = "Güncellemeyi uygula";
    public const string VerificationTaskTitle = "Servisi doğrula";

    public static readonly string[] TaskTitles =
    [
        "Yeni çalışan bilgisayarını hazırla",
        "Eski cihazları depoya teslim al",
        "Ağ kablolamasını kontrol et",
        "Lisans listesini güncelle",
        "Yedek dönüş testini yap",
        "Sunucu odası sıcaklığını denetle",
        "Telefon hatlarını yeniden eşle",
        "Antivirüs politikasını gözden geçir",
    ];

    public const string TaskDescription = "Pilot dönemi rutin iş.";
    public const string TaskResultNote = "İş tamamlandı, kayıt güncellendi.";
    public const string TaskCancelReason = "İhtiyaç kalmadı.";
}
