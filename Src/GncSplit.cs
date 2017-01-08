using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp
{
    public class GncSplit
    {
        private GncTransaction _transaction;
        private string _guid;
        private GncReconciled _reconciled;
        private decimal _value;
        private decimal _quantity;
        private string _accountGuid;
        private string _memo;
        internal decimal _cacheBalance = decimal.MinValue;

        public GncSplit(GncTransaction transaction)
        {
            _transaction = transaction;
            _guid = null;
            _reconciled = GncReconciled.UNKNOWN;
            _value = 0;
            _quantity = 0;
            _accountGuid = null;
            _memo = null;
        }

        public GncSplit(GncTransaction transaction, XElement xml)
            : this(transaction)
        {
            _guid = xml.ChkElement(GncName.Split("id")).Value;
            //_reconciled = xml.ChkElement(GncName.Split("reconciled-state")).Value;
            _accountGuid = xml.ChkElement(GncName.Split("account")).Value;
            _memo = xml.ValueOrDefault(GncName.Split("memo"), (string) null);
            try
            {
                _value = xml.ChkElement(GncName.Split("value")).Value.ToGncDecimal();
                _quantity = xml.ChkElement(GncName.Split("quantity")).Value.ToGncDecimal();
            }
            catch (Exception e)
            {
                throw new Exception($"Could not read quantity for split {_guid}, account {_accountGuid}: {e.Message}", e);
            }
        }

        public GncTransaction Transaction
        {
            get { return _transaction; }
        }

        public string Guid
        {
            get { return _guid; }
        }

        public GncReconciled Reconciled
        {
            get { return _reconciled; }
        }

        /// <summary>
        /// Amount of change in transaction's currency.
        /// </summary>
        public decimal Value
        {
            get { return _value; }
        }

        /// <summary>
        /// Amount of change in the destination account's currency (i.e. in split's currency).
        /// </summary>
        public decimal Quantity
        {
            get { return _quantity; }
        }

        public string AccountGuid
        {
            get { return _accountGuid; }
        }

        public GncAccount Account
        {
            get { return _transaction.Book.GetAccount(_accountGuid); }
        }

        public GncCommodity Commodity
        {
            get { return Account.Commodity; }
        }

        public string Memo
        {
            get { return _memo; }
        }

        public GncAmount Amount
        {
            get { return new GncAmount(_quantity, Commodity, _transaction.DatePosted.ToUniversalTime()); }
        }

        public decimal AccountBalanceAfter
        {
            get
            {
                if (_cacheBalance == decimal.MinValue)
                    _transaction.Book.rebuildCacheAccountBalance(_guid, false);
                return _cacheBalance;
            }
        }

        public string ReadableDescAndMemo
        {
            get
            {
                if (_transaction.Description == null && _memo == null)
                    return "<???>";
                else if (_transaction.Description == null || _transaction.Description == _memo)
                    return _memo;
                else if (_memo == null)
                    return _transaction.Description;
                else
                    return "{0} (({1}))".Fmt(_memo, _transaction.Description);
            }
        }

        /// <summary>
        /// Returns true if this split represents a balance snapshot.
        /// </summary>
        public bool IsBalsnap
        {
            get
            {
                return Memo == null &&
                    _transaction.Description != null &&
                    _transaction.Description.StartsWith(_transaction.Book.Session.BalsnapPrefix);
            }
        }

        /// <summary>
        /// Gets the balance snapshot value represented by this split. This should only
        /// be called if IsBalsnap returns true, otherwise an <see cref="InvalidOperationException"/>
        /// will be thrown. If the value cannot be parsed a <see cref="GncBalsnapParseException"/>
        /// will be thrown.
        /// </summary>
        public decimal Balsnap
        {
            get
            {
                if (!IsBalsnap)
                    throw new InvalidOperationException("Cannot get Balance Snapshot value because this transaction is not a balance snapshot.");

                decimal result;
                string value = Regex.Replace(_transaction.Description.Substring(_transaction.Book.Session.BalsnapPrefix.Length), @" |(\(.*\))", "");
                if (decimal.TryParse(value, out result))
                    return result;
                else
                    throw new GncBalsnapParseException(this, value);
            }
        }

        /// <summary>
        /// Converts this split's amount to the target currency. Uses the implicit transaction
        /// exchange rate if it can be inferred from the splits of the transaction.
        /// Otherwise uses <see cref="GncAmount.ConvertTo()"/>.
        /// </summary>
        public GncAmount ConvertAmount(GncCommodity toCommodity)
        {
            return Transaction.ConvertAmount(Amount, toCommodity);
        }
    }

    /// <summary>
    /// Thrown by <see cref="GncSplit.Balsnap"/> when the split is a balance snapshot but
    /// the snapshotted value cannot be parsed.
    /// </summary>
    public class GncBalsnapParseException : RTException
    {
        public GncBalsnapParseException(GncSplit split, string offendingValue)
        {
            Split = split;
            OffendingValue = offendingValue;
        }

        public GncSplit Split { get; private set; }
        public string OffendingValue { get; private set; }
    }
}
