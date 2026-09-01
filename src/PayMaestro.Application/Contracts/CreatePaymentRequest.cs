using System.ComponentModel.DataAnnotations;

namespace PayMaestro.Application.Contracts;

/// <summary>Data required to create and process a payment.</summary>
public sealed record CreatePaymentRequest
{
    [Required]
    public string MerchantReference { get; init; } = string.Empty;

    [Required]
    public string CustomerId { get; init; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Amount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = string.Empty;

    [Required]
    [StringLength(19, MinimumLength = 13)]
    public string CardNumber { get; init; } = string.Empty;

    [Required]
    public string CustomerIp { get; init; } = string.Empty;
}
