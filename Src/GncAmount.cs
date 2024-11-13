using System.Collections;
using RT.Util.Collections;

namespace GnuCashSharp;

/// <summary>
///     Represents an amount of a specific commodity with an optional point in time. See Remarks.</summary>
/// <remarks>
///     As this is a value type, it has a default constructor which will create an instance with a null commodity. Arithmetic
///     operations have special support for this value, treating it as commodity-agnostic zero value.</remarks>
public struct GncCommodityAmount
{
    public GncCommodityAmount(decimal quantity, GncCommodity commodity, DateTime? timepoint = null)
        : this()
    {
        if (timepoint != null && timepoint.Value.Kind == DateTimeKind.Unspecified)
            throw new ArgumentException("Time point must be a local or UTC time", nameof(timepoint));
        Quantity = quantity;
        Commodity = commodity ?? throw new ArgumentNullException(nameof(commodity));
        Timepoint = timepoint;
    }

    /// <summary>Gets the quantity of the <see cref="Commodity"/> represented by this instance.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Gets the commodity that the amount is specified in.</summary>
    public GncCommodity Commodity { get; private set; }

    /// <summary>
    ///     The point in time at which the amount is defined. The time is always in UTC. When specified, currency conversions
    ///     become possible. Arithmetic operations preserve this value if it matches in all operands, but reset it to null if
    ///     it doesn't.</summary>
    public DateTime? Timepoint { get; private set; }

    public override string ToString()
    {
        var timestr = Timepoint == null ? "" : $" on {Timepoint.Value.ToShortDateString()}";
        switch (Commodity.Identifier)
        {
            case "GBP": return $"£{Quantity:#,0.00}{timestr}";
            case "EUR": return $"€{Quantity:#,0.00}{timestr}";
            case "USD": return $"${Quantity:#,0.00}{timestr}";
            case "UAH": return $"{Quantity:#,0} грн{timestr}";
            default: return $"{Commodity.Identifier} {Quantity:#,0.00}{timestr}";
        }
    }

    public GncCommodityAmount WithTimepoint(DateTime? timepoint)
    {
        return new GncCommodityAmount(Quantity, Commodity, timepoint);
    }

    public static GncCommodityAmount operator +(GncCommodityAmount amt1, decimal amt2)
    {
        return new GncCommodityAmount(amt1.Quantity + amt2, amt1.Commodity, amt1.Timepoint);
    }

    public static GncCommodityAmount operator +(decimal amt1, GncCommodityAmount amt2)
    {
        return new GncCommodityAmount(amt1 + amt2.Quantity, amt2.Commodity, amt2.Timepoint);
    }

    public static GncCommodityAmount operator +(GncCommodityAmount amt1, GncCommodityAmount amt2)
    {
        // Commodity can only be null if the default constructor was used. Allow this as a special case, a value that can be added to any amount without changing that amount
        if (amt1.Commodity == null)
            return amt2;
        if (amt2.Commodity == null)
            return amt1;

        if (amt1.Commodity != amt2.Commodity)
            throw new InvalidOperationException($"Cannot add amounts because commodities differ: {amt1.Commodity} and {amt2.Commodity}");
        return new GncCommodityAmount(amt1.Quantity + amt2.Quantity, amt1.Commodity, amt1.Timepoint == amt2.Timepoint ? amt1.Timepoint : null);
    }

    public static GncCommodityAmount operator -(GncCommodityAmount amt1, decimal amt2)
    {
        return new GncCommodityAmount(amt1.Quantity - amt2, amt1.Commodity, amt1.Timepoint);
    }

    public static GncCommodityAmount operator -(decimal amt1, GncCommodityAmount amt2)
    {
        return new GncCommodityAmount(amt1 - amt2.Quantity, amt2.Commodity, amt2.Timepoint);
    }

    public static GncCommodityAmount operator -(GncCommodityAmount amt1, GncCommodityAmount amt2)
    {
        // Commodity can only be null if the default constructor was used. Allow this as a special case, a value that can be added to any amount without changing that amount
        if (amt1.Commodity == null)
            return -amt2;
        if (amt2.Commodity == null)
            return amt1;

        if (amt1.Commodity != amt2.Commodity)
            throw new InvalidOperationException($"Cannot subtract amounts because commodities differ: {amt1.Commodity} and {amt2.Commodity}");
        return new GncCommodityAmount(amt1.Quantity - amt2.Quantity, amt1.Commodity, amt1.Timepoint == amt2.Timepoint ? amt1.Timepoint : null);
    }

