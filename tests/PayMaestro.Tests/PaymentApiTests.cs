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
    public async Task ShouldAnswer404WithEmptyBodyWhenPaymentIsUnknown()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
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
}
