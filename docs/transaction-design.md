# Transaction tasarımı

## Sınır ve sahiplik

Transaction sınırı yalnızca servis metodundadır. Her yazma kullanım senaryosu
`IServiceDataSessionFactory` üzerinden yeni bir oturum üretir, `IUnitOfWork.BeginAsync` çağırır ve aynı
oturumdaki repository'leri kullanır. Repository'ler transaction açmaz veya commit etmez. Başarılı akışın
son adımı commit, bütün hata yollarının son adımı rollback'tir.

Bu ayrım `UI -> Services -> Data -> Domain` bağımlılık yönünü korur. UI yalnızca komut gönderir ve domain
hatalarını kullanıcı mesajına dönüştürür. Yetki, transaction ve audit kararları UI'a bırakılamaz.

## Complete akışı

`WorkOrderService.CompleteAsync` aşağıdaki sırayı uygular:

1. `ExecuteOwnWorkOrder` yetkisini doğrular.
2. Unit of Work ve SQL transaction başlatır.
3. İş emrini, rowversion değerini ve talep edilen parçaları yükler.
4. Parçaları `SparePartId` sırasına dizer; bu sabit kilit sırası deadlock riskini azaltır.
5. Her parçayı `WITH (UPDLOCK, ROWLOCK)` ile okur ve stok yeterliliğini domain nesnesinde doğrular.
6. Stoku rowversion kontrolüyle günceller ve `StockMovement` kaydı ekler.
7. İş emrini `Completed` durumuna geçirir ve durum geçmişini ekler.
8. Arızalı/bakım altındaki ekipmanı çalışır duruma getirir.
9. Eski/yeni değerleri JSON olarak `AuditLog` tablosuna yazar.
10. Bütün adımlar başarılıysa commit eder; herhangi bir exception'da rollback yapar.

Audit ana işlemle aynı transaction'a yazıldığı için audit hatası bile stok, hareket, durum geçmişi, iş emri
ve ekipman güncellemelerini geri alır. Testte audit repository hatası kasıtlı üretilerek bu davranış gerçek
SQL Server üzerinde doğrulanır.

## Eşzamanlılık

Stok satırları kimlik sırasıyla kilitlenir. Aynı parçanın son üç adedinden iki iş emri eşzamanlı olarak
ikişer adet tüketmeye çalıştığında ilk transaction başarılı olur. İkinci transaction güncel stok değerini
kilit alındıktan sonra görür, `BR-02` ile reddedilir ve rollback olur. Son stok bir kalır; negatif stok ve çift
hareket kaydı oluşmaz.

WorkOrder, Equipment, SparePart ve UserAccount güncellemeleri ayrıca rowversion kullanır. Eski rowversion
ile yapılan güncelleme `ConcurrencyException` üretir ve son yazanın önceki veriyi sessizce ezmesini engeller.

## Yetki ve oturum

`CurrentUserContext` kullanıcı hesabı, kişi kimliği, kullanıcı adı ve rolü taşır. `PermissionService`,
`docs/permissions.md` matrisini tek kaynak olarak kodlar. Servisler repository çağrısından önce gerekli
yetkiyi denetler; yetkisiz çağrı `PermissionDeniedException` üretir.

Teknisyen yalnızca kendi iş emrini başlatabilir, parça ekleyebilir ve tamamlayabilir. Admin ve Supervisor
yüksek yetkiyle işlem yapabilir. Kapatma, yeniden açma ve iptal yalnızca bu iki role açıktır.

## Kimlik doğrulama

`PasswordHasher`, `Rfc2898DeriveBytes.Pbkdf2` ile SHA-256, 120.000 yineleme, 16 bayt rastgele tuz ve
32 bayt özet kullanır. Parola karşılaştırması `CryptographicOperations.FixedTimeEquals` ile yapılır. Beş
ardışık hatalı giriş hesabı on dakika kilitler. Başarılı giriş sayacı sıfırlar; parola ve tuz hiçbir zaman audit
kaydına yazılmaz.

## Test kanıtı

- Repository mock'larıyla yetki, PBKDF2, oturum, servis akışları ve rollback birim testleri.
- Gerçek SQL Server Express üzerinde UI'sız Created -> Assigned -> InProgress -> Completed -> Closed akışı.
- Audit yazımı hata verdiğinde stok, stok hareketi, iş emri, durum geçmişi ve ekipmanın birlikte geri alınması.
- İki eşzamanlı Complete çağrısında stok kilidi ve negatif stok engeli.
- CI'da Domain satır kapsamı en az yüzde 85, Services satır kapsamı en az yüzde 70.
