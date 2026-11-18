# Kurulum notları

İlk iskelet, makinede bulunan .NET 8 ve önbellekteki DevExpress 25.1.3 ile hazırlandı. .NET 8 desteği 10 Kasım 2026'da sona erdi. Desteklenen hedef için .NET 10 SDK ve DevExpress 25.2 veya uyumlu bir sonraki sürüm gerekiyor. Hedef framework `Directory.Build.props` içinde, UI paket sürümü `src/MaintainIQ.UI/MaintainIQ.UI.csproj` içinde tanımlı.

- [Microsoft .NET destek tarihleri](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core)
- [DevExpress WinForms sürüm uyumluluğu](https://docs.devexpress.com/WindowsForms/8092/prerequisites?v=25.2)

Faz 0 için kalan kontroller:

- Derleme DevExpress deneme lisansı uyarısı (DX1000) verdi. Lisansı, Visual Studio Toolbox erişimini ve tema düğmesini doğrula.
- LocalDB veya SQL Server Express bağlantısını doğrula ve boş `MaintainIQ` veritabanını oluştur.
- Dersin paket, lisans ve teslim koşullarını netleştir.
- GitHub deposu ve faz takibini ayarla.

Bu oturumda LocalDB örnek yapılandırmasına erişilemedi ve `.SQLEXPRESS` bağlantısı zaman aşımına uğradı. SQL bağlantısını Visual Studio veya SSMS üzerinden doğrula. Yerel Git deposu `main` dalında başlatıldı. İlk commit için repo düzeyinde Git yazar adı ve e-posta adresi gerekiyor.
