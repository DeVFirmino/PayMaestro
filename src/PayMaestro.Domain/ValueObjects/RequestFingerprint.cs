namespace PayMaestro.Domain.ValueObjects;

public readonly record struct RequestFingerprint(string Value)
{
    public const int MaxLength = 128;

    public static RequestFingerprint Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Request fingerprint is required.", nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"Request fingerprint cannot exceed {MaxLength} characters.", nameof(value));
        }

        return new RequestFingerprint(value);
    }

    public override string ToString() => Value;
}
