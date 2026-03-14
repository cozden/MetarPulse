namespace MetarPulse.Maui.Services;

/// <summary>
/// HttpClient DelegatingHandler — her istekte Bearer token ekler.
/// Token yoksa veya 401 gelirse token yenilemeyi dener.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly AuthService _auth;

    public AuthHeaderHandler(AuthService auth) => _auth = auth;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (token is not null)
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);

        // 401 → token yenileme dene
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && await _auth.TryRefreshAsync())
        {
            var newToken = await _auth.GetAccessTokenAsync();
            if (newToken is not null)
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
                response = await base.SendAsync(request, ct);
            }
        }

        return response;
    }
}
