using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp
{
    public class GncAccount
    {
        private GncBook _book;
        private string _name;
        private string _guid;
        private string _parentGuid;
        private GncAccountType _type;
        private string _commodity;
        private int _commodityScu;
        private string _description;

        public GncAccount(GncBook book)
        {
            _book = book;
            _name = null;
            _guid = null;
            _parentGuid = null;
            _type = GncAccountType.UNKNOWN;
            _commodity = null;
            _commodityScu = 0;
            _description = null;
        }

        public GncAccount(GncBook book, XElement xml)
            : this(book)
        {
            _name = xml.ChkElement(GncName.Act("name")).Value;
            _guid = xml.ChkElement(GncName.Act("id")).Value;
            _parentGuid = xml.ValueOrDefault(GncName.Act("parent"), (string)null);
            _commodity = GncCommodity.MakeIdentifier(xml.Element(GncName.Act("commodity")));
            _description = xml.ValueOrDefault(GncName.Act("description"), (string)null);
        }

        public GncBook Book
        {
            get { return _book; }
        }

        public string Name
        {
            get { return _name; }
        }

        public string Guid
        {
            get { return _guid; }
        }

        public string ParentGuid
        {
            get { return _parentGuid; }
        }

        public GncAccountType Type
        {
            get { return _type; }
        }

        public string Commodity
        {
            get { return _commodity; }
        }

        public int CommodityScu
        {
            get { return _commodityScu; }
        }

        public string Description
        {
            get { return _description; }
        }

        public GncAccount Parent
        {
            get { return _book.GetAccount(_parentGuid); }
        }

        public IEnumerable<GncAccount> EnumChildren()
        {
            foreach (var child in Book.AccountEnumChildren(this))
                yield return child;
        }

        public IEnumerable<GncSplit> EnumSplits()
        {
            foreach (var split in Book.AccountEnumSplits(this))
                yield return split;
        }

        public decimal GetTotalDebit(DateInterval interval, bool includeSubaccts)
        {
            decimal total = 0;
            foreach (var split in EnumSplits().Where(spl => interval.Contains(spl.Transaction.DatePosted)))
                if (split.Value > 0)
                    total += split.Value;
            if (includeSubaccts)
            {
                foreach (var subacct in EnumChildren())
                    total += subacct.GetTotalDebit(interval, true);
            }
            return total;
        }

        public decimal GetTotalCredit(DateInterval interval, bool includeSubaccts)
        {
            decimal total = 0;
            foreach (var split in EnumSplits().Where(spl => interval.Contains(spl.Transaction.DatePosted)))
                if (split.Value < 0)
                    total -= split.Value;
            if (includeSubaccts)
            {
                foreach (var subacct in EnumChildren())
                    total += subacct.GetTotalCredit(interval, true);
            }
            return total;
        }

        public decimal GetTotal(DateInterval interval, bool includeSubaccts, GncCommodity cmdty)
        {
            decimal total = 0;
            foreach (var split in EnumSplits().Where(spl => interval.Contains(spl.Transaction.DatePosted)))
                total += split.Amount.ConvertTo(cmdty).Quantity;
            if (includeSubaccts)
            {
                foreach (var subacct in EnumChildren())
                    total += subacct.GetTotal(interval, true, cmdty);
            }
            return total;
        }

        public string Path(string separator)
        {
            StringBuilder sb = new StringBuilder(_name);
            var acct = this.Parent;
            while (acct != _book.AccountRoot)
            {
                sb.Insert(0, acct.Name + separator);
                acct = acct.Parent;
            }
            return sb.ToString();
        }
    }
}
