using System.ComponentModel.DataAnnotations;

namespace PayMaestro.Application.Contracts;

/// <summary>Data required to create and process a payment.</summary>
public sealed record CreatePaymentRequest
{
    [Required]
    public required string MerchantReference { get; init; }

    [Required]
    public required string CustomerId { get; init; }

    [Range(0.01, 1_000_000)]
    public required decimal Amount { get; init; }

    [Required, StringLength(3, MinimumLength = 3)]
    public required string Currency { get; init; }

    [Required, StringLength(19, MinimumLength = 13)]
    public required string CardNumber { get; init; }

    [Required]
    public required string CustomerIp { get; init; }
}
