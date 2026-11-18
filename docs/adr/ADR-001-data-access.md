# ADR-001: ADO.NET ve Dapper ile veri erişimi

- Durum: Kabul edildi
- Tarih: 2026-11-18

## Bağlam

Proje SQL, transaction, kilitleme ve eşzamanlılık davranışını açık biçimde göstermelidir. Saf ADO.NET çok fazla satır eşleme kodu üretir. EF Core kritik SQL ayrıntılarını görünmez kılabilir.

## Karar

Bağlantı, komut, transaction, `UPDLOCK` ve rowversion yönetimi ADO.NET ile açıkça yürütülecek. Dapper yalnızca parametre bağlama ve satır-nesne eşleme için kullanılacak. SQL satır DTO'larına okunacak, domain nesneleri kontrollü `Rehydrate` yollarıyla oluşturulacak.

## Sonuçlar

SQL davranışı ve transaction sınırları görünür kalır. Repository kodu saf ADO.NET'e göre daha kısa olur. Kalıcılık modeli ile domain modeli ayrı tutulduğu için ek eşleme kodu gerekir.

