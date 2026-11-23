# MaintainIQ

MaintainIQ, ekipman arızalarını, iş emirlerini, yedek parçaları ve planlı bakımı takip etmek için geliştirilen bir Windows masaüstü uygulamasıdır. NTP dönem projesidir.

## Çalıştırma

Visual Studio'da `MaintainIQ.sln` dosyasını aç. Başlangıç projesi olarak `MaintainIQ.UI` seçilebilir. Komut satırından:

```powershell
dotnet build MaintainIQ.sln
dotnet test MaintainIQ.sln
dotnet run --project src/MaintainIQ.UI
```

Proje .NET 9 ve DevExpress WinForms 25.2.3 kullanır. Bu hedef, kurulu Visual Studio 2022 17.14 ile uyumludur. İlk ekrandaki boş grid ve tema düğmesi kurulum kontrolü içindir.

Yerel SQL Server Express veritabanını oluşturmak ve güncellemek için:

```powershell
pwsh -File .\db\deploy.ps1
sqlcmd -S .\SQLEXPRESS -E -b -d MaintainIQ -i .\db\verify.sql
```

Ayrıntılar için [veritabanı kurulum rehberine](db/README.md) bakılabilir.

Veri katmanı ADO.NET bağlantı/transaction yönetimi ile Dapper satır eşlemesini birlikte kullanır. SQL,
`UPDLOCK`, rowversion ve Unit of Work sınırları açıkça yönetilir; Dapper domain nesnelerine doğrudan
yazmaz. UI olmadan repository doğrulaması için:

```powershell
dotnet run --project tools/ConsoleSmoke/ConsoleSmoke.csproj
```

## Transaction tasarımı

Servis katmanı her yazma kullanım senaryosunda yeni bir veri oturumu ve `UnitOfWork` açar. Repository'ler
transaction başlatmaz; aynı bağlantı ve transaction'ı kullanır. `WorkOrderService.CompleteAsync` parçaları
kimlik sırasıyla `UPDLOCK` kullanarak kilitler, stoku düşer, stok hareketlerini yazar, iş emrini tamamlar,
durum geçmişini ve ekipman durumunu günceller, audit kaydını ekler ve en son commit eder. Adımların herhangi
birinde hata oluşursa bütün değişiklikler geri alınır.

Yetki kontrolleri merkezi `IPermissionService` rol matrisiyle servis katmanında uygulanır. Parolalar
PBKDF2-SHA256, 120.000 yineleme ve 16 bayt rastgele tuzla saklanır; doğrulama sabit zamanlı karşılaştırma
kullanır. Ayrıntılı akış için [transaction tasarımına](docs/transaction-design.md) bakılabilir.

```powershell
dotnet test tests/MaintainIQ.Tests/MaintainIQ.Tests.csproj --filter "Category!=SqlServerIntegration"
dotnet test tests/MaintainIQ.Tests/MaintainIQ.Tests.csproj --filter "Category=SqlServerIntegration"
```

## Projeler

- `MaintainIQ.Domain`: İş kuralları ve model.
- `MaintainIQ.Data`: SQL Server veri erişimi.
- `MaintainIQ.Services`: Uygulama iş akışları.
- `MaintainIQ.UI`: DevExpress WinForms arayüzü.
- `MaintainIQ.Tests`: xUnit testleri.
- `ConsoleSmoke`: Repository'lerin SQL Server'a karşı uçtan uca kontrolü.

## Belgeler

- [Kurulum notları](docs/kurulum-notlari.md)
- [Hocaya sorulacaklar](docs/hoca-sorulari.md)
- [Faz 1 analiz ve tasarım belgeleri](docs/README.md)
- [Faz 6 transaction tasarımı](docs/transaction-design.md)
