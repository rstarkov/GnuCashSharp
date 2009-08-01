using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;
using RT.Util;

namespace GnuCashSharp
{
    public class GncTransaction: IComparable<GncTransaction>
    {
        private GncBook _book;
        private string _guid;
        private DateTime _datePosted;
        private DateTimeOffset _dateEntered;
        private string _num;
        private string _description;
        private Dictionary<string, GncSplit> _splits;

        public GncTransaction(GncBook book)
        {
            _book = book;
            _guid = null;
            _datePosted = new DateTime();
            _dateEntered = new DateTimeOffset();
            _num = "";
            _description = null;
            _splits = new Dictionary<string, GncSplit>();
        }

        public GncTransaction(GncBook book, XElement xml)
            : this(book)
        {
            _guid = xml.ChkElement(GncName.Trn("id")).Value;
            _datePosted = DateTimeOffset.Parse(xml.ChkElement(GncName.Trn("date-posted")).ChkElement(GncName.Ts("date")).Value).Date;
            _dateEntered = DateTimeOffset.Parse(xml.ChkElement(GncName.Trn("date-entered")).ChkElement(GncName.Ts("date")).Value);
            _num = xml.ValueOrDefault(GncName.Trn("num"), "");
            _description = xml.ChkElement(GncName.Trn("description")).Value;
            foreach (var el in xml.ChkElement(GncName.Trn("splits")).Elements(GncName.Trn("split")))
            {
                GncSplit split = new GncSplit(this, el);
                _splits.Add(split.Guid, split);
            }
        }

        public GncBook Book
        {
            get { return _book; }
        }

        public string Guid
        {
            get { return _guid; }
        }

        public DateTime DatePosted
        {
            get { return _datePosted; }
        }

        public DateTimeOffset DateEntered
        {
            get { return _dateEntered; }
        }

        public string Num
        {
            get { return _num; }
        }

        public string Description
        {
            get { return _description; }
        }

        public GncSplit GetSplit(string guid)
        {
            return _splits[guid];
        }

        public IEnumerable<GncSplit> EnumSplits()
        {
            foreach (var split in _splits.Values)
                yield return split;
        }

        public int CompareTo(GncTransaction other)
        {
            int res = this._datePosted.CompareTo(other._datePosted);
            if (res != 0)
                return res;
            int thisNum, otherNum;
            if (int.TryParse(this._num, out thisNum) && int.TryParse(other._num, out otherNum))
                res = thisNum.CompareTo(otherNum);
            else
                res = this._num.CompareTo(other._num);
            if (res != 0)
                return res;
            res = this._dateEntered.CompareTo(other._dateEntered);
            return res;
        }
    }
}
