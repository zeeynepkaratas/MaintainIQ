# Rol-yetki matrisi

| İşlem | Admin | Supervisor | Technician | Operator |
|---|:---:|:---:|:---:|:---:|
| Ekipman oluştur/düzenle/taşı/sil | E | E | H | H |
| Arıza bildir | E | E | E | E |
| Arızadan iş emri oluştur | E | E | H | H |
| İş emri ata/yeniden ata | E | E | H | H |
| Kendi işini başlat/tamamla | E | E | E | H |
| İş emri kapat/yeniden aç/iptal | E | E | H | H |
| Parça stok girişi/düzeltme | E | E | H | H |
| Bakım planı yönet | E | E | H | H |
| KPI/dashboard/rapor görüntüle | E | E | Kendi işleri | Salt okur |
| Advisor önerilerini yönet | E | E | H | H |
| Kullanıcı/rol/ayar yönet | E | H | H | H |
| Denetim kaydı görüntüle | E | E | H | H |

`E` izinli, `H` izinsizdir. Bu tablo Faz 6'da `IPermissionService` tarafından tek yetki kaynağı olarak kodlanacaktır.
