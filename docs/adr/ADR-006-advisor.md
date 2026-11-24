# ADR-006: Advisor arayüzü ve kural motoru

- Durum: Kabul edildi
- Tarih: 2026-11-18

## Bağlam

Bakım önerileri açıklanabilir ve çevrimdışı çalışabilir olmalıdır. LLM kullanımı ağ, maliyet ve değişken çıktı riski taşır.

## Karar

Services katmanında `IAdvisorService` tanımlanacak. Varsayılan uygulama yedi deterministik kuralı kanıt verileriyle çalıştıracak. LLM uygulaması kapalı varsayılan bir stub olacak ve hata hâlinde kural motoruna dönecek.

## Sonuçlar

Temel ürün dış hizmet olmadan çalışır ve öneriler test edilebilir. İleride LLM eklenmesi UI veya domain değişikliği gerektirmez.

## Uygulama notu (Faz 7, 2026-11-24)

Arayüz yol haritasındaki adla `IMaintenanceAdvisor` olarak uygulandı (`src/MaintainIQ.Services/Advisor`). `RuleBasedAdvisor` R1-R7 kurallarını çalıştırır, `LlmAdvisor` yalnızca kural önerilerinin üzerine özet ekleyen bir stub'dır. `AdvisorFactory`, `Advisor:Mode = Llm` olsa bile API anahtarı (`MAINTAINIQ_LLM_API_KEY` ortam değişkeni) ya da istemci yoksa kural moduna düşer. LLM çağrısı hata verirse kural sonuçları değişmeden döner.
