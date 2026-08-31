using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task Configured_route_reaches_the_beta_2222_scenario()
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
    public async Task A_hard_declined_card_stops_the_cascade_after_one_attempt()
    {
        HttpResponseMessage response = await PostPayment("4111111111110000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Declined", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("attempts").GetArrayLength());
    }

    [Fact]
    public async Task A_charge_without_an_answer_waits_for_reconciliation()
    {
        HttpResponseMessage response = await PostPayment("4111111111119999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("RequiresReconciliation", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_amount_above_the_first_cap_goes_to_the_next_gateway()
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
    public async Task A_currency_no_gateway_accepts_is_declined_without_an_attempt()
    {
        HttpResponseMessage response = await PostPayment("4111111111117777", currency: "JPY");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Declined", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("attempts").GetArrayLength());
    }

    [Fact]
    public async Task Configured_route_reaches_the_gamma_3333_scenario()
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
    public async Task An_anonymous_caller_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/payments/{Guid.NewGuid()}");
        request.Headers.Add(PaymentApiFactory.TestAuthenticationHandler.AnonymousHeader, "true");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_health_endpoint_reports_the_service_as_healthy()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reusing_a_key_with_a_changed_payload_returns_422_as_problem_details()
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
