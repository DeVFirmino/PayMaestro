using System.Security.Claims;

namespace PayMaestro.API.Security;

/// <summary>
/// Reads the merchant from the authenticated caller. One reader, used everywhere the merchant
/// is needed, so the request body and the headers can never become a second source of it.
/// </summary>
public static class MerchantIdentity
{
    public const string ClaimType = "merchant_id";

    /// <summary>The merchant, or null when the caller carries no merchant identity.</summary>
    public static string? Find(ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimType) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// The merchant of an authorized caller. Every endpoint that calls this is behind a policy
    /// requiring an authenticated user, so a missing merchant is a configuration fault.
    /// </summary>
    public static string Require(ClaimsPrincipal principal)
        => Find(principal)
        ?? throw new InvalidOperationException("Authenticated merchant identity is missing.");
}
