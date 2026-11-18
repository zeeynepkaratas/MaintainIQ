# ADR-003: Hata stratejisi

- Durum: Kabul edildi
- Tarih: 2026-11-18

## Bağlam

Kural ihlalleri, altyapı hataları ve kullanıcı mesajları aynı biçimde ele alınırsa UI teknik ayrıntılara bağımlı olur.

## Karar

Domain, anlamlı `DomainException` alt türleri üretir. Services beklenen domain ve eşzamanlılık hatalarını kullanım sonucu hâline çevirir, beklenmeyen hataları log'lar ve transaction'ı geri alır. UI kullanıcıya kısa Türkçe mesaj gösterir, stack trace göstermez.

## Sonuçlar

Hata davranışı ekranlar arasında tutarlı olur. Exception türleri ve mesaj eşlemeleri merkezi olarak korunmalıdır.

