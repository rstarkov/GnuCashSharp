using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
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

        private Dictionary<string, List<string>> _cacheAccountSplits;
        private Dictionary<string, List<string>> _cacheAccountChildren;

        public GncBook(GncSession session)
        {
            _session = session;
            _guid = null;
            _accountRoot = null;
            _accounts = new Dictionary<string, GncAccount>();
            _transactions = new Dictionary<string, GncTransaction>();
            _splits = new Dictionary<string, GncSplit>();
            _commodities = new Dictionary<string, GncCommodity>();
        }

        public GncBook(GncSession session, XElement xml)
            : this(session)
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
            RebuildCacheAccountChildren();
            RebuildCacheAccountSplits();
        }

        internal void RebuildCacheAccountChildren()
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

        internal void RebuildCacheAccountSplits()
        {
            _cacheAccountSplits = new Dictionary<string, List<string>>();
            foreach (var trans in _transactions.Values)
            {
                foreach (var split in trans.EnumSplits())
                {
                    if (!_cacheAccountSplits.ContainsKey(split.AccountGuid))
                        _cacheAccountSplits.Add(split.AccountGuid, new List<string>());
                    _cacheAccountSplits[split.AccountGuid].Add(split.Guid);
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
            return _accounts[guid];
        }

        public GncAccount AccountRoot
        {
            get { return _accountRoot; }
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

    }
}
