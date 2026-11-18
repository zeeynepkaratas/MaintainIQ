# Kurulum notları

## Doğrulanan ortam

- .NET SDK 10.0.404
- Hedef framework `net10.0`
- Visual Studio 2022 Community
- DevExpress WinForms 25.2.3
- Git deposu `main` dalında ve `origin` olarak GitHub'a bağlı

Hedef framework `Directory.Build.props` içinde, UI paket sürümü `src/MaintainIQ.UI/MaintainIQ.UI.csproj` içinde tanımlıdır.

## Faz 0 için kalan kontroller

- DevExpress lisansını yenile. Kurulu 25.2.3 deneme lisansının 5 Mart 2026'da dolduğu ve uygulamanın lisans penceresinde durduğu görsel olarak doğrulandı.
- Lisans etkinleştirildikten sonra Visual Studio Toolbox erişimini, boş grid'i ve tema düğmesini görsel olarak doğrula.
- SQL Server Express bağlantısını doğrula ve boş `MaintainIQ` veritabanını oluştur.
- Dersin paket, lisans ve teslim koşullarını hocayla netleştir.
- GitHub Projects panosunu ve faz takibini oluştur.

DevExpress lisans sorunu derlemeyi engellemiyor. Çalışan masaüstü uygulamasını ve UI kontrollerini doğrulamayı engelliyor.

## Kaynaklar

- [Microsoft .NET destek tarihleri](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core)
- [DevExpress WinForms sürüm uyumluluğu](https://docs.devexpress.com/WindowsForms/8092/prerequisites?v=25.2)
