# İş kuralları

| ID | Kural | İlgili senaryo |
|---|---|---|
| BR-01 | `Closed` ve `Cancelled` iş emirleri değiştirilemez. | UC-07 - UC-14 |
| BR-02 | Stok negatif olamaz. Yetersiz stokta kullanım reddedilir veya iş emri `WaitingForParts` olur. | UC-09 - UC-11 |
| BR-03 | Yalnızca aktif teknisyene atama yapılır. Atanmamış iş emri başlatılamaz. | UC-07, UC-08 |
| BR-04 | Tamamlama için işçilik süresi sıfırdan büyük olmalıdır. | UC-11 |
| BR-05 | İptal ve yeniden açma gerekçesiz yapılamaz. | UC-13, UC-14 |
| BR-06 | Kapatma yalnızca Admin veya Supervisor tarafından yapılır. | UC-12 |
| BR-07 | Ekipman kendisine veya bir torununa taşınamaz. | UC-03 |
| BR-08 | Çocuğu veya açık iş emri olan ekipman silinemez. Silme soft delete'tir. | UC-04 |
| BR-09 | Kritik arıza ekipmanı `Faulty` yapar ve üretilen işin önceliğini en az `High` yapar. | UC-05, UC-06 |
| BR-10 | Parça birim maliyeti kullanım anında iş emrine kopyalanır ve geçmişte değişmez. | UC-10 |
| BR-11 | Aynı bakım planı ve vade tarihi için yalnızca bir iş emri üretilebilir. | UC-16 |
| BR-12 | Teknisyenin zaman aralığı çakışıyorsa uyarı verilir ve yalnızca Supervisor/Admin onayıyla devam edilir. | UC-07 |
| BR-13 | Kritik işlem ve audit aynı transaction'dadır. Ana işlem geri alınırsa audit de geri alınır. | UC-11, UC-22 |
| BR-14 | Rowversion çakışmasında kayıt üzerine yazılmaz ve kullanıcı güncel veriyi yenilemeye yönlendirilir. | UC-02, UC-07, UC-10, UC-18 |
| BR-15 | Hesap beş ardışık başarısız girişten sonra 10 dakika kilitlenir. | UC-01 |
| BR-16 | Stok düzeltmesi gerekçesiz yapılamaz. Her stok değişimi hareket kaydı üretir. | UC-17, UC-18 |
| BR-17 | Kullanıcı adı, ekipman kodu, iş emri numarası, parça kodu ve QR token benzersizdir. | UC-02, UC-21, UC-24 |
| BR-18 | Bakım planı periyot değeri pozitiftir ve pasif ekipman için yeni iş üretmez. | UC-15, UC-16 |
| BR-19 | Teknisyen yalnızca kendisine atanmış iş emrini başlatabilir ve tamamlayabilir. | UC-08, UC-11 |
| BR-20 | Fotoğraf türü ve boyutu ayarlanan sınırlar içinde olmalı, dosya adı uygulama tarafından üretilmelidir. | UC-26 |

