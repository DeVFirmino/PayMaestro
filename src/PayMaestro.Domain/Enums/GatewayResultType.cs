namespace PayMaestro.Domain.Enums;

public enum GatewayResultType
{
    Approved,
    SoftDecline,
    HardDecline,

    /// <summary>The gateway refused the request before processing it. Nothing was charged, so the cascade may continue.</summary>
    Error,

    /// <summary>No answer (timeout, dropped connection). The charge may or may not have happened, so the cascade must stop.</summary>
    Uncertain
}
