using System.Xml.Linq;
using RT.Serialization;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp;

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
    ///     GnuCash stores dates in a fucked up format: it stores the date, the time set to all zeroes, and a time zone
    ///     offset. This function parses such a string into a Date-only DateTime of kind UTC.</summary>
    public static DateTime ParseGncDate(string value)
    {
        var d = DateTimeOffset.Parse(value);
        if (d.Hour >= 20)
            d = d.AddHours(12).Date;
        else
            d = d.Date;
        return new DateTime(d.Ticks, DateTimeKind.Utc);
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

    /// <summary>
    ///     Returns the value of this element, converted to type T. If the element does not exist returns the default value.
    ///     If the element's value cannot be converted, throws an exception.</summary>
    public static T ValueOrDefault<T>(this XElement element, XName name, T defaultValue)
    {
        // Copied from RT.Util
        XElement el = element.Element(name);
        if (el == null)
            return defaultValue;
        else
            try { return ExactConvert.To<T>(el.Value); }
            catch (ExactConvertException E) { throw new InvalidOperationException(("Element \"{0}/{1}\", when present, must contain a value convertible to a certain type: " + E.Message).Fmt(element.Path(), name)); }
    }
}
