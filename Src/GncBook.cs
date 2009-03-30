using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp
{
    public class GncBook
    {
        private GncSession _session;
        private string _guid;
        private GncAccount _accountRoot;
        private Dictionary<string, GncAccount> _accounts;
        private Dictionary<string, GncTransaction> _transactions;
        private Dictionary<string, GncSplit> _splits;
        private Dictionary<string, GncCommodity> _commodities;
        private string _baseCurrencyId;

        private Dictionary<string, LinkedList<string>> _cacheAccountSplits;
        private Dictionary<string, List<string>> _cacheAccountChildren;

        public GncBook(GncSession session, string baseCurrency)
        {
            _session = session;
            _guid = null;
            _accountRoot = null;
            _accounts = new Dictionary<string, GncAccount>();
            _transactions = new Dictionary<string, GncTransaction>();
            _splits = new Dictionary<string, GncSplit>();
            _commodities = new Dictionary<string, GncCommodity>();
            _baseCurrencyId = baseCurrency;
        }

        public GncBook(GncSession session, XElement xml, string baseCurrency)
            : this(session, baseCurrency)
        {
            _guid = xml.ChkElement(GncName.Book("id")).Value;
            foreach (var cmdtyXml in xml.Elements(GncName.Gnc("commodity")))
            {
                GncCommodity cmdty = new GncCommodity(this, cmdtyXml);
                _commodities.Add(cmdty.Identifier, cmdty);
            }
            foreach (var acctXml in xml.Elements(GncName.Gnc("account")))
            {
                GncAccount acct = new GncAccount(this, acctXml);
                _accounts.Add(acct.Guid, acct);
            }
            foreach (var transXml in xml.Elements(GncName.Gnc("transaction")))
            {
                GncTransaction trans = new GncTransaction(this, transXml);
                _transactions.Add(trans.Guid, trans);
                foreach (var split in trans.EnumSplits())
                    _splits.Add(split.Guid, split);
            }

            // Price DB
            {
                var pel = xml.ChkElement(GncName.Gnc("pricedb"));
                foreach (var priceXml in pel.Elements("price"))
                {
                    var cmdty = new GncCommodity(null, priceXml.ChkElement(GncName.Price("commodity")));
                    var ccy = new GncCommodity(null, priceXml.ChkElement(GncName.Price("currency")));
                    DateTime timepoint = GncUtil.ParseGncDate(priceXml.ChkElement(GncName.Price("time")).ChkElement(GncName.Ts("date")).Value);
                    decimal value = priceXml.ChkElement(GncName.Price("value")).Value.ToGncDecimal();
                    string source = priceXml.ChkElement(GncName.Price("source")).Value;

                    if (cmdty.Identifier == _baseCurrencyId)
                    {
                        _commodities[cmdty.Identifier].ExRate[timepoint] = 1m / value;
                    }
                    else if (ccy.Identifier == _baseCurrencyId)
                    {
                        _commodities[cmdty.Identifier].ExRate[timepoint] = value;
                    }
                    else if (source != "user:xfer-dialog")
                    {
                        // Ignore and warn
                        _session.Warn("Ignoring commodity price {0}/{1}, on {3}, source {4}, as it is not linked to the base currency ({2})".Fmt(cmdty.Identifier, ccy.Identifier, _baseCurrencyId, timepoint.ToShortDateString(), source));
                    }
                    // Otherwise just ignore completely
                }
                // Always add 1.0 to the base currency ExRate curve to make it more like the others
                _commodities[_baseCurrencyId].ExRate[new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)] = 1;
            }

            rebuildCacheAccountChildren();
            rebuildCacheAccountSplits();
            rebuildCacheAccountAllBalances();
            verifyBalsnaps();
        }

        internal void rebuildCacheAccountChildren()
        {
            _cacheAccountChildren = new Dictionary<string, List<string>>();
            _accountRoot = null;
            foreach (var acct in _accounts.Values)
            {
                if (acct.ParentGuid == null)
                {
                    if (_accountRoot == null)
                        _accountRoot = acct;
                    else
                        throw new GncException("Multiple root accounts found.");
                }
                else
                {
                    if (!_cacheAccountChildren.ContainsKey(acct.ParentGuid))
                        _cacheAccountChildren.Add(acct.ParentGuid, new List<string>());
                    _cacheAccountChildren[acct.ParentGuid].Add(acct.Guid);
                }
            }
        }

        internal void rebuildCacheAccountSplits()
        {
            _cacheAccountSplits = new Dictionary<string, LinkedList<string>>();

            foreach (var trans in _transactions.Values.Order())
            {
                foreach (var split in trans.EnumSplits())
                {
                    if (!_cacheAccountSplits.ContainsKey(split.AccountGuid))
                        _cacheAccountSplits.Add(split.AccountGuid, new LinkedList<string>());
                    _cacheAccountSplits[split.AccountGuid].AddLast(split.Guid);
                }
            }
        }

        /// <summary>
        /// Rebuilds the account balance cache for every account, forcefully.
        /// </summary>
        internal void rebuildCacheAccountAllBalances()
        {
            foreach (var acctSplitGuids in _cacheAccountSplits.Values)
                rebuildCacheAccountBalance(acctSplitGuids.Last.Value, true);
        }

        /// <summary>
        /// Rebuilds the account balance cache, stored inside the GncSplits, so as
        /// to obtain the balance after the specified split. Only rebuilds as much
        /// as is necessary.
        /// </summary>
        /// <param name="splitGuid">The GUID of the split to be calculated.</param>
        /// <param name="force">If true, previous cached values will be disregarded.</param>
        internal void rebuildCacheAccountBalance(string splitGuid, bool force)
        {
            var splits = _cacheAccountSplits[_splits[splitGuid].AccountGuid];
            LinkedListNode<string> curnode = null;

            // If necessary find the first node which we have a cached value for
            if (!force)
            {
                curnode = splits.Find(splitGuid);
                while (curnode != null && _splits[curnode.Value]._cacheBalance == decimal.MinValue)
                    curnode = curnode.Previous;
            }

            // Compute the balance from there forwards, up to the specified split
            decimal balance = curnode == null ? 0 : _splits[curnode.Value]._cacheBalance;
            if (curnode == null)
                curnode = splits.First;
            else
                curnode = curnode.Next;
            while (true)
            {
                var curGuid = curnode.Value;
                balance += _splits[curGuid].Quantity;
                _splits[curGuid]._cacheBalance = balance;
                if (curGuid == splitGuid)
                    break;
                curnode = curnode.Next;
            }
        }

        /// <summary>
        /// Verifies whether all balsnaps match the actual account balance. Issues warnings about
        /// any mismatch, as well as about balsnaps that cannot be parsed correctly.
        /// </summary>
        internal void verifyBalsnaps()
        {
            foreach (var split in _splits.Values)
            {
                if (!split.IsBalsnap)
                    continue;

                try
                {
                    decimal balsnap = split.Balsnap;
                    if (balsnap != split.AccountBalanceAfter)
                        _session.Warn("Balance snapshot not correct in account \"{0}\", date \"{1}\": snapshot is {2} but actual balance is {3}".Fmt(
                            split.Account.Path(":"), split.Transaction.DatePosted, balsnap, split.AccountBalanceAfter));
                }
                catch (GncBalsnapParseException e)
                {
                    _session.Warn("Could not parse balance snapshot in account \"{0}\", date \"{1}\", value \"{2}\"".Fmt(
                        split.Account.Path(":"), split.Transaction.DatePosted, e.OffendingValue));
                }
            }
        }

        public GncSession Session
        {
            get { return _session; }
        }

        public string Guid
        {
            get { return _guid; }
        }

        public GncAccount GetAccount(string guid)
        {
            if (guid == null)
                return null;
            else
                return _accounts[guid];
        }

        public GncAccount AccountRoot
        {
            get { return _accountRoot; }
        }

        public string BaseCurrencyId
        {
            get { return _baseCurrencyId; }
            set { _baseCurrencyId = value; }
        }

        public GncCommodity BaseCurrency
        {
            get { return GetCommodity(_baseCurrencyId); }
        }

        public IEnumerable<GncAccount> AccountEnumChildren(GncAccount acct)
        {
            if (!_cacheAccountChildren.ContainsKey(acct.Guid))
                yield break;
            foreach (var childGuid in _cacheAccountChildren[acct.Guid])
                yield return _accounts[childGuid];
        }

        public IEnumerable<GncSplit> AccountEnumSplits(GncAccount acct)
        {
            if (!_cacheAccountSplits.ContainsKey(acct.Guid))
                yield break;
            foreach (var splitGuid in _cacheAccountSplits[acct.Guid])
                yield return _splits[splitGuid];
        }

        public IEnumerable<GncCommodity> EnumCommodities()
        {
            foreach (var cmdty in _commodities.Values)
                yield return cmdty;
        }

        public GncCommodity GetCommodity(string identifier)
        {
            return _commodities[identifier];
        }

        public GncAccount GetAccountByPath(string path)
        {
            var cur = _accountRoot;
            var remains = path;
            while (remains != "")
            {
                var index = remains.IndexOf(':');
                var next = index <= 0 ? remains : remains.Substring(0, index);
                remains = index <= 0 ? "" : remains.Substring(next.Length + 1);
                try { cur = cur.EnumChildren().First(acct => acct.Name == next); }
                catch { throw new RTException("Account not found: \"{0}\", while retrieving \"{1}\".".Fmt(next, path)); }
            }
            return cur;
        }

        public GncSplit GetSplit(string guid)
        {
            return _splits[guid];
        }

        public DateTime EarliestDate
        {
            get
            {
                return _transactions.Values.Select(trn => trn.DatePosted).Min();
            }
        }
    }
}
