# ADR-006: Advisor arayüzü ve kural motoru

- Durum: Kabul edildi
- Tarih: 2026-11-18

## Bağlam

Bakım önerileri açıklanabilir ve çevrimdışı çalışabilir olmalıdır. LLM kullanımı ağ, maliyet ve değişken çıktı riski taşır.

## Karar

Services katmanında `IAdvisorService` tanımlanacak. Varsayılan uygulama yedi deterministik kuralı kanıt verileriyle çalıştıracak. LLM uygulaması kapalı varsayılan bir stub olacak ve hata hâlinde kural motoruna dönecek.

## Sonuçlar

Temel ürün dış hizmet olmadan çalışır ve öneriler test edilebilir. İleride LLM eklenmesi UI veya domain değişikliği gerektirmez.
