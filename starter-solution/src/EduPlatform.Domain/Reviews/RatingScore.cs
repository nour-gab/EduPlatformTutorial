using Ardalis.GuardClauses;

namespace EduPlatform.Domain.Reviews;

public readonly struct RatingScore : IEquatable<RatingScore>, IComparable<RatingScore>
{
    public double Value { get; }

    public RatingScore(double value)
    {
        Guard.Against.OutOfRange(value, nameof(value), 0.0, 5.0);
        Value = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
    }

    public static readonly RatingScore Min = new(0.0);
    public static readonly RatingScore Max = new(5.0);

    public int CompareTo(RatingScore other) => Value.CompareTo(other.Value);
    public bool Equals(RatingScore other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is RatingScore r && Equals(r);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"{Value:F1}";
}
