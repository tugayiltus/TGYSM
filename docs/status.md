# TGYSM — durum özeti

Biten faz ve işlerin tek sayfalık özeti. Ayrıntı her satırdaki plan, spec ve ADR'de; planlar yerinde kalır. Faz tablosu: `tgysm.md` (Faz 0–6 = ilk sürüm).

Güncel: 2026-11-13.

## Biten fazlar (ilk sürüm)

| Faz | İçerik | Plan | ADR | Sonuç |
|---|---|---|---|---|
| 0 — Tasarım | Akışlar, rol matrisi, şema, ADR'ler | `docs/superpowers/plans/2026-09-26-phase-0-1-foundation.md` | 0001–0008 | Tamam |
| 1 — Temel | Katmanlar, DB/migration, ilk yönetici CLI, giriş, `/admin`, kurum/ekip, audit | aynı plan (16 görev) | 0001–0008 | Tamam |
| 2 — Varlık | Envanter, CSV içe aktarma, zimmet, teslim/iade, geçmiş | `docs/superpowers/plans/2026-09-26-phase-2-3-assets-and-tickets.md` (20 görev) | 0010 | Tamam |
| 3 — Destek | Arıza/talep, atama, yorum/ek, onay, varlık ilişkisi, kapanış | aynı plan | 0009, 0011–0013 | Tamam |
| 4 — SLA/E-posta | Worker, outbox, e-posta şablonları, parola linkleri, iş takvimi, SLA | `docs/superpowers/plans/2026-09-27-phase-4-sla-email.md` (15 görev) | 0014–0018 | Tamam |
| 5 — Bilgi/Rapor | Bilgi bankası, raporlar, drill-down, Excel (anlık + arka plan) | `docs/superpowers/plans/2026-10-02-phase-5-knowledge-reports.md` (14 görev) | 0019–0020 | Tamam |
| 6 — İlk sürüm | Docker Compose, yedek/geri yükleme provası, kabul kanıtı, pilot veri, performans, CI, rehber, operatör el kitabı | `docs/superpowers/plans/2026-10-09-phase-6-release.md` (18 görev) | 0024–0025 | İlk sürüm onayı `2d5ec6b` (2026-10-10) |

## İlk sürüm sonrası biten işler

| İş | Plan | ADR | Durum |
|---|---|---|---|
| CoreUI arayüz yenileme (sol menü, breadcrumb, cihaz kataloğu, üretilen etiket, Chart.js) | `docs/superpowers/plans/2026-10-09-coreui-ui-ux.md` (18 görev) | 0021–0023 | `master`; arayüz katmanı sonra Astryx ile değişti |
| Arayüz bütünlüğü (renk modları, kart düzeni) | `docs/superpowers/plans/2026-10-09-ui-cohesion.md` (11 görev) | 0026 | `master`; Astryx ile yerini aldı |
| Astryx geçişi (CoreUI ve Sass kaldırıldı) | `docs/superpowers/plans/2026-11-10-astryx-migration.md` (13 görev) | 0027 | `master` (`3ee50f7`) |
| Rapor filtreleri, Tür → Marka → Model akışı, QR etiket (yazdırma, toplu, tarayarak arıza) | `docs/superpowers/plans/2026-11-11-reports-catalog-qr.md` (11 görev) | 0028 | `master` (`4d70063`) |
| Model listesinde tür filtresi, Tür – Marka – Ad sırası | — | — | `master` (`6be4532`) |
| Yıllık varlık etiketi `ASSET-YYYY-NNNN` | — | 0029 | `master` (`d43ad8c`) |
| Giriş yöntemi panelden: Yerel, Microsoft Entra ID, Active Directory (LDAP) | `docs/superpowers/plans/2026-11-12-sso-ldap-login.md` (9 görev) | 0030 | `master` (`9a7a0b3`) |

## Görev ve Değişiklik Yönetimi

| Faz | İçerik | Plan | ADR | Durum |
|---|---|---|---|---|
| 7 — Görev | `TSK-` görevler, üstlenme/atama/aktarma/bekletme/tamamlama, geçmiş ve notlar, `task_assigned` e-postası, İş listem kartları ve birleşik liste, kayıtta Görevler bölümü | `docs/superpowers/plans/2026-11-13-phase-7-tasks.md` (9 görev) | 0031 | `master` (`2046892`) |
| 8 — Değişiklik | Normal değişiklik (`CHG-`), durum makinesi, faz görevleri, yönetici onayı, kapanış ve geri dönüş | — | 0032 (planlı) | Sırada |
| 9 — Takvim/Rapor | Bakım takvimi, pencere çakışma uyarısı, "Bu hafta" kartı, değişiklik/görev raporları | — | — | Bekliyor |

## Açık işler

- SSO canlıya almadan önce: gerçek AD ile `TGYSM_TEST_LDAP_*` sözleşme testi; gerçek Entra ve AD ile elle giriş.
- `SECURITY.md` ve kullanıcı rehberi QR, `ASSET-` etiketi, SSO ve görevler için eski; sahibin isteğiyle güncellenir. `README.md` ve `capabilities.md` `2046892` itibarıyla güncel.
- Bekleyen Dependabot PR'ları.
- Sıradaki: Faz 8 Değişiklik Yönetimi (spec `docs/superpowers/specs/2026-11-12-task-and-change-management-design.md`).
