using System;
using RT.Util;

namespace GnuCashSharp
{
    /// <summary>Represents an amount of a certain currency/commodity at a certain point in time.</summary>
    public class GncAmount
    {
        public GncAmount(decimal quantity, GncCommodity commodity, DateTime timepoint)
        {
            Quantity = quantity;
            Commodity = commodity ?? throw new ArgumentNullException("commodity");
            Timepoint = timepoint;
            if (Timepoint.Kind != DateTimeKind.Utc)
                throw new RTException("The DateTime passed to GncAmount constructor must be a UTC DateTime.");
        }

        public override string ToString() => $"{Commodity}: {Quantity:0.00} on {Timepoint.ToShortDateString()}";

        /// <summary>Gets the quantity of the <see cref="Commodity"/> represented by this instance.</summary>
        public decimal Quantity { get; private set; }

        /// <summary>Gets the commodity that the amount is specified in.</summary>
        public GncCommodity Commodity { get; private set; }

        /// <summary>Gets the point in time at which the amount is defined. The time is always in UTC.</summary>
        public DateTime Timepoint { get; private set; }

        /// <summary>Converts this amount to a different commodity at the same point in time.</summary>
        public GncAmount ConvertTo(GncCommodity toCommodity)
        {
            decimal fromRate = Commodity.IsBaseCurrency ? 1m : Commodity.ExRate.Get(Timepoint, GncInterpolation.Linear);
            decimal toRate = toCommodity.IsBaseCurrency ? 1m : toCommodity.ExRate.Get(Timepoint, GncInterpolation.Linear);
            return new GncAmount(Quantity * fromRate / toRate, toCommodity, Timepoint);
        }

        public static GncAmount operator +(GncAmount amt1, GncAmount amt2)
        {
            if (amt1.Commodity != amt2.Commodity)
                throw new InvalidOperationException($"Cannot add amounts in different commodities ({amt1.Commodity} and {amt2.Commodity}). Use ConvertTo.");
            if (amt1.Timepoint != amt2.Timepoint)
                throw new InvalidOperationException($"Cannot add amounts at different points in time ({amt1.Timepoint} and {amt2.Timepoint}). Cast to decimal to strip the date.");
            return new GncAmount(amt1.Quantity + amt2.Quantity, amt1.Commodity, amt1.Timepoint);
        }

        public static implicit operator decimal(GncAmount amt)
        {
            return amt.Quantity;
        }
    }
}
