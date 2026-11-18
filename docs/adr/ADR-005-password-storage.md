# ADR-005: PBKDF2 ile parola saklama

- Durum: Kabul edildi
- Tarih: 2026-11-18

## Bağlam

Parolalar geri döndürülebilir veya düz metin biçiminde saklanamaz. Her hesap için bağımsız tuz ve zamanlamaya dayanıklı karşılaştırma gerekir.

## Karar

Parolalar kriptografik rastgele tuzla PBKDF2 kullanılarak türetilecek. Algoritma parametreleri kayıtla birlikte sürümlenebilir biçimde saklanacak. Doğrulama sabit zamanlı karşılaştırma kullanacak.

## Sonuçlar

Aynı parolalar farklı hash üretir. Parametre yükseltmesi giriş sırasında yeniden hash stratejisi gerektirir. Parola veya türetilmiş anahtar log'a yazılmaz.

