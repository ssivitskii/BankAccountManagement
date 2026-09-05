using Banking.Application;
using Banking.Domain;
using System.Security.Claims;

namespace Banking.Api;

public sealed class CurrentActorAccessor
{
    public Actor Get(ClaimsPrincipal principal)
    {
        string idValue = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("Authenticated user ID claim is missing.");
        string roleValue = principal.FindFirstValue("role")
            ?? throw new InvalidOperationException("Authenticated role claim is missing.");
        if (!Guid.TryParse(idValue, out Guid userId) || !Enum.TryParse(roleValue, out UserRole role))
            throw new InvalidOperationException("Authentication claims are invalid.");
        return new Actor(userId, role);
    }
}
