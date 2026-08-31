using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PayMaestro.API.Security;

namespace PayMaestro.Tests;

/// <summary>
/// The shape a real authorization server sends. A token carries its granted scopes as one
/// space-delimited string, so these tests run the policies against that shape rather than
/// against the one-claim-per-scope shape a test double would hand them.
/// </summary>
public class ScopeClaimTransformationTests
{
    [Fact]
    public async Task Should_grant_every_scope_when_one_claim_holds_them_space_delimited()
    {
        ClaimsPrincipal caller = Caller(new Claim("scope", "payments:read payments:write"));

        ClaimsPrincipal expanded = await new ScopeClaimTransformation().TransformAsync(caller);

        Assert.True(expanded.HasClaim("scope", "payments:read"));
        Assert.True(expanded.HasClaim("scope", "payments:write"));
    }

    [Fact]
    public async Task Should_grant_every_scope_when_the_claim_is_named_scp()
    {
        ClaimsPrincipal caller = Caller(new Claim("scp", "payments:reconcile payments:read"));

        ClaimsPrincipal expanded = await new ScopeClaimTransformation().TransformAsync(caller);

        Assert.True(expanded.HasClaim("scope", "payments:reconcile"));
        Assert.True(expanded.HasClaim("scope", "payments:read"));
    }

    [Fact]
    public async Task Should_not_duplicate_a_scope_when_the_transformation_runs_twice()
    {
        ScopeClaimTransformation transformation = new();
        ClaimsPrincipal caller = Caller(new Claim("scope", "payments:write"));

        ClaimsPrincipal once = await transformation.TransformAsync(caller);
        ClaimsPrincipal twice = await transformation.TransformAsync(once);

        Assert.Single(twice.FindAll("scope"), claim => claim.Value == "payments:write");
    }

    [Fact]
    public async Task Should_satisfy_the_write_policy_when_the_token_carries_a_space_delimited_scope()
    {
        ClaimsPrincipal caller = Caller(new Claim("scope", "payments:read payments:write"));

        AuthorizationResult result = await Authorize(caller, PaymentPolicies.Write);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Should_refuse_the_reconcile_policy_when_the_token_lacks_that_scope()
    {
        ClaimsPrincipal caller = Caller(new Claim("scope", "payments:read payments:write"));

        AuthorizationResult result = await Authorize(caller, PaymentPolicies.Reconcile);

        Assert.False(result.Succeeded);
    }

    /// <summary>Runs the real policy registration over the caller, transformation included.</summary>
    private static async Task<AuthorizationResult> Authorize(ClaimsPrincipal caller, string policy)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddPaymentAuthorization();

        using ServiceProvider provider = services.BuildServiceProvider();
        ClaimsPrincipal expanded = await new ScopeClaimTransformation().TransformAsync(caller);

        return await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(expanded, resource: null, policy);
    }

    private static ClaimsPrincipal Caller(params Claim[] claims)
        => new(new ClaimsIdentity([new Claim("merchant_id", "merchant-1"), .. claims], "Bearer"));
}
