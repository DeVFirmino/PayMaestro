namespace PayMaestro.Domain.ValueObjects;

public readonly record struct PaymentMethodToken(string Value)
{
    public const int MaxLength = 128;

    public static PaymentMethodToken Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Payment method token is required.", nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"Payment method token cannot exceed {MaxLength} characters.", nameof(value));
        }

        return new PaymentMethodToken(value);
    }

    public override string ToString() => Value;
}
