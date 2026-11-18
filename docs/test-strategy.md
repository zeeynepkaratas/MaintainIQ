# Test stratejisi

| Katman | Kapsam | Araç | Hedef |
|---|---|---|---|
| Domain | Durum geçiş matrisi, ekipman ağacı, stok, plan, maliyet ve değer nesneleri | xUnit | En az 60 test, en az %85 satır kapsamı |
| Data | CRUD, recursive CTE, Unit of Work, rowversion ve kilitleme | xUnit + SQL Server test veritabanı | Her repository için mutlu ve hata akışları |
| Services | Complete rollback, yetki, eşzamanlılık, KPI, plan motoru ve Advisor | xUnit + NSubstitute + test DB | En az %70 satır kapsamı |
| UI | Her rolün ana akışı, validasyon, klavye ve yüksek DPI | Manuel kontrol listesi | Faz 8, 9 ve 11'de tekrarlanır |
| Performans | 5.000-10.000 iş emrinde liste ve KPI süreleri | Stopwatch + SQL ölçümü | 2 saniyenin altında, hedef 1 saniye |

## Test veri ilkeleri

- Testler birbirinden bağımsızdır ve kendi verisini oluşturur.
- Entegrasyon testleri transaction veya veritabanı yeniden oluşturma ile temizlenir.
- Zaman kullanan kurallar `TimeProvider` üzerinden kontrol edilir.
- Durum geçiş matrisindeki geçerli ve geçersiz bütün hücreler test edilir.
- Rollback testi stok, maliyet, geçmiş ve audit tablolarının hiçbirinde kısmi kayıt kalmadığını doğrular.

## CI kapısı

Pull request için Domain/Data/Services test grafiğinde restore, uyarıları hata kabul eden build ve test çalışır. Cobertura kapsam raporu artifact olarak saklanır. Domain satır kapsamı %85'in altına düşerse CI başarısız olur. Ticari DevExpress paket kaynağı sağlandığında UI projesi de CI restore ve build kapsamına alınacaktır.

