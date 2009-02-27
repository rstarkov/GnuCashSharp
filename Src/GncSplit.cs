using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
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
            _value = xml.ChkElement(GncName.Split("value")).Value.ToGncDecimal();
            _quantity = xml.ChkElement(GncName.Split("quantity")).Value.ToGncDecimal();
            _accountGuid = xml.ChkElement(GncName.Split("account")).Value;
            _memo = xml.ValueOrDefault(GncName.Split("memo"), (string)null);
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
        /// Amount of change in the destination account's currency.
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
            get { return _transaction.Book.GetCommodity(Account.Commodity); }
        }

        public string Memo
        {
            get { return _memo; }
        }

        public GncAmount Amount
        {
            get { return new GncAmount(_quantity, Commodity, _transaction.DatePosted.ToUniversalTime()); }
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
    }
}
