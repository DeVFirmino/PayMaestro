using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// The HTTP contract as a client sees it, through the real pipeline: controllers, the exception
/// filter and the API behaviour options that unit tests on the use cases never exercise.
/// </summary>
public sealed class PaymentApiTests : IClassFixture<PaymentApiFactory>
{
    private readonly HttpClient _client;

    public PaymentApiTests(PaymentApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ShouldAnswer404WithErrorContractWhenPaymentIsUnknown()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("No payment exists", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ShouldAnswer400WithErrorContractWhenIdempotencyKeyIsMissing()
    {
        HttpResponseMessage response = await PostPaymentAsync(null, "4111111111117777");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Idempotency-Key header is required.", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ShouldAnswer400WithErrorContractWhenRequestValidationFails()
    {
        HttpResponseMessage response = await PostPaymentAsync(Guid.NewGuid().ToString(), "4111111111117777", 0m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Request validation failed.", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ShouldAnswer200WithAttemptTrailWhenPaymentIsCreated()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new
            {
                merchantReference = "ORDER-1",
                customerId = "cust-1",
                amount = 50m,
                currency = "EUR",
                cardNumber = "4111111111117777",
                customerIp = "203.0.113.10",
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Captured", body.RootElement.GetProperty("status").GetString());
        Assert.True(body.RootElement.GetProperty("attempts").GetArrayLength() > 0);
    }

    [Fact]
    public async Task ShouldAnswer422WithoutNewAttemptWhenSameKeyCarriesCardDifferingOnlyInMiddleDigits()
    {
        string key = Guid.NewGuid().ToString();

        HttpResponseMessage first = await PostPaymentAsync(key, "4111111111117777");
        using JsonDocument created = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        string paymentId = created.RootElement.GetProperty("id").GetString()!;

        HttpResponseMessage second = await PostPaymentAsync(key, "4111119999997777");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.True(error.RootElement.GetProperty("error").GetString()!.Length > 0);

        // The refused request reached no provider: the stored payment still has its one attempt.
        using JsonDocument stored = JsonDocument.Parse(await _client.GetStringAsync($"/api/payments/{paymentId}"));
        Assert.Equal(1, stored.RootElement.GetProperty("attempts").GetArrayLength());
    }

    [Fact]
    public async Task ShouldAnswer200WithPaymentListWhenRecoveryRuns()
    {
        HttpResponseMessage response = await _client.PostAsync("/api/payments/recovery", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.GetProperty("payments").ValueKind);
    }

    private async Task<HttpResponseMessage> PostPaymentAsync(string? idempotencyKey, string cardNumber, decimal amount = 50m)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new
            {
                merchantReference = "ORDER-1",
                customerId = "cust-1",
                amount,
                currency = "EUR",
                cardNumber,
                customerIp = "203.0.113.10",
            }),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await _client.SendAsync(request);
    }
}
