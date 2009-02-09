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
            var tmpXml = xml.Element(GncName.Act("commodity"));
            if (tmpXml != null)
                _commodity = tmpXml.ChkElement(GncName.Cmdty("space")).Value + ":" + tmpXml.ChkElement(GncName.Cmdty("id")).Value;
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
    }
}
