using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GnuCashSharp
{
    public enum GncAccountType
    {
        UNKNOWN=0,
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
        UNKNOWN=0,
        Yes,
        No,
        Chk,
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
    }
}
