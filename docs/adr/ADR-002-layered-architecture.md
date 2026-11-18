# ADR-002: Katmanlı mimari ve bağımlılık yönü

- Durum: Kabul edildi
- Tarih: 2026-11-18

## Bağlam

İş kurallarının WinForms, SQL Server ve DevExpress'ten bağımsız test edilebilmesi gerekir.

## Karar

Bağımlılık yönü `UI -> Services -> Data -> Domain` olacaktır. Domain başka proje veya altyapı paketine başvurmayacaktır. UI yalnızca kullanıcı etkileşimini ve sunumu, Services kullanım akışlarını, Data kalıcılığı, Domain iş kurallarını üstlenecektir.

## Sonuçlar

İş kuralları hızlı birim testleriyle doğrulanabilir. Katmanlar arasında DTO ve eşleme ihtiyacı doğar. UI içinde SQL veya iş kuralına izin verilmez.

