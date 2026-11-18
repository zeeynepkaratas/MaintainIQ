# MaintainIQ veritabanı

SQL Server Express üzerinde Windows kimlik doğrulamasıyla kurulum:

```powershell
pwsh -File .\db\deploy.ps1
```

Farklı sunucu için `-ServerInstance` kullanılabilir. Script önce `MaintainIQ` veritabanını oluşturur, ardından `db/migrations` altındaki sürümlü dosyaları ad sırasıyla uygular. Migration dosyaları `SchemaVersion` tablosuyla izlenir ve tekrar çalıştırılabilir.

Kurulum doğrulaması:

```powershell
sqlcmd -S .\SQLEXPRESS -E -b -d MaintainIQ -i .\db\verify.sql
```

Kısıt ve recursive CTE davranış testleri:

```powershell
sqlcmd -S .\SQLEXPRESS -E -b -d MaintainIQ -i .\db\test-invariants.sql
```

Şema; foreign key, check constraint, benzersiz indeks, filtreli indeks ve iyimser eşzamanlılık için `rowversion` içerir. `vw_EquipmentHierarchy` görünümü recursive CTE ile ekipman ağacını üretir.
