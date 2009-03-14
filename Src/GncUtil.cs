using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Collections;

namespace GnuCashSharp
{
    public static class GncUtil
    {
        public static decimal ToGncDecimal(this string value)
        {
            decimal res;
            if (decimal.TryParse(value, out res))
                return res;
            string[] parts = value.Split('/');
            if (parts.Length != 2)
                throw new GncException("Cannot parse Gnc Numeric value: \"{0}\"", value);
            try { return decimal.Parse(parts[0]) / decimal.Parse(parts[1]); }
            catch { throw new GncException("Cannot parse Gnc Numeric value: \"{0}\"", value); }
        }

        /// <summary>
        /// GnuCash stores dates in a fucked up format: it stores the date,
        /// the time set to all zeroes, and a time zone offset. This function
        /// parses such a string into a Date-only DateTime of kind UTC.
        /// </summary>
        public static DateTime ParseGncDate(string value)
        {
            return new DateTime(DateTimeOffset.Parse(value).Date.Ticks, DateTimeKind.Utc);
        }
    }
}
