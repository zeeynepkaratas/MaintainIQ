# MaintainIQ

MaintainIQ, ekipman arızalarını, iş emirlerini, yedek parçaları ve planlı bakımı takip etmek için geliştirilen bir Windows masaüstü uygulamasıdır. NTP dönem projesidir.

## Çalıştırma

Visual Studio'da `MaintainIQ.sln` dosyasını aç. Başlangıç projesi olarak `MaintainIQ.UI` seçilebilir. Komut satırından:

```powershell
dotnet build MaintainIQ.sln
dotnet test MaintainIQ.sln
dotnet run --project src/MaintainIQ.UI
```

İlk derleme için DevExpress WinForms 25.1.3 paketlerinin NuGet'ten veya yerel önbellekten erişilebilir olması gerekir. Boş grid ve tema düğmesi kurulum kontrolü içindir. Veritabanı bağlantısı bu aşamada gerekli değildir.

## Projeler

- `MaintainIQ.Domain`: İş kuralları ve model.
- `MaintainIQ.Data`: SQL Server veri erişimi.
- `MaintainIQ.Services`: Uygulama iş akışları.
- `MaintainIQ.UI`: DevExpress WinForms arayüzü.
- `MaintainIQ.Tests`: xUnit testleri.

[Kurulum notları](docs/kurulum-notlari.md)
