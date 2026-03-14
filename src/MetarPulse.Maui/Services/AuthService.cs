using System.Net.Http.Json;
using System.Text.Json;

namespace MetarPulse.Maui.Services;

/// <summary>
/// JWT access token + refresh token yönetimi.
/// Tokenlar MAUI SecureStorage'da saklanır (iOS Keychain, Android Keystore).
/// </summary>
public class AuthService
{
    private readonly HttpClient _http;

    private const string KeyAccessToken  = "mp_access_token";
    private const string KeyRefreshToken = "mp_refresh_token";
    private const string KeyUserId       = "mp_user_id";
    private const string KeyEmail        = "mp_email";

    public AuthService(HttpClient http) => _http = http;

    // ── Token erişimi ─────────────────────────────────────────────────────────

    public async Task<string?> GetAccessTokenAsync()
        => await SecureStorage.Default.GetAsync(KeyAccessToken);

    public string? CurrentUserId { get; private set; }
    public string? CurrentEmail  { get; private set; }
    public bool IsAuthenticated  => CurrentUserId is not null;

    public event Action? OnAuthStateChanged;

    public async Task InitializeAsync()
    {
        CurrentUserId = await SecureStorage.Default.GetAsync(KeyUserId);
        CurrentEmail  = await SecureStorage.Default.GetAsync(KeyEmail);
    }

    // ── Giriş (e-posta + şifre) ───────────────────────────────────────────────

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login",
                new { Email = email, Password = password });

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
                return AuthResult.Fail(err?.Message ?? "Giriş başarısız.");
            }

            var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokens is null) return AuthResult.Fail("Sunucu yanıtı okunamadı.");

            await StoreTokensAsync(tokens);
            return AuthResult.Ok();
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Bağlantı hatası: {ex.Message}");
        }
    }

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    public async Task<AuthResult> RegisterAsync(string email, string password, string? displayName = null)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/register",
                new { Email = email, Password = password, DisplayName = displayName });

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
                return AuthResult.Fail(err?.Message ?? "Kayıt başarısız.");
            }

            var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokens is null) return AuthResult.Fail("Sunucu yanıtı okunamadı.");

            await StoreTokensAsync(tokens);
            return AuthResult.Ok();
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Bağlantı hatası: {ex.Message}");
        }
    }

    // ── Magic Link ────────────────────────────────────────────────────────────

    public async Task<AuthResult> SendMagicLinkAsync(string email)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/magic-link/send",
                new { Email = email });
            return resp.IsSuccessStatusCode
                ? AuthResult.Ok()
                : AuthResult.Fail("Magic link gönderilemedi.");
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Bağlantı hatası: {ex.Message}");
        }
    }

    public async Task<AuthResult> VerifyMagicLinkAsync(string token)
    {
        try
        {
            var resp = await _http.GetAsync($"api/auth/magic-link/verify?token={Uri.EscapeDataString(token)}");
            if (!resp.IsSuccessStatusCode) return AuthResult.Fail("Geçersiz veya süresi dolmuş bağlantı.");

            var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokens is null) return AuthResult.Fail("Sunucu yanıtı okunamadı.");

            await StoreTokensAsync(tokens);
            return AuthResult.Ok();
        }
        catch (Exception ex)
        {
            return AuthResult.Fail($"Bağlantı hatası: {ex.Message}");
        }
    }

    // ── Token yenileme ────────────────────────────────────────────────────────

    public async Task<bool> TryRefreshAsync()
    {
        var refreshToken = await SecureStorage.Default.GetAsync(KeyRefreshToken);
        if (refreshToken is null) return false;

        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/refresh",
                new { RefreshToken = refreshToken });

            if (!resp.IsSuccessStatusCode) { await ClearAsync(); return false; }

            var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokens is null) { await ClearAsync(); return false; }

            await StoreTokensAsync(tokens);
            return true;
        }
        catch { return false; }
    }

    // ── Çıkış ────────────────────────────────────────────────────────────────

    public async Task LogoutAsync()
    {
        try { await _http.PostAsync("api/auth/logout", null); } catch { }
        await ClearAsync();
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private async Task StoreTokensAsync(TokenResponse tokens)
    {
        await SecureStorage.Default.SetAsync(KeyAccessToken,  tokens.AccessToken);
        await SecureStorage.Default.SetAsync(KeyRefreshToken, tokens.RefreshToken);
        await SecureStorage.Default.SetAsync(KeyUserId,       tokens.User.Id);
        await SecureStorage.Default.SetAsync(KeyEmail,        tokens.User.Email);

        CurrentUserId = tokens.User.Id;
        CurrentEmail  = tokens.User.Email;
        OnAuthStateChanged?.Invoke();
    }

    private async Task ClearAsync()
    {
        SecureStorage.Default.Remove(KeyAccessToken);
        SecureStorage.Default.Remove(KeyRefreshToken);
        SecureStorage.Default.Remove(KeyUserId);
        SecureStorage.Default.Remove(KeyEmail);
        CurrentUserId = null;
        CurrentEmail  = null;
        OnAuthStateChanged?.Invoke();
        await Task.CompletedTask;
    }

    // ── Private DTO'lar ───────────────────────────────────────────────────────

    private record TokenResponse(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAt,
        UserDto User);

    private record UserDto(
        string Id,
        string Email,
        string? DisplayName,
        string PreferredLanguage,
        string PreferredUnits,
        bool IsOnboardingCompleted,
        DateTime CreatedAt);

    private record ErrorResponse(string? Message);
}

// ── Auth sonuç wrapper ────────────────────────────────────────────────────────
public record AuthResult(bool Success, string? Error = null)
{
    public static AuthResult Ok()           => new(true);
    public static AuthResult Fail(string e) => new(false, e);
}
