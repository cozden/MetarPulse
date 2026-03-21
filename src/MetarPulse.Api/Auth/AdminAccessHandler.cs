using Microsoft.AspNetCore.Authorization;

namespace MetarPulse.Api.Auth;

public class AdminAccessRequirement : IAuthorizationRequirement { }

public class AdminAccessHandler : AuthorizationHandler<AdminAccessRequirement>
{
    private readonly IConfiguration _config;

    public AdminAccessHandler(IConfiguration config) => _config = config;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminAccessRequirement requirement)
    {
        // JWT Admin rolü varsa geç
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // X-Admin-Key header ile iç erişim
        if (context.Resource is HttpContext http)
        {
            var expectedKey = _config["Admin:InternalApiKey"];
            if (!string.IsNullOrWhiteSpace(expectedKey))
            {
                var sentKey = http.Request.Headers["X-Admin-Key"].ToString();
                if (sentKey == expectedKey)
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
        }

        return Task.CompletedTask;
    }
}
