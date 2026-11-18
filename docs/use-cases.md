# Kullanım senaryoları

## Aktörler

| Aktör | Sorumluluk |
|---|---|
| Admin | Kullanıcıları, rolleri, ayarları ve tüm bakım verilerini yönetir. |
| Supervisor | Ekipman, arıza, iş emri, stok ve bakım planı süreçlerini yönetir. |
| Technician | Kendisine atanan işleri yürütür, parça kullanır ve arıza bildirir. |
| Operator | Arıza bildirir ve kendisine açık salt okunur bilgileri görüntüler. |

## Senaryolar

| ID | Senaryo | Aktör | Ön koşul | Ana akış | Alternatif / hata | Son durum |
|---|---|---|---|---|---|---|
| UC-01 | Giriş yap | Tümü | Aktif hesap vardır. | Kullanıcı kimlik bilgilerini girer, parola doğrulanır ve rolüne uygun ana ekran açılır. | Hatalı giriş sayacı artar. Beşinci hatada hesap 10 dakika kilitlenir. | Oturum açılır veya açıklamalı hata gösterilir. |
| UC-02 | Ekipman ekle/düzenle | Admin, Supervisor | Kullanıcı yetkilidir. | Kod, ad, konum ve teknik bilgiler doğrulanıp kaydedilir. | Tekrarlanan kod veya geçersiz veri reddedilir. | Ekipman ağaçta görünür. |
| UC-03 | Ekipman taşı | Admin, Supervisor | Kaynak ve hedef ekipman aktiftir. | Yeni üst ekipman seçilir, döngü kontrolünden sonra hiyerarşi güncellenir. | Kendisine veya torununa taşıma reddedilir. | Yeni ekipman yolu kaydedilir. |
| UC-04 | Ekipman sil | Admin, Supervisor | Ekipman aktiftir. | Çocuk ve açık iş kontrol edilir, kayıt soft delete yapılır. | Çocuğu veya açık işi varsa işlem reddedilir. | Ekipman normal listelerden gizlenir. |
| UC-05 | Arıza bildir | Tümü | Ekipman aktiftir. | Ekipman, neden, ciddiyet, açıklama ve isteğe bağlı fotoğraf kaydedilir. | Kritik arıza ekipmanı `Faulty` yapar. Eksik zorunlu alan reddedilir. | Açık arıza oluşur. |
| UC-06 | Arızadan iş emri üret | Admin, Supervisor | Açık arıza vardır. | Düzeltici iş emri arıza ve ekipman bilgileriyle oluşturulur. | Aynı arıza için açık iş varsa kullanıcı uyarılır. | `Created` durumunda iş emri oluşur. |
| UC-07 | İş emri ata/yeniden ata | Admin, Supervisor | İş emri terminal durumda değildir. | Aktif teknisyen ve zaman aralığı seçilir, iş yükü kontrol edilip atama yapılır. | Çakışmada Supervisor onayı istenir. Pasif teknisyen reddedilir. | İş emri `Assigned` olur. |
| UC-08 | İş emrini başlat | Admin, Supervisor, Technician | İş emri kullanıcıya atanmıştır. | Kullanıcı işi başlatır ve gerçek başlangıç zamanı kaydedilir. | Atanmamış veya başka teknisyene ait iş reddedilir. | İş emri `InProgress` olur. |
| UC-09 | Parça iste | Admin, Supervisor, Technician | İş emri `InProgress` durumundadır. | Gerekli parça ve miktar kaydedilir. | Stok yetersizse gerekçeyle beklemeye alınır. | İş emri `WaitingForParts` olabilir. |
| UC-10 | Parça kullan | Admin, Supervisor, Technician | İş emri açıktır ve stok yeterlidir. | Miktar stoktan düşülür, hareket ve anlık birim maliyet kaydedilir. | Yetersiz stokta transaction geri alınır. | Stok ve iş emri maliyeti tutarlı güncellenir. |
| UC-11 | İş emrini tamamla | Admin, Supervisor, Technician | İş emri `InProgress`, işçilik süresi pozitiftir. | Parça düşümü, işçilik, maliyet, durum geçmişi ve audit tek transaction'da yazılır. | Herhangi bir hatada bütün işlem geri alınır. | İş emri `Completed` olur. |
| UC-12 | İş emrini kapat | Admin, Supervisor | İş emri `Completed` durumundadır. | Son kontrol yapılıp kapanış zamanı kaydedilir. | Yetkisiz kullanıcı veya eksik bilgi reddedilir. | İş emri `Closed` olur. |
| UC-13 | İş emrini yeniden aç | Admin, Supervisor | İş emri `Completed` durumundadır. | Gerekçe kaydedilip iş tekrar yürütmeye alınır. | Boş gerekçe reddedilir. | İş emri `InProgress` olur. |
| UC-14 | İş emrini iptal et | Admin, Supervisor | İş emri terminal durumda değildir. | Gerekçe alınır, durum geçmişi ve audit yazılır. | Boş gerekçe veya terminal durum reddedilir. | İş emri `Cancelled` olur. |
| UC-15 | Bakım planı yönet | Admin, Supervisor | Ekipman aktiftir. | Periyot, öncelik, süre ve sonraki vade kaydedilir. | Geçersiz periyot veya pasif ekipman reddedilir. | Aktif bakım planı oluşur veya güncellenir. |
| UC-16 | Plandan iş emri üret | Sistem, Admin, Supervisor | Aktif ve vadesi gelen plan vardır. | Motor vade tarihini bulur ve planlı iş emri üretir. | Aynı plan-vade kaydı varsa işlem atlanır. | Tekil planlı iş emri oluşur. |
| UC-17 | Stok girişi yap | Admin, Supervisor | Parça aktiftir. | Pozitif miktar ve maliyet girilir, stok ve hareket kaydedilir. | Geçersiz miktar reddedilir. | Stok artar ve `StockMovement` oluşur. |
| UC-18 | Stok düzelt | Admin, Supervisor | Parça aktiftir. | Yeni miktar ve zorunlu gerekçe kaydedilir. | Negatif sonuç veya boş gerekçe reddedilir. | Stok düzelir ve hareket geçmişi oluşur. |
| UC-19 | KPI ve dashboard görüntüle | Tümü | Kullanıcı oturum açmıştır. | Tarih ve ekipman kapsamı seçilir, KPI ve grafikler hesaplanır. | Veri yoksa sıfır yerine anlamlı `veri yok` durumu gösterilir. | Filtrelenmiş bakım görünümü sunulur. |
| UC-20 | Rapor al | Admin, Supervisor, Operator | Rapor izni vardır. | Rapor türü ve filtre seçilir, önizleme oluşturulur ve PDF/Excel dışa aktarılır. | Geçersiz tarih aralığı reddedilir. | Arşivlenebilir çıktı üretilir. |
| UC-21 | QR ile ekipman bul | Tümü | Geçerli QR etiketi vardır. | QR token okunur ve ekipman kartı açılır. | Bilinmeyen veya silinmiş token için hata gösterilir. | Doğru ekipman bağlamı açılır. |
| UC-22 | Denetim kaydını incele | Admin, Supervisor | Kullanıcı yetkilidir. | Varlık, kullanıcı, işlem ve tarih filtreleriyle eski/yeni değerler görüntülenir. | Yetkisiz erişim reddedilir. | Değişiklik geçmişi salt okunur sunulur. |
| UC-23 | Advisor önerisi görüntüle/yönet | Admin, Supervisor | Kural motoru için veri vardır. | Öneriler kanıt ve önem derecesiyle listelenir, kullanıcı eyleme geçer veya kapatır. | LLM kapalı ya da hatalıysa kural motoru kullanılır. | Önerinin durumu ve yapılan eylem kaydedilir. |
| UC-24 | Kullanıcı ve rol yönet | Admin | Personel kaydı vardır. | Hesap açılır, rol ve aktiflik güncellenir, parola sıfırlanır. | Tekrarlanan kullanıcı adı veya geçersiz rol reddedilir. | Yetkilendirme verisi güncellenir. |
| UC-25 | Grid görünümünü yönet | Yetkili kullanıcı | İlgili liste ekranı açıktır. | Kullanıcı filtreler, gruplar, kolon düzenini kaydeder ve dışa aktarır. | Bozuk kişisel düzen varsayılana döner. | Kişisel görünüm saklanır. |
| UC-26 | Fotoğraf ekle/görüntüle | Yetkili kullanıcı | Ekipman veya arıza vardır. | Dosya doğrulanır, güvenli adla saklanır ve küçük resim üretilir. | Desteklenmeyen tür veya boyut reddedilir. | Galeride ek görünür. |

