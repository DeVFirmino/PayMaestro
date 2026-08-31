using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;
using PayMaestro.Infrastructure;
using PayMaestro.Infrastructure.PaymentGateways.Http;
using PayMaestro.Tests.Support;

namespace PayMaestro.Tests;

/// <summary>
/// These tests speak to the fake acquirer over HTTP. They prove the boundary, not a stub:
/// the request leaves through an HttpClient and the answer comes back as JSON.
/// </summary>
public class HttpPaymentGatewayTests
{
    [Fact]
    public async Task Should_approve_when_the_acquirer_approves_the_card()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("AlphaPay", provider.HttpClientFactory);

        GatewayResult result = await gateway.ProcessAsync(PaymentWith("7777"), "merchant-1:key-1:AlphaPay:1");

        Assert.Equal(GatewayResultType.Approved, result.ResultType);
        Assert.Equal("00", result.ResponseCode);
    }

    [Fact]
    public async Task Should_soft_decline_when_the_card_routes_past_the_first_acquirer()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("AlphaPay", provider.HttpClientFactory);

        GatewayResult result = await gateway.ProcessAsync(PaymentWith("1111"), "merchant-1:key-2:AlphaPay:1");

        Assert.Equal(GatewayResultType.SoftDecline, result.ResultType);
    }

    [Fact]
    public async Task Should_hard_decline_when_the_acquirer_reports_a_stolen_card()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("BetaPay", provider.HttpClientFactory);

        GatewayResult result = await gateway.ProcessAsync(PaymentWith("0000"), "merchant-1:key-3:BetaPay:1");

        Assert.Equal(GatewayResultType.HardDecline, result.ResultType);
    }

    [Fact]
    public async Task Should_report_an_unavailable_acquirer_as_an_error()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("GammaPay", provider.HttpClientFactory);

        GatewayResult result = await gateway.ProcessAsync(PaymentWith("3333"), "merchant-1:key-4:GammaPay:1");

        Assert.Equal(GatewayResultType.Error, result.ResultType);
        Assert.Equal("96", result.ResponseCode);
    }

    [Fact]
    public async Task Should_report_an_uncertain_outcome_when_the_answer_is_lost()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("AlphaPay", provider.HttpClientFactory);

        GatewayResult result = await gateway.ProcessAsync(PaymentWith("9999"), "merchant-1:key-5:AlphaPay:1");

        Assert.Equal(GatewayResultType.Uncertain, result.ResultType);
    }

    [Fact]
    public async Task Should_answer_a_query_with_the_outcome_the_acquirer_holds_for_a_lost_answer()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("AlphaPay", provider.HttpClientFactory);
        const string providerKey = "merchant-1:key-6:AlphaPay:1";

        await gateway.ProcessAsync(PaymentWith("9999"), providerKey);
        GatewayResult recovered = await gateway.QueryAsync(providerKey);

        // The money moved before the answer was lost, so the acquirer reports it as approved.
        Assert.Equal(GatewayResultType.Approved, recovered.ResultType);
    }

    [Fact]
    public async Task Should_report_no_record_when_the_acquirer_never_saw_the_key()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("AlphaPay", provider.HttpClientFactory);

        GatewayResult result = await gateway.QueryAsync("merchant-1:key-never-sent:AlphaPay:1");

        Assert.Equal(GatewayResultType.Error, result.ResultType);
        Assert.Equal("not_found", result.ResponseCode);
    }

    [Fact]
    public async Task Should_return_the_first_outcome_when_the_same_provider_key_is_sent_again()
    {
        using var provider = new FakeProviderHost();
        var gateway = new HttpPaymentGateway("AlphaPay", provider.HttpClientFactory);
        const string providerKey = "merchant-1:key-7:AlphaPay:1";

        GatewayResult first = await gateway.ProcessAsync(PaymentWith("7777"), providerKey);
        GatewayResult second = await gateway.ProcessAsync(PaymentWith("7777"), providerKey);

        Assert.Equal(first.ResultType, second.ResultType);
        Assert.Equal(first.ResponseCode, second.ResponseCode);
    }

    [Fact]
    public void Should_register_one_http_gateway_per_configured_route_when_the_http_mode_is_selected()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentSecurity:FingerprintSecret"] = "test-fingerprint-secret",
                ["PaymentProviders:Mode"] = "Http",
                ["PaymentProviders:BaseUrl"] = "http://fake-provider:8080",
                ["GatewayRouting:Gateways:0:Name"] = "AlphaPay",
                ["GatewayRouting:Gateways:0:Priority"] = "1",
                ["GatewayRouting:Gateways:1:Name"] = "BetaPay",
                ["GatewayRouting:Gateways:1:Priority"] = "2"
            })
            .Build();

        services.AddInfrastructure(configuration);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        string[] names = serviceProvider.GetServices<IPaymentGateway>()
            .Select(gateway => gateway.Name)
            .ToArray();

        Assert.Equal(["AlphaPay", "BetaPay"], names);
        Assert.All(serviceProvider.GetServices<IPaymentGateway>(), gateway => Assert.IsType<HttpPaymentGateway>(gateway));

        IHttpClientFactory clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using HttpClient charge = clientFactory.CreateClient(HttpPaymentGateway.ChargeClientName("AlphaPay"));
        Assert.Equal("http://fake-provider:8080/", charge.BaseAddress?.ToString());
    }

    private static Payment PaymentWith(string cardLast4)
        => TestPayment.New(idempotencyKey: $"key-{cardLast4}", cardLast4: cardLast4);
}
