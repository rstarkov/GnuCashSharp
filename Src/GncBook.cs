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
                    string source = priceXml.ChkElement(GncName.Price("source")).Value;
                    if (priceXml.ChkElement(GncName.Price("value")).Value == "1/0")
                    {
                        _session.Warn("Ignoring commodity price {0}/{1}, on {2}, source {3}, because the price is 1/0 (bug in GnuCash)".Fmt(cmdty.Identifier, ccy.Identifier, timepoint.ToShortDateString(), source));
                        continue;
                    }
                    decimal value = priceXml.ChkElement(GncName.Price("value")).Value.ToGncDecimal();

                    if (!_commodities.ContainsKey(cmdty.Identifier))
                        _commodities.Add(cmdty.Identifier, new GncCommodity(this, identifier: cmdty.Identifier, name: cmdty.Identifier));

                    if (cmdty.Identifier == _baseCurrencyId)
                    {
                        _commodities[ccy.Identifier].ExRate[timepoint] = 1m / value;
                    }
                    else if (ccy.Identifier == _baseCurrencyId)
                    {
                        _commodities[cmdty.Identifier].ExRate[timepoint] = value;
                    }
                    else if (source != "user:xfer-dialog")
                    {
                        // Ignore and warn
                        //_session.Warn("Ignoring commodity price {0}/{1}, on {3}, source {4}, as it is not linked to the base currency ({2})".Fmt(cmdty.Identifier, ccy.Identifier, _baseCurrencyId, timepoint.ToShortDateString(), source));
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
            verifyCurrencies();
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
            foreach (var list in _cacheAccountChildren.Values)
                list.Sort((x, y) => _accounts[x].Name.CompareTo(_accounts[y].Name));
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
        private void verifyBalsnaps()
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

        /// <summary>
        /// Warn about certain types of incorrectly recorded transactions.
        /// </summary>
        private void verifyCurrencies()
        {
            // Report vast deviations of the exchange rate, especially where it is close to the inverse
            var anySuspiciousExchangeRates = false;
            foreach (var cmdty in _commodities.Values)
            {
                var suspicious = from p in cmdty.ExRate.ConsecutivePairs(false)
                                 let r1 = p.Item1
                                 let r2 = p.Item2
                                 let ratio = Ut.OnException(() => Math.Max(r1.Value, 1 / r2.Value) / Math.Min(r1.Value, 1 / r2.Value), (DivideByZeroException e) => 0m)
                                 where ratio < 1.1m
                                 select new { r1, r2 };
                var surrounded = suspicious.ConsecutivePairs(false)
                    .Where(x => x.Item1.r2.Key == x.Item2.r1.Key && x.Item1.r2.Value == x.Item2.r1.Value)
                    .Select(x => new { main = x.Item1.r2, before = x.Item1.r1, after = x.Item2.r2 })
                    .ToList();
                var dates = surrounded.Select(x => x.main.Key).ToHashSet();
                var warnings = surrounded.Select(x => new
                {
                    date = x.main.Key,
                    msg = "Suspicious exchange rate for {0}: {1:0.00##} on {2}, but surrounded by {3:0.00##} and {4:0.00##}, close to the inverse.".Fmt(cmdty.Name, x.main.Value, x.main.Key, x.before.Value, x.after.Value)
                }).ToList();
                foreach (var sus in suspicious)
                {
                    var match1 = !dates.Contains(sus.r1.Key) ? null : surrounded.Where(s => s.main.Key == sus.r1.Key && s.main.Value == sus.r1.Value).SingleOrDefault();
                    if (match1 != null)
                        continue;
                    var match2 = !dates.Contains(sus.r2.Key) ? null : surrounded.Where(s => s.main.Key == sus.r2.Key && s.main.Value == sus.r2.Value).SingleOrDefault();
                    if (match2 != null)
                        continue;
                    warnings.Add(new { date = sus.r1.Key, msg = "Suspicious exchange rate for {0}: {1:0.00##} on {2}, {3:0.00##} on {4}, which is close to the inverse.".Fmt(cmdty.Name, sus.r1.Value, sus.r1.Key, sus.r2.Value, sus.r2.Key) });
                }
                foreach (var w in warnings.OrderBy(ww => ww.date))
                    _session.Warn(w.msg);
                anySuspiciousExchangeRates |= warnings.Count > 0;
            }


            foreach (var trn in _transactions.Values)
            {
                var getAccounts = Ut.Lambda(() => trn.EnumSplits().Select(s => s.Account.Path(":")).Order().JoinString(", ", "\"", "\"", " and "));
                var getTrnDesc = Ut.Lambda(() => "date {0}, descr \"{1}\", accounts {2}, guid {3}".Fmt(trn.DatePosted.ToLocalTime().ToShortDateString(), trn.Description, getAccounts(), trn.Guid));
                var getSplitDesc = Ut.Lambda((GncSplit split) => "date {0}, descr \"{1}\", account {2}, guid {3}".Fmt(
                    trn.DatePosted.ToLocalTime().ToShortDateString(), trn.Description + (string.IsNullOrWhiteSpace(split.Memo) ? "" : (" / " + split.Memo)), split.Account.Path(":"), split.Guid));

                // The total value in the transaction currency must add up to zero
                if (trn.EnumSplits().Sum(s => s.Value) != 0)
                {
                    _session.Warn("Transaction with unbalanced split values in the transaction currency (bug in GnuCash?): accounts {0}, date \"{1}\", guid \"{2}\"".Fmt(
                        getAccounts(), trn.DatePosted, trn.Guid));
                    continue;
                }

                // If all accounts are in the same currency, the total must add up to zero
                if (trn.EnumSplits().Select(s => s.Commodity).Distinct().Count() == 1)
                {
                    var leastPrecise = trn.EnumSplits().MinElement(s => s.Account.CommodityScu).Account;
                    if (trn.EnumSplits().Sum(s => leastPrecise.RoundQuantity(s.Quantity)) != 0)
                    {
                        _session.Warn("Single-currency transaction with unbalanced splits (bug in GnuCash?): accounts {0}, date \"{1}\", guid \"{2}\"".Fmt(
                            getAccounts(), trn.DatePosted, trn.Guid));
                        continue;
                    }
                }

                // The transaction currency must be equal to at least one account's currency (although why the transaction even has a currency is a very good question)
                if (trn.EnumSplits().All(s => s.Commodity != trn.Commodity))
                {
                    _session.Warn("The transaction currency ({0}) is not the currency of any of the splits' destination accounts: {1}, date \"{2}\", guid \"{3}\"".Fmt(
                        trn.Commodity.Identifier, getAccounts(), trn.DatePosted, trn.Guid));
                    continue;
                }

                // If the split and transaction currencies are the same, the value in both had better be the same too
                foreach (var split in trn.EnumSplits())
                {
                    if (trn.Commodity == split.Commodity && !split.Account.RoundedEquals(split.Quantity, split.Value))
                    {
                        _session.Warn("Split in the transaction currency ({4}) whose transaction-currency-value ({5}, rounded) is different to split-currency-value ({6}) (bug in GnuCash?): account {0}, date \"{1}\", value \"{2}\", guid \"{3}\"".Fmt(
                            split.Account.Path(":"), trn.DatePosted, split.Quantity, trn.Guid, split.Commodity.Name, split.Account.RoundQuantity(split.Value), split.Quantity));
                        continue;
                    }
                }

                // Various exchange rate checks
                foreach (var split in trn.EnumSplits())
                {
                    if ((split.Value == 0) != (split.Quantity == 0))
                    {
                        if (split.Value >= 1 || split.Quantity >= 1)
                            _session.Warn("Split with a zero or infinite exchange rate: " + getSplitDesc(split));
                        continue;
                    }
                    if (split.Value == 0)
                        continue;
                    if (trn.Commodity != split.Commodity)
                    {
                        if (Math.Sign(split.Value) != Math.Sign(split.Quantity))
                        {
                            _session.Warn("Split with a negative exchange rate: " + getSplitDesc(split));
                            continue;
                        }
                        if (anySuspiciousExchangeRates)
                            continue;
                        var expectedExRateFrom = trn.Commodity.IsBaseCurrency ? 1m : trn.Commodity.ExRate.Get(trn.DatePosted.ToUniversalTime(), GncInterpolation.Linear);
                        var expectedExRateTo = split.Commodity.IsBaseCurrency ? 1m : split.Commodity.ExRate.Get(trn.DatePosted.ToUniversalTime(), GncInterpolation.Linear);
                        var expectedExRate = expectedExRateTo / expectedExRateFrom;
                        var actualExRate = split.Value / split.Quantity;
                        if ((expectedExRate < 1) != (actualExRate < 1) && Math.Max(expectedExRate, 1 / expectedExRate) > 1.1m)
                        {
                            _session.Warn($"Split with a suspicious exchange rate: expected {expectedExRate:0.00##} {trn.Commodity}/{split.Commodity}, actual {actualExRate:0.00##}, " + getSplitDesc(split));
                            continue;
                        }
                        // Too sensitive, but worth enabling with support for some sort of an "ignore" feature.
                        //if (Math.Max(expectedExRate, actualExRate) / Math.Min(expectedExRate, actualExRate) > 1.5m)
                        //{
                        //    _session.Warn("Split with an exchange rate that differs significantly from expected: expected \"{0:0.00##}\", actual \"{1:0.00##}\", {2}".Fmt(
                        //        expectedExRate, actualExRate, getSplitDesc(split)));
                        //    continue;
                        //}
                    }
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
            return identifier == null ? null : _commodities[identifier];
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

        public DateTime LatestDate
        {
            get
            {
                return _transactions.Values.Select(trn => trn.DatePosted).Max();
            }
        }
    }
}
