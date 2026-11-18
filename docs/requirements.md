# Gereksinimler ve izlenebilirlik

## Fonksiyonel gereksinimler

| ID | Gereksinim | Kullanım senaryoları | Öncelik |
|---|---|---|---|
| F-01 | Giriş, rol bazlı yetki, parola hash ve hesap kilidi | UC-01, UC-24 | P0 |
| F-02 | Ekipman ağacı, taşıma, döngü engeli ve soft delete | UC-02, UC-03, UC-04 | P0 |
| F-03 | Arıza bildirimi, neden, ciddiyet ve iş emri üretimi | UC-05, UC-06 | P0 |
| F-04 | Yedi durumlu iş emri durum makinesi | UC-07 - UC-14 | P0 |
| F-05 | Parça düşümü, maliyet ve rollback için tek transaction | UC-10, UC-11 | P0 |
| F-06 | Parça, stok hareketi, tedarikçi ve minimum stok | UC-09, UC-10, UC-17, UC-18 | P0 |
| F-07 | Teknisyen, atama, iş yükü ve çakışma uyarısı | UC-07, UC-08 | P0 |
| F-08 | Eski/yeni değerli transaction uyumlu audit log | UC-11, UC-22 | P0 |
| F-09 | Rowversion ile iyimser eşzamanlılık | UC-02, UC-07, UC-10, UC-18 | P0 |
| F-10 | Periyodik plan ve idempotent iş emri üretimi | UC-15, UC-16 | P1 |
| F-11 | MTBF, MTTR, Availability, PM uyum, backlog ve maliyet KPI'ları | UC-19 | P1 |
| F-12 | Recursive CTE ile alt ağaç sorgusu ve KPI kapsamı | UC-03, UC-19 | P1 |
| F-13 | Kartlar, grafikler, göstergeler ve yan panelli dashboard | UC-19 | P1 |
| F-14 | Teknisyen kaynaklı sürükle-bırak bakım takvimi | UC-07, UC-15 | P1 |
| F-15 | Altı rapor ve PDF/Excel dışa aktarma | UC-20 | P1 |
| F-16 | Kanıtlı ve eyleme dönüşebilen yedi kuraldan Advisor | UC-23 | P2 |
| F-17 | QR etiket üretimi, yazdırma ve arama | UC-21 | P2 |
| F-18 | Ekipman/arıza fotoğrafları ve küçük resim galerisi | UC-05, UC-26 | P2 |
| F-19 | PivotGrid maliyet ve arıza analizi | UC-19 | P2 |
| F-20 | Ekipman kartları ve iş emri durum grupları | UC-02, UC-19 | P2 |
| F-21 | Grid düzeni, filtre, gruplama ve dışa aktarma | UC-25 | P2 |
| F-22 | Birim/entegrasyon testleri, kapsam ve CI | Tüm iş kuralları | P1 |
| F-23 | README, ADR, diyagram, demo ve sürüm | UC-20 | P1 |
| F-24 | Kapalı varsayılan LLM stub'ı ve kurallara geri dönüş | UC-23 | P3 |

## Fonksiyonel olmayan gereksinimler

| ID | Alan | Ölçülebilir kabul ölçütü |
|---|---|---|
| NFR-01 | Performans | Liste ve dashboard ekranları 10.000 kayıtta geliştirme makinesinde 2 saniyenin altında açılır. Hedef 1 saniyedir. |
| NFR-02 | Güvenlik | Parolalar PBKDF2 ve kullanıcıya özel tuzla saklanır. Tüm SQL sorguları parametrelidir. |
| NFR-03 | Yetkilendirme | Her servis işlemi merkezi rol-yetki tablosundan doğrulanır. UI gizleme tek güvenlik katmanı sayılmaz. |
| NFR-04 | İzlenebilirlik | Kritik oluşturma, güncelleme ve durum geçişleri kullanıcı, zaman, eski ve yeni değerlerle audit'e yazılır. |
| NFR-05 | Tutarlılık | İş emri tamamlama ve stok değişimleri atomiktir. Hata hâlinde veri kısmen yazılmaz. |
| NFR-06 | Eşzamanlılık | Rowversion uyuşmazlığı kullanıcıya açıklanır ve sessiz son-yazan-kazan uygulanmaz. |
| NFR-07 | Kullanılabilirlik | Tek tema, ortak durum renkleri, klavye erişimi ve yüksek DPI desteği kullanılır. |
| NFR-08 | Bakım yapılabilirlik | UI içinde SQL ve iş kuralı bulunmaz. Domain katmanı altyapı paketlerine bağlı değildir. |
| NFR-09 | Test edilebilirlik | Domain kapsamı en az %85, Services kapsamı en az %70 olur. |
| NFR-10 | Kurtarılabilirlik | Dosya ve veritabanı hataları kullanıcıya anlamlı mesajla bildirilir, teknik ayrıntı log'a yazılır. |

