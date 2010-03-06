using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;

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
                throw new GncException("Cannot parse Gnc Numeric value: \"{0}\"".Fmt(value));
            try { return decimal.Parse(parts[0]) / decimal.Parse(parts[1]); }
            catch { throw new GncException("Cannot parse Gnc Numeric value: \"{0}\"".Fmt(value)); }
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

        public static DateTime StartOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1, date.Hour, date.Minute, date.Second, date.Millisecond, date.Kind);
        }

        public static DateTime EndOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), date.Hour, date.Minute, date.Second, date.Millisecond, date.Kind);
        }

        public static DateTime AssumeUtc(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Utc);
        }
    }
}
