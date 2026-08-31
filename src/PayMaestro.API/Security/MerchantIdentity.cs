using System.Security.Claims;

namespace PayMaestro.API.Security;

/// <summary>
/// Reads the merchant from the authenticated caller. One reader, used everywhere the merchant
/// is needed, so the request body and the headers can never become a second source of it.
/// </summary>
public static class MerchantIdentity
{
    public const string ClaimType = "merchant_id";

    /// <summary>
    /// Reserved: an earlier revision of the merchant-scoping migration grouped pre-scoping rows
    /// under this id. No caller may ever authenticate as it — a token carrying it would own
    /// whatever any database migrated with that revision still holds.
    /// </summary>
    public const string ReservedLegacyId = "legacy-unscoped";

    /// <summary>The merchant, or null when the caller carries no merchant identity.</summary>
    public static string? Find(ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimType) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Whether the caller carries a merchant identity the API may act for.</summary>
    public static bool IsUsable(ClaimsPrincipal principal)
        => Find(principal) is { Length: > 0 } merchant && merchant != ReservedLegacyId;

    /// <summary>
    /// The merchant of an authorized caller. Every endpoint that calls this is behind a policy
    /// requiring an authenticated user, so a missing merchant is a configuration fault.
    /// </summary>
    public static string Require(ClaimsPrincipal principal)
        => Find(principal)
        ?? throw new InvalidOperationException("Authenticated merchant identity is missing.");
}
