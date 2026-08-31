using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace PayMaestro.API.Security;

/// <summary>
/// Expands OAuth 2.0 scope claims into one claim per scope.
/// <para>
/// An authorization server sends the granted scopes as a single space-delimited string —
/// "payments:read payments:write" — under "scope", or as "scp". A policy that requires a claim
/// with an exact value matches neither form, so a valid token would be refused. This splits
/// them once, before authorization runs.
/// </para>
/// </summary>
public sealed class ScopeClaimTransformation : IClaimsTransformation
{
    public const string ScopeClaimType = "scope";
    private const string AlternateScopeClaimType = "scp";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        HashSet<string> granted = new(StringComparer.Ordinal);

        foreach (Claim claim in principal.Claims)
        {
            if (claim.Type is not (ScopeClaimType or AlternateScopeClaimType))
            {
                continue;
            }

            string[] scopes = claim.Value.Split(
                ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string scope in scopes)
            {
                if (principal.HasClaim(ScopeClaimType, scope) is false)
                {
                    granted.Add(scope);
                }
            }
        }

        if (granted.Count == 0 || principal.Identity is not ClaimsIdentity)
        {
            return Task.FromResult(principal);
        }

        // The incoming principal is not mutated: this runs on every request, and adding to it
        // in place would accumulate duplicates across repeated transformations.
        ClaimsPrincipal expanded = principal.Clone();
        ((ClaimsIdentity)expanded.Identity!).AddClaims(granted.Select(scope => new Claim(ScopeClaimType, scope)));

        return Task.FromResult(expanded);
    }
}
