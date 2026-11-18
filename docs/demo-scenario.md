# 10 dakikalık demo senaryosu

| Süre | Akış |
|---|---|
| 0:00 | Supervisor girişi. Dashboard kartları, trend, Pareto ve en önemli Advisor önerisi. |
| 1:30 | Ekipman ağacında Tesis > Hat > CNC. Kart, QR, arıza geçmişi ve maliyet özeti. |
| 3:00 | Operator fotoğraflı arıza bildirir. Supervisor arızadan iş emri üretir ve çakışma uyarısıyla teknisyene atar. |
| 4:30 | Technician işi başlatır ve parça ister. Yetersiz stokla `WaitingForParts`, stok girişinden sonra devam gösterilir. |
| 6:00 | İşçilik ve parçalarla tamamlama yapılır. Bilerek hata üretilip transaction rollback sonucu gösterilir. |
| 7:00 | Supervisor işi kapatır. Audit eski/yeni değerleri ve durum zaman çizelgesi açılır. |
| 8:00 | Bakım takviminde planlı bakımlar ve idempotent otomatik iş emri üretimi gösterilir. |
| 9:00 | QR'lı iş emri formu ve aylık maliyet PDF'i açılır. Mimari diyagram ve test sonucu ile kapanır. |

Demo verisi `db/seed.sql` ile tekrarlanabilir olacaktır. Her adım için beklenen sonuç ve geri dönüş verisi Faz 11'de son hâline getirilecektir.
