using Ardalis.GuardClauses;

namespace EduPlatform.Domain.Common;

public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Guard.Against.NullOrWhiteSpace(currency);
        Guard.Against.InvalidInput(currency, nameof(currency), c => c.Length == 3,
            "Currency must be a 3-letter ISO 4217 code.");
        Guard.Against.Negative(amount);
        Amount = Math.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency;
    }

    public static Money Zero => new(0m, "USD");

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new DomainException("Currency mismatch.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new DomainException("Currency mismatch.");
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public bool Equals(Money other) => Amount == other.Amount && Currency == other.Currency;
    public override bool Equals(object? obj) => obj is Money m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
    public int CompareTo(Money other) =>
        Currency == other.Currency
            ? Amount.CompareTo(other.Amount)
            : throw new DomainException("Currency mismatch.");
    public override string ToString() => $"{Amount:F2} {Currency}";
}
