using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GnuCashSharp
{
    public enum GncAccountType
    {
        UNKNOWN = 0,
        ROOT,
        CASH,
        ASSET,
        BANK,
        INCOME,
        EXPENSE,
        EQUITY,
    }

    public enum GncReconciled
    {
        UNKNOWN = 0,
        Yes,
        No,
        Chk,
    }

    public enum GncInterpolation
    {
        None,
        Nearest,
        NearestBefore,
        NearestAfter,
        Linear,
    }

    public static class GncName
    {
        public static string Gnc(string name) { return "{http://www.gnucash.org/XML/gnc}" + name; }
        public static string Act(string name) { return "{http://www.gnucash.org/XML/act}" + name; }
        public static string Book(string name) { return "{http://www.gnucash.org/XML/book}" + name; }
        public static string Cd(string name) { return "{http://www.gnucash.org/XML/cd}" + name; }
        public static string Cmdty(string name) { return "{http://www.gnucash.org/XML/cmdty}" + name; }
        public static string Price(string name) { return "{http://www.gnucash.org/XML/price}" + name; }
        public static string Slot(string name) { return "{http://www.gnucash.org/XML/slot}" + name; }
        public static string Split(string name) { return "{http://www.gnucash.org/XML/split}" + name; }
        public static string Sx(string name) { return "{http://www.gnucash.org/XML/sx}" + name; }
        public static string Trn(string name) { return "{http://www.gnucash.org/XML/trn}" + name; }
        public static string Ts(string name) { return "{http://www.gnucash.org/XML/ts}" + name; }
        public static string Fs(string name) { return "{http://www.gnucash.org/XML/fs}" + name; }
        public static string Bgt(string name) { return "{http://www.gnucash.org/XML/bgt}" + name; }
        public static string Recurrence(string name) { return "{http://www.gnucash.org/XML/recurrence}" + name; }
        public static string Lot(string name) { return "{http://www.gnucash.org/XML/lot}" + name; }
        public static string Cust(string name) { return "{http://www.gnucash.org/XML/cust}" + name; }
        public static string Job(string name) { return "{http://www.gnucash.org/XML/job}" + name; }
        public static string Addr(string name) { return "{http://www.gnucash.org/XML/addr}" + name; }
        public static string Owner(string name) { return "{http://www.gnucash.org/XML/owner}" + name; }
        public static string Taxtable(string name) { return "{http://www.gnucash.org/XML/taxtable}" + name; }
        public static string Tte(string name) { return "{http://www.gnucash.org/XML/tte}" + name; }
        public static string Employee(string name) { return "{http://www.gnucash.org/XML/employee}" + name; }
        public static string Order(string name) { return "{http://www.gnucash.org/XML/order}" + name; }
        public static string Billterm(string name) { return "{http://www.gnucash.org/XML/billterm}" + name; }
        public static string BtDays(string name) { return "{http://www.gnucash.org/XML/bt-days}" + name; }
        public static string BtProx(string name) { return "{http://www.gnucash.org/XML/bt-prox}" + name; }
        public static string Invoice(string name) { return "{http://www.gnucash.org/XML/invoice}" + name; }
        public static string Entry(string name) { return "{http://www.gnucash.org/XML/entry}" + name; }
        public static string Vendor(string name) { return "{http://www.gnucash.org/XML/vendor}" + name; }
    }

    public struct DateInterval : IEquatable<DateInterval>
    {
        private DateTime _start;
        private DateTime _end;

        public DateInterval(DateTime start, DateTime end)
        {
            _start = new DateTime(start.Year, start.Month, start.Day, 0, 0, 0, DateTimeKind.Utc);
            _end = new DateTime(end.Year, end.Month, end.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        public DateInterval(int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
        {
            _start = new DateTime(startYear, startMonth, startDay, 0, 0, 0, DateTimeKind.Utc);
            _end = new DateTime(endYear, endMonth, endDay, 0, 0, 0, DateTimeKind.Utc);
        }

        public DateTime Start
        {
            get { return _start; }
        }

        public DateTime End
        {
            get { return _end; }
        }

        public override int GetHashCode()
        {
            int c1 = _start.GetHashCode();
            int c2 = _end.GetHashCode();
            return unchecked(c1 * 37 + c2);
        }

        public override bool Equals(object obj)
        {
            if (obj is DateInterval)
                return Equals((DateInterval) obj);
            else
                return base.Equals(obj);
        }

        public bool Equals(DateInterval other)
        {
            return this._start == other._start && this._end == other._end;
        }

        public override string ToString()
        {
            return _start.ToString("yyyy-MM-dd") + ".." + _end.ToString("yyyy-MM-dd");
        }

        public IEnumerable<DateInterval> EnumMonths()
        {
            DateTime from = _start.AssumeUtc();
            DateTime to = _end;
            while (from <= to)
            {
                int year = from.Year;
                int month = from.Month + 1;
                if (month > 12)
                {
                    month -= 12;
                    year += 1;
                }
                var periodTo = new DateTime(year, month, 1).AssumeUtc();
                yield return new DateInterval(from, periodTo.AddDays(-1));
                from = periodTo;
            }
        }

        public bool Contains(DateTime date)
        {
            date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
            return date >= _start && date <= _end;
        }

        public double TotalMonths
        {
            get
            {
                double months = (_end.Year * 12 + _end.Month) - (_start.Year * 12 + _start.Month) + 1;
                months -= ((double) _start.Day - 1.0) / DateTime.DaysInMonth(_start.Year, _start.Month);
                months -= ((double) DateTime.DaysInMonth(_end.Year, _end.Month) - _end.Day) / DateTime.DaysInMonth(_end.Year, _end.Month);
                return months;
            }
        }
    }
}
