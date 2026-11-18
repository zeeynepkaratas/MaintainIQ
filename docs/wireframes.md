# Ekran wireframe'leri

Wireframe'ler alan ve eylem yerleşimini gösteren ilk taslaktır. DevExpress uygulamasında ölçüler DPI uyumlu düzenlerle uygulanacaktır.

## 1. Giriş

```text
+---------------- MaintainIQ ----------------+
| Kullanıcı adı [__________________________] |
| Parola         [__________________________] |
| [ ] Beni hatırla                 [Giriş]   |
| Durum / doğrulama mesajı                   |
+---------------------------------------------+
```

## 2. Dashboard

```text
+ Tarih + ekipman filtresi ------------------+
| MTTR | MTBF | Availability | Backlog       |
+---------------------------------------------+
| Trend grafiği      | Arıza Pareto           |
| Planlı/düzeltici   | Maliyet                |
+---------------------------------------------+
| Kritik işler       | Advisor önerileri      |
+---------------------------------------------+
```

## 3. Ekipman

```text
+ Ekipman ağacı ----+ Ekipman kartı ----------+
| Tesis             | Kod / ad / durum         |
|  + Hat            | Teknik bilgiler          |
|    + Makine       | Fotoğraf / QR            |
|                   | [Ekle] [Taşı] [Düzenle]  |
+-------------------+---------------------------+
| Arıza geçmişi | İş emirleri | Maliyet         |
+-----------------------------------------------+
```

## 4. Arıza

```text
+ Filtreler -----------------------------------+
| Grid: tarih | ekipman | ciddiyet | durum      |
+-----------------------------------------------+
| Seçili arıza detayı / fotoğraf                |
| [Bildir] [Onayla] [İş emri oluştur]           |
+-----------------------------------------------+
```

## 5. İş emri

```text
+ Durum / teknisyen / tarih filtresi -----------+
| Master grid: no | ekipman | durum | öncelik   |
|  Detail: parçalar | geçmiş | yorumlar          |
+-----------------------------------------------+
| [Ata] [Başlat] [Parça] [Tamamla] [Kapat]      |
+-----------------------------------------------+
```

## 6. Parça ve stok

```text
+ Arama / minimum stok filtresi ----------------+
| Kod | parça | stok | minimum | maliyet        |
+-----------------------------------------------+
| Stok hareket grafiği | hareket geçmişi         |
| [Giriş] [Düzelt] [Dışa aktar]                  |
+-----------------------------------------------+
```

## 7. Teknisyen

```text
+ Teknisyen kartları ---------------------------+
| Ad | uzmanlık | aktif işler | haftalık saat   |
+-----------------------------------------------+
| İş yükü grafiği      | Zaman çakışmaları       |
+-----------------------------------------------+
```

## 8. Bakım takvimi

```text
+ Tarih gezgini + görünüm seçici ---------------+
| Kaynak: teknisyen | Scheduler                 |
|                   | plan ve iş emri blokları  |
+-----------------------------------------------+
| [Plan ekle] [İş üret] [Yenile]                 |
+-----------------------------------------------+
```

## 9. Raporlar

```text
+ Rapor listesi ----+ Parametreler --------------+
| İş emri formu     | Tarih / ekipman / durum    |
| Aylık maliyet     | [Önizle] [PDF] [Excel]     |
+-------------------+-----------------------------+
| DocumentViewer                                  |
+-------------------------------------------------+
```

## 10. Denetim ve kullanıcı

```text
+ Sekmeler: Denetim | Kullanıcılar --------------+
| Filtre: kullanıcı / varlık / tarih / işlem     |
| Grid: zaman | kullanıcı | eylem | varlık       |
+-----------------------------------------------+
| Eski değer JSON       | Yeni değer JSON        |
+-----------------------------------------------+
```

