# MetarPulse — QNAP + Cloudflare Tunnel Kurulum Kılavuzu

Android telefonda ve dış ağda test için QNAP NAS üzerinde Docker kurulumu.

---

## Aşama 1 — QNAP'ı Hazırlama

- [ ] QNAP web arayüzüne gir (`http://192.168.x.x:8080` veya NAS'ın IP'si)
- [ ] **App Center** → **Container Station** ara → Yükle
- [ ] **Control Panel → Terminal & SNMP → SSH** → "SSH servisine izin ver" → Uygula
- [ ] Bilgisayardan SSH bağlantısını test et:
  ```bash
  ssh admin@192.168.x.x
  ```

---

## Aşama 2 — Proje Dosyalarını QNAP'a Aktarma

- [ ] **File Station** → `/share/Container/metarpulse/` klasörü oluştur
- [ ] Bilgisayarından kopyala:
  ```bash
  scp -r "C:\Users\canel\source\repos\Antigravity\MetarTaf\MetarPulse" admin@192.168.x.x:/share/Container/metarpulse/
  ```

---

## Aşama 3 — Ortam Değişkenleri (.env)

- [ ] SSH ile QNAP'a bağlan, proje klasörüne git:
  ```bash
  cd /share/Container/metarpulse/MetarPulse
  ```
- [ ] `.env` dosyası oluştur (şifreleri kendin belirle):
  ```bash
  cat > .env << 'EOF'
  POSTGRES_PASSWORD=BuriyaGucluBirSifreYaz123!
  JWT_SECRET=BuriyaEnAz32KarakterlikGizliAnahtar_XYZ!
  CLOUDFLARE_TUNNEL_TOKEN=buraya_cloudflare_token_gelecek
  EOF
  ```

---

## Aşama 4 — Cloudflare Tunnel Kurulumu (Ücretsiz, Port Açmana Gerek Yok)

- [ ] [cloudflare.com](https://cloudflare.com) → ücretsiz hesap aç
- [ ] Sol menü → **Zero Trust** → **Networks** → **Tunnels** → **Create a tunnel**
- [ ] Tunnel adı: `metarpulse` → **Save tunnel**
- [ ] Connector olarak **Docker** seç → Verilen token'ı kopyala
- [ ] Token'ı `.env` dosyasındaki `CLOUDFLARE_TUNNEL_TOKEN` alanına yapıştır
- [ ] Cloudflare panelinde **Public Hostname** ekle:
  - `api.senindomain.com` → Service: `http://api:5000`
  - `app.senindomain.com` → Service: `http://web:8080`
  > **Domain yoksa:** Tunnel panelinde "Quick Tunnels" seç → `*.trycloudflare.com` subdomain alırsın (ücretsiz, domain gerekmez)

---

## Aşama 5 — Servisleri QNAP'ta Başlat

- [ ] QNAP SSH terminalinde:
  ```bash
  cd /share/Container/metarpulse/MetarPulse
  docker compose up -d --build
  ```
  > İlk build 5-10 dakika sürer
- [ ] Durumu kontrol et:
  ```bash
  docker compose ps
  docker compose logs api --tail=50
  ```
- [ ] Tüm container'lar `Up (healthy)` görünmeli
- [ ] API'yi test et: `https://api.senindomain.com/health` tarayıcıdan aç

---

## Aşama 6 — Android APK Build (Geliştirme Bilgisayarında)

- [ ] `src/MetarPulse.Maui/MauiProgram.cs` içindeki RELEASE URL'ini güncelle:
  ```csharp
  var apiBaseUrl = "https://api.senindomain.com/";
  ```
- [ ] Release APK derle:
  ```bash
  cd src\MetarPulse.Maui
  dotnet publish -f net10.0-android -c Release
  ```
- [ ] APK şurada oluşur:
  ```
  bin\Release\net10.0-android\publish\com.metarpulse.app-Signed.apk
  ```

---

## Aşama 7 — APK'yı Tester'lara Dağıt

- [ ] APK dosyasını WhatsApp / Telegram / e-posta ile tester'a gönder
- [ ] Tester'ın telefonda yapması gerekenler:
  1. **Ayarlar → Uygulamalar** → sağ üst 3 nokta → **Özel erişim** → **Bilinmeyen kaynaklardan yükle** → ilgili uygulama (WhatsApp vb.) için izin ver
  2. APK dosyasına dokun → **Yükle**
  3. Uygulama ikonuna dokun → Aç

---

## Özet: Ne Nerede Çalışır?

| Bileşen | Nerede | Adres |
|---|---|---|
| PostgreSQL | QNAP Docker | İçeride, dışa kapalı |
| API | QNAP Docker | `https://api.senindomain.com` |
| Web (Blazor) | QNAP Docker | `https://app.senindomain.com` |
| Android APK | S24 Ultra | API'ye HTTPS ile bağlanır |
| Cloudflare Tunnel | QNAP Docker | Köprü — router'da port açmana gerek yok |

---

## Günlük Yönetim Komutları

```bash
# Logları izle
docker compose logs -f

# Servisleri yeniden başlat
docker compose restart

# Güncelleme deploy et
git pull
docker compose up -d --build

# Durdur
docker compose down

# Veritabanı dahil tamamen sıfırla (dikkatli!)
docker compose down -v
```