    public static GncCommodityAmount operator *(GncCommodityAmount amt1, decimal amt2)
    {
        return new GncCommodityAmount(amt1.Quantity * amt2, amt1.Commodity, amt1.Timepoint);
    }

    public static GncCommodityAmount operator *(decimal amt1, GncCommodityAmount amt2)
    {
        return new GncCommodityAmount(amt1 * amt2.Quantity, amt2.Commodity, amt2.Timepoint);
    }

    public static GncCommodityAmount operator /(GncCommodityAmount amt1, decimal amt2)
    {
        return new GncCommodityAmount(amt1.Quantity / amt2, amt1.Commodity, amt1.Timepoint);
    }

    public static GncCommodityAmount operator -(GncCommodityAmount amt)
    {
        // Commodity can only be null if the default constructor was used. Allow this as a special case of a "zero" value
        if (amt.Commodity == null)
            return amt;
        return new GncCommodityAmount(-amt.Quantity, amt.Commodity, amt.Timepoint);
    }

    public static explicit operator decimal(GncCommodityAmount amt)
    {
        return amt.Quantity;
    }

    /// <summary>
    ///     Converts this amount to a different commodity at the same point in time. Throws if this amount does not have a
    ///     timepoint.</summary>
    public GncCommodityAmount ConvertTo(GncCommodity toCommodity)
    {
        if (Timepoint == null)
            throw new InvalidOperationException("Cannot convert this amount to another commodity because it doesn't have a timepoint.");
        if (toCommodity == Commodity)
            return this;
        decimal fromRate = Commodity.IsBaseCurrency ? 1m : Commodity.ExRate.Get(Timepoint.Value, GncInterpolation.Linear);
        decimal toRate = toCommodity.IsBaseCurrency ? 1m : toCommodity.ExRate.Get(Timepoint.Value, GncInterpolation.Linear);
        return new GncCommodityAmount(Quantity * fromRate / toRate, toCommodity, Timepoint);
    }
}

/// <summary>
///     Represents an amount of money consisting of multiple commodities, optionally at a specific point in time. See Remarks.</summary>
/// <remarks>
///     When enumerating this type, only non-zero commodities are enumerated. Because of this, the <see cref="Count"/>
///     property is zero if and only if all commodities are zero. This type also supports comparisons to the number 0, but not
///     to any other numbers. Comparison operators ignore time point where present. Arithmetic operators create a new instance
///     for the result, which should be avoided in performance-sensitive loops; use in-place operations in such code.</remarks>
public class GncMultiAmount : IReadOnlyCollection<GncCommodityAmount>, IEquatable<GncMultiAmount>
{
    /// <summary>
    ///     The point in time at which the amount is defined. The time is always in UTC. When specified, currency conversions
    ///     become possible. Arithmetic operations preserve this value if it matches in all operands, but reset it to null if
    ///     it doesn't.</summary>
    public DateTime? Timepoint { get; set; }
    private AutoDictionary<GncCommodity, decimal> _commodities = new AutoDictionary<GncCommodity, decimal>();

    public GncMultiAmount()
    {
    }

    public GncMultiAmount(decimal quantity, GncCommodity commodity, DateTime? timepoint = null)
    {
        if (commodity == null)
            throw new ArgumentNullException(nameof(commodity));
        if (timepoint != null && timepoint.Value.Kind == DateTimeKind.Unspecified)
            throw new ArgumentException("Time point must be a local or UTC time", nameof(timepoint));
        _commodities[commodity] = quantity;
        Timepoint = timepoint;
    }

    public int Count => _commoditiesEnum.Count();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<GncCommodityAmount> GetEnumerator() => _commoditiesEnum.GetEnumerator();
    private IEnumerable<GncCommodityAmount> _commoditiesEnum => _commodities.Where(kvp => kvp.Value != 0).Select(kvp => new GncCommodityAmount(kvp.Value, kvp.Key, Timepoint));

    public static implicit operator GncMultiAmount(GncCommodityAmount amt)
    {
        if (amt.Quantity == 0)
            return new GncMultiAmount { Timepoint = amt.Timepoint };
        return new GncMultiAmount(amt.Quantity, amt.Commodity, amt.Timepoint);
    }

    public GncMultiAmount Clone()
    {
        var result = new GncMultiAmount();
        result.Timepoint = Timepoint;
        result._commodities = new AutoDictionary<GncCommodity, decimal>(_commodities);
        return result;
    }

