# ADR-004: Transaction sınırı servis katmanındadır

- Durum: Kabul edildi
- Tarih: 2026-11-18

## Bağlam

İş emri tamamlama, stok düşümü, maliyet, durum geçmişi ve audit kayıtlarını birlikte değiştirir.

## Karar

Unit of Work transaction'ı Services katmanında kullanım senaryosu başında açılır. Aynı bağlantı ve transaction bütün repository çağrılarına aktarılır. Başarıda commit, herhangi bir hatada rollback yapılır.

## Sonuçlar

Kısmi yazım engellenir ve transaction sınırı iş akışıyla aynı yerde görünür. Repository'ler transaction sahiplenmez ve bağımsız commit yapmaz.

