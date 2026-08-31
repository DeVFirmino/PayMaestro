namespace PayMaestro.API.Security;

/// <summary>The authorization policies, and the scopes a token must carry to satisfy them.</summary>
public static class PaymentPolicies
{
    public const string Write = "payments:write";
    public const string Read = "payments:read";
    public const string Reconcile = "payments:reconcile";
}
