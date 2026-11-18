# Kurulum notları

## Doğrulanan ortam

- .NET SDK 9.0.305
- Hedef framework `net9.0`
- Visual Studio 2022 Community
- DevExpress WinForms 25.2.3
- Git deposu `main` dalında ve `origin` olarak GitHub'a bağlı

Hedef framework `Directory.Build.props` içinde, UI paket sürümü `src/MaintainIQ.UI/MaintainIQ.UI.csproj` içinde tanımlıdır.

## Faz 0 için kalan kontroller

- DevExpress lisansını yenile. Kurulu 25.2.3 deneme lisansının 5 Mart 2026'da dolduğu ve uygulamanın lisans penceresinde durduğu görsel olarak doğrulandı.
- Lisans etkinleştirildikten sonra Visual Studio Toolbox erişimini, boş grid'i ve tema düğmesini görsel olarak doğrula.
- Dersin paket, lisans ve teslim koşullarını hocayla netleştir.
- `v0.0-setup` etiketini, DevExpress ve hoca onayları tamamlandıktan sonra oluştur.

DevExpress lisans sorunu derlemeyi engellemiyor. Çalışan masaüstü uygulamasını ve UI kontrollerini doğrulamayı engelliyor.

## SQL Server

`MSSQL$SQLEXPRESS` hizmeti otomatik başlangıçla çalışıyor. `MaintainIQ` veritabanı oluşturuldu ve `ONLINE` durumda doğrulandı. Geliştirme bağlantısı Windows kimlik doğrulaması kullanır. Yeniden kurulum için `db/00-create-database.sql` çalıştırılabilir.

## GitHub takibi

Public [MaintainIQ Roadmap](https://github.com/users/zeeynepkaratas/projects/2) panosu repoya bağlandı. Pano `Backlog`, `Bu hafta`, `Devam`, `Test` ve `Bitti` akış seçeneklerini içerir. Faz 0-11 için 12 milestone oluşturuldu.

## Kaynaklar

- [Microsoft .NET destek tarihleri](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core)
- [DevExpress WinForms sürüm uyumluluğu](https://docs.devexpress.com/WindowsForms/8092/prerequisites?v=25.2)
