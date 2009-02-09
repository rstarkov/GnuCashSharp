using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp
{
    public class GncCommodity
    {
        private GncBook _book;
        private string _identifier;
        private string _space;
        private string _name;

        public GncCommodity(GncBook book)
        {
            _book = book;
            _identifier = null;
        }

        public GncCommodity(GncBook book, XElement xml)
            : this(book)
        {
            _space = xml.ChkElement(GncName.Cmdty("space")).Value;
            _name = xml.ChkElement(GncName.Cmdty("id")).Value;
            _identifier = _space + ":" + _name;
        }

        public GncBook Book
        {
            get { return Book; }
        }

        public string Identifier
        {
            get { return _identifier; }
        }

        public string Space
        {
            get { return _space; }
        }

        public string Name
        {
            get { return _name; }
        }
    }
}
