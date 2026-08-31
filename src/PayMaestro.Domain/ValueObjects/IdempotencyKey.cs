using System.Text.RegularExpressions;

namespace PayMaestro.Domain.ValueObjects;

public readonly partial record struct IdempotencyKey(string Value)
{
    public const int MaxLength = 100;

    public static IdempotencyKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(value));
        }

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength || IdempotencyKeyPattern().IsMatch(trimmed) is false)
        {
            throw new ArgumentException(
                "Idempotency key must be 1-100 characters and contain only letters, numbers, '.', '_', ':' or '-'.",
                nameof(value));
        }

        return new IdempotencyKey(trimmed);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex IdempotencyKeyPattern();
}
