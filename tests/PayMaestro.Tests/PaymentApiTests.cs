using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PayMaestro.API.Security;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The HTTP contract as a client sees it, through the real pipeline: controllers, the exception
/// filter and the API behaviour options that unit tests on the use cases never exercise.
/// </summary>
public class PaymentApiTests : IClassFixture<PaymentApiFactory>
{
    private readonly HttpClient _client;

    public PaymentApiTests(PaymentApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Getting_an_unknown_payment_returns_404_with_an_empty_body()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Creating_a_payment_returns_200_with_the_attempt_trail()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new
            {
                merchantReference = "ORDER-1",
                customerId = "cust-1",
                amount = 50m,
                currency = "EUR",
                cardNumber = "4111111111117777",
                customerIp = "203.0.113.10"
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Captured", body.RootElement.GetProperty("status").GetString());
        Assert.True(body.RootElement.GetProperty("attempts").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Should_reach_the_beta_scenario_when_the_configured_route_is_used()
    {
        HttpResponseMessage response = await PostPayment("4111111111112222");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement attempts = body.RootElement.GetProperty("attempts");

        Assert.Equal(3, attempts.GetArrayLength());
        Assert.Equal("BetaPay", attempts[1].GetProperty("gatewayName").GetString());
        Assert.Equal("SoftDecline", attempts[1].GetProperty("resultType").GetString());
        Assert.Equal("GammaPay", attempts[2].GetProperty("gatewayName").GetString());
        Assert.Equal("Approved", attempts[2].GetProperty("resultType").GetString());
        Assert.Equal("Captured", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Should_stop_the_cascade_after_one_attempt_when_the_card_is_hard_declined()
    {
        HttpResponseMessage response = await PostPayment("4111111111110000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Declined", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("attempts").GetArrayLength());
    }

    [Fact]
    public async Task Should_wait_for_reconciliation_when_a_charge_gets_no_answer()
    {
        HttpResponseMessage response = await PostPayment("4111111111119999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("RequiresReconciliation", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Should_use_the_next_gateway_when_the_amount_is_above_the_first_cap()
    {
        HttpResponseMessage response = await PostPayment("4111111111117777", amount: 6000m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement attempts = body.RootElement.GetProperty("attempts");

        Assert.Equal("Captured", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, attempts.GetArrayLength());
        Assert.Equal("BetaPay", attempts[0].GetProperty("gatewayName").GetString());
    }

    [Fact]
    public async Task Should_decline_without_an_attempt_when_no_gateway_accepts_the_currency()
    {
        HttpResponseMessage response = await PostPayment("4111111111117777", currency: "JPY");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Declined", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("attempts").GetArrayLength());
    }

    [Fact]
    public async Task Should_reach_the_gamma_scenario_when_the_configured_route_is_used()
    {
        HttpResponseMessage response = await PostPayment("4111111111113333");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement attempts = body.RootElement.GetProperty("attempts");

        Assert.Equal(3, attempts.GetArrayLength());
        Assert.Equal("GammaPay", attempts[2].GetProperty("gatewayName").GetString());
        Assert.Equal("Error", attempts[2].GetProperty("resultType").GetString());
        Assert.Equal("Declined", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Should_refuse_the_request_when_the_merchant_claim_is_the_reserved_legacy_id()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/payments/{Guid.NewGuid()}");
        request.Headers.Add(
            PaymentApiFactory.TestAuthenticationHandler.MerchantHeader,
            MerchantIdentity.ReservedLegacyId);

        HttpResponseMessage response = await _client.SendAsync(request);

        // The reserved id may never act as a caller: any database migrated with the revision
        // that grouped legacy rows under it would hand that caller another tenant's history.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Should_refuse_the_request_when_the_subject_claim_is_the_reserved_legacy_id()
    {
        // MerchantIdentity falls back to NameIdentifier when merchant_id is absent, so the
        // reserved id must be refused on that route too.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/payments/{Guid.NewGuid()}");
        request.Headers.Add(
            PaymentApiFactory.TestAuthenticationHandler.SubjectOnlyHeader,
            MerchantIdentity.ReservedLegacyId);

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Should_refuse_the_request_when_the_caller_is_anonymous()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/payments/{Guid.NewGuid()}");
        request.Headers.Add(PaymentApiFactory.TestAuthenticationHandler.AnonymousHeader, "true");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_report_healthy_when_the_health_endpoint_is_called()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Should_answer_422_as_problem_details_when_a_key_is_reused_with_a_changed_payload()
    {
        string key = Guid.NewGuid().ToString();

        HttpResponseMessage first = await PostPayment("4111111111117777", key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same BIN, same last four digits, different card number: a different payment.
        HttpResponseMessage second = await PostPayment("4111119999997777", key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        using JsonDocument body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(422, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("detail").GetString()));
    }

    private async Task<HttpResponseMessage> PostPayment(
        string cardNumber, decimal amount = 50m, string currency = "EUR")
        => await PostPayment(cardNumber, Guid.NewGuid().ToString(), amount, currency);

    private async Task<HttpResponseMessage> PostPayment(
        string cardNumber, string idempotencyKey, decimal amount = 50m, string currency = "EUR")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new
            {
                merchantReference = Guid.NewGuid().ToString("N"),
                customerId = "cust-1",
                amount,
                currency,
                cardNumber,
                customerIp = "203.0.113.10"
            })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return await _client.SendAsync(request);
    }
}
