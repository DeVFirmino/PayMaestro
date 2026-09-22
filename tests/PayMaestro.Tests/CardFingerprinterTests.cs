using Microsoft.Extensions.Options;
using PayMaestro.Application.Cards;
using PayMaestro.Application.Options;
using PayMaestro.Domain.Entities;

namespace PayMaestro.Tests;

/// <summary>
/// The fingerprint has to do two jobs at once: tell any two cards apart, and give away nothing
/// about the number it was computed from. The card numbers are load-bearing, so they are fixed.
/// </summary>
public sealed class CardFingerprinterTests
{
    private const string Card = "4111111111117777";

    [Fact]
    public void ShouldReturnSameFingerprintWhenSameCardIsFingerprintedTwice()
    {
        HmacCardFingerprinter fingerprinter = WithKey("first-key-for-fingerprint-tests-0001");

        Assert.Equal(fingerprinter.Fingerprint(Card), fingerprinter.Fingerprint(Card));
    }

    [Fact]
    public void ShouldReturnDifferentFingerprintWhenOnlyMiddleDigitsDiffer()
    {
        HmacCardFingerprinter fingerprinter = WithKey("first-key-for-fingerprint-tests-0001");

        Assert.NotEqual(fingerprinter.Fingerprint("4111111111117777"), fingerprinter.Fingerprint("4111119999997777"));
    }

    [Fact]
    public void ShouldReturnDifferentFingerprintWhenKeyDiffers()
    {
        string first = WithKey("first-key-for-fingerprint-tests-0001").Fingerprint(Card);
        string second = WithKey("second-key-for-fingerprint-tests-002").Fingerprint(Card);

        Assert.NotEqual(first, second);    // without the key, nobody can recompute it from a guessed number
    }

    [Fact]
    public void ShouldReturnFixedLengthHexWithoutCardDigitsWhenCardIsFingerprinted()
    {
        string fingerprint = WithKey("first-key-for-fingerprint-tests-0001").Fingerprint(Card);

        Assert.Equal(Payment.CardFingerprintLength, fingerprint.Length);
        Assert.Matches("^[0-9a-f]+$", fingerprint);
        Assert.DoesNotContain(Card, fingerprint);
        Assert.DoesNotContain(Card[..6], fingerprint);
    }

    private static HmacCardFingerprinter WithKey(string key)
        => new(Options.Create(new CardFingerprintOptions { Key = key }));
}
