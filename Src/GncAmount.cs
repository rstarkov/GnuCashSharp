using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util;

namespace GnuCashSharp
{
    /// <summary>
    /// Represents an amount of a certain currency/commodity at a certain point in time.
    /// </summary>
    public class GncAmount
    {
        private decimal _quantity;
        private GncCommodity _commodity;
        private DateTime _timepoint;

        public GncAmount(decimal quantity, GncCommodity commodity, DateTime timepoint)
        {
            if (commodity == null)
                throw new ArgumentNullException("commodity");
            _quantity = quantity;
            _commodity = commodity;
            _timepoint = timepoint;
            if (_timepoint.Kind != DateTimeKind.Utc)
                throw new RTException("The DateTime passed to GncAmount constructor must be a UTC DateTime.");
        }

        public override string ToString()
        {
            return string.Format("{0}: {1:0.00} on {2}", Commodity, Quantity, Timepoint.ToShortDateString());
        }

        /// <summary>
        /// Gets the quantity of the <see cref="Commodity"/> represented by this instance.
        /// </summary>
        public decimal Quantity
        {
            get { return _quantity; }
        }

        /// <summary>
        /// Gets the commodity that the amount is specified in.
        /// </summary>
        public GncCommodity Commodity
        {
            get { return _commodity; }
        }

        /// <summary>
        /// Gets the point in time at which the amount is defined. The time is always in UTC.
        /// </summary>
        public DateTime Timepoint
        {
            get { return _timepoint; }
        }

        /// <summary>
        /// Converts this amount to a different commodity at the same point in time.
        /// </summary>
        public GncAmount ConvertTo(GncCommodity toCommodity)
        {
            decimal fromRate = _commodity.IsBaseCurrency ? 1m : _commodity.ExRate.Get(_timepoint, GncInterpolation.Linear);
            decimal toRate = toCommodity.IsBaseCurrency ? 1m : toCommodity.ExRate.Get(_timepoint, GncInterpolation.Linear);
            return new GncAmount(_quantity * fromRate / toRate, toCommodity, _timepoint);
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
