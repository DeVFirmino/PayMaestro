namespace PayMaestro.Domain.Cards;

/// <summary>
/// Turns a card number into a stable identifier for the exact card, so two requests can be
/// compared card for card without the full number ever being stored.
/// </summary>
public interface ICardFingerprinter
{
    string Fingerprint(string cardNumber);
}
