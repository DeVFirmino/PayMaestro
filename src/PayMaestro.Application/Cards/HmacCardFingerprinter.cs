using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PayMaestro.Application.Options;
using PayMaestro.Domain.Cards;

namespace PayMaestro.Application.Cards;

/// <summary>
/// HMAC-SHA256 of the card number under a server-side key. A plain hash would not do: card
/// numbers are few enough to enumerate, so anyone holding the database could rebuild every PAN
/// from an unkeyed digest. Without the key the fingerprint can be neither reversed nor recomputed.
/// </summary>
public sealed class HmacCardFingerprinter : ICardFingerprinter
{
    private readonly byte[] _key;

    public HmacCardFingerprinter(IOptions<CardFingerprintOptions> options)
    {
        _key = Encoding.UTF8.GetBytes(options.Value.Key);
    }

    public string Fingerprint(string cardNumber)
        => Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(cardNumber)));
}
