# Faz 4 kontrol listesi

- [x] SQL Server şeması 16 ana tabloyla oluşturuldu.
- [x] Primary key ve foreign key ilişkileri tanımlandı.
- [x] Enum, pozitif değer, zaman ve JSON check constraint'leri eklendi.
- [x] Kod, kullanıcı adı, iş emri numarası ve QR token benzersiz yapıldı.
- [x] Aynı plan ve vade için yinelenen önleyici iş emri engellendi.
- [x] Değiştirilebilir kayıtlara `rowversion` eklendi.
- [x] Liste, dashboard, takvim ve audit sorguları için indeksler eklendi.
- [x] Recursive CTE tabanlı ekipman hiyerarşi görünümü eklendi.
- [x] Arıza nedenleri idempotent seed verisi olarak eklendi.
- [x] Sürümlü ve tekrar çalıştırılabilir deployment script'i hazırlandı.
- [x] Gerçek SQL Server Express üzerinde doğrulama script'i hazırlandı.
- [x] Benzersizlik, negatif stok ve recursive CTE davranış testleri eklendi.

Sıradaki adım Faz 5'te ADO.NET + Dapper repository'leri ve Unit of Work altyapısıdır.
