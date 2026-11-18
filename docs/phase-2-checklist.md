# Faz 2 domain modeli kontrol listesi

## Tamamlananlar

- [x] Altyapıdan bağımsız `BaseEntity` ve rowversion alanı eklendi.
- [x] `DomainException`, `DomainValidationException` ve kural kodlu `BusinessRuleException` eklendi.
- [x] `Money` ve `DateRange` value object'leri eklendi.
- [x] Rol, ekipman, arıza, iş emri, öncelik, stok ve bakım periyodu enum'ları eklendi.
- [x] Personel alt türleri ve kullanıcı hesabı modeli oluşturuldu.
- [x] Ekipman ağacı, döngü engeli ve soft delete invariant'ları uygulandı.
- [x] Arıza ve arıza nedeni modelleri oluşturuldu.
- [x] Düzeltici ve önleyici iş emri modelleri oluşturuldu.
- [x] Parça maliyet snapshot'ı, yorum ve durum geçmişi modelleri oluşturuldu.
- [x] Yedek parça, stok hareketi, tedarikçi ve negatif stok engeli uygulandı.
- [x] Bakım planı, ek, audit kaydı ve destek modelleri oluşturuldu.
- [x] Domain katmanının başka MaintainIQ katmanına bağımlı olmadığı doğrulandı.
- [x] Toplam 37 test geçti.
- [x] Solution 0 uyarı ve 0 hata ile derlendi.

## Faz 3'e bırakılanlar

- İş emrinin yedi durumlu geçiş motoru ve tam geçiş matrisi.
- Ekipman durum geçişlerinin terminal durum kontrolleri.
- Atama, başlatma, bekletme, tamamlama, yeniden açma, kapatma ve iptal komutları.
- Durum geçişleri için en az 60 domain testine ulaşılması ve kapsam raporu.

Faz 2 tamamlanmıştır. Faz 3, mevcut entity ve enum'ları kullanarak durum makinelerini ekleyecektir.
