# Faz 6 kontrol listesi

## Servis altyapısı

- [x] Servisler arayüzlerle tanımlandı ve constructor injection ile kuruluyor.
- [x] `CurrentUserContext` kullanıcı, kişi kimliği ve rolü taşıyor.
- [x] `IServiceDataSessionFactory` her kullanım senaryosu için ortak Unit of Work ve repository seti üretiyor.
- [x] Transaction sınırı servis metodunda; repository transaction açmıyor.
- [x] Beklenen domain, yetki, kimlik doğrulama ve bulunamadı hataları anlamlı exception'larla taşınıyor.

## Kimlik doğrulama ve yetki

- [x] Giriş, çıkış ve parola değiştirme akışları hazır.
- [x] PBKDF2-SHA256, 120.000 yineleme, 16 bayt rastgele tuz ve sabit zamanlı karşılaştırma kullanılıyor.
- [x] Beş hatalı girişte on dakikalık hesap kilidi uygulanıyor.
- [x] `IPermissionService`, rol-yetki matrisini servis katmanında zorluyor.
- [x] Yetkisiz işlem repository'ye ulaşmadan reddediliyor.

## Kullanım servisleri

- [x] EquipmentService: oluşturma, güncelleme, taşıma, soft delete, durum, ağaç ve arama.
- [x] FailureService: bildirme, kabul, çözme ve arızadan düzeltici iş emri üretme.
- [x] Kritik arıza ekipmanı Faulty yapıyor ve iş emri önceliğini en az High seviyesine yükseltiyor.
- [x] WorkOrderService: atama, başlatma, parça bekleme/ekleme, devam, tamamlama, kapatma, yeniden açma ve iptal.
- [x] InventoryService: stok girişi, gerekçeli düzeltme, hareket ve minimum stok sorgusu.
- [x] TechnicianService: oluşturma, güncelleme, pasifleştirme, iş yükü ve uygunluk.
- [x] Kritik işlemler eski/yeni değerlerle JSON audit kaydı üretiyor.

## Complete transaction

- [x] Parçalar kimlik sırasıyla `UPDLOCK, ROWLOCK` kullanılarak kilitleniyor.
- [x] Stok, hareket, iş emri, durum geçmişi, ekipman ve audit tek transaction'da yazılıyor.
- [x] Audit hatasında bütün iş değişiklikleri rollback oluyor.
- [x] Eşzamanlı iki tüketimde stok negatife inmiyor; yalnızca bir iş emri tamamlanıyor.
- [x] UI olmadan tam iş emri yaşam döngüsü gerçek SQL Server üzerinde çalışıyor.

## Test ve kalite

- [x] 122 CI uyumlu birim testi başarılı.
- [x] 8 SQL Server entegrasyon testi başarılı.
- [x] Domain satır kapsamı yüzde 95'in üzerinde.
- [x] Services satır kapsamı yüzde 90'ın üzerinde.
- [x] CI Domain yüzde 85 ve Services yüzde 70 eşiklerini ayrı ayrı zorluyor.
- [x] Release test grafiği uyarıları hata kabul ederek derleniyor.

## Çalıştırma

```powershell
dotnet test tests\MaintainIQ.Tests\MaintainIQ.Tests.csproj --filter "Category!=SqlServerIntegration"
dotnet test tests\MaintainIQ.Tests\MaintainIQ.Tests.csproj --filter "Category=SqlServerIntegration"
```

Sıradaki adım Faz 7'de bakım planı motoru, KPI, Advisor, dosya ve QR servisleridir.
