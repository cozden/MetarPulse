# MetarPulse Admin Panel — Geliştirme Planı

## Genel Bilgi
- **Proje:** `src/MetarPulse.Admin`
- **Tip:** Blazor Server (.NET 10)
- **Port:** 5001 (API: 5000, Web WASM: 8080)
- **Auth:** Cookie tabanlı (ASP.NET Core Identity — aynı DB)
- **UI:** Tailwind CSS + DaisyUI (dark mode, API ile aynı stil)
- **Erişim:** Sadece `Role = "Admin"` kullanıcılar

---

## Modüller & İlerleme

### Modül 1 — Proje İskeleti ✅
- [x] `src/MetarPulse.Admin` Blazor Server projesi oluştur
- [x] Solution'a ekle (`MetarPulse.slnx`)
- [x] Bağımlılıklar: Core, Abstractions, Infrastructure referansları
- [x] NuGet: Tailwind CDN + DaisyUI CDN (App.razor'da)
- [x] `appsettings.json`: ConnectionStrings, Cookie config
- [x] `Program.cs`: DbContext, Identity, Cookie auth, Authorization, CascadingAuthState
- [x] Layout: MainLayout (sol sidebar, nav linkleri), EmptyLayout (login için)
- [x] Auth: `/login` sayfası, `RedirectToLogin` bileşeni, `[Authorize(Roles="Admin")]` guard
- [ ] Docker: `docker-compose.yml`'e `metarpulse-admin` servisi ekle (port 5001)

### Modül 2 — Dashboard (Ana Sayfa) ✅
- [x] Özet kartlar: Toplam kullanıcı, kayıtlı meydan, METAR 24h, NOTAM sayısı
- [x] Provider durum listesi (OK/HATA/Circuit/Pasif) + sağlıklı/toplam badge
- [x] Hızlı erişim butonları (Provider'lar, Kullanıcılar, Cache, NOTAM)
- [x] GET /api/admin/stats endpoint (AdminController.cs)

### Modül 3 — Provider Yönetimi ✅
- [x] Provider listesi: Ad, Durum, Circuit Breaker, Öncelik, Son başarı/hata zamanı
- [x] Yukarı/aşağı öncelik değiştir (▲▼) → POST /api/providers/reorder
- [x] Aktif/Pasif toggle → POST /api/providers/{name}/enable
- [x] Provider test butonu → POST /api/providers/{name}/test (HealthCheckAsync)
- [x] Admin HttpClient: "MetarPulseApi" named client (Program.cs + appsettings.json ApiBaseUrl)

### Modül 4 — Kullanıcı Yönetimi ✅
- [x] Kullanıcı listesi: E-posta, isim, rol, kayıt tarihi, son giriş, onboarding
- [x] Rol değiştir (User ↔ Admin) → PUT /api/admin/users/{id}/role
- [x] Kullanıcı sil (soft-delete) → DELETE /api/admin/users/{id} + onay modal
- [x] Arama & filtreleme (e-posta/isim, Enter ile arama)
- [x] Sayfalama (20/sayfa)
- [x] GET /api/admin/users, PUT /api/admin/users/{id}/role, DELETE /api/admin/users/{id}

### Modül 5 — METAR/TAF Cache Yönetimi ✅
- [x] Cache listesi: ICAO, kategori badge, gözlem zamanı (yaş), kaynak provider, sayfalama
- [x] Ham METAR göster/gizle (expandable ▼/▲)
- [x] Tek meydan yenile → POST /api/admin/cache/{icao}/refresh
- [x] Tümünü Yenile (bookmark'lı meydanlar) → POST /api/admin/cache/refresh-all
- [x] Cache temizle (onay modal) → DELETE /api/admin/cache/{icao}
- [x] GET /api/admin/cache (GroupBy StationId, en güncel), arama, sayfalama

### Modül 6 — NOTAM Yönetimi ✅
- [x] NOTAM listesi: ID, ICAO, VFR etki (Warning/Caution/Advisory), Trafik, Süre, Konu, Kaynak
- [x] Süresi dolanlar gri gösterilir (IsExpired), genişletilmiş NOTAM metni (▼/▲)
- [x] Filtreler: ICAO, VFR etki, sadece aktif checkbox
- [x] Polling tetikle → POST /api/admin/notams/poll (yeni NOTAM'ları çeker)
- [x] Süresi dolan toplu temizleme → DELETE /api/admin/notams/expired
- [x] Tek NOTAM sil (onay modal) → DELETE /api/admin/notams/{id}
- [x] GET/DELETE /api/admin/notams, POST /api/admin/notams/poll

### Modül 7 — Log Görüntüleyici ✅
- [x] Serilog in-memory sink: `AdminLogBuffer` (circular, son 1000 kayıt) + `AdminLogBufferSink`
- [x] Log tablosu: Zaman | Level badge (ERR/WRN/INF/DBG) | Kaynak (kısa sınıf adı) | Mesaj | Hata
- [x] Filtreler: level select, kaynak text filtresi, limit select (50/100/200/500)
- [x] Otomatik yenileme toggle (5 sn interval, `IAsyncDisposable` ile temiz cleanup)
- [x] Log buffer temizle butonu → DELETE /api/admin/logs
- [x] GET /api/admin/logs?level=&source=&limit=, DELETE /api/admin/logs

### Modül 8 — Sistem Ayarları ✅
- [x] Sistem bilgisi: Ortam, .NET versiyonu, Uptime (dd:hh:mm:ss), DB bağlantı durumu
- [x] Bakım modu toggle (503 middleware) → PUT /api/admin/settings/maintenance
- [x] FCM durumu (service account dosyası var mı)
- [x] Provider API key durumu (AVWX, CheckWX)
- [x] Polling interval bilgisi (read-only, hardcoded değerler)
- [x] GET /api/admin/settings, PUT /api/admin/settings/maintenance
- [x] SystemSettingsService (singleton, uptime + maintenance flag)

---

## API Endpoint Planı (MetarPulse.Api)

Tüm endpoint'ler `[Authorize(Roles = "Admin")]` ile korunacak:

```
GET    /api/admin/stats
GET    /api/admin/providers
PUT    /api/admin/providers/{id}
POST   /api/admin/providers/{id}/test
GET    /api/admin/users
GET    /api/admin/users/{id}
PUT    /api/admin/users/{id}
DELETE /api/admin/users/{id}
GET    /api/admin/cache
POST   /api/admin/cache/{icao}/refresh
POST   /api/admin/cache/refresh-all
GET    /api/admin/notams
POST   /api/admin/notams/poll
GET    /api/admin/logs
GET    /api/admin/settings
PUT    /api/admin/settings
```

---

## Teknik Notlar
- Admin projesi Infrastructure'ı doğrudan kullanır (DbContext inject)
- Cookie auth: `AddAuthentication().AddCookie()` — JWT değil (browser tabanlı)
- Provider durumları DB'de `WeatherProviders` tablosuna yansıtılacak (veya memory cache)
- Blazor Server: InteractiveServer render mode, SignalR circuit
- `AdminController.cs` API'ye eklenecek (ayrı controller dosyası)

---

## Öncelik Sırası
1. **Modül 1** — İskelet & auth
2. **Modül 3** — Provider yönetimi (en kritik)
3. **Modül 2** — Dashboard
4. **Modül 4** — Kullanıcı yönetimi
5. **Modül 5** — Cache yönetimi
6. **Modül 6** — NOTAM yönetimi
7. **Modül 7** — Log görüntüleyici
8. **Modül 8** — Sistem ayarları

---

## İlerleme Özeti
- [x] Modül 1 — İskelet ✅
- [x] Modül 2 — Dashboard ✅
- [x] Modül 3 — Provider Yönetimi ✅
- [x] Modül 4 — Kullanıcı Yönetimi ✅
- [x] Modül 5 — Cache Yönetimi ✅
- [x] Modül 6 — NOTAM Yönetimi ✅
- [x] Modül 7 — Log Görüntüleyici ✅
- [x] Modül 8 — Sistem Ayarları ✅
