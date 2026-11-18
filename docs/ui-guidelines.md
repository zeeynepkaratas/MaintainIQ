# UI yönergeleri

## Navigasyon

Ana pencere bir Ribbon ve sol modül menüsü kullanır. Modüller Dashboard, Ekipman, Arızalar, İş Emirleri, Yedek Parça, Teknisyenler, Bakım Takvimi, Analiz, Raporlar, Advisor ve Yönetim olarak gruplanır. Yetkisiz modül görünmez, servis katmanı ayrıca yetki doğrular.

## Renkler

| Anlam | Renk | Hex |
|---|---|---|
| Created / nötr | Gri | `#9E9E9E` |
| Assigned | Mavi | `#1976D2` |
| InProgress | Turuncu | `#F57C00` |
| WaitingForParts | Sarı | `#FBC02D` |
| Completed | Açık yeşil | `#66BB6A` |
| Closed | Koyu yeşil | `#2E7D32` |
| Cancelled / hata | Soluk kırmızı | `#E57373` |
| Kritik | Kırmızı | `#D32F2F` |

Renk tek başına bilgi taşımaz. Her durumda metin ve ikon birlikte gösterilir.

## Öncelik

| Öncelik | İkon yaklaşımı | Görünüm |
|---|---|---|
| Low | Aşağı ok | Nötr gri |
| Medium | Eşittir | Mavi |
| High | Yukarı ok | Turuncu |
| Critical | Ünlem üçgeni | Kırmızı |

## Tipografi ve yerleşim

- Varsayılan UI yazısı 10-11 pt, ekran başlığı 18 pt, bölüm başlığı 13-14 pt.
- Formlar `LayoutControl` ile hizalanır. Zorunlu alan etiketi ve doğrulama mesajı birlikte gösterilir.
- Birincil eylem sağ altta, iptal eylemi yanında yer alır. Silme ve iptal gerekçe ister.
- Grid'lerde arama, filtre satırı, kolon seçici ve dışa aktarma aynı sırada bulunur.
- Klavye odağı görünürdür. `Ctrl+F` arama, `Ctrl+N` yeni kayıt, `Ctrl+S` kaydetme için ayrılır.
- Yüksek DPI'da sabit piksel konumlandırması kullanılmaz.

## Mesajlar

- Domain kuralı mesajları kullanıcının neyi nasıl düzelteceğini söyler.
- Teknik exception ve SQL ayrıntıları ekranda gösterilmez.
- Başarılı kayıtlar kısa bildirimle, geri alınamaz eylemler açık onayla bildirilir.

