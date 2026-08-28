namespace PayMaestro.Application.Contracts;

public sealed record ErrorResponse
{
    public required string Error { get; init; }
}
