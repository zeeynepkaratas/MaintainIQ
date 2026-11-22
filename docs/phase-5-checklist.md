# Faz 5 kontrol listesi

## Altyapı

- [x] `IDbConnectionFactory` ve `SqlConnectionFactory` eklendi.
- [x] Ortak bağlantı ve transaction taşıyan `IUnitOfWork` / `UnitOfWork` eklendi.
- [x] Commit edilmeyen transaction dispose sırasında geri alınıyor.
- [x] Dapper yalnızca parametre bağlama ve satır eşleme için kullanılıyor.
- [x] Kalıcı entity'ler internal Row DTO -> Mapper -> internal `Rehydrate` yoluyla oluşturuluyor.
- [x] Liste ve rapor sorguları domain yerine read-model DTO döndürüyor.

## Repository'ler

- [x] Generic `IRepository<T>` ve `Repository<T>` tabanı hazır.
- [x] Equipment repository: CRUD, soft delete, arama, açık iş kontrolü, ağaç, yol ve torun CTE'leri.
- [x] WorkOrder repository: CRUD, filtre, sayfalama, güvenli sıralama, parça grafiği ve açık işler.
- [x] SparePart repository: CRUD, minimum stok ve `UPDLOCK, ROWLOCK` sorgusu.
- [x] Failure, StockMovement, MaintenancePlan, Technician, User, Attachment ve Audit repository'leri hazır.
- [x] `IReportQueries` Faz 7 uygulaması için tanımlandı.

## Doğruluk ve test

- [x] SQL değerleri parametreli; kullanıcı girdisi SQL metnine eklenmiyor.
- [x] Güncellemeler rowversion kontrolü yapıyor ve 0 satırda `ConcurrencyException` üretiyor.
- [x] Async API ve `CancellationToken` desteği var.
- [x] Gerçek SQL Server Express üzerinde CRUD, recursive CTE, filtre/sayfalama, kilit, rowversion, rollback ve soft delete testleri var.
- [x] Domain testleriyle birlikte tüm test grafiği çalıştırılıyor.
- [x] `tools/ConsoleSmoke` bütün repository'leri UI olmadan çağırıyor.

## Çalıştırma

```powershell
pwsh -File .\db\deploy.ps1
dotnet test tests\MaintainIQ.Tests\MaintainIQ.Tests.csproj
dotnet run --project tools\ConsoleSmoke\ConsoleSmoke.csproj
```

Sıradaki adım Faz 6'da servis katmanı transaction, yetki ve audit iş akışlarıdır.