    public void NegateInplace()
    {
        foreach (var k in _commodities.Keys.ToList())
            _commodities[k] = -_commodities[k];
    }

    public static GncMultiAmount operator -(GncMultiAmount amt)
    {
        var result = amt.Clone();
        result.NegateInplace();
        return result;
    }

    public void AddInplace(decimal quantity, GncCommodity commodity)
    {
        _commodities[commodity] += quantity;
    }

    public void AddInplace(GncCommodityAmount amt)
    {
        _commodities[amt.Commodity] += amt.Quantity;
        if (Timepoint != amt.Timepoint)
            Timepoint = null;
    }

    public void AddInplace(GncMultiAmount amt)
    {
        foreach (var kvp in amt._commodities)
            _commodities[kvp.Key] += kvp.Value;
        if (Timepoint != amt.Timepoint)
            Timepoint = null;
    }

    public static GncMultiAmount operator +(GncMultiAmount amt1, GncMultiAmount amt2)
    {
        var result = amt1.Clone();
        result.AddInplace(amt2);
        return result;
    }

    public static GncMultiAmount operator -(GncMultiAmount amt1, GncMultiAmount amt2)
    {
        var result = amt1.Clone();
        result.NegateInplace();
        result.AddInplace(amt2);
        result.NegateInplace();
        return result;
    }

    public static bool operator ==(GncMultiAmount amt1, GncMultiAmount amt2)
    {
        foreach (var a1 in amt1.Where(a => a.Quantity != 0))
        {
            if (!amt2._commodities.ContainsKey(a1.Commodity))
                return false;
            if (amt2._commodities[a1.Commodity] != a1.Quantity)
                return false;
        }
        foreach (var a2 in amt2.Where(a => a.Quantity != 0))
        {
            if (!amt1._commodities.ContainsKey(a2.Commodity))
                return false;
            if (amt1._commodities[a2.Commodity] != a2.Quantity)
                return false;
        }
        return true;
    }

    public static bool operator !=(GncMultiAmount amt1, GncMultiAmount amt2)
    {
        return !(amt1 == amt2);
    }

    public static bool operator ==(GncMultiAmount amt1, decimal amt2)
    {
        if (amt2 != 0)
            throw new InvalidOperationException($"Cannot compare {nameof(GncMultiAmount)} and decimal, except if decimal is zero.");
        return amt1.Count == 0;
    }

    public static bool operator !=(GncMultiAmount amt1, decimal amt2)
    {
        return !(amt1 == amt2);
    }

    public override bool Equals(object obj) => (obj is GncMultiAmount) ? ((GncMultiAmount) obj == this) : false;
    public bool Equals(GncMultiAmount other) => this == other;

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 0;
            foreach (var kvp in _commodities.Where(kvp => kvp.Value != 0))
            {
                hash = hash * 17 + kvp.Key.Identifier.GetHashCode();
                hash = hash * 17 + kvp.Value.GetHashCode();
            }
            return hash;
        }
    }

    public void MultiplyInplace(decimal amt)
    {
        foreach (var k in _commodities.Keys.ToList())
            _commodities[k] *= amt;
    }

    public static GncMultiAmount operator *(GncMultiAmount amt, decimal value)
    {
        var result = amt.Clone();
        result.MultiplyInplace(value);
        return result;
    }

    /// <summary>Applies the specified function to the quantity of each commodity in this <see cref="GncMultiAmount"/>.</summary>
    public void ApplyInplace(Func<decimal, decimal> func)
    {
        foreach (var k in _commodities.Keys.ToList())
            _commodities[k] = func(_commodities[k]);
    }

    /// <summary>
    ///     Returns a new <see cref="GncMultiAmount"/> with the specified function applied to the quantity of each commodity
    ///     in this <see cref="GncMultiAmount"/>.</summary>
    public GncMultiAmount Apply(Func<decimal, decimal> func)
    {
        var result = Clone();
        result.ApplyInplace(func);
        return result;
    }

    /// <summary>
    ///     Converts this amount to a different commodity at the same point in time. Throws if this amount does not have a
    ///     timepoint.</summary>
    public GncCommodityAmount ConvertTo(GncCommodity toCommodity)
    {
        if (Timepoint == null)
            throw new InvalidOperationException("Cannot convert this amount to another commodity because it doesn't have a timepoint.");
        var result = new GncCommodityAmount();
        foreach (var amt in this)
            result += amt.ConvertTo(toCommodity);
        return result;
    }

    public override string ToString()
    {
        return string.Join(" + ", _commoditiesEnum);
    }
}
