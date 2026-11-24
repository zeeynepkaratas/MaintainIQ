# Faz 7 kontrol listesi

## Bakım planı motoru

- [x] `MaintenancePlanService`: oluşturma, güncelleme, aktif/pasif yapma, sorgular. Planlar silinmez, pasifleştirilir; geçmiş iş emirleri plana bağlı kalır.
- [x] Sonraki vade gün/hafta/ay/yıl periyoduyla hesaplanıyor. Vadeler `AnchorDate + k × periyot` formülüyle bulunduğundan ay sonunda kayma olmuyor (31 Oca → 28 Şub → 31 Mar).
- [x] `GenerateDueWorkOrdersAsync(now)`: vadesi gelmiş ve gecikmiş aktif planlardan `PreventiveWorkOrder` üretiyor. Her plan ayrı transaction'da işleniyor; bir planın hatası diğerlerini durdurmuyor.
- [x] İdempotentlik: kodda `ExistsPreventiveOccurrenceAsync` (UPDLOCK, HOLDLOCK) kontrolü, DB'de `UX_WorkOrder_PreventiveOccurrence`. Eşzamanlı çakışmada 2601/2627 hatası "zaten var" olarak raporlanıyor.
- [x] Gecikmiş planda kaçırılan dönemler için tek iş emri üretiliyor; atlanan dönem sayısı sonuçta raporlanıyor.
- [x] Önleyici iş emri tamamlanınca `LastDoneAt` ve `NextDueDate`, Complete transaction'ının içinde güncelleniyor.
- [ ] Tetikleme (açılışta bir kez, "Şimdi çalıştır" butonu, saatlik WinForms Timer) UI'a bağlanacak, **Faz 8**. Servis tarafı hazır, ayarlar `appsettings.json` içinde `MaintenanceEngine` bölümünde.
- [ ] Çalışma saati tabanlı periyot **ertelendi**: ekipmanda sayaç verisi yok. Takvim periyotları tam çalışıyor.

## KPI

- [x] `KpiService` / `KpiCalculator`: MTBF, MTTR, Availability, PM uyumu, backlog ve yaş dağılımı, ortalama kapanış, planlı oran, toplam maliyet.
- [x] SQL veri setleri: ekipman başına maliyet, en çok parça tüketimi, kritik stok, teknisyen iş yükü, aylık arıza trendi, Pareto.
- [x] Dönem ve kapsam filtresi: tüm tesis, alt ağaç (özyinelemeli CTE) ya da tek ekipman.
- [x] Pencere fonksiyonları: `RANK`, kümülatif `SUM OVER`, hareketli `AVG OVER`.
- [x] En ağır sorgunun yürütme planı incelendi ve indeks eklendi. Ayrıntı [kpi.md](kpi.md) içinde: 6172 → 31 ve 6775 → 61 mantıksal okuma.
- [x] Formül belgesi: [docs/kpi.md](kpi.md).

## Advisor

- [x] `IMaintenanceAdvisor.GetRecommendationsAsync(scope)`; `Recommendation` kayıtları ciddiyet, başlık, açıklama, kanıt ve önerilen eylem içeriyor.
- [x] `RuleBasedAdvisor`, R1-R7 kurallarını çalıştırıyor. Eşikler `AdvisorOptions` / `appsettings.json` içinde `Advisor` bölümünden geliyor.
- [x] Her önerinin kanıt listesi var. Advisor veri yazmıyor, yalnızca öneriyor.
- [x] `AdvisorFactory`: `Mode = Rules | Llm`. Anahtar ya da istemci yoksa kural moduna düşüyor; `LlmAdvisor` hata alırsa kural sonuçlarını döndürüyor.

## Dosya ve QR

- [x] `AttachmentService`: `App_Data/attachments` + DB meta verisi. Uzantı, dosya imzası, gerçek görüntü biçimi, boyut (5 MB) ve piksel sınırı doğrulanıyor. 200 px PNG küçük resim üretiliyor. DB hatasında yazılan dosyalar siliniyor; kayıt silinince dosyalar temizleniyor.
- [x] `QrService` (QRCoder): `MAINTAINIQ:EQ:<QrToken>` payload'ı, PNG üretimi, etiket (QR PNG + kod ve ad içeren yazdırılabilir SVG), çözümleme ve ekipmana eşleme. Token yoksa atanıp audit'e yazılıyor.

## Veritabanı

- [x] `V004`: `Equipment.IsCritical`, `Equipment.WarrantyEndDate`, `MaintenancePlan.AnchorDate`, `MaintenancePlan.LastDoneAt` ve iki KPI indeksi. Tekrar çalıştırılabilir; `db/verify.sql` 4 göç bekliyor.

## Test ve kalite

- [x] 210 CI uyumlu birim testi ve 12 SQL Server entegrasyon testi başarılı (toplam 222).
- [x] Plan motoru: idempotentlik (birim + gerçek SQL), gecikmiş plan, ay sonu ve artık yıl, pasif plan.
- [x] KPI: elle hesaplanmış örnekle birebir eşleşme; sıfır bölme koruması; dönem içi gözlem.
- [x] Advisor: her kural için tetikleyen ve tetiklemeyen veri, eşik değişimi, LLM geri düşüşü.
- [x] QR: üret → ZXing ile çöz → ayrıştır gidiş-dönüş testi.
- [x] Satır kapsamı: Domain %96,55, Services %94,39.

## Bağımlılıklar

| Paket | Sürüm | Lisans | Not |
|---|---|---|---|
| QRCoder | 1.8.1 | MIT | 1.9.0 çok yeni olduğu için bir önceki sürüm seçildi |
| SkiaSharp (+ Linux NativeAssets, yalnızca testte) | 4.153.2 | MIT | ImageSharp 3.1 bilinen açıklar içerdiği ve 3.2+ lisans anahtarı istediği için tercih edilmedi |
| ZXing.Net (yalnızca test) | 0.16.11 | Apache-2.0 | QR çözümleme testi |

## Çalıştırma

```powershell
powershell -ExecutionPolicy Bypass -File db\deploy.ps1
dotnet test tests\MaintainIQ.Tests\MaintainIQ.Tests.csproj --filter "Category!=SqlServerIntegration"
dotnet test tests\MaintainIQ.Tests\MaintainIQ.Tests.csproj --filter "Category=SqlServerIntegration"
```

Sıradaki adım Faz 8: UI iskeleti, ekipman ve arıza ekranları, plan motoru tetikleyicisinin bağlanması.
