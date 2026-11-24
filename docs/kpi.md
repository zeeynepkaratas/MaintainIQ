# KPI formülleri (Ek E uygulaması)

KPI'lar iki katmanda hesaplanır:

- **SQL (`ReportQueries`)**: kapsamı özyinelemeli CTE ile `@Scope` tablo değişkenine alır (tüm tesis / alt ağaç / tek ekipman) ve ham olguları (arıza, iş emri, parça maliyeti) getirir. Sıralama ve kümülatif veri setleri pencere fonksiyonlarıyla SQL'de üretilir.
- **C# (`KpiCalculator`)**: Ek E formüllerinin tek ve saf uygulaması. Elle hesaplanmış örnekle birim testine kilitlidir (`KpiCalculatorTests`).

## Gözlem penceresi

Dönem `[Başlangıç, Bitiş)` aralığıdır. Dönem henüz bitmediyse hesaplar **şimdiye kadar** yapılır (`ObservedUntil = min(Bitiş, şimdi)`). Toplam çalışma süresi = gözlem saati × kapsamdaki ekipman sayısı.

## Tanımlar

| KPI | Formül | Sınır durumu |
|---|---|---|
| Duruş süresi | Arıza bildirimi → düzeltici iş emrinin `CompletedAt` zamanı. İş emri yoksa arızanın `ResolvedAt` zamanı, o da yoksa "şimdi". Dönem sınırlarına kırpılır. Aynı ekipmandaki çakışan duruşlar birleştirilir. | — |
| MTTR | Σ(`CompletedAt` − `ActualStartAt`) / dönemde tamamlanan düzeltici iş emri sayısı (saat) | İş emri yoksa `null` ("veri yok") |
| MTBF | (Toplam çalışma süresi − Σ duruş) / dönemde bildirilen arıza sayısı | Arıza yoksa `null` + `IsFailureFree = true` ("arızasız") |
| Availability | (Toplam çalışma süresi − Σ duruş) / Toplam çalışma süresi × 100 | Kapsamda ekipman yoksa `null` |
| PM uyum oranı | Vadesinde (vade + tolerans günü içinde) tamamlanan önleyici iş emri / değerlendirilebilir önleyici iş emri × 100 | İptaller hariç. Tolerans penceresi henüz kapanmamış ve tamamlanmamış iş emri sayılmaz. Payda 0 ise `null`. Tolerans: `KpiOptions.PmToleranceDays` (varsayılan 3). |
| Backlog | Created / Assigned / InProgress / WaitingForParts durumundaki iş emri sayısı | Yaş dağılımı: 0-7, 8-30, 30+ gün |
| Ortalama kapanış süresi | Σ(`ClosedAt` − `CreatedAt`) / dönemde kapatılan iş emri sayısı (saat) | Kapanış yoksa `null` |
| Planlı/düzeltici oran | Dönemde oluşturulan önleyici iş emri / dönemde oluşturulan toplam iş emri × 100 | İş emri yoksa `null` |
| Ekipman maliyeti | Σ(parça miktarı × anlık birim maliyet) + işçilik dakikası / 60 × saatlik ücret; yalnızca dönemde **kapatılan** iş emirleri | Saatlik ücret: `KpiOptions.HourlyLaborRate` |

### SQL veri setleri

| Veri seti | Teknik |
|---|---|
| Arıza nedeni Pareto | `SUM() OVER ()` ile pay, `SUM() OVER (ORDER BY … ROWS UNBOUNDED PRECEDING)` ile kümülatif yüzde |
| Aylık arıza trendi | Ay takvimi özyinelemeli CTE ile üretilir (arızasız aylar 0), `AVG() OVER (ROWS 2 PRECEDING)` ile 3 aylık hareketli ortalama |
| En çok parça tüketimi | `RANK() OVER (ORDER BY TotalQuantity DESC)` |
| Ekipman başına maliyet | Kapsam ekipmanları + `OUTER APPLY` parça maliyeti |
| Teknisyen iş yükü | Açık iş emri, dönemde tamamlanan iş emri ve işçilik saati |
| Kritik stok | `StockQuantity < MinimumStock` olan parça sayısı |

## Örnek hesap (testle birebir aynı)

Dönem 01.11.2026 – 01.12.2026 (720 saat), 2 ekipman → 1440 ekipman-saat. Hesap tarihi 05.12.2026.

| Olgu | Değer |
|---|---|
| F1 (Ekipman 1) | 02.11 08:00 → iş emri bitişi 12:00 = 4 saat |
| F2 (Ekipman 1) | 10.11 10:00 → 16:00 = 6 saat |
| F3 (Ekipman 2) | 20.11 00:00 → iş emri yok, çözüm 21.11 00:00 = 24 saat |
| F4 (Ekipman 2) | 31.10 22:00 bildirildi (dönem dışı) → 01.11 02:00; dönem içi 2 saat |

- Σ duruş = 4 + 6 + 24 + 2 = **36 saat**; dönemde bildirilen arıza = 3 (F4 hariç)
- MTBF = (1440 − 36) / 3 = **468 saat**
- Availability = 1404 / 1440 = **%97,50**
- MTTR = (3 + 5 + 2) / 3 = **3,33 saat**
- PM: P1 zamanında, P2 geç, P3 tamamlanmadı ve tolerans geçti, P4 iptal → 1 / 3 = **%33,33**
- Ortalama kapanış = (24 + 48) / 2 = **36 saat**; planlı oran = 4 / 7 = **%57,14**
- Maliyet = (100 + 1 sa × 500) + (250 + 1,5 sa × 500) = **1600 TRY**

## Yürütme planı incelemesi (7.6)

En ağır sorgu `GetKpiFactsAsync`: kapsamdaki arıza ve iş emri olguları. İnceleme yerel SQL Server Express'te, transaction içinde üretilip sonra geri alınan sentetik veriyle yapıldı: 300 ekipman, 60.000 arıza ve 60.000 iş emri. Kapsam 11 ekipmanlık bir alt ağaç, dönem 3 ay. `SET STATISTICS IO` ile ölçülen mantıksal okumalar:

| Sorgu | V004 indeksleri yok | V004 indeksleri var |
|---|---|---|
| Arıza olguları (`Failure`) | 6172 | 31 |
| İş emri olguları (`WorkOrder`) | 6775 | 61 |

- `IX_Failure_Equipment_ReportedAt (EquipmentId, ReportedAt) INCLUDE (CauseId, Severity, Status, ResolvedAt)`: ilk ölçümde `ResolvedAt` INCLUDE listesinde yoktu. Optimizer key lookup yerine tam tarama seçti (388 okuma). Kolon eklenince indeks seek'e döndü.
- `IX_WorkOrder_Equipment_CreatedAt`: iş emri taraması kapsam başına seek'e döndü (6775 → 61).
- Kalan maliyet: arıza sorgusundaki `LEFT JOIN WorkOrder ON FailureId` iş emri tablosunu hash join ile tarıyor (yaklaşık 900 okuma). 60 bin satırda kabul edilebilir. Veri büyürse `UX_WorkOrder_FailureId` filtreli indeksine `CompletedAt` INCLUDE olarak eklenebilir.
