using System.Net;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;
using PayMaestro.Domain.Entities;
using PayMaestro.Domain.Enums;
using PayMaestro.Domain.Gateways;

namespace PayMaestro.Infrastructure.PaymentGateways.Http;

/// <summary>
/// An acquirer that lives outside this process and is reached over HTTP.
/// <para>
/// The charge is sent once. It is never repeated automatically, because a repeated charge whose
/// first answer was lost is exactly the case the orchestrator must not guess about. When the
/// call gives no usable answer, the outcome is <see cref="GatewayResultType.Uncertain"/>, and
/// only a query on the same provider idempotency key can settle it.
/// </para>
/// <para>
/// A call the circuit breaker refuses is different: it never left this process, so no money
/// moved. That is reported as <see cref="GatewayResultType.Error"/>, which the cascade may
/// safely carry to the next acquirer.
/// </para>
/// </summary>
public sealed class HttpPaymentGateway : IPaymentGateway
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpPaymentGateway(string name, IHttpClientFactory httpClientFactory)
    {
        Name = name;
        _httpClientFactory = httpClientFactory;
    }

    public string Name { get; }

    public static string ChargeClientName(string gatewayName) => $"provider:{gatewayName}:charge";

    public static string QueryClientName(string gatewayName) => $"provider:{gatewayName}:query";

    public async Task<GatewayResult> ProcessAsync(
        Payment payment, string providerIdempotencyKey, CancellationToken cancellationToken = default)
    {
        // A client from the factory is pooled: it is used, not disposed.
        HttpClient client = _httpClientFactory.CreateClient(ChargeClientName(Name));
        using HttpRequestMessage request = new(HttpMethod.Post, "provider/charges")
        {
            Content = JsonContent.Create(new ProviderChargeRequest
            {
                GatewayName = Name,
                Amount = payment.Amount,
                Currency = payment.Currency,
                CardLast4 = payment.CardLast4
            })
        };
        request.Headers.Add("Idempotency-Key", providerIdempotencyKey);

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                // The acquirer refused the message itself. Nothing was charged.
                return new GatewayResult(GatewayResultType.Error, "bad_request", "The acquirer refused the request.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return NoAnswer($"The acquirer answered {(int)response.StatusCode}.");
            }

            ProviderChargeResponse? body = await response.Content
                .ReadFromJsonAsync<ProviderChargeResponse>(cancellationToken);

            return body is null
                ? NoAnswer("The acquirer answered with an empty body.")
                : Map(body);
        }
        catch (BrokenCircuitException)
        {
            // The call never left this process, so no money moved.
            return new GatewayResult(GatewayResultType.Error, "circuit_open", $"{Name} is not accepting calls.");
        }
        catch (Exception exception) when (exception is TimeoutRejectedException or HttpRequestException or TaskCanceledException)
        {
            return NoAnswer(exception.Message);
        }
    }

    public async Task<GatewayResult> QueryAsync(
        string providerIdempotencyKey, CancellationToken cancellationToken = default)
    {
        HttpClient client = _httpClientFactory.CreateClient(QueryClientName(Name));

        try
        {
            using HttpResponseMessage response = await client.GetAsync(
                $"provider/charges/{Uri.EscapeDataString(providerIdempotencyKey)}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new GatewayResult(
                    GatewayResultType.Error, "not_found", "The provider holds no record for this key.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return NoAnswer($"The acquirer answered {(int)response.StatusCode}.");
            }

            ProviderChargeResponse? body = await response.Content
                .ReadFromJsonAsync<ProviderChargeResponse>(cancellationToken);

            return body is null ? NoAnswer("The acquirer answered with an empty body.") : Map(body);
        }
        catch (Exception exception) when (exception
            is BrokenCircuitException or TimeoutRejectedException or HttpRequestException or TaskCanceledException)
        {
            return NoAnswer(exception.Message);
        }
    }

    private static GatewayResult Map(ProviderChargeResponse body)
        => Enum.TryParse(body.ResultType, ignoreCase: true, out GatewayResultType resultType)
            ? new GatewayResult(resultType, body.ResponseCode, body.Message)
            : NoAnswer($"The acquirer answered with the unknown result type '{body.ResultType}'.");

    private static GatewayResult NoAnswer(string message)
        => new(GatewayResultType.Uncertain, "no_response", message);
}
