namespace PayMaestro.Application.Options;

/// <summary>
/// Selects how the orchestrator reaches its acquirers, and how patient it is with them.
/// </summary>
public sealed class PaymentProviderOptions
{
    /// <summary>Acquirers that run inside this process. This is the default.</summary>
    public const string InProcessMode = "InProcess";

    /// <summary>Acquirers reached over HTTP, at <see cref="BaseUrl"/>.</summary>
    public const string HttpMode = "Http";

    public string Mode { get; set; } = InProcessMode;

    public string BaseUrl { get; set; } = "http://localhost:5300";

    /// <summary>How long one call to an acquirer can take before it counts as no answer.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 5;

    /// <summary>How long the whole call, retries included, can take.</summary>
    public int TotalTimeoutSeconds { get; set; } = 15;

    /// <summary>The share of failed calls that opens the circuit.</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>How long the circuit stays open before it lets a call through again.</summary>
    public int CircuitBreakerBreakSeconds { get; set; } = 15;

    /// <summary>The number of calls in the sampling window before the circuit can open.</summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>The sampling window the failure ratio is measured over.</summary>
    public int CircuitBreakerSamplingSeconds { get; set; } = 30;

    /// <summary>How many times a query — never a charge — is repeated after a failure.</summary>
    public int QueryRetryCount { get; set; } = 2;
}
