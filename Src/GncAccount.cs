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

        /// <summary>
        /// The depth of the root account is 0. The depth of its immediate children is 1. Etc.
        /// </summary>
        public int Depth
        {
            get
            {
                int depth = 0;
                var acct = this;
                while (acct.Parent != null)
                {
                    depth++;
                    acct = acct.Parent;
                }
                return depth;
            }
        }

        public IEnumerable<GncAccount> EnumChildren()
        {
            foreach (var child in Book.AccountEnumChildren(this))
                yield return child;
        }

        /// <summary>
        /// Enumerates all splits in this account, and optionally, in subaccounts.
        /// The splits will be ordered correctly if only this account's splits are
        /// enumerated.
        /// </summary>
        public IEnumerable<GncSplit> EnumSplits(bool subAccts)
        {
            if (subAccts)
            {
                foreach (var split in _book.AccountEnumSplits(this))
                    yield return split;
                foreach (var acct in EnumChildren())
                    foreach (var split in acct.EnumSplits(true))
                        yield return split;
            }
            else
            {
                foreach (var split in _book.AccountEnumSplits(this))
                    yield return split;
            }
        }

        public decimal GetTotal(DateInterval interval, bool includeSubaccts, GncCommodity cmdty)
        {
            decimal total = 0;
            foreach (var split in EnumSplits(false).Where(spl => interval.Contains(spl.Transaction.DatePosted)))
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
            if (this == _book.AccountRoot)
                return "";
            StringBuilder sb = new StringBuilder(_name);
            var acct = this.Parent;
            while (acct != _book.AccountRoot)
            {
                sb.Insert(0, acct.Name + separator);
                acct = acct.Parent;
            }
            return sb.ToString();
        }

        public List<GncAccount> PathAsList()
        {
            return PathAsList(_book.AccountRoot);
        }

        public List<GncAccount> PathAsList(GncAccount acctBase)
        {
            var result = new List<GncAccount>();
            var acct = this;
            while (acct != acctBase)
            {
                result.Insert(0, acct);
                acct = acct.Parent;
            }
            return result;
        }
    }
}
